using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PXToolBoxDev.ListForgeIntegrated;

namespace PXToolBoxDev.Diagnostics
{
    /// <summary>Mostra o relatório de <see cref="PageCaptureDiff"/>; o rótulo da ferramenta
    /// testada é acrescentado ao texto ao digitar. Só texto — nada aqui lê ou altera o Corel.</summary>
    public partial class PageCaptureReportWindow : Window
    {
        private readonly ThreadKeyboardRouter _keyboardRouter;
        private readonly PageCapture _before;
        private readonly PageCapture _after;

        public PageCaptureReportWindow(PageCapture before, PageCapture after)
        {
            InitializeComponent();
            _keyboardRouter = new ThreadKeyboardRouter(this);
            _before = before;
            _after = after;
            ChooseItem(CaptureChecklistWindow.CurrentItemId);
            Refresh();
        }

        public string ReportText => ReportTextBox.Text;

        /// <summary>Item do roteiro desta comparação (null = livre).</summary>
        public string? SelectedItemId { get; private set; }

        public string ChecklistButtonText => (string)ChecklistItemButton.Content;

        /// <summary>Escolhe o item do roteiro e põe o nome dele no campo "Ferramenta testada"
        /// (o que já foi escrito lá é trocado só se era o nome do item anterior ou estava vazio).</summary>
        public void ChooseItem(string? id)
        {
            var previous = CaptureChecklist.Find(SelectedItemId);
            var item = CaptureChecklist.Find(id);
            SelectedItemId = item?.Id;
            ChecklistItemButton.Content = item?.Label ?? "Nenhum (relatório livre)";
            var current = ToolLabelTextBox.Text.Trim();
            if (current.Length == 0 || current == previous?.Label)
            {
                ToolLabelTextBox.Text = item?.Label ?? "";
            }
        }

        /// <summary>Salva na pasta de capturas e passa o roteiro para o próximo item pendente.
        /// Devolve o caminho, ou null com a mensagem no status.</summary>
        public string? SaveToFolder()
        {
            try
            {
                var path = CaptureReportStore.Save(SelectedItemId, ReportTextBox.Text);
                StatusText.Text = $"Salvo na pasta de capturas: {System.IO.Path.GetFileName(path)}";
                if (SelectedItemId != null)
                {
                    var saved = CaptureReportStore.SavedItems();
                    var all = CaptureChecklist.All;
                    var start = all.ToList().FindIndex(i => i.Id == SelectedItemId);
                    var next = all.Skip(start + 1).Concat(all.Take(start + 1)).FirstOrDefault(i => !saved.ContainsKey(i.Id));
                    CaptureChecklistWindow.CurrentItemId = next?.Id ?? SelectedItemId;
                }

                return path;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Não foi possível salvar: {ex.Message}";
                return null;
            }
        }

        private void OnChecklistItemClick(object sender, RoutedEventArgs e)
        {
            var saved = CaptureReportStore.SavedItems();
            var menu = new ContextMenu { Style = (Style)FindResource("Px.ContextMenu"), PlacementTarget = ChecklistItemButton, Placement = PlacementMode.Bottom };
            var itemStyle = (Style)FindResource("Px.MenuItem");
            menu.Items.Add(MenuEntry("Nenhum (relatório livre)", null, itemStyle));
            foreach (var item in CaptureChecklist.All)
            {
                menu.Items.Add(MenuEntry((saved.ContainsKey(item.Id) ? "✓  " : "○  ") + item.Label, item.Id, itemStyle));
            }

            menu.IsOpen = true;
        }

        private MenuItem MenuEntry(string text, string? id, Style style)
        {
            var entry = new MenuItem { Header = text, Style = style, FontWeight = id == SelectedItemId ? FontWeights.SemiBold : FontWeights.Normal };
            entry.Click += (s, e) => ChooseItem(id);
            return entry;
        }

        private void OnSaveToFolderClick(object sender, RoutedEventArgs e) => SaveToFolder();

        private void Refresh() => ReportTextBox.Text = PageCaptureDiff.Report(_before, _after, ToolLabelTextBox.Text);

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _keyboardRouter.Install();
            ToolLabelTextBox.Focus();
        }

        private void Window_Activated(object sender, EventArgs e) => _keyboardRouter.ReassertFocus();

        private void Window_Closed(object? sender, EventArgs e) => _keyboardRouter.Uninstall();

        private void OnToolLabelChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => Refresh();

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ReportTextBox.Text);
                StatusText.Text = "Relatório copiado.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Não foi possível copiar: {ex.Message}";
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Salvar comparação antes/depois",
                Filter = "Texto (*.txt)|*.txt",
                FileName = $"px-comparacao-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                System.IO.File.WriteAllText(dialog.FileName, ReportTextBox.Text, new UTF8Encoding(false));
                StatusText.Text = $"Salvo em {dialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Não foi possível salvar: {ex.Message}";
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
