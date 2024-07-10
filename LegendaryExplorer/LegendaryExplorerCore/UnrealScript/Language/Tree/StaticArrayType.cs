using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class StaticArrayType : VariableType, IEquatable<StaticArrayType>
    {
        public VariableType ElementType;
        public readonly int Length;

        public StaticArrayType(VariableType elementType, int length, int start = -1, int end = -1) : base(elementType.Name, start, end)
        {
            ElementType = elementType;
            Length = length;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                if (Declaration != null) yield return Declaration;
                yield return ElementType;
            }
        }

        public override int Size(MEGame game) => (ElementType?.Size(game) ?? 0) * Length;

        public bool Equals(StaticArrayType other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(ElementType, other.ElementType) && Length == other.Length;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((StaticArrayType)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ElementType != null ? ElementType.GetHashCode() : 0) * 397) ^ Length;
            }
        }

        public static bool operator ==(StaticArrayType left, StaticArrayType right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(StaticArrayType left, StaticArrayType right)
        {
            return !Equals(left, right);
        }
    }
}
