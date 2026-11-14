using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Nesting;
using Xunit;
using Rotations = PXToolBox.Core.Nesting.ContourNester.Rotations;

namespace PXToolBox.Tests
{
    public class ContourNesterTests
    {
        private static IReadOnlyList<NestPoint> Ring(params double[] xy) =>
            Enumerable.Range(0, xy.Length / 2).Select(i => new NestPoint(xy[2 * i], xy[2 * i + 1])).ToList();

        private static ContourPiece Piece(int index, string name, params IReadOnlyList<NestPoint>[] rings) =>
            new ContourPiece(index, name, new IReadOnlyList<IReadOnlyList<NestPoint>>[] { rings });

        /// <summary>L de 100 × 100 com braços de 30 (Y para baixo): coluna à esquerda e barra embaixo.</summary>
        private static ContourPiece L(int index) =>
            Piece(index, $"L{index + 1}", Ring(0, 0, 30, 0, 30, 70, 100, 70, 100, 100, 0, 100));

        [Fact]
        public void TwoLs_InterlockWithAHalfTurn_WhereBoxesWouldNeedDoubleTheLength()
        {
            var pieces = new[] { L(0), L(1) };

            var result = ContourNester.Nest(pieces, 140, null, 0, 0, Rotations.HalfTurn, 5);

            Assert.Empty(result.Unplaced);
            Assert.Equal(100, result.UsedLength, 6); // pela caixa: 200 (uma embaixo da outra)
            Assert.Equal(new[] { 0, 180 }, result.Placements.Select(p => p.Rotation));
            Assert.Empty(ContourNester.Verify(result, 0, 0));

            var boxes = RectangleNester.Nest(pieces.Select(p => new NestItem(p.Index, p.Name, p.Width, p.Height)).ToList(), 140, null, 0, 0, true);
            Assert.Equal(200, boxes.UsedLength, 6);
        }

        [Fact]
        public void APieceFitsInsideTheHoleOfAnother()
        {
            var frame = Piece(0, "Moldura", Ring(0, 0, 100, 0, 100, 100, 0, 100), Ring(10, 10, 90, 10, 90, 90, 10, 90));
            var square = Piece(1, "Quadrado", Ring(0, 0, 30, 0, 30, 30, 0, 30));

            var result = ContourNester.Nest(new[] { frame, square }, 100, 100, 0, 0, Rotations.None, 5);

            Assert.Empty(result.Unplaced);
            var inside = result.Placements.Single(p => p.Piece.Name == "Quadrado");
            Assert.True(inside.X >= 10 && inside.Y >= 10 && inside.X + inside.Width <= 90 && inside.Y + inside.Height <= 90);
            Assert.Empty(ContourNester.Verify(result, 0, 0));
        }

        [Fact]
        public void LogoOverAPiece_IsNotAHole()
        {
            // Dois objetos na mesma peça: o quadrado de dentro SOMA, não fura.
            var piece = new ContourPiece(0, "Frente", new IReadOnlyList<IReadOnlyList<NestPoint>>[]
            {
                new[] { Ring(0, 0, 100, 0, 100, 100, 0, 100) },
                new[] { Ring(20, 20, 80, 20, 80, 80, 20, 80) },
            });
            var small = Piece(1, "Escudo", Ring(0, 0, 30, 0, 30, 30, 0, 30));

            var result = ContourNester.Nest(new[] { piece, small }, 100, null, 0, 0, Rotations.None, 5);

            Assert.True(result.Placements.Single(p => p.Piece.Name == "Escudo").Y >= 100);
        }

        [Fact]
        public void Rotate_IsCounterClockwiseOnScreen()
        {
            // Y para baixo: um ponto à direita, girado 90° anti-horário, vai para cima.
            var p = ContourNester.Rotate(new NestPoint(1, 0), 90);
            Assert.Equal((0.0, -1.0), (p.X, p.Y));
            p = ContourNester.Rotate(new NestPoint(1, 0), 270);
            Assert.Equal((0.0, 1.0), (p.X, p.Y));
        }

        [Fact]
        public void ManyShapes_KeepTheSpacing_StayInside_AndRepeatTheSameResult()
        {
            var random = new Random(11);
            var pieces = Enumerable.Range(0, 40).Select(i =>
            {
                // Polígonos irregulares (estrelas de 5 a 9 pontas) de 30 a 150 mm.
                var points = 5 + random.Next(5);
                var outer = 15 + random.Next(60);
                var ring = Enumerable.Range(0, points * 2).Select(k =>
                {
                    var radius = k % 2 == 0 ? outer : outer * (0.4 + random.NextDouble() * 0.4);
                    var angle = Math.PI * k / points;
                    return new NestPoint(outer + radius * Math.Cos(angle), outer + radius * Math.Sin(angle));
                }).ToList();
                return Piece(i, $"P{i}", ring);
            }).ToList();

            var result = ContourNester.Nest(pieces, 600, null, 5, 4, Rotations.QuarterTurns, 3);

            Assert.Empty(result.Unplaced);
            Assert.Equal(40, result.Placements.Count);
            Assert.Empty(ContourNester.Verify(result, 5, 4));
            var again = ContourNester.Nest(pieces, 600, null, 5, 4, Rotations.QuarterTurns, 3);
            Assert.Equal(result.Placements.Select(p => (p.X, p.Y, p.Rotation)), again.Placements.Select(p => (p.X, p.Y, p.Rotation)));
        }

        [Fact]
        public void Verify_FindsOverlapCrossingAndTooClose()
        {
            var square = Piece(0, "A", Ring(0, 0, 50, 0, 50, 50, 0, 50));
            var other = Piece(1, "B", Ring(0, 0, 50, 0, 50, 50, 0, 50));
            IReadOnlyList<IReadOnlyList<IReadOnlyList<NestPoint>>> At(double x, double y) =>
                new IReadOnlyList<IReadOnlyList<NestPoint>>[] { new[] { Ring(x, y, x + 50, y, x + 50, y + 50, x, y + 50) } };
            ContourNestResult Layout(double bx, double by) => new ContourNestResult(300, 300, 200, 5,
                new List<ContourPlacement>
                {
                    new ContourPlacement(square, 0, 10, 10, 50, 50, At(10, 10)),
                    new ContourPlacement(other, 0, bx, by, 50, 50, At(bx, by)),
                },
                new List<ContourPiece>(), 0);

            Assert.Equal(new[] { "A e B se sobrepõem." }, ContourNester.Verify(Layout(40, 40), 0, 0));
            Assert.Equal(new[] { "A e B ficaram a menos de 5 mm." }, ContourNester.Verify(Layout(62, 10), 0, 5));
            Assert.Empty(ContourNester.Verify(Layout(65, 10), 0, 5));
            Assert.Equal(new[] { "B sai da área." }, ContourNester.Verify(Layout(280, 10), 0, 0));
        }

        [Fact]
        public void PageMode_ListsWhatDidNotFit()
        {
            var result = ContourNester.Nest(new[] { L(0), Piece(1, "Grande", Ring(0, 0, 300, 0, 300, 20, 0, 20)) }, 200, 200, 0, 0, Rotations.QuarterTurns, 5);

            Assert.Equal("Grande", Assert.Single(result.Unplaced).Name);
            Assert.Equal("L1", Assert.Single(result.Placements).Piece.Name);
        }

        /// <summary>Peça como as do smoke test de 2026-11-11 (75 × 62 cm): topo e base curvos (3 cm), laterais com
        /// recorte (3 cm) — a do meio sobe no "vale" entre as duas de cima.</summary>
        private static ContourPiece Curved(int index) => Piece(index, $"G{index + 1}", Ring(
            0, 30, 225, 9, 375, 0, 525, 9, 750, 30, 720, 217, 750, 590, 525, 611, 375, 620, 225, 611, 0, 590, 30, 217));

        [Fact]
        public void CurvedPieces_KeepRowsOfTwo_InsteadOfTwoOneTwoOne()
        {
            var pieces = Enumerable.Range(0, 28).Select(Curved).ToList();

            var topmost = ContourNester.Nest(pieces, 1750, null, 0, 12, Rotations.HalfTurn, 5, alignTolerance: 0);
            var result = ContourNester.Nest(pieces, 1750, null, 0, 12, Rotations.HalfTurn, 5);

            // Fileiras: peças cujo topo fica a menos de 10 cm do topo da anterior (em Y).
            static List<int> Rows(ContourNestResult r)
            {
                var rows = new List<int>();
                double? last = null;
                foreach (var y in r.Placements.Select(p => p.Y).OrderBy(y => y))
                {
                    if (last == null || y - last > 100) rows.Add(0);
                    rows[rows.Count - 1]++;
                    last = y;
                }

                return rows;
            }

            // Só a posição mais alta: a peça do meio desalinha e as fileiras alternam 2 e 1.
            Assert.Contains(1, Rows(topmost));
            // Com as fileiras alinhadas: 14 fileiras de 2.
            Assert.Equal(28, result.Placements.Count);
            Assert.Equal(Enumerable.Repeat(2, 14), Rows(result));
            Assert.True(result.UsedLength < topmost.UsedLength * 0.85, $"{result.UsedLength} × {topmost.UsedLength}");
            Assert.Empty(ContourNester.Verify(result, 0, 12));
        }

        [Fact]
        public void TheAlignedRule_IsKeptOnlyWhenItIsNotWorse()
        {
            // Peças retangulares: as duas regras dão o mesmo comprimento; o resultado nunca piora.
            var pieces = Enumerable.Range(0, 9).Select(k => Piece(k, $"R{k}", Ring(0, 0, 300, 0, 300, 200, 0, 200))).ToList();

            var topmost = ContourNester.Nest(pieces, 1000, null, 0, 10, Rotations.None, 5, alignTolerance: 0);
            var result = ContourNester.Nest(pieces, 1000, null, 0, 10, Rotations.None, 5);

            Assert.True(result.UsedLength <= topmost.UsedLength);
            Assert.Equal(topmost.Placements.Count, result.Placements.Count);
        }
    }
}
