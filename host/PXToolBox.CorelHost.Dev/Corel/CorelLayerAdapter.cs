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

        public ICorelShape CreateRectangle(double left, double bottom, double width, double height) =>
            new CorelShapeAdapter(Native.CreateRectangle2(left, bottom, width, height));

        /// <summary>Fonte não instalada: o Corel aplica outra sem erro — por isso a leitura de volta e a
        /// troca por Arial (mesma regra da TAG do MoldMotor).</summary>
        public ICorelShape CreateArtisticText(string text, string fontFamily, double sizePoints)
        {
            var shape = Native.CreateArtisticText(0, 0, text, Font: fontFamily, Size: (float)sizePoints);
            var story = shape.Text.Story;
            if (!string.Equals(story.Font, fontFamily, System.StringComparison.OrdinalIgnoreCase))
            {
                story.Font = "Arial";
                story.Size = (float)sizePoints;
            }

            return new CorelShapeAdapter(shape);
        }

        // O RCW: o CLR mantém um único RCW por ponteiro IUnknown, e a regra de identidade do COM
        // exige o mesmo IUnknown para o mesmo objeto — então ReferenceEquals só é verdadeiro
        // para o mesmo objeto COM. Se o Corel entregar objetos diferentes para a mesma camada,
        // o resultado é só "não encontrada", nunca outra camada (ver LayerVisibilityService).
        public object Identity => Native;

        // Cada acesso cria um adaptador novo — a igualdade aqui é página + índice
        // (Layer.Page.Index/Layer.Index, confirmados por reflexão sobre Corel.Interop.VGCore),
        // usada só para deduplicar DENTRO de uma operação (nenhuma reorganização acontece no
        // meio dela). Entre operações, a identidade é <see cref="Identity"/>.
        private (int Page, int Layer) Key => (Native.Page.Index, Native.Index);

        public override bool Equals(object? obj) => obj is CorelLayerAdapter other && Key.Equals(other.Key);

        public override int GetHashCode() => Key.GetHashCode();
    }
}
