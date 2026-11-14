using System.Linq;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;
using Axis = PXToolBox.Outputs.Tools.TapeMeasureService.Axis;

namespace PXToolBox.Tests
{
    public class TapeMeasureServiceTests
    {
        private readonly FakePage _page = new FakePage();
        private readonly FakeLayer _layer = new FakeLayer { Name = "Camada 1" };

        public TapeMeasureServiceTests()
        {
            _page.LayersList.Add(_layer);
        }

        private FakeSelection Select(params FakeShape[] shapes)
        {
            var selection = new FakeSelection();
            foreach (var shape in shapes) selection.ShapesList.Add(_layer.AddShape(shape));
            return selection;
        }

        private static FakeShape Box(double left, double bottom, double width, double height) =>
            new FakeShape { Name = "Peça", Kind = ShapeKind.Curve, CenterX = left + width / 2, CenterY = bottom + height / 2, Width = width, Height = height };

        private FakeLayer Ruler => _page.LayersList.Single(l => l.Name == TapeMeasureService.LayerName);

        private static FakeShape Part(FakeShape group, string name) => (FakeShape)group.Shapes.Single(s => s.Name == name);

        [Fact]
        public void WidthRuler_HasATickPerMillimeter_NumbersPerCentimeter_AndTheTotal()
        {
            // Documento em mm (1 mm = 1 unidade); seleção de 52,3 mm de largura, canto em (100, 200).
            var selection = Select(Box(100, 200, 30, 40), Box(120, 210, 32.3, 10));

            var result = new TapeMeasureService().Create(selection, _page, 1, Axis.Width);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("Régua criada na camada RÉGUA, a 3 mm da seleção: largura 5,23 cm (52,3 mm). Ctrl+Z desfaz.", result.Message);
            var group = (FakeShape)Ruler.Shapes.Single();
            Assert.Equal("Régua largura", group.Name);

            var ticks = Part(group, "Traços").Lines;
            Assert.Equal(53 + 1, ticks.Count); // 0…52 mm + o fim exato
            Assert.All(ticks, t => Assert.Equal(197, t.Y1, 6)); // 3 mm abaixo da seleção
            Assert.Equal((100.0, 192.0), (ticks[0].X2, ticks[0].Y2)); // cm: 5 mm
            Assert.Equal(197 - 2, ticks[1].Y2, 6); // mm: 2 mm
            Assert.Equal(197 - 3.5, ticks[5].Y2, 6); // meio cm: 3,5 mm
            Assert.Equal(152.3, ticks.Last().X1, 6);

            var texts = group.Shapes.Cast<FakeShape>().Where(s => s.Kind == ShapeKind.Text).ToList();
            Assert.Equal(new[] { "1", "2", "3", "4", "5", "5,23 cm (52,3 mm)" }, texts.Select(t => t.Text));
            Assert.Equal((110.0, 189.5), (texts[0].CenterX, texts[0].CenterY));
            Assert.True(texts.Last().CenterX - texts.Last().Width / 2 >= 152.3 + 2 - 1e-6);

            var band = Part(group, "Faixa");
            Assert.Equal(126.15, band.CenterX, 6);
            Assert.Equal(191.0, band.CenterY, 6);
            Assert.Equal(52.3, band.Width, 6);
            Assert.Equal(12.0, band.Height, 6);
            Assert.Equal((255, 255, 255), band.Fill);

            Assert.Equal(new[] { "Peça", "Peça" }, _layer.Shapes.Select(s => s.Name));
        }

        [Fact]
        public void HeightRuler_GoesUpOnTheLeft_InDocumentUnits()
        {
            // Documento em décimos de mm (1 mm = 10 unidades): 2 cm de altura.
            var selection = Select(Box(1000, 500, 300, 200));

            var result = new TapeMeasureService().Create(selection, _page, 10, Axis.Height);

            Assert.True(result.IsSuccess, result.Message);
            var group = (FakeShape)Ruler.Shapes.Single();
            Assert.Equal("Régua altura", group.Name);
            var ticks = Part(group, "Traços").Lines;
            Assert.Equal(21, ticks.Count);
            Assert.Equal((970.0, 500.0, 920.0, 500.0), (ticks[0].X1, ticks[0].Y1, ticks[0].X2, ticks[0].Y2));
            Assert.Equal((970.0, 700.0), (ticks[20].X1, ticks[20].Y1));
            var band = Part(group, "Faixa");
            Assert.Equal((910.0, 600.0, 120.0, 200.0), (band.CenterX, band.CenterY, band.Width, band.Height));
        }

        [Fact]
        public void Both_MakesTwoRulersFromTheSameCorner_AndReusesTheLayer()
        {
            _page.CreateLayer(TapeMeasureService.LayerName);
            var selection = Select(Box(0, 0, 40, 25));

            var result = new TapeMeasureService().Create(selection, _page, 1, Axis.Both);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Contains("largura 4 cm (40 mm) e altura 2,5 cm (25 mm)", result.Message);
            Assert.Single(_page.LayersList, l => l.Name == TapeMeasureService.LayerName);
            Assert.Equal(new[] { "Régua largura", "Régua altura" }, Ruler.Shapes.Select(s => s.Name));
        }

        [Fact]
        public void NothingSelected_OrTooSmall_IsRefused_WithoutCreatingTheLayer()
        {
            var service = new TapeMeasureService();

            var empty = service.Create(new FakeSelection(), _page, 1, Axis.Width);
            var thin = service.Create(Select(Box(0, 0, 50, 0.5)), _page, 1, Axis.Both);

            Assert.False(empty.IsSuccess);
            Assert.Equal("Selecione o que vai ser medido. Nada foi alterado.", empty.Message);
            Assert.Equal("A seleção tem menos de 1 mm de altura. Nada foi alterado.", thin.Message);
            Assert.DoesNotContain(_page.LayersList, l => l.Name == TapeMeasureService.LayerName);
        }

        [Fact]
        public void Gap_IsTheDistanceFromTheSelection_ZeroTouches_NegativeIsRefused()
        {
            var service = new TapeMeasureService();

            var far = service.Create(Select(Box(0, 0, 20, 30)), _page, 1, Axis.Both, 12.5);
            Assert.Contains("a 12,5 mm da seleção", far.Message);
            var width = Part((FakeShape)Ruler.Shapes[0], "Traços").Lines;
            var height = Part((FakeShape)Ruler.Shapes[1], "Traços").Lines;
            Assert.Equal(-12.5, width[0].Y1, 6);
            Assert.Equal(-12.5, height[0].X1, 6);

            var touching = service.Create(Select(Box(0, 0, 20, 30)), _page, 1, Axis.Width, 0);
            Assert.True(touching.IsSuccess, touching.Message);
            Assert.Equal(0, Part((FakeShape)Ruler.Shapes[2], "Traços").Lines[0].Y1, 6);

            var negative = service.Create(Select(Box(0, 0, 20, 30)), _page, 1, Axis.Width, -1);
            Assert.Equal("A distância da régua precisa ser 0 ou mais (mm). Nada foi alterado.", negative.Message);
            Assert.Equal(3, Ruler.Shapes.Count);
        }

        [Fact]
        public void LongRulers_NumberEvery5Or10Centimeters()
        {
            Assert.Equal(1, TapeMeasureService.LabelStepCm(2000));
            Assert.Equal(5, TapeMeasureService.LabelStepCm(2500));
            Assert.Equal(10, TapeMeasureService.LabelStepCm(6000));
            Assert.Equal("100 cm (1000 mm)", TapeMeasureService.Format(1000));
        }
    }
}
