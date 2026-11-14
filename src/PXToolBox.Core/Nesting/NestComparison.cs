using System;
using System.Collections.Generic;
using System.Linq;

namespace PXToolBox.Core.Nesting
{
    /// <summary>
    /// "Comparar com o original" (N2 de docs/reference-feature-roadmap.md): a caixa que as peças ocupam hoje
    /// e a que ocupam no encaixe, em mm. Aproveitamento dos dois lados = soma das caixas das peças ÷ área da
    /// caixa ocupada, a mesma conta nos dois, para comparar arranjos (não inclui margem nem área livre).
    /// </summary>
    public sealed class NestComparison
    {
        private NestComparison(double originalWidth, double originalHeight, double nestedWidth, double nestedHeight, double piecesArea)
        {
            OriginalWidth = originalWidth;
            OriginalHeight = originalHeight;
            NestedWidth = nestedWidth;
            NestedHeight = nestedHeight;
            PiecesArea = piecesArea;
        }

        public double OriginalWidth { get; }
        public double OriginalHeight { get; }
        public double NestedWidth { get; }
        public double NestedHeight { get; }

        /// <summary>Soma das caixas das peças (as da seleção, sem girar).</summary>
        public double PiecesArea { get; }

        public double OriginalUtilization => PiecesArea / (OriginalWidth * OriginalHeight);
        public double NestedUtilization => PiecesArea / (NestedWidth * NestedHeight);

        /// <summary>Área da caixa do encaixe ÷ a do original − 1 (negativo = encaixe menor).</summary>
        public double AreaChange => NestedWidth * NestedHeight / (OriginalWidth * OriginalHeight) - 1;

        /// <param name="items">As peças da seleção (uma por objeto).</param>
        /// <param name="origins">Canto superior esquerdo atual de cada peça, na mesma ordem.</param>
        /// <param name="placed">Caixas no encaixe (X, Y, largura, altura), uma por peça da seleção.</param>
        /// <returns>Null (com o motivo) quando a comparação não faz sentido: quantidade diferente de 1 ou
        /// peça que não coube — as duas caixas teriam peças diferentes.</returns>
        public static NestComparison? Of(IReadOnlyList<NestItem> items, IReadOnlyList<NestPoint> origins,
            IReadOnlyList<(double X, double Y, double Width, double Height)> placed, out string reason)
        {
            reason = "";
            if (origins.Count != items.Count) throw new ArgumentException("Uma posição por peça.");
            if (placed.Count != items.Count)
            {
                reason = "só quando cada peça entra uma vez e todas cabem.";
                return null;
            }

            var originals = items.Select((item, k) => (origins[k].X, origins[k].Y, item.Width, item.Height)).ToList();
            var (ow, oh) = Box(originals);
            var (nw, nh) = Box(placed);
            if (ow <= 0 || oh <= 0 || nw <= 0 || nh <= 0)
            {
                reason = "peças sem medida.";
                return null;
            }

            return new NestComparison(ow, oh, nw, nh, items.Sum(i => i.Width * i.Height));
        }

        private static (double Width, double Height) Box(IReadOnlyList<(double X, double Y, double Width, double Height)> boxes) =>
            (boxes.Max(b => b.X + b.Width) - boxes.Min(b => b.X), boxes.Max(b => b.Y + b.Height) - boxes.Min(b => b.Y));
    }
}
