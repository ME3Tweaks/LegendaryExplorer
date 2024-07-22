using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UByteProperty : UProperty
    {
        public bool IsEnum => Enum != 0;

        public UIndex Enum;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Enum);
        }

        public static UByteProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            base.ForEachUIndex(game, in action);
            Unsafe.AsRef(in action).Invoke(ref Enum, nameof(Enum));
        }
    }

    public class UObjectProperty : UProperty
    {
        public UIndex ObjectRef;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ObjectRef);
        }

        public static UObjectProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            base.ForEachUIndex(game, in action);
            Unsafe.AsRef(in action).Invoke(ref ObjectRef, nameof(ObjectRef));
        }
    }

    public class UComponentProperty : UObjectProperty
    {
        public new static UComponentProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }
    }

    public class UClassProperty : UObjectProperty
    {
        public UIndex ClassRef;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ClassRef);
        }

        public new static UClassProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            base.ForEachUIndex(game, in action);
            Unsafe.AsRef(in action).Invoke(ref ClassRef, nameof(ClassRef));
        }
    }

    public class UInterfaceProperty : UObjectProperty
    {
        public new static UInterfaceProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }
    }

    public class UArrayProperty : UProperty
    {
        public UIndex ElementType;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ElementType);
        }

        public static UArrayProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            base.ForEachUIndex(game, in action);
            Unsafe.AsRef(in action).Invoke(ref ElementType, nameof(ElementType));
        }
    }

    public class UStructProperty : UProperty
    {
        public UIndex Struct;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Struct);
        }

        public static UStructProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            base.ForEachUIndex(game, in action);
            Unsafe.AsRef(in action).Invoke(ref Struct, nameof(Struct));
        }
    }

    public class UBioMask4Property : UByteProperty
    {
        public new static UBioMask4Property Create()
        {
            return new()
            {
                Category = "None"
            };
        }
    }

    public class UMapProperty : UProperty
    {
        public UIndex KeyType;
        public UIndex ValueType;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref KeyType);
            sc.Serialize(ref ValueType);
        }

        public static UMapProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            base.ForEachUIndex(game, in action);
            Unsafe.AsRef(in action).Invoke(ref KeyType, nameof(KeyType));
            Unsafe.AsRef(in action).Invoke(ref ValueType, nameof(ValueType));
        }
    }
    public class UDelegateProperty : UProperty
    {
        public UIndex Function;
        public UIndex Delegate;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Function);
            sc.Serialize(ref Delegate);
        }

        public static UDelegateProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            base.ForEachUIndex(game, in action);
            Unsafe.AsRef(in action).Invoke(ref Function, nameof(Function));
            Unsafe.AsRef(in action).Invoke(ref Delegate, nameof(Delegate));
        }
    }
}
