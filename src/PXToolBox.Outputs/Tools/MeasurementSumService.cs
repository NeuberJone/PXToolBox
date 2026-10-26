using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PXToolBox.Core.Measurement;
using PXToolBox.Core.Result;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Somar medidas dos selecionados: somente altura e modo com escolha".
    /// Ported de Utilidades.SomaAlturasSelecionadasEmMetros (atalho só-altura) e
    /// Utilidades.SomaMedidasSelecionadas (modo com escolha: altura/largura/ambas).
    ///
    /// Os dois pontos de entrada da macro original (Referência/Macros/modulos/Medidas.txt)
    /// usam o MESMO Select Case de unidades (mm/cm/m/polegada/pé/jarda) — CORRIGIDO aqui: uma
    /// revisão anterior desta migração assumiu, incorretamente, que o atalho só-altura aceitava
    /// só mm/cm/m; comparação linha a linha com a macro original confirma que não há essa
    /// diferença entre os dois pontos de entrada (ver docs/migrated-tools.md).
    /// </summary>
    public sealed class MeasurementSumService
    {
        public OperationResult<MeasurementSummary> SumHeightOnly(IReadOnlyList<ShapeSize> sizes, DocumentUnit? unit)
        {
            if (sizes.Count == 0)
            {
                return OperationResult<MeasurementSummary>.Fail(OperationStatus.InvalidSelection, "Nenhum objeto selecionado.");
            }

            if (unit == null)
            {
                return OperationResult<MeasurementSummary>.Fail(
                    OperationStatus.InconclusiveRead,
                    "Unidade não reconhecida. Por favor, use mm, cm, m, polegada, pé ou jarda.");
            }

            var factor = DocumentUnitConversion.ToMetersFactor(unit.Value);
            var totalHeight = sizes.Sum(s => s.Height * factor);
            var clipboardText = FormatMeters(totalHeight);
            var displayText = $"Altura total: {clipboardText}";

            var summary = new MeasurementSummary(sizes.Count, MeasurementSumMode.Height, totalHeight, 0, clipboardText, displayText);
            return OperationResult<MeasurementSummary>.Success(summary);
        }

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
