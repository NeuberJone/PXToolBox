#if NET48
// Só compila na perna net48 (ver cabeçalho de ListForgeIntegratedTests.cs). Testa
// Integrations/ListForgeProcessingHistoryService.cs, do host do PX (PXToolBoxDev). Usa sempre um
// arquivo em pasta temporária própria (construtor com path+clock injetáveis, mesmo padrão de
// ListForge.Tests/ProcessingHistoryServiceTests.cs no standalone) — nunca toca no arquivo real em
// %LocalAppData%\PXToolBox\Dev\config\.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.ListForgeIntegrated;
using Xunit;

namespace PXToolBox.Tests
{
    /// <summary>
    /// Histórico de processamentos do ListForge Integrado (ver
    /// docs/listforge-classic-parity.md, grupo "Revisão e resumos") — porta de
    /// ListForge.Services.ProcessingHistoryService. Reproduz os mesmos casos de
    /// ListForge.Tests/ProcessingHistoryServiceTests.cs (ordem mais recente primeiro, limite de
    /// 100 registros sem deduplicação, metadados seguros, arquivo inválido preservado, Limpar
    /// histórico não afeta o arquivo de saída em si, "Abrir pasta" com arquivo ausente), mais os
    /// casos específicos desta porta (BuildSource, isolamento do caminho do PX DEV).
    /// </summary>
    [Collection("PXToolBox Dev shared filesystem resources")]
    public class ListForgeProcessingHistoryServiceTests
    {
        [Fact]
        public void Add_SavesAndLoadsEntriesNewestFirst()
        {
            var dir = CreateTempDir();
            var path = Path.Combine(dir, "history.json");
            var clock = new Queue<DateTimeOffset>(new[]
            {
                new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero),
            });
            var service = new ListForgeProcessingHistoryService(path, () => clock.Dequeue());

            try
            {
                var first = service.Add(NewEntry("lista-a.txt", 2, @"C:\Saidas\lista-a.txt"));
                var second = service.Add(NewEntry("lista-b.txt", 3, @"C:\Saidas\lista-b.txt"));

                Assert.True(first.Success);
                Assert.True(second.Success);

                var loaded = service.Load();
                Assert.Equal(2, loaded.Count);
                Assert.Equal("lista-b.txt", loaded[0].SourceDisplayName);
                Assert.Equal("lista-a.txt", loaded[1].SourceDisplayName);
                Assert.Equal(3, loaded[0].ProcessedLineCount);
                Assert.Equal(@"C:\Saidas\lista-b.txt", loaded[0].OutputPath);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [Fact]
        public void Add_KeepsOnlyOneHundredEntriesWithoutDeduplication()
        {
            var dir = CreateTempDir();
            var path = Path.Combine(dir, "history.json");
            var current = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
            var service = new ListForgeProcessingHistoryService(path, () => current = current.AddMinutes(1));

            try
            {
                for (var i = 0; i < 105; i++)
                {
                    var result = service.Add(NewEntry("lista.txt", i, $@"C:\Saidas\lista-{i}.txt"));
                    Assert.True(result.Success);
                }

                var loaded = service.Load();
                Assert.Equal(ListForgeProcessingHistoryService.MaxEntries, loaded.Count);
                Assert.Equal(@"C:\Saidas\lista-104.txt", loaded[0].OutputPath);
                Assert.Equal(@"C:\Saidas\lista-5.txt", loaded[loaded.Count - 1].OutputPath);
                Assert.Equal(100, loaded.Count(entry => entry.SourceDisplayName == "lista.txt"));
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [Fact]
        public void Add_StoresOnlySafeSourceMetadata()
        {
            var dir = CreateTempDir();
            var path = Path.Combine(dir, "history.json");
            var service = new ListForgeProcessingHistoryService(path);

            try
            {
                var result = service.Add(NewEntry(
                    @"C:\Users\neube\Documents\entrada-real-secreta.xlsx",
                    1,
                    @"C:\Saidas\lista.txt",
                    ListForgeProcessingHistorySourceTypes.File));

                Assert.True(result.Success);
                var loaded = service.Load().Single();
                var rawJson = File.ReadAllText(path);

                Assert.Equal("entrada-real-secreta.xlsx", loaded.SourceDisplayName);
                Assert.DoesNotContain(@"C:\Users\neube\Documents", rawJson);
                Assert.DoesNotContain("ANA,10,G", rawJson);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [Fact]
        public void BuildSource_File_UsesFileNameWithoutPath()
        {
            var source = ListForgeProcessingHistoryService.BuildSource(
                ListForgeProcessingHistorySourceTypes.File,
                @"C:\Pedidos\cliente-secreto\lista-2026.txt");

            Assert.Equal("lista-2026.txt", source.DisplayName);
            Assert.Equal(ListForgeProcessingHistorySourceTypes.File, source.SourceType);
        }

        [Theory]
        [InlineData(ListForgeProcessingHistorySourceTypes.Link, "Lista extraída de link")]
        [InlineData(ListForgeProcessingHistorySourceTypes.EditedJson, "JSON editado")]
        [InlineData(ListForgeProcessingHistorySourceTypes.EditedOutput, "Lista de saída editada")]
        [InlineData(ListForgeProcessingHistorySourceTypes.PastedText, "Texto colado")]
        public void BuildSource_NonFileTypes_UseFixedFriendlyNames(string sourceType, string expectedDisplayName)
        {
            var source = ListForgeProcessingHistoryService.BuildSource(sourceType, currentInputFile: null);

            Assert.Equal(expectedDisplayName, source.DisplayName);
            Assert.Equal(sourceType, source.SourceType);
        }

        [Fact]
        public void Load_InvalidFileStartsEmptyAndPreservesInvalidFile()
        {
            var dir = CreateTempDir();
            var path = Path.Combine(dir, "history.json");
            File.WriteAllText(path, "{ invalid json");
            var service = new ListForgeProcessingHistoryService(path);

            try
            {
                var loaded = service.Load();

                Assert.Empty(loaded);
                Assert.False(File.Exists(path));
                Assert.Single(Directory.GetFiles(dir, "history.json.invalid-*"));
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [Fact]
        public void Clear_RemovesOnlyHistoryMetadata_NeverTheOutputFileItself()
        {
            var dir = CreateTempDir();
            var path = Path.Combine(dir, "history.json");
            var outputPath = Path.Combine(dir, "saida.txt");
            File.WriteAllText(outputPath, "ANA,10,G");
            var service = new ListForgeProcessingHistoryService(path);

            try
            {
                Assert.True(service.Add(NewEntry("lista.txt", 1, outputPath)).Success);

                var clear = service.Clear();

                Assert.True(clear.Success);
                Assert.Empty(service.Load());
                Assert.True(File.Exists(outputPath));
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [Fact]
        public void Add_MissingOutputPath_FailsWithoutWritingEntry()
        {
            var dir = CreateTempDir();
            var path = Path.Combine(dir, "history.json");
            var service = new ListForgeProcessingHistoryService(path);

            try
            {
                var result = service.Add(NewEntry("lista.txt", 1, outputPath: ""));

                Assert.False(result.Success);
                Assert.Empty(service.Load());
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [Fact]
        public void OpenOutputFolder_MissingFileReturnsFriendlyFailure()
        {
            var dir = CreateTempDir();
            var service = new ListForgeProcessingHistoryService(Path.Combine(dir, "history.json"));

            try
            {
                var result = service.OpenOutputFolder(NewEntry("lista.txt", 1, @"C:\Saidas\arquivo-inexistente.txt"));

                Assert.False(result.Success);
                Assert.Equal("O arquivo de saída não foi encontrado.", result.UserMessage);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [Fact]
        public void DefaultPath_IsUnderPxToolBoxDevConfig_NeverTheStandaloneAppDir()
        {
            var expectedSuffix = Path.Combine("PXToolBox", "Dev", "config", "listforge-integrated-history.json");
            Assert.EndsWith(expectedSuffix, ListForgeProcessingHistoryService.DefaultPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Mesma classe de regressão de
        /// ListForgeIntegratedTests.ClassicWorkspaceWindow_ConstructsWithoutThrowing (crash real
        /// relatado pelo usuário ao abrir uma janela nova deste módulo) — constrói a janela real
        /// (sem mostrar) numa thread STA dedicada, com um histórico vazio em pasta temporária
        /// (nunca o arquivo real do usuário).</summary>
        [Fact]
        public void ProcessingHistoryWindow_ConstructsWithoutThrowing_WithEmptyHistory()
        {
            var dir = CreateTempDir();
            try
            {
                var service = new ListForgeProcessingHistoryService(Path.Combine(dir, "history.json"));
                Exception? caught = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        _ = new ProcessingHistoryWindow(service);
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();

                Assert.Null(caught);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        /// <summary>Mesmo mecanismo acima, mas com registros reais já presentes — garante que o
        /// data-binding da DataGrid e o estado inicial dos botões ("Abrir pasta da saída"/
        /// "Limpar histórico") não lançam ao construir com dados.</summary>
        [Fact]
        public void ProcessingHistoryWindow_ConstructsWithoutThrowing_WithExistingEntries()
        {
            var dir = CreateTempDir();
            try
            {
                var service = new ListForgeProcessingHistoryService(Path.Combine(dir, "history.json"));
                service.Add(NewEntry("lista.txt", 3, Path.Combine(dir, "saida.txt")));

                Exception? caught = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        _ = new ProcessingHistoryWindow(service);
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();

                Assert.Null(caught);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        private static ListForgeProcessingHistoryEntry NewEntry(
            string sourceDisplayName,
            int processedLineCount,
            string outputPath,
            string sourceType = ListForgeProcessingHistorySourceTypes.PastedText) =>
            new ListForgeProcessingHistoryEntry
            {
                SourceDisplayName = sourceDisplayName,
                SourceType = sourceType,
                ProcessedLineCount = processedLineCount,
                OutputPath = outputPath,
            };

        private static string CreateTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-history-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void DeleteTempDir(string dir)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Pastas temporárias de teste podem ser limpas pelo SO depois.
            }
        }
    }
}
#endif
