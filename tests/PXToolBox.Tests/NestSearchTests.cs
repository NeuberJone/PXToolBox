using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PXToolBox.Core.Nesting;
using Xunit;
using Rotations = PXToolBox.Core.Nesting.ContourNester.Rotations;

namespace PXToolBox.Tests
{
    public class NestSearchTests
    {
        /// <summary>Uma régua 10 × 100 e quatro quadrados 50 × 50 numa área 100 × 100: pelo maior lado a régua
        /// entra primeiro e só cabem 2 quadrados; pela área os 4 quadrados enchem tudo.</summary>
        private static NestItem[] RulerAndSquares() => new[]
        {
            new NestItem(0, "Régua", 10, 100),
            new NestItem(1, "Q1", 50, 50), new NestItem(2, "Q2", 50, 50), new NestItem(3, "Q3", 50, 50), new NestItem(4, "Q4", 50, 50),
        };

        [Fact]
        public void DefaultOrder_IsUnchanged()
        {
            var result = RectangleNester.Nest(RulerAndSquares(), 100, 100, 0, 0, false);

            Assert.Equal(new[] { "Régua", "Q1", "Q2" }, result.Placements.Select(p => p.Item.Name));
            Assert.Equal(new[] { "Q3", "Q4" }, result.Unplaced.Select(i => i.Name));
        }

        [Fact]
        public void Search_KeepsTheOrderThatFitsMorePieces()
        {
            var items = RulerAndSquares();

            var found = NestSearch.Best(NestOrders.All, (order, _) => RectangleNester.Nest(items, 100, 100, 0, 0, false, order),
                NestScore.Of, TimeSpan.FromSeconds(30));

            Assert.Equal((5, 5, false, false), (found.Tried, found.Total, found.TimedOut, found.Cancelled));
            Assert.Equal(NestOrder.Area, found.Order); // a 1ª ordem que encaixa 4
            Assert.Equal(4, found.Result.Placements.Count);
            Assert.Equal("Régua", Assert.Single(found.Result.Unplaced).Name);
            Assert.Equal("maior área primeiro", NestOrders.Name(found.Order));
        }

        [Fact]
        public void Score_PrefersMorePieces_ThenMoreArea_ThenFewerAreas_ThenShorterLength()
        {
            Assert.True(new NestScore(3, 1, 9, 900).IsBetterThan(new NestScore(2, 99, 1, 1)));
            Assert.True(new NestScore(2, 100, 9, 900).IsBetterThan(new NestScore(2, 99, 1, 1)));
            Assert.True(new NestScore(2, 100, 1, 900).IsBetterThan(new NestScore(2, 100, 2, 1)));
            Assert.True(new NestScore(2, 100, 1, 899).IsBetterThan(new NestScore(2, 100, 1, 900)));
            Assert.False(new NestScore(2, 100, 1, 900).IsBetterThan(new NestScore(2, 100, 1, 900))); // empate: fica a primeira
        }

        [Fact]
        public void TimeLimit_InterruptsALaterTry_AndKeepsTheBestSoFar()
        {
            var tries = new List<NestOrder>();

            var found = NestSearch.Best(NestOrders.All, (order, cancel) =>
            {
                tries.Add(order);
                if (order != NestOrder.LongestSide)
                {
                    cancel.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                    cancel.ThrowIfCancellationRequested();
                }

                return order;
            }, order => new NestScore(order == NestOrder.LongestSide ? 1 : 9, 0, 1, 0), TimeSpan.FromMilliseconds(100));

            Assert.Equal((1, true, false), (found.Tried, found.TimedOut, found.Cancelled));
            Assert.Equal(NestOrder.LongestSide, found.Result);
            Assert.Equal(new[] { NestOrder.LongestSide, NestOrder.Area }, tries);
        }

        [Fact]
        public void CancellingAfterTheFirstTry_KeepsIt_ButCancellingTheFirstTryGivesNoResult()
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            var kept = NestSearch.Best(NestOrders.All, (order, _) => order, _ => new NestScore(1, 0, 1, 0), TimeSpan.FromSeconds(30), cancelled.Token);
            Assert.Equal((1, true, NestOrder.LongestSide), (kept.Tried, kept.Cancelled, kept.Result));

            var piece = new ContourPiece(0, "Q", new IReadOnlyList<IReadOnlyList<NestPoint>>[]
            {
                new[] { new[] { new NestPoint(0, 0), new NestPoint(10, 0), new NestPoint(10, 10), new NestPoint(0, 10) } },
            });
            Assert.ThrowsAny<OperationCanceledException>(() => NestSearch.Best(NestOrders.All,
                (order, cancel) => ContourNester.Nest(new[] { piece }, 100, null, 0, 0, Rotations.None, 5, order, cancel),
                NestScore.Of, TimeSpan.FromSeconds(30), cancelled.Token));
        }

        [Fact]
        public void Search_WorksForAreasToo()
        {
            var items = RulerAndSquares();
            var areas = new[] { new NestArea("A", 100, 100), new NestArea("B", 20, 100) };

            var found = NestSearch.Best(NestOrders.All, (order, _) => NestAreas.Nest(items, areas, 0, 0, false, order),
                NestScore.Of, TimeSpan.FromSeconds(30));

            // Pelo maior lado: régua + 2 quadrados em A, B não comporta quadrado → 3. Pela área: 4 em A e a régua em B → 5.
            Assert.Equal(NestOrder.Area, found.Order);
            Assert.Equal(5, found.Result.Fills.Sum(f => f.Result.Placements.Count));
            Assert.Empty(found.Result.Unplaced);
        }
    }
}
