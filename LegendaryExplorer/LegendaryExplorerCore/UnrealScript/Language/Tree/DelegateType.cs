using System;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class DelegateType : VariableType, IEquatable<DelegateType>
    {
        public Function DefaultFunction;

        public bool IsFunction;

        public DelegateType(Function defaultFunction, int start = -1, int end = -1) : base(defaultFunction.Name, start, end, EPropertyType.Delegate)
        {
            DefaultFunction = defaultFunction;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public bool Equals(DelegateType other)
        {
            if (other is null) return false;
            return ReferenceEquals(this, other) || DefaultFunction.Equals(other.DefaultFunction);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((DelegateType)obj);
        }

        public override int GetHashCode()
        {
            return DefaultFunction.GetHashCode();
        }

        public static bool operator ==(DelegateType left, DelegateType right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(DelegateType left, DelegateType right)
        {
            return !Equals(left, right);
        }
    }
}
