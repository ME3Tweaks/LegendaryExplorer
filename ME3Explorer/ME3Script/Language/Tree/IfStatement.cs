using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class IfStatement : Statement
    {
        public Expression Condition;
        public CodeBody Then;
        public CodeBody Else;

        public bool IsNullCheck;

        public IfStatement(Expression cond, CodeBody then,
            SourcePosition start, SourcePosition end, CodeBody optelse = null)
            : base(ASTNodeType.IfStatement, start, end) 
        {
            Condition = cond;
            Then = then;
            Else = optelse;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Condition;
                yield return Then;
                yield return Else;
            }
        }
    }
}
