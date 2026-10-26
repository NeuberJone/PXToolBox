using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class OutputPreparationServiceTests
    {
        [Fact]
        public void PrepareForOutput_NoSelectableShapes_Fails()
        {
            var document = new FakeDocument();
            var service = new OutputPreparationService();

            var result = service.PrepareForOutput(document, OutputPreparationService.ShieldName);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void PrepareForOutput_RenamesPageAndDocument()
        {
            var document = new FakeDocument();
            var layer = (FakeLayer)document.ActivePageFake.CreateLayer("L1");
            layer.AddShape(new FakeShape { Width = 10, Height = 10 });

            var service = new OutputPreparationService();
            var result = service.PrepareForOutput(document, OutputPreparationService.FlagName);

            Assert.True(result.IsSuccess);
            Assert.Equal("Bandeira", document.ActivePageFake.Name);
            Assert.Equal("Bandeira", document.Name);
        }

        [Theory]
        [InlineData(null, "C:\\Saidas\\Escudo.cdr")]
        [InlineData("Time X", "C:\\Saidas\\Escudo - Time X.cdr")]
        public void BuildOutputFilePath_UsesConfigurableFolder(string? layoutName, string expected)
        {
            var service = new OutputPreparationService();
            var path = service.BuildOutputFilePath("C:\\Saidas", OutputPreparationService.ShieldName, layoutName);
            Assert.Equal(expected, path);
        }
    }
}
