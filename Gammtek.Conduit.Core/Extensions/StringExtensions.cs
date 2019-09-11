using System;

namespace Gammtek.Conduit.Extensions
{
	public static class StringExtensions
	{
		public static string Left(this string value, int count)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			return value.Substring(0, count.Clamp(0, value.Length));
		}

		public static string RemoveLeft(this string value, int count)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			return value.Substring((value.Length - count).Clamp(0, value.Length));
		}

		public static string RemoveRight(this string value, int count)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			return value.Substring(0, value.Length - count.Clamp(0, value.Length));
		}

		public static string Right(this string value, int count)
		{
			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}
			
			return value.Substring(value.Length - count.Clamp(0, value.Length));
		}

		public static bool ToBoolean(this string value)
		{
			return Convert.ToBoolean(value);
		}

		public static byte ToByte(this string value)
		{
			return Convert.ToByte(value);
		}

		public static char ToChar(this string value)
		{
			return Convert.ToChar(value);
		}

		public static DateTime ToDateTime(this string value)
		{
			return Convert.ToDateTime(value);
		}

		public static decimal ToDecimal(this string value)
		{
			return Convert.ToDecimal(value);
		}

		public static double ToDouble(this string value)
		{
			return Convert.ToDouble(value);
		}

		public static short ToInt16(this string value)
		{
			return Convert.ToInt16(value);
		}

		public static int ToInt32(this string value)
		{
			return Convert.ToInt32(value);
		}

		public static long ToInt64(this string value)
		{
			return Convert.ToInt64(value);
		}

		public static sbyte ToSByte(this string value)
		{
			return Convert.ToSByte(value);
		}

		public static float ToSingle(this string value)
		{
			return Convert.ToSingle(value);
		}

		public static ushort ToUInt16(this string value)
		{
			return Convert.ToUInt16(value);
		}

		public static uint ToUInt32(this string value)
		{
			return Convert.ToUInt32(value);
		}

		public static ulong ToUInt64(this string value)
		{
			return Convert.ToUInt64(value);
		}

        /// <summary>
        /// Truncates string so that it is no longer than the specified number of characters.
        /// </summary>
        /// <param name="str">String to truncate.</param>
        /// <param name="length">Maximum string length.</param>
        /// <param name="ellipsis">Whether an ellipsis should be appended if truncated. (this will take up three of the allowed characters)</param>
        /// <returns>Original string or a truncated one if the original was too long.</returns>
        public static string Truncate(this string str, int length, bool ellipsis = false)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be >= 0");
            }

            if (str == null)
            {
                return null;
            }

            if (ellipsis && length > 4 && length < str.Length)
            {
                return $"{str.Substring(0, length - 3)}...";
            }

            int maxLength = Math.Min(str.Length, length);
            return str.Substring(0, maxLength);
        }
    }
}
