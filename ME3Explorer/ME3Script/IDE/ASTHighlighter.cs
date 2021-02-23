using System;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

namespace ME3Explorer.ME3Script.IDE
{
    public class ASTHighlighter : IHighlighter, ILineTracker
    {

        public IDocument Document { get; }
        public HighlightingColor DefaultTextColor => null;
        readonly WeakLineTracker weakLineTracker;

        private readonly SyntaxInfo SyntaxInfo;

        public ASTHighlighter(TextDocument document, SyntaxInfo syntaxInfo)
        {
            SyntaxInfo = syntaxInfo ?? throw new ArgumentNullException(nameof(syntaxInfo)); ;
            Document = document ?? throw new ArgumentNullException(nameof(document));
            document.VerifyAccess();
            //weakLineTracker = WeakLineTracker.Register(document, this);
        }

        public HighlightedLine HighlightLine(int lineNumber)
        {
            IDocumentLine line = Document.GetLineByNumber(lineNumber);
            var highlightedLine = new HighlightedLine(Document, line);
            lineNumber--;
            if (lineNumber >= SyntaxInfo.Count)
            {
                return highlightedLine;
            }
            int lineOffset = line.Offset;
            int pos = 0;
            List<SyntaxSpan> spans = SyntaxInfo[lineNumber];

            foreach (SyntaxSpan span in spans)
            {
                if (pos >= line.Length)
                {
                    break;
                }
                highlightedLine.Sections.Add(new HighlightedSection
                {
                    Offset = pos + lineOffset,
                    Length = Math.Min(span.Length, line.Length - pos),
                    Color = SyntaxInfo.Colors[span.FormatType]
                });
                pos += span.Length;
            }

            return highlightedLine;
        }

        public void UpdateHighlightingState(int lineNumber)
        {
            
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
