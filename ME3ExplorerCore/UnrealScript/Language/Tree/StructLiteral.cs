using System.Collections.Generic;
using System.Linq;
using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
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
