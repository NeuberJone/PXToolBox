using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PXToolBox.Core.Molds;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.ListForgeIntegrated;

namespace PXToolBoxDev.Molds
{
    /// <summary>Edita as configurações do Cadastro de moldes. Nada é gravado até Salvar, e só o que
    /// passa em <see cref="MoldSettings.Validate"/>.</summary>
    public partial class MoldSettingsWindow : Window
    {
        private sealed class CategoryFields
        {
            public TextBox Code = null!;
            public TextBox BaseSizes = null!;
            public TextBox Prefixes = null!;
            public TextBox Suffixes = null!;
            public TextBlock Preview = null!;
        }

        private readonly ThreadKeyboardRouter _keyboardRouter;
        private readonly CategoryFields _male;
        private readonly CategoryFields _female;
        private readonly CategoryFields _kids;
        private bool _filling;

        public MoldSettingsWindow()
        {
            InitializeComponent();
            _keyboardRouter = new ThreadKeyboardRouter(this);
            _male = AddColumn("Masculino");
            _female = AddColumn("Feminino");
            _kids = AddColumn("Infantil");
            TagPositionBox.ItemsSource = MoldTagPlacement.Labels.Select(l => l.Label).ToList();
            foreach (var box in new[] { InnerMaleStyleBox, InnerFemaleStyleBox, InnerKidsStyleBox })
            {
                box.ItemsSource = MoldInnerOutlineOptions.StyleLabels.Select(l => l.Label).ToList();
            }
            var (settings, notice) = MoldSettingsStore.Load();
            Fill(settings);
            if (notice != null) StatusText.Text = notice;
        }

        /// <summary>As configurações como estão nos campos agora.</summary>
        public MoldSettings Draft => new MoldSettings(
            Category(_male), Category(_female), Category(_kids),
            GridBaseSizeBox.Text,
            LaserNamesBox.Text.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
            TagNameBox.Text, IdentifierNameBox.Text,
            new MoldDefineOptions(CreateLaserCopyBox.IsChecked == true, LaserColorBox.Text, Number(LaserWidthBox.Text),
                CreateTagBox.IsChecked == true, Number(TagFontSizeBox.Text), Number(TagOutlineWidthBox.Text),
                MoldTagPlacement.Labels[Math.Max(0, TagPositionBox.SelectedIndex)].Position,
                Number(TagLeftMarginBox.Text), Number(TagRightMarginBox.Text), Number(TagTopMarginBox.Text), Number(TagBottomMarginBox.Text)),
            new MoldInnerOutlineOptions(InnerOutlineBox.IsChecked == true, Number(InnerWidthBox.Text),
                Style(InnerMaleStyleBox), Style(InnerFemaleStyleBox), Style(InnerKidsStyleBox),
                MoldInnerOutlineOptions.ParseColors(InnerColorsBox.Text)));

        private static MoldLineStyle Style(ComboBox box) => MoldInnerOutlineOptions.StyleLabels[Math.Max(0, box.SelectedIndex)].Style;

        private static int IndexOf(MoldLineStyle style) => MoldInnerOutlineOptions.StyleLabels.Select(l => l.Style).ToList().IndexOf(style);

        /// <summary>Vírgula ou ponto; texto que não é número vira NaN e cai na validação.</summary>
        private static double Number(string text) =>
            double.TryParse((text ?? string.Empty).Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : double.NaN;

        private static string Number(double value) => value.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

        public IReadOnlyList<string> Problems => Draft.Validate();

        public string PreviewText(string category) => (category switch
        {
            "male" => _male,
            "female" => _female,
            _ => _kids,
        }).Preview.Text;

        public string GridPreviewText => GridPreview.Text;

        // Para testes (os campos gerados pelo XAML são internos).
        public string TagNameText { get => TagNameBox.Text; set => TagNameBox.Text = value; }

        public string GridBaseSizeText { get => GridBaseSizeBox.Text; set => GridBaseSizeBox.Text = value; }

        /// <summary>Grava se válido; senão mostra os problemas. Devolve se gravou.</summary>
        public bool TrySave()
        {
            var draft = Draft;
            var problems = draft.Validate();
            if (problems.Count > 0)
            {
                ProblemsText.Text = string.Join(Environment.NewLine, problems);
                return false;
            }

            try
            {
                MoldSettingsStore.Save(draft);
                return true;
            }
            catch (Exception ex)
            {
                ProblemsText.Text = $"Não foi possível salvar: {ex.Message}";
                return false;
            }
        }

        public void RestoreDefault() => Fill(MoldSettings.Default);

        private void Fill(MoldSettings settings)
        {
            _filling = true;
            Fill(_male, settings.Male);
            Fill(_female, settings.Female);
            Fill(_kids, settings.Kids);
            GridBaseSizeBox.Text = settings.GridBaseSize;
            LaserNamesBox.Text = string.Join(", ", settings.LaserNames);
            TagNameBox.Text = settings.TagName;
            IdentifierNameBox.Text = settings.IdentifierName;
            CreateLaserCopyBox.IsChecked = settings.Define.CreateLaserCopy;
            LaserColorBox.Text = settings.Define.LaserColor;
            LaserWidthBox.Text = Number(settings.Define.LaserWidthMillimeters);
            CreateTagBox.IsChecked = settings.Define.CreateTag;
            TagFontSizeBox.Text = Number(settings.Define.TagFontSizePoints);
            TagOutlineWidthBox.Text = Number(settings.Define.TagOutlineWidthMillimeters);
            TagPositionBox.SelectedIndex = MoldTagPlacement.Labels.Select(l => l.Position).ToList().IndexOf(settings.Define.TagPosition);
            TagLeftMarginBox.Text = Number(settings.Define.TagLeftMarginCm);
            TagRightMarginBox.Text = Number(settings.Define.TagRightMarginCm);
            TagTopMarginBox.Text = Number(settings.Define.TagTopMarginCm);
            TagBottomMarginBox.Text = Number(settings.Define.TagBottomMarginCm);
            InnerOutlineBox.IsChecked = settings.InnerOutline.Enabled;
            InnerWidthBox.Text = Number(settings.InnerOutline.WidthMillimeters);
            InnerMaleStyleBox.SelectedIndex = IndexOf(settings.InnerOutline.MaleStyle);
            InnerFemaleStyleBox.SelectedIndex = IndexOf(settings.InnerOutline.FemaleStyle);
            InnerKidsStyleBox.SelectedIndex = IndexOf(settings.InnerOutline.KidsStyle);
            // Na ordem dos tamanhos das categorias (os desconhecidos no fim, para a validação apontar).
            var order = settings.AllSizes.ToList();
            InnerColorsBox.Text = string.Join(Environment.NewLine, settings.InnerOutline.SizeColors
                .OrderBy(c => order.IndexOf(c.Key) < 0 ? int.MaxValue : order.IndexOf(c.Key)).Select(c => $"{c.Key} {c.Value}"));
            _filling = false;
            Refresh();
        }

        private static void Fill(CategoryFields fields, MoldSizeCategory category)
        {
            fields.Code.Text = category.Code;
            fields.BaseSizes.Text = string.Join(", ", category.BaseSizes);
            fields.Prefixes.Text = string.Join(", ", category.Prefixes);
            fields.Suffixes.Text = string.Join(", ", category.Suffixes);
        }

        private static MoldSizeCategory Category(CategoryFields fields) =>
            new MoldSizeCategory(fields.Code.Text, new[] { fields.BaseSizes.Text }, new[] { fields.Prefixes.Text }, new[] { fields.Suffixes.Text });

        private void Refresh()
        {
            if (_filling) return;
            var draft = Draft;
            foreach (var (fields, category) in new[] { (_male, draft.Male), (_female, draft.Female), (_kids, draft.Kids) })
            {
                fields.Preview.Text = category.Sizes.Count == 0 ? "Nenhum tamanho." : $"Tamanhos: {string.Join(", ", category.Sizes)}";
            }

            var grid = draft.Categories.Select(c => $"{string.Join("/", draft.GridSizesOf(c.Code).DefaultIfEmpty("—"))} ({c.Code})");
            GridPreview.Text = $"O Grid copia: {string.Join(", ", grid)}.";
            TagPreview.Text = TagDescription(draft.Define);
            RefreshInnerColors(draft);
            var problems = draft.Validate();
            ProblemsText.Text = string.Join(Environment.NewLine, problems);
            // Salvar fica sempre ativo (o desabilitado padrão fica claro e parece destacado neste fundo);
            // TrySave recusa e mostra os problemas.
            ProblemsText.Visibility = problems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private CategoryFields AddColumn(string title)
        {
            var fields = new CategoryFields();
            var panel = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            panel.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("Heading") });
            fields.Code = AddField(panel, "Código da camada:");
            fields.BaseSizes = AddField(panel, "Tamanhos base:");
            fields.Prefixes = AddField(panel, "Prefixos (opcional):");
            fields.Suffixes = AddField(panel, "Sufixos (opcional):");
            fields.Preview = new TextBlock { Style = (Style)FindResource("Preview") };
            panel.Children.Add(fields.Preview);
            CategoryColumns.Children.Add(panel);
            return fields;
        }

        private TextBox AddField(Panel panel, string label)
        {
            panel.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("Label"), TextWrapping = TextWrapping.Wrap });
            var box = new TextBox();
            box.TextChanged += OnFieldChanged;
            panel.Children.Add(box);
            return box;
        }

        private void OnFieldChanged(object sender, TextChangedEventArgs e) => Refresh();

        private void OnOptionChanged(object sender, RoutedEventArgs e) => Refresh();

        private void OnPositionChanged(object sender, SelectionChangedEventArgs e) => Refresh();

        /// <summary>Uma amostra por tamanho com a cor que o contorno interno vai usar (cadastrada ou sugerida).</summary>
        private void RefreshInnerColors(MoldSettings draft)
        {
            InnerColorsPreview.Children.Clear();
            foreach (var size in draft.AllSizes)
            {
                var color = draft.InnerOutlineColorOf(size);
                var swatch = new System.Windows.Media.SolidColorBrush(MoldDefineOptions.TryParseRgb(color, out var r, out var g, out var b)
                    ? System.Windows.Media.Color.FromRgb((byte)r, (byte)g, (byte)b) : System.Windows.Media.Colors.Transparent);
                var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 4), ToolTip = color };
                item.Children.Add(new Border { Width = 12, Height = 12, Background = swatch, BorderBrush = System.Windows.Media.Brushes.Gray, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 3, 0) });
                item.Children.Add(new TextBlock { Text = size, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB4, 0xB4, 0xBC)), FontSize = 11 });
                InnerColorsPreview.Children.Add(item);
            }

            InnerColorsPreview.Opacity = draft.InnerOutline.Enabled ? 1 : 0.45;
        }

        /// <summary>Em palavras, onde a TAG vai ficar (só as margens que contam na posição escolhida).</summary>
        public static string TagDescription(MoldDefineOptions options)
        {
            var position = options.TagPosition;
            var horizontal = position switch
            {
                MoldTagPosition.TopLeft or MoldTagPosition.MiddleLeft or MoldTagPosition.BottomLeft => ("encostada na esquerda", options.TagLeftMarginCm, 0.0),
                MoldTagPosition.TopRight or MoldTagPosition.MiddleRight or MoldTagPosition.BottomRight => ("encostada na direita", 0.0, options.TagRightMarginCm),
                _ => ("centralizada", options.TagLeftMarginCm, options.TagRightMarginCm),
            };
            var vertical = position switch
            {
                MoldTagPosition.TopLeft or MoldTagPosition.TopCenter or MoldTagPosition.TopRight => ("no topo", 0.0, options.TagTopMarginCm),
                MoldTagPosition.BottomLeft or MoldTagPosition.BottomCenter or MoldTagPosition.BottomRight => ("na base", options.TagBottomMarginCm, 0.0),
                _ => ("no meio", options.TagBottomMarginCm, options.TagTopMarginCm),
            };
            var shifts = new List<string>();
            var dx = horizontal.Item2 - horizontal.Item3;
            var dy = vertical.Item2 - vertical.Item3;
            if (dx > 0) shifts.Add($"{Number(dx)} cm para a direita");
            if (dx < 0) shifts.Add($"{Number(-dx)} cm para a esquerda");
            if (dy > 0) shifts.Add($"{Number(dy)} cm para cima");
            if (dy < 0) shifts.Add($"{Number(-dy)} cm para baixo");
            var where = $"A TAG fica dentro da caixa da peça, {vertical.Item1}, {horizontal.Item1}";
            return shifts.Count == 0 ? where + "." : $"{where}, deslocada {string.Join(" e ", shifts)}.";
        }

        // Para testes.
        public string LaserColorText { get => LaserColorBox.Text; set => LaserColorBox.Text = value; }

        public string LaserWidthText { get => LaserWidthBox.Text; set => LaserWidthBox.Text = value; }

        public bool CreateLaserCopyChecked { get => CreateLaserCopyBox.IsChecked == true; set => CreateLaserCopyBox.IsChecked = value; }

        public int TagPositionIndex { get => TagPositionBox.SelectedIndex; set => TagPositionBox.SelectedIndex = value; }

        public string TagLeftMarginText { get => TagLeftMarginBox.Text; set => TagLeftMarginBox.Text = value; }

        public string TagPreviewText => TagPreview.Text;

        public bool InnerOutlineChecked { get => InnerOutlineBox.IsChecked == true; set => InnerOutlineBox.IsChecked = value; }

        public string InnerColorsText { get => InnerColorsBox.Text; set => InnerColorsBox.Text = value; }

        public int InnerFemaleStyleIndex { get => InnerFemaleStyleBox.SelectedIndex; set => InnerFemaleStyleBox.SelectedIndex = value; }

        public int InnerColorsPreviewCount => InnerColorsPreview.Children.Count;

        private void OnRestoreDefault(object sender, RoutedEventArgs e)
        {
            RestoreDefault();
            StatusText.Text = "Valores de fábrica nos campos — Salvar para valer.";
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (TrySave())
            {
                DialogResult = true;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _keyboardRouter.Install();
            _male.BaseSizes.Focus();
        }

        private void Window_Activated(object sender, EventArgs e) => _keyboardRouter.ReassertFocus();

        private void Window_Closed(object? sender, EventArgs e) => _keyboardRouter.Uninstall();
    }
}
