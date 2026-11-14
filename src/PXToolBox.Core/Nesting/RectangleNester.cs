using System;
using System.Collections.Generic;
using System.Linq;

namespace PXToolBox.Core.Nesting
{
    /// <summary>Uma peça a encaixar: a caixa dela, em mm. <see cref="Index"/> é a posição na lista de
    /// entrada (devolvido nas posições para o chamador achar o objeto).</summary>
    public sealed class NestItem
    {
        public NestItem(int index, string name, double width, double height)
        {
            Index = index;
            Name = name;
            Width = width;
            Height = height;
        }

        public int Index { get; }
        public string Name { get; }
        public double Width { get; }
        public double Height { get; }
    }

    /// <summary>Onde a peça ficou, em mm, a partir do canto superior esquerdo da ÁREA (margem incluída).
    /// <see cref="Width"/>/<see cref="Height"/> já são as da peça girada, quando <see cref="Rotated"/>.</summary>
    public sealed class NestPlacement
    {
        internal NestPlacement(NestItem item, double x, double y, bool rotated)
        {
            Item = item;
            X = x;
            Y = y;
            Rotated = rotated;
            Width = rotated ? item.Height : item.Width;
            Height = rotated ? item.Width : item.Height;
        }

        public NestItem Item { get; }
        public double X { get; }
        public double Y { get; }
        public bool Rotated { get; }
        public double Width { get; }
        public double Height { get; }
    }

    public sealed class NestResult
    {
        internal NestResult(double areaWidth, double? areaHeight, double usedLength, List<NestPlacement> placements, List<NestItem> unplaced)
        {
            AreaWidth = areaWidth;
            AreaHeight = areaHeight;
            UsedLength = usedLength;
            Placements = placements.AsReadOnly();
            Unplaced = unplaced.AsReadOnly();
        }

        public double AreaWidth { get; }

        /// <summary>Null = rolo (comprimento livre).</summary>
        public double? AreaHeight { get; }

        /// <summary>Do topo da área até a borda de baixo da peça mais baixa, mais a margem de baixo
        /// (o comprimento de tecido/página realmente usado); começando por um canto de baixo, medido a
        /// partir do fundo. 0 sem peças encaixadas.</summary>
        public double UsedLength { get; }

        public IReadOnlyList<NestPlacement> Placements { get; }

        /// <summary>Peças que não couberam (nem giradas, quando permitido), na ordem de entrada.</summary>
        public IReadOnlyList<NestItem> Unplaced { get; }

        /// <summary>Aproveitamento: soma das áreas das peças encaixadas ÷ (largura da área × comprimento
        /// usado). 0 sem peças.</summary>
        public double Utilization => UsedLength <= 0 ? 0 : Placements.Sum(p => p.Width * p.Height) / (AreaWidth * UsedLength);
    }

    /// <summary>
    /// Encaixe retangular (Nesting N1 de docs/reference-feature-roadmap.md), sem Corel, em mm.
    /// MaxRects com regra "mais em cima, depois mais à esquerda": serve tanto para área fechada
    /// (página) quanto para rolo de largura fixa e comprimento livre, onde minimiza o comprimento.
    /// <list type="bullet">
    /// <item>Ordem: maior lado primeiro, depois maior área, depois a ordem de entrada — mesmo
    /// resultado para a mesma entrada (determinístico).</item>
    /// <item>Distância entre peças: cada peça ocupa (largura + distância) × (altura + distância) e a
    /// área útil ganha a mesma distância a mais, assim a última peça encosta na margem.</item>
    /// <item>Margem: em volta da área inteira. Giro: só 90°, quando permitido.</item>
    /// </list>
    /// Medidas inválidas: ArgumentException com a mensagem para o usuário.
    /// </summary>
    public static class RectangleNester
    {
        public const int MaximumItems = 2000;

        private const double Epsilon = 1e-6;

        /// <param name="order">Ordem das peças (<see cref="NestOrders"/>); o padrão é a descrita acima.</param>
        /// <param name="origin">Canto de início (<see cref="NestOrigins"/>): as caixas são espelhadas na área.</param>
        public static NestResult Nest(IReadOnlyList<NestItem> items, double areaWidth, double? areaHeight,
            double margin, double spacing, bool allowRotation, NestOrder order = NestOrder.LongestSide,
            NestOrigin origin = NestOrigin.TopLeft)
        {
            Positive(areaWidth, "A largura da área");
            if (areaHeight.HasValue) Positive(areaHeight.Value, "A altura da área");
            NestOrigins.Check(origin, areaHeight);
            NonNegative(margin, "A margem");
            NonNegative(spacing, "A distância entre as peças");
            if (items.Count == 0) throw new ArgumentException("Selecione as peças que vão ser encaixadas.");
            if (items.Count > MaximumItems) throw new ArgumentException($"O encaixe aceita até {MaximumItems} peças.");
            foreach (var item in items) Positive(Math.Min(item.Width, item.Height), "Cada peça");

            var usableWidth = areaWidth - 2 * margin;
            var usableHeight = areaHeight.HasValue ? areaHeight.Value - 2 * margin : (double?)null;
            if (usableWidth <= 0 || usableHeight <= 0)
                throw new ArgumentException("A margem não deixa espaço para as peças.");

            // Rolo: altura "infinita" na prática — a soma de tudo sempre cabe.
            var binHeight = (usableHeight ?? items.Sum(i => Math.Max(i.Width, i.Height) + spacing)) + spacing;
            var free = new List<Rect> { new Rect(0, 0, usableWidth + spacing, binHeight) };

            var ordered = NestOrders.Sort(items, i => i.Width, i => i.Height, order);

            var placements = new List<NestPlacement>();
            var unplaced = new List<NestItem>();
            foreach (var item in ordered)
            {
                Rect? best = null;
                var bestRotated = false;
                foreach (var rotated in allowRotation && Math.Abs(item.Width - item.Height) > Epsilon ? new[] { false, true } : new[] { false })
                {
                    var w = (rotated ? item.Height : item.Width) + spacing;
                    var h = (rotated ? item.Width : item.Height) + spacing;
                    foreach (var space in free)
                    {
                        if (w > space.Width + Epsilon || h > space.Height + Epsilon) continue;
                        var candidate = new Rect(space.X, space.Y, w, h);
                        if (best == null || Better(candidate, best.Value))
                        {
                            best = candidate;
                            bestRotated = rotated;
                        }
                    }
                }

                if (best == null)
                {
                    unplaced.Add(item);
                    continue;
                }

                placements.Add(new NestPlacement(item, margin + best.Value.X, margin + best.Value.Y, bestRotated));
                Split(free, best.Value);
            }

            var used = placements.Count == 0 ? 0 : placements.Max(p => p.Y + p.Height) + margin;
            if (origin != NestOrigin.TopLeft)
                placements = placements.Select(p => new NestPlacement(p.Item,
                    NestOrigins.FromRight(origin) ? areaWidth - p.X - p.Width : p.X,
                    NestOrigins.FromBottom(origin) ? areaHeight!.Value - p.Y - p.Height : p.Y, p.Rotated)).ToList();
            unplaced.Sort((a, b) => a.Index.CompareTo(b.Index));
            return new NestResult(areaWidth, areaHeight, used, placements.OrderBy(p => p.Item.Index).ToList(), unplaced);
        }

        /// <summary>Mais em cima (fundo da peça mais alto, para encurtar o rolo); empate: mais à esquerda.</summary>
        private static bool Better(Rect a, Rect b)
        {
            var bottomA = a.Y + a.Height;
            var bottomB = b.Y + b.Height;
            if (Math.Abs(bottomA - bottomB) > Epsilon) return bottomA < bottomB;
            return a.X < b.X - Epsilon;
        }

        /// <summary>MaxRects: cada espaço livre que encosta na peça vira até quatro espaços maiores
        /// possíveis em volta dela; espaços contidos em outros saem.</summary>
        private static void Split(List<Rect> free, Rect used)
        {
            var next = new List<Rect>();
            foreach (var space in free)
            {
                if (!space.Intersects(used))
                {
                    next.Add(space);
                    continue;
                }

                if (used.X > space.X + Epsilon) next.Add(new Rect(space.X, space.Y, used.X - space.X, space.Height));
                if (used.Right < space.Right - Epsilon) next.Add(new Rect(used.Right, space.Y, space.Right - used.Right, space.Height));
                if (used.Y > space.Y + Epsilon) next.Add(new Rect(space.X, space.Y, space.Width, used.Y - space.Y));
                if (used.Bottom < space.Bottom - Epsilon) next.Add(new Rect(space.X, used.Bottom, space.Width, space.Bottom - used.Bottom));
            }

            free.Clear();
            for (var i = 0; i < next.Count; i++)
            {
                var contained = false;
                for (var j = 0; j < next.Count && !contained; j++)
                {
                    if (i == j) continue;
                    // Iguais: fica só o primeiro.
                    contained = next[j].Contains(next[i]) && (!next[i].Contains(next[j]) || j < i);
                }

                if (!contained) free.Add(next[i]);
            }
        }

        private static void Positive(double value, string what)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                throw new ArgumentException($"{what} precisa ter medida maior que zero.");
        }

        private static void NonNegative(double value, string what)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentException($"{what} precisa ser 0 ou mais.");
        }

        private readonly struct Rect
        {
            public Rect(double x, double y, double width, double height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public double X { get; }
            public double Y { get; }
            public double Width { get; }
            public double Height { get; }
            public double Right => X + Width;
            public double Bottom => Y + Height;

            public bool Intersects(Rect other) =>
                X < other.Right - Epsilon && other.X < Right - Epsilon && Y < other.Bottom - Epsilon && other.Y < Bottom - Epsilon;

            public bool Contains(Rect other) =>
                other.X >= X - Epsilon && other.Y >= Y - Epsilon && other.Right <= Right + Epsilon && other.Bottom <= Bottom + Epsilon;
        }
    }
}
