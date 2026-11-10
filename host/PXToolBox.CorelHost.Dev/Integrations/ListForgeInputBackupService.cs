using System;
using System.IO;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Backup automático de "Salvar entrada"/"Salvar como" do modo Clássico do ListForge
    /// Integrado — ver docs/listforge-classic-parity.md, grupo "Entrada e importação", porta de
    /// <c>ConfigManager.CreateBackup</c>/<c>MainViewModel.SaveInputFile</c>/<c>SaveInputAsFile</c>
    /// do standalone (<c>ConfigManager.BackupDir</c> = <c>AppDir\backups</c> lá). Grava em
    /// <c>%LocalAppData%\PXToolBox\Dev\ListForge\Backups\</c> — pasta própria do PX DEV, nunca o
    /// <c>BackupDir</c> do ListForge Standalone, nem nada do PX de produção/instalação.
    ///
    /// <para><b>Regras reproduzidas do standalone</b> (confirmadas lendo o código real): nome do
    /// backup = <c>{nome-sem-extensão}_{yyyyMMdd_HHmmss}{extensão}</c>; cópia simples do
    /// arquivo original, nunca reformatada; <c>File.Copy(..., overwrite: false)</c> — nunca
    /// sobrescreve um backup já existente. Quem decide QUANDO fazer backup é o chamador (ver
    /// <see cref="ClassicWorkspaceWindow.OnSaveInputFile"/>/<c>OnSaveInputFileAs</c>), replicando
    /// exatamente a mesma assimetria do standalone: "Salvar entrada" só faz backup quando o
    /// conteúdo em disco é DIFERENTE do que está sendo salvo (evita um backup inútil ao salvar
    /// duas vezes sem mudar nada); "Salvar como" faz backup sempre que o destino escolhido já
    /// existir, sem comparação de conteúdo (é um arquivo possivelmente não relacionado ao atual).
    ///
    /// <para><b>Retenção</b>: o standalone não tem nenhuma política de retenção/limpeza para
    /// backups (confirmado — nada em <c>ConfigManager</c> os remove). Esta classe também nunca
    /// remove um backup — acumulam indefinidamente, igual à referência, documentado aqui de
    /// propósito (decisão explícita desta tarefa: não inventar uma política que não existe na
    /// referência). Nunca usa a política de retenção dos logs (<see cref="PxLogger"/>) — são
    /// dados diferentes.</para>
    ///
    /// <para><b>Reforço desta entrega</b> (aditivo, não muda o formato nem o nome-base do
    /// standalone): como todos os backups caem numa única pasta plana, dois arquivos de ORIGEM
    /// com o mesmo nome em pastas diferentes (ex.: <c>C:\A\lista.txt</c> e
    /// <c>C:\B\lista.txt</c>) produziriam o mesmo nome de backup se copiados no mesmo segundo —
    /// <see cref="ResolveUniqueBackupPath"/> acrescenta um sufixo numérico só quando isso de
    /// fato colide, nunca alterando o nome quando não há colisão.</para>
    /// </summary>
    public static class ListForgeInputBackupService
    {
        public static string BackupDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PXToolBox", "Dev", "ListForge", "Backups");

        /// <summary>
        /// Copia <paramref name="sourceFile"/> para a pasta de backups e retorna o caminho do
        /// backup criado. Lança se o arquivo de origem não existir (chamador decide: backup só
        /// é pedido quando o arquivo já existe de verdade) ou se a cópia falhar (permissão, disco
        /// sem espaço, etc.) — nunca engole a excessão, para o chamador poder recusar sobrescrever
        /// o original sem um backup confirmado (ver comentário da classe).
        /// </summary>
        public static string CreateBackup(string sourceFile)
        {
            if (!File.Exists(sourceFile))
                throw new FileNotFoundException($"Arquivo não encontrado para backup: {sourceFile}", sourceFile);

            Directory.CreateDirectory(BackupDirectory);

            var stem = Path.GetFileNameWithoutExtension(sourceFile);
            var ext = Path.GetExtension(sourceFile);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = ResolveUniqueBackupPath(Path.Combine(BackupDirectory, $"{stem}_{timestamp}{ext}"));

            File.Copy(sourceFile, backupPath, overwrite: false);
            return backupPath;
        }

        /// <summary>Pura e pública para ser testável sem depender do relógio real (ver
        /// ListForgeInputBackupServiceTests) — mesma lógica usada internamente por
        /// <see cref="CreateBackup"/>.</summary>
        public static string ResolveUniqueBackupPath(string backupPath)
        {
            if (!File.Exists(backupPath)) return backupPath;

            var dir = Path.GetDirectoryName(backupPath)!;
            var stem = Path.GetFileNameWithoutExtension(backupPath);
            var ext = Path.GetExtension(backupPath);

            for (var index = 2; ; index++)
            {
                var candidate = Path.Combine(dir, $"{stem}_{index}{ext}");
                if (!File.Exists(candidate))
                    return candidate;
            }
        }
    }
}
