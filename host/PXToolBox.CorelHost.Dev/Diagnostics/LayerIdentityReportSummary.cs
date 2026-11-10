using System;
using System.Linq;

namespace PXToolBoxDev.Diagnostics
{
    /// <summary>
    /// Resumo curto do relatório de <see cref="LayerIdentityDiagnostics"/> para o painel — só
    /// LÊ os marcadores que o relatório já escreve ([FALHOU], [ERRO], [OBS] e a linha final
    /// "Resultado:"); não muda nada do diagnóstico. O relatório completo continua disponível
    /// em "Ver relatório"/"Copiar relatório".
    /// </summary>
    public static class LayerIdentityReportSummary
    {
        public static string Summarize(string report)
        {
            var lines = (report ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (!lines.Any(l => l.StartsWith("Resultado:", StringComparison.Ordinal)))
            {
                return "Diagnóstico concluído sem resultado final identificado — veja o relatório.";
            }

            var failures = lines.Count(l => l.Contains("[FALHOU]") || l.Contains("[ERRO]"));
            var observations = lines.Count(l => l.Contains("[OBS]"));
            if (failures > 0)
            {
                return $"Diagnóstico concluído: {Count(failures, "falha", "falhas")} — veja o relatório.";
            }

            return observations > 0
                ? $"Diagnóstico concluído: nenhuma falha ({Count(observations, "observação", "observações")} no relatório)."
                : "Diagnóstico concluído: nenhuma falha.";
        }

        private static string Count(int n, string singular, string plural) => $"{n} {(n == 1 ? singular : plural)}";
    }
}
