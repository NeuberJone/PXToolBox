using System.Linq;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;
using Direction = PXToolBox.Outputs.Tools.MoldDuplicationService.CopyDirection;

namespace PXToolBox.Tests
{
    public class MoldDuplicationServiceTests
    {
        private readonly FakeArrangement _arrangement = new FakeArrangement();
        private readonly FakePage _page = new FakePage();
        private readonly FakeLayer _layer = new FakeLayer { Name = "Camada 1" };

        public MoldDuplicationServiceTests() => _page.LayersList.Add(_layer);

        /// <summary>Grupo de tamanho montado (TAG, logos, peça, x), com a peça de centro (x, y).</summary>
        private FakeShape SizeGroup(string size, double x, double y, double width, double height, string piece, params string[] logos)
        {
            var group = _layer.AddShape(new FakeShape { Name = size, Kind = ShapeKind.Group, CenterX = x, CenterY = y, Width = width, Height = height });
            void Add(string name, double cx, double cy, double w, double h) =>
                group.ShapesList.Add(new FakeShape { Name = name, Kind = ShapeKind.Curve, CenterX = cx, CenterY = cy, Width = w, Height = h, ParentGroup = group, CurrentLayer = _layer });
            Add("TAG", x, y - height / 2 + 5, 20, 10);
            for (var i = 0; i < logos.Length; i++) Add(logos[i], x - 20 + 10 * i, y + 30, 8, 8);
            Add(piece, x, y, width, height);
            Add("x", x, y, width, height);
            return group;
        }

        private static FakeSelection Select(params ICorelShape[] shapes)
        {
            var selection = new FakeSelection();
            selection.ShapesList.AddRange(shapes);
            return selection;
        }

        private static FakeShape Child(ICorelShape group, string name) => (FakeShape)group.Shapes.Single(s => s.Name == name);

        [Fact]
        public void EachSize_GetsAMirroredCopyBesideIt_WithNewLogoNumbers()
        {
            var g = SizeGroup("G", 500, 100, 100, 200, "Frente", "L1", "L2");
            var m = SizeGroup("M", 800, 80, 80, 160, "Frente", "L1", "L2");
            SizeGroup("G", 1500, 100, 100, 200, "Costas", "L7");

            var result = new MoldDuplicationService().Duplicate(Select(g, m), _page, _arrangement, " Frente2 ", Direction.Left, 10, mirror: true);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(new[] { "G", "M" }, _arrangement.Selected.Select(s => s.Name));
            var copyG = _arrangement.Selected[0];
            Assert.Equal(new[] { "TAG", "L8", "L9", "Frente2", "x" }, copyG.Shapes.Select(s => s.Name));
            Assert.Equal((390.0, 100.0), (Child(copyG, "Frente2").CenterX, Child(copyG, "Frente2").CenterY));
            Assert.Equal(390.0, Child(copyG, "x").CenterX);
            Assert.Equal((1, 1, 0, 0), (Child(copyG, "Frente2").Flips, Child(copyG, "x").Flips, Child(copyG, "TAG").Flips, Child(copyG, "L8").Flips));
            Assert.Equal(390.0 - 20, Child(copyG, "L8").CenterX);
            Assert.Equal(710.0, Child(_arrangement.Selected[1], "Frente2").CenterX);

            Assert.Equal(new[] { "TAG", "L1", "L2", "Frente", "x" }, g.Shapes.Select(s => s.Name));
            Assert.Equal(500.0, Child(g, "Frente").CenterX);
            Assert.Equal(0, Child(g, "Frente").Flips);
            Assert.Contains("Cópia \"Frente2\" de 2 tamanho(s) (G, M), à esquerda, espelhada. Logos: L1 → L8, L2 → L9.", result.Message);
        }

        [Fact]
        public void AgainWithTheSameName_ReplacesThePreviousCopy_AndKeepsItsNumbers()
        {
            var g = SizeGroup("G", 500, 100, 100, 200, "Frente", "L1", "L2");
            var service = new MoldDuplicationService();
            Assert.True(service.Duplicate(Select(g), _page, _arrangement, "Frente2", Direction.Left, 10, mirror: false).IsSuccess);
            SizeGroup("M", 1500, 100, 100, 200, "Costas", "L20");

            var result = service.Duplicate(Select(g), _page, _arrangement, "Frente2", Direction.Below, 0, mirror: false);

            Assert.True(result.IsSuccess, result.Message);
            var copies = _layer.ShapesList.Where(s => s.Shapes.Any(c => c.Name == "Frente2")).ToList();
            var copy = Assert.Single(copies);
            Assert.Equal(new[] { "TAG", "L3", "L4", "Frente2", "x" }, copy.Shapes.Select(s => s.Name));
            Assert.Equal((500.0, -100.0), (Child(copy, "Frente2").CenterX, Child(copy, "Frente2").CenterY));
            Assert.Contains("1 cópia(s) anterior(es) substituída(s)", result.Message);
        }

        [Fact]
        public void Refusals_ChangeNothing()
        {
            var g = SizeGroup("G", 500, 100, 100, 200, "Frente", "L1");
            var loose = _layer.AddShape(new FakeShape { Name = "Curva", Kind = ShapeKind.Curve, Width = 10, Height = 10 });
            var service = new MoldDuplicationService();

            Assert.Contains("Informe o nome da cópia", service.Duplicate(Select(g), _page, _arrangement, " ", Direction.Left, 10, false).Message);
            Assert.Contains("ponto nem espaço", service.Duplicate(Select(g), _page, _arrangement, "Frente 2", Direction.Left, 10, false).Message);
            Assert.Contains("diferente do nome da peça", service.Duplicate(Select(g), _page, _arrangement, "FRENTE", Direction.Left, 10, false).Message);
            var notGroup = service.Duplicate(Select(g, loose), _page, _arrangement, "Frente2", Direction.Left, 10, false);
            Assert.Contains("\"Curva\" não é um grupo de tamanho", notGroup.Message);
            Assert.EndsWith("Nada foi alterado.", notGroup.Message);
            Assert.Equal(2, _layer.ShapesList.Count);
        }
    }
}
