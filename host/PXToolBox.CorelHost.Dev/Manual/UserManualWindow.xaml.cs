using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PXToolBoxDev.ListForgeIntegrated;

namespace PXToolBoxDev.Manual
{
    /// <summary>Mostra o <see cref="UserManual"/>: assuntos gerais e seções do painel à esquerda,
    /// o conteúdo à direita. O campo do alto filtra as duas coisas: com texto, a lista só mostra o
    /// que tem todas as palavras (seções com a contagem) e cada página só as ferramentas que
    /// combinam. Só texto.</summary>
    public partial class UserManualWindow : Window
    {
        private const string TopicTag = "topic:";
        private const string SectionTag = "section:";

        private static UserManualWindow? _open;

        private readonly ThreadKeyboardRouter _keyboardRouter;
        private readonly List<string> _displayedTools = new List<string>();

        /// <summary>Página escolhida (tag); null com filtro = visão geral dos resultados.</summary>
        private string? _page;

        /// <summary>Página de antes do filtro, para voltar a ela quando o filtro é apagado.</summary>
        private string? _pageBeforeFilter;

        private bool _rebuildingNavigation;

        public UserManualWindow()
        {
            InitializeComponent();
            _keyboardRouter = new ThreadKeyboardRouter(this);
            var sections = UserManual.Sections;
            FooterText.Text = $"{sections.Sum(s => s.Tools.Count)} ferramentas em {sections.Count} seções.";
            _page = TopicTag + UserManual.Topics[0].Title;
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

            _open = new UserManualWindow();
            _open.Show();
        }

        /// <summary>Para testes: títulos da lista da esquerda (já filtrada), na ordem.</summary>
        public IReadOnlyList<string> NavigationTitles =>
            NavList.Items.OfType<ListBoxItem>().Select(i => i.Tag as string).Where(t => t != null).Select(t => TitleOf(t!)).ToList();

        /// <summary>Ids das ferramentas mostradas agora, na ordem.</summary>
        public IReadOnlyList<string> DisplayedToolIds => _displayedTools;

        /// <summary>Todo o texto mostrado à direita, uma linha por bloco.</summary>
        public string DisplayedText =>
            string.Join("\n", Descendants(ContentPanel).OfType<TextBlock>().Select(t => t.Text));

        public string SearchText { get => SearchBox.Text; set => SearchBox.Text = value; }

        /// <summary>Para testes: título escolhido na lista (null = nenhum; "" = um item sem página).</summary>
        public string? SelectedNavigationTitle =>
            NavList.SelectedItem is ListBoxItem item ? (item.Tag is string tag ? TitleOf(tag) : "") : null;

        /// <summary>Abre o assunto ou a seção com esse título (como clicar na lista); com filtro,
        /// só se ele estiver na lista filtrada.</summary>
        public void ShowPage(string title)
        {
            NavList.SelectedItem = NavList.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag is string tag && TitleOf(tag) == title);
        }

        private string Query => SearchBox.Text.Trim();

        private static string TitleOf(string tag) =>
            tag.StartsWith(TopicTag, StringComparison.Ordinal) ? tag.Substring(TopicTag.Length) : tag.Substring(SectionTag.Length);

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            if (Query.Length == 0)
            {
                _page = _pageBeforeFilter ?? _page ?? TopicTag + UserManual.Topics[0].Title;
                _pageBeforeFilter = null;
            }
            else
            {
                // Cada mudança no filtro volta à visão geral dos resultados; escolher na lista restringe.
                if (_pageBeforeFilter == null) _pageBeforeFilter = _page;
                _page = null;
            }

            Refresh();
        }

        private void OnClearSearchClick(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
            SearchBox.Focus();
        }

        private void OnNavSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_rebuildingNavigation || !((NavList.SelectedItem as ListBoxItem)?.Tag is string tag)) return;
            _page = tag;
            ShowContent();
        }

        private void Refresh()
        {
            BuildNavigation();
            ShowContent();
        }

        private void BuildNavigation()
        {
            var query = Query;
            _rebuildingNavigation = true;
            try
            {
                NavList.Items.Clear();
                foreach (var topic in UserManual.Topics.Where(t => UserManual.Matches(t, query)))
                {
                    NavList.Items.Add(NavItem(topic.Title, TopicTag + topic.Title, null));
                }

                var sections = UserManual.Sections
                    .Select(s => (Section: s, Count: s.Tools.Count(t => UserManual.Matches(t, query))))
                    .Where(s => s.Count > 0)
                    .ToList();
                if (sections.Count > 0)
                {
                    // Só um título: sem o visual de item (não seleciona nem destaca).
                    NavList.Items.Add(new ListBoxItem
                    {
                        Content = new TextBlock { Text = "SEÇÕES DO PAINEL", Style = (Style)FindResource("Px.Label") },
                        Style = (Style)FindResource("Px.NavHeader"),
                        IsEnabled = false,
                        Focusable = false,
                    });
                }

                foreach (var (section, count) in sections)
                {
                    NavList.Items.Add(NavItem(section.Title, SectionTag + section.Title, query.Length > 0 ? count : (int?)null));
                }

                // _page null (visão geral do filtro) não pode casar com o título, que não tem Tag.
                var current = _page == null ? null : NavList.Items.OfType<ListBoxItem>().FirstOrDefault(i => (i.Tag as string) == _page);
                if (current == null) _page = null;
                NavList.SelectedItem = current;
            }
            finally
            {
                _rebuildingNavigation = false;
            }

            HintText.Text = query.Length == 0
                ? "Escolha um assunto ou uma seção do painel. Cada ferramenta: para que serve, como usar e o que observar."
                : "Filtro ligado: a lista e as páginas mostram só o que tem todas as palavras. ✕ limpa o filtro.";
        }

        private ListBoxItem NavItem(string title, string tag, int? count)
        {
            var text = new TextBlock { Text = title };
            if (count != null)
            {
                text.Inlines.Add(new System.Windows.Documents.Run($"  {count}") { Foreground = (Brush)FindResource("Px.Accent") });
            }

            return new ListBoxItem { Tag = tag, Content = text };
        }

        private void ShowContent()
        {
            ContentPanel.Children.Clear();
            _displayedTools.Clear();
            ContentScroll.ScrollToTop();
            var query = Query;

            if (_page == null)
            {
                ShowResults(query);
                return;
            }

            if (_page.StartsWith(TopicTag, StringComparison.Ordinal))
            {
                var topic = UserManual.Topics.First(t => TopicTag + t.Title == _page);
                Add(new TextBlock { Text = topic.Title, Style = (Style)FindResource("Px.PageTitle") });
                foreach (var paragraph in topic.Paragraphs)
                {
                    Add(new TextBlock { Text = paragraph, Margin = new Thickness(0, 0, 0, 10) });
                }

                return;
            }

            var section = UserManual.Sections.First(s => SectionTag + s.Title == _page);
            var tools = section.Tools.Where(t => UserManual.Matches(t, query)).ToList();
            Add(new TextBlock { Text = section.Title, Style = (Style)FindResource("Px.PageTitle") });
            if (query.Length > 0)
            {
                Add(Muted($"{tools.Count} de {section.Tools.Count} ferramenta(s) desta seção com “{query}”. ✕ no filtro mostra todas."));
            }
            else if (section.Intro.Length > 0)
            {
                Add(new TextBlock { Text = section.Intro, Margin = new Thickness(0, 0, 0, 6) });
            }

            foreach (var tool in tools)
            {
                AddTool(tool, showSection: false);
            }
        }

        private void ShowResults(string query)
        {
            var tools = UserManual.Search(query);
            var topics = UserManual.SearchTopics(query);
            Add(new TextBlock { Text = "Resultado do filtro", Style = (Style)FindResource("Px.PageTitle") });
            if (tools.Count == 0 && topics.Count == 0)
            {
                Add(Muted($"Nada encontrado para “{query}”. Tente outra palavra ou limpe o filtro (✕)."));
                return;
            }

            Add(Muted($"{tools.Count} ferramenta(s) com “{query}”."
                + (topics.Count > 0 ? $" Assuntos gerais com essas palavras: {string.Join(", ", topics.Select(t => t.Title))} (na lista à esquerda)." : "")));
            foreach (var tool in tools)
            {
                AddTool(tool, showSection: true);
            }
        }

        private TextBlock Muted(string text) =>
            new TextBlock { Text = text, Style = (Style)FindResource("Px.Muted"), Margin = new Thickness(0, 0, 0, 6) };

        private void AddTool(ManualTool tool, bool showSection)
        {
            _displayedTools.Add(tool.Id);
            var block = new StackPanel();
            block.Children.Add(new TextBlock { Text = tool.Label, Style = (Style)FindResource("Px.ToolTitle") });
            if (showSection)
            {
                block.Children.Add(new TextBlock { Text = "Seção " + tool.Section, Style = (Style)FindResource("Px.Muted") });
            }

            block.Children.Add(new TextBlock { Text = tool.Purpose, Margin = new Thickness(0, 4, 0, 0) });
            if (tool.Steps.Count > 0)
            {
                block.Children.Add(new TextBlock { Text = "COMO USAR", Style = (Style)FindResource("Px.Label") });
                for (var i = 0; i < tool.Steps.Count; i++)
                {
                    block.Children.Add(Line($"{i + 1}.", tool.Steps[i]));
                }
            }

            if (tool.Notes.Count > 0)
            {
                block.Children.Add(new TextBlock { Text = "OBSERVAÇÕES", Style = (Style)FindResource("Px.Label") });
                foreach (var note in tool.Notes)
                {
                    block.Children.Add(Line("•", note));
                }
            }

            Add(new Border
            {
                Child = block,
                Background = (Brush)FindResource("Px.Surface1"),
                BorderBrush = (Brush)FindResource("Px.Border"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 8, 0, 0),
            });
        }

        private static Grid Line(string marker, string text)
        {
            var line = new Grid { Margin = new Thickness(0, 1, 0, 2) };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.Children.Add(new TextBlock { Text = marker });
            var body = new TextBlock { Text = text };
            Grid.SetColumn(body, 1);
            line.Children.Add(body);
            return line;
        }

        private void Add(UIElement element) => ContentPanel.Children.Add(element);

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _keyboardRouter.Install();
            SearchBox.Focus();
        }

        private void Window_Activated(object sender, EventArgs e) => _keyboardRouter.ReassertFocus();

        private void Window_Closed(object? sender, EventArgs e)
        {
            _keyboardRouter.Uninstall();
            if (ReferenceEquals(_open, this)) _open = null;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
