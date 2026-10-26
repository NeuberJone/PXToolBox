using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// Ported de Utilidades.RenomearObjetosSelecionados (CorelMacros/Utilidades.bas).
    /// A obtenção do novo nome (InputBox na macro original) é responsabilidade do host/UI;
    /// este serviço só recebe o nome já escolhido e aplica.
    /// </summary>
    public sealed class RenameSelectedObjectsService
    {
        public OperationResult Rename(ICorelSelection selection, string newName)
        {
            if (selection.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Nenhum objeto selecionado.");
            }

            if (string.IsNullOrEmpty(newName))
            {
                return OperationResult.Fail(OperationStatus.Cancelled, "Nenhum nome digitado. Operação cancelada.");
            }

            foreach (var shape in selection.Shapes)
            {
                shape.Name = newName;
            }

            return OperationResult.Success($"{selection.Count} objeto(s) renomeado(s) para \"{newName}\".");
        }
    }
}
