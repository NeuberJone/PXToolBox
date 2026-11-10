using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PXToolBoxDev.Diagnostics
{
    /// <summary>Lista do <see cref="CaptureChecklist"/> com o que já foi salvo
    /// (<see cref="CaptureReportStore.SavedItems"/>). O item escolhido vira
    /// <see cref="CurrentItemId"/>, que a janela de comparação usa. Só texto.</summary>
    public partial class CaptureChecklistWindow : Window
    {
        /// <summary>Item escolhido por último no roteiro (só nesta sessão do Corel).</summary>
        public static string? CurrentItemId { get; set; }

        private static CaptureChecklistWindow? _open;

        private IReadOnlyDictionary<string, DateTime> _saved = new Dictionary<string, DateTime>();

        public CaptureChecklistWindow()
        {
            InitializeComponent();
            CaptureReportStore.Saved += OnReportSaved;
            Refresh();
        }

        /// <summary>Uma janela só: abrir de novo traz a que já está aberta.</summary>
        public static void ShowSingle()
        {
            if (_open != null)
            {
                _open.Activate();
                return;
            }

            _open = new CaptureChecklistWindow();
            _open.Show();
        }

        /// <summary>Para testes e para a janela de comparação: item, ✓ e texto da linha.</summary>
        public IReadOnlyList<(string Id, bool Done, string Text)> DisplayedItems =>
            ItemsList.Items.OfType<ListBoxItem>().Select(i => ((string)i.Tag, _saved.ContainsKey((string)i.Tag), RowText(i))).ToList();

        public string ProgressLine => ProgressText.Text;

        public void Select(string id)
        {
            var row = ItemsList.Items.OfType<ListBoxItem>().FirstOrDefault(i => (string)i.Tag == id);
            if (row != null) ItemsList.SelectedItem = row;
        }

        public void Refresh()
        {
            _saved = CaptureReportStore.SavedItems();
            var selected = CurrentItemId;
            ItemsList.Items.Clear();
            foreach (var item in CaptureChecklist.All)
            {
                var done = _saved.ContainsKey(item.Id);
                var row = new StackPanel();
                row.Children.Add(new TextBlock
                {
                    Text = (done ? "✓  " : "○  ") + item.Tool,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = done ? (Brush)FindResource("Px.Done") : (Brush)FindResource("Px.Text"),
                });
                row.Children.Add(new TextBlock { Text = "     " + item.Variant, Style = (Style)FindResource("Px.Muted") });
                ItemsList.Items.Add(new ListBoxItem { Tag = item.Id, Content = row });
            }

            var doneCount = CaptureChecklist.All.Count(i => _saved.ContainsKey(i.Id));
            ProgressText.Text = $"{doneCount} de {CaptureChecklist.All.Count} capturas salvas";
            Select(selected ?? CaptureChecklist.All.FirstOrDefault(i => !_saved.ContainsKey(i.Id))?.Id ?? CaptureChecklist.All[0].Id);
        }

        private static string RowText(ListBoxItem row) =>
            string.Join(" | ", ((Panel)row.Content).Children.OfType<TextBlock>().Select(t => t.Text.Trim()));

        private void OnItemSelected(object sender, SelectionChangedEventArgs e)
        {
            var item = CaptureChecklist.Find((ItemsList.SelectedItem as ListBoxItem)?.Tag as string);
            if (item == null) return;

            CurrentItemId = item.Id;
            DetailTitle.Text = item.Label;
            DetailStatus.Text = _saved.TryGetValue(item.Id, out var when)
                ? $"✓ Salva em {when:dd/MM/yyyy HH:mm}. Pode repetir; cada relatório vira um arquivo."
                : "Ainda não salva.";
            DetailPrepare.Text = item.Prepare;
            DetailAction.Text = item.Action;
        }

        private void OnReportSaved() => Dispatcher.BeginInvoke(new Action(Refresh));

        private void OnOpenFolderClick(object sender, RoutedEventArgs e)
        {
            try
            {
                System.IO.Directory.CreateDirectory(CaptureReportStore.Directory);
                System.Diagnostics.Process.Start("explorer.exe", $"\"{CaptureReportStore.Directory}\"");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Não foi possível abrir a pasta: {ex.Message}";
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        private void Window_Closed(object? sender, EventArgs e)
        {
            CaptureReportStore.Saved -= OnReportSaved;
            if (ReferenceEquals(_open, this)) _open = null;
        }
    }
}
