using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorerCore.UnrealScript.Compiling.Errors
{
    public class ExternalError : Error
    {
        private readonly Class Class;
        private readonly int Line;

        public ExternalError(string msg, Class cls, int line = -1) : base(msg)
        {
            Class = cls;
            Line = line;
        }

        public override string ToString()
        {
            string className = Class.Name;
            if (Line != -1)
            {
                return $"ERROR in class {className}| Line {Line} |: {Message}";
            }
            return $"ERROR in class {className}: {Message}";
        }
    }
}
