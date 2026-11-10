using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Molds;
using PXToolBox.Core.Naming;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// Ferramentas de Cadastro de moldes — reescritas no padrão do PX a partir das do MoldMotor
    /// (CAMADAS: Criar camadas para a seleção, maiúsculas, Excluir camadas vazias; MOLDES: Ordenar e
    /// distribuir, Criar Grid, Selecionar RECT). Os assistentes não fazem parte (decisão do usuário).
    /// O PX não depende do MoldMotor; nenhum texto visível o cita.
    /// </summary>
    public sealed class MoldToolsService
    {
        private readonly Func<MoldSettings> _settings;

        /// <param name="settings">Configurações de moldes lidas a cada uso (o host lê o arquivo
        /// salvo); sem ele, os valores de fábrica.</param>
        public MoldToolsService(Func<MoldSettings>? settings = null)
        {
            _settings = settings ?? (() => MoldSettings.Default);
        }

        public enum SortOrder
        {
            LargestFirst,
            SmallestFirst
        }

        // ---- Camadas ----

        /// <summary>
        /// Move cada grupo de tamanho selecionado (inteiro, sem desagrupar) para a camada
        /// "TIPO.MOLDE.CATEGORIA.PEÇA" (padrão novo), criando-a se não existir. A categoria vem do
        /// nome do grupo (tamanho); a peça, do objeto reconhecido dentro dele.
        /// </summary>
        public OperationResult CreateLayersForSelection(ICorelSelection selection, ICorelPage page, PieceTypeCode pieceType)
        {
            if (selection.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Selecione os grupos de tamanho (G, M, BLG…).");
            }

            var settings = _settings();
            var moved = 0;
            var problems = new List<string>();
            foreach (var group in selection.Shapes)
            {
                var size = group.Name.Trim();
                var recognized = MoldPieceRecognizer.Recognize(group, settings);
                if (recognized.Piece == null)
                {
                    problems.Add($"\"{size}\": {recognized.Problem}");
                    continue;
                }

                var pieceName = recognized.Piece.Name.Trim();
                if (pieceName.IndexOf(' ') >= 0)
                {
                    problems.Add($"\"{size}\": o nome da peça \"{pieceName}\" tem espaço");
                    continue;
                }

                var category = settings.CategoryOf(size);
                if (category.Length == 0)
                {
                    problems.Add($"\"{size}\": não é um tamanho de {string.Join(", ", settings.Categories.Select(c => c.Code))} nas configurações de moldes");
                    continue;
                }

                var layerName = $"{pieceType.Code}.{LayerStandardService.NewStandardSegment}.{category}.{pieceName}";
                var matches = page.Layers.Where(l => !l.IsSpecialLayer && SameName(l.Name, layerName)).ToList();
                if (matches.Count > 1)
                {
                    problems.Add($"\"{size}\": a camada {layerName} existe mais de uma vez");
                    continue;
                }

                var layer = matches.FirstOrDefault() ?? CreateLayer(page, layerName);
                group.MoveToLayer(layer);
                moved++;
            }

            var message = $"{moved} grupo(s) movido(s) para as camadas das peças."
                + (problems.Count > 0 ? $" Não movidos: {string.Join("; ", problems)}." : string.Empty);
            return moved == 0
                ? OperationResult.Fail(OperationStatus.InvalidSelection, message)
                : OperationResult.Success(message);
        }

        /// <summary>
        /// Nome da peça em maiúsculas em cada grupo selecionado, e a camada dele também quando está
        /// no padrão novo (contém ".MOLDE."). O objeto do laser não muda (continua "x").
        /// </summary>
        public OperationResult UppercasePieceNames(ICorelSelection selection)
        {
            var groups = selection.Shapes.Where(s => s.Kind == ShapeKind.Group).ToList();
            if (groups.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Selecione os grupos de tamanho.");
            }

            var settings = _settings();
            var pieces = 0;
            var layers = 0;
            var problems = new List<string>();
            foreach (var group in groups)
            {
                var recognized = MoldPieceRecognizer.Recognize(group, settings);
                if (recognized.Piece == null)
                {
                    problems.Add($"\"{group.Name}\": {recognized.Problem}");
                    continue;
                }

                var upper = recognized.Piece.Name.ToUpperInvariant();
                if (recognized.Piece.Name != upper)
                {
                    recognized.Piece.Name = upper;
                    pieces++;
                }

                var layer = group.Layer;
                if (layer != null && layer.Name.IndexOf("." + LayerStandardService.NewStandardSegment + ".", StringComparison.OrdinalIgnoreCase) >= 0
                    && layer.Name != layer.Name.ToUpperInvariant())
                {
                    layer.Name = layer.Name.ToUpperInvariant();
                    layers++;
                }
            }

            var message = $"{pieces} peça(s) e {layers} camada(s) em maiúsculas."
                + (problems.Count > 0 ? $" Sem peça reconhecida: {string.Join("; ", problems)}." : string.Empty);
            return OperationResult.Success(message);
        }

        public OperationResult DeleteEmptyLayers(ICorelPage page)
        {
            var result = new LayerOrganizationService().DeleteEmptyLayers(page);
            return OperationResult.Success(result.Value == 0 ? "Nenhuma camada vazia na página ativa." : result.Message);
        }

        // ---- Moldes ----

        /// <summary>
        /// Ordena a seleção pela área (maior ou menor primeiro) e enfileira: na vertical, de cima para
        /// baixo, centralizados no X do primeiro; na horizontal, da esquerda para a direita,
        /// centralizados no Y do primeiro. <paramref name="gap"/> na unidade do documento; null =
        /// automático (a menor dimensão entre as peças, no sentido da fila). A ordem na camada
        /// acompanha: o primeiro fica na frente.
        /// </summary>
        public OperationResult Distribute(ICorelSelection selection, SortOrder order, bool vertical, double? gap)
        {
            var shapes = selection.Shapes.ToList();
            if (shapes.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Selecione os objetos a distribuir.");
            }

            if (gap.HasValue && (double.IsNaN(gap.Value) || double.IsInfinity(gap.Value) || gap.Value < 0))
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Distanciamento inválido.");
            }

            var items = shapes.Select((s, i) => (Shape: s, Index: i, Width: s.Width, Height: s.Height, X: s.CenterX, Y: s.CenterY)).ToList();
            items.Sort((a, b) =>
            {
                var area = order == SortOrder.LargestFirst
                    ? (b.Width * b.Height).CompareTo(a.Width * a.Height)
                    : (a.Width * a.Height).CompareTo(b.Width * b.Height);
                return area != 0 ? area : a.Index.CompareTo(b.Index);
            });

            var extents = items.Select(i => vertical ? i.Height : i.Width).Where(e => e > 0).ToList();
            var spacing = gap ?? (extents.Count > 0 ? extents.Min() : 0);
            var moved = 0;
            if (vertical)
            {
                var centerX = items[0].X;
                var cursor = items.Max(i => i.Y + i.Height / 2);
                foreach (var item in items)
                {
                    var targetY = cursor - item.Height / 2;
                    moved += Move(item.Shape, centerX - item.X, targetY - item.Y);
                    cursor = targetY - item.Height / 2 - spacing;
                }
            }
            else
            {
                var centerY = items[0].Y;
                var cursor = items.Min(i => i.X - i.Width / 2);
                foreach (var item in items)
                {
                    var targetX = cursor + item.Width / 2;
                    moved += Move(item.Shape, targetX - item.X, centerY - item.Y);
                    cursor = targetX + item.Width / 2 + spacing;
                }
            }

            for (var i = items.Count - 1; i >= 0; i--)
            {
                items[i].Shape.OrderToFront();
            }

            return OperationResult.Success($"{items.Count} objeto(s) ordenado(s) e distribuído(s) ({moved} movido(s)).");
        }

        /// <summary>Seleciona, na página ativa, todo objeto com "_RECT" no nome (no nível da camada
        /// e dentro de grupos). Não altera o documento.</summary>
        public OperationResult SelectRectangles(ICorelPage page, IShapeArrangement arrangement)
        {
            var matches = new List<ICorelShape>();
            foreach (var layer in page.Layers.Where(l => !l.IsSpecialLayer))
            {
                foreach (var shape in layer.Shapes)
                {
                    if (HasRect(shape)) matches.Add(shape);
                    matches.AddRange(shape.Shapes.Where(HasRect));
                }
            }

            if (matches.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, $"Nenhum objeto com \"{LayerStandardService.RectangleSuffix}\" no nome na página ativa.");
            }

            arrangement.Select(matches);
            return OperationResult.Success($"{matches.Count} objeto(s) \"{LayerStandardService.RectangleSuffix}\" selecionado(s).");
        }

        /// <summary>
        /// Criar Grid: a partir do documento de moldes (camadas "TIPO.x.CATEGORIA.PEÇA", padrão antigo
        /// ou novo), copia o tamanho do Grid de cada peça (G, BLG, o maior infantil) para um ARQUIVO
        /// novo no padrão novo do Grid: uma camada por categoria ("TIPO.MOLDE.CATEGORIA") com a peça,
        /// os logos, o identificador de tamanho e o retângulo &lt;PEÇA&gt;_RECT no fundo. TAG e laser
        /// ficam de fora. As cópias só existem para salvar a seleção (SaveAsCopy) e são apagadas em
        /// seguida, com ou sem sucesso — o documento de moldes termina como estava.
        /// </summary>
        /// <returns>Sucesso com o caminho salvo em <paramref name="savedPath"/>.</returns>
        public OperationResult CreateGrid(ICorelDocument document, IShapeArrangement arrangement, string filePath, out string? savedPath)
        {
            savedPath = null;
            var settings = _settings();
            var page = document.ActivePage;
            var skipped = new List<string>();
            var plan = new List<(ICorelLayer Layer, ICorelShape Group, string Target, string Piece)>();
            foreach (var layer in page.Layers.Where(l => !l.IsSpecialLayer))
            {
                var parts = layer.Name.Split('.');
                if (parts.Length < 4 || parts[0].Trim().Length == 0) continue;
                var pieceName = string.Join(".", parts, 3, parts.Length - 3).Trim();
                var category = settings.CanonicalCategory(parts[2]);
                if (category.Length == 0 || pieceName.Length == 0)
                {
                    skipped.Add($"{layer.Name}: categoria \"{parts[2]}\" não está nas configurações de moldes");
                    continue;
                }

                var gridSizes = settings.GridSizesOf(category);
                var size = string.Join("/", gridSizes);
                var groups = layer.Shapes.Where(s => s.Kind == ShapeKind.Group && gridSizes.Any(g => SameName(s.Name, g))).ToList();
                if (groups.Count != 1)
                {
                    skipped.Add(groups.Count == 0 ? $"{layer.Name}: tamanho {size} não encontrado" : $"{layer.Name}: mais de um grupo {size}");
                    continue;
                }

                var pieces = groups[0].Shapes.Count(s => SameName(s.Name, pieceName));
                if (pieces != 1)
                {
                    skipped.Add(pieces == 0 ? $"{layer.Name}: objeto \"{pieceName}\" não encontrado no grupo {size}" : $"{layer.Name}: mais de um objeto \"{pieceName}\" no grupo {size}");
                    continue;
                }

                plan.Add((layer, groups[0], $"{parts[0].Trim()}.{LayerStandardService.NewStandardSegment}.{category}", pieceName));
            }

            if (plan.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection,
                    "Nenhuma peça para o Grid na página ativa" + (skipped.Count > 0 ? $": {string.Join("; ", skipped)}." : " (camadas TIPO.PEÇA.CATEGORIA.PEÇA ou TIPO.MOLDE.CATEGORIA.PEÇA)."));
            }

            var temporaries = new List<ICorelShape>();
            var createdLayers = new List<ICorelLayer>();
            var targets = new Dictionary<string, ICorelLayer>(StringComparer.OrdinalIgnoreCase);
            var copied = 0;
            try
            {
                foreach (var item in plan)
                {
                    if (!targets.TryGetValue(item.Target, out var target))
                    {
                        var existing = page.Layers.Where(l => !l.IsSpecialLayer && SameName(l.Name, item.Target)).ToList();
                        if (existing.Count > 1)
                        {
                            skipped.Add($"{item.Layer.Name}: a camada {item.Target} existe mais de uma vez");
                            continue;
                        }

                        target = existing.FirstOrDefault();
                        if (target == null)
                        {
                            target = CreateLayer(page, item.Target);
                            createdLayers.Add(target);
                        }

                        targets[item.Target] = target;
                    }

                    var copy = item.Group.Duplicate();
                    copy.MoveToLayer(target);
                    foreach (var child in copy.Shapes.ToList())
                    {
                        if (!SameName(child.Name, item.Piece) && !LayerStandardService.IsLogoName(child.Name) && !MoldPieceRecognizer.IsIdentifier(child.Name, settings))
                        {
                            child.Delete();
                        }
                    }

                    var loose = copy.Ungroup();
                    temporaries.AddRange(loose);
                    var piece = loose.First(s => SameName(s.Name, item.Piece));
                    var rectangle = target.CreateRectangle(piece.CenterX - piece.Width / 2, piece.CenterY - piece.Height / 2, piece.Width, piece.Height);
                    temporaries.Add(rectangle);
                    rectangle.Name = piece.Name.Trim() + LayerStandardService.RectangleSuffix;
                    rectangle.OrderToBack();
                    copied++;
                }

                if (copied > 0)
                {
                    arrangement.Select(temporaries);
                    document.SaveSelectionAsCopy(filePath);
                    savedPath = filePath;
                }
            }
            finally
            {
                foreach (var shape in temporaries)
                {
                    try { shape.Delete(); }
                    catch (Exception) { /* uma cópia que não sai não impede apagar as outras */ }
                }

                foreach (var layer in createdLayers.Where(l => l.Shapes.Count == 0))
                {
                    layer.Delete();
                }
            }

            var report = $"{copied} peça(s) no Grid" + (skipped.Count > 0 ? $"; puladas: {string.Join("; ", skipped)}" : string.Empty) + ".";
            return savedPath != null
                ? OperationResult.Success($"Grid salvo em {filePath} — {report}")
                : OperationResult.Fail(OperationStatus.InvalidSelection, $"Nada foi salvo — {report}");
        }

        private static int Move(ICorelShape shape, double dx, double dy)
        {
            const double tolerance = 0.000001;
            if (Math.Abs(dx) <= tolerance && Math.Abs(dy) <= tolerance) return 0;
            if (Math.Abs(dx) > tolerance) shape.PositionX += dx;
            if (Math.Abs(dy) > tolerance) shape.PositionY += dy;
            return 1;
        }

        private static ICorelLayer CreateLayer(ICorelPage page, string name)
        {
            var layer = page.CreateLayer(name);
            layer.Visible = true;
            layer.Locked = false;
            return layer;
        }

        private static bool HasRect(ICorelShape shape) =>
            shape.Name.IndexOf(LayerStandardService.RectangleSuffix, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool SameName(string left, string right) =>
            string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
