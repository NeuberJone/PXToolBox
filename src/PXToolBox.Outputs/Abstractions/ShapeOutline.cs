namespace PXToolBox.Outputs.Abstractions
{
    public enum ShapeOutlineDash
    {
        Solid,
        Dashed,
        Dotted,
    }

    /// <summary>Contorno a aplicar num objeto (<see cref="ICorelShape.SetOutline"/>): cor RGB, espessura
    /// em mm (o host converte para a unidade do documento); contínuo e centralizado, salvo
    /// <see cref="Dash"/> e <see cref="Inside"/>.</summary>
    public sealed class ShapeOutline
    {
        public ShapeOutline(int red, int green, int blue, double widthMillimeters, bool roundCorners = false,
            bool behindFill = false, bool scaleWithShape = false, ShapeOutlineDash dash = ShapeOutlineDash.Solid, bool inside = false)
        {
            Dash = dash;
            Inside = inside;
            Red = red;
            Green = green;
            Blue = blue;
            WidthMillimeters = widthMillimeters;
            RoundCorners = roundCorners;
            BehindFill = behindFill;
            ScaleWithShape = scaleWithShape;
        }

        public int Red { get; }

        public int Green { get; }

        public int Blue { get; }

        public double WidthMillimeters { get; }

        /// <summary>Cantos e pontas arredondados; senão, cantos vivos.</summary>
        public bool RoundCorners { get; }

        public bool BehindFill { get; }

        public bool ScaleWithShape { get; }

        /// <summary>Tracejado/pontilhado: traço e vão em múltiplos da espessura (8/4 e 1/3, como o MoldMotor).</summary>
        public ShapeOutlineDash Dash { get; }

        /// <summary>Contorno por dentro da forma (não avança sobre o caminho de corte).</summary>
        public bool Inside { get; }
    }
}
