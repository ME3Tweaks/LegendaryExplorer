using System;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class DelegateType : VariableType, IEquatable<DelegateType>
    {
        public Function DefaultFunction;

        public bool IsFunction;

        public DelegateType(Function defaultFunction, SourcePosition start = null, SourcePosition end = null) : base(defaultFunction.Name, start, end, EPropertyType.Delegate)
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
