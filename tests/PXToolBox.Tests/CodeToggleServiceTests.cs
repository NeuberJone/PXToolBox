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

        /// <summary>
        /// Shape.PowerClip.Shapes é uma coleção separada de Shape.Shapes (confirmado por
        /// reflexão sobre Corel.Interop.VGCore.dll) — a macro original (Correcoes.bas,
        /// RenameObjects) nunca desceu em PowerClip para esta ferramenta, então sem esta
        /// descida um "NO"/"NU" dentro de um PowerClip ficava invisível.
        /// </summary>
        [Fact]
        public void ExpandNoAndNuCodesRecursively_DescendsIntoPowerClipContents()
        {
            var page = new FakePage();
            var layer = (FakeLayer)page.CreateLayer("L1");

            var powerClipContainer = new FakeShape { Kind = ShapeKind.Rectangle, HasPowerClipContents = true };
            var clippedNo = new FakeShape { Name = "NO", Kind = ShapeKind.Text };
            powerClipContainer.PowerClipShapesList.Add(clippedNo);
            layer.AddShape(powerClipContainer);

            var service = new CodeToggleService();
            var result = service.ExpandNoAndNuCodesRecursively(page);

            Assert.True(result.IsSuccess);
            Assert.Equal("TextoNome", clippedNo.Name);
            Assert.Equal("Código NO/NU removido do nome de 1 objeto(s).", result.Message);
        }

        /// <summary>O próprio contêiner do PowerClip nunca tem o nome verificado — mesma regra
        /// já aplicada ao contêiner de um grupo.</summary>
        [Fact]
        public void ExpandNoAndNuCodesRecursively_NeverRenamesThePowerClipContainerItself()
        {
            var page = new FakePage();
            var layer = (FakeLayer)page.CreateLayer("L1");

            var powerClipContainer = new FakeShape { Name = "NO", Kind = ShapeKind.Rectangle, HasPowerClipContents = true };
            layer.AddShape(powerClipContainer);

            var service = new CodeToggleService();
            service.ExpandNoAndNuCodesRecursively(page);

            Assert.Equal("NO", powerClipContainer.Name);
        }

        /// <summary>Um shape pode ser grupo E contêiner de PowerClip ao mesmo tempo (duas
        /// coleções independentes) — as duas descidas acontecem, nenhuma impede a outra.</summary>
        [Fact]
        public void ExpandNoAndNuCodesRecursively_DescendsIntoBothGroupChildrenAndPowerClipContents_WhenBothPresent()
        {
            var page = new FakePage();
            var layer = (FakeLayer)page.CreateLayer("L1");

            var container = new FakeShape { Kind = ShapeKind.Group, HasPowerClipContents = true };
            var groupChild = new FakeShape { Name = "NO", Kind = ShapeKind.Text };
            var clippedChild = new FakeShape { Name = "NU", Kind = ShapeKind.Text };
            container.ShapesList.Add(groupChild);
            container.PowerClipShapesList.Add(clippedChild);
            layer.AddShape(container);

            var service = new CodeToggleService();
            var result = service.ExpandNoAndNuCodesRecursively(page);

            Assert.True(result.IsSuccess);
            Assert.Equal("TextoNome", groupChild.Name);
            Assert.Equal("TextoNumero", clippedChild.Name);
        }

        /// <summary>Descida em PowerClip funciona em qualquer profundidade de aninhamento
        /// (PowerClip dentro de grupo dentro de PowerClip, etc.).</summary>
        [Fact]
        public void ExpandNoAndNuCodesRecursively_DescendsIntoNestedPowerClipInsideGroup()
        {
            var page = new FakePage();
            var layer = (FakeLayer)page.CreateLayer("L1");

            var group = new FakeShape { Kind = ShapeKind.Group };
            var nestedPowerClip = new FakeShape { Kind = ShapeKind.Rectangle, HasPowerClipContents = true };
            var deeplyClippedNu = new FakeShape { Name = "NU", Kind = ShapeKind.Text };
            nestedPowerClip.PowerClipShapesList.Add(deeplyClippedNu);
            group.ShapesList.Add(nestedPowerClip);
            layer.AddShape(group);

            var service = new CodeToggleService();
            var result = service.ExpandNoAndNuCodesRecursively(page);

            Assert.True(result.IsSuccess);
            Assert.Equal("TextoNumero", deeplyClippedNu.Name);
        }

        private static (FakePage Page, FakeShape No, FakeShape Nu, FakeShape Ap, FakeShape Ts, FakeShape Other) PageWithAllCodes()
        {
            var page = new FakePage();
            var layer = (FakeLayer)page.CreateLayer("L1");
            var no = layer.AddShape(new FakeShape { Name = "NO", Kind = ShapeKind.Text });
            var group = new FakeShape { Kind = ShapeKind.Group };
            var nu = new FakeShape { Name = "NU", Kind = ShapeKind.Text };
            group.ShapesList.Add(nu);
            layer.AddShape(group);
            var clip = new FakeShape { Kind = ShapeKind.Rectangle, HasPowerClipContents = true };
            var ap = new FakeShape { Name = "AP", Kind = ShapeKind.Text };
            clip.PowerClipShapesList.Add(ap);
            layer.AddShape(clip);
            var ts = ((FakeLayer)page.CreateLayer("L2")).AddShape(new FakeShape { Name = "TS", Kind = ShapeKind.Text });
            var other = layer.AddShape(new FakeShape { Name = "Texto Nome", Kind = ShapeKind.Text });
            return (page, (FakeShape)no, nu, ap, (FakeShape)ts, (FakeShape)other);
        }

        [Fact]
        public void RemoveCodesRecursively_SingleCode_RenamesOnlyThatCode_InGroupsPowerClipsAndAllLayers()
        {
            var (page, no, nu, ap, ts, _) = PageWithAllCodes();
            var service = new CodeToggleService();

            var result = service.RemoveCodesRecursively(page, ObjectCode.Ap);

            Assert.True(result.IsSuccess);
            Assert.Equal("Código AP removido do nome de 1 objeto(s).", result.Message);
            Assert.Equal("Texto Apelido", ap.Name); // dentro do PowerClip
            Assert.Equal("NO", no.Name);
            Assert.Equal("NU", nu.Name);
            Assert.Equal("TS", ts.Name);

            Assert.Equal("Código TS removido do nome de 1 objeto(s).", service.RemoveCodesRecursively(page, ObjectCode.Ts).Message);
            Assert.Equal("Texto Tipo Sanguíneo", ts.Name); // outra camada
            service.RemoveCodesRecursively(page, ObjectCode.Nu);
            Assert.Equal("TextoNumero", nu.Name); // dentro do grupo, mesmo nome do Remover NO/NU
            service.RemoveCodesRecursively(page, ObjectCode.No);
            Assert.Equal("TextoNome", no.Name);
        }

        [Fact]
        public void RemoveCodesRecursively_AllCodes_RenamesEach_AndLeavesOtherNamesAlone()
        {
            var (page, no, nu, ap, ts, other) = PageWithAllCodes();

            var result = new CodeToggleService().RemoveCodesRecursively(page, ObjectCode.No, ObjectCode.Nu, ObjectCode.Ap, ObjectCode.Ts);

            Assert.True(result.IsSuccess);
            Assert.Equal("Códigos NO/NU/AP/TS removidos do nome de 4 objeto(s).", result.Message);
            Assert.Equal(new[] { "TextoNome", "TextoNumero", "Texto Apelido", "Texto Tipo Sanguíneo" }, new[] { no.Name, nu.Name, ap.Name, ts.Name });
            Assert.Equal("Texto Nome", other.Name);
        }

        [Fact]
        public void RemoveCodesRecursively_NothingToRemove_SucceedsWithZero_AndNoCodesFails()
        {
            var page = new FakePage();
            ((FakeLayer)page.CreateLayer("L1")).AddShape(new FakeShape { Name = "outro", Kind = ShapeKind.Text });
            var service = new CodeToggleService();

            Assert.Equal("Código TS removido do nome de 0 objeto(s).", service.RemoveCodesRecursively(page, ObjectCode.Ts).Message);
            Assert.False(service.RemoveCodesRecursively(page).IsSuccess);
        }
    }
}
