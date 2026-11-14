using System;
using System.Linq;
using PXToolBox.Core.Nesting;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    /// <summary>Distância entre blocos e legenda de cada bloco (dev.119).</summary>
    public sealed class NestBlockOptionsTests
    {
        private static NestItem[] Items(int count, double size) =>
            Enumerable.Range(0, count).Select(k => new NestItem(k, $"P{k + 1}", size, size)).ToArray();

        [Fact]
        public void Gap_IsUsedBetweenAreas_SheetsSideBySide_AndLayers()
        {
            var areas = NestAreas.Nest(Items(2, 60), new[] { new NestArea("A", 70, 70), new NestArea("B", 70, 70) }, 0, 0, false, gap: 50);
            Assert.Equal(new[] { 0.0, 120 }, areas.Lefts);

            var row = NestAreas.SideBySide(NestAreas.Sheets(Items(3, 60), 100, 70, 0, 0, false), gap: 0);
            Assert.Equal(new[] { 0.0, 100, 200 }, row.Lefts);
            Assert.Equal(300.0, row.TotalWidth);

            var layers = new[] { "Tactel", "Dry fit" };
            var split = NestAreas.ByGroup(Items(2, 60), item => layers[item.Index],
                group => NestAreas.Roll(RectangleNester.Nest(group, 100, null, 0, 0, false)), item => item.Index, gap: 5);
            Assert.Equal(new[] { 0.0, 105 }, split.Lefts);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(10001)]
        [InlineData(double.NaN)]
        public void Gap_OutOfRange_Explains(double gap)
        {
            var sheets = NestAreas.Sheets(Items(1, 60), 100, 70, 0, 0, false);
            var error = Assert.Throws<ArgumentException>(() => NestAreas.SideBySide(sheets, gap));
            Assert.Equal("Distância entre blocos: use de 0 a 1000 cm.", error.Message);
            Assert.Throws<ArgumentException>(() => NestAreas.Nest(Items(1, 60), new[] { new NestArea("A", 70, 70) }, 0, 0, false, gap: gap));
        }

        [Fact]
        public void LegendText_AndSize()
        {
            var area = new NestArea("Tactel — Folha 1", 1600, 3000);
            Assert.Equal("Tactel — Folha 1 · 160 × 300 cm · 12 peça(s)", NestingService.LegendText(area, 12));
            // 160 cm ÷ 40 = 4 cm de letra ≈ 113 pt; um bloco estreito fica no mínimo, um largo no máximo.
            Assert.Equal(1600 / 40 / (25.4 / 72), NestingService.LegendPoints(area), 6);
            Assert.Equal(18, NestingService.LegendPoints(new NestArea("A", 100, 100)));
            Assert.Equal(120, NestingService.LegendPoints(new NestArea("A", 5000, 100)));
        }

        [Fact]
        public void Apply_Legends_GoInsideEachBlock_AboveIt_FlushLeft()
        {
            // Documento em mm; página 300 × 200 com o canto inferior esquerdo em (0, 0) → topo em 200.
            var page = new FakePage { Width = 300, Height = 200 };
            var layer = new FakeLayer { Name = "Camada 1" };
            page.LayersList.Add(layer);
            var selection = new FakeSelection();
            var shapes = Enumerable.Range(0, 3)
                .Select(k => new FakeShape { Name = $"P{k + 1}", Kind = ShapeKind.Group, CenterX = 900, CenterY = 900, Width = 40, Height = 40 })
                .ToList();
            foreach (var shape in shapes) selection.ShapesList.Add(layer.AddShape(shape));
            var service = new NestingService();
            var items = service.ReadItems(selection, 1);
            // Folhas de 100 × 50: 2 peças na 1ª, 1 na 2ª; lado a lado com 30 mm entre elas.
            var plan = NestAreas.SideBySide(NestAreas.Sheets(items, 100, 50, 0, 0, false), 30);

            var result = service.ApplyAreas(selection, page, 1, items, plan, resizePage: false, drawAreas: true, groupBlocks: true, legends: true);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Contains("2 bloco(s) agrupado(s), com legenda", result.Message);
            var groups = layer.ShapesList.OfType<FakeShape>().Where(s => s.Name.StartsWith("Bloco: ")).OrderBy(s => s.Name).ToList();
            Assert.Equal(new[] { "Área: Folha 1 (10 × 5 cm)", "P1", "P2", "Legenda: Folha 1" }, groups[0].ShapesList.Select(s => s.Name));
            Assert.Equal(new[] { "Área: Folha 2 (10 × 5 cm)", "P3", "Legenda: Folha 2" }, groups[1].ShapesList.Select(s => s.Name));
            var first = (FakeShape)groups[0].ShapesList.Single(s => s.Name == "Legenda: Folha 1");
            Assert.Equal("Folha 1 · 10 × 5 cm · 2 peça(s)", first.Text);
            // Encostada na esquerda da folha (x 0) e 10 mm acima do topo (y 200): fake de 18 pt = 18 de altura.
            Assert.Equal(0, first.CenterX - first.Width / 2, 6);
            Assert.Equal(210, first.CenterY - first.Height / 2, 6);
            var second = groups[1].ShapesList.Single(s => s.Name == "Legenda: Folha 2");
            Assert.Equal(130, second.CenterX - second.Width / 2, 6);   // folha 2 começa em 100 + 30
        }

        [Fact]
        public void Apply_WithoutGroups_NoLegend()
        {
            var page = new FakePage { Width = 300, Height = 200 };
            var layer = new FakeLayer { Name = "Camada 1" };
            page.LayersList.Add(layer);
            var selection = new FakeSelection();
            selection.ShapesList.Add(layer.AddShape(new FakeShape { Name = "P1", Kind = ShapeKind.Group, CenterX = 900, CenterY = 900, Width = 40, Height = 40 }));
            var service = new NestingService();
            var items = service.ReadItems(selection, 1);
            var plan = NestAreas.Sheets(items, 100, 50, 0, 0, false);

            var result = service.ApplyAreas(selection, page, 1, items, plan, resizePage: false, drawAreas: false, groupBlocks: false, legends: true);

            Assert.True(result.IsSuccess, result.Message);
            Assert.DoesNotContain("legenda", result.Message);
            Assert.DoesNotContain(layer.ShapesList.OfType<FakeShape>(), s => s.Kind == ShapeKind.Text);
        }
    }
}
