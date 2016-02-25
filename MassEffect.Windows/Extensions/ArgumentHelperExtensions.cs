using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace MassEffect.Windows.Extensions
{
	public static class ArgumentHelperExtensions
	{
		[DebuggerHidden]
		public static void AssertNotNull<T>(this T arg, string argName)
			where T : class
		{
			ArgumentHelper.AssertNotNull(arg, argName);
		}

		[DebuggerHidden]
		public static void AssertNotNull<T>(this T? arg, string argName)
			where T : struct
		{
			ArgumentHelper.AssertNotNull(arg, argName);
		}

		[DebuggerHidden]
		public static void AssertGenericArgumentNotNull<T>(this T arg, string argName)
		{
			ArgumentHelper.AssertGenericArgumentNotNull(arg, argName);
		}

		[DebuggerHidden]
		public static void AssertNotNull<T>(this IEnumerable<T> arg, string argName, bool assertContentsNotNull)
		{
			ArgumentHelper.AssertNotNull(arg, argName, assertContentsNotNull);
		}

		[DebuggerHidden]
		public static void AssertNotNullOrEmpty(this string arg, string argName)
		{
			ArgumentHelper.AssertNotNullOrEmpty(arg, argName);
		}

		[DebuggerHidden]
		public static void AssertNotNullOrEmpty(this IEnumerable arg, string argName)
		{
			ArgumentHelper.AssertNotNullOrEmpty(arg, argName);
		}

		[DebuggerHidden]
		public static void AssertNotNullOrEmpty(this ICollection arg, string argName)
		{
			ArgumentHelper.AssertNotNullOrEmpty(arg, argName);
		}

		[DebuggerHidden]
		public static void AssertNotNullOrWhiteSpace(this string arg, string argName)
		{
			ArgumentHelper.AssertNotNullOrWhiteSpace(arg, argName);
		}

		[DebuggerHidden]
		[CLSCompliant(false)]
		public static void AssertEnumMember<TEnum>(this TEnum enumValue, string argName)
			where TEnum : struct, IConvertible
		{
			ArgumentHelper.AssertEnumMember(enumValue, argName);
		}

		[DebuggerHidden]
		[CLSCompliant(false)]
		public static void AssertEnumMember<TEnum>(this TEnum enumValue, string argName, params TEnum[] validValues)
			where TEnum : struct, IConvertible
		{
			ArgumentHelper.AssertEnumMember(enumValue, argName, validValues);
		}
	}
}
