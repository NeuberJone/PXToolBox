using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Nesting;
using Xunit;
using Rotations = PXToolBox.Core.Nesting.ContourNester.Rotations;

namespace PXToolBox.Tests
{
    /// <summary>Encaixe começando por outro canto da área (origem).</summary>
    public class NestOriginTests
    {
        private static IReadOnlyList<NestPoint> Ring(params double[] xy) =>
            Enumerable.Range(0, xy.Length / 2).Select(i => new NestPoint(xy[2 * i], xy[2 * i + 1])).ToList();

        /// <summary>L de 100 × 100 com braços de 30 (Y para baixo): coluna à esquerda e barra embaixo.</summary>
        private static ContourPiece L(int index) =>
            new ContourPiece(index, $"L{index + 1}", new IReadOnlyList<IReadOnlyList<NestPoint>>[] { new[] { Ring(0, 0, 30, 0, 30, 70, 100, 70, 100, 100, 0, 100) } });

        private static readonly NestItem[] Boxes =
        {
            new NestItem(0, "Frente", 100, 50),
            new NestItem(1, "Costas", 100, 50),
            new NestItem(2, "Manga", 40, 30),
        };

        [Theory]
        [InlineData(NestOrigin.TopRight)]
        [InlineData(NestOrigin.BottomLeft)]
        [InlineData(NestOrigin.BottomRight)]
        public void Boxes_FromAnotherCorner_AreTheTopLeftLayoutMirrored(NestOrigin origin)
        {
            var normal = RectangleNester.Nest(Boxes, 300, 200, 10, 5, false);
            var mirrored = RectangleNester.Nest(Boxes, 300, 200, 10, 5, false, NestOrder.LongestSide, origin);

            var right = NestOrigins.FromRight(origin);
            var bottom = NestOrigins.FromBottom(origin);
            Assert.Equal(normal.Placements.Select(p => (p.Item.Name, right ? 300 - p.X - p.Width : p.X, bottom ? 200 - p.Y - p.Height : p.Y)),
                mirrored.Placements.Select(p => (p.Item.Name, p.X, p.Y)));
            Assert.Equal(normal.UsedLength, mirrored.UsedLength);
            // A primeira peça encosta na margem do canto escolhido.
            var first = mirrored.Placements[0];
            Assert.Equal(right ? 290 : 10, right ? first.X + first.Width : first.X, 9);
            Assert.Equal(bottom ? 190 : 10, bottom ? first.Y + first.Height : first.Y, 9);
        }

        [Theory]
        [InlineData(NestOrigin.TopLeft)]
        [InlineData(NestOrigin.TopRight)]
        [InlineData(NestOrigin.BottomLeft)]
        [InlineData(NestOrigin.BottomRight)]
        public void Contours_FromAnyCorner_AreTheSamePiecesTurnedByTheReportedAngle(NestOrigin origin)
        {
            var pieces = Enumerable.Range(0, 6).Select(L).ToList();

            var result = ContourNester.Nest(pieces, 400, 300, 10, 4, Rotations.QuarterTurns, 2, NestOrder.Area, default, origin);

            Assert.Empty(result.Unplaced);
            Assert.Empty(ContourNester.Verify(result, 10, 4));
            Assert.All(result.Placements, p => Assert.Same(pieces[p.Piece.Index], p.Piece));
            foreach (var p in result.Placements)
            {
                // O contorno final é a peça original girada pelo ângulo informado (o que o Corel faz) e posta na caixa.
                var turned = p.Piece.Shapes[0][0].Select(q => ContourNester.Rotate(q, p.Rotation)).ToList();
                var minX = turned.Min(q => q.X);
                var minY = turned.Min(q => q.Y);
                Assert.Equal(turned.Select(q => (Math.Round(q.X - minX + p.X, 6), Math.Round(q.Y - minY + p.Y, 6))),
                    p.Shapes[0][0].Select(q => (Math.Round(q.X, 6), Math.Round(q.Y, 6))));
            }

            // Alguma peça encosta nas margens do canto escolhido.
            var points = result.Placements.SelectMany(p => p.Shapes[0][0]).ToList();
            Assert.Equal(10, NestOrigins.FromRight(origin) ? 400 - points.Max(q => q.X) : points.Min(q => q.X), 6);
            Assert.Equal(10, NestOrigins.FromBottom(origin) ? 300 - points.Max(q => q.Y) : points.Min(q => q.Y), 6);
        }

        [Fact]
        public void Roll_StartsAtTheTop_AndOnlyTheSideChanges()
        {
            var error = Assert.Throws<ArgumentException>(() => RectangleNester.Nest(Boxes, 300, null, 0, 0, false, NestOrder.LongestSide, NestOrigin.BottomLeft));
            Assert.Equal("No rolo o encaixe começa no início do rolo: escolha um canto de cima.", error.Message);
            Assert.Throws<ArgumentException>(() => ContourNester.Nest(new[] { L(0) }, 300, null, 0, 0, Rotations.None, 5, NestOrder.Area, default, NestOrigin.BottomRight));

            var right = RectangleNester.Nest(Boxes, 300, null, 0, 0, false, NestOrder.LongestSide, NestOrigin.TopRight);
            Assert.Equal(300, right.Placements.Max(p => p.X + p.Width), 9);
            Assert.Equal(0, right.Placements.Min(p => p.Y), 9);
        }

        [Fact]
        public void Areas_StartFromTheChosenCornerOfEachArea()
        {
            var areas = new[] { new NestArea("A", 120, 60), new NestArea("B", 200, 100) };

            var result = NestAreas.Nest(Boxes, areas, 0, 0, false, NestOrder.LongestSide, NestOrigin.BottomRight);

            Assert.Equal(new[] { "A", "B" }, result.Fills.Select(f => f.Area.Name));
            foreach (var fill in result.Fills)
            {
                Assert.Equal(fill.Area.Width, fill.Result.Placements.Max(p => p.X + p.Width), 9);
                Assert.Equal(fill.Area.Height, fill.Result.Placements.Max(p => p.Y + p.Height), 9);
            }

            Assert.Equal("canto inferior direito", NestOrigins.Name(NestOrigin.BottomRight));
        }
    }
}
