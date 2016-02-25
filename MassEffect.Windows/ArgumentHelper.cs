using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace MassEffect.Windows
{
	/// <summary>
	/// Provides helper methods for asserting arguments.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This class provides helper methods for asserting the validity of arguments. It can be used to reduce the number of
	/// laborious <c>if</c>, <c>throw</c> sequences in your code.
	/// </para>
	/// <para>
	/// The <see cref="AssertNotNull"/> methods can be used to ensure that arguments are not <see langword="null"/>. The
	/// <see cref="AssertNotNullOrEmpty"/> overloads can be used to ensure that strings are not <see langword="null"/> or empty.
	/// The <see cref="AssertEnumMember"/> overloads can be used to assert the validity of enumeration arguments.
	/// </para>
	/// </remarks>
	/// <example>
	/// The following code ensures that the <c>name</c> argument is not <see langword="null"/>:
	/// <code>
	/// public void DisplayDetails(string name)
	/// {
	///     ArgumentHelper.AssertNotNull(name, "name");
	///     //now we know that name is not null
	///     ...
	/// }
	/// </code>
	/// </example>
	/// <example>
	/// The following code ensures that the <c>name</c> argument is not <see langword="null"/> or an empty <c>string</c>:
	/// <code>
	/// public void DisplayDetails(string name)
	/// {
	///     ArgumentHelper.AssertNotNullOrEmpty(name, "name", true);
	///     //now we know that name is not null and is not an empty string (or blank)
	///     ...
	/// }
	/// </code>
	/// </example>
	/// <example>
	/// The following code ensures that the <c>day</c> parameter is a valid member of its enumeration:
	/// <code>
	/// public void DisplayInformation(DayOfWeek day)
	/// {
	///     ArgumentHelper.AssertEnumMember(day);
	///     //now we know that day is a valid member of DayOfWeek
	///     ...
	/// }
	/// </code>
	/// </example>
	/// <example>
	/// The following code ensures that the <c>day</c> parameter is either DayOfWeek.Monday or DayOfWeek.Thursday:
	/// <code>
	/// public void DisplayInformation(DayOfWeek day)
	/// {
	///     ArgumentHelper.AssertEnumMember(day, DayOfWeek.Monday, DayOfWeek.Thursday);
	///     //now we know that day is either Monday or Thursday
	///     ...
	/// }
	/// </code>
	/// </example>
	/// <example>
	/// The following code ensures that the <c>bindingFlags</c> parameter is either BindingFlags.Public, BindingFlags.NonPublic
	/// or both:
	/// <code>
	/// public void GetInformation(BindingFlags bindingFlags)
	/// {
	///     ArgumentHelper.AssertEnumMember(bindingFlags, BindingFlags.Public, BindingFlags.NonPublic);
	///     //now we know that bindingFlags is either Public, NonPublic or both
	///     ...
	/// }
	/// </code>
	/// </example>
	/// <example>
	/// The following code ensures that the <c>bindingFlags</c> parameter is either BindingFlags.Public, BindingFlags.NonPublic,
	/// both or neither (BindingFlags.None):
	/// <code>
	/// public void GetInformation(BindingFlags bindingFlags)
	/// {
	///     ArgumentHelper.AssertEnumMember(bindingFlags, BindingFlags.Public, BindingFlags.NonPublic, BindingFlags.None);
	///     //now we know that bindingFlags is either Public, NonPublic, both or neither
	///     ...
	/// }
	/// </code>
	/// </example>
	public static class ArgumentHelper
	{
		/// <include file='ArgumentHelper.doc.xml' path='doc/member[@name="AssertNotNull{T}(T,string)"]/*' />
		[DebuggerHidden]
		public static void AssertNotNull<T>(T arg, string argName)
			where T : class
		{
			if (arg == null)
			{
				throw new ArgumentNullException(argName);
			}
		}

		/// <include file='ArgumentHelper.doc.xml' path='doc/member[@name="AssertNotNull{T}(Nullable{T},string)"]/*' />
		[DebuggerHidden]
		public static void AssertNotNull<T>(T? arg, string argName)
			where T : struct
		{
			if (!arg.HasValue)
			{
				throw new ArgumentNullException(argName);
			}
		}

		/// <include file='ArgumentHelper.doc.xml' path='doc/member[@name="AssertGenericArgumentNotNull{T}(T,string)"]/*' />
		[DebuggerHidden]
		public static void AssertGenericArgumentNotNull<T>(T arg, string argName)
		{
			Type type = typeof(T);

			if (!type.IsValueType || (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Nullable<>))))
			{
				AssertNotNull((object)arg, argName);
			}
		}

		/// <include file='ArgumentHelper.doc.xml' path='doc/member[@name="AssertNotNull{T}(IEnumerable{T},string,bool)"]/*' />
		[DebuggerHidden]
		public static void AssertNotNull<T>(IEnumerable<T> arg, string argName, bool assertContentsNotNull)
		{
			// make sure the enumerable item itself isn't null
			AssertNotNull(arg, argName);

			if (assertContentsNotNull && typeof(T).IsClass)
			{
				// make sure each item in the enumeration isn't null
				foreach (var item in arg)
				{
					if (item == null)
					{
						throw new ArgumentException("An item inside the enumeration was null.", argName);
					}
				}
			}
		}

		/// <include file='ArgumentHelper.doc.xml' path='doc/member[@name="AssertNotNullOrEmpty(string,string)"]/*' />
		[DebuggerHidden]
		public static void AssertNotNullOrEmpty(string arg, string argName)
		{
			if (string.IsNullOrEmpty(arg))
			{
				throw new ArgumentException("Cannot be null or empty.", argName);
			}
		}

		/// <include file='ArgumentHelper.doc.xml' path='doc/member[@name="AssertNotNullOrEmpty(IEnumerable,string)"]/*' />
		[DebuggerHidden]
		public static void AssertNotNullOrEmpty(IEnumerable arg, string argName)
		{
			if (arg == null || !arg.GetEnumerator().MoveNext())
			{
				throw new ArgumentException("Cannot be null or empty.", argName);
			}
		}

		/// <include file='ArgumentHelper.doc.xml' path='doc/member[@name="AssertNotNullOrEmpty(ICollection,string)"]/*' />
		[DebuggerHidden]
		public static void AssertNotNullOrEmpty(ICollection arg, string argName)
		{
			if (arg == null || arg.Count == 0)
			{
				throw new ArgumentException("Cannot be null or empty.", argName);
			}
		}

		/// <include file='ArgumentHelper.doc.xml' path='doc/member[@name="AssertNotNullOrWhiteSpace(string,string)"]/*' />
		[DebuggerHidden]
		public static void AssertNotNullOrWhiteSpace(string arg, string argName)
		{
			if (arg == null)
			{
				throw new ArgumentException("Cannot be null or white-space.", argName);
			}

			for (var i = 0; i < arg.Length; ++i)
			{
				if (!char.IsWhiteSpace(arg, i))
				{
					return;
				}
			}

			throw new ArgumentException("Cannot be null or white-space.", argName);
		}

		/// <include file='ArgumentHelper.doc.xml' path='doc/member[@name="AssertEnumMember{TEnum}(TEnum,string)"]/*' />
		[DebuggerHidden]
		[CLSCompliant(false)]
		public static void AssertEnumMember<TEnum>(TEnum enumValue, string argName)
				where TEnum : struct, IConvertible
		{
			if (Attribute.IsDefined(typeof(TEnum), typeof(FlagsAttribute), false))
			{
				// flag enumeration - we can only get here if TEnum is a valid enumeration type, since the FlagsAttribute can
				// only be applied to enumerations
				bool throwEx;
				var longValue = enumValue.ToInt64(CultureInfo.InvariantCulture);

				if (longValue == 0)
				{
					// only throw if zero isn't defined in the enum - we have to convert zero to the underlying type of the enum
					//throwEx = !Enum.IsDefined(typeof(TEnum), ((IConvertible)0).ToType(Enum.GetUnderlyingType(typeof(TEnum)), CultureInfo.InvariantCulture));
					throwEx = !Enum.IsDefined(typeof(TEnum), default(TEnum));
				}
				else
				{
					foreach (TEnum value in GetEnumValues<TEnum>())
					{
						longValue &= ~value.ToInt64(CultureInfo.InvariantCulture);
					}

					// throw if there is a value left over after removing all valid values
					throwEx = longValue != 0;
				}

				if (throwEx)
				{
					throw new ArgumentException(
						string.Format(
							CultureInfo.InvariantCulture,
							"Enum value '{0}' is not valid for flags enumeration '{1}'.",
							enumValue,
							typeof(TEnum).FullName),
						argName);
				}
			}
			else
			{
				// not a flag enumeration
				if (!Enum.IsDefined(typeof(TEnum), enumValue))
				{
					throw new ArgumentException(
						string.Format(
							CultureInfo.InvariantCulture,
							"Enum value '{0}' is not defined for enumeration '{1}'.",
							enumValue,
							typeof(TEnum).FullName),
						argName);
				}
			}
		}

		/// <include file='ArgumentHelper.doc.xml' path='doc/member[@name="AssertEnumMember{TEnum}(TEnum,string,TEnum[])"]/*' />
		[DebuggerHidden]
		[CLSCompliant(false)]
		public static void AssertEnumMember<TEnum>(TEnum enumValue, string argName, params TEnum[] validValues)
			where TEnum : struct, IConvertible
		{
			AssertNotNull(validValues, "validValues");

			if (Attribute.IsDefined(typeof(TEnum), typeof(FlagsAttribute), false))
			{
				// flag enumeration
				bool throwEx;
				var longValue = enumValue.ToInt64(CultureInfo.InvariantCulture);

				if (longValue == 0)
				{
					// only throw if zero isn't permitted by the valid values
					throwEx = true;

					foreach (TEnum value in validValues)
					{
						if (value.ToInt64(CultureInfo.InvariantCulture) == 0)
						{
							throwEx = false;
							break;
						}
					}
				}
				else
				{
					foreach (var value in validValues)
					{
						longValue &= ~value.ToInt64(CultureInfo.InvariantCulture);
					}

					// throw if there is a value left over after removing all valid values
					throwEx = longValue != 0;
				}

				if (throwEx)
				{
					throw new ArgumentException(
						string.Format(
							CultureInfo.InvariantCulture,
							"Enum value '{0}' is not allowed for flags enumeration '{1}'.",
							enumValue,
							typeof(TEnum).FullName),
						argName);
				}
			}
			else
			{
				// not a flag enumeration
				foreach (var value in validValues)
				{
					if (enumValue.Equals(value))
					{
						return;
					}
				}

				// at this point we know an exception is required - however, we want to tailor the message based on whether the
				// specified value is undefined or simply not allowed
				if (!Enum.IsDefined(typeof(TEnum), enumValue))
				{
					throw new ArgumentException(
						string.Format(
							CultureInfo.InvariantCulture,
							"Enum value '{0}' is not defined for enumeration '{1}'.",
							enumValue,
							typeof(TEnum).FullName),
						argName);
				}
				else
				{
					throw new ArgumentException(
						string.Format(
							CultureInfo.InvariantCulture,
							"Enum value '{0}' is defined for enumeration '{1}' but it is not permitted in this context.",
							enumValue,
							typeof(TEnum).FullName),
						argName);
				}
			}
		}

		private static bool IsOnlyWhitespace(string arg)
		{
			Debug.Assert(arg != null, "Expecting arg to be non-null.");

			foreach (var c in arg)
			{
				if (!char.IsWhiteSpace(c))
				{
					return false;
				}
			}

			return true;
		}

		private static IEnumerable<T> GetEnumValues<T>()
		{
			var type = typeof(T);

			if (!type.IsEnum)
			{
				throw new ArgumentException("Type '" + type.Name + "' is not an enum");
			}

			return from field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
				   where field.IsLiteral
				   select (T)field.GetValue(null);
		}
	}
}