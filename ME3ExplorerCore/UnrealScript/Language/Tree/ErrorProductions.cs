using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Lexing.Tokenizing;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
{
    public class ErrorStatement : Statement
    {
        public Statement InnerStatement;

        public Token<string>[] ErrorTokens; 

        public ErrorStatement(Statement innerStatement) : base(ASTNodeType.INVALID, innerStatement.StartPos, innerStatement.EndPos)
        {
            InnerStatement = innerStatement;
        }

        public ErrorStatement(SourcePosition start, SourcePosition end, params Token<string>[] tokens) : base(ASTNodeType.INVALID, start, end)
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

        public Token<string>[] ErrorTokens;

        public ErrorExpression(Expression innerExpression) : base(ASTNodeType.INVALID, innerExpression.StartPos, innerExpression.EndPos)
        {
            InnerExpression = innerExpression;
        }

        public ErrorExpression(SourcePosition start, SourcePosition end, params Token<string>[] tokens) : base(ASTNodeType.INVALID, start, end)
        {
            ErrorTokens = tokens;
            foreach (Token<string> token in tokens)
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
