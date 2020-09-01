using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System.Collections.Generic;

namespace ME3Script.Language.Tree
{
    public class FunctionCall : Expression
    {
        public SymbolReference Function;
        public List<Expression> Parameters;

        public FunctionCall(SymbolReference func, List<Expression> parameters, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.FunctionCall, start, end)
        {
            Function = func;
            Parameters = parameters;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return ((Function)Function.Node).ReturnType;
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Function;
                foreach (Expression expression in Parameters) yield return expression;
            }
        }
    }
}
