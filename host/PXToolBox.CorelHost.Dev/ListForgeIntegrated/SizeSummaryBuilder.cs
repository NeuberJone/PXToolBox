using System;
using System.Collections.Generic;
using System.Linq;
using ListForge.Core;
using ListForge.Models;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Resumo de tamanhos processados (ver docs/listforge-classic-parity.md, grupo "Revisão e
    /// resumos" — equivalente a RefreshSizeSummary() do standalone). Lógica pura, extraída de
    /// ClassicWorkspaceWindow para ser testável sem depender de uma Window/Dispatcher reais —
    /// mesmo padrão já usado por OutputFreshnessTracker/TextLineLocator nesta mesma entrega.
    /// </summary>
    public static class SizeSummaryBuilder
    {
        public static string Build(IReadOnlyList<ParsedRow> rows, SizeConfig sizeConfig)
        {
            var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                foreach (var token in row.Tams)
                {
                    var (qty, size) = SizeHelper.ParseQtyAndSize(token, sizeConfig);
                    totals[size] = totals.TryGetValue(size, out var current) ? current + qty : qty;
                }
            }

            if (totals.Count == 0) return "";

            var parts = totals
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => $"{entry.Key}: {entry.Value}");
            return "Resumo de tamanhos: " + string.Join("  ·  ", parts);
        }
    }
}
