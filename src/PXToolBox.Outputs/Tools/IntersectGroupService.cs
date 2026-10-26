using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Measurement;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Agrupar Interseção". Ported de Agrupamento.AgruparObjetosSobrepostos (revisão candidata
    /// em Referência/Macros/modulos/Agrupamento.bas) — a macro que faltava na primeira busca no
    /// repositório CorelMacros original e que motivou o bloqueio documentado em
    /// docs/migrated-tools.md. Com esta macro em mãos, a ferramenta deixa de ser um stub.
    ///
    /// Algoritmo (opera sobre TODA a página, não sobre a seleção atual):
    /// 1. Para cada par de objetos com o MESMO centro e MESMO tamanho (dentro de tolerância),
    ///    forma um conjunto inicial.
    /// 2. Se um conjunto foi formado, absorve também objetos menores ou iguais cujo centro caia
    ///    dentro da caixa delimitadora (expandida por uma tolerância interna) do primeiro objeto
    ///    do par — isto é o que lhe dá o nome "interseção": objetos sobrepostos/aninhados na
    ///    mesma área.
    /// 3. Agrupa cada conjunto com 2+ objetos.
    ///
    /// Tolerâncias fixas em milímetros, convertidas para a unidade do documento.
    /// </summary>
    public sealed class IntersectGroupService
    {
        private const double TolerancePositionMm = 0.3;
        private const double ToleranceSizeMm = 0.3;
        private const double ToleranceInternalMm = 0.5;

        public OperationResult<int> GroupOverlapping(ICorelPage page, DocumentUnit? documentUnit)
        {
            if (documentUnit == null)
            {
                return OperationResult<int>.Fail(OperationStatus.InconclusiveRead, "Unidade do documento não reconhecida.");
            }

            var shapes = page.Layers.SelectMany(l => l.Shapes).ToList();
            if (shapes.Count < 2)
            {
                return OperationResult<int>.Fail(OperationStatus.InvalidSelection, "Não há objetos suficientes na página.");
            }

            var tolPos = DocumentUnitConversion.FromMillimeters(TolerancePositionMm, documentUnit.Value);
            var tolSize = DocumentUnitConversion.FromMillimeters(ToleranceSizeMm, documentUnit.Value);
            var tolInternal = DocumentUnitConversion.FromMillimeters(ToleranceInternalMm, documentUnit.Value);

            var used = new bool[shapes.Count];
            var groupsCreated = 0;

            for (var i = 0; i < shapes.Count - 1; i++)
            {
                if (used[i])
                {
                    continue;
                }

                var included = new bool[shapes.Count];
                var foundPair = false;

                for (var j = i + 1; j < shapes.Count; j++)
                {
                    if (used[j])
                    {
                        continue;
                    }

                    if (SamePositionAndSize(shapes[i], shapes[j], tolPos, tolSize))
                    {
                        if (!foundPair)
                        {
                            included[i] = true;
                            foundPair = true;
                        }

                        included[j] = true;
                    }
                }

                if (!foundPair)
                {
                    continue;
                }

                for (var k = 0; k < shapes.Count; k++)
                {
                    if (!used[k] && !included[k] && BelongsToSet(shapes[k], shapes[i], tolSize, tolInternal))
                    {
                        included[k] = true;
                    }
                }

                var groupIndices = new List<int>();
                for (var k = 0; k < shapes.Count; k++)
                {
                    if (included[k])
                    {
                        used[k] = true;
                        groupIndices.Add(k);
                    }
                }

                if (groupIndices.Count >= 2)
                {
                    page.Group(groupIndices.Select(idx => shapes[idx]));
                    groupsCreated++;
                }
            }

            return OperationResult<int>.Success(groupsCreated, $"{groupsCreated} grupo(s) criado(s).");
        }

        private static bool SamePositionAndSize(ICorelShape a, ICorelShape b, double tolPos, double tolSize) =>
            Math.Abs(a.CenterX - b.CenterX) <= tolPos &&
            Math.Abs(a.CenterY - b.CenterY) <= tolPos &&
            Math.Abs(a.Width - b.Width) <= tolSize &&
            Math.Abs(a.Height - b.Height) <= tolSize;

        private static bool BelongsToSet(ICorelShape candidate, ICorelShape baseShape, double tolSize, double tolInternal)
        {
            var left = baseShape.CenterX - baseShape.Width / 2 - tolInternal;
            var right = baseShape.CenterX + baseShape.Width / 2 + tolInternal;
            var bottom = baseShape.CenterY - baseShape.Height / 2 - tolInternal;
            var top = baseShape.CenterY + baseShape.Height / 2 + tolInternal;

            if (candidate.CenterX < left || candidate.CenterX > right) return false;
            if (candidate.CenterY < bottom || candidate.CenterY > top) return false;
            if (candidate.Width > baseShape.Width + tolSize) return false;
            if (candidate.Height > baseShape.Height + tolSize) return false;

            return true;
        }
    }
}
