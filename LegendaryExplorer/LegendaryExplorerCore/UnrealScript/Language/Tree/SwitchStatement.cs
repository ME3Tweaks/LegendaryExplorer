﻿using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class SwitchStatement : Statement
    {
        public Expression Expression;
        public CodeBody Body;

        public SwitchStatement(Expression expr, CodeBody body,
            SourcePosition start, SourcePosition end)
            : base(ASTNodeType.SwitchStatement, start, end)
        {
            Expression = expr;
            Body = body;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Expression;
                yield return Body;
            }
        }
    }
}