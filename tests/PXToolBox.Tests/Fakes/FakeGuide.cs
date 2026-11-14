using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Tests.Fakes
{
    public sealed class FakeGuide : ICorelGuide
    {
        public FakeGuide(bool horizontal, double position)
        {
            Horizontal = horizontal;
            Position = position;
            Points = horizontal ? (0, position, 100, position) : (position, 0, position, 100);
        }

        /// <summary>Guia inclinada pelos dois pontos (unidade do documento, Y para cima).</summary>
        public FakeGuide(double x1, double y1, double x2, double y2)
        {
            Slanted = true;
            Points = (x1, y1, x2, y2);
        }

        public bool Horizontal { get; }

        public bool Slanted { get; }

        public double Position { get; }

        public (double X1, double Y1, double X2, double Y2) Points { get; }

        public string Name { get; set; } = string.Empty;

        public FakePage? Owner { get; set; }

        public void Delete() => Owner?.GuidesList.Remove(this);
    }
}
