namespace LegendaryExplorerCore.UnrealScript.Compiling.Errors
{
    public class PositionedMessage : LogMessage
    {
        public readonly int Start;
        public readonly int End;
        public readonly int Line;

        public PositionedMessage(string msg, int start, int end, int line) : base(msg)
        {
            Start = start;
            if (end == -1)
            {
                end = start + 1;
            }
            End = end;
            Line = line;
        }

        public override string ToString()
        {
            return "LOG| Line " + Line + " |: " + Message; 
        }
    }
}
