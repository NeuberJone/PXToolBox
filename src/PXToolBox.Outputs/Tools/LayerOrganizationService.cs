using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Naming;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// Utilitários de organização de camadas usados pela preparação de Grid e de Molde.
    /// Ported de Utilidades.OrganizarCamadas, Utilidades.ExcluirCamadasVazias,
    /// Correcoes.RenomearObjetosComNomeDaCamada (Grid.bas), Correcoes.RenomearObjetosNoGrid,
    /// Utilidades.RenomeiaCamadasNoGrid e Utilidades.LimpaObjetosGrid.
    /// </summary>
    public sealed class LayerOrganizationService
    {
        /// <summary>
        /// Ordem de prefixo usada por OrganizarCamadas. A macro original (e a revisão
        /// candidata) usam "CHO1." nesta lista, mas o único código de tipo de peça válido para
        /// "Short" é "SHO1" (ver Correcoes.RenomeiaTipoDePeca / PieceTypeCode.Sho1) — uma
        /// inconsistência pré-existente, não introduzida por esta migração. CORRIGIDO aqui para
        /// "SHO1." por decisão explícita do usuário (ver docs/migrated-tools.md), para que
        /// camadas de Short sejam ordenadas corretamente pelo Preparar Grid.
        /// </summary>
        private static readonly string[] PrefixOrder = { "COL1.", "CAL1.", "SHO1.", "REG1.", "CAM2.", "CAM1." };

        private static readonly string[] SuffixOrder = { ".C", ".FE", ".MA" };

        public OperationResult OrganizeLayersByPieceAndCategory(ICorelPage page)
        {
            if (page.Layers.Count < 2)
            {
                return OperationResult.Fail(OperationStatus.InconclusiveRead, "A página precisa ter ao menos duas camadas para ser organizada.");
            }

            var ordered = new List<ICorelLayer>();
            foreach (var prefix in PrefixOrder)
            {
                foreach (var suffix in SuffixOrder)
                {
                    foreach (var layer in page.Layers)
                    {
                        if (layer.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                            layer.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        {
                            ordered.Add(layer);
                        }
                    }
                }
            }

            for (var i = 0; i < ordered.Count; i++)
            {
                if (i == 0)
                {
                    ordered[i].MoveAbove(page.Layers[1]);
                }
                else
                {
                    ordered[i].MoveAbove(ordered[i - 1]);
                }
            }

            return OperationResult.Success($"{ordered.Count} camada(s) reordenada(s).");
        }

        public OperationResult<int> DeleteEmptyLayers(ICorelPage page)
        {
            var toDelete = page.Layers.Where(l => !l.IsSpecialLayer && l.Shapes.Count == 0).ToList();
            foreach (var layer in toDelete)
            {
                layer.Delete();
            }

            return OperationResult<int>.Success(toDelete.Count, $"{toDelete.Count} camada(s) vazia(s) excluída(s).");
        }

        /// <summary>Renomeia cada objeto da camada com o nome da própria camada (Grid.RenomearObjetosComNomeDaCamada).</summary>
        public OperationResult RenameShapesWithLayerName(ICorelPage page)
        {
            foreach (var layer in page.Layers)
            {
                foreach (var shape in layer.Shapes)
                {
                    shape.Name = layer.Name;
                }
            }

            return OperationResult.Success();
        }

        /// <summary>Renomeia cada objeto com o segmento após o primeiro ponto do nome da camada (Correcoes.RenomearObjetosNoGrid).</summary>
        public OperationResult RenameShapesAfterFirstDot(ICorelPage page)
        {
            foreach (var layer in page.Layers)
            {
                var afterFirstDot = AfterFirstDot(layer.Name);
                foreach (var shape in layer.Shapes)
                {
                    shape.Name = afterFirstDot;
                }
            }

            return OperationResult.Success();
        }

        private static string AfterFirstDot(string layerName)
        {
            var parts = layerName.Split('.');
            return parts.Length >= 2 ? parts[1] : string.Empty;
        }

        /// <summary>
        /// Mantém só os 3 primeiros segmentos pontuados do nome da camada, se houver 4 ou mais
        /// (Utilidades.RenomeiaCamadasNoGrid) — em TODAS as páginas do documento, fiel ao
        /// original ("For i = 1 To doc.Pages.Count").
        /// </summary>
        public OperationResult<int> TrimLayerNamesToThreeSegments(ICorelDocument document)
        {
            var trimmed = 0;
            foreach (var page in document.Pages)
            {
                foreach (var layer in page.Layers)
                {
                    var parts = layer.Name.Split('.');
                    if (parts.Length >= 4)
                    {
                        layer.Name = string.Join(".", parts[0], parts[1], parts[2]);
                        trimmed++;
                    }
                }
            }

            return OperationResult<int>.Success(trimmed, $"{trimmed} camada(s) com nome reduzido para 3 segmentos.");
        }

        /// <summary>Remove objetos auxiliares de grid chamados "x" ou "tag" em todas as páginas do documento (Utilidades.LimpaObjetosGrid).</summary>
        public OperationResult<int> DeleteGridHelperShapes(ICorelDocument document)
        {
            var deleted = 0;
            foreach (var page in document.Pages)
            {
                foreach (var layer in page.Layers)
                {
                    foreach (var shape in layer.Shapes.ToList())
                    {
                        if (shape.Name == "x" || shape.Name == "tag")
                        {
                            shape.Delete();
                            deleted++;
                        }
                    }
                }
            }

            return OperationResult<int>.Success(deleted, $"{deleted} objeto(s) auxiliar(es) de grid removido(s).");
        }
    }
}
