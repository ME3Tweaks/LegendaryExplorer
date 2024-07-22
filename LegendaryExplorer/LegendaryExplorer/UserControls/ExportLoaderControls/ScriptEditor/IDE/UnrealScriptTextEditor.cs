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
        public UnrealScriptTextEditor()
        {
            SearchPanel.Install(TextArea);
            Options.ConvertTabsToSpaces = true;
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


        //from https://github.com/icsharpcode/AvalonEdit/issues/143#issuecomment-411834415
        #region FontSize

        // Reasonable max and min font size values
        private const double FONT_MAX_SIZE = 60d;
        private const double FONT_MIN_SIZE = 5d;

        // Update function, increases/decreases by a specific increment
        public void UpdateFontSize(bool increase)
        {
            double currentSize = FontSize;

            if (increase)
            {
                if (currentSize < FONT_MAX_SIZE)
                {
                    double newSize = Math.Min(FONT_MAX_SIZE, currentSize + 1);
                    FontSize = newSize;
                }
            }
            else
            {
                if (currentSize > FONT_MIN_SIZE)
                {
                    double newSize = Math.Max(FONT_MIN_SIZE, currentSize - 1);
                    FontSize = newSize;
                }
            }
        }

        #endregion
    }
}
