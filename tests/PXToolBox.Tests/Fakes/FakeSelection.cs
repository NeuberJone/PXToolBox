using System.Collections.Generic;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Tests.Fakes
{
    public sealed class FakeSelection : ICorelSelection
    {
        public List<ICorelShape> ShapesList { get; } = new List<ICorelShape>();

        public IReadOnlyList<ICorelShape> Shapes => ShapesList;

        public int Count => ShapesList.Count;
    }
}
