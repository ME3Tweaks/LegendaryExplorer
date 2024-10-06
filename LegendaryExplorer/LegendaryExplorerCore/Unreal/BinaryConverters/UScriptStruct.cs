using System;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UScriptStruct : UStruct
    {
        public ScriptStructFlags StructFlags;
        public PropertyCollection Defaults; //I'm assuming any ObjectProperties in here are set to 0, so relinking will be unnecessary
        public long DefaultsStartPosition = -1;

        protected override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref StructFlags);
            DefaultsStartPosition = sc.ms.Position;
            if (sc.IsLoading)
            {
                Defaults = PropertyCollection.ReadProps(Export, sc.ms.BaseStream, Export.ObjectName, entry: Export, packageCache: sc.PackageCache);
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
                ScriptBytes = [],
                Defaults = []
            };
        }
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref ScriptStructFlags flags)
        {
            if (IsLoading)
            {
                flags = (ScriptStructFlags)ms.ReadUInt32();
            }
            else
            {
                ms.Writer.WriteUInt32((uint)flags);
            }
        }
    }
}