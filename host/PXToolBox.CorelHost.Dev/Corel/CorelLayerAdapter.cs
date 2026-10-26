using System.Collections.Generic;
using Corel.Interop.VGCore;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBoxDev.Corel
{
    internal sealed class CorelLayerAdapter : ICorelLayer
    {
        internal Layer Native { get; }

        public CorelLayerAdapter(Layer native)
        {
            Native = native;
        }

        public string Name
        {
            get => Native.Name ?? string.Empty;
            set => Native.Name = value;
        }

        public bool Visible
        {
            get => Native.Visible;
            set => Native.Visible = value;
        }

        /// <summary>O VGCore não tem "Locked" em Layer — o equivalente é "Editable" (invertido).</summary>
        public bool Locked
        {
            get => !Native.Editable;
            set => Native.Editable = !value;
        }

        public bool IsSpecialLayer => Native.IsSpecialLayer;

        public IReadOnlyList<ICorelShape> Shapes => CorelCollections.ToShapeList(Native.Shapes);

        public void Delete() => Native.Delete();

        public void MoveAbove(ICorelLayer reference) => Native.MoveAbove(((CorelLayerAdapter)reference).Native);
    }
}
