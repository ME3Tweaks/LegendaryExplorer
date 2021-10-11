using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class StructLiteral : Expression
    {
        public List<Statement> Statements;

        public readonly Struct StructType;

        public StructLiteral(Struct structType, List<Statement> statements, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.StructLiteral, start, end)
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

        public override VariableType ResolveType() => StructType;

        public override IEnumerable<ASTNode> ChildNodes => Statements;
    }
}
