using System.Collections.Generic;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UByteProperty : UProperty
    {
        public bool IsEnum => Enum.value != 0;

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
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0,
                Enum = 0
            };
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((Enum, nameof(Enum)));
            return uIndices;
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
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0,
                ObjectRef = 0
            };
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((ObjectRef, nameof(ObjectRef)));
            return uIndices;
        }
    }

    public class UComponentProperty : UObjectProperty
    {
        public new static UComponentProperty Create()
        {
            return new()
            {
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0,
                ObjectRef = 0
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
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0,
                ObjectRef = 0,
                ClassRef = 0
            };
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((ClassRef, nameof(ClassRef)));
            return uIndices;
        }
    }

    public class UInterfaceProperty : UObjectProperty
    {
        public new static UInterfaceProperty Create()
        {
            return new()
            {
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0,
                ObjectRef = 0
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
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0,
                ElementType = 0
            };
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((ElementType, nameof(ElementType)));
            return uIndices;
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
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0,
                Struct = 0
            };
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((Struct, nameof(Struct)));
            return uIndices;
        }
    }

    public class UBioMask4Property : UByteProperty
    {
        public static UBioMask4Property Create()
        {
            return new()
            {
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0,
                Enum = 0
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
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0,
                KeyType = 0,
                ValueType = 0
            };
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((KeyType, nameof(KeyType)));
            uIndices.Add((ValueType, nameof(ValueType)));
            return uIndices;
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
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0,
                Function = 0,
                Delegate = 0
            };
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((Function, nameof(Function)));
            uIndices.Add((Delegate, nameof(Delegate)));
            return uIndices;
        }
    }
}
