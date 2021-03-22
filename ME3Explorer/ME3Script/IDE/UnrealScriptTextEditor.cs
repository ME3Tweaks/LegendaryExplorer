using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Indentation.CSharp;
using ICSharpCode.AvalonEdit.Rendering;
using Unrealscript.Analysis.Visitors;

namespace ME3Explorer.ME3Script.IDE
{
    public class UnrealScriptTextEditor : TextEditor
    {
        private ASTColorizer Colorizer;

        public UnrealScriptTextEditor()
        {
            Options.ConvertTabsToSpaces = true;
            TextArea.IndentationStrategy = new CSharpIndentationStrategy(Options);
        }

        protected override IVisualLineTransformer CreateColorizer(IHighlightingDefinition highlightingDefinition)
        {
            if (highlightingDefinition == null)
                throw new ArgumentNullException(nameof(highlightingDefinition));

            if (highlightingDefinition is SyntaxInfo syntaxInfo)
            {
                return Colorizer = new ASTColorizer(syntaxInfo);
            }

            Colorizer = null;
            return new HighlightingColorizer(highlightingDefinition);
        }
    }
}
