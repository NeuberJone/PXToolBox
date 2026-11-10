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
    /// "Duplicar peça" do Cadastro de moldes — reescrito no padrão do PX a partir da duplicação de peças
    /// do MoldMotor, na versão simples: cada grupo de tamanho selecionado ganha uma cópia inteira (mesmo
    /// tamanho no nome do grupo), com a peça renomeada para o nome da cópia, ao lado da original.
    /// <list type="bullet">
    /// <item>Logos da cópia: números novos, iguais em todos os tamanhos (a cópia é outra peça). Se a cópia
    /// já existe na página, reaproveita os números dela.</item>
    /// <item>Espelhar: só a peça e o laser da cópia, cada um no próprio centro; TAG e logos não.</item>
    /// <item>Posição: a peça da cópia fica à distância pedida da peça original, na direção pedida.</item>
    /// <item>De novo com o mesmo nome: a cópia anterior do mesmo tamanho é substituída (nunca duas).</item>
    /// </list>
    /// Fora desta versão (o MoldMotor tem): configuração de duplicação guardada por peça.
    /// </summary>
    public sealed class MoldDuplicationService
    {
        public enum CopyDirection
        {
            Left,
            Right,
            Above,
            Below,
        }

        private readonly Func<MoldSettings> _settings;

        public MoldDuplicationService(Func<MoldSettings>? settings = null)
        {
            _settings = settings ?? (() => MoldSettings.Default);
        }

        private sealed class Source
        {
            public Source(ICorelShape group, string size, ICorelShape piece)
            {
                Group = group;
                Size = size;
                Piece = piece;
            }

            public ICorelShape Group { get; }
            public string Size { get; }
            public ICorelShape Piece { get; }
        }

        /// <param name="gap">Distância entre a peça original e a da cópia, na unidade do documento.</param>
        public OperationResult Duplicate(ICorelSelection selection, ICorelPage page, IShapeArrangement arrangement,
            string copyName, CopyDirection direction, double gap, bool mirror)
        {
            var settings = _settings();
            copyName = (copyName ?? string.Empty).Trim();
            if (copyName.Length == 0) return Refuse("Informe o nome da cópia.");
            if (copyName.Contains(".") || copyName.Contains(" ")) return Refuse("O nome da cópia não pode ter ponto nem espaço.");
            if (gap < 0) return Refuse("A distância não pode ser negativa.");
            if (selection.Count == 0) return Refuse("Selecione os grupos de tamanho da peça a duplicar.");

            var sources = new List<Source>();
            foreach (var shape in selection.Shapes)
            {
                var size = shape.Name.Trim().ToUpperInvariant();
                if (shape.Kind != ShapeKind.Group || settings.CategoryOf(size).Length == 0)
                {
                    return Refuse($"\"{(size.Length == 0 ? "(sem nome)" : shape.Name.Trim())}\" não é um grupo de tamanho. Monte os grupos antes com Definir objetos (ou em lote) e selecione só os grupos.");
                }

                var recognized = MoldPieceRecognizer.Recognize(shape, settings);
                if (recognized.Piece == null) return Refuse($"Grupo \"{size}\": {recognized.Problem}.");
                sources.Add(new Source(shape, size, recognized.Piece));
            }

            var repeated = sources.GroupBy(s => s.Size).FirstOrDefault(g => g.Count() > 1);
            if (repeated != null) return Refuse($"Há mais de um grupo \"{repeated.Key}\" na seleção.");
            var pieces = sources.Select(s => s.Piece.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (pieces.Count > 1) return Refuse($"Os grupos são de peças diferentes ({string.Join(", ", pieces)}) — selecione os tamanhos de uma peça só.");
            var pieceName = pieces[0];
            if (Same(pieceName, copyName)) return Refuse("O nome da cópia precisa ser diferente do nome da peça.");

            // Cópias já feitas antes (grupo de tamanho com a peça da cópia), fora da seleção.
            var selected = new HashSet<int>(sources.Select(s => s.Group.StaticId));
            var previous = new Dictionary<string, ICorelShape>(StringComparer.OrdinalIgnoreCase);
            foreach (var layer in page.Layers.Where(l => !l.IsSpecialLayer))
            foreach (var shape in layer.Shapes)
            {
                if (shape.Kind != ShapeKind.Group || selected.Contains(shape.StaticId) || !shape.Shapes.Any(c => Same(c.Name, copyName))) continue;
                var size = shape.Name.Trim();
                if (!sources.Any(s => Same(s.Size, size))) continue;
                if (previous.ContainsKey(size)) return Refuse($"Há mais de uma cópia \"{copyName}\" do tamanho {size.ToUpperInvariant()} na página — apague a sobrando antes.");
                previous[size] = shape;
            }

            var renames = LogoRenames(sources, previous.Values.ToList(), page);

            var copies = new List<ICorelShape>();
            foreach (var source in sources)
            {
                if (previous.TryGetValue(source.Size, out var old)) old.Delete();

                var copy = source.Group.Duplicate();
                copy.Name = source.Size;
                ICorelShape? copyPiece = null;
                var copyLasers = new List<ICorelShape>();
                foreach (var child in copy.Shapes.ToList())
                {
                    var name = child.Name.Trim();
                    if (copyPiece == null && Same(name, source.Piece.Name)) copyPiece = child;
                    else if (MoldPieceRecognizer.IsLaser(name, settings)) copyLasers.Add(child);
                    else if (renames.TryGetValue(name, out var target)) child.Name = target;
                }

                copyPiece ??= copyLasers.FirstOrDefault();
                if (copyPiece == null) continue;
                copyLasers.Remove(copyPiece);
                copyPiece.Name = copyName;

                if (mirror)
                {
                    Flip(copyPiece);
                    foreach (var laser in copyLasers) Flip(laser);
                }

                var (targetX, targetY) = Place(source.Piece, direction, gap);
                copy.PositionX += targetX - copyPiece.CenterX;
                copy.PositionY += targetY - copyPiece.CenterY;
                copies.Add(copy);
            }

            arrangement.Select(copies);
            var parts = new List<string> { $"Cópia \"{copyName}\" de {copies.Count} tamanho(s) ({string.Join(", ", sources.Select(s => s.Size))})", DirectionText(direction) };
            if (mirror) parts.Add("espelhada");
            var message = string.Join(", ", parts) + ".";
            if (renames.Count > 0) message += $" Logos: {string.Join(", ", renames.OrderBy(r => LogoNumber(r.Key)).Select(r => $"{r.Key} → {r.Value}"))}.";
            if (previous.Count > 0) message += $" {previous.Count} cópia(s) anterior(es) substituída(s).";
            return OperationResult.Success(message);
        }

        /// <summary>Números dos logos da cópia, iguais em todos os tamanhos: os da cópia já existente
        /// (pela ordem), quando não colidem com os da original; senão, os próximos livres da página.</summary>
        private static Dictionary<string, string> LogoRenames(List<Source> sources, List<ICorelShape> previous, ICorelPage page)
        {
            var sourceNames = sources.SelectMany(s => s.Group.Shapes).Select(c => c.Name.Trim().ToUpperInvariant())
                .Where(LayerStandardService.IsLogoName).Distinct().OrderBy(LogoNumber).ToList();
            var reusable = previous.SelectMany(p => p.Shapes).Select(c => c.Name.Trim().ToUpperInvariant())
                .Where(LayerStandardService.IsLogoName).Distinct().OrderBy(LogoNumber).ToList();
            if (reusable.Any(sourceNames.Contains)) reusable.Clear();

            var next = 0;
            foreach (var layer in page.Layers.Where(l => !l.IsSpecialLayer))
            foreach (var shape in layer.Shapes)
            {
                next = Math.Max(next, LogoNumber(shape.Name));
                foreach (var child in shape.Shapes) next = Math.Max(next, LogoNumber(child.Name));
            }

            var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < sourceNames.Count; i++)
            {
                renames[sourceNames[i]] = i < reusable.Count ? reusable[i] : "L" + (++next).ToString(CultureInfo.InvariantCulture);
            }

            return renames;
        }

        /// <summary>Centro da peça da cópia: mesma caixa da peça original, deslocada na direção.</summary>
        private static (double X, double Y) Place(ICorelShape piece, CopyDirection direction, double gap)
        {
            switch (direction)
            {
                case CopyDirection.Right: return (piece.CenterX + piece.Width + gap, piece.CenterY);
                case CopyDirection.Above: return (piece.CenterX, piece.CenterY + piece.Height + gap);
                case CopyDirection.Below: return (piece.CenterX, piece.CenterY - piece.Height - gap);
                default: return (piece.CenterX - piece.Width - gap, piece.CenterY);
            }
        }

        /// <summary>Espelho horizontal no próprio centro (o Corel pode deslocar; o centro volta).</summary>
        private static void Flip(ICorelShape shape)
        {
            var (x, y) = (shape.CenterX, shape.CenterY);
            shape.FlipHorizontally();
            shape.PositionX += x - shape.CenterX;
            shape.PositionY += y - shape.CenterY;
        }

        private static string DirectionText(CopyDirection direction)
        {
            switch (direction)
            {
                case CopyDirection.Right: return "à direita";
                case CopyDirection.Above: return "acima";
                case CopyDirection.Below: return "abaixo";
                default: return "à esquerda";
            }
        }

        private static OperationResult Refuse(string message) =>
            OperationResult.Fail(OperationStatus.InvalidSelection, message + " Nada foi alterado.");

        private static int LogoNumber(string name) =>
            LayerStandardService.IsLogoName(name) && int.TryParse(name.Trim().Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;

        private static bool Same(string left, string right) =>
            string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
