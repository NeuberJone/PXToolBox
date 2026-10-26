using System.Collections.Generic;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Tests.Fakes
{
    public sealed class FakeLayer : ICorelLayer
    {
        public string Name { get; set; } = string.Empty;

        public bool Visible { get; set; } = true;

        public bool Locked { get; set; }

        public bool IsSpecialLayer { get; set; }

        public List<ICorelShape> ShapesList { get; } = new List<ICorelShape>();

        public IReadOnlyList<ICorelShape> Shapes => ShapesList;

        public bool IsDeleted { get; private set; }

        public FakeLayer? MovedAboveOf { get; private set; }

        public void Delete() => IsDeleted = true;

        public void MoveAbove(ICorelLayer reference) => MovedAboveOf = (FakeLayer)reference;

        /// <summary>Adiciona um shape já vinculando a referência de volta (para MoveToLayer funcionar).</summary>
        public FakeShape AddShape(FakeShape shape)
        {
            shape.CurrentLayer = this;
            ShapesList.Add(shape);
            return shape;
        }
    }
}
