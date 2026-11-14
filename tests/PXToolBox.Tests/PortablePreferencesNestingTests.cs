#if NET48
// Só compila na perna net48 — PXToolBoxDev (host) só existe lá. Exportar/Importar preferências
// levam os perfis do "Encaixar peças" (dev.106). Nada aqui grava em disco nem abre o CorelDRAW.
using System.Linq;
using PXToolBox.Core.Nesting;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.Nesting;
using Xunit;

namespace PXToolBox.Tests
{
    public class PortablePreferencesNestingTests
    {
        private const string Header = "\"formatId\": \"PxToolBoxDevPortablePreferences\", \"schemaVersion\": 1, \"editorFontSize\": 14";

        [Fact]
        public void RoundTrip_CarriesTheNestingProfiles()
        {
            var profiles = new[]
            {
                new NestingProfile { Name = "Rolo 160 sublimação", Area = NestingProfileArea.Roll, RollWidth = "160", SearchBest = true },
                new NestingProfile { Name = "Retalhos", Area = NestingProfileArea.Areas, AreasText = "A: 50 x 50", AreaOrder = NestAreaOrder.SmallestFirst },
            };
            var document = PortablePreferencesService.Export(ListForgeIntegratedSettings.Default(), null, profiles);
            var json = PortablePreferencesService.Serialize(document);

            var validation = PortablePreferencesService.Validate(json);
            var imported = PortablePreferencesService.ReadNestingProfiles(validation.Document!);

            Assert.True(validation.IsValid);
            Assert.Empty(validation.IncompatibleFieldSummaries);
            Assert.Contains("\"nestingProfiles\"", json);
            Assert.Equal(new[] { "Rolo 160 sublimação", "Retalhos" }, imported!.Select(p => p.Name));
            Assert.Equal((NestingProfileArea.Roll, "160", true), (imported[0].Area, imported[0].RollWidth, imported[0].SearchBest));
            Assert.Equal((NestingProfileArea.Areas, "A: 50 x 50", NestAreaOrder.SmallestFirst), (imported[1].Area, imported[1].AreasText, imported[1].AreaOrder));
            const string line = "Perfis do encaixe (2): Rolo 160 sublimação, Retalhos";
            Assert.Contains(line, validation.RecognizedFieldSummaries);
            Assert.Contains(line, PortablePreferencesService.BuildExportSummary(document));
        }

        // Arquivo de versão anterior, ou exportado com o arquivo de perfis ilegível: mantém os atuais.
        [Fact]
        public void FileWithoutTheKey_KeepsCurrentProfiles_AndAnEmptyListClearsThem()
        {
            var without = PortablePreferencesService.Validate("{ " + Header + " }");
            var exported = PortablePreferencesService.Export(ListForgeIntegratedSettings.Default());
            Assert.Null(PortablePreferencesService.ReadNestingProfiles(without.Document!));
            Assert.Null(exported.NestingProfiles);
            Assert.DoesNotContain(PortablePreferencesService.BuildExportSummary(exported), l => l.StartsWith("Perfis do encaixe"));

            var empty = PortablePreferencesService.Validate("{ " + Header + ", \"nestingProfiles\": [] }");
            Assert.Empty(PortablePreferencesService.ReadNestingProfiles(empty.Document!)!);
            Assert.Contains("Perfis do encaixe: nenhum (lista vazia).", empty.RecognizedFieldSummaries);
        }

        // Cada perfil ruim é relatado e ignorado sozinho; o resto do arquivo vale.
        [Fact]
        public void InvalidOrRepeatedProfiles_AreReportedAndIgnoredOneByOne()
        {
            var validation = PortablePreferencesService.Validate("{ " + Header + ", \"panelFavoriteToolIds\": [\"grid.prepare\"], \"nestingProfiles\": [ " +
                "{ \"name\": \"\" }, { \"name\": \"A\", \"rollWidthCm\": \"150\" }, { \"name\": \" a \" }, 3, { \"name\": \"B\", \"drawAreas\": \"talvez\" } ] }");

            Assert.True(validation.IsValid);
            Assert.Equal(new[] { "grid.prepare" }, validation.Document!.PanelFavoriteToolIds);
            var kept = Assert.Single(PortablePreferencesService.ReadNestingProfiles(validation.Document)!);
            Assert.Equal(("A", "150"), (kept.Name, kept.RollWidth));
            Assert.Equal(4, validation.IncompatibleFieldSummaries.Count);
            Assert.Contains("Perfil do encaixe “”: Perfil sem nome. — ignorado.", validation.IncompatibleFieldSummaries);
            Assert.Contains("Perfil do encaixe “a”: Perfil repetido: “A”. — ignorado.", validation.IncompatibleFieldSummaries);
            Assert.All(validation.IncompatibleFieldSummaries, l => Assert.EndsWith("— ignorado.", l));
        }

        [Fact]
        public void ProfilesOfAnUnexpectedType_AreIgnored_KeepingTheCurrentOnes()
        {
            var validation = PortablePreferencesService.Validate("{ " + Header + ", \"nestingProfiles\": \"texto\" }");

            Assert.True(validation.IsValid);
            Assert.Null(validation.Document!.NestingProfiles);
            Assert.Equal("Perfis do encaixe com tipo inesperado — ignorados, mantidos os atuais.", Assert.Single(validation.IncompatibleFieldSummaries));
        }
    }
}
#endif
