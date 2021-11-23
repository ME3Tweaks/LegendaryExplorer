using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class ErrorStatement : Statement
    {
        public Statement InnerStatement;

        public ScriptToken[] ErrorTokens; 

        public ErrorStatement(Statement innerStatement) : base(ASTNodeType.INVALID, innerStatement.StartPos, innerStatement.EndPos)
        {
            InnerStatement = innerStatement;
        }

        public ErrorStatement(SourcePosition start, SourcePosition end, params ScriptToken[] tokens) : base(ASTNodeType.INVALID, start, end)
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
        public Expression InnerExpression;

        public ScriptToken[] ErrorTokens;

        public ErrorExpression(Expression innerExpression) : base(ASTNodeType.INVALID, innerExpression.StartPos, innerExpression.EndPos)
        {
            InnerExpression = innerExpression;
        }

        public ErrorExpression(SourcePosition start, SourcePosition end, params ScriptToken[] tokens) : base(ASTNodeType.INVALID, start, end)
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
