namespace Unrealscript.Compiling.Errors
{
    public class Warning : LogMessage
    {
        public Warning(string msg) : base(msg) { }

        public override string ToString()
        {
            return "WARNING: " + Message;
        }
    }
}
