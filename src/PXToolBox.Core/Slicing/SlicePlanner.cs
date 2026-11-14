using System;
using System.Collections.Generic;
using System.Linq;

namespace PXToolBox.Core.Slicing
{
    /// <summary>Uma parte, em mm, medida a partir do canto superior esquerdo da área.</summary>
    public sealed class SliceTile
    {
        internal SliceTile(int row, int column, double x, double y, double width, double height)
        {
            Row = row;
            Column = column;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Row { get; }
        public int Column { get; }
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
    }

    public sealed class SlicePlan
    {
        internal SlicePlan(double width, double height, double overlap, int rows, int columns,
            List<SliceTile> tiles)
        {
            Width = width;
            Height = height;
            Overlap = overlap;
            Rows = rows;
            Columns = columns;
            Tiles = tiles.AsReadOnly();
        }

        public double Width { get; }
        public double Height { get; }
        public double Overlap { get; }
        public int Rows { get; }
        public int Columns { get; }
        public IReadOnlyList<SliceTile> Tiles { get; }
    }

    /// <summary>
    /// Planeja a divisão retangular da área, sem Corel e sem alterar documento. A sobreposição é a
    /// faixa TOTAL compartilhada em cada emenda. Por tabela, a faixa é dividida igualmente entre os
    /// dois lados de cada divisão. Por dimensão máxima, cada parte avança (máximo − sobreposição) e
    /// a última é cortada na borda da área. Medidas inválidas: ArgumentException com a mensagem
    /// para o usuário.
    /// </summary>
    public static class SlicePlanner
    {
        public const int MaximumTiles = 1000;

        public static SlicePlan ByTable(double width, double height, int rows, int columns, double overlap)
        {
            ValidateArtwork(width, height, overlap);
            ValidateCount(rows, columns);
            var cellWidth = width / columns;
            var cellHeight = height / rows;
            if ((columns > 1 && overlap >= cellWidth) || (rows > 1 && overlap >= cellHeight))
                throw new ArgumentException("A sobreposição deve ser menor que cada divisão nos eixos divididos.");

            var tiles = new List<SliceTile>(rows * columns);
            for (var row = 0; row < rows; row++)
            {
                var top = row == 0 ? 0 : row * cellHeight - overlap / 2;
                var bottom = row == rows - 1 ? height : (row + 1) * cellHeight + overlap / 2;
                for (var column = 0; column < columns; column++)
                {
                    var left = column == 0 ? 0 : column * cellWidth - overlap / 2;
                    var right = column == columns - 1 ? width : (column + 1) * cellWidth + overlap / 2;
                    tiles.Add(new SliceTile(row + 1, column + 1, left, top, right - left, bottom - top));
                }
            }
            return new SlicePlan(width, height, overlap, rows, columns, tiles);
        }

        public static SlicePlan ByMaximumSize(double width, double height, double maximumWidth,
            double maximumHeight, double overlap)
        {
            ValidateArtwork(width, height, overlap);
            ValidatePositive(maximumWidth, nameof(maximumWidth));
            ValidatePositive(maximumHeight, nameof(maximumHeight));
            if ((width > maximumWidth && overlap >= maximumWidth) ||
                (height > maximumHeight && overlap >= maximumHeight))
                throw new ArgumentException("A sobreposição deve ser menor que a dimensão máxima nos eixos divididos.");

            var columns = RequiredCount(width, maximumWidth, overlap);
            var rows = RequiredCount(height, maximumHeight, overlap);
            ValidateCount(rows, columns);
            var tiles = new List<SliceTile>(rows * columns);
            for (var row = 0; row < rows; row++)
            {
                var top = row * (maximumHeight - overlap);
                for (var column = 0; column < columns; column++)
                {
                    var left = column * (maximumWidth - overlap);
                    tiles.Add(new SliceTile(row + 1, column + 1, left, top,
                        Math.Min(maximumWidth, width - left), Math.Min(maximumHeight, height - top)));
                }
            }
            return new SlicePlan(width, height, overlap, rows, columns, tiles);
        }

        /// <summary>Cortes que não chegam a esta distância da borda ou de outro corte são ignorados.</summary>
        public const double MinimumCutGap = 0.5;

        /// <summary>
        /// Divide nas emendas dadas (mm a partir da esquerda e do topo da área, ex.: guias da página).
        /// Como na tabela, cada emenda leva metade da sobreposição para cada lado. Cortes fora da área,
        /// a menos de <see cref="MinimumCutGap"/> da borda ou repetidos são ignorados; sem corte num
        /// eixo, esse eixo não é dividido.
        /// </summary>
        public static SlicePlan ByCuts(double width, double height, IEnumerable<double> columnCuts,
            IEnumerable<double> rowCuts, double overlap)
        {
            ValidateArtwork(width, height, overlap);
            var xs = Edges(columnCuts, width);
            var ys = Edges(rowCuts, height);
            ValidateCount(ys.Count - 1, xs.Count - 1);
            if (Gaps(xs).Any(gap => xs.Count > 2 && overlap >= gap) || Gaps(ys).Any(gap => ys.Count > 2 && overlap >= gap))
                throw new ArgumentException("A sobreposição deve ser menor que cada divisão nos eixos divididos.");

            var tiles = new List<SliceTile>((xs.Count - 1) * (ys.Count - 1));
            for (var row = 0; row < ys.Count - 1; row++)
            {
                var top = row == 0 ? 0 : ys[row] - overlap / 2;
                var bottom = row == ys.Count - 2 ? height : ys[row + 1] + overlap / 2;
                for (var column = 0; column < xs.Count - 1; column++)
                {
                    var left = column == 0 ? 0 : xs[column] - overlap / 2;
                    var right = column == xs.Count - 2 ? width : xs[column + 1] + overlap / 2;
                    tiles.Add(new SliceTile(row + 1, column + 1, left, top, right - left, bottom - top));
                }
            }

            return new SlicePlan(width, height, overlap, ys.Count - 1, xs.Count - 1, tiles);
        }

        /// <summary>Meio de cada emenda do plano (onde ficaria a guia), em mm a partir da esquerda e do topo.</summary>
        public static (IReadOnlyList<double> Columns, IReadOnlyList<double> Rows) SeamCenters(SlicePlan plan)
        {
            var firstRow = plan.Tiles.Where(t => t.Row == 1).OrderBy(t => t.Column).ToList();
            var firstColumn = plan.Tiles.Where(t => t.Column == 1).OrderBy(t => t.Row).ToList();
            var columns = firstRow.Skip(1).Select((t, i) => (t.X + firstRow[i].X + firstRow[i].Width) / 2).ToList();
            var rows = firstColumn.Skip(1).Select((t, i) => (t.Y + firstColumn[i].Y + firstColumn[i].Height) / 2).ToList();
            return (columns, rows);
        }

        /// <summary>0, os cortes válidos em ordem e o comprimento.</summary>
        private static List<double> Edges(IEnumerable<double> cuts, double length)
        {
            var edges = new List<double> { 0 };
            foreach (var cut in (cuts ?? Enumerable.Empty<double>()).Where(c => !double.IsNaN(c) && !double.IsInfinity(c)).OrderBy(c => c))
                if (cut - edges[edges.Count - 1] >= MinimumCutGap && length - cut >= MinimumCutGap) edges.Add(cut);
            edges.Add(length);
            return edges;
        }

        private static IEnumerable<double> Gaps(List<double> edges) =>
            edges.Skip(1).Select((edge, i) => edge - edges[i]);

        private static int RequiredCount(double length, double maximum, double overlap)
        {
            if (length <= maximum) return 1;
            var count = Math.Ceiling((length - maximum) / (maximum - overlap)) + 1;
            if (double.IsInfinity(count) || count > MaximumTiles)
                throw new ArgumentException($"A prévia aceita até {MaximumTiles} partes. Aumente a dimensão máxima.");
            return (int)count;
        }

        private static void ValidateArtwork(double width, double height, double overlap)
        {
            ValidatePositive(width, nameof(width));
            ValidatePositive(height, nameof(height));
            if (double.IsNaN(overlap) || double.IsInfinity(overlap) || overlap < 0)
                throw new ArgumentException("A sobreposição deve ser um número finito maior ou igual a zero.");
        }

        private static void ValidatePositive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
                throw new ArgumentException("As dimensões devem ser números finitos maiores que zero.", name);
        }

        private static void ValidateCount(int rows, int columns)
        {
            if (rows < 1 || columns < 1 || (long)rows * columns > MaximumTiles)
                throw new ArgumentException($"Use pelo menos uma linha e coluna, com até {MaximumTiles} partes no total.");
        }
    }
}
