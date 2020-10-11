using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;

namespace ME3Explorer.ME3Script.IDE
{
    public class ASTColorizer : HighlightingColorizer
    {
        protected override IHighlighter CreateHighlighter(TextView textView, TextDocument document)
        {
            return new ASTHighlighter(document);
        }
    }
}
