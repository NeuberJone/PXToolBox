using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PXToolBox.Core.Result;
using PXToolBox.Core.Slicing;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Dividir em partes (Slice)" — aplica no documento o plano do <see cref="SlicePlanner"/>.
    /// <list type="bullet">
    /// <item>Cada parte é um retângulo sem contorno ("Parte L1 C1") com uma cópia da seleção inteira
    /// dentro, como PowerClip (Shape.AddToPowerClip sem centralizar): texto, bitmap, grupo, cores e
    /// Properties da arte não são convertidos nem perdidos — o retângulo só esconde o que fica fora.</item>
    /// <item>As partes ficam no lugar da arte, afastadas <c>separationMm</c> por coluna (para a direita)
    /// e por linha (para baixo), num grupo "Divisão (Slice)" na camada do primeiro objeto selecionado.</item>
    /// <item>O original fica oculto (ou é apagado, se pedido) só depois de todas as partes prontas.
    /// Falha no meio: as partes já criadas são apagadas e o original não muda.</item>
    /// <item>O plano precisa ser da seleção atual: se a caixa da seleção não tiver a área do plano
    /// (tolerância <see cref="AreaToleranceMm"/>), recusa sem mexer em nada.</item>
    /// </list>
    /// </summary>
    public sealed class SliceCutService
    {
        public const string GroupName = "Divisão (Slice)";

        public const double AreaToleranceMm = 0.5;

        /// <param name="millimeter">Quanto vale 1 mm na unidade do documento.</param>
        /// <param name="separationMm">Afastamento entre partes vizinhas, em mm (0 = no lugar).</param>
        public OperationResult Apply(ICorelSelection selection, ICorelPage page, double millimeter, SlicePlan plan,
            double separationMm, bool deleteOriginal) =>
            Divide(selection, page, millimeter, plan.Width, plan.Height, separationMm, deleteOriginal,
                plan.Tiles.Select(tile => new Part(
                    $"Parte L{tile.Row} C{tile.Column}",
                    // Plano: X/Y a partir do canto superior esquerdo, Y para baixo. Documento: Y para cima.
                    (layer, left, top) => layer.CreateRectangle(left + tile.X * millimeter, top - (tile.Y + tile.Height) * millimeter,
                        tile.Width * millimeter, tile.Height * millimeter),
                    (tile.Column - 1) * separationMm, (tile.Row - 1) * separationMm)).ToList(),
                $"{plan.Tiles.Count} parte(s) ({plan.Rows} × {plan.Columns})");

        /// <summary>
        /// Divisão livre (<see cref="SliceRegionPlanner"/>): cada parte é um polígono sem contorno ("Parte 1"…) com a
        /// cópia da arte dentro, como no <see cref="Apply"/>. Afastamento: cada parte se afasta do centro da arte na
        /// direção do centro dela, <paramref name="separationMm"/> por parte.
        /// </summary>
        public OperationResult ApplyRegions(ICorelSelection selection, ICorelPage page, double millimeter, SliceRegionPlan plan,
            double separationMm, bool deleteOriginal) =>
            Divide(selection, page, millimeter, plan.Width, plan.Height, separationMm, deleteOriginal,
                plan.Regions.Select(region =>
                {
                    var dx = region.CenterX - plan.Width / 2;
                    var dy = region.CenterY - plan.Height / 2;
                    var length = Math.Sqrt(dx * dx + dy * dy);
                    var (moveX, moveY) = length < 1e-9 ? (0.0, 0.0) : (dx / length * separationMm, dy / length * separationMm);
                    return new Part($"Parte {region.Number}",
                        (layer, left, top) => layer.CreatePolygon(region.Points.Select(p => (left + p.X * millimeter, top - p.Y * millimeter)).ToList()),
                        moveX, moveY);
                }).ToList(),
                $"{plan.Regions.Count} parte(s) pelas linhas de corte");

        /// <summary>Uma parte a criar: o contêiner (na camada, a partir do canto superior esquerdo da arte, na unidade
        /// do documento) e quanto afastar, em mm (X para a direita, Y para baixo).</summary>
        private sealed class Part
        {
            public Part(string name, Func<ICorelLayer, double, double, ICorelShape> create, double moveRightMm, double moveDownMm)
            {
                Name = name;
                Create = create;
                MoveRightMm = moveRightMm;
                MoveDownMm = moveDownMm;
            }

            public string Name { get; }
            public Func<ICorelLayer, double, double, ICorelShape> Create { get; }
            public double MoveRightMm { get; }
            public double MoveDownMm { get; }
        }

        private OperationResult Divide(ICorelSelection selection, ICorelPage page, double millimeter, double planWidth, double planHeight,
            double separationMm, bool deleteOriginal, IReadOnlyList<Part> plannedParts, string summary)
        {
            if (selection.Count == 0) return Refuse("Selecione a arte que vai ser dividida.");
            if (millimeter <= 0) return Refuse("Não foi possível converter milímetros para a unidade do documento.");
            if (separationMm < 0 || double.IsNaN(separationMm) || double.IsInfinity(separationMm))
                return Refuse("O afastamento entre as partes precisa ser 0 ou mais.");

            var originals = selection.Shapes.ToList();
            var left = originals.Min(s => s.CenterX - s.Width / 2);
            var right = originals.Max(s => s.CenterX + s.Width / 2);
            var bottom = originals.Min(s => s.CenterY - s.Height / 2);
            var top = originals.Max(s => s.CenterY + s.Height / 2);
            var widthMm = (right - left) / millimeter;
            var heightMm = (top - bottom) / millimeter;
            if (Math.Abs(widthMm - planWidth) > AreaToleranceMm || Math.Abs(heightMm - planHeight) > AreaToleranceMm)
                return Refuse($"A seleção atual tem {Cm(widthMm)} × {Cm(heightMm)} cm, mas o plano é de {Cm(planWidth)} × {Cm(planHeight)} cm. Selecione a arte da prévia ou clique em “Usar seleção atual”.");

            var layer = originals[0].Layer;
            if (layer == null) return Refuse("Não foi possível descobrir a camada da seleção.");

            var parts = new List<ICorelShape>();
            var copies = new List<ICorelShape>();
            try
            {
                foreach (var planned in plannedParts)
                {
                    var part = planned.Create(layer, left, top);
                    parts.Add(part);
                    part.RemoveOutline();
                    part.Name = planned.Name;

                    var partCopies = originals.Select(s => s.Duplicate()).ToList();
                    copies.AddRange(partCopies);
                    var content = partCopies.Count == 1 ? partCopies[0] : page.Group(partCopies);
                    if (partCopies.Count > 1) copies.Add(content);
                    content.PlaceInsidePowerClip(part);

                    part.PositionX += planned.MoveRightMm * millimeter;
                    part.PositionY -= planned.MoveDownMm * millimeter;
                }

                var group = page.Group(parts);
                group.Name = GroupName;
            }
            catch (Exception ex)
            {
                foreach (var shape in parts.Concat(copies)) TryDelete(shape);
                return OperationResult.Fail(OperationStatus.ExecutionFailed,
                    $"Falha ao criar as partes ({ex.Message}). As partes criadas foram apagadas; a arte original não mudou.");
            }

            foreach (var original in originals)
            {
                if (deleteOriginal) original.Delete();
                else original.Visible = false;
            }

            var originalNote = deleteOriginal ? "a arte original foi apagada" : "a arte original ficou oculta, no mesmo lugar";
            return OperationResult.Success($"{summary} no grupo “{GroupName}”; {originalNote}. Ctrl+Z desfaz.");
        }

        private static void TryDelete(ICorelShape shape)
        {
            try { shape.Delete(); }
            catch (Exception) { /* já apagado junto com o contêiner */ }
        }

        private static string Cm(double millimeters) => (millimeters / 10).ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));

        private static OperationResult Refuse(string message) => OperationResult.Fail(OperationStatus.InvalidSelection, message + " Nada foi alterado.");
    }
}
