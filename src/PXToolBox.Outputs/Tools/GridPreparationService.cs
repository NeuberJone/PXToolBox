using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Preparar Grid" e "Preparar Grid Novo".
    ///
    /// Ported da revisão candidata em Referência/Macros/modulos/Grid.bas e FluxoGrid.bas — uma
    /// reescrita deliberada do fluxo legado (Grid.bas original em CorelMacros), documentada em
    /// Referência/Macros/LEIA-ME.md. Principais diferenças em relação ao fluxo legado antigo
    /// (que agrupava/desagrupava e dependia de CreateSelection):
    ///
    /// 1. Valida ANTES de qualquer mutação: nenhuma camada não-especial com objetos pode estar
    ///    bloqueada (Layer.Editable = false). Se estiver, falha imediatamente sem tocar no
    ///    documento.
    /// 2. Torna TODAS as camadas não-especiais temporariamente visíveis, coleta todos os seus
    ///    objetos diretamente (sem Group/Ungroup, sem mover entre camadas) e translada o
    ///    conjunto inteiro para o centro da página redimensionada. Isto INCLUI objetos de
    ///    camadas originalmente ocultas (ex.: FE, C) — eles participam do Grid.
    /// 3. Restaura a visibilidade original de cada camada sempre, inclusive em falha (try/finally).
    /// 4. Só exclui camadas vazias DEPOIS de restaurar a visibilidade, e só se não houve erro.
    ///
    /// Isto substitui a leitura anterior deste serviço (que excluía camadas ocultas do
    /// agrupamento via GetSelectableShapes). A revisão candidata é explícita: FE/MA/C ocultas
    /// DEVEM participar do Grid. Ver docs/ARCHITECTURE.md.
    /// </summary>
    public sealed class GridPreparationService
    {
        private readonly LayerOrganizationService _layerOrg = new LayerOrganizationService();

        /// <summary>
        /// Estilo de contorno cravado em Utilidades.AplicaContornoGrid — opaco de propósito,
        /// só repassado para Shape.Style.StringAssign pelo host. Usado por PrepareNewGrid.
        /// </summary>
        private const string GridOutlineStyleJson =
            "{\"fill\":{\"overprint\":\"0\",\"secondaryColor\":\"CMYK,USER,0,0,0,0,100,00000000-0000-0000-0000-000000000000\",\"type\":\"1\",\"primaryColor\":\"CMYK255,USER,0,0,0,0,100,cccd19cb-4675-4a5e-8bda-d0bbbaab8af0\"},\"outline\":{\"overprint\":\"0\",\"type\":\"0\",\"angle\":\"0\",\"screenSpec\":\"0,0,45000000,60,0\",\"overlapArrow\":\"0\",\"leftArrowAttributes\":\"0|0|0|0|0|0|0\",\"dotLength\":\"0\",\"width\":\"10000\",\"dashDotSpec\":\"0\",\"rightArrow\":\"|0\",\"endCaps\":\"0\",\"rightArrowAttributes\":\"0|0|0|0|0|0|0\",\"variableAttributes\":\"0|0\",\"scaleWithObject\":\"0\",\"projectMatrix\":\"1,0,0,0,1,0\",\"miterLimit\":\"5\",\"matrix\":\"1,0,0,0,1,0\",\"aspect\":\"100\",\"color\":\"CMYK255,USER,0,0,0,255,100,00000000-0000-0000-0000-000000000000\",\"behindFill\":\"0\",\"shareArrow\":\"0\",\"joinType\":\"0\",\"leftArrow\":\"|0\",\"dashAdjust\":\"0\",\"justification\":\"1\"},\"transparency\":{}}";

        public OperationResult PrepareGrid(ICorelDocument document)
        {
            var page = document.ActivePage;
            var workingLayers = page.Layers.Where(l => !l.IsSpecialLayer).ToList();

            // 1. Validação ANTES de qualquer mutação.
            foreach (var layer in workingLayers)
            {
                if (layer.Shapes.Count > 0 && layer.Locked)
                {
                    return OperationResult.Fail(
                        OperationStatus.InvalidSelection,
                        $"Desbloqueie a camada antes de preparar o Grid: {layer.Name}");
                }
            }

            var originalVisibility = workingLayers.Select(l => (Layer: l, Visible: l.Visible)).ToList();
            OperationResult result;
            try
            {
                // 2. Mostra tudo temporariamente para capturar e transladar o conjunto completo.
                foreach (var layer in workingLayers)
                {
                    layer.Visible = true;
                }

                var shapes = workingLayers.SelectMany(l => l.Shapes).ToList();
                if (shapes.Count == 0)
                {
                    result = OperationResult.Success("Nenhum objeto na página para processar.");
                }
                else
                {
                    ResizePageToFitAndCenter(page, shapes);
                    _layerOrg.RenameShapesAfterFirstDot(page);
                    page.Name = "DESIGN";
                    RenameDocument(document, "SISBolt_");
                    _layerOrg.OrganizeLayersByPieceAndCategory(page);
                    result = OperationResult.Success("Grid preparado.");
                }
            }
            finally
            {
                // 3. Restaura a visibilidade original, sempre — inclusive se algo lançou acima.
                foreach (var (layer, visible) in originalVisibility)
                {
                    layer.Visible = visible;
                }
            }

            // 4. Só exclui camadas vazias depois de restaurar os estados, e só em sucesso.
            if (result.IsSuccess)
            {
                _layerOrg.DeleteEmptyLayers(page);
            }

            return result;
        }

        /// <summary>
        /// "Preparar Grid Novo". Ported de FluxoGrid.ConfiguraNovoGrid (revisão candidata) — o
        /// próprio fluxo (anterior ao PreparaGrid) não foi reescrito nesta revisão, incluindo o
        /// passo de aplicar Utilidades.AplicaContornoGrid, preservado aqui.
        /// </summary>
        public OperationResult PrepareNewGrid(ICorelDocument document)
        {
            // Desagrupa grupos diretos de cada camada, em TODAS as páginas (um nível, não
            // recursivo — fiel ao original).
            foreach (var docPage in document.Pages)
            {
                foreach (var layer in docPage.Layers)
                {
                    foreach (var shape in layer.Shapes.Where(s => s.Kind == ShapeKind.Group).ToList())
                    {
                        docPage.Ungroup(shape);
                    }
                }
            }

            _layerOrg.DeleteEmptyLayers(document.ActivePage);
            _layerOrg.DeleteGridHelperShapes(document);
            ApplyGridOutlineStyle(document);
            _layerOrg.TrimLayerNamesToThreeSegments(document);

            return PrepareGrid(document);
        }

        private static void ApplyGridOutlineStyle(ICorelDocument document)
        {
            foreach (var page in document.Pages)
            {
                foreach (var layer in page.Layers)
                {
                    foreach (var shape in layer.Shapes)
                    {
                        shape.ApplyStyleString(GridOutlineStyleJson);
                    }
                }
            }
        }

        /// <summary>
        /// Redimensiona a página à caixa delimitadora combinada do conjunto (+30 unidades do
        /// documento, sem conversão) e translada tudo para o novo centro da página. Usa
        /// CenterX/CenterY (não PositionX/Y) para a caixa delimitadora, porque Center não
        /// depende do ReferencePoint do documento.
        ///
        /// CORRIGIDO (causa confirmada por teste direto no CorelDRAW real, não suposição):
        /// redimensionar a página (<c>Page.SizeWidth</c>/<c>SizeHeight</c>) desloca o referencial
        /// em que CenterX/CenterY dos SHAPES são relidos — CorelDRAW recalcula a página em torno
        /// do centro atual dela, então o centro da página ANTES do resize e o centro relido pelos
        /// shapes DEPOIS do resize não são mais o mesmo referencial. Reaproveitar um centro
        /// combinado calculado ANTES do resize (como a versão anterior fazia) produz um
        /// deslocamento errado — exatamente o sintoma relatado ("Preparar Grid não centraliza").
        /// A macro VBA original nunca sofreu disso porque relê `conjunto.CenterX`/`CenterY`
        /// (propriedades COM ao vivo do ShapeRange) só no próprio `.Move`, já depois do resize —
        /// aqui replicado relendo a caixa delimitadora dos shapes DEPOIS de redimensionar a
        /// página, nunca reaproveitando o valor de antes.
        /// </summary>
        private static void ResizePageToFitAndCenter(ICorelPage page, IReadOnlyList<ICorelShape> shapes)
        {
            var before = ComputeBounds(shapes);
            page.Width = (before.Right - before.Left) + 30;
            page.Height = (before.Top - before.Bottom) + 30;

            var after = ComputeBounds(shapes);
            var dx = page.CenterX - (after.Left + after.Right) / 2;
            var dy = page.CenterY - (after.Bottom + after.Top) / 2;

            foreach (var shape in shapes)
            {
                shape.PositionX += dx;
                shape.PositionY += dy;
            }
        }

        private static (double Left, double Right, double Bottom, double Top) ComputeBounds(IReadOnlyList<ICorelShape> shapes)
        {
            var left = shapes.Min(s => s.CenterX - s.Width / 2);
            var right = shapes.Max(s => s.CenterX + s.Width / 2);
            var bottom = shapes.Min(s => s.CenterY - s.Height / 2);
            var top = shapes.Max(s => s.CenterY + s.Height / 2);
            return (left, right, bottom, top);
        }

        private static void RenameDocument(ICorelDocument document, string candidateName)
        {
            // Correcoes.RenomearDocumentoAtual: só aplica o prefixo "SISBolt_" se o nome atual
            // ainda for o padrão "Sem título-N"; senão mantém o nome atual (sem a extensão .cdr).
            if (candidateName != "SISBolt_")
            {
                document.Name = candidateName;
                return;
            }

            if (document.Name.StartsWith("Sem título-", System.StringComparison.OrdinalIgnoreCase))
            {
                document.Name = "SISBolt_";
            }
            else
            {
                document.Name = document.Name.Replace(".cdr", string.Empty);
            }
        }
    }
}
