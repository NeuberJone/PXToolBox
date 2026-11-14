using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CorelApplication = Corel.Interop.VGCore.Application;
using CorelDocument = Corel.Interop.VGCore.Document;
using PXToolBox.Core.Measurement;
using PXToolBox.Core.Naming;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBoxDev.Corel;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.Favorites;
using PXToolBoxDev.PanelLayout;

namespace PXToolBoxDev
{
    /// <summary>
    /// Painel raiz do add-on PX Tool Box DEV, hospedado pelo CorelDRAW como um item de UI do
    /// tipo "wpfhost" (ver docs/ARCHITECTURE.md e Packaging/AppUI.xslt). Cada botão chama um
    /// serviço de PXToolBox.Outputs através dos adaptadores em Corel/*, nunca manipula COM
    /// diretamente — mantendo a lógica de negócio isolada e testável.
    /// </summary>
    public partial class ControlUI : UserControl
    {
        private readonly CodeToggleService _codeToggle = new CodeToggleService();
        private readonly RenameSelectedObjectsService _renameSelected = new RenameSelectedObjectsService();
        private readonly PieceTypeRenameService _pieceTypeRename = new PieceTypeRenameService();
        private readonly LayerVisibilityService _layerVisibility = new LayerVisibilityService();
        private readonly MeasurementSumService _measurementSum = new MeasurementSumService();
        private readonly GridPreparationService _grid = new GridPreparationService();
        private readonly StandardSizeService _standardSize = new StandardSizeService();
        private readonly SisboltMarkingService _sisboltMarking = new SisboltMarkingService();
        private readonly SingleItemGroupService _singleItemGroup = new SingleItemGroupService();
        private readonly RotationResetService _rotationReset = new RotationResetService();
        private readonly LayerStandardService _layerStandard = new LayerStandardService();
        private readonly MoldToolsService _moldTools = new MoldToolsService(() => MoldSettingsStore.Load().Settings);
        private readonly MoldDefineService _moldDefine;
        private readonly MoldLogoPropagationService _moldLogos = new MoldLogoPropagationService(() => MoldSettingsStore.Load().Settings);
        private readonly MoldValidationService _moldValidation = new MoldValidationService(() => MoldSettingsStore.Load().Settings);
        private readonly TapeMeasureService _tapeMeasure = new TapeMeasureService();
        private readonly SliceCutService _sliceCut = new SliceCutService();
        private readonly SliceGuideService _sliceGuides = new SliceGuideService();
        private readonly NestingService _nesting = new NestingService();
        private readonly MoldDuplicationService _moldDuplication = new MoldDuplicationService(() => MoldSettingsStore.Load().Settings);
        private readonly MoldSaveService _moldSave = new MoldSaveService();
        private readonly HorizontalAlignmentLinkService _horizontalAlignmentLink = new HorizontalAlignmentLinkService();
        private readonly MoldPreparationService _mold = new MoldPreparationService();
        private readonly OutputPreparationService _output = new OutputPreparationService();
        private readonly IntersectGroupService _intersectGroup = new IntersectGroupService();
        private readonly PageRenameService _pageRename = new PageRenameService();
        private readonly ListForgeLauncher _listForge = new ListForgeLauncher(
            ListForgeConfigStore.ReadConfiguredPath,
            ListForgeConfigStore.SaveConfiguredPath,
            System.IO.File.Exists,
            StartListForgeProcess);
        private readonly NexorLauncher _nexor = new NexorLauncher(
            NexorConfigStore.ReadConfiguredPath,
            NexorConfigStore.SaveConfiguredPath,
            System.IO.File.Exists,
            StartNexorProcess);

        private CorelApplication? _app;
        private ListForgeIntegrated.ClassicWorkspaceWindow? _listForgeIntegratedWindow;
        private Production.OrderContextWindow? _orderContextWindow;

        public ControlUI()
        {
            InitializeComponent();
            ToTypeCombo.ItemsSource = PieceTypeCode.All.Select(p => p.Code).ToList();
            ToTypeCombo.SelectedIndex = 0;
            MoldPieceTypeCombo.ItemsSource = PieceTypeCode.All.Select(p => $"{p.Code} — {p.DisplayName}").ToList();
            MoldPieceTypeCombo.SelectedIndex = 0;
            RefreshMoldSizes();
            MoldSettingsStore.Changed += () => Dispatcher.BeginInvoke(new Action(RefreshMoldSizes));
            MoldSortCombo.ItemsSource = new[] { "Do maior para o menor", "Do menor para o maior" };
            MoldSortCombo.SelectedIndex = 0;
            MoldDirectionCombo.ItemsSource = new[] { "Vertical", "Horizontal" };
            MoldDirectionCombo.SelectedIndex = 0;
            // Margens da TAG (cm) na unidade do documento, como a distância do Distribuir (mm).
            _moldDefine = new MoldDefineService(() => MoldSettingsStore.Load().Settings,
                cm => _app == null ? cm : _app.ConvertUnits(cm, global::Corel.Interop.VGCore.cdrUnit.cdrCentimeter, _app.ActiveDocument.Unit));
            MoldCopyDirectionCombo.ItemsSource = new[] { "À esquerda", "À direita", "Acima", "Abaixo" };
            MoldCopyDirectionCombo.SelectedIndex = 0;
            FromTypeCombo.ItemsSource = new[] { AnyTypeLabel }.Concat(PieceTypeCode.All.Select(p => p.Code)).ToList();
            FromTypeCombo.SelectedIndex = 0;

            OutputFolderTextBox.TextChanged += (_, _) => UpdateOutputFolderPreview();
            UseRollSubfolderCheckBox.Checked += (_, _) => UpdateOutputFolderPreview();
            UseRollSubfolderCheckBox.Unchecked += (_, _) => UpdateOutputFolderPreview();
            UpdateOutputFolderPreview();
            UpdateListForgePathText();
            UpdateNexorPathText();
            AppVersionText.Text = $"PX Tool Box DEV • v{ResolveAppVersion()}";

            // SelectionChange (DIVGApplicationEvents, confirmado por reflexão sobre
            // Corel.Interop.VGCore.dll) atualiza o estado do botão de Tamanho Padrão sem
            // polling. Loaded/Unloaded evitam subscription duplicada se este painel for
            // escondido/reexibido pelo host sem destruir a instância (Unloaded sempre
            // desinscreve antes; Loaded reinscreve de forma idempotente via DetachSelectionChange
            // dentro de AttachSelectionChange).
            Loaded += (_, _) => AttachSelectionChange();
            Unloaded += (_, _) => OnPanelUnloaded();

            InitializeFavorites();
            Loaded += (_, _) => AttachFavoritesChanged();
            Unloaded += (_, _) => DetachFavoritesChanged();

            InitializeSectionLayout();
            UpdateRecentOrderContextsButton();
            Loaded += (_, _) => AttachSectionLayoutChanged();
            Unloaded += (_, _) => DetachSectionLayoutChanged();

            Connect();
        }

        // ---- Favoritos (ver Favorites/PanelToolCatalog.cs) ----

        private PanelFavoritesList _favorites = new PanelFavoritesList(null);
        private Dictionary<string, Button> _favoriteSources = new Dictionary<string, Button>();
        private bool _favoritesChangedAttached;
        private bool _savingFavorites;
        private List<string> _visibleFavoriteIds = new List<string>();
        private bool _favoritesShowLabels;
        private bool _loadingFavoritesDisplay;
        private FavoriteDragReorder? _favoriteDrag;

        /// <summary>Liga cada Id estável do catálogo ao botão original da sua seção — só em
        /// memória; o que é gravado é o Id. O botão é achado pelo mesmo Id em
        /// <c>AutomationProperties.AutomationId</c> (todo botão de ferramenta das seções tem um).</summary>
        private void InitializeFavorites()
        {
            _favoriteSources = FindToolButtons(SectionsHost)
                .Where(b => PanelToolCatalog.Find(AutomationProperties.GetAutomationId(b)) != null)
                .ToDictionary(b => AutomationProperties.GetAutomationId(b), b => b);

            foreach (var source in _favoriteSources)
            {
                AttachFavoriteMenu(source.Key, source.Value);
            }

            _favoriteDrag = new FavoriteDragReorder(FavoritesItems, FavoritesDropIndicator, OnFavoriteDropped);
            ReloadFavorites();
        }

        /// <summary>Todos os botões com AutomationId sob <paramref name="root"/> (árvore lógica:
        /// inclui as seções recolhidas, cujo conteúdo ainda não tem árvore visual).</summary>
        internal static IEnumerable<Button> FindToolButtons(DependencyObject root)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                if (child is Button button && !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(button)))
                {
                    yield return button;
                }

                foreach (var nested in FindToolButtons(child))
                {
                    yield return nested;
                }
            }
        }

        private void AttachFavoriteMenu(string toolId, Button original)
        {
            var item = new MenuItem { Style = (Style)FindResource("Px.MenuItem") };
            var menu = new ContextMenu { Style = (Style)FindResource("Px.ContextMenu") };
            menu.Items.Add(item);
            menu.Opened += (_, _) => item.Header = _favorites.Contains(toolId) ? "Remover dos favoritos" : "Adicionar aos favoritos";
            item.Click += (_, _) => ChangeFavorites(f => f.Contains(toolId) ? f.Remove(toolId) : f.Add(toolId));
            original.ContextMenu = menu;
            // "Mostrar últimas camadas ocultadas" passa a maior parte do tempo desabilitado.
            ContextMenuService.SetShowOnDisabled(original, true);
        }

        private void ChangeFavorites(Func<PanelFavoritesList, bool> change)
        {
            if (!change(_favorites))
            {
                return;
            }

            RebuildFavorites();
            try
            {
                _savingFavorites = true;
                ListForgeIntegratedSettingsStore.SavePanelFavorites(_favorites.IdsToPersist);
            }
            catch (Exception ex)
            {
                SetStatus($"Favoritos alterados nesta sessão, mas NÃO salvos: {ex.Message}");
            }
            finally
            {
                _savingFavorites = false;
            }
        }

        private void ReloadFavorites()
        {
            var loaded = ListForgeIntegratedSettingsStore.LoadPanelFavorites();
            _favorites = new PanelFavoritesList(loaded.Ids);
            _favoritesShowLabels = ListForgeIntegratedSettingsStore.LoadPanelFavoritesShowLabels();
            _loadingFavoritesDisplay = true;
            FavoritesShowLabelsCheckBox.IsChecked = _favoritesShowLabels;
            _loadingFavoritesDisplay = false;
            RebuildFavorites();
            if (!string.IsNullOrEmpty(loaded.Notice))
            {
                SetStatus(loaded.Notice);
            }
        }

        private void RebuildFavorites()
        {
            FavoritesItems.Children.Clear();
            var visible = _favorites.Visible.Where(t => _favoriteSources.ContainsKey(t.Id)).ToList();
            _visibleFavoriteIds = visible.Select(t => t.Id).ToList();
            FavoritesEmptyText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            FavoritesShowLabelsCheckBox.Visibility = visible.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            // Com nome: um botão por linha, na largura da seção (ItemWidth segue o Grid que
            // contém o WrapPanel); só ícone: quadrados que quebram linha.
            if (_favoritesShowLabels && FavoritesItems.Parent is FrameworkElement column)
            {
                FavoritesItems.SetBinding(WrapPanel.ItemWidthProperty, new System.Windows.Data.Binding(nameof(FrameworkElement.ActualWidth)) { Source = column });
            }
            else
            {
                System.Windows.Data.BindingOperations.ClearBinding(FavoritesItems, WrapPanel.ItemWidthProperty);
                FavoritesItems.ClearValue(WrapPanel.ItemWidthProperty);
            }

            var shortcutStyle = (Style)FindResource(_favoritesShowLabels ? "Px.FavoriteLabelButton" : "Px.FavoriteIconButton");
            for (var i = 0; i < visible.Count; i++)
            {
                var tool = visible[i];
                var content = _favoritesShowLabels ? CreateFavoriteLabelContent(tool) : CreateFavoriteIcon(tool);
                var shortcut = FavoriteShortcut.Create(_favoriteSources[tool.Id], tool.Label, content, shortcutStyle);
                shortcut.ContextMenu = CreateFavoriteShortcutMenu(tool, isFirst: i == 0, isLast: i == visible.Count - 1);
                ContextMenuService.SetShowOnDisabled(shortcut, true);
                FavoritesItems.Children.Add(shortcut);
            }
        }

        /// <summary>"Mostrar nomes": ícone (com a marca da seção) e o nome da ferramenta. O nome
        /// acompanha o Foreground do botão, como o ícone (estado ativo do Px.StateButton).</summary>
        private UIElement CreateFavoriteLabelContent(PanelToolCatalog.Tool tool)
        {
            var label = new TextBlock
            {
                Text = tool.Label,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
            };
            label.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(nameof(Control.Foreground))
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1),
            });
            var row = new DockPanel { LastChildFill = true };
            var icon = CreateFavoriteIcon(tool);
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            row.Children.Add(label);
            return row;
        }

        private void OnFavoritesShowLabelsChanged(object sender, RoutedEventArgs e)
        {
            if (_loadingFavoritesDisplay) return;

            _favoritesShowLabels = FavoritesShowLabelsCheckBox.IsChecked == true;
            RebuildFavorites();
            try
            {
                _savingFavorites = true;
                ListForgeIntegratedSettingsStore.SavePanelFavoritesShowLabels(_favoritesShowLabels);
            }
            catch (Exception ex)
            {
                SetStatus($"Favoritos: modo de exibição alterado nesta sessão, mas NÃO salvo: {ex.Message}");
            }
            finally
            {
                _savingFavorites = false;
            }
        }

        // Glifo da fonte de ícones do Windows, ou a sigla do botão original (MA/FE/C); "+"
        // âmbar no canto para quem cria algo novo. Cor herdada do botão: segue o estado ativo.
        private UIElement CreateFavoriteIcon(PanelToolCatalog.Tool tool)
        {
            var icon = tool.Icon;
            var symbol = new TextBlock
            {
                Text = icon.Symbol,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
            };
            // O estilo implícito de TextBlock deste painel fixaria Px.Text: o ícone acompanha o
            // Foreground do botão (Px.StateButton troca para Px.AccentText quando ativo). Remoção
            // (nunca tem estado ativo): sempre vermelho.
            var removal = icon.IsRemoval ? (System.Windows.Media.Brush)FindResource("Px.Removal") : null;
            if (removal != null)
            {
                symbol.Foreground = removal;
            }
            else
            {
                symbol.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(nameof(Control.Foreground))
                {
                    RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1),
                });
            }
            if (icon.IsText)
            {
                symbol.FontWeight = FontWeights.SemiBold;
                // Duas linhas (NO/NU) ou três caracteres não cabem a 12 num quadrado de 36.
                symbol.TextAlignment = TextAlignment.Center;
                if (icon.Symbol.Contains("\n"))
                {
                    symbol.FontSize = 9;
                    symbol.LineHeight = 10;
                    symbol.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                }
                else
                {
                    symbol.FontSize = 12;
                }
            }
            else
            {
                symbol.FontFamily = (System.Windows.Media.FontFamily)FindResource("Px.IconFont");
                symbol.FontSize = 16;
            }

            UIElement content = symbol;
            var badgeText = BadgeFor(tool);
            if (!string.IsNullOrEmpty(badgeText))
            {
                // Marca da seção (até duas letras, Tool.Badge) presa no canto superior direito do
                // quadrado; o símbolo desce um pouco para as duas não se encostarem (com 11 e
                // margem negativa, "Co"/"Ca" invadiam NO/MA — conferido na renderização).
                var badge = new TextBlock
                {
                    Text = badgeText,
                    FontWeight = FontWeights.Bold,
                    FontSize = removal != null ? 11 : 9,
                    Foreground = removal ?? (System.Windows.Media.Brush)FindResource("Px.Favorite"),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = removal != null ? new Thickness(0, -3, 3, 0) : new Thickness(0, -2, 0, 0),
                };
                if (removal == null)
                {
                    symbol.Margin = new Thickness(0, 7, 0, 0);
                }

                var grid = new Grid { Width = 32, Height = 32 };
                grid.Children.Add(symbol);
                grid.Children.Add(badge);
                content = grid;
            }

            if (removal == null)
            {
                return content;
            }

            // Moldura vermelha ocupando o quadrado do atalho — a diferença visível de longe.
            return new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(3),
                BorderThickness = new Thickness(1.5),
                BorderBrush = removal,
                Background = (System.Windows.Media.Brush)FindResource("Px.RemovalSurface"),
                Child = content,
            };
        }

        // Posições do painel (só os atalhos mostrados). Um favorito conhecido cujo botão não
        // existe neste painel não aparece: a posição é convertida para a da lista inteira.
        private bool OnFavoriteDropped(int from, int insertBefore)
        {
            if (from < 0 || from >= _visibleFavoriteIds.Count) return false;

            var id = _visibleFavoriteIds[from];
            var all = _favorites.Visible.Select(t => t.Id).ToList();
            var target = insertBefore < _visibleFavoriteIds.Count ? all.IndexOf(_visibleFavoriteIds[insertBefore]) : all.Count;
            var changed = false;
            ChangeFavorites(f => changed = f.MoveTo(id, target));
            return changed;
        }

        private ContextMenu CreateFavoriteShortcutMenu(PanelToolCatalog.Tool tool, bool isFirst, bool isLast)
        {
            var menu = new ContextMenu { Style = (Style)FindResource("Px.ContextMenu") };
            menu.Items.Add(FavoriteMenuItem(_favoritesShowLabels ? "Mover para cima" : "Mover para a esquerda", !isFirst, () => ChangeFavorites(f => f.MoveUp(tool.Id))));
            menu.Items.Add(FavoriteMenuItem(_favoritesShowLabels ? "Mover para baixo" : "Mover para a direita", !isLast, () => ChangeFavorites(f => f.MoveDown(tool.Id))));
            menu.Items.Add(FavoriteMenuItem($"Remover dos favoritos (continua em {tool.Section})", true, () => ChangeFavorites(f => f.Remove(tool.Id))));
            return menu;
        }

        private MenuItem FavoriteMenuItem(string header, bool enabled, Action onClick)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled, Style = (Style)FindResource("Px.MenuItem") };
            item.Click += (_, _) => onClick();
            return item;
        }

        // Importação na tela de Configurações (ou este próprio painel) gravou favoritos.
        private void OnStoredFavoritesChanged()
        {
            if (_savingFavorites) return;
            Dispatcher.BeginInvoke(new Action(ReloadFavorites));
        }

        private void AttachFavoritesChanged()
        {
            if (_favoritesChangedAttached) return;
            ListForgeIntegratedSettingsStore.PanelFavoritesChanged += OnStoredFavoritesChanged;
            _favoritesChangedAttached = true;
        }

        private void DetachFavoritesChanged()
        {
            ListForgeIntegratedSettingsStore.PanelFavoritesChanged -= OnStoredFavoritesChanged;
            _favoritesChangedAttached = false;
        }

        // ---- Layout das seções (ver PanelLayout/PanelSectionCatalog.cs) ----

        private PanelSectionLayout _sectionLayout = PanelSectionLayout.Default();
        private Dictionary<string, Expander> _sectionElements = new Dictionary<string, Expander>();

        // Personalização (PanelLayout/PanelCustomization): nomes, seções criadas, ferramentas
        // movidas. As seções criadas são Expanders montados aqui; as ferramentas movidas viram
        // atalhos completos no fim da seção de destino e o original fica oculto na de origem.
        private PanelCustomization _customization = PanelCustomization.Default();
        private readonly Dictionary<string, Expander> _customSectionElements = new Dictionary<string, Expander>();
        private readonly Dictionary<string, StackPanel> _movedToolPanels = new Dictionary<string, StackPanel>();
        private readonly List<Button> _hiddenByPlacement = new List<Button>();
        private readonly List<UIElement> _hiddenStructure = new List<UIElement>();
        private IReadOnlyDictionary<string, string> _sectionBadges = new Dictionary<string, string>();
        private bool _sectionLayoutChangedAttached;
        private bool _savingSectionLayout;

        private void InitializeSectionLayout()
        {
            _sectionElements = new Dictionary<string, Expander>
            {
                [PanelSectionCatalog.Favorites] = FavoritesSection,
                [PanelSectionCatalog.Grid] = GridSection,
                [PanelSectionCatalog.Assembly] = AssemblySection,
                [PanelSectionCatalog.Molds] = MoldsSection,
                [PanelSectionCatalog.Codes] = CodesSection,
                [PanelSectionCatalog.Layers] = LayersSection,
                [PanelSectionCatalog.Output] = OutputSection,
                [PanelSectionCatalog.ToolsPieceType] = PieceTypeSection,
                [PanelSectionCatalog.ToolsObjects] = ObjectsSection,
                [PanelSectionCatalog.ToolsMeasures] = MeasuresSection,
                [PanelSectionCatalog.Lists] = ListsSection,
                [PanelSectionCatalog.Production] = ProductionSection,
                [PanelSectionCatalog.Diagnostics] = DiagnosticsSection,
            };

            ReloadSectionLayout();
        }

        private void ReloadSectionLayout()
        {
            var loaded = ListForgeIntegratedSettingsStore.LoadPanelSectionLayout();
            _customization = ListForgeIntegratedSettingsStore.LoadPanelCustomization();
            _sectionLayout = new PanelSectionLayout(loaded.Entries, _customization.CustomSections.Select(s => s.Id));
            ApplySectionLayout();
            if (!string.IsNullOrEmpty(loaded.Notice))
            {
                SetStatus(loaded.Notice);
            }
        }

        /// <summary>
        /// Reordena os MESMOS Expanders em <c>SectionsHost</c> e oculta (Collapsed) os
        /// desmarcados — nada é recriado: campos preenchidos, seções abertas/fechadas, favoritos
        /// e os botões originais (que os atalhos acionam mesmo com a seção oculta) continuam
        /// como estavam. O status do último comando fica depois das seções; quando Diagnóstico é
        /// a última visível, logo antes dela — que é a posição do layout padrão.
        /// </summary>
        private void ApplySectionLayout()
        {
            ApplyCustomization();
            var elements = _sectionElements.Concat(_customSectionElements).ToDictionary(p => p.Key, p => p.Value);
            var ordered = _sectionLayout.Sections.Where(e => elements.ContainsKey(e.Id)).ToList();
            var desired = new List<UIElement>();
            foreach (var entry in ordered)
            {
                var section = elements[entry.Id];
                section.Visibility = entry.Visible && !IsToolFilterActive ? Visibility.Visible : Visibility.Collapsed;
                desired.Add(section);
            }

            var lastVisible = ordered.LastOrDefault(e => e.Visible);
            var statusIndex = lastVisible?.Id == PanelSectionCatalog.Diagnostics ? desired.IndexOf(DiagnosticsSection) : desired.Count;
            desired.Insert(statusIndex, StatusPanel);

            if (SectionsHost.Children.Cast<UIElement>().SequenceEqual(desired))
            {
                return;
            }

            SectionsHost.Children.Clear();
            foreach (var element in desired)
            {
                SectionsHost.Children.Add(element);
            }
        }

        /// <summary>Nomes das seções, seções criadas e ferramentas movidas — refeito por inteiro a
        /// cada mudança (só atalhos e Expanders criados aqui; os originais nunca são recriados).</summary>
        private void ApplyCustomization()
        {
            foreach (var element in _hiddenByPlacement.Cast<UIElement>().Concat(_hiddenStructure))
            {
                element.ClearValue(VisibilityProperty);
            }

            _hiddenByPlacement.Clear();
            _hiddenStructure.Clear();
            foreach (var panel in _movedToolPanels.Values)
            {
                (panel.Parent as Panel)?.Children.Remove(panel);
            }

            _movedToolPanels.Clear();

            // Seções criadas: Expander próprio (some quando a seção é removida).
            var customIds = _customization.CustomSections.Select(s => s.Id).ToList();
            foreach (var gone in _customSectionElements.Keys.Where(id => !customIds.Contains(id)).ToList())
            {
                SectionsHost.Children.Remove(_customSectionElements[gone]);
                _customSectionElements.Remove(gone);
            }

            foreach (var id in customIds.Where(id => !_customSectionElements.ContainsKey(id)))
            {
                _customSectionElements[id] = new Expander { IsExpanded = true };
            }

            // Conteúdo das seções criadas é só o que foi movido para elas: refeito do zero.
            foreach (var section in _customSectionElements.Values)
            {
                section.Content = new StackPanel();
            }

            var all = _sectionElements.Concat(_customSectionElements).ToList();
            foreach (var pair in all)
            {
                pair.Value.Header = _customization.TitleOf(pair.Key);
                var tools = _customization.MovedInto(pair.Key).Where(_favoriteSources.ContainsKey).ToList();
                if (tools.Count == 0 || pair.Value.Content is not Panel content) continue;

                var moved = new StackPanel();
                foreach (var toolId in tools)
                {
                    var original = _favoriteSources[toolId];
                    var tool = PanelToolCatalog.Find(toolId)!;
                    var shortcut = FavoriteShortcut.Create(original, tool.Label, new TextBlock { Text = tool.Label, TextWrapping = TextWrapping.Wrap }, original.Style);
                    AttachFavoriteMenu(toolId, shortcut);
                    moved.Children.Add(shortcut);
                    original.Visibility = Visibility.Collapsed;
                    _hiddenByPlacement.Add(original);
                }

                content.Children.Add(moved);
                _movedToolPanels[pair.Key] = moved;
            }

            foreach (var section in _sectionElements.Values)
            {
                if (section.Content is Panel panel) TidySectionStructure(panel);
            }

            foreach (var section in _customSectionElements.Values.Where(e => ((Panel)e.Content).Children.Count == 0))
            {
                ((Panel)section.Content).Children.Add(new TextBlock
                {
                    Text = "Seção vazia. Em \"Personalizar painel…\", escolha esta seção para as ferramentas que quer aqui.",
                    Style = (Style)FindResource("Px.MutedText"),
                });
            }

            _sectionBadges = _customization.SectionBadges(all.Select(p => p.Key));
            RebuildFavorites();
        }

        /// <summary>
        /// Depois de mover ferramentas para outras seções: um grupo (trecho entre linhas
        /// separadoras) cujos botões foram TODOS movidos perde também o subtítulo — senão sobrava
        /// "Tamanho Padrão" sozinho entre duas linhas, parecendo um botão desabilitado (relato do
        /// usuário, dev.76). Linhas separadoras repetidas, no começo ou no fim também somem.
        /// Campos, textos de estado e qualquer outro conteúdo mantêm o grupo.
        /// </summary>
        private void TidySectionStructure(Panel panel)
        {
            var children = panel.Children.Cast<UIElement>().ToList();
            var segment = new List<UIElement>();

            void CloseSegment()
            {
                var movedAway = 0;
                var keeps = false;
                foreach (var element in segment.Where(e => e is not TextBlock))
                {
                    var tools = element is Button b && !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(b))
                        ? new List<Button> { b }
                        : FindToolButtons(element).ToList();
                    if (tools.Count > 0 && tools.All(_hiddenByPlacement.Contains))
                    {
                        movedAway += tools.Count;
                    }
                    else
                    {
                        keeps = true;
                    }
                }

                if (movedAway > 0 && !keeps)
                {
                    foreach (var element in segment.Where(e => e.Visibility == Visibility.Visible))
                    {
                        element.Visibility = Visibility.Collapsed;
                        _hiddenStructure.Add(element);
                    }
                }

                segment.Clear();
            }

            foreach (var child in children)
            {
                if (child is Separator)
                {
                    CloseSegment();
                    continue;
                }

                segment.Add(child);
            }

            CloseSegment();

            // Linhas separadoras: nenhuma no começo, no fim ou duas seguidas.
            var lastVisibleWasSeparator = true;
            Separator? pending = null;
            foreach (var child in children.Where(c => c.Visibility == Visibility.Visible))
            {
                if (child is Separator separator)
                {
                    if (lastVisibleWasSeparator)
                    {
                        separator.Visibility = Visibility.Collapsed;
                        _hiddenStructure.Add(separator);
                    }
                    else
                    {
                        pending = separator;
                    }

                    lastVisibleWasSeparator = true;
                    continue;
                }

                lastVisibleWasSeparator = false;
                pending = null;
            }

            if (pending != null)
            {
                pending.Visibility = Visibility.Collapsed;
                _hiddenStructure.Add(pending);
            }
        }

        // ---- Buscar ferramenta (topo do painel) ----

        private bool IsToolFilterActive => ToolFilterBox.Text.Trim().Length > 0;

        private void OnToolFilterChanged(object sender, TextChangedEventArgs e) => ApplyToolFilter();

        private void OnClearToolFilter(object sender, RoutedEventArgs e)
        {
            ToolFilterBox.Text = string.Empty;
            ToolFilterBox.Focus();
        }

        private void OnToolFilterKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Escape || ToolFilterBox.Text.Length == 0) return;
            ToolFilterBox.Text = string.Empty;
            e.Handled = true;
        }

        /// <summary>Com texto: esconde as seções (sem mexer no layout salvo) e mostra os atalhos das
        /// ferramentas que combinam, por seção, na ordem do painel. Vazio: seções como o layout manda.</summary>
        private void ApplyToolFilter()
        {
            var query = ToolFilterBox.Text.Trim();
            ToolFilterHint.Visibility = ToolFilterBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            ToolFilterClearButton.Visibility = ToolFilterBox.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

            var elements = _sectionElements.Concat(_customSectionElements).ToDictionary(p => p.Key, p => p.Value);
            foreach (var entry in _sectionLayout.Sections.Where(e => elements.ContainsKey(e.Id)))
            {
                elements[entry.Id].Visibility = entry.Visible && query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            ToolFilterResults.Children.Clear();
            ToolFilterResults.Visibility = query.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (query.Length == 0) return;

            var sectionOrder = _sectionLayout.Sections.Select(s => s.Id).ToList();
            var catalogOrder = PanelToolCatalog.All.Select(t => t.Id).ToList();
            int Position(List<string> list, string id) => list.IndexOf(id) is var index && index >= 0 ? index : int.MaxValue;
            var candidates = _favoriteSources
                .Select(source =>
                {
                    var sectionId = _customization.SectionOf(source.Key);
                    return new PanelToolFilter.Candidate(source.Key, PanelToolCatalog.Find(source.Key)!.Label, sectionId,
                        _customization.TitleOf(sectionId), source.Value.ToolTip as string ?? string.Empty);
                })
                .OrderBy(c => Position(sectionOrder, c.SectionId))
                .ThenBy(c => Position(catalogOrder, c.Id));
            var matches = PanelToolFilter.Filter(candidates, query);

            var muted = (Style)FindResource("Px.MutedText");
            if (matches.Count == 0)
            {
                ToolFilterResults.Children.Add(new TextBlock { Text = $"Nenhuma ferramenta com “{query}”. ✕ ou Esc limpa a busca.", Style = muted, TextWrapping = TextWrapping.Wrap });
                return;
            }

            ToolFilterResults.Children.Add(new TextBlock
            {
                Text = $"{matches.Count} ferramenta(s) com “{query}”. Clique no nome da seção para abrir a seção inteira (campos como nome e tamanho ficam lá).",
                Style = muted,
                TextWrapping = TextWrapping.Wrap,
            });
            foreach (var group in matches.GroupBy(m => m.SectionId))
            {
                var sectionId = group.Key;
                var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(group.First().SectionTitle))
                {
                    ToolTip = "Limpa a busca e abre esta seção.",
                    Foreground = (System.Windows.Media.Brush)FindResource("Px.Accent"),
                };
                link.Click += (_, _) => GoToSection(sectionId);
                ToolFilterResults.Children.Add(new TextBlock(link) { Style = (Style)FindResource("Px.SectionLabel"), Margin = new Thickness(0, 8, 0, 2) });
                foreach (var match in group)
                {
                    var original = _favoriteSources[match.Id];
                    var shortcut = FavoriteShortcut.Create(original, match.Label, new TextBlock { Text = match.Label, TextWrapping = TextWrapping.Wrap }, original.Style);
                    AutomationProperties.SetAutomationId(shortcut, "filter." + match.Id);
                    ToolFilterResults.Children.Add(shortcut);
                }
            }
        }

        /// <summary>Do resultado da busca para a seção: limpa a busca, abre a seção e rola até ela.</summary>
        private void GoToSection(string sectionId)
        {
            ToolFilterBox.Text = string.Empty;
            var elements = _sectionElements.Concat(_customSectionElements).ToDictionary(p => p.Key, p => p.Value);
            if (!elements.TryGetValue(sectionId, out var section) || section.Visibility != Visibility.Visible)
            {
                SetStatus($"A seção \"{_customization.TitleOf(sectionId)}\" está oculta — mostre-a em \"Personalizar painel…\".");
                return;
            }

            section.IsExpanded = true;
            Dispatcher.BeginInvoke(new Action(section.BringIntoView), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>Para testes: digita na busca e devolve os Ids mostrados, na ordem.</summary>
        public IReadOnlyList<string> SearchToolsForTesting(string query)
        {
            ToolFilterBox.Text = query;
            return ToolFilterResults.Children.OfType<Button>()
                .Select(b => AutomationProperties.GetAutomationId(b).Substring("filter.".Length)).ToList();
        }

        /// <summary>Marca do canto do atalho: "−" para remoção; senão a da seção ONDE a
        /// ferramenta está agora, pelo nome atual.</summary>
        private string BadgeFor(PanelToolCatalog.Tool tool)
        {
            if (tool.Icon.IsRemoval) return "−";
            return _sectionBadges.TryGetValue(_customization.SectionOf(tool.Id), out var badge) ? badge : tool.Badge;
        }

        private void OnCustomizePanel(object sender, RoutedEventArgs e)
        {
            // Sem Owner, como OrderContextPathDialog: o painel é um docker nativo do Corel, não
            // uma Window WPF.
            var window = new PanelCustomizationWindow(_sectionLayout, _customization, SavePanelCustomization);
            if (window.ShowDialog() == true)
            {
                SetStatus("Layout do painel salvo.");
            }
        }

        // Chamado por "Salvar" da janela: grava primeiro e só então aplica — se a gravação
        // falhar, o painel continua como estava e a janela mostra o erro.
        private string? SavePanelCustomization(PanelSectionLayout draft, PanelCustomization customization)
        {
            try
            {
                _savingSectionLayout = true;
                ListForgeIntegratedSettingsStore.SavePanelCustomization(customization, draft.ToPersist());
            }
            catch (Exception ex)
            {
                return $"Não foi possível salvar o layout: {ex.Message} O painel não foi alterado.";
            }
            finally
            {
                _savingSectionLayout = false;
            }

            _customization = customization.Clone().Normalize();
            _sectionLayout = draft;
            ApplySectionLayout();
            return null;
        }

        // Importação de preferências na tela de Configurações gravou um layout.
        private void OnStoredSectionLayoutChanged()
        {
            if (_savingSectionLayout) return;
            Dispatcher.BeginInvoke(new Action(ReloadSectionLayout));
        }

        private void AttachSectionLayoutChanged()
        {
            if (_sectionLayoutChangedAttached) return;
            ListForgeIntegratedSettingsStore.PanelSectionLayoutChanged += OnStoredSectionLayoutChanged;
            _sectionLayoutChangedAttached = true;
        }

        private void DetachSectionLayoutChanged()
        {
            ListForgeIntegratedSettingsStore.PanelSectionLayoutChanged -= OnStoredSectionLayoutChanged;
            _sectionLayoutChangedAttached = false;
        }

        private void OnOpenUserManual(object sender, RoutedEventArgs e) => Manual.UserManualWindow.ShowSingle();

        private void OnReleaseNotes(object sender, RoutedEventArgs e)
        {
            // O docker do Corel é nativo, não uma Window WPF (mesmo padrão de Personalizar painel).
            new ReleaseNotes.ReleaseNotesWindow(ResolveAppVersion()).ShowDialog();
        }

        /// <summary>
        /// Lê a versão real embutida no assembly em tempo de build (ver
        /// PXToolBoxWriteVersionInfo em PXToolBox.CorelHost.Dev.csproj, que grava
        /// AssemblyInformationalVersion a partir de -p:PxToolBoxVersion, passado pelo
        /// build-release.ps1). Nunca cravada aqui — numa build local fora do script de
        /// release, o padrão "0.0.0-dev.local" deixa isso óbvio, em vez de mostrar um número de
        /// versão real que não corresponde ao que foi de fato compilado.
        /// </summary>
        private static string ResolveAppVersion() =>
            typeof(ControlUI).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
            ?? "0.0.0-dev.local";

        /// <summary>Construtor alternativo, caso o host "wpfhost" instancie passando a CorelApplication (ver CorelApplicationLocator).</summary>
        public ControlUI(object app) : this()
        {
            if (app is CorelApplication application)
            {
                SetApp(application);
            }
        }

        private void Connect() => SetApp(CorelApplicationLocator.TryGetRunningApplication());

        private bool _selectionChangeAttached;
        private bool _documentCloseAttached;

        /// <summary>Único ponto que troca <see cref="_app"/> — sempre desinscreve do app
        /// anterior antes de trocar a referência e reinscreve no novo, nunca deixando um
        /// handler "pendurado" numa instância COM antiga nem duplicando a subscription no
        /// mesmo app (ver AttachSelectionChange/DetachSelectionChange).</summary>
        private void SetApp(CorelApplication? newApp)
        {
            DetachSelectionChange();
            _app = newApp;
            AttachSelectionChange();
            UpdateConnectionStatus();
            UpdateStandardSizeButtonState();
            UpdateRestoreHiddenLayersButtonState();
        }

        /// <summary>DIVGApplicationEvents.SelectionChange (confirmado por reflexão sobre
        /// Corel.Interop.VGCore.dll) — evento sem parâmetros; quem recebe deve reconsultar a
        /// seleção atual. Idempotente: nunca duplica se já estiver inscrito.</summary>
        private void AttachSelectionChange()
        {
            if (_app == null || _selectionChangeAttached) return;
            try
            {
                _app.SelectionChange += OnCorelSelectionChange;
                _selectionChangeAttached = true;
                _app.DocumentClose += OnCorelDocumentClose;
                _documentCloseAttached = true;
            }
            catch
            {
                // Evento indisponível nesta instância/versão — Alternar/Atualizar continuam
                // funcionando normalmente (não dependem deste evento); só a atualização
                // automática do estado do botão fica sem esse gatilho.
            }
        }

        private void DetachSelectionChange()
        {
            if (_app != null && _selectionChangeAttached)
            {
                try { _app.SelectionChange -= OnCorelSelectionChange; }
                catch { /* app já encerrado/desconectado — nada a desinscrever de verdade */ }
            }
            if (_app != null && _documentCloseAttached)
            {
                try { _app.DocumentClose -= OnCorelDocumentClose; }
                catch { /* idem */ }
            }
            _selectionChangeAttached = false;
            _documentCloseAttached = false;
        }

        // Mesma thread STA do host (nenhum outro handler deste arquivo usa Dispatcher.Invoke
        // pra chamar COM — este painel já roda inteiro na thread do CorelDRAW).
        private void OnCorelSelectionChange() => UpdateStandardSizeButtonState();

        private void UpdateConnectionStatus()
        {
            if (_app == null)
            {
                DocStateText.Text = "Não conectado ao CorelDRAW.";
                DiagnosticsText.Text = "Não conectado ao CorelDRAW (ver CorelApplicationLocator).";
                return;
            }

            var document = _app.ActiveDocument;
            DocStateText.Text = document == null
                ? "Nenhum documento aberto."
                : "Documento aberto.";
            DiagnosticsText.Text = $"Conectado ao CorelDRAW ({_app.Version}).";
        }

        /// <summary>Reflete o estado real da seleção na aparência do botão Alternar Tamanho
        /// Padrão (Tag → Px.StateButton); a dica e o nome de acessibilidade dizem o estado em
        /// texto. Chamado no connect/reconnect, depois de cada operação (RunWithWorkspace) e a
        /// cada SelectionChange — nunca faz polling.</summary>
        private void UpdateStandardSizeButtonState()
        {
            StandardSizeSelectionState state;
            try
            {
                if (_app == null || _app.ActiveDocument == null)
                {
                    state = StandardSizeSelectionState.Unavailable;
                }
                else
                {
                    var selection = new CorelWorkspaceAdapter(_app).ActiveSelection;
                    state = _standardSize.InspectSelection(selection);
                }
            }
            catch
            {
                // Falha ao consultar app/documento/seleção — nunca apresentado como "inativo"
                // (fingiria desmarcado quando a resposta real é "não sei").
                state = StandardSizeSelectionState.Unknown;
            }

            ShowSelectionState(ToggleStandardSizeButton, state, "Tamanho Padrão",
                "Adiciona ou remove a propriedade Tamanho Padrão dos objetos selecionados.");
            UpdateSisboltMarkingButtonStates();
        }

        /// <summary>Mesmo tratamento do Tamanho Padrão para as marcações do SISBolt (Esticar,
        /// Ignorar métricas, Mão Livre) — chamado junto com ele (seleção, conexão, depois de cada
        /// operação). Atalhos dos Favoritos seguem o Tag do botão original.</summary>
        private void UpdateSisboltMarkingButtonStates()
        {
            ICorelSelection? selection = null;
            var failed = false;
            try
            {
                if (_app != null && _app.ActiveDocument != null)
                {
                    selection = new CorelWorkspaceAdapter(_app).ActiveSelection;
                }
            }
            catch
            {
                failed = true;
            }

            StandardSizeSelectionState StateOf(SisboltMarking marking)
            {
                if (failed) return StandardSizeSelectionState.Unknown;
                try { return _sisboltMarking.InspectSelection(selection, marking); }
                catch { return StandardSizeSelectionState.Unknown; }
            }

            ShowSelectionState(StretchToMoldButton, StateOf(SisboltMarking.StretchToMold), "Esticar no Tamanho do Molde",
                "Liga ou desliga a marcação STRETCH_PROPS nos objetos selecionados — o objeto é esticado no tamanho do molde na montagem; o tamanho não muda agora. Desligar remove a marcação. Preserva Tamanho Padrão e as demais propriedades.");
            ShowSelectionState(IgnoreVariableMetricsButton, StateOf(SisboltMarking.IgnoreVariableMetrics), "Ignorar métricas da variável",
                "Liga ou desliga a marcação que ignora as métricas da variável nos textos de variável selecionados (ex.: NO). Desligar remove a marcação. Não muda texto, nome nem geometria.");
            ShowSelectionState(FreeHandButton, StateOf(SisboltMarking.FreeHand), "Mão Livre",
                "Liga ou desliga a marcação FREE_HAND_PROPS nos textos selecionados com o nome NO, NU, AP ou TS (só eles podem ser ligados). Desligar remove a marcação. Não muda texto, nome nem geometria.");
        }

        /// <summary>Tag → Px.StateButton (ativo = cor de acento; misto = só a borda); dica e
        /// nome de acessibilidade dizem o estado em texto.</summary>
        private static void ShowSelectionState(Button button, StandardSizeSelectionState state, string label, string description)
        {
            button.Tag = state switch
            {
                StandardSizeSelectionState.AllMarked => "Active",
                StandardSizeSelectionState.Mixed => "Mixed",
                _ => null,
            };
            var stateText = state switch
            {
                StandardSizeSelectionState.Unavailable => "indisponível (sem documento ou seleção)",
                StandardSizeSelectionState.AllMarked => "ATIVO nesta seleção",
                StandardSizeSelectionState.NoneMarked => "inativo nesta seleção",
                StandardSizeSelectionState.Mixed => "MISTO nesta seleção (só parte dos objetos)",
                StandardSizeSelectionState.Unknown => "estado desconhecido (falha ao ler)",
                _ => "",
            };
            button.ToolTip = $"{label}: {stateText}. {description}";
            System.Windows.Automation.AutomationProperties.SetHelpText(button, $"{label}: {stateText}");
        }

        private void UpdateOutputFolderPreview()
        {
            var machineDefault = OutputFolderConfigStore.ReadConfiguredPath();
            var effective = EffectiveOutputFolder();
            var origin = !string.IsNullOrWhiteSpace(OutputFolderTextBox.Text) ? "digitada acima"
                : machineDefault != null ? "padrão desta máquina"
                : "padrão original";
            if (RollSubfolder() != null)
            {
                origin += " + subpasta do rolo";
            }
            OutputFolderPreviewText.Text = $"Destino: {effective} ({origin})";
            OutputFolderPreviewText.ToolTip = effective;
            ResetDefaultOutputFolderButton.IsEnabled = machineDefault != null;
            ResetDefaultOutputFolderButton.ToolTip = machineDefault != null
                ? $"Apaga a pasta padrão desta máquina ({machineDefault}) e volta a usar {OutputPreparationService.DefaultOutputFolder}."
                : $"Nenhuma pasta padrão definida nesta máquina — já vale {OutputPreparationService.DefaultOutputFolder}.";
        }

        // Pasta padrão desta máquina (OutputFolderConfigStore) — vale quando o campo "Pasta de
        // saída" fica vazio. Sem Owner: o painel é um docker nativo, não uma Window WPF.
        private void OnSetDefaultOutputFolder(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Pasta padrão de saída nesta máquina (Escudo, Bandeira, Faixa de Capitão)",
                SelectedPath = OutputPreparationService.ResolveOutputFolder(null, OutputFolderConfigStore.ReadConfiguredPath()),
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                SetStatus("Pasta padrão de saída: seleção cancelada — nada foi alterado.");
                return;
            }

            try
            {
                OutputFolderConfigStore.SaveConfiguredPath(dialog.SelectedPath);
                SetStatus($"Pasta padrão de saída nesta máquina: {dialog.SelectedPath}");
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                SetStatus($"Pasta padrão de saída: não foi possível salvar — {ex.Message}");
            }

            UpdateOutputFolderPreview();
        }

        private void OnResetDefaultOutputFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                OutputFolderConfigStore.ClearConfiguredPath();
                SetStatus($"Pasta padrão de saída: voltou ao padrão original ({OutputPreparationService.DefaultOutputFolder}).");
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                SetStatus($"Pasta padrão de saída: não foi possível apagar a configuração — {ex.Message}");
            }

            UpdateOutputFolderPreview();
        }

        private void OnToggleNo(object sender, RoutedEventArgs e) => RunOnSelection("Alternar NO", ws => _codeToggle.Toggle(ws.ActiveSelection, ObjectCode.No));
        private void OnToggleNu(object sender, RoutedEventArgs e) => RunOnSelection("Alternar NU", ws => _codeToggle.Toggle(ws.ActiveSelection, ObjectCode.Nu));
        private void OnToggleAp(object sender, RoutedEventArgs e) => RunOnSelection("Alternar AP", ws => _codeToggle.Toggle(ws.ActiveSelection, ObjectCode.Ap));
        private void OnToggleTs(object sender, RoutedEventArgs e) => RunOnSelection("Alternar TS", ws => _codeToggle.Toggle(ws.ActiveSelection, ObjectCode.Ts));
        private void OnExpandNoNu(object sender, RoutedEventArgs e) => RunOnActivePage("Remover NO/NU da página", page => _codeToggle.ExpandNoAndNuCodesRecursively(page));
        private void OnRemoveNoFromPage(object sender, RoutedEventArgs e) => RunOnActivePage("Remover NO da página", page => _codeToggle.RemoveCodesRecursively(page, ObjectCode.No));
        private void OnRemoveNuFromPage(object sender, RoutedEventArgs e) => RunOnActivePage("Remover NU da página", page => _codeToggle.RemoveCodesRecursively(page, ObjectCode.Nu));
        private void OnRemoveApFromPage(object sender, RoutedEventArgs e) => RunOnActivePage("Remover AP da página", page => _codeToggle.RemoveCodesRecursively(page, ObjectCode.Ap));
        private void OnRemoveTsFromPage(object sender, RoutedEventArgs e) => RunOnActivePage("Remover TS da página", page => _codeToggle.RemoveCodesRecursively(page, ObjectCode.Ts));
        private void OnRemoveAllCodesFromPage(object sender, RoutedEventArgs e) =>
            RunOnActivePage("Remover todos os códigos da página", page => _codeToggle.RemoveCodesRecursively(page, ObjectCode.No, ObjectCode.Nu, ObjectCode.Ap, ObjectCode.Ts));

        private void OnToggleMa(object sender, RoutedEventArgs e) => RunOnActivePage("Visibilidade MA", page => Describe(_layerVisibility.Toggle(page, LayerCategory.MA)));
        private void OnToggleFe(object sender, RoutedEventArgs e) => RunOnActivePage("Visibilidade FE", page => Describe(_layerVisibility.Toggle(page, LayerCategory.FE)));
        private void OnToggleC(object sender, RoutedEventArgs e) => RunOnActivePage("Visibilidade C", page => Describe(_layerVisibility.Toggle(page, LayerCategory.C)));
        private void OnHideSelectedObjectsLayers(object sender, RoutedEventArgs e) =>
            RunWithWorkspace("Ocultar camadas dos selecionados", (ws, doc) => _layerVisibility.HideLayersOfSelection(doc, ws.ActiveSelection, ws.OpenDocuments));

        // Exige o documento de origem ativo (ver LayerVisibilityService.RestoreLastHiddenLayers),
        // então o passo de desfazer de RunWithWorkspace fica no documento certo.
        private void OnRestoreLastHiddenLayers(object sender, RoutedEventArgs e) =>
            RunWithWorkspace("Mostrar últimas camadas ocultadas", (ws, doc) => _layerVisibility.RestoreLastHiddenLayers(doc, ws.OpenDocuments));

        /// <summary>Habilitado só com registro em memória; a dica explica o motivo quando não
        /// há. Antes, descarta o registro se o documento de origem não está mais aberto (só lê
        /// Application.Documents); camadas são verificadas no clique.</summary>
        private void UpdateRestoreHiddenLayersButtonState()
        {
            try
            {
                if (_app != null && _layerVisibility.LastHidden != null)
                {
                    _layerVisibility.DiscardIfSourceClosed(new CorelWorkspaceAdapter(_app).OpenDocuments);
                }
            }
            catch
            {
                // Falha ao ler os documentos abertos — o clique refaz a verificação.
            }

            var record = _layerVisibility.LastHidden;
            RestoreHiddenLayersButton.IsEnabled = record != null;
            RestoreHiddenLayersButton.ToolTip = record == null
                ? _layerVisibility.UnavailableReason
                  ?? "Indisponível: nenhuma camada foi ocultada por \"Ocultar camadas dos objetos selecionados\" nesta sessão."
                : $"Mostra de novo só as {record.Entries.Count} camada(s) ocultada(s) pela última operação ({string.Join(", ", record.Entries.Select(x => x.LayerName))}) no documento \"{record.DocumentName}\", que precisa estar ativo. Não mostra outras camadas. Registro só nesta sessão — fechar o documento o descarta. Desfazível com Ctrl+Z.";
        }

        // Painel fechado/descarregado: sem DocumentClose inscrito, o registro não seria mais
        // mantido em dia — solta as referências de sessão (sem ReleaseComObject; ver
        // LayerVisibilityService.ReleaseSessionState).
        private void OnPanelUnloaded()
        {
            DetachSelectionChange();
            if (_layerVisibility.LastHidden != null)
            {
                _layerVisibility.ReleaseSessionState("Indisponível: o painel foi fechado depois da última ocultação — registro descartado.");
            }
            UpdateRestoreHiddenLayersButtonState();
        }

        // DIVGApplicationEvents.DocumentClose(Document Doc), confirmado por reflexão. Só
        // compara a identidade do documento que está fechando com a do registro.
        private void OnCorelDocumentClose(CorelDocument doc)
        {
            _layerVisibility.OnDocumentClosed(doc);
            UpdateRestoreHiddenLayersButtonState();
        }

        private void OnPrepareGrid(object sender, RoutedEventArgs e) => RunOnDocument("Preparar Grid", doc => _grid.PrepareGrid(doc));
        // Shape.Width/Height retornam sempre em polegadas (ver StandardSizeService) — gravar
        // sem converter era o primeiro bug confirmado por evidência real. Duas tentativas de
        // escolher UMA propriedade de unidade pra converter (Document.Unit, depois
        // Application.Unit) foram cada uma refutada por um teste real do usuário. Corrigido
        // usando doc.ConvertFromInches (Document.ToUnits nativo do Corel) — deixa o próprio
        // Corel decidir a unidade "atual", em vez de PX adivinhar qual propriedade ler.
        private void OnToggleStandardSize(object sender, RoutedEventArgs e) =>
            RunWithWorkspace("Alternar Tamanho Padrão", (ws, doc) => _standardSize.Toggle(ws.ActiveSelection, doc.ConvertFromInches));
        private void OnUpdateStandardSize(object sender, RoutedEventArgs e) =>
            RunWithWorkspace("Atualizar Tamanho Padrão", (ws, doc) => _standardSize.UpdateSelection(ws.ActiveSelection, doc.ConvertFromInches));
        private void OnPrepareNewGrid(object sender, RoutedEventArgs e) => RunOnDocument("Preparar Grid Novo", doc => _grid.PrepareNewGrid(doc));
        private void OnApplyGridOutline(object sender, RoutedEventArgs e) => RunOnDocument("Aplicar contorno do Grid", doc => _grid.ApplyGridOutline(doc));
        private void OnPrepareNewMold(object sender, RoutedEventArgs e) => RunOnActivePage("Preparar Molde Novo", page => _mold.PrepareNewMold(page));
        private void OnPrepareNewGridNewStandard(object sender, RoutedEventArgs e) =>
            RunOnDocument("Preparar Grid Novo (padrão novo)", doc => _grid.PrepareNewGrid(doc, LayerStandardService.Standard.New));
        private void OnPrepareNewMoldNewStandard(object sender, RoutedEventArgs e) =>
            RunOnActivePage("Preparar Molde Novo (padrão novo)", page => _mold.PrepareNewMold(page, LayerStandardService.Standard.New));
        private void OnConvertLayerStandard(object sender, RoutedEventArgs e) =>
            RunOnDocument("Converter documento", doc => _layerStandard.ConvertDocument(doc));

        // ---- Cadastro de moldes (MoldToolsService) ----

        private void OnMoldSettings(object sender, RoutedEventArgs e)
        {
            var window = new Molds.MoldSettingsWindow();
            SetStatus(window.ShowDialog() == true
                ? "Configurações de moldes salvas."
                : "Configurações de moldes: nada foi alterado.");
        }

        /// <summary>Tamanhos das configurações de moldes, mantendo o escolhido quando ele continua existindo.</summary>
        private void RefreshMoldSizes()
        {
            var current = MoldSizeCombo.SelectedItem as string;
            var sizes = MoldSettingsStore.Load().Settings.AllSizes;
            MoldSizeCombo.ItemsSource = sizes;
            var index = current == null ? -1 : sizes.ToList().FindIndex(s => string.Equals(s, current, StringComparison.OrdinalIgnoreCase));
            MoldSizeCombo.SelectedIndex = index >= 0 ? index : sizes.ToList().FindIndex(s => s == "G");

            var category = MoldCategoryCombo.SelectedIndex;
            var settings = MoldSettingsStore.Load().Settings;
            MoldCategoryCombo.ItemsSource = new[] { ("Masculino", settings.Male), ("Feminino", settings.Female), ("Infantil", settings.Kids) }
                .Select(c => $"{c.Item1} ({c.Item2.Code}): {string.Join(", ", c.Item2.Sizes)}").ToList();
            MoldCategoryCombo.SelectedIndex = category >= 0 ? category : 0;
        }

        private void OnMoldDefineBatch(object sender, RoutedEventArgs e)
        {
            var pieceName = MoldPieceNameTextBox.Text;
            var settings = MoldSettingsStore.Load().Settings;
            var category = settings.Categories[Math.Max(0, MoldCategoryCombo.SelectedIndex)].Code;
            RunWithWorkspace("Detectar objetos em lote", (ws, doc) => _moldDefine.DefineBatch(ws.ActiveSelection, doc.ActivePage, (CorelWorkspaceAdapter)ws, pieceName, category));
        }

        /// <summary>Distância digitada em mm, convertida para a unidade do documento.</summary>
        private void OnMoldDuplicate(object sender, RoutedEventArgs e)
        {
            const string command = "Duplicar peça";
            var text = MoldCopyGapTextBox.Text.Trim().Replace(',', '.');
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var gapMm) || gapMm < 0)
            {
                SetStatus($"{command}: distância inválida — use um número em mm (0 = encostadas).");
                return;
            }

            var copyName = MoldCopyNameTextBox.Text;
            var direction = (MoldDuplicationService.CopyDirection)Math.Max(0, MoldCopyDirectionCombo.SelectedIndex);
            var mirror = MoldCopyMirrorCheckBox.IsChecked == true;
            RunWithWorkspace(command, (ws, doc) =>
            {
                var gap = _app!.ConvertUnits(gapMm, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, _app.ActiveDocument.Unit);
                return _moldDuplication.Duplicate(ws.ActiveSelection, doc.ActivePage, (CorelWorkspaceAdapter)ws, copyName, direction, gap, mirror);
            });
        }

        private void OnMoldPropagateLogos(object sender, RoutedEventArgs e)
        {
            var reference = MoldSizeCombo.SelectedItem as string ?? string.Empty;
            RunWithWorkspace("Propagar logos", (ws, doc) => _moldLogos.Propagate(ws.ActiveSelection, (CorelWorkspaceAdapter)ws, reference));
        }

        private void OnMoldValidate(object sender, RoutedEventArgs e) => ValidateMold();

        /// <summary>Só leitura: o resumo vai para o status e o relatório para a janela (que chama de
        /// novo por "Validar de novo").</summary>
        private void ValidateMold()
        {
            MoldValidationService.Report? report = null;
            RunWithWorkspace("Validar molde", (_, doc) =>
            {
                report = _moldValidation.Validate(doc.ActivePage);
                return OperationResult.Success(report.Summary);
            });
            if (report != null) Molds.MoldValidationWindow.ShowReport(report, ValidateMold);
        }

        private void OnMoldDefine(object sender, RoutedEventArgs e)
        {
            var pieceName = MoldPieceNameTextBox.Text;
            var size = MoldSizeCombo.SelectedItem as string ?? string.Empty;
            RunWithWorkspace("Definir objetos", (ws, doc) => _moldDefine.Define(ws.ActiveSelection, doc.ActivePage, (CorelWorkspaceAdapter)ws, pieceName, size));
        }

        private void OnMoldCreateLayers(object sender, RoutedEventArgs e)
        {
            var pieceType = PieceTypeCode.All[Math.Max(0, MoldPieceTypeCombo.SelectedIndex)];
            RunWithWorkspace("Criar camadas para a seleção", (ws, doc) => _moldTools.CreateLayersForSelection(ws.ActiveSelection, doc.ActivePage, pieceType));
        }

        private void OnMoldUppercase(object sender, RoutedEventArgs e) =>
            RunOnSelection("Converter peça e camada para maiúsculas", ws => _moldTools.UppercasePieceNames(ws.ActiveSelection));

        private void OnMoldDeleteEmptyLayers(object sender, RoutedEventArgs e) =>
            RunOnActivePage("Excluir camadas vazias", page => _moldTools.DeleteEmptyLayers(page));

        /// <summary>Distanciamento digitado em mm, convertido para a unidade do documento (a das
        /// medidas de Shape) — nunca comparado direto em mm.</summary>
        private void OnMoldDistribute(object sender, RoutedEventArgs e)
        {
            const string command = "Distribuir";
            var text = MoldGapTextBox.Text.Trim().Replace(',', '.');
            double? gapMm = null;
            if (text.Length > 0)
            {
                if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
                {
                    SetStatus($"{command}: distanciamento inválido — use um número em mm, ou deixe em branco.");
                    return;
                }

                gapMm = parsed;
            }

            var order = MoldSortCombo.SelectedIndex == 1 ? MoldToolsService.SortOrder.SmallestFirst : MoldToolsService.SortOrder.LargestFirst;
            var vertical = MoldDirectionCombo.SelectedIndex != 1;
            RunWithWorkspace(command, (ws, _) =>
            {
                double? gap = gapMm.HasValue
                    ? _app!.ConvertUnits(gapMm.Value, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, _app.ActiveDocument.Unit)
                    : (double?)null;
                return _moldTools.Distribute(ws.ActiveSelection, order, vertical, gap);
            });
        }

        private void OnMoldSelectRect(object sender, RoutedEventArgs e) =>
            RunWithWorkspace("Selecionar RECT", (ws, doc) => _moldTools.SelectRectangles(doc.ActivePage, (CorelWorkspaceAdapter)ws));

        /// <summary>Pergunta onde salvar (sugestão: nome do documento + "_GRID") e abre o Grid salvo
        /// só depois de terminar tudo no documento de moldes.</summary>
        private void OnMoldCreateGrid(object sender, RoutedEventArgs e)
        {
            const string command = "Criar Grid";
            if (_app == null) Connect();
            var document = _app?.ActiveDocument;
            if (document == null)
            {
                SetStatus($"{command}: {(_app == null ? "não conectado ao CorelDRAW" : "nenhum documento aberto")}.");
                return;
            }

            var baseName = System.IO.Path.GetFileNameWithoutExtension(document.FullFileName ?? string.Empty);
            if (string.IsNullOrEmpty(baseName)) baseName = System.IO.Path.GetFileNameWithoutExtension(document.Name ?? string.Empty);
            var suggestion = baseName.EndsWith("_GRID", StringComparison.OrdinalIgnoreCase) ? baseName : baseName + "_GRID";
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Salvar o Grid",
                Filter = "CorelDRAW (*.cdr)|*.cdr",
                FileName = suggestion + ".cdr",
                InitialDirectory = string.IsNullOrEmpty(document.FullFileName) ? string.Empty : System.IO.Path.GetDirectoryName(document.FullFileName),
            };
            if (dialog.ShowDialog() != true)
            {
                SetStatus($"{command}: cancelado — nada foi feito.");
                return;
            }

            string? saved = null;
            RunWithWorkspace(command, (ws, doc) => _moldTools.CreateGrid(doc, (CorelWorkspaceAdapter)ws, dialog.FileName, out saved));
            if (saved == null) return;
            try
            {
                _app!.OpenDocument(saved, 0);
            }
            catch (Exception ex)
            {
                SetStatus($"{command}: Grid salvo em {saved}, mas não foi possível abri-lo — {ex.Message}");
            }
        }

        /// <summary>Nome digitado → arquivo .cdr sugerido; o usuário confirma onde e o documento inteiro é
        /// salvo lá (SaveAs: o documento aberto passa a ser esse arquivo).</summary>
        private void OnMoldSave(object sender, RoutedEventArgs e)
        {
            const string command = "Salvar molde";
            if (!MoldSaveService.TryCreateFileName(MoldSaveNameTextBox.Text, out var fileName, out var error))
            {
                SetStatus($"{command}: {error}");
                return;
            }

            if (_app == null) Connect();
            var document = _app?.ActiveDocument;
            if (document == null)
            {
                SetStatus($"{command}: {(_app == null ? "não conectado ao CorelDRAW" : "nenhum documento aberto")}.");
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Salvar o molde",
                Filter = "CorelDRAW (*.cdr)|*.cdr",
                DefaultExt = ".cdr",
                AddExtension = true,
                FileName = fileName,
                InitialDirectory = MoldSaveService.InitialFolder(document.FullFileName, System.IO.Directory.Exists),
            };
            if (dialog.ShowDialog() != true)
            {
                SetStatus($"{command}: cancelado — nada foi salvo.");
                return;
            }

            var path = dialog.FileName;
            RunWithWorkspace(command, (ws, doc) => _moldSave.Save(doc, path));
        }

        private void OnPrepareShield(object sender, RoutedEventArgs e) => PrepareAndSaveOutput(OutputPreparationService.ShieldName);
        private void OnPrepareFlag(object sender, RoutedEventArgs e) => PrepareAndSaveOutput(OutputPreparationService.FlagName);
        private void OnPrepareCaptainSash(object sender, RoutedEventArgs e) => PrepareAndSaveOutput(OutputPreparationService.CaptainSashName);

        private void PrepareAndSaveOutput(string outputName)
        {
            var layoutName = string.IsNullOrWhiteSpace(LayoutNameTextBox.Text) ? null : LayoutNameTextBox.Text;
            var outputFolder = EffectiveOutputFolder();
            var context = _loadedOrderContext;
            var usesRollSubfolder = RollSubfolder() != null;

            RunOnDocument(outputName, doc =>
            {
                var prepareResult = _output.PrepareForOutput(doc, outputName);
                if (!prepareResult.IsSuccess)
                {
                    return prepareResult;
                }

                // A subpasta do rolo é nova a cada rolo: criada aqui, antes de salvar. A pasta de
                // saída em si continua como antes (não é criada pelo PX).
                if (usesRollSubfolder)
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(outputFolder);
                    }
                    catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                    {
                        return OperationResult.Fail(OperationStatus.ExecutionFailed,
                            $"não foi possível criar a subpasta do rolo \"{outputFolder}\" — {ex.Message} Documento preparado, mas não salvo.");
                    }
                }

                var saveResult = _output.SaveOutput(doc, outputName, layoutName, outputFolder);
                if (!saveResult.IsSuccess || context == null)
                {
                    return saveResult;
                }

                // Registrado para "Exportar resultado para o Nexor…" — só em memória; nada é
                // enviado ao Nexor aqui.
                _executionResult.Record(new Integrations.ExecutionResultService.OutputRecord(
                    Integrations.ExecutionResultService.KindFor(outputName), outputName, layoutName,
                    _output.BuildOutputFilePath(outputFolder, outputName, layoutName), DateTimeOffset.UtcNow));
                UpdateOrderContextSummary();
                var note = Production.OrderContextSummary.OutputNote(context);
                Integrations.PxLogger.Info("OutputSave", $"{outputName}: {saveResult.Message} {note}.");
                return OperationResult.Success($"{saveResult.Message} {note}.");
            });
        }

        /// <summary>Subpasta do rolo, se houver contexto carregado e a opção estiver marcada.</summary>
        private string? RollSubfolder() =>
            _loadedOrderContext != null && UseRollSubfolderCheckBox.IsChecked == true
                ? Production.OrderContextSummary.RollFolderName(_loadedOrderContext)
                : null;

        private string EffectiveOutputFolder() =>
            OutputPreparationService.WithSubfolder(
                OutputPreparationService.ResolveOutputFolder(OutputFolderTextBox.Text, OutputFolderConfigStore.ReadConfiguredPath()),
                RollSubfolder());

        private void OnChangeSelectedObjectsType(object sender, RoutedEventArgs e)
        {
            if (!TryGetToCode(out var to))
            {
                return;
            }

            RunOnSelection("Alterar tipo dos objetos selecionados", ws => _pieceTypeRename.RenameSelectedObjects(ws.ActiveSelection, to));
        }

        private void OnChangeActiveLayerType(object sender, RoutedEventArgs e)
        {
            if (!TryGetToCode(out var to))
            {
                return;
            }

            RunOnDocument("Alterar tipo da camada ativa", doc => _pieceTypeRename.RenameActiveLayer(doc.ActiveLayer, to));
        }

        // Função original (Correcoes.RenomeiaTipoDePeca) restaurada: antes da versão atual só
        // ter "objetos selecionados"/"camada ativa", existia também a opção de buscar o
        // documento inteiro (todas as páginas/camadas) pelo tipo de origem, como "CAM1 → CAM2".
        // O serviço (PieceTypeRenameService.Rename) nunca foi removido, só não estava ligado a
        // nenhum botão — ver docs/migrated-tools.md.
        private const string AnyTypeLabel = "Tudo";

        private void OnChangeDocumentType(object sender, RoutedEventArgs e)
        {
            if (!TryGetToCode(out var to))
            {
                return;
            }

            var fromLabel = FromTypeCombo.SelectedItem as string;
            PieceTypeCode? from = null;
            if (fromLabel != null && fromLabel != AnyTypeLabel)
            {
                if (!PieceTypeCode.TryParse(fromLabel, out var parsedFrom) || parsedFrom == null)
                {
                    SetStatus("Selecione o tipo de origem.");
                    return;
                }
                from = parsedFrom;
            }

            RunOnDocument("Alterar tipo no documento inteiro", doc =>
            {
                var result = _pieceTypeRename.Rename(doc, from, to);
                return result.IsSuccess
                    ? OperationResult.Success(result.Message)
                    : OperationResult.Fail(result.Status, result.Message);
            });
        }

        private bool TryGetToCode(out PieceTypeCode to)
        {
            var toCode = ToTypeCombo.SelectedItem as string;
            if (toCode == null || !PieceTypeCode.TryParse(toCode, out var parsed) || parsed == null)
            {
                SetStatus("Selecione o novo tipo de peça.");
                to = null!;
                return false;
            }

            to = parsed;
            return true;
        }

        private void OnRenameSelected(object sender, RoutedEventArgs e)
        {
            var newName = RenameTextBox.Text;
            RunOnSelection("Renomear Objetos Selecionados", ws => _renameSelected.Rename(ws.ActiveSelection, newName));
        }

        private void OnSumHeight(object sender, RoutedEventArgs e) => RunSum("Somar Altura", (sizes, unit) => _measurementSum.SumSelected(sizes, unit, MeasurementSumMode.Height));
        private void OnSumWidth(object sender, RoutedEventArgs e) => RunSum("Somar Largura", (sizes, unit) => _measurementSum.SumSelected(sizes, unit, MeasurementSumMode.Width));
        private void OnSumBoth(object sender, RoutedEventArgs e) => RunSum("Somar Altura e Largura", (sizes, unit) => _measurementSum.SumSelected(sizes, unit, MeasurementSumMode.Both));

        private void OnRulerWidth(object sender, RoutedEventArgs e) => CreateRuler("Régua da largura", TapeMeasureService.Axis.Width);

        private void OnRulerHeight(object sender, RoutedEventArgs e) => CreateRuler("Régua da altura", TapeMeasureService.Axis.Height);

        private void OnRulerBoth(object sender, RoutedEventArgs e) => CreateRuler("Régua da largura e da altura", TapeMeasureService.Axis.Both);

        /// <summary>Distância digitada em mm; o serviço trabalha em mm e recebe quanto vale 1 mm na
        /// unidade do documento.</summary>
        private void CreateRuler(string command, TapeMeasureService.Axis axis)
        {
            var text = RulerGapTextBox.Text.Trim().Replace(',', '.');
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var gapMm) || gapMm < 0)
            {
                SetStatus($"{command}: distância inválida — use um número em mm (0 = encostada).");
                return;
            }

            RunWithWorkspace(command, (ws, doc) =>
            {
                var millimeter = _app!.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, _app.ActiveDocument.Unit);
                return _tapeMeasure.Create(ws.ActiveSelection, doc.ActivePage, millimeter, axis, gapMm);
            });
        }

        // Inspecionar Shape.Properties — ferramenta de diagnóstico exclusiva da distribuição DEV
        // (ver docs/listforge-classic-parity.md não se aplica; ver docs/ARCHITECTURE.md). Não usa
        // RunWithWorkspace (que agrupa a operação como um passo de desfazer via
        // BeginCommandGroup/EndCommandGroup) — esta ferramenta é 100% somente leitura, não há
        // nada para desfazer, e BeginCommandGroup criaria um passo de undo vazio sem sentido.
        private void OnInspectShapeProperties(object sender, RoutedEventArgs e)
        {
            if (_app == null)
            {
                Connect();
            }

            // Sem Owner: ControlUI é hospedado como docker nativo pelo Corel, não dentro de uma
            // Window WPF própria — mesmo padrão já usado por OnOpenListForgeIntegrated (também
            // sem Owner).
            var window = new Diagnostics.ShapeInspectorWindow(_app);
            window.Show();
        }

        // Comparação antes/depois (Diagnostics/PageCapture*.cs) — como o inspetor, só leitura e
        // fora de RunWithWorkspace (nada para desfazer). A captura "antes" fica só em memória.
        private Diagnostics.PageCapture? _pageCaptureBefore;

        // Roteiro das capturas pendentes (Diagnostics/CaptureChecklist.cs) — só texto, uma janela.
        private void OnOpenCaptureChecklist(object sender, RoutedEventArgs e) => Diagnostics.CaptureChecklistWindow.ShowSingle();

        private void OnCapturePageBefore(object sender, RoutedEventArgs e)
        {
            if (_app == null) Connect();

            var capture = Diagnostics.PageCaptureService.Capture(_app, out var failure);
            if (capture == null)
            {
                SetStatus($"Capturar estado: {failure}");
                return;
            }

            _pageCaptureBefore = capture;
            ComparePageCaptureButton.IsEnabled = true;
            PageCaptureStatusText.Text = $"Antes: {capture.Shapes.Count} objeto(s), página {capture.PageIndex} de \"{capture.DocumentName}\" ({capture.CapturedAt.ToLocalTime():HH:mm:ss})."
                + (capture.Truncated ? " Página grande: só os primeiros objetos." : "");
            SetStatus("Estado capturado. Agora use a ferramenta que quer investigar e clique em \"Comparar com a captura (depois)\".");
        }

        private void OnComparePageCapture(object sender, RoutedEventArgs e)
        {
            if (_pageCaptureBefore == null)
            {
                SetStatus("Comparar: clique antes em \"Capturar estado (antes)\".");
                return;
            }

            if (_app == null) Connect();
            var after = Diagnostics.PageCaptureService.Capture(_app, out var failure);
            if (after == null)
            {
                SetStatus($"Comparar com a captura: {failure}");
                return;
            }

            // Sem Owner, como o inspetor: o painel é um docker nativo do Corel.
            new Diagnostics.PageCaptureReportWindow(_pageCaptureBefore, after).Show();
            SetStatus("Comparação aberta. A captura \"antes\" continua guardada — capture de novo antes de testar outra ferramenta.");
        }

        // Investigação do sintoma "painel para de redesenhar" (ver AGENTS.md/ROADMAP.md,
        // branch fix/corel-panel-rendering, e o comentário completo em
        // Diagnostics/PanelRenderDiagnostics.cs) — somente leitura, nunca altera nada do host.
        // Não força nenhuma reconexão: se _app já estiver null, o relatório já diz isso na
        // própria linha de Optimization, sem precisar reconectar aqui (reconectar mudaria o
        // estado observado, o que não é o objetivo de um diagnóstico).
        private void OnRenderDiagnostics(object sender, RoutedEventArgs e)
        {
            RenderDiagnosticsText.Text = Diagnostics.PanelRenderDiagnostics.Describe(_app, this);
        }

        private void OnCopyRenderDiagnostics(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RenderDiagnosticsText.Text))
            {
                OnRenderDiagnostics(sender, e);
            }

            try
            {
                System.Windows.Clipboard.SetText(RenderDiagnosticsText.Text);
            }
            catch (Exception)
            {
                // Falha ao usar a área de transferência (ex.: outro processo com lock) nunca
                // pode travar o host — o texto já está visível na tela para cópia manual.
            }
        }

        // Diagnóstico de identidade de sessão (ver Diagnostics/LayerIdentityDiagnostics.cs): só
        // roda depois de o usuário confirmar o aviso com o efeito exato. Sem RunWithWorkspace —
        // não age no documento ativo do usuário, só num temporário criado pelo próprio diagnóstico.
        private void OnLayerIdentityDiagnostics(object sender, RoutedEventArgs e)
        {
            if (_app == null)
            {
                Connect();
            }

            if (_app == null)
            {
                ShowLayerIdentityResult("Não conectado ao CorelDRAW — diagnóstico não executado.", "");
                return;
            }

            var answer = MessageBox.Show(
                Diagnostics.LayerIdentityDiagnostics.Explanation,
                "Diagnóstico de identidade de camadas",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes)
            {
                ShowLayerIdentityResult("Diagnóstico cancelado — nada foi feito.", "");
                return;
            }

            try
            {
                var report = Diagnostics.LayerIdentityDiagnostics.Run(_app, ResolveAppVersion());
                ShowLayerIdentityResult(Diagnostics.LayerIdentityReportSummary.Summarize(report), report);
            }
            catch (Exception ex)
            {
                ShowLayerIdentityResult($"Diagnóstico interrompido antes de começar: {ex.GetType().Name}: {ex.Message}", "");
            }
            finally
            {
                UpdateRestoreHiddenLayersButtonState();
            }
        }

        // Resumo sempre visível; relatório completo guardado no TextBlock da área recolhível
        // (que começa fechada a cada execução — nunca despejado expandido no docker).
        private void ShowLayerIdentityResult(string summary, string report)
        {
            LayerIdentitySummaryText.Text = summary;
            LayerIdentityDiagnosticsText.Text = report;
            var hasReport = !string.IsNullOrEmpty(report);
            LayerIdentityReportToggleButton.IsEnabled = hasReport;
            LayerIdentityCopyButton.IsEnabled = hasReport;
            SetLayerIdentityReportVisible(false);
        }

        private void OnToggleLayerIdentityReport(object sender, RoutedEventArgs e) =>
            SetLayerIdentityReportVisible(LayerIdentityReportPanel.Visibility != Visibility.Visible);

        private void SetLayerIdentityReportVisible(bool visible)
        {
            LayerIdentityReportPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            LayerIdentityReportToggleLabel.Text = visible ? "Ocultar relatório" : "Ver relatório";
        }

        private void OnCopyLayerIdentityDiagnostics(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LayerIdentityDiagnosticsText.Text))
            {
                return;
            }

            try
            {
                System.Windows.Clipboard.SetText(LayerIdentityDiagnosticsText.Text);
            }
            catch (Exception)
            {
                // Mesmo motivo de OnCopyRenderDiagnostics — o texto continua visível.
            }
        }

        private void OnGroupIntersection(object sender, RoutedEventArgs e) => RunOnDocument("Agrupar Interseção", doc =>
        {
            var result = _intersectGroup.GroupOverlapping(doc.ActivePage, doc.Unit);
            return result.IsSuccess
                ? OperationResult.Success(result.Message)
                : OperationResult.Fail(result.Status, result.Message);
        });

        /// <summary>
        /// "Abrir ListForge Integrado". Reaproveita a mesma janela (ClassicWorkspaceWindow)
        /// entre cliques — evita abrir várias áreas de trabalho duplicadas, e preserva a lista
        /// em edição ao alternar o foco entre o Corel e o integrado (a janela não é recriada
        /// nem fechada sozinha). Não depende de documento aberto no CorelDRAW nem do executável
        /// do standalone — só do núcleo compartilhado (ListForge.Core).
        /// </summary>
        private void OnOpenListForgeIntegrated(object sender, RoutedEventArgs e)
        {
            if (_listForgeIntegratedWindow == null || !_listForgeIntegratedWindow.IsLoaded)
            {
                _listForgeIntegratedWindow = new ListForgeIntegrated.ClassicWorkspaceWindow(_app);
                _listForgeIntegratedWindow.Closed += (_, _) => _listForgeIntegratedWindow = null;
                _listForgeIntegratedWindow.Show();
                // Show() não garante foco nativo do Win32 quando quem chama é uma Window WPF
                // hospedada por reflexão dentro do processo do Corel (ver ThreadKeyboardRouter —
                // candidato a causa real do bloqueio de teclado: sem isso, GetFocus() pode
                // continuar apontando para a janela do Corel mesmo com esta janela visível).
                _listForgeIntegratedWindow.Activate();
                return;
            }

            if (_listForgeIntegratedWindow.WindowState == WindowState.Minimized)
            {
                _listForgeIntegratedWindow.WindowState = WindowState.Normal;
            }
            _listForgeIntegratedWindow.Activate();
        }

        /// <summary>
        /// "Abrir ListForge Standalone ↗". Deliberadamente NÃO passa por RunWithWorkspace/Connect
        /// — o ListForge standalone é um app externo e deve abrir mesmo sem documento (ou sem
        /// conexão) com o CorelDRAW, ao contrário de todo outro botão deste painel.
        /// </summary>
        private void OnOpenListForgeStandalone(object sender, RoutedEventArgs e)
        {
            var result = _listForge.TryOpenConfigured();
            if (result.Status == ListForgeLaunchStatus.NeedsPathSelection)
            {
                PromptForListForgePathAndOpen(result.Message);
                return;
            }

            ReportListForgeResult(result);
        }

        private void OnLocateListForgeExecutable(object sender, RoutedEventArgs e) => PromptForListForgePathAndOpen(null);

        private void PromptForListForgePathAndOpen(string? reasonForPrompt)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executável (*.exe)|*.exe",
                Title = "Localizar ListForge.exe",
                CheckFileExists = true
            };

            var currentPath = ListForgeConfigStore.ReadConfiguredPath();
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                try
                {
                    dialog.InitialDirectory = System.IO.Path.GetDirectoryName(currentPath);
                }
                catch (ArgumentException)
                {
                    // Caminho antigo malformado — ignora e abre o seletor sem pasta inicial.
                }
            }

            if (dialog.ShowDialog() != true)
            {
                SetStatus(reasonForPrompt == null
                    ? "Localizar ListForge: seleção cancelada — nenhum caminho foi alterado."
                    : $"Abrir ListForge: {reasonForPrompt} Seleção cancelada — nenhum caminho foi alterado.");
                return;
            }

            ReportListForgeResult(_listForge.OpenWithSelectedPath(dialog.FileName));
        }

        private void ReportListForgeResult(ListForgeLaunchResult result)
        {
            SetStatus($"ListForge: {result.Message}");
            UpdateListForgePathText();
        }

        private void UpdateListForgePathText()
        {
            var configured = ListForgeConfigStore.ReadConfiguredPath();
            ListForgePathText.Text = string.IsNullOrWhiteSpace(configured)
                ? "Nenhum caminho do ListForge configurado ainda."
                : $"Caminho configurado: {configured}";
            ListForgePathText.ToolTip = configured;
        }

        /// <summary>
        /// "Abrir Nexor ↗" — primeira etapa da integração com Produção (ver AGENTS.md). Mesmo
        /// padrão de OnOpenListForgeStandalone: deliberadamente NÃO passa por
        /// RunWithWorkspace/Connect — o Nexor é um app externo e deve abrir mesmo sem documento
        /// (ou sem conexão) com o CorelDRAW. Nenhuma interface do Nexor é incorporada, nenhum
        /// dado de pedido é lido/compartilhado e nexor.db nunca é tocado por este botão.
        /// </summary>
        private void OnOpenNexor(object sender, RoutedEventArgs e)
        {
            var result = _nexor.TryOpenConfigured();
            if (result.Status == NexorLaunchStatus.NeedsPathSelection)
            {
                PromptForNexorPathAndOpen(result.Message);
                return;
            }

            ReportNexorResult(result);
        }

        private void OnLocateNexorExecutable(object sender, RoutedEventArgs e) => PromptForNexorPathAndOpen(null);

        private void PromptForNexorPathAndOpen(string? reasonForPrompt)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executável (*.exe)|*.exe",
                Title = "Localizar executável do Nexor",
                CheckFileExists = true
            };

            var currentPath = NexorConfigStore.ReadConfiguredPath();
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                try
                {
                    dialog.InitialDirectory = System.IO.Path.GetDirectoryName(currentPath);
                }
                catch (ArgumentException)
                {
                    // Caminho antigo malformado — ignora e abre o seletor sem pasta inicial.
                }
            }

            if (dialog.ShowDialog() != true)
            {
                SetStatus(reasonForPrompt == null
                    ? "Localizar Nexor: seleção cancelada — nenhum caminho foi alterado."
                    : $"Abrir Nexor: {reasonForPrompt} Seleção cancelada — nenhum caminho foi alterado.");
                return;
            }

            ReportNexorResult(_nexor.OpenWithSelectedPath(dialog.FileName));
        }

        private void ReportNexorResult(NexorLaunchResult result)
        {
            SetStatus($"Nexor: {result.Message}");
            UpdateNexorPathText();
        }

        private void UpdateNexorPathText()
        {
            var configured = NexorConfigStore.ReadConfiguredPath();
            NexorPathText.Text = string.IsNullOrWhiteSpace(configured)
                ? "Nenhum caminho do Nexor configurado ainda."
                : $"Caminho configurado: {configured}";
            NexorPathText.ToolTip = configured;
        }

        // ---- Contexto de produção (Nexor) ----
        // O contexto carregado é da SESSÃO do painel, só em memória e só para consulta (ver
        // Integrations/OrderContextService.cs e docs/integrations/nexor-order-context.md): o
        // resumo na seção Produção o mostra; a janela de detalhes só exibe o mesmo objeto. Uma
        // importação nova só o substitui quando dá certo — cancelar ou falhar mantém o anterior.
        private Integrations.OrderContextService.OrderContext? _loadedOrderContext;

        // Arquivo de onde veio o contexto carregado — só para marcar o item em "Recentes".
        private string? _loadedOrderContextPath;

        // Saídas salvas com o contexto carregado (resultado de execução, PX → Nexor) — também só
        // da sessão, em memória, até o usuário exportar o arquivo.
        private readonly Integrations.ExecutionResultService.Session _executionResult = new Integrations.ExecutionResultService.Session();

        /// <summary>
        /// "Carregar contexto de produção…" (o Nexor não tem pedido comercial formal — isto é um
        /// trabalho de impressão dentro de um rolo). Deliberadamente NÃO passa por
        /// RunWithWorkspace/Connect — funciona sem documento aberto no CorelDRAW.
        /// </summary>
        private void OnLoadOrderContextClick(object sender, RoutedEventArgs e) => PromptForOrderContextFileAndLoad();

        private void PromptForOrderContextFileAndLoad()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Carregar contexto de produção (Nexor)",
                Filter = "Arquivo JSON (*.json)|*.json",
            };
            if (dialog.ShowDialog() != true)
            {
                ReportOrderContextNotReplaced("seleção cancelada");
                return;
            }

            string json;
            try
            {
                json = System.IO.File.ReadAllText(dialog.FileName);
            }
            catch (Exception ex)
            {
                ReportOrderContextNotReplaced($"não foi possível ler o arquivo — {ex.Message}");
                return;
            }

            var validation = Integrations.OrderContextService.Validate(json);
            if (!validation.IsValid)
            {
                ReportOrderContextNotReplaced(validation.FailureReason!);
                return;
            }

            SetLoadedOrderContext(validation.Context!, dialog.FileName);
        }

        /// <summary>
        /// "Colar caminho do contexto…" — alternativa ao seletor (ver
        /// Production/OrderContextPathDialog): mesma validação, mesmo caráter só leitura/só
        /// memória, mesma regra de só substituir em caso de sucesso.
        /// </summary>
        private void OnPasteOrderContextPathClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Production.OrderContextPathDialog();
            if (dialog.ShowDialog() != true)
            {
                ReportOrderContextNotReplaced("cancelado");
                return;
            }

            SetLoadedOrderContext(dialog.ResultContext!, dialog.ResultPath);
        }

        /// <summary>
        /// "Recentes…": menu com os últimos arquivos carregados (OrderContextHistoryStore). Abre
        /// onde está o mouse — funciona igual pelo atalho dos Favoritos, com a seção recolhida.
        /// </summary>
        private void OnShowRecentOrderContexts(object sender, RoutedEventArgs e)
        {
            var entries = Integrations.OrderContextHistoryStore.Load();
            if (entries.Count == 0)
            {
                UpdateRecentOrderContextsButton();
                SetStatus("Contexto de produção: nenhum contexto recente nesta máquina.");
                return;
            }

            var menu = new ContextMenu
            {
                Style = (Style)FindResource("Px.ContextMenu"),
                Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
            };
            foreach (var entry in entries)
            {
                var path = entry.Path;
                var isLoaded = string.Equals(path, _loadedOrderContextPath, StringComparison.OrdinalIgnoreCase);
                var item = new MenuItem
                {
                    Header = RecentOrderContextHeader(entry, isLoaded),
                    Style = (Style)FindResource("Px.MenuItem"),
                    ToolTip = path,
                };
                item.Click += (_, _) => LoadOrderContextFromHistory(path);
                menu.Items.Add(item);
            }

            menu.Items.Add(new Separator());
            var clear = new MenuItem { Header = "Limpar lista de recentes", Style = (Style)FindResource("Px.MenuItem") };
            clear.Click += (_, _) => ChangeOrderContextHistory(Integrations.OrderContextHistoryStore.Clear, "Lista de contextos recentes limpa (os arquivos .json não foram tocados).");
            menu.Items.Add(clear);
            menu.IsOpen = true;
        }

        private UIElement RecentOrderContextHeader(Integrations.OrderContextHistoryStore.Entry entry, bool isLoaded)
        {
            var name = System.IO.Path.GetFileName(entry.Path);
            var folder = ShortFolder(System.IO.Path.GetDirectoryName(entry.Path) ?? "");
            var when = entry.LoadedAtUtc == DateTimeOffset.MinValue ? "" : $" · {entry.LoadedAtUtc.ToLocalTime():dd/MM HH:mm}";
            var panel = new StackPanel { MaxWidth = 320 };
            panel.Children.Add(new TextBlock
            {
                Text = isLoaded ? $"{name} (carregado)" : name,
                FontWeight = isLoaded ? FontWeights.SemiBold : FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            panel.Children.Add(new TextBlock
            {
                Text = folder + when,
                Style = (Style)FindResource("Px.MutedText"),
                Margin = new Thickness(0),
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            return panel;
        }

        // Início (unidade/servidor) e as duas últimas pastas — o caminho inteiro fica na dica.
        internal static string ShortFolder(string folder)
        {
            var parts = folder.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 3) return folder;

            var start = folder.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\" + parts[0] : parts[0];
            return $@"{start}\…\{parts[parts.Length - 2]}\{parts[parts.Length - 1]}";
        }

        /// <summary>Mesmo caminho de "Colar caminho do contexto": lê e valida agora; falhar
        /// mantém o contexto anterior. Arquivo sumido: oferece tirar da lista (pasta de rede
        /// desconectada também cai aqui — por isso pergunta, nunca remove sozinho).</summary>
        private void LoadOrderContextFromHistory(string path)
        {
            var result = Integrations.OrderContextPathLoader.Load(path);
            if (result.IsValid)
            {
                SetLoadedOrderContext(result.Context!, result.NormalizedPath);
                return;
            }

            ReportOrderContextNotReplaced(result.FailureReason!);
            if (System.IO.File.Exists(path) || System.IO.Directory.Exists(path)) return;

            var choice = MessageBox.Show(
                $"O arquivo não foi encontrado:\n{path}\n\nSe estiver numa pasta de rede, ela pode só estar desconectada. Tirar este item da lista de recentes? (Nada mais é apagado.)",
                "PX Tool Box DEV",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (choice == MessageBoxResult.Yes)
            {
                ChangeOrderContextHistory(() => Integrations.OrderContextHistoryStore.Remove(path), "Item tirado da lista de contextos recentes.");
            }
        }

        private void ChangeOrderContextHistory(Func<IReadOnlyList<Integrations.OrderContextHistoryStore.Entry>> change, string? doneMessage)
        {
            try
            {
                change();
                if (doneMessage != null) SetStatus(doneMessage);
            }
            catch (Exception ex)
            {
                SetStatus($"Não foi possível atualizar a lista de contextos recentes: {ex.Message}");
            }

            UpdateRecentOrderContextsButton();
        }

        private void UpdateRecentOrderContextsButton() =>
            RecentOrderContextsButton.IsEnabled = Integrations.OrderContextHistoryStore.Load().Count > 0;

        private void ReportOrderContextNotReplaced(string reason)
        {
            reason = reason.TrimEnd('.', ' ');
            SetStatus(_loadedOrderContext == null
                ? $"Contexto de produção: {reason}."
                : $"Contexto de produção: {reason} — o contexto anterior continua carregado.");
        }

        /// <summary>Só depois de uma validação bem-sucedida: troca o contexto da sessão,
        /// atualiza o resumo e, se a janela de detalhes estiver aberta, ela também.</summary>
        private void SetLoadedOrderContext(Integrations.OrderContextService.OrderContext context, string? sourcePath)
        {
            _loadedOrderContext = context;
            _loadedOrderContextPath = sourcePath;
            _executionResult.SetContext(context);
            UpdateOrderContextSummary();
            if (_orderContextWindow != null && _orderContextWindow.IsLoaded)
            {
                _orderContextWindow.LoadContext(context);
            }

            SetStatus("Contexto de produção carregado — resumo na seção Produção; \"Ver detalhes\" mostra tudo.");
            if (!string.IsNullOrEmpty(sourcePath))
            {
                // Só o caminho; uma falha aqui não desfaz o carregamento.
                ChangeOrderContextHistory(() => Integrations.OrderContextHistoryStore.Record(sourcePath!, DateTimeOffset.UtcNow), null);
            }
        }

        private void UpdateOrderContextSummary()
        {
            var context = _loadedOrderContext;
            OrderContextSummaryPanel.Visibility = context == null ? Visibility.Collapsed : Visibility.Visible;
            ShowOrderContextDetailsButton.IsEnabled = context != null;
            ClearOrderContextButton.IsEnabled = context != null;
            var hasSuggestion = context != null && Production.OrderContextSummary.SuggestLayoutName(context) != null;
            UseOrderContextLayoutNameButton.IsEnabled = hasSuggestion;
            UseOrderContextRenameButton.IsEnabled = hasSuggestion;
            NameActivePageFromContextButton.IsEnabled = hasSuggestion;
            UseRollSubfolderCheckBox.IsEnabled = context != null;
            UpdateOutputFolderPreview();
            var outputCount = context == null ? 0 : _executionResult.Outputs.Count;
            ExportExecutionResultButton.IsEnabled = outputCount > 0;
            OrderContextOutputsText.Text = outputCount == 0
                ? "Nenhuma saída salva com este contexto nesta sessão."
                : $"Saídas salvas nesta sessão: {outputCount} ({string.Join(", ", _executionResult.Outputs.Select(o => o.Label))}).";
            if (context == null)
            {
                OrderContextRollText.Text = "";
                OrderContextJobText.Text = "";
                OrderContextPedidoText.Text = "";
                OrderContextExportedText.Text = "";
                return;
            }

            var summary = Production.OrderContextSummary.Build(context);
            OrderContextRollText.Text = summary.RollLine;
            OrderContextJobText.Text = summary.JobLine;
            OrderContextPedidoText.Text = summary.PedidoLine ?? "";
            OrderContextPedidoText.Visibility = summary.PedidoLine == null ? Visibility.Collapsed : Visibility.Visible;
            OrderContextExportedText.Text = summary.ExportedLine;
        }

        /// <summary>Preparação e saída → "Usar contexto de produção": preenche "Nome do layout"
        /// com a sugestão do contexto carregado (OrderContextSummary.SuggestLayoutName). O valor
        /// anterior aparece no status, para poder ser redigitado.</summary>
        private void OnUseOrderContextLayoutName(object sender, RoutedEventArgs e) =>
            FillFromOrderContext(LayoutNameTextBox, "Nome do layout");

        /// <summary>Outras ferramentas — Objetos → "Usar contexto de produção": mesmo
        /// preenchimento, no campo de Renomear Objetos (não renomeia nada sozinho).</summary>
        private void OnUseOrderContextRename(object sender, RoutedEventArgs e) =>
            FillFromOrderContext(RenameTextBox, "Novo nome dos objetos");

        private void FillFromOrderContext(System.Windows.Controls.TextBox target, string fieldName)
        {
            if (!TryGetOrderContextName(out var suggestion)) return;

            var previous = target.Text;
            target.Text = suggestion;
            SetStatus(string.IsNullOrWhiteSpace(previous) || previous == suggestion
                ? $"{fieldName}: \"{suggestion}\" (do contexto de produção)."
                : $"{fieldName}: \"{suggestion}\" (do contexto de produção) — antes era \"{previous}\".");
        }

        /// <summary>Produção → "Nomear página ativa com o contexto": a mesma sugestão, como
        /// nome da página ativa (um passo de desfazer).</summary>
        private void OnNameActivePageFromContext(object sender, RoutedEventArgs e)
        {
            if (!TryGetOrderContextName(out var suggestion)) return;

            RunOnActivePage("Nomear página ativa com o contexto", page => _pageRename.Rename(page, suggestion));
        }

        private bool TryGetOrderContextName(out string suggestion)
        {
            var value = _loadedOrderContext == null ? null : Production.OrderContextSummary.SuggestLayoutName(_loadedOrderContext);
            if (value == null)
            {
                SetStatus(_loadedOrderContext == null
                    ? "Usar contexto de produção: nenhum contexto carregado (seção Produção)."
                    : "Usar contexto de produção: o arquivo não traz pedido informado nem arquivo de origem.");
                suggestion = "";
                return false;
            }

            suggestion = value;
            return true;
        }

        /// <summary>"Ver detalhes": a janela existente, com o contexto da sessão.</summary>
        private void OnShowOrderContextDetails(object sender, RoutedEventArgs e)
        {
            if (_loadedOrderContext == null)
            {
                SetStatus("Contexto de produção: nenhum contexto carregado.");
                return;
            }

            if (_orderContextWindow == null || !_orderContextWindow.IsLoaded)
            {
                _orderContextWindow = new Production.OrderContextWindow();
                _orderContextWindow.Closed += (_, _) => _orderContextWindow = null;
                _orderContextWindow.RequestLoadAnother += (_, _) => PromptForOrderContextFileAndLoad();
                _orderContextWindow.RequestClear += (_, _) => ClearLoadedOrderContext();
            }

            _orderContextWindow.LoadContext(_loadedOrderContext);
            if (_orderContextWindow.WindowState == WindowState.Minimized)
                _orderContextWindow.WindowState = WindowState.Normal;
            _orderContextWindow.Show();
            _orderContextWindow.Activate();
        }

        private void OnClearOrderContext(object sender, RoutedEventArgs e)
        {
            if (_loadedOrderContext == null)
            {
                SetStatus("Contexto de produção: nenhum contexto carregado.");
                return;
            }

            var outputsNote = _executionResult.Outputs.Count == 0
                ? ""
                : $"\n\nAs {_executionResult.Outputs.Count} saída(s) registrada(s) com este contexto também são esquecidas — exporte o resultado antes, se for usar. Os arquivos .cdr continuam onde foram salvos.";
            var choice = MessageBox.Show(
                "Limpar o contexto de produção carregado nesta sessão? Isso não afeta o Nexor, o arquivo exportado nem nenhum dado do ListForge ou do documento." + outputsNote,
                "PX Tool Box DEV",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (choice != MessageBoxResult.Yes) return;

            ClearLoadedOrderContext();
        }

        /// <summary>Esquece o contexto da sessão (nada é gravado ou apagado em disco) e fecha a
        /// janela de detalhes, se aberta.</summary>
        private void ClearLoadedOrderContext()
        {
            _loadedOrderContext = null;
            _loadedOrderContextPath = null;
            _executionResult.SetContext(null);
            UpdateOrderContextSummary();
            _orderContextWindow?.Close();
            SetStatus("Contexto de produção limpo nesta sessão.");
        }

        /// <summary>
        /// Produção → "Exportar resultado para o Nexor…": grava o PxExecutionResult (ver
        /// Integrations/ExecutionResultService e docs/integrations/px-execution-result.md) onde o
        /// usuário escolher — por padrão, na pasta da última saída salva. Não passa por
        /// RunWithWorkspace: não usa o Corel.
        /// </summary>
        private void OnExportExecutionResult(object sender, RoutedEventArgs e)
        {
            var context = _executionResult.Context;
            if (context == null || _executionResult.Outputs.Count == 0)
            {
                SetStatus("Exportar resultado: nenhuma saída salva com o contexto carregado nesta sessão.");
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exportar resultado de execução para o Nexor",
                Filter = "Arquivo JSON (*.json)|*.json",
                FileName = Integrations.ExecutionResultService.SuggestFileName(context),
            };
            var lastFolder = System.IO.Path.GetDirectoryName(_executionResult.Outputs[_executionResult.Outputs.Count - 1].FilePath);
            if (!string.IsNullOrEmpty(lastFolder) && System.IO.Directory.Exists(lastFolder))
            {
                dialog.InitialDirectory = lastFolder;
            }

            if (dialog.ShowDialog() != true)
            {
                SetStatus("Exportar resultado: cancelado — nada foi gravado.");
                return;
            }

            try
            {
                var json = Integrations.ExecutionResultService.Build(context, _executionResult.Outputs, DateTimeOffset.UtcNow, Integrations.PxVersionInfo.PxToolBoxVersion);
                System.IO.File.WriteAllText(dialog.FileName, json, new System.Text.UTF8Encoding(false));
                Integrations.PxLogger.Info("ExecutionResultExport", $"Resultado de execução exportado ({_executionResult.Outputs.Count} saída(s)).", dialog.FileName);
                SetStatus($"Resultado exportado ({_executionResult.Outputs.Count} saída(s)): {dialog.FileName}. O Nexor não foi alterado.");
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                SetStatus($"Exportar resultado: não foi possível gravar — {ex.Message}");
            }
        }

        /// <summary>
        /// Processo real, sem shell e sem argumentos (ver ListForgeLauncher — nenhum mecanismo
        /// de ativação de instância existente é inventado aqui). WorkingDirectory afeta só o
        /// processo filho — nunca Environment.CurrentDirectory do processo do Corel.
        /// </summary>
        private static int StartListForgeProcess(string exePath, string workingDirectory)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("O sistema não retornou um processo iniciado.");
            }

            return process.Id;
        }

        /// <summary>
        /// Processo real, sem shell e sem argumentos (ver NexorLauncher — nenhum mecanismo de
        /// ativação de instância existente é inventado aqui). WorkingDirectory afeta só o
        /// processo filho — nunca Environment.CurrentDirectory do processo do Corel.
        /// </summary>
        private static int StartNexorProcess(string exePath, string workingDirectory)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("O sistema não retornou um processo iniciado.");
            }

            return process.Id;
        }

        private void RunSum(string commandName, Func<System.Collections.Generic.IReadOnlyList<ShapeSize>, DocumentUnit?, OperationResult<MeasurementSummary>> operation)
        {
            RunWithWorkspace(commandName, (ws, doc) =>
            {
                var sizes = ws.ActiveSelection.Shapes.Select(s => new ShapeSize(s.Width, s.Height)).ToList();
                var result = operation(sizes, doc.Unit);
                if (!result.IsSuccess)
                {
                    // Seleção vazia ou unidade não reconhecida: nenhuma janela de sucesso, nada
                    // copiado — o Status mostra o motivo da falha, sem apresentar um resultado
                    // zerado ou incompleto como se fosse sucesso.
                    return OperationResult.Fail(result.Status, result.Message);
                }

                var summary = result.Value!;
                bool copied;
                try
                {
                    ws.CopyToClipboard(summary.ClipboardText);
                    copied = true;
                }
                catch (Exception ex)
                {
                    copied = false;
                    System.Diagnostics.Debug.WriteLine($"Falha ao copiar para a área de transferência: {ex.Message}");
                }

                var copyLine = copied
                    ? "Resultado copiado para a área de transferência."
                    : "Não foi possível copiar o resultado para a área de transferência.";

                MessageBox.Show(
                    $"{commandName}\n\n{summary.DisplayText}\n\n{copyLine}",
                    commandName,
                    MessageBoxButton.OK,
                    copied ? MessageBoxImage.Information : MessageBoxImage.Warning);

                return OperationResult.Success($"{summary.DisplayText} {copyLine}");
            });
        }

        private void RunOnSelection(string commandName, Func<ICorelWorkspace, OperationResult> operation) =>
            RunWithWorkspace(commandName, (ws, _) => operation(ws));

        // ---- Montagem → Dividir em partes (Slice): prévia na janela; dividir pelo SliceCutService ----

        private const string SliceCommand = "Dividir em partes (Slice)";

        private void OnSlicePreview(object sender, RoutedEventArgs e)
        {
            var size = MeasureSelectionForSlice(out var message);
            if (size == null)
            {
                SetStatus($"{SliceCommand}: {message}");
                return;
            }

            Slicing.SlicePreviewWindow.ShowFor(size.Value.Width, size.Value.Height, new SliceHost(this));
        }

        /// <summary>Caixa da seleção em mm (lida na unidade do documento, como em PageCaptureService):
        /// a janela só recebe os números.</summary>
        private (double Width, double Height)? MeasureSelectionForSlice(out string message)
        {
            message = string.Empty;
            try
            {
                if (_app == null) Connect();
                var document = _app?.ActiveDocument;
                if (_app == null || document == null)
                {
                    message = "abra um documento e selecione a arte.";
                    return null;
                }

                var shapes = new CorelWorkspaceAdapter(_app).ActiveSelection.Shapes;
                if (shapes.Count == 0)
                {
                    message = "selecione os objetos que formam a área da arte.";
                    return null;
                }

                var unit = document.Unit;
                var left = shapes.Min(s => s.CenterX - s.Width / 2);
                var right = shapes.Max(s => s.CenterX + s.Width / 2);
                var bottom = shapes.Min(s => s.CenterY - s.Height / 2);
                var top = shapes.Max(s => s.CenterY + s.Height / 2);
                return (_app.ConvertUnits(right - left, unit, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter),
                    _app.ConvertUnits(top - bottom, unit, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter));
            }
            catch (Exception ex)
            {
                message = $"não foi possível ler a seleção — {ex.Message}";
                return null;
            }
        }

        // ---- Montagem → Encaixar peças (pela caixa ou pelo contorno): prévia na janela; aplicar pelo NestingService ----

        private const string NestingCommand = "Encaixar peças";

        private void OnNesting(object sender, RoutedEventArgs e)
        {
            var selection = ReadNestingSelection(out var message);
            if (selection == null)
            {
                SetStatus($"{NestingCommand}: {message}");
                return;
            }

            Nesting.NestingWindow.ShowFor(selection, new NestingHost(this));
        }

        private Nesting.NestingSelection? ReadNestingSelection(out string message)
        {
            message = string.Empty;
            try
            {
                if (_app == null) Connect();
                var document = _app?.ActiveDocument;
                if (_app == null || document == null)
                {
                    message = "abra um documento e selecione as peças.";
                    return null;
                }

                var selection = new CorelWorkspaceAdapter(_app).ActiveSelection;
                if (selection.Count == 0)
                {
                    message = "selecione as peças que vão ser encaixadas (cada objeto ou grupo é uma peça).";
                    return null;
                }

                var millimeter = _app.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, document.Unit);
                var page = document.ActivePage;
                return new Nesting.NestingSelection(_nesting.ReadItems(selection, millimeter),
                    page.SizeWidth / millimeter, page.SizeHeight / millimeter,
                    _nesting.ReadOrigins(selection, new CorelPageAdapter(page, document), millimeter),
                    _nesting.ReadLayers(selection));
            }
            catch (Exception ex)
            {
                message = $"não foi possível ler a seleção — {ex.Message}";
                return null;
            }
        }

        private sealed class NestingHost : Nesting.INestingHost
        {
            private readonly ControlUI _panel;

            public NestingHost(ControlUI panel) => _panel = panel;

            public Nesting.NestingSelection? ReadSelection(out string message) => _panel.ReadNestingSelection(out message);

            public OperationResult? Apply(IReadOnlyList<PXToolBox.Core.Nesting.NestItem> items, IReadOnlyList<int> sources,
                PXToolBox.Core.Nesting.NestResult result, bool resizePage) =>
                _panel.RunWithWorkspace(NestingCommand, (ws, doc) =>
                {
                    var millimeter = _panel._app!.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, _panel._app.ActiveDocument.Unit);
                    return _panel._nesting.Apply(ws.ActiveSelection, doc.ActivePage, millimeter, items, result, resizePage, sources);
                });

            // Só leitura (sem grupo de desfazer), como medir a seleção.
            public ContourReading? ReadContours(out string message)
            {
                message = string.Empty;
                try
                {
                    if (_panel._app == null) _panel.Connect();
                    var document = _panel._app?.ActiveDocument;
                    if (_panel._app == null || document == null)
                    {
                        message = "Abra um documento e selecione as peças.";
                        return null;
                    }

                    var selection = new CorelWorkspaceAdapter(_panel._app).ActiveSelection;
                    if (selection.Count == 0)
                    {
                        message = "Selecione as peças que vão ser encaixadas.";
                        return null;
                    }

                    var millimeter = _panel._app.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, document.Unit);
                    return _panel._nesting.ReadContours(selection, millimeter);
                }
                catch (Exception ex)
                {
                    message = $"Não foi possível ler os contornos — {ex.Message}";
                    return null;
                }
            }

            public OperationResult? ApplyContour(ContourReading reading, IReadOnlyList<int> sources, PXToolBox.Core.Nesting.ContourNestResult result,
                double marginMm, double spacingMm, bool resizePage) =>
                _panel.RunWithWorkspace(NestingCommand, (ws, doc) =>
                {
                    var millimeter = _panel._app!.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, _panel._app.ActiveDocument.Unit);
                    return _panel._nesting.ApplyContour(ws.ActiveSelection, doc.ActivePage, millimeter, reading, result, marginMm, spacingMm, resizePage, sources);
                });

            public OperationResult? ApplyAreas(IReadOnlyList<PXToolBox.Core.Nesting.NestItem> items, IReadOnlyList<int> sources,
                PXToolBox.Core.Nesting.AreaNestResult<PXToolBox.Core.Nesting.NestResult, PXToolBox.Core.Nesting.NestItem> result,
                bool resizePage, bool drawAreas, bool groupBlocks, bool legends) =>
                _panel.RunWithWorkspace(NestingCommand, (ws, doc) =>
                {
                    var millimeter = _panel._app!.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, _panel._app.ActiveDocument.Unit);
                    return _panel._nesting.ApplyAreas(ws.ActiveSelection, doc.ActivePage, millimeter, items, result, resizePage, drawAreas, sources, groupBlocks, legends);
                });

            public OperationResult? ApplyContourAreas(ContourReading reading, IReadOnlyList<int> sources,
                PXToolBox.Core.Nesting.AreaNestResult<PXToolBox.Core.Nesting.ContourNestResult, PXToolBox.Core.Nesting.ContourPiece> result,
                double marginMm, double spacingMm, bool resizePage, bool drawAreas, bool groupBlocks, bool legends) =>
                _panel.RunWithWorkspace(NestingCommand, (ws, doc) =>
                {
                    var millimeter = _panel._app!.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, _panel._app.ActiveDocument.Unit);
                    return _panel._nesting.ApplyContourAreas(ws.ActiveSelection, doc.ActivePage, millimeter, reading, result,
                        marginMm, spacingMm, resizePage, drawAreas, sources, groupBlocks, legends);
                });
        }

        /// <summary>Ponte da janela do Slice com o painel: medir e dividir passam pelo mesmo caminho dos
        /// botões (RunWithWorkspace: um passo de desfazer e o status do painel).</summary>
        private sealed class SliceHost : Slicing.ISliceHost
        {
            private readonly ControlUI _panel;

            public SliceHost(ControlUI panel) => _panel = panel;

            public (double Width, double Height)? MeasureSelection(out string message) =>
                _panel.MeasureSelectionForSlice(out message);

            public OperationResult? Apply(PXToolBox.Core.Slicing.SlicePlan plan, double separationMm, bool deleteOriginal) =>
                _panel.RunWithWorkspace(SliceCommand, (ws, doc) =>
                {
                    var millimeter = _panel._app!.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, _panel._app.ActiveDocument.Unit);
                    return _panel._sliceCut.Apply(ws.ActiveSelection, doc.ActivePage, millimeter, plan, separationMm, deleteOriginal);
                });

            public OperationResult? ApplyRegions(PXToolBox.Core.Slicing.SliceRegionPlan plan, double separationMm, bool deleteOriginal) =>
                _panel.RunWithWorkspace(SliceCommand, (ws, doc) =>
                {
                    var millimeter = _panel._app!.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, _panel._app.ActiveDocument.Unit);
                    return _panel._sliceCut.ApplyRegions(ws.ActiveSelection, doc.ActivePage, millimeter, plan, separationMm, deleteOriginal);
                });

            // Só leitura (sem grupo de desfazer), como medir a seleção.
            public SliceGuideCuts? ReadGuides(out string message)
            {
                message = string.Empty;
                try
                {
                    if (_panel._app == null) _panel.Connect();
                    var document = _panel._app?.ActiveDocument;
                    if (_panel._app == null || document == null)
                    {
                        message = "Abra um documento e selecione a arte.";
                        return null;
                    }

                    var millimeter = _panel._app.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, document.Unit);
                    var result = _panel._sliceGuides.ReadCuts(new CorelWorkspaceAdapter(_panel._app).ActiveSelection,
                        new CorelDocumentAdapter(document).ActivePage, millimeter);
                    message = result.Message;
                    return result.IsSuccess ? result.Value : null;
                }
                catch (Exception ex)
                {
                    message = $"Não foi possível ler as guias — {ex.Message}";
                    return null;
                }
            }

            public OperationResult? CreateGuides(IReadOnlyList<double> columns, IReadOnlyList<double> rows) =>
                _panel.RunWithWorkspace(SliceCommand, (ws, doc) =>
                {
                    var millimeter = _panel._app!.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, _panel._app.ActiveDocument.Unit);
                    return _panel._sliceGuides.CreateGuides(ws.ActiveSelection, doc.ActivePage, millimeter, columns, rows);
                });

            public OperationResult? RemoveGuides() =>
                _panel.RunWithWorkspace(SliceCommand, (_, doc) => _panel._sliceGuides.RemoveGuides(doc.ActivePage));

            // Só leitura (sem grupo de desfazer), como medir a seleção.
            public SliceArtOutline? ReadArtOutline(out string message)
            {
                message = string.Empty;
                try
                {
                    if (_panel._app == null) _panel.Connect();
                    var document = _panel._app?.ActiveDocument;
                    if (_panel._app == null || document == null)
                    {
                        message = "Abra um documento e selecione a arte.";
                        return null;
                    }

                    var millimeter = _panel._app.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, document.Unit);
                    var result = new SliceOutlineService(_panel._nesting).Read(new CorelWorkspaceAdapter(_panel._app).ActiveSelection, millimeter);
                    message = result.Message;
                    return result.IsSuccess ? result.Value : null;
                }
                catch (Exception ex)
                {
                    message = $"Não foi possível ler o contorno da arte — {ex.Message}";
                    return null;
                }
            }

            // Só leitura (sem grupo de desfazer), como ler as guias.
            public SliceDrawnCuts? ReadDrawnCuts(out string message)
            {
                message = string.Empty;
                try
                {
                    if (_panel._app == null) _panel.Connect();
                    var document = _panel._app?.ActiveDocument;
                    if (_panel._app == null || document == null)
                    {
                        message = "Abra um documento e selecione a arte.";
                        return null;
                    }

                    var millimeter = _panel._app.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, document.Unit);
                    var result = new SliceDrawnCutService().ReadCuts(new CorelWorkspaceAdapter(_panel._app).ActiveSelection,
                        new CorelDocumentAdapter(document).ActivePage, millimeter);
                    message = result.Message;
                    return result.IsSuccess ? result.Value : null;
                }
                catch (Exception ex)
                {
                    message = $"Não foi possível ler as linhas de corte — {ex.Message}";
                    return null;
                }
            }

            public OperationResult? CreateCutLayer() =>
                _panel.RunWithWorkspace(SliceCommand, (_, doc) => new SliceDrawnCutService().CreateLayer(doc.ActivePage));

            // Só leitura (sem grupo de desfazer): o PNG vai para a pasta temporária e é apagado logo depois.
            public SliceThumbnail? ReadArtThumbnail(out string message)
            {
                message = string.Empty;
                try
                {
                    if (_panel._app == null) _panel.Connect();
                    var document = _panel._app?.ActiveDocument;
                    if (_panel._app == null || document == null)
                    {
                        message = "Abra um documento e selecione a arte.";
                        return null;
                    }

                    var millimeter = _panel._app.ConvertUnits(1, global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, document.Unit);
                    var result = new SliceThumbnailService().Read(new CorelDocumentAdapter(document),
                        new CorelWorkspaceAdapter(_panel._app).ActiveSelection, millimeter,
                        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PXToolBox-Dev"));
                    message = result.Message;
                    return result.IsSuccess ? result.Value : null;
                }
                catch (Exception ex)
                {
                    message = $"Não foi possível gerar a miniatura da arte — {ex.Message}";
                    return null;
                }
            }
        }

        // ---- Ferramentas do SISBolt (docs/sisbolt-tools-investigation.md) ----

        private void OnToggleStretchToMold(object sender, RoutedEventArgs e) =>
            RunOnSelection("Alternar Esticar no Tamanho do Molde", ws => _sisboltMarking.Toggle(ws.ActiveSelection, SisboltMarking.StretchToMold));

        private void OnToggleIgnoreVariableMetrics(object sender, RoutedEventArgs e) =>
            RunOnSelection("Alternar Ignorar métricas da variável", ws => _sisboltMarking.Toggle(ws.ActiveSelection, SisboltMarking.IgnoreVariableMetrics));

        private void OnToggleFreeHand(object sender, RoutedEventArgs e) =>
            RunOnSelection("Alternar Mão Livre", ws => _sisboltMarking.Toggle(ws.ActiveSelection, SisboltMarking.FreeHand));

        /// <summary>Tolerância da conferência convertida para a unidade do documento (como o
        /// MoldMotor: nunca comparar coordenadas de página contra um valor em mm).</summary>
        private void OnResetRotation(object sender, RoutedEventArgs e) =>
            RunWithWorkspace("Resetar ângulo", (ws, _) =>
            {
                var tolerance = _app!.ConvertUnits(RotationResetService.ToleranceMillimeters,
                    global::Corel.Interop.VGCore.cdrUnit.cdrMillimeter, _app.ActiveDocument.Unit);
                return _rotationReset.Reset(ws.ActiveSelection, (CorelWorkspaceAdapter)ws, tolerance);
            });

        private void OnCreateSingleItemGroup(object sender, RoutedEventArgs e) =>
            RunOnSelection("Criar grupo com item único", ws => _singleItemGroup.CreateGroup(ws.ActiveSelection));

        /// <summary>Lê a seleção (dois objetos) e abre a janela de confirmação; só o Salvar dela
        /// altera o documento, num único Ctrl+Z (as duas escritas no mesmo grupo de comando).</summary>
        private void OnAutomaticHorizontalAlignment(object sender, RoutedEventArgs e)
        {
            const string command = "Alinhamento horizontal automático";
            if (_app == null) Connect();
            if (_app?.ActiveDocument == null)
            {
                SetStatus($"{command}: {(_app == null ? "não conectado ao CorelDRAW" : "nenhum documento aberto")}.");
                return;
            }

            var selection = _horizontalAlignmentLink.ValidateSelection(new CorelWorkspaceAdapter(_app).ActiveSelection);
            if (!selection.IsSuccess)
            {
                SetStatus($"{command}: {selection.Message}");
                return;
            }

            var window = new SisboltTools.HorizontalAlignmentLinkWindow(
                selection.Value.Target, selection.Value.Reference, _horizontalAlignmentLink,
                ZoomToShape,
                (target, reference) => RunWithWorkspace(command, (_, _) => _horizontalAlignmentLink.SaveLink(target, reference, Guid.NewGuid)));
            if (window.ShowDialog() != true)
            {
                SetStatus($"{command}: cancelado — nada foi gravado.");
            }
        }

        // Só a vista: ActiveWindow.ActiveView.ToFitShape não altera o documento nem a seleção.
        private void ZoomToShape(ICorelShape shape)
        {
            try
            {
                if (shape is Corel.CorelShapeAdapter adapter)
                {
                    _app?.ActiveWindow?.ActiveView?.ToFitShape(adapter.Native);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Zoom na referência: {ex.Message}");
            }
        }

        private void RunOnDocument(string commandName, Func<ICorelDocument, OperationResult> operation) =>
            RunWithWorkspace(commandName, (_, doc) => operation(doc));

        private void RunOnActivePage(string commandName, Func<ICorelPage, OperationResult> operation) =>
            RunWithWorkspace(commandName, (_, doc) => operation(doc.ActivePage));

        /// <summary>Roda a operação como um único passo de Desfazer e mostra o resultado no
        /// status. Devolve o resultado (null se não chegou a rodar: sem conexão/documento ou
        /// exceção) — a maioria dos chamadores ignora.</summary>
        private OperationResult? RunWithWorkspace(string commandName, Func<ICorelWorkspace, ICorelDocument, OperationResult> operation)
        {
            if (_app == null)
            {
                Connect();
            }

            if (_app == null)
            {
                SetStatus($"{commandName}: não conectado ao CorelDRAW.");
                return null;
            }

            var document = _app.ActiveDocument;
            if (document == null)
            {
                SetStatus($"{commandName}: nenhum documento aberto.");
                return null;
            }

            var workspace = new CorelWorkspaceAdapter(_app);
            var documentAdapter = new CorelDocumentAdapter(document);

            // Agrupa a operação como um único passo de desfazer. EndCommandGroup é sempre
            // chamado (inclusive em falha) — mas isso não desfaz sozinho o que já rodou; cada
            // serviço de Outputs valida pré-condições antes de alterar o documento para reduzir
            // o risco de deixar estado parcial em caso de erro (ver docs/ARCHITECTURE.md).
            document.BeginCommandGroup(commandName);
            try
            {
                var result = operation(workspace, documentAdapter);
                SetStatus($"{commandName}: {result.Message}");
                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"{commandName}: falha inesperada — {ex.Message}");
                return OperationResult.Fail(OperationStatus.ExecutionFailed, $"Falha inesperada — {ex.Message}");
            }
            finally
            {
                document.EndCommandGroup();
                UpdateConnectionStatus();
                UpdateStandardSizeButtonState();
                UpdateRestoreHiddenLayersButtonState();
            }
        }

        private static OperationResult Describe(OperationResult<(bool NewVisibility, int AffectedCount)> result) =>
            result.IsSuccess
                ? OperationResult.Success($"{result.Value.AffectedCount} camada(s) agora {(result.Value.NewVisibility ? "visível(is)" : "oculta(s)")}.")
                : OperationResult.Fail(result.Status, result.Message);

        private void SetStatus(string message)
        {
            StatusText.Text = $"{DateTime.Now:HH:mm:ss}  {message}";
            StatusPanel.Visibility = Visibility.Visible;
        }
    }
}
