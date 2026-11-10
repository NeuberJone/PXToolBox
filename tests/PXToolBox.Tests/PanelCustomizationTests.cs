#if NET48
// Só compila na perna net48 — PXToolBoxDev (host) só existe lá. Personalização do painel
// (pedido do usuário, 2026-11-06): nomes das seções, seções criadas, seção de cada ferramenta,
// marcas do canto pelo nome atual, gravação. Nada aqui abre o CorelDRAW.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using PXToolBoxDev.Favorites;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.PanelLayout;
using Xunit;

namespace PXToolBox.Tests
{
    public class PanelCustomizationModelTests
    {
        [Fact]
        public void Default_EveryToolAtHome_OriginalTitles()
        {
            var c = PanelCustomization.Default();

            Assert.True(c.IsDefault);
            Assert.All(PanelToolCatalog.All, t => Assert.Equal(PanelCustomization.HomeSectionOf(t.Id), c.SectionOf(t.Id)));
            Assert.All(PanelToolCatalog.All, t => Assert.False(string.IsNullOrEmpty(PanelCustomization.HomeSectionOf(t.Id)), t.Id));
            Assert.Equal("Objetos", c.TitleOf(PanelSectionCatalog.ToolsObjects));
        }

        [Fact]
        public void Rename_BuiltIn_EmptyOrOriginalGoesBack()
        {
            var c = PanelCustomization.Default();

            Assert.True(c.Rename("grid", "  Grade  "));
            Assert.Equal("Grade", c.TitleOf("grid"));
            Assert.True(c.Rename("grid", ""));
            Assert.Equal("Grid", c.TitleOf("grid"));
            Assert.False(c.Rename("grid", "Grid"));
            Assert.True(c.IsDefault);
        }

        [Fact]
        public void PlaceTool_MovesItAndBack_AndKeepsTheOrderInsideTheSection()
        {
            var c = PanelCustomization.Default();
            var mine = c.AddSection("Meu dia a dia", () => "a");

            Assert.True(c.Place("grid.sisbolt.free-hand", mine));
            Assert.True(c.Place("tools.objects.group-intersection", mine));
            Assert.True(c.Place(PanelToolCatalog.GridPrepare, mine));
            Assert.Equal(new[] { "grid.sisbolt.free-hand", "tools.objects.group-intersection", PanelToolCatalog.GridPrepare }, c.MovedInto(mine));
            Assert.True(c.IsMoved("grid.sisbolt.free-hand"));

            Assert.True(c.MoveUp(PanelToolCatalog.GridPrepare));
            Assert.False(c.MoveUp("grid.sisbolt.free-hand"));
            Assert.Equal(new[] { "grid.sisbolt.free-hand", PanelToolCatalog.GridPrepare, "tools.objects.group-intersection" }, c.MovedInto(mine));

            // Voltar para a seção de origem desfaz a mudança.
            Assert.True(c.Place("grid.sisbolt.free-hand", PanelSectionCatalog.Assembly));
            Assert.False(c.IsMoved("grid.sisbolt.free-hand"));
            Assert.Equal(2, c.MovedInto(mine).Count);
        }

        [Fact]
        public void PlaceTool_IntoAnotherBuiltInSection_Works()
        {
            var c = PanelCustomization.Default();

            Assert.True(c.Place("tools.measures.sum-height", PanelSectionCatalog.Grid));

            Assert.Equal(PanelSectionCatalog.Grid, c.SectionOf("tools.measures.sum-height"));
            Assert.Equal(new[] { "tools.measures.sum-height" }, c.MovedInto(PanelSectionCatalog.Grid));
        }

        [Fact]
        public void RemoveSection_SendsItsToolsHome()
        {
            var c = PanelCustomization.Default();
            var mine = c.AddSection("Temporária", () => "b");
            c.Place("codes.toggle-no", mine);

            Assert.True(c.RemoveSection(mine));

            Assert.Equal(PanelSectionCatalog.Codes, c.SectionOf("codes.toggle-no"));
            Assert.True(c.IsDefault);
            Assert.False(c.RemoveSection(PanelSectionCatalog.Grid)); // só seções criadas
        }

        [Fact]
        public void Normalize_DropsJunk_KeepsUnknownTools()
        {
            var c = new PanelCustomization
            {
                SectionTitles = new Dictionary<string, string> { ["grid"] = "Grade", ["nao.existe"] = "X", ["codes"] = "  " },
                CustomSections = new List<CustomSectionEntry> { new CustomSectionEntry("custom.a", "Minha"), new CustomSectionEntry("sem-prefixo", "Y"), new CustomSectionEntry("custom.a", "Repetida") },
                ToolPlacements = new List<ToolPlacementEntry>
                {
                    new ToolPlacementEntry("codes.toggle-no", "custom.a"),
                    new ToolPlacementEntry("codes.toggle-no", "grid"),          // repetida: vale a primeira
                    new ToolPlacementEntry("future.tool", "custom.a"),          // de versão mais nova: preservada
                    new ToolPlacementEntry("codes.toggle-nu", "custom.sumiu"),  // seção inexistente
                    new ToolPlacementEntry("codes.toggle-ap", "codes"),         // já é a origem
                },
            }.Normalize();

            Assert.Equal(new[] { "grid" }, c.SectionTitles.Keys);
            Assert.Equal(new[] { "custom.a" }, c.CustomSections.Select(s => s.Id));
            Assert.Equal("Minha", c.TitleOf("custom.a"));
            Assert.Equal(new[] { "codes.toggle-no", "future.tool" }, c.ToolPlacements.Select(p => p.ToolId));
            Assert.Equal(new[] { "codes.toggle-no" }, c.MovedInto("custom.a"));
        }

        // Regra do usuário: inicial; mesma inicial → duas letras; Preparação e saída × Produção →
        // iniciais das palavras. Sem acento ("Co", não "Có").
        [Fact]
        public void Badges_ForTheOriginalSections_MatchTheAgreedTable()
        {
            var badges = PanelCustomization.Default().SectionBadges(PanelSectionCatalog.All.Select(s => s.Id));

            Assert.Equal("G", badges["grid"]);
            Assert.Equal("Mo", badges["assembly"]);
            Assert.Equal("Co", badges["codes"]);
            Assert.Equal("Ca", badges["layers"]);
            Assert.Equal("O", badges["tools.objects"]);
            Assert.Equal("T", badges["tools.piece-type"]);
            Assert.Equal("Me", badges["tools.measures"]);
            Assert.Equal("PS", badges["output"]);
            Assert.Equal("Pr", badges["production"]);
            Assert.Equal("L", badges["lists"]);
            Assert.Equal("D", badges["diagnostics"]);
        }

        [Fact]
        public void Badges_FollowRenamesAndCreatedSections_AlwaysUnique()
        {
            var c = PanelCustomization.Default();
            c.Rename("grid", "Molde");                          // M: Molde × Montagem × Medidas
            var mine = c.AddSection("Meu dia a dia", () => "c"); // M também
            var ids = PanelSectionCatalog.All.Select(s => s.Id).Concat(new[] { mine }).ToList();

            var badges = c.SectionBadges(ids);

            Assert.Equal(ids.Count, badges.Values.Distinct().Count());
            Assert.All(new[] { "grid", "assembly", "tools.measures", mine }, id => Assert.StartsWith("M", badges[id]));
            Assert.All(badges.Values, b => Assert.InRange(b.Length, 1, 2)); // sem "Mo2"
            Assert.Equal("Me", badges["tools.measures"]);
        }
    }

    public class PanelSectionLayoutCustomSectionsTests
    {
        [Fact]
        public void CreatedSections_AreKnown_AppendedVisible_AndDroppedWhenRemoved()
        {
            var layout = new PanelSectionLayout(null, new[] { "custom.a" });

            Assert.Equal("custom.a", layout.Sections.Last().Id);
            Assert.True(layout.IsVisible("custom.a"));
            Assert.True(layout.MoveUp("custom.a"));
            Assert.True(layout.SetVisible("custom.a", false));
            Assert.Contains(layout.ToPersist(), e => e.Id == "custom.a");

            var removed = layout.WithCustomSections(Array.Empty<string>());
            Assert.DoesNotContain(removed.ToPersist(), e => e.Id == "custom.a");
            Assert.Equal(PanelSectionCatalog.All.Count, removed.Sections.Count);
        }

        [Fact]
        public void ResetToDefault_KeepsCreatedSectionsVisibleAtTheEnd()
        {
            var layout = new PanelSectionLayout(new[] { new PanelSectionLayoutEntry("custom.a", false), new PanelSectionLayoutEntry("grid", true) }, new[] { "custom.a" });

            layout.ResetToDefault();

            Assert.Equal(PanelSectionCatalog.All.Select(s => s.Id).Concat(new[] { "custom.a" }), layout.Sections.Select(e => e.Id));
            Assert.True(layout.IsDefault);
        }
    }

    [Collection("PXToolBox Dev shared filesystem resources")]
    public class PanelCustomizationStoreTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "pxtoolbox-tests", "panel-custom-" + Guid.NewGuid().ToString("N"));

        public PanelCustomizationStoreTests()
        {
            Directory.CreateDirectory(_dir);
            ListForgeIntegratedSettingsStore.SetConfigDirectoryForTesting(_dir);
        }

        public void Dispose()
        {
            ListForgeIntegratedSettingsStore.SetConfigDirectoryForTesting(null);
            try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private string FilePath => Path.Combine(_dir, "listforge-integrated-settings.json");

        [Fact]
        public void SaveAndLoad_RoundTrip_TogetherWithTheLayout_AndRegularSaveKeepsIt()
        {
            ListForgeIntegratedSettingsStore.Save(ListForgeIntegratedSettings.Default());
            var oldCopy = ListForgeIntegratedSettingsStore.Load().Settings;
            var c = PanelCustomization.Default();
            c.Rename("tools.objects", "Objetos do dia");
            var mine = c.AddSection("Meu dia a dia", () => "d");
            c.Place("grid.sisbolt.free-hand", mine);
            var layout = new PanelSectionLayout(null, new[] { mine });
            layout.MoveUp(mine);

            ListForgeIntegratedSettingsStore.SavePanelCustomization(c, layout.ToPersist());
            ListForgeIntegratedSettingsStore.Save(oldCopy); // janela do ListForge com cópia antiga

            var loaded = ListForgeIntegratedSettingsStore.LoadPanelCustomization();
            Assert.Equal("Objetos do dia", loaded.TitleOf("tools.objects"));
            Assert.Equal("Meu dia a dia", loaded.TitleOf(mine));
            Assert.Equal(mine, loaded.SectionOf("grid.sisbolt.free-hand"));
            var loadedLayout = new PanelSectionLayout(ListForgeIntegratedSettingsStore.LoadPanelSectionLayout().Entries, loaded.CustomSections.Select(s => s.Id));
            Assert.Equal(layout.Sections.Select(e => e.Id), loadedLayout.Sections.Select(e => e.Id));
            Assert.Null(((JObject)JObject.Parse(File.ReadAllText(FilePath))["PanelCustomization"]!)["IsDefault"]);
        }

        [Fact]
        public void Default_IsNotWritten_AndBadFieldLoadsDefault()
        {
            ListForgeIntegratedSettingsStore.SavePanelCustomization(PanelCustomization.Default(), PanelSectionLayout.Default().ToPersist());
            Assert.DoesNotContain("PanelCustomization", File.ReadAllText(FilePath));

            var root = JObject.Parse(File.ReadAllText(FilePath));
            root["PanelCustomization"] = "não é objeto";
            File.WriteAllText(FilePath, root.ToString());
            Assert.True(ListForgeIntegratedSettingsStore.LoadPanelCustomization().IsDefault);
        }
    }

    [Collection("WPF created in code — no parallel WPF initialization")]
    public class PanelCustomizationWindowEditingTests
    {
        private static void RunSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw new Xunit.Sdk.XunitException(failure.ToString());
        }

        [Fact]
        public void NewSection_ToolPlacement_AndRename_GoToSaveTogether()
        {
            RunSta(() =>
            {
                PanelSectionLayout? savedLayout = null;
                PanelCustomization? saved = null;
                var window = new PanelCustomizationWindow(PanelSectionLayout.Default(), PanelCustomization.Default(),
                    (layout, custom) => { savedLayout = layout; saved = custom; return null; });

                var mine = window.AddSection("Meu dia a dia");
                window.PlaceTool("grid.sisbolt.free-hand", mine);
                window.DraftCustomization.Rename("tools.objects", "Objetos do dia");

                Assert.Equal("Meu dia a dia", window.DisplayedRows.Last().Title);
                Assert.Contains(("Alternar Mão Livre", "Meu dia a dia"), window.DisplayedTools);
                Assert.True(window.TrySave());
                Assert.Equal(mine, saved!.SectionOf("grid.sisbolt.free-hand"));
                Assert.Equal("Objetos do dia", saved.TitleOf("tools.objects"));
                Assert.Contains(savedLayout!.Sections, e => e.Id == mine);

                window.RemoveSection(mine);
                Assert.Contains(("Alternar Mão Livre", "Montagem"), window.DisplayedTools);
                window.RestoreDefault();
                Assert.True(window.DraftCustomization.IsDefault);
                Assert.True(window.Draft.IsDefault);
                window.Close();
            });
        }
    }
}
#endif
