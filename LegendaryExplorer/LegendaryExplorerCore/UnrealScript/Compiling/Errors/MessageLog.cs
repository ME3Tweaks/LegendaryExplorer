using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Compiling.Errors
{
    public class MessageLog
    {
        private readonly List<LogMessage> content;
        public IReadOnlyList<LogMessage> Content => content.AsReadOnly();

        public IReadOnlyList<LogMessage> Messages => content.Where(m => m.GetType() == typeof(LogMessage)).ToList();

        public IReadOnlyList<LogMessage> PositionedMessages => content.Where(m => m.GetType() == typeof(PositionedMessage)).ToList();

        public IReadOnlyList<LogMessage> Errors => content.Where(m => m is Error).ToList();

        public IReadOnlyList<LogMessage> LineErrors => content.Where(m => m is LineError).ToList();

        public IReadOnlyList<LogMessage> Warnings => content.Where(m => m is Warning).ToList();

        public IReadOnlyList<LogMessage> LineWarnings => content.Where(m => m is LineWarning).ToList();

        public IReadOnlyList<LogMessage> AllErrors => content.Where(m => m is Error or LineError).ToList();
        public IReadOnlyList<LogMessage> AllWarnings => content.Where(m => m is Warning or LineWarning).ToList();

        public bool HasErrors { get; private set; }

        public MessageLog()
        {
            content = new List<LogMessage>();
        }

        public void LogMessage(string msg, SourcePosition start = null, SourcePosition end = null)
        {
            if (start == null && end == null)
                content.Add(new LogMessage(msg));
            else if (end == null)
                content.Add(new PositionedMessage(msg, start, start.GetModifiedPosition(0, 1, 1)));
            else
                content.Add(new PositionedMessage(msg, start, end));
        }

        public void LogError(string msg, SourcePosition start = null, SourcePosition end = null)
        {
            HasErrors = true;
            if (start == null && end == null)
                content.Add(new Error(msg));
            else if (end == null)
                content.Add(new LineError(msg, start, start.GetModifiedPosition(0, 1, 1)));
            else
                content.Add(new LineError(msg, start, end));
        }

        public void LogWarning(string msg, SourcePosition start = null, SourcePosition end = null)
        {
            if (start == null && end == null)
                content.Add(new Warning(msg));
            else if (end == null)
                content.Add(new LineWarning(msg, start, start.GetModifiedPosition(0, 1, 1)));
            else
                content.Add(new LineWarning(msg, start, end));
        }

        public override string ToString()
        {
            return string.Join("\n", content);
        }
    }
}
