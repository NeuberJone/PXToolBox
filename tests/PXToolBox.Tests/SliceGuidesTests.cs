using System;
using System.Linq;
using PXToolBox.Core.Slicing;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class SliceGuidesTests
    {
        [Fact]
        public void ByCuts_SplitsAtTheGivenSeams_WithHalfTheOverlapOnEachSide()
        {
            var plan = SlicePlanner.ByCuts(300, 100, new[] { 200.0, 120 }, new[] { 40.0 }, 10);

            Assert.Equal((2, 3), (plan.Rows, plan.Columns));
            var row1 = plan.Tiles.Where(t => t.Row == 1).ToList();
            Assert.Equal(new[] { (0.0, 125.0), (115.0, 90.0), (195.0, 105.0) }, row1.Select(t => (t.X, t.Width)));
            Assert.Equal(new[] { (0.0, 45.0), (35.0, 65.0) }, plan.Tiles.Where(t => t.Column == 1).Select(t => (t.Y, t.Height)));
        }

        [Fact]
        public void ByCuts_IgnoresCutsOutsideNearTheEdgeOrRepeated_AndWithoutCutsKeepsOnePiece()
        {
            var plan = SlicePlanner.ByCuts(100, 50, new[] { -5, 0.2, 50, 50.3, 99.8, 140 }, Array.Empty<double>(), 0);
            Assert.Equal((1, 2), (plan.Rows, plan.Columns));
            Assert.Equal(new[] { 50.0, 50.0 }, plan.Tiles.Select(t => t.Width));

            Assert.Single(SlicePlanner.ByCuts(100, 50, Array.Empty<double>(), Array.Empty<double>(), 0).Tiles);
            Assert.Equal("A sobreposição deve ser menor que cada divisão nos eixos divididos.",
                Assert.Throws<ArgumentException>(() => SlicePlanner.ByCuts(100, 50, new[] { 10.0 }, Array.Empty<double>(), 10)).Message);
        }

        [Fact]
        public void SeamCenters_GiveBackTheCuts_ForTableAndMaximumSize()
        {
            var (columns, rows) = SlicePlanner.SeamCenters(SlicePlanner.ByTable(300, 100, 2, 3, 6));
            Assert.Equal(new[] { 100.0, 200.0 }, columns);
            Assert.Equal(new[] { 50.0 }, rows);

            // Máximo 120 com sobreposição 20: partes em 0, 100, 200 → emendas no meio de cada faixa comum.
            var (maxColumns, _) = SlicePlanner.SeamCenters(SlicePlanner.ByMaximumSize(300, 100, 120, 200, 20));
            Assert.Equal(new[] { 110.0, 210.0 }, maxColumns);

            var again = SlicePlanner.ByCuts(300, 100, columns, rows, 6);
            Assert.Equal(SlicePlanner.ByTable(300, 100, 2, 3, 6).Tiles.Select(t => (t.X, t.Y, t.Width, t.Height)),
                again.Tiles.Select(t => (t.X, t.Y, t.Width, t.Height)));
        }

        private static (FakePage Page, FakeSelection Selection) ArtAt(double left, double bottom, double width, double height)
        {
            var page = new FakePage { Width = 1000, Height = 1000 };
            var layer = new FakeLayer { Name = "Camada 1" };
            page.LayersList.Add(layer);
            var selection = new FakeSelection();
            selection.ShapesList.Add(layer.AddShape(new FakeShape
            {
                Name = "Arte", Kind = ShapeKind.Curve, CenterX = left + width / 2, CenterY = bottom + height / 2, Width = width, Height = height,
            }));
            return (page, selection);
        }

        [Fact]
        public void ReadCuts_TakesGuidesCrossingTheArt_MeasuredFromItsTopLeft()
        {
            // Arte de 300 × 100 com o canto inferior esquerdo em (100, 200): topo em 300.
            var (page, selection) = ArtAt(100, 200, 300, 100);
            page.CreateGuide(false, 250);  // vertical: 150 da esquerda
            page.CreateGuide(false, 180);  // vertical: 80
            page.CreateGuide(true, 260);   // horizontal: 40 do topo
            page.CreateGuide(false, 50);   // fora (à esquerda da arte)
            page.CreateGuide(true, 900);   // fora (acima)

            var result = new SliceGuideService().ReadCuts(selection, page, 1);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(new[] { 80.0, 150.0 }, result.Value!.Columns);
            Assert.Equal(new[] { 40.0 }, result.Value.Rows);
            Assert.Equal(2, result.Value.Outside);
        }

        [Fact]
        public void ReadCuts_WithoutGuidesOverTheArt_Explains()
        {
            var (page, selection) = ArtAt(0, 0, 100, 100);
            page.CreateGuide(true, 500);

            var result = new SliceGuideService().ReadCuts(selection, page, 1);

            Assert.False(result.IsSuccess);
            Assert.StartsWith("Nenhuma guia cruza a arte selecionada.", result.Message);
        }

        [Fact]
        public void CreateGuides_ReplacesOwnGuides_AndRemoveKeepsTheUsersOnes()
        {
            var (page, selection) = ArtAt(100, 200, 300, 100);
            var users = page.CreateGuide(true, 10);
            var service = new SliceGuideService();

            Assert.True(service.CreateGuides(selection, page, 1, new[] { 100.0, 200 }, new[] { 50.0 }).IsSuccess);
            var second = service.CreateGuides(selection, page, 1, new[] { 150.0 }, Array.Empty<double>());

            Assert.Contains("(3 guia(s) anteriores do Slice removidas)", second.Message);
            var own = page.GuidesList.Where(g => g.Name == SliceGuideService.GuideName).ToList();
            Assert.Equal(new[] { (false, 250.0) }, own.Select(g => (g.Horizontal, g.Position)));
            Assert.Equal(new[] { 150.0 }, service.ReadCuts(selection, page, 1).Value!.Columns);

            var removed = service.RemoveGuides(page);
            Assert.True(removed.IsSuccess, removed.Message);
            Assert.Equal(new[] { users }, page.Guides);
        }
    }
}
