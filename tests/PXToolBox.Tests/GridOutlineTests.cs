using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    /// <summary>
    /// "Aplicar contorno do Grid" como ferramenta própria (GridPreparationService.ApplyGridOutline,
    /// porte de Utilidades.AplicaContornoGrid fora do Preparar Grid Novo).
    /// </summary>
    public class GridOutlineTests
    {
        [Fact]
        public void AppliesTheSameStyleToTopLevelShapesOfEveryPage()
        {
            var document = new FakeDocument();
            var first = (FakeLayer)document.ActivePageFake.CreateLayer("CAM1.MA");
            var a = first.AddShape(new FakeShape { Name = "a" });
            var group = new FakeShape { Kind = ShapeKind.Group };
            var child = new FakeShape { Name = "filho" };
            group.ShapesList.Add(child);
            first.AddShape(group);
            var secondPage = new FakePage();
            document.PagesList.Add(secondPage);
            var b = ((FakeLayer)secondPage.CreateLayer("CAM1.FE")).AddShape(new FakeShape { Name = "b" });

            var result = new GridPreparationService().ApplyGridOutline(document);

            Assert.True(result.IsSuccess);
            Assert.Equal("Contorno do Grid aplicado em 3 objeto(s), em todas as páginas.", result.Message);
            var style = ((FakeShape)a).LastAppliedStyle;
            Assert.NotNull(style);
            Assert.Contains("\"outline\"", style);
            Assert.Equal(style, ((FakeShape)b).LastAppliedStyle);   // outra página, mesmo estilo
            Assert.Equal(style, group.LastAppliedStyle);             // o grupo como um objeto só
            Assert.Null(child.LastAppliedStyle);                     // sem descer no grupo (fiel à macro)
        }

        [Fact]
        public void NeverTouchesSpecialCorelLayers()
        {
            var document = new FakeDocument();
            var guides = (FakeLayer)document.ActivePageFake.CreateLayer("Guias");
            guides.IsSpecialLayer = true;
            var guideline = (FakeShape)guides.AddShape(new FakeShape { Name = "guia" });
            var normal = (FakeShape)((FakeLayer)document.ActivePageFake.CreateLayer("CAM1.MA")).AddShape(new FakeShape());

            var result = new GridPreparationService().ApplyGridOutline(document);

            Assert.Equal("Contorno do Grid aplicado em 1 objeto(s), em todas as páginas.", result.Message);
            Assert.Null(guideline.LastAppliedStyle);
            Assert.NotNull(normal.LastAppliedStyle);
        }

        [Fact]
        public void EmptyDocument_SucceedsWithoutChanges()
        {
            var result = new GridPreparationService().ApplyGridOutline(new FakeDocument());

            Assert.True(result.IsSuccess);
            Assert.Equal("Nenhum objeto no documento para aplicar o contorno do Grid.", result.Message);
        }

        [Fact]
        public void UsesExactlyTheStyleOfPrepareNewGrid()
        {
            var standalone = new FakeDocument();
            var s1 = (FakeShape)((FakeLayer)standalone.ActivePageFake.CreateLayer("CAM1.MA")).AddShape(new FakeShape { Width = 1, Height = 1 });
            new GridPreparationService().ApplyGridOutline(standalone);

            var viaNewGrid = new FakeDocument();
            var s2 = (FakeShape)((FakeLayer)viaNewGrid.ActivePageFake.CreateLayer("CAM1.MA")).AddShape(new FakeShape { Width = 1, Height = 1 });
            new GridPreparationService().PrepareNewGrid(viaNewGrid);

            Assert.Equal(s2.LastAppliedStyle, s1.LastAppliedStyle);
        }
    }
}
