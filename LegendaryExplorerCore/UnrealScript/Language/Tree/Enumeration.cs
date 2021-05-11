using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
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
