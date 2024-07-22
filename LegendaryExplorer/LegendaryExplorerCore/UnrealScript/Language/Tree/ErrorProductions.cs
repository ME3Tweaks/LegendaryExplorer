using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Lexing;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class ErrorStatement : Statement
    {
        public readonly Statement InnerStatement;

        public readonly ScriptToken[] ErrorTokens; 

        public ErrorStatement(Statement innerStatement) : base(ASTNodeType.INVALID, innerStatement.StartPos, innerStatement.EndPos)
        {
            InnerStatement = innerStatement;
        }

        public ErrorStatement(int start, int end, params ScriptToken[] tokens) : base(ASTNodeType.INVALID, start, end)
        {
            ErrorTokens = tokens;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }

    public class ErrorExpression : Expression
    {
        public readonly Expression InnerExpression;

        public readonly ScriptToken[] ErrorTokens;

        public ErrorExpression(Expression innerExpression) : base(ASTNodeType.INVALID, innerExpression.StartPos, innerExpression.EndPos)
        {
            InnerExpression = innerExpression;
        }

        public ErrorExpression(int start, int end, params ScriptToken[] tokens) : base(ASTNodeType.INVALID, start, end)
        {
            ErrorTokens = tokens;
            foreach (ScriptToken token in tokens)
            {
                token.SyntaxType = EF.ERROR;
            }
        }

        public override VariableType ResolveType()
        {
            return InnerExpression?.ResolveType();
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
