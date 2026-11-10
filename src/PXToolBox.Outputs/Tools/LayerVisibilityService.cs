using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Naming;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Alternar visibilidade MA, FE e C". Ported de Montagem.AlternarMA / AlternarFE / AlternarC
    /// (revisão candidata em Referência/Macros/modulos/Montagem.bas) — encontrada depois de uma
    /// busca inicial não ter achado nenhuma referência (Layouts.bas, que tem
    /// Oculta/MostraVendedores, é sobre um recurso diferente e obsoleto; ver docs/migrated-tools.md).
    ///
    /// Correção deliberada em relação à macro candidata: ela compara os ÚLTIMOS 2 CARACTERES do
    /// nome da camada contra "MA"/"FE" sem exigir um ponto antes (só o caso ".C" é passado com o
    /// ponto incluído) — isso deixaria "MA"/"FE" vulneráveis a corresponder uma camada como
    /// "FORMA" (termina em "MA") por acidente, exatamente o que o escopo desta entrega pediu
    /// para evitar. Esta implementação usa <see cref="LayerName.Category"/>, que exige que o
    /// ÚLTIMO SEGMENTO PONTUADO INTEIRO seja igual a MA/FE/C, para as três categorias igualmente.
    ///
    /// Esta ferramenta é a base do caso crítico "Grid com camadas ocultas": oculta-se FE e C,
    /// executa-se Preparar Grid, depois mostra-se FE e C de novo. Ver GridPreparationService.
    /// </summary>
    public sealed class LayerVisibilityService
    {
        public OperationResult<int> SetVisibility(ICorelPage page, LayerCategory category, bool visible)
        {
            var affected = 0;
            foreach (var layer in page.Layers)
            {
                if (new LayerName(layer.Name).Category != category)
                {
                    continue;
                }

                layer.Visible = visible;
                affected++;
            }

            var state = visible ? "visível" : "oculta";
            return OperationResult<int>.Success(affected, $"{affected} camada(s) \"{LayerName.SuffixFor(category)}\" agora {state}.");
        }

        /// <summary>
        /// Alterna: se QUALQUER camada da categoria estiver oculta, mostra todas; caso contrário,
        /// oculta todas. Retorna o novo estado (true = visível) junto com a contagem afetada.
        /// </summary>
        public OperationResult<(bool NewVisibility, int AffectedCount)> Toggle(ICorelPage page, LayerCategory category)
        {
            var matching = page.Layers.Where(l => new LayerName(l.Name).Category == category).ToList();
            if (matching.Count == 0)
            {
                return OperationResult<(bool, int)>.Fail(
                    OperationStatus.InconclusiveRead,
                    $"Nenhuma camada \"{LayerName.SuffixFor(category)}\" encontrada na página.");
            }

            var newVisibility = matching.Any(l => !l.Visible);
            foreach (var layer in matching)
            {
                layer.Visible = newVisibility;
            }

            return OperationResult<(bool, int)>.Success((newVisibility, matching.Count));
        }

        /// <summary>
        /// Registro SÓ EM MEMÓRIA da última ocultação EFETIVA (pelo menos uma camada mudou de
        /// visível para oculta). Nada é escrito no documento (nenhuma Properties): a identidade
        /// do documento e das camadas é o objeto da sessão (<see cref="ICorelDocument.Identity"/>/
        /// <see cref="ICorelLayer.Identity"/>), comparado só com ReferenceEquals e nunca invocado.
        /// Perdido ao fechar o Corel/recarregar o painel; invalidado ao fechar o documento de
        /// origem — reabrir o arquivo cria outro objeto e nunca recupera a operação.
        /// </summary>
        public LastHiddenLayersRecord? LastHidden { get; private set; }

        /// <summary>Por que não há registro agora (para a dica do botão), ou null se nunca houve.</summary>
        public string? UnavailableReason { get; private set; }

        /// <summary>
        /// Oculta cada camada que contém pelo menos um objeto selecionado (cada camada uma vez só,
        /// mesmo com vários objetos nela). Camadas especiais do Corel (Guias, Área de trabalho,
        /// Grade) nunca são ocultadas — afetariam todas as páginas, não só a seleção — e são
        /// informadas à parte na mensagem. Camadas já ocultas não são tocadas nem registradas.
        ///
        /// Só uma operação que oculte de fato pelo menos uma camada substitui
        /// <see cref="LastHidden"/>; falhas e tentativas sem alteração preservam o registro.
        /// Antes de ocultar, confere (só leitura) se o documento e cada camada são encontrados
        /// exatamente uma vez pela identidade de sessão; se não, oculta mesmo assim mas não
        /// registra, dizendo que a restauração ficou indisponível — nunca registra algo que
        /// depois teria de ser achado por aproximação.
        /// </summary>
        public OperationResult HideLayersOfSelection(
            ICorelDocument document, ICorelSelection selection, IReadOnlyList<ICorelDocument> openDocuments)
        {
            if (selection.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Nenhum objeto selecionado.");
            }

            var toHide = new List<ICorelLayer>();
            var alreadyHidden = new List<string>();
            var seen = new HashSet<ICorelLayer>();
            var specialCount = 0;
            foreach (var shape in selection.Shapes)
            {
                var layer = shape.Layer;
                if (layer == null || !seen.Add(layer))
                {
                    continue;
                }

                if (layer.IsSpecialLayer)
                {
                    specialCount++;
                    continue;
                }

                if (!layer.Visible)
                {
                    alreadyHidden.Add(layer.Name);
                    continue;
                }

                toHide.Add(layer);
            }

            var notes = (specialCount > 0 ? $" {specialCount} camada(s) especial(is) do Corel ignorada(s)." : "")
                        + (alreadyHidden.Count > 0 ? $" Já oculta(s), não registrada(s): {string.Join(", ", alreadyHidden)}." : "");
            if (toHide.Count == 0)
            {
                return OperationResult.Fail(
                    OperationStatus.InvalidSelection,
                    $"Nenhuma camada visível a ocultar para os objetos selecionados.{notes}");
            }

            var identifiable = CountMatches(openDocuments, document.Identity) == 1;
            if (identifiable)
            {
                var allLayers = document.AllLayers;
                identifiable = toHide.All(layer => CountMatches(allLayers, layer.Identity) == 1);
            }

            var entries = new List<HiddenLayerEntry>();
            try
            {
                foreach (var layer in toHide)
                {
                    layer.Visible = false;
                    entries.Add(new HiddenLayerEntry(layer.Identity, layer.Name));
                }
            }
            finally
            {
                // Mesmo se uma camada falhar no meio, o registro reflete exatamente as que
                // chegaram a ser ocultadas. Um registro anterior não sobrevive a uma ocultação
                // efetiva nova: "últimas" passaria a apontar para a operação errada.
                if (entries.Count > 0)
                {
                    if (identifiable)
                    {
                        LastHidden = new LastHiddenLayersRecord(document.Identity, document.Name, entries);
                        UnavailableReason = null;
                    }
                    else
                    {
                        Discard(UnidentifiableReason);
                    }
                }
            }

            var names = string.Join(", ", entries.Select(e => e.LayerName));
            var restoreNote = identifiable ? "" : $" {UnidentifiableReason}";
            return OperationResult.Success($"{entries.Count} camada(s) oculta(s): {names}.{notes}{restoreNote}");
        }

        private const string UnidentifiableReason =
            "Restauração indisponível: o Corel não forneceu uma identidade única para o documento/camadas nesta sessão (use Ctrl+Z).";

        /// <summary>
        /// Descarta o registro se o documento fechado for o de origem. Compara só identidade
        /// (ReferenceEquals) — nunca acessa o documento em fechamento.
        /// </summary>
        public void OnDocumentClosed(object closedDocumentIdentity)
        {
            if (LastHidden != null && IsSameSessionObject(LastHidden.DocumentIdentity, closedDocumentIdentity))
            {
                Discard($"Indisponível: o documento de origem \"{LastHidden.DocumentName}\" foi fechado.");
            }
        }

        /// <summary>
        /// Descarta o registro se o documento de origem não está mais entre os abertos (rede de
        /// segurança caso o evento de fechamento não chegue). Só lê a lista de documentos vivos.
        /// </summary>
        public void DiscardIfSourceClosed(IReadOnlyList<ICorelDocument> openDocuments)
        {
            if (LastHidden != null && CountMatches(openDocuments, LastHidden.DocumentIdentity) == 0)
            {
                Discard($"Indisponível: o documento de origem \"{LastHidden.DocumentName}\" foi fechado.");
            }
        }

        /// <summary>
        /// Mostra de novo SOMENTE as camadas registradas pela última ocultação efetiva, e só no
        /// documento de origem, que precisa ser o ativo (o passo de desfazer fica nele). Toda a
        /// localização é por identidade de sessão entre os objetos vivos — nunca por nome,
        /// caminho, índice ou marcação no arquivo. Qualquer identidade ambígua (achada mais de
        /// uma vez) invalida a restauração inteira sem alterar nada. Camadas não encontradas
        /// (excluídas, ou recriadas por um Desfazer) são informadas; as demais são restauradas.
        /// Camadas especiais nunca são alteradas.
        /// </summary>
        public OperationResult RestoreLastHiddenLayers(ICorelDocument? activeDocument, IReadOnlyList<ICorelDocument> openDocuments)
        {
            var record = LastHidden;
            if (record == null)
            {
                return OperationResult.Fail(
                    OperationStatus.InvalidSelection,
                    UnavailableReason ?? "Nenhuma ocultação registrada nesta sessão.");
            }

            var sourceMatches = CountMatches(openDocuments, record.DocumentIdentity);
            if (sourceMatches == 0)
            {
                var reason = $"Documento de origem \"{record.DocumentName}\" foi fechado — registro descartado; reabrir o arquivo não recupera a operação.";
                Discard(reason);
                return OperationResult.Fail(OperationStatus.NoActiveDocument, reason);
            }

            if (sourceMatches > 1)
            {
                return Invalidate("o documento de origem foi encontrado mais de uma vez entre os abertos");
            }

            if (activeDocument == null || !IsSameSessionObject(activeDocument.Identity, record.DocumentIdentity))
            {
                return OperationResult.Fail(
                    OperationStatus.InvalidSelection,
                    $"Ative o documento de origem \"{record.DocumentName}\" para restaurar — nada foi alterado.");
            }

            var allLayers = activeDocument.AllLayers;
            var found = new List<(HiddenLayerEntry Entry, ICorelLayer? Layer)>();
            foreach (var entry in record.Entries)
            {
                var matches = allLayers.Where(l => IsSameSessionObject(l.Identity, entry.LayerIdentity)).ToList();
                if (matches.Count > 1)
                {
                    return Invalidate($"a camada \"{entry.LayerName}\" foi encontrada mais de uma vez");
                }

                found.Add((entry, matches.FirstOrDefault()));
            }

            var restored = new List<string>();
            var missing = new List<string>();
            var alreadyVisible = new List<string>();
            var special = new List<string>();
            foreach (var (entry, layer) in found)
            {
                if (layer == null)
                {
                    missing.Add(entry.LayerName);
                }
                else if (layer.IsSpecialLayer)
                {
                    special.Add(layer.Name);
                }
                else if (layer.Visible)
                {
                    alreadyVisible.Add(layer.Name);
                }
                else
                {
                    layer.Visible = true;
                    restored.Add(layer.Name);
                }
            }

            Discard("Indisponível: a última ocultação já foi restaurada.");

            var message = $"{restored.Count} camada(s) restaurada(s)"
                          + (restored.Count > 0 ? $": {string.Join(", ", restored)}." : ".")
                          + (missing.Count > 0 ? $" Não encontrada(s) (excluída(s) ou recriada(s) por Desfazer): {string.Join(", ", missing)}." : "")
                          + (alreadyVisible.Count > 0 ? $" Já visível(is): {string.Join(", ", alreadyVisible)}." : "")
                          + (special.Count > 0 ? $" Camada(s) especial(is) não alterada(s): {string.Join(", ", special)}." : "");
            return restored.Count > 0
                ? OperationResult.Success(message)
                : OperationResult.Fail(OperationStatus.InconclusiveRead, message);
        }

        private OperationResult Invalidate(string why)
        {
            var reason = $"Restauração invalidada: {why} — sem identificação confiável, nada foi alterado. Use Ctrl+Z ou o Gerenciador de Objetos.";
            Discard(reason);
            return OperationResult.Fail(OperationStatus.InconclusiveRead, reason);
        }

        /// <summary>
        /// Solta o registro (ex.: painel encerrado). Só larga as referências — nunca chama
        /// ReleaseComObject: o RCW é o mesmo que o host usa para essa camada/documento, e
        /// liberá-lo manualmente quebraria qualquer outro uso dele no add-on.
        /// </summary>
        public void ReleaseSessionState(string reason) => Discard(reason);

        private void Discard(string reason)
        {
            LastHidden = null;
            UnavailableReason = reason;
        }

        private static int CountMatches(IEnumerable<ICorelDocument> documents, object identity) =>
            CountIdentityMatches(documents.Select(d => d.Identity), identity);

        private static int CountMatches(IEnumerable<ICorelLayer> layers, object identity) =>
            CountIdentityMatches(layers.Select(l => l.Identity), identity);

        /// <summary>
        /// O único critério de identidade da restauração: o MESMO objeto da sessão
        /// (ReferenceEquals). Público para o diagnóstico DEV no host usar exatamente esta
        /// comparação.
        /// </summary>
        public static bool IsSameSessionObject(object a, object b) => ReferenceEquals(a, b);

        public static int CountIdentityMatches(IEnumerable<object> identities, object identity) =>
            identities.Count(candidate => IsSameSessionObject(candidate, identity));
    }

    public sealed class HiddenLayerEntry
    {
        internal HiddenLayerEntry(object layerIdentity, string layerName)
        {
            LayerIdentity = layerIdentity;
            LayerName = layerName;
        }

        /// <summary>Objeto da sessão — só para ReferenceEquals, nunca invocado.</summary>
        internal object LayerIdentity { get; }

        /// <summary>Nome no momento da ocultação — só para mensagens, nunca para localizar.</summary>
        public string LayerName { get; }
    }

    public sealed class LastHiddenLayersRecord
    {
        internal LastHiddenLayersRecord(object documentIdentity, string documentName, IReadOnlyList<HiddenLayerEntry> entries)
        {
            DocumentIdentity = documentIdentity;
            DocumentName = documentName;
            Entries = entries;
        }

        /// <summary>Objeto da sessão — só para ReferenceEquals, nunca invocado.</summary>
        internal object DocumentIdentity { get; }

        /// <summary>Nome no momento da ocultação — só para mensagens, nunca para localizar.</summary>
        public string DocumentName { get; }

        public IReadOnlyList<HiddenLayerEntry> Entries { get; }
    }
}
