using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Compiling.Errors
{
    public class MessageLog
    {
        private List<LogMessage> content;
        public IList<LogMessage> Content { get { return content.AsReadOnly(); } }
        public IList<LogMessage> Messages
        {
            get { return content.Where(m => m.GetType() == typeof(LogMessage)).ToList(); }
        }
        public IList<LogMessage> PositionedMessages
        {
            get { return content.Where(m => m.GetType() == typeof(PositionedMessage)).ToList(); }
        }
        public IList<LogMessage> Errors
        {
            get { return content.Where(m => m is Error).ToList(); }
        }
        public IList<LogMessage> LineErrors
        {
            get { return content.Where(m => m is LineError).ToList(); }
        }
        public IList<LogMessage> Warnings
        {
            get { return content.Where(m => m is Warning).ToList(); }
        }
        public IList<LogMessage> LineWarnings
        {
            get { return content.Where(m => m is LineWarning).ToList(); }
        }
        public IList<LogMessage> AllErrors
        {
            get { return content.Where(m => m is Error || m is LineError).ToList(); }
        }
        public IList<LogMessage> AllWarnings
        {
            get { return content.Where(m => m is Warning || m is LineWarning).ToList(); }
        }

        public MessageLog()
        {
            content = new List<LogMessage>();
        }

        public void Log(LogMessage msg)
        {
            content.Add(msg);
        }

        public void LogMessage(String msg, SourcePosition start = null, SourcePosition end = null)
        {
            if (start == null && end == null)
                content.Add(new LogMessage(msg));
            else
                content.Add(new PositionedMessage(msg, start, end));
        }

        public void LogError(String msg, SourcePosition start = null, SourcePosition end = null)
        {
            if (start == null && end == null)
                content.Add(new Error(msg));
            else
                content.Add(new LineError(msg, start, end));
        }

        public void LogWarning(String msg, SourcePosition start = null, SourcePosition end = null)
        {
            if (start == null && end == null)
                content.Add(new Warning(msg));
            else
                content.Add(new LineWarning(msg, start, end));
        }
    }
}
