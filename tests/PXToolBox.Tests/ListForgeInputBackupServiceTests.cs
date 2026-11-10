#if NET48
// Só compila na perna net48 (ver cabeçalho de ListForgeIntegratedTests.cs). Testa
// Integrations/ListForgeInputBackupService.cs, do host do PX (PXToolBoxDev).
using System;
using System.IO;
using PXToolBoxDev.Integrations;
using Xunit;

namespace PXToolBox.Tests
{
    /// <summary>
    /// Backup automático de "Salvar entrada"/"Salvar como" (ver
    /// docs/listforge-classic-parity.md, grupo "Entrada e importação") — porta de
    /// ConfigManager.CreateBackup do standalone. A pasta real de backups
    /// (%LocalAppData%\PXToolBox\Dev\ListForge\Backups) é usada de verdade (igual a
    /// ListForgeIntegratedSettingsStoreTests) — cada teste cria arquivos com nomes exclusivos
    /// (prefixo de teste + Guid) e remove exatamente o que criou no fim, nunca a pasta inteira
    /// (que pode conter backups reais de uso do add-on).
    /// </summary>
    public class ListForgeInputBackupServiceTests
    {
        [Fact]
        public void CreateBackup_CopiesContentExactly_WithoutChangingFormat()
        {
            var sourceDir = CreateTempDir();
            var createdBackups = new System.Collections.Generic.List<string>();
            try
            {
                var source = Path.Combine(sourceDir, $"lista-{Guid.NewGuid():N}.txt");
                const string content = "JOAO,07,G\nMARIA,10,BLM";
                File.WriteAllText(source, content);

                var backupPath = ListForgeInputBackupService.CreateBackup(source);
                createdBackups.Add(backupPath);

                Assert.True(File.Exists(backupPath));
                Assert.Equal(content, File.ReadAllText(backupPath));
                Assert.Equal(ListForgeInputBackupService.BackupDirectory, Path.GetDirectoryName(backupPath));
                Assert.EndsWith(".txt", backupPath);
            }
            finally
            {
                CleanUp(sourceDir, createdBackups);
            }
        }

        [Fact]
        public void CreateBackup_ThrowsWhenSourceFileDoesNotExist()
        {
            var sourceDir = CreateTempDir();
            try
            {
                var missing = Path.Combine(sourceDir, $"nao-existe-{Guid.NewGuid():N}.txt");
                Assert.Throws<FileNotFoundException>(() => ListForgeInputBackupService.CreateBackup(missing));
            }
            finally
            {
                Directory.Delete(sourceDir, recursive: true);
            }
        }

        [Fact]
        public void CreateBackup_NeverThrowsSilently_PropagatesCopyFailure()
        {
            // Força a cópia a falhar criando, no caminho exato que CreateBackup produziria, um
            // DIRETÓRIO em vez de um arquivo — File.Copy(overwrite: false) para um caminho que
            // já existe (como qualquer entrada do sistema de arquivos) lança, nunca sobrescreve
            // nem finge sucesso. Prova indireta de "mensagem clara se o backup obrigatório
            // falhar, sem sobrescrever silenciosamente o original".
            var sourceDir = CreateTempDir();
            try
            {
                var stem = $"colisao-{Guid.NewGuid():N}";
                var source = Path.Combine(sourceDir, $"{stem}.txt");
                File.WriteAllText(source, "conteudo");

                Directory.CreateDirectory(ListForgeInputBackupService.BackupDirectory);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var blockingPath = Path.Combine(ListForgeInputBackupService.BackupDirectory, $"{stem}_{timestamp}.txt");
                Directory.CreateDirectory(blockingPath); // ocupa o caminho exato com um diretório

                try
                {
                    Assert.ThrowsAny<IOException>(() => ListForgeInputBackupService.CreateBackup(source));
                }
                finally
                {
                    Directory.Delete(blockingPath, recursive: true);
                }
            }
            finally
            {
                Directory.Delete(sourceDir, recursive: true);
            }
        }

        [Fact]
        public void ResolveUniqueBackupPath_ReturnsSameNameWhenNoCollision()
        {
            var candidate = Path.Combine(ListForgeInputBackupService.BackupDirectory, $"livre-{Guid.NewGuid():N}.txt");
            Assert.Equal(candidate, ListForgeInputBackupService.ResolveUniqueBackupPath(candidate));
        }

        [Fact]
        public void ResolveUniqueBackupPath_AppendsSuffixOnCollision_NeverOverwritingAnExistingBackup()
        {
            // Mesmo cenário descrito na tarefa: dois arquivos de ORIGEM com o mesmo nome em
            // pastas diferentes podem produzir o mesmo nome de backup (mesmo stem + mesmo
            // segundo) — a pasta de backups é plana, então isso é uma colisão real, não teórica.
            Directory.CreateDirectory(ListForgeInputBackupService.BackupDirectory);
            var taken = Path.Combine(ListForgeInputBackupService.BackupDirectory, $"colide-{Guid.NewGuid():N}.txt");
            File.WriteAllText(taken, "backup já existente — nunca deve ser sobrescrito");

            try
            {
                var resolved = ListForgeInputBackupService.ResolveUniqueBackupPath(taken);

                Assert.NotEqual(taken, resolved);
                Assert.False(File.Exists(resolved));
                // O backup "já existente" (simulando um backup anterior real) continua intacto.
                Assert.Equal("backup já existente — nunca deve ser sobrescrito", File.ReadAllText(taken));
            }
            finally
            {
                File.Delete(taken);
            }
        }

        [Fact]
        public void CreateBackup_SameFileNameInDifferentSourceFolders_NeverCollide()
        {
            var dirA = CreateTempDir();
            var dirB = CreateTempDir();
            var createdBackups = new System.Collections.Generic.List<string>();
            try
            {
                var name = $"mesmo-nome-{Guid.NewGuid():N}.txt";
                var sourceA = Path.Combine(dirA, name);
                var sourceB = Path.Combine(dirB, name);
                File.WriteAllText(sourceA, "conteudo da pasta A");
                File.WriteAllText(sourceB, "conteudo da pasta B");

                var backupA = ListForgeInputBackupService.CreateBackup(sourceA);
                var backupB = ListForgeInputBackupService.CreateBackup(sourceB);
                createdBackups.Add(backupA);
                createdBackups.Add(backupB);

                Assert.NotEqual(backupA, backupB);
                Assert.Equal("conteudo da pasta A", File.ReadAllText(backupA));
                Assert.Equal("conteudo da pasta B", File.ReadAllText(backupB));
            }
            finally
            {
                CleanUp(dirA, createdBackups);
                Directory.Delete(dirB, recursive: true);
            }
        }

        [Fact]
        public void BackupDirectory_IsUnderPxToolBoxDevListForgeBackups_NeverTheStandaloneBackupDir()
        {
            var expectedSuffix = Path.Combine("PXToolBox", "Dev", "ListForge", "Backups");
            Assert.EndsWith(expectedSuffix, ListForgeInputBackupService.BackupDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private static string CreateTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"pxtoolbox-backup-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void CleanUp(string sourceDir, System.Collections.Generic.List<string> createdBackups)
        {
            foreach (var backup in createdBackups)
            {
                try { if (File.Exists(backup)) File.Delete(backup); }
                catch { /* melhor esforço — não falha o teste por isso */ }
            }

            Directory.Delete(sourceDir, recursive: true);
        }
    }
}
#endif
