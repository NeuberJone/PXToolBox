namespace PXToolBox.Core.Measurement
{
    /// <summary>Resultado de uma soma de medidas, pronto para exibição e para cópia na área de transferência.</summary>
    public sealed class MeasurementSummary
    {
        public int ObjectCount { get; }
        public MeasurementSumMode Mode { get; }
        public double TotalHeightMeters { get; }
        public double TotalWidthMeters { get; }
        public string ClipboardText { get; }
        public string DisplayText { get; }

        public MeasurementSummary(
            int objectCount,
            MeasurementSumMode mode,
            double totalHeightMeters,
            double totalWidthMeters,
            string clipboardText,
            string displayText)
        {
            ObjectCount = objectCount;
            Mode = mode;
            TotalHeightMeters = totalHeightMeters;
            TotalWidthMeters = totalWidthMeters;
            ClipboardText = clipboardText;
            DisplayText = displayText;
        }
    }
}
