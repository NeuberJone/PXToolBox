using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PXToolBox.Outputs.Tools;

namespace PXToolBoxDev.Molds
{
    /// <summary>Relatório do "Validar molde": resumo, peças encontradas e um item por problema
    /// (erros primeiro). Só texto — quem valida é o painel.</summary>
    public partial class MoldValidationWindow : Window
    {
        private static MoldValidationWindow? _open;

        private Action? _revalidate;
        private MoldValidationService.Report? _report;

        public MoldValidationWindow()
        {
            InitializeComponent();
        }

        /// <summary>Uma janela só: validar de novo atualiza a que já está aberta.</summary>
        public static void ShowReport(MoldValidationService.Report report, Action? revalidate)
        {
            if (_open == null)
            {
                _open = new MoldValidationWindow();
                _open.Show();
            }

            _open._revalidate = revalidate;
            _open.Display(report);
            _open.Activate();
        }

        /// <summary>Para testes: o relatório como texto (o mesmo do "Copiar").</summary>
        public string ReportText => _report == null ? string.Empty : AsText(_report);

        public void Display(MoldValidationService.Report report)
        {
            _report = report;
            SummaryText.Text = report.Summary;
            SummaryText.Foreground = (Brush)FindResource(report.ErrorCount > 0 ? "Px.Error" : report.WarningCount > 0 ? "Px.Warn" : "Px.Ok");
            PiecesText.Text = report.Pieces.Count == 0 ? string.Empty : "Peças: " + string.Join(", ", report.Pieces) + ".";
            PiecesText.Visibility = report.Pieces.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            IssuesPanel.Children.Clear();
            if (report.Issues.Count == 0)
            {
                IssuesPanel.Children.Add(new TextBlock
                {
                    Text = report.GroupCount == 0 ? "Nada para conferir nesta página." : "Tudo certo com os grupos de tamanho desta página.",
                    Style = (Style)FindResource("Px.Muted"),
                    Margin = new Thickness(0, 4, 0, 4),
                });
            }

            foreach (var issue in report.Issues)
            {
                IssuesPanel.Children.Add(Row(issue));
            }

            FooterText.Text = $"Conferido às {DateTime.Now:HH:mm:ss}. Erros primeiro; depois por peça e tamanho.";
        }

        private Grid Row(MoldValidationService.Issue issue)
        {
            var error = issue.Severity == MoldValidationService.Severity.Error;
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock
            {
                Text = error ? "ERRO" : "AVISO",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource(error ? "Px.Error" : "Px.Warn"),
                Margin = new Thickness(0, 1, 0, 0),
            });
            var message = new TextBlock { Text = issue.Message };
            Grid.SetColumn(message, 1);
            row.Children.Add(message);
            return row;
        }

        private static string AsText(MoldValidationService.Report report)
        {
            var lines = new List<string> { report.Summary };
            if (report.Pieces.Count > 0) lines.Add("Peças: " + string.Join(", ", report.Pieces) + ".");
            lines.AddRange(report.Issues.Select(i => (i.Severity == MoldValidationService.Severity.Error ? "ERRO: " : "AVISO: ") + i.Message));
            return string.Join(Environment.NewLine, lines);
        }

        private void OnRevalidateClick(object sender, RoutedEventArgs e) => _revalidate?.Invoke();

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ReportText);
                FooterText.Text = "Relatório copiado.";
            }
            catch (Exception ex)
            {
                FooterText.Text = $"Não foi possível copiar: {ex.Message}";
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        private void Window_Closed(object? sender, EventArgs e)
        {
            if (ReferenceEquals(_open, this)) _open = null;
        }
    }
}
