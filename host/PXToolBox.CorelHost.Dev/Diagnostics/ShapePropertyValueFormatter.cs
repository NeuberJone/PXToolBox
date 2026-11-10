using System;
using System.Globalization;

namespace PXToolBoxDev.Diagnostics
{
    /// <summary>
    /// Classifica um valor de <c>Shape.Properties.ItemByIndex[i]</c> (um <c>object</c>/Variant
    /// COM) num rótulo de tipo legível e num texto seguro — pura, sem dependência de COM, para
    /// ser testável sem um Corel real. Nunca chama serializador/ToString arbitrário em objetos
    /// COM/estruturas desconhecidas — só identifica o tipo .NET e mostra um marcador seguro
    /// (pedido explícito da tarefa: "não chamar métodos destrutivos ou serializadores
    /// arbitrários").
    /// </summary>
    public static class ShapePropertyValueFormatter
    {
        public static (string ValueType, string Display) Format(object? value)
        {
            switch (value)
            {
                case null:
                    return ("(nulo)", "");
                case bool b:
                    return ("Boolean", b ? "True" : "False");
                case byte or sbyte or short or ushort or int or uint or long or ulong:
                    return ("Integer", Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
                case float or double or decimal:
                    return ("Double", Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
                case DateTime dt:
                    return ("Date", dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                case string s:
                    return ("String", s);
                default:
                    // Array de bytes, struct COM (Point/Vector/Curve), RCW de outro objeto COM,
                    // ou qualquer coisa não prevista — nunca serializado; só o tipo .NET real,
                    // visível para comparação (ex.: no experimento do SISBolt).
                    return ("COM", $"<tipo COM: {value.GetType().FullName}>");
            }
        }
    }
}
