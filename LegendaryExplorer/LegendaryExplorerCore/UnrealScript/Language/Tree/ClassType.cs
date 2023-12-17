using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class ClassType : VariableType
    {
        public VariableType ClassLimiter;

        public ClassType(VariableType classLimiter, int start = -1, int end = -1) : base(CLASS, start, end, EPropertyType.Object)
        {
            ClassLimiter = classLimiter;
        }

        public override bool AcceptVisitor(IASTVisitor visitor, UnrealScriptOptionsPackage usop)
        {
            return visitor.VisitNode(this, usop);
        }
    }
}
