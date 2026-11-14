#if NET48
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using PXToolBox.Core.Nesting;
using PXToolBox.Core.Result;
using PXToolBox.Core.Slicing;
using PXToolBox.Outputs.Tools;
using PXToolBoxDev.Slicing;
using Xunit;

namespace PXToolBox.Tests
{
    [Collection("WPF created in code — no parallel WPF initialization")]
    public sealed class SlicePreviewWindowTests
    {
        [Fact]
        public void Constructor_LoadsXamlAndInvalidInputClearsPreviousPlan()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var window = new SlicePreviewWindow(2000, 1000);
                    try
                    {
                        var report = (TextBox)window.FindName("ReportText");
                        var copy = (Button)window.FindName("CopyButton");
                        var width = (TextBox)window.FindName("ArtworkWidth");
                        Assert.Contains("200 × 100 cm", report.Text);
                        Assert.True(copy.IsEnabled);
                        width.Text = "";
                        Assert.Empty(report.Text);
                        Assert.False(copy.IsEnabled);
                        width.Text = "210.5";
                        Assert.Contains("210,5 × 100 cm", report.Text);
                        Assert.True(copy.IsEnabled);
                    }
                    finally { window.Close(); }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A construção da janela não terminou.");
            if (failure != null) throw failure;
        }

        private sealed class RecordingHost : ISliceHost
        {
            public (double Width, double Height) Selection = (1500, 1000);
            public List<(SlicePlan Plan, double Separation, bool DeleteOriginal)> Applied { get; } = new();

            public (double Width, double Height)? MeasureSelection(out string message)
            {
                message = string.Empty;
                return Selection;
            }

            public OperationResult? Apply(SlicePlan plan, double separationMm, bool deleteOriginal)
            {
                Applied.Add((plan, separationMm, deleteOriginal));
                return OperationResult.Success("2 parte(s) criadas.");
            }

            public List<(SliceRegionPlan Plan, double Separation, bool DeleteOriginal)> AppliedRegions { get; } = new();

            public OperationResult? ApplyRegions(SliceRegionPlan plan, double separationMm, bool deleteOriginal)
            {
                AppliedRegions.Add((plan, separationMm, deleteOriginal));
                return OperationResult.Success("partes livres criadas.");
            }

            public SliceGuideCuts? Guides { get; set; }
            public List<(IReadOnlyList<double> Columns, IReadOnlyList<double> Rows)> CreatedGuides { get; } = new();

            public SliceGuideCuts? ReadGuides(out string message)
            {
                message = "Nenhuma guia horizontal ou vertical cruza a arte selecionada.";
                return Guides;
            }

            public OperationResult? CreateGuides(IReadOnlyList<double> columns, IReadOnlyList<double> rows)
            {
                CreatedGuides.Add((columns, rows));
                return OperationResult.Success("guias criadas");
            }

            public OperationResult? RemoveGuides() => OperationResult.Success("guias removidas");

            public SliceArtOutline? Outline { get; set; }
            public int OutlineReads { get; private set; }

            public SliceArtOutline? ReadArtOutline(out string message)
            {
                OutlineReads++;
                message = "Selecione a arte que vai ser dividida.";
                return Outline;
            }

            public SliceDrawnCuts? DrawnCuts { get; set; }
            public int CutLayersCreated { get; private set; }

            public SliceDrawnCuts? ReadDrawnCuts(out string message)
            {
                message = "Não há a camada “Cortes (Slice)” nesta página.";
                return DrawnCuts;
            }

            public OperationResult? CreateCutLayer()
            {
                CutLayersCreated++;
                return OperationResult.Success("Camada “Cortes (Slice)” criada.");
            }

            public SliceThumbnail? Thumbnail { get; set; }
            public int ThumbnailReads { get; private set; }

            public SliceThumbnail? ReadArtThumbnail(out string message)
            {
                ThumbnailReads++;
                message = "Não foi possível gerar a miniatura da arte — teste.";
                return Thumbnail;
            }
        }

        /// <summary>PNG de verdade (2 × 1 pixels), para a janela abrir.</summary>
        private static byte[] Png()
        {
            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(2, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null,
                new byte[] { 0, 0, 255, 255, 255, 0, 0, 255 }, 8);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var stream = new System.IO.MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        [Fact]
        public void ColorThumbnail_IsReadOnlyWhenAsked_AndDrawnUnderTheParts()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new RecordingHost { Selection = (1500, 1000) };
                    var window = new SlicePreviewWindow(1500, 1000, host);
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        void Click(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var canvas = Find<Canvas>("PreviewCanvas");
                        Find<TextBox>("Rows").Text = "1";
                        Find<TextBox>("Columns").Text = "2";

                        // Desligada: o Corel não é chamado.
                        Click("UseSelectionButton");
                        Assert.Equal(0, host.ThumbnailReads);
                        Assert.Empty(canvas.Children.OfType<Image>());

                        // Falha: só a mensagem; o plano continua.
                        Find<CheckBox>("ShowColors").IsChecked = true;
                        Assert.Equal(1, host.ThumbnailReads);
                        Assert.Equal("Não foi possível gerar a miniatura da arte — teste.", Find<TextBlock>("ArtText").Text);
                        Assert.Empty(canvas.Children.OfType<Image>());
                        Assert.True(Find<Button>("ApplyButton").IsEnabled);

                        // Nova seleção com a opção marcada: lê de novo e desenha por baixo das partes, no tamanho da arte.
                        host.Thumbnail = new SliceThumbnail(Png(), 750, 1000);
                        Click("UseSelectionButton");
                        Assert.Equal(2, host.ThumbnailReads);
                        var image = Assert.Single(canvas.Children.OfType<Image>());
                        Assert.Equal(200, image.Width, 6);
                        Assert.Equal(400.0 * 1000 / 1500, image.Height, 6);
                        Assert.Equal(0, canvas.Children.IndexOf(image));
                        Assert.Equal(2, canvas.Children.OfType<Rectangle>().Count());

                        // Mudar um campo não gera de novo; desmarcar esconde.
                        Find<TextBox>("Columns").Text = "3";
                        Assert.Equal(2, host.ThumbnailReads);
                        Assert.Single(canvas.Children.OfType<Image>());
                        Find<CheckBox>("ShowColors").IsChecked = false;
                        Assert.Empty(canvas.Children.OfType<Image>());
                        Find<CheckBox>("ShowColors").IsChecked = true;
                        Assert.Equal(2, host.ThumbnailReads);
                        Assert.Single(canvas.Children.OfType<Image>());
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
        public void DrawnLines_MakePolygonParts_ATCutIncluded_AndApplyGoesToTheRegionsHost()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new RecordingHost { Selection = (1000, 600) };
                    var window = new SlicePreviewWindow(1000, 600, host);
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        void Click(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var report = Find<TextBox>("ReportText");
                        var canvas = Find<Canvas>("PreviewCanvas");
                        Find<TextBox>("Overlap").Text = "0";

                        // Sem a camada: explica e não deixa dividir.
                        Find<RadioButton>("PathMode").IsChecked = true;
                        Assert.Equal("Não há a camada “Cortes (Slice)” nesta página.", Find<TextBlock>("PathText").Text);
                        Assert.False(Find<Button>("ApplyButton").IsEnabled);
                        Click("CreateCutLayerButton");
                        Assert.Equal(1, host.CutLayersCreated);
                        Assert.Equal("Camada “Cortes (Slice)” criada.", Find<TextBlock>("MessageText").Text);

                        // Corte vertical inteiro e um horizontal que para nele (T), mais uma linha solta.
                        SlicePoint[] P(params double[] xy) => Enumerable.Range(0, xy.Length / 2).Select(i => new SlicePoint(xy[2 * i], xy[2 * i + 1])).ToArray();
                        host.DrawnCuts = new SliceDrawnCuts(new IReadOnlyList<SlicePoint>[] { P(500, 0, 500, 600), P(0, 300, 499, 300), P(700, 100, 800, 100) }, 1);
                        Click("ReadPathsButton");
                        Assert.Equal("3 linha(s) lida(s) da camada “Cortes (Slice)”; 1 objeto(s) sem traçado (texto/imagem), ignorado(s).", Find<TextBlock>("PathText").Text);
                        Assert.Contains("Método: linhas desenhadas (camada “Cortes (Slice)”)", report.Text);
                        Assert.Contains("3 linha(s) desenhada(s) (1 não separa(m) nada: solta(s), fora da arte ou laço fechado por dentro): 3 parte(s)", report.Text);
                        Assert.Equal(3, canvas.Children.OfType<Polygon>().Count());
                        Assert.Equal(3, canvas.Children.OfType<Polyline>().Count());

                        Click("ApplyButton");
                        var applied = Assert.Single(host.AppliedRegions);
                        Assert.Equal(3, applied.Plan.Regions.Count);
                        Assert.Empty(host.Applied);

                        // Nada que divida: a mensagem do planejador.
                        host.DrawnCuts = new SliceDrawnCuts(new IReadOnlyList<SlicePoint>[] { P(700, 100, 800, 100) }, 0);
                        Click("ReadPathsButton");
                        Assert.StartsWith("Nenhuma linha desenhada divide a arte", Find<TextBlock>("MessageText").Text);
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
        public void SlantedGuides_MakeAFreePlan_WithPolygonParts_AndApplyGoesToTheRegionsHost()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new RecordingHost { Selection = (1000, 1000) };
                    var window = new SlicePreviewWindow(1000, 1000, host);
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        void Click(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var report = Find<TextBox>("ReportText");
                        var canvas = Find<Canvas>("PreviewCanvas");
                        Find<TextBox>("Overlap").Text = "0";

                        // Uma diagonal e uma vertical no meio: 4 partes (2 triângulos de cada lado da vertical).
                        host.Guides = new SliceGuideCuts(new[] { 500.0 }, new double[0], 0, new[] { new SliceLine(0, 0, 1000, 1000) });
                        Click("ReadGuidesButton");
                        Assert.Equal("1 guia(s) vertical(is), 0 horizontal(is) e 1 inclinada(s) sobre a arte.", Find<TextBlock>("GuideText").Text);
                        Assert.Contains("Método: guias da página, com inclinadas (linhas de corte livres)", report.Text);
                        Assert.Contains("2 linha(s) de corte: 4 parte(s)", report.Text);
                        Assert.Equal(4, canvas.Children.OfType<Polygon>().Count());
                        Assert.Empty(canvas.Children.OfType<Rectangle>());
                        Assert.True(Find<Button>("ApplyButton").IsEnabled);

                        Find<TextBox>("Separation").Text = "2";
                        Click("ApplyButton");
                        var applied = Assert.Single(host.AppliedRegions);
                        Assert.Equal((4, 20.0, false), (applied.Plan.Regions.Count, applied.Separation, applied.DeleteOriginal));
                        Assert.Empty(host.Applied);
                        Assert.Equal("partes livres criadas.", Find<TextBlock>("MessageText").Text);
                        Assert.False(Find<Button>("ApplyButton").IsEnabled);

                        // Linhas que formam uma parte fina: explica e não deixa dividir.
                        host.Guides = new SliceGuideCuts(new[] { 500.0, 500.3 }, new double[0], 0, new[] { new SliceLine(0, 0, 1000, 1000) });
                        Click("ReadGuidesButton");
                        Assert.StartsWith("Uma parte perto de", Find<TextBlock>("MessageText").Text);
                        Assert.False(Find<Button>("ApplyButton").IsEnabled);
                        Assert.Empty(report.Text);

                        // Sem inclinadas: volta ao plano retangular de sempre.
                        host.Guides = new SliceGuideCuts(new[] { 500.0 }, new double[0], 0);
                        Click("ReadGuidesButton");
                        Assert.Contains("Método: guias da página" + Environment.NewLine, report.Text);
                        Assert.Equal(2, canvas.Children.OfType<Rectangle>().Count());
                    }
                    finally { window.Close(); }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A janela não terminou.");
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        [Fact]
        public void ArtOutline_IsDrawnUnderTheParts_AndCanBeHidden()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new RecordingHost();
                    var window = new SlicePreviewWindow(1500, 1000, host);
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        void Click(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var canvas = Find<Canvas>("PreviewCanvas");
                        var art = Find<TextBlock>("ArtText");
                        Find<TextBox>("Rows").Text = "1";
                        Find<TextBox>("Columns").Text = "2";
                        Assert.Empty(canvas.Children.OfType<Polygon>());

                        // Sem arte lida: a mensagem do painel, e o plano continua.
                        Click("UseSelectionButton");
                        Assert.Equal(1, host.OutlineReads);
                        Assert.Equal("Selecione a arte que vai ser dividida.", art.Text);
                        Assert.True(Find<Button>("ApplyButton").IsEnabled);

                        // Um triângulo de 150 × 100 cm (mm a partir do canto da seleção) e um furo quadrado.
                        host.Outline = new SliceArtOutline(new IReadOnlyList<NestPoint>[]
                        {
                            new[] { new NestPoint(0, 1000), new NestPoint(750, 0), new NestPoint(1500, 1000) },
                            new[] { new NestPoint(700, 600), new NestPoint(800, 600), new NestPoint(800, 700), new NestPoint(700, 700) },
                        }, 1500, 1000, approximate: true, trimmed: false);
                        Click("UseSelectionButton");
                        Assert.Equal("Contorno da arte na prévia (texto, imagem ou contorno ilegível pela caixa).", art.Text);
                        var polygons = canvas.Children.OfType<Polygon>().ToList();
                        Assert.Equal(2, polygons.Count);
                        // Desenhada antes das partes (as partes ficam por cima).
                        Assert.True(canvas.Children.IndexOf(polygons[1]) < canvas.Children.IndexOf(canvas.Children.OfType<Rectangle>().First()));
                        // Escala da prévia: 400 px no lado maior (1500 mm).
                        Assert.Equal(new Point(200, 0), polygons[0].Points[1]);
                        Assert.Equal(400, polygons[0].Points[2].X, 6);
                        Assert.Equal(400.0 * 1000 / 1500, polygons[0].Points[2].Y, 6);
                        Assert.Equal(2, canvas.Children.OfType<Rectangle>().Count());

                        Find<CheckBox>("ShowArt").IsChecked = false;
                        Assert.Empty(canvas.Children.OfType<Polygon>());
                        Assert.Equal(2, canvas.Children.OfType<Rectangle>().Count());
                        Find<CheckBox>("ShowArt").IsChecked = true;
                        Assert.Equal(2, canvas.Children.OfType<Polygon>().Count());

                        host.Outline = new SliceArtOutline(Array.Empty<IReadOnlyList<NestPoint>>(), 1500, 1000, false, false);
                        Click("UseSelectionButton");
                        Assert.Equal("A arte não tem contorno para mostrar.", art.Text);
                        Assert.Empty(canvas.Children.OfType<Polygon>());
                    }
                    finally { window.Close(); }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A janela não terminou.");
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        [Fact]
        public void Guides_CreateFromThePlan_ThenDivideAlongTheGuidesRead()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new RecordingHost();
                    var window = new SlicePreviewWindow(3000, 1000, host);
                    try
                    {
                        T Find<T>(string name) => (T)window.FindName(name);
                        void Click(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        var report = Find<TextBox>("ReportText");
                        var message = Find<TextBlock>("MessageText");
                        Find<TextBox>("Rows").Text = "1";
                        Find<TextBox>("Columns").Text = "3";

                        Click("CreateGuidesButton");
                        var (columns, rows) = Assert.Single(host.CreatedGuides);
                        Assert.Equal(new[] { 1000.0, 2000.0 }, columns);
                        Assert.Empty(rows);
                        Assert.Equal("guias criadas", message.Text);

                        // Sem guias sobre a arte: explica e não deixa dividir.
                        Find<RadioButton>("GuideMode").IsChecked = true;
                        Assert.Equal("Nenhuma guia horizontal ou vertical cruza a arte selecionada.", Find<TextBlock>("GuideText").Text);
                        Assert.False(Find<Button>("ApplyButton").IsEnabled);

                        // O usuário arrastou as guias: 80 e 250 cm da esquerda, 40 do topo.
                        host.Guides = new SliceGuideCuts(new[] { 800.0, 2500 }, new[] { 400.0 }, 1);
                        Click("ReadGuidesButton");
                        Assert.Equal("2 guia(s) vertical(is), 1 horizontal(is) e 0 inclinada(s) sobre a arte; 1 fora dela, ignorada(s).", Find<TextBlock>("GuideText").Text);
                        Assert.Contains("Método: guias da página", report.Text);
                        Assert.Contains("2 linha(s) × 3 coluna(s): 6 parte(s)", report.Text);
                        Assert.Contains("L1 C2: X 79,25; Y 0; 171,5 × 40,75 cm", report.Text);
                        Assert.True(Find<Button>("ApplyButton").IsEnabled);

                        Click("ApplyButton");
                        Assert.Equal(6, Assert.Single(host.Applied).Plan.Tiles.Count);
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
        public void Apply_SendsThePlanToTheHost_OnceUntilSomethingChanges()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var host = new RecordingHost();
                    var window = new SlicePreviewWindow(2000, 1000, host);
                    try
                    {
                        var apply = (Button)window.FindName("ApplyButton");
                        var separation = (TextBox)window.FindName("Separation");
                        var message = (TextBlock)window.FindName("MessageText");
                        void Click(string name) => ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                        Assert.True(apply.IsEnabled);
                        separation.Text = "2,5";
                        Click("ApplyButton");
                        var (plan, sep, delete) = Assert.Single(host.Applied);
                        Assert.Equal((2000.0, 1000.0, 2), (plan.Width, plan.Height, plan.Tiles.Count));
                        Assert.Equal(25, sep, 6);
                        Assert.False(delete);
                        Assert.Equal("2 parte(s) criadas.", message.Text);
                        Assert.False(apply.IsEnabled);

                        // Nova seleção: mede de novo e libera outra divisão.
                        Click("UseSelectionButton");
                        Assert.Equal("150", ((TextBox)window.FindName("ArtworkWidth")).Text);
                        Assert.True(apply.IsEnabled);

                        separation.Text = "-1";
                        Click("ApplyButton");
                        Assert.Single(host.Applied);
                        Assert.Equal("O afastamento entre as partes precisa ser 0 ou mais.", message.Text);
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
