using System.Collections.Generic;
using PXToolBox.Core.Nesting;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Tools;

namespace PXToolBoxDev.Nesting
{
    /// <summary>Peças da seleção e tamanho da página ativa, em mm.</summary>
    public sealed class NestingSelection
    {
        public NestingSelection(IReadOnlyList<NestItem> items, double pageWidth, double pageHeight,
            IReadOnlyList<NestPoint>? origins = null, IReadOnlyList<string>? layers = null)
        {
            Items = items;
            PageWidth = pageWidth;
            PageHeight = pageHeight;
            Origins = origins;
            Layers = layers;
        }

        /// <summary>Camada de cada peça, para "Separar por camada". Null = não lida (tudo num grupo só).</summary>
        public IReadOnlyList<string>? Layers { get; }

        public IReadOnlyList<NestItem> Items { get; }
        public double PageWidth { get; }
        public double PageHeight { get; }

        /// <summary>Onde cada peça está agora (canto superior esquerdo da caixa, mm a partir do canto superior
        /// esquerdo da página), para "Comparar com o original". Null = não lido.</summary>
        public IReadOnlyList<NestPoint>? Origins { get; }

        /// <summary>Contornos das mesmas peças, lidos só quando o encaixe pelo contorno é pedido.</summary>
        public ContourReading? Contours { get; set; }
    }

    /// <summary>O que a janela do encaixe pede ao painel (ControlUI): ler a seleção e aplicar o
    /// resultado. A janela fica sem referências ao documento ou aos objetos.</summary>
    public interface INestingHost
    {
        /// <summary>Null (com a mensagem) sem documento ou seleção.</summary>
        NestingSelection? ReadSelection(out string message);

        /// <summary>Move/gira as peças, num passo de desfazer. Null = não rodou (o painel mostrou o motivo).</summary>
        /// <param name="items">As peças da seleção (uma por objeto), para conferir que ela não mudou.</param>
        /// <param name="sources">De qual objeto vem cada item do resultado (quantidades).</param>
        OperationResult? Apply(IReadOnlyList<NestItem> items, IReadOnlyList<int> sources, NestResult result, bool resizePage);

        /// <summary>Contornos da seleção atual. Null (com a mensagem) sem documento ou seleção.</summary>
        ContourReading? ReadContours(out string message);

        /// <summary>Aplica o encaixe pelo contorno, num passo de desfazer (confere de novo antes).</summary>
        OperationResult? ApplyContour(ContourReading reading, IReadOnlyList<int> sources, ContourNestResult result,
            double marginMm, double spacingMm, bool resizePage);

        /// <summary>Várias áreas pela caixa, num passo de desfazer.</summary>
        /// <param name="resizePage">Página = todas as áreas lado a lado.</param>
        /// <param name="drawAreas">Desenha um retângulo por área.</param>
        /// <param name="groupBlocks">Agrupa as peças de cada área (com o retângulo dela).</param>
        /// <param name="legends">Com <paramref name="groupBlocks"/>: uma legenda (nome, medidas, peças) acima de cada bloco.</param>
        OperationResult? ApplyAreas(IReadOnlyList<NestItem> items, IReadOnlyList<int> sources,
            AreaNestResult<NestResult, NestItem> result, bool resizePage, bool drawAreas, bool groupBlocks, bool legends);

        /// <summary>Várias áreas pelo contorno, num passo de desfazer (confere cada área antes).</summary>
        OperationResult? ApplyContourAreas(ContourReading reading, IReadOnlyList<int> sources,
            AreaNestResult<ContourNestResult, ContourPiece> result, double marginMm, double spacingMm, bool resizePage, bool drawAreas,
            bool groupBlocks, bool legends);
    }
}
