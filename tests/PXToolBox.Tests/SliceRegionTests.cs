using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Slicing;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    /// <summary>Divisão livre do Slice: linhas de corte em qualquer ângulo (guias inclinadas).</summary>
    public class SliceRegionTests
    {
        private static double Area(IReadOnlyList<SlicePoint> polygon) =>
            Math.Abs(Enumerable.Range(0, polygon.Count).Sum(k =>
                polygon[k].X * polygon[(k + 1) % polygon.Count].Y - polygon[(k + 1) % polygon.Count].X * polygon[k].Y)) / 2;

        [Fact]
        public void Diagonal_SplitsTheArtInTwoTriangles_NumberedFromTheTop()
        {
            var plan = SliceRegionPlanner.ByLines(100, 100, new[] { new SliceLine(0, 0, 100, 100) }, 0);

            Assert.Equal(2, plan.Regions.Count);
            var upper = plan.Regions[0];
            var lower = plan.Regions[1];
            Assert.Equal((1, 2), (upper.Number, lower.Number));
            Assert.Equal(66.67, upper.CenterX, 2);
            Assert.Equal(33.33, upper.CenterY, 2);
            Assert.Equal(3, upper.Points.Count);
            Assert.Equal(5000, upper.Area, 6);
            Assert.Equal(5000, Area(lower.Points), 6);
        }

        [Fact]
        public void Overlap_AddsHalfTheBandBeyondEachCutLine_AndNotBeyondTheArt()
        {
            var plan = SliceRegionPlanner.ByLines(100, 100, new[] { new SliceLine(0, 0, 100, 100) }, 10);

            var upper = plan.Regions[0]; // lado x > y
            // Distância assinada até a diagonal: (x − y) / √2. A parte de cima vai até −5 (metade da faixa).
            Assert.Equal(-5, upper.Points.Min(p => (p.X - p.Y) / Math.Sqrt(2)), 6);
            Assert.Equal(5, plan.Regions[1].Points.Max(p => (p.X - p.Y) / Math.Sqrt(2)), 6);
            Assert.All(plan.Regions.SelectMany(r => r.Points), p => Assert.InRange(p.X, 0, 100));
            Assert.All(plan.Regions.SelectMany(r => r.Points), p => Assert.InRange(p.Y, 0, 100));
            Assert.Equal(5000, upper.Area, 6); // a área informada é sem a sobreposição
            Assert.True(Area(upper.Points) > 5000);
        }

        [Fact]
        public void StraightLines_GiveTheSamePartsAsTheTable()
        {
            var plan = SliceRegionPlanner.ByLines(300, 100,
                new[] { new SliceLine(100, 0, 100, 1), new SliceLine(200, 0, 200, 1), new SliceLine(0, 50, 1, 50) }, 6);
            var table = SlicePlanner.ByTable(300, 100, 2, 3, 6);

            Assert.Equal(table.Tiles.Select(t => (t.X, t.Y, t.Width, t.Height)),
                plan.Regions.Select(r => (Math.Round(r.MinX, 9), Math.Round(r.MinY, 9), Math.Round(r.Width, 9), Math.Round(r.Height, 9))));
            Assert.Equal(Enumerable.Range(1, 6), plan.Regions.Select(r => r.Number));
        }

        [Fact]
        public void LinesOutsideOrTouchingACorner_AreIgnored_AndWithoutAnyCrossingItRefuses()
        {
            var plan = SliceRegionPlanner.ByLines(100, 100, new[]
            {
                new SliceLine(0, 50, 100, 60),     // cruza
                new SliceLine(0, 300, 100, 300),   // fora
                new SliceLine(-50, 50, 50, -50),   // encosta só no canto (0, 0)
            }, 0);
            Assert.Equal((1, 2, 2), (plan.Lines.Count, plan.Outside, plan.Regions.Count));
            Assert.False(SliceRegionPlanner.Crosses(100, 100, new SliceLine(-50, 50, 50, -50)));

            var ex = Assert.Throws<ArgumentException>(() => SliceRegionPlanner.ByLines(100, 100, new[] { new SliceLine(0, 300, 100, 300) }, 0));
            Assert.Equal("Nenhuma linha de corte cruza a arte.", ex.Message);
        }

        [Fact]
        public void ThinParts_AndTooManyParts_AreRefused()
        {
            var thin = Assert.Throws<ArgumentException>(() => SliceRegionPlanner.ByLines(100, 100,
                new[] { new SliceLine(50, 0, 50, 1), new SliceLine(50.3, 0, 50.3, 1) }, 0));
            Assert.StartsWith("Uma parte perto de X 5; Y 5 cm ficaria fina demais", thin.Message);

            var lines = Enumerable.Range(1, 32).Select(k => new SliceLine(k * 3, 0, k * 3, 1))
                .Concat(Enumerable.Range(1, 32).Select(k => new SliceLine(0, k * 3, 1, k * 3)));
            var many = Assert.Throws<ArgumentException>(() => SliceRegionPlanner.ByLines(100, 100, lines, 0));
            Assert.Equal("As linhas formam mais de 1000 partes.", many.Message);

            Assert.Throws<ArgumentException>(() => SliceRegionPlanner.ByLines(100, 100, new[] { new SliceLine(1, 1, 1, 1) }, 0));
            Assert.Throws<ArgumentException>(() => SliceRegionPlanner.ByLines(100, 100, new[] { new SliceLine(0, 0, 1, 1) }, -1));
        }

        [Fact]
        public void ReadCuts_TakesSlantedGuides_InMillimetersFromTheTopLeftOfTheArt()
        {
            // Documento em unidades de 2 mm. Arte de 200 × 100 unidades com o canto inferior esquerdo em (100, 200).
            var page = new FakePage { Width = 1000, Height = 1000 };
            var layer = new FakeLayer { Name = "Camada 1" };
            page.LayersList.Add(layer);
            var selection = new FakeSelection();
            selection.ShapesList.Add(layer.AddShape(new FakeShape { Name = "Arte", Kind = ShapeKind.Curve, CenterX = 200, CenterY = 250, Width = 200, Height = 100 }));
            page.GuidesList.Add(new FakeGuide(100, 300, 300, 200) { Owner = page }); // diagonal da arte
            page.GuidesList.Add(new FakeGuide(0, 900, 10, 950) { Owner = page });    // fora
            page.CreateGuide(false, 200);                                           // vertical no meio

            var result = new SliceGuideService().ReadCuts(selection, page, 2);

            Assert.True(result.IsSuccess, result.Message);
            var line = Assert.Single(result.Value!.Slanted);
            Assert.Equal((0.0, 0.0, 100.0, 50.0), (line.X1, line.Y1, line.X2, line.Y2));
            Assert.Equal(new[] { 50.0 }, result.Value.Columns);
            Assert.Equal(1, result.Value.Outside);
        }

        [Fact]
        public void ApplyRegions_MakesPolygonPartsWithTheArtInside_MovedAwayFromTheCenter()
        {
            var page = new FakePage();
            var layer = new FakeLayer { Name = "Camada 1" };
            page.LayersList.Add(layer);
            var selection = new FakeSelection();
            // Documento em mm; arte de 100 × 100 com o canto inferior esquerdo em (0, 0): topo em 100.
            var art = (FakeShape)layer.AddShape(new FakeShape { Name = "Arte", Kind = ShapeKind.Curve, CenterX = 50, CenterY = 50, Width = 100, Height = 100 });
            selection.ShapesList.Add(art);
            var plan = SliceRegionPlanner.ByLines(100, 100, new[] { new SliceLine(0, 0, 100, 100) }, 0);

            var result = new SliceCutService().ApplyRegions(selection, page, 1, plan, 10, deleteOriginal: false);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("2 parte(s) pelas linhas de corte no grupo “Divisão (Slice)”; a arte original ficou oculta, no mesmo lugar. Ctrl+Z desfaz.", result.Message);
            var parts = ((FakeShape)layer.Shapes.Single(s => s.Name == SliceCutService.GroupName)).Shapes.Cast<FakeShape>().ToList();
            Assert.Equal(new[] { "Parte 1", "Parte 2" }, parts.Select(p => p.Name));
            Assert.All(parts, p => Assert.True(p.OutlineRemoved));
            Assert.All(parts, p => Assert.Equal("Arte", p.PowerClipShapes.Single().Name));
            // Parte 1 (em cima, à direita no plano): no documento (Y para cima) os vértices são (0, 100), (100, 100), (100, 0).
            Assert.Equal(new[] { (0.0, 100.0), (100.0, 100.0), (100.0, 0.0) }.OrderBy(p => p),
                parts[0].PolygonPoints.Select(p => (Math.Round(p.X, 9), Math.Round(p.Y, 9))).OrderBy(p => p));
            // Afastamento de 10 mm para longe do centro: parte 1 vai para a direita e para cima (diagonal).
            var step = 10 / Math.Sqrt(2);
            Assert.Equal(50 + step, parts[0].CenterX, 6);
            Assert.Equal(50 + step, parts[0].CenterY, 6);
            Assert.Equal(50 - step, parts[1].CenterX, 6);
            Assert.Equal(50 - step, parts[1].CenterY, 6);
            Assert.False(art.Visible);
        }

        [Fact]
        public void ApplyRegions_FailingInTheMiddle_DeletesWhatWasCreated_AndKeepsTheArt()
        {
            var page = new FakePage();
            var layer = new FakeLayer { Name = "Camada 1", PolygonsBeforeFailure = 1 };
            page.LayersList.Add(layer);
            var selection = new FakeSelection();
            var art = (FakeShape)layer.AddShape(new FakeShape { Name = "Arte", Kind = ShapeKind.Curve, CenterX = 50, CenterY = 50, Width = 100, Height = 100 });
            selection.ShapesList.Add(art);
            var plan = SliceRegionPlanner.ByLines(100, 100, new[] { new SliceLine(0, 0, 100, 100) }, 0);

            var result = new SliceCutService().ApplyRegions(selection, page, 1, plan, 0, deleteOriginal: true);

            Assert.False(result.IsSuccess);
            Assert.Contains("polígono recusado", result.Message);
            Assert.Equal(new[] { art }, layer.Shapes.Where(s => !((FakeShape)s).IsDeleted));
            Assert.True(art.Visible);
            Assert.False(art.IsDeleted);
        }
    }
}
