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
        public int Index;

        public ArraySymbolRef(String symbol, int index, SourcePosition start, SourcePosition end) 
            : base(symbol, start, end)
        {
            Index = index;
            Type = ASTNodeType.ArrayReference;
        }
    }
}
