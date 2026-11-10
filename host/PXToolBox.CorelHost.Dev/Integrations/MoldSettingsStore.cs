using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PXToolBox.Core.Molds;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Configurações do Cadastro de moldes desta máquina, num arquivo PRÓPRIO do PX DEV
    /// ("mold-settings.json" na pasta de configuração) — nunca o arquivo do MoldMotor (AGENTS.md).
    /// Sem arquivo, valem os valores de fábrica (<see cref="MoldSettings.Default"/>). Arquivo
    /// ilegível ou inválido nunca derruba uma ferramenta: vale o de fábrica e <see cref="Load"/>
    /// devolve o aviso.
    /// </summary>
    public static class MoldSettingsStore
    {
        private const int Schema = 1;
        private static string? _filePathOverride;

        private static string DefaultFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PXToolBox", "Dev", "config", "mold-settings.json");

        public static string FilePath => _filePathOverride ?? DefaultFilePath;

        /// <summary>Só para testes e harnesses — nunca o arquivo real do usuário.</summary>
        public static void SetFilePathForTesting(string? path) => _filePathOverride = path;

        /// <summary>Disparado depois de salvar ou voltar ao padrão.</summary>
        public static event Action? Changed;

        public static (MoldSettings Settings, string? Notice) Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return (MoldSettings.Default, null);
                var root = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(FilePath));
                if (root == null) return (MoldSettings.Default, "Configurações de moldes vazias — usando os valores de fábrica.");
                var settings = FromJson(root);
                var problems = settings.Validate();
                return problems.Count == 0
                    ? (settings, null)
                    : (MoldSettings.Default, $"Configurações de moldes inválidas ({problems[0]}) — usando os valores de fábrica.");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is InvalidCastException || ex is FormatException)
            {
                return (MoldSettings.Default, $"Não foi possível ler as configurações de moldes ({ex.Message}) — usando os valores de fábrica.");
            }
        }

        /// <summary>Só o que já passou em <see cref="MoldSettings.Validate"/>.</summary>
        public static void Save(MoldSettings settings)
        {
            var problems = settings.Validate();
            if (problems.Count > 0) throw new ArgumentException(problems[0], nameof(settings));
            AtomicFileWriter.WriteTextSafely(FilePath, ToJson(settings).ToString(Formatting.Indented));
            Changed?.Invoke();
        }

        /// <summary>Volta aos valores de fábrica (apaga só este arquivo).</summary>
        public static void ResetToDefault()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            Changed?.Invoke();
        }

        internal static JObject ToJson(MoldSettings settings) => new JObject
        {
            ["schema"] = Schema,
            ["male"] = Category(settings.Male),
            ["female"] = Category(settings.Female),
            ["kids"] = Category(settings.Kids),
            ["gridBaseSize"] = settings.GridBaseSize,
            ["laserNames"] = new JArray(settings.LaserNames),
            ["tagName"] = settings.TagName,
            ["identifierName"] = settings.IdentifierName,
            ["define"] = new JObject
            {
                ["createLaserCopy"] = settings.Define.CreateLaserCopy,
                ["laserColor"] = settings.Define.LaserColor,
                ["laserWidthMm"] = settings.Define.LaserWidthMillimeters,
                ["createTag"] = settings.Define.CreateTag,
                ["tagFontSizePt"] = settings.Define.TagFontSizePoints,
                ["tagOutlineWidthMm"] = settings.Define.TagOutlineWidthMillimeters,
                ["tagPosition"] = settings.Define.TagPosition.ToString(),
                ["tagLeftMarginCm"] = settings.Define.TagLeftMarginCm,
                ["tagRightMarginCm"] = settings.Define.TagRightMarginCm,
                ["tagTopMarginCm"] = settings.Define.TagTopMarginCm,
                ["tagBottomMarginCm"] = settings.Define.TagBottomMarginCm,
            },
            ["innerOutline"] = new JObject
            {
                ["enabled"] = settings.InnerOutline.Enabled,
                ["widthMm"] = settings.InnerOutline.WidthMillimeters,
                ["maleStyle"] = settings.InnerOutline.MaleStyle.ToString(),
                ["femaleStyle"] = settings.InnerOutline.FemaleStyle.ToString(),
                ["kidsStyle"] = settings.InnerOutline.KidsStyle.ToString(),
                ["sizeColors"] = new JObject(settings.InnerOutline.SizeColors.Select(c => new JProperty(c.Key, c.Value))),
            },
        };

        internal static MoldSettings FromJson(JObject root)
        {
            var schema = root.Value<int?>("schema") ?? 0;
            if (schema != Schema) throw new FormatException($"versão {schema} desconhecida");
            var fallback = MoldSettings.Default;
            return new MoldSettings(
                Category(root["male"] as JObject, fallback.Male),
                Category(root["female"] as JObject, fallback.Female),
                Category(root["kids"] as JObject, fallback.Kids),
                root.Value<string>("gridBaseSize") ?? fallback.GridBaseSize,
                Strings(root["laserNames"]) ?? fallback.LaserNames,
                root.Value<string>("tagName") ?? fallback.TagName,
                root.Value<string>("identifierName") ?? fallback.IdentifierName,
                Define(root["define"] as JObject, fallback.Define),
                InnerOutline(root["innerOutline"] as JObject, fallback.InnerOutline));
        }

        /// <summary>Sem o bloco (antes da dev.91) ou sem um campo: o valor de fábrica; estilo com nome
        /// desconhecido também.</summary>
        private static MoldInnerOutlineOptions InnerOutline(JObject? json, MoldInnerOutlineOptions fallback)
        {
            if (json == null) return fallback;
            MoldLineStyle Style(string key, MoldLineStyle defaultStyle) =>
                Enum.TryParse<MoldLineStyle>(json.Value<string>(key) ?? string.Empty, true, out var style) && Enum.IsDefined(typeof(MoldLineStyle), style) ? style : defaultStyle;
            var colors = json["sizeColors"] is JObject map
                ? map.Properties().Select(p => new KeyValuePair<string, string>(p.Name,
                    p.Value.Type == JTokenType.String ? (string)p.Value! : throw new FormatException("cor que não é texto"))).ToList()
                : fallback.SizeColors.ToList();
            return new MoldInnerOutlineOptions(
                json.Value<bool?>("enabled") ?? fallback.Enabled,
                json.Value<double?>("widthMm") ?? fallback.WidthMillimeters,
                Style("maleStyle", fallback.MaleStyle), Style("femaleStyle", fallback.FemaleStyle), Style("kidsStyle", fallback.KidsStyle),
                colors);
        }

        /// <summary>Sem o bloco (arquivo da dev.83) ou sem um campo (posição e margens: antes da dev.90):
        /// o valor de fábrica. Posição com nome desconhecido também (como o MoldMotor).</summary>
        private static MoldDefineOptions Define(JObject? json, MoldDefineOptions fallback) => json == null
            ? fallback
            : new MoldDefineOptions(
                json.Value<bool?>("createLaserCopy") ?? fallback.CreateLaserCopy,
                json.Value<string>("laserColor") ?? fallback.LaserColor,
                json.Value<double?>("laserWidthMm") ?? fallback.LaserWidthMillimeters,
                json.Value<bool?>("createTag") ?? fallback.CreateTag,
                json.Value<double?>("tagFontSizePt") ?? fallback.TagFontSizePoints,
                json.Value<double?>("tagOutlineWidthMm") ?? fallback.TagOutlineWidthMillimeters,
                Enum.TryParse<MoldTagPosition>(json.Value<string>("tagPosition") ?? string.Empty, true, out var position)
                    && Enum.IsDefined(typeof(MoldTagPosition), position) ? position : fallback.TagPosition,
                json.Value<double?>("tagLeftMarginCm") ?? fallback.TagLeftMarginCm,
                json.Value<double?>("tagRightMarginCm") ?? fallback.TagRightMarginCm,
                json.Value<double?>("tagTopMarginCm") ?? fallback.TagTopMarginCm,
                json.Value<double?>("tagBottomMarginCm") ?? fallback.TagBottomMarginCm);

        private static JObject Category(MoldSizeCategory category) => new JObject
        {
            ["code"] = category.Code,
            ["baseSizes"] = new JArray(category.BaseSizes),
            ["prefixes"] = new JArray(category.Prefixes),
            ["suffixes"] = new JArray(category.Suffixes),
        };

        private static MoldSizeCategory Category(JObject? json, MoldSizeCategory fallback) => json == null
            ? fallback
            : new MoldSizeCategory(
                json.Value<string>("code") ?? fallback.Code,
                Strings(json["baseSizes"]) ?? fallback.BaseSizes,
                Strings(json["prefixes"]) ?? fallback.Prefixes,
                Strings(json["suffixes"]) ?? fallback.Suffixes);

        private static IReadOnlyList<string>? Strings(JToken? token) =>
            token is JArray array ? array.Select(t => t.Type == JTokenType.String ? (string)t! : throw new FormatException("lista com item que não é texto")).ToList() : null;
    }
}
