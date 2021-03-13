using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unrealscript.Analysis.Symbols;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class DelegateCall : Expression
    {
        public SymbolReference DelegateReference;
        public List<Expression> Arguments;
        public Function DefaultFunction => ((DelegateType)((VariableDeclaration)DelegateReference.Node).VarType).DefaultFunction;

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
