using System;
using System.Globalization;
using System.Text;

namespace PXToolBoxDev.Diagnostics
{
    /// <summary>
    /// Monta o relatório textual de "Copiar tudo"/"Salvar relatório..." — pura, sem COM, a
    /// partir de um <see cref="ShapePropertiesReport"/> já lido. Testável com um relatório
    /// fabricado (sem precisar de um Corel real).
    /// </summary>
    public static class ShapePropertiesReportFormatter
    {
        public static string Build(ShapePropertiesReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PX Shape.Properties Inspector");
            sb.AppendLine($"PX Tool Box: v{report.Diagnostics.ToolVersion}");
            if (!string.IsNullOrEmpty(report.Diagnostics.DocumentName))
            {
                sb.AppendLine($"Documento: {report.Diagnostics.DocumentName}");
            }
            if (!string.IsNullOrEmpty(report.Diagnostics.DocumentUnitLabel))
            {
                sb.AppendLine($"Unidade do documento (Document.Unit): {report.Diagnostics.DocumentUnitLabel}");
            }
            if (!string.IsNullOrEmpty(report.Diagnostics.ApplicationUnitLabel))
            {
                sb.AppendLine($"Unidade do aplicativo (Application.Unit): {report.Diagnostics.ApplicationUnitLabel}");
            }
            if (!string.IsNullOrEmpty(report.Diagnostics.RulersHUnitLabel))
            {
                sb.AppendLine($"Unidade da régua horizontal (Document.Rulers.HUnits): {report.Diagnostics.RulersHUnitLabel}");
            }
            if (!string.IsNullOrEmpty(report.Diagnostics.RulersVUnitLabel))
            {
                sb.AppendLine($"Unidade da régua vertical (Document.Rulers.VUnits): {report.Diagnostics.RulersVUnitLabel}");
            }
            sb.AppendLine();

            if (report.Object != null)
            {
                var o = report.Object;
                sb.AppendLine("Object");
                sb.AppendLine($"Name: {o.Name}");
                sb.AppendLine($"Type: {o.ShapeType}");
                sb.AppendLine($"Layer: {o.LayerName}");
                sb.AppendLine($"Page: {o.PageIndex}");
                sb.AppendLine($"StaticID: {o.StaticId}");
                sb.AppendLine($"Width: {o.Width.ToString(CultureInfo.InvariantCulture)}");
                sb.AppendLine($"Height: {o.Height.ToString(CultureInfo.InvariantCulture)}");
                sb.AppendLine();
            }

            sb.AppendLine("Properties");
            sb.AppendLine($"Count: {report.Properties.Count}");

            if (report.Properties.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("Nenhuma Shape.Properties encontrada neste objeto.");
                return sb.ToString();
            }

            foreach (var entry in report.Properties)
            {
                sb.AppendLine();
                sb.AppendLine($"[{entry.Index}]");
                sb.AppendLine($"Name: {entry.Name}");
                sb.AppendLine($"ID: {entry.Id}");
                if (entry.ReadError != null)
                {
                    sb.AppendLine($"Value: <erro ao ler: {entry.ReadError}>");
                }
                else
                {
                    sb.AppendLine($"Value: {entry.ValueDisplay}");
                    sb.AppendLine($"Type: {entry.ValueType}");
                }
            }

            return sb.ToString();
        }
    }
}
