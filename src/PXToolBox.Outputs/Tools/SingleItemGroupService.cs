using System;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Criar grupo com item único" — reproduz o observado no SISBolt (captura dev.68, um
    /// retângulo): grupo novo na mesma camada, com o objeto ORIGINAL como único filho (mesmo
    /// StaticID, geometria e propriedades intactas), grupo trazido para a frente e selecionado,
    /// sem nenhuma entrada em Properties no grupo.
    ///
    /// Nome do grupo: <see cref="GroupName"/> é o observado numa única captura. Ainda não foi
    /// confirmado se o SISBolt gera o mesmo nome para um objeto de outro nome — PROVISÓRIO até a
    /// captura de confirmação (docs/sisbolt-tools-investigation.md).
    /// </summary>
    public sealed class SingleItemGroupService
    {
        public const string GroupName = "--- RENOMEAR TAMANHO ---";

        public OperationResult CreateGroup(ICorelSelection selection)
        {
            if (selection.Count != 1)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection,
                    selection.Count == 0 ? "Selecione um objeto." : "Selecione um único objeto.");
            }

            var shape = selection.Shapes[0];
            if (shape.IsInsideGroup)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Selecione um objeto no nível da camada (fora de grupos).");
            }

            if (shape.Layer == null)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "O objeto selecionado não está numa camada.");
            }

            var originalId = shape.StaticId;
            var group = shape.GroupAlone();

            // Confere a estrutura ANTES de nomear/ordenar/selecionar: exatamente um filho, o
            // próprio original. Se o Corel fizer outra coisa, nada mais é alterado e o usuário
            // é avisado (o agrupamento já feito sai com um Ctrl+Z).
            var children = group.Shapes;
            if (group.Kind != ShapeKind.Group || children.Count != 1 || children[0].StaticId != originalId)
            {
                return OperationResult.Fail(OperationStatus.ExecutionFailed,
                    $"O Corel não criou o grupo esperado (tipo {group.Kind}, {children.Count} filho(s)). Use Ctrl+Z para desfazer.");
            }

            group.Name = GroupName;
            group.OrderToFront();
            group.SelectAlone();
            return OperationResult.Success($"Grupo criado com o objeto como único item, na frente da camada e selecionado (nome \"{GroupName}\").");
        }
    }
}
