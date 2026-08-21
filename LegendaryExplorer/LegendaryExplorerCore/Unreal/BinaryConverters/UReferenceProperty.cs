using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public partial class UByteProperty : UProperty
    {
        public bool IsEnum => Enum != 0;

        [UIndexRef("Enum")]
        public UIndex Enum;
        protected override void Serialize(SerializingContainer sc)
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

    public partial class UObjectProperty : UProperty
    {
        [UIndexRef("Class")]
        public UIndex ObjectRef;
        protected override void Serialize(SerializingContainer sc)
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

    public partial class UClassProperty : UObjectProperty
    {
        [UIndexRef("Class")]
        public UIndex ClassRef;
        protected override void Serialize(SerializingContainer sc)
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

    public partial class UArrayProperty : UProperty
    {
        [UIndexRef("Property")]
        public UIndex ElementType;
        protected override void Serialize(SerializingContainer sc)
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

    public partial class UStructProperty : UProperty
    {
        [UIndexRef("ScriptStruct")]
        public UIndex Struct;
        protected override void Serialize(SerializingContainer sc)
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

    public partial class UMapProperty : UProperty
    {
        [UIndexRef("Property")]
        public UIndex KeyType;
        [UIndexRef("Property")]
        public UIndex ValueType;
        protected override void Serialize(SerializingContainer sc)
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
    public partial class UDelegateProperty : UProperty
    {
        [UIndexRef("Function")]
        public UIndex Function;
        [UIndexRef("Function")]
        public UIndex Delegate;
        protected override void Serialize(SerializingContainer sc)
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
