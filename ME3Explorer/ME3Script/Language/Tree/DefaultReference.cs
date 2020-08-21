using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class DefaultReference : SymbolReference
    {
        public DefaultReference(ASTNode symbol, string name = "", SourcePosition start = null, SourcePosition end = null) : base(symbol, name, start, end)
        {
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
