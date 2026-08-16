using FontAwesome5;
using FontAwesome5.Extensions;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System;
using System.Windows.Media;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class CompletionData : ICompletionData
    {
        public CompletionData(string text, string description = null)
        {
            Text = text;
            descriptionText = description;
        }

        public string Text { get; set; }

        private string descriptionText;

        private object _description;
        public object Description
        {
            get
            {
                if (_description is null && descriptionText is not null)
                {
                    _description = CompletionHelper.CreateDescriptionBlock(descriptionText);
                }
                return _description;
            }
        }
        public double Priority => 0;
        public object Content => Text;

        private static readonly ImageSource _image = EFontAwesomeIcon.Solid_Table.CreateImageSource(Brushes.Black, 0.1);
        public ImageSource Image => _image;
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}
