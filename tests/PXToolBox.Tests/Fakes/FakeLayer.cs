using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Como no Corel: o retângulo novo entra na frente da camada.</summary>
        /// <summary>Quantos retângulos CreateRectangle cria antes de falhar (simula erro do Corel no meio).</summary>
        public int RectanglesBeforeFailure { get; set; } = int.MaxValue;

        public ICorelShape CreateRectangle(double left, double bottom, double width, double height)
        {
            if (RectanglesBeforeFailure-- <= 0) throw new System.InvalidOperationException("retângulo recusado");
            var rectangle = new FakeShape { Kind = ShapeKind.Rectangle, Width = width, Height = height, CenterX = left + width / 2, CenterY = bottom + height / 2, CurrentLayer = this };
            ShapesList.Insert(0, rectangle);
            return rectangle;
        }

        /// <summary>Como no Corel: o texto novo entra na frente da camada, em (0, 0). Tamanho
        /// aproximado: 1 unidade por ponto de altura, 0,6 por caractere de largura.</summary>
        public ICorelShape CreateArtisticText(string text, string fontFamily, double sizePoints)
        {
            var shape = new FakeShape
            {
                Kind = ShapeKind.Text, IsArtisticText = true, Text = text, FontFamily = fontFamily, FontSizePoints = sizePoints,
                Width = text.Length * sizePoints * 0.6, Height = sizePoints, CurrentLayer = this,
            };
            ShapesList.Insert(0, shape);
            return shape;
        }

        /// <summary>Como no Corel: a curva nova entra na frente da camada; caixa = a dos traços.</summary>
        /// <summary>Quantos polígonos CreatePolygon cria antes de falhar (simula erro do Corel no meio).</summary>
        public int PolygonsBeforeFailure { get; set; } = int.MaxValue;

        public ICorelShape CreatePolygon(IReadOnlyList<(double X, double Y)> points)
        {
            if (PolygonsBeforeFailure-- <= 0) throw new System.InvalidOperationException("polígono recusado");
            var shape = new FakeShape
            {
                Kind = ShapeKind.Curve, PolygonPoints = points.ToList(), CurrentLayer = this,
                Width = points.Max(p => p.X) - points.Min(p => p.X), Height = points.Max(p => p.Y) - points.Min(p => p.Y),
                CenterX = (points.Max(p => p.X) + points.Min(p => p.X)) / 2, CenterY = (points.Max(p => p.Y) + points.Min(p => p.Y)) / 2,
            };
            ShapesList.Insert(0, shape);
            return shape;
        }

        public ICorelShape CreateLines(IReadOnlyList<CurveLine> lines)
        {
            var xs = lines.SelectMany(l => new[] { l.X1, l.X2 }).ToList();
            var ys = lines.SelectMany(l => new[] { l.Y1, l.Y2 }).ToList();
            var shape = new FakeShape
            {
                Kind = ShapeKind.Curve, Lines = lines.ToList(), CurrentLayer = this,
                Width = xs.Max() - xs.Min(), Height = ys.Max() - ys.Min(),
                CenterX = (xs.Max() + xs.Min()) / 2, CenterY = (ys.Max() + ys.Min()) / 2,
            };
            ShapesList.Insert(0, shape);
            return shape;
        }

        /// <summary>Simula o Corel entregando um objeto novo a cada acesso (identidade instável).</summary>
        public bool UnstableIdentity { get; set; }

        /// <summary>Simula duas camadas vivas com a mesma identidade (ambiguidade).</summary>
        public object? SharedIdentity { get; set; }

        public object Identity => UnstableIdentity ? new object() : SharedIdentity ?? this;

        /// <summary>Adiciona um shape já vinculando a referência de volta (para MoveToLayer funcionar).</summary>
        public FakeShape AddShape(FakeShape shape)
        {
            shape.CurrentLayer = this;
            ShapesList.Add(shape);
            return shape;
        }
    }
}
