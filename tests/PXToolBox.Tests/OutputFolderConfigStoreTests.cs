#if NET48
// Só compila na perna net48 — PXToolBoxDev (host) só existe lá. Pasta de saída padrão desta
// máquina (Integrations/OutputFolderConfigStore.cs): sempre num arquivo temporário próprio,
// nunca o real em %LocalAppData%\PXToolBox\Dev\config\.
using System;
using System.IO;
using PXToolBoxDev.Integrations;
using Xunit;

namespace PXToolBox.Tests
{
    public class OutputFolderConfigStoreTests : IDisposable
    {
        private readonly string _tempDirectory = Path.Combine(
            Path.GetTempPath(), "pxtoolbox-tests", "output-folder-" + Guid.NewGuid().ToString("N"));

        private string FilePath => Path.Combine(_tempDirectory, "config", "output-folder.txt");

        public OutputFolderConfigStoreTests()
        {
            OutputFolderConfigStore.SetFilePathForTesting(FilePath);
        }

        public void Dispose()
        {
            OutputFolderConfigStore.SetFilePathForTesting(null);
            try { Directory.Delete(_tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        [Fact]
        public void NeverDefined_ReadsNull()
        {
            Assert.Null(OutputFolderConfigStore.ReadConfiguredPath());
        }

        [Fact]
        public void SaveAndRead_RoundTrips_CreatingTheFolder_AndTrims()
        {
            OutputFolderConfigStore.SaveConfiguredPath(@"  \\servidor\Saídas Com Espaço  ");

            Assert.True(File.Exists(FilePath));
            Assert.Equal(@"\\servidor\Saídas Com Espaço", OutputFolderConfigStore.ReadConfiguredPath());
        }

        [Fact]
        public void Clear_GoesBackToNull_AndIsSafeWhenNothingWasSaved()
        {
            OutputFolderConfigStore.ClearConfiguredPath();
            OutputFolderConfigStore.SaveConfiguredPath(@"E:\Saidas");
            OutputFolderConfigStore.ClearConfiguredPath();

            Assert.Null(OutputFolderConfigStore.ReadConfiguredPath());
            Assert.False(File.Exists(FilePath));
        }

        [Fact]
        public void BlankFile_CountsAsNotDefined()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, "   ");

            Assert.Null(OutputFolderConfigStore.ReadConfiguredPath());
        }
    }
}
#endif
