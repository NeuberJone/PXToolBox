using System.Collections.Generic;

namespace PXToolBox.Outputs.Abstractions
{
    /// <summary>Seleção atual do usuário no documento (ActiveSelectionRange).</summary>
    public interface ICorelSelection
    {
        IReadOnlyList<ICorelShape> Shapes { get; }

        int Count { get; }
    }
}
