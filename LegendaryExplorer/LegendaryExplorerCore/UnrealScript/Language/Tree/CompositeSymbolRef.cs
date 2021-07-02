using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class CompositeSymbolRef : SymbolReference
    {
        public override ASTNode Node => (InnerSymbol as SymbolReference)?.Node ?? InnerSymbol;

        public Expression InnerSymbol;
        public Expression OuterSymbol;
        public bool IsClassContext;
        public bool IsStructMemberExpression;

        public CompositeSymbolRef(Expression outer, Expression inner, bool isClassContext = false, SourcePosition start = null, SourcePosition end = null)
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
