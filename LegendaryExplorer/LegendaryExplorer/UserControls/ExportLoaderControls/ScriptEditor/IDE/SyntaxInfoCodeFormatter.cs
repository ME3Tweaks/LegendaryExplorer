using System;
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

        private static readonly Dictionary<EF, Color> Colors = new()
        {
            [EF.Keyword] = Color.FromRgb(0x56, 0x9b, 0xbf),
            [EF.Specifier] = Color.FromRgb(0x56, 0x9b, 0xbf),
            [EF.Class] = Color.FromRgb(0x4e, 0xc8, 0xaf),
            [EF.String] = Color.FromRgb(0xd5, 0x9c, 0x7c),
            [EF.Name] = Color.FromRgb(0xd5, 0x9c, 0x7c),
            [EF.Number] = Color.FromRgb(0xb1, 0xcd, 0xa7),
            [EF.Enum] = Color.FromRgb(0xb7, 0xdc, 0xa2),
            [EF.Comment] = Color.FromRgb(0x57, 0xa5, 0x4a),
            [EF.ERROR] = Color.FromRgb(0xff, 0x0, 0x0),
            [EF.Operator] = Color.FromRgb(0xB3, 0xB3, 0xB3),
            [EF.None] = Color.FromRgb(0xDB, 0xDB, 0xDB),
            [EF.Function] = Color.FromRgb(0xDB, 0xDB, 0xDB),
            [EF.State] = Color.FromRgb(0xDB, 0xDB, 0xDB),
            [EF.Label] = Color.FromRgb(0xDB, 0xDB, 0xDB),
            [EF.Struct] = Color.FromRgb(0x86, 0xC6, 0x91),
        };

        public static SyntaxInfo None { get; } = new();

        static SyntaxInfo()
        {
            foreach (EF value in Enum.GetValues<EF>())
            {
                HighlightingColors[value] = new HighlightingColor { Name = value.ToString(), Foreground = new SimpleHighlightingBrush(Colors[value]) };
                ColorBrushes[value] = new SolidColorBrush(Colors[value]);
            }
        }

        public static readonly Dictionary<EF, HighlightingColor> HighlightingColors = new ();
        public static readonly Dictionary<EF, SolidColorBrush> ColorBrushes = new();

        public string Name => "Unrealscript-Dark";
        public IEnumerable<HighlightingColor> NamedHighlightingColors => HighlightingColors.Values;
        public HighlightingColor GetNamedColor(string name) => NamedHighlightingColors.FirstOrDefault(hc => hc.Name == name);
        public IDictionary<string, string> Properties => null;
        public HighlightingRuleSet MainRuleSet => null;
        public HighlightingRuleSet GetNamedRuleSet(string name) => null;
    }
}