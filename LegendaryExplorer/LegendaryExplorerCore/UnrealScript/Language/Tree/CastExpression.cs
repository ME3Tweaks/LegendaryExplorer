﻿using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class CastExpression : Expression
    {
        public readonly VariableType CastType;
        public readonly Expression CastTarget;

        public bool IsInterfaceCast;//TODO:Remove

        public CastExpression(VariableType type, Expression expr, int start = -1, int end = -1)
            : base(ASTNodeType.CastExpression, start, end)
        {
            CastType = type;
            CastTarget = expr;
        }

        public override bool AcceptVisitor(IASTVisitor visitor, UnrealScriptOptionsPackage usop)
        {
            return visitor.VisitNode(this, usop);
        }

        public override VariableType ResolveType()
        {
            return CastType;
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return CastType;
                yield return CastTarget;
            }
        }
    }
}
