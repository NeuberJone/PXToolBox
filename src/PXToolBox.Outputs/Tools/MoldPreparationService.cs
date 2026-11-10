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

        /// <param name="standard">Antigo (padrão): camada "CAM1.Frente.MA.Frente", como a macro.
        /// Novo: "CAM1.MOLDE.MA.Frente" (LayerStandardService) e os logos L1..Ln mantêm o nome.</param>
        public OperationResult PrepareNewMold(ICorelPage page, LayerStandardService.Standard standard = LayerStandardService.Standard.Old)
        {
            _layerOrganization.DeleteEmptyLayers(page);

            foreach (var layer in page.Layers)
            {
                layer.Name = standard == LayerStandardService.Standard.New
                    ? NormalizeToNewStandard(layer.Name)
                    : NormalizeToFourSegments(layer.Name);
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
                        if (standard == LayerStandardService.Standard.New && LayerStandardService.IsLogoName(subItem.Name))
                        {
                            continue;
                        }

                        if (!SizeTokens.IsAllowed(subItem.Name))
                        {
                            subItem.Name = replacementName;
                            renamedSubItems++;
                        }
                    }
                }
            }

            var label = standard == LayerStandardService.Standard.New ? "padrão novo" : "padrão antigo";
            return OperationResult.Success($"Molde preparado ({label}). {renamedSubItems} subitem(ns) renomeado(s).");
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

        /// <summary>Padrão novo: "seg0.MOLDE.seg2.peça". Uma camada que já está no padrão novo fica
        /// como está; senão a peça é o 2º segmento, como em <see cref="NormalizeToFourSegments"/>.</summary>
        private static string NormalizeToNewStandard(string layerName)
        {
            var parts = layerName.Split('.');
            if (parts.Length >= 4 && LayerStandardService.IsNewSegment(parts[1]))
            {
                return string.Join(".", parts[0], LayerStandardService.NewStandardSegment, parts[2], parts[3]);
            }

            return parts.Length >= 3
                ? string.Join(".", parts[0], LayerStandardService.NewStandardSegment, parts[2], parts[1])
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
