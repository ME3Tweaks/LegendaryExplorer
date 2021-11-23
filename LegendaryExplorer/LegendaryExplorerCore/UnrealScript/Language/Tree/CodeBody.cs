using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Parsing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class CodeBody : Statement
    {
        public List<Statement> Statements;
        public TokenStream Tokens;

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
