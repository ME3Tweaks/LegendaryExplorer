using System;
using LegendaryExplorerCore.Gammtek.Extensions;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE
{
    internal enum CommentAction
    {
        Toggle,
        Add,
        Remove
    }

    /// <summary>
    /// Pure text transforms for comment toggling and indentation.
    /// No WPF dependencies — operates on string arrays.
    /// </summary>
    internal static class ScriptTextEditingService
    {
        /// <summary>
        /// Toggles, adds, or removes line comments on the given lines.
        /// Returns the modified lines, or null if all lines are whitespace-only.
        /// </summary>
        public static string[] ToggleCommentLines(string[] lines, CommentAction action)
        {
            int commentColumn = -1;
            bool hasNonCommentedLines = false;
            foreach (string line in lines)
            {
                int whitespaceCount = line.CountLeadingWhitespace();
                if (whitespaceCount < line.Length)
                {
                    //cast to uint so that -1 is "greater" than all positive numbers
                    commentColumn = (int)Math.Min((uint)whitespaceCount, (uint)commentColumn);
                    if (line[whitespaceCount] != '/' || line.Length > whitespaceCount + 1 && line[whitespaceCount + 1] != '/')
                    {
                        hasNonCommentedLines = true;
                    }
                }
            }
            if (commentColumn is -1)
            {
                return null;
            }

            string[] result = new string[lines.Length];
            Array.Copy(lines, result, lines.Length);

            if (action is CommentAction.Add || action is CommentAction.Toggle && hasNonCommentedLines)
            {
                for (int i = 0; i < result.Length; i++)
                {
                    string lineText = result[i];
                    if (lineText.Length > commentColumn && !string.IsNullOrWhiteSpace(lineText))
                    {
                        result[i] = $"{lineText.AsSpan(0, commentColumn)}//{lineText.AsSpan(commentColumn)}";
                    }
                }
            }
            else
            {
                for (int i = 0; i < result.Length; i++)
                {
                    string lineText = result[i];
                    int ws = lineText.CountLeadingWhitespace();
                    if (ws + 1 < lineText.Length && lineText[ws] == '/' && lineText[ws + 1] == '/')
                    {
                        result[i] = $"{lineText.AsSpan(0, ws)}{lineText.AsSpan(ws + 2)}";
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Toggles a line comment on a single line of text.
        /// </summary>
        public static string ToggleCommentSingleLine(string lineText)
        {
            int indentLength = lineText.CountLeadingWhitespace();
            ReadOnlySpan<char> indentation = lineText.AsSpan(0, indentLength);
            if (lineText.Length >= indentLength + 2 && lineText.AsSpan(indentLength, 2) is "//")
            {
                return $"{indentation}{lineText.AsSpan(indentLength + 2)}";
            }
            return $"{indentation}//{lineText.AsSpan(indentLength)}";
        }

        /// <summary>
        /// Indents or unindents the given lines. Stops at the first all-whitespace line (matching original behavior).
        /// </summary>
        public static string[] IndentLines(string[] lines, bool indent, int indentSize = 4)
        {
            string[] result = new string[lines.Length];
            Array.Copy(lines, result, lines.Length);
            for (int i = 0; i < result.Length; i++)
            {
                string line = result[i];
                int whitespaceCount = line.CountLeadingWhitespace();
                if (whitespaceCount == line.Length) break;
                if (indent)
                {
                    result[i] = new string(' ', indentSize) + line;
                }
                else
                {
                    int whitespaceRemoved = Math.Min(indentSize, whitespaceCount);
                    result[i] = line.AsSpan(whitespaceRemoved).ToString();
                }
            }
            return result;
        }

        /// <summary>
        /// Indents or unindents a single line. Returns null if the line is all whitespace.
        /// Returns the new line text and the caret offset delta.
        /// </summary>
        public static (string lineText, int caretDelta)? IndentSingleLine(string lineText, bool indent, int indentSize = 4)
        {
            int indentationLength = lineText.CountLeadingWhitespace();
            if (indentationLength == lineText.Length)
            {
                return null;
            }

            if (indent)
            {
                return (new string(' ', indentSize) + lineText, indentSize);
            }
            int whitespaceRemoved = Math.Min(indentSize, indentationLength);
            return (lineText.AsSpan(whitespaceRemoved).ToString(), -whitespaceRemoved);
        }
    }
}
