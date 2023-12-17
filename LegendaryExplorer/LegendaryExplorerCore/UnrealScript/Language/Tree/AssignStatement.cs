﻿using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class AssignStatement : Statement
    {
        public Expression Target;
        public Expression Value;
        public AssignStatement(Expression target, Expression value,
            int start = -1, int end = -1)
            : base(ASTNodeType.AssignStatement, start, end)
        {
            Target = target;
            Value = value;
        }

        public override bool AcceptVisitor(IASTVisitor visitor, UnrealScriptOptionsPackage usop)
        {
            return visitor.VisitNode(this, usop);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Target;
                yield return Value;
            }
        }
    }
}
