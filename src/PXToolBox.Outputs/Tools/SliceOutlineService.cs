using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Nesting;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>Contorno da arte para a prévia do Slice: anéis em mm a partir do canto superior esquerdo
    /// da caixa da seleção (Y para baixo), a mesma origem das emendas do plano.</summary>
    public sealed class SliceArtOutline
    {
        public SliceArtOutline(IReadOnlyList<IReadOnlyList<NestPoint>> rings, double width, double height, bool approximate, bool trimmed)
        {
            Rings = rings;
            Width = width;
            Height = height;
            Approximate = approximate;
            Trimmed = trimmed;
        }

        public IReadOnlyList<IReadOnlyList<NestPoint>> Rings { get; }

        /// <summary>Caixa da seleção em mm (a mesma que o Slice mede).</summary>
        public double Width { get; }

        public double Height { get; }

        /// <summary>Alguma parte entrou pela caixa (texto, imagem ou contorno ilegível).</summary>
        public bool Approximate { get; }

        /// <summary>Passou de <see cref="SliceOutlineService.MaximumPoints"/>: só os primeiros anéis.</summary>
        public bool Trimmed { get; }
    }

    /// <summary>
    /// Lê o contorno da arte selecionada para a "miniatura" da janela do Slice — só leitura, nada é
    /// alterado. Usa a mesma leitura do encaixe pelo contorno (<see cref="NestingService.ReadContours"/>:
    /// Bézier em retas, grupos somados, texto/imagem pela caixa).
    /// </summary>
    public sealed class SliceOutlineService
    {
        /// <summary>Limite de pontos desenhados, para uma arte pesada não travar a janela.</summary>
        public const int MaximumPoints = 20000;

        private readonly NestingService _nesting;

        public SliceOutlineService(NestingService nesting) => _nesting = nesting;

        public OperationResult<SliceArtOutline> Read(ICorelSelection selection, double millimeter)
        {
            if (selection.Count == 0)
                return OperationResult<SliceArtOutline>.Fail(OperationStatus.InvalidSelection, "Selecione a arte que vai ser dividida.");
            var (left, top, width, height) = SliceGuideService.Box(selection, millimeter);
            var reading = _nesting.ReadContours(selection, millimeter);
            var originX = left / millimeter;
            var originY = -top / millimeter; // os contornos vêm com Y para baixo (−y / mm)
            var rings = new List<IReadOnlyList<NestPoint>>();
            var points = 0;
            var trimmed = false;
            foreach (var ring in reading.Pieces.SelectMany(piece => piece.Shapes).SelectMany(shape => shape))
            {
                if (points + ring.Count > MaximumPoints)
                {
                    trimmed = true;
                    break;
                }

                points += ring.Count;
                rings.Add(ring.Select(p => new NestPoint(p.X - originX, p.Y - originY)).ToList());
            }

            return OperationResult<SliceArtOutline>.Success(new SliceArtOutline(rings, width, height, reading.BoxFallbacks.Count > 0, trimmed));
        }
    }
}
