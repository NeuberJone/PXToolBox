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
            return new CorelShapeAdapter(CorelShapeAdapter.GroupOnTheirLayer(range));
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

        // Assinaturas conferidas por reflexão (IVGPage.Guides(cdrGuideType) com cdrSlantedGuide, IVGShape.Guide,
        // IVGGuide.Point1X/Y/Point2X/Y, IVGLayer.CreateGuide); comportamento real só no smoke test.
        public IReadOnlyList<ICorelGuide> Guides
        {
            get
            {
                var guides = new List<ICorelGuide>();
                foreach (var page in new[] { Native, _owner.MasterPage })
                {
                    if (page == null) continue;
                    foreach (var horizontal in new[] { true, false })
                    {
                        var range = page.Guides[horizontal ? cdrGuideType.cdrHorizontalGuide : cdrGuideType.cdrVerticalGuide];
                        for (var i = 1; i <= range.Count; i++) guides.Add(new CorelGuideAdapter(range[i], horizontal));
                    }

                    var slanted = page.Guides[cdrGuideType.cdrSlantedGuide];
                    for (var i = 1; i <= slanted.Count; i++) guides.Add(new CorelGuideAdapter(slanted[i], false, slanted: true));
                }

                return guides;
            }
        }

        public ICorelGuide CreateGuide(bool horizontal, double position)
        {
            var shape = horizontal
                ? Native.GuidesLayer.CreateGuide(0, position, 100, position)
                : Native.GuidesLayer.CreateGuide(position, 0, position, 100);
            return new CorelGuideAdapter(shape, horizontal);
        }
    }

    internal sealed class CorelGuideAdapter : ICorelGuide
    {
        private readonly Shape _native;

        public CorelGuideAdapter(Shape native, bool horizontal, bool slanted = false)
        {
            _native = native;
            Horizontal = horizontal;
            Slanted = slanted;
        }

        public bool Horizontal { get; }

        public bool Slanted { get; }

        public double Position => Horizontal ? _native.Guide.Point1Y : _native.Guide.Point1X;

        public (double X1, double Y1, double X2, double Y2) Points
        {
            get
            {
                var guide = _native.Guide;
                return (guide.Point1X, guide.Point1Y, guide.Point2X, guide.Point2Y);
            }
        }

        public string Name
        {
            get => _native.Name ?? string.Empty;
            set => _native.Name = value;
        }

        public void Delete() => _native.Delete();
    }
}
