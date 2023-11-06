using System;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class ASTHighlighter : IHighlighter, ILineTracker
    {

        public IDocument Document { get; }
        public HighlightingColor DefaultTextColor { get; }
        private readonly WeakLineTracker weakLineTracker;

        private readonly SyntaxInfo SyntaxInfo;

        public ASTHighlighter(TextDocument document, SyntaxInfo syntaxInfo)
        {
            SyntaxInfo = syntaxInfo ?? throw new ArgumentNullException(nameof(syntaxInfo));
            DefaultTextColor = SyntaxInfo.HighlightingColors[EF.None];
            Document = document ?? throw new ArgumentNullException(nameof(document));
            document.VerifyAccess();
            //weakLineTracker = WeakLineTracker.Register(document, this);
        }

        public HighlightedLine HighlightLine(int lineNumber)
        {
            IDocumentLine line = Document.GetLineByNumber(lineNumber);
            var highlightedLine = new HighlightedLine(Document, line);
            
            int lineLength = line.Length;
            if (lineLength == 0)
            {
                return highlightedLine;
            }

            lineNumber--;

            List<int> lineToIndex = SyntaxInfo.LineToIndex;
            if (lineNumber < 0 || lineNumber >= lineToIndex.Count)
            {
                return highlightedLine;
            }

            int i = lineToIndex[lineNumber];

            int lineStart = line.Offset;
            int lineEnd = lineStart + lineLength;

            List<SyntaxSpan> syntaxSpans = SyntaxInfo.SyntaxSpans;

            lineNumber++;
            int endIndex = lineNumber == lineToIndex.Count ? syntaxSpans.Count : lineToIndex[lineNumber];

            for (; i < endIndex; i++)
            {
                SyntaxSpan syntaxSpan = syntaxSpans[i];
                
                //if a highlighted section is not entirely within the line,
                //AvalonEdit will throw an uncatchable exception and LEX will instantly crash.
                //let's avoid that
                if (syntaxSpan.Offset < lineStart || syntaxSpan.Offset + syntaxSpan.Length > lineEnd)
                {
                    break;
                }

                highlightedLine.Sections.Add(new HighlightedSection
                {
                    Offset = syntaxSpan.Offset,
                    Length = syntaxSpan.Length,
                    Color = SyntaxInfo.HighlightingColors[syntaxSpan.FormatType]
                });
            }

            if (SyntaxInfo.CommentSpans.TryGetValue(lineNumber, out SyntaxSpan commentSpan)
                && commentSpan.Offset >= lineStart && commentSpan.Offset + commentSpan.Length <= lineEnd)
            {
                highlightedLine.Sections.Add(new HighlightedSection
                {
                    Offset = commentSpan.Offset,
                    Length = commentSpan.Length,
                    Color = SyntaxInfo.HighlightingColors[commentSpan.FormatType]
                });
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
