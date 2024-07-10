using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class PrimitiveType : VariableType
    {
        public PrimitiveType(string name, EPropertyType propType) : base(name, -1, -1, propType)
        {
        }
    }
}
