using System.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public abstract class ObjectType : VariableType
    {
        public VariableType Parent;
        public abstract List<VariableDeclaration> VariableDeclarations { get; }
        public abstract List<VariableType> TypeDeclarations { get; }
        public abstract DefaultPropertiesBlock DefaultProperties { get; set; }

        protected ObjectType(string name, int start = -1, int end = -1, EPropertyType propType = EPropertyType.None) : base(name, start, end, propType)
        {
        }

        public Struct LookupStruct(string structName, bool lookInParents = true)
        {
            foreach (VariableType type in TypeDeclarations)
            {
                if (type is Struct @struct && @struct.Name.CaseInsensitiveEquals(structName))
                {
                    return @struct;
                }
            }
            if (lookInParents)
            {
                return (Parent as ObjectType)?.LookupStruct(structName);
            }
            return null;
        }

        public VariableDeclaration LookupVariable(string varName, bool lookInParents = true)
        {
            foreach (VariableDeclaration declaration in VariableDeclarations)
            {
                if (declaration.Name.CaseInsensitiveEquals(varName))
                {
                    return declaration;
                }
            }
            if (lookInParents)
            {
                return (Parent as ObjectType)?.LookupVariable(varName);
            }
            return null;
        }
    }
}
