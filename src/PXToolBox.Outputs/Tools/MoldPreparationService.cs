using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Naming;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Preparar Molde Novo". Ported de Utilidades.ConfiguraMoldNovo, que encadeia
    /// Utilidades.ExcluirCamadasVazias, Correcoes.RenomearCamadas e Correcoes.RenomearObjetosNoMold
    /// (CorelMacros). A ordem é significativa: RenomearCamadas normaliza o nome da camada para
    /// exatamente 4 segmentos ANTES de RenomearObjetosNoMold usar o 4º segmento — ver o
    /// comentário em <see cref="PrepareNewMold"/>.
    ///
    /// grupo.shapes.FindShapes(, cdrGroupShape) na macro original usa o parâmetro Recursive por
    /// padrão — confirmado por reflexão sobre Corel.Interop.VGCore.dll (o 3º parâmetro de
    /// Shapes.FindShapes tem valor padrão True). Por isso a busca por grupos aqui é recursiva
    /// (desce em qualquer subforma, não só nos filhos diretos da camada).
    /// </summary>
    public sealed class MoldPreparationService
    {
        private readonly LayerOrganizationService _layerOrganization = new LayerOrganizationService();

        public OperationResult PrepareNewMold(ICorelPage page)
        {
            _layerOrganization.DeleteEmptyLayers(page);

            foreach (var layer in page.Layers)
            {
                layer.Name = NormalizeToFourSegments(layer.Name);
            }

            var renamedSubItems = 0;
            foreach (var layer in page.Layers)
            {
                var replacementName = SegmentAfterSecondDotOrEmpty(layer.Name);

                foreach (var group in FindGroupsRecursive(layer.Shapes))
                {
                    if (group.Shapes.Count == 0)
                    {
                        continue;
                    }

                    foreach (var subItem in group.Shapes)
                    {
                        if (!SizeTokens.IsAllowed(subItem.Name))
                        {
                            subItem.Name = replacementName;
                            renamedSubItems++;
                        }
                    }
                }
            }

            return OperationResult.Success($"Molde preparado. {renamedSubItems} subitem(ns) renomeado(s).");
        }

        private static IEnumerable<ICorelShape> FindGroupsRecursive(IEnumerable<ICorelShape> shapes)
        {
            foreach (var shape in shapes)
            {
                if (shape.Kind == ShapeKind.Group)
                {
                    yield return shape;
                }

                foreach (var nested in FindGroupsRecursive(shape.Shapes))
                {
                    yield return nested;
                }
            }
        }

        /// <summary>Correcoes.RenomearCamadas: para nomes com 3+ segmentos, normaliza para "seg0.seg1.seg2.seg1".</summary>
        private static string NormalizeToFourSegments(string layerName)
        {
            var parts = layerName.Split('.');
            return parts.Length >= 3
                ? string.Join(".", parts[0], parts[1], parts[2], parts[1])
                : layerName;
        }

        /// <summary>
        /// Correcoes.ObterNomeCamadaDepoisDoSegundoPonto: retorna o 4º segmento (índice 3) se houver
        /// 3+ segmentos, senão "". Só é seguro ler o índice 3 porque NormalizeToFourSegments já
        /// garantiu, para qualquer nome com 3+ segmentos, exatamente 4 segmentos.
        /// </summary>
        private static string SegmentAfterSecondDotOrEmpty(string layerName)
        {
            var parts = layerName.Split('.');
            return parts.Length >= 3 ? parts[3] : string.Empty;
        }
    }
}
