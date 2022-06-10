using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public abstract class ObjectType : VariableType
    {
        public abstract List<VariableDeclaration> VariableDeclarations { get; }
        public abstract List<VariableType> TypeDeclarations { get; }
        public abstract DefaultPropertiesBlock DefaultProperties { get; set; }

        protected ObjectType(string name, int start = -1, int end = -1, EPropertyType propType = EPropertyType.None) : base(name, start, end, propType)
        {
        }
    }
}
