#if NET48
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using PXToolBox.Core.Nesting;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Tools;
using PXToolBoxDev.Nesting;
using Xunit;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace PXToolBox.Tests
{
    [Collection("WPF created in code — no parallel WPF initialization")]
    public sealed class NestingWindowTests : IDisposable
    {
        // A janela lê os perfis ao abrir: nunca o arquivo do usuário.
        public NestingWindowTests() => PXToolBoxDev.Integrations.NestingProfileStore.SetFilePathForTesting(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "px-nesting-profiles-" + Guid.NewGuid().ToString("N"), "nesting-profiles.json"));

        public void Dispose() => PXToolBoxDev.Integrations.NestingProfileStore.SetFilePathForTesting(null);

        private sealed class RecordingHost : INestingHost
        {
            public List<(NestResult Result, bool ResizePage)> Applied { get; } = new();

            public NestingSelection? ReadSelection(out string message)
            {
                message = "selecione as peças.";
                return null;
            }

            public List<IReadOnlyList<int>> Sources { get; } = new();

            public OperationResult? Apply(IReadOnlyList<NestItem> items, IReadOnlyList<int> sources, NestResult result, bool resizePage)
            {
                Sources.Add(sources);
                Applied.Add((result, resizePage));
                return OperationResult.Success("2 peça(s) encaixada(s).");
            }

            public ContourReading? Contours { get; set; }
            public int ContourReads { get; private set; }
            public List<(ContourNestResult Result, double Margin, double Spacing)> AppliedContours { get; } = new();

            public ContourReading? ReadContours(out string message)
            {
                ContourReads++;
                message = "sem contorno";
                return Contours;
            }

            public OperationResult? ApplyContour(ContourReading reading, IReadOnlyList<int> sources, ContourNestResult result,
                double marginMm, double spacingMm, bool resizePage)
            {
                Sources.Add(sources);
                AppliedContours.Add((result, marginMm, spacingMm));
                return OperationResult.Success("2 peça(s) encaixada(s) pelo contorno.");
            }

            public List<(AreaNestResult<NestResult, NestItem> Result, bool ResizePage, bool DrawAreas, bool GroupBlocks, bool Legends)> AppliedAreas { get; } = new();

            public OperationResult? ApplyAreas(IReadOnlyList<NestItem> items, IReadOnlyList<int> sources,
                AreaNestResult<NestResult, NestItem> result, bool resizePage, bool drawAreas, bool groupBlocks, bool legends)
            {
                Sources.Add(sources);
                AppliedAreas.Add((result, resizePage, drawAreas, groupBlocks, legends));
                return OperationResult.Success("2 peça(s) encaixada(s) em 2 de 2 área(s).");
            }

            public OperationResult? ApplyContourAreas(ContourReading reading, IReadOnlyList<int> sources,
                AreaNestResult<ContourNestResult, ContourPiece> result, double marginMm, double spacingMm, bool resizePage, bool drawAreas,
                bool groupBlocks, bool legends)
            {
                Sources.Add(sources);
                return OperationResult.Success("pelo contorno em áreas.");
            }
        }

        /// <summary>Roda a fila da janela (o resultado do cálculo em segundo plano chega por ela) até a condição.</summary>
        private static void WaitFor(Func<bool> done)
        {
            var until = DateTime.Now.AddSeconds(20);
            while (!done())
            {
                if (DateTime.Now > until) throw new TimeoutException("O cálculo não terminou.");
                Pump(TimeSpan.FromMilliseconds(10));
            }
        }

        private static void Pump(TimeSpan duration)
        {
            var until = DateTime.Now + duration;
            do
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                Thread.Sleep(5);
            }
            while (DateTime.Now < until);
        }

        [Fact]
        public void SearchBest_ByBox_KeepsTheOrderThatFitsMore_AndChecksTheTimeLimit()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var items = new[]
                    {
                        new NestItem(0, "Régua", 10, 100),
                        new NestItem(1, "Q1", 50, 50), new NestItem(2, "Q2", 50, 50), new NestItem(3, "Q3", 50, 50), new NestItem(4, "Q4", 50, 50),
                    };
                    var window = new NestingWindow(new NestingSelection(items, 100, 100), new RecordingHost());
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        var report = Find<TextBox>("ReportText");
                        Find<TextBox>("AreaMargin").Text = "0";
                        Find<TextBox>("Spacing").Text = "0";
                        Assert.Contains("3 peça(s) encaixada(s)", report.Text);
                        Assert.DoesNotContain("Busca:", report.Text);

                        Find<CheckBox>("SearchBest").IsChecked = true;
                        Assert.Contains("4 peça(s) encaixada(s)", report.Text);
                        Assert.Contains("Busca: 5 de 5 ordem(ns) testada(s) em ", report.Text);
                        Assert.Contains("melhor: maior área primeiro", report.Text);
                        Assert.StartsWith("PX Tool Box DEV — encaixe retangular" + Environment.NewLine + "Busca:", report.Text);

                        Find<TextBox>("TimeLimit").Text = "0";
                        Assert.Equal("Tempo máximo: use de 1 a 600 segundos.", Find<TextBlock>("MessageText").Text);
                        Assert.False(Find<Button>("ApplyButton").IsEnabled);
                    }
                    finally { window.Close(); }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A janela não terminou.");
            if (failure != null) throw failure;
        }

        [Fact]
        public void Areas_FillOneAfterTheOther_AndApplyGoesToTheAreasHost()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new RecordingHost();
                    var items = new[] { new NestItem(0, "Frente", 500, 700), new NestItem(1, "Manga", 250, 200) };
                    var window = new NestingWindow(new NestingSelection(items, 2100, 2970), host);
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        var report = Find<TextBox>("ReportText");
                        Assert.False(Find<TextBox>("AreasText").IsEnabled);

                        Find<TextBox>("AreasText").Text = "60 x 80; Retalho: 30 x 30";
                        Find<RadioButton>("AreasMode").IsChecked = true;
                        Assert.True(Find<TextBox>("AreasText").IsEnabled);
                        Assert.True(Find<CheckBox>("ResizePage").IsEnabled);
                        Assert.False(Find<TextBox>("RollWidth").IsEnabled);
                        // Margem e distância 0,5 cm: a Frente ocupa a 1ª área; a Manga vai para o retalho.
                        Assert.Contains("Área 1 (60 × 80 cm): 1 peça(s)", report.Text);
                        Assert.Contains("Retalho (30 × 30 cm): 1 peça(s)", report.Text);
                        Assert.Contains("Retalho — Manga: X 0,5; Y 0,5", report.Text);
                        Assert.Equal("Prévia pronta.", Find<TextBlock>("MessageText").Text);

                        Find<Button>("ApplyButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var applied = Assert.Single(host.AppliedAreas);
                        Assert.Equal((2, false, true), (applied.Result.Fills.Count, applied.ResizePage, applied.DrawAreas));
                        Assert.Empty(host.Applied);

                        // Menores primeiro: o retalho é preenchido (e desenhado) antes.
                        Assert.DoesNotContain("Áreas preenchidas:", report.Text);
                        Find<RadioButton>("AreasSmallestFirst").IsChecked = true;
                        Assert.Contains("Áreas preenchidas: menores primeiro", report.Text);
                        Assert.True(report.Text.IndexOf("Retalho (30 × 30 cm)", StringComparison.Ordinal)
                            < report.Text.IndexOf("Área 1 (60 × 80 cm)", StringComparison.Ordinal));
                        Find<Button>("ApplyButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        Assert.Equal(new[] { "Retalho", "Área 1" }, host.AppliedAreas[1].Result.Areas.Select(a => a.Name));
                        Assert.Equal(new[] { 0.0, 320.0 }, host.AppliedAreas[1].Result.Lefts);

                        Find<RadioButton>("PageMode").IsChecked = true;
                        Assert.False(Find<RadioButton>("AreasSmallestFirst").IsEnabled);
                        Find<RadioButton>("AreasMode").IsChecked = true;

                        Find<TextBox>("AreasText").Text = "x";
                        Assert.Equal("Área 1 (“x”): use largura x altura em cm, ex.: 100 x 150.", Find<TextBlock>("MessageText").Text);
                        Assert.False(Find<Button>("ApplyButton").IsEnabled);
                    }
                    finally { window.Close(); }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A janela não terminou.");
            if (failure != null) throw failure;
        }

        [Fact]
        public void Quantities_RepeatPieces_AndTheHostGetsWhereEachItemComesFrom()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new RecordingHost();
                    var items = new[] { new NestItem(0, "Frente", 500, 700), new NestItem(1, "Manga", 250, 200) };
                    var window = new NestingWindow(new NestingSelection(items, 2100, 2970), host);
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        var report = Find<TextBox>("ReportText");
                        var list = Find<StackPanel>("QuantityList");
                        // Desligado por padrão: nenhuma linha montada, 1 de cada.
                        Assert.False(Find<CheckBox>("UseQuantities").IsChecked);
                        Assert.Empty(list.Children);
                        Assert.Equal(Visibility.Collapsed, Find<StackPanel>("QuantityPanel").Visibility);
                        Assert.Contains("2 peça(s) encaixada(s)", report.Text);

                        Find<CheckBox>("UseQuantities").IsChecked = true;
                        Assert.Equal(Visibility.Visible, Find<StackPanel>("QuantityPanel").Visibility);
                        Assert.Equal(2, list.Children.Count);
                        TextBox Quantity(int row) => (TextBox)((DockPanel)list.Children[row]).Children[0];

                        Quantity(1).Text = "3";
                        Assert.Contains("4 peça(s) encaixada(s)", report.Text);
                        Assert.Contains("Manga (3/3)", report.Text);

                        Find<TextBox>("QuantityAll").Text = "2";
                        Find<Button>("QuantityAllButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        Assert.Equal(new[] { "2", "2" }, new[] { Quantity(0).Text, Quantity(1).Text });
                        Quantity(0).Text = "0";
                        Assert.Contains("2 peça(s) encaixada(s)", report.Text);
                        Assert.DoesNotContain("Frente", report.Text);

                        Quantity(1).Text = "x";
                        Assert.Equal("Quantidade: use números inteiros de 0 a 999.", Find<TextBlock>("MessageText").Text);
                        Quantity(1).Text = "2";

                        Find<Button>("ApplyButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        Assert.Equal(new[] { 1, 1 }, Assert.Single(host.Sources));

                        // Desligar volta a 1 de cada e desmonta a lista.
                        Find<CheckBox>("UseQuantities").IsChecked = false;
                        Assert.Empty(list.Children);
                        Assert.Contains("2 peça(s) encaixada(s)", report.Text);
                        Assert.Contains("Frente", report.Text);
                    }
                    finally { window.Close(); }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A janela não terminou.");
            if (failure != null) throw failure;
        }

        private static IReadOnlyList<NestPoint> L(double size) => new[]
        {
            new NestPoint(0, 0), new NestPoint(size * 0.3, 0), new NestPoint(size * 0.3, size * 0.7),
            new NestPoint(size, size * 0.7), new NestPoint(size, size), new NestPoint(0, size),
        };

        [Fact]
        public void ContourMode_CalculatesOnlyOnTheButton_ReadsContoursOnce_AndAppliesTheCheckedResult()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var items = new[] { new NestItem(0, "L1", 1000, 1000), new NestItem(1, "L2", 1000, 1000) };
                    var pieces = new[]
                    {
                        new ContourPiece(0, "L1", new IReadOnlyList<IReadOnlyList<NestPoint>>[] { new[] { L(1000) } }),
                        new ContourPiece(1, "L2", new IReadOnlyList<IReadOnlyList<NestPoint>>[] { new[] { L(1000) } }),
                    };
                    var host = new RecordingHost
                    {
                        Contours = new ContourReading(items, pieces, new[] { (0.0, 0.0), (0.0, 0.0) }, new[] { "Nome" }),
                    };
                    var window = new NestingWindow(new NestingSelection(items, 1400, 2970), host);
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        void Click(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var report = Find<TextBox>("ReportText");
                        var message = Find<TextBlock>("MessageText");
                        var apply = Find<Button>("ApplyButton");
                        Find<TextBox>("AreaMargin").Text = "0";
                        Find<TextBox>("Spacing").Text = "0";

                        Find<RadioButton>("ContourMethod").IsChecked = true;
                        Find<RadioButton>("HalfTurn").IsChecked = true;
                        Assert.Empty(report.Text);
                        Assert.False(apply.IsEnabled);
                        Assert.StartsWith("Pelo contorno: clique em “Calcular encaixe”", message.Text);
                        Assert.Equal(0, host.ContourReads);

                        Click("CalculateButton");
                        Assert.False(Find<Button>("CalculateButton").IsEnabled); // calcula em segundo plano
                        Assert.Equal(Visibility.Visible, Find<Button>("CancelButton").Visibility);
                        WaitFor(() => Find<Button>("CalculateButton").IsEnabled);
                        Assert.Equal(Visibility.Collapsed, Find<Button>("CancelButton").Visibility);
                        Assert.Equal(1, host.ContourReads);
                        Assert.Contains("encaixe pelo contorno", report.Text);
                        Assert.Contains("Página 140 × 297 cm: altura usada 100 cm", report.Text);
                        Assert.Contains("Pela caixa (texto, imagem ou contorno ilegível): Nome", report.Text);
                        Assert.Contains("L2: X ", report.Text);
                        Assert.Contains("(girada 180°)", report.Text);
                        Assert.StartsWith("Prévia pronta (conferida", message.Text);
                        Assert.True(apply.IsEnabled);

                        // Mudar um campo durante o cálculo descarta o resultado dele.
                        Click("CalculateButton");
                        Find<TextBox>("Spacing").Text = "0,0";
                        Pump(TimeSpan.FromMilliseconds(400));
                        Assert.Empty(report.Text);
                        Assert.False(apply.IsEnabled);
                        Assert.StartsWith("Pelo contorno: clique em “Calcular encaixe”", message.Text);

                        Find<CheckBox>("SearchBest").IsChecked = true;
                        Click("CalculateButton");
                        WaitFor(() => Find<Button>("CalculateButton").IsEnabled);
                        Assert.Contains("Busca: 5 de 5 ordem(ns) testada(s) em ", report.Text);
                        Assert.Equal(1, host.ContourReads); // contornos lidos uma vez só

                        Click("ApplyButton");
                        var applied = Assert.Single(host.AppliedContours);
                        Assert.Equal(2, applied.Result.Placements.Count);
                        Assert.Empty(host.Applied);
                        Assert.False(apply.IsEnabled);
                    }
                    finally { window.Close(); }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A janela não terminou.");
            if (failure != null) throw failure;
        }

        [Fact]
        public void CompareWithTheOriginal_ReportsBothBoxes_AndDrawsWhereThePiecesAreNow()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new RecordingHost();
                    var items = new[] { new NestItem(0, "Frente", 500, 700), new NestItem(1, "Costas", 500, 700) };
                    // Costas fora da página, à esquerda: o desenho cresce para mostrar as duas coisas.
                    var origins = new[] { new NestPoint(1000, 2000), new NestPoint(-500, 0) };
                    var window = new NestingWindow(new NestingSelection(items, 2100, 2970, origins), host);
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        var report = Find<TextBox>("ReportText");
                        var canvas = Find<Canvas>("PreviewCanvas");
                        var show = Find<CheckBox>("ShowOriginal");
                        Assert.Contains("Original: as peças ocupam 200 × 270 cm (aproveitamento 13,0%); no encaixe, 100,5 × 70 cm (aproveitamento 99,5%) — caixa 87% menor", report.Text);
                        Assert.True(show.IsEnabled);
                        Assert.DoesNotContain(canvas.Children.OfType<Rectangle>(), r => (string?)r.Tag == "original");
                        var width = canvas.Width;

                        show.IsChecked = true;
                        var originals = canvas.Children.OfType<Rectangle>().Where(r => (string?)r.Tag == "original").ToList();
                        Assert.Equal(2, originals.Count);
                        Assert.Equal(0, Canvas.GetLeft(originals[1]), 6);       // a peça mais à esquerda encosta na borda do desenho
                        Assert.True(canvas.Width > width);
                        var frame = canvas.Children.OfType<Rectangle>().First();  // a página, deslocada 50 cm
                        Assert.Equal(Canvas.GetLeft(originals[0]) * 500 / 1500, Canvas.GetLeft(frame), 6);
                        Assert.Contains("Original:", report.Text);              // mostrar não recalcula nem apaga o plano
                        Assert.True(Find<Button>("ApplyButton").IsEnabled);

                        show.IsChecked = false;
                        Assert.DoesNotContain(canvas.Children.OfType<Rectangle>(), r => (string?)r.Tag == "original");
                        Assert.Equal(width, canvas.Width, 6);

                        Find<CheckBox>("UseQuantities").IsChecked = true;
                        Find<TextBox>("QuantityAll").Text = "2";
                        Find<Button>("QuantityAllButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        Assert.Contains("Comparar com o original: só quando cada peça entra uma vez e todas cabem.", report.Text);

                        Find<RadioButton>("AreasMode").IsChecked = true;
                        Assert.False(show.IsEnabled);
                        Assert.DoesNotContain("Comparar com o original", report.Text);
                    }
                    finally { window.Close(); }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A janela não terminou.");
            if (failure != null) throw failure;
        }

        [Fact]
        public void NothingFits_SaysWhy_AndTheNextWindowOpensWithTheLastOptions()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var items = new[] { new NestItem(0, "G", 753.6, 618.9), new NestItem(1, "GG", 778.7, 628.9) };
                    var selection = new NestingSelection(items, 450, 450);
                    var window = new NestingWindow(selection, new RecordingHost());
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        Assert.Equal("Nenhuma peça coube na página atual (45 × 45 cm): a maior peça tem 77,87 × 62,89 cm. Use “Rolo” ou “Várias áreas”, ou aumente a página.",
                            Find<TextBlock>("MessageText").Text);
                        Assert.False(Find<Button>("ApplyButton").IsEnabled);

                        Find<TextBox>("AreasText").Text = "175 x 1000\n175 x 1000\n175 x 1000";
                        Find<RadioButton>("AreasMode").IsChecked = true;
                        Find<RadioButton>("HalfTurn").IsChecked = true;
                        Find<TextBox>("Spacing").Text = "1,2";
                        Assert.Contains("2 peça(s) encaixada(s)", Find<TextBox>("ReportText").Text);
                    }
                    finally { window.Close(); }

                    // Fechou: a próxima janela abre como esta ficou (perto dos perfis — aqui, a pasta do teste).
                    Assert.True(System.IO.File.Exists(PXToolBoxDev.Integrations.NestingProfileStore.LastFilePath));
                    var again = new NestingWindow(selection, new RecordingHost());
                    try
                    {
                        T Find<T>(string name) => (T)again.FindName(name);
                        Assert.True(Find<RadioButton>("AreasMode").IsChecked);
                        Assert.True(Find<RadioButton>("HalfTurn").IsChecked);
                        Assert.Equal("1,2", Find<TextBox>("Spacing").Text);
                        Assert.Equal("175 x 1000\n175 x 1000\n175 x 1000", Find<TextBox>("AreasText").Text);
                        Assert.False(Find<CheckBox>("UseQuantities").IsChecked);   // quantidades nunca voltam
                        Assert.StartsWith("PX Tool Box DEV — encaixe retangular em várias áreas", Find<TextBox>("ReportText").Text);
                    }
                    finally { again.Close(); }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A janela não terminou.");
            if (failure != null) throw failure;
        }

        [Fact]
        public void RollWithSheetHeight_ContinuesOnANewSheetBelow_AndThePreviewStandsUp()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new RecordingHost();
                    var items = Enumerable.Range(0, 4).Select(k => new NestItem(k, $"Peça {k + 1}", 500, 700)).ToArray();
                    var window = new NestingWindow(new NestingSelection(items, 2100, 2970), host);
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        var report = Find<TextBox>("ReportText");
                        var canvas = Find<Canvas>("PreviewCanvas");
                        Assert.False(Find<TextBox>("RollHeight").IsEnabled);

                        // Rolo livre, pouco comprimento usado: a prévia continua em pé (pelo menos tão alta quanto larga).
                        Find<RadioButton>("RollMode").IsChecked = true;
                        Find<TextBox>("RollWidth").Text = "220";
                        Assert.Contains("Rolo de 220 cm: comprimento usado 71 cm", report.Text);
                        Assert.True(canvas.Height >= canvas.Width);
                        Assert.False(Find<RadioButton>("OriginBottomLeft").IsEnabled);
                        Assert.False(Find<CheckBox>("DrawAreas").IsEnabled);

                        // Folhas de 110 × 80 cm (margem e distância 0,5): 2 peças por folha → 2 folhas, uma embaixo da outra.
                        Find<TextBox>("RollWidth").Text = "110";
                        Find<TextBox>("RollHeight").Text = "80";
                        Assert.StartsWith("PX Tool Box DEV — encaixe retangular no rolo, em folhas", report.Text);
                        Assert.Contains("Rolo de 110 cm em folhas de 80 cm: 2 folha(s), comprimento total 160 cm", report.Text);
                        Assert.Contains("Folha 1 (110 × 80 cm): 2 peça(s)", report.Text);
                        Assert.Contains("Folha 2 (110 × 80 cm): 2 peça(s)", report.Text);
                        Assert.True(canvas.Height > canvas.Width);
                        Assert.True(Find<RadioButton>("OriginBottomLeft").IsEnabled);   // folha é área fechada: qualquer canto
                        Assert.True(Find<CheckBox>("DrawAreas").IsEnabled);
                        Assert.False(Find<CheckBox>("ShowOriginal").IsEnabled);

                        Find<CheckBox>("ResizePage").IsChecked = true;
                        Find<Button>("ApplyButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var applied = Assert.Single(host.AppliedAreas);
                        Assert.Equal(new[] { 0.0, 800 }, applied.Result.Tops);
                        Assert.Equal(new[] { 0.0, 0 }, applied.Result.Lefts);
                        Assert.Equal((1100.0, 1600.0), (applied.Result.TotalWidth, applied.Result.TotalHeight));
                        Assert.True(applied.ResizePage);
                        Assert.Empty(host.Applied);

                        Assert.False(applied.GroupBlocks);

                        // Agrupar por bloco: as mesmas 2 folhas, lado a lado (sem recalcular), e cada uma vira um grupo.
                        var report2 = report.Text;
                        Find<CheckBox>("GroupBlocks").IsChecked = true;
                        Assert.Equal(report2, report.Text);
                        Assert.True(canvas.Width > canvas.Height);
                        Find<CheckBox>("GroupBlocks").IsChecked = false;
                        Assert.True(canvas.Height > canvas.Width);
                        Find<CheckBox>("GroupBlocks").IsChecked = true;
                        Find<TextBox>("Spacing").Text = "0,6";   // libera "Aplicar" de novo (recalcula)
                        Assert.True(canvas.Width > canvas.Height);
                        Find<Button>("ApplyButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var grouped = host.AppliedAreas[1];
                        Assert.True(grouped.GroupBlocks);
                        Assert.Equal(new[] { 0.0, 0 }, grouped.Result.Tops);
                        Assert.Equal(new[] { 0.0, 1100 + NestAreas.GapMm }, grouped.Result.Lefts);
                        Assert.Equal(new[] { 0.0, 1100 + NestAreas.GapMm }, grouped.Result.Fills.Select(f => f.Left));
                        Assert.Equal((2200 + NestAreas.GapMm, 800.0), (grouped.Result.TotalWidth, grouped.Result.TotalHeight));
                        Assert.False(grouped.Legends);

                        // Distância entre blocos e legenda: só com "Agrupar por bloco" a legenda liga.
                        Assert.True(Find<CheckBox>("Legends").IsEnabled);
                        Find<CheckBox>("GroupBlocks").IsChecked = false;
                        Assert.False(Find<CheckBox>("Legends").IsEnabled);
                        Find<CheckBox>("GroupBlocks").IsChecked = true;
                        Find<CheckBox>("Legends").IsChecked = true;
                        Find<TextBox>("BlockGap").Text = "-1";
                        Assert.Equal("Distância entre os blocos: use de 0 a 1000 cm.", Find<TextBlock>("MessageText").Text);
                        Assert.False(Find<Button>("ApplyButton").IsEnabled);
                        Find<TextBox>("BlockGap").Text = "5";
                        Find<Button>("ApplyButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var spaced = host.AppliedAreas[2];
                        Assert.True(spaced.Legends);
                        Assert.Equal(new[] { 0.0, 1100 + 50 }, spaced.Result.Lefts);
                        Assert.Equal((2200 + 50.0, 800.0), (spaced.Result.TotalWidth, spaced.Result.TotalHeight));

                        Find<TextBox>("RollHeight").Text = "0";
                        Assert.Equal("Altura da folha: use um número maior que zero (ou deixe vazio para comprimento livre).", Find<TextBlock>("MessageText").Text);
                        Assert.False(Find<Button>("ApplyButton").IsEnabled);

                        // Vazio volta ao comprimento livre.
                        Find<TextBox>("RollHeight").Text = " ";
                        Assert.Contains("Rolo de 110 cm: comprimento usado", report.Text);
                        Assert.False(Find<RadioButton>("OriginBottomLeft").IsEnabled);
                        Assert.False(Find<CheckBox>("GroupBlocks").IsEnabled);
                    }
                    finally { window.Close(); }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A janela não terminou.");
            if (failure != null) throw failure;
        }

        [Fact]
        public void Origin_MovesThePreviewToTheChosenCorner_AndTheRollOffersOnlyTheTopCorners()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new RecordingHost();
                    var items = new[] { new NestItem(0, "Frente", 500, 700), new NestItem(1, "Costas", 500, 700) };
                    var window = new NestingWindow(new NestingSelection(items, 2100, 2970), host);
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        var report = Find<TextBox>("ReportText");
                        Assert.DoesNotContain("Começa pelo", report.Text);

                        Find<RadioButton>("OriginBottomRight").IsChecked = true;
                        Assert.Contains("Começa pelo canto inferior direito", report.Text);
                        // Margem de 0,5 cm: a peça do canto encosta em 2095 × 2965 mm.
                        Assert.Contains("Frente: X 159,5; Y 226,5; 50 × 70 cm", report.Text);
                        Find<Button>("ApplyButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var (applied, _) = Assert.Single(host.Applied);
                        Assert.Equal(2095, applied.Placements.Max(p => p.X + p.Width), 6);
                        Assert.Equal(2965, applied.Placements.Max(p => p.Y + p.Height), 6);

                        // Rolo: os cantos de baixo desligam, e o que estava marcado explica o motivo.
                        Find<RadioButton>("RollMode").IsChecked = true;
                        Assert.False(Find<RadioButton>("OriginBottomLeft").IsEnabled);
                        Assert.False(Find<RadioButton>("OriginBottomRight").IsEnabled);
                        Assert.True(Find<RadioButton>("OriginTopRight").IsEnabled);
                        Assert.Equal("No rolo o encaixe começa no início do rolo: escolha um canto de cima.", Find<TextBlock>("MessageText").Text);
                        Assert.False(Find<Button>("ApplyButton").IsEnabled);
                        Find<RadioButton>("OriginTopRight").IsChecked = true;
                        Assert.Contains("Começa pelo canto superior direito", report.Text);
                        Assert.True(Find<Button>("ApplyButton").IsEnabled);
                    }
                    finally { window.Close(); }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A janela não terminou.");
            if (failure != null) throw failure;
        }

        [Fact]
        public void Preview_FollowsTheFields_AndApplySendsTheResultOnce()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new RecordingHost();
                    var items = new[] { new NestItem(0, "Frente", 500, 700), new NestItem(1, "Costas", 500, 700) };
                    var window = new NestingWindow(new NestingSelection(items, 2100, 2970), host);
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        var report = Find<TextBox>("ReportText");
                        var apply = Find<Button>("ApplyButton");
                        var message = Find<TextBlock>("MessageText");

                        Assert.Contains("Página 210 × 297 cm", report.Text);
                        Assert.Contains("2 peça(s) encaixada(s), 0 girada(s) 90°", report.Text);
                        Assert.True(apply.IsEnabled);

                        Find<RadioButton>("RollMode").IsChecked = true;
                        Find<TextBox>("RollWidth").Text = "160";
                        Find<CheckBox>("ResizePage").IsChecked = true;
                        Assert.Contains("Rolo de 160 cm: comprimento usado 71 cm", report.Text);

                        Find<TextBox>("Spacing").Text = "x";
                        Assert.Empty(report.Text);
                        Assert.False(apply.IsEnabled);
                        Find<TextBox>("Spacing").Text = "0,5";

                        apply.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var (result, resize) = Assert.Single(host.Applied);
                        Assert.Null(result.AreaHeight);
                        Assert.True(resize);
                        Assert.Equal("2 peça(s) encaixada(s).", message.Text);
                        Assert.False(apply.IsEnabled);

                        Find<Button>("UseSelectionButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        Assert.Equal("selecione as peças.", message.Text);
                    }
                    finally { window.Close(); }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A janela não terminou.");
            if (failure != null) throw failure;
        }

        [Fact]
        public void SplitByLayer_NestsEachLayerInItsOwnRoll_SideBySide()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new RecordingHost();
                    var items = Enumerable.Range(0, 4).Select(k => new NestItem(k, $"Peça {k + 1}", 500, 700)).ToArray();
                    var layers = new[] { "Tactel", "Dry fit", "Tactel", "Tactel" };
                    var window = new NestingWindow(new NestingSelection(items, 2100, 2970, layers: layers), host);
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        var report = Find<TextBox>("ReportText");
                        var split = Find<CheckBox>("SplitByLayer");
                        Assert.False(split.IsEnabled);

                        Find<RadioButton>("RollMode").IsChecked = true;
                        Find<TextBox>("RollWidth").Text = "110";
                        Assert.True(split.IsEnabled);
                        Assert.False(Find<CheckBox>("GroupBlocks").IsEnabled);
                        split.IsChecked = true;
                        Assert.True(Find<CheckBox>("GroupBlocks").IsEnabled);
                        Assert.True(Find<CheckBox>("DrawAreas").IsEnabled);
                        Assert.False(Find<CheckBox>("ShowOriginal").IsEnabled);
                        Assert.StartsWith("PX Tool Box DEV — encaixe retangular no rolo, separado por camada", report.Text);
                        // Tactel: 3 peças de 50 × 70 em 110 (margem e distância 0,5) → 2 fileiras; Dry fit: 1.
                        Assert.Contains("Tactel — Rolo (110 × ", report.Text);
                        Assert.Contains("): 3 peça(s)", report.Text);
                        Assert.Contains("Dry fit — Rolo (110 × ", report.Text);
                        Assert.Contains("): 1 peça(s)", report.Text);

                        Find<CheckBox>("GroupBlocks").IsChecked = true;
                        Find<Button>("ApplyButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var applied = Assert.Single(host.AppliedAreas);
                        Assert.True(applied.GroupBlocks);
                        Assert.Equal(new[] { "Tactel — Rolo", "Dry fit — Rolo" }, applied.Result.Areas.Select(a => a.Name));
                        Assert.Equal(new[] { 0.0, 1100 + NestAreas.GapMm }, applied.Result.Lefts);
                        Assert.Empty(host.Applied);

                        // Em folhas: as folhas de cada camada; com "Agrupar por bloco", todas numa fileira.
                        Find<TextBox>("RollHeight").Text = "80";
                        Assert.StartsWith("PX Tool Box DEV — encaixe retangular no rolo, separado por camada, em folhas", report.Text);
                        Assert.Contains("Tactel — Folha 2 (110 × 80 cm): 1 peça(s)", report.Text);
                        Find<Button>("ApplyButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        Assert.Equal(new[] { 0.0, 1100 + NestAreas.GapMm, 2 * (1100 + NestAreas.GapMm) }, host.AppliedAreas[1].Result.Lefts);
                        Assert.Equal(new[] { 0.0, 0, 0 }, host.AppliedAreas[1].Result.Tops);
                    }
                    finally { window.Close(); }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A janela não terminou.");
            if (failure != null) throw failure;
        }
    }
}
#endif
