using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace PXToolBox.Core.Nesting
{
    /// <summary>Uma área fechada de encaixe (retalho, folha), em mm.</summary>
    public sealed class NestArea
    {
        public NestArea(string name, double width, double height)
        {
            Name = name;
            Width = width;
            Height = height;
        }

        public string Name { get; }
        public double Width { get; }
        public double Height { get; }
    }

    /// <summary>Em que ordem as áreas são preenchidas (e desenhadas). Empate: a ordem digitada.</summary>
    public enum NestAreaOrder
    {
        AsTyped,
        SmallestFirst,
        LargestFirst,
    }

    /// <summary>Uma área usada no encaixe em várias áreas: o resultado dela (posições a partir do canto
    /// superior esquerdo DA ÁREA) e onde a área fica no desenho, a partir do canto superior esquerdo da
    /// primeira (<see cref="Left"/> e <see cref="Top"/>, em mm; áreas digitadas ficam lado a lado, alinhadas
    /// no topo; folhas do rolo, uma embaixo da outra).</summary>
    public sealed class AreaFill<TResult>
    {
        public AreaFill(NestArea area, double left, TResult result, double top = 0)
        {
            Area = area;
            Left = left;
            Top = top;
            Result = result;
        }

        public NestArea Area { get; }
        public double Left { get; }
        public double Top { get; }
        public TResult Result { get; }
    }

    /// <summary>Resultado do encaixe em várias áreas. <see cref="Fills"/> só tem as áreas que receberam
    /// alguma peça, na ordem de preenchimento; <see cref="Areas"/> tem todas (o desenho mostra todas).</summary>
    public sealed class AreaNestResult<TResult, TItem>
    {
        public AreaNestResult(IReadOnlyList<NestArea> areas, IReadOnlyList<double> lefts,
            IReadOnlyList<AreaFill<TResult>> fills, IReadOnlyList<TItem> unplaced, IReadOnlyList<double>? tops = null)
        {
            Areas = areas;
            Lefts = lefts;
            Tops = tops ?? areas.Select(_ => 0.0).ToList();
            Fills = fills;
            Unplaced = unplaced;
        }

        public IReadOnlyList<NestArea> Areas { get; }

        /// <summary>Canto esquerdo de cada área de <see cref="Areas"/>, em mm.</summary>
        public IReadOnlyList<double> Lefts { get; }

        /// <summary>Topo de cada área de <see cref="Areas"/>, em mm (0 nas áreas lado a lado).</summary>
        public IReadOnlyList<double> Tops { get; }

        public IReadOnlyList<AreaFill<TResult>> Fills { get; }

        /// <summary>Peças que não couberam em nenhuma área, na ordem de entrada.</summary>
        public IReadOnlyList<TItem> Unplaced { get; }

        /// <summary>Largura do desenho com todas as áreas (lado a lado: com <see cref="NestAreas.GapMm"/> entre elas).</summary>
        public double TotalWidth => Areas.Count == 0 ? 0 : Areas.Select((a, k) => Lefts[k] + a.Width).Max();

        public double TotalHeight => Areas.Count == 0 ? 0 : Areas.Select((a, k) => Tops[k] + a.Height).Max();
    }

    /// <summary>
    /// Encaixe em várias áreas fechadas (retalhos/folhas — N4/N5 de docs/reference-feature-roadmap.md,
    /// a parte que não depende de cadastro de tecidos): preenche a 1ª área com todas as peças, a 2ª com
    /// as que sobraram, e assim por diante, na ordem da lista (a digitada ou a de <see cref="Arrange"/>: quem
    /// quer gastar os retalhos primeiro põe eles em cima ou pede os menores primeiro). Cada área usa o mesmo encaixe de uma página (<see cref="RectangleNester"/> ou
    /// <see cref="ContourNester"/>), com a mesma margem e distância. No desenho as áreas ficam lado a lado,
    /// alinhadas no topo, com <see cref="GapMm"/> entre elas (ou o espaço pedido).
    /// </summary>
    public static class NestAreas
    {
        public const int MaximumAreas = 20;

        /// <summary>Espaço inicial entre uma área (bloco) e a seguinte no desenho; quem chama pode pedir outro.</summary>
        public const double GapMm = 20;

        /// <summary>Maior espaço entre blocos aceito (10 m).</summary>
        public const double MaximumGapMm = 10000;

        private static readonly char[] Separators = { 'x', 'X', '×', '*' };

        /// <summary>
        /// Uma área por linha (ou separadas por ";"), em cm: "100 x 150" ou "Retalho azul: 100 x 150".
        /// Sem nome, vira "Área n". Linhas vazias são ignoradas.
        /// </summary>
        public static IReadOnlyList<NestArea> Parse(string text)
        {
            var areas = new List<NestArea>();
            var entries = (text ?? "").Split(new[] { '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Trim())
                .Where(entry => entry.Length > 0)
                .ToList();
            if (entries.Count == 0) throw new ArgumentException("Digite as áreas, uma por linha: largura x altura em cm (ex.: 100 x 150).");
            if (entries.Count > MaximumAreas) throw new ArgumentException($"O encaixe aceita até {MaximumAreas} áreas.");
            foreach (var entry in entries)
            {
                var number = areas.Count + 1;
                var colon = entry.LastIndexOf(':');
                var name = colon > 0 ? entry.Substring(0, colon).Trim() : "";
                var size = (colon >= 0 ? entry.Substring(colon + 1) : entry).Split(Separators);
                if (size.Length != 2 || !TryCm(size[0], out var width) || !TryCm(size[1], out var height) || width <= 0 || height <= 0)
                    throw new ArgumentException($"Área {number} (“{entry}”): use largura x altura em cm, ex.: 100 x 150.");
                areas.Add(new NestArea(name.Length > 0 ? name : $"Área {number}", width * 10, height * 10));
            }

            return areas;
        }

        /// <summary>As áreas na ordem pedida (pela área de cada uma; empate: a ordem digitada).</summary>
        public static IReadOnlyList<NestArea> Arrange(IReadOnlyList<NestArea> areas, NestAreaOrder order)
        {
            var numbered = areas.Select((area, position) => (area, position));
            return order switch
            {
                NestAreaOrder.SmallestFirst => numbered.OrderBy(p => p.area.Width * p.area.Height).ThenBy(p => p.position).Select(p => p.area).ToList(),
                NestAreaOrder.LargestFirst => numbered.OrderByDescending(p => p.area.Width * p.area.Height).ThenBy(p => p.position).Select(p => p.area).ToList(),
                _ => areas,
            };
        }

        public static string Name(NestAreaOrder order) => order switch
        {
            NestAreaOrder.SmallestFirst => "menores primeiro",
            NestAreaOrder.LargestFirst => "maiores primeiro",
            _ => "na ordem digitada",
        };

        /// <param name="gap">Espaço entre uma área e a seguinte no desenho, em mm.</param>
        public static AreaNestResult<NestResult, NestItem> Nest(IReadOnlyList<NestItem> items, IReadOnlyList<NestArea> areas,
            double margin, double spacing, bool allowRotation, NestOrder order = NestOrder.LongestSide,
            NestOrigin origin = NestOrigin.TopLeft, double gap = GapMm) =>
            Fill(areas, items, (area, remaining) => RectangleNester.Nest(remaining, area.Width, area.Height, margin, spacing, allowRotation, order, origin),
                result => result.Unplaced, result => result.Placements.Count, item => item.Index, gap);

        /// <param name="origin">Canto de início dentro de CADA área.</param>
        public static AreaNestResult<ContourNestResult, ContourPiece> Nest(IReadOnlyList<ContourPiece> pieces, IReadOnlyList<NestArea> areas,
            double margin, double spacing, ContourNester.Rotations rotations, double cell, NestOrder order = NestOrder.Area,
            CancellationToken cancel = default, NestOrigin origin = NestOrigin.TopLeft, double gap = GapMm) =>
            Fill(areas, pieces, (area, remaining) => ContourNester.Nest(remaining, area.Width, area.Height, margin, spacing, rotations, cell, order, cancel, origin),
                result => result.Unplaced, result => result.Placements.Count, piece => piece.Index, gap);

        /// <summary>Folhas de um rolo: no máximo isso (o resto vai para "não couberam").</summary>
        public const int MaximumSheets = 200;

        /// <summary>
        /// Rolo em folhas de <paramref name="width"/> × <paramref name="height"/> mm: enche a 1ª folha, continua
        /// numa nova logo abaixo (sem espaço entre elas — é o mesmo rolo) com o que sobrou, até acabar. Cada folha
        /// usa o encaixe de uma página, com margem e canto de início dela. Uma peça que não cabe numa folha vazia
        /// fica em <see cref="AreaNestResult{TResult,TItem}.Unplaced"/>.
        /// </summary>
        public static AreaNestResult<NestResult, NestItem> Sheets(IReadOnlyList<NestItem> items, double width, double height,
            double margin, double spacing, bool allowRotation, NestOrder order = NestOrder.LongestSide, NestOrigin origin = NestOrigin.TopLeft) =>
            FillSheets(width, height, items, (area, remaining) => RectangleNester.Nest(remaining, area.Width, area.Height, margin, spacing, allowRotation, order, origin),
                result => result.Unplaced, result => result.Placements.Count, item => item.Index);

        public static AreaNestResult<ContourNestResult, ContourPiece> Sheets(IReadOnlyList<ContourPiece> pieces, double width, double height,
            double margin, double spacing, ContourNester.Rotations rotations, double cell, NestOrder order = NestOrder.Area,
            CancellationToken cancel = default, NestOrigin origin = NestOrigin.TopLeft) =>
            FillSheets(width, height, pieces, (area, remaining) => ContourNester.Nest(remaining, area.Width, area.Height, margin, spacing, rotations, cell, order, cancel, origin),
                result => result.Unplaced, result => result.Placements.Count, piece => piece.Index);

        /// <summary>
        /// As mesmas áreas e o mesmo encaixe, em blocos lado a lado (alinhados no topo, com <paramref name="gap"/> mm
        /// entre eles) — para as folhas do rolo, que o cálculo empilha uma embaixo da outra. Só muda o desenho:
        /// as posições dentro de cada bloco ficam iguais.
        /// </summary>
        public static AreaNestResult<TResult, TItem> SideBySide<TResult, TItem>(AreaNestResult<TResult, TItem> result, double gap = GapMm)
        {
            CheckGap(gap);
            var lefts = new List<double>();
            var left = 0.0;
            foreach (var area in result.Areas)
            {
                lefts.Add(left);
                left += area.Width + gap;
            }

            var fills = result.Fills
                .Select(fill => new AreaFill<TResult>(fill.Area, lefts[IndexOf(result.Areas, fill.Area)], fill.Result))
                .ToList();
            return new AreaNestResult<TResult, TItem>(result.Areas, lefts, fills, result.Unplaced);
        }

        /// <summary>Nome do grupo sem nome (peça fora de camada).</summary>
        public const string UnnamedGroup = "Sem camada";

        /// <summary>
        /// Separar por material (camada): encaixa as peças de cada grupo sozinhas, com <paramref name="nest"/>
        /// (rolo livre ou em folhas), e põe os resultados lado a lado, na ordem em que cada grupo aparece na
        /// lista, com <paramref name="gap"/> mm entre eles. As áreas ganham o nome do grupo ("Tactel — Folha 1"); dentro
        /// de um grupo, a arrumação de <paramref name="nest"/> fica igual.
        /// </summary>
        public static AreaNestResult<TResult, TItem> ByGroup<TResult, TItem>(IReadOnlyList<TItem> items, Func<TItem, string> groupOf,
            Func<IReadOnlyList<TItem>, AreaNestResult<TResult, TItem>> nest, Func<TItem, int> indexOf, double gap = GapMm)
        {
            if (items.Count == 0) throw new ArgumentException("Selecione as peças que vão ser encaixadas.");
            CheckGap(gap);

            var areas = new List<NestArea>();
            var lefts = new List<double>();
            var tops = new List<double>();
            var fills = new List<AreaFill<TResult>>();
            var unplaced = new List<TItem>();
            var offset = 0.0;
            foreach (var group in items.GroupBy(item => GroupName(groupOf(item))))
            {
                AreaNestResult<TResult, TItem> result;
                try
                {
                    result = nest(group.ToList());
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"{group.Key}: {ex.Message}");
                }

                unplaced.AddRange(result.Unplaced);
                if (result.Areas.Count == 0) continue;
                var renamed = new Dictionary<NestArea, NestArea>();
                for (var k = 0; k < result.Areas.Count; k++)
                {
                    var area = result.Areas[k];
                    renamed[area] = new NestArea($"{group.Key} — {area.Name}", area.Width, area.Height);
                    areas.Add(renamed[area]);
                    lefts.Add(offset + result.Lefts[k]);
                    tops.Add(result.Tops[k]);
                }

                fills.AddRange(result.Fills.Select(f => new AreaFill<TResult>(renamed[f.Area], offset + f.Left, f.Result, f.Top)));
                offset += result.TotalWidth + gap;
            }

            return new AreaNestResult<TResult, TItem>(areas, lefts, fills, unplaced.OrderBy(indexOf).ToList(), tops);
        }

        private static void CheckGap(double gap)
        {
            if (!(gap >= 0 && gap <= MaximumGapMm))
                throw new ArgumentException($"Distância entre blocos: use de 0 a {MaximumGapMm / 10:0} cm.");
        }

        private static string GroupName(string? name) => string.IsNullOrWhiteSpace(name) ? UnnamedGroup : name!.Trim();

        /// <summary>Rolo de comprimento livre como uma área só ("Rolo", largura × comprimento usado), para juntar
        /// com <see cref="ByGroup"/>. Nada encaixado = nenhuma área.</summary>
        public static AreaNestResult<NestResult, NestItem> Roll(NestResult result) =>
            Single(result, result.AreaWidth, result.UsedLength, result.Placements.Count, result.Unplaced);

        public static AreaNestResult<ContourNestResult, ContourPiece> Roll(ContourNestResult result) =>
            Single(result, result.AreaWidth, result.UsedLength, result.Placements.Count, result.Unplaced);

        private static AreaNestResult<TResult, TItem> Single<TResult, TItem>(TResult result, double width, double length, int placed,
            IReadOnlyList<TItem> unplaced)
        {
            if (placed == 0)
                return new AreaNestResult<TResult, TItem>(new NestArea[0], new double[0], new AreaFill<TResult>[0], unplaced);
            var roll = new NestArea("Rolo", width, length);
            return new AreaNestResult<TResult, TItem>(new[] { roll }, new[] { 0.0 }, new[] { new AreaFill<TResult>(roll, 0, result) }, unplaced);
        }

        private static int IndexOf(IReadOnlyList<NestArea> areas, NestArea area)
        {
            for (var k = 0; k < areas.Count; k++)
                if (ReferenceEquals(areas[k], area)) return k;
            throw new ArgumentException("A área preenchida não está na lista de áreas.");
        }

        private static AreaNestResult<TResult, TItem> FillSheets<TResult, TItem>(double width, double height, IReadOnlyList<TItem> items,
            Func<NestArea, IReadOnlyList<TItem>, TResult> nest, Func<TResult, IReadOnlyList<TItem>> unplacedOf,
            Func<TResult, int> placedCount, Func<TItem, int> indexOf)
        {
            if (!(height > 0)) throw new ArgumentException("Altura da folha: use um número maior que zero (ou deixe vazio para comprimento livre).");
            if (items.Count == 0) throw new ArgumentException("Selecione as peças que vão ser encaixadas.");

            var areas = new List<NestArea>();
            var tops = new List<double>();
            var fills = new List<AreaFill<TResult>>();
            IReadOnlyList<TItem> remaining = items;
            while (remaining.Count > 0 && areas.Count < MaximumSheets)
            {
                var sheet = new NestArea($"Folha {areas.Count + 1}", width, height);
                var result = nest(sheet, remaining);
                // Nada coube numa folha vazia: as que sobraram não cabem em nenhuma.
                if (placedCount(result) == 0) break;
                var top = areas.Count * height;
                areas.Add(sheet);
                tops.Add(top);
                fills.Add(new AreaFill<TResult>(sheet, 0, result, top));
                remaining = unplacedOf(result);
            }

            return new AreaNestResult<TResult, TItem>(areas, areas.Select(_ => 0.0).ToList(), fills, remaining.OrderBy(indexOf).ToList(), tops);
        }

        private static AreaNestResult<TResult, TItem> Fill<TResult, TItem>(IReadOnlyList<NestArea> areas, IReadOnlyList<TItem> items,
            Func<NestArea, IReadOnlyList<TItem>, TResult> nest, Func<TResult, IReadOnlyList<TItem>> unplacedOf,
            Func<TResult, int> placedCount, Func<TItem, int> indexOf, double gap)
        {
            if (areas.Count == 0) throw new ArgumentException("Digite pelo menos uma área.");
            if (areas.Count > MaximumAreas) throw new ArgumentException($"O encaixe aceita até {MaximumAreas} áreas.");
            if (items.Count == 0) throw new ArgumentException("Selecione as peças que vão ser encaixadas.");
            CheckGap(gap);

            var lefts = new List<double>();
            var left = 0.0;
            foreach (var area in areas)
            {
                lefts.Add(left);
                left += area.Width + gap;
            }

            var fills = new List<AreaFill<TResult>>();
            IReadOnlyList<TItem> remaining = items;
            for (var k = 0; k < areas.Count && remaining.Count > 0; k++)
            {
                TResult result;
                try
                {
                    result = nest(areas[k], remaining);
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"{areas[k].Name}: {ex.Message}");
                }

                if (placedCount(result) > 0) fills.Add(new AreaFill<TResult>(areas[k], lefts[k], result));
                remaining = unplacedOf(result);
            }

            return new AreaNestResult<TResult, TItem>(areas, lefts, fills, remaining.OrderBy(indexOf).ToList());
        }

        private static bool TryCm(string text, out double value) =>
            double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }
}
