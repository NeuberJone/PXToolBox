using System;
using System.Collections.Generic;
using System.Linq;

namespace PXToolBox.Core.Nesting
{
    /// <summary>
    /// Quantidade por peça no encaixe: cada peça vira <c>quantidade</c> itens (0 = fica de fora), com o
    /// nome "Frente (2/3)" quando há mais de um, e <c>Sources[k]</c> diz de qual peça original veio o
    /// item k (para o documento: a 1ª cópia encaixada usa o objeto original, as outras são duplicatas).
    /// </summary>
    public static class NestQuantities
    {
        public const int MaximumPerPiece = 999;

        public static (IReadOnlyList<NestItem> Items, IReadOnlyList<int> Sources) Expand(
            IReadOnlyList<NestItem> items, IReadOnlyList<int> quantities)
        {
            var sources = Sources(items.Count, quantities);
            var expanded = sources.Select((source, k) =>
                new NestItem(k, Name(items[source].Name, sources, k), items[source].Width, items[source].Height)).ToList();
            return (expanded, sources);
        }

        public static (IReadOnlyList<ContourPiece> Pieces, IReadOnlyList<int> Sources) Expand(
            IReadOnlyList<ContourPiece> pieces, IReadOnlyList<int> quantities)
        {
            var sources = Sources(pieces.Count, quantities);
            var expanded = sources.Select((source, k) =>
                new ContourPiece(k, Name(pieces[source].Name, sources, k), pieces[source].Shapes)).ToList();
            return (expanded, sources);
        }

        private static List<int> Sources(int count, IReadOnlyList<int> quantities)
        {
            if (quantities.Count != count) throw new ArgumentException("Informe uma quantidade para cada peça.");
            if (quantities.Any(q => q < 0 || q > MaximumPerPiece))
                throw new ArgumentException($"A quantidade de cada peça vai de 0 a {MaximumPerPiece}.");
            var sources = new List<int>();
            for (var source = 0; source < count; source++)
                sources.AddRange(Enumerable.Repeat(source, quantities[source]));
            if (sources.Count == 0) throw new ArgumentException("Todas as quantidades estão em 0: nada para encaixar.");
            return sources;
        }

        private static string Name(string name, IReadOnlyList<int> sources, int k)
        {
            var total = sources.Count(s => s == sources[k]);
            if (total == 1) return name;
            var copy = sources.Take(k + 1).Count(s => s == sources[k]);
            return $"{name} ({copy}/{total})";
        }
    }
}
