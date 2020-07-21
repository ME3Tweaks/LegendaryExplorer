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
        public ASTNode Node;
        public String Name;

        public SymbolReference(ASTNode symbol, SourcePosition start, SourcePosition end, String name = "") 
            : base(ASTNodeType.SymbolReference, start, end)
        {
            Node = symbol;
            Name = name;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            if (Node is Variable)
                return (Node as Variable).VarType;
            if (Node is FunctionParameter)
                return (Node as FunctionParameter).VarType;
            if (Node is Function)
                return (Node as Function).ReturnType;
            return (Node as Expression).ResolveType();
        }
    }
}
