namespace ME3ExplorerCore.UnrealScript.Compiling.Errors
{
    public class LogMessage
    {
        public string Message;

        public LogMessage(string msg)
        {
            Message = msg;
        }

        public override string ToString()
        {
            return "LOG: " + Message;
        }
    }
}
