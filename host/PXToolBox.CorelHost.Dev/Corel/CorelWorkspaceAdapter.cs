using Corel.Interop.VGCore;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBoxDev.Corel
{
    internal sealed class CorelWorkspaceAdapter : ICorelWorkspace
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

        public void CopyToClipboard(string text) => System.Windows.Clipboard.SetText(text);
    }
}
