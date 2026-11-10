#if NET48
// Só compila na perna net48 do projeto de testes (ver PXToolBox.Tests.csproj) — é o único
// framework onde PXToolBoxDev.Integrations existe (host/PXToolBox.CorelHost.Dev, net48/WPF).
// Nenhum teste aqui abre um processo real nem toca na configuração real da máquina: toda E/S
// é substituída por um fake em memória injetado no construtor de ListForgeLauncher.
using System;
using System.Collections.Generic;
using System.IO;
using PXToolBoxDev.Integrations;
using Xunit;

namespace PXToolBox.Tests
{
    public class ListForgeLauncherTests
    {
        private sealed class FakeStore
        {
            public string? SavedPath;
            public readonly HashSet<string> ExistingFiles = new HashSet<string>();
            public int StartCount;
            public Func<string, string, int>? StartOverride;

            public ListForgeLauncher BuildLauncher() => new ListForgeLauncher(
                () => SavedPath,
                path => SavedPath = path,
                path => ExistingFiles.Contains(path),
                (exePath, workingDirectory) =>
                {
                    StartCount++;
                    return StartOverride != null ? StartOverride(exePath, workingDirectory) : 1234;
                });
        }

        [Fact]
        public void TryOpenConfigured_NoPathEverConfigured_NeedsSelection_DoesNotStartProcess()
        {
            var store = new FakeStore();
            var result = store.BuildLauncher().TryOpenConfigured();

            Assert.Equal(ListForgeLaunchStatus.NeedsPathSelection, result.Status);
            Assert.Contains("Nenhum caminho", result.Message);
            Assert.Equal(0, store.StartCount);
        }

        [Fact]
        public void TryOpenConfigured_ConfiguredPathMissing_NeedsSelection_ExplainsMovedOrRemoved()
        {
            var store = new FakeStore { SavedPath = @"C:\Removido\ListForge.exe" };
            var result = store.BuildLauncher().TryOpenConfigured();

            Assert.Equal(ListForgeLaunchStatus.NeedsPathSelection, result.Status);
            Assert.Contains("movido ou removido", result.Message);
            Assert.Equal(0, store.StartCount);
        }

        [Fact]
        public void TryOpenConfigured_ValidPath_Launches_WithoutPromptingAgain()
        {
            var store = new FakeStore { SavedPath = @"C:\Projetos\ListForge\ListForge.exe" };
            store.ExistingFiles.Add(store.SavedPath);

            var result = store.BuildLauncher().TryOpenConfigured();

            Assert.Equal(ListForgeLaunchStatus.Launched, result.Status);
            Assert.Contains("PID 1234", result.Message);
            Assert.Equal(1, store.StartCount);
        }

        [Fact]
        public void TryOpenConfigured_NeverClaimsReadyForUse_OnlyThatTheProcessStarted()
        {
            var store = new FakeStore { SavedPath = @"C:\Projetos\ListForge\ListForge.exe" };
            store.ExistingFiles.Add(store.SavedPath);

            var result = store.BuildLauncher().TryOpenConfigured();

            // A mensagem pode mencionar "pronto para uso" só para NEGAR isso explicitamente —
            // o que não pode acontecer é uma afirmação de sucesso sem essa ressalva.
            Assert.Contains("Não é possível confirmar", result.Message);
            Assert.DoesNotContain("ListForge aberto", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void OpenWithSelectedPath_ValidPath_SavesBeforeLaunching_EvenIfLaunchLaterFails()
        {
            var store = new FakeStore();
            store.ExistingFiles.Add(@"C:\Nova Pasta Com Espaços\ListForge.exe");
            store.StartOverride = (_, _) => throw new InvalidOperationException("negado");

            var result = store.BuildLauncher().OpenWithSelectedPath(@"C:\Nova Pasta Com Espaços\ListForge.exe");

            Assert.Equal(ListForgeLaunchStatus.LaunchFailed, result.Status);
            Assert.Contains("Caminho salvo", result.Message);
            Assert.Equal(@"C:\Nova Pasta Com Espaços\ListForge.exe", store.SavedPath);
        }

        [Fact]
        public void OpenWithSelectedPath_NonexistentPath_FailsWithoutSaving()
        {
            var store = new FakeStore();
            var result = store.BuildLauncher().OpenWithSelectedPath(@"C:\Nao\Existe.exe");

            Assert.Equal(ListForgeLaunchStatus.LaunchFailed, result.Status);
            Assert.Null(store.SavedPath);
            Assert.Equal(0, store.StartCount);
        }

        [Fact]
        public void OpenWithSelectedPath_PathWithSpaces_UsesExecutableDirectoryAsWorkingDirectory()
        {
            var store = new FakeStore();
            var path = @"C:\Nova Pasta Com Espaços\ListForge.exe";
            store.ExistingFiles.Add(path);

            string? capturedWorkingDirectory = null;
            store.StartOverride = (exePath, workingDirectory) =>
            {
                capturedWorkingDirectory = workingDirectory;
                return 999;
            };

            var result = store.BuildLauncher().OpenWithSelectedPath(path);

            Assert.Equal(ListForgeLaunchStatus.Launched, result.Status);
            Assert.Equal(@"C:\Nova Pasta Com Espaços", capturedWorkingDirectory);
        }

        [Fact]
        public void LaunchFailure_DoesNotStopFutureAttempts_LauncherHasNoInternalState()
        {
            var store = new FakeStore { SavedPath = @"C:\Projetos\ListForge\ListForge.exe" };
            store.ExistingFiles.Add(store.SavedPath);
            store.StartOverride = (_, _) => throw new Exception("instável");

            var launcher = store.BuildLauncher();
            var first = launcher.TryOpenConfigured();
            Assert.Equal(ListForgeLaunchStatus.LaunchFailed, first.Status);

            store.StartOverride = null; // simula o processo voltando a funcionar numa nova tentativa
            var second = launcher.TryOpenConfigured();
            Assert.Equal(ListForgeLaunchStatus.Launched, second.Status);
        }
    }

    /// <summary>
    /// Desde a correção de isolamento de testes (ver docs/ARCHITECTURE.md, seção 31): aponta
    /// <see cref="ListForgeConfigStore"/> para um arquivo dentro de um diretório temporário
    /// próprio desta classe (<see cref="ListForgeConfigStore.SetFilePathForTesting"/>), nunca o
    /// caminho real em <c>%LocalAppData%\PXToolBox\Dev\config\listforge-path.txt</c>. Isso
    /// elimina não só a disputa entre classes de teste (já coberta por
    /// <see cref="SharedFilesystemCollection"/>), mas também o risco de ler/sobrescrever a
    /// configuração real enquanto o PX Tool Box DEV está aberto de verdade na mesma máquina —
    /// risco que a serialização entre classes não cobria, por afetar um processo externo, não
    /// outra classe de teste. Por isso esta classe NÃO precisa mais da coleção compartilhada
    /// (não toca mais o recurso que ela protege), mas permanece fora de qualquer coleção também
    /// por não ter mais nenhum recurso compartilhado com outra classe — EXCETO
    /// <see cref="PXToolBoxDev.Integrations.ListForgeConfigStore"/>._filePathOverride em si, que
    /// esta classe compartilha com <see cref="NexorConfigStoreTests"/> (ver
    /// TestCollections.LauncherConfigPathCollection), daí a coleção abaixo.
    /// </summary>
    [Collection("ListForge/Nexor launcher config-path static test seam")]
    public class ListForgeConfigStoreTests : IDisposable
    {
        private readonly string _tempDirectory = Path.Combine(
            Path.GetTempPath(), "pxtoolbox-tests", "lf-config-" + Guid.NewGuid().ToString("N"));

        public ListForgeConfigStoreTests()
        {
            Directory.CreateDirectory(_tempDirectory);
            ListForgeConfigStore.SetFilePathForTesting(Path.Combine(_tempDirectory, "listforge-path.txt"));
        }

        public void Dispose()
        {
            ListForgeConfigStore.SetFilePathForTesting(null);
            try { Directory.Delete(_tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        [Fact]
        public void SaveAndRead_RoundTrips_SimulatingPersistenceAcrossRestarts()
        {
            // ListForgeConfigStore é estático e lê sempre do disco (sem cache em memória) — o
            // round-trip abaixo já comprova persistência entre "reaberturas" do painel, sem
            // precisar recriar processo nenhum.
            var samplePath = @"C:\Nova Pasta Com Espaços\ListForge.exe";
            ListForgeConfigStore.SaveConfiguredPath(samplePath);

            Assert.Equal(samplePath, ListForgeConfigStore.ReadConfiguredPath());
        }

        [Fact]
        public void ConfiguredPath_LivesUnderLocalApplicationData_NotRoamingProfile()
        {
            // "pertence à máquina, não ao perfil portátil" (pedido do usuário): confirma o
            // caminho real de produção (DefaultFilePathForTesting, que ignora qualquer override)
            // sem nunca ler/gravar o arquivo real — só o caminho em si é comparado.
            Assert.StartsWith(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ListForgeConfigStore.DefaultFilePathForTesting);
            Assert.DoesNotContain("Roaming", ListForgeConfigStore.DefaultFilePathForTesting);

            // E confirma que o round-trip real da classe usa o caminho de teste isolado acima,
            // nunca o real.
            ListForgeConfigStore.SaveConfiguredPath(@"C:\Qualquer\ListForge.exe");
            Assert.True(File.Exists(Path.Combine(_tempDirectory, "listforge-path.txt")));
        }
    }
}
#endif
