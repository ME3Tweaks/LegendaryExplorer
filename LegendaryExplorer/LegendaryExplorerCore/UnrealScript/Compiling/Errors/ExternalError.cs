using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorerCore.UnrealScript.Compiling.Errors
{
    public class ExternalError : Error
    {
        private readonly string className;
        private readonly int Line;

        public ExternalError(string msg, Class cls, int line = -1) : base(msg)
        {
            className = cls.Name;
            Line = line;
        }

        public override string ToString()
        {
            if (Line != -1)
            {
                return $"ERROR in class {className}| Line {Line} |: {Message}";
            }
            return $"ERROR in class {className}: {Message}";
        }
    }
}
