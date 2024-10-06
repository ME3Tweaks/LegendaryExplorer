using System;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Indentation.CSharp;
using ICSharpCode.AvalonEdit.Rendering;
using LegendaryExplorer.UserControls.SharedToolControls;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class UnrealScriptTextEditor : CodeEditorBase
    {
        public UnrealScriptTextEditor()
        {
            TextArea.IndentationStrategy = new CSharpIndentationStrategy(Options);
        }

        protected override IVisualLineTransformer CreateColorizer(IHighlightingDefinition highlightingDefinition)
        {
            ArgumentNullException.ThrowIfNull(highlightingDefinition);

            if (highlightingDefinition is SyntaxInfo syntaxInfo)
            {
                return new ASTColorizer(syntaxInfo);
            }

            return new HighlightingColorizer(highlightingDefinition);
        }
    }
}
