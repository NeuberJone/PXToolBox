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
    }
}
