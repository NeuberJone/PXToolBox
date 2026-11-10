#if NET48
// Só compila na perna net48 — PXToolBoxDev (host) só existe lá. "Personalizar painel…": modelo
// (PanelLayout/), persistência (ListForgeIntegratedSettingsStore), preferências portáteis e a
// janela de rascunho. Nada aqui abre o CorelDRAW nem constrói o ControlUI.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Newtonsoft.Json.Linq;
using PXToolBoxDev.Favorites;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.PanelLayout;
using Xunit;

namespace PXToolBox.Tests
{
    public class PanelSectionLayoutModelTests
    {
        // Reorganização de 2026-11-06 (escolhida pelo usuário): Montagem depois do Grid, ordem
        // pelo fluxo de trabalho. Ids antigos inalterados.
        private static readonly string[] DefaultOrder =
        {
            "favorites", "grid", "assembly", "molds", "codes", "layers",
            "tools.objects", "tools.piece-type", "tools.measures", "output", "production", "lists", "diagnostics",
        };

        private static PanelSectionLayoutEntry E(string id, bool visible = true) => new PanelSectionLayoutEntry(id, visible);

        // Ids são contrato de persistência; a ordem do catálogo é o layout padrão (a do painel
        // antes da personalização).
        [Fact]
        public void SavedLayoutOfDev72_GetsAssemblyRightAfterGrid_KeepingTheUserOrder()
        {
            var dev72 = new[] { "favorites", "grid", "codes", "layers", "output", "tools.piece-type", "tools.objects", "tools.measures", "lists", "production", "diagnostics" };

            var layout = new PanelSectionLayout(dev72.Select(id => E(id)));

            Assert.Equal(new[] { "favorites", "grid", "assembly", "molds", "codes", "layers", "output", "tools.piece-type", "tools.objects", "tools.measures", "lists", "production", "diagnostics" },
                layout.Sections.Select(e => e.Id));
        }

        [Fact]
        public void Catalog_IdsAreStable_AndDefaultIsCurrentOrderAllVisible()
        {
            Assert.Equal(DefaultOrder, PanelSectionCatalog.All.Select(s => s.Id));
            var layout = PanelSectionLayout.Default();
            Assert.Equal(DefaultOrder, layout.Sections.Select(e => e.Id));
            Assert.All(layout.Sections, e => Assert.True(e.Visible));
            Assert.True(layout.IsDefault);
        }

        [Fact]
        public void StoredLayout_KeepsKnownChoices_InsertsMissingSectionsVisibleAfterTheirCatalogPredecessor()
        {
            // Como um layout salvo antes de existirem "assembly", "lists", "production" e
            // "diagnostics": a ordem escolhida pelo usuário fica; cada seção nova entra logo depois
            // da que a antecede no catálogo (Montagem depois do Grid, não no fim).
            var layout = new PanelSectionLayout(new[]
            {
                E("grid"), E("favorites", visible: false), E("codes"), E("layers"), E("output"),
                E("tools.piece-type"), E("tools.objects"), E("tools.measures"),
            });

            Assert.Equal(new[] { "grid", "assembly", "molds", "favorites", "codes", "layers", "output", "production", "lists", "diagnostics", "tools.piece-type", "tools.objects", "tools.measures" },
                layout.Sections.Select(e => e.Id));
            Assert.True(layout.IsVisible("assembly"));
            Assert.False(layout.IsVisible("favorites"));
            Assert.True(layout.IsVisible("lists"));
            Assert.True(layout.IsVisible("diagnostics"));
        }

        [Fact]
        public void UnknownAndInvalidEntries_AreHidden_UnknownPreservedAtTheEnd()
        {
            var layout = new PanelSectionLayout(new[] { E("future.section", false), null, E(""), E("grid", false), E("grid", true), E("codes") });

            Assert.DoesNotContain(layout.Sections, e => e.Id == "future.section");
            var ids = layout.Sections.Select(e => e.Id).ToList();
            Assert.True(ids.IndexOf("grid") < ids.IndexOf("codes"));
            Assert.False(layout.IsVisible("grid")); // primeira ocorrência vale
            var persisted = layout.ToPersist();
            Assert.Equal("future.section", persisted.Last().Id);
            Assert.False(persisted.Last().Visible);
            Assert.Equal(PanelSectionCatalog.All.Count + 1, persisted.Count);
        }

        [Fact]
        public void MoveAndVisibility_ChangeOnlyKnownSections()
        {
            var layout = PanelSectionLayout.Default();

            Assert.False(layout.MoveUp("favorites"));
            Assert.True(layout.MoveUp("diagnostics"));
            Assert.Equal("diagnostics", layout.Sections[11].Id);
            Assert.False(layout.MoveDown("future.section"));
            Assert.True(layout.SetVisible("codes", false));
            Assert.False(layout.SetVisible("codes", false));
            Assert.False(layout.SetVisible("future.section", false));
            Assert.False(layout.IsDefault);
        }

        [Fact]
        public void ResetToDefault_RestoresOrderAndVisibility_KeepsUnknown()
        {
            var layout = new PanelSectionLayout(new[] { E("diagnostics"), E("future.section"), E("favorites", false) });

            layout.ResetToDefault();

            Assert.True(layout.IsDefault);
            Assert.Equal(DefaultOrder, layout.Sections.Select(e => e.Id));
            Assert.Equal("future.section", layout.ToPersist().Last().Id);
        }
    }

    [Collection("PXToolBox Dev shared filesystem resources")]
    public class PanelSectionLayoutStoreTests : IDisposable
    {
        private readonly string _tempDirectory = Path.Combine(
            Path.GetTempPath(), "pxtoolbox-tests", "panel-layout-" + Guid.NewGuid().ToString("N"));

        public PanelSectionLayoutStoreTests()
        {
            Directory.CreateDirectory(_tempDirectory);
            ListForgeIntegratedSettingsStore.SetConfigDirectoryForTesting(_tempDirectory);
        }

        public void Dispose()
        {
            ListForgeIntegratedSettingsStore.SetConfigDirectoryForTesting(null);
            try { Directory.Delete(_tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private string FilePath => Path.Combine(_tempDirectory, "listforge-integrated-settings.json");

        private static List<PanelSectionLayoutEntry> Customized()
        {
            var layout = PanelSectionLayout.Default();
            layout.MoveUp("grid");
            layout.SetVisible("diagnostics", false);
            return layout.ToPersist();
        }

        [Fact]
        public void NoFileOrOldConfiguration_LoadsDefault_AndRegularSaveDoesNotAddTheField()
        {
            Assert.Null(ListForgeIntegratedSettingsStore.LoadPanelSectionLayout().Entries);

            ListForgeIntegratedSettingsStore.Save(ListForgeIntegratedSettings.Default());
            ListForgeIntegratedSettingsStore.Save(ListForgeIntegratedSettingsStore.Load().Settings);

            var loaded = ListForgeIntegratedSettingsStore.LoadPanelSectionLayout();
            Assert.Null(loaded.Entries);
            Assert.Equal("", loaded.Notice);
            Assert.DoesNotContain(nameof(ListForgeIntegratedSettings.PanelSectionLayout), File.ReadAllText(FilePath));
        }

        [Fact]
        public void SaveLayout_WritesOnlyThatField_AndKeepsFavoritesAndUnknownFields()
        {
            ListForgeIntegratedSettingsStore.SavePanelFavorites(new[] { PanelToolCatalog.LayersToggleC, PanelToolCatalog.GridPrepare });
            var json = JObject.Parse(File.ReadAllText(FilePath));
            json["CampoDeVersaoFutura"] = "preservar";
            File.WriteAllText(FilePath, json.ToString());

            ListForgeIntegratedSettingsStore.SavePanelSectionLayout(Customized());

            var stored = JObject.Parse(File.ReadAllText(FilePath));
            Assert.Equal("preservar", stored.Value<string>("CampoDeVersaoFutura"));
            Assert.Equal(new[] { PanelToolCatalog.LayersToggleC, PanelToolCatalog.GridPrepare }, ListForgeIntegratedSettingsStore.LoadPanelFavorites().Ids);
            var layout = new PanelSectionLayout(ListForgeIntegratedSettingsStore.LoadPanelSectionLayout().Entries);
            Assert.Equal("grid", layout.Sections[0].Id);
            Assert.False(layout.IsVisible("diagnostics"));
        }

        [Fact]
        public void RestoringDefaultLayout_KeepsFavoritesListAndOrder()
        {
            ListForgeIntegratedSettingsStore.SavePanelFavorites(new[] { PanelToolCatalog.OutputPrepareNewMold, PanelToolCatalog.LayersToggleMa });
            ListForgeIntegratedSettingsStore.SavePanelSectionLayout(Customized());

            var layout = new PanelSectionLayout(ListForgeIntegratedSettingsStore.LoadPanelSectionLayout().Entries);
            layout.ResetToDefault();
            ListForgeIntegratedSettingsStore.SavePanelSectionLayout(layout.ToPersist());

            Assert.True(new PanelSectionLayout(ListForgeIntegratedSettingsStore.LoadPanelSectionLayout().Entries).IsDefault);
            Assert.Equal(new[] { PanelToolCatalog.OutputPrepareNewMold, PanelToolCatalog.LayersToggleMa }, ListForgeIntegratedSettingsStore.LoadPanelFavorites().Ids);
        }

        [Fact]
        public void RegularSave_FromStaleCopy_KeepsLayoutOnDisk()
        {
            ListForgeIntegratedSettingsStore.Save(ListForgeIntegratedSettings.Default());
            var staleCopy = ListForgeIntegratedSettingsStore.Load().Settings; // ex.: ListForge Integrado aberto
            ListForgeIntegratedSettingsStore.SavePanelSectionLayout(Customized());

            staleCopy.EditorFontSize = 18;
            ListForgeIntegratedSettingsStore.Save(staleCopy);

            var loaded = ListForgeIntegratedSettingsStore.Load().Settings;
            Assert.Equal(18, loaded.EditorFontSize);
            Assert.Equal("grid", loaded.PanelSectionLayout![0].Id);
        }

        [Fact]
        public void SaveIncludingLayout_WritesTheObjectsValue_AndNotifiesOnlyLayout()
        {
            ListForgeIntegratedSettingsStore.SavePanelFavorites(new[] { PanelToolCatalog.GridPrepare });
            var imported = ListForgeIntegratedSettings.Default();
            imported.PanelSectionLayout = Customized();
            int layoutNotified = 0, favoritesNotified = 0;
            Action onLayout = () => layoutNotified++;
            Action onFavorites = () => favoritesNotified++;
            ListForgeIntegratedSettingsStore.PanelSectionLayoutChanged += onLayout;
            ListForgeIntegratedSettingsStore.PanelFavoritesChanged += onFavorites;
            try
            {
                ListForgeIntegratedSettingsStore.Save(imported, includePanelSectionLayout: true);
            }
            finally
            {
                ListForgeIntegratedSettingsStore.PanelSectionLayoutChanged -= onLayout;
                ListForgeIntegratedSettingsStore.PanelFavoritesChanged -= onFavorites;
            }

            Assert.Equal(1, layoutNotified);
            Assert.Equal(0, favoritesNotified);
            Assert.Equal("grid", ListForgeIntegratedSettingsStore.LoadPanelSectionLayout().Entries![0].Id);
            Assert.Equal(new[] { PanelToolCatalog.GridPrepare }, ListForgeIntegratedSettingsStore.LoadPanelFavorites().Ids);
        }

        [Fact]
        public void CorruptOrWrongType_PanelOpensWithDefaultAndNotice_FileUntouched()
        {
            File.WriteAllText(FilePath, "{ isto não é json");
            var corrupt = ListForgeIntegratedSettingsStore.LoadPanelSectionLayout();
            Assert.Null(corrupt.Entries);
            Assert.NotEqual("", corrupt.Notice);
            Assert.Throws<InvalidOperationException>(() => ListForgeIntegratedSettingsStore.SavePanelSectionLayout(Customized()));
            Assert.Equal("{ isto não é json", File.ReadAllText(FilePath));

            var json = JObject.FromObject(ListForgeIntegratedSettings.Default());
            json[nameof(ListForgeIntegratedSettings.PanelSectionLayout)] = "grid";
            File.WriteAllText(FilePath, json.ToString());
            var wrongType = ListForgeIntegratedSettingsStore.LoadPanelSectionLayout();
            Assert.Null(wrongType.Entries);
            Assert.NotEqual("", wrongType.Notice);
        }

        [Fact]
        public void InvalidItems_AreSkipped_ValidOnesKept()
        {
            var json = JObject.FromObject(ListForgeIntegratedSettings.Default());
            json[nameof(ListForgeIntegratedSettings.PanelSectionLayout)] = JArray.Parse(
                "[ { \"Id\": \"codes\", \"Visible\": false }, { \"Id\": 3 }, \"grid\", { \"Id\": \"grid\", \"Visible\": \"sim\" }, { \"Id\": \"layers\" } ]");
            File.WriteAllText(FilePath, json.ToString());

            var loaded = ListForgeIntegratedSettingsStore.LoadPanelSectionLayout();

            Assert.Equal("", loaded.Notice);
            Assert.Equal(new[] { "codes", "layers" }, loaded.Entries!.Select(e => e.Id));
            Assert.False(loaded.Entries![0].Visible);
            Assert.True(loaded.Entries![1].Visible);
        }
    }

    public class PanelSectionLayoutPortablePreferencesTests
    {
        private const string Header = "\"formatId\": \"PxToolBoxDevPortablePreferences\", \"schemaVersion\": 1, \"editorFontSize\": 14";

        [Fact]
        public void ExportImport_RoundTripsOrderAndVisibility()
        {
            var source = ListForgeIntegratedSettings.Default();
            var layout = PanelSectionLayout.Default();
            layout.MoveDown("favorites");
            layout.SetVisible("production", false);
            source.PanelSectionLayout = layout.ToPersist();
            var json = PortablePreferencesService.Serialize(PortablePreferencesService.Export(source));

            var validation = PortablePreferencesService.Validate(json);
            var target = ListForgeIntegratedSettings.Default();
            PortablePreferencesService.Apply(validation.Document!, target);

            Assert.True(validation.IsValid);
            Assert.Empty(validation.IncompatibleFieldSummaries);
            Assert.Contains("\"panelSectionLayout\"", json);
            Assert.Equal(layout.Sections.Select(e => (e.Id, e.Visible)), target.PanelSectionLayout!.Select(e => (e.Id, e.Visible)));
        }

        // Arquivo exportado antes desta etapa (só favoritos): importa os favoritos e mantém o
        // layout atual.
        [Fact]
        public void OldFileWithoutLayout_KeepsCurrentLayout()
        {
            var json = "{ " + Header + ", \"panelFavoriteToolIds\": [\"grid.prepare\"] }";
            var target = ListForgeIntegratedSettings.Default();
            var current = PanelSectionLayout.Default();
            current.SetVisible("codes", false);
            target.PanelSectionLayout = current.ToPersist();

            var validation = PortablePreferencesService.Validate(json);
            PortablePreferencesService.Apply(validation.Document!, target);

            Assert.True(validation.IsValid);
            Assert.Null(validation.Document!.PanelSectionLayout);
            Assert.False(new PanelSectionLayout(target.PanelSectionLayout).IsVisible("codes"));
            Assert.Equal(new[] { PanelToolCatalog.GridPrepare }, target.PanelFavoriteToolIds);
        }

        [Fact]
        public void PartialLayout_FromOlderVersion_AppendsNewSectionsVisible_AndReportsUnknown()
        {
            var json = "{ " + Header + ", \"panelSectionLayout\": [ { \"id\": \"diagnostics\", \"visible\": false }, { \"id\": \"grid\" }, { \"id\": \"future.section\" }, 5 ] }";
            var target = ListForgeIntegratedSettings.Default();

            var validation = PortablePreferencesService.Validate(json);
            PortablePreferencesService.Apply(validation.Document!, target);

            Assert.True(validation.IsValid);
            Assert.Equal(2, validation.IncompatibleFieldSummaries.Count(s => s.StartsWith("Seção do painel desconhecida")));
            var applied = new PanelSectionLayout(target.PanelSectionLayout);
            // A ordem importada fica; as seções que o arquivo não traz entram junto das vizinhas do catálogo.
            Assert.Equal(new[] { "diagnostics", "grid" }, applied.Sections.Select(e => e.Id).Where(id => id == "diagnostics" || id == "grid"));
            Assert.False(applied.IsVisible("diagnostics"));
            Assert.Equal(PanelSectionCatalog.All.Count, applied.Sections.Count);
            Assert.True(applied.IsVisible("favorites"));
        }

        [Fact]
        public void LayoutWithWrongType_IsIncompatible_NotFatal()
        {
            var validation = PortablePreferencesService.Validate("{ " + Header + ", \"panelSectionLayout\": \"grid\" }");

            Assert.True(validation.IsValid);
            Assert.Null(validation.Document!.PanelSectionLayout);
            Assert.Contains(validation.IncompatibleFieldSummaries, s => s.StartsWith("Layout do painel com tipo inesperado"));
        }
    }

    // Ver WpfCodeCreatedControlsCollection (TestCollections.cs).
    [Collection("WPF created in code — no parallel WPF initialization")]
    public class PanelCustomizationWindowTests
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

        private static T Find<T>(DependencyObject root, string automationName) where T : DependencyObject
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                if (child is T match && AutomationProperties.GetName(child) == automationName) return match;
                var found = FindOrNull<T>(child, automationName);
                if (found != null) return found;
            }

            throw new InvalidOperationException($"Controle \"{automationName}\" não encontrado.");
        }

        private static T? FindOrNull<T>(DependencyObject root, string automationName) where T : DependencyObject
        {
            try { return Find<T>(root, automationName); } catch (InvalidOperationException) { return null; }
        }

        private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));

        [Fact]
        public void EditsStayInTheDraft_UntilSave_AndCancelSavesNothing()
        {
            RunSta(() =>
            {
                var current = PanelSectionLayout.Default();
                var saved = new List<PanelSectionLayout>();
                var window = new PanelCustomizationWindow(current, draft => { saved.Add(draft); return null; });

                Click(Find<Button>(window, "Mover \"Grid\" para cima"));
                var codes = Find<CheckBox>(window, "Exibir \"Códigos\"");
                codes.IsChecked = false;
                Click(codes);

                // O layout em uso não mudou; nada foi gravado (fechar sem salvar = cancelar).
                Assert.True(current.IsDefault);
                Assert.Empty(saved);
                Assert.Equal("Grid", window.DisplayedRows[0].Title);
                Assert.False(window.DisplayedRows[0].CanMoveUp);
                Assert.False(window.DisplayedRows.Single(r => r.Title == "Códigos").Visible);
                window.Close();
                Assert.Empty(saved);
            });
        }

        [Fact]
        public void Save_HandsTheDraftToTheCallback()
        {
            RunSta(() =>
            {
                PanelSectionLayout? saved = null;
                var window = new PanelCustomizationWindow(PanelSectionLayout.Default(), draft => { saved = draft; return null; });
                Click(Find<Button>(window, "Mover \"Diagnóstico\" para cima"));

                Assert.True(window.TrySave());
                Assert.Equal("diagnostics", saved!.Sections[11].Id);
                Assert.Equal("", window.ErrorText);
                window.Close();
            });
        }

        [Fact]
        public void SaveFailure_ShowsError_AndKeepsTheDraft()
        {
            RunSta(() =>
            {
                var window = new PanelCustomizationWindow(PanelSectionLayout.Default(), _ => "Não foi possível salvar o layout: disco cheio.");
                Click(Find<Button>(window, "Mover \"Listas\" para cima"));

                Assert.False(window.TrySave());
                Assert.Contains("disco cheio", window.ErrorText);
                Assert.Equal("lists", window.Draft.Sections[10].Id);
                window.Close();
            });
        }

        [Fact]
        public void RestoreDefault_ResetsOnlyTheDraft()
        {
            RunSta(() =>
            {
                var stored = new PanelSectionLayout(new[] { new PanelSectionLayoutEntry("diagnostics", true), new PanelSectionLayoutEntry("favorites", false) });
                PanelSectionLayout? saved = null;
                var window = new PanelCustomizationWindow(stored, draft => { saved = draft; return null; });

                window.RestoreDefault();

                Assert.Null(saved); // ainda precisa de "Salvar"
                Assert.True(window.Draft.IsDefault);
                Assert.All(window.DisplayedRows, r => Assert.True(r.Visible));
                Assert.Equal(PanelSectionCatalog.All.Select(s => s.Title), window.DisplayedRows.Select(r => r.Title));
                Assert.False(stored.IsDefault); // o original não foi tocado
                Assert.True(window.TrySave());
                Assert.True(saved!.IsDefault);
                window.Close();
            });
        }
    }
}
#endif
