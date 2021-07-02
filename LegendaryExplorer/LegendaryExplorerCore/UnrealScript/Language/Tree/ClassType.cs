using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class ClassType : VariableType
    {
        public VariableType ClassLimiter;

        public ClassType(VariableType classLimiter, SourcePosition start = null, SourcePosition end = null) : base(CLASS, start, end, EPropertyType.Object)
        {
            ClassLimiter = classLimiter;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
