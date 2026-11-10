using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Corel.Interop.VGCore;
using CorelApplication = Corel.Interop.VGCore.Application;

namespace PXToolBoxDev.Diagnostics
{
    /// <summary>
    /// UI do inspetor de Shape.Properties — só chama <see cref="ShapePropertiesInspectorService"/>,
    /// nunca VGCore diretamente (o acesso COM fica concentrado no serviço). Ver
    /// ShapePropertiesInspectorService.cs para as garantias de só-leitura.
    /// </summary>
    public partial class ShapeInspectorWindow : System.Windows.Window
    {
        private readonly ShapePropertiesInspectorService _service = new();
        private readonly CorelApplication? _app;
        private Shape? _shape;
        private ShapePropertiesReport _lastReport = new();

        private sealed class PropertyRow
        {
            public int Position { get; set; }
            public int Index { get; set; }
            public string Name { get; set; } = "";
            public int Id { get; set; }
            public string Value { get; set; } = "";
            public string Type { get; set; } = "";
        }

        public ShapeInspectorWindow(CorelApplication? app)
        {
            InitializeComponent();
            _app = app;

            var report = _service.InspectActiveSelection(_app, out _shape);
            ApplyReport(report);
        }

        private void ApplyReport(ShapePropertiesReport report)
        {
            _lastReport = report;

            if (report.Status != ShapeSelectionStatus.Ok || report.Object == null)
            {
                ObjectSummaryText.Text = "";
                PropertiesGrid.ItemsSource = null;
                PropertiesHeaderText.Text = "Shape.Properties";
                StatusMessageText.Text = report.StatusMessage;
                StatusMessageText.Visibility = Visibility.Visible;
                FooterStatusText.Text = "";
                return;
            }

            StatusMessageText.Visibility = Visibility.Collapsed;

            var o = report.Object;
            ObjectSummaryText.Text =
                $"Nome: {o.Name}\n" +
                $"Tipo: {o.ShapeType}\n" +
                $"Camada: {o.LayerName}\n" +
                $"Página: {o.PageIndex}\n" +
                $"StaticID: {o.StaticId}\n" +
                $"Largura: {o.Width}\n" +
                $"Altura: {o.Height}";

            PropertiesHeaderText.Text = $"Shape.Properties — Count: {report.Properties.Count}";

            if (report.Properties.Count == 0)
            {
                PropertiesGrid.ItemsSource = null;
                FooterStatusText.Text = "Nenhuma Shape.Properties encontrada neste objeto.";
                return;
            }

            var rows = report.Properties.Select((entry, position) => new PropertyRow
            {
                Position = position + 1,
                Index = entry.Index,
                Name = entry.Name,
                Id = entry.Id,
                Value = entry.ReadError != null ? $"<erro ao ler: {entry.ReadError}>" : entry.ValueDisplay,
                Type = entry.ReadError != null ? "" : entry.ValueType,
            }).ToList();

            PropertiesGrid.ItemsSource = rows;
            FooterStatusText.Text = $"{rows.Count} propriedade(s).";
        }

        // "Atualizar" — pedido explícito da tarefa: relê o MESMO objeto enquanto ele continuar
        // válido; nunca troca pela seleção atual do Corel (que pode ter mudado). Se o objeto
        // original foi excluído (ou o documento fechado), o próprio serviço detecta a falha ao
        // ler os dados básicos do Shape e devolve ShapeNoLongerAvailable — a referência COM
        // guardada (_shape) não é usada de novo depois disso além desta tentativa.
        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            if (_shape == null)
            {
                // Nunca abriu com um objeto válido (ex.: estava sem seleção) — tenta resolver a
                // seleção atual de novo, como se fosse a primeira abertura.
                var report = _service.InspectActiveSelection(_app, out _shape);
                ApplyReport(report);
                return;
            }

            ApplyReport(_service.BuildReport(_shape));
        }

        private void OnCopyAllClick(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(ShapePropertiesReportFormatter.Build(_lastReport));
                FooterStatusText.Text = "Relatório copiado para a área de transferência.";
            }
            catch (Exception ex)
            {
                // Nunca afirma que copiou se não copiou (pedido explícito da tarefa).
                FooterStatusText.Text = $"Falha ao copiar: {ex.Message}";
            }
        }

        private void OnSaveReportClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Salvar relatório do Shape.Properties Inspector",
                Filter = "Arquivo de texto (*.txt)|*.txt|Todos os arquivos|*.*",
                FileName = "shape-properties-report.txt",
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dialog.FileName, ShapePropertiesReportFormatter.Build(_lastReport));
                FooterStatusText.Text = $"Relatório salvo: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                FooterStatusText.Text = $"Falha ao salvar: {ex.Message}";
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
