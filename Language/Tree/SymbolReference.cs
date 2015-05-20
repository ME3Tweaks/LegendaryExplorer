using ME3Script.Analysis.Visitors;
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
        public ASTNode Symbol;

        public SymbolReference(ASTNode symbol, SourcePosition start, SourcePosition end) 
            : base(ASTNodeType.SymbolReference, start, end)
        {
            Symbol = symbol;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
