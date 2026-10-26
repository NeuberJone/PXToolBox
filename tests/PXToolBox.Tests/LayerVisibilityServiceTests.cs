using PXToolBox.Core.Naming;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class LayerVisibilityServiceTests
    {
        [Fact]
        public void SetVisibility_OnlyAffectsLayersOfThatCategory()
        {
            var page = new FakePage();
            var fe1 = (FakeLayer)page.CreateLayer("CAM1.PP.FE");
            var fe2 = (FakeLayer)page.CreateLayer("CAM1.GG.FE");
            var ma1 = (FakeLayer)page.CreateLayer("CAM1.PP.MA");

            var service = new LayerVisibilityService();
            var result = service.SetVisibility(page, LayerCategory.FE, visible: false);

            Assert.Equal(2, result.Value);
            Assert.False(fe1.Visible);
            Assert.False(fe2.Visible);
            Assert.True(ma1.Visible); // não afetada
        }

        [Fact]
        public void Toggle_ShowsAll_WhenAnyIsHidden()
        {
            var page = new FakePage();
            var c1 = (FakeLayer)page.CreateLayer("CAM1.PP.C");
            var c2 = (FakeLayer)page.CreateLayer("CAM1.GG.C");
            c1.Visible = false;
            c2.Visible = true;

            var service = new LayerVisibilityService();
            var result = service.Toggle(page, LayerCategory.C);

            Assert.True(result.IsSuccess);
            Assert.True(result.Value.NewVisibility);
            Assert.True(c1.Visible);
            Assert.True(c2.Visible);
        }

        [Fact]
        public void Toggle_HidesAll_WhenAllAreVisible()
        {
            var page = new FakePage();
            var c1 = (FakeLayer)page.CreateLayer("CAM1.PP.C");
            c1.Visible = true;

            var service = new LayerVisibilityService();
            var result = service.Toggle(page, LayerCategory.C);

            Assert.False(result.Value.NewVisibility);
            Assert.False(c1.Visible);
        }

        [Fact]
        public void Toggle_NoMatchingLayers_ReturnsInconclusive()
        {
            var page = new FakePage();
            var service = new LayerVisibilityService();

            var result = service.Toggle(page, LayerCategory.MA);

            Assert.False(result.IsSuccess);
        }
    }
}
