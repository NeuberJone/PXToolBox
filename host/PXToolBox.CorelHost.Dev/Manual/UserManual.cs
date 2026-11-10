using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PXToolBoxDev.Favorites;
using PXToolBoxDev.PanelLayout;

namespace PXToolBoxDev.Manual
{
    /// <summary>
    /// Manual de uso (Diagnóstico → Manual de uso). O texto fica em <c>Manual/user-manual.json</c>
    /// (recurso embutido); nome e seção de cada ferramenta vêm de <see cref="PanelToolCatalog"/>,
    /// então renomear um botão no catálogo já aparece aqui. Toda ferramenta do catálogo precisa
    /// de uma entrada no manual — conferido por UserManualTests: ferramenta nova, removida ou
    /// alterada → atualize o JSON junto (regra do AGENTS.md).
    /// </summary>
    public static class UserManual
    {
        private const string ResourceName = "PXToolBoxDev.Manual.user-manual.json";
        private static readonly Lazy<ManualFile> Content = new Lazy<ManualFile>(Load);

        public static IReadOnlyList<ManualTopic> Topics =>
            Content.Value.Topics.Select(t => new ManualTopic(t.Title, t.Paragraphs)).ToList();

        /// <summary>Seções do painel na ordem do catálogo, só as que têm ferramentas.</summary>
        public static IReadOnlyList<ManualSection> Sections =>
            PanelSectionCatalog.All
                .Select(s => new ManualSection(s.Title, Intro(s.Title), PanelToolCatalog.All.Where(t => t.Section == s.Title).Select(ToolEntry).ToList()))
                .Where(s => s.Tools.Count > 0)
                .ToList();

        /// <summary>Ids com texto no manual (para o teste conferir contra o catálogo).</summary>
        public static IReadOnlyCollection<string> DocumentedToolIds => Content.Value.Tools.Select(t => t.Id).ToList();

        public static IReadOnlyCollection<string> DocumentedSections => Content.Value.Sections.Keys.ToList();

        public static ManualTool? Find(string id) =>
            PanelToolCatalog.Find(id) is { } tool ? ToolEntry(tool) : null;

        /// <summary>Ferramentas cujo nome, seção ou texto contêm todas as palavras da busca
        /// (sem diferenciar maiúsculas nem acentos), na ordem do manual.</summary>
        public static IReadOnlyList<ManualTool> Search(string query) =>
            Words(query).Length == 0 ? Array.Empty<ManualTool>() : Sections.SelectMany(s => s.Tools).Where(t => Matches(t, query)).ToList();

        /// <summary>Assuntos gerais com todas as palavras no título ou no texto.</summary>
        public static IReadOnlyList<ManualTopic> SearchTopics(string query) =>
            Words(query).Length == 0 ? Array.Empty<ManualTopic>() : Topics.Where(t => Matches(t, query)).ToList();

        /// <summary>Busca vazia casa com tudo.</summary>
        public static bool Matches(ManualTool tool, string query) =>
            ContainsAll(new[] { tool.Label, tool.Section, tool.Purpose }.Concat(tool.Steps).Concat(tool.Notes), query);

        public static bool Matches(ManualTopic topic, string query) =>
            ContainsAll(new[] { topic.Title }.Concat(topic.Paragraphs), query);

        private static bool ContainsAll(IEnumerable<string> texts, string query)
        {
            var haystack = Normalize(string.Join(" ", texts));
            return Words(query).All(haystack.Contains);
        }

        private static string[] Words(string? query) =>
            Normalize(query).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>Minúsculas e sem acentos, para a busca.</summary>
        public static string Normalize(string? text)
        {
            var decomposed = (text ?? string.Empty).ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string Intro(string section) =>
            Content.Value.Sections.TryGetValue(section, out var intro) ? intro : string.Empty;

        private static ManualTool ToolEntry(PanelToolCatalog.Tool tool)
        {
            var text = Content.Value.Tools.FirstOrDefault(t => t.Id == tool.Id);
            return new ManualTool(tool.Id, tool.Label, tool.Section, text?.Purpose ?? string.Empty,
                text?.Steps ?? Array.Empty<string>(), text?.Notes ?? Array.Empty<string>());
        }

        private static ManualFile Load()
        {
            using (var stream = typeof(UserManual).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException("Manual de uso não encontrado no assembly DEV."))
            using (var reader = new StreamReader(stream))
            {
                return JsonConvert.DeserializeObject<ManualFile>(reader.ReadToEnd())
                    ?? throw new InvalidOperationException("Manual de uso vazio ou inválido.");
            }
        }

        private sealed class ManualFile
        {
            public TopicText[] Topics { get; set; } = Array.Empty<TopicText>();
            public Dictionary<string, string> Sections { get; set; } = new Dictionary<string, string>();
            public ToolText[] Tools { get; set; } = Array.Empty<ToolText>();
        }

        private sealed class TopicText
        {
            public string Title { get; set; } = "";
            public string[] Paragraphs { get; set; } = Array.Empty<string>();
        }

        private sealed class ToolText
        {
            public string Id { get; set; } = "";
            public string Purpose { get; set; } = "";
            public string[] Steps { get; set; } = Array.Empty<string>();
            public string[] Notes { get; set; } = Array.Empty<string>();
        }
    }

    public sealed class ManualTopic
    {
        public ManualTopic(string title, IReadOnlyList<string> paragraphs)
        {
            Title = title;
            Paragraphs = paragraphs;
        }

        public string Title { get; }
        public IReadOnlyList<string> Paragraphs { get; }
    }

    public sealed class ManualSection
    {
        public ManualSection(string title, string intro, IReadOnlyList<ManualTool> tools)
        {
            Title = title;
            Intro = intro;
            Tools = tools;
        }

        public string Title { get; }
        public string Intro { get; }
        public IReadOnlyList<ManualTool> Tools { get; }
    }

    public sealed class ManualTool
    {
        public ManualTool(string id, string label, string section, string purpose, IReadOnlyList<string> steps, IReadOnlyList<string> notes)
        {
            Id = id;
            Label = label;
            Section = section;
            Purpose = purpose;
            Steps = steps;
            Notes = notes;
        }

        public string Id { get; }
        public string Label { get; }
        public string Section { get; }
        public string Purpose { get; }
        public IReadOnlyList<string> Steps { get; }
        public IReadOnlyList<string> Notes { get; }
    }
}
