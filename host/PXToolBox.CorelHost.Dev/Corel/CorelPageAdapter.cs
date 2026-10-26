using System.Collections.Generic;
using System.Linq;
using Corel.Interop.VGCore;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBoxDev.Corel
{
    internal sealed class CorelPageAdapter : ICorelPage
    {
        internal Page Native { get; }
        private readonly Document _owner;

        public CorelPageAdapter(Page native, Document owner)
        {
            Native = native;
            _owner = owner;
        }

        public string Name
        {
            get => Native.Name ?? string.Empty;
            set => Native.Name = value;
        }

        public double Width
        {
            get => Native.SizeWidth;
            set => Native.SizeWidth = value;
        }

        public double Height
        {
            get => Native.SizeHeight;
            set => Native.SizeHeight = value;
        }

        public double CenterX => Native.CenterX;

        public double CenterY => Native.CenterY;

        public IReadOnlyList<ICorelLayer> Layers => CorelCollections.ToLayerList(Native.Layers);

        public ICorelLayer? FindLayer(string name)
        {
            foreach (var layer in Layers)
            {
                if (layer.Name == name)
                {
                    return layer;
                }
            }

            return null;
        }

        public ICorelLayer CreateLayer(string name) => new CorelLayerAdapter(Native.CreateLayer(name));

        public ICorelShape Group(IEnumerable<ICorelShape> shapes)
        {
            var array = shapes.Cast<CorelShapeAdapter>().Select(s => (object)s.Native).ToArray();
            var range = _owner.CreateShapeRangeFromArray(ref array);
            return new CorelShapeAdapter(range.Group());
        }

        public IReadOnlyList<ICorelShape> Ungroup(ICorelShape group)
        {
            var native = ((CorelShapeAdapter)group).Native;
            var resultRange = native.UngroupEx();
            return CorelCollections.ToShapeList(resultRange);
        }

        public void CenterOnPage(IEnumerable<ICorelShape> shapes)
        {
            var array = shapes.Cast<CorelShapeAdapter>().Select(s => (object)s.Native).ToArray();
            if (array.Length == 0)
            {
                return;
            }

            var range = _owner.CreateShapeRangeFromArray(ref array);

            // Equivalente a ".AlignAndDistribute 3, 3, 2, 0, False, 2" na macro original
            // (Correcoes.AgrupaECentralizaObjetos / CentralizaObjetosNaPagina): centralizar
            // horizontal e verticalmente em relação ao centro da página.
            range.AlignAndDistribute(
                cdrAlignDistributeH.cdrAlignDistributeHAlignCenter,
                cdrAlignDistributeV.cdrAlignDistributeVAlignCenter,
                cdrAlignShapesTo.cdrAlignShapesToCenterOfPage,
                cdrDistributeArea.cdrDistributeToSelection,
                false,
                cdrTextAlignOrigin.cdrTextAlignBoundingBox);
        }

        public IReadOnlyList<ICorelShape> GetSelectableShapes() => CorelCollections.ToShapeList(Native.SelectableShapes);
    }
}
