using Unrealscript.Utilities;

namespace Unrealscript.Compiling.Errors
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
