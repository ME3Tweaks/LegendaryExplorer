using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class ForEachLoop : Statement
    {
        public Expression IteratorFunction;
        public List<Expression> Parameters;
        public CodeBody Body;

        public ForEachLoop(Expression iterator, List<Expression> parameters, CodeBody body, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.ForEachLoop, start, end)
        {
            IteratorFunction = iterator;
            Parameters = parameters;
            Body = body;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
