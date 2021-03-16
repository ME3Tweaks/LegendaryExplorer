using System.Collections.Generic;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class CodeBody : Statement
    {
        public List<Statement> Statements;

        public CodeBody(List<Statement> contents = null, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.CodeBody, start, end) 
        {
            Statements = contents ?? new List<Statement>();
            foreach (Statement statement in Statements)
            {
                statement.Outer = this;
            }
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes => Statements;
    }
}
