using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PXToolBox.Core.Slicing
{
    /// <summary>Um ponto do plano livre, em mm a partir do canto superior esquerdo da arte (Y para baixo).</summary>
    public readonly struct SlicePoint
    {
        public SlicePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
    }

    /// <summary>Uma linha de corte (reta infinita) por dois pontos, em mm a partir do canto superior esquerdo da arte.</summary>
    public sealed class SliceLine
    {
        public SliceLine(double x1, double y1, double x2, double y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public double X1 { get; }
        public double Y1 { get; }
        public double X2 { get; }
        public double Y2 { get; }
    }

    /// <summary>Uma parte do plano livre: o polígono convexo dela já com a sobreposição (dentro da arte) e o
    /// centro da parte sem sobreposição.</summary>
    public sealed class SliceRegion
    {
        public SliceRegion(int number, IReadOnlyList<SlicePoint> points, double centerX, double centerY, double area)
        {
            Number = number;
            Points = points;
            CenterX = centerX;
            CenterY = centerY;
            Area = area;
        }

        public int Number { get; }
        public IReadOnlyList<SlicePoint> Points { get; }
        public double CenterX { get; }
        public double CenterY { get; }

        /// <summary>Área sem a sobreposição, em mm².</summary>
        public double Area { get; }

        public double MinX => Points.Min(p => p.X);
        public double MinY => Points.Min(p => p.Y);
        public double Width => Points.Max(p => p.X) - MinX;
        public double Height => Points.Max(p => p.Y) - MinY;
    }

    public sealed class SliceRegionPlan
    {
        public SliceRegionPlan(double width, double height, double overlap, IReadOnlyList<SliceLine> lines, int outside,
            IReadOnlyList<SliceRegion> regions, IReadOnlyList<IReadOnlyList<SlicePoint>>? paths = null)
        {
            Paths = paths ?? new IReadOnlyList<SlicePoint>[0];
            Width = width;
            Height = height;
            Overlap = overlap;
            Lines = lines;
            Outside = outside;
            Regions = regions;
        }

        public double Width { get; }
        public double Height { get; }
        public double Overlap { get; }

        /// <summary>As linhas que cruzam a arte (as usadas).</summary>
        public IReadOnlyList<SliceLine> Lines { get; }

        /// <summary>Linhas que não cruzam a arte (ignoradas); nas linhas desenhadas, as que não separaram nenhuma parte.</summary>
        public int Outside { get; }

        /// <summary>Linhas desenhadas (<see cref="SlicePathPlanner"/>), em mm da arte; vazio nas retas.</summary>
        public IReadOnlyList<IReadOnlyList<SlicePoint>> Paths { get; }

        public IReadOnlyList<SliceRegion> Regions { get; }
    }

    /// <summary>
    /// Divisão livre (S3 de docs/reference-feature-roadmap.md): cada linha de corte atravessa a arte inteira, em
    /// qualquer ângulo, e as partes são os polígonos convexos que as linhas formam dentro da caixa da arte.
    /// <list type="bullet">
    /// <item>Sobreposição: a faixa total compartilhada em cada emenda — cada parte avança metade dela além de cada
    /// linha que a limita (as bordas da arte não crescem), como na divisão por linhas e colunas.</item>
    /// <item>Partes numeradas de cima para baixo e, na mesma altura, da esquerda para a direita (pelo centro).</item>
    /// <item>Recusa partes finas demais (menos de <see cref="SlicePlanner.MinimumCutGap"/> de espessura, ex.: duas
    /// linhas quase juntas ou uma linha raspando um canto) e mais de <see cref="SlicePlanner.MaximumTiles"/> partes.</item>
    /// </list>
    /// </summary>
    public static class SliceRegionPlanner
    {
        private const double Epsilon = 1e-9;

        /// <summary>A linha atravessa o interior da arte (não só encosta numa borda ou num canto).</summary>
        public static bool Crosses(double width, double height, SliceLine line)
        {
            var dx = line.X2 - line.X1;
            var dy = line.Y2 - line.Y1;
            if (!(Math.Sqrt(dx * dx + dy * dy) > Epsilon)) return false;
            var sides = new[] { (0.0, 0.0), (width, 0.0), (width, height), (0.0, height) }
                .Select(p => -dy * (p.Item1 - line.X1) + dx * (p.Item2 - line.Y1)).ToList();
            return sides.Min() < -Epsilon && sides.Max() > Epsilon;
        }

        public static SliceRegionPlan ByLines(double width, double height, IEnumerable<SliceLine> lines, double overlap)
        {
            if (!(width > 0) || !(height > 0) || double.IsInfinity(width) || double.IsInfinity(height))
                throw new ArgumentException("Largura e altura da arte precisam ser maiores que zero.");
            if (!(overlap >= 0) || double.IsInfinity(overlap))
                throw new ArgumentException("A sobreposição precisa ser 0 ou mais.");

            var box = new List<SlicePoint> { new(0, 0), new(width, 0), new(width, height), new(0, height) };
            var used = new List<(SliceLine Line, double Nx, double Ny, double C)>();
            var outside = 0;
            foreach (var line in lines)
            {
                var dx = line.X2 - line.X1;
                var dy = line.Y2 - line.Y1;
                var length = Math.Sqrt(dx * dx + dy * dy);
                if (!(length > Epsilon)) throw new ArgumentException("Linha de corte sem direção (os dois pontos coincidem).");
                // Normal unitária: n · p = c na linha.
                var nx = -dy / length;
                var ny = dx / length;
                var c = nx * line.X1 + ny * line.Y1;
                var sides = box.Select(p => nx * p.X + ny * p.Y - c).ToList();
                if (sides.Min() < -Epsilon && sides.Max() > Epsilon) used.Add((line, nx, ny, c));
                else outside++;
            }

            if (used.Count == 0) throw new ArgumentException("Nenhuma linha de corte cruza a arte.");

            // Cada parte: o polígono e de que lado de cada linha ela fica (−1 ou +1).
            var parts = new List<(List<SlicePoint> Polygon, int[] Sides)> { (box, new int[used.Count]) };
            for (var i = 0; i < used.Count; i++)
            {
                var (_, nx, ny, c) = used[i];
                var next = new List<(List<SlicePoint>, int[])>();
                foreach (var (polygon, sides) in parts)
                {
                    foreach (var side in new[] { -1, 1 })
                    {
                        var piece = Clip(polygon, nx, ny, c, side);
                        if (Area(piece) <= Epsilon) continue;
                        var copy = (int[])sides.Clone();
                        copy[i] = side;
                        next.Add((piece, copy));
                    }
                }

                parts = next;
                if (parts.Count > SlicePlanner.MaximumTiles)
                    throw new ArgumentException($"As linhas formam mais de {SlicePlanner.MaximumTiles} partes.");
            }

            var regions = new List<(List<SlicePoint> Expanded, double Cx, double Cy, double Area)>();
            foreach (var (polygon, sides) in parts)
            {
                var area = Area(polygon);
                var perimeter = Perimeter(polygon);
                var (cx, cy) = Centroid(polygon, area);
                if (2 * area / perimeter < SlicePlanner.MinimumCutGap)
                    throw new ArgumentException(
                        $"Uma parte perto de X {Cm(cx)}; Y {Cm(cy)} cm ficaria fina demais: afaste as linhas de corte que se cruzam ou passam muito perto ali.");

                var expanded = box;
                for (var i = 0; i < used.Count; i++)
                {
                    var (_, nx, ny, c) = used[i];
                    // Lado −1: n·p ≤ c + o/2; lado +1: n·p ≥ c − o/2.
                    expanded = Clip(expanded, nx, ny, c + sides[i] * -overlap / 2, sides[i]);
                }

                regions.Add((expanded, cx, cy, area));
            }

            var numbered = regions
                .OrderBy(r => Math.Round(r.Cy, 1))
                .ThenBy(r => r.Cx)
                .Select((r, k) => new SliceRegion(k + 1, r.Expanded, r.Cx, r.Cy, r.Area))
                .ToList();
            return new SliceRegionPlan(width, height, overlap, used.Select(u => u.Line).ToList(), outside, numbered);
        }

        /// <summary>Parte do polígono convexo do lado pedido da linha n·p = c (−1: n·p ≤ c; +1: n·p ≥ c).</summary>
        private static List<SlicePoint> Clip(IReadOnlyList<SlicePoint> polygon, double nx, double ny, double c, int side)
        {
            var result = new List<SlicePoint>();
            double Inside(SlicePoint p) => side * (nx * p.X + ny * p.Y - c); // ≥ 0 = dentro
            for (var k = 0; k < polygon.Count; k++)
            {
                var a = polygon[k];
                var b = polygon[(k + 1) % polygon.Count];
                var da = Inside(a);
                var db = Inside(b);
                if (da >= 0) result.Add(a);
                if ((da >= 0) != (db >= 0))
                {
                    var t = da / (da - db);
                    result.Add(new SlicePoint(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y)));
                }
            }

            // Vértice exatamente na linha entra duas vezes: tira os repetidos.
            var unique = new List<SlicePoint>();
            foreach (var p in result)
                if (unique.Count == 0 || Math.Abs(unique[unique.Count - 1].X - p.X) > Epsilon || Math.Abs(unique[unique.Count - 1].Y - p.Y) > Epsilon)
                    unique.Add(p);
            if (unique.Count > 1 && Math.Abs(unique[0].X - unique[unique.Count - 1].X) <= Epsilon && Math.Abs(unique[0].Y - unique[unique.Count - 1].Y) <= Epsilon)
                unique.RemoveAt(unique.Count - 1);
            return unique;
        }

        internal static double Area(IReadOnlyList<SlicePoint> polygon)
        {
            if (polygon.Count < 3) return 0;
            var sum = 0.0;
            for (var k = 0; k < polygon.Count; k++)
            {
                var a = polygon[k];
                var b = polygon[(k + 1) % polygon.Count];
                sum += a.X * b.Y - b.X * a.Y;
            }

            return Math.Abs(sum) / 2;
        }

        internal static double Perimeter(IReadOnlyList<SlicePoint> polygon)
        {
            var sum = 0.0;
            for (var k = 0; k < polygon.Count; k++)
            {
                var a = polygon[k];
                var b = polygon[(k + 1) % polygon.Count];
                sum += Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            }

            return sum;
        }

        internal static (double X, double Y) Centroid(IReadOnlyList<SlicePoint> polygon, double area)
        {
            double x = 0, y = 0, signed = 0;
            for (var k = 0; k < polygon.Count; k++)
            {
                var a = polygon[k];
                var b = polygon[(k + 1) % polygon.Count];
                var cross = a.X * b.Y - b.X * a.Y;
                signed += cross;
                x += (a.X + b.X) * cross;
                y += (a.Y + b.Y) * cross;
            }

            return signed == 0 ? (polygon.Average(p => p.X), polygon.Average(p => p.Y)) : (x / (3 * signed), y / (3 * signed));
        }

        internal static string Cm(double millimeters) => (millimeters / 10).ToString("0.#", CultureInfo.GetCultureInfo("pt-BR"));
    }
}
