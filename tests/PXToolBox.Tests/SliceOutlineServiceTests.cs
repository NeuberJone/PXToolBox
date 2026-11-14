using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;
using Kind = PXToolBox.Outputs.Abstractions.OutlineElementKind;

namespace PXToolBox.Tests
{
    public class SliceOutlineServiceTests
    {
        private readonly FakeLayer _layer = new FakeLayer { Name = "Camada 1" };
        private readonly SliceOutlineService _service = new SliceOutlineService(new NestingService());

        private FakeSelection Select(params FakeShape[] shapes)
        {
            var selection = new FakeSelection();
            foreach (var shape in shapes) selection.ShapesList.Add(_layer.AddShape(shape));
            return selection;
        }

        private static List<OutlineElement> Polyline(params double[] xy) =>
            Enumerable.Range(0, xy.Length / 2)
                .Select(i => new OutlineElement(i == 0 ? Kind.Start : Kind.Line, xy[2 * i], xy[2 * i + 1])).ToList();

        [Fact]
        public void Read_GivesRingsInMillimeters_FromTheTopLeftOfTheSelection()
        {
            // Documento em unidades de 2 mm, Y para cima. Triângulo com base de (100, 50) a (300, 50) e topo em
            // (200, 150); texto de 20 × 10 com centro em (320, 140): a seleção vai de x 100 a 330 e y 50 a 150.
            var triangle = new FakeShape
            {
                Name = "Triângulo", Kind = ShapeKind.Curve, Width = 200, Height = 100, CenterX = 200, CenterY = 100,
                OutlineElements = Polyline(100, 50, 300, 50, 200, 150),
            };
            var text = new FakeShape { Name = "Texto", Kind = ShapeKind.Text, Width = 20, Height = 10, CenterX = 320, CenterY = 140 };

            var result = _service.Read(Select(triangle, text), 2);

            Assert.True(result.IsSuccess);
            var outline = result.Value!;
            Assert.Equal((115.0, 50.0), (outline.Width, outline.Height));
            Assert.True(outline.Approximate); // o texto entrou pela caixa
            Assert.False(outline.Trimmed);
            Assert.Equal(2, outline.Rings.Count);
            Assert.Equal(new[] { (0.0, 50.0), (100.0, 50.0), (50.0, 0.0) }, outline.Rings[0].Select(p => (p.X, p.Y)));
            Assert.Equal((105.0, 2.5), (outline.Rings[1].Min(p => p.X), outline.Rings[1].Min(p => p.Y)));
            Assert.Equal((115.0, 7.5), (outline.Rings[1].Max(p => p.X), outline.Rings[1].Max(p => p.Y)));
        }

        [Fact]
        public void Read_StopsAtTheLimitOfPoints_AndSaysSo()
        {
            var shapes = Enumerable.Range(0, SliceOutlineService.MaximumPoints / 4 + 1).Select(k => new FakeShape
            {
                Name = $"Q{k}", Kind = ShapeKind.Curve, Width = 1, Height = 1, CenterX = k + 0.5, CenterY = 0.5,
                OutlineElements = Polyline(k, 0, k + 1, 0, k + 1, 1, k, 1),
            }).ToArray();

            var outline = _service.Read(Select(shapes), 1).Value!;

            Assert.True(outline.Trimmed);
            Assert.Equal(SliceOutlineService.MaximumPoints / 4, outline.Rings.Count);
        }

        [Fact]
        public void Read_WithoutSelection_Fails()
        {
            var result = _service.Read(new FakeSelection(), 1);

            Assert.Equal(OperationStatus.InvalidSelection, result.Status);
            Assert.Equal("Selecione a arte que vai ser dividida.", result.Message);
        }
    }
}
