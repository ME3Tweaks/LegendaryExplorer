using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;
using static ME3ExplorerCore.UnrealScript.Utilities.Keywords;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
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
