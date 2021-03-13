using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;
using static Unrealscript.Utilities.Keywords;

namespace Unrealscript.Language.Tree
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
