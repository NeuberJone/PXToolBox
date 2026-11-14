using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBoxDev.Manual;

namespace PXToolBoxDev.PanelLayout
{
    /// <summary>
    /// Busca de ferramentas do topo do painel: uma ferramenta entra se o nome, a seção onde ela está
    /// agora (com o nome personalizado) ou a dica do botão têm TODAS as palavras digitadas, sem
    /// diferenciar maiúsculas nem acentos (mesma normalização do Manual de uso). A ordem de entrada
    /// é mantida — quem chama já ordena por seção e catálogo.
    /// </summary>
    public static class PanelToolFilter
    {
        public sealed class Candidate
        {
            public Candidate(string id, string label, string sectionId, string sectionTitle, string help)
            {
                Id = id;
                Label = label;
                SectionId = sectionId;
                SectionTitle = sectionTitle;
                Help = help;
            }

            public string Id { get; }
            public string Label { get; }
            public string SectionId { get; }
            public string SectionTitle { get; }

            /// <summary>Texto da dica do botão original.</summary>
            public string Help { get; }
        }

        /// <summary>Busca vazia: nada (o painel mostra as seções normais).</summary>
        public static IReadOnlyList<Candidate> Filter(IEnumerable<Candidate> candidates, string? query)
        {
            var words = Words(query);
            if (words.Length == 0) return Array.Empty<Candidate>();
            return candidates.Where(c =>
            {
                var text = UserManual.Normalize(string.Join(" ", c.Label, c.SectionTitle, c.Help));
                return words.All(text.Contains);
            }).ToList();
        }

        private static string[] Words(string? query) =>
            UserManual.Normalize(query).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
