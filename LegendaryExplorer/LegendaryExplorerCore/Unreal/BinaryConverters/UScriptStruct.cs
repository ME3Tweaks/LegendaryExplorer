using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UScriptStruct : UStruct
    {
        public ScriptStructFlags StructFlags;
        public PropertyCollection Defaults; //I'm assuming any ObjectProperties in here are set to 0, so relinking will be unnecesary
        public long DefaultsStartPosition = -1;

        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref StructFlags);
            DefaultsStartPosition = sc.ms.Position;
            if (sc.IsLoading)
            {
                Defaults = PropertyCollection.ReadProps(Export, sc.ms.BaseStream, Export.ObjectName, includeNoneProperty: true, entry: Export, packageCache: sc.packageCache);
            }
            else
            {
                Defaults.WriteTo(sc.ms.Writer, sc.Pcc, true);
            }
        }
    }

    [Flags]
    public enum ScriptStructFlags : uint
    {
        Native = 0x00000001,
        Export = 0x00000002,
        HasComponents = 0x00000004,
        Transient = 0x00000008,
        Atomic = 0x00000010,
        Immutable = 0x00000020,
        StrictConfig = 0x00000040,
        ImmutableWhenCooked = 0x00000080,
        AtomicWhenCooked = 0x00000100,
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref ScriptStructFlags flags)
        {
            if (sc.IsLoading)
            {
                flags = (ScriptStructFlags)sc.ms.ReadUInt32();
            }
            else
            {
                sc.ms.Writer.WriteUInt32((uint)flags);
            }
        }
    }
}