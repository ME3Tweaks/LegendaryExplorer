using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Parsing;

namespace LegendaryExplorerCore.UnrealScript.Compiling.Errors
{
    public class MessageLog
    {
        public bool HasErrors { get; private set; }

        public bool HasLexErrors { get; private set; }

        public Class CurrentClass;

        public Class Filter; 

        private readonly List<LogMessage> content = [];
        public IReadOnlyList<LogMessage> Content => content.AsReadOnly();

        public IReadOnlyList<LogMessage> AllErrors => content.Where(m => m is Error or LineError).ToList();
        public IReadOnlyList<LogMessage> AllWarnings => content.Where(m => m is Warning or LineWarning).ToList();

        public LineLookup LineLookup;

        //This being here is a gross hack. Needed some way to make the TokenStream accesible to the ClassValidationVisitor,
        //and the MessageLog is the one thing that gets passed through all the layers... bleh
        public TokenStream Tokens;

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

        public void LogLexError(string msg, int start, int end = -1)
        {
            HasLexErrors = true;
            int line = LineLookup?.GetLineFromCharIndex(start) ?? -1;
            content.Add(new LexError(msg, start, end, line));
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
