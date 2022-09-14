using System;
using System.IO;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal
{
    public readonly struct ScriptDelegate : IEquatable<ScriptDelegate>
    {
        public int ContainingObjectUIndex { get; }
        public NameReference FunctionName { get; }

        public ScriptDelegate(int containingObjectUIndex, NameReference functionName)
        {
            ContainingObjectUIndex = containingObjectUIndex;
            FunctionName = functionName;
        }

        #region IEquatable

        public bool Equals(ScriptDelegate other)
        {
            return ContainingObjectUIndex == other.ContainingObjectUIndex && FunctionName.Equals(other.FunctionName);
        }

        public override bool Equals(object obj)
        {
            return obj is ScriptDelegate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ContainingObjectUIndex * 397) ^ FunctionName.GetHashCode();
            }
        }

        public static bool operator ==(ScriptDelegate left, ScriptDelegate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScriptDelegate left, ScriptDelegate right)
        {
            return !left.Equals(right);
        }

        #endregion

        public static readonly ScriptDelegate Empty = new(0, "None");
    }

    public enum PropertyType
    {
        Unknown = -1,
        None = 0,
        StructProperty = 1,
        IntProperty = 2,
        FloatProperty = 3,
        ObjectProperty = 4,
        NameProperty = 5,
        BoolProperty = 6,
        ByteProperty = 7,
        ArrayProperty = 8,
        StrProperty = 9,
        StringRefProperty = 10,
        DelegateProperty = 11,
        BioMask4Property = 12,
        InterfaceProperty = 13,
        ComponentProperty = 14
    }

    public static class UPropertyExtensions
    {
        public static void WritePropHeader(this EndianWriter stream, IMEPackage pcc, NameReference propName, PropertyType type, int size, int staticArrayIndex)
        {
            stream.WriteNameReference(propName, pcc);
            stream.WriteNameReference(type.ToString(), pcc);
            stream.WriteInt32(size);
            stream.WriteInt32(staticArrayIndex);
        }

        public static void WriteNoneProperty(this EndianWriter stream, IMEPackage pcc)
        {
            //Debug.WriteLine("Writing none property at 0x" + stream.Position.ToString("X6"));
            stream.WriteNameReference("None", pcc);
        }

        public static void WriteStructProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, NameReference structName, Stream value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing struct property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.StructProperty, (int)value.Length, staticArrayIndex);
            stream.WriteNameReference(structName, pcc);
            stream.BaseStream.WriteStream(value);
        }

        public static void WriteStructProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, NameReference structName, Func<Stream> func, int staticArrayIndex)
        {
            stream.WriteStructProperty(pcc, propName, structName, func(), staticArrayIndex);
        }

        public static void WriteIntProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, int value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing int property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.IntProperty, 4, staticArrayIndex);
            stream.WriteInt32(value);
        }

        public static void WriteFloatProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, float value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing float property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.FloatProperty, 4, staticArrayIndex);
            stream.WriteFloat(value);
        }

        public static void WriteObjectProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, int value, int staticArrayIndex, PropertyType propType = PropertyType.ObjectProperty)
        {
            //Debug.WriteLine("Writing bool property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, propType, 4, staticArrayIndex);
            stream.WriteInt32(value);
        }

        public static void WriteNameProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, NameReference value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing name property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.NameProperty, 8, staticArrayIndex);
            stream.WriteNameReference(value, pcc);
        }

        public static void WriteBoolProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, bool value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing bool property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.BoolProperty, 0, staticArrayIndex);
            if (pcc.Game >= MEGame.ME3)
            {
                stream.WriteBoolByte(value);
            }
            else
            {
                stream.WriteBoolInt(value);
            }
        }

        public static void WriteByteProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, byte value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing byte property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));
            stream.WritePropHeader(pcc, propName, PropertyType.ByteProperty, 1, staticArrayIndex);
            if (pcc.Game >= MEGame.ME3)
            {
                stream.WriteNameReference("None", pcc);
            }
            stream.WriteByte(value);
        }

        public static void WriteEnumProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, NameReference enumName, NameReference enumValue, int staticArrayIndex)
        {
            stream.WritePropHeader(pcc, propName, PropertyType.ByteProperty, 8, staticArrayIndex);
            if (pcc.Game >= MEGame.ME3)
            {
                stream.WriteNameReference(enumName, pcc);
            }
            stream.WriteNameReference(enumValue, pcc);
        }

        public static void WriteArrayProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, int count, Stream value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing array property " + propName + ", count: " + count + " at 0x" + stream.Position.ToString("X6")+", length: "+value.Length);
            stream.WritePropHeader(pcc, propName, PropertyType.ArrayProperty, 4 + (int)value.Length, staticArrayIndex);
            stream.WriteInt32(count);
            stream.BaseStream.WriteStream(value);
        }

        public static void WriteArrayProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, int count, Func<Stream> func, int staticArrayIndex)
        {
            stream.WriteArrayProperty(pcc, propName, count, func(), staticArrayIndex);
        }

        public static void WriteStringProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, string value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing string property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));
            int strLen = value.Length == 0 ? 0 : value.Length + 1;
            if (pcc.Game.IsGame3() || pcc.Game is MEGame.LE1 or MEGame.LE2 && !value.IsLatin1())
            {
                stream.WritePropHeader(pcc, propName, PropertyType.StrProperty, (strLen * 2) + 4, staticArrayIndex);
                stream.WriteUnrealStringUnicode(value);
            }
            else
            {
                stream.WritePropHeader(pcc, propName, PropertyType.StrProperty, strLen + 4, staticArrayIndex);
                stream.WriteUnrealStringLatin1(value);
            }
        }

        public static void WriteStringRefProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, int value, int staticArrayIndex)
        {
            //Debug.WriteLine("Writing stringref property " + propName + ", value: " + value + " at 0x" + stream.Position.ToString("X6"));

            stream.WritePropHeader(pcc, propName, PropertyType.StringRefProperty, 4, staticArrayIndex);
            stream.WriteInt32(value);
        }

        public static void WriteDelegateProperty(this EndianWriter stream, IMEPackage pcc, NameReference propName, ScriptDelegate value, int staticArrayIndex)
        {
            stream.WritePropHeader(pcc, propName, PropertyType.DelegateProperty, 12, staticArrayIndex);
            stream.WriteInt32(value.ContainingObjectUIndex);
            stream.WriteNameReference(value.FunctionName, pcc);
        }

        public static StructProperty ToGuidStructProp(this Guid guid, NameReference propName) => CommonStructs.GuidProp(guid, propName);
    }
}
