#if NET48
// Só compila na perna net48 — PXToolBoxDev.Integrations só existe lá. Testa
// Integrations/AtomicFileWriter.cs (gravação segura/tolerante a bloqueio, compartilhada entre
// ListForgeIntegratedSettingsStore e ListForgeProcessingHistoryService — ver
// docs/ARCHITECTURE.md, correção do erro "Não foi possível remover o arquivo a ser
// substituído" ao salvar perfis de trabalho).
//
// Todos os testes aqui usam um ARQUIVO/DIRETÓRIO TEMPORÁRIO próprio (nunca o arquivo real de
// configuração do PX DEV, que outras classes de teste também tocam) — mesmo com bloqueios
// reais (FileShare.None numa FileStream de verdade, nunca simulado), isolado o suficiente para
// nunca colidir com outro teste sob a paralelização padrão do xUnit entre classes.
using System;
using System.IO;
using System.Threading;
using PXToolBoxDev.Integrations;
using Xunit;

namespace PXToolBox.Tests
{
    // Sem [Collection]: todos os testes abaixo usam um diretório temporário próprio por teste
    // (nunca o arquivo real de configuração nem nenhum seam estático de teste de outra classe —
    // ver TestCollections.cs, comentário de SharedFilesystemCollection, "auditoria pós-correção")
    // e o lock estático de AtomicFileWriter (Sync) já se auto-sincroniza corretamente sem
    // precisar de isolamento extra entre classes de teste.
    public class AtomicFileWriterTests
    {
        private static string NewTempDestinationPath() =>
            Path.Combine(Path.GetTempPath(), $"pxtoolbox-atomicwrite-{Guid.NewGuid():N}", "arquivo.json");

        [Fact]
        public void WriteTextSafely_FirstWrite_NoExistingFile_UsesPlainMove_Succeeds()
        {
            var destination = NewTempDestinationPath();
            var directory = Path.GetDirectoryName(destination)!;
            try
            {
                AtomicFileWriter.WriteTextSafely(destination, "conteudo-1");

                Assert.True(File.Exists(destination));
                Assert.Equal("conteudo-1", File.ReadAllText(destination));
                Assert.Empty(Directory.GetFiles(directory, "*.tmp-*"));
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        [Fact]
        public void WriteTextSafely_Update_ExistingFile_ReplacesContent()
        {
            var destination = NewTempDestinationPath();
            var directory = Path.GetDirectoryName(destination)!;
            try
            {
                AtomicFileWriter.WriteTextSafely(destination, "conteudo-1");
                AtomicFileWriter.WriteTextSafely(destination, "conteudo-2");

                Assert.Equal("conteudo-2", File.ReadAllText(destination));
                Assert.Empty(Directory.GetFiles(directory, "*.tmp-*"));
                Assert.Empty(Directory.GetFiles(directory, "*.bak-*"));
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        [Fact]
        public void WriteTextSafely_TransientLockOnDestination_RetriesAndEventuallySucceeds()
        {
            var destination = NewTempDestinationPath();
            var directory = Path.GetDirectoryName(destination)!;
            try
            {
                // Garante que já existe um arquivo válido (força o caminho de ATUALIZAÇÃO —
                // File.Replace/fallback — não o de primeiro salvamento).
                AtomicFileWriter.WriteTextSafely(destination, "conteudo-original");

                using var lockAcquired = new ManualResetEventSlim(false);
                using var releaseLockNow = new ManualResetEventSlim(false);
                Exception? lockThreadException = null;

                var lockThread = new Thread(() =>
                {
                    try
                    {
                        // FileShare.None: nenhum outro handle (nem para apagar) é permitido
                        // enquanto este stream estiver aberto — a mesma condição real que faz
                        // File.Replace falhar com "Não foi possível remover o arquivo a ser
                        // substituído".
                        using var lockStream = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.None);
                        lockAcquired.Set();
                        releaseLockNow.Wait(TimeSpan.FromSeconds(5));
                    }
                    catch (Exception ex)
                    {
                        lockThreadException = ex;
                        lockAcquired.Set();
                    }
                });
                lockThread.IsBackground = true;
                lockThread.Start();

                Assert.True(lockAcquired.Wait(TimeSpan.FromSeconds(5)), "A thread de bloqueio não conseguiu sinalizar a tempo.");
                Assert.Null(lockThreadException);

                // Libera o bloqueio ANTES do orçamento total de retentativas (0+60+200ms) se
                // esgotar, mas depois da primeira tentativa — exercita de verdade o caminho de
                // retentativa, não só o caminho feliz sem contenção nenhuma. Usa uma Thread
                // dedicada (Thread.Sleep), NUNCA System.Threading.Timer: o callback de Timer é
                // despachado pelo ThreadPool, que pode sofrer starvation sob carga paralela de
                // outras classes de teste rodando ao mesmo tempo (xUnit paraleliza coleções
                // diferentes por padrão) — nesse caso o release chegaria atrasado mesmo com os
                // 80ms "corretos" agendados, derrubando o orçamento de retentativa por um motivo
                // alheio à lógica real sendo testada. Uma Thread dedicada é agendada pelo SO,
                // não pela fila do ThreadPool, e não sofre esse tipo de atraso.
                var releaseThread = new Thread(() =>
                {
                    Thread.Sleep(80);
                    releaseLockNow.Set();
                });
                releaseThread.IsBackground = true;
                releaseThread.Start();
                try
                {
                    AtomicFileWriter.WriteTextSafely(destination, "conteudo-novo");
                }
                finally
                {
                    releaseLockNow.Set();
                    releaseThread.Join(TimeSpan.FromSeconds(5));
                    lockThread.Join(TimeSpan.FromSeconds(5));
                }

                Assert.Equal("conteudo-novo", File.ReadAllText(destination));
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        [Fact]
        public void WriteTextSafely_DestinationLockedForLongerThanAllRetries_ThrowsAndPreservesOriginalFile()
        {
            var destination = NewTempDestinationPath();
            var directory = Path.GetDirectoryName(destination)!;
            try
            {
                AtomicFileWriter.WriteTextSafely(destination, "conteudo-original");

                using var lockStream = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.None);

                Assert.ThrowsAny<IOException>(() => AtomicFileWriter.WriteTextSafely(destination, "conteudo-que-nao-deveria-entrar"));

                lockStream.Dispose();

                // O arquivo válido anterior precisa estar EXATAMENTE como estava — nunca
                // truncado, nunca substituído parcialmente, nunca removido.
                Assert.Equal("conteudo-original", File.ReadAllText(destination));

                // Nenhum arquivo temporário/backup órfão deveria sobrar na pasta.
                Assert.Empty(Directory.GetFiles(directory, "*.tmp-*"));
                Assert.Empty(Directory.GetFiles(directory, "*.bak-*"));
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        [Fact]
        public void WriteTextSafely_ConcurrentCallsFromMultipleThreads_NeverCorruptsFile_AndAllAttemptsSucceedOrThrowCleanly()
        {
            var destination = NewTempDestinationPath();
            var directory = Path.GetDirectoryName(destination)!;
            try
            {
                AtomicFileWriter.WriteTextSafely(destination, "conteudo-0");

                const int threadCount = 8;
                var exceptions = new Exception?[threadCount];
                var threads = new Thread[threadCount];

                for (var i = 0; i < threadCount; i++)
                {
                    var index = i;
                    threads[i] = new Thread(() =>
                    {
                        try
                        {
                            AtomicFileWriter.WriteTextSafely(destination, $"conteudo-{index}");
                        }
                        catch (Exception ex)
                        {
                            exceptions[index] = ex;
                        }
                    });
                }

                foreach (var t in threads) t.Start();
                foreach (var t in threads) t.Join(TimeSpan.FromSeconds(10));

                // O objetivo do lock interno é serializar, não garantir que toda tentativa
                // concorrente tenha sucesso (contenção externa ainda pode acontecer) — o que
                // nunca pode acontecer é uma exceção de corrupção, nem um arquivo final
                // ilegível/parcialmente escrito.
                Assert.All(exceptions, ex => Assert.True(ex == null || ex is IOException, $"Exceção inesperada: {ex}"));

                var finalContent = File.ReadAllText(destination);
                Assert.Matches("^conteudo-[0-7]$", finalContent);
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // Melhor esforço — não falha o teste por não conseguir limpar o próprio diretório temporário.
            }
        }
    }
}
#endif
