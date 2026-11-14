using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PXToolBox.Core.Slicing;
using PXToolBoxDev.ListForgeIntegrated;

namespace PXToolBoxDev.Slicing
{
    /// <summary>"Dividir em partes (Slice)": recebe as medidas da seleção (em mm), nunca referências do
    /// documento ou dos objetos; medir de novo e dividir passam pelo <see cref="ISliceHost"/> do painel.
    /// Não modal e uma só (<see cref="ShowFor"/>), como as outras janelas do PX.</summary>
    public partial class SlicePreviewWindow : Window
    {
        private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("pt-BR");
        private static SlicePreviewWindow? _open;
        private readonly ThreadKeyboardRouter _keyboardRouter;
        private ISliceHost? _host;
        private SlicePlan? _plan;
        private SliceRegionPlan? _regionPlan;
        private PXToolBox.Outputs.Tools.SliceGuideCuts? _guideCuts;
        private PXToolBox.Outputs.Tools.SliceDrawnCuts? _drawnCuts;
        private PXToolBox.Outputs.Tools.SliceArtOutline? _outline;
        private PXToolBox.Outputs.Tools.SliceThumbnail? _thumbnail;
        private ImageSource? _thumbnailImage;

        /// <summary>Muitas partes pedem confirmação: cada uma leva uma cópia da arte inteira.</summary>
        internal const int ConfirmAbove = 50;

        public SlicePreviewWindow(double widthMillimeters, double heightMillimeters, ISliceHost? host = null)
        {
            InitializeComponent();
            _host = host;
            UseSelectionButton.IsEnabled = ReadGuidesButton.IsEnabled = CreateGuidesButton.IsEnabled = RemoveGuidesButton.IsEnabled =
                ReadPathsButton.IsEnabled = CreateCutLayerButton.IsEnabled = host != null;
            _keyboardRouter = new ThreadKeyboardRouter(this);
            SourceText.Text = "Medidas da seleção ao abrir. A prévia acompanha os campos abaixo.";
            LoadSize(widthMillimeters, heightMillimeters);

            // Ligados depois do InitializeComponent, para não reagir aos valores iniciais do XAML.
            foreach (var field in new[] { ArtworkWidth, ArtworkHeight, Overlap, Rows, Columns, MaximumWidth, MaximumHeight, Separation })
                field.TextChanged += OnInputsChanged;
            TableMode.Checked += OnInputsChanged;
            SizeMode.Checked += OnInputsChanged;
            GuideMode.Checked += OnGuideModeChecked;
            PathMode.Checked += OnPathModeChecked;
            DeleteOriginal.Checked += OnInputsChanged;
            DeleteOriginal.Unchecked += OnInputsChanged;
            ShowArt.Checked += OnInputsChanged;
            ShowArt.Unchecked += OnInputsChanged;
            ShowColors.Checked += OnShowColorsChecked;
            ShowColors.Unchecked += OnInputsChanged;
            UpdatePlan();
        }

        /// <summary>Abre a janela com as medidas da seleção; já aberta, só troca as medidas.</summary>
        public static void ShowFor(double widthMillimeters, double heightMillimeters, ISliceHost host)
        {
            if (_open == null)
            {
                _open = new SlicePreviewWindow(widthMillimeters, heightMillimeters, host);
                _open.Show();
            }
            else
            {
                _open._host = host;
                _open.LoadSize(widthMillimeters, heightMillimeters);
            }

            _open.ReadOutline();
            _open.ResetThumbnail();
            _open.UpdatePlan();

            _open.Activate();
        }

        private void LoadSize(double widthMillimeters, double heightMillimeters)
        {
            ArtworkWidth.Text = Format(widthMillimeters / 10);
            ArtworkHeight.Text = Format(heightMillimeters / 10);
        }

        private static string Format(double value) => value.ToString("0.########", DisplayCulture);

        private static double Length(TextBox field)
        {
            var text = field.Text.Trim().Replace(',', '.');
            if (!double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value))
                throw new ArgumentException("Preencha as medidas com números, usando vírgula ou ponto decimal, sem separador de milhar.");
            return value * 10;
        }

        private static int Count(TextBox field)
        {
            if (!int.TryParse(field.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                throw new ArgumentException("Linhas e colunas devem ser números inteiros positivos.");
            return value;
        }

        private void OnPreview(object sender, RoutedEventArgs e) => UpdatePlan();

        private void OnUseSelection(object sender, RoutedEventArgs e)
        {
            if (_host == null)
            {
                MessageText.Text = "Abra pelo botão do painel para ler a seleção.";
                return;
            }

            var size = _host.MeasureSelection(out var message);
            if (size == null)
            {
                MessageText.Text = message;
                return;
            }

            SourceText.Text = "Medidas da seleção atual.";
            // As guias foram medidas a partir da seleção anterior: lidas de novo, se estiverem em uso.
            _guideCuts = null;
            _drawnCuts = null;
            LoadSize(size.Value.Width, size.Value.Height);
            if (GuideMode.IsChecked == true) ReadGuides();
            if (PathMode.IsChecked == true) ReadPaths();
            ReadOutline();
            ResetThumbnail();
            UpdatePlan();
        }

        /// <summary>Contorno da arte para a prévia (só leitura). Falha não impede o plano: a prévia fica só
        /// com a caixa e as partes.</summary>
        private void ReadOutline()
        {
            if (_host == null) return;
            _outline = _host.ReadArtOutline(out var message);
            if (_outline == null)
            {
                ArtText.Text = message;
                return;
            }

            ArtText.Text = _outline.Rings.Count == 0
                ? "A arte não tem contorno para mostrar."
                : "Contorno da arte na prévia" + (_outline.Approximate ? " (texto, imagem ou contorno ilegível pela caixa)" : "")
                  + (_outline.Trimmed ? "; arte pesada: só parte do contorno aparece" : "") + ".";
        }

        /// <summary>Seleção nova: a miniatura antiga sai; com "Mostrar a arte em cores" marcado, gera outra.</summary>
        private void ResetThumbnail()
        {
            _thumbnail = null;
            _thumbnailImage = null;
            if (ShowColors.IsChecked == true) ReadThumbnail();
        }

        private void OnShowColorsChecked(object sender, RoutedEventArgs e)
        {
            if (_thumbnail == null) ReadThumbnail();
            UpdatePlan();
        }

        /// <summary>Miniatura com cores (só leitura; o Corel gera um PNG pequeno). Falha só mostra a mensagem.</summary>
        private void ReadThumbnail()
        {
            if (_host == null) return;
            _thumbnail = _host.ReadArtThumbnail(out var message);
            _thumbnailImage = null;
            if (_thumbnail == null)
            {
                ArtText.Text = message;
                return;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = new System.IO.MemoryStream(_thumbnail.Png);
                image.EndInit();
                image.Freeze();
                _thumbnailImage = image;
            }
            catch (Exception ex) when (ex is NotSupportedException || ex is System.IO.IOException || ex is ArgumentException || ex is InvalidOperationException)
            {
                ArtText.Text = $"A miniatura da arte não pôde ser aberta ({ex.Message}).";
            }
        }

        private void OnGuideModeChecked(object sender, RoutedEventArgs e)
        {
            if (_guideCuts == null) ReadGuides();
            UpdatePlan();
        }

        private void OnReadGuides(object sender, RoutedEventArgs e)
        {
            ReadGuides();
            GuideMode.IsChecked = true; // já marcado: UpdatePlan abaixo
            UpdatePlan();
        }

        private void ReadGuides()
        {
            if (_host == null) return;
            _guideCuts = _host.ReadGuides(out var message);
            GuideText.Text = _guideCuts == null
                ? message
                : $"{_guideCuts.Columns.Count} guia(s) vertical(is), {_guideCuts.Rows.Count} horizontal(is) e {_guideCuts.Slanted.Count} inclinada(s) sobre a arte" +
                  (_guideCuts.Outside > 0 ? $"; {_guideCuts.Outside} fora dela, ignorada(s)." : ".");
        }

        private void OnPathModeChecked(object sender, RoutedEventArgs e)
        {
            if (_drawnCuts == null) ReadPaths();
            UpdatePlan();
        }

        private void OnReadPaths(object sender, RoutedEventArgs e)
        {
            ReadPaths();
            PathMode.IsChecked = true; // já marcado: UpdatePlan abaixo
            UpdatePlan();
        }

        private void ReadPaths()
        {
            if (_host == null) return;
            _drawnCuts = _host.ReadDrawnCuts(out var message);
            PathText.Text = _drawnCuts == null
                ? message
                : $"{_drawnCuts.Paths.Count} linha(s) lida(s) da camada “{PXToolBox.Outputs.Tools.SliceDrawnCutService.LayerName}”" +
                  (_drawnCuts.Unreadable > 0 ? $"; {_drawnCuts.Unreadable} objeto(s) sem traçado (texto/imagem), ignorado(s)." : ".");
        }

        private void OnCreateCutLayer(object sender, RoutedEventArgs e)
        {
            var result = _host?.CreateCutLayer();
            if (result != null) MessageText.Text = result.Message;
        }

        private void OnCreateGuides(object sender, RoutedEventArgs e)
        {
            if (_host == null) return;
            if (_plan == null || GuideMode.IsChecked == true)
            {
                MessageText.Text = "Faça antes a prévia por linhas e colunas ou por dimensão máxima: as guias vão nas emendas dela.";
                return;
            }

            var (columns, rows) = SlicePlanner.SeamCenters(_plan);
            var result = _host.CreateGuides(columns, rows);
            if (result != null) MessageText.Text = result.Message;
            _guideCuts = null;
        }

        private void OnRemoveGuides(object sender, RoutedEventArgs e)
        {
            var result = _host?.RemoveGuides();
            if (result == null) return;
            MessageText.Text = result.Message;
            _guideCuts = null;
            if (GuideMode.IsChecked == true)
            {
                ReadGuides();
                UpdatePlan();
            }
        }

        private void OnApply(object sender, RoutedEventArgs e)
        {
            if (_host == null || (_plan == null && _regionPlan == null)) return;
            double separation;
            try
            {
                separation = Length(Separation);
                if (separation < 0) throw new ArgumentException("O afastamento entre as partes precisa ser 0 ou mais.");
            }
            catch (ArgumentException ex)
            {
                MessageText.Text = ex.Message;
                return;
            }

            var count = _regionPlan?.Regions.Count ?? _plan!.Tiles.Count;
            if (count > ConfirmAbove && MessageBox.Show(this,
                    $"Criar {count} partes? Cada uma leva uma cópia da arte inteira, e uma arte pesada pode deixar o Corel lento.",
                    Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var result = _regionPlan != null
                ? _host.ApplyRegions(_regionPlan, separation, DeleteOriginal.IsChecked == true)
                : _host.Apply(_plan!, separation, DeleteOriginal.IsChecked == true);
            if (result == null)
            {
                MessageText.Text = "Não foi possível dividir: veja a mensagem no painel.";
                return;
            }

            MessageText.Text = result.Message;
            // Feito: não dividir de novo o resultado com um segundo clique. Mudar qualquer campo ou
            // "Usar seleção atual" libera outra vez.
            if (result.IsSuccess) ApplyButton.IsEnabled = false;
        }
        private void OnInputsChanged(object sender, RoutedEventArgs e) => UpdatePlan();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _keyboardRouter.Install();
            ArtworkWidth.Focus();
        }

        private void Window_Activated(object sender, EventArgs e) => _keyboardRouter.ReassertFocus();

        private void Window_Closed(object? sender, EventArgs e)
        {
            _keyboardRouter.Uninstall();
            if (ReferenceEquals(_open, this)) _open = null;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

        private void UpdatePlan()
        {
            // Entrada inválida apaga o plano anterior, para não copiar nem aplicar um plano desatualizado.
            _plan = null;
            _regionPlan = null;
            CopyButton.IsEnabled = ApplyButton.IsEnabled = false;
            ReportText.Clear();
            PreviewCanvas.Children.Clear();
            try
            {
                var table = TableMode.IsChecked == true;
                var guides = GuideMode.IsChecked == true;
                var drawn = PathMode.IsChecked == true;
                Rows.IsEnabled = Columns.IsEnabled = table;
                MaximumWidth.IsEnabled = MaximumHeight.IsEnabled = SizeMode.IsChecked == true;
                var width = Length(ArtworkWidth);
                var height = Length(ArtworkHeight);
                var overlap = Length(Overlap);
                if (guides && _guideCuts == null)
                {
                    MessageText.Text = _host == null ? "Abra pelo botão do painel para ler as guias." : "Sem guias lidas: confira a mensagem acima e clique em “Ler as guias”.";
                    return;
                }

                if (guides && _guideCuts!.Slanted.Count > 0)
                {
                    var lines = _guideCuts.Slanted
                        .Concat(_guideCuts.Columns.Select(x => new SliceLine(x, 0, x, 1)))
                        .Concat(_guideCuts.Rows.Select(y => new SliceLine(0, y, 1, y)));
                    var linePlan = SliceRegionPlanner.ByLines(width, height, lines, overlap);
                    ShowRegions(linePlan, "guias da página, com inclinadas (linhas de corte livres)", $"{linePlan.Lines.Count} linha(s) de corte");
                    return;
                }

                if (drawn)
                {
                    if (_drawnCuts == null)
                    {
                        MessageText.Text = _host == null ? "Abra pelo botão do painel para ler as linhas." : "Sem linhas lidas: confira a mensagem acima e clique em “Ler as linhas”.";
                        return;
                    }

                    var pathPlan = SlicePathPlanner.ByPaths(width, height, _drawnCuts.Paths, overlap);
                    var loose = pathPlan.Outside > 0 ? $" ({pathPlan.Outside} não separa(m) nada: solta(s), fora da arte ou laço fechado por dentro)" : "";
                    ShowRegions(pathPlan, $"linhas desenhadas (camada “{PXToolBox.Outputs.Tools.SliceDrawnCutService.LayerName}”)",
                        $"{pathPlan.Paths.Count} linha(s) desenhada(s){loose}");
                    return;
                }

                var plan = table
                    ? SlicePlanner.ByTable(width, height, Count(Rows), Count(Columns), overlap)
                    : guides
                        ? SlicePlanner.ByCuts(width, height, _guideCuts!.Columns, _guideCuts.Rows, overlap)
                        : SlicePlanner.ByMaximumSize(width, height, Length(MaximumWidth), Length(MaximumHeight), overlap);
                Draw(plan);
                var report = new StringBuilder();
                report.AppendLine("PX Tool Box DEV — plano de divisão (Slice)");
                report.AppendLine($"Método: {(table ? "linhas e colunas" : guides ? "guias da página" : "dimensão máxima")}");
                report.AppendLine($"Área: {Format(width / 10)} × {Format(height / 10)} cm");
                report.AppendLine($"{plan.Rows} linha(s) × {plan.Columns} coluna(s): {plan.Tiles.Count} parte(s)");
                report.AppendLine($"Sobreposição total por emenda: {Format(overlap / 10)} cm");
                report.AppendLine("X e Y medidos a partir do canto superior esquerdo da área, em cm.");
                foreach (var tile in plan.Tiles)
                    report.AppendLine($"L{tile.Row} C{tile.Column}: X {Format(tile.X / 10)}; Y {Format(tile.Y / 10)}; {Format(tile.Width / 10)} × {Format(tile.Height / 10)} cm");
                ReportText.Text = report.ToString();
                _plan = plan;
                CopyButton.IsEnabled = true;
                ApplyButton.IsEnabled = _host != null;
                MessageText.Text = _host != null
                    ? "Prévia pronta. “Dividir no Corel” cria as partes com a seleção atual."
                    : "Prévia pronta.";
            }
            catch (ArgumentException ex)
            {
                MessageText.Text = ex.Message;
            }
        }

        /// <summary>Tamanho da prévia (400 px no lado maior) e o contorno da arte por baixo; devolve o lado maior em mm.</summary>
        private double DrawFrame(double width, double height)
        {
            var longest = Math.Max(width, height);
            PreviewCanvas.Width = width / longest * 400;
            PreviewCanvas.Height = height / longest * 400;
            if (ShowColors.IsChecked == true && _thumbnail != null && _thumbnailImage != null)
            {
                // A imagem cobre a caixa da seleção lida, no tamanho real dela (como o contorno: não estica).
                PreviewCanvas.Children.Add(new Image
                {
                    Source = _thumbnailImage,
                    Width = _thumbnail.WidthMm / longest * 400,
                    Height = _thumbnail.HeightMm / longest * 400,
                    Stretch = Stretch.Fill,
                    Opacity = 0.9,
                    IsHitTestVisible = false,
                });
            }

            if (ShowArt.IsChecked == true && _outline != null)
            {
                // Em mm a partir do canto da seleção lida: se as medidas foram digitadas diferentes, a arte
                // fica no tamanho real dela, não esticada.
                var stroke = new SolidColorBrush(Color.FromRgb(200, 200, 206));
                foreach (var ring in _outline.Rings)
                {
                    var polygon = new Polygon { Stroke = stroke, StrokeThickness = 0.6, IsHitTestVisible = false };
                    foreach (var point in ring) polygon.Points.Add(new Point(point.X / longest * 400, point.Y / longest * 400));
                    PreviewCanvas.Children.Add(polygon);
                }
            }

            return longest;
        }

        /// <summary>Partes em polígonos: guias com inclinada (<see cref="SliceRegionPlanner"/>) ou linhas desenhadas
        /// (<see cref="SlicePathPlanner"/>, com as linhas tracejadas por cima).</summary>
        private void ShowRegions(SliceRegionPlan plan, string method, string cuts)
        {
            var longest = DrawFrame(plan.Width, plan.Height);
            foreach (var region in plan.Regions)
            {
                var polygon = new Polygon
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(77, 163, 255)),
                    StrokeThickness = 0.8,
                    Fill = new SolidColorBrush(Color.FromArgb(35, 77, 163, 255)),
                    ToolTip = $"Parte {region.Number}: caixa de {Format(region.Width / 10)} × {Format(region.Height / 10)} cm",
                };
                foreach (var point in region.Points) polygon.Points.Add(new Point(point.X / longest * 400, point.Y / longest * 400));
                PreviewCanvas.Children.Add(polygon);
            }

            foreach (var path in plan.Paths)
            {
                var line = new Polyline
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(232, 90, 90)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    IsHitTestVisible = false,
                };
                foreach (var point in path) line.Points.Add(new Point(point.X / longest * 400, point.Y / longest * 400));
                PreviewCanvas.Children.Add(line);
            }

            var report = new StringBuilder();
            report.AppendLine("PX Tool Box DEV — plano de divisão (Slice)");
            report.AppendLine($"Método: {method}");
            report.AppendLine($"Área: {Format(plan.Width / 10)} × {Format(plan.Height / 10)} cm");
            report.AppendLine($"{cuts}: {plan.Regions.Count} parte(s)");
            report.AppendLine($"Sobreposição total por emenda: {Format(plan.Overlap / 10)} cm");
            report.AppendLine("Centro e caixa de cada parte (com a sobreposição), a partir do canto superior esquerdo da área, em cm.");
            foreach (var region in plan.Regions)
                report.AppendLine($"Parte {region.Number}: centro X {Format(Math.Round(region.CenterX / 10, 2))}; Y {Format(Math.Round(region.CenterY / 10, 2))}; " +
                                  $"caixa {Format(Math.Round(region.Width / 10, 2))} × {Format(Math.Round(region.Height / 10, 2))} cm; {region.Points.Count} lados");
            ReportText.Text = report.ToString();
            _regionPlan = plan;
            CopyButton.IsEnabled = true;
            ApplyButton.IsEnabled = _host != null;
            MessageText.Text = _host != null
                ? "Prévia pronta. “Dividir no Corel” cria as partes com a seleção atual."
                : "Prévia pronta.";
        }

        private void Draw(SlicePlan plan)
        {
            var longest = DrawFrame(plan.Width, plan.Height);
            foreach (var tile in plan.Tiles)
            {
                var rectangle = new Rectangle
                {
                    Width = tile.Width / longest * 400,
                    Height = tile.Height / longest * 400,
                    Stroke = new SolidColorBrush(Color.FromRgb(77, 163, 255)),
                    StrokeThickness = 0.8,
                    Fill = new SolidColorBrush(Color.FromArgb(35, 77, 163, 255)),
                    ToolTip = $"Linha {tile.Row}, coluna {tile.Column}: {Format(tile.Width / 10)} × {Format(tile.Height / 10)} cm"
                };
                Canvas.SetLeft(rectangle, tile.X / longest * 400);
                Canvas.SetTop(rectangle, tile.Y / longest * 400);
                PreviewCanvas.Children.Add(rectangle);
            }
        }

        private void OnCopy(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ReportText.Text);
                MessageText.Text = "Plano copiado.";
            }
            catch (Exception ex)
            {
                MessageText.Text = $"Falha ao copiar o plano: {ex.Message}";
            }
        }
    }
}
