using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class ArraySymbolRef : SymbolReference
    {
        public Expression Index;

        public ArraySymbolRef(ASTNode symbol, Expression index, SourcePosition start, SourcePosition end, String name = "") 
            : base(symbol, start, end)
        {
            Index = index;
            Type = ASTNodeType.ArrayReference;
            Name = name;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
