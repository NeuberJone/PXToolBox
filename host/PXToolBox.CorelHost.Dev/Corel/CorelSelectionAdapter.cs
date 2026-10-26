using System.Collections.Generic;
using Corel.Interop.VGCore;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBoxDev.Corel
{
    internal sealed class CorelSelectionAdapter : ICorelSelection
    {
        private readonly ShapeRange _native;

        public CorelSelectionAdapter(ShapeRange native)
        {
            _native = native;
        }

        public IReadOnlyList<ICorelShape> Shapes => CorelCollections.ToShapeList(_native);

        public int Count => _native?.Count ?? 0;
    }
}
