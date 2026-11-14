namespace PXToolBox.Outputs.Abstractions
{
    /// <summary>Uma guia da página (Shape do tipo guia): horizontal, vertical ou inclinada.</summary>
    public interface ICorelGuide
    {
        /// <summary>True = horizontal (posição é Y); false = vertical ou inclinada.</summary>
        bool Horizontal { get; }

        /// <summary>Inclinada (Guide.Type = cdrSlantedGuide): use <see cref="Points"/>; <see cref="Position"/> não vale.</summary>
        bool Slanted { get; }

        /// <summary>Na unidade do documento (horizontal: Y; vertical: X).</summary>
        double Position { get; }

        /// <summary>Dois pontos da guia (Guide.Point1X/Y, Point2X/Y), na unidade do documento, Y para cima.</summary>
        (double X1, double Y1, double X2, double Y2) Points { get; }

        string Name { get; set; }

        void Delete();
    }
}
