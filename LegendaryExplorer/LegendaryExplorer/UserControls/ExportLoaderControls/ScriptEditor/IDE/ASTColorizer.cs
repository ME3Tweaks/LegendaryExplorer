using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class ASTColorizer(SyntaxInfo syntaxInfo) : HighlightingColorizer
    {
        protected override IHighlighter CreateHighlighter(TextView textView, TextDocument document)
        {
            return new ASTHighlighter(document, syntaxInfo);
        }
    }
}
