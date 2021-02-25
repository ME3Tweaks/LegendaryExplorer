using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace ME3Explorer.ME3Script.IDE
{
    public class CompletionData : ICompletionData
    {
        public CompletionData(string text, string description = null)
        {
            Text = text;
            Description = description;
        }

        public string Text { get; set; }
        public object Description { get; set; }
        public double Priority => 0;
        public object Content => Text;
        public ImageSource Image => null;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}
