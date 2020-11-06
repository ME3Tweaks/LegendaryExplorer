using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using static ME3Script.Utilities.Keywords;

namespace ME3Script.Language.Tree
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
