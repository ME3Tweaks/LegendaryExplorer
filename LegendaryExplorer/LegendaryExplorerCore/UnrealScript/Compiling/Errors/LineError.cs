namespace LegendaryExplorerCore.UnrealScript.Compiling.Errors
{
    public class LineError : PositionedMessage
    {
        public LineError(string msg, int start, int end, int line)
            : base(msg, start, end, line) { }

        public override string ToString()
        {
            return "ERROR| Line " + Line + " |: " + Message; 
        }
    }
}
