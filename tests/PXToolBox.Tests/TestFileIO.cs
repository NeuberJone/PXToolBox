#if NET48
using System;
using System.IO;
using System.Threading;

namespace PXToolBox.Tests
{
    /// <summary>
    /// Tolerância a um bloqueio transitório real no sistema de arquivos, de origem NÃO
    /// identificada — nunca presuma aqui qual processo é o responsável (nenhum processo externo
    /// foi confirmado; "indexador/antivírus" seria apenas uma suposição, não uma evidência). Mesma
    /// categoria de problema que <see cref="PXToolBoxDev.Integrations.AtomicFileWriter"/> já
    /// existe para tolerar em produção (ver seu próprio comentário: "qualquer handle aberto... de
    /// qualquer processo — nunca presumido aqui qual"), aplicada aqui aos testes que tocam um
    /// arquivo diretamente com File.WriteAllText/ReadAllText/Delete — fora de AtomicFileWriter de
    /// propósito, porque alguns destes testes escrevem conteúdo inválido para simular corrupção,
    /// nunca passando pelo caminho de gravação real do Store.
    ///
    /// <para><b>Evidência real, não presumida</b>: duas falhas capturadas com stack trace
    /// completo em
    /// <see cref="ListForgeIntegratedSettingsStoreTests.Load_CorruptedJson_QuarantinesFileAndReturnsDefaults_WithoutOverwritingSilently"/>,
    /// já DEPOIS de toda disputa ENTRE CLASSES diferentes sobre o mesmo caminho real ter sido
    /// eliminada por <see cref="SharedFilesystemCollection"/> (na época, ainda contra o caminho
    /// real em <c>%LocalAppData%\PXToolBox\Dev\config\</c> — desde a correção descrita na mesma
    /// seção, <see cref="ListForgeIntegratedSettingsStoreTests"/> usa um diretório temporário
    /// isolado por instância, nunca mais aquele caminho) — uma falha em
    /// <c>File.WriteAllText(RealFilePath, garbage)</c> (a primeira linha do teste), outra em
    /// <c>File.ReadAllText(quarantine)</c> logo depois de <c>Load()</c> criar o arquivo de
    /// quarentena. Ambas "IOException: O processo não pode acessar o arquivo... porque ele está
    /// sendo usado por outro processo" — a mesma mensagem que motivou o AtomicFileWriter
    /// original, aqui contra um bloqueio transitório de origem não identificada (nunca atribuído
    /// a um processo específico, nem precisa ser, pela mesma razão que AtomicFileWriter nunca
    /// presume a causa). Mantido como rede de segurança geral mesmo após o isolamento por
    /// diretório temporário, por ser um fenômeno do sistema de arquivos do Windows em si (pode
    /// ocorrer em qualquer pasta recém-tocada, não só na real), não um sintoma específico do
    /// caminho real compartilhado que motivou sua criação.</para>
    /// </summary>
    internal static class TestFileIO
    {
        private static readonly int[] DelaysMs = { 0, 50, 150 };

        public static void WriteAllTextWithRetry(string path, string content)
        {
            Retry(() => File.WriteAllText(path, content));
        }

        public static string ReadAllTextWithRetry(string path)
        {
            string? result = null;
            Retry(() => result = File.ReadAllText(path));
            return result!;
        }

        public static void DeleteWithRetry(string path)
        {
            Retry(() =>
            {
                if (File.Exists(path)) File.Delete(path);
            });
        }

        private static void Retry(Action action)
        {
            for (var attempt = 0; attempt < DelaysMs.Length; attempt++)
            {
                if (attempt > 0) Thread.Sleep(DelaysMs[attempt]);
                try
                {
                    action();
                    return;
                }
                catch (IOException) when (attempt < DelaysMs.Length - 1)
                {
                    // Bloqueio transitório externo — ver comentário da classe. Tenta de novo;
                    // na última tentativa, a exceção propaga normalmente (nunca engole um erro
                    // real de verdade).
                }
            }
        }
    }
}
#endif
