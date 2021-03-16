using System.Collections.Generic;
using System.Linq;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class StructLiteral : Expression
    {
        public List<Statement> Statements;

        public string StructType;

        public StructLiteral(string structType, List<Statement> statements, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.StructLiteral, start, end)
        {
            Statements = statements;
            StructType = structType;

            foreach (AssignStatement assignStatement in statements.OfType<AssignStatement>())
            {
                assignStatement.Value.Outer = this;
            }
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return new VariableType(StructType, null, null);
        }
        public override IEnumerable<ASTNode> ChildNodes => Statements;
    }
}
