using PXToolBox.Core.Measurement;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class IntersectGroupServiceTests
    {
        [Fact]
        public void GroupOverlapping_UnknownUnit_Fails()
        {
            var page = new FakePage();
            var service = new IntersectGroupService();

            var result = service.GroupOverlapping(page, null);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void GroupOverlapping_FewerThanTwoShapes_Fails()
        {
            var page = new FakePage();
            var layer = (FakeLayer)page.CreateLayer("L1");
            layer.AddShape(new FakeShape());

            var service = new IntersectGroupService();
            var result = service.GroupOverlapping(page, DocumentUnit.Millimeter);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void GroupOverlapping_GroupsExactDuplicatesAndNestedSmallerShape()
        {
            var page = new FakePage();
            var layer = (FakeLayer)page.CreateLayer("L1");

            // Dois objetos coincidentes (mesma posição e tamanho).
            var a = layer.AddShape(new FakeShape { CenterX = 0, CenterY = 0, Width = 10, Height = 10 });
            var b = layer.AddShape(new FakeShape { CenterX = 0, CenterY = 0, Width = 10, Height = 10 });

            // Um objeto menor, centrado dentro da área de "a" — deve ser absorvido.
            var nested = layer.AddShape(new FakeShape { CenterX = 1, CenterY = 1, Width = 2, Height = 2 });

            // Um objeto totalmente independente, em outra posição — não deve ser afetado.
            var unrelated = layer.AddShape(new FakeShape { CenterX = 100, CenterY = 100, Width = 3, Height = 3 });

            var service = new IntersectGroupService();
            var result = service.GroupOverlapping(page, DocumentUnit.Millimeter);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value);

            // "unrelated" nunca foi agrupado: continua solto na camada.
            Assert.Contains(unrelated, layer.ShapesList);

            // a, b e nested foram removidos da camada (agora são filhos do grupo criado).
            Assert.DoesNotContain(a, layer.ShapesList);
            Assert.DoesNotContain(b, layer.ShapesList);
            Assert.DoesNotContain(nested, layer.ShapesList);
        }

        [Fact]
        public void GroupOverlapping_NeverAbsorbsAShapeLargerThanTheBase()
        {
            var page = new FakePage();
            var layer = (FakeLayer)page.CreateLayer("L1");

            var a = layer.AddShape(new FakeShape { CenterX = 0, CenterY = 0, Width = 10, Height = 10 });
            var b = layer.AddShape(new FakeShape { CenterX = 0, CenterY = 0, Width = 10, Height = 10 });
            var larger = layer.AddShape(new FakeShape { CenterX = 0, CenterY = 0, Width = 50, Height = 50 });

            var service = new IntersectGroupService();
            var result = service.GroupOverlapping(page, DocumentUnit.Millimeter);

            Assert.True(result.IsSuccess);
            Assert.Contains(larger, layer.ShapesList); // maior que a base: nunca absorvido
        }
    }
}
