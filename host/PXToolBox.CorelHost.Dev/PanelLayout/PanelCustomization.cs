using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PXToolBoxDev.Favorites;

namespace PXToolBoxDev.PanelLayout
{
    public sealed class CustomSectionEntry
    {
        public CustomSectionEntry()
        {
        }

        public CustomSectionEntry(string id, string title)
        {
            Id = id;
            Title = title;
        }

        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
    }

    public sealed class ToolPlacementEntry
    {
        public ToolPlacementEntry()
        {
        }

        public ToolPlacementEntry(string toolId, string sectionId)
        {
            ToolId = toolId;
            SectionId = sectionId;
        }

        public string ToolId { get; set; } = "";
        public string SectionId { get; set; } = "";
    }

    /// <summary>
    /// Personalização do painel além de ordem/visibilidade (<see cref="PanelSectionLayout"/>):
    /// nomes das seções, seções criadas pelo usuário e em que seção cada ferramenta aparece
    /// (pedido do usuário, 2026-11-06 — "Mover o botão de seção").
    ///
    /// Uma ferramenta movida continua existindo na seção de origem (o botão original fica oculto
    /// lá, com os campos de que depende — Nome do layout, Pasta, nome dos objetos…); na seção de
    /// destino aparece um atalho completo ligado ao original (mesmo mecanismo dos Favoritos).
    /// Por isso mover nunca quebra uma ferramenta. Gravado por Id estável de ferramenta e de
    /// seção; nunca lança com o que vier da configuração (entradas inválidas são ignoradas,
    /// ferramentas desconhecidas preservadas).
    /// </summary>
    public sealed class PanelCustomization
    {
        public const string CustomSectionPrefix = "custom.";

        public Dictionary<string, string> SectionTitles { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<CustomSectionEntry> CustomSections { get; set; } = new List<CustomSectionEntry>();
        public List<ToolPlacementEntry> ToolPlacements { get; set; } = new List<ToolPlacementEntry>();

        public static PanelCustomization Default() => new PanelCustomization();

        [Newtonsoft.Json.JsonIgnore]
        public bool IsDefault => SectionTitles.Count == 0 && CustomSections.Count == 0 && ToolPlacements.Count == 0;

        public PanelCustomization Clone()
        {
            var copy = new PanelCustomization
            {
                SectionTitles = new Dictionary<string, string>(SectionTitles, StringComparer.Ordinal),
                CustomSections = CustomSections.Select(s => new CustomSectionEntry(s.Id, s.Title)).ToList(),
                ToolPlacements = ToolPlacements.Select(p => new ToolPlacementEntry(p.ToolId, p.SectionId)).ToList(),
            };
            return copy;
        }

        /// <summary>Limpa o que veio da configuração: ids vazios/repetidos, títulos vazios,
        /// títulos para seções que não existem, posições para seções que não existem. Ferramentas
        /// desconhecidas (de uma versão mais nova) ficam.</summary>
        public PanelCustomization Normalize()
        {
            CustomSections = (CustomSections ?? new List<CustomSectionEntry>())
                .Where(s => s != null && (s.Id ?? "").StartsWith(CustomSectionPrefix, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(s.Title))
                .GroupBy(s => s.Id, StringComparer.Ordinal).Select(g => new CustomSectionEntry(g.Key, g.First().Title.Trim()))
                .ToList();
            SectionTitles = (SectionTitles ?? new Dictionary<string, string>())
                .Where(p => PanelSectionCatalog.Find(p.Key) != null && !string.IsNullOrWhiteSpace(p.Value))
                .ToDictionary(p => p.Key, p => p.Value.Trim(), StringComparer.Ordinal);
            ToolPlacements = (ToolPlacements ?? new List<ToolPlacementEntry>())
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.ToolId) && SectionExists(p.SectionId))
                .Where(p => PanelToolCatalog.Find(p.ToolId) == null || p.SectionId != HomeSectionOf(p.ToolId))
                .GroupBy(p => p.ToolId, StringComparer.Ordinal).Select(g => new ToolPlacementEntry(g.Key, g.First().SectionId))
                .ToList();
            return this;
        }

        // ---- Seções ----

        public bool IsCustomSection(string? sectionId) => CustomSections.Any(s => s.Id == sectionId);

        public bool SectionExists(string? sectionId) =>
            sectionId != null && (PanelSectionCatalog.Find(sectionId) != null || IsCustomSection(sectionId));

        /// <summary>Nome mostrado: o personalizado; senão o da seção criada; senão o original.</summary>
        public string TitleOf(string sectionId)
        {
            if (SectionTitles.TryGetValue(sectionId, out var renamed)) return renamed;
            var custom = CustomSections.FirstOrDefault(s => s.Id == sectionId);
            if (custom != null) return custom.Title;
            return PanelSectionCatalog.Find(sectionId)?.Title ?? sectionId;
        }

        /// <summary>Renomeia. Seção original com nome vazio (ou igual ao original) volta ao
        /// original; seção criada não aceita nome vazio.</summary>
        public bool Rename(string sectionId, string? title)
        {
            var trimmed = (title ?? "").Trim();
            var custom = CustomSections.FirstOrDefault(s => s.Id == sectionId);
            if (custom != null)
            {
                if (trimmed.Length == 0 || trimmed == custom.Title) return false;
                custom.Title = trimmed;
                return true;
            }

            var original = PanelSectionCatalog.Find(sectionId)?.Title;
            if (original == null) return false;
            if (trimmed.Length == 0 || trimmed == original) return SectionTitles.Remove(sectionId);
            if (SectionTitles.TryGetValue(sectionId, out var current) && current == trimmed) return false;
            SectionTitles[sectionId] = trimmed;
            return true;
        }

        public string AddSection(string title, Func<string>? newId = null)
        {
            var id = CustomSectionPrefix + (newId?.Invoke() ?? Guid.NewGuid().ToString("N").Substring(0, 12));
            var trimmed = (title ?? "").Trim();
            CustomSections.Add(new CustomSectionEntry(id, trimmed.Length == 0 ? "Nova seção" : trimmed));
            return id;
        }

        /// <summary>Remove uma seção criada; as ferramentas dela voltam para a seção de origem.</summary>
        public bool RemoveSection(string sectionId)
        {
            if (CustomSections.RemoveAll(s => s.Id == sectionId) == 0) return false;
            ToolPlacements.RemoveAll(p => p.SectionId == sectionId);
            return true;
        }

        // ---- Ferramentas ----

        /// <summary>Seção original da ferramenta (onde o botão está no painel).</summary>
        public static string HomeSectionOf(string toolId)
        {
            var tool = PanelToolCatalog.Find(toolId);
            return tool == null ? "" : PanelSectionCatalog.All.FirstOrDefault(s => s.Title == tool.Section)?.Id ?? "";
        }

        public string SectionOf(string toolId)
        {
            var placement = ToolPlacements.FirstOrDefault(p => p.ToolId == toolId);
            return placement != null && SectionExists(placement.SectionId) ? placement.SectionId : HomeSectionOf(toolId);
        }

        public bool IsMoved(string toolId) => SectionOf(toolId) != HomeSectionOf(toolId);

        /// <summary>Leva a ferramenta para a seção (no fim das que foram movidas para lá). Para a
        /// seção de origem = desfaz a mudança.</summary>
        public bool Place(string toolId, string sectionId)
        {
            if (PanelToolCatalog.Find(toolId) == null || !SectionExists(sectionId) || SectionOf(toolId) == sectionId) return false;
            ToolPlacements.RemoveAll(p => p.ToolId == toolId);
            if (sectionId != HomeSectionOf(toolId))
            {
                ToolPlacements.Add(new ToolPlacementEntry(toolId, sectionId));
            }

            return true;
        }

        /// <summary>Ferramentas movidas PARA a seção, na ordem escolhida (só conhecidas).</summary>
        public IReadOnlyList<string> MovedInto(string sectionId) =>
            ToolPlacements.Where(p => p.SectionId == sectionId && PanelToolCatalog.Find(p.ToolId) != null)
                .Select(p => p.ToolId).ToList();

        public bool MoveUp(string toolId) => Move(toolId, -1);

        public bool MoveDown(string toolId) => Move(toolId, +1);

        // Troca com a vizinha movida para a MESMA seção.
        private bool Move(string toolId, int direction)
        {
            var section = ToolPlacements.FirstOrDefault(p => p.ToolId == toolId)?.SectionId;
            if (section == null) return false;
            var siblings = MovedInto(section).ToList();
            var index = siblings.IndexOf(toolId);
            var neighbor = index + direction;
            if (index < 0 || neighbor < 0 || neighbor >= siblings.Count) return false;

            var a = ToolPlacements.FindIndex(p => p.ToolId == toolId);
            var b = ToolPlacements.FindIndex(p => p.ToolId == siblings[neighbor]);
            (ToolPlacements[a], ToolPlacements[b]) = (ToolPlacements[b], ToolPlacements[a]);
            return true;
        }

        // ---- Marcas do canto ----

        /// <summary>Marca de cada seção pelo nome ATUAL (regra do usuário): inicial; mesma inicial
        /// → primeira e segunda letras; ainda igual → iniciais das palavras (sem "e", "de"…);
        /// ainda igual → número no fim.</summary>
        public IReadOnlyDictionary<string, string> SectionBadges(IEnumerable<string> sectionIds) =>
            ComputeBadges(sectionIds.Select(id => (id, TitleOf(id))).ToList());

        private static readonly HashSet<string> Connectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "e", "de", "da", "do", "das", "dos", "a", "o", "as", "os", "com", "para", "-", "—",
        };

        public static IReadOnlyDictionary<string, string> ComputeBadges(IReadOnlyList<(string Id, string Title)> sections)
        {
            // Sem acento: "Códigos" vira "Co", não "Có".
            string Letters(string title) => new string(title.Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)).ToArray());
            string First(string title)
            {
                var letters = Letters(title);
                return letters.Length == 0 ? "?" : letters.Substring(0, 1).ToUpper(CultureInfo.CurrentCulture);
            }

            string Two(string title)
            {
                var letters = Letters(title);
                return letters.Length < 2 ? First(title) : First(title) + letters.Substring(1, 1).ToLower(CultureInfo.CurrentCulture);
            }

            string Initials(string title)
            {
                var words = title.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => !Connectors.Contains(w) && Letters(w).Length > 0).ToList();
                return words.Count < 2 ? Two(title) : First(words[0]) + First(words[1]);
            }

            // Cada passo só mexe em quem AINDA colide depois do anterior.
            var badges = sections.ToDictionary(s => s.Id, s => First(s.Title), StringComparer.Ordinal);
            foreach (var step in new Func<string, string>[] { Two, Initials })
            {
                foreach (var group in badges.GroupBy(p => p.Value).Where(g => g.Count() > 1).ToList())
                {
                    foreach (var pair in group.ToList())
                    {
                        badges[pair.Key] = step(sections.First(s => s.Id == pair.Key).Title);
                    }
                }
            }

            // Ainda igual (ex.: "Molde" × "Montagem"): inicial + a próxima letra que diferencia
            // o grupo inteiro ("Ml", "Mn"); só sem nenhuma, número no fim.
            foreach (var group in badges.GroupBy(p => p.Value).Where(g => g.Count() > 1).ToList())
            {
                var ids = group.Select(p => p.Key).ToList();
                var titles = ids.Select(id => Letters(sections.First(s => s.Id == id).Title)).ToList();
                for (var k = 2; k < titles.Max(t => t.Length); k++)
                {
                    var candidates = titles.Select(t => t.Length > k ? First(t) + t.Substring(k, 1).ToLower(CultureInfo.CurrentCulture) : null).ToList();
                    var others = badges.Where(p => !ids.Contains(p.Key)).Select(p => p.Value).ToList();
                    if (candidates.All(c => c != null) && candidates.Distinct().Count() == candidates.Count && !candidates.Any(others.Contains))
                    {
                        for (var i = 0; i < ids.Count; i++) badges[ids[i]] = candidates[i]!;
                        break;
                    }
                }
            }

            foreach (var group in badges.GroupBy(p => p.Value).Where(g => g.Count() > 1).ToList())
            {
                var n = 1;
                foreach (var pair in group.Skip(1).ToList())
                {
                    badges[pair.Key] = pair.Value + (++n).ToString(CultureInfo.InvariantCulture);
                }
            }

            return badges;
        }
    }
}
