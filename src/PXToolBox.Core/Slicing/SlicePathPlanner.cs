using System;
using System.Collections.Generic;
using System.Linq;

namespace PXToolBox.Core.Slicing
{
    /// <summary>
    /// Divisão por linhas desenhadas (S3 de docs/reference-feature-roadmap.md: cortes que param no meio da arte e
    /// linhas curvas). Cada linha é uma polilinha (as curvas já em retas), em mm a partir do canto superior esquerdo
    /// da arte, Y para baixo.
    /// <list type="bullet">
    /// <item>Uma linha separa uma parte quando vai de uma borda dela a outra — da borda da arte ou de outro corte.
    /// Pode parar no meio da arte encostando noutro corte (um "T"). Cada ponta é estendida
    /// <see cref="JoinToleranceMm"/>, para um traço feito à mão que para um pouco antes do outro corte ainda
    /// encostar nele; o que sobra além do encontro não corta nada.</item>
    /// <item>Linha que não separa nada (solta no meio da arte, fora dela ou um laço fechado por dentro) é contada em
    /// <see cref="SliceRegionPlan.Outside"/>.</item>
    /// <item>Sobreposição: como nas retas, cada parte avança metade da faixa além de cada linha que a limita (as
    /// bordas da arte não crescem). Calculada deslocando cada linha para longe da parte e refazendo a divisão.</item>
    /// <item>Partes não convexas; numeração, recusa de parte fina e limite de partes iguais aos de
    /// <see cref="SliceRegionPlanner"/>.</item>
    /// </list>
    /// </summary>
    public static class SlicePathPlanner
    {
        /// <summary>Quanto cada ponta de linha é estendida para encostar noutro corte ou na borda.</summary>
        public const double JoinToleranceMm = 2;

        private const double Epsilon = 1e-9;
        private const double MinimumArea = 1e-4;

        public static SliceRegionPlan ByPaths(double width, double height, IEnumerable<IReadOnlyList<SlicePoint>> paths, double overlap)
        {
            if (!(width > 0) || !(height > 0) || double.IsInfinity(width) || double.IsInfinity(height))
                throw new ArgumentException("Largura e altura da arte precisam ser maiores que zero.");
            if (!(overlap >= 0) || double.IsInfinity(overlap))
                throw new ArgumentException("A sobreposição precisa ser 0 ou mais.");

            var box = new List<SlicePoint> { new(0, 0), new(width, 0), new(width, height), new(0, height) };
            var cuts = paths.Select(Clean).Where(path => path.Count >= 2).ToList();
            if (cuts.Count == 0) throw new ArgumentException("Nenhuma linha de corte desenhada.");

            var used = new bool[cuts.Count];
            var faces = Arrange(box, cuts.Select(cut => Extend(cut, JoinToleranceMm)).ToList(), used);
            if (faces.Count < 2)
                throw new ArgumentException("Nenhuma linha desenhada divide a arte: cada corte precisa ir de uma borda da arte (ou de outro corte) até outra borda ou corte.");

            var regions = new List<(IReadOnlyList<SlicePoint> Expanded, double Cx, double Cy, double Area)>();
            foreach (var face in faces)
            {
                var area = SliceRegionPlanner.Area(face);
                var (cx, cy) = SliceRegionPlanner.Centroid(face, area);
                if (2 * area / SliceRegionPlanner.Perimeter(face) < SlicePlanner.MinimumCutGap)
                    throw new ArgumentException(
                        $"Uma parte perto de X {SliceRegionPlanner.Cm(cx)}; Y {SliceRegionPlanner.Cm(cy)} cm ficaria fina demais: afaste as linhas de corte que se cruzam ou passam muito perto ali.");
                regions.Add((overlap > 0 ? Expanded(box, cuts, face, overlap / 2) : face, cx, cy, area));
            }

            var numbered = regions
                .OrderBy(r => Math.Round(r.Cy, 1))
                .ThenBy(r => r.Cx)
                .Select((r, k) => new SliceRegion(k + 1, r.Expanded, r.Cx, r.Cy, r.Area))
                .ToList();
            return new SliceRegionPlan(width, height, overlap, new SliceLine[0], used.Count(u => !u), numbered, cuts);
        }

        /// <summary>A parte com a sobreposição: as linhas deslocadas <paramref name="half"/> para longe dela e estendidas,
        /// e a divisão refeita só com as linhas perto dela; fica a parte que contém um ponto de dentro da original.</summary>
        private static IReadOnlyList<SlicePoint> Expanded(IReadOnlyList<SlicePoint> box, IReadOnlyList<IReadOnlyList<SlicePoint>> cuts,
            IReadOnlyList<SlicePoint> face, double half)
        {
            var seed = InteriorPoint(face);
            var reach = 3 * half + JoinToleranceMm + 1;
            var (minX, minY, maxX, maxY) = Bounds(face);
            var near = cuts
                .Where(cut =>
                {
                    var (cx0, cy0, cx1, cy1) = Bounds(cut);
                    return cx1 >= minX - reach && cx0 <= maxX + reach && cy1 >= minY - reach && cy0 <= maxY + reach;
                })
                .Select(cut => Extend(Offset(cut, -Side(cut, seed) * half), JoinToleranceMm + 2 * half))
                .ToList();
            var faces = Arrange(box, near, null);
            return faces.FirstOrDefault(f => Inside(f, seed)) ?? face;
        }

        /// <summary>Divide a caixa pelas linhas até nenhuma separar mais nada.</summary>
        private static List<IReadOnlyList<SlicePoint>> Arrange(IReadOnlyList<SlicePoint> box, IReadOnlyList<IReadOnlyList<SlicePoint>> cuts, bool[]? used)
        {
            var faces = new List<(IReadOnlyList<SlicePoint> Polygon, HashSet<int> Tried)> { (box, new HashSet<int>()) };
            var bounds = cuts.Select(Bounds).ToList();
            bool changed;
            do
            {
                changed = false;
                for (var c = 0; c < cuts.Count; c++)
                {
                    for (var f = 0; f < faces.Count; f++)
                    {
                        var (polygon, tried) = faces[f];
                        if (!tried.Add(c) || !Overlaps(bounds[c], Bounds(polygon))) continue;
                        if (!TrySplit(polygon, cuts[c], out var first, out var second)) continue;
                        faces[f] = (first, new HashSet<int>());
                        faces.Add((second, new HashSet<int>()));
                        if (used != null) used[c] = true;
                        changed = true;
                        if (faces.Count > SlicePlanner.MaximumTiles)
                            throw new ArgumentException($"As linhas formam mais de {SlicePlanner.MaximumTiles} partes.");
                        f--; // a mesma linha pode cortar de novo a primeira metade
                    }
                }
            }
            while (changed);

            return faces.Select(f => f.Polygon).ToList();
        }

        /// <summary>Um ponto onde a linha cruza a borda do polígono: posição na linha (segmento + fração) e na borda.</summary>
        private readonly struct Hit
        {
            public Hit(double along, int edge, double at, SlicePoint point)
            {
                Along = along;
                Edge = edge;
                At = at;
                Point = point;
            }

            public double Along { get; }
            public int Edge { get; }
            public double At { get; }
            public SlicePoint Point { get; }
        }

        /// <summary>Corta o polígono no primeiro trecho da linha que vai de uma borda a outra por dentro dele.</summary>
        private static bool TrySplit(IReadOnlyList<SlicePoint> polygon, IReadOnlyList<SlicePoint> cut,
            out IReadOnlyList<SlicePoint> first, out IReadOnlyList<SlicePoint> second)
        {
            first = second = polygon;
            var n = polygon.Count;
            var hits = new List<Hit>();
            for (var k = 0; k + 1 < cut.Count; k++)
            {
                var p = cut[k];
                var q = cut[k + 1];
                for (var e = 0; e < n; e++)
                {
                    var u = polygon[e];
                    var v = polygon[(e + 1) % n];
                    var rx = q.X - p.X;
                    var ry = q.Y - p.Y;
                    var sx = v.X - u.X;
                    var sy = v.Y - u.Y;
                    var denominator = rx * sy - ry * sx;
                    if (Math.Abs(denominator) < Epsilon * Math.Max(1, Math.Sqrt((rx * rx + ry * ry) * (sx * sx + sy * sy)))) continue;
                    var t = ((u.X - p.X) * sy - (u.Y - p.Y) * sx) / denominator;
                    var s = ((u.X - p.X) * ry - (u.Y - p.Y) * rx) / denominator;
                    if (t < -Epsilon || t > 1 + Epsilon || s < -Epsilon || s > 1 + Epsilon) continue;
                    t = Math.Max(0, Math.Min(1, t));
                    s = Math.Max(0, Math.Min(1, s));
                    var edge = e;
                    if (s > 1 - 1e-9)
                    {
                        edge = (e + 1) % n;
                        s = 0;
                    }

                    hits.Add(new Hit(k + t, edge, s, new SlicePoint(p.X + t * rx, p.Y + t * ry)));
                }
            }

            hits = hits.OrderBy(h => h.Along).ToList();
            var unique = new List<Hit>();
            foreach (var hit in hits)
                if (unique.Count == 0 || Distance(unique[unique.Count - 1].Point, hit.Point) > 1e-7)
                    unique.Add(hit);

            for (var k = 0; k + 1 < unique.Count; k++)
            {
                var a = unique[k];
                var b = unique[k + 1];
                var middle = At(cut, (a.Along + b.Along) / 2);
                if (!Inside(polygon, middle) || DistanceToBoundary(polygon, middle) < 1e-6) continue;
                var chord = new List<SlicePoint>();
                for (var j = (int)Math.Floor(a.Along) + 1; j < b.Along && j < cut.Count; j++)
                    if (j > a.Along) chord.Add(cut[j]);
                if (Split(polygon, a, b, chord, out first, out second)) return true;
            }

            return false;
        }

        /// <summary>Os dois lados da corda de <paramref name="a"/> a <paramref name="b"/> (pontos de dentro em
        /// <paramref name="chord"/>).</summary>
        private static bool Split(IReadOnlyList<SlicePoint> polygon, Hit a, Hit b, IReadOnlyList<SlicePoint> chord,
            out IReadOnlyList<SlicePoint> first, out IReadOnlyList<SlicePoint> second)
        {
            first = second = polygon;
            // A borda com A e B inseridos (ou marcados, quando caem num vértice).
            var ring = new List<SlicePoint>();
            int indexA = -1, indexB = -1;
            for (var e = 0; e < polygon.Count; e++)
            {
                var onVertex = new List<(Hit Hit, bool IsA)>();
                var onEdge = new List<(Hit Hit, bool IsA)>();
                foreach (var (hit, isA) in new[] { (a, true), (b, false) })
                {
                    if (hit.Edge != e) continue;
                    (hit.At < 1e-9 ? onVertex : onEdge).Add((hit, isA));
                }

                ring.Add(polygon[e]);
                foreach (var (_, isA) in onVertex)
                {
                    if (isA) indexA = ring.Count - 1;
                    else indexB = ring.Count - 1;
                }

                foreach (var (hit, isA) in onEdge.OrderBy(h => h.Hit.At))
                {
                    ring.Add(hit.Point);
                    if (isA) indexA = ring.Count - 1;
                    else indexB = ring.Count - 1;
                }
            }

            if (indexA < 0 || indexB < 0 || indexA == indexB) return false;

            var one = new List<SlicePoint>();
            for (var i = indexA; ; i = (i + 1) % ring.Count)
            {
                one.Add(ring[i]);
                if (i == indexB) break;
            }

            one.AddRange(chord.Reverse());

            var other = new List<SlicePoint>();
            for (var i = indexB; ; i = (i + 1) % ring.Count)
            {
                other.Add(ring[i]);
                if (i == indexA) break;
            }

            other.AddRange(chord);

            var cleanOne = ClosedClean(one);
            var cleanOther = ClosedClean(other);
            if (SliceRegionPlanner.Area(cleanOne) < MinimumArea || SliceRegionPlanner.Area(cleanOther) < MinimumArea) return false;
            first = cleanOne;
            second = cleanOther;
            return true;
        }

        /// <summary>Sem pontos repetidos seguidos.</summary>
        private static IReadOnlyList<SlicePoint> Clean(IReadOnlyList<SlicePoint> points)
        {
            var result = new List<SlicePoint>();
            foreach (var p in points)
                if (result.Count == 0 || Distance(result[result.Count - 1], p) > 1e-9)
                    result.Add(p);
            return result;
        }

        /// <summary>Polígono: sem repetidos seguidos nem o último igual ao primeiro.</summary>
        private static IReadOnlyList<SlicePoint> ClosedClean(IReadOnlyList<SlicePoint> points)
        {
            var result = Clean(points).ToList();
            while (result.Count > 1 && Distance(result[0], result[result.Count - 1]) <= 1e-9) result.RemoveAt(result.Count - 1);
            return result;
        }

        /// <summary>Cada ponta estendida <paramref name="length"/> na direção do primeiro/último trecho.</summary>
        private static IReadOnlyList<SlicePoint> Extend(IReadOnlyList<SlicePoint> path, double length)
        {
            var result = path.ToList();
            SlicePoint Away(SlicePoint end, SlicePoint previous)
            {
                var d = Distance(end, previous);
                return d < Epsilon ? end : new SlicePoint(end.X + (end.X - previous.X) / d * length, end.Y + (end.Y - previous.Y) / d * length);
            }

            result[0] = Away(path[0], path[1]);
            result[result.Count - 1] = Away(path[path.Count - 1], path[path.Count - 2]);
            return result;
        }

        /// <summary>A linha deslocada <paramref name="distance"/> para a esquerda (normal (−dy, dx); negativo = direita),
        /// com as quinas em esquadria (limitada).</summary>
        private static IReadOnlyList<SlicePoint> Offset(IReadOnlyList<SlicePoint> path, double distance)
        {
            (double X, double Y) Normal(int segment)
            {
                var a = path[segment];
                var b = path[segment + 1];
                var d = Distance(a, b);
                return (-(b.Y - a.Y) / d, (b.X - a.X) / d);
            }

            var result = new List<SlicePoint>();
            for (var i = 0; i < path.Count; i++)
            {
                var before = i > 0 ? Normal(i - 1) : Normal(i);
                var after = i + 1 < path.Count ? Normal(i) : Normal(i - 1);
                var mx = before.X + after.X;
                var my = before.Y + after.Y;
                var length = Math.Sqrt(mx * mx + my * my);
                if (length < Epsilon)
                {
                    mx = after.X;
                    my = after.Y;
                    length = 1;
                }

                mx /= length;
                my /= length;
                var scale = distance / Math.Max(0.25, mx * after.X + my * after.Y);
                result.Add(new SlicePoint(path[i].X + mx * scale, path[i].Y + my * scale));
            }

            return result;
        }

        /// <summary>+1 se o ponto fica à esquerda da linha (pelo trecho mais perto), −1 à direita.</summary>
        private static int Side(IReadOnlyList<SlicePoint> path, SlicePoint point)
        {
            var best = double.MaxValue;
            var side = 1;
            for (var k = 0; k + 1 < path.Count; k++)
            {
                var a = path[k];
                var b = path[k + 1];
                var (closest, distance) = Closest(a, b, point);
                if (distance >= best) continue;
                best = distance;
                var cross = (b.X - a.X) * (point.Y - closest.Y) - (b.Y - a.Y) * (point.X - closest.X);
                side = cross >= 0 ? 1 : -1;
            }

            return side;
        }

        /// <summary>Um ponto bem dentro do polígono: o meio do trecho mais largo de uma linha horizontal.</summary>
        private static SlicePoint InteriorPoint(IReadOnlyList<SlicePoint> polygon)
        {
            var (_, minY, _, maxY) = Bounds(polygon);
            var best = (Width: -1.0, Point: new SlicePoint(polygon.Average(p => p.X), polygon.Average(p => p.Y)));
            foreach (var fraction in new[] { 0.5, 0.31, 0.69, 0.17, 0.83 })
            {
                var y = minY + (maxY - minY) * fraction + 1e-7;
                var xs = new List<double>();
                for (var k = 0; k < polygon.Count; k++)
                {
                    var a = polygon[k];
                    var b = polygon[(k + 1) % polygon.Count];
                    if ((a.Y > y) != (b.Y > y)) xs.Add(a.X + (y - a.Y) / (b.Y - a.Y) * (b.X - a.X));
                }

                xs.Sort();
                for (var k = 0; k + 1 < xs.Count; k += 2)
                    if (xs[k + 1] - xs[k] > best.Width)
                        best = (xs[k + 1] - xs[k], new SlicePoint((xs[k] + xs[k + 1]) / 2, y));
            }

            return best.Point;
        }

        /// <summary>Par/ímpar.</summary>
        private static bool Inside(IReadOnlyList<SlicePoint> polygon, SlicePoint point)
        {
            var inside = false;
            for (int k = 0, j = polygon.Count - 1; k < polygon.Count; j = k++)
            {
                var a = polygon[k];
                var b = polygon[j];
                if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }

            return inside;
        }

        private static double DistanceToBoundary(IReadOnlyList<SlicePoint> polygon, SlicePoint point)
        {
            var best = double.MaxValue;
            for (var k = 0; k < polygon.Count; k++)
                best = Math.Min(best, Closest(polygon[k], polygon[(k + 1) % polygon.Count], point).Distance);
            return best;
        }

        private static (SlicePoint Point, double Distance) Closest(SlicePoint a, SlicePoint b, SlicePoint point)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var squared = dx * dx + dy * dy;
            var t = squared < Epsilon ? 0 : Math.Max(0, Math.Min(1, ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / squared));
            var closest = new SlicePoint(a.X + t * dx, a.Y + t * dy);
            return (closest, Distance(closest, point));
        }

        /// <summary>Ponto da linha na posição segmento + fração.</summary>
        private static SlicePoint At(IReadOnlyList<SlicePoint> path, double along)
        {
            var k = Math.Min((int)Math.Floor(along), path.Count - 2);
            var t = along - k;
            return new SlicePoint(path[k].X + t * (path[k + 1].X - path[k].X), path[k].Y + t * (path[k + 1].Y - path[k].Y));
        }

        private static (double MinX, double MinY, double MaxX, double MaxY) Bounds(IReadOnlyList<SlicePoint> points) =>
            (points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));

        private static bool Overlaps((double MinX, double MinY, double MaxX, double MaxY) a, (double MinX, double MinY, double MaxX, double MaxY) b) =>
            a.MaxX >= b.MinX - Epsilon && a.MinX <= b.MaxX + Epsilon && a.MaxY >= b.MinY - Epsilon && a.MinY <= b.MaxY + Epsilon;

        private static double Distance(SlicePoint a, SlicePoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}
