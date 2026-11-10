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

        public DocumentUnit? Unit => CorelUnitMapping.Map(Native.Unit);

        // Terceira tentativa — as duas anteriores (Document.Unit e Document.ToUnits, que usa
        // Document.Unit internamente) foram refutadas por evidência real: em documentos reais
        // (inclusive já salvos), Document.Unit e Application.Unit reportam cdrInch mesmo com a
        // régua/barra de propriedades mostrando cm. Document.Rulers.HUnits, ao contrário,
        // reportou cdrCentimeter corretamente nos mesmos testes — é essa a propriedade
        // sincronizada com o que a UI mostra. Application.ConvertUnits(Value, FromUnit, ToUnit)
        // é usado em vez de Document.ToUnits porque recebe os dois lados explicitamente, sem
        // depender de nenhum estado ambiente (nem Document.Unit, nem Application.Unit) — ver
        // StandardSizeService e docs/ARCHITECTURE.md.
        public void SaveSelectionAsCopy(string fullPath)
        {
            var options = Native.Application.CreateStructSaveAsOptions();
            options.Range = cdrExportRange.cdrSelection;
            options.Overwrite = true;
            Native.SaveAsCopy(fullPath, options);
        }

        public double ConvertFromInches(double inches) =>
            Native.Application.ConvertUnits(inches, cdrUnit.cdrInch, Native.Rulers.HUnits);

        public ICorelPage ActivePage => new CorelPageAdapter(Native.ActivePage, Native);

        public ICorelLayer? ActiveLayer => Native.ActiveLayer == null ? null : new CorelLayerAdapter(Native.ActiveLayer);

        public IReadOnlyList<ICorelPage> Pages => CorelCollections.ToPageList(Native.Pages, Native);

        // Camadas mestras (Layer.Master, confirmado por reflexão) vêm só da página mestra —
        // puladas nas páginas comuns, para cada camada aparecer uma única vez mesmo que
        // Page.Layers também as liste.
        public IReadOnlyList<ICorelLayer> AllLayers
        {
            get
            {
                var list = new List<ICorelLayer>(CorelCollections.ToLayerList(Native.MasterPage.Layers));
                var pages = Native.Pages;
                for (var i = 1; i <= pages.Count; i++)
                {
                    var layers = pages[i].Layers;
                    for (var j = 1; j <= layers.Count; j++)
                    {
                        var layer = layers[j];
                        if (!layer.Master)
                        {
                            list.Add(new CorelLayerAdapter(layer));
                        }
                    }
                }

                return list;
            }
        }

        // Ver CorelLayerAdapter.Identity.
        public object Identity => Native;

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

    }
}
