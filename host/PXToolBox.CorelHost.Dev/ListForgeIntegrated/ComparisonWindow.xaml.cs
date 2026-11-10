using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ListForge.Models;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Porta de ListForge/UI/Views/ComparisonWindow.xaml(.cs) + ViewModels/ComparisonViewModel.cs
    /// (standalone) para o host do PX — mesmos dados/categorias/navegação/filtro/relatório, sem
    /// MVVM (consistente com o resto deste host, que é todo code-behind direto). Só leitura — não
    /// precisa do ThreadKeyboardRouter.
    /// </summary>
    public partial class ComparisonWindow : Window
    {
        private readonly ListComparisonResult _result;
        private List<FilterOption> _filters = new();
        private List<ComparisonItem> _filteredItems = new();

        // Classe simples, não record — record/init exige IsExternalInit, que não existe em
        // net48 sem um polyfill próprio (o host ainda não precisava de um até agora).
        private sealed class FilterOption
        {
            public FilterOption(ComparisonCategory? category, string label, int count)
            {
                Category = category;
                Label = label;
                Count = count;
            }

            public ComparisonCategory? Category { get; }
            public string Label { get; }
            public int Count { get; }
            public string DisplayName => $"{Label} ({Count})";
            public override string ToString() => DisplayName;
        }

        public ComparisonWindow(ListComparisonResult result)
        {
            InitializeComponent();
            _result = result;

            FillSummary();
            ApplyStatusTheme();
            BuildFilters();
            FilterCombo.ItemsSource = _filters;
            FilterCombo.SelectedIndex = 0;

            InputPane.Text = _result.Snapshot.InputText;
            OutputPane.Text = _result.Snapshot.OutputText;
            OutputOriginText.Text = _result.Snapshot.OutputWasManuallyEdited
                ? "Saída com edição manual aplicada"
                : "Saída gerada pelo processamento";
        }

        private void FillSummary()
        {
            var summary = _result.Summary;
            MetricInput.Text = $"Entrada: {summary.InputRecords}";
            MetricOutput.Text = $"Saída: {summary.OutputRecords}";
            MetricMatching.Text = $"Correspondentes: {summary.Matching}";
            MetricReordered.Text = $"Reorganizados: {summary.Reordered}";
            MetricTransformed.Text = $"Transformados: {summary.Transformed}";
            MetricChanged.Text = $"Alterados: {summary.Changed}";
            MetricMissing.Text = $"Ausentes: {summary.PossiblyMissing}";
            MetricAdded.Text = $"Adicionados: {summary.Added}";
            MetricUncertain.Text = $"Incertos: {summary.Uncertain}";
            MetricDuplicates.Text = $"Duplicidades entrada/saída: {summary.InputDuplicates} / {summary.OutputDuplicates}";
            StatusMessageText.Text = summary.StatusMessage;
        }

        private void ApplyStatusTheme()
        {
            var critical = _result.Summary.HasCriticalDifferences;
            StatusBorder.Background = (System.Windows.Media.Brush)FindResource(critical ? "Px.WarnBg" : "Px.Surface2");
            StatusBorder.BorderBrush = (System.Windows.Media.Brush)FindResource(critical ? "Px.Warn" : "Px.Border");
            StatusMessageText.Foreground = (System.Windows.Media.Brush)FindResource(critical ? "Px.Warn" : "Px.Text");
        }

        private void BuildFilters()
        {
            _filters = new List<FilterOption> { new(null, "Todos", _result.Items.Count) };
            AddFilter(ComparisonCategory.Matching, "Correspondentes");
            AddFilter(ComparisonCategory.Reordered, "Reorganizados");
            AddFilter(ComparisonCategory.Transformed, "Transformados");
            AddFilter(ComparisonCategory.Changed, "Alterados");
            AddFilter(ComparisonCategory.PossiblyMissing, "Possivelmente ausentes");
            AddFilter(ComparisonCategory.Added, "Adicionados");
            AddFilter(ComparisonCategory.Uncertain, "Correspondência incerta");
        }

        private void AddFilter(ComparisonCategory category, string label) =>
            _filters.Add(new FilterOption(category, label, _result.Items.Count(item => item.Category == category)));

        private void OnFilterChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

        private void ApplyFilter()
        {
            var selected = FilterCombo.SelectedItem as FilterOption;
            _filteredItems = selected?.Category == null
                ? _result.Items.ToList()
                : _result.Items.Where(item => item.Category == selected.Category).ToList();

            ItemsGrid.ItemsSource = _filteredItems;
            FilterStatusText.Text = $"Exibindo {_filteredItems.Count} de {_result.Items.Count} registro(s) comparado(s).";

            var toSelect = _filteredItems.FirstOrDefault(item => item.RequiresReview) ?? _filteredItems.FirstOrDefault();
            ItemsGrid.SelectedItem = toSelect;
        }

        private void OnItemSelected(object sender, SelectionChangedEventArgs e)
        {
            var item = ItemsGrid.SelectedItem as ComparisonItem;
            DifferencesGrid.ItemsSource = item?.FieldDifferences;

            SelectedDetailsTitleText.Text = item == null
                ? "Selecione um registro para ver os detalhes."
                : $"{item.CategoryLabel} - entrada {item.InputLineDisplay}, saída {item.OutputLineDisplay}";
            SelectedDetailsText.Text = item?.Details ?? "";

            HighlightLine(InputPane, item?.InputLineNumber);
            HighlightLine(OutputPane, item?.OutputLineNumber);
        }

        private static void HighlightLine(TextBox pane, int? lineNumber)
        {
            if (!lineNumber.HasValue) return;

            var start = TextLineLocator.GetLineStartIndex(pane.Text, lineNumber.Value);
            var length = TextLineLocator.GetLineLength(pane.Text, lineNumber.Value);
            pane.Select(start, length);
            pane.ScrollToLine(Math.Max(0, lineNumber.Value - 1));
        }

        private void OnNextDifference(object sender, RoutedEventArgs e) => NavigateDifference(1);

        private void OnPreviousDifference(object sender, RoutedEventArgs e) => NavigateDifference(-1);

        private void NavigateDifference(int direction)
        {
            var problems = _result.Items.Where(item => item.RequiresReview).ToList();
            if (problems.Count == 0) return;

            var current = ItemsGrid.SelectedItem as ComparisonItem;
            var currentIndex = current == null ? -1 : problems.IndexOf(current);
            var nextIndex = currentIndex < 0
                ? (direction > 0 ? 0 : problems.Count - 1)
                : (currentIndex + direction + problems.Count) % problems.Count;
            var target = problems[nextIndex];

            if (!_filteredItems.Contains(target))
                FilterCombo.SelectedIndex = 0;

            ItemsGrid.SelectedItem = target;
            ItemsGrid.ScrollIntoView(target);
        }

        private void OnCopyReport(object sender, RoutedEventArgs e) => Clipboard.SetText(BuildClipboardReport());

        private string BuildClipboardReport()
        {
            var summary = _result.Summary;
            var lines = new List<string>
            {
                "Comparação entre entrada e saída",
                "O relatório pode conter dados da lista. Compartilhe-o somente quando necessário.",
                "",
                $"Entrada: {summary.InputRecords} registro(s)",
                $"Saída: {summary.OutputRecords} registro(s)",
                $"Correspondentes: {summary.Matching}",
                $"Apenas reorganizados: {summary.Reordered}",
                $"Transformados: {summary.Transformed}",
                $"Alterados: {summary.Changed}",
                $"Possivelmente ausentes: {summary.PossiblyMissing}",
                $"Adicionados: {summary.Added}",
                $"Correspondências incertas: {summary.Uncertain}",
                $"Duplicidades na entrada: {summary.InputDuplicates}",
                $"Duplicidades na saída: {summary.OutputDuplicates}",
                "",
                summary.StatusMessage,
            };

            var reviewItems = _result.Items.Where(item => item.RequiresReview).ToList();
            if (reviewItems.Count > 0)
            {
                lines.Add("");
                lines.Add("Diferenças para revisão:");
                foreach (var item in reviewItems)
                {
                    lines.Add($"- {item.CategoryLabel} | entrada {item.InputLineDisplay} | saída {item.OutputLineDisplay} | {item.Summary}");
                    foreach (var difference in item.FieldDifferences)
                    {
                        var reason = string.IsNullOrWhiteSpace(difference.Reason) ? "" : $" | {difference.Reason}";
                        lines.Add($"  {difference.FieldName}: '{difference.InputValue}' -> '{difference.OutputValue}'{reason}");
                    }
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
