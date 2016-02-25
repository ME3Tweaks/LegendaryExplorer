using System;

namespace Gammtek.Conduit.Extensions
{
	public static class StringExtensions
	{
		[StringFormatMethod("value")]
		public static string Format(this string value, IFormatProvider provider, params object[] args)
		{
			return string.Format(provider, value, args);
		}

		[StringFormatMethod("value")]
		public static string Format(this string value, object arg0)
		{
			return string.Format(value, arg0);
		}

		[StringFormatMethod("value")]
		public static string Format(this string value, object arg0, object arg1)
		{
			return string.Format(value, arg0, arg1);
		}

		[StringFormatMethod("value")]
		public static string Format(this string value, object arg0, object arg1, object arg2)
		{
			return string.Format(value, arg0, arg1, arg2);
		}

		[StringFormatMethod("value")]
		public static string Format(this string value, params object[] args)
		{
			return string.Format(value, args);
		}

		[ContractAnnotation("value:null => true")]
		public static bool IsNull(this string value)
		{
			return value == null;
		}

		[ContractAnnotation("value:null => true")]
		public static bool IsNullOrEmpty(this string value)
		{
			return string.IsNullOrEmpty(value);
		}

		[ContractAnnotation("value:null => true")]
		public static bool IsNullOrWhiteSpace(this string value)
		{
			return string.IsNullOrWhiteSpace(value);
		}

		public static string Left(this string value, int count)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}

			return value.Substring(0, count.Clamp(0, value.Length));
		}

		public static string RemoveLeft(this string value, int count)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}

			return value.Substring((value.Length - count).Clamp(0, value.Length));
		}

		public static string RemoveRight(this string value, int count)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}

			return value.Substring(0, value.Length - count.Clamp(0, value.Length));
		}

		public static string Right(this string value, int count)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
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
	}
}
