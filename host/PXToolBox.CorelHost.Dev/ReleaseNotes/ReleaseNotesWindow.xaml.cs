using System.Windows;

namespace PXToolBoxDev.ReleaseNotes
{
    public partial class ReleaseNotesWindow : Window
    {
        public ReleaseNotesWindow(string version)
        {
            InitializeComponent();
            var notes = ReleaseNotesCatalog.ForVersion(version);
            VersionText.Text = $"PX Tool Box DEV • v{notes.Version}";
            NoticeText.Text = notes.Notice;
            NoticeText.Visibility = string.IsNullOrEmpty(notes.Notice) ? Visibility.Collapsed : Visibility.Visible;
            ImplementedItems.ItemsSource = notes.Implemented;
            ChangedItems.ItemsSource = notes.Changed;
            RemovedItems.ItemsSource = notes.Removed;
            ImplementedEmptyText.Visibility = notes.Implemented.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ChangedEmptyText.Visibility = notes.Changed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RemovedEmptyText.Visibility = notes.Removed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
