using System.Linq;
using PXToolBox.Core.Nesting;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    /// <summary>"Comparar com o original" do encaixe.</summary>
    public class NestComparisonTests
    {
        private static readonly NestItem[] Items = { new NestItem(0, "Frente", 100, 50), new NestItem(1, "Costas", 100, 50) };

        [Fact]
        public void Compares_TheBoxTheyUseToday_WithTheNestedBox()
        {
            var origins = new[] { new NestPoint(0, 0), new NestPoint(300, 200) };
            var placed = new[] { (10.0, 10.0, 100.0, 50.0), (115.0, 10.0, 100.0, 50.0) };

            var comparison = NestComparison.Of(Items, origins, placed, out var reason)!;

            Assert.Equal("", reason);
            Assert.Equal((400.0, 250.0, 205.0, 50.0),
                (comparison.OriginalWidth, comparison.OriginalHeight, comparison.NestedWidth, comparison.NestedHeight));
            Assert.Equal(0.1, comparison.OriginalUtilization, 9);       // 10 000 ÷ 100 000
            Assert.Equal(10000.0 / 10250, comparison.NestedUtilization, 9);
            Assert.Equal(10250.0 / 100000 - 1, comparison.AreaChange, 9);
        }

        [Fact]
        public void WithoutOnePlacementPerPiece_ThereIsNothingToCompare()
        {
            var origins = new[] { new NestPoint(0, 0), new NestPoint(300, 200) };

            Assert.Null(NestComparison.Of(Items, origins, new[] { (0.0, 0.0, 100.0, 50.0) }, out var reason));
            Assert.Equal("só quando cada peça entra uma vez e todas cabem.", reason);
        }

        [Fact]
        public void ReadOrigins_GivesTheTopLeftOfEachBox_FromTheTopLeftOfThePage_InMillimeters()
        {
            // Documento em unidades de 2 mm; página 100 × 150 unidades (Y para cima, origem no canto de baixo).
            var page = new FakePage { Width = 100, Height = 150 };
            var layer = new FakeLayer { Name = "Camada 1" };
            page.LayersList.Add(layer);
            var selection = new FakeSelection();
            selection.ShapesList.Add(layer.AddShape(new FakeShape { Name = "A", Kind = ShapeKind.Curve, CenterX = 20, CenterY = 145, Width = 20, Height = 10 }));
            selection.ShapesList.Add(layer.AddShape(new FakeShape { Name = "B", Kind = ShapeKind.Curve, CenterX = 110, CenterY = 5, Width = 20, Height = 30 }));

            var origins = new NestingService().ReadOrigins(selection, page, 2);

            Assert.Equal(new[] { (5.0, 0.0), (50.0, 65.0) }, origins.Select(o => (o.X, o.Y)));
        }
    }
}
