using System;
using System.IO;
using System.Linq;
using System.Text;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Log próprio do modo Clássico do ListForge Integrado — ver docs/listforge-classic-parity.md,
    /// grupo "Configurações aplicáveis ao integrado" ("Gerar pacote de suporte"/"Abrir pasta de
    /// logs"), e docs/ARCHITECTURE.md. Grava em
    /// <c>%LocalAppData%\PXToolBox\Dev\logs\pxtoolbox-integrated-&lt;data&gt;.log</c> — pasta
    /// irmã de <c>config\</c> (já usada por <see cref="ListForgeIntegratedSettingsStore"/>/
    /// <see cref="ListForgeConfigStore"/>), dentro do mesmo diretório de dados próprio do PX DEV.
    /// Nunca lê/grava nada do ListForge Standalone, do PX de produção ou do MoldMotor.
    ///
    /// <para><b>Nunca registrado por padrão</b>: conteúdo de listas, nomes/números de jogadores,
    /// conteúdo da área de transferência, credenciais/tokens/licenças. Por isso cada chamador
    /// decide o texto de <c>message</c> (sempre um texto fixo descrevendo o QUE falhou, nunca
    /// dados do usuário) e esta classe nunca grava <see cref="Exception.Message"/> nem
    /// <c>InnerException.Message"</c> de nenhuma excessão — só o TIPO (nome completo) e o
    /// <see cref="Exception.StackTrace"/> (nomes de método/arquivo de código-fonte/linha, nunca
    /// dado do usuário). Uma excessão de validação de lista pode ecoar o próprio token digitado
    /// na mensagem (ex.: "Tamanho inválido: MARIA" se o usuário digitou um nome onde um tamanho
    /// era esperado) — por isso a mensagem em si nunca é gravada, só o tipo.</para>
    ///
    /// <para>Nunca lança: uma falha ao gravar o log (disco cheio, pasta sem permissão, arquivo em
    /// uso por outro processo) é silenciosamente ignorada aqui — logging nunca pode derrubar o
    /// Corel nem ocultar o erro original que o chamador já reporta ao usuário por outro caminho
    /// (MessageBox/status). Ver também <see cref="SupportPackageService"/>, que inclui os logs
    /// recentes dentro da política de retenção abaixo.</para>
    /// </summary>
    public static class PxLogger
    {
        public const int MaxLogAgeDays = 14;
        public const long MaxTotalLogBytes = 20L * 1024 * 1024;

        private static readonly object Sync = new();
        private static string? _logDirectoryOverride;

        public static string LogDirectory =>
            _logDirectoryOverride ?? System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PXToolBox", "Dev", "logs");

        public static string CurrentLogPath => BuildLogPath(DateTime.Now);

        public static string BuildLogPath(DateTime date) =>
            System.IO.Path.Combine(LogDirectory, $"pxtoolbox-integrated-{date:yyyy-MM-dd}.log");

        /// <summary>Só para testes — aponta o logger para um diretório temporário isolado, nunca
        /// o real do usuário.</summary>
        public static void SetLogDirectoryForTesting(string? directory) => _logDirectoryOverride = directory;

        public static void Info(string context, string message, string? relatedFilePath = null) =>
            Write("INFO", context, message, null, relatedFilePath);

        public static void Warning(string context, string message, Exception? exception = null, string? relatedFilePath = null) =>
            Write("WARN", context, message, exception, relatedFilePath);

        public static void Error(string context, string message, Exception? exception = null, string? relatedFilePath = null) =>
            Write("ERROR", context, message, exception, relatedFilePath);

        private static void Write(string level, string context, string message, Exception? exception, string? relatedFilePath)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                var entry = FormatEntry(level, context, message, exception, relatedFilePath);

                lock (Sync)
                {
                    File.AppendAllText(CurrentLogPath, entry, new UTF8Encoding(false));
                    EnforceRetention();
                }
            }
            catch
            {
                // Logging nunca pode derrubar o Corel nem mascarar o erro original — ver
                // comentário da classe. Não há nada de útil a fazer aqui além de desistir desta
                // gravação específica.
            }
        }

        private static string FormatEntry(string level, string context, string message, Exception? exception, string? relatedFilePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [PX Tool Box DEV {PxVersionInfo.PxToolBoxVersion} / ListForge.Core {PxVersionInfo.ListForgeCoreVersion}] [{context}]");
            sb.AppendLine($"Message: {message}");

            if (!string.IsNullOrWhiteSpace(relatedFilePath))
                sb.AppendLine($"File: {relatedFilePath}");

            if (exception != null)
            {
                // Deliberadamente SEM exception.Message/InnerException.Message — ver comentário
                // da classe sobre por que uma mensagem de excessão de validação de lista pode
                // ecoar o próprio dado do usuário.
                sb.AppendLine($"ExceptionType: {exception.GetType().FullName}");
                sb.AppendLine($"HResult: 0x{exception.HResult:X8}");
                if (exception.InnerException != null)
                {
                    sb.AppendLine($"InnerExceptionType: {exception.InnerException.GetType().FullName}");
                    sb.AppendLine($"InnerHResult: 0x{exception.InnerException.HResult:X8}");
                }
                if (!string.IsNullOrWhiteSpace(exception.StackTrace))
                    sb.AppendLine($"StackTrace: {exception.StackTrace}");
            }

            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// Rotação/retenção (ver docs/ARCHITECTURE.md): remove arquivos de log com mais de
        /// <see cref="MaxLogAgeDays"/> dias e, se o total ainda exceder
        /// <see cref="MaxTotalLogBytes"/>, remove os mais antigos (por data de modificação) até
        /// caber no limite. Só enumera/remove arquivos que casam com o nome exato de log deste
        /// módulo ("pxtoolbox-integrated-*.log") — nunca a pasta de configuração, nunca um pacote
        /// de suporte exportado pelo usuário (sempre salvo fora desta pasta, por escolha
        /// explícita dele num diálogo). Chamada depois de cada gravação (dentro do mesmo try/catch
        /// de <see cref="Write"/>) — nunca lança para quem chamou.
        /// </summary>
        private static void EnforceRetention()
        {
            if (!Directory.Exists(LogDirectory)) return;

            var cutoff = DateTime.Now.AddDays(-MaxLogAgeDays);
            var files = Directory.GetFiles(LogDirectory, "pxtoolbox-integrated-*.log")
                .Select(path => new FileInfo(path))
                .ToList();

            foreach (var file in files.Where(f => f.LastWriteTime < cutoff).ToList())
            {
                TryDelete(file);
                files.Remove(file);
            }

            var totalBytes = files.Sum(f => f.Length);
            if (totalBytes <= MaxTotalLogBytes) return;

            foreach (var file in files.OrderBy(f => f.LastWriteTime))
            {
                if (totalBytes <= MaxTotalLogBytes) break;
                totalBytes -= file.Length;
                TryDelete(file);
            }
        }

        private static void TryDelete(FileInfo file)
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // Arquivo em uso por outro processo/sem permissão — tenta de novo na próxima
                // gravação; nunca interrompe a retenção dos demais arquivos por conta de um só.
            }
        }
    }
}
