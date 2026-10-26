using System;
using System.Collections.Generic;
using System.Linq;

namespace PXToolBox.Core.Naming
{
    /// <summary>
    /// Códigos de tipo de peça reconhecidos por Correcoes.RenomeiaTipoDePeca (CorelMacros).
    /// A ordem reflete a ordem das opções exibidas ao usuário na macro original.
    /// </summary>
    public sealed class PieceTypeCode
    {
        public static readonly PieceTypeCode Cam1 = new PieceTypeCode("CAM1", "Camisa Manga Curta");
        public static readonly PieceTypeCode Cam2 = new PieceTypeCode("CAM2", "Camisa Manga Longa");
        public static readonly PieceTypeCode Reg1 = new PieceTypeCode("REG1", "Regata");
        public static readonly PieceTypeCode Col1 = new PieceTypeCode("COL1", "Colete");
        public static readonly PieceTypeCode Cal1 = new PieceTypeCode("CAL1", "Calça");
        public static readonly PieceTypeCode Sho1 = new PieceTypeCode("SHO1", "Short");

        public string Code { get; }
        public string DisplayName { get; }

        private PieceTypeCode(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }

        /// <summary>Lista na ordem original da macro (usada para montar os menus 1-6).</summary>
        public static readonly IReadOnlyList<PieceTypeCode> All = new[] { Cam1, Cam2, Reg1, Col1, Cal1, Sho1 };

        public static bool TryParse(string code, out PieceTypeCode? result)
        {
            result = All.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
            return result != null;
        }

        public override string ToString() => Code;
    }
}
