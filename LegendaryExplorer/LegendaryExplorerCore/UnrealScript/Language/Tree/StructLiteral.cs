using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class StructLiteral : Expression
    {
        public readonly List<AssignStatement> Statements;

        public readonly Struct StructType;

        public StructLiteral(Struct structType, List<AssignStatement> statements, int start = -1, int end = -1) : base(ASTNodeType.StructLiteral, start, end)
        {
            Statements = statements;
            StructType = structType;

            foreach (AssignStatement assignStatement in statements)
            {
                assignStatement.Value.Outer = this;
            }
        }

        public override bool AcceptVisitor(IASTVisitor visitor, UnrealScriptOptionsPackage usop)
        {
            return visitor.VisitNode(this, usop);
        }

        public override VariableType ResolveType() => StructType;

        public override IEnumerable<ASTNode> ChildNodes => Statements;
    }
}
