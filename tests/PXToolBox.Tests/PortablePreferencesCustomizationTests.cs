#if NET48
// Só compila na perna net48 — PXToolBoxDev (host) só existe lá. Exportar/Importar preferências
// levam a personalização do painel e o "Mostrar nomes" dos Favoritos (dev.78). Nada aqui grava
// em disco nem abre o CorelDRAW.
using System.Linq;
using PXToolBoxDev.Favorites;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.PanelLayout;
using Xunit;

namespace PXToolBox.Tests
{
    public class PortablePreferencesCustomizationTests
    {
        private const string Header = "\"formatId\": \"PxToolBoxDevPortablePreferences\", \"schemaVersion\": 1, \"editorFontSize\": 14";

        private static ListForgeIntegratedSettings CustomizedSource(out string sectionId)
        {
            var source = ListForgeIntegratedSettings.Default();
            var custom = PanelCustomization.Default();
            sectionId = custom.AddSection("Meu dia a dia", () => "abc123");
            custom.Rename(PanelSectionCatalog.Grid, "Grade");
            custom.Place("grid.sisbolt.free-hand", sectionId);
            custom.Place(PanelToolCatalog.GridPrepare, sectionId);
            var layout = new PanelSectionLayout(null, new[] { sectionId });
            layout.MoveUp(sectionId);
            layout.SetVisible(PanelSectionCatalog.Diagnostics, false);
            source.PanelCustomization = custom;
            source.PanelSectionLayout = layout.ToPersist();
            source.PanelFavoritesShowLabels = true;
            return source;
        }

        [Fact]
        public void RoundTrip_CarriesCustomization_CustomSectionLayout_AndShowLabels()
        {
            var source = CustomizedSource(out var mine);
            var json = PortablePreferencesService.Serialize(PortablePreferencesService.Export(source));

            var validation = PortablePreferencesService.Validate(json);
            var target = ListForgeIntegratedSettings.Default();
            PortablePreferencesService.Apply(validation.Document!, target);

            Assert.True(validation.IsValid);
            Assert.Empty(validation.IncompatibleFieldSummaries);
            Assert.Contains("\"panelCustomization\"", json);
            Assert.True(target.PanelFavoritesShowLabels);
            Assert.Equal("Grade", target.PanelCustomization!.TitleOf(PanelSectionCatalog.Grid));
            Assert.Equal("Meu dia a dia", target.PanelCustomization.TitleOf(mine));
            Assert.Equal(new[] { "grid.sisbolt.free-hand", PanelToolCatalog.GridPrepare }, target.PanelCustomization.MovedInto(mine));
            Assert.Equal(
                new PanelSectionLayout(source.PanelSectionLayout, new[] { mine }).Sections.Select(e => (e.Id, e.Visible)),
                new PanelSectionLayout(target.PanelSectionLayout, new[] { mine }).Sections.Select(e => (e.Id, e.Visible)));
        }

        // Exportar um painel nunca personalizado e importar desfaz a personalização do destino.
        [Fact]
        public void DefaultExport_ResetsTargetCustomization()
        {
            var json = PortablePreferencesService.Serialize(PortablePreferencesService.Export(ListForgeIntegratedSettings.Default()));
            var target = CustomizedSource(out var mine);

            var validation = PortablePreferencesService.Validate(json);
            PortablePreferencesService.Apply(validation.Document!, target);

            Assert.Null(target.PanelCustomization);
            Assert.False(target.PanelFavoritesShowLabels);
            Assert.DoesNotContain(target.PanelSectionLayout!, e => e.Id == mine);
        }

        // Arquivo de versão anterior (sem as chaves novas): mantém o que o destino tem.
        [Fact]
        public void OlderFile_KeepsCurrentCustomizationAndShowLabels()
        {
            var target = CustomizedSource(out var mine);

            var validation = PortablePreferencesService.Validate("{ " + Header + ", \"panelFavoriteToolIds\": [\"grid.prepare\"] }");
            PortablePreferencesService.Apply(validation.Document!, target);

            Assert.Null(validation.Document!.PanelCustomization);
            Assert.Null(validation.Document.PanelFavoritesShowLabels);
            Assert.True(target.PanelFavoritesShowLabels);
            Assert.Equal(mine, target.PanelCustomization!.SectionOf("grid.sisbolt.free-hand"));
        }

        // Layout no arquivo, sem personalização: as seções criadas do destino continuam.
        [Fact]
        public void LayoutOnlyFile_KeepsTargetCustomSections()
        {
            var target = CustomizedSource(out var mine);

            var validation = PortablePreferencesService.Validate("{ " + Header + ", \"panelSectionLayout\": [ { \"id\": \"grid\", \"visible\": false } ] }");
            PortablePreferencesService.Apply(validation.Document!, target);

            var layout = new PanelSectionLayout(target.PanelSectionLayout, new[] { mine });
            Assert.Contains(layout.Sections, e => e.Id == mine);
            Assert.False(layout.IsVisible(PanelSectionCatalog.Grid));
        }

        [Fact]
        public void BadEntries_AreReportedAndSkipped_RestIsImported()
        {
            var json = "{ " + Header + ", \"panelFavoritesShowLabels\": \"sim\", \"panelCustomization\": {"
                + " \"sectionTitles\": { \"grid\": \"Grade\", \"futura\": \"X\", \"codes\": \"  \" },"
                + " \"customSections\": [ { \"id\": \"custom.a\", \"title\": \"A\" }, { \"id\": \"semprefixo\", \"title\": \"B\" }, { \"id\": \"custom.c\", \"title\": \"\" } ],"
                + " \"toolPlacements\": [ { \"toolId\": \"grid.prepare\", \"sectionId\": \"custom.a\" }, { \"toolId\": \"tool.futura\", \"sectionId\": \"custom.a\" },"
                + " { \"toolId\": \"grid.apply-outline\", \"sectionId\": \"custom.zz\" }, { \"toolId\": \"grid.prepare\", \"sectionId\": \"codes\" } ] },"
                + " \"panelSectionLayout\": [ { \"id\": \"custom.a\" }, { \"id\": \"custom.zz\" } ] }";

            var validation = PortablePreferencesService.Validate(json);

            Assert.True(validation.IsValid);
            var c = validation.Document!.PanelCustomization!;
            Assert.Null(validation.Document.PanelFavoritesShowLabels);
            Assert.Equal("Grade", c.TitleOf("grid"));
            Assert.Equal(new[] { "custom.a" }, c.CustomSections.Select(s => s.Id));
            Assert.Equal("custom.a", c.SectionOf(PanelToolCatalog.GridPrepare));
            Assert.False(c.IsMoved("grid.apply-outline"));
            Assert.Equal(new[] { "custom.a" }, validation.Document.PanelSectionLayout!.Select(e => e.Id));
            // "sim"; futura; codes vazio; semprefixo; custom.c; tool.futura; custom.zz (posição); custom.zz (layout).
            Assert.Equal(8, validation.IncompatibleFieldSummaries.Count);
        }

        [Fact]
        public void ExportSummary_DescribesCustomization()
        {
            var lines = PortablePreferencesService.BuildExportSummary(PortablePreferencesService.Export(CustomizedSource(out _)));

            Assert.Contains("Favoritos: mostrar nomes (botão inteiro).", lines);
            Assert.Contains(lines, l => l.StartsWith("Personalização do painel:") && l.Contains("\"Meu dia a dia\"") && l.Contains("2 ferramenta(s)"));
            Assert.Contains(lines, l => l.StartsWith("Layout do painel:") && l.Contains("Diagnóstico"));
        }
    }
}
#endif
