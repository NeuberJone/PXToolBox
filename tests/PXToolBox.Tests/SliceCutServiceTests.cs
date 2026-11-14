using System.Linq;
using PXToolBox.Core.Slicing;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class SliceCutServiceTests
    {
        private readonly FakePage _page = new FakePage();
        private readonly FakeLayer _layer = new FakeLayer { Name = "Camada 1" };

        public SliceCutServiceTests()
        {
            _page.LayersList.Add(_layer);
        }

        private FakeSelection Select(params FakeShape[] shapes)
        {
            var selection = new FakeSelection();
            foreach (var shape in shapes) selection.ShapesList.Add(_layer.AddShape(shape));
            return selection;
        }

        private static FakeShape Box(string name, double left, double bottom, double width, double height) =>
            new FakeShape { Name = name, Kind = ShapeKind.Curve, CenterX = left + width / 2, CenterY = bottom + height / 2, Width = width, Height = height };

        [Fact]
        public void EachTile_IsARectangleWithACopyOfTheArtInside_SpreadByTheSeparation()
        {
            // Documento em mm; arte de 200 × 100 mm com o canto inferior esquerdo em (100, 200).
            var art = Box("Arte", 100, 200, 150, 100);
            var text = Box("Texto", 250, 260, 50, 20);
            var selection = Select(art, text);
            var plan = SlicePlanner.ByTable(200, 100, 2, 2, 0);

            var result = new SliceCutService().Apply(selection, _page, 1, plan, 10, deleteOriginal: false);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("4 parte(s) (2 × 2) no grupo “Divisão (Slice)”; a arte original ficou oculta, no mesmo lugar. Ctrl+Z desfaz.", result.Message);
            var group = (FakeShape)_layer.Shapes.Single(s => s.Name == SliceCutService.GroupName);
            var parts = group.Shapes.Cast<FakeShape>().ToList();
            Assert.Equal(new[] { "Parte L1 C1", "Parte L1 C2", "Parte L2 C1", "Parte L2 C2" }, parts.Select(p => p.Name));
            Assert.All(parts, p => Assert.True(p.OutlineRemoved));
            Assert.All(parts, p => Assert.Equal((100.0, 50.0), (p.Width, p.Height)));

            // Linha 1 em cima (Y do documento para cima); coluna 2 e linha 2 afastadas 10 mm.
            Assert.Equal((150.0, 275.0), (parts[0].CenterX, parts[0].CenterY));
            Assert.Equal((260.0, 275.0), (parts[1].CenterX, parts[1].CenterY));
            Assert.Equal((150.0, 215.0), (parts[2].CenterX, parts[2].CenterY));
            Assert.Equal((260.0, 215.0), (parts[3].CenterX, parts[3].CenterY));

            // Dentro de cada parte: um grupo com cópias dos dois objetos.
            Assert.All(parts, p =>
            {
                var content = (FakeShape)p.PowerClipShapes.Single();
                Assert.Equal(new[] { "Arte", "Texto" }, content.Shapes.Select(s => s.Name));
            });

            Assert.False(art.Visible);
            Assert.False(text.Visible);
            Assert.False(art.IsDeleted);
            Assert.Equal(new[] { art, text }, _layer.Shapes.Where(s => s.Name != SliceCutService.GroupName));
        }

        [Fact]
        public void SingleObject_GoesInsideWithoutGroup_AndTheOriginalCanBeDeleted()
        {
            var art = Box("Arte", 0, 0, 100, 300);
            var plan = SlicePlanner.ByMaximumSize(100, 300, 100, 120, 20);

            var result = new SliceCutService().Apply(Select(art), _page, 1, plan, 0, deleteOriginal: true);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Contains("a arte original foi apagada", result.Message);
            var parts = ((FakeShape)_layer.Shapes.Single()).Shapes.Cast<FakeShape>().ToList();
            Assert.Equal(3, parts.Count);
            Assert.All(parts, p => Assert.Equal("Arte", p.PowerClipShapes.Single().Name));
            // Sem afastamento: no lugar da arte, avançando 100 mm (120 − 20 em comum); a última vai até a borda.
            Assert.Equal(new[] { 240.0, 140.0, 50.0 }, parts.Select(p => p.CenterY));
            Assert.Equal(new[] { 120.0, 120.0, 100.0 }, parts.Select(p => p.Height));
            Assert.True(art.IsDeleted);
        }

        [Fact]
        public void PlanOfAnotherArea_IsRefusedWithoutTouchingTheDocument()
        {
            var art = Box("Arte", 0, 0, 100, 50);
            var plan = SlicePlanner.ByTable(120, 50, 1, 2, 0);

            var result = new SliceCutService().Apply(Select(art), _page, 1, plan, 0, deleteOriginal: true);

            Assert.False(result.IsSuccess);
            Assert.Equal("A seleção atual tem 10 × 5 cm, mas o plano é de 12 × 5 cm. Selecione a arte da prévia ou clique em “Usar seleção atual”. Nada foi alterado.", result.Message);
            Assert.Equal(new[] { art }, _layer.Shapes);
            Assert.True(art.Visible);
        }

        [Fact]
        public void FailureInTheMiddle_RemovesTheCreatedParts_AndKeepsTheOriginal()
        {
            var art = Box("Arte", 0, 0, 300, 100);
            _layer.RectanglesBeforeFailure = 2;

            var result = new SliceCutService().Apply(Select(art), _page, 1, SlicePlanner.ByTable(300, 100, 1, 3, 0), 5, deleteOriginal: true);

            Assert.False(result.IsSuccess);
            Assert.Contains("a arte original não mudou", result.Message);
            Assert.Equal(new[] { art }, _layer.Shapes);
            Assert.True(art.Visible);
            Assert.False(art.IsDeleted);
        }
    }
}
