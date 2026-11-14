using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Nesting;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;
using Kind = PXToolBox.Outputs.Abstractions.OutlineElementKind;
using Rotations = PXToolBox.Core.Nesting.ContourNester.Rotations;

namespace PXToolBox.Tests
{
    public class NestAreasTests
    {
        private readonly FakePage _page = new FakePage { Width = 200, Height = 300 };
        private readonly FakeLayer _layer = new FakeLayer { Name = "Camada 1" };

        public NestAreasTests()
        {
            _page.LayersList.Add(_layer);
        }

        private FakeSelection Select(params FakeShape[] shapes)
        {
            var selection = new FakeSelection();
            foreach (var shape in shapes) selection.ShapesList.Add(_layer.AddShape(shape));
            return selection;
        }

        private static FakeShape Piece(string name, double centerX, double centerY, double width, double height) =>
            new FakeShape { Name = name, Kind = ShapeKind.Group, CenterX = centerX, CenterY = centerY, Width = width, Height = height };

        [Fact]
        public void Parse_ReadsOneAreaPerLine_InCentimeters_WithOptionalNames()
        {
            var areas = NestAreas.Parse("100 x 150\r\n\r\nRetalho azul: 80,5×60; 30*20");

            Assert.Equal(new[] { "Área 1", "Retalho azul", "Área 3" }, areas.Select(a => a.Name));
            Assert.Equal(new[] { (1000.0, 1500.0), (805.0, 600.0), (300.0, 200.0) }, areas.Select(a => (a.Width, a.Height)));
        }

        [Theory]
        [InlineData("", "Digite as áreas, uma por linha: largura x altura em cm (ex.: 100 x 150).")]
        [InlineData("100 x 150\n100", "Área 2 (“100”): use largura x altura em cm, ex.: 100 x 150.")]
        [InlineData("0 x 10", "Área 1 (“0 x 10”): use largura x altura em cm, ex.: 100 x 150.")]
        [InlineData("-5 x 10", "Área 1 (“-5 x 10”): use largura x altura em cm, ex.: 100 x 150.")]
        public void Parse_ExplainsWhatIsWrong(string text, string message)
        {
            var error = Assert.Throws<ArgumentException>(() => NestAreas.Parse(text));
            Assert.Equal(message, error.Message);
        }

        [Fact]
        public void Parse_LimitsTheNumberOfAreas()
        {
            var error = Assert.Throws<ArgumentException>(() => NestAreas.Parse(string.Join("\n", Enumerable.Repeat("10 x 10", NestAreas.MaximumAreas + 1))));
            Assert.Equal($"O encaixe aceita até {NestAreas.MaximumAreas} áreas.", error.Message);
        }

        [Fact]
        public void Boxes_FillTheFirstArea_ThenTheNextWithWhatIsLeft()
        {
            var items = new[] { new NestItem(0, "Média", 60, 60), new NestItem(1, "Pequena", 40, 40), new NestItem(2, "Grande", 90, 90) };
            var areas = new[] { new NestArea("A", 100, 100), new NestArea("B", 50, 50), new NestArea("C", 300, 300) };

            var result = NestAreas.Nest(items, areas, 0, 0, allowRotation: false);

            // A: só a Grande (sobram 10 mm). B: só a Pequena. C: a Média.
            Assert.Equal(new[] { "A", "B", "C" }, result.Fills.Select(f => f.Area.Name));
            Assert.Equal(new[] { "Grande", "Pequena", "Média" }, result.Fills.Select(f => Assert.Single(f.Result.Placements).Item.Name));
            Assert.Empty(result.Unplaced);
            Assert.Equal(new[] { 0.0, 120, 190 }, result.Lefts);
            Assert.Equal((490.0, 300.0), (result.TotalWidth, result.TotalHeight));
        }

        [Fact]
        public void Arrange_OrdersByArea_KeepingTheTypedOrderOnTies()
        {
            var areas = new[] { new NestArea("A", 100, 100), new NestArea("B", 50, 50), new NestArea("C", 300, 300), new NestArea("D", 25, 100) };

            Assert.Same(areas, NestAreas.Arrange(areas, NestAreaOrder.AsTyped));
            Assert.Equal(new[] { "B", "D", "A", "C" }, NestAreas.Arrange(areas, NestAreaOrder.SmallestFirst).Select(a => a.Name));
            Assert.Equal(new[] { "C", "A", "B", "D" }, NestAreas.Arrange(areas, NestAreaOrder.LargestFirst).Select(a => a.Name));
            Assert.Equal("menores primeiro", NestAreas.Name(NestAreaOrder.SmallestFirst));
        }

        [Fact]
        public void Boxes_SmallestFirst_SpendTheScrapsBeforeTheBigAreas()
        {
            var items = new[] { new NestItem(0, "Média", 60, 60), new NestItem(1, "Pequena", 40, 40), new NestItem(2, "Grande", 90, 90) };
            var areas = NestAreas.Arrange(new[] { new NestArea("A", 100, 100), new NestArea("B", 50, 50), new NestArea("C", 300, 300) },
                NestAreaOrder.SmallestFirst);

            var result = NestAreas.Nest(items, areas, 0, 0, allowRotation: false);

            // B (a menor) recebe a Pequena; A, a Grande; C, a Média. O desenho segue a mesma ordem.
            Assert.Equal(new[] { "B", "A", "C" }, result.Fills.Select(f => f.Area.Name));
            Assert.Equal(new[] { "Pequena", "Grande", "Média" }, result.Fills.Select(f => Assert.Single(f.Result.Placements).Item.Name));
            Assert.Equal(new[] { 0.0, 70, 190 }, result.Lefts);
        }

        [Fact]
        public void Boxes_StopWhenEverythingFits_AndListWhatFitsNowhere()
        {
            var items = new[] { new NestItem(0, "Bandeira", 500, 500), new NestItem(1, "Frente", 40, 40) };
            var areas = new[] { new NestArea("A", 100, 100), new NestArea("B", 100, 100) };

            var result = NestAreas.Nest(items, areas, 0, 0, allowRotation: true);

            Assert.Equal("A", Assert.Single(result.Fills).Area.Name);
            Assert.Equal("Bandeira", Assert.Single(result.Unplaced).Name);
        }

        [Fact]
        public void AnAreaTooSmallForTheMargin_IsNamedInTheError()
        {
            var items = new[] { new NestItem(0, "Grande", 90, 90) };
            var areas = new[] { new NestArea("A", 100, 100), new NestArea("Retalho", 50, 50) };

            var error = Assert.Throws<ArgumentException>(() => NestAreas.Nest(items, areas, 30, 0, false));

            Assert.Equal("Retalho: A margem não deixa espaço para as peças.", error.Message);
        }

        [Fact]
        public void Contours_FillAreasInOrder()
        {
            ContourPiece L(int index) => new ContourPiece(index, $"L{index + 1}", new IReadOnlyList<IReadOnlyList<NestPoint>>[]
            {
                new[] { new[] { new NestPoint(0, 0), new NestPoint(30, 0), new NestPoint(30, 70), new NestPoint(100, 70), new NestPoint(100, 100), new NestPoint(0, 100) } },
            });
            var areas = new[] { new NestArea("A", 110, 110), new NestArea("B", 110, 110) };

            var result = NestAreas.Nest(new[] { L(0), L(1) }, areas, 0, 0, Rotations.None, 5);

            Assert.Equal(new[] { "L1", "L2" }, result.Fills.Select(f => Assert.Single(f.Result.Placements).Piece.Name));
            Assert.Empty(result.Unplaced);
            Assert.All(result.Fills, f => Assert.Empty(ContourNester.Verify(f.Result, 0, 0)));
        }

        [Fact]
        public void Apply_PutsEachAreaSideBySide_FromThePageTopLeft_AndDrawsTheAreas()
        {
            // Documento em mm; página 200 × 300 com o canto inferior esquerdo em (0, 0) → topo em 300.
            var big = Piece("Costas", 900, 900, 80, 80);
            var small = Piece("Frente", -50, -50, 40, 40);
            var selection = Select(big, small);
            var service = new NestingService();
            var items = service.ReadItems(selection, 1);
            var plan = NestAreas.Nest(items, new[] { new NestArea("A", 50, 50), new NestArea("B", 100, 100) }, 0, 0, false);

            var result = service.ApplyAreas(selection, _page, 1, items, plan, resizePage: false, drawAreas: true);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal((20.0, 280.0), (small.CenterX, small.CenterY)); // área A em x 0..50
            Assert.Equal((110.0, 260.0), (big.CenterX, big.CenterY));    // área B em x 70..170
            var outlines = _layer.ShapesList.OfType<FakeShape>().Where(s => s.Kind == ShapeKind.Rectangle)
                .OrderBy(s => s.CenterX).ToList();
            Assert.Equal(new[] { "Área: A (5 × 5 cm)", "Área: B (10 × 10 cm)" }, outlines.Select(s => s.Name));
            Assert.Equal(new[] { (25.0, 275.0, 50.0), (120.0, 250.0, 100.0) }, outlines.Select(s => (s.CenterX, s.CenterY, s.Width)));
            Assert.Equal("2 peça(s) encaixada(s) em 2 de 2 área(s); A: 1 peça(s), aproveitamento 64,0%; B: 1 peça(s), aproveitamento 64,0%; áreas desenhadas. Ctrl+Z desfaz.",
                result.Message);
        }

        [Fact]
        public void Apply_WithQuantities_ResizesThePageToAllAreas_AndCopiesOnlyWhatFits()
        {
            var piece = Piece("Bolso", 0, 0, 40, 40);
            var selection = Select(piece);
            var service = new NestingService();
            var items = service.ReadItems(selection, 1);
            var (expanded, sources) = NestQuantities.Expand(items, new[] { 7 });
            // A cabe 1, B cabe 4 → 5 encaixadas, 2 não couberam.
            var plan = NestAreas.Nest(expanded, new[] { new NestArea("A", 50, 50), new NestArea("B", 100, 100) }, 0, 0, false);

            var result = service.ApplyAreas(selection, _page, 1, items, plan, resizePage: true, drawAreas: false, sources);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal((170.0, 100.0), (_page.Width, _page.Height));
            Assert.Equal((20.0, 80.0), (piece.CenterX, piece.CenterY));
            Assert.Equal(5, _layer.ShapesList.Count);
            Assert.DoesNotContain(_layer.ShapesList.OfType<FakeShape>(), s => s.Kind == ShapeKind.Rectangle);
            Assert.StartsWith("5 peça(s) encaixada(s) em 2 de 2 área(s) (4 cópia(s) criada(s)); A: 1 peça(s)", result.Message);
            Assert.EndsWith("; página ajustada às áreas. Não couberam em nenhuma área (ficaram onde estavam): Bolso (6/7), Bolso (7/7). Ctrl+Z desfaz.",
                result.Message);
        }

        [Fact]
        public void ApplyContour_MovesEachPieceIntoItsArea()
        {
            List<OutlineElement> Polyline(params double[] xy) => Enumerable.Range(0, xy.Length / 2)
                .Select(i => new OutlineElement(i == 0 ? Kind.Start : Kind.Line, xy[2 * i], xy[2 * i + 1])).ToList();
            FakeShape L(string name, double x, double y) => new FakeShape
            {
                Name = name, Kind = ShapeKind.Curve, Width = 100, Height = 100, CenterX = x + 50, CenterY = y + 50,
                OutlineElements = Polyline(x, y, x + 100, y, x + 100, y + 30, x + 30, y + 30, x + 30, y + 100, x, y + 100),
            };
            var first = L("L1", 500, 500);
            var second = L("L2", -400, 0);
            var selection = Select(first, second);
            var service = new NestingService();
            var reading = service.ReadContours(selection, 1);
            var plan = NestAreas.Nest(reading.Pieces, new[] { new NestArea("A", 120, 120), new NestArea("B", 120, 120) }, 0, 0, Rotations.None, 5);

            var result = service.ApplyContourAreas(selection, _page, 1, reading, plan, 0, 0, resizePage: false, drawAreas: false);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal((50.0, 250.0), (first.CenterX, first.CenterY));
            Assert.Equal((190.0, 250.0), (second.CenterX, second.CenterY)); // área B começa em 140
            Assert.StartsWith("2 peça(s) encaixada(s) pelo contorno em 2 de 2 área(s); A: 1 peça(s), aproveitamento ~", result.Message);
        }

        [Fact]
        public void Apply_RefusesAChangedSelection_WithoutDrawingAnything()
        {
            var piece = Piece("Frente", 7, 7, 40, 40);
            var selection = Select(piece);
            var service = new NestingService();
            var items = service.ReadItems(selection, 1);
            var plan = NestAreas.Nest(items, new[] { new NestArea("A", 50, 50) }, 0, 0, false);
            piece.Height = 45;

            var result = service.ApplyAreas(selection, _page, 1, items, plan, false, true);

            Assert.False(result.IsSuccess);
            Assert.Single(_layer.ShapesList);
            Assert.Equal((7.0, 7.0), (piece.CenterX, piece.CenterY));
        }
    }
}
