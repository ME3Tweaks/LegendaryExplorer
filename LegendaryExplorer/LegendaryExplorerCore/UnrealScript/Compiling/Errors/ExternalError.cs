using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Compiling.Errors
{
    public class ExternalError : Error
    {
        public Class Class;
        public SourcePosition Start;

        public ExternalError(string msg, Class cls, SourcePosition start = null) : base(msg)
        {
            Class = cls;
            Start = start;
        }

        public override string ToString()
        {
            string className = Class.Name;
            if (Start != null)
            {
                return $"ERROR in class {className}| Line {Start.Line} |: {Message}";
            }
            return $"ERROR in class {className}: {Message}";
        }
    }
}
