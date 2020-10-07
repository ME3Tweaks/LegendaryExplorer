using System.Collections.Generic;

namespace ME3Script.Language.Tree
{
    public interface IObjectType
    {
        public List<VariableDeclaration> VariableDeclarations { get; }
        public List<VariableType> TypeDeclarations { get; }
        public DefaultPropertiesBlock DefaultProperties { get; }

    }
}
