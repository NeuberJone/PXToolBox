using System.Linq;
using PXToolBox.Core.Naming;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// Adiciona/remove os códigos NO, NU, AP e TS dos objetos selecionados.
    /// Ported de Utilidades.CodigoNome / CodigoNumero / CodigoApelido / CodigoTipoSanguineo
    /// (CorelMacros/Utilidades.bas).
    ///
    /// Comportamento original preservado deliberadamente, incluindo a particularidade de que a
    /// verificação "é texto?" olha para QUALQUER forma da seleção, mas a alternância de nome é
    /// aplicada a TODAS as formas selecionadas (mesmo não-texto) quando a seleção contém pelo
    /// menos um texto. Isso não é um bug introduzido pela migração — é o comportamento validado
    /// em produção; documentar antes de "corrigir" (ver docs/migrated-tools.md).
    /// </summary>
    public sealed class CodeToggleService
    {
        public OperationResult Toggle(ICorelSelection selection, ObjectCode code)
        {
            if (selection.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Nenhum objeto selecionado.");
            }

            var hasTextShape = selection.Shapes.Any(s => s.Kind == ShapeKind.Text);
            if (!hasTextShape)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "O objeto selecionado não é texto.");
            }

            foreach (var shape in selection.Shapes)
            {
                shape.Name = code.Toggle(shape.Name);
            }

            return OperationResult.Success($"Código {code.ShortCode} alternado em {selection.Count} objeto(s).");
        }

        /// <summary>
        /// Ported de Correcoes.RemoveCodigosNoNu / RenameObjects (CorelMacros/Correcoes.bas;
        /// confirmado como entrada de barra preservada em
        /// Referência/Macros/relatorios/BARRA_PRIORITARIAS.md). Ferramenta DIFERENTE de
        /// <see cref="Toggle"/>: não opera na seleção atual, mas em TODOS os objetos da página
        /// (descendo recursivamente em grupos e PowerClips — ver nota abaixo), expandindo
        /// "NO" → "TextoNome" e "NU" → "TextoNumero" (sem espaço — convenção própria desta
        /// rotina, diferente de "Texto Nome"/"Texto Número" usados por <see cref="Toggle"/>;
        /// preservado tal como está, não é inconsistência a corrigir). Esta rotina nunca expande
        /// AP/TS — a macro original só trata NO e NU (ver docs/migrated-tools.md, "Lacunas
        /// funcionais").
        ///
        /// Descida em PowerClip adicionada numa rodada posterior, a pedido do usuário — NÃO é
        /// uma restauração de comportamento original: a macro original
        /// (CorelMacros/Correcoes.bas, Sub RenameObjects) só verificava
        /// <c>s.Type = cdrGroupShape</c>, nunca <c>cdrPowerClipShape</c> — confirmado lendo o
        /// `.bas` real. Esse mesmo arquivo tem OUTRA macro
        /// (AjustarLarguraObjetosRecursivo) que já tratava os dois tipos iguais; RemoveCodigosNoNu
        /// nunca teve esse tratamento, nem antes nem depois da migração — é uma lacuna de sempre,
        /// não introduzida por esta migração. Confirmado por reflexão sobre
        /// Corel.Interop.VGCore.dll que, na API atual, Shape.Shapes e Shape.PowerClip.Shapes são
        /// coleções de tipos .NET diferentes (não necessariamente o mesmo objeto COM) — por isso
        /// a descida abaixo verifica as duas, nunca presume que uma inclui a outra.
        /// </summary>
        public OperationResult ExpandNoAndNuCodesRecursively(ICorelPage page)
        {
            var renamed = RenameOnPage(page, new[] { ObjectCode.No, ObjectCode.Nu });
            return OperationResult.Success($"Código NO/NU removido do nome de {renamed} objeto(s).");
        }

        /// <summary>
        /// Mesma ferramenta de página inteira de <see cref="ExpandNoAndNuCodesRecursively"/>
        /// (mesma descida em grupos e PowerClips, contêineres nunca renomeados), para os códigos
        /// pedidos — um só ou todos. Sem macro de origem para AP/TS (pedido do usuário,
        /// 2026-11): NO/NU continuam "TextoNome"/"TextoNumero" como a macro original; AP/TS
        /// recebem os mesmos nomes longos da alternância ("Texto Apelido"/"Texto Tipo
        /// Sanguíneo"), por decisão do usuário.
        /// </summary>
        public OperationResult RemoveCodesRecursively(ICorelPage page, params ObjectCode[] codes)
        {
            if (codes.Length == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Nenhum código escolhido.");
            }

            var renamed = RenameOnPage(page, codes);
            var label = string.Join("/", codes.Select(c => c.ShortCode));
            return OperationResult.Success(codes.Length == 1
                ? $"Código {label} removido do nome de {renamed} objeto(s)."
                : $"Códigos {label} removidos do nome de {renamed} objeto(s).");
        }

        /// <summary>Nome que cada código recebe ao ser removido da página.</summary>
        public static string PageRemovalName(ObjectCode code) =>
            code == ObjectCode.No ? "TextoNome"
            : code == ObjectCode.Nu ? "TextoNumero"
            : code.LongLabel;

        private static int RenameOnPage(ICorelPage page, ObjectCode[] codes)
        {
            var targets = codes.Distinct().ToDictionary(c => c.ShortCode, PageRemovalName);
            return RenameRecursive(page.Layers.SelectMany(l => l.Shapes), targets);
        }

        private static int RenameRecursive(System.Collections.Generic.IEnumerable<ICorelShape> shapes, System.Collections.Generic.IReadOnlyDictionary<string, string> targets)
        {
            var count = 0;
            foreach (var shape in shapes)
            {
                // Um shape pode ser grupo e contêiner de PowerClip ao mesmo tempo (duas
                // coleções independentes) — as duas descidas são verificadas, nunca
                // mutuamente exclusivas; só o nome do próprio contêiner nunca é conferido
                // (mesma regra já aplicada a grupos antes desta rodada).
                var isContainer = false;

                if (shape.Kind == ShapeKind.Group)
                {
                    count += RenameRecursive(shape.Shapes, targets);
                    isContainer = true;
                }

                if (shape.HasPowerClipContents)
                {
                    count += RenameRecursive(shape.PowerClipShapes, targets);
                    isContainer = true;
                }

                if (isContainer)
                {
                    continue;
                }

                if (shape.Name != null && targets.TryGetValue(shape.Name, out var newName))
                {
                    shape.Name = newName;
                    count++;
                }
            }

            return count;
        }
    }
}
