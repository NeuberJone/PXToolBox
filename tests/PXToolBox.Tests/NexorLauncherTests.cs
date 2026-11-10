#if NET48
// Só compila na perna net48 do projeto de testes — PXToolBoxDev.Integrations só existe lá (ver
// ListForgeLauncherTests.cs, mesmo padrão). Nenhum teste aqui abre um processo real nem toca na
// configuração real da máquina: toda E/S é substituída por um fake em memória injetado no
// construtor de NexorLauncher — mesma abstração usada pelos testes do ListForge, sem abrir
// aplicativos repetidamente.
using System;
using System.Collections.Generic;
using System.IO;
using PXToolBoxDev.Integrations;
using Xunit;

namespace PXToolBox.Tests
{
    public class NexorLauncherTests
    {
        private sealed class FakeStore
        {
            public string? SavedPath;
            public readonly HashSet<string> ExistingFiles = new HashSet<string>();
            public int StartCount;
            public Func<string, string, int>? StartOverride;

            public NexorLauncher BuildLauncher() => new NexorLauncher(
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

            Assert.Equal(NexorLaunchStatus.NeedsPathSelection, result.Status);
            Assert.Contains("Nenhum caminho", result.Message);
            Assert.Equal(0, store.StartCount);
        }

        [Fact]
        public void TryOpenConfigured_ConfiguredPathMissing_NeedsSelection_ExplainsMovedOrRemoved()
        {
            var store = new FakeStore { SavedPath = @"C:\Removido\Nexor.exe" };
            var result = store.BuildLauncher().TryOpenConfigured();

            Assert.Equal(NexorLaunchStatus.NeedsPathSelection, result.Status);
            Assert.Contains("movido ou removido", result.Message);
            Assert.Equal(0, store.StartCount);
        }

        [Fact]
        public void TryOpenConfigured_ValidPath_Launches_WithoutPromptingAgain()
        {
            var store = new FakeStore { SavedPath = @"C:\Projetos\ProjetoJocasta\dist\Nexor-1.6.29.exe" };
            store.ExistingFiles.Add(store.SavedPath);

            var result = store.BuildLauncher().TryOpenConfigured();

            Assert.Equal(NexorLaunchStatus.Launched, result.Status);
            Assert.Contains("PID 1234", result.Message);
            Assert.Equal(1, store.StartCount);
        }

        [Fact]
        public void TryOpenConfigured_NeverClaimsReadyForUse_OnlyThatTheProcessStarted()
        {
            var store = new FakeStore { SavedPath = @"C:\Projetos\ProjetoJocasta\dist\Nexor-1.6.29.exe" };
            store.ExistingFiles.Add(store.SavedPath);

            var result = store.BuildLauncher().TryOpenConfigured();

            // A mensagem pode mencionar "pronto para uso" só para NEGAR isso explicitamente —
            // o que não pode acontecer é uma afirmação de sucesso sem essa ressalva.
            Assert.Contains("Não é possível confirmar", result.Message);
            Assert.DoesNotContain("Nexor aberto", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void OpenWithSelectedPath_ValidPath_SavesBeforeLaunching_EvenIfLaunchLaterFails()
        {
            var store = new FakeStore();
            store.ExistingFiles.Add(@"C:\Nova Pasta Com Espaços\Nexor.exe");
            store.StartOverride = (_, _) => throw new InvalidOperationException("negado");

            var result = store.BuildLauncher().OpenWithSelectedPath(@"C:\Nova Pasta Com Espaços\Nexor.exe");

            Assert.Equal(NexorLaunchStatus.LaunchFailed, result.Status);
            Assert.Contains("Caminho salvo", result.Message);
            Assert.Equal(@"C:\Nova Pasta Com Espaços\Nexor.exe", store.SavedPath);
        }

        [Fact]
        public void OpenWithSelectedPath_NonexistentPath_FailsWithoutSaving()
        {
            var store = new FakeStore();
            var result = store.BuildLauncher().OpenWithSelectedPath(@"C:\Nao\Existe.exe");

            Assert.Equal(NexorLaunchStatus.LaunchFailed, result.Status);
            Assert.Null(store.SavedPath);
            Assert.Equal(0, store.StartCount);
        }

        [Fact]
        public void OpenWithSelectedPath_PathWithSpaces_UsesExecutableDirectoryAsWorkingDirectory()
        {
            var store = new FakeStore();
            var path = @"C:\Nova Pasta Com Espaços\Nexor.exe";
            store.ExistingFiles.Add(path);

            string? capturedWorkingDirectory = null;
            store.StartOverride = (exePath, workingDirectory) =>
            {
                capturedWorkingDirectory = workingDirectory;
                return 999;
            };

            var result = store.BuildLauncher().OpenWithSelectedPath(path);

            Assert.Equal(NexorLaunchStatus.Launched, result.Status);
            Assert.Equal(@"C:\Nova Pasta Com Espaços", capturedWorkingDirectory);
        }

        [Fact]
        public void LaunchFailure_DoesNotStopFutureAttempts_LauncherHasNoInternalState()
        {
            var store = new FakeStore { SavedPath = @"C:\Projetos\ProjetoJocasta\dist\Nexor-1.6.29.exe" };
            store.ExistingFiles.Add(store.SavedPath);
            store.StartOverride = (_, _) => throw new Exception("instável");

            var launcher = store.BuildLauncher();
            var first = launcher.TryOpenConfigured();
            Assert.Equal(NexorLaunchStatus.LaunchFailed, first.Status);

            store.StartOverride = null; // simula o processo voltando a funcionar numa nova tentativa
            var second = launcher.TryOpenConfigured();
            Assert.Equal(NexorLaunchStatus.Launched, second.Status);
        }
    }

    /// <summary>
    /// Desde a correção de isolamento de testes (ver docs/ARCHITECTURE.md, seção 31): aponta
    /// <see cref="NexorConfigStore"/> E <see cref="ListForgeConfigStore"/> (este teste exercita
    /// os dois, para comprovar que são independentes) para arquivos dentro de um diretório
    /// temporário próprio desta classe, nunca os caminhos reais em
    /// <c>%LocalAppData%\PXToolBox\Dev\config\</c>. Elimina tanto a disputa entre classes de
    /// teste quanto o risco de tocar a configuração real enquanto o PX Tool Box DEV está aberto
    /// de verdade na mesma máquina. Por isso não depende mais de
    /// <see cref="SharedFilesystemCollection"/> — mas, por também mutar
    /// <see cref="ListForgeConfigStore"/>._filePathOverride (campo estático compartilhado com
    /// <see cref="ListForgeConfigStoreTests"/>), precisa da coleção abaixo (ver
    /// TestCollections.LauncherConfigPathCollection).
    /// </summary>
    [Collection("ListForge/Nexor launcher config-path static test seam")]
    public class NexorConfigStoreTests : IDisposable
    {
        private readonly string _tempDirectory = Path.Combine(
            Path.GetTempPath(), "pxtoolbox-tests", "nexor-config-" + Guid.NewGuid().ToString("N"));

        public NexorConfigStoreTests()
        {
            Directory.CreateDirectory(_tempDirectory);
            NexorConfigStore.SetFilePathForTesting(Path.Combine(_tempDirectory, "nexor-path.txt"));
            ListForgeConfigStore.SetFilePathForTesting(Path.Combine(_tempDirectory, "listforge-path.txt"));
        }

        public void Dispose()
        {
            NexorConfigStore.SetFilePathForTesting(null);
            ListForgeConfigStore.SetFilePathForTesting(null);
            try { Directory.Delete(_tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        [Fact]
        public void SaveAndRead_RoundTrips_SimulatingPersistenceAcrossRestarts()
        {
            var samplePath = @"C:\Nova Pasta Com Espaços\Nexor.exe";
            NexorConfigStore.SaveConfiguredPath(samplePath);

            Assert.Equal(samplePath, NexorConfigStore.ReadConfiguredPath());
        }

        [Fact]
        public void ConfiguredPath_LivesUnderLocalApplicationData_NotRoamingProfile()
        {
            // "pertence à máquina, não ao perfil portátil" (pedido do usuário): confirma o
            // caminho real de produção (DefaultFilePathForTesting, que ignora qualquer override)
            // sem nunca ler/gravar o arquivo real — só o caminho em si é comparado.
            Assert.StartsWith(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                NexorConfigStore.DefaultFilePathForTesting);
            Assert.DoesNotContain("Roaming", NexorConfigStore.DefaultFilePathForTesting);

            // E confirma que o round-trip real da classe usa o caminho de teste isolado acima,
            // nunca o real.
            NexorConfigStore.SaveConfiguredPath(@"C:\Qualquer\Nexor.exe");
            Assert.True(File.Exists(Path.Combine(_tempDirectory, "nexor-path.txt")));
        }

        [Fact]
        public void NexorConfig_IsIndependentFromListForgeConfig_SeparateFiles()
        {
            // "preservando configurações separadas para cada aplicativo" (pedido do usuário):
            // salvar um caminho do Nexor nunca deve afetar o caminho já configurado do
            // ListForge, e vice-versa — são dois arquivos distintos (neste teste, dois arquivos
            // distintos dentro do mesmo diretório temporário isolado).
            ListForgeConfigStore.SaveConfiguredPath(@"C:\Projetos\ListForge\ListForge.exe");
            NexorConfigStore.SaveConfiguredPath(@"C:\Projetos\ProjetoJocasta\dist\Nexor-1.6.29.exe");

            Assert.Equal(@"C:\Projetos\ListForge\ListForge.exe", ListForgeConfigStore.ReadConfiguredPath());
            Assert.Equal(@"C:\Projetos\ProjetoJocasta\dist\Nexor-1.6.29.exe", NexorConfigStore.ReadConfiguredPath());
        }
    }
}
#endif
