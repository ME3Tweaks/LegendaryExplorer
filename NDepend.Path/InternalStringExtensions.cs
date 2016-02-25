using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace NDepend.Path
{
	/// <summary>
	///     Provides a set of extension methods dedicated to strings.
	/// </summary>
	internal static class InternalStringExtensions
	{
		/// <summary>Gets a value indicating whether this string ends with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible end string for <paramref name="source" />.</param>
		public static bool EndsWithAny(this string source, string value1, string value2)
		{
			ThrowHelper.ThrowArgumentNullException(nameof(source), source);
			ThrowHelper.ThrowArgumentNullException(nameof(source), value1);
			ThrowHelper.ThrowArgumentNullException(nameof(source), value2);

			return source.EndsWith(value1) || source.EndsWith(value2);
		}

		/// <summary>Gets a value indicating whether this string ends with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value3">One of the possible end string for <paramref name="source" />.</param>
		public static bool EndsWithAny(this string source, string value1, string value2, string value3)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");
			Contract.Requires(value3 != null, "str2 must not be null");

			return source.EndsWith(value1) || source.EndsWith(value2) || source.EndsWith(value3);
		}

		/// <summary>Gets a value indicating whether this string ends with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value3">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value4">One of the possible end string for <paramref name="source" />.</param>
		public static bool EndsWithAny(this string source, string value1, string value2, string value3, string value4)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");
			Contract.Requires(value3 != null, "str2 must not be null");
			Contract.Requires(value4 != null, "str3 must not be null");

			return source.EndsWith(value1) || source.EndsWith(value2) || source.EndsWith(value3) || source.EndsWith(value4);
		}

		/// <summary>Gets a value indicating whether this string ends with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value3">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value4">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value5">One of the possible end string for <paramref name="source" />.</param>
		public static bool EndsWithAny(this string source, string value1, string value2, string value3, string value4, string value5)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");
			Contract.Requires(value3 != null, "str2 must not be null");
			Contract.Requires(value4 != null, "str3 must not be null");
			Contract.Requires(value5 != null, "str4 must not be null");

			return source.EndsWith(value1) || source.EndsWith(value2) || source.EndsWith(value3) || source.EndsWith(value4)
				   || source.EndsWith(value5);
		}

		/// <summary>Gets a value indicating whether this string ends with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value3">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value4">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="value5">One of the possible end string for <paramref name="source" />.</param>
		/// <param name="values">An array containing possible end strings for <paramref name="source" />.</param>
		/// <exception cref="NullReferenceException">null string reference in <paramref name="values" /> not accepted.</exception>
		public static bool EndsWithAny(this string source, string value1, string value2, string value3, string value4, string value5, params string[] values)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");
			Contract.Requires(value3 != null, "str2 must not be null");
			Contract.Requires(value4 != null, "str3 must not be null");
			Contract.Requires(value5 != null, "str4 must not be null");
			Contract.Requires(values != null, "strs must not be null");

			if (source.EndsWith(value1) || source.EndsWith(value2) || source.EndsWith(value3) || source.EndsWith(value4) || source.EndsWith(value5))
			{
				return true;
			}

			var length = values.Length;

			for (var i = 0; i < length; i++)
			{
				var str = values[i];

				if (str == null)
				{
					throw new NullReferenceException("null string reference in array not accepted.");
				}

				if (source.EndsWith(values[i]))
				{
					return true;
				}
			}

			return false;
		}
		
		/// <summary>Gets a value indicating whether this string is equal <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible value for <paramref name="source" />.</param>
		public static bool EqualsAny(this string source, string value1, string value2)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");

			return source == value1 || source == value2;
		}

		/// <summary>Gets a value indicating whether this string is equal <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value3">One of the possible value for <paramref name="source" />.</param>
		public static bool EqualsAny(this string source, string value1, string value2, string value3)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");
			Contract.Requires(value3 != null, "str2 must not be null");

			return source == value1 || source == value2 || source == value3;
		}

		/// <summary>Gets a value indicating whether this string is equal <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value3">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value4">One of the possible value for <paramref name="source" />.</param>
		public static bool EqualsAny(this string source, string value1, string value2, string value3, string value4)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");
			Contract.Requires(value3 != null, "str2 must not be null");
			Contract.Requires(value4 != null, "str3 must not be null");

			return source == value1 || source == value2 || source == value3 || source == value4;
		}

		/// <summary>Gets a value indicating whether this string is equal <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value3">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value4">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value5">One of the possible value for <paramref name="source" />.</param>
		public static bool EqualsAny(this string source, string value1, string value2, string value3, string value4, string value5)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");
			Contract.Requires(value3 != null, "str2 must not be null");
			Contract.Requires(value4 != null, "str3 must not be null");
			Contract.Requires(value5 != null, "str4 must not be null");

			return source == value1 || source == value2 || source == value3 || source == value4 || source == value5;
		}

		/// <summary>Gets a value indicating whether this string is equal <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value3">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value4">One of the possible value for <paramref name="source" />.</param>
		/// <param name="value5">One of the possible value for <paramref name="source" />.</param>
		/// <param name="values">An array containing possible values for <paramref name="source" />.</param>
		public static bool EqualsAny(this string source, string value1, string value2, string value3, string value4, string value5, params string[] values)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");
			Contract.Requires(value3 != null, "str2 must not be null");
			Contract.Requires(value4 != null, "str3 must not be null");
			Contract.Requires(value5 != null, "str4 must not be null");
			Contract.Requires(values != null, "strs must not be null");

			if (source == value1 || source == value2 || source == value3 || source == value4 || source == value5)
			{
				return true;
			}

			var length = values.Length;

			for (var i = 0; i < length; i++)
			{
				if (source == values[i])
				{
					return true;
				}
			}

			return false;
		}


		/// <summary>Gets a value indicating whether <paramref name="hashSet" /> contains the string <paramref name="source" />.</summary>
		/// <remarks>
		///     This <see cref="EqualsAny(string,string,string)" /> overload can be used to check the value of <paramref name="source" /> against a
		///     large number of strings, in a constant time.
		/// </remarks>
		/// <param name="source">This string.</param>
		/// <param name="hashSet">An hashset containg possible values for this <paramref name="source" />.</param>
		public static bool EqualsAny(this string source, HashSet<string> hashSet)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(hashSet != null, "hashset must not be null");
			return hashSet.Contains(source);
		}
		
		/// <summary>Gets a value indicating whether this string starts with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible start string for <paramref name="source" />.</param>
		public static bool StartsWithAny(this string source, string value1, string value2)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");

			return source.StartsWith(value1) || source.StartsWith(value2);
		}

		/// <summary>Gets a value indicating whether this string starts with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value3">One of the possible start string for <paramref name="source" />.</param>
		public static bool StartsWithAny(this string source, string value1, string value2, string value3)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");
			Contract.Requires(value3 != null, "str2 must not be null");

			return source.StartsWith(value1) || source.StartsWith(value2) || source.StartsWith(value3);
		}

		/// <summary>Gets a value indicating whether this string starts with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value3">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value4">One of the possible start string for <paramref name="source" />.</param>
		public static bool StartsWithAny(this string source, string value1, string value2, string value3, string value4)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");
			Contract.Requires(value3 != null, "str2 must not be null");
			Contract.Requires(value4 != null, "str3 must not be null");

			return source.StartsWith(value1) || source.StartsWith(value2) || source.StartsWith(value3) || source.StartsWith(value4);
		}

		/// <summary>Gets a value indicating whether this string starts with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value3">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value4">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value5">One of the possible start string for <paramref name="source" />.</param>
		public static bool StartsWithAny(this string source, string value1, string value2, string value3, string value4, string value5)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");
			Contract.Requires(value3 != null, "str2 must not be null");
			Contract.Requires(value4 != null, "str3 must not be null");
			Contract.Requires(value5 != null, "str4 must not be null");

			return source.StartsWith(value1) || source.StartsWith(value2) || source.StartsWith(value3) || source.StartsWith(value4)
				   || source.StartsWith(value5);
		}

		/// <summary>Gets a value indicating whether this string starts with <i>case sensitive</i> to any of the strings specified.</summary>
		/// <param name="source">This string.</param>
		/// <param name="value1">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value2">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value3">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value4">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="value5">One of the possible start string for <paramref name="source" />.</param>
		/// <param name="values">An array containing possible start strings for <paramref name="source" />.</param>
		/// <exception cref="NullReferenceException">null string reference in <paramref name="values" /> not accepted.</exception>
		public static bool StartsWithAny(this string source, string value1, string value2, string value3, string value4, string value5, params string[] values)
		{
			Contract.Requires(source != null, "str must not be null");
			Contract.Requires(value1 != null, "str0 must not be null");
			Contract.Requires(value2 != null, "str1 must not be null");
			Contract.Requires(value3 != null, "str2 must not be null");
			Contract.Requires(value4 != null, "str3 must not be null");
			Contract.Requires(value5 != null, "str4 must not be null");
			Contract.Requires(values != null, "strs must not be null");

			if (source.StartsWith(value1) || source.StartsWith(value2) || source.StartsWith(value3) || source.StartsWith(value4)
				|| source.StartsWith(value5))
			{
				return true;
			}

			var length = values.Length;

			for (var i = 0; i < length; i++)
			{
				var str = values[i];

				if (str == null)
				{
					throw new NullReferenceException("null string reference in array not accepted.");
				}

				if (source.StartsWith(values[i]))
				{
					return true;
				}
			}

			return false;
		}
	}
}
