using System.Collections.Generic;
using PXToolBox.Core.Result;
using PXToolBox.Core.Slicing;
using PXToolBox.Outputs.Tools;

namespace PXToolBoxDev.Slicing
{
    /// <summary>O que a janela do Slice pede ao painel (ControlUI): medir a seleção e aplicar o plano.
    /// A janela continua sem referências ao documento ou aos objetos.</summary>
    public interface ISliceHost
    {
        /// <summary>Caixa da seleção atual, em mm; null (com a mensagem) sem documento ou seleção.</summary>
        (double Width, double Height)? MeasureSelection(out string message);

        /// <summary>Divide a seleção atual conforme o plano, num passo de desfazer. Null = não rodou
        /// (sem Corel/documento; o painel já mostrou o motivo).</summary>
        OperationResult? Apply(SlicePlan plan, double separationMm, bool deleteOriginal);

        /// <summary>Divide a seleção atual pelas linhas de corte livres (polígonos), num passo de desfazer.</summary>
        OperationResult? ApplyRegions(SliceRegionPlan plan, double separationMm, bool deleteOriginal);

        /// <summary>Emendas das guias que cruzam a seleção atual (mm da esquerda/topo). Null com a mensagem.</summary>
        SliceGuideCuts? ReadGuides(out string message);

        /// <summary>Guias "PX Slice" nas emendas (mm da esquerda/topo da seleção atual), num passo de desfazer.</summary>
        OperationResult? CreateGuides(IReadOnlyList<double> columns, IReadOnlyList<double> rows);

        /// <summary>Contorno da arte selecionada para a prévia (só leitura). Null com a mensagem.</summary>
        SliceArtOutline? ReadArtOutline(out string message);

        /// <summary>Apaga as guias "PX Slice" da página, num passo de desfazer.</summary>
        OperationResult? RemoveGuides();

        /// <summary>Linhas desenhadas na camada "Cortes (Slice)", em mm da arte selecionada (só leitura). Null com a mensagem.</summary>
        SliceDrawnCuts? ReadDrawnCuts(out string message);

        /// <summary>Cria a camada "Cortes (Slice)" na página, num passo de desfazer.</summary>
        OperationResult? CreateCutLayer();

        /// <summary>Miniatura com cores da arte selecionada (só leitura). Null com a mensagem.</summary>
        SliceThumbnail? ReadArtThumbnail(out string message);
    }
}
