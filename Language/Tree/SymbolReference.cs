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

        public override VariableType ResolveType()
        {
            if (Symbol is Variable)
                return (Symbol as Variable).VarType;
            if (Symbol is FunctionParameter)
                return (Symbol as FunctionParameter).VarType;
            if (Symbol is Function)
                return (Symbol as Function).ReturnType;
            return null;
        }
    }
}
