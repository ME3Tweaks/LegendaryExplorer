using System.Collections.Generic;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class CastExpression : Expression
    {
        public VariableType CastType;
        public Expression CastTarget;

        public bool IsInterfaceCast;//TODO:Remove

        public CastExpression(VariableType type, Expression expr, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.CastExpression, start, end)
        {
            CastType = type;
            CastTarget = expr;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
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
