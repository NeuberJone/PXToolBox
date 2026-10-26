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
        /// (descendo recursivamente em grupos), expandindo "NO" → "TextoNome" e "NU" → "TextoNumero"
        /// (sem espaço — convenção própria desta rotina, diferente de "Texto Nome"/"Texto Número"
        /// usados por <see cref="Toggle"/>; preservado tal como está, não é inconsistência a corrigir).
        /// Esta rotina nunca expande AP/TS — a macro original só trata NO e NU
        /// (ver docs/migrated-tools.md, "Lacunas funcionais").
        /// </summary>
        public OperationResult ExpandNoAndNuCodesRecursively(ICorelPage page)
        {
            var topLevelShapes = page.Layers.SelectMany(l => l.Shapes);
            var renamed = RenameRecursive(topLevelShapes);
            return OperationResult.Success($"Código NO/NU removido do nome de {renamed} objeto(s).");
        }

        private static int RenameRecursive(System.Collections.Generic.IEnumerable<ICorelShape> shapes)
        {
            var count = 0;
            foreach (var shape in shapes)
            {
                if (shape.Kind == ShapeKind.Group)
                {
                    count += RenameRecursive(shape.Shapes);
                    continue;
                }

                if (shape.Name == "NO")
                {
                    shape.Name = "TextoNome";
                    count++;
                }
                else if (shape.Name == "NU")
                {
                    shape.Name = "TextoNumero";
                    count++;
                }
            }

            return count;
        }
    }
}
