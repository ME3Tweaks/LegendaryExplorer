using System.Collections.Generic;
using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
{
    public class Enumeration : VariableType
    {
        public List<EnumValue> Values;
        public Enumeration(string name, List<EnumValue> values,
            SourcePosition start, SourcePosition end)
            : base(name, start, end, EPropertyType.Byte)
        {
            Type = ASTNodeType.Enumeration;
            Values = values;
            foreach (EnumValue enumValue in values)
            {
                enumValue.Enum = this;
            }
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes => Values;
    }
}
