using Corel.Interop.VGCore;
using PXToolBox.Core.Measurement;

namespace PXToolBoxDev.Corel
{
    /// <summary>Tradução de cdrUnit (VGCore) para DocumentUnit (PXToolBox.Core) — usada por
    /// Document.Unit (CorelDocumentAdapter), só pra exibição/diagnóstico (o inspetor mostra a
    /// unidade configurada no arquivo). A conversão real de Tamanho Padrão usa
    /// <see cref="PXToolBox.Outputs.Abstractions.ICorelDocument.ConvertFromInches"/>
    /// (Document.ToUnits nativo) — nem Document.Unit nem Application.Unit bateram com o
    /// comportamento real do SISBolt (confirmado por evidência real, duas tentativas
    /// descartadas).</summary>
    internal static class CorelUnitMapping
    {
        public static DocumentUnit? Map(cdrUnit unit) => unit switch
        {
            cdrUnit.cdrMillimeter => DocumentUnit.Millimeter,
            cdrUnit.cdrCentimeter => DocumentUnit.Centimeter,
            cdrUnit.cdrMeter => DocumentUnit.Meter,
            cdrUnit.cdrInch => DocumentUnit.Inch,
            cdrUnit.cdrFoot => DocumentUnit.Foot,
            cdrUnit.cdrYard => DocumentUnit.Yard,
            _ => null
        };
    }
}
