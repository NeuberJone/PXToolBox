namespace PXToolBox.Core.Measurement
{
    /// <summary>Largura/altura de uma forma, nas unidades do documento (antes da conversão para metros).</summary>
    public readonly struct ShapeSize
    {
        public double Width { get; }
        public double Height { get; }

        public ShapeSize(double width, double height)
        {
            Width = width;
            Height = height;
        }
    }
}
