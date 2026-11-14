using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PXToolBox.Core.Nesting;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Tools;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.ListForgeIntegrated;

namespace PXToolBoxDev.Nesting
{
    /// <summary>"Encaixar peças": recebe só as medidas das peças e da página (em mm) e, pelo contorno, os
    /// polígonos; ler a seleção de novo e aplicar passam pelo <see cref="INestingHost"/> do painel. Não
    /// modal e uma só (<see cref="ShowFor"/>), como a janela do Slice.</summary>
    public partial class NestingWindow : Window
    {
        private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("pt-BR");
        private static NestingWindow? _open;
        private readonly ThreadKeyboardRouter _keyboardRouter;
        private INestingHost? _host;
        private NestingSelection _selection;
        private NestResult? _result;
        private ContourNestResult? _contourResult;
        private double _contourMargin;
        private double _contourSpacing;
        private AreaNestResult<NestResult, NestItem>? _areasResult;
        private AreaNestResult<ContourNestResult, ContourPiece>? _contourAreasResult;
        private IReadOnlyList<int> _sources = Array.Empty<int>();
        private CancellationTokenSource? _calculation;
        private int _generation;
        private readonly List<TextBox> _quantityBoxes = new List<TextBox>();
        private List<NestingProfile> _profiles = new List<NestingProfile>();
        private bool _applyingProfile;

        /// <summary>Desenha de novo a prévia de página/rolo (para mostrar ou esconder o original sem recalcular).</summary>
        private Action? _redraw;

        public NestingWindow(NestingSelection selection, INestingHost? host = null)
        {
            InitializeComponent();
            _host = host;
            _selection = selection;
            UseSelectionButton.IsEnabled = host != null;
            _keyboardRouter = new ThreadKeyboardRouter(this);
            ShowSource();
            BuildQuantities();
            LoadProfiles();
            // Abre com as últimas opções usadas (antes de ligar os eventos: não recalcula a cada campo).
            var last = NestingProfileStore.LoadLast();
            if (last != null) FillFields(last);

            // Ligados depois do InitializeComponent, para não reagir aos valores iniciais do XAML.
            foreach (var field in new[] { RollWidth, RollHeight, AreasText, AreaMargin, Spacing, ContourCell, TimeLimit, BlockGap })
                field.TextChanged += OnInputsChanged;
            UseQuantities.Checked += OnUseQuantitiesChanged;
            UseQuantities.Unchecked += OnUseQuantitiesChanged;
            // Só muda o desenho das folhas: redesenha sem recalcular.
            GroupBlocks.Checked += OnGroupBlocksChanged;
            GroupBlocks.Unchecked += OnGroupBlocksChanged;
            foreach (var toggle in new System.Windows.Controls.Primitives.ToggleButton[]
                         {
                             PageMode, RollMode, AreasMode, AreasAsTyped, AreasSmallestFirst, AreasLargestFirst, ResizePage,
                             OriginTopLeft, OriginTopRight, OriginBottomLeft, OriginBottomRight,
                             BoxMethod, ContourMethod, NoRotation, HalfTurn, QuarterTurns, SearchBest, SplitByLayer,
                         })
            {
                toggle.Checked += OnInputsChanged;
                toggle.Unchecked += OnInputsChanged;
            }

            UpdatePlan();
        }

        /// <summary>Abre a janela com as peças da seleção; já aberta, só troca as peças.</summary>
        public static void ShowFor(NestingSelection selection, INestingHost host)
        {
            if (_open == null)
            {
                _open = new NestingWindow(selection, host);
                _open.Show();
            }
            else
            {
                _open._host = host;
                _open._selection = selection;
                _open.ShowSource();
                _open.BuildQuantities();
                _open.UpdatePlan();
            }

            _open.Activate();
        }

        private void ShowSource()
        {
            SourceText.Text = $"{_selection.Items.Count} peça(s) selecionada(s). Página atual: {Cm(_selection.PageWidth)} × {Cm(_selection.PageHeight)} cm.";
            PageMode.Content = $"Página atual ({Cm(_selection.PageWidth)} × {Cm(_selection.PageHeight)} cm)";
        }

        /// <summary>Uma linha por peça selecionada: quantidade e nome. Só com "Quantidade por peça" ligado —
        /// desligado, a lista nem é montada (centenas de peças deixariam a janela pesada).</summary>
        private void BuildQuantities()
        {
            QuantityList.Children.Clear();
            _quantityBoxes.Clear();
            QuantityPanel.Visibility = UseQuantities.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            if (UseQuantities.IsChecked != true) return;
            foreach (var item in _selection.Items)
            {
                var box = new TextBox { Text = "1", Width = 48, Margin = new Thickness(0, 1, 6, 1) };
                box.TextChanged += OnInputsChanged;
                _quantityBoxes.Add(box);
                var row = new DockPanel();
                DockPanel.SetDock(box, Dock.Left);
                row.Children.Add(box);
                row.Children.Add(new TextBlock { Text = item.Name, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0) });
                QuantityList.Children.Add(row);
            }
        }

        private void OnQuantityAll(object sender, RoutedEventArgs e)
        {
            foreach (var box in _quantityBoxes) box.Text = QuantityAll.Text.Trim();
        }

        private void OnUseQuantitiesChanged(object sender, RoutedEventArgs e)
        {
            BuildQuantities();
            UpdatePlan();
        }

        private void OnGroupBlocksChanged(object sender, RoutedEventArgs e)
        {
            Legends.IsEnabled = GroupBlocks.IsEnabled && GroupBlocks.IsChecked == true;
            if (_applyingProfile) return;
            PreviewCanvas.Children.Clear();
            _redraw?.Invoke();
        }

        /// <summary>Folhas do rolo com "Agrupar por bloco": lado a lado (o cálculo empilha). Áreas digitadas já
        /// ficam lado a lado.</summary>
        private AreaNestResult<TResult, TItem> Laid<TResult, TItem>(AreaNestResult<TResult, TItem> result) =>
            BySheets && GroupBlocks.IsChecked == true ? NestAreas.SideBySide(result, _blockGap) : result;

        /// <summary>Distância entre blocos do último cálculo (o desenho e o aplicar usam a mesma).</summary>
        private double _blockGap = NestAreas.GapMm;

        private static double Gap(TextBox field)
        {
            var gap = Length(field);
            return gap >= 0 && gap <= NestAreas.MaximumGapMm
                ? gap
                : throw new ArgumentException($"Distância entre os blocos: use de 0 a {(NestAreas.MaximumGapMm / 10).ToString("0", DisplayCulture)} cm.");
        }

        /// <summary>Desligado: 1 de cada.</summary>
        private IReadOnlyList<int> Quantities() => UseQuantities.IsChecked != true
            ? _selection.Items.Select(_ => 1).ToList()
            : _quantityBoxes.Select(box =>
                int.TryParse(box.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value <= NestQuantities.MaximumPerPiece
                    ? value
                    : throw new ArgumentException($"Quantidade: use números inteiros de 0 a {NestQuantities.MaximumPerPiece}.")).ToList();

        private static string Cm(double millimeters) => (millimeters / 10).ToString("0.##", DisplayCulture);

        private static double Length(TextBox field)
        {
            var text = field.Text.Trim().Replace(',', '.');
            if (!double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value))
                throw new ArgumentException("Preencha as medidas com números, usando vírgula ou ponto decimal, sem separador de milhar.");
            return value * 10;
        }

        private void OnInputsChanged(object sender, RoutedEventArgs e)
        {
            if (!_applyingProfile) UpdatePlan();
        }

        /// <summary>Lê os perfis salvos (de novo antes de salvar/excluir e ao voltar para a janela: podem ter
        /// sido importados nas preferências com a janela aberta). Arquivo ilegível: nenhum perfil, o aviso, e
        /// Salvar/Excluir desligados para não gravar por cima dele.</summary>
        private bool LoadProfiles(bool showStatus = true)
        {
            var (profiles, readable, notice) = NestingProfileStore.Load();
            _profiles = profiles.ToList();
            SaveProfileButton.IsEnabled = DeleteProfileButton.IsEnabled = readable;
            if (notice != null) ProfileMessage.Text = notice;
            else if (showStatus) ProfileMessage.Text = _profiles.Count == 0 ? "Nenhum perfil salvo." : "";
            ShowProfiles();
            return readable;
        }

        private void ShowProfiles()
        {
            ProfileList.Children.Clear();
            foreach (var profile in _profiles)
            {
                var button = new Button { Content = profile.Name, Tag = profile, Margin = new Thickness(0, 4, 6, 0), Padding = new Thickness(8, 3, 8, 3) };
                button.Click += OnUseProfile;
                ProfileList.Children.Add(button);
            }
        }

        private NestingProfile CaptureProfile(string name) => new NestingProfile
        {
            Name = name,
            Area = RollMode.IsChecked == true ? NestingProfileArea.Roll : ByAreas ? NestingProfileArea.Areas : NestingProfileArea.Page,
            RollWidth = RollWidth.Text,
            RollHeight = RollHeight.Text,
            AreasText = AreasText.Text,
            AreaOrder = ChosenAreaOrder,
            DrawAreas = DrawAreas.IsChecked == true,
            GroupBlocks = GroupBlocks.IsChecked == true,
            BlockGap = BlockGap.Text,
            Legends = Legends.IsChecked == true,
            SplitByLayer = SplitByLayer.IsChecked == true,
            ResizePage = ResizePage.IsChecked == true,
            Margin = AreaMargin.Text,
            Spacing = Spacing.Text,
            Origin = ChosenOrigin,
            Method = ByContour ? NestingProfileMethod.Contour : NestingProfileMethod.Box,
            ContourCell = ContourCell.Text,
            Rotation = QuarterTurns.IsChecked == true ? NestingProfileRotation.QuarterTurns
                : HalfTurn.IsChecked == true ? NestingProfileRotation.HalfTurn
                : NestingProfileRotation.None,
            SearchBest = SearchBest.IsChecked == true,
            TimeLimit = TimeLimit.Text,
        };

        /// <summary>Preenche os campos como se fossem digitados e recalcula uma vez só.</summary>
        private void ApplyProfile(NestingProfile profile)
        {
            _applyingProfile = true;
            try
            {
                FillFields(profile);
            }
            finally
            {
                _applyingProfile = false;
            }

            UpdatePlan();
        }

        /// <summary>Os campos do perfil, sem recalcular (quem chama decide).</summary>
        private void FillFields(NestingProfile profile)
        {
            (profile.Area == NestingProfileArea.Roll ? RollMode : profile.Area == NestingProfileArea.Areas ? AreasMode : PageMode).IsChecked = true;
            RollWidth.Text = profile.RollWidth;
            RollHeight.Text = profile.RollHeight;
            AreasText.Text = profile.AreasText;
            (profile.AreaOrder == NestAreaOrder.SmallestFirst ? AreasSmallestFirst
                : profile.AreaOrder == NestAreaOrder.LargestFirst ? AreasLargestFirst
                : AreasAsTyped).IsChecked = true;
            DrawAreas.IsChecked = profile.DrawAreas;
            GroupBlocks.IsChecked = profile.GroupBlocks;
            BlockGap.Text = profile.BlockGap;
            Legends.IsChecked = profile.Legends;
            SplitByLayer.IsChecked = profile.SplitByLayer;
            ResizePage.IsChecked = profile.ResizePage;
            AreaMargin.Text = profile.Margin;
            Spacing.Text = profile.Spacing;
            (profile.Origin == NestOrigin.TopRight ? OriginTopRight
                : profile.Origin == NestOrigin.BottomLeft ? OriginBottomLeft
                : profile.Origin == NestOrigin.BottomRight ? OriginBottomRight
                : OriginTopLeft).IsChecked = true;
            (profile.Method == NestingProfileMethod.Contour ? ContourMethod : BoxMethod).IsChecked = true;
            ContourCell.Text = profile.ContourCell;
            (profile.Rotation == NestingProfileRotation.QuarterTurns ? QuarterTurns
                : profile.Rotation == NestingProfileRotation.HalfTurn ? HalfTurn
                : NoRotation).IsChecked = true;
            SearchBest.IsChecked = profile.SearchBest;
            TimeLimit.Text = profile.TimeLimit;
        }

        private void OnUseProfile(object sender, RoutedEventArgs e)
        {
            if (!(((Button)sender).Tag is NestingProfile profile)) return;
            ApplyProfile(profile);
            ProfileName.Text = profile.Name;
            ProfileMessage.Text = $"Perfil “{profile.Name}” aplicado.";
        }

        private void OnSaveProfile(object sender, RoutedEventArgs e)
        {
            var name = ProfileName.Text.Trim();
            if (name.Length == 0)
            {
                ProfileMessage.Text = "Digite o nome do perfil ao lado de “Salvar”.";
                return;
            }

            if (!LoadProfiles(showStatus: false)) return;
            var updated = _profiles.ToList();
            var at = updated.FindIndex(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (at >= 0) updated[at] = CaptureProfile(name);
            else updated.Add(CaptureProfile(name));
            if (TryStoreProfiles(updated))
                ProfileMessage.Text = at >= 0 ? $"Perfil “{name}” atualizado." : $"Perfil “{name}” salvo.";
        }

        private void OnDeleteProfile(object sender, RoutedEventArgs e)
        {
            var name = ProfileName.Text.Trim();
            if (!LoadProfiles(showStatus: false)) return;
            var at = _profiles.FindIndex(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (at < 0)
            {
                ProfileMessage.Text = name.Length == 0 ? "Digite o nome do perfil que quer excluir." : $"Nenhum perfil chamado “{name}”.";
                return;
            }

            var updated = _profiles.ToList();
            updated.RemoveAt(at);
            if (TryStoreProfiles(updated))
            {
                ProfileName.Clear();
                ProfileMessage.Text = $"Perfil “{name}” excluído.";
            }
        }

        private bool TryStoreProfiles(List<NestingProfile> profiles)
        {
            var problems = NestingProfile.Validate(profiles);
            if (problems.Count > 0)
            {
                ProfileMessage.Text = problems[0];
                return false;
            }

            try
            {
                NestingProfileStore.Save(profiles);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                ProfileMessage.Text = $"Não foi possível salvar os perfis: {ex.Message}";
                return false;
            }

            _profiles = profiles;
            ShowProfiles();
            return true;
        }

        private void OnCalculate(object sender, RoutedEventArgs e) => UpdatePlan(calculate: true);

        private bool ByContour => ContourMethod.IsChecked == true;

        private bool ByAreas => AreasMode.IsChecked == true;

        /// <summary>Rolo com altura de folha preenchida (vazio = comprimento livre).</summary>
        private bool BySheets => RollMode.IsChecked == true && RollHeight.Text.Trim().Length > 0;

        private bool ByLayer => RollMode.IsChecked == true && SplitByLayer.IsChecked == true;

        /// <summary>Camada de cada item (já com as quantidades: <paramref name="sources"/> diz de qual peça vem).
        /// Sem as camadas lidas (janela aberta sem o painel), um grupo só.</summary>
        private Func<int, string> LayerOf(IReadOnlyList<int> sources)
        {
            var layers = _selection.Layers;
            return index => layers == null ? "" : layers[sources[index]];
        }

        private NestAreaOrder ChosenAreaOrder =>
            AreasSmallestFirst.IsChecked == true ? NestAreaOrder.SmallestFirst
            : AreasLargestFirst.IsChecked == true ? NestAreaOrder.LargestFirst
            : NestAreaOrder.AsTyped;

        private NestOrigin ChosenOrigin =>
            OriginTopRight.IsChecked == true ? NestOrigin.TopRight
            : OriginBottomLeft.IsChecked == true ? NestOrigin.BottomLeft
            : OriginBottomRight.IsChecked == true ? NestOrigin.BottomRight
            : NestOrigin.TopLeft;

        /// <summary>As áreas digitadas, na ordem de preenchimento escolhida.</summary>
        private IReadOnlyList<NestArea> ChosenAreas() => NestAreas.Arrange(NestAreas.Parse(AreasText.Text), ChosenAreaOrder);

        private ContourNester.Rotations ChosenRotations =>
            QuarterTurns.IsChecked == true ? ContourNester.Rotations.QuarterTurns
            : HalfTurn.IsChecked == true ? ContourNester.Rotations.HalfTurn
            : ContourNester.Rotations.None;

        private void Window_Loaded(object sender, RoutedEventArgs e) => _keyboardRouter.Install();

        private void Window_Activated(object sender, EventArgs e)
        {
            _keyboardRouter.ReassertFocus();
            LoadProfiles(showStatus: false);
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            try
            {
                NestingProfileStore.SaveLast(CaptureProfile("Última"));
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                // Só uma conveniência: sem gravar, a próxima janela abre com os valores iniciais.
            }

            _keyboardRouter.Uninstall();
            _generation++;
            _calculation?.Cancel();
            if (ReferenceEquals(_open, this)) _open = null;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        private void OnUseSelection(object sender, RoutedEventArgs e)
        {
            if (_host == null)
            {
                MessageText.Text = "Abra pelo botão do painel para ler a seleção.";
                return;
            }

            var selection = _host.ReadSelection(out var message);
            if (selection == null)
            {
                MessageText.Text = message;
                return;
            }

            _selection = selection;
            ShowSource();
            BuildQuantities();
            UpdatePlan();
        }

        /// <param name="calculate">Pelo contorno, só calcula quando true (botão); pela caixa, sempre.</param>
        private void UpdatePlan(bool calculate = false)
        {
            // Entrada inválida apaga o resultado anterior, para não copiar nem aplicar um plano desatualizado.
            StopCalculation();
            _result = null;
            _contourResult = null;
            _areasResult = null;
            _contourAreasResult = null;
            CopyButton.IsEnabled = ApplyButton.IsEnabled = false;
            ReportText.Clear();
            PreviewCanvas.Children.Clear();
            _redraw = null;
            var roll = RollMode.IsChecked == true;
            var areas = ByAreas;
            var sheets = BySheets;
            var split = ByLayer;
            ShowOriginal.IsEnabled = _selection.Origins != null && !areas && !sheets && !split;
            RollWidth.IsEnabled = RollHeight.IsEnabled = SplitByLayer.IsEnabled = roll;
            ResizePage.IsEnabled = roll || areas;
            AreasText.IsEnabled = AreasAsTyped.IsEnabled = AreasSmallestFirst.IsEnabled = AreasLargestFirst.IsEnabled = areas;
            DrawAreas.IsEnabled = GroupBlocks.IsEnabled = BlockGap.IsEnabled = areas || sheets || split;
            Legends.IsEnabled = GroupBlocks.IsEnabled && GroupBlocks.IsChecked == true;
            ContourCell.IsEnabled = HalfTurn.IsEnabled = ByContour;
            // Folhas são áreas fechadas: qualquer canto. Rolo livre: só os de cima.
            OriginBottomLeft.IsEnabled = OriginBottomRight.IsEnabled = !roll || sheets;
            if (ByContour)
            {
                if (calculate) CalculateContour(roll);
                else MessageText.Text = "Pelo contorno: clique em “Calcular encaixe” (pode levar alguns segundos).";
                return;
            }

            try
            {
                var quantities = Quantities();
                var (items, sources) = NestQuantities.Expand(_selection.Items, quantities);
                var search = SearchBest.IsChecked == true;
                var orders = search ? NestOrders.All : new[] { NestOrder.LongestSide };
                var limit = TimeLimitValue();
                var margin = Length(AreaMargin);
                var spacing = Length(Spacing);
                var turns = QuarterTurns.IsChecked == true;
                var origin = ChosenOrigin;
                if (areas || sheets || split)
                {
                    var gap = Gap(BlockGap);
                    NestSearchResult<AreaNestResult<NestResult, NestItem>> found;
                    if (split)
                    {
                        var rollWidth = Length(RollWidth);
                        double? sheetHeight = sheets ? SheetHeight() : (double?)null;
                        var layerOf = LayerOf(sources);
                        found = NestSearch.Best(orders, (order, _) => NestAreas.ByGroup(items, item => layerOf(item.Index),
                                group => sheetHeight != null
                                    ? NestAreas.Sheets(group, rollWidth, sheetHeight.Value, margin, spacing, turns, order, origin)
                                    : NestAreas.Roll(RectangleNester.Nest(group, rollWidth, null, margin, spacing, turns, order, origin)),
                                item => item.Index, gap),
                            NestScore.Of, limit);
                    }
                    else if (sheets)
                    {
                        var (sheetWidth, sheetHeight) = (Length(RollWidth), SheetHeight());
                        found = NestSearch.Best(orders, (order, _) => NestAreas.Sheets(items, sheetWidth, sheetHeight, margin, spacing, turns, order, origin),
                            NestScore.Of, limit);
                    }
                    else
                    {
                        var areaList = ChosenAreas();
                        found = NestSearch.Best(orders, (order, _) => NestAreas.Nest(items, areaList, margin, spacing, turns, order, origin, gap),
                            NestScore.Of, limit);
                    }

                    var areaResult = found.Result;
                    _blockGap = gap;
                    _sources = sources;
                    _areasResult = areaResult;
                    _redraw = () =>
                    {
                        var laid = Laid(areaResult);
                        var frames = AreaFrames(laid.Areas, laid.Lefts, laid.Tops, laid.TotalWidth, laid.TotalHeight);
                        foreach (var fill in laid.Fills) DrawBoxes(fill.Result, fill.Left, frames, fill.Top);
                    };
                    _redraw();
                    ReportText.Text = WithSearch(AreasReport(areaResult, sheets || split ? NestAreaOrder.AsTyped : ChosenAreaOrder, origin, sheets, split), search ? found : null);
                    var placedCount = areaResult.Fills.Sum(f => f.Result.Placements.Count);
                    ShowOutcome(placedCount, areaResult.Unplaced.Count, "Prévia pronta.");
                    return;
                }

                var width = roll ? Length(RollWidth) : _selection.PageWidth;
                double? height = roll ? (double?)null : _selection.PageHeight;
                var best = NestSearch.Best(orders, (order, _) => RectangleNester.Nest(items, width, height, margin, spacing, turns, order, origin),
                    NestScore.Of, limit);
                var result = best.Result;
                _sources = sources;
                _result = result;
                _redraw = () =>
                {
                    var scale = SingleFrame(result.AreaWidth, result.AreaHeight ?? RollFrameLength(result.UsedLength, result.AreaWidth));
                    DrawBoxes(result, 0, scale);
                    DrawOriginals(scale);
                };
                _redraw();
                var comparison = Comparison(quantities, result.Unplaced.Count,
                    result.Placements.Select(p => (p.Item.Index, p.X, p.Y, p.Width, p.Height)));
                ReportText.Text = WithSearch(WithLine(Report(result, roll, origin), comparison), search ? best : null);
                ShowOutcome(result.Placements.Count, result.Unplaced.Count, "Prévia pronta.");
            }
            catch (ArgumentException ex)
            {
                MessageText.Text = ex.Message;
            }
        }

        private TimeSpan TimeLimitValue() =>
            int.TryParse(TimeLimit.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds >= 1 && seconds <= 600
                ? TimeSpan.FromSeconds(seconds)
                : throw new ArgumentException("Tempo máximo: use de 1 a 600 segundos.");

        /// <summary>Linha da busca logo abaixo do título do relatório (só quando a busca foi pedida).</summary>
        private static string WithSearch<T>(string report, NestSearchResult<T>? search)
        {
            if (search == null) return report;
            var line = $"Busca: {search.Tried} de {search.Total} ordem(ns) testada(s) em {search.Seconds.ToString("0.0", DisplayCulture)} s; melhor: {NestOrders.Name(search.Order)}";
            if (search.TimedOut) line += " (parou no tempo máximo)";
            if (search.Cancelled) line += " (cancelada)";
            return WithLine(report, line);
        }

        /// <summary>Uma linha logo abaixo do título do relatório (nada, se a linha for null).</summary>
        private static string WithLine(string report, string? line)
        {
            if (line == null) return report;
            var firstBreak = report.IndexOf(Environment.NewLine, StringComparison.Ordinal);
            return firstBreak < 0
                ? report + Environment.NewLine + line
                : report.Substring(0, firstBreak + Environment.NewLine.Length) + line + report.Substring(firstBreak);
        }

        /// <summary>Interrompe o cálculo pelo contorno em andamento e descarta o resultado dele.</summary>
        private void StopCalculation()
        {
            _generation++;
            _calculation?.Cancel();
            _calculation = null;
            CancelButton.Visibility = Visibility.Collapsed;
            CalculateButton.IsEnabled = true;
        }

        private void OnCancelCalculation(object sender, RoutedEventArgs e)
        {
            if (_calculation == null) return;
            _calculation.Cancel();
            MessageText.Text = "Cancelando… (fica o melhor resultado já calculado, se houver)";
        }

        /// <summary>
        /// Pelo contorno: lê os contornos (no Corel, só a primeira vez) e calcula em segundo plano, para a
        /// janela e o Corel não travarem. "Cancelar cálculo" interrompe; mudar qualquer campo descarta o cálculo
        /// em andamento (<see cref="_generation"/>).
        /// </summary>
        private void CalculateContour(bool roll)
        {
            try
            {
                var width = roll ? Length(RollWidth) : _selection.PageWidth;
                double? height = roll ? (double?)null : _selection.PageHeight;
                var margin = Length(AreaMargin);
                var spacing = Length(Spacing);
                var cell = Length(ContourCell) / 10; // o campo já é em mm
                var limit = TimeLimitValue();
                var areaList = ByAreas ? ChosenAreas() : null;
                double? sheetHeight = roll && BySheets ? SheetHeight() : (double?)null;
                var split = ByLayer;
                var gap = areaList != null || sheetHeight != null || split ? Gap(BlockGap) : NestAreas.GapMm;
                var areaOrder = sheetHeight != null || split ? NestAreaOrder.AsTyped : ChosenAreaOrder;
                var search = SearchBest.IsChecked == true;
                var orders = search ? NestOrders.All : new[] { NestOrder.Area };
                var rotations = ChosenRotations;
                var origin = ChosenOrigin;
                var quantities = Quantities();
                if (_selection.Contours == null)
                {
                    if (_host == null)
                    {
                        MessageText.Text = "Abra pelo botão do painel para ler os contornos.";
                        return;
                    }

                    _selection.Contours = _host.ReadContours(out var message);
                    if (_selection.Contours == null)
                    {
                        MessageText.Text = message;
                        return;
                    }
                }

                var reading = _selection.Contours;
                var (pieces, sources) = NestQuantities.Expand(reading.Pieces, quantities);
                var layerOf = LayerOf(sources);
                var generation = _generation;
                var calculation = new CancellationTokenSource();
                _calculation = calculation;
                CalculateButton.IsEnabled = false;
                CancelButton.Visibility = Visibility.Visible;
                MessageText.Text = "Calculando o encaixe pelo contorno…";

                void Post(Action action) => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (generation != _generation) return; // campo mudou ou outro cálculo começou
                    _calculation = null;
                    CancelButton.Visibility = Visibility.Collapsed;
                    CalculateButton.IsEnabled = true;
                    action();
                }));

                void Progress(int k, int total) => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (generation == _generation && total > 1 && !calculation.IsCancellationRequested)
                        MessageText.Text = $"Calculando o encaixe pelo contorno… ordem {k} de {total}";
                }));

                Task.Run(() =>
                {
                    try
                    {
                        if (areaList != null || sheetHeight != null || split)
                        {
                            AreaNestResult<ContourNestResult, ContourPiece> Rolls(IReadOnlyList<ContourPiece> group, NestOrder order, CancellationToken cancel) =>
                                sheetHeight != null
                                    ? NestAreas.Sheets(group, width, sheetHeight.Value, margin, spacing, rotations, cell, order, cancel, origin)
                                    : NestAreas.Roll(ContourNester.Nest(group, width, null, margin, spacing, rotations, cell, order, cancel, origin));
                            var found = NestSearch.Best(orders,
                                (order, cancel) => split
                                    ? NestAreas.ByGroup(pieces, piece => layerOf(piece.Index), group => Rolls(group, order, cancel), piece => piece.Index, gap)
                                    : sheetHeight != null
                                        ? Rolls(pieces, order, cancel)
                                        : NestAreas.Nest(pieces, areaList!, margin, spacing, rotations, cell, order, cancel, origin, gap),
                                NestScore.Of, limit, calculation.Token, Progress);
                            var problems = found.Result.Fills
                                .SelectMany(f => ContourNester.Verify(f.Result, margin, spacing).Select(problem => $"{f.Area.Name}: {problem}"))
                                .ToList();
                            Post(() =>
                            {
                                _sources = sources;
                                _contourMargin = margin;
                                _contourSpacing = spacing;
                                _blockGap = gap;
                                _contourAreasResult = found.Result;
                                _redraw = () =>
                                {
                                    var laid = Laid(found.Result);
                                    var frames = AreaFrames(laid.Areas, laid.Lefts, laid.Tops, laid.TotalWidth, laid.TotalHeight);
                                    foreach (var fill in laid.Fills) DrawContours(fill.Result, fill.Left, frames, fill.Top);
                                };
                                _redraw();
                                ReportText.Text = WithSearch(ContourAreasReport(found.Result, areaOrder, origin, reading, found.Seconds, sheetHeight != null, split), search ? found : null);
                                ShowOutcome(found.Result.Fills.Sum(f => f.Result.Placements.Count), found.Result.Unplaced.Count,
                                    "Prévia pronta (conferida: sem sobreposição e com a distância pedida).", problems);
                            });
                        }
                        else
                        {
                            var found = NestSearch.Best(orders,
                                (order, cancel) => ContourNester.Nest(pieces, width, height, margin, spacing, rotations, cell, order, cancel, origin),
                                NestScore.Of, limit, calculation.Token, Progress);
                            var problems = ContourNester.Verify(found.Result, margin, spacing);
                            Post(() =>
                            {
                                var result = found.Result;
                                _sources = sources;
                                _contourMargin = margin;
                                _contourSpacing = spacing;
                                _contourResult = result;
                                _redraw = () =>
                                {
                                    var scale = SingleFrame(result.AreaWidth, result.AreaHeight ?? RollFrameLength(result.UsedLength, result.AreaWidth));
                                    DrawContours(result, 0, scale);
                                    DrawOriginals(scale);
                                };
                                _redraw();
                                var comparison = Comparison(quantities, result.Unplaced.Count,
                                    result.Placements.Select(p => (p.Piece.Index, p.X, p.Y, p.Width, p.Height)));
                                ReportText.Text = WithSearch(WithLine(ContourReport(result, roll, origin, reading, found.Seconds), comparison), search ? found : null);
                                ShowOutcome(result.Placements.Count, result.Unplaced.Count,
                                    "Prévia pronta (conferida: sem sobreposição e com a distância pedida).", problems);
                            });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Post(() => MessageText.Text = "Cálculo cancelado: nenhum resultado.");
                    }
                    catch (ArgumentException ex)
                    {
                        Post(() => MessageText.Text = ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Post(() => MessageText.Text = $"Falha no cálculo do encaixe: {ex.Message}");
                    }
                });
            }
            catch (ArgumentException ex)
            {
                MessageText.Text = ex.Message;
            }
        }

        private static string ContourReport(ContourNestResult result, bool roll, NestOrigin origin, ContourReading reading, double seconds)
        {
            var report = new StringBuilder();
            report.AppendLine("PX Tool Box DEV — encaixe pelo contorno");
            AppendOrigin(report, origin);
            report.AppendLine(roll
                ? $"Rolo de {Cm(result.AreaWidth)} cm: comprimento usado {Cm(result.UsedLength)} cm"
                : $"Página {Cm(result.AreaWidth)} × {Cm(result.AreaHeight!.Value)} cm: altura usada {Cm(result.UsedLength)} cm");
            report.AppendLine($"Aproveitamento: ~{result.Utilization.ToString("P0", DisplayCulture)} (área das peças medida na grade ÷ largura × comprimento usado)");
            report.AppendLine($"Precisão: {result.Cell.ToString("0.##", DisplayCulture)} mm; cálculo em {seconds.ToString("0.0", DisplayCulture)} s");
            report.AppendLine($"{result.Placements.Count} peça(s) encaixada(s), {result.Placements.Count(p => p.Rotation != 0)} girada(s)");
            if (reading.BoxFallbacks.Count > 0)
                report.AppendLine($"Pela caixa (texto, imagem ou contorno ilegível): {string.Join(", ", reading.BoxFallbacks)}");
            if (result.Unplaced.Count > 0)
                report.AppendLine($"Não couberam: {string.Join(", ", result.Unplaced.Select(p => $"{p.Name} ({Cm(p.Width)} × {Cm(p.Height)} cm)"))}");
            report.AppendLine("X e Y da caixa do contorno, a partir do canto superior esquerdo, em cm.");
            foreach (var p in result.Placements)
                report.AppendLine($"{p.Piece.Name}: X {Cm(p.X)}; Y {Cm(p.Y)}; {Cm(p.Width)} × {Cm(p.Height)} cm{(p.Rotation != 0 ? $" (girada {p.Rotation}°)" : "")}");
            return report.ToString();
        }

        /// <summary>Nada coube: diz o tamanho da maior peça e, na página, que o rolo ou as áreas resolvem.</summary>
        private string NothingFits()
        {
            var where = ByAreas ? "nas áreas" : BySheets ? "na folha do rolo" : RollMode.IsChecked == true ? "na largura do rolo"
                : $"na página atual ({Cm(_selection.PageWidth)} × {Cm(_selection.PageHeight)} cm)";
            var biggest = _selection.Items.OrderByDescending(i => i.Width * i.Height).FirstOrDefault();
            var size = biggest == null ? "" : $": a maior peça tem {Cm(biggest.Width)} × {Cm(biggest.Height)} cm";
            var hint = PageMode.IsChecked == true ? ". Use “Rolo” ou “Várias áreas”, ou aumente a página." : ".";
            return $"Nenhuma peça coube {where}{size}{hint}";
        }

        private void ShowOutcome(int placed, int unplaced, string ready, IReadOnlyList<string>? problems = null)
        {
            var failed = problems != null && problems.Count > 0;
            CopyButton.IsEnabled = true;
            ApplyButton.IsEnabled = _host != null && placed > 0 && !failed;
            MessageText.Text = failed
                ? $"A conferência do encaixe encontrou problema — não aplique: {string.Join(" ", problems!.Take(3))}"
                : placed == 0
                    ? NothingFits()
                    : unplaced > 0
                        ? $"{unplaced} peça(s) não couberam e ficarão onde estão."
                        : ready;
        }

        private static string AreasReport(AreaNestResult<NestResult, NestItem> result, NestAreaOrder areaOrder, NestOrigin origin, bool sheets = false,
            bool split = false)
        {
            var report = new StringBuilder();
            report.AppendLine(Title("PX Tool Box DEV — encaixe retangular", sheets, split));
            AppendSheets(report, result.Areas, sheets && !split);
            AppendAreaOrder(report, areaOrder);
            AppendOrigin(report, origin);
            AppendAreas(report, result.Areas, result.Fills.Select(f => (f.Area, f.Result.Placements.Count,
                f.Result.Placements.Sum(p => p.Width * p.Height))), "");
            report.AppendLine($"{result.Fills.Sum(f => f.Result.Placements.Count)} peça(s) encaixada(s), {result.Fills.Sum(f => f.Result.Placements.Count(p => p.Rotated))} girada(s) 90°");
            if (result.Unplaced.Count > 0)
                report.AppendLine($"Não couberam em nenhuma área: {string.Join(", ", result.Unplaced.Select(i => $"{i.Name} ({Cm(i.Width)} × {Cm(i.Height)} cm)"))}");
            report.AppendLine("X e Y a partir do canto superior esquerdo de cada área, em cm.");
            foreach (var fill in result.Fills)
                foreach (var p in fill.Result.Placements)
                    report.AppendLine($"{fill.Area.Name} — {p.Item.Name}: X {Cm(p.X)}; Y {Cm(p.Y)}; {Cm(p.Width)} × {Cm(p.Height)} cm{(p.Rotated ? " (girada)" : "")}");
            return report.ToString();
        }

        private static string ContourAreasReport(AreaNestResult<ContourNestResult, ContourPiece> result, NestAreaOrder areaOrder,
            NestOrigin origin, ContourReading reading, double seconds, bool sheets = false, bool split = false)
        {
            var report = new StringBuilder();
            report.AppendLine(Title("PX Tool Box DEV — encaixe pelo contorno", sheets, split));
            AppendSheets(report, result.Areas, sheets && !split);
            AppendAreaOrder(report, areaOrder);
            AppendOrigin(report, origin);
            AppendAreas(report, result.Areas, result.Fills.Select(f => (f.Area, f.Result.Placements.Count, f.Result.OccupiedArea)), "~");
            report.AppendLine($"Cálculo em {seconds.ToString("0.0", DisplayCulture)} s");
            report.AppendLine($"{result.Fills.Sum(f => f.Result.Placements.Count)} peça(s) encaixada(s), {result.Fills.Sum(f => f.Result.Placements.Count(p => p.Rotation != 0))} girada(s)");
            if (reading.BoxFallbacks.Count > 0)
                report.AppendLine($"Pela caixa (texto, imagem ou contorno ilegível): {string.Join(", ", reading.BoxFallbacks)}");
            if (result.Unplaced.Count > 0)
                report.AppendLine($"Não couberam em nenhuma área: {string.Join(", ", result.Unplaced.Select(p => $"{p.Name} ({Cm(p.Width)} × {Cm(p.Height)} cm)"))}");
            report.AppendLine("X e Y da caixa do contorno, a partir do canto superior esquerdo de cada área, em cm.");
            foreach (var fill in result.Fills)
                foreach (var p in fill.Result.Placements)
                    report.AppendLine($"{fill.Area.Name} — {p.Piece.Name}: X {Cm(p.X)}; Y {Cm(p.Y)}; {Cm(p.Width)} × {Cm(p.Height)} cm{(p.Rotation != 0 ? $" (girada {p.Rotation}°)" : "")}");
            return report.ToString();
        }

        /// <summary>Título do relatório de várias áreas, rolo em folhas ou rolo separado por camada.</summary>
        private static string Title(string kind, bool sheets, bool split) =>
            split ? $"{kind} no rolo, separado por camada{(sheets ? ", em folhas" : "")}"
            : sheets ? $"{kind} no rolo, em folhas"
            : $"{kind} em várias áreas";

        /// <summary>Rolo em folhas: quantas folhas e o comprimento total (sem espaço entre elas).</summary>
        private static void AppendSheets(StringBuilder report, IReadOnlyList<NestArea> sheets, bool bySheets)
        {
            if (!bySheets) return;
            report.AppendLine(sheets.Count == 0
                ? "Nenhuma folha usada"
                : $"Rolo de {Cm(sheets[0].Width)} cm em folhas de {Cm(sheets[0].Height)} cm: {sheets.Count} folha(s), comprimento total {Cm(sheets.Count * sheets[0].Height)} cm");
        }

        /// <summary>Altura da folha do rolo, em mm (o campo é em cm e precisa ser maior que zero).</summary>
        private double SheetHeight()
        {
            var height = Length(RollHeight);
            if (height <= 0) throw new ArgumentException("Altura da folha: use um número maior que zero (ou deixe vazio para comprimento livre).");
            return height;
        }

        /// <summary>Rolo de comprimento livre na prévia: pelo menos tão alto quanto largo, para ficar em pé (o rolo
        /// desce) mesmo quando as peças usam pouco comprimento.</summary>
        private static double RollFrameLength(double usedLength, double width) => Math.Max(usedLength, width);

        private static void AppendAreaOrder(StringBuilder report, NestAreaOrder areaOrder)
        {
            if (areaOrder != NestAreaOrder.AsTyped) report.AppendLine($"Áreas preenchidas: {NestAreas.Name(areaOrder)}");
        }

        private static void AppendOrigin(StringBuilder report, NestOrigin origin)
        {
            if (origin != NestOrigin.TopLeft) report.AppendLine($"Começa pelo {NestOrigins.Name(origin)}");
        }

        /// <summary>Uma linha por área: peças e aproveitamento (área das peças ÷ área inteira), ou "não usada".</summary>
        private static void AppendAreas(StringBuilder report, IReadOnlyList<NestArea> areas,
            IEnumerable<(NestArea Area, int Count, double PiecesArea)> fills, string approximately)
        {
            var used = fills.ToDictionary(f => f.Area);
            foreach (var area in areas)
            {
                var size = $"{area.Name} ({Cm(area.Width)} × {Cm(area.Height)} cm)";
                report.AppendLine(used.TryGetValue(area, out var fill)
                    ? $"{size}: {fill.Count} peça(s), aproveitamento {approximately}{(fill.PiecesArea / (area.Width * area.Height)).ToString(approximately.Length > 0 ? "P0" : "P1", DisplayCulture)}"
                    : $"{size}: não usada");
            }
        }

        /// <summary>Prepara o desenho das áreas (contorno cinza de cada uma: lado a lado, ou folhas uma embaixo da
        /// outra) e devolve a escala.</summary>
        private Func<double, double> AreaFrames(IReadOnlyList<NestArea> areas, IReadOnlyList<double> lefts, IReadOnlyList<double> tops,
            double totalWidth, double totalHeight)
        {
            var longest = Math.Max(1, Math.Max(totalWidth, totalHeight));
            double Scale(double mm) => mm / longest * 400;
            PreviewCanvas.Width = Scale(totalWidth);
            PreviewCanvas.Height = Scale(totalHeight);
            for (var k = 0; k < areas.Count; k++)
            {
                var frame = new Rectangle
                {
                    Width = Scale(areas[k].Width),
                    Height = Scale(areas[k].Height),
                    Stroke = new SolidColorBrush(Color.FromRgb(110, 110, 118)),
                    StrokeThickness = 0.8,
                    ToolTip = $"{areas[k].Name}: {Cm(areas[k].Width)} × {Cm(areas[k].Height)} cm",
                };
                Canvas.SetLeft(frame, Scale(lefts[k]));
                Canvas.SetTop(frame, Scale(tops[k]));
                PreviewCanvas.Children.Add(frame);
            }

            return Scale;
        }

        private Func<double, double> SingleFrame(double width, double height) =>
            AreaFrames(new[] { new NestArea("", width, height) }, new[] { 0.0 }, new[] { 0.0 }, width, height);

        private void DrawContours(ContourNestResult result, double left, Func<double, double> scale, double top = 0)
        {
            Point At(NestPoint q) => new Point(scale(left + q.X), scale(top + q.Y));
            foreach (var p in result.Placements)
            {
                var color = p.Rotation != 0 ? Color.FromRgb(224, 163, 62) : Color.FromRgb(77, 163, 255);
                foreach (var shape in p.Shapes)
                {
                    // Par/ímpar por objeto, como no cálculo: o furo aparece vazio.
                    var geometry = new PathGeometry { FillRule = FillRule.EvenOdd };
                    foreach (var ring in shape)
                    {
                        var figure = new PathFigure { StartPoint = At(ring[0]), IsClosed = true };
                        figure.Segments.Add(new PolyLineSegment(ring.Skip(1).Select(At), true));
                        geometry.Figures.Add(figure);
                    }

                    PreviewCanvas.Children.Add(new Path
                    {
                        Data = geometry,
                        Stroke = new SolidColorBrush(color),
                        StrokeThickness = 0.6,
                        Fill = new SolidColorBrush(Color.FromArgb(45, color.R, color.G, color.B)),
                        ToolTip = $"{p.Piece.Name}{(p.Rotation != 0 ? $" (girada {p.Rotation}°)" : "")}",
                    });
                }
            }
        }

        /// <summary>"Comparar com o original": caixa ocupada hoje × no encaixe. Null sem as posições (janela
        /// aberta sem o painel).</summary>
        private string? Comparison(IReadOnlyList<int> quantities, int unplaced,
            IEnumerable<(int Index, double X, double Y, double Width, double Height)> placed)
        {
            if (_selection.Origins == null) return null;
            const string title = "Comparar com o original: ";
            if (quantities.Any(q => q != 1) || unplaced > 0) return title + "só quando cada peça entra uma vez e todas cabem.";
            var boxes = placed.OrderBy(p => p.Index).Select(p => (p.X, p.Y, p.Width, p.Height)).ToList();
            var comparison = NestComparison.Of(_selection.Items, _selection.Origins, boxes, out var reason);
            if (comparison == null) return title + reason;
            var change = Math.Abs(comparison.AreaChange) < 0.0005 ? "do mesmo tamanho"
                : $"{Math.Abs(comparison.AreaChange).ToString("P0", DisplayCulture)} {(comparison.AreaChange < 0 ? "menor" : "maior")}";
            return $"Original: as peças ocupam {Cm(comparison.OriginalWidth)} × {Cm(comparison.OriginalHeight)} cm " +
                $"(aproveitamento {comparison.OriginalUtilization.ToString("P1", DisplayCulture)}); no encaixe, " +
                $"{Cm(comparison.NestedWidth)} × {Cm(comparison.NestedHeight)} cm " +
                $"(aproveitamento {comparison.NestedUtilization.ToString("P1", DisplayCulture)}) — caixa {change}";
        }

        private void OnShowOriginalChanged(object sender, RoutedEventArgs e)
        {
            PreviewCanvas.Children.Clear();
            _redraw?.Invoke();
        }

        /// <summary>Posição atual das peças (caixas tracejadas cinza), por cima da prévia. Peças fora da página
        /// aumentam o desenho para os dois caberem.</summary>
        private void DrawOriginals(Func<double, double> scale)
        {
            var origins = _selection.Origins;
            if (ShowOriginal.IsChecked != true || origins == null || origins.Count != _selection.Items.Count) return;
            var minX = Math.Min(0, origins.Min(o => o.X));
            var minY = Math.Min(0, origins.Min(o => o.Y));
            var maxX = origins.Select((o, k) => o.X + _selection.Items[k].Width).Max();
            var maxY = origins.Select((o, k) => o.Y + _selection.Items[k].Height).Max();
            var dx = scale(-minX);
            var dy = scale(-minY);
            foreach (UIElement child in PreviewCanvas.Children)
            {
                var left = Canvas.GetLeft(child);
                var top = Canvas.GetTop(child);
                Canvas.SetLeft(child, (double.IsNaN(left) ? 0 : left) + dx);
                Canvas.SetTop(child, (double.IsNaN(top) ? 0 : top) + dy);
            }

            PreviewCanvas.Width = Math.Max(PreviewCanvas.Width + dx, scale(maxX - minX));
            PreviewCanvas.Height = Math.Max(PreviewCanvas.Height + dy, scale(maxY - minY));
            var gray = new SolidColorBrush(Color.FromRgb(170, 170, 178));
            for (var k = 0; k < origins.Count; k++)
            {
                var item = _selection.Items[k];
                var box = new Rectangle
                {
                    Width = scale(item.Width),
                    Height = scale(item.Height),
                    Stroke = gray,
                    StrokeThickness = 0.8,
                    StrokeDashArray = new DoubleCollection { 3, 2 },
                    ToolTip = $"{item.Name}: posição original",
                    Tag = "original",
                };
                Canvas.SetLeft(box, scale(origins[k].X) + dx);
                Canvas.SetTop(box, scale(origins[k].Y) + dy);
                PreviewCanvas.Children.Add(box);
            }
        }


        private static string Report(NestResult result, bool roll, NestOrigin origin)
        {
            var report = new StringBuilder();
            report.AppendLine("PX Tool Box DEV — encaixe retangular");
            AppendOrigin(report, origin);
            report.AppendLine(roll
                ? $"Rolo de {Cm(result.AreaWidth)} cm: comprimento usado {Cm(result.UsedLength)} cm"
                : $"Página {Cm(result.AreaWidth)} × {Cm(result.AreaHeight!.Value)} cm: altura usada {Cm(result.UsedLength)} cm");
            report.AppendLine($"Aproveitamento: {result.Utilization.ToString("P1", DisplayCulture)} (área das peças ÷ largura × comprimento usado)");
            report.AppendLine($"{result.Placements.Count} peça(s) encaixada(s), {result.Placements.Count(p => p.Rotated)} girada(s) 90°");
            if (result.Unplaced.Count > 0)
                report.AppendLine($"Não couberam: {string.Join(", ", result.Unplaced.Select(i => $"{i.Name} ({Cm(i.Width)} × {Cm(i.Height)} cm)"))}");
            report.AppendLine("X e Y a partir do canto superior esquerdo, em cm.");
            foreach (var p in result.Placements)
                report.AppendLine($"{p.Item.Name}: X {Cm(p.X)}; Y {Cm(p.Y)}; {Cm(p.Width)} × {Cm(p.Height)} cm{(p.Rotated ? " (girada)" : "")}");
            return report.ToString();
        }

        private void DrawBoxes(NestResult result, double left, Func<double, double> scale, double top = 0)
        {
            double Scale(double mm) => scale(mm);
            var normal = Color.FromRgb(77, 163, 255);
            var turned = Color.FromRgb(224, 163, 62);
            foreach (var p in result.Placements)
            {
                var color = p.Rotated ? turned : normal;
                var piece = new Rectangle
                {
                    Width = Scale(p.Width),
                    Height = Scale(p.Height),
                    Stroke = new SolidColorBrush(color),
                    StrokeThickness = 0.8,
                    Fill = new SolidColorBrush(Color.FromArgb(45, color.R, color.G, color.B)),
                    ToolTip = $"{p.Item.Name}: {Cm(p.Width)} × {Cm(p.Height)} cm{(p.Rotated ? " (girada)" : "")}",
                };
                Canvas.SetLeft(piece, Scale(left + p.X));
                Canvas.SetTop(piece, Scale(top + p.Y));
                PreviewCanvas.Children.Add(piece);
            }
        }

        private void OnApply(object sender, RoutedEventArgs e)
        {
            if (_host == null) return;
            var resize = ResizePage.IsEnabled && ResizePage.IsChecked == true;
            var draw = DrawAreas.IsChecked == true;
            var group = GroupBlocks.IsChecked == true;
            var legends = group && Legends.IsChecked == true;
            OperationResult? result;
            if (_contourAreasResult != null)
                result = _host.ApplyContourAreas(_selection.Contours!, _sources, Laid(_contourAreasResult), _contourMargin, _contourSpacing, resize, draw, group, legends);
            else if (_areasResult != null)
                result = _host.ApplyAreas(_selection.Items, _sources, Laid(_areasResult), resize, draw, group, legends);
            else if (_contourResult != null)
                result = _host.ApplyContour(_selection.Contours!, _sources, _contourResult, _contourMargin, _contourSpacing, resize);
            else if (_result != null)
                result = _host.Apply(_selection.Items, _sources, _result, resize);
            else
                return;
            if (result == null)
            {
                MessageText.Text = "Não foi possível encaixar: veja a mensagem no painel.";
                return;
            }

            MessageText.Text = result.Message;
            // Feito: as peças já estão no lugar. Mudar um campo ou "Usar seleção atual" libera de novo.
            if (result.IsSuccess) ApplyButton.IsEnabled = false;
        }

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ReportText.Text);
                MessageText.Text = "Relatório copiado.";
            }
            catch (Exception ex)
            {
                MessageText.Text = $"Falha ao copiar o relatório: {ex.Message}";
            }
        }
    }
}
