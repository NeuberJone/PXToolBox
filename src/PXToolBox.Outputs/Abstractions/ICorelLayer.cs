using System.Collections.Generic;

namespace PXToolBox.Outputs.Abstractions
{
    /// <summary>
    /// Abstração de uma camada (layer). <see cref="Shapes"/> retorna sempre TODOS os objetos
    /// diretos da camada, independentemente de estarem visíveis — a decisão de incluir ou não
    /// objetos ocultos é da ferramenta (ver GridPreparationService e o caso crítico documentado
    /// em docs/ARCHITECTURE.md), nunca da abstração.
    /// </summary>
    public interface ICorelLayer
    {
        string Name { get; set; }

        bool Visible { get; set; }

        bool Locked { get; set; }

        /// <summary>Camadas especiais do Corel (guias, etc.) nunca devem ser apagadas por engano.</summary>
        bool IsSpecialLayer { get; }

        IReadOnlyList<ICorelShape> Shapes { get; }

        void Delete();

        void MoveAbove(ICorelLayer reference);
    }
}
