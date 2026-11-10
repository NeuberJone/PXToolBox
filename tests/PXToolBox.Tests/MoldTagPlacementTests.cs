using System.Linq;
using PXToolBox.Core.Molds;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class MoldTagPlacementTests
    {
        // Peça 100×200 com o canto inferior esquerdo em (0, 0); TAG 20×10; margens E=1, D=2, S=3, I=4.
        [Theory]
        [InlineData(MoldTagPosition.TopLeft, 11, 192)]
        [InlineData(MoldTagPosition.TopCenter, 49, 192)]
        [InlineData(MoldTagPosition.TopRight, 88, 192)]
        [InlineData(MoldTagPosition.MiddleLeft, 11, 101)]
        [InlineData(MoldTagPosition.Center, 49, 101)]
        [InlineData(MoldTagPosition.MiddleRight, 88, 101)]
        [InlineData(MoldTagPosition.BottomLeft, 11, 9)]
        [InlineData(MoldTagPosition.BottomCenter, 49, 9)]
        [InlineData(MoldTagPosition.BottomRight, 88, 9)]
        public void ResolveCenter_FollowsTheMoldMotorRule(MoldTagPosition position, double x, double y)
        {
            Assert.Equal((x, y), MoldTagPlacement.ResolveCenter(position, 0, 0, 100, 200, 20, 10, 1, 2, 3, 4));
        }

        [Fact]
        public void Labels_CoverEveryPosition_InGridOrder()
        {
            Assert.Equal(System.Enum.GetValues(typeof(MoldTagPosition)).Cast<MoldTagPosition>(), MoldTagPlacement.Labels.Select(l => l.Position));
            Assert.Equal("Inferior centro", MoldTagPlacement.Label(MoldTagPosition.BottomCenter));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        [InlineData(double.NaN)]
        public void Margins_OutOfRange_AreRefused(double margin)
        {
            var options = new MoldDefineOptions(true, "#FF0000", 0.2, true, 50, 2, MoldTagPosition.TopLeft, tagTopMarginCm: margin);
            Assert.Contains("Margens da TAG: entre 0 e 100 cm.", options.Validate());
        }

        [Fact]
        public void Define_PlacesTheTag_ByPositionAndMargins_InDocumentUnits()
        {
            var layer = new FakeLayer { Name = "Camada 1" };
            var page = new FakePage();
            page.LayersList.Add(layer);
            var piece = layer.AddShape(new FakeShape { Name = "Curva", Kind = ShapeKind.Curve, Width = 100, Height = 200, CenterX = 50, CenterY = 100 });
            var defaults = MoldSettings.Default;
            var settings = new MoldSettings(defaults.Male, defaults.Female, defaults.Kids, defaults.GridBaseSize, defaults.LaserNames, defaults.TagName, defaults.IdentifierName,
                new MoldDefineOptions(true, "#FF0000", 0.2, true, 50, 0, MoldTagPosition.TopRight, tagRightMarginCm: 1, tagTopMarginCm: 2));
            var selection = new FakeSelection();
            selection.ShapesList.Add(piece);

            // 1 cm = 10 unidades do documento (ex.: mm).
            var result = new MoldDefineService(() => settings, cm => cm * 10).Define(selection, page, new FakeArrangement(), "Frente", "G");

            Assert.True(result.IsSuccess, result.Message);
            var tag = (FakeShape)layer.ShapesList.Single().Shapes.Single(s => s.Name == "TAG");
            Assert.Equal(100 - 10, tag.CenterX + tag.Width / 2, 6);
            Assert.Equal(200 - 20, tag.CenterY + tag.Height / 2, 6);
        }
    }
}
