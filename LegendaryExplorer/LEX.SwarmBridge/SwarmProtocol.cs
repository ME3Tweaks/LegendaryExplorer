using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LegendaryExplorer.SwarmBridge;

/// <summary>
/// The line protocol between LEX and LEX.SwarmBridge.exe, compiled into both: one message per line, fields separated by tabs, with
/// backslashes, tabs and line breaks escaped inside fields.
/// </summary>
public static class SwarmProtocol
{
    public static string Join(IEnumerable<string> fields) => string.Join("\t", fields.Select(Escape));

    public static string[] Split(string line) => line.Split('\t').Select(Unescape).ToArray();

    public static string Escape(string s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");

    public static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char c = s[++i];
                sb.Append(c switch { 't' => '\t', 'r' => '\r', 'n' => '\n', _ => c });
            }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }
}
