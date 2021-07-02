namespace ME3ExplorerCore.UnrealScript.Compiling.Errors
{
    public class Error : LogMessage
    {
        public Error(string msg) : base(msg) { }

        public override string ToString()
        {
            return "ERROR: " + Message;
        }
    }
}
