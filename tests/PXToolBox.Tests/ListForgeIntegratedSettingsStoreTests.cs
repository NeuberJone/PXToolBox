#if NET48
// Só compila na perna net48 — ver cabeçalho de ListForgeIntegratedTests.cs. Testa
// Integrations/ListForgeIntegratedSettings(Store).cs, do host do PX (PXToolBoxDev).
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.ListForgeIntegrated;
using Xunit;

namespace PXToolBox.Tests
{
    /// <summary>
    /// Configurações do ListForge Integrado (ver docs/listforge-classic-parity.md, grupo
    /// "Configurações"). Desde a correção de isolamento de testes (ver docs/ARCHITECTURE.md,
    /// seção 31), aponta <see cref="ListForgeIntegratedSettingsStore"/> para um diretório
    /// TEMPORÁRIO próprio desta instância (via <c>SetConfigDirectoryForTesting</c>, no
    /// construtor, desfeito no <see cref="Dispose"/>) — nunca mais o caminho real em
    /// <c>%LocalAppData%\PXToolBox\Dev\config\listforge-integrated-settings.json</c>. Isso
    /// elimina tanto a disputa entre classes de teste (a antiga razão de
    /// <see cref="SharedFilesystemCollection"/> para este arquivo específico — já obsoleta)
    /// quanto o risco de ler/sobrescrever a configuração real enquanto o PX Tool Box DEV está
    /// aberto de verdade na mesma máquina. A coleção abaixo permanece, mas por outro motivo,
    /// agora introduzido por este próprio seam de teste: ver
    /// TestCollections.SharedFilesystemCollection.
    /// </summary>
    [Collection("PXToolBox Dev shared filesystem resources")]
    public class ListForgeIntegratedSettingsStoreTests : IDisposable
    {
        private readonly string _tempDirectory = Path.Combine(
            Path.GetTempPath(), "pxtoolbox-tests", "lf-settings-" + Guid.NewGuid().ToString("N"));

        public ListForgeIntegratedSettingsStoreTests()
        {
            Directory.CreateDirectory(_tempDirectory);
            ListForgeIntegratedSettingsStore.SetConfigDirectoryForTesting(_tempDirectory);
        }

        public void Dispose()
        {
            ListForgeIntegratedSettingsStore.SetConfigDirectoryForTesting(null);
            try { Directory.Delete(_tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private string RealFilePath => Path.Combine(_tempDirectory, "listforge-integrated-settings.json");

        private string ConfigDirectory => _tempDirectory;

        [Fact]
        public void Load_NoFile_ReturnsDefaultsWithoutNotice()
        {
            // Diretório isolado desta instância já começa vazio (ver construtor) — não precisa
            // apagar nada antes.
            var result = ListForgeIntegratedSettingsStore.Load();

            Assert.Equal("", result.Notice);
            Assert.Equal(ListForgeIntegratedSettings.DefaultEditorFontSize, result.Settings.EditorFontSize);

            // Primeira execução já garante o perfil "Padrão" (EnsureProfiles, chamado por
            // Normalize) com as configurações oficiais do standalone.
            var profile = Assert.Single(result.Settings.WorkProfiles);
            Assert.Equal(WorkProfileService.DefaultProfileId, profile.Id);
            Assert.True(profile.IsDefault);
            Assert.Equal(ListForgeIntegratedSettings.ModeLooseFiles, profile.Settings.AdvancedSaveMode);
            Assert.False(profile.Settings.UseDefaultOutputDir);
            Assert.Equal(profile.Id, result.Settings.ActiveWorkProfileId);
        }

        [Fact]
        public void SaveThenLoad_RoundTripsWorkProfiles()
        {
            var settings = ListForgeIntegratedSettings.Default();
            settings.EditorFontSize = 18;

            var service = new WorkProfileService();
            var created = service.CreateProfile(settings, "Pedido padrão", new WorkProfileSettings
            {
                DefaultInputSeparator = ";",
                DefaultCaseMode = "upper",
                EditorSortMode = "Crescente",
                AdvancedSaveMode = ListForgeIntegratedSettings.ModeZip,
                UseDefaultOutputDir = true,
                OutputDir = @"C:\Nova Pasta Com Espaços\Saidas",
                UseDefaultListName = true,
                DefaultListName = "pedido-padrao",
            });
            Assert.True(created.Success);

            ListForgeIntegratedSettingsStore.Save(settings);
            var loaded = ListForgeIntegratedSettingsStore.Load();

            Assert.Equal("", loaded.Notice);
            Assert.Equal(18, loaded.Settings.EditorFontSize);
            Assert.Equal(2, loaded.Settings.WorkProfiles.Count);

            var activeProfile = service.GetActiveProfile(loaded.Settings);
            Assert.NotNull(activeProfile);
            Assert.Equal("Pedido padrão", activeProfile!.Name);
            Assert.Equal(";", activeProfile.Settings.DefaultInputSeparator);
            Assert.Equal("upper", activeProfile.Settings.DefaultCaseMode);
            Assert.Equal("Crescente", activeProfile.Settings.EditorSortMode);
            Assert.Equal(ListForgeIntegratedSettings.ModeZip, activeProfile.Settings.AdvancedSaveMode);
            Assert.True(activeProfile.Settings.UseDefaultOutputDir);
            Assert.Equal(@"C:\Nova Pasta Com Espaços\Saidas", activeProfile.Settings.OutputDir);
            Assert.True(activeProfile.Settings.UseDefaultListName);
            Assert.Equal("pedido-padrao", activeProfile.Settings.DefaultListName);
        }

        [Fact]
        public void Load_SchemaVersion1_MigratesLegacyFieldsIntoDefaultProfile()
        {
            // Formato real gravado pela build anterior (esquema 1) — AdvancedSaveMode/
            // UseDefaultOutputDir/OutputDir/UseDefaultListName/DefaultListName eram campos
            // únicos no topo, antes de perfis de trabalho existirem.
            TestFileIO.WriteAllTextWithRetry(RealFilePath,
                "{\"SchemaVersion\": 1, \"EditorFontSize\": 16.0, \"AdvancedSaveMode\": \"Zip\", " +
                "\"UseDefaultOutputDir\": true, \"OutputDir\": \"C:\\\\Saidas\", " +
                "\"UseDefaultListName\": true, \"DefaultListName\": \"lista-antiga\"}");

            var result = ListForgeIntegratedSettingsStore.Load();

            Assert.NotEqual("", result.Notice);
            Assert.Equal(ListForgeIntegratedSettings.CurrentSchemaVersion, result.Settings.SchemaVersion);
            Assert.Equal(16.0, result.Settings.EditorFontSize);

            var profile = Assert.Single(result.Settings.WorkProfiles);
            Assert.True(profile.IsDefault);
            Assert.Equal(ListForgeIntegratedSettings.ModeZip, profile.Settings.AdvancedSaveMode);
            Assert.True(profile.Settings.UseDefaultOutputDir);
            Assert.Equal(@"C:\Saidas", profile.Settings.OutputDir);
            Assert.True(profile.Settings.UseDefaultListName);
            Assert.Equal("lista-antiga", profile.Settings.DefaultListName);

            // A migração nunca sobrescreve o arquivo original sozinha — só quando Save() for
            // chamado explicitamente depois (mesma regra de "nunca sobrescrever
            // silenciosamente" de qualquer outro caminho de carregamento problemático).
            Assert.Equal(1, (int)Newtonsoft.Json.Linq.JObject.Parse(TestFileIO.ReadAllTextWithRetry(RealFilePath))["SchemaVersion"]!);
        }

        [Fact]
        public void WorkProfiles_CreateRenameDuplicateDelete_FollowSameRulesAsStandalone()
        {
            var settings = ListForgeIntegratedSettings.Default();
            var service = new WorkProfileService();

            // O perfil Padrão não pode ser renomeado nem excluído.
            var defaultProfile = service.GetActiveProfile(settings)!;
            var renameDefault = service.RenameProfile(settings, defaultProfile.Id, "Outro nome");
            Assert.False(renameDefault.Success);
            Assert.Equal("WorkProfileDefaultRename", renameDefault.ErrorCode);

            var created = service.CreateProfile(settings, "Pedidos grandes", WorkProfileService.CaptureOfficialDefaultSettings());
            Assert.True(created.Success);
            Assert.Equal(created.Value!.Id, settings.ActiveWorkProfileId);

            // Nome duplicado (sem diferenciar maiúsculas/minúsculas) é rejeitado.
            var duplicateName = service.CreateProfile(settings, "pedidos GRANDES", WorkProfileService.CaptureOfficialDefaultSettings());
            Assert.False(duplicateName.Success);
            Assert.Equal("WorkProfileDuplicateName", duplicateName.ErrorCode);

            var renamed = service.RenameProfile(settings, created.Value.Id, "Pedidos grandes (novo)");
            Assert.True(renamed.Success);

            var duplicated = service.DuplicateProfile(settings, created.Value.Id);
            Assert.True(duplicated.Success);
            Assert.Equal("Pedidos grandes (novo) - Cópia", duplicated.Value!.Name);
            Assert.Equal(3, settings.WorkProfiles.Count);

            var deleted = service.DeleteProfile(settings, duplicated.Value.Id);
            Assert.True(deleted.Success);
            Assert.Equal(2, settings.WorkProfiles.Count);

            var deleteDefault = service.DeleteProfile(settings, defaultProfile.Id);
            Assert.False(deleteDefault.Success);
            Assert.Equal("WorkProfileDefaultDelete", deleteDefault.ErrorCode);

            // Excluir o perfil ATIVO (não o Padrão) cai para o Padrão.
            var another = service.CreateProfile(settings, "Outro perfil", WorkProfileService.CaptureOfficialDefaultSettings());
            Assert.True(another.Success);
            Assert.Equal(another.Value!.Id, settings.ActiveWorkProfileId);

            var deletedActive = service.DeleteProfile(settings, settings.ActiveWorkProfileId);
            Assert.True(deletedActive.Success);
            Assert.Equal(WorkProfileService.DefaultProfileId, settings.ActiveWorkProfileId);
        }

        [Fact]
        public void Save_NeverLeavesTemporaryFileBehind()
        {
            ListForgeIntegratedSettingsStore.Save(ListForgeIntegratedSettings.Default());

            var leftoverTempFiles = Directory.GetFiles(ConfigDirectory, "listforge-integrated-settings.json.tmp-*");
            Assert.Empty(leftoverTempFiles);
        }

        [Fact]
        public void Load_CorruptedJson_QuarantinesFileAndReturnsDefaults_WithoutOverwritingSilently()
        {
            const string garbage = "{ isto nao e json valido ";
            TestFileIO.WriteAllTextWithRetry(RealFilePath, garbage);

            var result = ListForgeIntegratedSettingsStore.Load();

            Assert.NotEqual("", result.Notice);
            Assert.Equal(ListForgeIntegratedSettings.DefaultEditorFontSize, result.Settings.EditorFontSize);

            var quarantineFiles = Directory.GetFiles(ConfigDirectory, "listforge-integrated-settings.corrompido-*.json");
            var quarantine = Assert.Single(quarantineFiles);
            Assert.Equal(garbage, TestFileIO.ReadAllTextWithRetry(quarantine));
        }

        [Fact]
        public void Load_UnknownSchemaVersion_QuarantinesFileAndReturnsDefaults()
        {
            TestFileIO.WriteAllTextWithRetry(RealFilePath, "{\"SchemaVersion\": 999, \"EditorFontSize\": 20.0}");

            var result = ListForgeIntegratedSettingsStore.Load();

            Assert.NotEqual("", result.Notice);
            Assert.Equal(ListForgeIntegratedSettings.CurrentSchemaVersion, result.Settings.SchemaVersion);
            Assert.Equal(ListForgeIntegratedSettings.DefaultEditorFontSize, result.Settings.EditorFontSize);
        }

        [Fact]
        public void Normalize_ClampsOutOfRangeFontSize()
        {
            var settings = new ListForgeIntegratedSettings { EditorFontSize = 999 };
            settings.Normalize();
            Assert.Equal(ListForgeIntegratedSettings.DefaultEditorFontSize, settings.EditorFontSize);

            var tooSmall = new ListForgeIntegratedSettings { EditorFontSize = 0 };
            tooSmall.Normalize();
            Assert.Equal(ListForgeIntegratedSettings.DefaultEditorFontSize, tooSmall.EditorFontSize);
        }

        [Fact]
        public void Normalize_FixesUnknownAdvancedSaveModeOnEachProfile()
        {
            var settings = new ListForgeIntegratedSettings();
            var service = new WorkProfileService();
            service.EnsureProfiles(settings, new WorkProfileSettings { AdvancedSaveMode = "Algo desconhecido" });

            settings.Normalize();

            var profile = service.GetActiveProfile(settings)!;
            Assert.Equal(ListForgeIntegratedSettings.ModeLooseFiles, profile.Settings.AdvancedSaveMode);
        }

        [Fact]
        public void Normalize_WithNoWorkProfiles_CreatesDefaultProfile()
        {
            var settings = new ListForgeIntegratedSettings();
            Assert.Empty(settings.WorkProfiles);

            settings.Normalize();

            var profile = Assert.Single(settings.WorkProfiles);
            Assert.True(profile.IsDefault);
            Assert.Equal(profile.Id, settings.ActiveWorkProfileId);
        }

        [Fact]
        public void SaveThenLoad_RoundTripsCustomSizes()
        {
            var settings = ListForgeIntegratedSettings.Default();
            settings.Sizes = SizeHelper.UpdateGroupConfig(
                settings.Sizes, SizeHelper.GroupMale,
                new List<string> { "PP", "P", "M", "G", "GIGANTE" },
                new List<string>(),
                new List<string>());

            ListForgeIntegratedSettingsStore.Save(settings);
            var loaded = ListForgeIntegratedSettingsStore.Load();

            Assert.Equal("", loaded.Notice);
            Assert.Equal(
                new[] { "PP", "P", "M", "G", "GIGANTE" },
                loaded.Settings.Sizes.Groups[SizeHelper.GroupMale].BaseSizes);
        }

        [Fact]
        public void Load_SchemaVersion1File_DefaultsSizesToOfficialValues()
        {
            // Campo aditivo: um arquivo salvo antes desta rodada não tem a chave "Sizes" no
            // JSON — a migração do esquema 1 não precisa (nem deve) tentar preenchê-la a partir
            // de nada, já que o esquema 1 nunca teve tamanhos customizados. Deve simplesmente
            // cair nos tamanhos oficiais, normalizados.
            TestFileIO.WriteAllTextWithRetry(RealFilePath, "{\"SchemaVersion\": 1, \"EditorFontSize\": 16.0}");

            var result = ListForgeIntegratedSettingsStore.Load();

            Assert.Equal(
                SizeConfig.Default().Groups[SizeHelper.GroupMale].BaseSizes,
                result.Settings.Sizes.Groups[SizeHelper.GroupMale].BaseSizes);
        }

        [Fact]
        public void Normalize_WithMissingSizeGroup_RestoresOfficialDefaultForThatGroupOnly()
        {
            var settings = new ListForgeIntegratedSettings
            {
                Sizes = new SizeConfig
                {
                    Groups = new Dictionary<string, SizeGroupConfig>
                    {
                        [SizeHelper.GroupMale] = new() { BaseSizes = new List<string> { "UNICO" } },
                        // female/child/sock ausentes de propósito — Normalize deve preencher só
                        // esses com o padrão oficial, preservando o masculino customizado.
                    },
                },
            };

            settings.Normalize();

            Assert.Equal(new[] { "UNICO" }, settings.Sizes.Groups[SizeHelper.GroupMale].BaseSizes);
            Assert.Equal(
                SizeConfig.Default().Groups[SizeHelper.GroupFemale].BaseSizes,
                settings.Sizes.Groups[SizeHelper.GroupFemale].BaseSizes);
        }

        [Fact]
        public void SizeConfigWindow_Save_RejectsGroupWithNoBaseSizes()
        {
            // TryBuildSizeConfigFromControls é privado — exercitado indiretamente via a janela
            // real numa thread STA, confirmando a mensagem e que nada é persistido quando um
            // grupo fica sem tamanho-base (mesma regra de BuildSizeConfigFromUI do standalone).
            // Diretório isolado desta instância já começa vazio — não precisa apagar nada antes.
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var window = new SizeConfigWindow(ListForgeIntegratedSettings.Default());
                    var baseBox = (System.Windows.Controls.TextBox)window.FindName("TxtFemaleBase")!;
                    baseBox.Text = "";
                    var saveButton = (System.Windows.Controls.Button)window.FindName("BtnSave")!;
                    saveButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

                    Assert.Null(window.SavedSettings);
                    var status = (System.Windows.Controls.TextBlock)window.FindName("StatusText")!;
                    Assert.Contains("Feminino", status.Text);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(caught);
            Assert.False(File.Exists(RealFilePath));
        }

        /// <summary>
        /// Regressão do mesmo padrão de crash corrigido em ClassicWorkspaceWindow/ComparisonWindow
        /// (ver ARCHITECTURE.md, iteração 8) — vários campos editáveis nesta janela, por isso
        /// constrói a janela real numa thread STA dedicada.
        /// </summary>
        [Fact]
        public void SizeConfigWindow_ConstructsWithoutThrowing()
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = new SizeConfigWindow(ListForgeIntegratedSettings.Default());
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(caught);
        }

        /// <summary>
        /// Regressão do mesmo padrão de crash corrigido em ClassicWorkspaceWindow/ComparisonWindow
        /// (ver ARCHITECTURE.md, iteração 8) — dois campos editáveis nesta janela, por isso
        /// constrói a janela real numa thread STA dedicada.
        /// </summary>
        [Fact]
        public void SettingsWindow_ConstructsWithoutThrowing()
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = new SettingsWindow(ListForgeIntegratedSettings.Default(), "");
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(caught);
        }

        // ---------------------------------------------------------------
        // Preferência de modo PX/Clássico por perfil (ver docs/design/listforge-px-workflow/
        // design.md, seção 3.3) — GetPreferredWorkspaceMode/SetPreferredWorkspaceMode em
        // ListForgeIntegratedSettings. Campo próprio deste módulo, nunca em WorkProfile/
        // WorkProfileSettings (compartilhados com o ListForge Standalone).
        // ---------------------------------------------------------------
        [Fact]
        public void GetPreferredWorkspaceMode_NoEntryYet_ReturnsClassic()
        {
            var settings = ListForgeIntegratedSettings.Default();

            Assert.Equal(ListForgeIntegratedSettings.WorkspaceModeClassic, settings.GetPreferredWorkspaceMode("perfil-1"));
        }

        [Fact]
        public void GetPreferredWorkspaceMode_EmptyProfileId_NeverThrows_ReturnsClassic()
        {
            var settings = ListForgeIntegratedSettings.Default();

            Assert.Equal(ListForgeIntegratedSettings.WorkspaceModeClassic, settings.GetPreferredWorkspaceMode(""));
        }

        [Fact]
        public void SetThenGetPreferredWorkspaceMode_RoundTripsPerProfile()
        {
            var settings = ListForgeIntegratedSettings.Default();

            settings.SetPreferredWorkspaceMode("perfil-1", ListForgeIntegratedSettings.WorkspaceModePx);
            settings.SetPreferredWorkspaceMode("perfil-2", ListForgeIntegratedSettings.WorkspaceModeClassic);

            Assert.Equal(ListForgeIntegratedSettings.WorkspaceModePx, settings.GetPreferredWorkspaceMode("perfil-1"));
            Assert.Equal(ListForgeIntegratedSettings.WorkspaceModeClassic, settings.GetPreferredWorkspaceMode("perfil-2"));
            // Um terceiro perfil nunca configurado continua resolvendo para Clássico.
            Assert.Equal(ListForgeIntegratedSettings.WorkspaceModeClassic, settings.GetPreferredWorkspaceMode("perfil-3"));
        }

        [Fact]
        public void SetPreferredWorkspaceMode_UnknownValue_NormalizesToClassic()
        {
            var settings = ListForgeIntegratedSettings.Default();

            settings.SetPreferredWorkspaceMode("perfil-1", "valor-desconhecido");

            Assert.Equal(ListForgeIntegratedSettings.WorkspaceModeClassic, settings.GetPreferredWorkspaceMode("perfil-1"));
        }

        [Fact]
        public void SaveThenLoad_RoundTripsPreferredWorkspaceModeByProfileId()
        {
            var settings = ListForgeIntegratedSettings.Default();
            settings.SetPreferredWorkspaceMode("perfil-1", ListForgeIntegratedSettings.WorkspaceModePx);
            ListForgeIntegratedSettingsStore.Save(settings);

            var loaded = ListForgeIntegratedSettingsStore.Load().Settings;

            Assert.Equal(ListForgeIntegratedSettings.WorkspaceModePx, loaded.GetPreferredWorkspaceMode("perfil-1"));
        }

        [Fact]
        public void Normalize_WithNullPreferredWorkspaceModeDictionary_NeverThrows()
        {
            var settings = ListForgeIntegratedSettings.Default();
            settings.PreferredWorkspaceModeByProfileId = null!;

            settings.Normalize();

            Assert.NotNull(settings.PreferredWorkspaceModeByProfileId);
            Assert.Equal(ListForgeIntegratedSettings.WorkspaceModeClassic, settings.GetPreferredWorkspaceMode("qualquer"));
        }

        // ---------------------------------------------------------------
        // Robustez de Save() contra bloqueio transitório no destino: ver AtomicFileWriterTests.cs
        // (a lógica mora em AtomicFileWriter, testada ali direto contra um diretório temporário
        // próprio — nunca este diretório, que também é isolado por instância desta classe, mas
        // não dedicado só a esse cenário).
    }
}
#endif
