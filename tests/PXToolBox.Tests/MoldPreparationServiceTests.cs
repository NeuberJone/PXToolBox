using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class MoldPreparationServiceTests
    {
        [Fact]
        public void PrepareNewMold_NormalizesLayerNameToFourSegments()
        {
            var page = new FakePage();
            var layer = (FakeLayer)page.CreateLayer("CAM1.PP.X");

            var service = new MoldPreparationService();
            service.PrepareNewMold(page);

            Assert.Equal("CAM1.PP.X.PP", layer.Name);
        }

        [Fact]
        public void PrepareNewMold_RenamesNonAllowedSubItemsToDuplicatedSegment()
        {
            var page = new FakePage();
            var layer = (FakeLayer)page.CreateLayer("CAM1.PP.X");
            var group = new FakeShape { Kind = ShapeKind.Group };
            var allowed = new FakeShape { Name = "PP" }; // já é um tamanho válido
            var notAllowed = new FakeShape { Name = "QualquerCoisa" };
            group.ShapesList.Add(allowed);
            group.ShapesList.Add(notAllowed);
            layer.AddShape(group);

            var service = new MoldPreparationService();
            service.PrepareNewMold(page);

            Assert.Equal("PP", allowed.Name); // não mexeu, já era um tamanho válido
            Assert.Equal("PP", notAllowed.Name); // passou a usar o 4º segmento (duplicado do 2º)
        }

        [Fact]
        public void PrepareNewMold_DeletesEmptyLayersFirst()
        {
            var page = new FakePage();
            var empty = (FakeLayer)page.CreateLayer("Vazia");

            var service = new MoldPreparationService();
            service.PrepareNewMold(page);

            Assert.True(empty.IsDeleted);
        }
    }
}
