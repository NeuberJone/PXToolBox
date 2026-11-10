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

        // Ordem: digitada no painel > padrão desta máquina > padrão original da macro.
        [Theory]
        [InlineData("D:\\Uso", "E:\\Maquina", "D:\\Uso")]
        [InlineData("  D:\\Uso  ", null, "D:\\Uso")]
        [InlineData(null, "E:\\Maquina", "E:\\Maquina")]
        [InlineData("   ", " E:\\Maquina ", "E:\\Maquina")]
        [InlineData(null, null, OutputPreparationService.DefaultOutputFolder)]
        [InlineData("", "  ", OutputPreparationService.DefaultOutputFolder)]
        public void ResolveOutputFolder_TypedThenMachineDefaultThenOriginal(string? typed, string? machineDefault, string expected)
        {
            Assert.Equal(expected, OutputPreparationService.ResolveOutputFolder(typed, machineDefault));
        }

        // Subpasta do rolo (contexto de produção) dentro da pasta efetiva.
        [Theory]
        [InlineData("E:\\Maquina", "M1_29-10-2026_0013", "E:\\Maquina\\M1_29-10-2026_0013")]
        [InlineData("E:\\Maquina\\", " Rolo 7 ", "E:\\Maquina\\Rolo 7")]
        [InlineData("E:\\Maquina", null, "E:\\Maquina")]
        [InlineData("E:\\Maquina", "  ", "E:\\Maquina")]
        public void WithSubfolder_AppendsOnlyWhenGiven(string folder, string? subfolder, string expected)
        {
            Assert.Equal(expected, OutputPreparationService.WithSubfolder(folder, subfolder));
        }

        [Fact]
        public void SaveOutput_InRollSubfolder()
        {
            var document = new FakeDocument();
            var service = new OutputPreparationService();

            var folder = OutputPreparationService.WithSubfolder(OutputPreparationService.ResolveOutputFolder(null, null), "M1_0013");
            var result = service.SaveOutput(document, OutputPreparationService.ShieldName, "Time X", folder);

            Assert.Equal("Salvo em \"Z:\\Neuber\\Saídas\\M1_0013\\Escudo - Time X.cdr\".", result.Message);
        }

        [Fact]
        public void SaveOutput_UsesTheResolvedFolder()
        {
            var document = new FakeDocument();
            var service = new OutputPreparationService();

            var folder = OutputPreparationService.ResolveOutputFolder(null, "E:\\Maquina");
            var result = service.SaveOutput(document, OutputPreparationService.FlagName, "Time X", folder);

            Assert.True(result.IsSuccess);
            Assert.Equal("Salvo em \"E:\\Maquina\\Bandeira - Time X.cdr\".", result.Message);
        }
    }
}
