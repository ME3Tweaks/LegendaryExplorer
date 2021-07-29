using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Compiling.Errors
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
