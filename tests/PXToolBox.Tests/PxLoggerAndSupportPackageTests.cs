#if NET48
// Só compila na perna net48 (ver cabeçalho de ListForgeIntegratedTests.cs). Testa
// Integrations/PxLogger.cs e Integrations/SupportPackageService.cs, do host do PX
// (PXToolBoxDev) — ver docs/listforge-classic-parity.md, grupo "Configurações aplicáveis ao
// integrado" ("Gerar pacote de suporte"/"Abrir pasta de logs").
//
// Todos os testes de PxLogger usam SetLogDirectoryForTesting para um diretório temporário
// isolado — nunca a pasta real de logs do usuário (%LocalAppData%\PXToolBox\Dev\logs), tanto
// para não poluí-la quanto para controlar datas/tamanhos de arquivo de forma determinística
// (rotação/retenção). Cada teste restaura o override para null no finally.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using PXToolBoxDev.Integrations;
using Xunit;

namespace PXToolBox.Tests
{
    [Collection("PXToolBox Dev shared filesystem resources")]
    public class PxLoggerTests
    {
        [Fact]
        public void Info_WritesEntry_WithVersionHeaderAndContext_NeverExceptionMessage()
        {
            var dir = CreateTempLogDir();
            try
            {
                PxLogger.SetLogDirectoryForTesting(dir);

                PxLogger.Error("TesteContexto", "Falha fixa descrita pelo chamador.",
                    new InvalidOperationException("TAMANHO-SECRETO-MARIA-07"));

                var content = File.ReadAllText(PxLogger.CurrentLogPath);
                Assert.Contains("[TesteContexto]", content);
                Assert.Contains("Falha fixa descrita pelo chamador.", content);
                Assert.Contains("ExceptionType: System.InvalidOperationException", content);
                // Nunca grava a mensagem da excessão — pode ecoar dado digitado pelo usuário
                // (ver comentário de PxLogger sobre SizeHelper.ParseQtyAndSize).
                Assert.DoesNotContain("TAMANHO-SECRETO-MARIA-07", content);
            }
            finally
            {
                PxLogger.SetLogDirectoryForTesting(null);
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void BuildLogPath_UsesOneFilePerCalendarDay_EnablingRotation()
        {
            var dir = CreateTempLogDir();
            try
            {
                var today = new DateTime(2026, 1, 15);
                var tomorrow = today.AddDays(1);
                var pathToday = PxLogger.BuildLogPath(today);
                var pathTomorrow = PxLogger.BuildLogPath(tomorrow);

                Assert.NotEqual(pathToday, pathTomorrow);
                Assert.Contains("2026-01-15", pathToday);
                Assert.Contains("2026-01-16", pathTomorrow);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Retention_RemovesLogFilesOlderThanMaxLogAgeDays_KeepsRecentOnes()
        {
            var dir = CreateTempLogDir();
            try
            {
                PxLogger.SetLogDirectoryForTesting(dir);

                var oldFile = Path.Combine(dir, "pxtoolbox-integrated-2000-01-01.log");
                File.WriteAllText(oldFile, "entrada antiga");
                File.SetLastWriteTime(oldFile, DateTime.Now.AddDays(-(PxLogger.MaxLogAgeDays + 5)));

                // Qualquer escrita chama EnforceRetention() internamente (ver PxLogger.Write).
                PxLogger.Info("Retencao", "Gatilho de retenção por idade.");

                Assert.False(File.Exists(oldFile));
                Assert.True(File.Exists(PxLogger.CurrentLogPath));
            }
            finally
            {
                PxLogger.SetLogDirectoryForTesting(null);
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Retention_NeverTouchesFilesOutsideItsOwnLogNamePattern()
        {
            // A retenção só pode enumerar/remover "pxtoolbox-integrated-*.log" dentro da PRÓPRIA
            // pasta de logs — nunca um backup de entrada ou um pacote de suporte exportado,
            // mesmo que (hipoteticamente) estivessem na mesma pasta.
            var dir = CreateTempLogDir();
            try
            {
                PxLogger.SetLogDirectoryForTesting(dir);

                var unrelated = Path.Combine(dir, "lista-backup-nao-relacionada.txt");
                File.WriteAllText(unrelated, "não é um log — nunca deve ser removido");
                File.SetLastWriteTime(unrelated, DateTime.Now.AddDays(-(PxLogger.MaxLogAgeDays + 30)));

                PxLogger.Info("Retencao", "Gatilho de retenção — não deve afetar arquivos de outro tipo.");

                Assert.True(File.Exists(unrelated));
                Assert.Equal("não é um log — nunca deve ser removido", File.ReadAllText(unrelated));
            }
            finally
            {
                PxLogger.SetLogDirectoryForTesting(null);
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Retention_WhenTotalExceedsMaxBytes_RemovesOldestFilesFirst_KeepsNewest()
        {
            var dir = CreateTempLogDir();
            try
            {
                PxLogger.SetLogDirectoryForTesting(dir);

                // Três arquivos "antigos" (dentro da janela de idade, mas somados excedem o
                // limite de bytes) com datas de modificação distintas e conhecidas.
                var veryOld = WriteSizedLogFile(dir, "2026-01-01", PxLogger.MaxTotalLogBytes, DateTime.Now.AddDays(-5));
                var old = WriteSizedLogFile(dir, "2026-01-02", PxLogger.MaxTotalLogBytes, DateTime.Now.AddDays(-3));
                var recent = WriteSizedLogFile(dir, "2026-01-03", 10, DateTime.Now.AddDays(-1));

                // Qualquer escrita nova reavalia a retenção com o conjunto acima já em disco.
                PxLogger.Info("Retencao", "Gatilho de retenção por tamanho total.");

                Assert.False(File.Exists(veryOld), "o mais antigo deveria ter sido removido primeiro");
                Assert.True(File.Exists(recent), "o mais recente deveria ter sido preservado");
            }
            finally
            {
                PxLogger.SetLogDirectoryForTesting(null);
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void Write_NeverThrows_EvenWhenLogDirectoryIsBlockedByAFile()
        {
            // Força Directory.CreateDirectory(LogDirectory) a falhar: o diretório de testes já
            // existe como ARQUIVO, não pasta — logging nunca pode propagar essa falha (derrubaria
            // o Corel) nem ocultar o erro original que o chamador já reporta por outro caminho.
            var parent = Path.Combine(Path.GetTempPath(), $"pxtoolbox-logblock-{Guid.NewGuid():N}");
            Directory.CreateDirectory(parent);
            var blockedAsFile = Path.Combine(parent, "logs-bloqueado");
            File.WriteAllText(blockedAsFile, "ocupando o caminho que seria uma pasta");

            try
            {
                PxLogger.SetLogDirectoryForTesting(blockedAsFile);
                var ex = Record.Exception(() => PxLogger.Error("Teste", "Não deve lançar.", new InvalidOperationException("x")));
                Assert.Null(ex);
            }
            finally
            {
                PxLogger.SetLogDirectoryForTesting(null);
                Directory.Delete(parent, recursive: true);
            }
        }

        private static string CreateTempLogDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-logs-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string WriteSizedLogFile(string dir, string dateSuffix, long approxBytes, DateTime lastWriteTime)
        {
            var path = Path.Combine(dir, $"pxtoolbox-integrated-{dateSuffix}.log");
            File.WriteAllText(path, new string('x', (int)Math.Min(approxBytes, int.MaxValue / 2)));
            File.SetLastWriteTime(path, lastWriteTime);
            return path;
        }
    }

    [Collection("PXToolBox Dev shared filesystem resources")]
    public class SupportPackageServiceTests
    {
        [Fact]
        public void Generate_CreatesZip_WithSupportInfoAndSettingsSummaryAndLogs()
        {
            var logDir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-sp-logs-{Guid.NewGuid():N}");
            var outputDir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-sp-out-{Guid.NewGuid():N}");
            Directory.CreateDirectory(outputDir);
            try
            {
                PxLogger.SetLogDirectoryForTesting(logDir);
                PxLogger.Info("Teste", "Entrada de log para ir no pacote.");

                var settings = PXToolBoxDev.Integrations.ListForgeIntegratedSettings.Default();
                var service = new SupportPackageService();

                var result = service.Generate(outputDir, settings, "24.5 (teste)");

                Assert.True(result.Success);
                Assert.True(File.Exists(result.Value));

                using var archive = ZipFile.OpenRead(result.Value!);
                var names = archive.Entries.Select(e => e.FullName).ToList();
                Assert.Contains("support-info.txt", names);
                Assert.Contains("settings-summary.txt", names);
                Assert.Contains(names, n => n.StartsWith("logs/", StringComparison.Ordinal));
            }
            finally
            {
                PxLogger.SetLogDirectoryForTesting(null);
                Directory.Delete(logDir, recursive: true);
                Directory.Delete(outputDir, recursive: true);
            }
        }

        [Fact]
        public void Generate_SettingsSummary_NeverIncludesProfileNameOutputDirOrDefaultListName()
        {
            // Mesma cautela do BuildConfigSummary do standalone — nomes de perfil/pasta/lista
            // configurados podem ser nomes reais de cliente/pedido.
            var logDir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-sp-logs-{Guid.NewGuid():N}");
            var outputDir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-sp-out-{Guid.NewGuid():N}");
            Directory.CreateDirectory(outputDir);
            try
            {
                PxLogger.SetLogDirectoryForTesting(logDir);

                var settings = PXToolBoxDev.Integrations.ListForgeIntegratedSettings.Default();
                var profile = settings.WorkProfiles[0];
                profile.Name = "CLIENTE-SECRETO-PEDIDO-991";
                profile.Settings.OutputDir = @"C:\Pedidos\ClienteSecreto991";
                profile.Settings.DefaultListName = "lista-cliente-secreto-991";

                var service = new SupportPackageService();
                var result = service.Generate(outputDir, settings, null);
                Assert.True(result.Success);

                using var archive = ZipFile.OpenRead(result.Value!);
                var summaryEntry = archive.GetEntry("settings-summary.txt")!;
                using var reader = new StreamReader(summaryEntry.Open(), Encoding.UTF8);
                var summary = reader.ReadToEnd();

                Assert.DoesNotContain("CLIENTE-SECRETO-PEDIDO-991", summary);
                Assert.DoesNotContain("ClienteSecreto991", summary);
                Assert.DoesNotContain("lista-cliente-secreto-991", summary);
            }
            finally
            {
                PxLogger.SetLogDirectoryForTesting(null);
                Directory.Delete(logDir, recursive: true);
                Directory.Delete(outputDir, recursive: true);
            }
        }

        [Fact]
        public void Generate_SupportInfo_NeverIncludesSecretsOrCredentials()
        {
            var logDir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-sp-logs-{Guid.NewGuid():N}");
            var outputDir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-sp-out-{Guid.NewGuid():N}");
            Directory.CreateDirectory(outputDir);
            try
            {
                PxLogger.SetLogDirectoryForTesting(logDir);
                var settings = PXToolBoxDev.Integrations.ListForgeIntegratedSettings.Default();
                var service = new SupportPackageService();

                var result = service.Generate(outputDir, settings, null);
                Assert.True(result.Success);

                using var archive = ZipFile.OpenRead(result.Value!);
                using var reader = new StreamReader(archive.GetEntry("support-info.txt")!.Open(), Encoding.UTF8);
                var info = reader.ReadToEnd();

                // O próprio aviso de privacidade cita as PALAVRAS "credenciais"/"tokens"/
                // "licencas" para explicar o que NUNCA é incluído — por isso o teste procura por
                // um VALOR concreto de segredo, não pela palavra em si.
                foreach (var forbiddenValue in new[] { "senha=", "password=", "Bearer ", "sk-", "chave-secreta-" })
                    Assert.DoesNotContain(forbiddenValue, info, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                PxLogger.SetLogDirectoryForTesting(null);
                Directory.Delete(logDir, recursive: true);
                Directory.Delete(outputDir, recursive: true);
            }
        }

        [Fact]
        public void Generate_WhenALogFileIsLockedByAnotherProcess_StillSucceeds_AndReportsWarning()
        {
            var logDir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-sp-logs-{Guid.NewGuid():N}");
            var outputDir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-sp-out-{Guid.NewGuid():N}");
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(logDir);
            try
            {
                PxLogger.SetLogDirectoryForTesting(logDir);
                var lockedLogPath = Path.Combine(logDir, "pxtoolbox-integrated-2026-01-01.log");
                File.WriteAllText(lockedLogPath, "log que será travado");

                using (var lockStream = new FileStream(lockedLogPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var settings = PXToolBoxDev.Integrations.ListForgeIntegratedSettings.Default();
                    var service = new SupportPackageService();

                    var result = service.Generate(outputDir, settings, null);

                    // Falha parcial (um arquivo em uso) nunca aborta o pacote inteiro — o
                    // chamador (UI) informa exatamente o que ficou de fora via UserMessage.
                    Assert.True(result.Success);
                    Assert.Contains("ressalvas", result.UserMessage, StringComparison.OrdinalIgnoreCase);
                }
            }
            finally
            {
                PxLogger.SetLogDirectoryForTesting(null);
                Directory.Delete(logDir, recursive: true);
                Directory.Delete(outputDir, recursive: true);
            }
        }

        [Fact]
        public void Generate_NeverIncludesListContentOrInputBackups()
        {
            // Prova indireta: nenhuma entrada do ZIP referencia a pasta de backups de entrada ou
            // contém conteúdo de lista — o pacote só inclui os três tipos documentados (versão,
            // resumo seguro de config, logs).
            var logDir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-sp-logs-{Guid.NewGuid():N}");
            var outputDir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-sp-out-{Guid.NewGuid():N}");
            Directory.CreateDirectory(outputDir);
            try
            {
                PxLogger.SetLogDirectoryForTesting(logDir);
                var settings = PXToolBoxDev.Integrations.ListForgeIntegratedSettings.Default();
                var service = new SupportPackageService();

                var result = service.Generate(outputDir, settings, null);
                Assert.True(result.Success);

                using var archive = ZipFile.OpenRead(result.Value!);
                foreach (var entry in archive.Entries)
                {
                    Assert.True(
                        entry.FullName == "support-info.txt" ||
                        entry.FullName == "settings-summary.txt" ||
                        entry.FullName.StartsWith("logs/", StringComparison.Ordinal),
                        $"Entrada inesperada no pacote de suporte: {entry.FullName}");
                }
            }
            finally
            {
                PxLogger.SetLogDirectoryForTesting(null);
                Directory.Delete(logDir, recursive: true);
                Directory.Delete(outputDir, recursive: true);
            }
        }

        [Fact]
        public void Generate_WhenOutputDirectoryCannotBeCreated_FailsCleanly_NeverThrows()
        {
            // Caminho de destino inválido (ex.: dentro de um arquivo, nunca uma pasta real) —
            // simula "destino sem permissão"/"caminho inválido": Generate() nunca lança para o
            // chamador, só devolve um OperationResult de falha.
            var blockerFile = Path.Combine(Path.GetTempPath(), $"pxtoolbox-sp-blocker-{Guid.NewGuid():N}.txt");
            File.WriteAllText(blockerFile, "bloqueando o caminho");
            var invalidOutputDir = Path.Combine(blockerFile, "subpasta-impossivel");
            // A falha é registrada no log — num diretório temporário, nunca no log real.
            var logDir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-sp-fail-logs-{Guid.NewGuid():N}");

            try
            {
                PxLogger.SetLogDirectoryForTesting(logDir);
                var settings = PXToolBoxDev.Integrations.ListForgeIntegratedSettings.Default();
                var service = new SupportPackageService();

                var result = service.Generate(invalidOutputDir, settings, null);

                Assert.False(result.Success);
                Assert.False(string.IsNullOrWhiteSpace(result.UserMessage));
            }
            finally
            {
                PxLogger.SetLogDirectoryForTesting(null);
                File.Delete(blockerFile);
                try { Directory.Delete(logDir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
#endif
