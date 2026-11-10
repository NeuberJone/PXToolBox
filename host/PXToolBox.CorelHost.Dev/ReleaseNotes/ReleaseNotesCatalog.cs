using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace PXToolBoxDev.ReleaseNotes
{
    public static class ReleaseNotesCatalog
    {
        private const string ResourceName = "PXToolBoxDev.ReleaseNotes.release-notes.json";
        private static readonly Lazy<ReleaseNotesEntry[]> Entries = new Lazy<ReleaseNotesEntry[]>(LoadEntries);

        public static ReleaseNotesEntry ForVersion(string version)
        {
            foreach (var entry in Entries.Value)
            {
                if (string.Equals(entry.Version, version, StringComparison.Ordinal))
                    return entry;
            }

            var notice = version.EndsWith("-dev.local", StringComparison.Ordinal)
                ? "Build local de desenvolvimento (" + version + "): não há notas de versão publicadas atribuídas a ela."
                : "Não há notas cadastradas para a versão “" + version + "”. Notas de outras versões não são exibidas.";
            return new ReleaseNotesEntry(version, notice,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        }

        private static ReleaseNotesEntry[] LoadEntries()
        {
            using (var stream = typeof(ReleaseNotesCatalog).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException("Catálogo de notas da versão não encontrado no assembly DEV."))
            using (var reader = new StreamReader(stream))
            {
                var entries = JsonConvert.DeserializeObject<CatalogEntry[]>(reader.ReadToEnd())
                    ?? throw new InvalidOperationException("Catálogo de notas da versão vazio ou inválido.");
                return entries.Select(entry => new ReleaseNotesEntry(entry.Version, "",
                    Array.AsReadOnly(entry.Implemented), Array.AsReadOnly(entry.Changed), Array.AsReadOnly(entry.Removed)))
                    .ToArray();
            }
        }

        private sealed class CatalogEntry
        {
            public string Version { get; set; } = "";
            public string[] Implemented { get; set; } = Array.Empty<string>();
            public string[] Changed { get; set; } = Array.Empty<string>();
            public string[] Removed { get; set; } = Array.Empty<string>();
        }
    }

    public sealed class ReleaseNotesEntry
    {
        internal ReleaseNotesEntry(string version, string notice,
            IReadOnlyList<string> implemented, IReadOnlyList<string> changed, IReadOnlyList<string> removed)
        {
            Version = version;
            Notice = notice;
            Implemented = implemented;
            Changed = changed;
            Removed = removed;
        }

        public string Version { get; }
        public string Notice { get; }
        public IReadOnlyList<string> Implemented { get; }
        public IReadOnlyList<string> Changed { get; }
        public IReadOnlyList<string> Removed { get; }
    }
}
