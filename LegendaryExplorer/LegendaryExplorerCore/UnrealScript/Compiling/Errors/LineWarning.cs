namespace LegendaryExplorerCore.UnrealScript.Compiling.Errors
{
    public class LineWarning : PositionedMessage
    {
        public LineWarning(string msg, int start, int end, int line)
            : base(msg, start, end, line) { }

        public override string ToString()
        {
            return "WARNING| Line " + Line + " |: " + Message; 
        }
    }
}
