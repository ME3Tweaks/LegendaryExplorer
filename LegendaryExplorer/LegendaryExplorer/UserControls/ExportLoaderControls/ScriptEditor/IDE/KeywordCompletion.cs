using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using FontAwesome5;
using FontAwesome5.Extensions;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class KeywordCompletion : ICompletionData
    {
        public KeywordCompletion(string text, string description = null)
        {
            Text = text;
            Description = description;
        }

        public string Text { get; set; }
        public object Description { get; set; }
        public double Priority => 0;
        public object Content => Text;

        private static readonly ImageSource _image = EFontAwesomeIcon.Solid_Key.CreateImageSource(Brushes.Black, 0.1);
        public ImageSource Image => _image;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}