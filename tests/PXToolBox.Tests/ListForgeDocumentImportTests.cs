#if NET48
// Só compila na perna net48 (ver cabeçalho de ListForgeIntegratedTests.cs) — é o único
// framework onde o host do PX (PXToolBoxDev) e sua referência ao núcleo compartilhado do
// ListForge (ListForge.Core, via ListForgeCoreProjectPath) estão disponíveis.
//
// Testa ListForge.Core.FileImporter e ListForge.Services.FileImportService — movidos do
// standalone para o núcleo compartilhado nesta rodada (ver docs/listforge-classic-parity.md,
// "Importação de documentos") — usando arquivos PDF/Word/Excel reais, construídos neste próprio
// teste (sem depender de nenhum arquivo externo versionado): PdfPig/DocumentFormat.OpenXml/
// ClosedXML sabem ler esse formato; construir um arquivo mínimo válido aqui e lê-lo de volta com
// o MESMO código de produção prova a extração de ponta a ponta, sem precisar de um binário de
// terceiros guardado no repositório.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ListForge.Core;
using ListForge.Services;
using Xunit;

namespace PXToolBox.Tests
{
    public class ListForgeDocumentImportTests
    {
        [Fact]
        public void ReadPdfText_ExtractsLiteralTextIncludingLeadingZeros()
        {
            var path = TempPath(".pdf");
            try
            {
                BuildMinimalPdf(path, "ANA,07,G");

                var text = FileImporter.ReadPdfText(path);

                Assert.Equal("ANA,07,G", text);
            }
            finally
            {
                DeleteQuiet(path);
            }
        }

        [Fact]
        public void ReadPdfText_WithoutExtractableText_ThrowsExplainingScannedPdfCase()
        {
            var path = TempPath(".pdf");
            try
            {
                BuildMinimalPdf(path, "");

                var ex = Assert.Throws<InvalidOperationException>(() => FileImporter.ReadPdfText(path));
                Assert.Contains("PDF escaneado", ex.Message);
            }
            finally
            {
                DeleteQuiet(path);
            }
        }

        [Fact]
        public void ReadDocxText_ExtractsParagraphsAndTableRows_PreservingAccentsAndLeadingZeros()
        {
            var path = TempPath(".docx");
            try
            {
                BuildDocx(path,
                    paragraphs: new[] { "João Pedro" },
                    tableRows: new[]
                    {
                        new[] { "MARIA", "07", "BLM" },
                        new[] { "CARLOS", "10", "G" },
                    });

                var text = FileImporter.ReadDocxText(path);
                var lines = text.Split('\n');

                Assert.Equal("João Pedro", lines[0]);
                Assert.Contains("MARIA,07,BLM", lines);
                Assert.Contains("CARLOS,10,G", lines);
            }
            finally
            {
                DeleteQuiet(path);
            }
        }

        [Fact]
        public void ReadDocxText_EmptyDocument_ThrowsUsefulMessage()
        {
            var path = TempPath(".docx");
            try
            {
                BuildDocx(path, paragraphs: Array.Empty<string>(), tableRows: null);

                var ex = Assert.Throws<InvalidOperationException>(() => FileImporter.ReadDocxText(path));
                Assert.Contains("Word", ex.Message);
            }
            finally
            {
                DeleteQuiet(path);
            }
        }

        [Fact]
        public void ReadExcelText_ExtractsFirstWorksheetRows_PreservingLeadingZerosAsText()
        {
            var path = TempPath(".xlsx");
            try
            {
                BuildXlsx(path, new[]
                {
                    new[] { "ANA", "07", "G" },
                    new[] { "JOÃO", "10", "BLM" },
                });

                var text = FileImporter.ReadExcelText(path);
                var lines = text.Split('\n');

                Assert.Equal("ANA,07,G", lines[0]);
                Assert.Equal("JOÃO,10,BLM", lines[1]);
            }
            finally
            {
                DeleteQuiet(path);
            }
        }

        [Fact]
        public void ReadExcelText_UsesOnlyFirstWorksheet_SameRuleAsStandalone()
        {
            var path = TempPath(".xlsx");
            try
            {
                using (var wb = new XLWorkbook())
                {
                    var ws1 = wb.Worksheets.Add("Primeira");
                    ws1.Cell(1, 1).SetValue("DA_PRIMEIRA");
                    var ws2 = wb.Worksheets.Add("Segunda");
                    ws2.Cell(1, 1).SetValue("DA_SEGUNDA");
                    wb.SaveAs(path);
                }

                var text = FileImporter.ReadExcelText(path);

                Assert.Equal("DA_PRIMEIRA", text);
            }
            finally
            {
                DeleteQuiet(path);
            }
        }

        [Fact]
        public void FileImportService_ImportInputFile_RoutesEachExtensionToTheRightReader()
        {
            var dir = CreateTempDir();
            try
            {
                var txtPath = Path.Combine(dir, "lista.txt");
                File.WriteAllText(txtPath, "ANA,10,G");

                var pdfPath = Path.Combine(dir, "lista.pdf");
                BuildMinimalPdf(pdfPath, "BIA,11,M");

                var docxPath = Path.Combine(dir, "lista.docx");
                BuildDocx(docxPath, paragraphs: new[] { "CLARA,08,P" }, tableRows: null);

                var xlsxPath = Path.Combine(dir, "lista.xlsx");
                BuildXlsx(xlsxPath, new[] { new[] { "DIEGO", "09", "GG" } });

                var service = new FileImportService();

                var txtResult = service.ImportInputFile(txtPath);
                Assert.True(txtResult.Success);
                Assert.True(txtResult.Value!.IsPlainText);
                Assert.Equal("ANA,10,G", txtResult.Value.Text);
                Assert.Null(txtResult.Value.ReviewMessage);

                var pdfResult = service.ImportInputFile(pdfPath);
                Assert.True(pdfResult.Success);
                Assert.False(pdfResult.Value!.IsPlainText);
                Assert.Equal("BIA,11,M", pdfResult.Value.Text);
                Assert.Contains("PDF", pdfResult.Value.ReviewMessage);

                var docxResult = service.ImportInputFile(docxPath);
                Assert.True(docxResult.Success);
                Assert.Equal("CLARA,08,P", docxResult.Value!.Text);
                Assert.Contains("Word", docxResult.Value.ReviewMessage);

                var xlsxResult = service.ImportInputFile(xlsxPath);
                Assert.True(xlsxResult.Success);
                Assert.Equal("DIEGO,09,GG", xlsxResult.Value!.Text);
                Assert.Contains("planilha", xlsxResult.Value.ReviewMessage);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [Fact]
        public void FileImportService_ImportInputFile_UnsupportedExtension_FailsWithoutThrowing()
        {
            var dir = CreateTempDir();
            try
            {
                var path = Path.Combine(dir, "lista.xyz");
                File.WriteAllText(path, "ANA,10,G");
                var service = new FileImportService();

                var result = service.ImportInputFile(path);

                Assert.False(result.Success);
                Assert.Equal("UnsupportedFileFormat", result.ErrorCode);
                Assert.Null(result.Exception);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [Fact]
        public void FileImportService_ImportInputFile_CorruptedDocx_FailsWithReadErrorNeverFakeSuccess()
        {
            var path = TempPath(".docx");
            try
            {
                File.WriteAllText(path, "isto não é um arquivo .docx de verdade");
                var service = new FileImportService();

                var result = service.ImportInputFile(path);

                Assert.False(result.Success);
                Assert.Equal("ImportFileFailed", result.ErrorCode);
                Assert.NotNull(result.Exception);
            }
            finally
            {
                DeleteQuiet(path);
            }
        }

        // -----------------------------------------------------------------
        // Construtores de arquivo de teste — cada um usa a MESMA biblioteca que o código de
        // produção usa para ler, só para gerar um arquivo real e válido sem depender de um
        // binário de terceiros guardado no repositório.
        // -----------------------------------------------------------------
        private static void BuildMinimalPdf(string path, string text)
        {
            var objects = new List<byte[]>
            {
                Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
                Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
                Encoding.ASCII.GetBytes("<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 4 0 R >> >> /MediaBox [0 0 300 300] /Contents 5 0 R >>"),
                Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
            };

            var escaped = text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            var streamBody = Encoding.ASCII.GetBytes(
                string.IsNullOrEmpty(text) ? "BT ET" : $"BT /F1 12 Tf 10 250 Td ({escaped}) Tj ET");
            var streamObj = Encoding.ASCII.GetBytes($"<< /Length {streamBody.Length} >>\nstream\n")
                .Concat(streamBody)
                .Concat(Encoding.ASCII.GetBytes("\nendstream"))
                .ToArray();
            objects.Add(streamObj);

            using (var stream = new MemoryStream())
            {
                void Write(string s) => stream.Write(Encoding.ASCII.GetBytes(s), 0, Encoding.ASCII.GetByteCount(s));
                void WriteBytes(byte[] b) => stream.Write(b, 0, b.Length);

                Write("%PDF-1.4\n");
                var offsets = new List<long>();
                for (var i = 0; i < objects.Count; i++)
                {
                    offsets.Add(stream.Position);
                    Write($"{i + 1} 0 obj\n");
                    WriteBytes(objects[i]);
                    Write("\nendobj\n");
                }

                var xrefOffset = stream.Position;
                var n = objects.Count + 1;
                Write($"xref\n0 {n}\n");
                Write("0000000000 65535 f \n");
                foreach (var off in offsets)
                    Write($"{off:0000000000} 00000 n \n");
                Write($"trailer\n<< /Size {n} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");

                File.WriteAllBytes(path, stream.ToArray());
            }
        }

        private static void BuildDocx(string path, string[] paragraphs, string[][]? tableRows)
        {
            using (var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
            {
                var mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = new Body();

                foreach (var text in paragraphs)
                {
                    body.AppendChild(new Paragraph(new Run(new Text(text))));
                }

                if (tableRows != null)
                {
                    var table = new Table();
                    foreach (var rowValues in tableRows)
                    {
                        var row = new TableRow();
                        foreach (var cellValue in rowValues)
                        {
                            row.AppendChild(new TableCell(new Paragraph(new Run(new Text(cellValue)))));
                        }
                        table.AppendChild(row);
                    }
                    body.AppendChild(table);
                }

                mainPart.Document.AppendChild(body);
                mainPart.Document.Save();
            }
        }

        private static void BuildXlsx(string path, string[][] rows)
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Lista");
                for (var r = 0; r < rows.Length; r++)
                {
                    for (var c = 0; c < rows[r].Length; c++)
                    {
                        // SetValue(string) grava como texto — preserva zeros à esquerda (ex.:
                        // "07"), nunca reinterpretado como número.
                        ws.Cell(r + 1, c + 1).SetValue(rows[r][c]);
                    }
                }
                wb.SaveAs(path);
            }
        }

        private static string TempPath(string extension) =>
            Path.Combine(Path.GetTempPath(), $"pxtoolbox-docimport-test-{Guid.NewGuid():N}{extension}");

        private static string CreateTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-docimport-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void DeleteQuiet(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* melhor esforço */ }
        }

        private static void DeleteTempDir(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { /* melhor esforço */ }
        }
    }
}
#endif
