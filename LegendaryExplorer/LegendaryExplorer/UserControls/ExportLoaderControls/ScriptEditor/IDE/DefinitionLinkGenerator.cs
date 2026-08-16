#nullable enable
using ICSharpCode.AvalonEdit.Rendering;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Parsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class DefinitionLinkGenerator : VisualLineElementGenerator
    {
        private readonly Dictionary<int, DefinitionLinkSpan> Spans = new();
        private readonly List<int> Offsets = new();
        private readonly Action<int, int> ScrollTo;
        public TokenStream Tokens { get; private set; }

        public DefinitionLinkGenerator(Action<int, int> scrollTo)
        {
            ScrollTo = scrollTo;
            Tokens = new TokenStream(new List<ScriptToken>(), new LineLookup(new List<int> {0}));
        }

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
            Tokens = tokens;
            foreach ((ASTNode node, int offset, int length) in Tokens.DefinitionLinks)
            {
                Spans[offset] = new DefinitionLinkSpan(node, length);
                Offsets.Add(offset);
            }
            Offsets.Sort();
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

            int index = Offsets.BinarySearch(startOffset);

            if (index < 0)
            {
                index = ~index;
            }

            if (index >= Offsets.Count)
            {
                return -1;
            }
            int offset = Offsets[index];
            if (offset >= startOffset && offset < endOffset)
            {
                return offset;
            }

            return -1;
        }

        public ASTNode? GetDefinitionFromOffset(int offset)
        {
            ASTNode? node = null;

            int index = Offsets.BinarySearch(offset);

            if (index < 0)
            {
                index = ~index - 1;
                if (index < 0)
                {
                    return null;
                }
            }
            if (index < Offsets.Count)
            {
                int spanOffset = Offsets[index];
                DefinitionLinkSpan span = Spans[spanOffset];

                if (offset >= spanOffset && offset < spanOffset + span.Length)
                {
                    node = span.Node;
                }
            }

            return node switch
            {
                StaticArrayType staticArrayType => staticArrayType.ElementType,
                ClassType classType => classType.ClassLimiter,
                DynamicArrayType dynArr => dynArr.ElementType,
                _ => node
            };
        }

        public override VisualLineElement? ConstructElement(int offset)
        {
            if (Spans.TryGetValue(offset, out DefinitionLinkSpan span))
            {
                //definition links should never span multiple lines, so either we're operating on stale data or their's a bug in the parser
                //LEX will hard-crash if we don't return null in this case, as AvalonEdit will get caught in an error loop
                if (offset + span.Length > CurrentContext.VisualLine.FirstDocumentLine.EndOffset)
                {
                    //if (Debugger.IsAttached)
                    //{
                    //    Debugger.Break();
                    //}
                    return null;
                }
                return new VisualLineDefinitionLinkText(CurrentContext.VisualLine, span.Node, span.Length, ScrollTo);
            }

            return null;
        }
    }
}
