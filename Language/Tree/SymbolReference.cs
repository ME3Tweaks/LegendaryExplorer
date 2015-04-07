using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class SymbolReference : Expression
    {
        public String Symbol;

        public SymbolReference(String symbol, SourcePosition start, SourcePosition end) 
            : base(ASTNodeType.SymbolReference, start, end)
        {
            Symbol = symbol;
        }
    }
}
