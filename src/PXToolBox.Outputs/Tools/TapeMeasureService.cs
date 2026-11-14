using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// Medidas → "Régua": uma trena em tamanho real ao lado da seleção, com traço a cada milímetro,
    /// maior a cada 5 mm e o maior, com o número, a cada centímetro.
    /// <list type="bullet">
    /// <item>Mede a caixa da seleção inteira (todos os objetos juntos). Largura: embaixo, da borda
    /// esquerda para a direita. Altura: à esquerda, de baixo para cima. As duas começam no mesmo
    /// canto (inferior esquerdo).</item>
    /// <item>Cada régua: faixa branca com contorno, traços numa curva só (<see cref="ICorelLayer.CreateLines"/>),
    /// números e o total no fim — tudo num grupo "Régua largura"/"Régua altura" na camada
    /// <see cref="LayerName"/> da página ativa (criada se não existir), para esconder ou apagar sem
    /// mexer no desenho.</item>
    /// <item>Os objetos medidos não mudam. Medidas em mm convertidas para a unidade do documento pelo
    /// fator que o chamador passa (Application.ConvertUnits).</item>
    /// </list>
    /// </summary>
    public sealed class TapeMeasureService
    {
        public const string LayerName = "RÉGUA";

        public const string FontFamily = "Arial";

        public const double LabelSizePoints = 7;

        /// <summary>Distância padrão entre a seleção e a régua (o painel deixa mudar).</summary>
        public const double DefaultGapMm = 3;

        /// <summary>Largura da faixa (no sentido dos traços).</summary>
        public const double BandMm = 12;

        public const double MillimeterTickMm = 2;

        public const double HalfCentimeterTickMm = 3.5;

        public const double CentimeterTickMm = 5;

        [Flags]
        public enum Axis
        {
            Width = 1,
            Height = 2,
            Both = Width | Height,
        }

        /// <param name="millimeter">Quanto vale 1 mm na unidade do documento.</param>
        /// <param name="gapMm">Distância entre a seleção e a régua, em mm (0 = encostada).</param>
        public OperationResult Create(ICorelSelection selection, ICorelPage page, double millimeter, Axis axis, double gapMm = DefaultGapMm)
        {
            if (selection.Count == 0) return Refuse("Selecione o que vai ser medido.");
            if (gapMm < 0 || double.IsNaN(gapMm) || double.IsInfinity(gapMm)) return Refuse("A distância da régua precisa ser 0 ou mais (mm).");
            if (millimeter <= 0) return Refuse("Não foi possível converter milímetros para a unidade do documento.");

            var shapes = selection.Shapes;
            var left = shapes.Min(s => s.CenterX - s.Width / 2);
            var right = shapes.Max(s => s.CenterX + s.Width / 2);
            var bottom = shapes.Min(s => s.CenterY - s.Height / 2);
            var top = shapes.Max(s => s.CenterY + s.Height / 2);
            var widthMm = (right - left) / millimeter;
            var heightMm = (top - bottom) / millimeter;
            if (axis.HasFlag(Axis.Width) && widthMm < 1) return Refuse("A seleção tem menos de 1 mm de largura.");
            if (axis.HasFlag(Axis.Height) && heightMm < 1) return Refuse("A seleção tem menos de 1 mm de altura.");

            var layer = page.FindLayer(LayerName) ?? page.CreateLayer(LayerName);
            var parts = new List<string>();
            if (axis.HasFlag(Axis.Width))
            {
                Build(page, layer, millimeter, widthMm, gapMm, horizontal: true, originX: left, originY: bottom);
                parts.Add($"largura {Format(widthMm)}");
            }

            if (axis.HasFlag(Axis.Height))
            {
                Build(page, layer, millimeter, heightMm, gapMm, horizontal: false, originX: left, originY: bottom);
                parts.Add($"altura {Format(heightMm)}");
            }

            return OperationResult.Success($"Régua criada na camada {LayerName}, a {Number(gapMm)} mm da seleção: {string.Join(" e ", parts)}. Ctrl+Z desfaz.");
        }

        /// <summary>
        /// Monta uma régua em coordenadas "ao longo" (a partir do canto) e "para fora" (para longe da
        /// seleção): largura = para baixo, altura = para a esquerda.
        /// </summary>
        private static void Build(ICorelPage page, ICorelLayer layer, double millimeter, double lengthMm, double gapMm, bool horizontal, double originX, double originY)
        {
            // (ao longo, para fora) em mm → ponto no documento.
            (double X, double Y) At(double alongMm, double outMm) => horizontal
                ? (originX + alongMm * millimeter, originY - (gapMm + outMm) * millimeter)
                : (originX - (gapMm + outMm) * millimeter, originY + alongMm * millimeter);

            // Canto inferior esquerdo da faixa = o ponto do início mais longe da seleção.
            var (bandX, bandY) = At(0, BandMm);
            var band = horizontal
                ? layer.CreateRectangle(bandX, bandY, lengthMm * millimeter, BandMm * millimeter)
                : layer.CreateRectangle(bandX, bandY, BandMm * millimeter, lengthMm * millimeter);
            band.SetUniformFill(255, 255, 255);
            band.SetOutline(new ShapeOutline(0, 0, 0, 0.2));
            band.Name = "Faixa";

            var lines = new List<CurveLine>();
            void Line(double alongMm, double fromOutMm, double toOutMm)
            {
                var (x1, y1) = At(alongMm, fromOutMm);
                var (x2, y2) = At(alongMm, toOutMm);
                lines.Add(new CurveLine(x1, y1, x2, y2));
            }

            var whole = (int)Math.Floor(lengthMm + 1e-6);
            for (var mm = 0; mm <= whole; mm++)
            {
                Line(mm, 0, mm % 10 == 0 ? CentimeterTickMm : mm % 5 == 0 ? HalfCentimeterTickMm : MillimeterTickMm);
            }

            // Fim exato da medida, quando não cai num milímetro inteiro.
            if (lengthMm - whole > 0.05) Line(lengthMm, 0, CentimeterTickMm);

            var ticks = layer.CreateLines(lines);
            ticks.SetOutline(new ShapeOutline(0, 0, 0, 0.1));
            ticks.Name = "Traços";

            var members = new List<ICorelShape> { ticks };
            var step = LabelStepCm(lengthMm);
            for (var cm = step; cm * 10 <= lengthMm + 1e-6; cm += step)
            {
                members.Add(Label(layer, cm.ToString(CultureInfo.InvariantCulture), At(cm * 10, CentimeterTickMm + 2.5)));
            }

            // Total depois do fim da régua, no meio da faixa.
            var total = Label(layer, Format(lengthMm), At(lengthMm, BandMm / 2));
            var offset = (horizontal ? total.Width : total.Height) / 2 + 2 * millimeter;
            if (horizontal) total.PositionX += offset;
            else total.PositionY += offset;
            members.Add(total);
            members.Add(band);

            var group = page.Group(members);
            group.Name = horizontal ? "Régua largura" : "Régua altura";
        }

        private static ICorelShape Label(ICorelLayer layer, string text, (double X, double Y) center)
        {
            var label = layer.CreateArtisticText(text, FontFamily, LabelSizePoints);
            label.SetUniformFill(0, 0, 0);
            label.PositionX += center.X - label.CenterX;
            label.PositionY += center.Y - label.CenterY;
            return label;
        }

        /// <summary>Número a cada 1 cm; em réguas longas, a cada 5 ou 10 cm para não embolar.</summary>
        public static int LabelStepCm(double lengthMm) => lengthMm > 5000 ? 10 : lengthMm > 2000 ? 5 : 1;

        /// <summary>"52,3 cm (523 mm)".</summary>
        public static string Format(double lengthMm)
        {
            var culture = CultureInfo.GetCultureInfo("pt-BR");
            var rounded = Math.Round(lengthMm, 1);
            return $"{(rounded / 10).ToString("0.##", culture)} cm ({rounded.ToString("0.#", culture)} mm)";
        }

        private static string Number(double value) => value.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));

        private static OperationResult Refuse(string message) =>
            OperationResult.Fail(OperationStatus.InvalidSelection, message + " Nada foi alterado.");
    }
}
