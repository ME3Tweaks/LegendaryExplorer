using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class LambdaExpression : Expression
    {
        public DelegateType DelegateType;

        public Function Lambda;

        public LambdaExpression(DelegateType delegateType, Function lambda, int start = -1, int end = -1) : base(ASTNodeType.LambdaExpression, start, end)
        {
            DelegateType = delegateType;
            Lambda = lambda;
        }

        public override VariableType ResolveType()
        {
            return DelegateType;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
