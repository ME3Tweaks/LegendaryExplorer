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

        public CompositeSymbolRef(String symbol, SymbolReference inner, SourcePosition start, SourcePosition end)
            : base(symbol, start, end)
        {
            InnerSymbol = inner;
            Type = ASTNodeType.CompositeReference;
        }
    }
}
