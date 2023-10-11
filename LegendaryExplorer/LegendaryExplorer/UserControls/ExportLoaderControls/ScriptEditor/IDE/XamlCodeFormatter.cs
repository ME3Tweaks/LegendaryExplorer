using System.Collections.Generic;
using System.Windows.Documents;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class XamlCodeFormatter : ICodeFormatter<List<Inline>>
    {
        public int NestingLevel { get; set; }
        public int ForcedAlignment { get; set; }
        public bool ForceNoNewLines { get; set; }

        private readonly List<Inline> Inlines = new();
        private EF CurrentFormat = EF.None;
        private string CurrentRun = "";
        private int lineDisplayLength;

        public List<Inline> GetOutput()
        {
            FinishRun();
            return Inlines;
        }

        public void Write(string text, EF formatType)
        {
            if (!ForceNoNewLines)
            {
                FinishRun();
                if (Inlines.Count > 0)
                {
                    Inlines.Add(new LineBreak());
                }
                CurrentRun = new string(' ', ForcedAlignment + NestingLevel * 4);
                lineDisplayLength = ForcedAlignment;
            }
            Append(text, formatType);
        }

        public void Append(string text, EF formatType)
        {
            if (formatType != CurrentFormat)
            {
                FinishRun();
                CurrentRun = text;
            }
            else
            {
                CurrentRun += text;
            }
            lineDisplayLength += text.Length;
            CurrentFormat = formatType;
        }

        private void FinishRun()
        {
            if (CurrentRun is not "")
            {
                Inlines.Add(new Run(CurrentRun)
                {
                    Foreground = SyntaxInfo.ColorBrushes[CurrentFormat]
                });
            }
        }

        public void Space()
        {
            CurrentRun += " ";
            lineDisplayLength += 1;
        }

        public void ForceAlignment()
        {
            ForcedAlignment = lineDisplayLength;
        }
    }

    public class XamlCodeBuilder : CodeBuilderVisitor<XamlCodeFormatter, List<Inline>>
    {
    }
}
