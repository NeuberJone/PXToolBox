using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Result;
using PXToolBox.Core.Slicing;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>Cortes lidos das guias, em mm a partir da esquerda e do topo da arte selecionada.</summary>
    public sealed class SliceGuideCuts
    {
        public SliceGuideCuts(IReadOnlyList<double> columns, IReadOnlyList<double> rows, int outside,
            IReadOnlyList<SliceLine>? slanted = null)
        {
            Slanted = slanted ?? new SliceLine[0];
            Columns = columns;
            Rows = rows;
            Outside = outside;
        }

        public IReadOnlyList<double> Columns { get; }
        public IReadOnlyList<double> Rows { get; }

        /// <summary>Guias horizontais/verticais que não cruzam a arte (ignoradas).</summary>
        public int Outside { get; }

        /// <summary>Guias inclinadas que cruzam a arte, em mm a partir do canto superior esquerdo dela (Y para baixo).</summary>
        public IReadOnlyList<SliceLine> Slanted { get; }
    }

    /// <summary>
    /// "Dividir em partes (Slice)" pelas guias (etapa S3 de docs/reference-feature-roadmap.md):
    /// <list type="bullet">
    /// <item><see cref="ReadCuts"/>: cada guia vertical/horizontal que cruza a caixa da seleção é uma
    /// emenda (as inclinadas e as de fora são ignoradas). Inclui as guias da página mestra.</item>
    /// <item><see cref="CreateGuides"/>: põe guias nas emendas do plano, com o nome <see cref="GuideName"/>
    /// (as anteriores com esse nome saem antes), para o usuário arrastar e depois dividir por elas.</item>
    /// <item><see cref="RemoveGuides"/>: apaga só as guias com esse nome.</item>
    /// </list>
    /// </summary>
    public sealed class SliceGuideService
    {
        public const string GuideName = "PX Slice";

        public OperationResult<SliceGuideCuts> ReadCuts(ICorelSelection selection, ICorelPage page, double millimeter)
        {
            if (selection.Count == 0)
                return OperationResult<SliceGuideCuts>.Fail(OperationStatus.InvalidSelection, "Selecione a arte que vai ser dividida.");
            var (left, top, width, height) = Box(selection, millimeter);
            var columns = new List<double>();
            var rows = new List<double>();
            var slanted = new List<SliceLine>();
            var outside = 0;
            foreach (var guide in page.Guides)
            {
                if (guide.Slanted)
                {
                    var (x1, y1, x2, y2) = guide.Points;
                    var line = new SliceLine((x1 - left) / millimeter, (top - y1) / millimeter, (x2 - left) / millimeter, (top - y2) / millimeter);
                    if (SliceRegionPlanner.Crosses(width, height, line)) slanted.Add(line);
                    else outside++;
                    continue;
                }

                var cut = guide.Horizontal ? (top - guide.Position) / millimeter : (guide.Position - left) / millimeter;
                var length = guide.Horizontal ? height : width;
                if (cut <= 0 || cut >= length)
                {
                    outside++;
                    continue;
                }

                (guide.Horizontal ? rows : columns).Add(cut);
            }

            columns.Sort();
            rows.Sort();
            if (columns.Count + rows.Count + slanted.Count == 0)
                return OperationResult<SliceGuideCuts>.Fail(OperationStatus.InvalidSelection,
                    "Nenhuma guia cruza a arte selecionada. Crie guias sobre ela, retas ou inclinadas (ou use “Criar guias do plano”).");
            return OperationResult<SliceGuideCuts>.Success(new SliceGuideCuts(columns, rows, outside, slanted));
        }

        /// <param name="columns">Emendas verticais em mm a partir da esquerda da arte.</param>
        /// <param name="rows">Emendas horizontais em mm a partir do topo da arte.</param>
        public OperationResult CreateGuides(ICorelSelection selection, ICorelPage page, double millimeter,
            IReadOnlyList<double> columns, IReadOnlyList<double> rows)
        {
            if (selection.Count == 0) return OperationResult.Fail(OperationStatus.InvalidSelection, "Selecione a arte que vai ser dividida. Nada foi alterado.");
            if (columns.Count + rows.Count == 0)
                return OperationResult.Fail(OperationStatus.InvalidSelection, "O plano tem uma parte só: não há emenda para marcar. Nada foi alterado.");
            var removed = DeleteOwnGuides(page);
            var (left, top, _, _) = Box(selection, millimeter);
            foreach (var x in columns) page.CreateGuide(false, left + x * millimeter).Name = GuideName;
            foreach (var y in rows) page.CreateGuide(true, top - y * millimeter).Name = GuideName;
            var replaced = removed > 0 ? $" ({removed} guia(s) anteriores do Slice removidas)" : string.Empty;
            return OperationResult.Success(
                $"{columns.Count} guia(s) vertical(is) e {rows.Count} horizontal(is) nas emendas{replaced}. Arraste-as para ajustar e escolha “Pelas guias da página”. Ctrl+Z desfaz.");
        }

        public OperationResult RemoveGuides(ICorelPage page)
        {
            var removed = DeleteOwnGuides(page);
            return removed == 0
                ? OperationResult.Fail(OperationStatus.InvalidSelection, $"Não há guias “{GuideName}” nesta página.")
                : OperationResult.Success($"{removed} guia(s) “{GuideName}” removidas; as outras guias ficaram. Ctrl+Z desfaz.");
        }

        private static int DeleteOwnGuides(ICorelPage page)
        {
            var own = page.Guides.Where(g => string.Equals(g.Name, GuideName, StringComparison.Ordinal)).ToList();
            foreach (var guide in own) guide.Delete();
            return own.Count;
        }

        /// <summary>Caixa da seleção: canto superior esquerdo em unidades do documento, medidas em mm.</summary>
        internal static (double Left, double Top, double Width, double Height) Box(ICorelSelection selection, double millimeter)
        {
            var shapes = selection.Shapes;
            var left = shapes.Min(s => s.CenterX - s.Width / 2);
            var right = shapes.Max(s => s.CenterX + s.Width / 2);
            var bottom = shapes.Min(s => s.CenterY - s.Height / 2);
            var top = shapes.Max(s => s.CenterY + s.Height / 2);
            return (left, top, (right - left) / millimeter, (top - bottom) / millimeter);
        }
    }
}
