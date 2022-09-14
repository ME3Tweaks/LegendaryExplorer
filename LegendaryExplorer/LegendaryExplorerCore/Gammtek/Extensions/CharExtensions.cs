﻿using System;
using System.Runtime.CompilerServices;

namespace LegendaryExplorerCore.Gammtek.Extensions
{
	/// <summary>
	/// </summary>
	public static class CharExtensions
	{
		public static bool IsDigit(this char c)
        {
			//not using char.IsDigit, since that returns true for all sorts of weird unicode "digits"
            return c is >= '0' and <= '9';
        }

		public static bool IsLetter(this char c)
		{
			return char.IsLetter(c);
		}

		public static bool IsQuote(this char c)
		{
			return c == '\"';
		}

		public static bool IsWhiteSpace(this char c)
		{
			return char.IsWhiteSpace(c);
		}

		public static bool ToBoolean(this char value)
		{
			return Convert.ToBoolean(value);
		}

		public static byte ToByte(this char value)
		{
			return Convert.ToByte(value);
		}

		public static DateTime ToDateTime(this char value)
		{
			return Convert.ToDateTime(value);
		}

		public static decimal ToDecimal(this char value)
		{
			return Convert.ToDecimal(value);
		}

		public static double ToDouble(this char value)
		{
			return Convert.ToDouble(value);
		}

		public static short ToInt16(this char value)
		{
			return Convert.ToInt16(value);
		}

		public static int ToInt32(this char value)
		{
			return Convert.ToInt32(value);
		}

		public static long ToInt64(this char value)
		{
			return Convert.ToInt64(value);
		}

		public static sbyte ToSByte(this char value)
		{
			return Convert.ToSByte(value);
		}

		public static float ToSingle(this char value)
		{
			return Convert.ToSingle(value);
		}

		public static ushort ToUInt16(this char value)
		{
			return Convert.ToUInt16(value);
		}

		public static uint ToUInt32(this char value)
		{
			return Convert.ToUInt32(value);
		}

		public static ulong ToUInt64(this char value)
		{
			return Convert.ToUInt64(value);
		}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AsciiCaseInsensitiveEquals(this char a, char b)
        {
            if (a == b)
            {
                return true;
            }
            int lowerCaseA = a | 0x20;
            return lowerCaseA == (b | 0x20) && lowerCaseA is >= 'a' and <= 'z';
        }

        public static bool IsNullOrWhiteSpace(this char c) => c is '\0' || char.IsWhiteSpace(c);
    }
}
