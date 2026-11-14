using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Nesting;
using Xunit;

namespace PXToolBox.Tests
{
    public class RectangleNesterTests
    {
        private static List<NestItem> Items(params (double W, double H)[] sizes) =>
            sizes.Select((s, i) => new NestItem(i, $"P{i + 1}", s.W, s.H)).ToList();

        [Fact]
        public void FourSquares_FillThePage_WithFullUtilization()
        {
            var result = RectangleNester.Nest(Items((50, 50), (50, 50), (50, 50), (50, 50)), 100, 100, 0, 0, allowRotation: false);

            Assert.Empty(result.Unplaced);
            Assert.Equal(new[] { (0.0, 0.0), (50.0, 0.0), (0.0, 50.0), (50.0, 50.0) }, result.Placements.Select(p => (p.X, p.Y)));
            Assert.Equal(100, result.UsedLength, 6);
            Assert.Equal(1, result.Utilization, 6);
        }

        [Fact]
        public void Spacing_IsBetweenPieces_AndMarginAroundTheArea()
        {
            // 10 + 45 + 10 + 45 + 10 = 120: duas peças lado a lado, encostando nas duas margens.
            var result = RectangleNester.Nest(Items((45, 30), (45, 30)), 120, null, 10, 10, allowRotation: false);

            Assert.Equal(new[] { (10.0, 10.0), (65.0, 10.0) }, result.Placements.Select(p => (p.X, p.Y)));
            Assert.Equal(50, result.UsedLength, 6); // 10 + 30 + 10
        }

        [Fact]
        public void Roll_RotatesAPieceWiderThanTheRoll_WhenAllowed()
        {
            var allowed = RectangleNester.Nest(Items((80, 40)), 50, null, 0, 0, allowRotation: true);
            var placement = Assert.Single(allowed.Placements);
            Assert.True(placement.Rotated);
            Assert.Equal((40.0, 80.0), (placement.Width, placement.Height));
            Assert.Equal(80, allowed.UsedLength, 6);

            var forbidden = RectangleNester.Nest(Items((80, 40)), 50, null, 0, 0, allowRotation: false);
            Assert.Equal("P1", Assert.Single(forbidden.Unplaced).Name);
            Assert.Equal(0, forbidden.UsedLength);
        }

        [Fact]
        public void PageMode_ListsWhatDidNotFit_InInputOrder()
        {
            var result = RectangleNester.Nest(Items((150, 20), (60, 60), (60, 60), (40, 40)), 100, 100, 0, 0, allowRotation: true);

            Assert.Equal(new[] { "P1", "P3" }, result.Unplaced.Select(i => i.Name));
            Assert.Equal(new[] { "P2", "P4" }, result.Placements.Select(p => p.Item.Name));
        }

        [Fact]
        public void ManyPieces_NeverOverlap_StayInside_AndRepeatTheSameResult()
        {
            var random = new Random(7);
            var items = Enumerable.Range(0, 120).Select(i => new NestItem(i, $"P{i}", 20 + random.Next(0, 200), 15 + random.Next(0, 120))).ToList();
            const double width = 1600, margin = 5, spacing = 4;

            var result = RectangleNester.Nest(items, width, null, margin, spacing, allowRotation: true);

            Assert.Empty(result.Unplaced);
            Assert.Equal(items.Count, result.Placements.Count);
            foreach (var p in result.Placements)
            {
                Assert.True(p.X >= margin - 1e-6 && p.X + p.Width <= width - margin + 1e-6, $"{p.Item.Name} fora da largura");
                Assert.True(p.Y >= margin - 1e-6 && p.Y + p.Height <= result.UsedLength - margin + 1e-6, $"{p.Item.Name} fora do comprimento");
            }

            var list = result.Placements.ToList();
            for (var i = 0; i < list.Count; i++)
                for (var j = i + 1; j < list.Count; j++)
                {
                    var a = list[i];
                    var b = list[j];
                    var apart = a.X + a.Width + spacing <= b.X + 1e-6 || b.X + b.Width + spacing <= a.X + 1e-6 ||
                                a.Y + a.Height + spacing <= b.Y + 1e-6 || b.Y + b.Height + spacing <= a.Y + 1e-6;
                    Assert.True(apart, $"{a.Item.Name} e {b.Item.Name} sem a distância");
                }

            Assert.InRange(result.Utilization, 0.6, 1);
            var again = RectangleNester.Nest(items, width, null, margin, spacing, allowRotation: true);
            Assert.Equal(result.Placements.Select(p => (p.X, p.Y, p.Rotated)), again.Placements.Select(p => (p.X, p.Y, p.Rotated)));
        }

        [Theory]
        [InlineData(0, 10, 0, 0, "A largura da área precisa ter medida maior que zero.")]
        [InlineData(100, 10, -1, 0, "A margem precisa ser 0 ou mais.")]
        [InlineData(100, 10, 0, -2, "A distância entre as peças precisa ser 0 ou mais.")]
        [InlineData(100, 10, 5, 0, "A margem não deixa espaço para as peças.")]
        public void InvalidInput_HasAMessageForTheUser(double width, double height, double margin, double spacing, string message)
        {
            var ex = Assert.Throws<ArgumentException>(() => RectangleNester.Nest(Items((5, 5)), width, height, margin, spacing, true));
            Assert.Equal(message, ex.Message);
            Assert.Equal("Selecione as peças que vão ser encaixadas.",
                Assert.Throws<ArgumentException>(() => RectangleNester.Nest(new List<NestItem>(), 100, null, 0, 0, true)).Message);
        }
    }
}
