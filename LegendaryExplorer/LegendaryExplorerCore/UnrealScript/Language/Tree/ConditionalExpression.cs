﻿using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class ConditionalExpression : Expression
    {
        public Expression Condition;
        public Expression TrueExpression;
        public Expression FalseExpression;

        public VariableType ExpressionType;

        public ConditionalExpression(Expression cond, Expression first, Expression second, int start, int end)
            : base(ASTNodeType.ConditionalExpression, start, end)
        {
            Condition = cond;
            TrueExpression = first;
            FalseExpression = second;
        }

        public override bool AcceptVisitor(IASTVisitor visitor, UnrealScriptOptionsPackage usop)
        {
            return visitor.VisitNode(this, usop);
        }

        public override VariableType ResolveType()
        {
            return ExpressionType ?? TrueExpression.ResolveType() ?? FalseExpression.ResolveType();
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Condition;
                yield return TrueExpression;
                yield return FalseExpression;
            }
        }
    }
}
