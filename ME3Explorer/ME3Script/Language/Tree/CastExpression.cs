using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
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
