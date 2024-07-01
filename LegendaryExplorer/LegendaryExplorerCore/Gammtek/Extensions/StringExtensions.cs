using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Gammtek.Extensions
{
	public static class StringExtensions
	{
        public static int ToGameNum(this MEGame game)
        {
            if (game == MEGame.ME1) return 1;
            if (game == MEGame.ME2) return 2;
            if (game == MEGame.ME3) return 3;
            if (game == MEGame.LE1) return 4;
            if (game == MEGame.LE2) return 5;
            if (game == MEGame.LE3) return 6;
            if (game == MEGame.LELauncher) return 7; // ME3Tweaks Mod Manager uses this to denote LELauncher game
            return 0;
        }

        public static string ToGameName(this MEGame game, bool useLEShortName = false)
        {
            if (game == MEGame.ME1) return "Mass Effect";
            if (game == MEGame.ME2) return "Mass Effect 2";
            if (game == MEGame.ME3) return "Mass Effect 3";
            if (game == MEGame.LE1)
            {
                if (useLEShortName) return "Mass Effect LE";
                return "Mass Effect (Legendary Edition)";
            }
            if (game == MEGame.LE2)
            {
                if (useLEShortName) return "Mass Effect 2 LE";
                return "Mass Effect 2 (Legendary Edition)";
            }
            if (game == MEGame.LE3)
            {
                if (useLEShortName) return "Mass Effect 3 LE";
                return "Mass Effect 3 (Legendary Edition)";
            }
            if (game == MEGame.LELauncher)
            {
                if (useLEShortName) return "LE Launcher";
                return "Mass Effect Legendary Edition Launcher";
            }
            return "UNKNOWN GAME";
        }

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

        /// <summary>
        /// Truncates string by removing characters fromm center (and replacing with ellipsis)
        /// </summary>
        /// <param name="str">String to truncate.</param>
        /// <param name="length">Maximum string length. Will be clamped to at least 5</param>
        public static string TruncCenter(this string str, int length)
        {
            if (str == null)
            {
                return null;
            }

            if (length < 5)
            {
                length = 5;
            }

            if (str.Length <= length)
            {
                return str;
            }

            if (length % 2 == 0)
            {
                int sideLen = (length - 2) / 2;
                return $"{str.Substring(0, sideLen)}..{str.Substring(str.Length - sideLen)}";
            }
            else
            {
                int sideLen = (length - 3) / 2;
                return $"{str.Substring(0, sideLen)}...{str.Substring(str.Length - sideLen)}";
            }
        }

        /// <summary>
        /// Wraps a sting to a max length
        /// </summary>
        /// <param name="text"></param>
        /// <param name="maxLength"></param>
        /// <returns></returns>
        public static string WordWrap(this string text, int maxLength)
        {
            return string.Join("\n", WrapLines(text, maxLength));
        }

        /// <summary>
        /// Returns a list of strings no larger than the max length sent in.
        /// </summary>
        /// <remarks>useful function used to wrap string text for reporting.</remarks>
        /// <param name="text">Text to be wrapped into of List of Strings</param>
        /// <param name="maxLength">Max length you want each line to be.</param>
        /// <returns>List of Strings</returns>
        public static List<String> WrapLines(this string text, int maxLength)
        {
            // Return empty list of strings if the text was empty
            if (text.Length == 0) return new List<string>();

            var words = text.Split(' ');
            var lines = new List<string>();
            var currentLine = "";

            foreach (var currentWord in words)
            {
                if ((currentLine.Length > maxLength) || ((currentLine.Length + currentWord.Length) > maxLength))
                {
                    lines.Add(currentLine);
                    currentLine = "";
                }

                if (currentLine.Length > 0)
                    currentLine += " " + currentWord;
                else
                    currentLine += currentWord;
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine);

            return lines;
        }

        public static int CountLeadingWhitespace(this string text)
        {
            int i = 0;
            for (; i < text.Length && char.IsWhiteSpace(text[i]); i++)
            {
            }
            return i;
        }
    }
}
