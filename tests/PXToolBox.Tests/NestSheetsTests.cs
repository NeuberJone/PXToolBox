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
    /// <summary>Rolo com altura de folha (<see cref="NestAreas.Sheets(IReadOnlyList{NestItem}, double, double, double, double, bool, NestOrder, NestOrigin)"/>).</summary>
    public class NestSheetsTests
    {
        [Fact]
        public void Boxes_FillASheet_ThenContinueOnANewOneBelow()
        {
            var items = Enumerable.Range(0, 3).Select(k => new NestItem(k, $"P{k + 1}", 60, 60)).ToList();

            var result = NestAreas.Sheets(items, 100, 70, 0, 0, allowRotation: false);

            Assert.Equal(new[] { "Folha 1", "Folha 2", "Folha 3" }, result.Areas.Select(a => a.Name));
            Assert.Equal(new[] { 0.0, 70, 140 }, result.Tops);
            Assert.Equal(new[] { 0.0, 0, 0 }, result.Lefts);
            Assert.Equal(new[] { 0.0, 70, 140 }, result.Fills.Select(f => f.Top));
            Assert.Equal((100.0, 210.0), (result.TotalWidth, result.TotalHeight));
            Assert.Empty(result.Unplaced);
        }

        [Fact]
        public void APieceBiggerThanASheet_StaysUnplaced_WithoutEndlessSheets()
        {
            var items = new[] { new NestItem(0, "Bandeira", 200, 200), new NestItem(1, "Bolso", 40, 40) };

            var result = NestAreas.Sheets(items, 100, 100, 0, 0, allowRotation: true);

            Assert.Equal("Bolso", Assert.Single(Assert.Single(result.Fills).Result.Placements).Item.Name);
            Assert.Equal("Bandeira", Assert.Single(result.Unplaced).Name);

            var none = NestAreas.Sheets(new[] { items[0] }, 100, 100, 0, 0, allowRotation: true);
            Assert.Empty(none.Areas);
            Assert.Equal((0.0, 0.0), (none.TotalWidth, none.TotalHeight));
        }

        [Fact]
        public void TheSheetHeightMustBePositive()
        {
            var error = Assert.Throws<ArgumentException>(() => NestAreas.Sheets(new[] { new NestItem(0, "P", 10, 10) }, 100, 0, 0, 0, false));
            Assert.Equal("Altura da folha: use um número maior que zero (ou deixe vazio para comprimento livre).", error.Message);
        }

        [Fact]
        public void Contours_GoToTheNextSheet_AndEachSheetPassesTheCheck()
        {
            ContourPiece L(int index) => new ContourPiece(index, $"L{index + 1}", new IReadOnlyList<IReadOnlyList<NestPoint>>[]
            {
                new[] { new[] { new NestPoint(0, 0), new NestPoint(30, 0), new NestPoint(30, 70), new NestPoint(100, 70), new NestPoint(100, 100), new NestPoint(0, 100) } },
            });

            var result = NestAreas.Sheets(new[] { L(0), L(1) }, 110, 110, 0, 0, Rotations.None, 5);

            Assert.Equal(new[] { 0.0, 110 }, result.Tops);
            Assert.Equal(new[] { "L1", "L2" }, result.Fills.Select(f => Assert.Single(f.Result.Placements).Piece.Name));
            Assert.All(result.Fills, f => Assert.Empty(ContourNester.Verify(f.Result, 0, 0)));
        }

        [Fact]
        public void Apply_StacksTheSheetsDownFromThePageTop_AndDrawsThem()
        {
            // Documento em mm; página 200 × 300 com o canto inferior esquerdo em (0, 0) → topo em 300.
            var page = new FakePage { Width = 200, Height = 300 };
            var layer = new FakeLayer { Name = "Camada 1" };
            page.LayersList.Add(layer);
            var selection = new FakeSelection();
            var first = new FakeShape { Name = "Frente", Kind = ShapeKind.Group, CenterX = 900, CenterY = 900, Width = 80, Height = 80 };
            var second = new FakeShape { Name = "Costas", Kind = ShapeKind.Group, CenterX = -50, CenterY = -50, Width = 80, Height = 80 };
            selection.ShapesList.Add(layer.AddShape(first));
            selection.ShapesList.Add(layer.AddShape(second));
            var service = new NestingService();
            var items = service.ReadItems(selection, 1);
            var plan = NestAreas.Sheets(items, 100, 100, 0, 0, false);

            var result = service.ApplyAreas(selection, page, 1, items, plan, resizePage: false, drawAreas: true);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal((40.0, 260.0), (first.CenterX, first.CenterY));    // folha 1: y 300..200
            Assert.Equal((40.0, 160.0), (second.CenterX, second.CenterY));  // folha 2: y 200..100
            var outlines = layer.ShapesList.OfType<FakeShape>().Where(s => s.Kind == ShapeKind.Rectangle).OrderByDescending(s => s.CenterY).ToList();
            Assert.Equal(new[] { "Área: Folha 1 (10 × 10 cm)", "Área: Folha 2 (10 × 10 cm)" }, outlines.Select(s => s.Name));
            Assert.Equal(new[] { (50.0, 250.0), (50.0, 150.0) }, outlines.Select(s => (s.CenterX, s.CenterY)));
        }

        [Fact]
        public void SideBySide_PutsTheSheetsInARow_WithTheSamePlacements()
        {
            var items = Enumerable.Range(0, 3).Select(k => new NestItem(k, $"P{k + 1}", 60, 60)).ToList();
            var stacked = NestAreas.Sheets(items, 100, 70, 0, 0, allowRotation: false);

            var row = NestAreas.SideBySide(stacked);

            Assert.Equal(stacked.Areas, row.Areas);
            Assert.Equal(new[] { 0.0, 120, 240 }, row.Lefts);
            Assert.Equal(new[] { 0.0, 0, 0 }, row.Tops);
            Assert.Equal(new[] { 0.0, 120, 240 }, row.Fills.Select(f => f.Left));
            Assert.Equal(new[] { 0.0, 0, 0 }, row.Fills.Select(f => f.Top));
            Assert.Equal(stacked.Fills.Select(f => f.Result), row.Fills.Select(f => f.Result));
            Assert.Equal((340.0, 70.0), (row.TotalWidth, row.TotalHeight));
        }

        [Fact]
        public void Apply_GroupBlocks_GroupsEachSheetWithItsOutline_SideBySide()
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
            // Folhas de 100 × 50: 2 peças na 1ª, 1 na 2ª.
            var plan = NestAreas.SideBySide(NestAreas.Sheets(items, 100, 50, 0, 0, false));

            var result = service.ApplyAreas(selection, page, 1, items, plan, resizePage: false, drawAreas: true, groupBlocks: true);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Contains("2 bloco(s) agrupado(s)", result.Message);
            Assert.Equal((20.0, 180.0), (shapes[0].CenterX, shapes[0].CenterY));   // folha 1: x 0..100, y 200..150
            Assert.Equal((140.0, 180.0), (shapes[2].CenterX, shapes[2].CenterY));  // folha 2: x 120..220, lado a lado
            var groups = layer.ShapesList.OfType<FakeShape>().Where(s => s.Name.StartsWith("Bloco: ")).OrderBy(s => s.Name).ToList();
            Assert.Equal(new[] { "Bloco: Folha 1", "Bloco: Folha 2" }, groups.Select(g => g.Name));
            Assert.Equal(new[] { "Área: Folha 1 (10 × 5 cm)", "P1", "P2" }, groups[0].ShapesList.Select(s => s.Name));
            Assert.Equal(new[] { "Área: Folha 2 (10 × 5 cm)", "P3" }, groups[1].ShapesList.Select(s => s.Name));
            Assert.All(layer.ShapesList.OfType<FakeShape>(), s => Assert.StartsWith("Bloco: ", s.Name));
        }

        [Fact]
        public void Apply_GroupBlocks_WithoutOutlines_GroupsEvenALonePiece()
        {
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
            var plan = NestAreas.SideBySide(NestAreas.Sheets(items, 100, 50, 0, 0, false));

            var result = service.ApplyAreas(selection, page, 1, items, plan, resizePage: false, drawAreas: false, groupBlocks: true);

            Assert.True(result.IsSuccess, result.Message);
            var groups = layer.ShapesList.OfType<FakeShape>().Where(s => s.Name.StartsWith("Bloco: ")).OrderBy(s => s.Name).ToList();
            Assert.Equal(new[] { "Bloco: Folha 1", "Bloco: Folha 2" }, groups.Select(g => g.Name));
            Assert.Equal(new[] { "P3" }, groups[1].ShapesList.Select(s => s.Name));
        }
    }
}
