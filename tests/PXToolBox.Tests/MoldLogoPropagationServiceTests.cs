using System.Linq;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class MoldLogoPropagationServiceTests
    {
        private readonly FakeArrangement _arrangement = new FakeArrangement();
        private readonly FakeLayer _layer = new FakeLayer { Name = "Camada 1" };

        /// <summary>Grupo de tamanho montado (TAG, logos, peça, x), com a peça de centro (x, y).</summary>
        private FakeShape SizeGroup(string size, double x, double y, double width, double height, string piece = "Frente",
            params (string Name, double X, double Y, double W, double H)[] logos)
        {
            var group = _layer.AddShape(new FakeShape { Name = size, Kind = ShapeKind.Group });
            void Add(string name, double cx, double cy, double w, double h) =>
                group.ShapesList.Add(new FakeShape { Name = name, Kind = ShapeKind.Curve, CenterX = cx, CenterY = cy, Width = w, Height = h, ParentGroup = group, CurrentLayer = _layer });
            Add("TAG", x, y - height / 2 + 5, 20, 10);
            foreach (var logo in logos) Add(logo.Name, logo.X, logo.Y, logo.W, logo.H);
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
        public void LogosOfTheFullestGroup_GoToTheOthers_ScaledByThePieceHeight()
        {
            // G: peça 100×200 no centro (50, 100). M: metade do tamanho, no centro (300, 50), com um L1 fora do lugar.
            var g = SizeGroup("G", 50, 100, 100, 200, "Frente", ("L1", 30, 150, 10, 10), ("L2", 70, 50, 20, 10));
            var m = SizeGroup("M", 300, 50, 50, 100, "Frente", ("L1", 280, 90, 3, 3));
            var p = SizeGroup("P", 500, 40, 40, 80);
            var m1 = Child(m, "L1");

            var result = new MoldLogoPropagationService().Propagate(Select(m, g, p), _arrangement, string.Empty);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(new[] { "TAG", "L1", "L2", "Frente", "x" }, g.Shapes.Select(s => s.Name));

            Assert.Equal((290.0, 75.0, 5.0, 5.0), (m1.CenterX, m1.CenterY, m1.Width, m1.Height));

            var newM = (FakeShape)_layer.ShapesList.Single(s => s.Name == "M");
            Assert.NotSame(m, newM);
            Assert.Equal("TAG", newM.Shapes[0].Name);
            Assert.Same(m1, Child(newM, "L1"));
            var m2 = Child(newM, "L2");
            Assert.Equal((310.0, 25.0, 10.0, 5.0), (m2.CenterX, m2.CenterY, m2.Width, m2.Height));
            Assert.True(newM.Shapes.ToList().IndexOf(m2) < newM.Shapes.ToList().FindIndex(s => s.Name == "Frente"));

            var newP = (FakeShape)_layer.ShapesList.Single(s => s.Name == "P");
            var p1 = Child(newP, "L1");
            Assert.Equal((500 - 20 * 0.4, 40 + 50 * 0.4, 4.0, 4.0), (p1.CenterX, p1.CenterY, p1.Width, p1.Height));

            Assert.Equal(new[] { "P", "M", "G" }, _arrangement.Selected.Select(s => s.Name));
            Assert.Contains("Logos do grupo \"G\" (L1, L2) propagados: M — 1 novo(s), 1 ajustado(s); P — 2 novo(s), 0 ajustado(s).", result.Message);
        }

        [Fact]
        public void Tie_IsSettledByThePanelSize_OrRefused()
        {
            var g = SizeGroup("G", 50, 100, 100, 200, "Frente", ("L1", 30, 150, 10, 10));
            var m = SizeGroup("M", 300, 50, 50, 100, "Frente", ("L1", 280, 90, 3, 3));
            var service = new MoldLogoPropagationService();

            var refused = service.Propagate(Select(g, m), _arrangement, "P");
            Assert.False(refused.IsSuccess);
            Assert.Contains("Mais de um grupo tem todos os 1 logos (G, M)", refused.Message);
            Assert.Equal(3.0, Child(m, "L1").Width);

            var result = service.Propagate(Select(g, m), _arrangement, "g");
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal((290.0, 75.0, 5.0), (Child(m, "L1").CenterX, Child(m, "L1").CenterY, Child(m, "L1").Width));
            Assert.Same(m, _layer.ShapesList.Single(s => s.Name == "M"));
        }

        [Fact]
        public void Refusals_ChangeNothing()
        {
            var g = SizeGroup("G", 50, 100, 100, 200, "Frente", ("L1", 30, 150, 10, 10));
            var m = SizeGroup("M", 300, 50, 50, 100, "Costas");
            var loose = _layer.AddShape(new FakeShape { Name = "Curva", Kind = ShapeKind.Curve, Width = 10, Height = 10 });
            var service = new MoldLogoPropagationService();

            Assert.Contains("pelo menos dois grupos", service.Propagate(Select(g), _arrangement, "G").Message);
            Assert.Contains("\"Curva\" não é um grupo de tamanho", service.Propagate(Select(g, loose), _arrangement, "G").Message);
            var different = service.Propagate(Select(g, m), _arrangement, "G");
            Assert.Contains("peças diferentes (Frente, Costas)", different.Message);
            Assert.EndsWith("Nada foi alterado.", different.Message);
            Assert.DoesNotContain(m.Shapes, s => s.Name == "L1");
            Assert.Equal(3, _layer.ShapesList.Count);
        }
    }
}
