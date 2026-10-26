using System.Collections.Generic;

namespace PXToolBox.Outputs.Abstractions
{
    /// <summary>
    /// Abstração de um "shape" do CorelDRAW, isolando a lógica de Saídas do COM/VGCore.
    /// A implementação real (CorelHost) traduz cada membro para chamadas de
    /// Corel.Interop.VGCore.Shape; os testes usam uma implementação em memória (fake).
    /// </summary>
    public interface ICorelShape
    {
        string Name { get; set; }

        ShapeKind Kind { get; }

        /// <summary>Visibilidade do próprio objeto (independe da visibilidade da camada).</summary>
        bool Visible { get; set; }

        bool Locked { get; set; }

        double Width { get; set; }

        double Height { get; set; }

        double PositionX { get; set; }

        double PositionY { get; set; }

        /// <summary>Centro geométrico — ao contrário de PositionX/Y, não depende do ReferencePoint do documento.</summary>
        double CenterX { get; }

        double CenterY { get; }

        /// <summary>
        /// Aplica uma string de estilo opaca (equivalente a Shape.Style.StringAssign no VGCore).
        /// Usada só por GridPreparationService para portar Utilidades.AplicaContornoGrid — não
        /// é uma API de estilo de propósito geral.
        /// </summary>
        void ApplyStyleString(string style);

        /// <summary>Subformas diretas (grupo ou PowerClip). Vazio para formas simples.</summary>
        IReadOnlyList<ICorelShape> Shapes { get; }

        void Delete();

        void MoveToLayer(ICorelLayer layer);
    }
}
