using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Parsing;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class ArraySymbolRef : SymbolReference
    {
        public Expression Index;
        public Expression Array;

        public bool IsDynamic => Array.ResolveType() is DynamicArrayType;

        public ArraySymbolRef(Expression array, Expression index, int start, int end) 
            : base(array, start: start, end: end)
        {
            Index = index;
            Type = ASTNodeType.ArrayReference;
            Array = array;
        }

        public override VariableType ResolveType()
        {
            return Array.ResolveType() switch
            {
                 DynamicArrayType dynArrType => dynArrType.ElementType,
                 StaticArrayType staticArrayType => staticArrayType.ElementType,
                _ => throw new ParseException("Expected an array!")
            };
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return Array;
                yield return Index;
            }
        }
    }
}
