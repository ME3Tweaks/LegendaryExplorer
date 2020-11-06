namespace ME3Script.Compiling.Errors
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
