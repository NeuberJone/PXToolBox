using System.Collections.Generic;
using Corel.Interop.VGCore;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBoxDev.Corel
{
    internal sealed class CorelShapeAdapter : ICorelShape
    {
        internal Shape Native { get; }

        public CorelShapeAdapter(Shape native)
        {
            Native = native;
        }

        public string Name
        {
            get => Native.Name ?? string.Empty;
            set => Native.Name = value;
        }

        public ShapeKind Kind => MapKind(Native.Type);

        public bool Visible
        {
            get => Native.Visible;
            set => Native.Visible = value;
        }

        public bool Locked
        {
            get => Native.Locked;
            set => Native.Locked = value;
        }

        public double Width
        {
            get => Native.SizeWidth;
            set => Native.SetSize(value, Native.SizeHeight);
        }

        public double Height
        {
            get => Native.SizeHeight;
            set => Native.SetSize(Native.SizeWidth, value);
        }

        public double PositionX
        {
            get => Native.PositionX;
            set => Native.Move(value - Native.PositionX, 0);
        }

        public double PositionY
        {
            get => Native.PositionY;
            set => Native.Move(0, value - Native.PositionY);
        }

        public double CenterX => Native.CenterX;

        public double CenterY => Native.CenterY;

        public IReadOnlyList<ICorelShape> Shapes => CorelCollections.ToShapeList(Native.Shapes);

        public void Delete() => Native.Delete();

        public void MoveToLayer(ICorelLayer layer) => Native.MoveToLayer(((CorelLayerAdapter)layer).Native);

        public void ApplyStyleString(string style) => Native.Style.StringAssign(style);

        /// <summary>
        /// cdrShapeType (CorelDRAW 2026 / VGCore v27) não tem mais um valor distinto para
        /// PowerClip — por isso ShapeKind.PowerClip nunca é retornado aqui. Validar no smoke
        /// test real se PowerClips aparecem como cdrGroupShape ou outro tipo.
        /// </summary>
        internal static ShapeKind MapKind(cdrShapeType type) => type switch
        {
            cdrShapeType.cdrRectangleShape => ShapeKind.Rectangle,
            cdrShapeType.cdrEllipseShape => ShapeKind.Ellipse,
            cdrShapeType.cdrCurveShape => ShapeKind.Curve,
            cdrShapeType.cdrTextShape => ShapeKind.Text,
            cdrShapeType.cdrGroupShape => ShapeKind.Group,
            cdrShapeType.cdrBitmapShape => ShapeKind.Bitmap,
            cdrShapeType.cdrPolygonShape => ShapeKind.Polygon,
            cdrShapeType.cdrBlendGroupShape => ShapeKind.Blend,
            cdrShapeType.cdrSymbolShape => ShapeKind.Symbol,
            _ => ShapeKind.Other
        };
    }
}
