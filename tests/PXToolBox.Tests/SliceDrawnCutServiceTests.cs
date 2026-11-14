using System.Linq;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class SliceDrawnCutServiceTests
    {
        [Fact]
        public void ReadCuts_TakesEveryPathOfTheCutLayer_InMillimetersOfTheArt_CurvesIncluded()
        {
            // Documento em unidades de 2 mm. Arte de 200 × 100 unidades com o canto inferior esquerdo em (100, 200).
            var page = new FakePage { Width = 1000, Height = 1000 };
            var art = new FakeLayer { Name = "Camada 1" };
            var cuts = new FakeLayer { Name = SliceDrawnCutService.LayerName };
            page.LayersList.Add(art);
            page.LayersList.Add(cuts);
            var selection = new FakeSelection();
            selection.ShapesList.Add(art.AddShape(new FakeShape { Name = "Arte", Kind = ShapeKind.Curve, CenterX = 200, CenterY = 250, Width = 200, Height = 100 }));
            // Reta vertical no meio da arte (de cima a baixo) e, num grupo, uma Bézier e um texto.
            cuts.AddShape(new FakeShape
            {
                Kind = ShapeKind.Curve,
                OutlineElements = new[]
                {
                    new OutlineElement(OutlineElementKind.Start, 200, 300),
                    new OutlineElement(OutlineElementKind.Line, 200, 200),
                },
            });
            var group = new FakeShape { Kind = ShapeKind.Group };
            group.ShapesList.Add(new FakeShape
            {
                Kind = ShapeKind.Curve,
                OutlineElements = new[]
                {
                    new OutlineElement(OutlineElementKind.Start, 100, 250),
                    new OutlineElement(OutlineElementKind.Control, 150, 300),
                    new OutlineElement(OutlineElementKind.Control, 250, 300),
                    new OutlineElement(OutlineElementKind.Curve, 300, 250),
                },
            });
            group.ShapesList.Add(new FakeShape { Kind = ShapeKind.Text, Width = 10, Height = 10 });
            cuts.AddShape(group);

            var result = new SliceDrawnCutService().ReadCuts(selection, page, 2);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(2, result.Value!.Paths.Count);
            Assert.Equal(new[] { (50.0, 0.0), (50.0, 50.0) }, result.Value.Paths[0].Select(p => (p.X, p.Y)));
            var curve = result.Value.Paths[1];
            Assert.Equal(1 + NestingService.CurveSteps, curve.Count);
            Assert.Equal((0.0, 25.0), (curve[0].X, curve[0].Y));
            Assert.Equal((100.0, 25.0), (curve[curve.Count - 1].X, curve[curve.Count - 1].Y));
            Assert.True(curve.Min(p => p.Y) < 15, "a curva sobe na arte (Y para baixo)");
            Assert.Equal(1, result.Value.Unreadable);
        }

        [Fact]
        public void WithoutTheLayerOrLines_Explains_AndCreateLayerMakesItOnce()
        {
            var page = new FakePage();
            var art = new FakeLayer { Name = "Camada 1" };
            page.LayersList.Add(art);
            var selection = new FakeSelection();
            selection.ShapesList.Add(art.AddShape(new FakeShape { Kind = ShapeKind.Curve, CenterX = 50, CenterY = 50, Width = 100, Height = 100 }));
            var service = new SliceDrawnCutService();

            var missing = service.ReadCuts(selection, page, 1);
            Assert.False(missing.IsSuccess);
            Assert.StartsWith("Não há a camada “Cortes (Slice)” nesta página", missing.Message);

            Assert.True(service.CreateLayer(page).IsSuccess);
            Assert.True(service.CreateLayer(page).IsSuccess);
            Assert.Single(page.LayersList, l => l.Name == SliceDrawnCutService.LayerName);

            var empty = service.ReadCuts(selection, page, 1);
            Assert.False(empty.IsSuccess);
            Assert.StartsWith("A camada “Cortes (Slice)” não tem linhas", empty.Message);

            Assert.False(service.ReadCuts(new FakeSelection(), page, 1).IsSuccess);
        }
    }
}
