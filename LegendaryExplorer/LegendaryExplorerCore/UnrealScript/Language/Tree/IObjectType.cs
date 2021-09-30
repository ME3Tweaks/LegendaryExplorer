using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public abstract class ObjectType : VariableType
    {
        public abstract List<VariableDeclaration> VariableDeclarations { get; }
        public abstract List<VariableType> TypeDeclarations { get; }
        public abstract DefaultPropertiesBlock DefaultProperties { get; set; }

        protected ObjectType(string name, SourcePosition start = null, SourcePosition end = null, EPropertyType propType = EPropertyType.None) : base(name, start, end, propType)
        {
        }
    }
}
