using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PXToolBoxDev.Favorites;
using PXToolBoxDev.ListForgeIntegrated;

namespace PXToolBoxDev.PanelLayout
{
    /// <summary>
    /// "Personalizar painel…": edita um RASCUNHO (cópias) do layout das seções e da
    /// personalização (nomes, seções criadas, seção de cada ferramenta). <c>save</c> só é chamado
    /// em "Salvar" — devolve null em caso de sucesso (a janela fecha) ou a mensagem de erro (a
    /// janela continua aberta com o rascunho, nada aplicado). Cancelar, Esc ou fechar descartam o
    /// rascunho sem gravar nada.
    /// </summary>
    public partial class PanelCustomizationWindow : Window
    {
        private PanelSectionLayout _draft;
        private PanelCustomization _customization;
        private readonly Func<PanelSectionLayout, PanelCustomization, string?> _save;
        private readonly Dictionary<string, Row> _rows = new Dictionary<string, Row>();
        private readonly ThreadKeyboardRouter _keyboardRouter;

        private sealed class Row
        {
            public Row(Grid element, CheckBox visible, TextBox title, Button up, Button down)
            {
                Element = element;
                Visible = visible;
                Title = title;
                Up = up;
                Down = down;
            }

            public Grid Element { get; }
            public CheckBox Visible { get; }
            public TextBox Title { get; }
            public Button Up { get; }
            public Button Down { get; }
        }

        /// <summary>Só o layout (compatível com quem ainda não passa personalização).</summary>
        public PanelCustomizationWindow(PanelSectionLayout current, Func<PanelSectionLayout, string?> save)
            : this(current, PanelCustomization.Default(), (layout, _) => save(layout))
        {
        }

        public PanelCustomizationWindow(PanelSectionLayout current, PanelCustomization customization, Func<PanelSectionLayout, PanelCustomization, string?> save)
        {
            InitializeComponent();
            _keyboardRouter = new ThreadKeyboardRouter(this);
            _customization = customization.Clone().Normalize();
            _draft = current.WithCustomSections(_customization.CustomSections.Select(s => s.Id));
            _save = save;
            RebuildSectionRows();
            RebuildToolRows();
        }

        /// <summary>O rascunho atual (para testes; o painel recebe o layout por <c>save</c>).</summary>
        public PanelSectionLayout Draft => _draft;

        /// <summary>A personalização em rascunho (para testes).</summary>
        public PanelCustomization DraftCustomization => _customization;

        private void Window_Loaded(object sender, RoutedEventArgs e) => _keyboardRouter.Install();

        private void Window_Activated(object sender, EventArgs e) => _keyboardRouter.ReassertFocus();

        private void Window_Closed(object? sender, EventArgs e) => _keyboardRouter.Uninstall();

        // ---- Seções ----

        private void RebuildSectionRows()
        {
            _rows.Clear();
            foreach (var entry in _draft.Sections)
            {
                _rows[entry.Id] = CreateRow(entry.Id);
            }

            Refresh();
        }

        private Row CreateRow(string sectionId)
        {
            var originalTitle = _customization.TitleOf(sectionId);
            var isCustom = _customization.IsCustomSection(sectionId);
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });

            var check = new CheckBox { Margin = new Thickness(0, 0, 6, 0) };
            AutomationProperties.SetName(check, $"Exibir \"{originalTitle}\"");
            check.Click += (_, _) =>
            {
                _draft.SetVisible(sectionId, check.IsChecked == true);
                UpdateRestoreDefault();
            };

            var title = new TextBox { Text = originalTitle, ToolTip = isCustom ? "Nome da seção criada." : $"Nome da seção (original: {PanelSectionCatalog.Find(sectionId)?.Title}). Vazio volta ao original." };
            AutomationProperties.SetName(title, $"Nome da seção \"{originalTitle}\"");
            title.TextChanged += (_, _) =>
            {
                if (_customization.Rename(sectionId, title.Text))
                {
                    UpdateRestoreDefault();
                }
            };
            title.LostFocus += (_, _) =>
            {
                // Vazio numa seção criada não vale: volta o nome guardado. Mostra o nome efetivo.
                title.Text = _customization.TitleOf(sectionId);
                RebuildToolRows();
            };

            var up = MoveButton("▲", $"Mover \"{originalTitle}\" para cima", () => Move(sectionId, up: true));
            var down = MoveButton("▼", $"Mover \"{originalTitle}\" para baixo", () => Move(sectionId, up: false));

            Grid.SetColumn(title, 1);
            Grid.SetColumn(up, 2);
            Grid.SetColumn(down, 3);
            grid.Children.Add(check);
            grid.Children.Add(title);
            grid.Children.Add(up);
            grid.Children.Add(down);
            if (isCustom)
            {
                var remove = MoveButton("✕", $"Remover a seção \"{originalTitle}\"", () => RemoveSection(sectionId));
                Grid.SetColumn(remove, 4);
                grid.Children.Add(remove);
            }

            return new Row(grid, check, title, up, down);
        }

        private Button MoveButton(string glyph, string description, Action onClick)
        {
            var button = new Button { Content = glyph, Style = (Style)FindResource("Px.MoveButton"), ToolTip = description };
            AutomationProperties.SetName(button, description);
            button.Click += (_, _) => onClick();
            return button;
        }

        private void Move(string id, bool up)
        {
            if (!(up ? _draft.MoveUp(id) : _draft.MoveDown(id))) return;
            Refresh();

            // Mantém o foco no mesmo botão (ou no outro, se este ficou desabilitado no extremo).
            var row = _rows[id];
            var target = up ? row.Up : row.Down;
            (target.IsEnabled ? target : (up ? row.Down : row.Up)).Focus();
        }

        // Reordena as MESMAS linhas (sem recriar: foco e teclado continuam válidos).
        private void Refresh()
        {
            var sections = _draft.Sections;
            SectionRows.Children.Clear();
            for (var i = 0; i < sections.Count; i++)
            {
                var row = _rows[sections[i].Id];
                row.Visible.IsChecked = sections[i].Visible;
                row.Up.IsEnabled = i > 0;
                row.Down.IsEnabled = i < sections.Count - 1;
                SectionRows.Children.Add(row.Element);
            }

            UpdateRestoreDefault();
        }

        private void UpdateRestoreDefault() => RestoreDefaultButton.IsEnabled = !_draft.IsDefault || !_customization.IsDefault;

        private void OnAddSectionClick(object sender, RoutedEventArgs e) => AddSection();

        /// <summary>"+ Nova seção": entra no fim, visível, com um nome a editar.</summary>
        public string AddSection(string? title = null)
        {
            var number = _customization.CustomSections.Count + 1;
            var id = _customization.AddSection(title ?? $"Nova seção {number}");
            _draft = _draft.WithCustomSections(_customization.CustomSections.Select(s => s.Id));
            RebuildSectionRows();
            RebuildToolRows();
            _rows[id].Title.Focus();
            _rows[id].Title.SelectAll();
            return id;
        }

        public void RemoveSection(string sectionId)
        {
            if (!_customization.RemoveSection(sectionId)) return;
            _draft = _draft.WithCustomSections(_customization.CustomSections.Select(s => s.Id));
            RebuildSectionRows();
            RebuildToolRows();
        }

        // ---- Ferramentas ----

        // Destinos possíveis: todas as seções na ordem do rascunho, menos os Favoritos (que têm
        // regras próprias).
        private IReadOnlyList<string> Destinations =>
            _draft.Sections.Select(e => e.Id).Where(id => id != PanelSectionCatalog.Favorites).ToList();

        private void RebuildToolRows()
        {
            ToolRows.Children.Clear();
            foreach (var group in PanelToolCatalog.All.GroupBy(t => PanelCustomization.HomeSectionOf(t.Id)))
            {
                ToolRows.Children.Add(new TextBlock
                {
                    Text = $"De {PanelSectionCatalog.Find(group.Key)?.Title ?? group.Key}",
                    Foreground = (System.Windows.Media.Brush)FindResource("Px.Accent"),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    Margin = new Thickness(0, ToolRows.Children.Count == 0 ? 2 : 12, 0, 4),
                });
                var alternate = false;
                foreach (var tool in group)
                {
                    var row = CreateToolRow(tool);
                    if (alternate) row.Background = (System.Windows.Media.Brush)FindResource("Px.RowAlt");
                    alternate = !alternate;
                    ToolRows.Children.Add(row);
                }
            }

            UpdateRestoreDefault();
        }

        private Grid CreateToolRow(PanelToolCatalog.Tool tool)
        {
            var grid = new Grid { Margin = new Thickness(-4, 0, -4, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = tool.Label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 4, 6, 4),
                Foreground = (System.Windows.Media.Brush)FindResource("Px.Text"),
            };
            var current = _customization.SectionOf(tool.Id);
            var moved = _customization.IsMoved(tool.Id);
            if (moved)
            {
                // Movida: em azul e em negrito — dá para achar de longe o que foi mudado.
                label.FontWeight = FontWeights.SemiBold;
                label.Foreground = (System.Windows.Media.Brush)FindResource("Px.Accent");
            }

            var picker = new Button
            {
                Style = (Style)FindResource("Px.SectionPicker"),
                Content = new TextBlock { Text = _customization.TitleOf(current) + "  ▾", TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap },
                ToolTip = "Escolher a seção onde esta ferramenta aparece.",
            };
            AutomationProperties.SetName(picker, $"Seção de \"{tool.Label}\"");
            picker.Click += (_, _) => OpenSectionMenu(picker, tool);

            var siblings = moved ? _customization.MovedInto(current) : Array.Empty<string>();
            var index = siblings.ToList().IndexOf(tool.Id);
            var up = MoveButton("▲", $"Subir \"{tool.Label}\"", () => { if (_customization.MoveUp(tool.Id)) RebuildToolRows(); });
            var down = MoveButton("▼", $"Descer \"{tool.Label}\"", () => { if (_customization.MoveDown(tool.Id)) RebuildToolRows(); });
            up.IsEnabled = moved && index > 0;
            down.IsEnabled = moved && index >= 0 && index < siblings.Count - 1;
            // Setas só nas movidas (as outras não têm ordem a escolher): menos ruído na lista.
            up.Visibility = moved ? Visibility.Visible : Visibility.Hidden;
            down.Visibility = moved ? Visibility.Visible : Visibility.Hidden;
            picker.Margin = new Thickness(6, 3, 0, 3);

            Grid.SetColumn(picker, 1);
            Grid.SetColumn(up, 2);
            Grid.SetColumn(down, 3);
            grid.Children.Add(label);
            grid.Children.Add(picker);
            grid.Children.Add(up);
            grid.Children.Add(down);
            return grid;
        }

        private void OpenSectionMenu(Button picker, PanelToolCatalog.Tool tool)
        {
            var menu = new ContextMenu { Style = (Style)FindResource("Px.ContextMenu"), PlacementTarget = picker, Placement = PlacementMode.Bottom };
            var home = PanelCustomization.HomeSectionOf(tool.Id);
            foreach (var id in Destinations)
            {
                var text = _customization.TitleOf(id) + (id == home ? "  (original)" : "");
                var item = new MenuItem
                {
                    Header = (_customization.SectionOf(tool.Id) == id ? "• " : "   ") + text,
                    Style = (Style)FindResource("Px.MenuItem"),
                };
                var target = id;
                item.Click += (_, _) => PlaceTool(tool.Id, target);
                menu.Items.Add(item);
            }

            menu.IsOpen = true;
        }

        /// <summary>Muda a seção de uma ferramenta no rascunho (também usado nos testes).</summary>
        public void PlaceTool(string toolId, string sectionId)
        {
            if (_customization.Place(toolId, sectionId)) RebuildToolRows();
        }

        // ---- Salvar / padrão ----

        private void OnRestoreDefaultClick(object sender, RoutedEventArgs e) => RestoreDefault();

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (TrySave())
            {
                DialogResult = true;
            }
        }

        /// <summary>"Restaurar padrão": só o rascunho (ordem, nomes, seções criadas, posições).</summary>
        public void RestoreDefault()
        {
            _customization = PanelCustomization.Default();
            _draft = PanelSectionLayout.Default();
            RebuildSectionRows();
            RebuildToolRows();
        }

        /// <summary>Entrega cópias do rascunho a <c>save</c>; em erro, mostra a mensagem e
        /// devolve false (a janela continua aberta).</summary>
        public bool TrySave()
        {
            var error = _save(_draft.Clone(), _customization.Clone().Normalize());
            if (error == null)
            {
                return true;
            }

            StatusMessageText.Text = error;
            StatusMessageText.Visibility = Visibility.Visible;
            return false;
        }

        /// <summary>Texto de erro visível (vazio se nenhum) — para testes.</summary>
        public string ErrorText => StatusMessageText.Visibility == Visibility.Visible ? StatusMessageText.Text : "";

        /// <summary>Linhas na ordem atual, como a janela mostra — para testes.</summary>
        public IReadOnlyList<(string Title, bool Visible, bool CanMoveUp, bool CanMoveDown)> DisplayedRows =>
            SectionRows.Children.Cast<Grid>()
                .Select(g =>
                {
                    var row = _rows.Values.First(r => r.Element == g);
                    return (row.Title.Text, row.Visible.IsChecked == true, row.Up.IsEnabled, row.Down.IsEnabled);
                })
                .ToList();

        /// <summary>Linhas de ferramentas: nome e o texto da seção escolhida — para testes.</summary>
        public IReadOnlyList<(string Tool, string Section)> DisplayedTools =>
            ToolRows.Children.OfType<Grid>()
                .Select(g => (((TextBlock)g.Children[0]).Text, ((TextBlock)((Button)g.Children[1]).Content).Text.Replace("  ▾", "")))
                .ToList();
    }
}
