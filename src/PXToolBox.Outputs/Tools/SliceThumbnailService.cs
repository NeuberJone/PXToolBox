using System;
using System.IO;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>Miniatura com cores da arte selecionada: PNG e o tamanho da seleção em mm (a imagem cobre a caixa dela).</summary>
    public sealed class SliceThumbnail
    {
        public SliceThumbnail(byte[] png, double widthMm, double heightMm)
        {
            Png = png;
            WidthMm = widthMm;
            HeightMm = heightMm;
        }

        public byte[] Png { get; }
        public double WidthMm { get; }
        public double HeightMm { get; }
    }

    /// <summary>
    /// "Dividir em partes (Slice)" — miniatura com cores na prévia (S2 de docs/reference-feature-roadmap.md). O Corel
    /// exporta só a seleção num PNG temporário (<see cref="ICorelDocument.ExportSelectionPng"/>, lado maior com
    /// <see cref="LongestSidePixels"/>), o PX lê os bytes e apaga o arquivo. Não muda o documento.
    /// </summary>
    public sealed class SliceThumbnailService
    {
        public const int LongestSidePixels = 600;

        /// <param name="folder">Pasta para o arquivo temporário (criada se preciso).</param>
        public OperationResult<SliceThumbnail> Read(ICorelDocument document, ICorelSelection selection, double millimeter, string folder)
        {
            if (selection.Count == 0)
                return OperationResult<SliceThumbnail>.Fail(OperationStatus.InvalidSelection, "Selecione a arte que vai ser dividida.");
            var (_, _, width, height) = SliceGuideService.Box(selection, millimeter);
            if (!(width > 0) || !(height > 0))
                return OperationResult<SliceThumbnail>.Fail(OperationStatus.InvalidSelection, "A seleção não tem largura e altura para a miniatura.");

            var scale = LongestSidePixels / Math.Max(width, height);
            var pixelsWide = Math.Max(1, (int)Math.Round(width * scale));
            var pixelsHigh = Math.Max(1, (int)Math.Round(height * scale));
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"px-slice-{Guid.NewGuid():N}.png");
            try
            {
                document.ExportSelectionPng(path, pixelsWide, pixelsHigh);
                if (!File.Exists(path))
                    return OperationResult<SliceThumbnail>.Fail(OperationStatus.ExecutionFailed, "O Corel não gerou a miniatura da arte.");
                return OperationResult<SliceThumbnail>.Success(new SliceThumbnail(File.ReadAllBytes(path), width, height));
            }
            finally
            {
                try { File.Delete(path); }
                catch (IOException) { /* fica na pasta temporária */ }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
