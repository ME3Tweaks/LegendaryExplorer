using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class RotatorLiteral : Expression
    {
        public int Pitch;
        public int Yaw;
        public int Roll;

        public RotatorLiteral(int pitch, int yaw, int roll, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.RotatorLiteral, start, end)
        {
            Pitch = pitch;
            Yaw = yaw;
            Roll = roll;
        }

        public override VariableType ResolveType()
        {
            return new VariableType(Keywords.ROTATOR)
            {
                PropertyType = EPropertyType.Rotator
            };
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
