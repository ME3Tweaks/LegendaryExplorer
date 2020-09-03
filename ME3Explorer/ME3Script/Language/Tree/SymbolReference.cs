using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class SymbolReference : Expression
    {
        public ASTNode Node;
        public string Name;

        public SymbolReference(ASTNode symbol, string name = "", SourcePosition start = null, SourcePosition end = null) 
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
                VariableDeclaration variable => variable.VarType,
                Function func => new DelegateType(func),
                VariableType type => type,
                _ => (Node as Expression)?.ResolveType()
            };
        }
    }
}
