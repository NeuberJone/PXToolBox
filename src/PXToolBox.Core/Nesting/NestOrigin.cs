using System;

namespace PXToolBox.Core.Nesting
{
    /// <summary>De que canto da área o encaixe começa. O padrão de sempre é o superior esquerdo.</summary>
    public enum NestOrigin
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    /// <summary>
    /// Origem do encaixe (N4 de docs/reference-feature-roadmap.md). Os encaixadores calculam sempre a partir
    /// do canto superior esquerdo; outro canto = o mesmo cálculo com as peças espelhadas, e o resultado
    /// espelhado de volta. Assim as peças continuam as mesmas (só giradas pelos ângulos permitidos), a
    /// distância e a margem continuam exatas, e a regra "mais em cima, depois mais à esquerda" vira "mais
    /// perto do canto escolhido".
    /// </summary>
    public static class NestOrigins
    {
        public static bool FromRight(NestOrigin origin) => origin == NestOrigin.TopRight || origin == NestOrigin.BottomRight;

        public static bool FromBottom(NestOrigin origin) => origin == NestOrigin.BottomLeft || origin == NestOrigin.BottomRight;

        public static string Name(NestOrigin origin) => origin switch
        {
            NestOrigin.TopRight => "canto superior direito",
            NestOrigin.BottomLeft => "canto inferior esquerdo",
            NestOrigin.BottomRight => "canto inferior direito",
            _ => "canto superior esquerdo",
        };

        /// <summary>O rolo não tem fundo: começa sempre no início (em cima).</summary>
        internal static void Check(NestOrigin origin, double? areaHeight)
        {
            if (!Enum.IsDefined(typeof(NestOrigin), origin)) throw new ArgumentException("Canto de início desconhecido.");
            if (areaHeight == null && FromBottom(origin))
                throw new ArgumentException("No rolo o encaixe começa no início do rolo: escolha um canto de cima.");
        }
    }
}
