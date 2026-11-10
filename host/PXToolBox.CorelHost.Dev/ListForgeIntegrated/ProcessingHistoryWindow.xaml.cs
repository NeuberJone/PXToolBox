using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Porta de ListForge/UI/Views/HistoryView.xaml(.cs) (standalone) para o modo Clássico do
    /// ListForge Integrado — ver docs/listforge-classic-parity.md, grupo "Revisão e resumos", e o
    /// comentário de <see cref="Integrations.ListForgeProcessingHistoryService"/> sobre por que
    /// só reproduz "Abrir pasta da saída" e "Limpar histórico" (o standalone não tem exclusão
    /// individual nem "restaurar" um registro). Sem MVVM, consistente com o resto deste host.
    /// </summary>
    public partial class ProcessingHistoryWindow : Window
    {
        private readonly Integrations.ListForgeProcessingHistoryService _historyService;

        public ProcessingHistoryWindow(Integrations.ListForgeProcessingHistoryService historyService)
        {
            InitializeComponent();
            _historyService = historyService;
            ReloadEntries();
        }

        private void ReloadEntries()
        {
            var entries = new List<Integrations.ListForgeProcessingHistoryEntry>(_historyService.Load());
            HistoryGrid.ItemsSource = entries;
            EmptyStateBorder.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateButtonsEnabled();
            StatusText.Text = entries.Count == 0
                ? ""
                : $"{entries.Count} registro(s).";
        }

        private void UpdateButtonsEnabled()
        {
            var hasEntries = HistoryGrid.Items.Count > 0;
            BtnClearHistory.IsEnabled = hasEntries;
            BtnOpenOutputFolder.IsEnabled = HistoryGrid.SelectedItem != null;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtonsEnabled();

        private void OnOpenOutputFolder(object sender, RoutedEventArgs e)
        {
            var selected = HistoryGrid.SelectedItem as Integrations.ListForgeProcessingHistoryEntry;
            if (selected == null) return;

            var result = _historyService.OpenOutputFolder(selected);
            StatusText.Text = result.Success ? "Pasta da saída aberta." : result.UserMessage;
            if (!result.Success)
                MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Mesma confirmação do standalone (MainViewModel.ClearProcessingHistory): Sim/Não antes
        // de apagar tudo — nunca some silenciosamente. Afeta só o histórico DESTE módulo (próprio
        // arquivo, próprio serviço) — nunca o histórico do ListForge Standalone.
        private void OnClearHistory(object sender, RoutedEventArgs e)
        {
            if (HistoryGrid.Items.Count == 0) return;

            var confirm = MessageBox.Show(
                "Deseja apagar todo o histórico de processamentos deste módulo? Esta ação não afeta o ListForge Standalone.",
                "ListForge Integrado",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            var result = _historyService.Clear();
            if (!result.Success)
            {
                MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ReloadEntries();
            StatusText.Text = "Histórico limpo.";
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
