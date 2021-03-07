using ME3Script.Utilities;

namespace ME3Script.Compiling.Errors
{
    public class PositionedMessage : LogMessage
    {
        public SourcePosition Start;
        public SourcePosition End;
        public int Line => Start.Line;

        public PositionedMessage(string msg, SourcePosition start, SourcePosition end) : base(msg)
        {
            Start = start;
            End = end;
        }

        public override string ToString()
        {
            return "LOG| Line " + Line + " |: " + Message; 
        }
    }
}
