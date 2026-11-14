namespace PXToolBox.Outputs.Abstractions
{
    public enum OutlineElementKind
    {
        /// <summary>Começo de um trecho (subcaminho) novo.</summary>
        Start,

        /// <summary>Reta do ponto anterior até este.</summary>
        Line,

        /// <summary>Ponto de controle da curva seguinte (vêm dois antes de cada <see cref="Curve"/>).</summary>
        Control,

        /// <summary>Fim de uma curva de Bézier que começa no ponto anterior.</summary>
        Curve,
    }

    /// <summary>Um elemento do contorno como o Corel desenha o objeto (Shape.DisplayCurve.GetCurveInfo),
    /// em coordenadas do documento.</summary>
    public readonly struct OutlineElement
    {
        public OutlineElement(OutlineElementKind kind, double x, double y)
        {
            Kind = kind;
            X = x;
            Y = y;
        }

        public OutlineElementKind Kind { get; }
        public double X { get; }
        public double Y { get; }
    }
}
