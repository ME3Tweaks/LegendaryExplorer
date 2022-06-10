using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class SyntaxInfoCodeFormatter : PlainTextCodeFormatter , ICodeFormatter<(string, SyntaxInfo)>
    {

        private readonly SyntaxInfo SyntaxInfo = new(0);

        public new (string, SyntaxInfo) GetOutput() => (base.GetOutput(), SyntaxInfo);

        public override void Append(string text, EF formatType)
        {
            if (text != "")
            {
                while (SyntaxInfo.Count <= Lines.Count)
                {
                    SyntaxInfo.Add(new List<SyntaxSpan>());
                }
                List<SyntaxSpan> spans = SyntaxInfo[Lines.Count];

                if (spans.Count == 0 && !string.IsNullOrEmpty(currentLine))
                {
                    spans.Add(new SyntaxSpan(EF.None, currentLine.Length));
                }
                //TODO: extend previous syntaxspan if same EF?
                spans.Add(new SyntaxSpan(formatType, text.Length));
                currentLine += text;
            }
        }
    }

    public readonly struct SyntaxSpan
    {
        public readonly EF FormatType;
        public readonly int Length;

        public SyntaxSpan(EF formatType, int length)
        {
            FormatType = formatType;
            Length = length;
        }
    }

    public class SyntaxInfo : List<List<SyntaxSpan>>, IHighlightingDefinition
    {
        public SyntaxInfo(int capacity) : base(capacity)
        {
            Name = "Unrealscript-Dark";
            Colors = new Dictionary<EF, HighlightingColor>
            {
                [EF.Keyword] = new() { Name = nameof(EF.Keyword), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x56, 0x9b, 0xbf))},
                [EF.Specifier] = new() { Name = nameof(EF.Specifier), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x56, 0x9b, 0xbf))},
                [EF.TypeName] = new() { Name = nameof(EF.TypeName), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x4e, 0xc8, 0xaf))},
                [EF.String] = new() { Name = nameof(EF.String), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xd5, 0x9c, 0x7c))},
                [EF.Name] = new() { Name = nameof(EF.Name), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xd5, 0x9c, 0x7c))},
                [EF.Number] = new() { Name = nameof(EF.Number), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xb1, 0xcd, 0xa7))},
                [EF.Enum] = new() { Name = nameof(EF.Enum), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xb7, 0xdc, 0xa2))},
                [EF.Comment] = new() { Name = nameof(EF.Comment), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x57, 0xa5, 0x4a))},
                [EF.ERROR] = new() { Name = nameof(EF.ERROR), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xff, 0x0, 0x0))},
                [EF.Operator] = new() { Name = nameof(EF.Operator), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xB3, 0xB3, 0xB3))},
                [EF.None] = new() { Name = nameof(EF.None), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xDB, 0xDB, 0xDB))},
                [EF.Function] = new() { Name = nameof(EF.Function), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xDB, 0xDB, 0xDB))},
                [EF.State] = new() { Name = nameof(EF.State), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xDB, 0xDB, 0xDB))},
                [EF.Label] = new() { Name = nameof(EF.Label), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xDB, 0xDB, 0xDB))},
            };
        }

        public readonly Dictionary<EF, HighlightingColor> Colors;

        public string Name { get; }
        public IEnumerable<HighlightingColor> NamedHighlightingColors => Colors.Values;
        public HighlightingColor GetNamedColor(string name) => NamedHighlightingColors.FirstOrDefault(hc => hc.Name == name);
        public IDictionary<string, string> Properties => null;
        public HighlightingRuleSet MainRuleSet => null;
        public HighlightingRuleSet GetNamedRuleSet(string name) => null;
    }
}