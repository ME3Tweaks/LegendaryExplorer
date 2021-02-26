using ME3Script.Utilities;

namespace ME3Script.Compiling.Errors
{
    public class LineError : PositionedMessage
    {
        public LineError(string msg, SourcePosition start, SourcePosition end)
            : base(msg, start, end) { }

        public override string ToString()
        {
            return "ERROR| Line " + Line + " |: " + Message; 
        }
    }
}
