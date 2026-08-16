using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Documents;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    internal static class CompletionHelper
    {
        public static TextBlock CreateDescriptionBlock(IEnumerable<Inline> inlines)
        {
            var textBlock = new TextBlock();
            textBlock.Inlines.AddRange(inlines);
            textBlock.Background = SyntaxInfo.BackgroundBrush;
            return textBlock;
        }

        public static TextBlock CreateDescriptionBlock(string text)
        {
            return CreateDescriptionBlock([new Run(text) { Foreground = SyntaxInfo.ColorBrushes[ST.None] }]);
        }
    }
}
