namespace PXToolBox.Core.Measurement
{
    /// <summary>
    /// Subconjunto de cdrUnit (Corel.Interop.VGCore) reconhecido por Utilidades.SomaMedidasSelecionadas
    /// e SomaAlturasSelecionadasEmMetros. Unidades fora desta lista resultam em falha (igual à macro
    /// original, que exibia "Unidade não reconhecida").
    /// </summary>
    public enum DocumentUnit
    {
        Millimeter,
        Centimeter,
        Meter,
        Inch,
        Foot,
        Yard
    }

    public static class DocumentUnitConversion
    {
        /// <summary>Fator de conversão para metros, idêntico ao Select Case da macro original.</summary>
        public static double ToMetersFactor(DocumentUnit unit) => unit switch
        {
            DocumentUnit.Millimeter => 0.001,
            DocumentUnit.Centimeter => 0.01,
            DocumentUnit.Meter => 1.0,
            DocumentUnit.Inch => 0.0254,
            DocumentUnit.Foot => 0.3048,
            DocumentUnit.Yard => 0.9144,
            _ => throw new System.ArgumentOutOfRangeException(nameof(unit), unit, "Unidade sem fator de conversão definido.")
        };

        /// <summary>
        /// Converte um valor em milímetros para a unidade do documento — equivalente a
        /// Document.ToUnits(valor, cdrMillimeter), usado por Agrupamento.AgruparObjetosSobrepostos
        /// para expressar tolerâncias fixas em mm independentemente da unidade do documento.
        /// </summary>
        public static double FromMillimeters(double millimeters, DocumentUnit unit) =>
            (millimeters / 1000.0) / ToMetersFactor(unit);
    }
}
