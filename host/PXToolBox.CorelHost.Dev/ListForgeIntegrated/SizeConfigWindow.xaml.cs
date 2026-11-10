using System;
using System.Windows;
using ListForge.Core;
using ListForge.Models;
using PXToolBoxDev.Integrations;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Tela de Tamanhos customizados do modo Clássico — ver docs/listforge-classic-parity.md,
    /// grupo "Configurações aplicáveis ao integrado". Porta de SizeGroupBindings/
    /// LoadSizeConfigIntoBindings/BuildSizeConfigFromUI/RestoreDefaultSizes do standalone,
    /// usando <see cref="SizeHelper"/> (núcleo compartilhado) para toda a lógica de parsing/
    /// normalização — esta classe só coleta/exibe os valores dos 4 grupos fixos (masculino/
    /// feminino/infantil/meião, <see cref="SizeHelper.EditableGroupOrder"/>).
    ///
    /// Independente de perfil de trabalho — igual ao standalone (sizes.json é um arquivo
    /// separado de config.json/WorkProfiles lá); aqui vive em
    /// <see cref="ListForgeIntegratedSettings.Sizes"/>, um campo de nível de módulo, nunca
    /// dentro de um perfil. Grava direto no disco ao clicar "Salvar" (mesmo padrão de
    /// <see cref="SettingsWindow"/>) — quem abriu esta janela é responsável por aplicar
    /// <see cref="SavedSettings"/> de volta ao processamento (ver
    /// ClassicWorkspaceWindow.OnOpenSizeConfig).
    /// </summary>
    public partial class SizeConfigWindow : Window
    {
        private readonly ThreadKeyboardRouter _keyboardRouter;
        private ListForgeIntegratedSettings _workingSettings;

        /// <summary>Preenchido só quando o usuário clica "Salvar" com sucesso — null se
        /// cancelado ou se a janela foi fechada sem salvar.</summary>
        public ListForgeIntegratedSettings? SavedSettings { get; private set; }

        public SizeConfigWindow(ListForgeIntegratedSettings current)
        {
            InitializeComponent();
            _keyboardRouter = new ThreadKeyboardRouter(this);

            _workingSettings = current.Clone();
            ApplyToControls(_workingSettings.Sizes);
        }

        private void ApplyToControls(SizeConfig config)
        {
            var normalized = SizeHelper.Normalize(config);

            SetGroupControls(normalized, SizeHelper.GroupMale, TxtMaleBase, TxtMalePrefixes, TxtMaleSuffixes);
            SetGroupControls(normalized, SizeHelper.GroupFemale, TxtFemaleBase, TxtFemalePrefixes, TxtFemaleSuffixes);
            SetGroupControls(normalized, SizeHelper.GroupChild, TxtChildBase, TxtChildPrefixes, TxtChildSuffixes);
            SetGroupControls(normalized, SizeHelper.GroupSock, TxtSockBase, TxtSockPrefixes, TxtSockSuffixes);

            RefreshSummary(normalized);
        }

        private static void SetGroupControls(
            SizeConfig config, string groupKey,
            System.Windows.Controls.TextBox baseBox,
            System.Windows.Controls.TextBox prefixesBox,
            System.Windows.Controls.TextBox suffixesBox)
        {
            var group = config.Groups[groupKey];
            baseBox.Text = SizeHelper.TokensToCsv(group.BaseSizes);
            prefixesBox.Text = SizeHelper.TokensToCsv(group.Prefixes);
            suffixesBox.Text = SizeHelper.TokensToCsv(group.Suffixes);
        }

        private void RefreshSummary(SizeConfig config) =>
            SummaryText.Text = SizeHelper.BuildSizeSummary(config);

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _keyboardRouter.Install();
            TxtMaleBase.Focus();
        }

        private void Window_Activated(object sender, EventArgs e) => _keyboardRouter.ReassertFocus();

        private void Window_Closed(object? sender, EventArgs e) => _keyboardRouter.Uninstall();

        // Reseta só os campos NESTA tela (ainda exige "Salvar" para valer) — mesmo padrão de
        // SettingsWindow.OnRestoreDefaultsClick. Escopo explícito no aviso: só os tamanhos, não
        // afeta perfis de trabalho nem as demais configurações.
        private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
        {
            var choice = MessageBox.Show(
                "Restaurar os tamanhos customizados (masculino/feminino/infantil/meião) para os " +
                "valores oficiais do ListForge?\n\nIsso não afeta perfis de trabalho, o tamanho de " +
                "fonte, o Salvar avançado nem nenhuma outra configuração do PX Tool Box. Os campos " +
                "abaixo voltam ao padrão nesta janela — clique \"Salvar\" para confirmar, ou " +
                "\"Cancelar\" para manter a configuração atual.",
                "ListForge Integrado — restaurar tamanhos padrão",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (choice != MessageBoxResult.Yes) return;

            ApplyToControls(SizeConfig.Default());
            StatusText.Text = "Tamanhos restaurados ao padrão — clique em \"Salvar\" para confirmar.";
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (!TryBuildSizeConfigFromControls(out var sizes, out var validationError))
            {
                StatusText.Text = validationError;
                return;
            }

            _workingSettings.Sizes = sizes;

            try
            {
                ListForgeIntegratedSettingsStore.Save(_workingSettings);
            }
            catch (Exception ex)
            {
                // Mesma garantia de SettingsWindow: a janela continua aberta com os valores
                // digitados, nada é perdido, e ListForgeIntegratedSettingsStore.Save nunca deixa
                // um arquivo parcialmente escrito no lugar do anterior.
                PxLogger.Error("SizeConfigWindow", "Falha ao salvar tamanhos customizados.", ex, ListForgeIntegratedSettingsStore.FilePathForDisplay);
                StatusText.Text = $"Não foi possível salvar os tamanhos: {ex.Message} As alterações continuam aqui na tela, mas NÃO foram salvas em disco.";
                return;
            }

            SavedSettings = _workingSettings;
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

        // Mesma regra do standalone (MainViewModel.BuildSizeConfigFromUI): cada grupo precisa de
        // ao menos um tamanho-base — nunca silenciosamente aceito vazio. Preserva tamanhos
        // digitados nos outros grupos quando um deles falha a validação (só recusa salvar, não
        // perde o que já foi digitado em nenhum campo).
        private bool TryBuildSizeConfigFromControls(out SizeConfig sizes, out string error)
        {
            sizes = SizeConfig.Default();
            error = "";
            var cfg = SizeConfig.Default();

            foreach (var groupKey in SizeHelper.EditableGroupOrder)
            {
                var (baseBox, prefixesBox, suffixesBox) = GetGroupControls(groupKey);
                var bases = SizeHelper.ParseCsvTokens(baseBox.Text);
                var prefixes = SizeHelper.ParseCsvTokens(prefixesBox.Text);
                var suffixes = SizeHelper.ParseCsvTokens(suffixesBox.Text);

                if (bases.Count == 0)
                {
                    error = $"Informe ao menos um tamanho-base para {SizeHelper.GroupLabels[groupKey]}.";
                    return false;
                }

                cfg = SizeHelper.UpdateGroupConfig(cfg, groupKey, bases, prefixes, suffixes);
            }

            sizes = cfg;
            return true;
        }

        private (System.Windows.Controls.TextBox Base, System.Windows.Controls.TextBox Prefixes, System.Windows.Controls.TextBox Suffixes) GetGroupControls(string groupKey) =>
            groupKey switch
            {
                SizeHelper.GroupMale => (TxtMaleBase, TxtMalePrefixes, TxtMaleSuffixes),
                SizeHelper.GroupFemale => (TxtFemaleBase, TxtFemalePrefixes, TxtFemaleSuffixes),
                SizeHelper.GroupChild => (TxtChildBase, TxtChildPrefixes, TxtChildSuffixes),
                _ => (TxtSockBase, TxtSockPrefixes, TxtSockSuffixes),
            };
    }
}
