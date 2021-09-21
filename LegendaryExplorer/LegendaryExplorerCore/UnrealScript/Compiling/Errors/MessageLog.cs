using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Compiling.Errors
{
    public class MessageLog
    {
        private readonly List<LogMessage> content;
        public IList<LogMessage> Content => content.AsReadOnly();

        public IList<LogMessage> Messages => content.Where(m => m.GetType() == typeof(LogMessage)).ToList();

        public IList<LogMessage> PositionedMessages => content.Where(m => m.GetType() == typeof(PositionedMessage)).ToList();

        public IList<LogMessage> Errors => content.Where(m => m is Error).ToList();

        public IList<LogMessage> LineErrors => content.Where(m => m is LineError).ToList();

        public IList<LogMessage> Warnings => content.Where(m => m is Warning).ToList();

        public IList<LogMessage> LineWarnings => content.Where(m => m is LineWarning).ToList();

        public IList<LogMessage> AllErrors => content.Where(m => m is Error or LineError).ToList();
        public IList<LogMessage> AllWarnings => content.Where(m => m is Warning or LineWarning).ToList();

        public MessageLog()
        {
            content = new List<LogMessage>();
        }

        public void Log(LogMessage msg)
        {
            content.Add(msg);
        }

        public void LogMessage(string msg, SourcePosition start = null, SourcePosition end = null)
        {
            if (start == null && end == null)
                content.Add(new LogMessage(msg));
            else
                content.Add(new PositionedMessage(msg, start, end));
        }

        public void LogError(string msg, SourcePosition start = null, SourcePosition end = null)
        {
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
            else
                content.Add(new LineWarning(msg, start, end));
        }

        public override string ToString()
        {
            return string.Join("\n", content);
        }
    }
}
