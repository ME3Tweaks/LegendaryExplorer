using System;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Indentation.CSharp;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class UnrealScriptTextEditor : TextEditor
    {
        private ASTColorizer Colorizer;

        public UnrealScriptTextEditor()
        {
            SearchPanel.Install(TextArea);
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
