using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unrealscript.Analysis.Symbols;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public class FunctionCall : Expression
    {
        public SymbolReference Function;
        public List<Expression> Arguments;

        public bool IsCalledOnInterface;

        public FunctionCall(SymbolReference func, List<Expression> arguments, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.FunctionCall, start, end)
        {
            Function = func;
            Arguments = arguments;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            Function function = ((Function)Function.Node);
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
                yield return Function;
                foreach (Expression expression in Arguments) yield return expression;
            }
        }
    }
}
