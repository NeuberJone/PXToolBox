using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Result;
using PXToolBox.Core.Slicing;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>Linhas de corte desenhadas, em mm a partir do canto superior esquerdo da arte selecionada (Y para baixo).</summary>
    public sealed class SliceDrawnCuts
    {
        public SliceDrawnCuts(IReadOnlyList<IReadOnlyList<SlicePoint>> paths, int unreadable)
        {
            Paths = paths;
            Unreadable = unreadable;
        }

        public IReadOnlyList<IReadOnlyList<SlicePoint>> Paths { get; }

        /// <summary>Objetos da camada sem contorno legível (texto, imagem), ignorados.</summary>
        public int Unreadable { get; }
    }

    /// <summary>
    /// "Dividir em partes (Slice)" pelas linhas desenhadas (<see cref="SlicePathPlanner"/>): as linhas ficam numa camada
    /// própria, <see cref="LayerName"/>, fora da seleção — a seleção continua sendo só a arte. Cada objeto da camada
    /// (dentro de grupos também) vale pelo contorno desenhado: cada subcaminho é uma linha, reta ou curva (Bézier em
    /// retas). Ler não muda nada; criar a camada é um passo de desfazer.
    /// </summary>
    public sealed class SliceDrawnCutService
    {
        public const string LayerName = "Cortes (Slice)";

        public OperationResult<SliceDrawnCuts> ReadCuts(ICorelSelection selection, ICorelPage page, double millimeter)
        {
            if (selection.Count == 0)
                return OperationResult<SliceDrawnCuts>.Fail(OperationStatus.InvalidSelection, "Selecione a arte que vai ser dividida.");
            var layer = page.FindLayer(LayerName);
            if (layer == null)
                return OperationResult<SliceDrawnCuts>.Fail(OperationStatus.InvalidSelection,
                    $"Não há a camada “{LayerName}” nesta página: clique em “Criar camada de cortes”, desenhe as linhas nela e leia de novo.");

            var (left, top, _, _) = SliceGuideService.Box(selection, millimeter);
            var paths = new List<IReadOnlyList<SlicePoint>>();
            var unreadable = 0;
            void Collect(ICorelShape shape)
            {
                if (shape.Kind == ShapeKind.Group && !shape.HasPowerClipContents)
                {
                    foreach (var child in shape.Shapes) Collect(child);
                    return;
                }

                var elements = shape.Kind == ShapeKind.Text || shape.Kind == ShapeKind.Bitmap ? null : shape.ReadOutline();
                if (elements == null)
                {
                    unreadable++;
                    return;
                }

                // OutlinePaths: mm com Y para baixo a partir da origem do documento → mm da arte.
                foreach (var path in NestingService.OutlinePaths(elements, millimeter))
                    paths.Add(path.Select(p => new SlicePoint(p.X - left / millimeter, p.Y + top / millimeter)).ToList());
            }

            foreach (var shape in layer.Shapes) Collect(shape);
            if (paths.Count == 0)
                return OperationResult<SliceDrawnCuts>.Fail(OperationStatus.InvalidSelection,
                    $"A camada “{LayerName}” não tem linhas: desenhe os cortes nela (reta, curva ou Bézier) por cima da arte e leia de novo.");
            return OperationResult<SliceDrawnCuts>.Success(new SliceDrawnCuts(paths, unreadable));
        }

        /// <summary>Cria a camada das linhas de corte (se ainda não existe). As linhas desenhadas nela não são impressas
        /// nem divididas: o Slice só lê o traçado.</summary>
        public OperationResult CreateLayer(ICorelPage page)
        {
            if (page.FindLayer(LayerName) != null)
                return OperationResult.Success($"A camada “{LayerName}” já existe: escolha-a no Gerenciador de objetos e desenhe as linhas de corte por cima da arte.");
            page.CreateLayer(LayerName);
            return OperationResult.Success($"Camada “{LayerName}” criada: escolha-a no Gerenciador de objetos, desenhe as linhas de corte por cima da arte, selecione a arte e clique em “Ler as linhas”. Ctrl+Z desfaz.");
        }
    }
}
