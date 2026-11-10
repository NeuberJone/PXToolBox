using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PXToolBox.Core.Molds;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Definir objetos" do Cadastro de moldes — reescrito no padrão do PX a partir do MoldMotor, na
    /// versão simples: a seleção (peça + logos de UM tamanho) vira o grupo de tamanho
    /// <c>TAG → logos L1… → peça → laser</c>, nessa ordem da frente para trás, com o nome do tamanho.
    /// <list type="bullet">
    /// <item>A peça é o maior objeto (pela caixa). Empate (dois objetos quase do mesmo tamanho) ou
    /// peça que não aceita contorno de laser (grupo, texto, bitmap): recusa sem alterar nada.</item>
    /// <item>Logos são os demais. Quem já se chama L&lt;n&gt; mantém o nome. Os outros copiam a
    /// numeração de outro tamanho já definido da mesma peça na página (logo na mesma posição relativa
    /// à peça); sem correspondência, ganham o próximo número livre da página.</item>
    /// <item>Laser: cópia exata da peça, com o nome do laser e o contorno das configurações; desligada,
    /// a própria peça recebe o contorno.</item>
    /// <item>TAG: texto com o tamanho, na posição e com as margens das configurações (fábrica: embaixo
    /// e no centro da peça, sem margens).</item>
    /// <item>Redefinir: selecionar um grupo já montado desfaz e monta de novo; TAG e laser antigos são
    /// trocados, o identificador de tamanho fica.</item>
    /// </list>
    /// <item>Contorno interno (ligado nas configurações, exige a cópia do laser): a peça recebe um contorno
    /// por dentro com a cor do tamanho e o estilo da categoria.</item>
    /// Fora desta versão (o MoldMotor tem): o metadado de papel em Shape.Comment.
    /// </summary>
    public sealed class MoldDefineService
    {
        public const string TagFont = "Arial Black";

        /// <summary>Segundo maior com área ≥ 98% da maior: não dá para saber qual é a peça.</summary>
        private const double AmbiguousAreaRatio = 0.98;

        /// <summary>Distância máxima (em frações da caixa da peça) para um logo herdar o nome do logo de
        /// outro tamanho.</summary>
        private const double MatchTolerance = 0.2;

        private readonly Func<MoldSettings> _settings;
        private readonly Func<double, double> _centimetersToDocument;

        /// <param name="centimetersToDocument">Converte as margens da TAG (cm) para a unidade do
        /// documento; sem ele, 1 cm = 1 unidade (testes).</param>
        public MoldDefineService(Func<MoldSettings>? settings = null, Func<double, double>? centimetersToDocument = null)
        {
            _settings = settings ?? (() => MoldSettings.Default);
            _centimetersToDocument = centimetersToDocument ?? (cm => cm);
        }

        public OperationResult Define(ICorelSelection selection, ICorelPage page, IShapeArrangement arrangement, string pieceName, string size)
        {
            var settings = _settings();
            pieceName = (pieceName ?? string.Empty).Trim();
            size = (size ?? string.Empty).Trim().ToUpperInvariant();

            var inputProblem = CheckInput(pieceName, settings);
            if (inputProblem != null) return Refuse(inputProblem);
            if (settings.CategoryOf(size).Length == 0) return Refuse($"\"{size}\" não é um tamanho das configurações de moldes.");
            if (selection.Count == 0) return Refuse("Selecione a peça e os logos de um tamanho (ou um grupo de tamanho para redefinir).");

            var redefined = selection.Count == 1 && selection.Shapes[0].Kind == ShapeKind.Group ? selection.Shapes[0] : null;
            var members = redefined?.Shapes.ToList() ?? selection.Shapes.ToList();
            var (plan, problem) = Analyze(members, redefined, settings);
            if (plan == null) return Refuse(problem);

            var summary = Apply(plan, pieceName, size, page, arrangement, settings, out var group);
            arrangement.Select(new[] { group });
            return OperationResult.Success($"Grupo \"{size}\" {(redefined != null ? "redefinido" : "criado")}: {summary}");
        }

        /// <summary>
        /// "Detectar objetos em lote": a seleção traz os conjuntos de vários tamanhos da mesma peça
        /// (peça + logos de cada um, soltos, ou grupos de tamanho já montados). Cada conjunto passa
        /// pelo mesmo tratamento de <see cref="Define"/>.
        /// <list type="bullet">
        /// <item>Conjuntos: um grupo já montado com nome de tamanho da categoria é um conjunto (e o
        /// tamanho dele é conhecido). Dos objetos soltos, do maior para o menor, quem tem o centro dentro
        /// da caixa de um conjunto entra nele (no menor que o contém); quem não tem começa um conjunto.</item>
        /// <item>Tamanhos (regra do MoldMotor): com grupos já montados, os demais conjuntos seguem a ordem
        /// de área da peça a partir deles; sem nenhum, só a grade completa da categoria (um conjunto por
        /// tamanho), do menor para o maior. Nunca supõe que o menor conjunto é o menor tamanho.</item>
        /// <item>Tudo é verificado antes: um conjunto com problema recusa o lote inteiro.</item>
        /// </list>
        /// </summary>
        public OperationResult DefineBatch(ICorelSelection selection, ICorelPage page, IShapeArrangement arrangement, string pieceName, string categoryCode)
        {
            var settings = _settings();
            pieceName = (pieceName ?? string.Empty).Trim();
            var inputProblem = CheckInput(pieceName, settings);
            if (inputProblem != null) return Refuse(inputProblem);
            var category = settings.Categories.FirstOrDefault(c => Same(c.Code, categoryCode ?? string.Empty));
            if (category == null || category.Sizes.Count == 0) return Refuse($"Categoria \"{categoryCode}\" não está nas configurações de moldes.");
            if (selection.Count == 0) return Refuse("Selecione os conjuntos (peça + logos) de todos os tamanhos.");

            var sets = Cluster(selection.Shapes, category);
            var plans = new List<(Set Set, SetPlan Plan, string Label)>();
            var problems = new List<string>();
            for (var i = 0; i < sets.Count; i++)
            {
                var label = sets[i].Anchor != null ? $"grupo \"{sets[i].Anchor}\"" : $"conjunto {i + 1}";
                var (plan, problem) = Analyze(sets[i].Members, sets[i].Group, settings);
                if (plan == null) problems.Add($"{label}: {problem}");
                else plans.Add((sets[i], plan, label));
            }

            if (problems.Count > 0) return Refuse($"Conjuntos com problema (contados de cima para baixo, da esquerda para a direita): {string.Join(" ", problems)}");
            if (plans.Count < 2) return Refuse("Só um conjunto encontrado — para um tamanho, use Definir objetos.");

            var sizes = ResolveSizes(plans.Select(p => (p.Plan.Piece.Width * p.Plan.Piece.Height, p.Set.Anchor)).ToList(), category.Sizes, out var sizeProblem);
            if (sizes == null) return Refuse(sizeProblem);

            var groups = new List<(string Size, ICorelShape Group, string Summary)>();
            foreach (var index in Enumerable.Range(0, plans.Count).OrderByDescending(i => plans[i].Plan.Piece.Width * plans[i].Plan.Piece.Height))
            {
                var summary = Apply(plans[index].Plan, pieceName, sizes[index], page, arrangement, settings, out var group);
                groups.Add((sizes[index], group, summary));
            }

            var ordered = groups.OrderBy(g => IndexOf(category.Sizes, g.Size)).ToList();
            arrangement.Select(ordered.Select(g => g.Group).ToList());
            return OperationResult.Success($"{ordered.Count} tamanho(s) de {pieceName}: {string.Join(" ", ordered.Select(g => $"{g.Size} — {g.Summary}"))}");
        }

        private static string? CheckInput(string pieceName, MoldSettings settings)
        {
            if (pieceName.Length == 0) return "Informe o nome da peça.";
            if (pieceName.Contains(".") || pieceName.Contains(" ")) return "O nome da peça não pode ter ponto nem espaço.";
            if (!settings.Define.TryGetLaserRgb(out _, out _, out _)) return "Cor do laser inválida nas configurações de moldes.";
            return null;
        }

        /// <summary>O que um conjunto vai virar, decidido sem alterar nada.</summary>
        private sealed class SetPlan
        {
            public SetPlan(List<ICorelShape> members, ICorelShape? redefined, ICorelShape piece, List<ICorelShape> logos,
                List<ICorelShape> oldParts, List<ICorelShape> identifiers, ICorelLayer? layer)
            {
                Members = members;
                Redefined = redefined;
                Piece = piece;
                Logos = logos;
                OldParts = oldParts;
                Identifiers = identifiers;
                Layer = layer;
            }

            public List<ICorelShape> Members { get; }
            public ICorelShape? Redefined { get; }
            public ICorelShape Piece { get; }
            public List<ICorelShape> Logos { get; }
            public List<ICorelShape> OldParts { get; }
            public List<ICorelShape> Identifiers { get; }
            public ICorelLayer? Layer { get; }
        }

        private static (SetPlan? Plan, string Problem) Analyze(List<ICorelShape> members, ICorelShape? redefined, MoldSettings settings)
        {
            var oldParts = members.Where(s => MoldPieceRecognizer.IsTag(s.Name, settings) || MoldPieceRecognizer.IsLaser(s.Name, settings)).ToList();
            var identifiers = members.Where(s => MoldPieceRecognizer.IsIdentifier(s.Name, settings)).ToList();
            var candidates = members.Except(oldParts).Except(identifiers)
                .Select((s, i) => (Shape: s, Index: i, Area: s.Width * s.Height))
                .OrderByDescending(c => c.Area).ThenBy(c => c.Index).ToList();
            if (candidates.Count == 0) return (null, "Nenhum objeto pode ser a peça (só TAG, laser ou identificador).");

            var piece = candidates[0].Shape;
            if (candidates.Count > 1 && candidates[1].Area >= candidates[0].Area * AmbiguousAreaRatio)
            {
                return (null, "Há dois objetos quase do mesmo tamanho — não dá para saber qual é a peça. Selecione só a peça e os logos dela.");
            }

            if (piece.Kind == ShapeKind.Group || piece.Kind == ShapeKind.Text || piece.Kind == ShapeKind.Bitmap)
            {
                return (null, "A peça (o maior objeto) precisa ser uma curva ou forma — grupo, texto e bitmap não recebem o contorno do laser.");
            }

            var layer = piece.Layer ?? redefined?.Layer;
            if (settings.Define.CreateTag && layer == null) return (null, "Não foi possível saber a camada da peça para criar a TAG.");
            return (new SetPlan(members, redefined, piece, candidates.Skip(1).Select(c => c.Shape).ToList(), oldParts, identifiers, layer), string.Empty);
        }

        /// <summary>Monta o grupo do tamanho (altera o documento). Devolve o resumo do que fez.</summary>
        private string Apply(SetPlan plan, string pieceName, string size, ICorelPage page, IShapeArrangement arrangement, MoldSettings settings, out ICorelShape group)
        {
            var options = settings.Define;
            options.TryGetLaserRgb(out var red, out var green, out var blue);
            var piece = plan.Piece;
            var logos = plan.Logos;
            // Por StaticId: no Corel cada leitura entrega um adaptador novo do mesmo objeto.
            var excluded = new HashSet<int>(plan.Members.Select(s => s.StaticId));
            if (plan.Redefined != null) excluded.Add(plan.Redefined.StaticId);
            var reference = FindReference(page, pieceName, excluded);
            var names = NameLogos(logos, piece, reference, page, excluded, out var inherited);

            if (plan.Redefined != null) plan.Redefined.Ungroup();
            foreach (var old in plan.OldParts) old.Delete();

            for (var i = 0; i < logos.Count; i++) logos[i].Name = names[i];
            piece.Name = pieceName;
            var laserOutline = new ShapeOutline(red, green, blue, options.LaserWidthMillimeters);
            ICorelShape? laser = null;
            if (options.CreateLaserCopy)
            {
                laser = piece.Duplicate();
                laser.Name = settings.LaserNames[0];
                laser.SetOutline(laserOutline);
            }
            else
            {
                piece.SetOutline(laserOutline);
            }

            string? innerColor = null;
            if (settings.InnerOutline.Enabled && laser != null)
            {
                innerColor = settings.InnerOutlineColorOf(size);
                MoldDefineOptions.TryParseRgb(innerColor, out var ir, out var ig, out var ib);
                var dash = (ShapeOutlineDash)(int)settings.InnerOutlineStyleOf(size);
                piece.SetOutline(new ShapeOutline(ir, ig, ib, settings.InnerOutline.WidthMillimeters, dash: dash, inside: true));
            }

            piece.OrderToBack();
            laser?.OrderToBack();

            ICorelShape? tag = null;
            if (options.CreateTag)
            {
                tag = plan.Layer!.CreateArtisticText(size, TagFont, options.TagFontSizePoints);
                tag.Name = settings.TagName;
                tag.SetUniformFill(0, 0, 0);
                if (options.TagOutlineWidthMillimeters > 0)
                {
                    tag.SetOutline(new ShapeOutline(255, 255, 255, options.TagOutlineWidthMillimeters, roundCorners: true, behindFill: true, scaleWithShape: true));
                }

                var cm = _centimetersToDocument;
                var (x, y) = MoldTagPlacement.ResolveCenter(options.TagPosition,
                    piece.CenterX - piece.Width / 2, piece.CenterY - piece.Height / 2, piece.Width, piece.Height, tag.Width, tag.Height,
                    cm(options.TagLeftMarginCm), cm(options.TagRightMarginCm), cm(options.TagTopMarginCm), cm(options.TagBottomMarginCm));
                tag.PositionX += x - tag.CenterX;
                tag.PositionY += y - tag.CenterY;
                tag.OrderToFront();
            }

            var ordered = new List<ICorelShape>();
            if (tag != null) ordered.Add(tag);
            ordered.AddRange(logos);
            ordered.AddRange(plan.Identifiers);
            ordered.Add(piece);
            if (laser != null) ordered.Add(laser);
            group = arrangement.Group(ordered);
            group.Name = size;

            var parts = new List<string> { $"peça {pieceName}" };
            parts.Add(logos.Count == 0 ? "sem logos" : $"{logos.Count} logo(s) ({string.Join(", ", names.OrderBy(LogoNumber))})");
            parts.Add(laser != null ? $"cópia do laser \"{laser.Name}\"" : "contorno do laser na própria peça");
            if (innerColor != null) parts.Add($"contorno interno {innerColor} ({MoldInnerOutlineOptions.Label(settings.InnerOutlineStyleOf(size)).ToLowerInvariant()})");
            if (tag != null) parts.Add("TAG");
            var summary = string.Join(", ", parts) + ".";
            if (inherited > 0 && reference != null) summary += $" {inherited} logo(s) com a numeração do grupo \"{reference.Value.Group.Name.Trim()}\".";
            return summary;
        }

        private sealed class Set
        {
            public Set(ICorelShape seed, string? anchor)
            {
                Group = anchor != null ? seed : null;
                Anchor = anchor;
                Members = anchor != null ? seed.Shapes.ToList() : new List<ICorelShape> { seed };
                (Left, Right, Bottom, Top) = (seed.CenterX - seed.Width / 2, seed.CenterX + seed.Width / 2, seed.CenterY - seed.Height / 2, seed.CenterY + seed.Height / 2);
                CenterX = seed.CenterX;
                CenterY = seed.CenterY;
            }

            public ICorelShape? Group { get; }
            public string? Anchor { get; }
            public List<ICorelShape> Members { get; }
            public double Left { get; }
            public double Right { get; }
            public double Bottom { get; }
            public double Top { get; }
            public double CenterX { get; }
            public double CenterY { get; }
            public double Area => (Right - Left) * (Top - Bottom);

            public bool Contains(ICorelShape shape) =>
                shape.CenterX >= Left && shape.CenterX <= Right && shape.CenterY >= Bottom && shape.CenterY <= Top;
        }

        private static List<Set> Cluster(IReadOnlyList<ICorelShape> shapes, MoldSizeCategory category)
        {
            var sets = new List<Set>();
            var loose = new List<(ICorelShape Shape, int Index)>();
            for (var i = 0; i < shapes.Count; i++)
            {
                var shape = shapes[i];
                var anchor = shape.Kind == ShapeKind.Group ? category.Sizes.FirstOrDefault(s => Same(s, shape.Name)) : null;
                if (anchor != null) sets.Add(new Set(shape, anchor));
                else loose.Add((shape, i));
            }

            foreach (var (shape, _) in loose.OrderByDescending(l => l.Shape.Width * l.Shape.Height).ThenBy(l => l.Index))
            {
                var container = sets.Where(s => s.Contains(shape)).OrderBy(s => s.Area).FirstOrDefault();
                if (container != null) container.Members.Add(shape);
                else sets.Add(new Set(shape, null));
            }

            return sets.OrderByDescending(s => Math.Round(s.CenterY, 6)).ThenBy(s => s.CenterX).ToList();
        }

        /// <summary>Tamanho de cada conjunto (mesma ordem), pela regra do MoldMotor; null com o motivo
        /// quando não dá para saber com segurança.</summary>
        private static string[]? ResolveSizes(List<(double Area, string? Anchor)> sets, IReadOnlyList<string> sizes, out string problem)
        {
            problem = string.Empty;
            var byArea = Enumerable.Range(0, sets.Count).OrderBy(i => sets[i].Area).ToList();
            for (var k = 1; k < byArea.Count; k++)
            {
                if (sets[byArea[k - 1]].Area >= sets[byArea[k]].Area * AmbiguousAreaRatio)
                {
                    problem = "Dois conjuntos têm peças quase do mesmo tamanho — não dá para saber qual tamanho é cada um.";
                    return null;
                }
            }

            int? shift = null;
            for (var position = 0; position < byArea.Count; position++)
            {
                var anchor = sets[byArea[position]].Anchor;
                if (anchor == null) continue;
                var candidate = IndexOf(sizes, anchor) - position;
                if (shift.HasValue && shift.Value != candidate)
                {
                    problem = "Os grupos já montados não seguem a ordem de tamanho das peças (um tamanho maior com peça menor).";
                    return null;
                }

                shift = candidate;
            }

            if (!shift.HasValue)
            {
                if (sets.Count != sizes.Count)
                {
                    problem = $"Sem nenhum grupo de tamanho já montado na seleção, é preciso um conjunto para cada tamanho da categoria ({sizes.Count}: {string.Join(", ", sizes)}); foram encontrados {sets.Count}. Inclua um grupo já definido (ex.: o G) para servir de referência.";
                    return null;
                }

                shift = 0;
            }

            var result = new string[sets.Count];
            for (var position = 0; position < byArea.Count; position++)
            {
                var index = position + shift.Value;
                if (index < 0 || index >= sizes.Count)
                {
                    problem = "Não há tamanhos suficientes na categoria para todos os conjuntos a partir do grupo já montado.";
                    return null;
                }

                result[byArea[position]] = sizes[index];
            }

            return result;
        }

        private static int IndexOf(IReadOnlyList<string> sizes, string size)
        {
            for (var i = 0; i < sizes.Count; i++)
            {
                if (Same(sizes[i], size)) return i;
            }

            return -1;
        }

        private static OperationResult Refuse(string message) =>
            OperationResult.Fail(OperationStatus.InvalidSelection, message + " Nada foi alterado.");

        /// <summary>Outro grupo de tamanho da mesma peça na página (fora da seleção), com logos: a
        /// numeração dele é a que os logos novos devem seguir.</summary>
        private static (ICorelShape Group, ICorelShape Piece)? FindReference(ICorelPage page, string pieceName, HashSet<int> excluded)
        {
            foreach (var layer in page.Layers.Where(l => !l.IsSpecialLayer))
            foreach (var shape in layer.Shapes)
            {
                if (shape.Kind != ShapeKind.Group || excluded.Contains(shape.StaticId)) continue;
                var piece = shape.Shapes.FirstOrDefault(s => Same(s.Name, pieceName));
                if (piece != null && shape.Shapes.Any(s => LayerStandardService.IsLogoName(s.Name))) return (shape, piece);
            }

            return null;
        }

        private static List<string> NameLogos(List<ICorelShape> logos, ICorelShape piece, (ICorelShape Group, ICorelShape Piece)? reference,
            ICorelPage page, HashSet<int> excluded, out int inherited)
        {
            inherited = 0;
            var names = new string?[logos.Count];
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < logos.Count; i++)
            {
                var current = logos[i].Name.Trim().ToUpperInvariant();
                if (LayerStandardService.IsLogoName(current) && taken.Add(current)) names[i] = current;
            }

            if (reference != null)
            {
                var referenceLogos = reference.Value.Group.Shapes.Where(s => LayerStandardService.IsLogoName(s.Name))
                    .Select(s => (Name: s.Name.Trim().ToUpperInvariant(), Position: Relative(s, reference.Value.Piece)))
                    .Where(r => !taken.Contains(r.Name)).ToList();
                var pairs = new List<(int Logo, string Name, double Distance)>();
                for (var i = 0; i < logos.Count; i++)
                {
                    if (names[i] != null) continue;
                    var position = Relative(logos[i], piece);
                    foreach (var candidate in referenceLogos)
                    {
                        var distance = Math.Sqrt(Math.Pow(position.X - candidate.Position.X, 2) + Math.Pow(position.Y - candidate.Position.Y, 2));
                        if (distance <= MatchTolerance) pairs.Add((i, candidate.Name, distance));
                    }
                }

                foreach (var pair in pairs.OrderBy(p => p.Distance))
                {
                    if (names[pair.Logo] != null || taken.Contains(pair.Name)) continue;
                    names[pair.Logo] = pair.Name;
                    taken.Add(pair.Name);
                    inherited++;
                }
            }

            // Sem correspondência: próximo número livre da página, de cima para baixo e da esquerda
            // para a direita.
            var next = HighestLogoNumber(page, excluded) + 1;
            foreach (var used in taken) next = Math.Max(next, LogoNumber(used) + 1);
            foreach (var i in Enumerable.Range(0, logos.Count).Where(i => names[i] == null)
                         .OrderByDescending(i => Math.Round(logos[i].CenterY, 6)).ThenBy(i => logos[i].CenterX))
            {
                names[i] = "L" + next.ToString(CultureInfo.InvariantCulture);
                next++;
            }

            return names.Select(n => n!).ToList();
        }

        /// <summary>Centro do objeto em frações da caixa da peça (0,0 = canto inferior esquerdo).</summary>
        private static (double X, double Y) Relative(ICorelShape shape, ICorelShape piece)
        {
            var width = piece.Width > 0 ? piece.Width : 1;
            var height = piece.Height > 0 ? piece.Height : 1;
            return ((shape.CenterX - (piece.CenterX - width / 2)) / width, (shape.CenterY - (piece.CenterY - height / 2)) / height);
        }

        private static int HighestLogoNumber(ICorelPage page, HashSet<int> excluded)
        {
            var highest = 0;
            foreach (var layer in page.Layers.Where(l => !l.IsSpecialLayer))
            foreach (var shape in layer.Shapes)
            {
                if (excluded.Contains(shape.StaticId)) continue;
                highest = Math.Max(highest, LogoNumber(shape.Name));
                foreach (var child in shape.Shapes) highest = Math.Max(highest, LogoNumber(child.Name));
            }

            return highest;
        }

        private static int LogoNumber(string name) =>
            LayerStandardService.IsLogoName(name) && int.TryParse(name.Trim().Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;

        private static bool Same(string left, string right) =>
            string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
