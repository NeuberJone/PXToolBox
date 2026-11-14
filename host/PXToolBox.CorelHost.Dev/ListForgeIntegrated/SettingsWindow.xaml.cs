using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using ListForge.Services;
using Newtonsoft.Json;
using PXToolBoxDev.Integrations;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Tela de Configurações do modo Clássico — ver docs/listforge-classic-parity.md, grupo
    /// "Configurações", e Integrations/ListForgeIntegratedSettingsStore.cs para onde isso é
    /// gravado. Só coleta/valida os valores; a gravação em disco acontece aqui mesmo (ao clicar
    /// "Salvar"), mas quem abriu esta janela (<see cref="ClassicWorkspaceWindow"/>) é responsável
    /// por aplicar os valores já carregados (ex.: tamanho de fonte nas caixas de texto).
    ///
    /// Esquema 2 (iteração 13, perfis de trabalho): o formato do Salvar avançado e a pasta/nome
    /// padrão de exportação deixaram de ser campos únicos no topo de
    /// <see cref="ListForgeIntegratedSettings"/> e passaram a pertencer ao perfil de trabalho
    /// ATIVO (<see cref="ListForgeIntegratedSettings.WorkProfiles"/>/
    /// <see cref="ListForgeIntegratedSettings.ActiveWorkProfileId"/>). Esta tela continua
    /// editando só esses campos (mais o tamanho de fonte, que fica fora de qualquer perfil) —
    /// quem cria/seleciona/renomeia/duplica/exclui perfis é
    /// <see cref="ClassicWorkspaceWindow"/>, que também tem os controles de separador/
    /// capitalização/ordenação/lista avançada que compõem o resto de cada perfil.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly ThreadKeyboardRouter _keyboardRouter;
        private readonly WorkProfileService _workProfileService = new();

        // Cópia de trabalho do objeto recebido — "Importar configurações" pode substituí-la por
        // completo (import troca a lista de perfis inteira, igual ao standalone); as demais
        // edições desta tela (fonte, Salvar avançado, pasta/nome) só tocam o perfil ativo dela.
        // O objeto ORIGINAL passado ao construtor nunca é mutado até "Salvar" ter sucesso —
        // "Cancelar" simplesmente descarta esta cópia.
        private ListForgeIntegratedSettings _workingSettings;
        private string _chosenOutputDir;

        // Favoritos do painel só são gravados por esta tela quando uma importação os trouxe —
        // fora isso, Save mantém os que estão em disco (ver ListForgeIntegratedSettingsStore.Save).
        private bool _favoritesImported;

        // Mesma regra para o layout do painel (ordem/visibilidade das seções).
        private bool _sectionLayoutImported;

        // E para "Mostrar nomes" dos Favoritos e a personalização do painel.
        private bool _showLabelsImported;
        private bool _customizationImported;

        // Configurações de moldes trazidas por uma importação (arquivo próprio — MoldSettingsStore),
        // gravadas só ao Salvar; null = nenhuma importação as trouxe.
        private PXToolBox.Core.Molds.MoldSettings? _importedMoldSettings;
        private System.Collections.Generic.IReadOnlyList<PXToolBoxDev.Nesting.NestingProfile>? _importedNestingProfiles;

        /// <summary>Preenchido só quando o usuário clica "Salvar" com sucesso — null se
        /// cancelado ou se a janela foi fechada sem salvar.</summary>
        public ListForgeIntegratedSettings? SavedSettings { get; private set; }

        public SettingsWindow(ListForgeIntegratedSettings current, string loadNotice)
        {
            InitializeComponent();
            _keyboardRouter = new ThreadKeyboardRouter(this);

            LocationText.Text =
                "Gravado em %LocalAppData%\\PXToolBox\\Dev\\config\\listforge-integrated-settings.json — " +
                "exclusivo do PX Tool Box DEV. Nunca lido nem gravado pelo ListForge Standalone, pelo PX " +
                "de produção ou pelo MoldMotor. Formato do Salvar avançado e pasta/nome padrão abaixo " +
                "pertencem ao perfil de trabalho ativo — gerencie perfis na janela principal.";

            if (!string.IsNullOrEmpty(loadNotice))
            {
                NoticeText.Text = loadNotice;
                NoticeText.Visibility = Visibility.Visible;
            }

            _workingSettings = current.Clone();
            _chosenOutputDir = "";
            ApplyToControls(_workingSettings);
        }

        private void ApplyToControls(ListForgeIntegratedSettings settings)
        {
            FontSizeTextBox.Text = settings.EditorFontSize.ToString(CultureInfo.InvariantCulture);

            var profile = _workProfileService.GetActiveProfile(settings);
            var profileSettings = profile?.Settings ?? WorkProfileService.CaptureOfficialDefaultSettings();
            ActiveProfileNameText.Text = profile != null
                ? $"Estes dois campos pertencem ao perfil ativo: \"{profile.Name}\"."
                : "Perfil ativo não encontrado — usando valores padrão.";

            RadioZip.IsChecked = profileSettings.AdvancedSaveMode == ListForgeIntegratedSettings.ModeZip;
            RadioLooseFiles.IsChecked = profileSettings.AdvancedSaveMode != ListForgeIntegratedSettings.ModeZip;

            UseDefaultOutputDirCheck.IsChecked = profileSettings.UseDefaultOutputDir;
            _chosenOutputDir = profileSettings.OutputDir;
            OutputDirText.Text = string.IsNullOrWhiteSpace(_chosenOutputDir) ? "Nenhuma pasta escolhida ainda." : _chosenOutputDir;

            UseDefaultListNameCheck.IsChecked = profileSettings.UseDefaultListName;
            DefaultListNameTextBox.Text = profileSettings.DefaultListName;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _keyboardRouter.Install();
            FontSizeTextBox.Focus();
        }

        private void Window_Activated(object sender, EventArgs e) => _keyboardRouter.ReassertFocus();

        private void Window_Closed(object? sender, EventArgs e) => _keyboardRouter.Uninstall();

        private void OnUseDefaultOutputDirChanged(object sender, RoutedEventArgs e) { }

        private void OnUseDefaultListNameChanged(object sender, RoutedEventArgs e) { }

        private void OnChooseOutputDirClick(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Escolha a pasta padrão de exportação",
            };

            var ownerHandle = new WindowInteropHelper(this).Handle;
            if (dialog.ShowDialog(new Win32WindowWrapper(ownerHandle)) != System.Windows.Forms.DialogResult.OK)
                return;

            _chosenOutputDir = dialog.SelectedPath;
            OutputDirText.Text = _chosenOutputDir;
            StatusText.Text = "";
        }

        private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
        {
            var choice = MessageBox.Show(
                "Restaurar os valores padrão de tamanho de fonte e do Salvar avançado/pasta/nome do " +
                "perfil ATIVO?\n\nIsso não afeta nenhum outro perfil, o ListForge Standalone, o PX de " +
                "produção, o MoldMotor nem nenhuma outra configuração do PX Tool Box. Os campos abaixo " +
                "voltam ao padrão nesta janela — clique \"Salvar\" para confirmar, ou \"Cancelar\" para " +
                "manter a configuração atual.",
                "ListForge Integrado — restaurar padrões",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (choice != MessageBoxResult.Yes) return;

            FontSizeTextBox.Text = ListForgeIntegratedSettings.DefaultEditorFontSize.ToString(CultureInfo.InvariantCulture);
            RadioLooseFiles.IsChecked = true;
            RadioZip.IsChecked = false;
            UseDefaultOutputDirCheck.IsChecked = false;
            _chosenOutputDir = "";
            OutputDirText.Text = "Nenhuma pasta escolhida ainda.";
            UseDefaultListNameCheck.IsChecked = false;
            DefaultListNameTextBox.Text = "lista";
            StatusText.Text = "Campos restaurados ao padrão — clique em \"Salvar\" para confirmar.";
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (!TryBuildSettingsFromControls(out var settings, out var validationError))
            {
                StatusText.Text = validationError;
                return;
            }

            var profileSettings = _workProfileService.GetActiveProfile(settings)?.Settings;
            if (profileSettings == null)
            {
                StatusText.Text = "Perfil ativo não encontrado — não foi possível salvar.";
                return;
            }

            if (profileSettings.UseDefaultOutputDir && string.IsNullOrWhiteSpace(profileSettings.OutputDir))
            {
                StatusText.Text = "Escolha uma pasta antes de ativar \"Usar sempre esta pasta ao exportar\".";
                return;
            }

            if (profileSettings.UseDefaultListName && string.IsNullOrWhiteSpace(profileSettings.DefaultListName))
            {
                StatusText.Text = "Informe um nome base antes de ativar \"Usar sempre este nome base ao exportar\".";
                return;
            }

            try
            {
                ListForgeIntegratedSettingsStore.Save(settings, includePanelFavorites: _favoritesImported, includePanelSectionLayout: _sectionLayoutImported,
                    includePanelFavoritesShowLabels: _showLabelsImported, includePanelCustomization: _customizationImported);
            }
            catch (Exception ex)
            {
                // Mensagem clara quando não for possível salvar (pasta sem permissão, disco sem
                // espaço, etc.) — a janela continua aberta com os valores digitados, nada é
                // perdido, e nenhum arquivo parcialmente escrito fica no lugar do anterior
                // (ListForgeIntegratedSettingsStore.Save grava num temporário e só troca no
                // final; se a troca falhar, o arquivo anterior é restaurado antes de propagar).
                PxLogger.Error("SettingsWindow", "Falha ao salvar configurações.", ex, ListForgeIntegratedSettingsStore.FilePathForDisplay);
                StatusText.Text = $"Não foi possível salvar as configurações: {ex.Message} As alterações continuam aqui na tela, mas NÃO foram salvas em disco.";
                return;
            }

            _workingSettings = settings;
            SavedSettings = settings;

            // Arquivo próprio, gravado depois: se falhar, o resto já está salvo e a tela avisa.
            if (_importedMoldSettings != null)
            {
                try
                {
                    MoldSettingsStore.Save(_importedMoldSettings);
                    _importedMoldSettings = null;
                }
                catch (Exception ex)
                {
                    PxLogger.Error("SettingsWindow", "Falha ao salvar as configurações de moldes importadas.", ex, MoldSettingsStore.FilePath);
                    StatusText.Text = $"Configurações salvas, mas NÃO as de moldes importadas: {ex.Message} Clique em \"Salvar\" para tentar de novo.";
                    return;
                }
            }

            // Perfis do encaixe: arquivo próprio, nunca gravado por cima de um ilegível.
            if (_importedNestingProfiles != null)
            {
                if (!NestingProfileStore.Load().Readable)
                {
                    StatusText.Text = $"Configurações salvas, mas NÃO os perfis do encaixe importados: o arquivo atual de perfis está ilegível e não é gravado por cima ({NestingProfileStore.FilePath}). Corrija ou apague esse arquivo e importe de novo.";
                    return;
                }

                try
                {
                    NestingProfileStore.Save(_importedNestingProfiles);
                    _importedNestingProfiles = null;
                }
                catch (Exception ex)
                {
                    PxLogger.Error("SettingsWindow", "Falha ao salvar os perfis do encaixe importados.", ex, NestingProfileStore.FilePath);
                    StatusText.Text = $"Configurações salvas, mas NÃO os perfis do encaixe importados: {ex.Message} Clique em \"Salvar\" para tentar de novo.";
                    return;
                }
            }

            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

        // "Abrir pasta de configuração" (ver docs/listforge-classic-parity.md, grupo
        // "Configurações") — equivalente a OpenConfigFolder() do standalone, mas apontando para
        // a pasta do PX DEV (nunca ConfigManager.AppDir do ListForge). "Pasta de logs" continua
        // ausente: o integrado ainda não tem nenhuma infraestrutura de log própria (seria um
        // item novo, não só reaproveitar este caminho).
        private void OnOpenConfigFolderClick(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", ListForgeIntegratedSettingsStore.ConfigDirectoryForDisplay);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Não foi possível abrir a pasta: {ex.Message}";
            }
        }

        // Exportar/Importar configurações (.json portátil) — equivalente a SettingsExportService
        // do standalone, reduzido ao que já existe aqui (sem AppConfig/SizeConfig completos do
        // standalone, que o integrado não replica). Exporta os valores ATUAIS do formulário
        // (ainda não precisa ter clicado "Salvar"); importar só preenche o formulário — ainda é
        // preciso clicar "Salvar" para valer, igual a qualquer outra alteração nesta tela.
        private void OnExportSettingsClick(object sender, RoutedEventArgs e)
        {
            if (!TryBuildSettingsFromControls(out var settings, out var validationError))
            {
                StatusText.Text = validationError;
                return;
            }

            UseStoredPanelPreferences(settings);

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exportar configurações do ListForge Integrado",
                Filter = "Arquivo JSON (*.json)|*.json",
                FileName = "listforge-integrado-configuracoes.json",
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dialog.FileName, JsonConvert.SerializeObject(settings, Formatting.Indented));
                StatusText.Text = $"Configurações exportadas: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Não foi possível exportar: {ex.Message}";
            }
        }

        private void OnImportSettingsClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importar configurações do ListForge Integrado",
                Filter = "Arquivo JSON (*.json)|*.json",
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var imported = JsonConvert.DeserializeObject<ListForgeIntegratedSettings>(File.ReadAllText(dialog.FileName));
                if (imported == null)
                {
                    StatusText.Text = "Arquivo inválido — não foi possível importar.";
                    return;
                }

                imported.Normalize();
                // Import troca a lista de perfis inteira (igual ao standalone, que também
                // sobrescreve config.WorkProfiles/ActiveWorkProfileId ao importar) — não só os
                // campos do perfil ativo.
                _workingSettings = imported;
                _favoritesImported |= imported.PanelFavoriteToolIds != null;
                _sectionLayoutImported |= imported.PanelSectionLayout != null;
                _showLabelsImported |= imported.PanelFavoritesShowLabels != null;
                _customizationImported |= imported.PanelCustomization != null;
                ApplyToControls(_workingSettings);
                StatusText.Text = "Configurações importadas para esta tela (inclui perfis de trabalho) — clique em \"Salvar\" para confirmar.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Não foi possível importar: {ex.Message}";
            }
        }

        // Exportar/Importar PREFERÊNCIAS (distinto do par acima): só o que é realmente portável
        // entre máquinas — ver Integrations/PortablePreferencesService.cs para a lista explícita
        // de campos e o porquê de "configurações" (acima) e "preferências" (aqui) serem coisas
        // diferentes.
        private void OnExportPreferencesClick(object sender, RoutedEventArgs e)
        {
            if (!TryBuildSettingsFromControls(out var currentSettings, out var validationError))
            {
                StatusText.Text = validationError;
                return;
            }

            UseStoredPanelPreferences(currentSettings);
            var document = PortablePreferencesService.Export(currentSettings, _importedMoldSettings ?? MoldSettingsStore.Load().Settings, CurrentNestingProfiles());

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exportar preferências portáteis do PX Tool Box DEV",
                Filter = "Arquivo JSON (*.json)|*.json",
                FileName = "pxtoolbox-dev-preferencias.json",
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                AtomicFileWriter.WriteTextSafely(dialog.FileName, PortablePreferencesService.Serialize(document));
                StatusText.Text = $"Preferências exportadas: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                PxLogger.Error("SettingsWindow", "Falha ao exportar preferências portáteis.", ex, dialog.FileName);
                StatusText.Text = $"Não foi possível exportar: {ex.Message}";
            }
        }

        private void OnImportPreferencesClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importar preferências portáteis do PX Tool Box DEV",
                Filter = "Arquivo JSON (*.json)|*.json",
            };
            if (dialog.ShowDialog() != true) return;

            string json;
            try
            {
                json = File.ReadAllText(dialog.FileName);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Não foi possível ler o arquivo: {ex.Message}";
                return;
            }

            var validation = PortablePreferencesService.Validate(json);
            if (!validation.IsValid)
            {
                StatusText.Text = $"Não foi possível importar: {validation.FailureReason}";
                return;
            }

            var summaryLines = new System.Text.StringBuilder();
            summaryLines.AppendLine("Preferências reconhecidas neste arquivo:");
            foreach (var line in validation.RecognizedFieldSummaries)
                summaryLines.AppendLine($"  • {line}");

            if (validation.IncompatibleFieldSummaries.Count > 0)
            {
                summaryLines.AppendLine();
                summaryLines.AppendLine("Campos incompatíveis (serão ignorados):");
                foreach (var line in validation.IncompatibleFieldSummaries)
                    summaryLines.AppendLine($"  • {line}");
            }

            summaryLines.AppendLine();
            summaryLines.AppendLine("Confirma a substituição das preferências atuais por estas? Um backup recuperável do estado atual será salvo antes.");

            var choice = MessageBox.Show(summaryLines.ToString(), "Importar preferências — confirmação", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (choice != MessageBoxResult.Yes)
            {
                StatusText.Text = "Importação de preferências cancelada — nada foi alterado.";
                return;
            }

            // Backup recuperável do estado ATUAL antes de aplicar — nunca o arquivo de
            // configuração real (isto é só sobre as preferências portáteis, escrito ao lado do
            // arquivo real na mesma pasta de configuração do PX DEV).
            try
            {
                var backupSource = _workingSettings.Clone();
                UseStoredPanelPreferences(backupSource);
                var backupDocument = PortablePreferencesService.Export(backupSource, _importedMoldSettings ?? MoldSettingsStore.Load().Settings, CurrentNestingProfiles());
                var backupPath = System.IO.Path.Combine(
                    ListForgeIntegratedSettingsStore.ConfigDirectoryForDisplay,
                    $"pxtoolbox-dev-preferencias-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                AtomicFileWriter.WriteTextSafely(backupPath, PortablePreferencesService.Serialize(backupDocument));
            }
            catch (Exception ex)
            {
                // Melhor esforço, mas nunca prossegue com a importação sem o backup — preserva o
                // estado anterior em caso de dúvida, em vez de aplicar "sem rede de segurança".
                PxLogger.Error("SettingsWindow", "Falha ao salvar backup de preferências antes de importar.", ex);
                StatusText.Text = $"Importação cancelada: não foi possível salvar um backup recuperável antes de aplicar ({ex.Message}).";
                return;
            }

            // Aplica sobre o que está em disco agora (o painel pode ter mudado com esta tela aberta).
            UseStoredPanelPreferences(_workingSettings);
            var applyResult = PortablePreferencesService.Apply(validation.Document!, _workingSettings);
            _favoritesImported |= validation.Document!.PanelFavoriteToolIds != null;
            _sectionLayoutImported |= validation.Document!.PanelSectionLayout != null;
            _showLabelsImported |= validation.Document!.PanelFavoritesShowLabels != null;
            _customizationImported |= validation.Document!.PanelCustomization != null;
            _importedMoldSettings = PortablePreferencesService.ReadMoldSettings(validation.Document!) ?? _importedMoldSettings;
            _importedNestingProfiles = PortablePreferencesService.ReadNestingProfiles(validation.Document!) ?? _importedNestingProfiles;
            // Uma personalização importada redefine as seções criadas: o layout gravado junto
            // precisa refletir isso mesmo quando o arquivo não trouxe layout.
            _sectionLayoutImported |= validation.Document!.PanelCustomization != null && _workingSettings.PanelSectionLayout != null;
            ApplyToControls(_workingSettings);

            var statusBuilder = new System.Text.StringBuilder("Preferências importadas para esta tela — clique em \"Salvar\" para confirmar.");
            if (applyResult.SkippedSummaries.Count > 0)
                statusBuilder.Append($" {applyResult.SkippedSummaries.Count} ignorada(s) (perfil não encontrado nesta instalação).");
            StatusText.Text = statusBuilder.ToString();
        }

        /// <summary>Os perfis do encaixe a exportar: os importados nesta tela, ou os do arquivo — nenhum
        /// (chave ausente) se o arquivo estiver ilegível.</summary>
        private System.Collections.Generic.IReadOnlyList<PXToolBoxDev.Nesting.NestingProfile>? CurrentNestingProfiles()
        {
            if (_importedNestingProfiles != null) return _importedNestingProfiles;
            var (profiles, readable, _) = NestingProfileStore.Load();
            return readable ? profiles : null;
        }

        // Esta tela pode ter sido aberta antes de o usuário mudar favoritos ou o layout no
        // painel — exportar sempre o que está em disco, salvo se uma importação desta tela já
        // os substituiu.
        private void UseStoredPanelPreferences(ListForgeIntegratedSettings settings)
        {
            if (!_favoritesImported)
            {
                settings.PanelFavoriteToolIds = ListForgeIntegratedSettingsStore.ReadStoredPanelFavoritesOrNull();
            }

            if (!_sectionLayoutImported)
            {
                settings.PanelSectionLayout = ListForgeIntegratedSettingsStore.ReadStoredPanelSectionLayoutOrNull();
            }

            if (!_showLabelsImported)
            {
                settings.PanelFavoritesShowLabels = ListForgeIntegratedSettingsStore.ReadStoredPanelFavoritesShowLabelsOrNull();
            }

            if (!_customizationImported)
            {
                settings.PanelCustomization = ListForgeIntegratedSettingsStore.ReadStoredPanelCustomizationOrNull();
            }
        }

        // Parte de _workingSettings (preserva WorkProfiles/ActiveWorkProfileId inteiros) e só
        // sobrescreve o tamanho de fonte (fora de qualquer perfil) e os campos do perfil ATIVO
        // editados nesta tela (formato do Salvar avançado, pasta/nome padrão de exportação).
        private bool TryBuildSettingsFromControls(out ListForgeIntegratedSettings settings, out string error)
        {
            settings = _workingSettings.Clone();
            error = "";

            if (!double.TryParse(FontSizeTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fontSize)
                || fontSize < ListForgeIntegratedSettings.MinEditorFontSize
                || fontSize > ListForgeIntegratedSettings.MaxEditorFontSize)
            {
                error = $"Tamanho de fonte inválido — informe um número entre {ListForgeIntegratedSettings.MinEditorFontSize:0} e {ListForgeIntegratedSettings.MaxEditorFontSize:0}.";
                return false;
            }

            settings.EditorFontSize = fontSize;

            var profile = _workProfileService.GetActiveProfile(settings);
            if (profile == null)
            {
                error = "Perfil ativo não encontrado.";
                return false;
            }

            profile.Settings.AdvancedSaveMode = RadioZip.IsChecked == true ? ListForgeIntegratedSettings.ModeZip : ListForgeIntegratedSettings.ModeLooseFiles;
            profile.Settings.UseDefaultOutputDir = UseDefaultOutputDirCheck.IsChecked == true;
            profile.Settings.OutputDir = _chosenOutputDir;
            profile.Settings.UseDefaultListName = UseDefaultListNameCheck.IsChecked == true;
            profile.Settings.DefaultListName = DefaultListNameTextBox.Text.Trim();
            profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            return true;
        }
    }
}
