using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// Os dois padrões de nome de camada da montagem (pedido do usuário, 2026-11-09). O antigo é o
    /// das macros (Preparar Molde Novo/Grid Novo); o novo é o que o MoldMotor grava.
    ///
    /// <code>
    /// Molde  antigo: CAM1.Frente.MA.Frente → grupo G → Tag, Frente, x
    ///        novo:   CAM1.MOLDE.MA.Frente  → grupo G → Tag, Frente, x   (só a camada muda)
    /// Grid   antigo: uma camada por peça     CAM1.Frente.MA → Frente
    ///        novo:   uma camada por categoria CAM1.MOLDE.MA → Frente, Costa, L1…, Frente_RECT, Costa_RECT
    /// </code>
    ///
    /// Os objetos do molde não são renomeados: o laser continua "x" (decisão do usuário). O
    /// retângulo <c>&lt;peça&gt;_RECT</c> tem a caixa da peça e fica no fundo da camada, como no
    /// MoldMotor.
    ///
    /// Camada de molde = 4+ segmentos; camada de Grid = exatamente 3. Para não confundir um molde
    /// ainda não configurado (camadas de 3 segmentos) com um Grid, as camadas de 3 segmentos só são
    /// tratadas como Grid quando o documento não tem nenhuma camada de molde.
    /// </summary>
    public sealed class LayerStandardService
    {
        public const string NewStandardSegment = "MOLDE";
        public const string RectangleSuffix = "_RECT";

        private static readonly Regex LogoName = new Regex(@"^L\d+$", RegexOptions.IgnoreCase);

        public enum Standard
        {
            Old,
            New
        }

        public static bool IsLogoName(string name) => LogoName.IsMatch((name ?? string.Empty).Trim());

        public static bool IsNewSegment(string segment) =>
            string.Equals(segment.Trim(), NewStandardSegment, StringComparison.OrdinalIgnoreCase);

        /// <summary>Camada de molde no padrão antigo → novo (null se não for camada de molde antiga).</summary>
        public static string? MoldLayerToNew(string name)
        {
            var parts = name.Split('.');
            if (parts.Length < 4 || IsNewSegment(parts[1])) return null;
            parts[1] = NewStandardSegment;
            return string.Join(".", parts);
        }

        /// <summary>Camada de molde no padrão novo → antigo: o 2º segmento volta a ser o nome da peça.</summary>
        public static string? MoldLayerToOld(string name)
        {
            var parts = name.Split('.');
            if (parts.Length < 4 || !IsNewSegment(parts[1])) return null;
            parts[1] = string.Join(".", parts, 3, parts.Length - 3);
            return string.Join(".", parts);
        }

        /// <summary>
        /// Converte o documento inteiro (todas as páginas) para o outro padrão: se houver alguma
        /// camada no padrão antigo, vai para o novo; senão, se houver no novo, volta para o antigo.
        /// </summary>
        public OperationResult ConvertDocument(ICorelDocument document)
        {
            var layers = document.Pages.SelectMany(p => p.Layers).Where(l => !l.IsSpecialLayer).ToList();
            var isMold = layers.Any(l => Segments(l.Name).Length >= 4);
            var anyOld = layers.Any(l => isMold ? MoldLayerToNew(l.Name) != null : IsOldGridLayer(l.Name));
            var anyNew = layers.Any(l => isMold ? MoldLayerToOld(l.Name) != null : IsNewGridLayer(l.Name));

            if (!anyOld && !anyNew)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection,
                    "Nenhuma camada de molde (CAM1.Frente.MA.Frente / CAM1.MOLDE.MA.Frente) nem de Grid (CAM1.Frente.MA / CAM1.MOLDE.MA) no documento.");
            }

            var target = anyOld ? Standard.New : Standard.Old;
            var kind = isMold ? "Molde" : "Grid";
            var report = new List<string>();
            foreach (var page in document.Pages)
            {
                if (isMold) ConvertMoldPage(page, target, report);
                else if (target == Standard.New) GridToNew(page, report);
                else GridToOld(page, report);
            }

            var direction = target == Standard.New ? "padrão novo" : "padrão antigo";
            return OperationResult.Success($"{kind} convertido para o {direction}: {string.Join("; ", report)}.");
        }

        private static void ConvertMoldPage(ICorelPage page, Standard target, List<string> report)
        {
            var renamed = 0;
            foreach (var layer in page.Layers.Where(l => !l.IsSpecialLayer))
            {
                var newName = target == Standard.New ? MoldLayerToNew(layer.Name) : MoldLayerToOld(layer.Name);
                if (newName == null) continue;
                layer.Name = newName;
                renamed++;
            }

            report.Add($"{renamed} camada(s) renomeada(s)");
        }

        /// <summary>Junta as camadas por peça (CAM1.Frente.MA) numa camada por categoria
        /// (CAM1.MOLDE.MA) e cria o retângulo &lt;peça&gt;_RECT de cada peça, no fundo.</summary>
        public void GridToNew(ICorelPage page, List<string> report)
        {
            var moved = 0;
            var rectangles = 0;
            var withoutPiece = new List<string>();
            var targets = new List<ICorelLayer>();
            foreach (var layer in page.Layers.Where(l => !l.IsSpecialLayer && IsOldGridLayer(l.Name)).ToList())
            {
                var parts = Segments(layer.Name);
                var pieceName = parts[1].Trim();
                var target = FindOrCreate(page, $"{parts[0]}.{NewStandardSegment}.{parts[2]}");
                if (!targets.Contains(target)) targets.Add(target);

                var shapes = layer.Shapes.ToList();
                // A peça é o objeto com o nome da peça; se houver vários (o Grid antigo dá esse nome
                // a tudo da camada), o maior.
                var piece = shapes.Where(s => NameIs(s, pieceName)).OrderByDescending(s => s.Width * s.Height).FirstOrDefault();
                foreach (var shape in shapes)
                {
                    shape.MoveToLayer(target);
                    moved++;
                }

                if (piece != null && !target.Shapes.Any(s => NameIs(s, pieceName + RectangleSuffix)))
                {
                    var rectangle = target.CreateRectangle(piece.CenterX - piece.Width / 2, piece.CenterY - piece.Height / 2, piece.Width, piece.Height);
                    rectangle.Name = pieceName + RectangleSuffix;
                    rectangles++;
                }
                else if (piece == null)
                {
                    withoutPiece.Add(layer.Name);
                }

                if (layer.Shapes.Count == 0) layer.Delete();
            }

            // Retângulos sempre no fundo da camada (o último objeto), como no MoldMotor.
            foreach (var target in targets)
            {
                foreach (var rectangle in target.Shapes.Where(s => s.Name.EndsWith(RectangleSuffix, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    rectangle.OrderToBack();
                }
            }

            report.Add($"{moved} objeto(s) em {targets.Count} camada(s) por categoria, {rectangles} retângulo(s) {RectangleSuffix} criado(s)");
            if (withoutPiece.Count > 0)
            {
                report.Add($"sem objeto com o nome da peça (retângulo não criado): {string.Join(", ", withoutPiece)}");
            }
        }

        /// <summary>Separa cada camada por categoria (CAM1.MOLDE.MA) em uma camada por peça
        /// (CAM1.Frente.MA). As peças são as que têm retângulo &lt;peça&gt;_RECT, que é apagado. Logos e
        /// outros objetos vão para a camada da peça que os contém (ou da mais próxima), com o
        /// nome que já têm.</summary>
        public void GridToOld(ICorelPage page, List<string> report)
        {
            var moved = 0;
            var created = 0;
            var skipped = new List<string>();
            foreach (var layer in page.Layers.Where(l => !l.IsSpecialLayer && IsNewGridLayer(l.Name)).ToList())
            {
                var parts = Segments(layer.Name);
                var shapes = layer.Shapes.ToList();
                var rectangles = shapes.Where(s => s.Name.Trim().EndsWith(RectangleSuffix, StringComparison.OrdinalIgnoreCase)).ToList();
                var pieces = new List<(string Name, ICorelShape Shape)>();
                foreach (var rectangle in rectangles)
                {
                    var name = rectangle.Name.Trim();
                    var pieceName = name.Substring(0, name.Length - RectangleSuffix.Length);
                    var piece = shapes.FirstOrDefault(s => !rectangles.Contains(s) && NameIs(s, pieceName));
                    if (piece != null) pieces.Add((pieceName, piece));
                }

                if (pieces.Count == 0)
                {
                    skipped.Add(layer.Name);
                    continue;
                }

                var layerOf = new Dictionary<string, ICorelLayer>(StringComparer.OrdinalIgnoreCase);
                foreach (var (name, _) in pieces)
                {
                    var targetName = $"{parts[0]}.{name}.{parts[2]}";
                    if (page.FindLayer(targetName) == null) created++;
                    layerOf[name] = FindOrCreate(page, targetName);
                }

                foreach (var shape in shapes)
                {
                    if (rectangles.Contains(shape))
                    {
                        shape.Delete();
                        continue;
                    }

                    var owner = pieces.FirstOrDefault(p => ReferenceEquals(p.Shape, shape));
                    if (owner.Shape == null) owner = Closest(pieces, shape);
                    shape.MoveToLayer(layerOf[owner.Name]);
                    moved++;
                }

                if (layer.Shapes.Count == 0) layer.Delete();
            }

            report.Add($"{moved} objeto(s) em camadas por peça ({created} criada(s))");
            if (skipped.Count > 0)
            {
                report.Add($"sem nenhum retângulo {RectangleSuffix} com a peça correspondente (camada mantida): {string.Join(", ", skipped)}");
            }
        }

        /// <summary>A peça cuja caixa contém o centro do objeto; senão, a de centro mais próximo.</summary>
        private static (string Name, ICorelShape Shape) Closest(List<(string Name, ICorelShape Shape)> pieces, ICorelShape shape)
        {
            var containing = pieces.Where(p => Math.Abs(shape.CenterX - p.Shape.CenterX) <= p.Shape.Width / 2
                                             && Math.Abs(shape.CenterY - p.Shape.CenterY) <= p.Shape.Height / 2).ToList();
            var candidates = containing.Count > 0 ? containing : pieces;
            return candidates.OrderBy(p => Math.Pow(shape.CenterX - p.Shape.CenterX, 2) + Math.Pow(shape.CenterY - p.Shape.CenterY, 2)).First();
        }

        public static bool IsOldGridLayer(string name)
        {
            var parts = Segments(name);
            return parts.Length == 3 && parts.All(p => p.Trim().Length > 0) && !IsNewSegment(parts[1]);
        }

        public static bool IsNewGridLayer(string name)
        {
            var parts = Segments(name);
            return parts.Length == 3 && parts[0].Trim().Length > 0 && parts[2].Trim().Length > 0 && IsNewSegment(parts[1]);
        }

        private static string[] Segments(string name) => (name ?? string.Empty).Split('.');

        private static bool NameIs(ICorelShape shape, string name) =>
            string.Equals(shape.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase);

        private static ICorelLayer FindOrCreate(ICorelPage page, string name) =>
            page.Layers.FirstOrDefault(l => !l.IsSpecialLayer && string.Equals(l.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
            ?? page.CreateLayer(name);
    }
}
