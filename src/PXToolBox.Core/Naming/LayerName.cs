using System;

namespace PXToolBox.Core.Naming
{
    /// <summary>
    /// Interpreta a convenção de nomes de camada usada pelas macros legadas (CorelMacros):
    /// segmentos separados por ponto, ex.: "CAM1.PP.X.MA".
    /// O primeiro segmento é o código do tipo de peça (ver <see cref="PieceTypeCodes"/>);
    /// o último segmento é a categoria MA/FE/C (ver <see cref="LayerCategory"/>).
    ///
    /// Importante (caso crítico do Grid): a comparação de sufixo é feita pelo último segmento
    /// pontuado inteiro, nunca por substring, para não casar acidentalmente "MA" dentro de
    /// outra palavra (ex.: uma camada chamada "Estampa" não deve ser tratada como ".MA").
    /// </summary>
    public readonly struct LayerName
    {
        public string Original { get; }
        public string[] Segments { get; }

        public LayerName(string original)
        {
            Original = original ?? string.Empty;
            Segments = Original.Split('.');
        }

        public string? FirstSegment => Segments.Length > 0 ? Segments[0] : null;

        public string? LastSegment => Segments.Length > 0 ? Segments[Segments.Length - 1] : null;

        public bool HasAtLeastSegments(int count) => Segments.Length >= count;

        /// <summary>
        /// Categoria MA/FE/C da camada, determinada pelo último segmento pontuado (comparação
        /// ordinal, sem distinção de maiúsculas/minúsculas, e exigindo que o nome tenha mais de
        /// um segmento — uma camada sem ponto no nome nunca é MA/FE/C por acidente).
        /// </summary>
        public LayerCategory Category
        {
            get
            {
                if (Segments.Length < 2)
                {
                    return LayerCategory.Unknown;
                }

                var last = Segments[Segments.Length - 1];
                if (string.Equals(last, "MA", StringComparison.OrdinalIgnoreCase)) return LayerCategory.MA;
                if (string.Equals(last, "FE", StringComparison.OrdinalIgnoreCase)) return LayerCategory.FE;
                if (string.Equals(last, "C", StringComparison.OrdinalIgnoreCase)) return LayerCategory.C;
                return LayerCategory.Unknown;
            }
        }

        public static string SuffixFor(LayerCategory category) => category switch
        {
            LayerCategory.MA => "MA",
            LayerCategory.FE => "FE",
            LayerCategory.C => "C",
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Categoria sem sufixo definido.")
        };

        /// <summary>
        /// Renomeia o tipo de peça (primeiro segmento) preservando os demais segmentos.
        /// Ported de Correcoes.RenomeiaTipoDePeca.
        /// </summary>
        public string WithFirstSegment(string newFirstSegment)
        {
            if (Segments.Length == 0)
            {
                return newFirstSegment;
            }

            var copy = (string[])Segments.Clone();
            copy[0] = newFirstSegment;
            return string.Join(".", copy);
        }

        public override string ToString() => Original;
    }
}
