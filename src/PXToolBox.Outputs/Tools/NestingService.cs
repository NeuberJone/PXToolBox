using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PXToolBox.Core.Nesting;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Encaixar peças (retangular)" — leva para o documento o resultado do <see cref="RectangleNester"/>.
    /// <list type="bullet">
    /// <item>Peça = cada objeto selecionado (grupo conta como uma peça), pela caixa dele.
    /// <see cref="ReadItems"/> monta a lista na ordem da seleção; <see cref="Apply"/> confere que a
    /// seleção ainda é a mesma (quantidade e tamanhos) antes de mexer em qualquer coisa.</item>
    /// <item>A área começa no canto superior esquerdo da página ativa. Rolo com "ajustar a página": a
    /// página passa a ter a largura do rolo e o comprimento usado, antes de posicionar.</item>
    /// <item>Peça encaixada girada: Shape.Rotate(90), depois movida pelo centro. As que não couberam
    /// ficam onde estavam e são citadas na mensagem.</item>
    /// <item>Só move e gira: tamanho, conteúdo, PowerClip e Properties das peças não mudam.</item>
    /// </list>
    /// </summary>
    public sealed class NestingService
    {
        public const double SizeToleranceMm = 0.5;

        /// <summary>Peças da seleção em mm, na ordem dela. Nome vazio vira "Peça n".</summary>
        public IReadOnlyList<NestItem> ReadItems(ICorelSelection selection, double millimeter) =>
            selection.Shapes
                .Select((shape, index) => new NestItem(index,
                    string.IsNullOrWhiteSpace(shape.Name) ? $"Peça {index + 1}" : shape.Name,
                    shape.Width / millimeter, shape.Height / millimeter))
                .ToList();

        /// <summary>Nome da camada de cada peça da seleção ("" sem camada), para separar por material. Só leitura.</summary>
        public IReadOnlyList<string> ReadLayers(ICorelSelection selection) =>
            selection.Shapes.Select(shape => shape.Layer?.Name ?? "").ToList();

        /// <summary>Onde cada peça da seleção está agora: canto superior esquerdo da caixa, em mm a partir do
        /// canto superior esquerdo da página (Y para baixo, como o encaixe). Só leitura.</summary>
        public IReadOnlyList<NestPoint> ReadOrigins(ICorelSelection selection, ICorelPage page, double millimeter)
        {
            var left = page.CenterX - page.Width / 2;
            var top = page.CenterY + page.Height / 2;
            return selection.Shapes
                .Select(shape => new NestPoint((shape.CenterX - shape.Width / 2 - left) / millimeter,
                    (top - (shape.CenterY + shape.Height / 2)) / millimeter))
                .ToList();
        }

        /// <param name="millimeter">Quanto vale 1 mm na unidade do documento.</param>
        /// <param name="plannedItems">As peças da seleção na prévia (uma por objeto), para conferir a seleção.</param>
        /// <param name="sources">Com quantidades (<see cref="NestQuantities"/>): de qual objeto selecionado
        /// vem cada item do resultado. Null = item k é o objeto k.</param>
        /// <param name="resizePage">Só no rolo: página = largura do rolo × comprimento usado.</param>
        public OperationResult Apply(ICorelSelection selection, ICorelPage page, double millimeter,
            IReadOnlyList<NestItem> plannedItems, NestResult result, bool resizePage, IReadOnlyList<int>? sources = null)
        {
            if (millimeter <= 0) return Refuse("Não foi possível converter milímetros para a unidade do documento.");
            if (result.Placements.Count == 0) return Refuse("Nenhuma peça coube na área.");

            var shapes = selection.Shapes;
            if (!SameSelection(ReadItems(selection, millimeter), plannedItems))
                return Refuse("A seleção mudou desde a prévia. Selecione as mesmas peças ou clique em “Usar seleção atual”.");

            if (resizePage && result.AreaHeight == null)
            {
                page.Width = result.AreaWidth * millimeter;
                page.Height = result.UsedLength * millimeter;
            }

            var placed = ShapesFor(shapes, sources, result.Placements.Select(p => p.Item.Index), out var copies);
            var left = page.CenterX - page.Width / 2;
            var top = page.CenterY + page.Height / 2;
            var rotated = 0;
            foreach (var placement in result.Placements)
            {
                var shape = placed[placement.Item.Index];
                if (placement.Rotated)
                {
                    shape.Rotate(90);
                    rotated++;
                }

                shape.PositionX += left + (placement.X + placement.Width / 2) * millimeter - shape.CenterX;
                shape.PositionY += top - (placement.Y + placement.Height / 2) * millimeter - shape.CenterY;
            }

            var message = $"{result.Placements.Count} peça(s) encaixada(s)";
            if (copies > 0) message += $" ({copies} cópia(s) criada(s))";
            if (rotated > 0) message += $", {rotated} girada(s) 90°";
            message += $"; comprimento usado {Cm(result.UsedLength)} cm, aproveitamento {result.Utilization.ToString("P1", PtBr)}";
            if (resizePage && result.AreaHeight == null) message += "; página ajustada ao encaixe";
            if (result.Unplaced.Count > 0)
                message += $". Não couberam (ficaram onde estavam): {string.Join(", ", result.Unplaced.Select(i => i.Name))}";
            return OperationResult.Success(message + ". Ctrl+Z desfaz.");
        }

        // ---- Pelo contorno (ContourNester) ----

        /// <summary>Acima disso, um grupo entra pela caixa (ler centenas de objetos pelo COM é lento).</summary>
        public const int MaximumOutlineObjects = 400;

        /// <summary>Pontos por curva de Bézier ao transformar o contorno em polígono.</summary>
        public const int CurveSteps = 12;

        /// <summary>
        /// Contorno de cada peça selecionada, em mm com Y para baixo:
        /// <list type="bullet">
        /// <item>curva, retângulo, elipse, polígono e contêiner de PowerClip: o contorno desenhado
        /// (<see cref="ICorelShape.ReadOutline"/>), curvas de Bézier em <see cref="CurveSteps"/> retas;</item>
        /// <item>grupo: cada objeto dentro, somados (um logo por cima não vira furo);</item>
        /// <item>texto e bitmap: a caixa deles; contorno ilegível ou grupo com mais de
        /// <see cref="MaximumOutlineObjects"/> objetos: a caixa da peça inteira — as duas situações
        /// citadas em <see cref="ContourReading.BoxFallbacks"/>.</item>
        /// </list>
        /// </summary>
        public ContourReading ReadContours(ICorelSelection selection, double millimeter)
        {
            var pieces = new List<ContourPiece>();
            var offsets = new List<(double X, double Y)>();
            var fallbacks = new List<string>();
            var items = ReadItems(selection, millimeter);
            for (var index = 0; index < selection.Shapes.Count; index++)
            {
                var shape = selection.Shapes[index];
                var name = items[index].Name;
                var shapes = new List<IReadOnlyList<IReadOnlyList<NestPoint>>>();
                var budget = MaximumOutlineObjects;
                var partlyBoxed = false;
                if (!Collect(shape, millimeter, shapes, ref budget, ref partlyBoxed))
                {
                    shapes.Clear();
                    shapes.Add(new[] { Box(shape, millimeter) });
                    partlyBoxed = true;
                }

                if (partlyBoxed) fallbacks.Add(name);

                var piece = new ContourPiece(index, name, shapes);
                pieces.Add(piece);
                offsets.Add((shape.CenterX / millimeter - (piece.MinX + piece.Width / 2),
                    -shape.CenterY / millimeter - (piece.MinY + piece.Height / 2)));
            }

            return new ContourReading(items, pieces, offsets, fallbacks);
        }

        /// <summary>False = esta parte não tem contorno legível (a peça inteira vai pela caixa).
        /// <paramref name="boxed"/>: algum texto/bitmap entrou pela própria caixa.</summary>
        private static bool Collect(ICorelShape shape, double millimeter, List<IReadOnlyList<IReadOnlyList<NestPoint>>> into,
            ref int budget, ref bool boxed)
        {
            if (--budget < 0) return false;
            if (shape.Kind == ShapeKind.Text || shape.Kind == ShapeKind.Bitmap)
            {
                into.Add(new[] { Box(shape, millimeter) });
                boxed = true;
                return true;
            }

            if (shape.Kind == ShapeKind.Group && !shape.HasPowerClipContents)
            {
                foreach (var child in shape.Shapes)
                    if (!Collect(child, millimeter, into, ref budget, ref boxed)) return false;
                return shape.Shapes.Count > 0;
            }

            var elements = shape.ReadOutline();
            if (elements == null) return false;
            var rings = OutlineRings(elements, millimeter);
            if (rings.Count == 0) return false;
            into.Add(rings);
            return true;
        }

        /// <summary>Elementos do contorno → anéis fechados em mm (Y para baixo); Bézier em <see cref="CurveSteps"/> retas.</summary>
        public static IReadOnlyList<IReadOnlyList<NestPoint>> OutlineRings(IReadOnlyList<OutlineElement> elements, double millimeter) =>
            OutlinePaths(elements, millimeter).Where(path => path.Count >= 3).ToList();

        /// <summary>Elementos do contorno → um trecho por subcaminho (aberto ou fechado, 2 pontos ou mais), em mm (Y para
        /// baixo); Bézier em <see cref="CurveSteps"/> retas. As linhas de corte desenhadas do Slice usam os abertos.</summary>
        public static IReadOnlyList<IReadOnlyList<NestPoint>> OutlinePaths(IReadOnlyList<OutlineElement> elements, double millimeter)
        {
            var rings = new List<IReadOnlyList<NestPoint>>();
            List<NestPoint>? ring = null;
            var controls = new List<OutlineElement>();
            NestPoint At(double x, double y) => new NestPoint(x / millimeter, -y / millimeter);
            void Close()
            {
                if (ring != null && ring.Count >= 2) rings.Add(ring);
            }

            foreach (var element in elements)
            {
                switch (element.Kind)
                {
                    case OutlineElementKind.Start:
                        Close();
                        ring = new List<NestPoint> { At(element.X, element.Y) };
                        controls.Clear();
                        break;
                    case OutlineElementKind.Line when ring != null:
                        ring.Add(At(element.X, element.Y));
                        break;
                    case OutlineElementKind.Control:
                        controls.Add(element);
                        break;
                    case OutlineElementKind.Curve when ring != null:
                        var start = ring[ring.Count - 1];
                        var c1 = controls.Count >= 2 ? At(controls[0].X, controls[0].Y) : start;
                        var c2 = controls.Count >= 2 ? At(controls[1].X, controls[1].Y) : At(element.X, element.Y);
                        var end = At(element.X, element.Y);
                        for (var k = 1; k <= CurveSteps; k++)
                        {
                            var t = (double)k / CurveSteps;
                            var u = 1 - t;
                            ring.Add(new NestPoint(
                                u * u * u * start.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * end.X,
                                u * u * u * start.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * end.Y));
                        }

                        controls.Clear();
                        break;
                }
            }

            Close();
            return rings;
        }

        private static IReadOnlyList<NestPoint> Box(ICorelShape shape, double millimeter)
        {
            var left = (shape.CenterX - shape.Width / 2) / millimeter;
            var right = (shape.CenterX + shape.Width / 2) / millimeter;
            var top = -(shape.CenterY + shape.Height / 2) / millimeter;
            var bottom = -(shape.CenterY - shape.Height / 2) / millimeter;
            return new[] { new NestPoint(left, top), new NestPoint(right, top), new NestPoint(right, bottom), new NestPoint(left, bottom) };
        }

        /// <summary>Aplica o encaixe pelo contorno. Antes de mexer em qualquer coisa: a seleção precisa ser a
        /// da leitura e a conferência independente (<see cref="ContourNester.Verify"/>) não pode achar
        /// problema. Peça girada: Shape.Rotate e depois o centro do objeto vai para o lugar, mantendo a
        /// distância entre ele e o contorno (girada junto).</summary>
        public OperationResult ApplyContour(ICorelSelection selection, ICorelPage page, double millimeter,
            ContourReading reading, ContourNestResult result, double marginMm, double spacingMm, bool resizePage,
            IReadOnlyList<int>? sources = null)
        {
            if (millimeter <= 0) return Refuse("Não foi possível converter milímetros para a unidade do documento.");
            if (result.Placements.Count == 0) return Refuse("Nenhuma peça coube na área.");
            if (!SameSelection(ReadItems(selection, millimeter), reading.Items))
                return Refuse("A seleção mudou desde a prévia. Selecione as mesmas peças ou clique em “Usar seleção atual”.");
            var problems = ContourNester.Verify(result, marginMm, spacingMm);
            if (problems.Count > 0)
                return OperationResult.Fail(OperationStatus.ExecutionFailed,
                    $"A conferência do encaixe encontrou problema: {string.Join(" ", problems.Take(3))} Nada foi alterado.");

            if (resizePage && result.AreaHeight == null)
            {
                page.Width = result.AreaWidth * millimeter;
                page.Height = result.UsedLength * millimeter;
            }

            var placed = ShapesFor(selection.Shapes, sources, result.Placements.Select(p => p.Piece.Index), out var copies);
            var left = page.CenterX - page.Width / 2;
            var top = page.CenterY + page.Height / 2;
            var rotated = 0;
            foreach (var placement in result.Placements)
            {
                var index = sources?[placement.Piece.Index] ?? placement.Piece.Index;
                var shape = placed[placement.Piece.Index];
                if (placement.Rotation != 0)
                {
                    shape.Rotate(placement.Rotation);
                    rotated++;
                }

                var offset = ContourNester.Rotate(new NestPoint(reading.Offsets[index].X, reading.Offsets[index].Y), placement.Rotation);
                shape.PositionX += left + (placement.CenterX + offset.X) * millimeter - shape.CenterX;
                shape.PositionY += top - (placement.CenterY + offset.Y) * millimeter - shape.CenterY;
            }

            var message = $"{result.Placements.Count} peça(s) encaixada(s) pelo contorno";
            if (copies > 0) message += $" ({copies} cópia(s) criada(s))";
            if (rotated > 0) message += $", {rotated} girada(s)";
            message += $"; comprimento usado {Cm(result.UsedLength)} cm, aproveitamento ~{result.Utilization.ToString("P0", PtBr)}";
            if (resizePage && result.AreaHeight == null) message += "; página ajustada ao encaixe";
            if (reading.BoxFallbacks.Count > 0) message += $"; pela caixa: {string.Join(", ", reading.BoxFallbacks)}";
            if (result.Unplaced.Count > 0)
                message += $". Não couberam (ficaram onde estavam): {string.Join(", ", result.Unplaced.Select(i => i.Name))}";
            return OperationResult.Success(message + ". Ctrl+Z desfaz.");
        }

        // ---- Várias áreas (NestAreas) ----

        /// <summary>
        /// Aplica o encaixe em várias áreas pela caixa. As áreas ficam lado a lado a partir do canto superior
        /// esquerdo da página (as que passarem da página ficam fora dela, a não ser com
        /// <paramref name="resizePage"/>: página = todas as áreas). <paramref name="drawAreas"/>: um
        /// retângulo com o contorno padrão do documento por área, na camada da seleção, para ver os limites.
        /// <paramref name="groupBlocks"/>: as peças de cada área (e o retângulo dela, se desenhado) viram um grupo
        /// "Bloco: …"; com <paramref name="legends"/>, o grupo ganha um texto acima do bloco (<see cref="LegendText"/>).
        /// </summary>
        public OperationResult ApplyAreas(ICorelSelection selection, ICorelPage page, double millimeter,
            IReadOnlyList<NestItem> plannedItems, AreaNestResult<NestResult, NestItem> result, bool resizePage, bool drawAreas,
            IReadOnlyList<int>? sources = null, bool groupBlocks = false, bool legends = false)
        {
            if (millimeter <= 0) return Refuse("Não foi possível converter milímetros para a unidade do documento.");
            if (result.Fills.Count == 0) return Refuse("Nenhuma peça coube nas áreas.");
            if (!SameSelection(ReadItems(selection, millimeter), plannedItems))
                return Refuse("A seleção mudou desde a prévia. Selecione as mesmas peças ou clique em “Usar seleção atual”.");

            var layer = selection.Shapes[0].Layer;
            if (drawAreas && layer == null) return Refuse("Não foi possível descobrir a camada da seleção.");
            var (left, top, outlines) = PrepareAreas(page, millimeter, result.Areas, result.Lefts, result.Tops, result.TotalWidth, result.TotalHeight,
                resizePage, drawAreas ? AreaLayers(result.Areas, result.Fills.Select(f => (f.Area, f.Result.Placements.Select(p => p.Item.Index))), selection.Shapes, sources, layer!) : null);

            var placed = ShapesFor(selection.Shapes, sources,
                result.Fills.SelectMany(f => f.Result.Placements).Select(p => p.Item.Index), out var copies);
            var rotated = 0;
            foreach (var fill in result.Fills)
            {
                foreach (var placement in fill.Result.Placements)
                {
                    var shape = placed[placement.Item.Index];
                    if (placement.Rotated)
                    {
                        shape.Rotate(90);
                        rotated++;
                    }

                    shape.PositionX += left + (fill.Left + placement.X + placement.Width / 2) * millimeter - shape.CenterX;
                    shape.PositionY += top - (fill.Top + placement.Y + placement.Height / 2) * millimeter - shape.CenterY;
                }
            }

            var blocks = groupBlocks
                ? GroupBlocks(page, result.Areas, outlines, result.Fills.Select(f => (f.Area, f.Result.Placements.Select(p => placed[p.Item.Index]))),
                    legends ? Legends(millimeter, left, top, result.Areas, result.Lefts, result.Tops) : null)
                : 0;

            var message = $"{result.Fills.Sum(f => f.Result.Placements.Count)} peça(s) encaixada(s) em {result.Fills.Count} de {result.Areas.Count} área(s)";
            if (copies > 0) message += $" ({copies} cópia(s) criada(s))";
            if (rotated > 0) message += $", {rotated} girada(s) 90°";
            message += "; " + string.Join("; ", result.Fills.Select(f =>
                $"{f.Area.Name}: {f.Result.Placements.Count} peça(s), aproveitamento {AreaUtilization(f.Area, f.Result.Placements.Sum(p => p.Width * p.Height)).ToString("P1", PtBr)}"));
            return OperationResult.Success(message + AreasTail(result.Unplaced.Select(i => i.Name), resizePage, drawAreas, null, blocks, legends));
        }

        /// <summary>Várias áreas pelo contorno: as mesmas regras de <see cref="ApplyAreas"/> e de
        /// <see cref="ApplyContour"/> (conferência independente de cada área antes de mexer).</summary>
        public OperationResult ApplyContourAreas(ICorelSelection selection, ICorelPage page, double millimeter,
            ContourReading reading, AreaNestResult<ContourNestResult, ContourPiece> result, double marginMm, double spacingMm,
            bool resizePage, bool drawAreas, IReadOnlyList<int>? sources = null, bool groupBlocks = false, bool legends = false)
        {
            if (millimeter <= 0) return Refuse("Não foi possível converter milímetros para a unidade do documento.");
            if (result.Fills.Count == 0) return Refuse("Nenhuma peça coube nas áreas.");
            if (!SameSelection(ReadItems(selection, millimeter), reading.Items))
                return Refuse("A seleção mudou desde a prévia. Selecione as mesmas peças ou clique em “Usar seleção atual”.");
            var problems = result.Fills
                .SelectMany(f => ContourNester.Verify(f.Result, marginMm, spacingMm).Select(problem => $"{f.Area.Name}: {problem}"))
                .ToList();
            if (problems.Count > 0)
                return OperationResult.Fail(OperationStatus.ExecutionFailed,
                    $"A conferência do encaixe encontrou problema: {string.Join(" ", problems.Take(3))} Nada foi alterado.");

            var layer = selection.Shapes[0].Layer;
            if (drawAreas && layer == null) return Refuse("Não foi possível descobrir a camada da seleção.");
            var (left, top, outlines) = PrepareAreas(page, millimeter, result.Areas, result.Lefts, result.Tops, result.TotalWidth, result.TotalHeight,
                resizePage, drawAreas ? AreaLayers(result.Areas, result.Fills.Select(f => (f.Area, f.Result.Placements.Select(p => p.Piece.Index))), selection.Shapes, sources, layer!) : null);

            var placed = ShapesFor(selection.Shapes, sources,
                result.Fills.SelectMany(f => f.Result.Placements).Select(p => p.Piece.Index), out var copies);
            var rotated = 0;
            foreach (var fill in result.Fills)
            {
                foreach (var placement in fill.Result.Placements)
                {
                    var index = sources?[placement.Piece.Index] ?? placement.Piece.Index;
                    var shape = placed[placement.Piece.Index];
                    if (placement.Rotation != 0)
                    {
                        shape.Rotate(placement.Rotation);
                        rotated++;
                    }

                    var offset = ContourNester.Rotate(new NestPoint(reading.Offsets[index].X, reading.Offsets[index].Y), placement.Rotation);
                    shape.PositionX += left + (fill.Left + placement.CenterX + offset.X) * millimeter - shape.CenterX;
                    shape.PositionY += top - (fill.Top + placement.CenterY + offset.Y) * millimeter - shape.CenterY;
                }
            }

            var blocks = groupBlocks
                ? GroupBlocks(page, result.Areas, outlines, result.Fills.Select(f => (f.Area, f.Result.Placements.Select(p => placed[p.Piece.Index]))),
                    legends ? Legends(millimeter, left, top, result.Areas, result.Lefts, result.Tops) : null)
                : 0;

            var message = $"{result.Fills.Sum(f => f.Result.Placements.Count)} peça(s) encaixada(s) pelo contorno em {result.Fills.Count} de {result.Areas.Count} área(s)";
            if (copies > 0) message += $" ({copies} cópia(s) criada(s))";
            if (rotated > 0) message += $", {rotated} girada(s)";
            message += "; " + string.Join("; ", result.Fills.Select(f =>
                $"{f.Area.Name}: {f.Result.Placements.Count} peça(s), aproveitamento ~{AreaUtilization(f.Area, f.Result.OccupiedArea).ToString("P0", PtBr)}"));
            return OperationResult.Success(message + AreasTail(result.Unplaced.Select(p => p.Name), resizePage, drawAreas, reading.BoxFallbacks, blocks, legends));
        }

        /// <summary>Nome do retângulo desenhado para cada área ("Área: Retalho azul (100 × 150 cm)").</summary>
        public static string AreaShapeName(NestArea area) => $"Área: {area.Name} ({Cm(area.Width)} × {Cm(area.Height)} cm)";

        /// <summary>Ajusta a página (se pedido), desenha as áreas (se houver camada) e devolve o canto superior
        /// esquerdo da página, na unidade do documento, e o retângulo de cada área (vazio sem desenho).</summary>
        private static (double Left, double Top, IReadOnlyList<ICorelShape> Outlines) PrepareAreas(ICorelPage page, double millimeter, IReadOnlyList<NestArea> areas,
            IReadOnlyList<double> lefts, IReadOnlyList<double> tops, double totalWidth, double totalHeight, bool resizePage,
            IReadOnlyList<ICorelLayer>? drawOn)
        {
            if (resizePage)
            {
                page.Width = totalWidth * millimeter;
                page.Height = totalHeight * millimeter;
            }

            var left = page.CenterX - page.Width / 2;
            var top = page.CenterY + page.Height / 2;
            var outlines = new List<ICorelShape>();
            if (drawOn != null)
            {
                for (var k = 0; k < areas.Count; k++)
                {
                    var outline = drawOn[k].CreateRectangle(left + lefts[k] * millimeter, top - (tops[k] + areas[k].Height) * millimeter,
                        areas[k].Width * millimeter, areas[k].Height * millimeter);
                    outline.Name = AreaShapeName(areas[k]);
                    outlines.Add(outline);
                }
            }

            return (left, top, outlines);
        }

        /// <summary>Camada do retângulo de cada área: a da 1ª peça encaixada nela (separado por camada, cada bloco
        /// fica na camada do seu material); área sem peça, a da 1ª peça da seleção.</summary>
        private static IReadOnlyList<ICorelLayer> AreaLayers(IReadOnlyList<NestArea> areas, IEnumerable<(NestArea Area, IEnumerable<int> Items)> fills,
            IReadOnlyList<ICorelShape> shapes, IReadOnlyList<int>? sources, ICorelLayer fallback)
        {
            var layers = areas.Select(_ => fallback).ToList();
            foreach (var (area, items) in fills)
            {
                var first = items.Cast<int?>().FirstOrDefault();
                if (first == null) continue;
                var layer = shapes[sources?[first.Value] ?? first.Value].Layer;
                if (layer == null) continue;
                for (var k = 0; k < areas.Count; k++)
                    if (ReferenceEquals(areas[k], area)) layers[k] = layer;
            }

            return layers;
        }

        /// <summary>Nome do grupo de cada área ("Bloco: Folha 1").</summary>
        public static string BlockName(NestArea area) => $"Bloco: {area.Name}";

        /// <summary>Um grupo por área preenchida: o retângulo dela (se desenhado), as peças e a legenda (se pedida).
        /// Uma peça sozinha, sem retângulo nem legenda, também vira grupo (<see cref="ICorelShape.GroupAlone"/>) —
        /// todo bloco é um grupo. Devolve quantos.</summary>
        private static int GroupBlocks(ICorelPage page, IReadOnlyList<NestArea> areas, IReadOnlyList<ICorelShape> outlines,
            IEnumerable<(NestArea Area, IEnumerable<ICorelShape> Pieces)> fills, Func<NestArea, IReadOnlyList<ICorelShape>, ICorelShape?>? legend)
        {
            var count = 0;
            foreach (var (area, blockPieces) in fills)
            {
                var pieces = blockPieces.ToList();
                var shapes = new List<ICorelShape>();
                for (var k = 0; k < outlines.Count && k < areas.Count; k++)
                    if (ReferenceEquals(areas[k], area)) shapes.Add(outlines[k]);
                shapes.AddRange(pieces);
                var label = legend?.Invoke(area, pieces);
                if (label != null) shapes.Add(label);
                var group = shapes.Count == 1 ? shapes[0].GroupAlone() : page.Group(shapes);
                group.Name = BlockName(area);
                count++;
            }

            return count;
        }

        /// <summary>Espaço entre a legenda e o topo do bloco.</summary>
        public const double LegendGapMm = 10;

        public const string LegendFont = "Arial";

        private const double PointMm = 25.4 / 72;

        /// <summary>Texto da legenda de um bloco: "Tactel — Folha 1 · 100 × 200 cm · 3 peça(s)".</summary>
        public static string LegendText(NestArea area, int pieces) => $"{area.Name} · {Cm(area.Width)} × {Cm(area.Height)} cm · {pieces} peça(s)";

        /// <summary>Tamanho da letra: a altura de um quadragésimo da largura do bloco (100 cm → 2,5 cm), entre 18 e 120 pt.</summary>
        public static double LegendPoints(NestArea area) => Math.Max(18, Math.Min(120, area.Width / 40 / PointMm));

        /// <summary>Cria a legenda de um bloco: texto preto na camada da 1ª peça, encostado na esquerda do bloco e
        /// <see cref="LegendGapMm"/> acima dele (fora da área: não cobre peça). Sem camada, nada.</summary>
        private static Func<NestArea, IReadOnlyList<ICorelShape>, ICorelShape?> Legends(double millimeter, double left, double top,
            IReadOnlyList<NestArea> areas, IReadOnlyList<double> lefts, IReadOnlyList<double> tops) => (area, pieces) =>
        {
            var layer = pieces.Count > 0 ? pieces[0].Layer : null;
            var k = 0;
            while (k < areas.Count && !ReferenceEquals(areas[k], area)) k++;
            if (layer == null || k == areas.Count) return null;
            var label = layer.CreateArtisticText(LegendText(area, pieces.Count), LegendFont, LegendPoints(area));
            label.Name = $"Legenda: {area.Name}";
            label.SetUniformFill(0, 0, 0);
            label.PositionX += left + lefts[k] * millimeter + label.Width / 2 - label.CenterX;
            label.PositionY += top - tops[k] * millimeter + LegendGapMm * millimeter + label.Height / 2 - label.CenterY;
            return label;
        };

        /// <summary>Aproveitamento de uma área fechada: área das peças ÷ área inteira (o retalho é gasto todo).</summary>
        private static double AreaUtilization(NestArea area, double piecesArea) => piecesArea / (area.Width * area.Height);

        private static string AreasTail(IEnumerable<string> unplaced, bool resizePage, bool drawAreas, IReadOnlyList<string>? boxFallbacks,
            int blocks, bool legends)
        {
            var tail = "";
            if (drawAreas) tail += "; áreas desenhadas";
            if (blocks > 0) tail += $"; {blocks} bloco(s) agrupado(s){(legends ? ", com legenda" : "")}";
            if (resizePage) tail += "; página ajustada às áreas";
            if (boxFallbacks != null && boxFallbacks.Count > 0) tail += $"; pela caixa: {string.Join(", ", boxFallbacks)}";
            var missing = unplaced.ToList();
            if (missing.Count > 0) tail += $". Não couberam em nenhuma área (ficaram onde estavam): {string.Join(", ", missing)}";
            return tail + ". Ctrl+Z desfaz.";
        }

        /// <summary>
        /// Objeto de cada item encaixado: o 1º item de cada objeto usa o próprio objeto; os outros, uma
        /// duplicata dele. Todas as duplicatas são feitas ANTES de mover ou girar qualquer coisa (senão a
        /// cópia herdaria o giro do original) e só para itens que couberam.
        /// </summary>
        private static Dictionary<int, ICorelShape> ShapesFor(IReadOnlyList<ICorelShape> originals, IReadOnlyList<int>? sources,
            IEnumerable<int> placedItems, out int copies)
        {
            var shapes = new Dictionary<int, ICorelShape>();
            var used = new HashSet<int>();
            copies = 0;
            foreach (var item in placedItems.OrderBy(i => i))
            {
                var source = sources?[item] ?? item;
                if (used.Add(source))
                {
                    shapes[item] = originals[source];
                }
                else
                {
                    shapes[item] = originals[source].Duplicate();
                    copies++;
                }
            }

            return shapes;
        }

        private static bool SameSelection(IReadOnlyList<NestItem> current, IReadOnlyList<NestItem> planned) =>
            current.Count == planned.Count &&
            current.Zip(planned, (now, then) =>
                    Math.Abs(now.Width - then.Width) <= SizeToleranceMm && Math.Abs(now.Height - then.Height) <= SizeToleranceMm)
                .All(same => same);

        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        private static string Cm(double millimeters) => (millimeters / 10).ToString("0.##", PtBr);

        private static OperationResult Refuse(string message) =>
            OperationResult.Fail(OperationStatus.InvalidSelection, message + " Nada foi alterado.");
    }
}

namespace PXToolBox.Outputs.Tools
{
    /// <summary>Contornos lidos da seleção (<see cref="NestingService.ReadContours"/>): as peças para o
    /// <see cref="ContourNester"/>, o tamanho de cada objeto (para conferir que a seleção não mudou) e a
    /// distância entre o centro do objeto e o centro do contorno, em mm com Y para baixo.</summary>
    public sealed class ContourReading
    {
        public ContourReading(IReadOnlyList<NestItem> items, IReadOnlyList<ContourPiece> pieces,
            IReadOnlyList<(double X, double Y)> offsets, IReadOnlyList<string> boxFallbacks)
        {
            Items = items;
            Pieces = pieces;
            Offsets = offsets;
            BoxFallbacks = boxFallbacks;
        }

        public IReadOnlyList<NestItem> Items { get; }
        public IReadOnlyList<ContourPiece> Pieces { get; }
        public IReadOnlyList<(double X, double Y)> Offsets { get; }

        /// <summary>Peças que entraram pela caixa (texto, bitmap, contorno ilegível, grupo grande demais).</summary>
        public IReadOnlyList<string> BoxFallbacks { get; }
    }
}
