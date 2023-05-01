using System;

namespace LegendaryExplorerCore.Coalesced
{
    /// <summary>
    /// The type of action for this coalesced line. In Game3 terminology this is the type, in 1/2 it is the prefix.
    /// </summary>
    public enum CoalesceParseAction
    {
        // Type 2 - Add always (No prefix)
        Add,
        // Type 3 - Add if unique (No prefix)
        AddUnique,
        // Type 0 - Overwrite
        New,
        None,
        // Type 4 - Remove if same
        Remove,
        // Type 1 - Remove entirely
        RemoveProperty
    }

    public struct CoalesceValue : IEquatable<CoalesceValue>
    {
        public CoalesceValue(string value = null, int? valueType = null)
            : this()
        {
            Value = value;
            ValueType = valueType ?? CoalesceProperty.DefaultValueType;
        }

        public CoalesceValue(string value, CoalesceParseAction valueType)
            : this()
        {
            Value = value;
            ValueType = GetValueType(valueType);
        }

        public static int GetValueType(CoalesceParseAction valueType)
        {
            switch (valueType)
            {
                case CoalesceParseAction.New:
                    return 0;
                case CoalesceParseAction.RemoveProperty:
                    return 1;
                case CoalesceParseAction.Add:
                    return 2;
                case CoalesceParseAction.AddUnique:
                    return 3;
                case CoalesceParseAction.Remove:
                    return 4;
            }

            return CoalesceProperty.DefaultValueType;
        }

        public bool IsNull
        {
            get { return Value == null || ValueType == CoalesceProperty.NullValueType; }
        }

        public CoalesceParseAction ParseAction
        {
            get
            {
                switch (ValueType)
                {
                    case 0:
                        {
                            return CoalesceParseAction.New;
                        }
                    case 1:
                        {
                            return CoalesceParseAction.RemoveProperty;
                        }
                    case 2:
                        {
                            return CoalesceParseAction.Add;
                        }
                    case 3:
                        {
                            return CoalesceParseAction.AddUnique;
                        }
                    case 4:
                        {
                            return CoalesceParseAction.Remove;
                        }
                    default:
                        {
                            return CoalesceParseAction.None;
                        }
                }
            }
        }

        public string Value { get; set; }

        public int ValueType { get; set; }

        public static bool operator ==(CoalesceValue v1, CoalesceValue v2)
        {
            return v1.Equals(v2);
        }

        public static implicit operator CoalesceValue(string value)
        {
            return new CoalesceValue(value);
        }

        public static implicit operator string(CoalesceValue value)
        {
            return value.Value;
        }

        public static bool operator !=(CoalesceValue v1, CoalesceValue v2)
        {
            return !(v1 == v2);
        }

        public bool Equals(CoalesceValue other)
        {
            return Equals(other.Value) && ValueType == other.ValueType;
        }

        public bool Equals(string other)
        {
            return string.Equals(Value, other, StringComparison.InvariantCultureIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return obj is CoalesceValue && Equals((CoalesceValue)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Value != null ? Value.GetHashCode() : 0) * 397) ^ ValueType;
            }
        }
    }
}
