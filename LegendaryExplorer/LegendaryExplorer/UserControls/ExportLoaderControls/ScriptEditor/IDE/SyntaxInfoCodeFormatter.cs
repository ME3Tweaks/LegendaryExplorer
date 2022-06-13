using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class SyntaxInfoCodeFormatter : PlainTextCodeFormatter , ICodeFormatter<(string, SyntaxInfo)>
    {

        private readonly SyntaxInfo SyntaxInfo = new();

        private int Position;

        public new (string, SyntaxInfo) GetOutput() => (base.GetOutput(), SyntaxInfo);
        public override void Write(string text, EF formatType)
        {
            if (!ForceNoNewLines)
            {
                SyntaxInfo.LineToIndex.Add(SyntaxInfo.SyntaxSpans.Count);
                if (currentLine != null)
                {
                    Lines.Add(currentLine);
                    Position++;
                }

                int numSpaces = ForcedAlignment + NestingLevel * 4;
                currentLine = new string(' ', numSpaces);
                Position += numSpaces;
            }
            Append(text, formatType);
        }

        public override void Append(string text, EF formatType)
        {
            if (text != "")
            {
                if (formatType != EF.None)
                {
                    SyntaxInfo.SyntaxSpans.Add(new SyntaxSpan(formatType, text.Length, Position));
                }
                
                currentLine += text;
                Position += text.Length;
            }
        }
    }

    public readonly struct SyntaxSpan
    {
        public readonly int Offset;
        public readonly int Length;
        public readonly EF FormatType;

        public SyntaxSpan(EF formatType, int length, int offset)
        {
            FormatType = formatType;
            Length = length;
            Offset = offset;
        }
    }

    public class SyntaxInfo : IHighlightingDefinition
    {
        public readonly List<int> LineToIndex;
        public readonly List<SyntaxSpan> SyntaxSpans;
        public readonly Dictionary<int, SyntaxSpan> CommentSpans; 

        public SyntaxInfo(List<int> lineToIndex = null, List<SyntaxSpan> syntaxSpans = null, Dictionary<int, SyntaxSpan> commentSpans = null)
        {
            LineToIndex = lineToIndex ?? new List<int>();
            SyntaxSpans = syntaxSpans ?? new List<SyntaxSpan>();
            CommentSpans = commentSpans ?? new Dictionary<int, SyntaxSpan>();
        }

        public static readonly Dictionary<EF, HighlightingColor> Colors = new()
        {
            [EF.Keyword] = new HighlightingColor { Name = nameof(EF.Keyword), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x56, 0x9b, 0xbf)) },
            [EF.Specifier] = new HighlightingColor { Name = nameof(EF.Specifier), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x56, 0x9b, 0xbf)) },
            [EF.TypeName] = new HighlightingColor { Name = nameof(EF.TypeName), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x4e, 0xc8, 0xaf)) },
            [EF.String] = new HighlightingColor { Name = nameof(EF.String), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xd5, 0x9c, 0x7c)) },
            [EF.Name] = new HighlightingColor { Name = nameof(EF.Name), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xd5, 0x9c, 0x7c)) },
            [EF.Number] = new HighlightingColor { Name = nameof(EF.Number), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xb1, 0xcd, 0xa7)) },
            [EF.Enum] = new HighlightingColor { Name = nameof(EF.Enum), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xb7, 0xdc, 0xa2)) },
            [EF.Comment] = new HighlightingColor { Name = nameof(EF.Comment), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x57, 0xa5, 0x4a)) },
            [EF.ERROR] = new HighlightingColor { Name = nameof(EF.ERROR), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xff, 0x0, 0x0)) },
            [EF.Operator] = new HighlightingColor { Name = nameof(EF.Operator), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xB3, 0xB3, 0xB3)) },
            [EF.None] = new HighlightingColor { Name = nameof(EF.None), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xDB, 0xDB, 0xDB)) },
            [EF.Function] = new HighlightingColor { Name = nameof(EF.Function), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xDB, 0xDB, 0xDB)) },
            [EF.State] = new HighlightingColor { Name = nameof(EF.State), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xDB, 0xDB, 0xDB)) },
            [EF.Label] = new HighlightingColor { Name = nameof(EF.Label), Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xDB, 0xDB, 0xDB)) },
        };

        public string Name => "Unrealscript-Dark";
        public IEnumerable<HighlightingColor> NamedHighlightingColors => Colors.Values;
        public HighlightingColor GetNamedColor(string name) => NamedHighlightingColors.FirstOrDefault(hc => hc.Name == name);
        public IDictionary<string, string> Properties => null;
        public HighlightingRuleSet MainRuleSet => null;
        public HighlightingRuleSet GetNamedRuleSet(string name) => null;
    }
}