using System;
using System.Collections.Generic;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class StaticArrayType : VariableType, IEquatable<StaticArrayType>
    {
        public VariableType ElementType;
        public int Size;

        public StaticArrayType(VariableType elementType, int size, SourcePosition start = null, SourcePosition end = null) : base(elementType.Name, start, end)
        {
            ElementType = elementType;
            Size = size;
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

        public bool Equals(StaticArrayType other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(ElementType, other.ElementType) && Size == other.Size;
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
                return ((ElementType != null ? ElementType.GetHashCode() : 0) * 397) ^ Size;
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
