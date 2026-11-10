using System;
using System.IO;
using System.Text;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Gravação de texto segura contra arquivo parcialmente escrito e tolerante a bloqueio
    /// transitório no destino — infraestrutura compartilhada entre
    /// <see cref="ListForgeIntegratedSettingsStore"/> (configurações, perfis de trabalho e
    /// tamanhos customizados — os três vivem no mesmo arquivo) e
    /// <see cref="ListForgeProcessingHistoryService"/> (histórico de processamentos), que
    /// tinham cada uma sua própria cópia quase idêntica desta lógica antes desta correção.
    ///
    /// Motivo da correção: mensagem real observada ao salvar perfis de trabalho — "Não foi
    /// possível remover o arquivo a ser substituído" — é a mensagem exata que
    /// <see cref="File.Replace(string, string, string)"/> produz quando o SISTEMA OPERACIONAL
    /// não consegue remover o arquivo de destino no momento da troca (qualquer handle aberto
    /// sem FileShare.Delete nele, de qualquer processo — nunca presumido aqui qual). Grava
    /// sempre num arquivo temporário NA MESMA PASTA do destino primeiro; o arquivo anterior
    /// válido só é tocado depois do novo conteúdo estar inteiramente escrito, e nunca fica
    /// indisponível por mais tempo que uma única operação de sistema de arquivos mesmo quando a
    /// substituição precisa do caminho de contingência (<see cref="ReplaceWithManualBackup"/>).
    /// </summary>
    public static class AtomicFileWriter
    {
        /// <summary>Chamadas concorrentes para o MESMO caminho de destino, dentro deste
        /// processo, são serializadas aqui — nunca dependendo só de operações de arquivo
        /// atômicas isoladamente para evitar que duas gravações quase simultâneas disputem o
        /// mesmo arquivo temporário/destino ao mesmo tempo. Um único lock para todos os
        /// destinos (em vez de um por caminho) é suficiente: estas gravações são raras
        /// (configurações/histórico, não dados por linha) e nunca ocorrem em paralelo de
        /// verdade dentro desta aplicação.</summary>
        private static readonly object Sync = new();

        /// <summary>Grava <paramref name="content"/> em <paramref name="destinationPath"/> de
        /// forma segura. <paramref name="encoding"/> nulo usa o padrão de
        /// <see cref="File.WriteAllText(string, string)"/> (UTF-8 sem BOM).</summary>
        public static void WriteTextSafely(string destinationPath, string content, Encoding? encoding = null)
        {
            lock (Sync)
            {
                var directory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var tempPath = destinationPath + $".tmp-{Guid.NewGuid():N}";
                if (encoding != null)
                    File.WriteAllText(tempPath, content, encoding);
                else
                    File.WriteAllText(tempPath, content);

                try
                {
                    ReplaceAtomically(tempPath, destinationPath);
                }
                catch
                {
                    TryDeleteFile(tempPath);
                    throw;
                }
            }
        }

        /// <summary>
        /// Primeiro salvamento (destino ainda não existe) usa File.Move puro — nenhuma lógica de
        /// substituição é necessária, e esse caminho não costuma apresentar o erro relatado, que
        /// é específico de File.Replace. Atualização (destino já existe) tenta File.Replace
        /// algumas vezes — mais atômico quando funciona — antes de cair para um
        /// rename-com-backup manual (<see cref="ReplaceWithManualBackup"/>), mais tolerante a um
        /// bloqueio curto porque não depende da mesma chamada de sistema específica do
        /// File.Replace. As retentativas tratam qualquer contenção TRANSITÓRIA no destino de
        /// forma genérica — nunca presumem qual processo/mecanismo a estaria causando.
        /// </summary>
        private static void ReplaceAtomically(string tempPath, string destinationPath)
        {
            const int maxAttempts = 3;
            var delaysMs = new[] { 0, 60, 200 };

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (attempt > 0)
                    System.Threading.Thread.Sleep(delaysMs[attempt]);

                var isLastAttempt = attempt == maxAttempts - 1;

                if (!File.Exists(destinationPath))
                {
                    try
                    {
                        File.Move(tempPath, destinationPath);
                        return;
                    }
                    catch (IOException) when (!isLastAttempt)
                    {
                        // Pode ter sido criado por outra chamada entre o Exists e o Move — a
                        // próxima volta já trata como atualização (destino passou a existir).
                        continue;
                    }
                }

                try
                {
                    File.Replace(tempPath, destinationPath, null);
                    return;
                }
                catch (IOException) when (!isLastAttempt)
                {
                    continue;
                }
                catch (IOException)
                {
                    ReplaceWithManualBackup(tempPath, destinationPath);
                    return;
                }
            }
        }

        /// <summary>
        /// Troca manual tolerante a bloqueio: renomeia o destino atual para um nome de backup,
        /// move o temporário para o lugar do destino e só então remove o backup. Se a segunda
        /// etapa falhar por qualquer motivo, o backup é movido de volta para o nome original
        /// ANTES de propagar a exceção — o arquivo anterior válido nunca fica indisponível por
        /// mais tempo do que uma única operação de sistema de arquivos, e nunca é perdido.
        /// </summary>
        private static void ReplaceWithManualBackup(string tempPath, string destinationPath)
        {
            var backupPath = destinationPath + $".bak-{Guid.NewGuid():N}";
            File.Move(destinationPath, backupPath);

            try
            {
                File.Move(tempPath, destinationPath);
            }
            catch
            {
                File.Move(backupPath, destinationPath);
                throw;
            }

            TryDeleteFile(backupPath);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Melhor esforço — nunca mascara a falha original da gravação por não conseguir
                // limpar um arquivo temporário/backup.
            }
        }
    }
}
