using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class ASTColorizer : HighlightingColorizer
    {
        private readonly SyntaxInfo SyntaxInfo;

        public ASTColorizer(SyntaxInfo syntaxInfo)
        {
            SyntaxInfo = syntaxInfo;
        }

        protected override IHighlighter CreateHighlighter(TextView textView, TextDocument document)
        {
            return new ASTHighlighter(document, SyntaxInfo);
        }
    }
}
