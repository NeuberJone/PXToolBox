using System.Collections.Generic;
using Corel.Interop.VGCore;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBoxDev.Corel
{
    internal sealed class CorelWorkspaceAdapter : ICorelWorkspace, IShapeArrangement
    {
        private readonly Application _app;

        public CorelWorkspaceAdapter(Application app)
        {
            _app = app;
        }

        public ICorelDocument? ActiveDocument
        {
            get
            {
                var doc = _app.ActiveDocument;
                return doc == null ? null : new CorelDocumentAdapter(doc);
            }
        }

        public ICorelSelection ActiveSelection => new CorelSelectionAdapter(_app.ActiveSelectionRange);

        public IReadOnlyList<ICorelDocument> OpenDocuments
        {
            get
            {
                var documents = _app.Documents;
                var list = new List<ICorelDocument>(documents.Count);
                for (var i = 1; i <= documents.Count; i++)
                {
                    list.Add(new CorelDocumentAdapter(documents[i]));
                }

                return list;
            }
        }

        public void CopyToClipboard(string text) => System.Windows.Clipboard.SetText(text);

        public ICorelShape Group(IReadOnlyList<ICorelShape> shapes) => new CorelShapeAdapter(CorelShapeAdapter.GroupOnTheirLayer(Range(shapes)));

        public void Select(IReadOnlyList<ICorelShape> shapes) => Range(shapes).CreateSelection();

        private ShapeRange Range(IReadOnlyList<ICorelShape> shapes)
        {
            var range = _app.CreateShapeRange();
            foreach (var shape in shapes)
            {
                range.Add(((CorelShapeAdapter)shape).Native);
            }

            return range;
        }
    }
}
