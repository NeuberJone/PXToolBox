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

        /// <summary>Retângulo novo nesta camada (Layer.CreateRectangle2), na unidade do documento,
        /// a partir do canto inferior esquerdo.</summary>
        ICorelShape CreateRectangle(double left, double bottom, double width, double height);

        /// <summary>Texto artístico novo nesta camada (Layer.CreateArtisticText), na frente, com a
        /// fonte pedida — ou Arial, se ela não estiver instalada. A posição é ajustada por quem chama.</summary>
        ICorelShape CreateArtisticText(string text, string fontFamily, double sizePoints);

        /// <summary>
        /// Identidade opaca da camada durante a sessão — no host, o próprio objeto COM (RCW).
        /// Só pode ser comparada com <see cref="object.ReferenceEquals"/>; nunca invocada nem
        /// convertida. Não sobrevive ao fechamento do Corel e nunca é gravada no documento.
        /// </summary>
        object Identity { get; }
    }
}
