using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public abstract class UProperty : UField
    {
        public int ArraySize;//If this is not 0, this property is a static array, of ArraySize length
        public UnrealFlags.EPropertyFlags PropertyFlags;
        public NameReference Category;
        public UIndex ArraySizeEnum; //If this is not 0, this property is a static array, and the number of copies of this property there should be is equal to the MAX value of the Enum this points to 
        public ushort ReplicationOffset;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ArraySize);
            sc.Serialize(ref PropertyFlags);
            if (sc.Pcc.Platform is MEPackage.GamePlatform.PC || (sc.Game is not MEGame.ME3 && sc.Pcc.Platform is MEPackage.GamePlatform.Xenon))
            {
                sc.Serialize(ref Category);
                sc.Serialize(ref ArraySizeEnum);
            }

            if (PropertyFlags.Has(UnrealFlags.EPropertyFlags.Net))
            {
                sc.Serialize(ref ReplicationOffset);
            }
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            names.Add((Category, nameof(Category)));

            return names;
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            base.ForEachUIndex(game, in action);
            Unsafe.AsRef(in action).Invoke(ref ArraySizeEnum, nameof(ArraySizeEnum));
        }
    }

    public class UIntProperty : UProperty
    {
        public static UIntProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }
    }
    public class UBoolProperty : UProperty
    {
        public static UBoolProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }
    }
    public class UFloatProperty : UProperty
    {
        public static UFloatProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }
    }
    public class UNameProperty : UProperty
    {
        public static UNameProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }
    }
    public class UStrProperty : UProperty
    {
        public static UStrProperty Create()
        {
            return new()
            {
                Category = "None"
            };
        }
    }
    public class UStringRefProperty : UProperty
    {
        public static UStringRefProperty Create()
        {
            return new()
            {
                Category = "None"
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