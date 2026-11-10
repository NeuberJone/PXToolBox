using System.Collections.Generic;

namespace PXToolBox.Outputs.Abstractions
{
    /// <summary>Operações sobre um conjunto de objetos (Application.CreateShapeRange no VGCore).</summary>
    public interface IShapeArrangement
    {
        /// <summary>Agrupa os objetos num grupo NOVO (ShapeRange.Group) e devolve o grupo.</summary>
        ICorelShape Group(IReadOnlyList<ICorelShape> shapes);

        /// <summary>Torna estes objetos a seleção atual (ShapeRange.CreateSelection).</summary>
        void Select(IReadOnlyList<ICorelShape> shapes);
    }
}
