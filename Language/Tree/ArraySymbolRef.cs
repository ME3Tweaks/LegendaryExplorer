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
        public Expression Array;

        public ArraySymbolRef(Expression array, Expression index, SourcePosition start, SourcePosition end) 
            : base(array, start, end)
        {
            Index = index;
            Type = ASTNodeType.ArrayReference;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
