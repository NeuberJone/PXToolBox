using System.Collections.Generic;
using System.Linq;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Tests.Fakes
{
    /// <summary>
    /// Implementação em memória de ICorelPage. GetSelectableShapes() simula o comportamento
    /// nativo do Corel: só retorna shapes cuja camada E o próprio shape estejam visíveis e
    /// desbloqueados — é essa regra que valida o "caso crítico" do Grid com camadas ocultas.
    ///
    /// Width/Height também simulam um comportamento real do CorelDRAW confirmado por teste
    /// direto no Corel (não documentado em nenhuma referência, descoberto ao investigar
    /// "Preparar Grid não centraliza"): redimensionar a página desloca o referencial em que
    /// Shape.CenterX/CenterY são relidos — a página é redimensionada simetricamente em torno do
    /// seu próprio centro, então todo shape da página "aparenta" ter se movido metade da
    /// variação de tamanho, mesmo sem nenhum Move explícito. Sem simular isso aqui, um teste com
    /// fakes não seria capaz de detectar a regressão que esse comportamento causa em
    /// GridPreparationService (ver ResizePageToFitAndCenter).
    /// </summary>
    public sealed class FakePage : ICorelPage
    {
        public string Name { get; set; } = string.Empty;

        private double _width = 100;
        public double Width
        {
            get => _width;
            set
            {
                var delta = value - _width;
                _width = value;
                ShiftAllShapesCenterX(delta / 2);
            }
        }

        private double _height = 100;
        public double Height
        {
            get => _height;
            set
            {
                var delta = value - _height;
                _height = value;
                ShiftAllShapesCenterY(delta / 2);
            }
        }

        public double CenterX => _width / 2;

        public double CenterY => _height / 2;

        private void ShiftAllShapesCenterX(double delta)
        {
            foreach (var layer in LayersList)
            {
                foreach (var shape in layer.ShapesList.OfType<FakeShape>())
                {
                    shape.CenterX += delta;
                }
            }
        }

        private void ShiftAllShapesCenterY(double delta)
        {
            foreach (var layer in LayersList)
            {
                foreach (var shape in layer.ShapesList.OfType<FakeShape>())
                {
                    shape.CenterY += delta;
                }
            }
        }

        public List<FakeLayer> LayersList { get; } = new List<FakeLayer>();

        public IReadOnlyList<ICorelLayer> Layers => LayersList;

        public ICorelLayer? FindLayer(string name) =>
            LayersList.FirstOrDefault(l => l.Name == name);

        public ICorelLayer CreateLayer(string name)
        {
            var layer = new FakeLayer { Name = name };
            LayersList.Add(layer);
            return layer;
        }

        public ICorelShape Group(IEnumerable<ICorelShape> shapes)
        {
            var shapeList = shapes.ToList();
            var hostLayer = shapeList.OfType<FakeShape>().Select(s => s.CurrentLayer).FirstOrDefault(l => l != null)
                            ?? LayersList.FirstOrDefault();

            var group = new FakeShape { Kind = ShapeKind.Group };
            foreach (var shape in shapeList)
            {
                if (shape is FakeShape fakeShape)
                {
                    fakeShape.CurrentLayer?.ShapesList.Remove(fakeShape);
                    fakeShape.CurrentLayer = null;
                }

                group.ShapesList.Add(shape);
            }

            group.Width = shapeList.Count > 0 ? shapeList.Max(s => s.Width) : 0;
            group.Height = shapeList.Count > 0 ? shapeList.Max(s => s.Height) : 0;

            if (hostLayer != null)
            {
                hostLayer.AddShape(group);
            }

            return group;
        }

        public IReadOnlyList<ICorelShape> Ungroup(ICorelShape group)
        {
            var fakeGroup = (FakeShape)group;
            var hostLayer = fakeGroup.CurrentLayer;
            var children = fakeGroup.ShapesList.ToList();

            fakeGroup.Delete();

            foreach (var child in children)
            {
                if (child is FakeShape fakeChild && hostLayer != null)
                {
                    hostLayer.AddShape(fakeChild);
                }
            }

            return children;
        }

        public void CenterOnPage(IEnumerable<ICorelShape> shapes)
        {
            // Posicionamento real não é relevante para os testes de orquestração; registrar
            // chamada é suficiente caso algum teste queira inspecionar.
            CenterOnPageCallCount++;
        }

        public int CenterOnPageCallCount { get; private set; }

        public IReadOnlyList<ICorelShape> GetSelectableShapes() =>
            LayersList
                .Where(l => l.Visible && !l.Locked)
                .SelectMany(l => l.Shapes)
                .Where(s => s.Visible && !s.Locked)
                .ToList();
    }
}
