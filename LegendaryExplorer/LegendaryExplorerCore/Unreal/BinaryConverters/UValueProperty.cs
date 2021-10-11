using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public abstract class UProperty : UField
    {
        public int ArraySize;//If this is not 0, this property is a static array, of ArraySize length
        public UnrealFlags.EPropertyFlags PropertyFlags;
        public NameReference Category;
        public UIndex ArraySizeEnum; //If this is not 0, this property is a static array,
        //and the number of copies of this property there should be is equal to the MAX value of the Enum this points to 
        public ushort ReplicationOffset;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ArraySize);
            sc.Serialize(ref PropertyFlags);
            if (sc.Pcc.Platform == MEPackage.GamePlatform.PC)
            {
                sc.Serialize(ref Category);
                sc.Serialize(ref ArraySizeEnum);
            }

            if (PropertyFlags.Has(UnrealFlags.EPropertyFlags.Net))
            {
                sc.Serialize(ref ReplicationOffset);
            }
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((ArraySizeEnum, "ArraySizeEnum"));
            return uIndices;
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            names.Add((Category, nameof(Category)));

            return names;
        }
    }

    public class UIntProperty : UProperty
    {
        public static UIntProperty Create()
        {
            return new()
            {
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0
            };
        }
    }
    public class UBoolProperty : UProperty
    {
        public static UBoolProperty Create()
        {
            return new()
            {
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0
            };
        }
    }
    public class UFloatProperty : UProperty
    {
        public static UFloatProperty Create()
        {
            return new()
            {
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0
            };
        }
    }
    public class UNameProperty : UProperty
    {
        public static UNameProperty Create()
        {
            return new()
            {
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0
            };
        }
    }
    public class UStrProperty : UProperty
    {
        public static UStrProperty Create()
        {
            return new()
            {
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0
            };
        }
    }
    public class UStringRefProperty : UProperty
    {
        public static UStringRefProperty Create()
        {
            return new()
            {
                SuperClass = 0,
                Next = 0,
                Category = "None",
                ArraySizeEnum = 0
            };
        }
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref UnrealFlags.EPropertyFlags flags)
        {
            if (sc.IsLoading)
            {
                flags = (UnrealFlags.EPropertyFlags)sc.ms.ReadUInt64();
            }
            else
            {
                sc.ms.Writer.WriteUInt64((ulong)flags);
            }
        }
    }
}