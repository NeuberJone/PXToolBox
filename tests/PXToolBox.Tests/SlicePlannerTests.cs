using System;
using System.Linq;
using PXToolBox.Core.Slicing;
using Xunit;

namespace PXToolBox.Tests
{
    public sealed class SlicePlannerTests
    {
        [Fact]
        public void Table_SharesSpecifiedBandAndKeepsExternalBounds()
        {
            var plan = SlicePlanner.ByTable(2000, 1000, 2, 2, 15);
            Assert.Equal(4, plan.Tiles.Count);
            var first = plan.Tiles[0];
            var right = plan.Tiles[1];
            var bottom = plan.Tiles[2];
            Assert.Equal(0, first.X);
            Assert.Equal(0, first.Y);
            Assert.Equal(15, first.X + first.Width - right.X);
            Assert.Equal(15, first.Y + first.Height - bottom.Y);
            Assert.Equal(2000, right.X + right.Width);
            Assert.Equal(1000, bottom.Y + bottom.Height);
        }

        [Fact]
        public void MaximumSize_IncludesOverlapInLimitAndClipsLastPart()
        {
            var plan = SlicePlanner.ByMaximumSize(2100, 950, 1000, 600, 15);
            Assert.Equal(3, plan.Columns);
            Assert.Equal(2, plan.Rows);
            Assert.Equal(6, plan.Tiles.Count);
            Assert.Equal(985, plan.Tiles[1].X);
            Assert.Equal(130, plan.Tiles[2].Width);
            Assert.Equal(365, plan.Tiles[3].Height);
            Assert.All(plan.Tiles, t =>
            {
                Assert.InRange(t.Width, double.Epsilon, 1000);
                Assert.InRange(t.Height, double.Epsilon, 600);
                Assert.InRange(t.X + t.Width, 0, plan.Width);
                Assert.InRange(t.Y + t.Height, 0, plan.Height);
            });
        }

        [Fact]
        public void ExactFit_DoesNotProduceAnExtraStrip()
        {
            var plan = SlicePlanner.ByMaximumSize(1985, 600, 1000, 600, 15);
            Assert.Equal(2, plan.Tiles.Count);
            Assert.Equal(1000, plan.Tiles[1].Width);
        }

        [Fact]
        public void SinglePart_HasNoExternalBleed()
        {
            var plan = SlicePlanner.ByTable(100, 50, 1, 1, 15);
            Assert.Equal(100, plan.Tiles.Single().Width);
            Assert.Equal(50, plan.Tiles.Single().Height);
            Assert.Single(SlicePlanner.ByMaximumSize(100, 50, 200, 200, 15).Tiles);
        }

        [Fact]
        public void ZeroOverlap_TableCoversArtworkExactly()
        {
            var plan = SlicePlanner.ByTable(110, 70, 3, 4, 0);
            Assert.Equal(110 * 70, plan.Tiles.Sum(t => t.Width * t.Height), 8);
            Assert.Equal((3, 4), (plan.Tiles[11].Row, plan.Tiles[11].Column));
        }

        [Theory]
        [InlineData(0, 100, 0)]
        [InlineData(-1, 100, 0)]
        [InlineData(double.NaN, 100, 0)]
        [InlineData(100, double.PositiveInfinity, 0)]
        [InlineData(100, 100, -1)]
        [InlineData(100, 100, double.NaN)]
        public void InvalidLengths_AreRejected(double width, double height, double overlap)
        {
            Assert.Throws<ArgumentException>(() => SlicePlanner.ByTable(width, height, 2, 2, overlap));
            Assert.Throws<ArgumentException>(() => SlicePlanner.ByMaximumSize(width, height, 50, 50, overlap));
        }

        [Fact]
        public void ExcessiveOverlapAndCounts_AreRejectedBeforeAllocation()
        {
            Assert.Throws<ArgumentException>(() => SlicePlanner.ByTable(100, 100, 2, 2, 50));
            Assert.Throws<ArgumentException>(() => SlicePlanner.ByTable(100, 100, int.MaxValue, int.MaxValue, 0));
            Assert.Throws<ArgumentException>(() => SlicePlanner.ByTable(100, 100, 0, 1, 0));
            Assert.Throws<ArgumentException>(() => SlicePlanner.ByMaximumSize(100, 100, 50, 50, 50));
            Assert.Throws<ArgumentException>(() => SlicePlanner.ByMaximumSize(100, 100, 0, 50, 0));
            Assert.Throws<ArgumentException>(() => SlicePlanner.ByMaximumSize(100, 100, 1e-300, 1e-300, 0));
        }
    }
}
