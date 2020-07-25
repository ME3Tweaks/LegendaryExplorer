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
        public string Name;

        public SymbolReference(ASTNode symbol, SourcePosition start, SourcePosition end, string name = "") 
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
            return Node switch
            {
                FunctionParameter functionParameter => functionParameter.VarType,
                VariableDeclaration variable => variable.VarType,
                Function function => function.ReturnType,
                _ => (Node as Expression)?.ResolveType()
            };
        }
    }
}
