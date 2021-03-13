using Unrealscript.Utilities;

namespace Unrealscript.Compiling.Errors
{
    public class LineWarning : PositionedMessage
    {
        public LineWarning(string msg, SourcePosition start, SourcePosition end)
            : base(msg, start, end) { }

        public override string ToString()
        {
            return "WARNING| Line " + Line + " |: " + Message; 
        }
    }
}
