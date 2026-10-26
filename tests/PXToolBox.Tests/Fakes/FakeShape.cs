using System.Collections.Generic;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Tests.Fakes
{
    /// <summary>Implementação em memória de ICorelShape, usada para testar a lógica de Saídas sem COM.</summary>
    public sealed class FakeShape : ICorelShape
    {
        public string Name { get; set; } = string.Empty;

        public ShapeKind Kind { get; set; } = ShapeKind.Rectangle;

        public bool Visible { get; set; } = true;

        public bool Locked { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        // PositionX/Y e CenterX/Y são acoplados porque descrevem o MESMO shape: mover a posição
        // move o centro junto, igual no Corel real — necessário para que um teste com fakes
        // consiga detectar corretamente o efeito de GridPreparationService.ResizePageToFitAndCenter
        // (que translada via PositionX/Y mas mede a centralização via CenterX/Y).
        private double _positionX;
        public double PositionX
        {
            get => _positionX;
            set
            {
                CenterX += value - _positionX;
                _positionX = value;
            }
        }

        private double _positionY;
        public double PositionY
        {
            get => _positionY;
            set
            {
                CenterY += value - _positionY;
                _positionY = value;
            }
        }

        public double CenterX { get; set; }

        public double CenterY { get; set; }

        public string? LastAppliedStyle { get; private set; }

        public void ApplyStyleString(string style) => LastAppliedStyle = style;

        public List<ICorelShape> ShapesList { get; } = new List<ICorelShape>();

        public IReadOnlyList<ICorelShape> Shapes => ShapesList;

        public bool IsDeleted { get; private set; }

        /// <summary>Camada atual (usada só pelo fake, para MoveToLayer funcionar sem precisar de um "dono" externo).</summary>
        public FakeLayer? CurrentLayer { get; set; }

        public void Delete()
        {
            IsDeleted = true;
            CurrentLayer?.ShapesList.Remove(this);
        }

        public void MoveToLayer(ICorelLayer layer)
        {
            CurrentLayer?.ShapesList.Remove(this);
            var target = (FakeLayer)layer;
            target.ShapesList.Add(this);
            CurrentLayer = target;
        }
    }
}
