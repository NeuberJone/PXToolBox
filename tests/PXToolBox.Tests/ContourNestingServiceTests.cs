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
    public class ContourNestingServiceTests
    {
        private readonly FakePage _page = new FakePage { Width = 200, Height = 300 };
        private readonly FakeLayer _layer = new FakeLayer { Name = "Camada 1" };

        public ContourNestingServiceTests()
        {
            _page.LayersList.Add(_layer);
        }

        private FakeSelection Select(params FakeShape[] shapes)
        {
            var selection = new FakeSelection();
            foreach (var shape in shapes) selection.ShapesList.Add(_layer.AddShape(shape));
            return selection;
        }

        /// <summary>Contorno fechado de retas, em coordenadas do documento (Y para cima).</summary>
        private static List<OutlineElement> Polyline(params double[] xy) =>
            Enumerable.Range(0, xy.Length / 2)
                .Select(i => new OutlineElement(i == 0 ? Kind.Start : Kind.Line, xy[2 * i], xy[2 * i + 1])).ToList();

        /// <summary>L de 100 × 100, braços de 30, com o canto inferior esquerdo em (x, y): coluna à
        /// esquerda e barra EMBAIXO (no documento, Y para cima).</summary>
        private static FakeShape L(string name, double x, double y) => new FakeShape
        {
            Name = name, Kind = ShapeKind.Curve, Width = 100, Height = 100, CenterX = x + 50, CenterY = y + 50,
            OutlineElements = Polyline(x, y, x + 100, y, x + 100, y + 30, x + 30, y + 30, x + 30, y + 100, x, y + 100),
        };

        [Fact]
        public void OutlineRings_TurnBezierIntoLines_InMillimetersWithYDown()
        {
            var rings = NestingService.OutlineRings(new[]
            {
                new OutlineElement(Kind.Start, 0, 0),
                new OutlineElement(Kind.Control, 0, 20),
                new OutlineElement(Kind.Control, 20, 20),
                new OutlineElement(Kind.Curve, 20, 0),
            }, 2);

            var ring = Assert.Single(rings);
            Assert.Equal(1 + NestingService.CurveSteps, ring.Count);
            var middle = ring[NestingService.CurveSteps / 2];
            Assert.Equal((5.0, -7.5), (middle.X, middle.Y)); // meio da curva: (10, 15) no documento, ÷ 2 mm
        }

        [Fact]
        public void ReadContours_UsesTheOutline_SumsGroupParts_AndFallsBackToTheBoxForText()
        {
            var group = new FakeShape { Name = "Frente", Kind = ShapeKind.Group, Width = 100, Height = 100, CenterX = 50, CenterY = 50 };
            group.ShapesList.Add(new FakeShape { Kind = ShapeKind.Curve, OutlineElements = Polyline(0, 0, 100, 0, 100, 100, 0, 100) });
            group.ShapesList.Add(new FakeShape { Kind = ShapeKind.Curve, OutlineElements = Polyline(20, 20, 80, 20, 80, 80, 20, 80) });
            var text = new FakeShape { Name = "Nome", Kind = ShapeKind.Text, Width = 40, Height = 10, CenterX = 300, CenterY = 20 };
            var noOutline = new FakeShape { Name = "Bitmap?", Kind = ShapeKind.Curve, Width = 10, Height = 10 };

            var reading = new NestingService().ReadContours(Select(L("Manga", 0, 0), group, text, noOutline), 1);

            Assert.Single(reading.Pieces[0].Shapes);
            Assert.Equal(6, reading.Pieces[0].Shapes[0][0].Count);
            Assert.Equal(-100, reading.Pieces[0].MinY); // Y para baixo: o topo do L (y = 100) vira −100
            Assert.Equal(2, reading.Pieces[1].Shapes.Count); // dois objetos, somados
            Assert.Equal((40.0, 10.0), (reading.Pieces[2].Width, reading.Pieces[2].Height));
            Assert.Equal(new[] { "Nome", "Bitmap?" }, reading.BoxFallbacks);
            Assert.All(reading.Offsets.Take(3), o => Assert.Equal((0.0, 0.0), o));
        }

        [Fact]
        public void ApplyContour_InterlocksTwoLs_TurningOneAHalfTurn()
        {
            var first = L("L1", 500, 500);
            var second = L("L2", -300, 40);
            var selection = Select(first, second);
            var service = new NestingService();
            var reading = service.ReadContours(selection, 1);
            var result = ContourNester.Nest(reading.Pieces, 140, null, 0, 0, Rotations.HalfTurn, 5);

            var outcome = service.ApplyContour(selection, _page, 1, reading, result, 0, 0, resizePage: false);

            Assert.True(outcome.IsSuccess, outcome.Message);
            // L1 no canto (página de 300 de altura: topo em 300); L2 girado 180°, encaixado ao lado.
            Assert.Equal((50.0, 250.0), (first.CenterX, first.CenterY));
            Assert.Equal(180, second.RotationAngle);
            Assert.Equal((85.0, 250.0), (second.CenterX, second.CenterY)); // coluna 7 da grade de 5 mm: x 35
            Assert.StartsWith("2 peça(s) encaixada(s) pelo contorno, 1 girada(s); comprimento usado 10 cm", outcome.Message);
        }

        [Fact]
        public void ApplyContour_KeepsTheDistanceBetweenObjectCenterAndOutline_WhenTurning()
        {
            // Objeto com o centro 10 mm à direita do centro do contorno (ex.: algo invisível fora dele).
            var piece = new FakeShape
            {
                Name = "P", Kind = ShapeKind.Curve, Width = 40, Height = 20, CenterX = 30, CenterY = 10,
                OutlineElements = Polyline(0, 0, 40, 0, 40, 20, 0, 20),
            };
            piece.OutlineElements = Polyline(-10, 0, 30, 0, 30, 20, -10, 20); // contorno centrado em x = 10
            var selection = Select(piece);
            var service = new NestingService();
            var reading = service.ReadContours(selection, 1);
            Assert.Equal((20.0, 0.0), reading.Offsets[0]);
            var result = ContourNester.Nest(reading.Pieces, 30, null, 0, 0, Rotations.QuarterTurns, 1);
            var placement = Assert.Single(result.Placements);
            Assert.Equal(90, placement.Rotation);

            Assert.True(service.ApplyContour(selection, _page, 1, reading, result, 0, 0, false).IsSuccess);

            // Girado 90° anti-horário, o deslocamento de 20 para a direita vira 20 para cima.
            Assert.Equal(placement.CenterX, piece.CenterX, 6);
            Assert.Equal(300 - placement.CenterY + 20, piece.CenterY, 6);
        }

        [Fact]
        public void ApplyContour_RefusesWhenTheIndependentCheckFindsAnOverlap()
        {
            var a = L("A", 0, 0);
            var b = L("B", 200, 0);
            var selection = Select(a, b);
            var service = new NestingService();
            var reading = service.ReadContours(selection, 1);
            var bad = new ContourNestResult(200, 300, 100, 5, new List<ContourPlacement>
            {
                new ContourPlacement(reading.Pieces[0], 0, 0, 0, 100, 100, reading.Pieces[0].Shapes),
                new ContourPlacement(reading.Pieces[1], 0, 0, 0, 100, 100, reading.Pieces[0].Shapes),
            }, new List<ContourPiece>(), 0);

            var outcome = service.ApplyContour(selection, _page, 1, reading, bad, 0, 0, false);

            Assert.False(outcome.IsSuccess);
            Assert.StartsWith("A conferência do encaixe encontrou problema:", outcome.Message);
            Assert.Equal((50.0, 50.0), (a.CenterX, a.CenterY));
            Assert.Equal((250.0, 50.0), (b.CenterX, b.CenterY));
        }
    }
}
