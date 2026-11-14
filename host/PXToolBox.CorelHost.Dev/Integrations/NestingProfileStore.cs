using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PXToolBoxDev.Nesting;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Perfis do "Encaixar peças" desta máquina ("nesting-profiles.json" na pasta de configuração do PX DEV).
    /// Sem arquivo, nenhum perfil. Arquivo ilegível ou inválido nunca derruba a janela: <see cref="Load"/>
    /// devolve lista vazia, o aviso e <c>Readable = false</c> — e a janela não grava por cima dele.
    /// Campo ausente ou com valor desconhecido vale o inicial da janela.
    /// </summary>
    public static class NestingProfileStore
    {
        private const int Schema = 1;
        private static string? _filePathOverride;

        private static string DefaultFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PXToolBox", "Dev", "config", "nesting-profiles.json");

        public static string FilePath => _filePathOverride ?? DefaultFilePath;

        /// <summary>Só para testes e harnesses — nunca o arquivo real do usuário.</summary>
        public static void SetFilePathForTesting(string? path) => _filePathOverride = path;

        public static (IReadOnlyList<NestingProfile> Profiles, bool Readable, string? Notice) Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return (Array.Empty<NestingProfile>(), true, null);
                var root = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(FilePath))
                    ?? throw new FormatException("arquivo vazio");
                var schema = root.Value<int?>("schema") ?? 0;
                if (schema != Schema) throw new FormatException($"versão {schema} desconhecida");
                var profiles = (root["profiles"] as JArray ?? throw new FormatException("sem a lista de perfis"))
                    .Select(token => FromJson(token as JObject ?? throw new FormatException("perfil que não é objeto")))
                    .ToList();
                var problems = NestingProfile.Validate(profiles);
                if (problems.Count > 0) throw new FormatException(problems[0]);
                return (profiles, true, null);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is InvalidCastException || ex is FormatException)
            {
                return (Array.Empty<NestingProfile>(), false,
                    $"Não foi possível ler os perfis ({ex.Message}). Nada será gravado por cima de {FilePath}.");
            }
        }

        /// <summary>As últimas opções da janela ("nesting-last.json", ao lado dos perfis): a janela abre com elas.</summary>
        public static string LastFilePath => Path.Combine(Path.GetDirectoryName(FilePath)!, "nesting-last.json");

        /// <summary>Null sem arquivo ou com arquivo ilegível (a janela abre com os valores iniciais; nada é avisado —
        /// é só uma conveniência).</summary>
        public static NestingProfile? LoadLast()
        {
            try
            {
                if (!File.Exists(LastFilePath)) return null;
                var root = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(LastFilePath));
                return root?.Value<int?>("schema") == Schema && root["last"] is JObject last ? FromJson(last) : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is InvalidCastException || ex is FormatException)
            {
                return null;
            }
        }

        public static void SaveLast(NestingProfile profile)
        {
            var root = new JObject { ["schema"] = Schema, ["last"] = ToJson(profile) };
            AtomicFileWriter.WriteTextSafely(LastFilePath, root.ToString(Formatting.Indented));
        }

        /// <summary>Só o que já passou em <see cref="NestingProfile.Validate"/>.</summary>
        public static void Save(IReadOnlyList<NestingProfile> profiles)
        {
            var problems = NestingProfile.Validate(profiles);
            if (problems.Count > 0) throw new ArgumentException(problems[0], nameof(profiles));
            var root = new JObject
            {
                ["schema"] = Schema,
                ["profiles"] = new JArray(profiles.Select(ToJson)),
            };
            AtomicFileWriter.WriteTextSafely(FilePath, root.ToString(Formatting.Indented));
        }

        internal static JObject ToJson(NestingProfile profile) => new JObject
        {
            ["name"] = profile.Name.Trim(),
            ["area"] = profile.Area.ToString(),
            ["rollWidthCm"] = profile.RollWidth,
            ["rollSheetHeightCm"] = profile.RollHeight,
            ["areas"] = profile.AreasText,
            ["areaOrder"] = profile.AreaOrder.ToString(),
            ["drawAreas"] = profile.DrawAreas,
            ["groupBlocks"] = profile.GroupBlocks,
            ["blockGapCm"] = profile.BlockGap,
            ["legends"] = profile.Legends,
            ["splitByLayer"] = profile.SplitByLayer,
            ["resizePage"] = profile.ResizePage,
            ["marginCm"] = profile.Margin,
            ["spacingCm"] = profile.Spacing,
            ["origin"] = profile.Origin.ToString(),
            ["method"] = profile.Method.ToString(),
            ["contourCellMm"] = profile.ContourCell,
            ["rotation"] = profile.Rotation.ToString(),
            ["searchBest"] = profile.SearchBest,
            ["timeLimitSeconds"] = profile.TimeLimit,
        };

        internal static NestingProfile FromJson(JObject json)
        {
            var fallback = new NestingProfile();
            return new NestingProfile
            {
                Name = (json.Value<string>("name") ?? "").Trim(),
                Area = Choice(json, "area", fallback.Area),
                RollWidth = json.Value<string>("rollWidthCm") ?? fallback.RollWidth,
                RollHeight = json.Value<string>("rollSheetHeightCm") ?? fallback.RollHeight,
                AreasText = json.Value<string>("areas") ?? fallback.AreasText,
                AreaOrder = Choice(json, "areaOrder", fallback.AreaOrder),
                DrawAreas = json.Value<bool?>("drawAreas") ?? fallback.DrawAreas,
                GroupBlocks = json.Value<bool?>("groupBlocks") ?? fallback.GroupBlocks,
                BlockGap = json.Value<string>("blockGapCm") ?? fallback.BlockGap,
                Legends = json.Value<bool?>("legends") ?? fallback.Legends,
                SplitByLayer = json.Value<bool?>("splitByLayer") ?? fallback.SplitByLayer,
                ResizePage = json.Value<bool?>("resizePage") ?? fallback.ResizePage,
                Margin = json.Value<string>("marginCm") ?? fallback.Margin,
                Spacing = json.Value<string>("spacingCm") ?? fallback.Spacing,
                Origin = Choice(json, "origin", fallback.Origin),
                Method = Choice(json, "method", fallback.Method),
                ContourCell = json.Value<string>("contourCellMm") ?? fallback.ContourCell,
                Rotation = Choice(json, "rotation", fallback.Rotation),
                SearchBest = json.Value<bool?>("searchBest") ?? fallback.SearchBest,
                TimeLimit = json.Value<string>("timeLimitSeconds") ?? fallback.TimeLimit,
            };
        }

        /// <summary>Nome desconhecido (ou número) vale o inicial.</summary>
        private static T Choice<T>(JObject json, string key, T fallback) where T : struct, Enum =>
            json[key]?.Type == JTokenType.String && Enum.TryParse<T>((string)json[key]!, true, out var value)
                && Enum.IsDefined(typeof(T), value) && !char.IsDigit(((string)json[key]!).Trim().FirstOrDefault())
                ? value
                : fallback;
    }
}
