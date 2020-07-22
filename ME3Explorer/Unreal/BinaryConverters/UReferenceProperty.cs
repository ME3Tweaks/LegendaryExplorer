using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.BinaryConverters
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

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((ObjectRef, nameof(ObjectRef)));
            return uIndices;
        }
    }

    public class UComponentProperty : UObjectProperty
    {
    }

    public class UClassProperty : UObjectProperty
    {
        public UIndex ClassRef;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ClassRef);
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
    }

    public class UArrayProperty : UProperty
    {
        public UIndex ElementType;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ElementType);
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

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((Struct, nameof(Struct)));
            return uIndices;
        }
    }

    public class UBioMask4Property : UByteProperty
    {
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

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((Function, nameof(Function)));
            uIndices.Add((Delegate, nameof(Delegate)));
            return uIndices;
        }
    }
}
