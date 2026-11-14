using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Slicing;
using Xunit;

namespace PXToolBox.Tests
{
    /// <summary>Divisão por linhas desenhadas (<see cref="SlicePathPlanner"/>).</summary>
    public class SlicePathTests
    {
        private static IReadOnlyList<SlicePoint> Path(params double[] xy) =>
            Enumerable.Range(0, xy.Length / 2).Select(i => new SlicePoint(xy[2 * i], xy[2 * i + 1])).ToList();

        private static (double MinX, double MinY, double MaxX, double MaxY) Box(SliceRegion region) =>
            (Math.Round(region.MinX, 6), Math.Round(region.MinY, 6), Math.Round(region.MinX + region.Width, 6), Math.Round(region.MinY + region.Height, 6));

        [Fact]
        public void ATCut_StopsAtTheOtherCut_AndMakesThreeParts()
        {
            // Arte 100 × 60: corte vertical inteiro em x 50; horizontal da borda esquerda até 1 mm antes dele.
            var plan = SlicePathPlanner.ByPaths(100, 60, new[] { Path(50, -5, 50, 65), Path(0, 30, 49, 30) }, 0);

            Assert.Equal(3, plan.Regions.Count);
            Assert.Equal(0, plan.Outside);
            Assert.Equal(new[] { (0.0, 0.0, 50.0, 30.0), (50.0, 0.0, 100.0, 60.0), (0.0, 30.0, 50.0, 60.0) }, plan.Regions.Select(Box));
            Assert.Equal(new[] { 1500.0, 3000, 1500 }, plan.Regions.Select(r => Math.Round(r.Area, 6)));
            Assert.Equal(2, plan.Paths.Count);
            Assert.Empty(plan.Lines);
        }

        [Fact]
        public void Overlap_GoesHalfPastEachCutThatBoundsThePart_NotPastTheArtEdges()
        {
            var plan = SlicePathPlanner.ByPaths(100, 60, new[] { Path(50, -5, 50, 65), Path(0, 30, 49, 30) }, 10);

            Assert.Equal(new[] { (0.0, 0.0, 55.0, 35.0), (45.0, 0.0, 100.0, 60.0), (0.0, 25.0, 55.0, 60.0) }, plan.Regions.Select(Box));
            // A área informada é a da parte sem sobreposição.
            Assert.Equal(new[] { 1500.0, 3000, 1500 }, plan.Regions.Select(r => Math.Round(r.Area, 6)));
        }

        [Fact]
        public void StraightPaths_GiveTheSamePartsAsTheTable()
        {
            var table = SlicePlanner.ByTable(120, 80, 2, 2, 6);

            var plan = SlicePathPlanner.ByPaths(120, 80, new[] { Path(60, 0, 60, 80), Path(0, 40, 120, 40) }, 6);

            Assert.Equal(table.Tiles.Select(t => (t.X, t.Y, t.X + t.Width, t.Y + t.Height)), plan.Regions.Select(Box));
        }

        [Fact]
        public void ACurvedCut_SplitsTheArtInTwo_WithNothingLost()
        {
            // Arco de cima a baixo, curvado para a direita (como uma Bézier já em retas).
            var arc = Enumerable.Range(0, 25)
                .Select(k => k / 24.0)
                .Select(t => new SlicePoint(40 + 30 * Math.Sin(Math.PI * t), -2 + 84 * t))
                .ToList();

            var plan = SlicePathPlanner.ByPaths(100, 80, new[] { arc }, 0);

            Assert.Equal(2, plan.Regions.Count);
            Assert.Equal(8000, plan.Regions.Sum(r => r.Area), 6);
            Assert.True(plan.Regions[0].Points.Count > 10, "a parte segue a curva");
            var left = plan.Regions.Single(r => r.MinX < 1);
            Assert.Equal(70, left.MinX + left.Width, 0); // o ponto mais à direita da curva

            var overlapped = SlicePathPlanner.ByPaths(100, 80, new[] { arc }, 8);
            var leftWithOverlap = overlapped.Regions.Single(r => r.MinX < 1);
            Assert.Equal(74, leftWithOverlap.MinX + leftWithOverlap.Width, 0);
            Assert.Equal(0, leftWithOverlap.MinY, 6);
            Assert.Equal(80, leftWithOverlap.MinY + leftWithOverlap.Height, 6);
        }

        [Fact]
        public void LoosePaths_AreCounted_AndNothingThatDividesIsRefused()
        {
            var loose = Path(30, 30, 60, 30);   // solta no meio da arte
            var outside = Path(200, 0, 200, 50);

            var error = Assert.Throws<ArgumentException>(() => SlicePathPlanner.ByPaths(100, 60, new[] { loose, outside }, 0));
            Assert.Equal("Nenhuma linha desenhada divide a arte: cada corte precisa ir de uma borda da arte (ou de outro corte) até outra borda ou corte.", error.Message);

            var plan = SlicePathPlanner.ByPaths(100, 60, new[] { loose, outside, Path(50, 0, 50, 60) }, 0);
            Assert.Equal(2, plan.Regions.Count);
            Assert.Equal(2, plan.Outside);

            Assert.Equal("Nenhuma linha de corte desenhada.",
                Assert.Throws<ArgumentException>(() => SlicePathPlanner.ByPaths(100, 60, new[] { Path(5, 5) }, 0)).Message);
        }

        [Fact]
        public void ACutThatCrossesTheArtTwice_MakesThreeParts()
        {
            // "U" que entra pela borda de cima, desce e volta a sair por cima: o miolo é uma parte só.
            var plan = SlicePathPlanner.ByPaths(100, 60, new[] { Path(30, -5, 30, 40, 70, 40, 70, -5) }, 0);

            Assert.Equal(2, plan.Regions.Count);
            Assert.Contains(plan.Regions, r => Math.Abs(r.Area - 40 * 40) < 1e-6);

            // Uma linha que atravessa duas vezes (entra e sai por baixo também): 3 partes.
            var zigzag = SlicePathPlanner.ByPaths(100, 60, new[] { Path(20, -5, 20, 65), Path(80, -5, 80, 65) }, 0);
            Assert.Equal(3, zigzag.Regions.Count);
        }

        [Fact]
        public void RandomCurvedCuts_NeverLoseOrDoubleArea()
        {
            var random = new Random(7);
            var checkedPlans = 0;
            for (var round = 0; round < 60; round++)
            {
                // 1 a 4 linhas onduladas de borda a borda, em direções variadas, e às vezes um "T" que para no meio.
                var paths = new List<IReadOnlyList<SlicePoint>>();
                var count = 1 + random.Next(4);
                for (var k = 0; k < count; k++)
                {
                    var vertical = random.Next(2) == 0;
                    var at = 20 + random.NextDouble() * 160;
                    var wave = random.NextDouble() * 15;
                    paths.Add(Enumerable.Range(0, 21).Select(i => i / 20.0)
                        .Select(t => vertical
                            ? new SlicePoint(at + wave * Math.Sin(6 * t), -5 + 130 * t)
                            : new SlicePoint(-5 + 210 * t, at * 0.6 + wave * Math.Sin(6 * t)))
                        .ToList());
                }

                if (random.Next(2) == 0) paths.Add(Path(0, 60, 100 + random.NextDouble() * 50, 60));
                SliceRegionPlan plan;
                try
                {
                    plan = SlicePathPlanner.ByPaths(200, 120, paths, 0);
                }
                catch (ArgumentException ex) when (ex.Message.Contains("fina demais"))
                {
                    continue;
                }

                Assert.Equal(200 * 120, plan.Regions.Sum(r => r.Area), 3);
                checkedPlans++;
            }

            Assert.True(checkedPlans > 30, $"{checkedPlans} planos conferidos");
        }

        [Fact]
        public void ThinParts_AreRefused()
        {
            var error = Assert.Throws<ArgumentException>(() =>
                SlicePathPlanner.ByPaths(100, 60, new[] { Path(50, -5, 50, 65), Path(50.5, -5, 50.5, 65) }, 0));
            Assert.Contains("fina demais", error.Message);
        }
    }
}
