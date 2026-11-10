#if NET48
// Só compila na perna net48 — PXToolBoxDev (host) só existe lá. Exportar/Importar preferências
// levam as configurações de moldes (dev.89). Nada aqui grava em disco nem abre o CorelDRAW.
using System.Linq;
using PXToolBox.Core.Molds;
using PXToolBoxDev.Integrations;
using Xunit;

namespace PXToolBox.Tests
{
    public class PortablePreferencesMoldsTests
    {
        private const string Header = "\"formatId\": \"PxToolBoxDevPortablePreferences\", \"schemaVersion\": 1, \"editorFontSize\": 14";

        private static MoldSettings Custom() => new MoldSettings(
            new MoldSizeCategory("MA", new[] { "P", "M", "G" }),
            new MoldSizeCategory("FE", new[] { "P", "M" }, new[] { "BL" }),
            new MoldSizeCategory("C", new[] { "4", "6" }, null, new[] { "A" }),
            "M", new[] { "LASER" }, "ETIQUETA", "",
            new MoldDefineOptions(false, "#00FF00", 0.5, true, 40, 1));

        [Fact]
        public void RoundTrip_CarriesTheMoldSettings()
        {
            var json = PortablePreferencesService.Serialize(PortablePreferencesService.Export(ListForgeIntegratedSettings.Default(), Custom()));

            var validation = PortablePreferencesService.Validate(json);
            var imported = PortablePreferencesService.ReadMoldSettings(validation.Document!);

            Assert.True(validation.IsValid);
            Assert.Empty(validation.IncompatibleFieldSummaries);
            Assert.Contains("\"moldSettings\"", json);
            Assert.NotNull(imported);
            Assert.Equal(new[] { "BLP", "BLM" }, imported!.Female.Sizes);
            Assert.Equal(new[] { "4A", "6A" }, imported.Kids.Sizes);
            Assert.Equal(("M", "LASER", "ETIQUETA"), (imported.GridBaseSize, imported.LaserNames.Single(), imported.TagName));
            Assert.Equal((false, "#00FF00", 0.5, 40.0), (imported.Define.CreateLaserCopy, imported.Define.LaserColor, imported.Define.LaserWidthMillimeters, imported.Define.TagFontSizePoints));
            Assert.Contains(validation.RecognizedFieldSummaries, l => l == "Configurações de moldes: MA (3 tamanhos), FE (2 tamanhos), C (2 tamanhos); laser \"LASER\", TAG \"ETIQUETA\".");
        }

        // Arquivo de versão anterior, ou exportado sem as configurações: mantém as atuais.
        [Fact]
        public void FileWithoutTheKey_KeepsCurrentMoldSettings()
        {
            var validation = PortablePreferencesService.Validate("{ " + Header + " }");
            var exported = PortablePreferencesService.Export(ListForgeIntegratedSettings.Default());

            Assert.True(validation.IsValid);
            Assert.Null(PortablePreferencesService.ReadMoldSettings(validation.Document!));
            Assert.Null(exported.MoldSettings);
            Assert.DoesNotContain(PortablePreferencesService.BuildExportSummary(exported), l => l.StartsWith("Configurações de moldes"));
        }

        // Configurações inválidas: relatadas e ignoradas inteiras; o resto do arquivo vale.
        [Theory]
        [InlineData("{ \"schema\": 1, \"tagName\": \"\" }", "Informe o nome da TAG.")]
        [InlineData("{ \"schema\": 7 }", "versão 7 desconhecida")]
        [InlineData("{ \"schema\": 1, \"laserNames\": [ 3 ] }", "item que não é texto")]
        [InlineData("\"texto\"", "tipo inesperado")]
        public void InvalidMoldSettings_AreReportedAndIgnored(string molds, string expected)
        {
            var validation = PortablePreferencesService.Validate("{ " + Header + ", \"panelFavoriteToolIds\": [\"grid.prepare\"], \"moldSettings\": " + molds + " }");

            Assert.True(validation.IsValid);
            Assert.Null(validation.Document!.MoldSettings);
            Assert.Equal(new[] { "grid.prepare" }, validation.Document.PanelFavoriteToolIds);
            var problem = Assert.Single(validation.IncompatibleFieldSummaries);
            Assert.Contains(expected, problem);
            Assert.Contains("mantidas as atuais", problem);
        }
    }
}
#endif
