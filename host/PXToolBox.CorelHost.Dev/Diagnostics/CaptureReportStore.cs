using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PXToolBoxDev.Integrations;

namespace PXToolBoxDev.Diagnostics
{
    /// <summary>
    /// Pasta dos relatórios de comparação antes/depois salvos pelo roteiro
    /// (<see cref="CaptureChecklist"/>): <c>%LocalAppData%\PXToolBox\Dev\capturas</c>, exclusiva do
    /// PX DEV. Um arquivo por relatório, nunca sobrescrito: <c>&lt;id do item&gt;__aaaaMMdd-HHmmss.txt</c>
    /// (<c>livre__…</c> sem item do roteiro). O roteiro marca como feito o item com pelo menos um
    /// arquivo. Nada aqui toca no documento do Corel.
    /// </summary>
    public static class CaptureReportStore
    {
        public const string FreeId = "livre";
        private const string Separator = "__";
        private const string StampFormat = "yyyyMMdd-HHmmss";

        private static string? _directoryOverride;

        private static string DefaultDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PXToolBox", "Dev", "capturas");

        public static string Directory => _directoryOverride ?? DefaultDirectory;

        /// <summary>Só para testes — aponta para uma pasta temporária isolada, nunca a real.</summary>
        public static void SetDirectoryForTesting(string? path) => _directoryOverride = path;

        /// <summary>Grava o relatório e devolve o caminho. Mesmo segundo: sufixo -2, -3… (nunca
        /// sobrescreve). Lança em erro de disco — quem chama mostra a mensagem.</summary>
        public static string Save(string? itemId, string text, DateTime? now = null)
        {
            var id = CaptureChecklist.Find(itemId)?.Id ?? FreeId;
            System.IO.Directory.CreateDirectory(Directory);
            var stem = $"{id}{Separator}{(now ?? DateTime.Now).ToString(StampFormat, CultureInfo.InvariantCulture)}";
            var path = Path.Combine(Directory, stem + ".txt");
            for (var n = 2; File.Exists(path); n++)
            {
                path = Path.Combine(Directory, $"{stem}-{n}.txt");
            }

            AtomicFileWriter.WriteTextSafely(path, text);
            Saved?.Invoke();
            return path;
        }

        /// <summary>Um relatório foi salvo — o roteiro aberto atualiza os ✓.</summary>
        public static event Action? Saved;

        /// <summary>Último relatório salvo de cada item do roteiro (pelo nome do arquivo). Pasta
        /// ausente ou ilegível → vazio; nunca lança.</summary>
        public static IReadOnlyDictionary<string, DateTime> SavedItems()
        {
            var result = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            string[] files;
            try
            {
                files = System.IO.Directory.Exists(Directory) ? System.IO.Directory.GetFiles(Directory, "*.txt") : Array.Empty<string>();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return result;
            }

            foreach (var name in files.Select(Path.GetFileNameWithoutExtension))
            {
                var split = name.IndexOf(Separator, StringComparison.Ordinal);
                if (split <= 0) continue;
                var id = name.Substring(0, split);
                var stamp = name.Substring(split + Separator.Length);
                if (stamp.Length < StampFormat.Length || CaptureChecklist.Find(id) == null) continue;
                if (!DateTime.TryParseExact(stamp.Substring(0, StampFormat.Length), StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when)) continue;
                if (!result.TryGetValue(id, out var current) || when > current) result[id] = when;
            }

            return result;
        }
    }
}
