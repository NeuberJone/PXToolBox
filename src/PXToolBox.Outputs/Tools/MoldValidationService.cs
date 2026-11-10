using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PXToolBox.Core.Molds;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Validar molde" do Cadastro de moldes — reescrita simples da validação de documento do
    /// MoldMotor (<c>MoldValidator</c> + <c>LogoNumberingValidator</c>), só leitura: confere os
    /// grupos de tamanho da página ativa e devolve um relatório, sem alterar nada.
    /// <list type="bullet">
    /// <item>Grupo de tamanho = grupo com nome de tamanho das configurações (G, M, BLG, 8A…), solto
    /// numa camada ou dentro de outro grupo que não é de tamanho. Família = nome da peça.</item>
    /// <item>Erros: peça não reconhecida, logo repetido no grupo, o mesmo L&lt;n&gt; em peças
    /// diferentes, logo no lugar de outro número da referência (numeração trocada).</item>
    /// <item>Avisos: sem TAG ou sem laser (quando as configurações os criam), tamanho repetido na
    /// mesma peça, logo da referência que falta num tamanho, logo de mesmo número fora do lugar.</item>
    /// <item>Referência da peça: o grupo com mais logos (empate: o primeiro na ordem de tamanhos).
    /// Posição como no Propagar logos: distância ao centro da peça na escala da altura da peça;
    /// "mesmo lugar" = até <see cref="PositionTolerance"/> da altura da peça.</item>
    /// </list>
    /// Diferente do MoldMotor: casamento pelo logo mais próximo (o MoldMotor usa atribuição com
    /// pontuação de posição e tamanho) e nenhum metadado em <c>Shape.Comment</c>.
    /// </summary>
    public sealed class MoldValidationService
    {
        /// <summary>Distância máxima, em alturas da peça, para dois logos estarem no mesmo lugar.</summary>
        public const double PositionTolerance = 0.08;

        private readonly Func<MoldSettings> _settings;

        public MoldValidationService(Func<MoldSettings>? settings = null)
        {
            _settings = settings ?? (() => MoldSettings.Default);
        }

        public enum Severity
        {
            Error,
            Warning,
        }

        public sealed class Issue
        {
            public Issue(Severity severity, string piece, string size, string message)
            {
                Severity = severity;
                Piece = piece;
                Size = size;
                Message = message;
            }

            public Severity Severity { get; }

            /// <summary>Peça (família); vazio quando o problema não é de uma peça só.</summary>
            public string Piece { get; }

            /// <summary>Tamanho (nome do grupo); vazio quando é da peça inteira.</summary>
            public string Size { get; }

            public string Message { get; }
        }

        public sealed class Report
        {
            public Report(int groupCount, IReadOnlyList<string> pieces, IReadOnlyList<Issue> issues)
            {
                GroupCount = groupCount;
                Pieces = pieces;
                Issues = issues;
            }

            public int GroupCount { get; }

            /// <summary>Peças (famílias) encontradas, na ordem da página.</summary>
            public IReadOnlyList<string> Pieces { get; }

            /// <summary>Erros primeiro; depois por peça e pela ordem de tamanhos.</summary>
            public IReadOnlyList<Issue> Issues { get; }

            public int ErrorCount => Issues.Count(i => i.Severity == Severity.Error);

            public int WarningCount => Issues.Count(i => i.Severity == Severity.Warning);

            public string Summary =>
                GroupCount == 0
                    ? "Nenhum grupo de tamanho (G, M, BLG, 8A…) na página ativa. Monte os grupos com Definir objetos. Nada foi alterado."
                    : $"{GroupCount} grupo(s) de tamanho de {Pieces.Count} peça(s): "
                      + (Issues.Count == 0 ? "nenhum problema encontrado." : $"{ErrorCount} erro(s) e {WarningCount} aviso(s).")
                      + " Nada foi alterado.";
        }

        private sealed class SizeGroup
        {
            public SizeGroup(string size, string layer, ICorelShape? piece, List<(string Name, ICorelShape Shape)> logos)
            {
                Size = size;
                Layer = layer;
                Piece = piece;
                Logos = logos;
            }

            public string Size { get; }
            public string Layer { get; }
            public ICorelShape? Piece { get; }
            public List<(string Name, ICorelShape Shape)> Logos { get; }

            /// <summary>Logos de nome único no grupo (os repetidos já viraram erro).</summary>
            public Dictionary<string, ICorelShape> UniqueLogos =>
                Logos.GroupBy(l => l.Name).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First().Shape);
        }

        public Report Validate(ICorelPage page)
        {
            var settings = _settings();
            var sizes = settings.AllSizes.ToList();
            var issues = new List<Issue>();
            var families = new List<(string Piece, List<SizeGroup> Groups)>();
            var groupCount = 0;

            foreach (var layer in page.Layers.Where(l => !l.IsSpecialLayer))
            {
                foreach (var group in SizeGroups(layer.Shapes, settings))
                {
                    groupCount++;
                    var sizeGroup = Read(group, layer.Name, settings, issues);
                    if (sizeGroup.Piece == null) continue;

                    var piece = sizeGroup.Piece.Name.Trim();
                    var family = families.FirstOrDefault(f => Same(f.Piece, piece));
                    if (family.Groups == null)
                    {
                        family = (piece, new List<SizeGroup>());
                        families.Add(family);
                    }

                    family.Groups.Add(sizeGroup);
                }
            }

            foreach (var (piece, groups) in families)
            {
                foreach (var repeated in groups.GroupBy(g => g.Size).Where(g => g.Count() > 1))
                {
                    issues.Add(new Issue(Severity.Warning, piece, repeated.Key,
                        $"Há {repeated.Count()} grupos \"{repeated.Key}\" da peça {piece} na página (camadas: {string.Join(", ", repeated.Select(g => g.Layer).Distinct())}). Propagar logos e Criar Grid esperam um por tamanho."));
                }

                CompareWithReference(piece, groups, sizes, issues);
            }

            CollectNumberConflicts(families, issues);

            int PieceOrder(string piece)
            {
                var index = families.FindIndex(f => Same(f.Piece, piece));
                return index < 0 ? int.MaxValue : index;
            }

            var ordered = issues
                .OrderBy(i => i.Severity)
                .ThenBy(i => PieceOrder(i.Piece))
                .ThenBy(i => SizeOrder(sizes, i.Size))
                .ToList();
            return new Report(groupCount, families.Select(f => f.Piece).ToList(), ordered);
        }

        /// <summary>Grupos de tamanho entre as formas, procurando dentro de grupos que não são de tamanho.</summary>
        private static IEnumerable<ICorelShape> SizeGroups(IEnumerable<ICorelShape> shapes, MoldSettings settings)
        {
            foreach (var shape in shapes.Where(s => s.Kind == ShapeKind.Group))
            {
                if (settings.CategoryOf(shape.Name.Trim()).Length > 0)
                {
                    yield return shape;
                    continue;
                }

                foreach (var nested in SizeGroups(shape.Shapes, settings)) yield return nested;
            }
        }

        private static SizeGroup Read(ICorelShape group, string layer, MoldSettings settings, List<Issue> issues)
        {
            var size = group.Name.Trim().ToUpperInvariant();
            var recognized = MoldPieceRecognizer.Recognize(group, settings);
            var piece = recognized.Piece?.Name.Trim() ?? string.Empty;
            var logos = group.Shapes.Where(s => LayerStandardService.IsLogoName(s.Name))
                .Select(s => (Name: s.Name.Trim().ToUpperInvariant(), Shape: s)).ToList();

            if (recognized.Piece == null)
            {
                issues.Add(new Issue(Severity.Error, string.Empty, size,
                    $"Grupo \"{size}\" (camada {layer}): {recognized.Problem}. Redefina o grupo com Definir objetos."));
                return new SizeGroup(size, layer, null, logos);
            }

            var where = $"Grupo \"{size}\" da peça {piece}";
            foreach (var repeated in logos.GroupBy(l => l.Name).Where(g => g.Count() > 1).OrderBy(g => LogoNumber(g.Key)))
            {
                issues.Add(new Issue(Severity.Error, piece, size, $"{where}: {repeated.Key} aparece {repeated.Count()} vezes."));
            }

            if (settings.Define.CreateTag && !group.Shapes.Any(s => MoldPieceRecognizer.IsTag(s.Name, settings)))
            {
                issues.Add(new Issue(Severity.Warning, piece, size, $"{where}: sem TAG ({settings.TagName})."));
            }

            if (settings.Define.CreateLaserCopy && !group.Shapes.Any(s => MoldPieceRecognizer.IsLaser(s.Name, settings)))
            {
                issues.Add(new Issue(Severity.Warning, piece, size, $"{where}: sem o laser ({string.Join(" ou ", settings.LaserNames)})."));
            }

            if (recognized.Piece.Height <= 0)
            {
                issues.Add(new Issue(Severity.Error, piece, size, $"{where}: a peça não tem altura — a posição dos logos não foi conferida."));
                return new SizeGroup(size, layer, null, logos);
            }

            return new SizeGroup(size, layer, recognized.Piece, logos);
        }

        /// <summary>Cada tamanho contra a referência da peça (o grupo com mais logos).</summary>
        private static void CompareWithReference(string piece, List<SizeGroup> groups, List<string> sizes, List<Issue> issues)
        {
            var withLogos = groups.Where(g => g.UniqueLogos.Count > 0).ToList();
            if (withLogos.Count == 0 || groups.Count < 2) return;

            var reference = withLogos
                .OrderByDescending(g => g.UniqueLogos.Count)
                .ThenBy(g => SizeOrder(sizes, g.Size))
                .First();
            var referenceLogos = reference.UniqueLogos.Select(l => (Name: l.Key, Offset: Offset(l.Value, reference.Piece!))).ToList();

            foreach (var group in groups.Where(g => g != reference))
            {
                var where = $"Grupo \"{group.Size}\" da peça {piece}";
                var matched = new HashSet<string>();
                foreach (var logo in group.UniqueLogos.OrderBy(l => LogoNumber(l.Key)))
                {
                    var offset = Offset(logo.Value, group.Piece!);
                    var nearest = referenceLogos
                        .Select(r => (r.Name, Distance: Distance(r.Offset, offset)))
                        .OrderBy(r => r.Distance)
                        .First();

                    if (nearest.Distance <= PositionTolerance)
                    {
                        matched.Add(nearest.Name);
                        if (nearest.Name != logo.Key)
                        {
                            issues.Add(new Issue(Severity.Error, piece, group.Size,
                                $"{where}: {logo.Key} está no lugar do {nearest.Name} do \"{reference.Size}\" (a referência). O mesmo logo precisa do mesmo número em todos os tamanhos."));
                        }
                    }
                    else if (referenceLogos.Any(r => r.Name == logo.Key))
                    {
                        matched.Add(logo.Key);
                        issues.Add(new Issue(Severity.Warning, piece, group.Size,
                            $"{where}: {logo.Key} não está no mesmo lugar que no \"{reference.Size}\" (a referência). Propagar logos acerta a posição."));
                    }
                }

                var missing = referenceLogos.Select(r => r.Name).Where(n => !matched.Contains(n) && !group.Logos.Any(l => l.Name == n)).OrderBy(LogoNumber).ToList();
                if (missing.Count > 0)
                {
                    issues.Add(new Issue(Severity.Warning, piece, group.Size,
                        $"{where}: falta {string.Join(", ", missing)}, que o \"{reference.Size}\" (a referência) tem. Propagar logos cria o que falta."));
                }
            }
        }

        /// <summary>O mesmo L&lt;n&gt; em peças diferentes deixa ambíguo de qual peça é o logo.</summary>
        private static void CollectNumberConflicts(List<(string Piece, List<SizeGroup> Groups)> families, List<Issue> issues)
        {
            var conflicts = families
                .SelectMany(f => f.Groups.SelectMany(g => g.Logos).Select(l => (l.Name, f.Piece)))
                .Distinct()
                .GroupBy(l => l.Name)
                .Where(g => g.Count() > 1)
                .OrderBy(g => LogoNumber(g.Key));
            foreach (var conflict in conflicts)
            {
                issues.Add(new Issue(Severity.Error, string.Empty, string.Empty,
                    $"{conflict.Key} é usado pelas peças {string.Join(", ", conflict.Select(c => c.Piece))}. Cada peça precisa de números próprios (Duplicar peça já faz assim)."));
            }
        }

        /// <summary>Centro do logo menos o centro da peça, na escala da altura da peça.</summary>
        private static (double X, double Y) Offset(ICorelShape logo, ICorelShape piece) =>
            ((logo.CenterX - piece.CenterX) / piece.Height, (logo.CenterY - piece.CenterY) / piece.Height);

        private static double Distance((double X, double Y) a, (double X, double Y) b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private static int SizeOrder(List<string> sizes, string size)
        {
            var index = sizes.FindIndex(s => Same(s, size));
            return index < 0 ? int.MaxValue : index;
        }

        private static int LogoNumber(string name) =>
            int.TryParse(name.Trim().Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;

        private static bool Same(string left, string right) =>
            string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
