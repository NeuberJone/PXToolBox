#if NET48
// Só compila na perna net48 — PXToolBoxDev.Integrations só existe lá. Testa
// Integrations/PortablePreferencesService.cs (preferências PESSOAIS portáveis, distinto dos
// perfis de trabalho do ListForge e do botão mais amplo "Exportar/Importar configurações...").
using System.Collections.Generic;
using System.Linq;
using ListForge.Models;
using ListForge.Services;
using PXToolBoxDev.Integrations;
using Xunit;

namespace PXToolBox.Tests
{
    public class PortablePreferencesServiceTests
    {
        private static ListForgeIntegratedSettings SettingsWithProfiles(params string[] profileNames)
        {
            var settings = ListForgeIntegratedSettings.Default();
            settings.WorkProfiles.Clear();
            foreach (var name in profileNames)
            {
                settings.WorkProfiles.Add(new WorkProfile
                {
                    Id = $"id-{name}",
                    Name = name,
                    Settings = WorkProfileService.CaptureOfficialDefaultSettings(),
                });
            }
            settings.ActiveWorkProfileId = settings.WorkProfiles.First().Id;
            return settings;
        }

        [Fact]
        public void Export_IncludesEditorFontSize_AndResolvesProfileIdsToNames()
        {
            var settings = SettingsWithProfiles("Padrão", "Camisetas");
            settings.EditorFontSize = 17;
            settings.SetPreferredWorkspaceMode("id-Padrão", ListForgeIntegratedSettings.WorkspaceModePx);
            settings.SetPreferredWorkspaceMode("id-Camisetas", ListForgeIntegratedSettings.WorkspaceModeClassic);

            var document = PortablePreferencesService.Export(settings);

            Assert.Equal(17, document.EditorFontSize);
            Assert.Equal(ListForgeIntegratedSettings.WorkspaceModePx, document.PreferredWorkspaceModeByProfileName["Padrão"]);
            Assert.Equal(ListForgeIntegratedSettings.WorkspaceModeClassic, document.PreferredWorkspaceModeByProfileName["Camisetas"]);
        }

        [Fact]
        public void Export_NeverIncludesProfileId_OnlyNames()
        {
            var settings = SettingsWithProfiles("Padrão");
            settings.SetPreferredWorkspaceMode("id-Padrão", ListForgeIntegratedSettings.WorkspaceModePx);

            var document = PortablePreferencesService.Export(settings);
            var json = PortablePreferencesService.Serialize(document);

            Assert.DoesNotContain("id-Padrão", json);
            Assert.Contains("Padrão", json);
        }

        [Fact]
        public void Export_IgnoresOrphanedPreferenceEntries_ForProfilesThatNoLongerExist()
        {
            var settings = SettingsWithProfiles("Padrão");
            settings.PreferredWorkspaceModeByProfileId["id-removido-ha-muito"] = ListForgeIntegratedSettings.WorkspaceModePx;

            var document = PortablePreferencesService.Export(settings);

            Assert.Empty(document.PreferredWorkspaceModeByProfileName);
        }

        [Fact]
        public void Serialize_UsesCamelCaseForSchemaFields_ButNeverLowercasesProfileNames()
        {
            var settings = SettingsWithProfiles("Padrão");
            settings.SetPreferredWorkspaceMode("id-Padrão", ListForgeIntegratedSettings.WorkspaceModePx);
            var json = PortablePreferencesService.Serialize(PortablePreferencesService.Export(settings));

            Assert.Contains("\"formatId\"", json);
            Assert.Contains("\"schemaVersion\"", json);
            Assert.Contains("\"editorFontSize\"", json);
            Assert.Contains("\"preferredWorkspaceModeByProfileName\"", json);
            // A chave do dicionário é o NOME do perfil (dado do usuário) — nunca recaseada.
            Assert.Contains("\"Padrão\"", json);
            Assert.DoesNotContain("\"padrão\"", json);
        }

        [Fact]
        public void Validate_RoundTrip_RecognizesAllFields()
        {
            var settings = SettingsWithProfiles("Padrão");
            settings.EditorFontSize = 20;
            settings.SetPreferredWorkspaceMode("id-Padrão", ListForgeIntegratedSettings.WorkspaceModePx);
            var json = PortablePreferencesService.Serialize(PortablePreferencesService.Export(settings));

            var result = PortablePreferencesService.Validate(json);

            Assert.True(result.IsValid);
            Assert.Equal(20, result.Document!.EditorFontSize);
            // Fonte, modo do perfil, "Mostrar nomes" e personalização do painel (sempre exportados).
            Assert.Equal(4, result.RecognizedFieldSummaries.Count);
            Assert.Empty(result.IncompatibleFieldSummaries);
        }

        [Fact]
        public void Validate_WrongFormatId_FailsWithClearReason()
        {
            var result = PortablePreferencesService.Validate("{\"formatId\":\"AlgumOutroFormato\",\"schemaVersion\":1}");

            Assert.False(result.IsValid);
            Assert.Contains("formato", result.FailureReason!, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Validate_MissingFormatId_Fails()
        {
            var result = PortablePreferencesService.Validate("{\"schemaVersion\":1}");

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_InvalidJson_FailsWithoutThrowing()
        {
            var result = PortablePreferencesService.Validate("isto nao e json {{{");

            Assert.False(result.IsValid);
            Assert.NotNull(result.FailureReason);
        }

        [Fact]
        public void Validate_UnknownSchemaVersion_Fails()
        {
            var result = PortablePreferencesService.Validate(
                $"{{\"formatId\":\"{PortablePreferencesService.FormatId}\",\"schemaVersion\":999}}");

            Assert.False(result.IsValid);
            Assert.Contains("esquema", result.FailureReason!, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Validate_FontSizeOutOfRange_IsIncompatibleButDoesNotFailWholeFile()
        {
            var json = $@"{{""formatId"":""{PortablePreferencesService.FormatId}"",""schemaVersion"":1,""editorFontSize"":999.0,""preferredWorkspaceModeByProfileName"":{{}}}}";

            var result = PortablePreferencesService.Validate(json);

            Assert.True(result.IsValid);
            Assert.Single(result.IncompatibleFieldSummaries);
            Assert.Equal(ListForgeIntegratedSettings.DefaultEditorFontSize, result.Document!.EditorFontSize);
        }

        [Fact]
        public void Validate_UnknownModeValue_IsIncompatibleButDoesNotFailWholeFile()
        {
            var json = $@"{{""formatId"":""{PortablePreferencesService.FormatId}"",""schemaVersion"":1,""editorFontSize"":13.0,""preferredWorkspaceModeByProfileName"":{{""Padrão"":""ModoInventado""}}}}";

            var result = PortablePreferencesService.Validate(json);

            Assert.True(result.IsValid);
            Assert.Single(result.IncompatibleFieldSummaries);
            Assert.Empty(result.Document!.PreferredWorkspaceModeByProfileName);
        }

        [Fact]
        public void Apply_UpdatesEditorFontSize_AndAppliesModeForMatchingProfileByName()
        {
            var target = SettingsWithProfiles("Padrão");
            var document = new PortablePreferencesService.Document
            {
                EditorFontSize = 22,
                PreferredWorkspaceModeByProfileName = new Dictionary<string, string> { ["Padrão"] = ListForgeIntegratedSettings.WorkspaceModePx },
            };

            var result = PortablePreferencesService.Apply(document, target);

            Assert.Equal(22, target.EditorFontSize);
            Assert.Equal(ListForgeIntegratedSettings.WorkspaceModePx, target.GetPreferredWorkspaceMode("id-Padrão"));
            Assert.Equal(2, result.AppliedSummaries.Count);
            Assert.Empty(result.SkippedSummaries);
        }

        [Fact]
        public void Apply_ProfileNameNotFoundOnTarget_IsSkipped_NeverCreatesNewProfile()
        {
            var target = SettingsWithProfiles("Padrão");
            var originalProfileCount = target.WorkProfiles.Count;
            var document = new PortablePreferencesService.Document
            {
                PreferredWorkspaceModeByProfileName = new Dictionary<string, string> { ["PerfilQueNaoExisteAqui"] = ListForgeIntegratedSettings.WorkspaceModePx },
            };

            var result = PortablePreferencesService.Apply(document, target);

            Assert.Single(result.SkippedSummaries);
            Assert.Equal(originalProfileCount, target.WorkProfiles.Count);
        }

        [Fact]
        public void Apply_NeverTouchesWorkProfileLocalFields()
        {
            var target = SettingsWithProfiles("Padrão");
            target.WorkProfiles[0].Settings.OutputDir = @"C:\Caminho\Especifico\Desta\Maquina";
            target.WorkProfiles[0].Settings.AdvancedSaveMode = ListForgeIntegratedSettings.ModeZip;
            var document = new PortablePreferencesService.Document
            {
                EditorFontSize = 15,
                PreferredWorkspaceModeByProfileName = new Dictionary<string, string> { ["Padrão"] = ListForgeIntegratedSettings.WorkspaceModePx },
            };

            PortablePreferencesService.Apply(document, target);

            Assert.Equal(@"C:\Caminho\Especifico\Desta\Maquina", target.WorkProfiles[0].Settings.OutputDir);
            Assert.Equal(ListForgeIntegratedSettings.ModeZip, target.WorkProfiles[0].Settings.AdvancedSaveMode);
        }
    }
}
#endif
