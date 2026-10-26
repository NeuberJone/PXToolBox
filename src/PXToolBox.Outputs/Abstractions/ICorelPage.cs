using System.Collections.Generic;

namespace PXToolBox.Outputs.Abstractions
{
    public interface ICorelPage
    {
        string Name { get; set; }

        double Width { get; set; }

        double Height { get; set; }

        double CenterX { get; }

        double CenterY { get; }

        IReadOnlyList<ICorelLayer> Layers { get; }

        ICorelLayer? FindLayer(string name);

        ICorelLayer CreateLayer(string name);

        /// <summary>
        /// Agrupa as formas indicadas (equivalente a ActiveSelection.Group após CreateSelection)
        /// e retorna o grupo resultante. A escolha de QUAIS formas entram na lista é da ferramenta
        /// que chama este método — esta abstração nunca filtra por visibilidade por conta própria.
        /// </summary>
        ICorelShape Group(IEnumerable<ICorelShape> shapes);

        /// <summary>Desagrupa e retorna as formas resultantes (equivalente a ShapeRange.UngroupEx).</summary>
        IReadOnlyList<ICorelShape> Ungroup(ICorelShape group);

        /// <summary>Centraliza as formas indicadas em relação à página (AlignAndDistribute center/center).</summary>
        void CenterOnPage(IEnumerable<ICorelShape> shapes);

        /// <summary>
        /// Equivalente a "Shapes.All.CreateSelection": as formas atualmente selecionáveis na
        /// página. No CorelDRAW real, uma forma numa camada oculta ou bloqueada NÃO pode ser
        /// selecionada — é exatamente esse comportamento nativo do Corel (não uma regra inventada
        /// aqui) que faz com que, ao ocultar as camadas FE e C antes de "Preparar Grid", os
        /// objetos dessas camadas sejam automaticamente excluídos do agrupamento/centralização,
        /// mas permaneçam intactos em sua camada (ver GridPreparationService e
        /// docs/mvp-smoke-test.md, caso crítico "Grid com camadas ocultas"). A implementação real
        /// (CorelHost) delega para a seleção nativa do Corel; não deve filtrar por conta própria.
        /// </summary>
        IReadOnlyList<ICorelShape> GetSelectableShapes();
    }
}
