﻿using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class ExpressionOnlyStatement : Statement
    {
        public Expression Value;

        public ExpressionOnlyStatement(Expression value, int start = -1, int end = -1)
            : base(ASTNodeType.ExpressionStatement, start, end)
        {
            Value = value;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get { yield return Value; }
        }
    }
}
