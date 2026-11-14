using System;
using System.Collections.Generic;

namespace PXToolBox.Outputs.Abstractions
{
    /// <summary>Ponto em coordenadas de PÁGINA, na unidade do documento.</summary>
    public readonly struct CurvePoint
    {
        public CurvePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }

        public bool IsNear(CurvePoint other, double tolerance) =>
            Math.Abs(X - other.X) <= tolerance && Math.Abs(Y - other.Y) <= tolerance;
    }

    /// <summary>Um segmento de <c>Curve.Segments</c>, pelo índice. Só segmento curvo tem pontos de
    /// controle; um reto é definido pelos nós (escrever pontos de controle nele lança no Corel).</summary>
    /// <summary>Trecho reto de (X1, Y1) a (X2, Y2), na unidade do documento.</summary>
    public readonly struct CurveLine
    {
        public CurveLine(double x1, double y1, double x2, double y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public double X1 { get; }

        public double Y1 { get; }

        public double X2 { get; }

        public double Y2 { get; }
    }

    public sealed class CurveSegment
    {
        private CurveSegment(bool isCurve, CurvePoint startControl, CurvePoint endControl)
        {
            IsCurve = isCurve;
            StartControl = startControl;
            EndControl = endControl;
        }

        public bool IsCurve { get; }

        public CurvePoint StartControl { get; }

        public CurvePoint EndControl { get; }

        public static CurveSegment Line() => new CurveSegment(false, default, default);

        public static CurveSegment Curve(CurvePoint startControl, CurvePoint endControl) =>
            new CurveSegment(true, startControl, endControl);
    }

    /// <summary>
    /// Geometria completa de uma curva no momento da leitura: subcaminhos, nós e cada segmento no
    /// seu índice. Mecanismo do "Zerar ângulo" do MoldMotor, validado no CorelDRAW: os nós de
    /// <c>Curve</c> já estão em coordenadas de página, então zerar a rotação e regravar
    /// exatamente estes valores preserva a aparência — sem nenhuma matriz.
    /// </summary>
    public sealed class CurveGeometry
    {
        public CurveGeometry(int subPathCount, IReadOnlyList<CurvePoint> nodes, IReadOnlyList<CurveSegment> segments)
        {
            SubPathCount = subPathCount;
            Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            Segments = segments ?? throw new ArgumentNullException(nameof(segments));
        }

        public int SubPathCount { get; }

        public IReadOnlyList<CurvePoint> Nodes { get; }

        public IReadOnlyList<CurveSegment> Segments { get; }

        /// <summary>Mesma estrutura (contagens e tipo de cada segmento) e cada nó e ponto de controle
        /// dentro da tolerância, comparado só dentro do próprio segmento.</summary>
        public bool Matches(CurveGeometry other, double tolerance)
        {
            if (other == null
                || SubPathCount != other.SubPathCount
                || Nodes.Count != other.Nodes.Count
                || Segments.Count != other.Segments.Count)
            {
                return false;
            }

            for (var i = 0; i < Nodes.Count; i++)
            {
                if (!Nodes[i].IsNear(other.Nodes[i], tolerance)) return false;
            }

            for (var i = 0; i < Segments.Count; i++)
            {
                var mine = Segments[i];
                var theirs = other.Segments[i];
                if (mine.IsCurve != theirs.IsCurve) return false;
                if (mine.IsCurve && (!mine.StartControl.IsNear(theirs.StartControl, tolerance) || !mine.EndControl.IsNear(theirs.EndControl, tolerance)))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Uma entrada de <c>Shape.Properties</c> (nome, ID e valor bruto).</summary>
    public sealed class ShapePropertyEntry
    {
        public ShapePropertyEntry(string name, int id, object? value)
        {
            Name = name;
            Id = id;
            Value = value;
        }

        public string Name { get; }

        public int Id { get; }

        public object? Value { get; }
    }
}
