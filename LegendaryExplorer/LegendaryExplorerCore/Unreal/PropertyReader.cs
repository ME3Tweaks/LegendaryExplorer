using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.Unreal
{
    [DebuggerDisplay("NameReference - Name: {Name} Number: {Number} Instanced: {Instanced}")]
    public readonly struct NameReference : IEquatable<NameReference>, IComparable<NameReference>, IComparable
    {
        private readonly int _number;
        private readonly string _name;
        
        // ReSharper disable once ConvertToAutoPropertyWhenPossible
        public string Name => _name;
        // ReSharper disable once ConvertToAutoPropertyWhenPossible
        public int Number => _number;

        public NameReference(string name, int number = 0)
        {
            _name = name;
            _number = number;
        }

        //https://api.unrealengine.com/INT/API/Runtime/Core/UObject/FName/index.html
        //heavily optimized version of this: _number > 0 ? $"{_name}_{_number - 1}" : _name;
        [JsonIgnore]
        public string Instanced
        {
            get
            {
                if (_number > 0)
                {
                    int n = _number - 1;
                    int numChars = _name.Length + 1 + 
                                   //determines the number of digits in n, assuming n >= 0
                                   (n < 100000 ? n < 100 ? n < 10 ? 1 : 2 : n < 1000 ? 3 : n < 10000 ? 4 : 5 : n < 10000000 ? n < 1000000 ? 6 : 7 : n < 100000000 ? 8 : n < 1000000000 ? 9 : 10);
                    return string.Create(numChars, this, (span, nameRef) =>
                    {
                        ReadOnlySpan<char> nameSpan = nameRef._name.AsSpan();
                        nameSpan.CopyTo(span);
                        int nameLength = nameSpan.Length;
                        span[nameLength] = '_';
                        ((uint)nameRef._number - 1).ToStrInPlace(span.Slice(nameLength + 1));
                    });
                }

                return _name;
            }
        }

        /// <summary>
        /// Adds instanced name to end of <paramref name="parentPath"/>, after a '.'
        /// </summary>
        /// <param name="parentPath"></param>
        /// <returns></returns>
        public string AddToPath(string parentPath)
        {
            int n = _number - 1;
            int length = parentPath.Length + _name.Length + 1;
            if (_number > 0)
            {
                length += 1 + (n < 100000 ? n < 100 ? n < 10 ? 1 : 2 : n < 1000 ? 3 : n < 10000 ? 4 : 5 : n < 10000000 ? n < 1000000 ? 6 : 7 : n < 100000000 ? 8 : n < 1000000000 ? 9 : 10);
            }
            return string.Create(length, (parentPath, _name, n), (span, tuple) =>
            {
                ReadOnlySpan<char> parentSpan = tuple.parentPath.AsSpan();
                int parentLength = parentSpan.Length;
                parentSpan.CopyTo(span);
                span[parentLength] = '.';
                ReadOnlySpan<char> nameSpan = tuple._name.AsSpan();
                nameSpan.CopyTo(span.Slice(parentLength + 1));
                int nameLength = parentLength + 1 + nameSpan.Length;
                if (tuple.n >= 0)
                {
                    span[nameLength] = '_';
                    ((uint)tuple.n).ToStrInPlace(span.Slice(nameLength + 1));
                }
            });
        }

        public static implicit operator NameReference(string s)
        {
            return new NameReference(s);
        }

        public static implicit operator string(NameReference n)
        {
            return n._name;
        }

        public override string ToString()
        {
            return _name ?? string.Empty;
        }

        public static bool operator ==(NameReference r, string s)
        {
            return string.Equals(s, r._name, StringComparison.OrdinalIgnoreCase);
        }

        public static bool operator !=(NameReference r, string s)
        {
            return !string.Equals(s, r._name, StringComparison.OrdinalIgnoreCase);
        }


        public static bool operator ==(string s, NameReference r)
        {
            return string.Equals(s, r._name, StringComparison.OrdinalIgnoreCase);
        }

        public static bool operator !=(string s, NameReference r)
        {
            return !string.Equals(s, r._name, StringComparison.OrdinalIgnoreCase);
        }

        public static NameReference FromInstancedString(string s)
        {
            int num = 0;
            int _Idx = s.LastIndexOf('_');
            if (_Idx > 0)
            {
                string numComponent = s.Substring(_Idx + 1);
                if (numComponent.Length > 0 
                    && !(numComponent.Length > 1 && numComponent[0] == '0') //if there's more than one character and a leading zero, it's just part of the string
                    && int.TryParse(numComponent, NumberStyles.None, null, out num))
                {
                    s = s.Substring(0, _Idx);
                    num += 1;
                }
            }
            return new NameReference(s, num);
        }

        #region IEquatable
        public static bool operator ==(NameReference n1, NameReference n2)
        {
            return n1.Equals(n2);
        }

        public static bool operator !=(NameReference n1, NameReference n2)
        {
            return !n1.Equals(n2);
        }
        public bool Equals(NameReference other)
        {
            return string.Equals(_name, other._name, StringComparison.OrdinalIgnoreCase) && _number == other._number;
        }

        public override bool Equals(object obj)
        {
            return obj is NameReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_name.GetHashCode() * 397) ^ _number;
            }
        }
        #endregion

        #region IComparable
        public int CompareTo(NameReference other)
        {
            int nameComparison = string.Compare(_name, other._name, StringComparison.OrdinalIgnoreCase);
            if (nameComparison != 0) return nameComparison;
            return _number.CompareTo(other._number);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            return obj is NameReference other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(NameReference)}");
        }

        public static bool operator <(NameReference left, NameReference right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(NameReference left, NameReference right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(NameReference left, NameReference right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(NameReference left, NameReference right)
        {
            return left.CompareTo(right) >= 0;
        } 
        #endregion

        public static readonly NameReference None = new("None");
    }


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
