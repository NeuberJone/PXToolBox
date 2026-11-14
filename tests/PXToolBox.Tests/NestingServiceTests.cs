using System.Linq;
using PXToolBox.Core.Nesting;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class NestingServiceTests
    {
        private readonly FakePage _page = new FakePage { Width = 200, Height = 300 };
        private readonly FakeLayer _layer = new FakeLayer { Name = "Camada 1" };

        public NestingServiceTests()
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
        public void PageMode_MovesEachPieceToItsPlace_FromThePageTopLeft()
        {
            // Documento em mm; página 200 × 300 com o canto inferior esquerdo em (0, 0).
            var big = Piece("Costas", 500, 500, 100, 150);
            var small = Piece("", -80, 40, 60, 40);
            var selection = Select(small, big);
            var service = new NestingService();
            var items = service.ReadItems(selection, 1);
            Assert.Equal(new[] { "Peça 1", "Costas" }, items.Select(i => i.Name));
            var plan = RectangleNester.Nest(items, 200, 300, 10, 5, allowRotation: false);

            var result = service.Apply(selection, _page, 1, items, plan, resizePage: false);

            Assert.True(result.IsSuccess, result.Message);
            // Costas (maior) primeiro, no canto: x 10..110, y 10..160 a partir do topo (300).
            Assert.Equal((60.0, 215.0), (big.CenterX, big.CenterY));
            // A menor ao lado, depois de 5 mm: x 115..175, y 10..50.
            Assert.Equal((145.0, 270.0), (small.CenterX, small.CenterY));
            Assert.StartsWith("2 peça(s) encaixada(s); comprimento usado 17 cm, aproveitamento ", result.Message);
            Assert.EndsWith(". Ctrl+Z desfaz.", result.Message);
        }

        [Fact]
        public void Roll_RotatesWhenPlanned_AndCanResizeThePage()
        {
            var piece = Piece("Manga", 0, 0, 80, 40);
            var selection = Select(piece);
            var service = new NestingService();
            var items = service.ReadItems(selection, 1);
            var plan = RectangleNester.Nest(items, 50, null, 0, 0, allowRotation: true);

            var result = service.Apply(selection, _page, 1, items, plan, resizePage: true);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(90, piece.RotationAngle);
            Assert.Equal((40.0, 80.0), (piece.Width, piece.Height));
            Assert.Equal((50.0, 80.0), (_page.Width, _page.Height));
            Assert.Equal((20.0, 40.0), (piece.CenterX, piece.CenterY));
            Assert.Contains("1 girada(s) 90°", result.Message);
            Assert.Contains("página ajustada ao encaixe", result.Message);
        }

        [Fact]
        public void PiecesThatDidNotFit_StayWhereTheyWere_AndAreNamed()
        {
            var fits = Piece("Frente", 0, 0, 50, 50);
            var huge = Piece("Bandeira", 900, 900, 400, 400);
            var selection = Select(fits, huge);
            var service = new NestingService();
            var items = service.ReadItems(selection, 1);

            var result = service.Apply(selection, _page, 1, items, RectangleNester.Nest(items, 200, 300, 0, 0, true), false);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal((900.0, 900.0), (huge.CenterX, huge.CenterY));
            Assert.Contains("Não couberam (ficaram onde estavam): Bandeira", result.Message);
        }

        [Fact]
        public void ChangedSelection_IsRefusedWithoutMovingAnything()
        {
            var piece = Piece("Frente", 7, 7, 50, 50);
            var selection = Select(piece);
            var service = new NestingService();
            var items = service.ReadItems(selection, 1);
            var plan = RectangleNester.Nest(items, 200, 300, 0, 0, true);
            piece.Width = 70;

            var result = service.Apply(selection, _page, 1, items, plan, false);

            Assert.False(result.IsSuccess);
            Assert.Equal("A seleção mudou desde a prévia. Selecione as mesmas peças ou clique em “Usar seleção atual”. Nada foi alterado.", result.Message);
            Assert.Equal((7.0, 7.0), (piece.CenterX, piece.CenterY));
        }
    }
}
