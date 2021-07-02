using System.Collections.Generic;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public interface IObjectType
    {
        public List<VariableDeclaration> VariableDeclarations { get; }
        public List<VariableType> TypeDeclarations { get; }
        public DefaultPropertiesBlock DefaultProperties { get; }

    }
}
