using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Nesting;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;
using Kind = PXToolBox.Outputs.Abstractions.OutlineElementKind;

namespace PXToolBox.Tests
{
    public class NestQuantitiesTests
    {
        [Fact]
        public void Expand_RepeatsEachPiece_NamesTheCopies_AndKeepsTheSource()
        {
            var items = new[] { new NestItem(0, "Frente", 50, 70), new NestItem(1, "Manga", 20, 20), new NestItem(2, "Gola", 40, 5) };

            var (expanded, sources) = NestQuantities.Expand(items, new[] { 2, 0, 1 });

            Assert.Equal(new[] { "Frente (1/2)", "Frente (2/2)", "Gola" }, expanded.Select(i => i.Name));
            Assert.Equal(new[] { 0, 1, 2 }, expanded.Select(i => i.Index));
            Assert.Equal(new[] { 0, 0, 2 }, sources);
            Assert.Equal("Todas as quantidades estão em 0: nada para encaixar.",
                Assert.Throws<ArgumentException>(() => NestQuantities.Expand(items, new[] { 0, 0, 0 })).Message);
            Assert.Throws<ArgumentException>(() => NestQuantities.Expand(items, new[] { 1, 1000, 1 }));
        }

        private readonly FakePage _page = new FakePage { Width = 200, Height = 300 };
        private readonly FakeLayer _layer = new FakeLayer { Name = "Camada 1" };

        public NestQuantitiesTests()
        {
            _page.LayersList.Add(_layer);
        }

        private FakeSelection Select(params FakeShape[] shapes)
        {
            var selection = new FakeSelection();
            foreach (var shape in shapes) selection.ShapesList.Add(_layer.AddShape(shape));
            return selection;
        }

        [Fact]
        public void BoxNesting_DuplicatesTheOriginalForEachExtraCopy_OnlyForWhatFits()
        {
            // Página 200 × 300, peças de 90 × 140: cabem 4 (2 × 2); a 5ª cópia não é criada.
            var piece = new FakeShape { Name = "Frente", Kind = ShapeKind.Group, Width = 90, Height = 140, CenterX = 900, CenterY = 900 };
            var selection = Select(piece);
            var service = new NestingService();
            var items = service.ReadItems(selection, 1);
            var (expanded, sources) = NestQuantities.Expand(items, new[] { 5 });
            var plan = RectangleNester.Nest(expanded, 200, 300, 0, 0, allowRotation: false);

            var result = service.Apply(selection, _page, 1, items, plan, false, sources);

            Assert.True(result.IsSuccess, result.Message);
            Assert.StartsWith("4 peça(s) encaixada(s) (3 cópia(s) criada(s))", result.Message);
            Assert.Contains("Não couberam (ficaram onde estavam): Frente (5/5)", result.Message);
            Assert.Equal(3, piece.Duplicates.Count);
            var centers = new[] { piece }.Concat(piece.Duplicates).Select(s => (s.CenterX, s.CenterY)).OrderBy(c => c).ToList();
            Assert.Equal(new[] { (45.0, 90.0), (45.0, 230.0), (135.0, 90.0), (135.0, 230.0) }, centers); // linhas: 0–140 e 140–280 a partir do topo (300)
        }

        [Fact]
        public void ContourNesting_CopiesAreMadeBeforeTurning_SoEachTurnsOnlyOnce()
        {
            // L de 100 × 100 (coluna à esquerda, barra embaixo no documento), 2 cópias com meia-volta.
            var shape = new FakeShape
            {
                Name = "L", Kind = ShapeKind.Curve, Width = 100, Height = 100, CenterX = 50, CenterY = 50,
                OutlineElements = new List<OutlineElement>
                {
                    new OutlineElement(Kind.Start, 0, 0), new OutlineElement(Kind.Line, 100, 0), new OutlineElement(Kind.Line, 100, 30),
                    new OutlineElement(Kind.Line, 30, 30), new OutlineElement(Kind.Line, 30, 100), new OutlineElement(Kind.Line, 0, 100),
                },
            };
            var selection = Select(shape);
            var service = new NestingService();
            var reading = service.ReadContours(selection, 1);
            var (pieces, sources) = NestQuantities.Expand(reading.Pieces, new[] { 2 });
            var plan = ContourNester.Nest(pieces, 140, null, 0, 0, ContourNester.Rotations.HalfTurn, 5);
            Assert.Equal(new[] { 0, 180 }, plan.Placements.Select(p => p.Rotation));

            var result = service.ApplyContour(selection, _page, 1, reading, plan, 0, 0, false, sources);

            Assert.True(result.IsSuccess, result.Message);
            var copy = Assert.Single(shape.Duplicates);
            Assert.Equal(0, shape.RotationAngle);
            Assert.Equal(180, copy.RotationAngle);
            Assert.Equal((50.0, 250.0), (shape.CenterX, shape.CenterY));
            Assert.Equal((85.0, 250.0), (copy.CenterX, copy.CenterY));
            Assert.Contains("(1 cópia(s) criada(s))", result.Message);
        }
    }
}
