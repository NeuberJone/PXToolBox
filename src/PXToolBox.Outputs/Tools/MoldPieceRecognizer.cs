using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Molds;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// Quem é quem dentro de um grupo de tamanho (ex.: grupo G com Tag, Frente, x, L1). Mesma regra
    /// do reconhecimento por nome do MoldMotor: a TAG, o laser, os logos (L1, L2…) e o
    /// identificador de tamanho saem primeiro; a peça é o único objeto que sobra. Sobrando mais de
    /// um, só vale o que tiver o nome da peça esperado; senão é ambíguo e nada é feito. Os nomes da
    /// TAG, do laser e do identificador vêm das configurações de moldes (<see cref="MoldSettings"/>).
    /// </summary>
    public static class MoldPieceRecognizer
    {
        public sealed class Result
        {
            private Result(ICorelShape? piece, string problem)
            {
                Piece = piece;
                Problem = problem;
            }

            public ICorelShape? Piece { get; }

            /// <summary>Motivo quando não há peça reconhecida; vazio quando há.</summary>
            public string Problem { get; }

            public static Result Found(ICorelShape piece) => new Result(piece, string.Empty);

            public static Result Failed(string problem) => new Result(null, problem);
        }

        public static bool IsTag(string name, MoldSettings settings) => Same(name, settings.TagName);

        public static bool IsLaser(string name, MoldSettings settings) => settings.LaserNames.Any(n => Same(name, n));

        public static bool IsIdentifier(string name, MoldSettings settings) =>
            settings.IdentifierName.Length > 0 && Same(name, settings.IdentifierName);

        /// <param name="expectedPieceName">Nome da peça esperado (ex.: o 4º segmento da camada), para
        /// desempatar; null quando não se sabe.</param>
        public static Result Recognize(ICorelShape group, MoldSettings settings, string? expectedPieceName = null)
        {
            if (group.Kind != ShapeKind.Group || group.Shapes.Count == 0)
            {
                return Result.Failed("não é um grupo com conteúdo");
            }

            var candidates = group.Shapes.Where(s => !IsTag(s.Name, settings) && !IsLaser(s.Name, settings) && !IsIdentifier(s.Name, settings)
                                                     && !LayerStandardService.IsLogoName(s.Name)).ToList();
            if (candidates.Count > 1 && expectedPieceName != null)
            {
                var named = candidates.Where(s => Same(s.Name, expectedPieceName)).ToList();
                if (named.Count == 1) return Result.Found(named[0]);
            }

            if (candidates.Count == 1) return Result.Found(candidates[0]);

            // Contorno interno desligado: só sobrou o objeto com o nome do laser, que é a própria peça.
            if (candidates.Count == 0)
            {
                var lasers = group.Shapes.Where(s => IsLaser(s.Name, settings)).ToList();
                return lasers.Count == 1
                    ? Result.Found(lasers[0])
                    : Result.Failed("nenhum objeto do grupo pôde ser identificado como a peça");
            }

            return Result.Failed("há mais de um objeto candidato a peça dentro do grupo");
        }

        private static bool Same(string? left, string right) =>
            string.Equals((left ?? string.Empty).Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
