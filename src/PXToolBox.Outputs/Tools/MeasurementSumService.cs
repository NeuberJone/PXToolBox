using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PXToolBox.Core.Measurement;
using PXToolBox.Core.Result;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Somar medidas dos selecionados" — sempre em metros, qualquer modo (altura/largura/ambas).
    /// Ported de Utilidades.SomaMedidasSelecionadas (modo com escolha: altura/largura/ambas).
    ///
    /// A macro original também tinha um atalho separado, só-altura
    /// (Utilidades.SomaAlturasSelecionadasEmMetros), inicialmente portado como
    /// <c>SumHeightOnly</c>/botão "Somar Altura (m)". Removido: comparação linha a linha com a
    /// macro original (Referência/Macros/modulos/Medidas.txt) já tinha confirmado que os dois
    /// pontos de entrada usam o MESMO Select Case de unidades (mm/cm/m/polegada/pé/jarda) e
    /// sempre convertem para metros — ou seja, "Somar Altura (m)" e "Somar Altura" (este
    /// serviço, modo Height) sempre produziram exatamente o mesmo resultado; mantinha-se só como
    /// duplicata do botão "Somar Altura" (ver docs/migrated-tools.md).
    /// </summary>
    public sealed class MeasurementSumService
    {
        public OperationResult<MeasurementSummary> SumSelected(IReadOnlyList<ShapeSize> sizes, DocumentUnit? unit, MeasurementSumMode mode)
        {
            if (sizes.Count == 0)
            {
                return OperationResult<MeasurementSummary>.Fail(OperationStatus.InvalidSelection, "Nenhum objeto selecionado.");
            }

            if (unit == null)
            {
                return OperationResult<MeasurementSummary>.Fail(
                    OperationStatus.InconclusiveRead,
                    "Unidade não reconhecida. Por favor, use mm, cm ou m.");
            }

            var factor = DocumentUnitConversion.ToMetersFactor(unit.Value);
            var totalHeight = (mode == MeasurementSumMode.Height || mode == MeasurementSumMode.Both) ? sizes.Sum(s => s.Height * factor) : 0;
            var totalWidth = (mode == MeasurementSumMode.Width || mode == MeasurementSumMode.Both) ? sizes.Sum(s => s.Width * factor) : 0;

            var countLine = $"{sizes.Count} objeto(s) selecionado(s)";
            string displayText;
            string clipboardText;

            switch (mode)
            {
                case MeasurementSumMode.Height:
                    displayText = $"{countLine}\nAltura total: {FormatMetersWithWord(totalHeight)}";
                    clipboardText = FormatMeters(totalHeight);
                    break;
                case MeasurementSumMode.Width:
                    displayText = $"{countLine}\nLargura total: {FormatMetersWithWord(totalWidth)}";
                    clipboardText = FormatMeters(totalWidth);
                    break;
                default:
                    displayText = $"{countLine}\nAltura total: {FormatMetersWithWord(totalHeight)}\nLargura total: {FormatMetersWithWord(totalWidth)}";
                    // Ordem width x height preservada da macro original (textoCopiar).
                    clipboardText = $"{FormatDecimalComma(totalWidth)}x{FormatDecimalComma(totalHeight)}m";
                    break;
            }

            var summary = new MeasurementSummary(sizes.Count, mode, totalHeight, totalWidth, clipboardText, displayText);
            return OperationResult<MeasurementSummary>.Success(summary);
        }

        /// <summary>Formatação locale-independente, reproduzindo o separador decimal por vírgula observado em produção (pt-BR).</summary>
        private static string FormatDecimalComma(double meters) =>
            meters.ToString("0.00", CultureInfo.InvariantCulture).Replace(".", ",");

        private static string FormatMeters(double meters) => $"{FormatDecimalComma(meters)}m";

        private static string FormatMetersWithWord(double meters) => $"{FormatDecimalComma(meters)} metros";
    }
}
