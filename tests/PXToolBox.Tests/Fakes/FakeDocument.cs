using System.Collections.Generic;
using PXToolBox.Core.Measurement;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Tests.Fakes
{
    public sealed class FakeDocument : ICorelDocument
    {
        public string Name { get; set; } = "Sem título-1";

        public string FullFileName { get; set; } = string.Empty;

        public DocumentUnit? Unit { get; set; } = DocumentUnit.Centimeter;

        public FakePage ActivePageFake { get; } = new FakePage();

        public ICorelPage ActivePage => ActivePageFake;

        public ICorelLayer? ActiveLayer { get; set; }

        public List<ICorelPage> PagesList { get; }

        public IReadOnlyList<ICorelPage> Pages => PagesList;

        public string? SavedAsPath { get; private set; }

        public FakeDocument()
        {
            PagesList = new List<ICorelPage> { ActivePageFake };
        }

        public void SaveAsCdr(string fullPath) => SavedAsPath = fullPath;
    }
}
