using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Analysis.Visitors;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
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
