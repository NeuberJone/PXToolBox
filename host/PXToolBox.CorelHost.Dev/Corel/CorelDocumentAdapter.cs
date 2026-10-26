using System.Collections.Generic;
using Corel.Interop.VGCore;
using PXToolBox.Core.Measurement;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBoxDev.Corel
{
    internal sealed class CorelDocumentAdapter : ICorelDocument
    {
        internal Document Native { get; }

        public CorelDocumentAdapter(Document native)
        {
            Native = native;
        }

        public string Name
        {
            get => Native.Name ?? string.Empty;
            set => Native.Name = value;
        }

        public string FullFileName => Native.FullFileName ?? string.Empty;

        public DocumentUnit? Unit => MapUnit(Native.Unit);

        public ICorelPage ActivePage => new CorelPageAdapter(Native.ActivePage, Native);

        public ICorelLayer? ActiveLayer => Native.ActiveLayer == null ? null : new CorelLayerAdapter(Native.ActiveLayer);

        public IReadOnlyList<ICorelPage> Pages => CorelCollections.ToPageList(Native.Pages, Native);

        public void SaveAsCdr(string fullPath)
        {
            var options = new StructSaveAsOptionsClass
            {
                EmbedVBAProject = false,
                Filter = cdrFilter.cdrCDR,
                IncludeCMXData = false,
                Range = cdrExportRange.cdrAllPages,
                EmbedICCProfile = false,
                KeepAppearance = true
            };

            Native.SaveAs(fullPath, options);
        }

        private static DocumentUnit? MapUnit(cdrUnit unit) => unit switch
        {
            cdrUnit.cdrMillimeter => DocumentUnit.Millimeter,
            cdrUnit.cdrCentimeter => DocumentUnit.Centimeter,
            cdrUnit.cdrMeter => DocumentUnit.Meter,
            cdrUnit.cdrInch => DocumentUnit.Inch,
            cdrUnit.cdrFoot => DocumentUnit.Foot,
            cdrUnit.cdrYard => DocumentUnit.Yard,
            _ => null
        };
    }
}
