using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Measurement;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Tests.Fakes
{
    public sealed class FakeDocument : ICorelDocument
    {
        public string Name { get; set; } = "Sem título-1";

        public string FullFileName { get; set; } = string.Empty;

        public DocumentUnit? Unit { get; set; } = DocumentUnit.Centimeter;

        /// <summary>Simula Document.ToUnits real (ver ICorelDocument.ConvertFromInches) —
        /// identidade por padrão; testes que precisam simular uma conversão real sobrescrevem.</summary>
        public System.Func<double, double> ConvertFromInchesFunc { get; set; } = inches => inches;

        public double ConvertFromInches(double inches) => ConvertFromInchesFunc(inches);

        public FakePage ActivePageFake { get; } = new FakePage();

        public ICorelPage ActivePage => ActivePageFake;

        public ICorelLayer? ActiveLayer { get; set; }

        public List<ICorelPage> PagesList { get; }

        public IReadOnlyList<ICorelPage> Pages => PagesList;

        /// <summary>Camadas locais das páginas — o fake não tem página mestra separada.</summary>
        public IReadOnlyList<ICorelLayer> AllLayers => PagesList.SelectMany(p => p.Layers).ToList();

        /// <summary>Simula o Corel entregando um objeto novo a cada acesso (identidade instável).</summary>
        public bool UnstableIdentity { get; set; }

        public object Identity => UnstableIdentity ? new object() : this;

        public string? SavedAsPath { get; private set; }

        public FakeDocument()
        {
            PagesList = new List<ICorelPage> { ActivePageFake };
        }

        public void SaveAsCdr(string fullPath) => SavedAsPath = fullPath;

        /// <summary>Nomes dos objetos salvos por SaveSelectionAsCopy, com a camada de cada um.</summary>
        public List<(string Layer, string Name)> SavedCopy { get; } = new List<(string Layer, string Name)>();

        public string? SavedCopyPath { get; private set; }

        /// <summary>A seleção vem de <see cref="SelectionForCopy"/> (o FakeArrangement da operação).</summary>
        public System.Func<IReadOnlyList<ICorelShape>>? SelectionForCopy { get; set; }

        public void SaveSelectionAsCopy(string fullPath)
        {
            SavedCopyPath = fullPath;
            foreach (var shape in SelectionForCopy?.Invoke() ?? new List<ICorelShape>())
            {
                SavedCopy.Add((((FakeShape)shape).CurrentLayer?.Name ?? "", shape.Name));
            }
        }

        /// <summary>Cada ExportSelectionPng: arquivo e tamanho pedidos.</summary>
        public List<(string Path, int Width, int Height)> Exported { get; } = new List<(string Path, int Width, int Height)>();

        /// <summary>O que o "Corel" grava no arquivo (null = não grava nada).</summary>
        public byte[]? ExportBytes { get; set; } = { 137, 80, 78, 71 };

        public void ExportSelectionPng(string fullPath, int widthPixels, int heightPixels)
        {
            Exported.Add((fullPath, widthPixels, heightPixels));
            if (ExportBytes != null) System.IO.File.WriteAllBytes(fullPath, ExportBytes);
        }
    }
}
