using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ListForge.Models;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Porta de ListForge/UI/Views/ProcessingPreviewDialog.cs (standalone) para o host do PX —
    /// mesmas seções, tema PX. Ver <see cref="ClassicWorkspaceWindow.OnProcessWithPreview"/> para
    /// a orquestração (Analyze → mostra esta janela → se confirmado, ExecuteConfirmed).
    /// </summary>
    public partial class ProcessingPreviewWindow : Window
    {
        private ProcessingPreviewWindow(ProcessingPreview preview)
        {
            InitializeComponent();
            Fill(preview);
        }

        public static bool ShowDialog(Window owner, ProcessingPreview preview)
        {
            var window = new ProcessingPreviewWindow(preview) { Owner = owner };
            return window.ShowDialog() == true;
        }

        private void OnProcessNowClick(object sender, RoutedEventArgs e) => DialogResult = true;

        private void OnBackClick(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Fill(ProcessingPreview preview)
        {
            SummaryText.Text =
                $"{preview.TotalRecords} registro(s) analisado(s)\n" +
                $"{preview.ValidRecords} registro(s) válido(s)\n" +
                $"{preview.WarningRecords} registro(s) com possível problema\n" +
                $"{preview.InvalidRecords} registro(s) inválido(s)\n" +
                $"Lista avançada: {(preview.Snapshot.AdvancedListEnabled ? "ativa" : "inativa")}";

            SizesList.ItemsSource = preview.Sizes.Count == 0
                ? new[] { "Nenhum tamanho processável encontrado." }
                : preview.Sizes.Select(item => $"{item.Size}: {item.Count}").ToList();

            PieceTypesList.ItemsSource = preview.PieceTypes.Count == 0
                ? new[] { "Nenhum tipo de peça processável encontrado." }
                : preview.PieceTypes.Select(item => $"{item.PieceType}: {item.Count}").ToList();

            OutputText.Text =
                $"Pasta: {preview.Snapshot.OutputDirectoryDescription}\n" +
                $"Arquivo: {preview.Snapshot.OutputFileDescription}\n" +
                "Formato: Lista organizada e Prévia JSON no editor; arquivos são gerados apenas nas ações de salvar.";

            if (preview.Warnings.Count > 0)
            {
                WarningsCard.Visibility = Visibility.Visible;
                WarningsList.ItemsSource = preview.Warnings;
            }

            if (preview.Issues.Count > 0)
            {
                IssuesCard.Visibility = Visibility.Visible;
                IssuesPreviewList.ItemsSource = preview.Issues
                    .Take(30)
                    .Select(issue => $"Linha {issue.LineNumber} — {SeverityLabel(issue.Severity)}: {issue.Message}. {issue.SuggestedAction}")
                    .ToList();
            }

            if (!preview.CanProcess)
            {
                BlockedCard.Visibility = Visibility.Visible;
                BtnProcessNow.IsEnabled = false;
            }
        }

        private static string SeverityLabel(ProcessingIssueSeverity severity) =>
            severity == ProcessingIssueSeverity.Warning ? "Possível problema" : "Inválido";
    }
}
