using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ME3Script.Language.Tree;

namespace ME3Explorer.ME3Script.IDE
{
    public class ASTHighlighter : IHighlighter, ILineTracker
    {

        public IDocument Document { get; }
        public HighlightingColor DefaultTextColor => null;
        readonly WeakLineTracker weakLineTracker;

        private ASTNode AST; //TODO: initialize this somehow?

        public ASTHighlighter(TextDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            document.VerifyAccess();
            weakLineTracker = WeakLineTracker.Register(document, this);

        }

        public HighlightedLine HighlightLine(int lineNumber)
        {
            throw new NotImplementedException();
        }

        public void UpdateHighlightingState(int lineNumber)
        {
            throw new NotImplementedException();
        }

        public event HighlightingStateChangedEventHandler HighlightingStateChanged;

        public void Dispose()
        {
            weakLineTracker?.Deregister();
        }

        #region ILineTracker

        void ILineTracker.BeforeRemoveLine(DocumentLine line)
        {
            throw new NotImplementedException();
        }

        void ILineTracker.SetLineLength(DocumentLine line, int newTotalLength)
        {
            throw new NotImplementedException();
        }

        void ILineTracker.LineInserted(DocumentLine insertionPos, DocumentLine newLine)
        {
            throw new NotImplementedException();
        }

        void ILineTracker.RebuildDocument()
        {
            throw new NotImplementedException();
        }

        void ILineTracker.ChangeComplete(DocumentChangeEventArgs e)
        {
            throw new NotImplementedException();
        }

        #endregion

        public void BeginHighlighting() {}
        public void EndHighlighting() {}
        public HighlightingColor GetNamedColor(string name) => throw new NotImplementedException();
        public IEnumerable<HighlightingColor> GetColorStack(int lineNumber) => throw new NotImplementedException();
    }
}
