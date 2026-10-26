using PXToolBox.Core.Naming;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Alterar Tipo de Peça". Ported de Correcoes.RenomeiaTipoDePeca (CorelMacros/Correcoes.bas).
    /// A escolha do tipo de origem (ou "Tudo") e do tipo de destino é responsabilidade do
    /// host/UI; aqui chega já resolvida.
    /// </summary>
    public sealed class PieceTypeRenameService
    {
        /// <param name="fromCode">Tipo de origem, ou null para alterar qualquer tipo válido ("Tudo").</param>
        public OperationResult<int> Rename(ICorelDocument document, PieceTypeCode? fromCode, PieceTypeCode toCode)
        {
            var renamedCount = 0;

            foreach (var page in document.Pages)
            {
                foreach (var layer in page.Layers)
                {
                    var layerName = new LayerName(layer.Name);
                    if (!layerName.HasAtLeastSegments(2))
                    {
                        continue;
                    }

                    var first = layerName.FirstSegment ?? string.Empty;

                    var matches = fromCode == null
                        ? PieceTypeCode.TryParse(first, out _)
                        : string.Equals(first, fromCode.Code, System.StringComparison.OrdinalIgnoreCase);

                    if (!matches)
                    {
                        continue;
                    }

                    layer.Name = layerName.WithFirstSegment(toCode.Code);
                    renamedCount++;
                }
            }

            return OperationResult<int>.Success(renamedCount, $"{renamedCount} camada(s) renomeada(s) para {toCode.Code}.");
        }

        /// <summary>
        /// "Alterar tipo dos objetos selecionados". Reconhece o tipo de origem pelo NOME de cada
        /// objeto selecionado, usando a mesma convenção pontuada de <see cref="LayerName"/>
        /// (primeiro segmento = tipo de peça) já usada para camadas — convenção real desta base
        /// de código, não inventada aqui: é exatamente o nome que
        /// <see cref="LayerOrganizationService.RenameShapesWithLayerName"/>
        /// (Correcoes.RenomearObjetosComNomeDaCamada) grava nos objetos. Objetos cujo nome não
        /// tem pelo menos 2 segmentos pontuados, ou cujo primeiro segmento não é um
        /// <see cref="PieceTypeCode"/> válido, são ignorados (contados, nunca alterados "no
        /// escuro"). Só o componente de tipo de peça muda; o resto do nome é preservado.
        /// </summary>
        public OperationResult RenameSelectedObjects(ICorelSelection selection, PieceTypeCode toCode)
        {
            if (selection.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Nenhum objeto selecionado.");
            }

            var renamed = 0;
            var ignored = 0;
            foreach (var shape in selection.Shapes)
            {
                var name = new LayerName(shape.Name);
                if (!name.HasAtLeastSegments(2) || !PieceTypeCode.TryParse(name.FirstSegment ?? string.Empty, out _))
                {
                    ignored++;
                    continue;
                }

                shape.Name = name.WithFirstSegment(toCode.Code);
                renamed++;
            }

            return OperationResult.Success(
                $"{renamed} objeto(s) alterado(s) para {toCode.Code}; {ignored} ignorado(s) por formato incompatível.");
        }

        /// <summary>
        /// "Alterar tipo das camadas selecionadas". A API do CorelDRAW (Corel.Interop.VGCore) não
        /// expõe multi-seleção de camadas do Gerenciador de Objetos — confirmado por reflexão
        /// direta sobre a assembly: só existe <c>Document.ActiveLayer</c> (singular). Por decisão
        /// explícita do usuário, esta ação atua SÓ na camada ativa — nunca em "todas as camadas"
        /// nem nas "camadas dos objetos selecionados" como substituição silenciosa. A UI precisa
        /// deixar este alcance explícito (não é multi-seleção).
        /// </summary>
        public OperationResult RenameActiveLayer(ICorelLayer? activeLayer, PieceTypeCode toCode)
        {
            if (activeLayer == null)
            {
                return OperationResult.Fail(OperationStatus.InconclusiveRead, "Nenhuma camada ativa.");
            }

            var name = new LayerName(activeLayer.Name);
            if (!name.HasAtLeastSegments(2) || !PieceTypeCode.TryParse(name.FirstSegment ?? string.Empty, out _))
            {
                return OperationResult.Fail(
                    OperationStatus.InconclusiveRead,
                    $"A camada ativa (\"{activeLayer.Name}\") não tem nome no formato <TIPO>.<resto> reconhecido.");
            }

            var newName = name.WithFirstSegment(toCode.Code);
            activeLayer.Name = newName;
            return OperationResult.Success($"Camada ativa renomeada para \"{newName}\".");
        }
    }
}
