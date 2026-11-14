using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Nesting;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;
using Rotations = PXToolBox.Core.Nesting.ContourNester.Rotations;

namespace PXToolBox.Tests
{
    /// <summary>"Separar por camada" (<see cref="NestAreas.ByGroup"/>): cada material encaixado sozinho, lado a lado.</summary>
    public class NestByLayerTests
    {
        private static readonly string[] Layers = { "Tactel", "Dry fit", "Tactel", "Dry fit", "Tactel" };

        private static IReadOnlyList<NestItem> Items() =>
            Enumerable.Range(0, 5).Select(k => new NestItem(k, $"P{k + 1}", 60, 60)).ToList();

        [Fact]
        public void FreeRoll_EachLayerGetsItsOwnRoll_SideBySide_InTheOrderTheyAppear()
        {
            var result = NestAreas.ByGroup(Items(), item => Layers[item.Index],
                group => NestAreas.Roll(RectangleNester.Nest(group, 130, null, 0, 0, false)), item => item.Index);

            Assert.Equal(new[] { "Tactel — Rolo", "Dry fit — Rolo" }, result.Areas.Select(a => a.Name));
            Assert.Equal(new[] { 0.0, 130 + NestAreas.GapMm }, result.Lefts);
            Assert.Equal(new[] { 0.0, 0 }, result.Tops);
            // Tactel: 3 peças de 60 num rolo de 130 → 2 + 1 = 120 de comprimento; Dry fit: 2 → 60.
            Assert.Equal(new[] { 120.0, 60 }, result.Areas.Select(a => a.Height));
            Assert.Equal(new[] { "P1", "P3", "P5" }, result.Fills[0].Result.Placements.Select(p => p.Item.Name).OrderBy(n => n));
            Assert.Equal(new[] { "P2", "P4" }, result.Fills[1].Result.Placements.Select(p => p.Item.Name).OrderBy(n => n));
            Assert.Equal(new[] { 0.0, 130 + NestAreas.GapMm }, result.Fills.Select(f => f.Left));
            Assert.Empty(result.Unplaced);
        }

        [Fact]
        public void Sheets_KeepTheirStackInsideEachLayer_AndSideBySideMakesOneRow()
        {
            var result = NestAreas.ByGroup(Items(), item => Layers[item.Index],
                group => NestAreas.Sheets(group, 130, 70, 0, 0, false), item => item.Index);

            Assert.Equal(new[] { "Tactel — Folha 1", "Tactel — Folha 2", "Dry fit — Folha 1" }, result.Areas.Select(a => a.Name));
            Assert.Equal(new[] { 0.0, 0, 130 + NestAreas.GapMm }, result.Lefts);
            Assert.Equal(new[] { 0.0, 70, 0 }, result.Tops);

            var row = NestAreas.SideBySide(result);
            Assert.Equal(new[] { 0.0, 150, 300 }, row.Lefts);
            Assert.Equal(new[] { 0.0, 0, 0 }, row.Tops);
        }

        [Fact]
        public void Unplaced_AndUnnamedLayers()
        {
            var items = new[] { new NestItem(0, "Bandeira", 500, 500), new NestItem(1, "Bolso", 40, 40), new NestItem(2, "Gola", 40, 40) };
            string[] layers = { "Tactel", "", "Tactel" };

            var result = NestAreas.ByGroup(items, item => layers[item.Index],
                group => NestAreas.Roll(RectangleNester.Nest(group, 100, null, 0, 0, false)), item => item.Index);

            Assert.Equal(new[] { "Tactel — Rolo", "Sem camada — Rolo" }, result.Areas.Select(a => a.Name));
            Assert.Equal("Bandeira", Assert.Single(result.Unplaced).Name);

            // Um grupo inteiro que não cabe não ocupa lugar.
            var none = NestAreas.ByGroup(items, item => item.Index == 0 ? "Grande" : "Pequenas",
                group => NestAreas.Roll(RectangleNester.Nest(group, 100, null, 0, 0, false)), item => item.Index);
            Assert.Equal(new[] { "Pequenas — Rolo" }, none.Areas.Select(a => a.Name));
            Assert.Equal(new[] { 0.0 }, none.Lefts);
        }

        [Fact]
        public void Contours_ByLayer_PassTheCheck()
        {
            ContourPiece Square(int index) => new ContourPiece(index, $"Q{index + 1}", new IReadOnlyList<IReadOnlyList<NestPoint>>[]
            {
                new[] { new[] { new NestPoint(0, 0), new NestPoint(50, 0), new NestPoint(50, 50), new NestPoint(0, 50) } },
            });
            var pieces = Enumerable.Range(0, 4).Select(Square).ToList();

            var result = NestAreas.ByGroup(pieces, piece => piece.Index % 2 == 0 ? "A" : "B",
                group => NestAreas.Roll(ContourNester.Nest(group, 120, null, 0, 5, Rotations.None, 5)), piece => piece.Index);

            Assert.Equal(new[] { "A — Rolo", "B — Rolo" }, result.Areas.Select(a => a.Name));
            Assert.All(result.Fills, f => Assert.Equal(2, f.Result.Placements.Count));
            Assert.All(result.Fills, f => Assert.Empty(ContourNester.Verify(f.Result, 0, 5)));
        }

        [Fact]
        public void Apply_DrawsEachBlockInItsOwnLayer_AndGroupsIt()
        {
            var page = new FakePage { Width = 400, Height = 200 };
            var tactel = new FakeLayer { Name = "Tactel" };
            var dryFit = new FakeLayer { Name = "Dry fit" };
            page.LayersList.Add(tactel);
            page.LayersList.Add(dryFit);
            var selection = new FakeSelection();
            var shapes = Enumerable.Range(0, 4)
                .Select(k => new FakeShape { Name = $"P{k + 1}", Kind = ShapeKind.Group, CenterX = 900, CenterY = 900, Width = 60, Height = 60 })
                .ToList();
            for (var k = 0; k < shapes.Count; k++) selection.ShapesList.Add((k % 2 == 0 ? tactel : dryFit).AddShape(shapes[k]));
            var service = new NestingService();
            var items = service.ReadItems(selection, 1);
            var layers = service.ReadLayers(selection);
            Assert.Equal(new[] { "Tactel", "Dry fit", "Tactel", "Dry fit" }, layers);
            var plan = NestAreas.ByGroup(items, item => layers[item.Index],
                group => NestAreas.Roll(RectangleNester.Nest(group, 130, null, 0, 0, false)), item => item.Index);

            var result = service.ApplyAreas(selection, page, 1, items, plan, resizePage: false, drawAreas: true, groupBlocks: true);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Contains("2 bloco(s) agrupado(s)", result.Message);
            var tactelBlock = Assert.Single(tactel.ShapesList.OfType<FakeShape>());
            var dryFitBlock = Assert.Single(dryFit.ShapesList.OfType<FakeShape>());
            Assert.Equal("Bloco: Tactel — Rolo", tactelBlock.Name);
            Assert.Equal(new[] { "Área: Tactel — Rolo (13 × 6 cm)", "P1", "P3" }, tactelBlock.ShapesList.Select(s => s.Name));
            Assert.Equal("Bloco: Dry fit — Rolo", dryFitBlock.Name);
            Assert.Equal(new[] { "Área: Dry fit — Rolo (13 × 6 cm)", "P2", "P4" }, dryFitBlock.ShapesList.Select(s => s.Name));
            Assert.Equal(150 + 30, Math.Min(shapes[1].CenterX, shapes[3].CenterX));   // Dry fit começa em 130 + 20
        }
    }
}
