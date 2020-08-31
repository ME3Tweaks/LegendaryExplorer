using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Analysis.Symbols;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class DelegateCall : Expression
    {
        public SymbolReference DelegateReference;
        public List<Expression> Arguments;

        public DelegateCall(SymbolReference del, List<Expression> arguments, SourcePosition start = null, SourcePosition end = null)
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
            Function function = ((DelegateType)((VariableDeclaration)DelegateReference.Node).VarType).DefaultFunction;
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
