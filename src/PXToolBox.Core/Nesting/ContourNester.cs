using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PXToolBox.Core.Nesting
{
    public readonly struct NestPoint
    {
        public NestPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
    }

    /// <summary>
    /// Uma peça pelo contorno, em mm, X para a direita e Y para BAIXO (como o resto do encaixe).
    /// <see cref="Shapes"/>: um item por objeto da peça; cada objeto é uma lista de anéis fechados
    /// preenchidos por par/ímpar (o furo de uma curva fica livre). Objetos diferentes se SOMAM —
    /// um logo por cima da peça não vira furo.
    /// </summary>
    public sealed class ContourPiece
    {
        public ContourPiece(int index, string name, IReadOnlyList<IReadOnlyList<IReadOnlyList<NestPoint>>> shapes)
        {
            Index = index;
            Name = name;
            Shapes = shapes;
            var points = shapes.SelectMany(s => s).SelectMany(r => r).ToList();
            if (points.Count == 0) throw new ArgumentException($"A peça “{name}” não tem contorno.");
            MinX = points.Min(p => p.X);
            MinY = points.Min(p => p.Y);
            Width = points.Max(p => p.X) - MinX;
            Height = points.Max(p => p.Y) - MinY;
        }

        public int Index { get; }
        public string Name { get; }
        public IReadOnlyList<IReadOnlyList<IReadOnlyList<NestPoint>>> Shapes { get; }
        public double MinX { get; }
        public double MinY { get; }
        public double Width { get; }
        public double Height { get; }
    }

    /// <summary>Peça encaixada: girada <see cref="Rotation"/> graus (anti-horário na tela, como
    /// Shape.Rotate) e com a caixa do contorno girado começando em (<see cref="X"/>, <see cref="Y"/>),
    /// a partir do canto superior esquerdo da área. <see cref="Shapes"/> já está na posição final.</summary>
    public sealed class ContourPlacement
    {
        public ContourPlacement(ContourPiece piece, int rotation, double x, double y, double width, double height,
            IReadOnlyList<IReadOnlyList<IReadOnlyList<NestPoint>>> shapes)
        {
            Piece = piece;
            Rotation = rotation;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Shapes = shapes;
        }

        public ContourPiece Piece { get; }
        public int Rotation { get; }
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public IReadOnlyList<IReadOnlyList<IReadOnlyList<NestPoint>>> Shapes { get; }
        public double CenterX => X + Width / 2;
        public double CenterY => Y + Height / 2;
    }

    public sealed class ContourNestResult
    {
        public ContourNestResult(double areaWidth, double? areaHeight, double usedLength, double cell,
            List<ContourPlacement> placements, List<ContourPiece> unplaced, double occupiedArea)
        {
            AreaWidth = areaWidth;
            AreaHeight = areaHeight;
            UsedLength = usedLength;
            Cell = cell;
            Placements = placements.AsReadOnly();
            Unplaced = unplaced.AsReadOnly();
            OccupiedArea = occupiedArea;
        }

        public double AreaWidth { get; }
        public double? AreaHeight { get; }
        public double UsedLength { get; }

        /// <summary>Lado da célula da grade usada (mm) — a precisão do encaixe.</summary>
        public double Cell { get; }

        public IReadOnlyList<ContourPlacement> Placements { get; }
        public IReadOnlyList<ContourPiece> Unplaced { get; }

        /// <summary>Área das peças medida pela grade (células tocadas × célula²): um pouco acima da real.</summary>
        public double OccupiedArea { get; }

        public double Utilization => UsedLength <= 0 ? 0 : OccupiedArea / (AreaWidth * UsedLength);
    }

    /// <summary>
    /// Encaixe pelo contorno (Nesting N3 de docs/reference-feature-roadmap.md), sem Corel, em mm.
    /// <list type="bullet">
    /// <item>Cada peça girada (0°, 90°, 180°, 270°, ou só 0° e 180°, ou só 0°) vira uma grade de células
    /// de lado <c>cell</c>: marca toda célula que o contorno cruza e toda célula cujo meio de linha está
    /// dentro (par/ímpar por objeto, objetos somados). Assim nenhuma parte da peça fica fora das células
    /// marcadas.</item>
    /// <item>Distância: em volta de cada peça colocada, bloqueia mais ⌈distância ÷ célula⌉ células em
    /// todas as direções; peças só ocupam células livres e não bloqueadas → a distância real é pelo menos
    /// a pedida. Concavidades e furos ficam livres para outras peças.</item>
    /// <item>Busca: para cada giro, a primeira posição de cima para baixo, da esquerda para a direita;
    /// fica o giro cujo fundo fica mais alto (empate: o primeiro giro da lista). Ordem das
    /// peças: maior área da caixa, depois a ordem de entrada → determinístico.</item>
    /// <item>Fileiras alinhadas (dev.112): o encaixe também roda com a regra "a posição mais à esquerda entre as
    /// quase tão altas quanto a mais alta" (<see cref="AlignTolerances"/>) e fica com o melhor resultado — evita as
    /// fileiras 2-1-2-1 de peças de topo/base curvos.</item>
    /// </list>
    /// <see cref="Verify"/> confere o resultado de forma independente, nos polígonos exatos.
    /// </summary>
    public static class ContourNester
    {
        public const int MaximumItems = 500;

        public const long MaximumGridCells = 40_000_000;

        public enum Rotations
        {
            None,
            HalfTurn,
            QuarterTurns,
        }

        /// <param name="order">Ordem das peças (<see cref="NestOrders"/>); o padrão é a descrita acima.</param>
        /// <param name="cancel">Conferido antes de cada peça (OperationCanceledException).</param>
        /// <param name="origin">Canto de início (<see cref="NestOrigins"/>): calcula com as peças espelhadas e
        /// espelha o resultado de volta.</param>
        /// <param name="alignTolerance">Null = tenta <see cref="AlignTolerances"/>; 0 = só a regra antiga; outro valor =
        /// a antiga e só essa tolerância (testes).</param>
        public static ContourNestResult Nest(IReadOnlyList<ContourPiece> pieces, double areaWidth, double? areaHeight,
            double margin, double spacing, Rotations rotations, double cell, NestOrder order = NestOrder.Area,
            CancellationToken cancel = default, NestOrigin origin = NestOrigin.TopLeft, double? alignTolerance = null)
        {
            Positive(areaWidth, "A largura da área");
            if (areaHeight.HasValue) Positive(areaHeight.Value, "A altura da área");
            NestOrigins.Check(origin, areaHeight);
            if (origin != NestOrigin.TopLeft)
                return Mirrored(pieces, areaWidth, areaHeight, margin, spacing, rotations, cell, order, cancel, origin, alignTolerance);
            NonNegative(margin, "A margem");
            NonNegative(spacing, "A distância entre as peças");
            if (double.IsNaN(cell) || cell < 0.5 || cell > 50) throw new ArgumentException("A precisão precisa estar entre 0,5 e 50 mm.");
            if (pieces.Count == 0) throw new ArgumentException("Selecione as peças que vão ser encaixadas.");
            if (pieces.Count > MaximumItems) throw new ArgumentException($"O encaixe pelo contorno aceita até {MaximumItems} peças.");

            var columns = (int)Math.Floor((areaWidth - 2 * margin) / cell + 1e-9);
            int? rowLimit = areaHeight.HasValue ? (int)Math.Floor((areaHeight.Value - 2 * margin) / cell + 1e-9) : (int?)null;
            if (columns <= 0 || rowLimit <= 0) throw new ArgumentException("A margem não deixa espaço para as peças.");
            var reach = (int)Math.Ceiling(spacing / cell - 1e-9);

            var angles = rotations switch
            {
                Rotations.QuarterTurns => new[] { 0, 90, 180, 270 },
                Rotations.HalfTurn => new[] { 0, 180 },
                _ => new[] { 0 },
            };

            var masks = pieces.ToDictionary(p => p, p => angles.Select(a => Mask.Of(p, a, cell)).ToArray());
            if ((long)columns * (rowLimit ?? masks.Values.Sum(m => m.Max(x => Math.Max(x.Rows, x.Columns)) + reach)) > MaximumGridCells)
                throw new ArgumentException("Área grande demais para essa precisão. Aumente a precisão (mm).");

            var ordered = NestOrders.Sort(pieces, p => p.Width, p => p.Height, order).ToList();
            ContourNestResult Run(double leftTolerance) =>
                Fill(ordered, masks, new Grid(columns, rowLimit), areaWidth, areaHeight, margin, cell, reach, leftTolerance, cancel);

            // Regra antiga (a posição mais alta) e as de fileira alinhada; fica a que encaixa mais / gasta menos
            // (empate: a de menor tolerância, depois a antiga — nesta ordem de tentativa).
            var best = Run(0);
            foreach (var tolerance in alignTolerance.HasValue ? new[] { alignTolerance.Value } : AlignTolerances)
            {
                if (tolerance <= 0) continue;
                var aligned = Run(tolerance);
                if (!NestScore.Of(best).IsBetterThan(NestScore.Of(aligned))) best = aligned;
            }

            return best;
        }

        /// <summary>
        /// Quanto mais baixo (fração da altura da peça) uma posição mais à esquerda pode ficar e ainda ganhar da mais
        /// alta. Sem isso, peças de topo/base curvos sobem no "vale" entre as duas de cima, no meio da fileira, e a
        /// fileira seguinte não comporta duas lado a lado (fileiras 2-1-2-1). Tolerância grande demais também
        /// desalinha (deixa a peça descer demais à esquerda); por isso tenta algumas e fica com a melhor.
        /// </summary>
        public static IReadOnlyList<double> AlignTolerances { get; } = new[] { 0.12, 0.06, 0.03 };

        /// <param name="leftTolerance">0 = a posição mais alta (empate: mais à esquerda). &gt; 0 = entre as posições até
        /// essa fração da altura da peça mais baixas que a mais alta, a mais à esquerda.</param>
        private static ContourNestResult Fill(IReadOnlyList<ContourPiece> ordered, IReadOnlyDictionary<ContourPiece, Mask[]> masks, Grid grid,
            double areaWidth, double? areaHeight, double margin, double cell, int reach, double leftTolerance, CancellationToken cancel)
        {
            var placements = new List<ContourPlacement>();
            var unplaced = new List<ContourPiece>();
            long occupiedCells = 0;
            foreach (var piece in ordered)
            {
                cancel.ThrowIfCancellationRequested();
                var candidates = new List<(Mask Mask, int Column, int Row)>();
                (Mask Mask, int Column, int Row)? highest = null;
                foreach (var mask in masks[piece])
                {
                    var limit = leftTolerance <= 0 && highest.HasValue ? highest.Value.Row + highest.Value.Mask.Rows : int.MaxValue;
                    var found = grid.FirstFit(mask, limit);
                    if (found == null) continue;
                    var (column, row) = found.Value;
                    candidates.Add((mask, column, row));
                    if (highest == null || Better(row + mask.Rows, column, highest.Value.Row + highest.Value.Mask.Rows, highest.Value.Column))
                        highest = (mask, column, row);
                }

                if (highest == null)
                {
                    unplaced.Add(piece);
                    continue;
                }

                var best = highest.Value;
                if (leftTolerance > 0)
                {
                    // Fundo até `slack` linhas abaixo do mais alto; entre eles, a coluna menor (empate: o mais alto).
                    var slack = (int)Math.Ceiling(leftTolerance * best.Mask.Rows);
                    var bottomLimit = best.Row + best.Mask.Rows + slack;
                    foreach (var (mask, column, row) in candidates)
                    {
                        var left = grid.LeftmostAbove(mask, row, column, bottomLimit) ?? (column, row);
                        if (left.Column < best.Column || (left.Column == best.Column && left.Row + mask.Rows < best.Row + best.Mask.Rows))
                            best = (mask, left.Column, left.Row);
                    }
                }

                var (chosen, at, top) = best;
                grid.Place(chosen, at, top, reach);
                occupiedCells += chosen.Count;
                var x = margin + at * cell;
                var y = margin + top * cell;
                placements.Add(new ContourPlacement(piece, chosen.Angle, x, y, chosen.Width, chosen.Height,
                    Translate(chosen.Shapes, x, y)));
            }

            var used = placements.Count == 0 ? 0 : placements.Max(p => p.Y + p.Height) + margin;
            unplaced.Sort((a, b) => a.Index.CompareTo(b.Index));
            return new ContourNestResult(areaWidth, areaHeight, used, cell,
                placements.OrderBy(p => p.Piece.Index).ToList(), unplaced, occupiedCells * cell * cell);
        }

        /// <summary>
        /// Outro canto: espelha as peças (direita → X invertido; baixo → Y invertido), encaixa a partir do canto
        /// superior esquerdo e espelha o resultado de volta na área. Espelhar duas vezes devolve a peça
        /// original; um espelho só inverte o sentido do giro (θ vira 360 − θ, que continua permitido: as listas
        /// de giros são simétricas).
        /// </summary>
        private static ContourNestResult Mirrored(IReadOnlyList<ContourPiece> pieces, double areaWidth, double? areaHeight,
            double margin, double spacing, Rotations rotations, double cell, NestOrder order, CancellationToken cancel,
            NestOrigin origin, double? alignTolerance)
        {
            var flipX = NestOrigins.FromRight(origin);
            var flipY = NestOrigins.FromBottom(origin);
            var originals = new Dictionary<ContourPiece, ContourPiece>();
            var mirrored = pieces.Select(piece =>
            {
                var copy = new ContourPiece(piece.Index, piece.Name,
                    Map(piece.Shapes, p => new NestPoint(flipX ? -p.X : p.X, flipY ? -p.Y : p.Y)));
                originals[copy] = piece;
                return copy;
            }).ToList();

            var result = Nest(mirrored, areaWidth, areaHeight, margin, spacing, rotations, cell, order, cancel, alignTolerance: alignTolerance);
            var placements = result.Placements.Select(p => new ContourPlacement(originals[p.Piece],
                flipX != flipY ? (360 - p.Rotation) % 360 : p.Rotation,
                flipX ? areaWidth - p.X - p.Width : p.X,
                flipY ? areaHeight!.Value - p.Y - p.Height : p.Y,
                p.Width, p.Height,
                Map(p.Shapes, q => new NestPoint(flipX ? areaWidth - q.X : q.X, flipY ? areaHeight!.Value - q.Y : q.Y)))).ToList();
            return new ContourNestResult(areaWidth, areaHeight, result.UsedLength, result.Cell, placements,
                result.Unplaced.Select(p => originals[p]).ToList(), result.OccupiedArea);
        }

        private static IReadOnlyList<IReadOnlyList<IReadOnlyList<NestPoint>>> Map(
            IReadOnlyList<IReadOnlyList<IReadOnlyList<NestPoint>>> shapes, Func<NestPoint, NestPoint> map) =>
            shapes.Select(s => (IReadOnlyList<IReadOnlyList<NestPoint>>)s
                .Select(r => (IReadOnlyList<NestPoint>)r.Select(map).ToList()).ToList()).ToList();

        private static bool Better(int bottom, int column, int bestBottom, int bestColumn) =>
            bottom < bestBottom || (bottom == bestBottom && column < bestColumn);

        /// <summary>Gira em volta da origem, anti-horário NA TELA (Y para baixo): 90° leva (x, y) a (y, −x).</summary>
        public static NestPoint Rotate(NestPoint point, int degrees) => (((degrees % 360) + 360) % 360) switch
        {
            90 => new NestPoint(point.Y, -point.X),
            180 => new NestPoint(-point.X, -point.Y),
            270 => new NestPoint(-point.Y, point.X),
            _ => point,
        };

        private static IReadOnlyList<IReadOnlyList<IReadOnlyList<NestPoint>>> Translate(
            IReadOnlyList<IReadOnlyList<IReadOnlyList<NestPoint>>> shapes, double dx, double dy) =>
            shapes.Select(s => (IReadOnlyList<IReadOnlyList<NestPoint>>)s
                .Select(r => (IReadOnlyList<NestPoint>)r.Select(p => new NestPoint(p.X + dx, p.Y + dy)).ToList()).ToList()).ToList();

        // ---- Verificação independente (polígonos exatos, sem grade) ----

        /// <summary>Problemas do resultado, conferido sem a grade: peça fora da área (com a margem), peças
        /// encostando/sobrepostas ou mais perto que a distância (tolerância 0,01 mm). Vazio = ok.</summary>
        public static IReadOnlyList<string> Verify(ContourNestResult result, double margin, double spacing)
        {
            const double tolerance = 0.01;
            var problems = new List<string>();
            var bottomLimit = result.AreaHeight ?? double.PositiveInfinity;
            foreach (var p in result.Placements)
            {
                var points = p.Shapes.SelectMany(s => s).SelectMany(r => r).ToList();
                if (points.Min(q => q.X) < margin - tolerance || points.Max(q => q.X) > result.AreaWidth - margin + tolerance ||
                    points.Min(q => q.Y) < margin - tolerance || points.Max(q => q.Y) > bottomLimit - margin + tolerance)
                    problems.Add($"{p.Piece.Name} sai da área.");
            }

            var list = result.Placements.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                for (var j = i + 1; j < list.Count; j++)
                {
                    var a = list[i];
                    var b = list[j];
                    if (a.X > b.X + b.Width + spacing || b.X > a.X + a.Width + spacing ||
                        a.Y > b.Y + b.Height + spacing || b.Y > a.Y + a.Height + spacing)
                        continue;

                    var (distance, crossed) = Distance(a, b);
                    if (crossed || Overlaps(a, b) || Overlaps(b, a))
                        problems.Add($"{a.Piece.Name} e {b.Piece.Name} se sobrepõem.");
                    else if (distance < spacing - tolerance)
                        problems.Add($"{a.Piece.Name} e {b.Piece.Name} ficaram a menos de {spacing:0.##} mm.");
                }
            }

            return problems;
        }

        /// <summary>Algum vértice de <paramref name="a"/> dentro de algum objeto de <paramref name="b"/>.</summary>
        private static bool Overlaps(ContourPlacement a, ContourPlacement b) =>
            a.Shapes.SelectMany(s => s).SelectMany(r => r).Any(point => b.Shapes.Any(shape => Inside(shape, point)));

        private static bool Inside(IReadOnlyList<IReadOnlyList<NestPoint>> shape, NestPoint point)
        {
            var inside = false;
            foreach (var ring in shape)
            {
                for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
                {
                    var pi = ring[i];
                    var pj = ring[j];
                    if ((pi.Y > point.Y) != (pj.Y > point.Y) &&
                        point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X)
                        inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Menor distância entre os contornos e se algum lado cruza o outro (cruzar = sobrepor;
        /// só encostar num ponto não é cruzar).</summary>
        private static (double Distance, bool Crossed) Distance(ContourPlacement a, ContourPlacement b)
        {
            var best = double.PositiveInfinity;
            foreach (var (a1, a2) in Edges(a))
            {
                foreach (var (b1, b2) in Edges(b))
                {
                    if (SegmentsCross(a1, a2, b1, b2)) return (0, true);
                    best = Math.Min(best, SegmentDistance(a1, a2, b1, b2));
                }
            }

            return (best, false);
        }

        private static IEnumerable<(NestPoint, NestPoint)> Edges(ContourPlacement placement)
        {
            foreach (var ring in placement.Shapes.SelectMany(s => s))
                for (var i = 0; i < ring.Count; i++)
                    yield return (ring[i], ring[(i + 1) % ring.Count]);
        }

        private static double SegmentDistance(NestPoint p1, NestPoint p2, NestPoint q1, NestPoint q2) =>
            Math.Min(Math.Min(PointSegment(p1, q1, q2), PointSegment(p2, q1, q2)),
                Math.Min(PointSegment(q1, p1, p2), PointSegment(q2, p1, p2)));

        private static double Cross(NestPoint o, NestPoint a, NestPoint b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

        private static bool SegmentsCross(NestPoint p1, NestPoint p2, NestPoint q1, NestPoint q2)
        {
            var d1 = Cross(q1, q2, p1);
            var d2 = Cross(q1, q2, p2);
            var d3 = Cross(p1, p2, q1);
            var d4 = Cross(p1, p2, q2);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private static double PointSegment(NestPoint p, NestPoint a, NestPoint b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = dx * dx + dy * dy;
            var t = length <= 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length));
            var x = a.X + t * dx - p.X;
            var y = a.Y + t * dy - p.Y;
            return Math.Sqrt(x * x + y * y);
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

        // ---- Grade ----

        /// <summary>A peça girada em células: linha a linha como bits (bit c = coluna c).</summary>
        private sealed class Mask
        {
            private Mask(int angle, int columns, int rows, ulong[][] bits, int count, double width, double height,
                IReadOnlyList<IReadOnlyList<IReadOnlyList<NestPoint>>> shapes)
            {
                Angle = angle;
                Columns = columns;
                Rows = rows;
                Bits = bits;
                Count = count;
                Width = width;
                Height = height;
                Shapes = shapes;
            }

            public int Angle { get; }
            public int Columns { get; }
            public int Rows { get; }
            public ulong[][] Bits { get; }
            public int Count { get; }
            public double Width { get; }
            public double Height { get; }

            /// <summary>Contorno girado com a caixa começando em (0, 0).</summary>
            public IReadOnlyList<IReadOnlyList<IReadOnlyList<NestPoint>>> Shapes { get; }

            public static Mask Of(ContourPiece piece, int angle, double cell)
            {
                var rotated = piece.Shapes.Select(s => s.Select(r => r.Select(p => Rotate(p, angle)).ToList()).ToList()).ToList();
                var all = rotated.SelectMany(s => s).SelectMany(r => r).ToList();
                var minX = all.Min(p => p.X);
                var minY = all.Min(p => p.Y);
                var shapes = rotated.Select(s => (IReadOnlyList<IReadOnlyList<NestPoint>>)s
                    .Select(r => (IReadOnlyList<NestPoint>)r.Select(p => new NestPoint(p.X - minX, p.Y - minY)).ToList()).ToList()).ToList();
                var width = all.Max(p => p.X) - minX;
                var height = all.Max(p => p.Y) - minY;
                var columns = Math.Max(1, (int)Math.Ceiling(width / cell - 1e-9));
                var rows = Math.Max(1, (int)Math.Ceiling(height / cell - 1e-9));
                var cells = new bool[rows, columns];

                void Mark(double cx, double cy)
                {
                    var c = Math.Min(columns - 1, Math.Max(0, (int)Math.Floor(cx)));
                    var r = Math.Min(rows - 1, Math.Max(0, (int)Math.Floor(cy)));
                    cells[r, c] = true;
                }

                foreach (var shape in shapes)
                {
                    // Contorno: amostras a cada 1/5 de célula marcam toda célula que a linha atravessa.
                    foreach (var ring in shape)
                    {
                        for (var i = 0; i < ring.Count; i++)
                        {
                            var a = ring[i];
                            var b = ring[(i + 1) % ring.Count];
                            var steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)) / cell * 5));
                            for (var k = 0; k <= steps; k++)
                                Mark((a.X + (b.X - a.X) * k / steps) / cell, (a.Y + (b.Y - a.Y) * k / steps) / cell);
                        }
                    }

                    // Interior: par/ímpar no meio de cada linha de células.
                    for (var r = 0; r < rows; r++)
                    {
                        var y = (r + 0.5) * cell;
                        var crossings = new List<double>();
                        foreach (var ring in shape)
                        {
                            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
                            {
                                var pi = ring[i];
                                var pj = ring[j];
                                if ((pi.Y > y) != (pj.Y > y))
                                    crossings.Add(pi.X + (pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y));
                            }
                        }

                        crossings.Sort();
                        for (var k = 0; k + 1 < crossings.Count; k += 2)
                            for (var c = (int)Math.Floor(crossings[k] / cell); c <= (int)Math.Floor(crossings[k + 1] / cell); c++)
                                if (c >= 0 && c < columns) cells[r, c] = true;
                    }
                }

                var words = (columns + 63) / 64;
                var bits = new ulong[rows][];
                var count = 0;
                for (var r = 0; r < rows; r++)
                {
                    bits[r] = new ulong[words];
                    for (var c = 0; c < columns; c++)
                    {
                        if (!cells[r, c]) continue;
                        bits[r][c >> 6] |= 1UL << (c & 63);
                        count++;
                    }
                }

                return new Mask(angle, columns, rows, bits, count, width, height, shapes);
            }

            private string? _key;

            /// <summary>Mesmas células → mesma chave (peças iguais, como os tamanhos repetidos de um pedido).</summary>
            public string Key => _key ??= Columns + "x" + Rows + ":" + string.Join(",", Bits.SelectMany(r => r));
        }

        /// <summary>Área em células: "bloqueadas" = ocupadas por peças + a faixa da distância em volta.
        /// Rolo: linhas criadas conforme a necessidade.</summary>
        private sealed class Grid
        {
            private readonly int _columns;
            private readonly int? _rowLimit;
            private readonly int _words;
            private readonly List<ulong[]> _blocked = new List<ulong[]>();

            public Grid(int columns, int? rowLimit)
            {
                _columns = columns;
                _rowLimit = rowLimit;
                _words = (columns + 63) / 64;
            }

            private ulong[]? Row(int row) => row < _blocked.Count ? _blocked[row] : null;

            /// <summary>Por formato de peça (<see cref="Mask.Key"/>): acima desta linha ela já não cabia em lugar
            /// nenhum. Vale para sempre, porque as células só são bloqueadas, nunca liberadas.</summary>
            private readonly Dictionary<string, int> _firstFree = new Dictionary<string, int>();

            /// <summary>Primeira posição livre (linha menor, depois coluna menor) com o fundo ACIMA de
            /// <paramref name="bottomLimit"/>; null se não houver. No rolo sempre termina: abaixo da
            /// última linha bloqueada tudo está livre.</summary>
            public (int Column, int Row)? FirstFit(Mask mask, int bottomLimit)
            {
                if (mask.Columns > _columns) return null;
                _firstFree.TryGetValue(mask.Key, out var start);
                for (var row = start; ; row++)
                {
                    if (row + mask.Rows >= bottomLimit || (_rowLimit.HasValue && row + mask.Rows > _rowLimit.Value))
                    {
                        _firstFree[mask.Key] = row;
                        return null;
                    }

                    for (var column = 0; column + mask.Columns <= _columns; column++)
                    {
                        if (!Fits(mask, column, row)) continue;
                        _firstFree[mask.Key] = row;
                        return (column, row);
                    }
                }
            }

            /// <summary>A posição livre de coluna menor que <paramref name="column"/>, a partir da linha
            /// <paramref name="row"/>, com o fundo até <paramref name="bottomLimit"/> (exclusivo); em cada coluna, a linha
            /// mais alta. Null se não houver.</summary>
            public (int Column, int Row)? LeftmostAbove(Mask mask, int row, int column, int bottomLimit)
            {
                for (var c = 0; c < column; c++)
                {
                    for (var r = row; r + mask.Rows < bottomLimit; r++)
                    {
                        if (_rowLimit.HasValue && r + mask.Rows > _rowLimit.Value) break;
                        if (Fits(mask, c, r)) return (c, r);
                    }
                }

                return null;
            }

            private bool Fits(Mask mask, int column, int row)
            {
                for (var r = 0; r < mask.Rows; r++)
                {
                    var target = Row(row + r);
                    if (target == null) return true; // daqui para baixo, rolo vazio
                    if (Hits(mask.Bits[r], target, column)) return false;
                }

                return true;
            }

            /// <summary>Linha da peça deslocada <paramref name="shift"/> colunas AND linha da área.</summary>
            private static bool Hits(ulong[] piece, ulong[] area, int shift)
            {
                var wordShift = shift >> 6;
                var bitShift = shift & 63;
                for (var i = 0; i < piece.Length; i++)
                {
                    var value = piece[i];
                    if (value == 0) continue;
                    var low = value << bitShift;
                    var index = i + wordShift;
                    if (index < area.Length && (area[index] & low) != 0) return true;
                    if (bitShift != 0 && index + 1 < area.Length && (area[index + 1] & (value >> (64 - bitShift))) != 0) return true;
                }

                return false;
            }

            /// <summary>Bloqueia as células da peça e as que ficam a até <paramref name="reach"/> células dela.</summary>
            public void Place(Mask mask, int column, int row, int reach)
            {
                for (var r = 0; r < mask.Rows; r++)
                {
                    for (var c = 0; c < mask.Columns; c++)
                    {
                        if ((mask.Bits[r][c >> 6] & (1UL << (c & 63))) == 0) continue;
                        for (var rr = Math.Max(0, row + r - reach); rr <= row + r + reach; rr++)
                        {
                            if (_rowLimit.HasValue && rr >= _rowLimit.Value) break;
                            while (_blocked.Count <= rr) _blocked.Add(new ulong[_words]);
                            var line = _blocked[rr];
                            for (var cc = Math.Max(0, column + c - reach); cc <= Math.Min(_columns - 1, column + c + reach); cc++)
                                line[cc >> 6] |= 1UL << (cc & 63);
                        }
                    }
                }
            }
        }
    }
}
