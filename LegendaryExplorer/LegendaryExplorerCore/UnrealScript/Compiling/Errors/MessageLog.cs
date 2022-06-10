using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Parsing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Compiling.Errors
{
    public class MessageLog
    {
        public bool HasErrors { get; private set; }

        public Class CurrentClass;

        public Class Filter; 

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

        public LineLookup LineLookup;


        public MessageLog()
        {
            content = new List<LogMessage>();
        }
        public void LogMessage(string msg)
        {
            if (Filter is not null && CurrentClass is not null && Filter != CurrentClass)
            {
                return;
            }
            content.Add(new LogMessage(msg));
        }

        public void LogMessage(string msg, int start, int end = -1)
        {
            if (Filter is not null && CurrentClass is not null && Filter != CurrentClass)
            {
                return;
            }
            if (start is -1)
            {
                LogMessage(msg);
                return;
            }
            int line = LineLookup?.GetLineFromCharIndex(start) ?? -1;
            content.Add(new PositionedMessage(msg, start, end, line));
        }

        public void LogError(string msg)
        {
            HasErrors = true;
            if (Filter is not null && CurrentClass is not null && Filter != CurrentClass)
            {
                content.Add(new ExternalError(msg, CurrentClass));
                return;
            }
            content.Add(new Error(msg));
        }

        public void LogError(string msg, int start, int end = -1)
        {
            HasErrors = true;
            if (Filter is not null && CurrentClass is not null && Filter != CurrentClass)
            {
                content.Add(new ExternalError(msg, CurrentClass));
                return;
            }
            if (start is -1)
            {
                LogError(msg);
                return;
            }
            int line = LineLookup?.GetLineFromCharIndex(start) ?? -1;
            content.Add(new LineError(msg, start, end, line));
        }

        public void LogWarning(string msg)
        {
            if (Filter is not null && CurrentClass is not null && Filter != CurrentClass)
            {
                return;
            }
            content.Add(new Warning(msg));
        }

        public void LogWarning(string msg, int start, int end = -1)
        {
            if (Filter is not null && CurrentClass is not null && Filter != CurrentClass)
            {
                return;
            }
            if (start is -1)
            {
                LogWarning(msg);
                return;
            }
            int line = LineLookup?.GetLineFromCharIndex(start) ?? -1;
            content.Add(new LineWarning(msg, start, end, line));
        }

        public override string ToString()
        {
            return string.Join("\n", content);
        }
    }
}
