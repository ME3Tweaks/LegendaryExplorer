using ICSharpCode.AvalonEdit.Highlighting;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    public class SyntaxInfoCodeFormatter : PlainTextCodeFormatter , ICodeFormatter<(string, SyntaxInfo)>
    {
        private readonly SyntaxInfo SyntaxInfo = new();

        private int Position;

        public new (string, SyntaxInfo) GetOutput() => (base.GetOutput(), SyntaxInfo);
        public override void AppendToNewLine(string text, ST formatType)
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

        public override void Append(string text, ST formatType)
        {
            if (text != "")
            {
                if (formatType != ST.None)
                {
                    SyntaxInfo.SyntaxSpans.Add(new SyntaxSpan(formatType, text.Length, Position));
                }
                
                currentLine += text;
                Position += text.Length;
            }
        }
    }

    public readonly record struct SyntaxSpan(ST FormatType, int Length, int Offset);

    public class SyntaxInfo : IHighlightingDefinition
    {
        public readonly List<int> LineToIndex;
        public readonly List<SyntaxSpan> SyntaxSpans;
        public Dictionary<int, SyntaxSpan> CommentSpans;

        public SyntaxInfo(List<int> lineToIndex = null, List<SyntaxSpan> syntaxSpans = null, Dictionary<int, SyntaxSpan> commentSpans = null)
        {
            LineToIndex = lineToIndex ?? [];
            SyntaxSpans = syntaxSpans ?? [];
            CommentSpans = commentSpans ?? [];
        }

        /// <summary>
        /// Adjusts span offsets in place to account for a text change, keeping stale highlighting approximately in sync
        /// until the next full parse. Spans that straddle the edit point have their length adjusted;
        /// spans entirely after the edit point are shifted by the delta.
        /// </summary>
        public void AdjustForChange(int changeOffset, int insertionLength, int removalLength)
        {
            int delta = insertionLength - removalLength;
            if (delta == 0) return;

            int originalCursorOffset = changeOffset + removalLength;
            int i = SyntaxSpans.BinarySearch(new SyntaxSpan(default, default, originalCursorOffset), new SyntaxSpanPositionComparer());
            if (i > 0)
            {
                SyntaxSpan span = SyntaxSpans[i];
                SyntaxSpans[i] = span with { Offset = span.Offset + delta };
            }
            else
            {
                i = ~i;
            }
            for (int j = i - 1; j >= 0; j--)
            {
                SyntaxSpan span = SyntaxSpans[j];
                int spanEnd = span.Offset + span.Length;
                if (spanEnd <= changeOffset)
                {
                    break;
                }
                int newLength = Math.Max(0, span.Length + delta);
                SyntaxSpans[j] = span with { Length = newLength };
            }
            for (; i < SyntaxSpans.Count; i++)
            {
                SyntaxSpan span = SyntaxSpans[i];
                SyntaxSpans[i] = span with { Offset = span.Offset + delta };
            }

            var adjustedComments = new Dictionary<int, SyntaxSpan>(CommentSpans.Count);
            foreach (var (line, span) in CommentSpans)
            {
                if (span.Offset >= changeOffset)
                {
                    adjustedComments[line] = span with { Offset = span.Offset + delta };
                }
                else if (span.Offset + span.Length > changeOffset)
                {
                    int newLength = Math.Max(0, span.Length + delta);
                    adjustedComments[line] = span with { Length = newLength };
                }
                else
                {
                    adjustedComments[line] = span;
                }
            }
            CommentSpans = adjustedComments;
        }

        private readonly struct SyntaxSpanPositionComparer : IComparer<SyntaxSpan>
        {
            public readonly int Compare(SyntaxSpan x, SyntaxSpan y) => x.Offset.CompareTo(y.Offset);
        }

        internal static readonly FrozenDictionary<ST, Color> DefaultColors = new Dictionary<ST, Color>
        {
            [ST.Keyword] = Color.FromRgb(0x56, 0x9b, 0xbf),
            [ST.Specifier] = Color.FromRgb(0x56, 0x9b, 0xbf),
            [ST.Class] = Color.FromRgb(0x4e, 0xc8, 0xaf),
            [ST.String] = Color.FromRgb(0xd5, 0x9c, 0x7c),
            [ST.Name] = Color.FromRgb(0xd5, 0x9c, 0x7c),
            [ST.Number] = Color.FromRgb(0xb1, 0xcd, 0xa7),
            [ST.Enum] = Color.FromRgb(0xb7, 0xdc, 0xa2),
            [ST.Comment] = Color.FromRgb(0x57, 0xa5, 0x4a),
            [ST.ERROR] = Color.FromRgb(0xff, 0x0, 0x0),
            [ST.Operator] = Color.FromRgb(0xB3, 0xB3, 0xB3),
            [ST.None] = Color.FromRgb(0xDB, 0xDB, 0xDB),
            [ST.Function] = Color.FromRgb(0xDB, 0xDB, 0xDB),
            [ST.State] = Color.FromRgb(0xDB, 0xDB, 0xDB),
            [ST.Label] = Color.FromRgb(0xDB, 0xDB, 0xDB),
            [ST.Struct] = Color.FromRgb(0x86, 0xC6, 0x91),
        }.ToFrozenDictionary();

        internal static readonly Color DefaultBackground = Color.FromRgb(0x1E, 0x1E, 0x1E);

        public static SyntaxInfo None { get; } = new();

        public static event Action ThemeChanged;

        public static FrozenDictionary<ST, HighlightingColor> HighlightingColors { get; private set; }
        public static FrozenDictionary<ST, SolidColorBrush> ColorBrushes { get; private set; }
        public static SolidColorBrush BackgroundBrush { get; private set; }

        public static void ApplyTheme(IDictionary<ST, Color> colors, Color background)
        {
            var newHighlightingColors = new Dictionary<ST, HighlightingColor>();
            var newColorBrushes = new Dictionary<ST, SolidColorBrush>();
            foreach (ST value in Enum.GetValues<ST>())
            {
                Color c = colors.TryGetValue(value, out Color col) ? col : DefaultColors[value];
                newHighlightingColors[value] = new HighlightingColor { Name = value.ToString(), Foreground = new SimpleHighlightingBrush(c) };
                newColorBrushes[value] = new SolidColorBrush(c);
            }
            var newBackgroundBrush = new SolidColorBrush(background);
            newBackgroundBrush.Freeze();

            HighlightingColors = newHighlightingColors.ToFrozenDictionary();
            ColorBrushes = newColorBrushes.ToFrozenDictionary();
            BackgroundBrush = newBackgroundBrush;

            ThemeChanged?.Invoke();
        }

        public static void LoadFromSettings()
        {
            string themeName = Settings.ScriptIDE_ActiveTheme;
            if (!string.IsNullOrEmpty(themeName))
            {
                var themeData = LookupSavedTheme(themeName);
                if (themeData is not null)
                {
                    ApplyTheme(themeData.Colors, themeData.Background);
                    return;
                }
            }
            ApplyTheme(DefaultColors, DefaultBackground);
        }

        internal static ThemeData LookupSavedTheme(string themeName)
        {
            var allThemes = Settings.ScriptIDE_SavedThemes;
            if (allThemes is not null && allThemes.TryGetValue(themeName, out var themeData))
            {
                return themeData;
            }
            return null;
        }

        public string Name => "Unrealscript-Dark";
        public IEnumerable<HighlightingColor> NamedHighlightingColors => HighlightingColors.Values;
        public HighlightingColor GetNamedColor(string name) => NamedHighlightingColors.FirstOrDefault(hc => hc.Name == name);
        public IDictionary<string, string> Properties => null;
        public HighlightingRuleSet MainRuleSet => null;
        public HighlightingRuleSet GetNamedRuleSet(string name) => null;
    }
}