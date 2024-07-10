using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class DelegateCall : Expression
    {
        public SymbolReference DelegateReference;
        public List<Expression> Arguments;
        public Function DefaultFunction => ((DelegateType)((VariableDeclaration)DelegateReference.Node).VarType).DefaultFunction;

        public DelegateCall(SymbolReference del, List<Expression> arguments, int start = -1, int end = -1)
            : base(ASTNodeType.FunctionCall, start, end)
        {
            DelegateReference = del;
            Arguments = arguments;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            Function function = DefaultFunction;
            if (function.CoerceReturn && function.ReturnType != SymbolTable.StringType)
            {
                return ((ClassType)Arguments[0].ResolveType()).ClassLimiter;
            }

            return function.ReturnType;
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return DelegateReference;
                foreach (Expression expression in Arguments) yield return expression;
            }
        }
    }
}
