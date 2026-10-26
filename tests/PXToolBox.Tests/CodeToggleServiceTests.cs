using PXToolBox.Core.Naming;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class CodeToggleServiceTests
    {
        [Fact]
        public void Toggle_EmptySelection_ReturnsInvalidSelection()
        {
            var service = new CodeToggleService();
            var selection = new FakeSelection();

            var result = service.Toggle(selection, ObjectCode.No);

            Assert.False(result.IsSuccess);
            Assert.Equal(OperationStatus.InvalidSelection, result.Status);
        }

        [Fact]
        public void Toggle_NoTextShapeInSelection_ReturnsInvalidSelection()
        {
            var service = new CodeToggleService();
            var selection = new FakeSelection();
            selection.ShapesList.Add(new FakeShape { Kind = ShapeKind.Rectangle, Name = "NO" });

            var result = service.Toggle(selection, ObjectCode.No);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void Toggle_AppliesShortCode_WhenCurrentNameIsNotShortCode()
        {
            var service = new CodeToggleService();
            var selection = new FakeSelection();
            var text = new FakeShape { Kind = ShapeKind.Text, Name = "qualquer coisa" };
            selection.ShapesList.Add(text);

            var result = service.Toggle(selection, ObjectCode.No);

            Assert.True(result.IsSuccess);
            Assert.Equal("NO", text.Name);
        }

        [Fact]
        public void Toggle_ExpandsToLongLabel_WhenCurrentNameIsShortCode()
        {
            var service = new CodeToggleService();
            var selection = new FakeSelection();
            var text = new FakeShape { Kind = ShapeKind.Text, Name = "NO" };
            selection.ShapesList.Add(text);

            var result = service.Toggle(selection, ObjectCode.No);

            Assert.True(result.IsSuccess);
            Assert.Equal("Texto Nome", text.Name);
        }

        [Fact]
        public void Toggle_AffectsAllSelectedShapes_EvenNonText_WhenAnyIsText()
        {
            // Comportamento original preservado: a verificação de "é texto" olha a seleção toda,
            // mas a alternância é aplicada a todas as formas selecionadas.
            var service = new CodeToggleService();
            var selection = new FakeSelection();
            var text = new FakeShape { Kind = ShapeKind.Text, Name = "x" };
            var rectangle = new FakeShape { Kind = ShapeKind.Rectangle, Name = "y" };
            selection.ShapesList.Add(text);
            selection.ShapesList.Add(rectangle);

            var result = service.Toggle(selection, ObjectCode.Ap);

            Assert.True(result.IsSuccess);
            Assert.Equal("AP", text.Name);
            Assert.Equal("AP", rectangle.Name);
        }

        [Fact]
        public void ExpandNoAndNuCodesRecursively_DescendsIntoGroups_AndUsesNoSpaceLabels()
        {
            var page = new FakePage();
            var layer = (FakeLayer)page.CreateLayer("L1");

            var topLevelNo = layer.AddShape(new FakeShape { Name = "NO", Kind = ShapeKind.Text });

            var group = new FakeShape { Kind = ShapeKind.Group };
            var nestedNu = new FakeShape { Name = "NU", Kind = ShapeKind.Text };
            group.ShapesList.Add(nestedNu);
            layer.AddShape(group);

            var unrelated = layer.AddShape(new FakeShape { Name = "outro", Kind = ShapeKind.Text });

            var service = new CodeToggleService();
            var result = service.ExpandNoAndNuCodesRecursively(page);

            Assert.True(result.IsSuccess);
            Assert.Equal("TextoNome", topLevelNo.Name);
            Assert.Equal("TextoNumero", nestedNu.Name); // dentro do grupo, encontrado recursivamente
            Assert.Equal("outro", unrelated.Name); // não é NO nem NU, não muda
        }
    }
}
