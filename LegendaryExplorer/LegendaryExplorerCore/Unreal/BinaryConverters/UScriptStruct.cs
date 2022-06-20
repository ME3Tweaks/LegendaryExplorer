using System;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UScriptStruct : UStruct
    {
        public ScriptStructFlags StructFlags;
        public PropertyCollection Defaults; //I'm assuming any ObjectProperties in here are set to 0, so relinking will be unnecessary
        public long DefaultsStartPosition = -1;

        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref StructFlags);
            DefaultsStartPosition = sc.ms.Position;
            if (sc.IsLoading)
            {
                Defaults = PropertyCollection.ReadProps(Export, sc.ms.BaseStream, Export.ObjectName, entry: Export, packageCache: sc.packageCache);
            }
            else
            {
                Defaults.WriteTo(sc.ms.Writer, sc.Pcc, true);
            }
        }

        public static UScriptStruct Create()
        {
            return new()
            {
                ScriptBytes = Array.Empty<byte>(),
                Defaults = new PropertyCollection()
            };
        }
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