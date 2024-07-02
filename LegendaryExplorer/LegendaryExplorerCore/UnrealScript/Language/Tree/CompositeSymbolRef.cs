using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class CompositeSymbolRef : SymbolReference
    {
        public override ASTNode Node => (InnerSymbol as SymbolReference)?.Node ?? InnerSymbol;

        public readonly Expression InnerSymbol;
        public readonly Expression OuterSymbol;
        public readonly bool IsClassContext;
        public bool IsStructMemberExpression;

        public CompositeSymbolRef(Expression outer, Expression inner, bool isClassContext = false, int start = -1, int end = -1)
            : base(inner, start: start, end: end)
        {
            InnerSymbol = inner;
            OuterSymbol = outer;
            IsClassContext = isClassContext;
            Type = ASTNodeType.CompositeReference;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return InnerSymbol.ResolveType();
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return OuterSymbol;
                yield return InnerSymbol;
            }
        }
    }
}
