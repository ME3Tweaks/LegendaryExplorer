using System;
using System.Buffers.Binary;
using System.Linq;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Gammtek.IO;

namespace LegendaryExplorerCore.Unreal
{
    /// <summary>
    ///     Converts base data types to an array of bytes, and an array of bytes to base
    ///     data types.
    ///     All info taken from the meta data of System.BitConverter. This implementation
    ///     allows for Endianness consideration.
    ///</summary>
    public static class EndianBitConverter
    {
        static EndianBitConverter()
        {
            IsLittleEndian = true;
        }

        /// <summary>
        ///     Indicates the byte order ("endianess") in which data is stored in this computer
        ///     architecture.
        ///</summary>
        public static bool IsLittleEndian { get; set; }
        
        ///
        /// <summary>
        ///     Returns the specified Boolean value as an array of bytes.
        ///
        /// Parameters:
        ///   value:
        ///     A Boolean value.
        ///
        /// Returns:
        ///     An array of bytes with length 1.
        ///</summary>
        public static byte[] GetBytes(bool value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                var bytes = System.BitConverter.GetBytes(value);
                bytes.Reverse();
                return bytes;
            }
        }
        ///
        /// <summary>
        ///     Returns the specified Unicode character value as an array of bytes.
        ///
        /// Parameters:
        ///   value:
        ///     A character to convert.
        ///
        /// Returns:
        ///     An array of bytes with length 2.
        ///</summary>
        public static byte[] GetBytes(char value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                var bytes = System.BitConverter.GetBytes(value);
                bytes.Reverse();
                return bytes;
            }
        }
        ///
        /// <summary>
        ///     Returns the specified double-precision floating point value as an array of
        ///     bytes.
        ///
        /// Parameters:
        ///   value:
        ///     The number to convert.
        ///
        /// Returns:
        ///     An array of bytes with length 8.
        ///</summary>
        public static byte[] GetBytes(double value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                var bytes = System.BitConverter.GetBytes(value);
                bytes.Reverse();
                return bytes;
            }
        }
        ///
        /// <summary>
        ///     Returns the specified single-precision floating point value as an array of
        ///     bytes.
        ///
        /// Parameters:
        ///   value:
        ///     The number to convert.
        ///
        /// Returns:
        ///     An array of bytes with length 4.
        ///</summary>
        public static byte[] GetBytes(float value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                var bytes = System.BitConverter.GetBytes(value);
                bytes.Reverse();
                return bytes;
            }
        }
        ///
        /// <summary>
        ///     Returns the specified 32-bit signed integer value as an array of bytes.
        ///
        /// Parameters:
        ///   value:
        ///     The number to convert.
        ///
        /// Returns:
        ///     An array of bytes with length 4.
        ///</summary>
        public static byte[] GetBytes(int value, Endian endian)
        {
            if (endian.IsNative)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                return System.BitConverter.GetBytes(value.Swap());
            }
        }
        ///
        /// <summary>
        ///     Returns the specified 64-bit signed integer value as an array of bytes.
        ///
        /// Parameters:
        ///   value:
        ///     The number to convert.
        ///
        /// Returns:
        ///     An array of bytes with length 8.
        ///</summary>
        public static byte[] GetBytes(long value, Endian endian)
        {
            if (endian.IsNative)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                return System.BitConverter.GetBytes(value.Swap());
            }
        }
        ///
        /// <summary>
        ///     Returns the specified 16-bit signed integer value as an array of bytes.
        ///
        /// Parameters:
        ///   value:
        ///     The number to convert.
        ///
        /// Returns:
        ///     An array of bytes with length 2.
        ///</summary>
        public static byte[] GetBytes(short value, Endian endian)
        {
            if (endian.IsNative)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                return System.BitConverter.GetBytes(value.Swap());
            }
        }
        ///
        /// <summary>
        ///     Returns the specified 32-bit unsigned integer value as an array of bytes.
        ///
        /// Parameters:
        ///   value:
        ///     The number to convert.
        ///
        /// Returns:
        ///     An array of bytes with length 4.
        ///</summary>
        public static byte[] GetBytes(uint value, Endian endian)
        {
            if (endian.IsNative)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                return System.BitConverter.GetBytes(value.Swap());
            }
        }
        ///
        /// <summary>
        ///     Returns the specified 64-bit unsigned integer value as an array of bytes.
        ///
        /// Parameters:
        ///   value:
        ///     The number to convert.
        ///
        /// Returns:
        ///     An array of bytes with length 8.
        ///</summary>
        public static byte[] GetBytes(ulong value, Endian endian)
        {
            if (endian.IsNative)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                return System.BitConverter.GetBytes(value.Swap());
            }
        }
        ///
        /// <summary>
        ///     Returns the specified 16-bit unsigned integer value as an array of bytes.
        ///
        /// Parameters:
        ///   value:
        ///     The number to convert.
        ///
        /// Returns:
        ///     An array of bytes with length 2.
        ///</summary>
        public static byte[] GetBytes(ushort value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.GetBytes(value);
            }
            else
            {
                return System.BitConverter.GetBytes(value.Swap());
            }
        }

        public static void WriteAsBytes(long value, Span<byte> buff, Endian endian)
        {
            if (!endian.IsNative)
            {
                value = value.Swap();
            }
            MemoryMarshal.Write(buff, in value);
        }

        public static void WriteAsBytes(ulong value, Span<byte> buff, Endian endian)
        {
            if (!endian.IsNative)
            {
                value = value.Swap();
            }
            MemoryMarshal.Write(buff, in value);
        }

        public static void WriteAsBytes(int value, Span<byte> buff, Endian endian)
        {
            if (!endian.IsNative)
            {
                value = value.Swap();
            }
            MemoryMarshal.Write(buff, in value);
        }

        public static void WriteAsBytes(Guid value, Span<byte> buff, Endian endian)
        {
            if (!endian.IsNative)
            {
                int a = BinaryPrimitives.ReadInt32BigEndian(buff);
                int b = BinaryPrimitives.ReadInt16BigEndian(buff.Slice(4));
                int c = BinaryPrimitives.ReadInt16BigEndian(buff.Slice(8));
                MemoryMarshal.Write(buff, in a);
                MemoryMarshal.Write(buff.Slice(4), in b);
                MemoryMarshal.Write(buff.Slice(8), in c);
                return;
            }
            MemoryMarshal.Write(buff, in value);
        }
        ///
        /// <summary>
        ///     Returns a Boolean value converted from one byte at a specified position in
        ///     a byte array.
        ///
        /// Parameters:
        ///   value:
        ///     An array of bytes.
        ///
        ///   startIndex:
        ///     The starting position within value.
        ///
        /// Returns:
        ///     true if the byte at startIndex in value is nonzero; otherwise, false.
        ///
        /// Exceptions:
        ///   System.ArgumentNullException:
        ///     value is null.
        ///
        ///   System.ArgumentOutOfRangeException:
        ///     startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public static bool ToBoolean(byte[] value, int startIndex)
        {
            return System.BitConverter.ToBoolean(value, startIndex);
        }
        ///
        /// <summary>
        ///     Returns a Unicode character converted from two bytes at a specified position
        ///     in a byte array.
        ///
        /// Parameters:
        ///   value:
        ///     An array.
        ///
        ///   startIndex:
        ///     The starting position within value.
        ///
        /// Returns:
        ///     A character formed by two bytes beginning at startIndex.
        ///
        /// Exceptions:
        ///   System.ArgumentException:
        ///     startIndex equals the length of value minus 1.
        ///
        ///   System.ArgumentNullException:
        ///     value is null.
        ///
        ///   System.ArgumentOutOfRangeException:
        ///     startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public static char ToChar(byte[] value, int startIndex) { return (char)value[startIndex]; }
        ///
        /// <summary>
        ///     Returns a double-precision floating point number converted from eight bytes
        ///     at a specified position in a byte array.
        ///
        /// Parameters:
        ///   value:
        ///     An array of bytes.
        ///
        ///   startIndex:
        ///     The starting position within value.
        ///
        /// Returns:
        ///     A double precision floating point number formed by eight bytes beginning
        ///     at startIndex.
        ///
        /// Exceptions:
        ///   System.ArgumentException:
        ///     startIndex is greater than or equal to the length of value minus 7, and is
        ///     less than or equal to the length of value minus 1.
        ///
        ///   System.ArgumentNullException:
        ///     value is null.
        ///
        ///   System.ArgumentOutOfRangeException:
        ///     startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public static double ToDouble(byte[] value, int startIndex)
        {
            if (startIndex > value.Length - sizeof(double))
                return 0;
            if (IsLittleEndian)
            {
                return System.BitConverter.ToDouble(value, startIndex);
            }
            else
            {
                Span<byte> bytes = stackalloc byte[sizeof(double)];
                value.AsSpan(startIndex).CopyTo(bytes);
                bytes.Reverse();
                return System.BitConverter.ToDouble(bytes);
            }
        }
        ///
        /// <summary>
        ///     Returns a 16-bit signed integer converted from two bytes at a specified position
        ///     in a byte array.
        ///
        /// Parameters:
        ///   value:
        ///     An array of bytes.
        ///
        ///   startIndex:
        ///     The starting position within value.
        ///
        /// Returns:
        ///     A 16-bit signed integer formed by two bytes beginning at startIndex.
        ///
        /// Exceptions:
        ///   System.ArgumentException:
        ///     startIndex equals the length of value minus 1.
        ///
        ///   System.ArgumentNullException:
        ///     value is null.
        ///
        ///   System.ArgumentOutOfRangeException:
        ///     startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public static short ToInt16(byte[] value, int startIndex)
        {
            if (startIndex <= value.Length - 2)
            {
                if (IsLittleEndian)
                {
                    return System.BitConverter.ToInt16(value, startIndex);
                }
                else
                {
                    Span<byte> bytes = stackalloc byte[sizeof(short)];
                    value.AsSpan(startIndex).CopyTo(bytes);
                    bytes.Reverse();
                    return System.BitConverter.ToInt16(bytes);
                }
            }
            else
                return 0;
        }
        ///
        /// <summary>
        ///     Returns a 32-bit signed integer converted from four bytes at a specified
        ///     position in a byte array.
        ///
        /// Parameters:
        ///   value:
        ///     An array of bytes.
        ///
        ///   startIndex:
        ///     The starting position within value.
        ///
        /// Returns:
        ///     A 32-bit signed integer formed by four bytes beginning at startIndex.
        ///
        /// Exceptions:
        ///   System.ArgumentException:
        ///     startIndex is greater than or equal to the length of value minus 3, and is
        ///     less than or equal to the length of value minus 1.
        ///
        ///   System.ArgumentNullException:
        ///     value is null.
        ///
        ///   System.ArgumentOutOfRangeException:
        ///     startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public static int ToInt32(byte[] value, int startIndex)
        {
            if (startIndex > value.Length - 4)
                return 0;
            if (IsLittleEndian)
            {                
                return System.BitConverter.ToInt32(value, startIndex);
            }
            else
            {
                Span<byte> bytes = stackalloc byte[sizeof(int)];
                value.AsSpan(startIndex).CopyTo(bytes);
                bytes.Reverse();
                return System.BitConverter.ToInt32(bytes);
            }
        }
        ///
        /// <summary>
        ///     Returns a 64-bit signed integer converted from eight bytes at a specified
        ///     position in a byte array.
        ///
        /// Parameters:
        ///   value:
        ///     An array of bytes.
        ///
        ///   startIndex:
        ///     The starting position within value.
        ///
        /// Returns:
        ///     A 64-bit signed integer formed by eight bytes beginning at startIndex.
        ///
        /// Exceptions:
        ///   System.ArgumentException:
        ///     startIndex is greater than or equal to the length of value minus 7, and is
        ///     less than or equal to the length of value minus 1.
        ///
        ///   System.ArgumentNullException:
        ///     value is null.
        ///
        ///   System.ArgumentOutOfRangeException:
        ///     startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public static long ToInt64(byte[] value, int startIndex)
        {
            if (startIndex > value.Length - 8)
                return 0;
            if (IsLittleEndian)
            {
                return System.BitConverter.ToInt64(value, startIndex);
            }
            else
            {
                Span<byte> bytes = stackalloc byte[sizeof(long)];
                value.AsSpan(startIndex).CopyTo(bytes);
                bytes.Reverse();
                return System.BitConverter.ToInt64(bytes);
            }
        }
        ///
        /// <summary>
        ///     Returns a single-precision floating point number converted from four bytes
        ///     at a specified position in a byte array.
        ///
        /// Parameters:
        ///   value:
        ///     An array of bytes.
        ///
        ///   startIndex:
        ///     The starting position within value.
        ///
        /// Returns:
        ///     A single-precision floating point number formed by four bytes beginning at
        ///     startIndex.
        ///
        /// Exceptions:
        ///   System.ArgumentException:
        ///     startIndex is greater than or equal to the length of value minus 3, and is
        ///     less than or equal to the length of value minus 1.
        ///
        ///   System.ArgumentNullException:
        ///     value is null.
        ///
        ///   System.ArgumentOutOfRangeException:
        ///     startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public static float ToSingle(byte[] value, int startIndex)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToSingle(value, startIndex);
            }
            else
            {
                Span<byte> bytes = stackalloc byte[sizeof(float)];
                value.AsSpan(startIndex).CopyTo(bytes);
                bytes.Reverse();
                return System.BitConverter.ToSingle(bytes);
            }
        }
        ///
        /// <summary>
        ///     Converts the numeric value of each element of a specified array of bytes
        ///     to its equivalent hexadecimal string representation.
        ///
        /// Parameters:
        ///   value:
        ///     An array of bytes.
        ///
        /// Returns:
        ///     A System.String of hexadecimal pairs separated by hyphens, where each pair
        ///     represents the corresponding element in value; for example, "7F-2C-4A".
        ///
        /// Exceptions:
        ///   System.ArgumentNullException:
        ///     value is null.
        ///</summary>
        public static string ToString(byte[] value)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToString(value);
            }
            else
            {
                var bytes = new byte[value.Length];
                value.CopyTo(bytes.AsSpan());
                bytes.Reverse();
                return System.BitConverter.ToString(bytes);
            }
        }
        ///
        /// <summary>
        ///     Converts the numeric value of each element of a specified subarray of bytes
        ///     to its equivalent hexadecimal string representation.
        ///
        /// Parameters:
        ///   value:
        ///     An array of bytes.
        ///
        ///   startIndex:
        ///     The starting position within value.
        ///
        /// Returns:
        ///     A System.String of hexadecimal pairs separated by hyphens, where each pair
        ///     represents the corresponding element in a subarray of value; for example,
        ///     "7F-2C-4A".
        ///
        /// Exceptions:
        ///   System.ArgumentNullException:
        ///     value is null.
        ///
        ///   System.ArgumentOutOfRangeException:
        ///     startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public static string ToString(byte[] value, int startIndex)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToString(value, startIndex);
            }
            else
            {
                var bytes = new byte[value.Length - startIndex];
                value.AsSpan(startIndex).CopyTo(bytes);
                bytes.Reverse();
                return System.BitConverter.ToString(bytes);
            }
        }
        ///
        /// <summary>
        ///     Converts the numeric value of each element of a specified subarray of bytes
        ///     to its equivalent hexadecimal string representation.
        ///
        /// Parameters:
        ///   value:
        ///     An array of bytes.
        ///
        ///   startIndex:
        ///     The starting position within value.
        ///
        ///   length:
        ///     The number of array elements in value to convert.
        ///
        /// Returns:
        ///     A System.String of hexadecimal pairs separated by hyphens, where each pair
        ///     represents the corresponding element in a subarray of value; for example,
        ///     "7F-2C-4A".
        ///
        /// Exceptions:
        ///   System.ArgumentNullException:
        ///     value is null.
        ///
        ///   System.ArgumentOutOfRangeException:
        ///     startIndex or length is less than zero.  -or- startIndex is greater than
        ///     zero and is greater than or equal to the length of value.
        ///
        ///   System.ArgumentException:
        ///     The combination of startIndex and length does not specify a position within
        ///     value; that is, the startIndex parameter is greater than the length of value
        ///     minus the length parameter.
        ///</summary>
        public static string ToString(byte[] value, int startIndex, int length)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToString(value, startIndex, length);
            }
            else
            {
                var bytes = new byte[length];
                value.AsSpan(startIndex).CopyTo(bytes);
                bytes.Reverse();
                return System.BitConverter.ToString(bytes);
            }
        }
        ///
        /// <summary>
        ///     Returns a 16-bit unsigned integer converted from two bytes at a specified
        ///     position in a byte array.
        ///
        /// Parameters:
        ///   value:
        ///     The array of bytes.
        ///
        ///   startIndex:
        ///     The starting position within value.
        ///
        /// Returns:
        ///     A 16-bit unsigned integer formed by two bytes beginning at startIndex.
        ///
        /// Exceptions:
        ///   System.ArgumentException:
        ///     startIndex equals the length of value minus 1.
        ///
        ///   System.ArgumentNullException:
        ///     value is null.
        ///
        ///   System.ArgumentOutOfRangeException:
        ///     startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public static ushort ToUInt16(byte[] value, int startIndex)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToUInt16(value, startIndex);
            }
            else
            {
                var bytes = new byte[sizeof(ushort)];
                value.CopyTo(bytes.AsSpan());
                bytes.Reverse();
                return System.BitConverter.ToUInt16(bytes, value.Length - sizeof(ushort) - startIndex);
            }
        }
        ///
        /// <summary>
        ///     Returns a 32-bit unsigned integer converted from four bytes at a specified
        ///     position in a byte array.
        ///
        /// Parameters:
        ///   value:
        ///     An array of bytes.
        ///
        ///   startIndex:
        ///     The starting position within value.
        ///
        /// Returns:
        ///     A 32-bit unsigned integer formed by four bytes beginning at startIndex.
        ///
        /// Exceptions:
        ///   System.ArgumentException:
        ///     startIndex is greater than or equal to the length of value minus 3, and is
        ///     less than or equal to the length of value minus 1.
        ///
        ///   System.ArgumentNullException:
        ///     value is null.
        ///
        ///   System.ArgumentOutOfRangeException:
        ///     startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public static uint ToUInt32(byte[] value, int startIndex)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToUInt32(value, startIndex);
            }
            else
            {
                var bytes = new byte[sizeof(uint)];
                value.CopyTo(bytes.AsSpan());
                bytes.Reverse();
                return System.BitConverter.ToUInt32(bytes, value.Length - sizeof(uint) - startIndex);
            }
        }
        ///
        /// <summary>
        ///     Returns a 64-bit unsigned integer converted from eight bytes at a specified
        ///     position in a byte array.
        ///
        /// Parameters:
        ///   value:
        ///     An array of bytes.
        ///
        ///   startIndex:
        ///     The starting position within value.
        ///
        /// Returns:
        ///     A 64-bit unsigned integer formed by the eight bytes beginning at startIndex.
        ///
        /// Exceptions:
        ///   System.ArgumentException:
        ///     startIndex is greater than or equal to the length of value minus 7, and is
        ///     less than or equal to the length of value minus 1.
        ///
        ///   System.ArgumentNullException:
        ///     value is null.
        ///
        ///   System.ArgumentOutOfRangeException:
        ///     startIndex is less than zero or greater than the length of value minus 1.
        ///</summary>
        public static ulong ToUInt64(byte[] value, int startIndex)
        {
            if (IsLittleEndian)
            {
                return System.BitConverter.ToUInt64(value, startIndex);
            }
            else
            {
                Span<byte> bytes = stackalloc byte[sizeof(ulong)];
                value.AsSpan(startIndex).CopyTo(bytes);
                bytes.Reverse();
                return System.BitConverter.ToUInt64(bytes);
            }
        }
    }
}