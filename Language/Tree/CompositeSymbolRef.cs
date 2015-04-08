using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class CompositeSymbolRef : SymbolReference
    {
        public SymbolReference InnerSymbol;
        public SymbolReference OuterSymbol;

        public CompositeSymbolRef(SymbolReference outer, SymbolReference inner, SourcePosition start, SourcePosition end)
            : base(outer.Symbol, start, end)
        {
            InnerSymbol = inner;
            OuterSymbol = outer;
            Type = ASTNodeType.CompositeReference;
        }

        public override void VisitNode(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
