using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class DynamicArrayType : VariableType, IEquatable<DynamicArrayType>
    {
        public VariableType ElementType;
        public UnrealFlags.EPropertyFlags ElementPropertyFlags;

        public DynamicArrayType(VariableType elementType, int start = -1, int end = -1) : base(elementType.Name, start, end)
        {
            ElementType = elementType;
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

        public bool Equals(DynamicArrayType other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return ElementType.Equals(other.ElementType);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((DynamicArrayType)obj);
        }

        public override int GetHashCode()
        {
            return ElementType.GetHashCode();
        }

        public static bool operator ==(DynamicArrayType left, DynamicArrayType right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(DynamicArrayType left, DynamicArrayType right)
        {
            return !Equals(left, right);
        }
    }
}
