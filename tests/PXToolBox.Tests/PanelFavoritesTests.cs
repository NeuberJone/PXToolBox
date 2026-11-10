#if NET48
// Só compila na perna net48 — PXToolBoxDev (host) só existe lá. Favoritos do painel: lista e
// catálogo (Favorites/), persistência (ListForgeIntegratedSettingsStore), preferências portáteis e
// o atalho ligado ao botão original. Nada aqui abre o CorelDRAW.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.Favorites;
using Xunit;

namespace PXToolBox.Tests
{
    public class PanelFavoritesListTests
    {
        [Fact]
        public void StoredIds_DropBlanksAndDuplicates_KeepOrder()
        {
            var list = new PanelFavoritesList(new[] { PanelToolCatalog.LayersToggleFe, "", null, " ", PanelToolCatalog.GridPrepare, PanelToolCatalog.LayersToggleFe });

            Assert.Equal(new[] { PanelToolCatalog.LayersToggleFe, PanelToolCatalog.GridPrepare }, list.IdsToPersist);
        }

        [Fact]
        public void UnknownIds_AreHidden_ButPreservedWhenSaving()
        {
            var list = new PanelFavoritesList(new[] { "future.tool", PanelToolCatalog.GridPrepare });

            Assert.Equal(new[] { PanelToolCatalog.GridPrepare }, list.Visible.Select(t => t.Id));
            Assert.False(list.Contains("future.tool"));
            Assert.Equal(new[] { PanelToolCatalog.GridPrepare, "future.tool" }, list.IdsToPersist);
        }

        [Fact]
        public void Add_OnlyKnownAndNotYetPresent()
        {
            var list = new PanelFavoritesList(null);

            Assert.True(list.Add(PanelToolCatalog.GridPrepare));
            Assert.False(list.Add(PanelToolCatalog.GridPrepare));
            Assert.False(list.Add("future.tool"));
            Assert.Equal(new[] { PanelToolCatalog.GridPrepare }, list.IdsToPersist);
        }

        [Fact]
        public void MoveUpDown_SwapsWithVisibleNeighbor_SkippingUnknown()
        {
            var list = new PanelFavoritesList(new[] { PanelToolCatalog.GridPrepare, "future.tool", PanelToolCatalog.LayersToggleMa, PanelToolCatalog.LayersToggleFe });

            Assert.True(list.MoveUp(PanelToolCatalog.LayersToggleMa));
            Assert.Equal(new[] { PanelToolCatalog.LayersToggleMa, PanelToolCatalog.GridPrepare, PanelToolCatalog.LayersToggleFe }, list.Visible.Select(t => t.Id));
            Assert.False(list.MoveUp(PanelToolCatalog.LayersToggleMa));
            Assert.True(list.MoveDown(PanelToolCatalog.GridPrepare));
            Assert.False(list.MoveDown(PanelToolCatalog.GridPrepare));
            Assert.Equal(new[] { PanelToolCatalog.LayersToggleMa, PanelToolCatalog.LayersToggleFe, PanelToolCatalog.GridPrepare, "future.tool" }, list.IdsToPersist);
        }

        [Theory]
        [InlineData(0, 2, new[] { "layers.toggle-ma", "grid.prepare", "layers.toggle-fe" })]
        [InlineData(0, 3, new[] { "layers.toggle-ma", "layers.toggle-fe", "grid.prepare" })]
        [InlineData(2, 0, new[] { "layers.toggle-fe", "grid.prepare", "layers.toggle-ma" })]
        [InlineData(2, 1, new[] { "grid.prepare", "layers.toggle-fe", "layers.toggle-ma" })]
        public void MoveTo_InsertsBeforeTheGivenVisiblePosition(int from, int insertBefore, string[] expected)
        {
            var ids = new[] { PanelToolCatalog.GridPrepare, PanelToolCatalog.LayersToggleMa, PanelToolCatalog.LayersToggleFe };
            var list = new PanelFavoritesList(ids);

            Assert.True(list.MoveTo(ids[from], insertBefore));
            Assert.Equal(expected, list.Visible.Select(t => t.Id));
        }

        [Fact]
        public void MoveTo_SamePlaceOrInvalid_ChangesNothing_AndKeepsUnknownAtTheEnd()
        {
            var list = new PanelFavoritesList(new[] { PanelToolCatalog.GridPrepare, "future.tool", PanelToolCatalog.LayersToggleMa, PanelToolCatalog.LayersToggleFe });

            Assert.False(list.MoveTo(PanelToolCatalog.LayersToggleMa, 1));
            Assert.False(list.MoveTo(PanelToolCatalog.LayersToggleMa, 2));
            Assert.False(list.MoveTo(PanelToolCatalog.LayersToggleMa, 4));
            Assert.False(list.MoveTo(PanelToolCatalog.LayersToggleMa, -1));
            Assert.False(list.MoveTo("future.tool", 0));
            Assert.False(list.MoveTo(PanelToolCatalog.LayersToggleC, 0));

            Assert.True(list.MoveTo(PanelToolCatalog.LayersToggleFe, 0));
            Assert.Equal(new[] { PanelToolCatalog.LayersToggleFe, PanelToolCatalog.GridPrepare, PanelToolCatalog.LayersToggleMa, "future.tool" }, list.IdsToPersist);
        }

        [Fact]
        public void Remove_TakesOutOnlyThatTool()
        {
            var list = new PanelFavoritesList(new[] { PanelToolCatalog.GridPrepare, PanelToolCatalog.LayersToggleC });

            Assert.True(list.Remove(PanelToolCatalog.GridPrepare));
            Assert.False(list.Remove(PanelToolCatalog.GridPrepare));
            Assert.Equal(new[] { PanelToolCatalog.LayersToggleC }, list.IdsToPersist);
        }

        // Ids são contrato de persistência: mudar algum aqui quebra favoritos já salvos. Os 10
        // da dev.50 continuam primeiro, iguais; todo botão de ferramenta do painel está no
        // catálogo (a correspondência com o XAML é conferida carregando o painel — ver
        // docs/ARCHITECTURE.md, 35.2).
        [Fact]
        public void Catalog_KeepsDev50IdsFirst_CoversEveryToolButton_AndIdsAreUnique()
        {
            Assert.Equal(new[]
            {
                "grid.prepare", "grid.standard-size.toggle", "grid.standard-size.update",
                "layers.toggle-ma", "layers.toggle-fe", "layers.toggle-c", "layers.hide-selected", "layers.restore-last-hidden",
                "output.prepare-new-grid", "output.prepare-new-mold",
            }, PanelToolCatalog.All.Take(10).Select(t => t.Id));
            Assert.Equal(81, PanelToolCatalog.All.Count);
            Assert.Equal(PanelToolCatalog.All.Count, PanelToolCatalog.All.Select(t => t.Id).Distinct().Count());
            Assert.Equal(PanelToolCatalog.All.Count, PanelToolCatalog.All.Select(t => t.Label).Distinct().Count());
            Assert.Contains(PanelToolCatalog.All, t => t.Id == "tools.objects.group-intersection");
            Assert.Contains(PanelToolCatalog.All, t => t.Id == "output.prepare-shield");
            Assert.Contains(PanelToolCatalog.All, t => t.Section == "Diagnóstico");
            Assert.All(PanelToolCatalog.All, t => Assert.False(string.IsNullOrEmpty(t.Icon.Symbol)));
        }

        // Pedido do usuário (2026-11-06): ícones não podem se repetir — nem com marca diferente
        // no canto. Remoção (vermelho com moldura) é outro ícone mesmo com a mesma sigla.
        [Fact]
        public void Catalog_EveryToolHasItsOwnIcon()
        {
            var repeated = PanelToolCatalog.All
                .GroupBy(t => (t.Icon.Symbol, t.Icon.IsText, t.Icon.IsRemoval))
                .Where(g => g.Count() > 1)
                .Select(g => string.Join(" / ", g.Select(t => t.Label)))
                .ToList();

            Assert.Empty(repeated);
        }

        // Pedido do usuário (2026-11-06): marca do canto = inicial da seção; mesma inicial →
        // primeira e segunda letras. Cada seção com uma marca diferente; remoção mostra "−".
        [Fact]
        public void Catalog_CornerBadge_IsTheSectionInitials_UniquePerSection()
        {
            Assert.All(PanelToolCatalog.All, t => Assert.True(t.Icon.IsRemoval || PanelToolCatalog.SectionBadges.ContainsKey(t.Section), t.Section));
            Assert.Equal(PanelToolCatalog.SectionBadges.Count, PanelToolCatalog.SectionBadges.Values.Distinct().Count());
            Assert.Equal("G", PanelToolCatalog.Find(PanelToolCatalog.GridPrepare)!.Badge);
            Assert.Equal("Co", PanelToolCatalog.Find("codes.toggle-no")!.Badge);
            Assert.Equal("Ca", PanelToolCatalog.Find(PanelToolCatalog.LayersToggleMa)!.Badge);
            Assert.Equal("PS", PanelToolCatalog.Find("output.prepare-shield")!.Badge);
            Assert.Equal("Pr", PanelToolCatalog.Find("production.open-nexor")!.Badge);
            Assert.Equal("Mo", PanelToolCatalog.Find("grid.sisbolt.free-hand")!.Badge);
            Assert.Equal("Me", PanelToolCatalog.Find("tools.measures.sum-height")!.Badge);
            Assert.Equal("O", PanelToolCatalog.Find("tools.objects.group-intersection")!.Badge);
            Assert.DoesNotContain(PanelToolCatalog.All, t => t.Label.IndexOf("SISBolt", StringComparison.OrdinalIgnoreCase) >= 0
                || t.Section.IndexOf("SISBolt", StringComparison.OrdinalIgnoreCase) >= 0 || t.Section.StartsWith("Outras ferramentas", StringComparison.Ordinal));
            Assert.Equal("−", PanelToolCatalog.Find("codes.remove-no-page")!.Badge);
        }

        [Fact]
        public void Catalog_SisboltTools_HaveNoCornerS_AndFreeHandIsAHand()
        {
            var sisbolt = PanelToolCatalog.All.Where(t => t.Id.StartsWith("grid.sisbolt.", StringComparison.Ordinal)).ToList();

            Assert.Equal(6, sisbolt.Count);
            Assert.All(sisbolt, t => Assert.Equal("Montagem", t.Section));
            Assert.All(sisbolt, t => Assert.Equal("Mo", t.Badge));
            // Pedido do usuário: o que liga/desliga se chama "Alternar …", como o Tamanho Padrão.
            Assert.Equal(new[] { "Alternar Esticar no Tamanho do Molde", "Alternar Ignorar métricas da variável", "Alternar Mão Livre" },
                new[] { "grid.sisbolt.stretch-to-mold", "grid.sisbolt.ignore-variable-metrics", "grid.sisbolt.free-hand" }.Select(id => PanelToolCatalog.Find(id)!.Label));
            Assert.Equal("\uE7C9", PanelToolCatalog.Find("grid.sisbolt.free-hand")!.Icon.Symbol);
        }

        // Ícones de remoção (vermelhos nos Favoritos): exatamente os "Remover … da página".
        [Fact]
        public void Catalog_RemovalIcons_AreTheCodeRemovalTools()
        {
            Assert.Equal(
                new[] { "codes.remove-no-nu-page", "codes.remove-no-page", "codes.remove-nu-page", "codes.remove-ap-page", "codes.remove-ts-page", "codes.remove-all-page" },
                PanelToolCatalog.All.Where(t => t.Icon.IsRemoval).Select(t => t.Id));
        }
    }

    // Geometria do arrastar e soltar: ícones de 36 com 4 de margem, quebrando em duas linhas
    // (3 na primeira, 2 na segunda). O arraste em si (captura do mouse) só é exercitado com o
    // painel carregado — ver docs/ARCHITECTURE.md, 35.4.
    public class FavoriteDragReorderTests
    {
        private static readonly System.Windows.Rect[] TwoRows =
        {
            new System.Windows.Rect(0, 0, 36, 36), new System.Windows.Rect(40, 0, 36, 36), new System.Windows.Rect(80, 0, 36, 36),
            new System.Windows.Rect(0, 40, 36, 36), new System.Windows.Rect(40, 40, 36, 36),
        };

        [Theory]
        [InlineData(5, 10, 0)]    // metade esquerda do primeiro
        [InlineData(30, 10, 1)]   // metade direita do primeiro → antes do segundo
        [InlineData(115, 10, 3)]  // fim da primeira linha
        [InlineData(150, 20, 3)]  // à direita da primeira linha, fora dos ícones
        [InlineData(10, 39, 3)]   // no vão entre as linhas, mais perto da segunda
        [InlineData(70, 60, 5)]   // fim da segunda linha = fim da lista
        [InlineData(10, 120, 3)]  // abaixo de tudo: linha mais próxima (a segunda)
        [InlineData(50, -20, 1)]  // acima de tudo: primeira linha
        public void InsertionIndex_UsesNearestRowAndItemCenters(double x, double y, int expected)
        {
            Assert.Equal(expected, FavoriteDragReorder.InsertionIndex(TwoRows, new System.Windows.Point(x, y)));
        }

        [Fact]
        public void InsertionIndex_Empty_IsZero()
        {
            Assert.Equal(0, FavoriteDragReorder.InsertionIndex(new System.Windows.Rect[0], new System.Windows.Point(10, 10)));
        }

        [Fact]
        public void IndexAt_FindsTheItemUnderThePoint_OrMinusOneInTheGap()
        {
            Assert.Equal(4, FavoriteDragReorder.IndexAt(TwoRows, new System.Windows.Point(50, 50)));
            Assert.Equal(-1, FavoriteDragReorder.IndexAt(TwoRows, new System.Windows.Point(38, 10)));
        }

        // Favoritos com nome: um botão por linha, na largura da seção.
        private static readonly System.Windows.Rect[] OneColumn =
        {
            new System.Windows.Rect(0, 0, 280, 36), new System.Windows.Rect(0, 40, 280, 36), new System.Windows.Rect(0, 80, 280, 36),
        };

        [Theory]
        [InlineData(200, 5, 0)]    // metade de cima do primeiro
        [InlineData(10, 30, 1)]    // metade de baixo do primeiro → antes do segundo
        [InlineData(250, 95, 2)]   // metade de cima do terceiro
        [InlineData(5, 110, 3)]    // metade de baixo do último → fim
        [InlineData(150, -20, 0)]  // acima de tudo
        public void InsertionIndex_SingleColumn_UsesVerticalCenters(double x, double y, int expected)
        {
            Assert.True(FavoriteDragReorder.IsSingleColumn(OneColumn));
            Assert.False(FavoriteDragReorder.IsSingleColumn(TwoRows));
            Assert.Equal(expected, FavoriteDragReorder.InsertionIndex(OneColumn, new System.Windows.Point(x, y)));
        }

        [Fact]
        public void IndicatorBounds_SingleColumn_IsAHorizontalBarAboveTheItem_OrBelowTheLast()
        {
            Assert.Equal(new System.Windows.Rect(0, 37, 280, 2), FavoriteDragReorder.IndicatorBounds(OneColumn, 1, 2));
            Assert.Equal(new System.Windows.Rect(0, 0, 280, 2), FavoriteDragReorder.IndicatorBounds(OneColumn, 0, 2));
            Assert.Equal(new System.Windows.Rect(0, 117, 280, 2), FavoriteDragReorder.IndicatorBounds(OneColumn, 3, 2));
        }

        [Fact]
        public void IndicatorBounds_SitsInTheGapBeforeTheItem_OrAfterTheLast()
        {
            Assert.Equal(new System.Windows.Rect(37, 0, 2, 36), FavoriteDragReorder.IndicatorBounds(TwoRows, 1, 2));
            Assert.Equal(new System.Windows.Rect(0, 40, 2, 36), FavoriteDragReorder.IndicatorBounds(TwoRows, 3, 2));
            Assert.Equal(new System.Windows.Rect(77, 40, 2, 36), FavoriteDragReorder.IndicatorBounds(TwoRows, 5, 2));
        }
    }

    [Collection("PXToolBox Dev shared filesystem resources")]
    public class PanelFavoritesStoreTests : IDisposable
    {
        private readonly string _tempDirectory = Path.Combine(
            Path.GetTempPath(), "pxtoolbox-tests", "panel-favorites-" + Guid.NewGuid().ToString("N"));

        public PanelFavoritesStoreTests()
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

        [Fact]
        public void NoFile_LoadsEmpty_ThenSaveCreatesValidConfiguration()
        {
            Assert.Empty(ListForgeIntegratedSettingsStore.LoadPanelFavorites().Ids);

            ListForgeIntegratedSettingsStore.SavePanelFavorites(new[] { PanelToolCatalog.LayersHideSelected, PanelToolCatalog.GridPrepare });

            var loaded = ListForgeIntegratedSettingsStore.Load();
            Assert.Equal("", loaded.Notice);
            Assert.Equal(new[] { PanelToolCatalog.LayersHideSelected, PanelToolCatalog.GridPrepare }, loaded.Settings.PanelFavoriteToolIds);
            Assert.Equal(new[] { PanelToolCatalog.LayersHideSelected, PanelToolCatalog.GridPrepare }, ListForgeIntegratedSettingsStore.LoadPanelFavorites().Ids);
        }

        [Fact]
        public void OldConfigurationWithoutField_LoadsEmpty_AndSavingFavoritesKeepsEverythingElse()
        {
            var old = ListForgeIntegratedSettings.Default();
            old.EditorFontSize = 21;
            var json = JObject.FromObject(old);
            json.Remove(nameof(ListForgeIntegratedSettings.PanelFavoriteToolIds));
            json["CampoDeVersaoFutura"] = "preservar";
            File.WriteAllText(FilePath, json.ToString());

            var favorites = ListForgeIntegratedSettingsStore.LoadPanelFavorites();
            Assert.Empty(favorites.Ids);
            Assert.Equal("", favorites.Notice);
            Assert.Null(ListForgeIntegratedSettingsStore.Load().Settings.PanelFavoriteToolIds);

            ListForgeIntegratedSettingsStore.SavePanelFavorites(new[] { PanelToolCatalog.GridPrepare });

            var stored = JObject.Parse(File.ReadAllText(FilePath));
            Assert.Equal(21, stored.Value<double>(nameof(ListForgeIntegratedSettings.EditorFontSize)));
            Assert.Equal("preservar", stored.Value<string>("CampoDeVersaoFutura"));
            Assert.Equal(new[] { PanelToolCatalog.GridPrepare }, stored[nameof(ListForgeIntegratedSettings.PanelFavoriteToolIds)]!.Values<string>());
        }

        [Fact]
        public void RegularSave_WithoutFavoritesDefined_DoesNotAddTheField()
        {
            ListForgeIntegratedSettingsStore.Save(ListForgeIntegratedSettings.Default());
            ListForgeIntegratedSettingsStore.Save(ListForgeIntegratedSettingsStore.Load().Settings);

            Assert.DoesNotContain(nameof(ListForgeIntegratedSettings.PanelFavoriteToolIds), File.ReadAllText(FilePath));
        }

        [Fact]
        public void UnknownIds_DoNotBreakLoading_AndSurviveRoundTrip()
        {
            ListForgeIntegratedSettingsStore.SavePanelFavorites(new[] { "future.tool", PanelToolCatalog.LayersToggleC });

            var loaded = ListForgeIntegratedSettingsStore.LoadPanelFavorites();
            var list = new PanelFavoritesList(loaded.Ids);
            Assert.Equal("", loaded.Notice);
            Assert.Equal(new[] { PanelToolCatalog.LayersToggleC }, list.Visible.Select(t => t.Id));

            list.Add(PanelToolCatalog.GridPrepare);
            ListForgeIntegratedSettingsStore.SavePanelFavorites(list.IdsToPersist);
            Assert.Contains("future.tool", ListForgeIntegratedSettingsStore.LoadPanelFavorites().Ids);
            Assert.Equal("", ListForgeIntegratedSettingsStore.Load().Notice);
        }

        [Fact]
        public void RegularSave_FromStaleCopy_KeepsFavoritesOnDisk()
        {
            ListForgeIntegratedSettingsStore.Save(ListForgeIntegratedSettings.Default());
            var staleCopy = ListForgeIntegratedSettingsStore.Load().Settings; // ex.: ListForge Integrado aberto
            ListForgeIntegratedSettingsStore.SavePanelFavorites(new[] { PanelToolCatalog.GridPrepare, PanelToolCatalog.LayersToggleMa });

            staleCopy.EditorFontSize = 18;
            ListForgeIntegratedSettingsStore.Save(staleCopy);

            var loaded = ListForgeIntegratedSettingsStore.Load().Settings;
            Assert.Equal(18, loaded.EditorFontSize);
            Assert.Equal(new[] { PanelToolCatalog.GridPrepare, PanelToolCatalog.LayersToggleMa }, loaded.PanelFavoriteToolIds);
        }

        [Fact]
        public void SaveIncludingFavorites_WritesTheObjectsValue_AndNotifies()
        {
            ListForgeIntegratedSettingsStore.SavePanelFavorites(new[] { PanelToolCatalog.GridPrepare });
            var imported = ListForgeIntegratedSettings.Default();
            imported.PanelFavoriteToolIds = new List<string> { PanelToolCatalog.LayersRestoreLastHidden };
            var notified = 0;
            Action handler = () => notified++;
            ListForgeIntegratedSettingsStore.PanelFavoritesChanged += handler;
            try
            {
                ListForgeIntegratedSettingsStore.Save(imported, includePanelFavorites: true);
            }
            finally
            {
                ListForgeIntegratedSettingsStore.PanelFavoritesChanged -= handler;
            }

            Assert.Equal(1, notified);
            Assert.Equal(new[] { PanelToolCatalog.LayersRestoreLastHidden }, ListForgeIntegratedSettingsStore.LoadPanelFavorites().Ids);
        }

        [Fact]
        public void CorruptConfiguration_PanelOpensWithoutFavorites_AndFileIsNeverTouched()
        {
            File.WriteAllText(FilePath, "{ isto não é json");

            var loaded = ListForgeIntegratedSettingsStore.LoadPanelFavorites();
            Assert.Empty(loaded.Ids);
            Assert.NotEqual("", loaded.Notice);

            Assert.Throws<InvalidOperationException>(() => ListForgeIntegratedSettingsStore.SavePanelFavorites(new[] { PanelToolCatalog.GridPrepare }));
            Assert.Equal("{ isto não é json", File.ReadAllText(FilePath));
            Assert.Single(Directory.GetFiles(_tempDirectory)); // sem quarentena criada pelo painel
        }

        [Fact]
        public void FieldWithUnexpectedType_LoadsEmptyWithNotice()
        {
            var json = JObject.FromObject(ListForgeIntegratedSettings.Default());
            json[nameof(ListForgeIntegratedSettings.PanelFavoriteToolIds)] = "grid.prepare";
            File.WriteAllText(FilePath, json.ToString());

            var loaded = ListForgeIntegratedSettingsStore.LoadPanelFavorites();

            Assert.Empty(loaded.Ids);
            Assert.NotEqual("", loaded.Notice);
        }

        [Fact]
        public void UnknownSchema_SaveRefusesWithoutTouchingTheFile()
        {
            var content = "{ \"SchemaVersion\": 99 }";
            File.WriteAllText(FilePath, content);

            Assert.Throws<InvalidOperationException>(() => ListForgeIntegratedSettingsStore.SavePanelFavorites(new[] { PanelToolCatalog.GridPrepare }));
            Assert.Equal(content, File.ReadAllText(FilePath));
        }

        [Fact]
        public void SchemaVersion1_IsMigratedWhenSavingFavorites()
        {
            File.WriteAllText(FilePath, "{ \"SchemaVersion\": 1, \"EditorFontSize\": 15, \"OutputDir\": \"C:\\\\Saidas\" }");

            ListForgeIntegratedSettingsStore.SavePanelFavorites(new[] { PanelToolCatalog.OutputPrepareNewMold });

            var loaded = ListForgeIntegratedSettingsStore.Load();
            Assert.Equal(ListForgeIntegratedSettings.CurrentSchemaVersion, loaded.Settings.SchemaVersion);
            Assert.Equal(15, loaded.Settings.EditorFontSize);
            Assert.Equal(new[] { PanelToolCatalog.OutputPrepareNewMold }, loaded.Settings.PanelFavoriteToolIds);
        }
    }

    [Collection("PXToolBox Dev shared filesystem resources")]
    public class PanelFavoritesShowLabelsStoreTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "pxtoolbox-tests", "fav-labels-" + Guid.NewGuid().ToString("N"));

        public PanelFavoritesShowLabelsStoreTests()
        {
            Directory.CreateDirectory(_dir);
            ListForgeIntegratedSettingsStore.SetConfigDirectoryForTesting(_dir);
        }

        public void Dispose()
        {
            ListForgeIntegratedSettingsStore.SetConfigDirectoryForTesting(null);
            try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        [Fact]
        public void Default_IsIconsOnly_AndRegularSaveDoesNotAddTheField()
        {
            Assert.False(ListForgeIntegratedSettingsStore.LoadPanelFavoritesShowLabels());

            ListForgeIntegratedSettingsStore.Save(ListForgeIntegratedSettings.Default());

            Assert.False(ListForgeIntegratedSettingsStore.LoadPanelFavoritesShowLabels());
            Assert.DoesNotContain("PanelFavoritesShowLabels", File.ReadAllText(Path.Combine(_dir, "listforge-integrated-settings.json")));
        }

        [Fact]
        public void SaveShowLabels_RoundTrips_AndARegularSaveFromAnOldCopyKeepsIt()
        {
            ListForgeIntegratedSettingsStore.SavePanelFavorites(new[] { PanelToolCatalog.GridPrepare });
            var oldCopy = ListForgeIntegratedSettingsStore.Load().Settings;

            ListForgeIntegratedSettingsStore.SavePanelFavoritesShowLabels(true);
            Assert.True(ListForgeIntegratedSettingsStore.LoadPanelFavoritesShowLabels());

            // Uma janela do ListForge que leu a configuração antes grava por cima: o modo fica.
            ListForgeIntegratedSettingsStore.Save(oldCopy);
            Assert.True(ListForgeIntegratedSettingsStore.LoadPanelFavoritesShowLabels());
            Assert.Equal(new[] { PanelToolCatalog.GridPrepare }, ListForgeIntegratedSettingsStore.LoadPanelFavorites().Ids);

            ListForgeIntegratedSettingsStore.SavePanelFavoritesShowLabels(false);
            Assert.False(ListForgeIntegratedSettingsStore.LoadPanelFavoritesShowLabels());
        }
    }

    public class PanelFavoritesPortablePreferencesTests
    {
        [Fact]
        public void ExportImport_RoundTripsFavoritesAndOrder()
        {
            var source = ListForgeIntegratedSettings.Default();
            source.PanelFavoriteToolIds = new List<string> { PanelToolCatalog.LayersToggleC, PanelToolCatalog.GridPrepare };
            var json = PortablePreferencesService.Serialize(PortablePreferencesService.Export(source));

            var validation = PortablePreferencesService.Validate(json);
            var target = ListForgeIntegratedSettings.Default();
            PortablePreferencesService.Apply(validation.Document!, target);

            Assert.True(validation.IsValid);
            Assert.Empty(validation.IncompatibleFieldSummaries);
            Assert.Equal(new[] { PanelToolCatalog.LayersToggleC, PanelToolCatalog.GridPrepare }, target.PanelFavoriteToolIds);
            Assert.Contains("\"panelFavoriteToolIds\"", json);
        }

        [Fact]
        public void FileWithoutFavorites_IsStillValid_AndKeepsCurrentFavorites()
        {
            var json = "{ \"formatId\": \"PxToolBoxDevPortablePreferences\", \"schemaVersion\": 1, \"editorFontSize\": 14 }";
            var target = ListForgeIntegratedSettings.Default();
            target.PanelFavoriteToolIds = new List<string> { PanelToolCatalog.GridPrepare };

            var validation = PortablePreferencesService.Validate(json);
            PortablePreferencesService.Apply(validation.Document!, target);

            Assert.True(validation.IsValid);
            Assert.Null(validation.Document!.PanelFavoriteToolIds);
            Assert.Equal(new[] { PanelToolCatalog.GridPrepare }, target.PanelFavoriteToolIds);
        }

        [Fact]
        public void UnknownFavoriteIds_AreReportedAndIgnored()
        {
            var json = "{ \"formatId\": \"PxToolBoxDevPortablePreferences\", \"schemaVersion\": 1, \"editorFontSize\": 14, " +
                       "\"panelFavoriteToolIds\": [\"future.tool\", \"grid.prepare\", 7] }";

            var validation = PortablePreferencesService.Validate(json);

            Assert.True(validation.IsValid);
            Assert.Equal(new[] { PanelToolCatalog.GridPrepare }, validation.Document!.PanelFavoriteToolIds);
            Assert.Equal(2, validation.IncompatibleFieldSummaries.Count(s => s.StartsWith("Favorito desconhecido")));
        }

        [Fact]
        public void FavoritesWithWrongType_AreIncompatible_NotFatal()
        {
            var json = "{ \"formatId\": \"PxToolBoxDevPortablePreferences\", \"schemaVersion\": 1, \"editorFontSize\": 14, \"panelFavoriteToolIds\": \"grid.prepare\" }";

            var validation = PortablePreferencesService.Validate(json);

            Assert.True(validation.IsValid);
            Assert.Null(validation.Document!.PanelFavoriteToolIds);
            Assert.Contains(validation.IncompatibleFieldSummaries, s => s.StartsWith("Favoritos do painel com tipo inesperado"));
        }
    }

    // Ver WpfCodeCreatedControlsCollection (TestCollections.cs): sem isto, este teste pode travar
    // com outra classe WPF inicializando tipos ao mesmo tempo.
    [Collection("WPF created in code — no parallel WPF initialization")]
    public class FavoriteShortcutTests
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

        // Arraste com o painel montado de verdade (sem janela nem mouse): 4 ícones numa largura de
        // 120 → 3 na primeira linha, 1 na segunda.
        [Fact]
        public void DragReorder_MovesOnlyAfterThreshold_AndCancelsOutsideThePanel()
        {
            RunSta(() =>
            {
                var items = new WrapPanel { Width = 120 };
                for (var i = 0; i < 4; i++)
                {
                    items.Children.Add(new Button { Width = 36, Height = 36, Margin = new System.Windows.Thickness(0, 0, 4, 4) });
                }

                var indicator = new System.Windows.Shapes.Rectangle { Width = 2 };
                var canvas = new Canvas();
                canvas.Children.Add(indicator);
                var host = new Grid();
                host.Children.Add(items);
                host.Children.Add(canvas);
                host.Measure(new System.Windows.Size(120, 200));
                host.Arrange(new System.Windows.Rect(0, 0, 120, 200));

                var moves = new List<(int From, int InsertBefore)>();
                var drag = new FavoriteDragReorder(items, indicator, (from, to) => { moves.Add((from, to)); return true; });

                // Clique curto: nunca vira arraste.
                drag.Press(new System.Windows.Point(5, 5));
                Assert.False(drag.Drag(new System.Windows.Point(6, 6)));
                Assert.False(drag.Release(new System.Windows.Point(6, 6)));

                // Arraste do primeiro para o fim da primeira linha.
                drag.Press(new System.Windows.Point(5, 5));
                Assert.True(drag.Drag(new System.Windows.Point(100, 10)));
                Assert.True(drag.IsDragging);
                Assert.Equal(System.Windows.Visibility.Visible, indicator.Visibility);
                Assert.Equal(0.45, items.Children[0].Opacity, 2);
                Assert.True(drag.Release(new System.Windows.Point(100, 10)));
                Assert.Equal(new[] { (0, 3) }, moves);
                Assert.Equal(1, items.Children[0].Opacity);
                Assert.Equal(System.Windows.Visibility.Collapsed, indicator.Visibility);

                // Arraste do da segunda linha, solto fora do painel: cancela.
                drag.Press(new System.Windows.Point(10, 50));
                Assert.True(drag.Drag(new System.Windows.Point(10, 10)));
                Assert.True(drag.Release(new System.Windows.Point(400, 400)));
                Assert.Single(moves);
                Assert.False(drag.IsDragging);

                // Pressionar no vão entre ícones não arrasta nada.
                drag.Press(new System.Windows.Point(38, 10));
                Assert.False(drag.Drag(new System.Windows.Point(100, 10)));
            });
        }

        // Regressão (dev.66–dev.70, relato do usuário: "arrastar os favoritos não está
        // funcionando"): ao começar o arraste o painel toma a captura do mouse; o botão
        // pressionado perde a dele e o LostMouseCapture SOBE até o painel, que cancelava o
        // arraste na hora. Aqui o aviso é disparado como o WPF dispara — no botão, subindo.
        [Fact]
        public void DragReorder_ButtonLosingCaptureWhenDragStarts_DoesNotCancelTheDrag()
        {
            RunSta(() =>
            {
                var items = new WrapPanel { Width = 120 };
                for (var i = 0; i < 3; i++)
                {
                    items.Children.Add(new Button { Width = 36, Height = 36, Margin = new System.Windows.Thickness(0, 0, 4, 4) });
                }

                var indicator = new System.Windows.Shapes.Rectangle { Width = 2 };
                var host = new Grid();
                host.Children.Add(items);
                host.Children.Add(new Canvas { Children = { indicator } });
                host.Measure(new System.Windows.Size(120, 100));
                host.Arrange(new System.Windows.Rect(0, 0, 120, 100));

                var moves = new List<(int From, int InsertBefore)>();
                var drag = new FavoriteDragReorder(items, indicator, (from, to) => { moves.Add((from, to)); return true; });
                System.Windows.Input.MouseEventArgs LostCapture() =>
                    new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0) { RoutedEvent = System.Windows.Input.Mouse.LostMouseCaptureEvent };

                drag.Press(new System.Windows.Point(5, 5));
                Assert.True(drag.Drag(new System.Windows.Point(100, 10)));
                items.Children[0].RaiseEvent(LostCapture());

                Assert.True(drag.IsDragging);
                Assert.True(drag.Release(new System.Windows.Point(100, 10)));
                Assert.Equal(new[] { (0, 3) }, moves);

                // O próprio painel perdendo a captura (soltou fora, Alt+Tab) continua cancelando.
                drag.Press(new System.Windows.Point(5, 5));
                Assert.True(drag.Drag(new System.Windows.Point(100, 10)));
                items.RaiseEvent(LostCapture());
                Assert.False(drag.IsDragging);
                Assert.Single(moves);
            });
        }

        [Fact]
        public void ShortcutClick_RunsTheOriginalClickHandlers_Once()
        {
            RunSta(() =>
            {
                var original = new Button();
                var originalClicks = 0;
                original.Click += (_, e) => { originalClicks++; Assert.Same(original, e.Source); };
                var shortcut = FavoriteShortcut.Create(original, "Preparar Grid", null, null);

                shortcut.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, shortcut));

                Assert.Equal(1, originalClicks);
            });
        }

        [Fact]
        public void Shortcut_MirrorsEnabledStateAndTagAndToolTip_AndNeverRunsWhenDisabled()
        {
            RunSta(() =>
            {
                var original = new Button { ToolTip = "Indisponível: nada registrado." };
                var originalClicks = 0;
                original.Click += (_, _) => originalClicks++;
                var shortcut = FavoriteShortcut.Create(original, "Mostrar últimas camadas ocultadas", new TextBlock { Text = "" }, null);

                original.IsEnabled = false;
                original.Tag = "Active";
                Assert.False(shortcut.IsEnabled);
                Assert.Equal("Active", shortcut.Tag);
                // Ícone no botão; nome da ferramenta + dica atual do original na dica do atalho.
                Assert.Equal("", ((TextBlock)shortcut.Content).Text);
                Assert.Equal("Mostrar últimas camadas ocultadas", System.Windows.Automation.AutomationProperties.GetName(shortcut));
                Assert.Equal("Mostrar últimas camadas ocultadas\r\nIndisponível: nada registrado.", FavoriteShortcut.ToolTipText(shortcut).TrimEnd());
                original.ToolTip = "Mostra 2 camadas ocultadas.";
                Assert.EndsWith("Mostra 2 camadas ocultadas.", FavoriteShortcut.ToolTipText(shortcut).TrimEnd());
                Assert.False(FavoriteShortcut.Invoke(original));
                shortcut.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, shortcut));
                Assert.Equal(0, originalClicks);

                original.IsEnabled = true;
                original.Tag = null;
                Assert.True(shortcut.IsEnabled);
                Assert.Null(shortcut.Tag);
            });
        }
    }
}
#endif
