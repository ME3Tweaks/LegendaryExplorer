using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class Const : VariableType
    {
        public string Value;
        public Expression Literal;

        public Const(string name, string value, SourcePosition start = null, SourcePosition end = null) : base(name, start, end)
        {
            Type = ASTNodeType.Const;
            Value = value;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
