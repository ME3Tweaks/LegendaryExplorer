namespace LegendaryExplorerCore.UnrealScript.Compiling.Errors
{
    public class LexError : LineError
    {
        public LexError(string msg, int start, int end, int line) : base(msg, start, end, line)
        {
        }
    }
}
