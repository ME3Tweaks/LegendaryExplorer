using System.Collections.Generic;
using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
{
    public class IfStatement : Statement
    {
        public Expression Condition;
        public CodeBody Then;
        public CodeBody Else;

        public IfStatement(Expression cond, CodeBody then,
                           CodeBody optelse = null,
                           SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.IfStatement, start, end) 
        {
            Condition = cond;
            Then = then;
            Else = optelse;
            cond.Outer = this;
            then.Outer = this;
            if (optelse is not null)
            {
                optelse.Outer = this;
            }
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
