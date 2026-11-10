using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Contextos de produção recentes (Produção → "Recentes"): só os CAMINHOS dos últimos
    /// <see cref="MaxEntries"/> arquivos .json carregados com sucesso, e quando — nunca o conteúdo
    /// do contexto (rolo, pedido…). Escolher um relê e revalida o arquivo como um carregamento
    /// novo (<see cref="OrderContextPathLoader"/>): o contexto em si continua só em memória
    /// (decisão do usuário, 2026-11). Nada é carregado sozinho ao abrir o painel.
    ///
    /// Dado local DA MÁQUINA (caminhos de rede/disco), num arquivo próprio
    /// ("order-context-history.json"), nunca nas preferências portáteis — mesmo padrão de
    /// <see cref="OutputFolderConfigStore"/>. Nunca lança ao ler: arquivo ausente, corrompido ou
    /// de outra versão vira lista vazia (o arquivo ruim é substituído na próxima gravação).
    /// </summary>
    public static class OrderContextHistoryStore
    {
        public const string FormatId = "PxToolBoxDevOrderContextHistory";
        public const int CurrentSchemaVersion = 1;
        public const int MaxEntries = 10;

        public sealed class Entry
        {
            public Entry(string path, DateTimeOffset loadedAtUtc)
            {
                Path = path;
                LoadedAtUtc = loadedAtUtc;
            }

            public string Path { get; }
            public DateTimeOffset LoadedAtUtc { get; }
        }

        private static string? _filePathOverride;

        private static string DefaultFilePath => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PXToolBox", "Dev", "config", "order-context-history.json");

        private static string FilePath => _filePathOverride ?? DefaultFilePath;

        /// <summary>Só para testes — aponta esta classe para um arquivo temporário isolado,
        /// nunca o real do usuário.</summary>
        public static void SetFilePathForTesting(string? path) => _filePathOverride = path;

        public static IReadOnlyList<Entry> Load()
        {
            try
            {
                return File.Exists(FilePath) ? Parse(File.ReadAllText(FilePath)) : Array.Empty<Entry>();
            }
            catch (IOException)
            {
                return Array.Empty<Entry>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<Entry>();
            }
        }

        /// <summary>Registra um carregamento bem-sucedido e devolve a lista gravada.</summary>
        public static IReadOnlyList<Entry> Record(string path, DateTimeOffset loadedAtUtc) =>
            Save(WithRecorded(Load(), path, loadedAtUtc));

        public static IReadOnlyList<Entry> Remove(string path) => Save(Without(Load(), path));

        public static IReadOnlyList<Entry> Clear() => Save(Array.Empty<Entry>());

        // ---- Regras puras (testadas sem disco) ----

        /// <summary>O mais recente primeiro; o mesmo caminho (sem diferenciar maiúsculas, como o
        /// Windows) não se repete — sobe para o topo com a data nova; no máximo
        /// <see cref="MaxEntries"/>.</summary>
        public static IReadOnlyList<Entry> WithRecorded(IEnumerable<Entry> entries, string path, DateTimeOffset loadedAtUtc)
        {
            var normalized = path.Trim();
            return new[] { new Entry(normalized, loadedAtUtc) }
                .Concat(Without(entries, normalized))
                .Take(MaxEntries)
                .ToList();
        }

        public static IReadOnlyList<Entry> Without(IEnumerable<Entry> entries, string path) =>
            entries.Where(e => !string.Equals(e.Path, path.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        public static IReadOnlyList<Entry> Parse(string json)
        {
            JObject root;
            try
            {
                root = JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None })
                    ?? new JObject();
            }
            catch (JsonException)
            {
                return Array.Empty<Entry>();
            }

            if ((string?)root["formatId"] != FormatId || root["schemaVersion"]?.Type != JTokenType.Integer
                || (int)root["schemaVersion"]! != CurrentSchemaVersion || root["entries"] is not JArray items)
            {
                return Array.Empty<Entry>();
            }

            var result = new List<Entry>();
            foreach (var item in items.OfType<JObject>())
            {
                var path = item["path"]?.Type == JTokenType.String ? ((string)item["path"]!).Trim() : "";
                if (path.Length == 0 || result.Any(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var loadedAt = item["loadedAtUtc"]?.Type == JTokenType.String
                    && DateTimeOffset.TryParse((string)item["loadedAtUtc"]!, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
                    ? parsed
                    : DateTimeOffset.MinValue;
                result.Add(new Entry(path, loadedAt));
            }

            return result.Take(MaxEntries).ToList();
        }

        public static string Serialize(IEnumerable<Entry> entries)
        {
            var root = new JObject
            {
                ["formatId"] = FormatId,
                ["schemaVersion"] = CurrentSchemaVersion,
                ["entries"] = new JArray(entries.Select(e => new JObject
                {
                    ["path"] = e.Path,
                    ["loadedAtUtc"] = e.LoadedAtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                })),
            };
            return root.ToString(Formatting.Indented);
        }

        private static IReadOnlyList<Entry> Save(IReadOnlyList<Entry> entries)
        {
            AtomicFileWriter.WriteTextSafely(FilePath, Serialize(entries));
            return entries;
        }
    }
}
