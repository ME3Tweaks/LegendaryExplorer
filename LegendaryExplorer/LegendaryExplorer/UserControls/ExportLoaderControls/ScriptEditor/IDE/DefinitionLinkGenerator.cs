using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Rendering;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Parsing;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class DefinitionLinkGenerator : VisualLineElementGenerator
    {
        private readonly Dictionary<int, DefinitionLinkSpan> Spans = new();
        private readonly List<int> Offsets = new();

        private readonly struct DefinitionLinkSpan
        {
            public readonly ASTNode Node;
            public readonly int Length;

            public DefinitionLinkSpan(ASTNode node, int length)
            {
                Node = node;
                Length = length;
            }
        }

        public void SetTokens(TokenStream tokens)
        {
            Reset();
            foreach (ScriptToken token in tokens)
            {
                if (token.AssociatedNode is not null)
                {
                    int startPosCharIndex = token.StartPos;
                    Spans[startPosCharIndex] = new DefinitionLinkSpan(token.AssociatedNode, token.EndPos - startPosCharIndex);
                    Offsets.Add(startPosCharIndex);
                }
            }
        }

        public void Reset()
        {
            Spans.Clear();
            Offsets.Clear();
        }
        
        public override int GetFirstInterestedOffset(int startOffset)
        {
            //Debug.WriteLine($"Offset: {startOffset}");
            int endOffset = CurrentContext.VisualLine.FirstDocumentLine.EndOffset;

            var offset = Offsets.BinarySearch(startOffset);

            if (offset < 0)
            {
                offset = ~offset;
            }

            if (offset >= startOffset && offset < endOffset)
            {
                return offset;
            }

            return -1;
        }

        public override VisualLineElement ConstructElement(int offset)
        {
            //Debug.WriteLine($"Construct Offset: {offset}");
            if (Spans.TryGetValue(offset, out DefinitionLinkSpan span))
            {
                //Debug.WriteLine($"Constructed at Offset: {offset}");
                return new VisualLineDefinitionLinkText(CurrentContext.VisualLine, span.Node, span.Length);
            }

            return null;
        }
    }
}
