using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MassEffect2.SaveEdit
{
	public static class EnumExtensions
	{
		/*public static IEnumerable<Enum> GetFlags(this Enum value)
		{
			return GetFlags(value, Enum.GetValues(value.GetType()).Cast<Enum>().ToArray());
		}

		public static IEnumerable<Enum> GetIndividualFlags(this Enum value)
		{
			return GetFlags(value, GetFlagValues(value.GetType()).ToArray());
		}

		private static IEnumerable<Enum> GetFlags(Enum value, Enum[] values)
		{
			ulong bits = Convert.ToUInt64(value);
			List<Enum> results = new List<Enum>();
			for (int i = values.Length - 1; i >= 0; i--)
			{
				ulong mask = Convert.ToUInt64(values[i]);
				if (i == 0 && mask == 0L)
					break;
				if ((bits & mask) == mask)
				{
					results.Add(values[i]);
					bits -= mask;
				}
			}
			if (bits != 0L)
				return Enumerable.Empty<Enum>();
			if (Convert.ToUInt64(value) != 0L)
				return results.Reverse<Enum>();
			if (bits == Convert.ToUInt64(value) && values.Length > 0 && Convert.ToUInt64(values[0]) == 0L)
				return values.Take(1);
			return Enumerable.Empty<Enum>();
		}

		private static IEnumerable<Enum> GetFlagValues(Type enumType)
		{
			ulong flag = 0x1;
			foreach (var value in Enum.GetValues(enumType).Cast<Enum>())
			{
				ulong bits = Convert.ToUInt64(value);
				if (bits == 0L)
					//yield return value;
					continue; // skip the zero value
				while (flag < bits) flag <<= 1;
				if (flag == bits)
					yield return value;
			}
		}*/

		/*public static IEnumerable<T> GetFlags<T>(this T value) where T : struct
		{
			return GetFlags(value, Enum.GetValues(typeof(T)).Cast<T>().ToArray());
		}*/

		public static IEnumerable<T> GetIndividualFlags<T>(this T value) where T : struct
		{
			return GetFlags(value, GetFlagValues<T>().ToArray());
		}

		private static IEnumerable<T> GetFlagValues<T>() where T : struct 
		{
			CheckIsEnum<T>(true);

			ulong flag = 0x1;

			foreach (var value in Enum.GetValues(typeof(T)).Cast<T>())
			{
				var bits = Convert.ToUInt64(value);

				if (bits == 0L)
				{
					//yield return value;
					continue; // skip the zero value
				}
				while (flag < bits) flag <<= 1;
				
				if (flag == bits)
				{
					yield return value;
				}
			}
		}

		private static void CheckIsEnum<T>(bool withFlags)
		{
			if (!typeof (T).IsEnum)
			{
				throw new ArgumentException(string.Format("Type '{0}' is not an enum", typeof (T).FullName));
			}

			if (withFlags && !Attribute.IsDefined(typeof (T), typeof (FlagsAttribute)))
			{
				throw new ArgumentException(string.Format("Type '{0}' doesn't have the 'Flags' attribute", typeof (T).FullName));
			}
		}

		public static bool IsFlagSet<T>(this T value, T flag) where T : struct
		{
			CheckIsEnum<T>(true);

			var lValue = Convert.ToInt64(value);
			var lFlag = Convert.ToInt64(flag);

			return (lValue & lFlag) != 0;
		}

		public static IEnumerable<T> GetFlags<T>(this T value) where T : struct
		{
			CheckIsEnum<T>(true);

			return Enum.GetValues(typeof (T)).Cast<T>().Where(flag => value.IsFlagSet(flag));
		}

		public static IEnumerable<T> GetFlags<T>(T value, T[] values) where T : struct
		{
			CheckIsEnum<T>(true);

			if (values == null)
			{
				values = GetFlagValues<T>().ToArray();
			}

			var bits = Convert.ToUInt64(value);
			var results = new List<T>();

			for (var i = values.Length - 1; i >= 0; i--)
			{
				var mask = Convert.ToUInt64(values[i]);

				if (i == 0 && mask == 0L)
				{
					break;
				}

				if ((bits & mask) == mask)
				{
					results.Add(values[i]);
					bits -= mask;
				}
			}

			if (bits != 0L)
			{
				return Enumerable.Empty<T>();
			}

			if (Convert.ToUInt64(value) != 0L)
			{
				return results.Reverse<T>();
			}
			if (bits == Convert.ToUInt64(value) && values.Length > 0 && Convert.ToUInt64(values[0]) == 0L)
			{
				return values.Take(1);
			}

			return Enumerable.Empty<T>();
		}

		public static T SetFlags<T>(this T value, T flags, bool on) where T : struct
		{
			CheckIsEnum<T>(true);

			var lValue = Convert.ToInt64(value);
			var lFlag = Convert.ToInt64(flags);

			if (on)
			{
				lValue |= lFlag;
			}
			else
			{
				lValue &= (~lFlag);
			}

			return (T) Enum.ToObject(typeof (T), lValue);
		}

		public static T SetFlags<T>(this T value, T flags) where T : struct
		{
			return value.SetFlags(flags, true);
		}

		public static T ClearFlags<T>(this T value, T flags) where T : struct
		{
			return value.SetFlags(flags, false);
		}

		public static T CombineFlags<T>(this IEnumerable<T> flags) where T : struct
		{
			CheckIsEnum<T>(true);

			var lValue = flags.Select(flag => Convert.ToInt64(flag)).Aggregate<long, long>(0, (current, lFlag) => current | lFlag);

			return (T) Enum.ToObject(typeof (T), lValue);
		}

		public static string GetDescription<T>(this T value) where T : struct
		{
			CheckIsEnum<T>(false);
			var name = Enum.GetName(typeof (T), value);

			if (name == null)
			{
				return null;
			}

			var field = typeof (T).GetField(name);

			if (field == null)
			{
				return null;
			}

			var attr = Attribute.GetCustomAttribute(field, typeof (DescriptionAttribute)) as DescriptionAttribute;

			if (attr != null)
			{
				return attr.Description;
			}

			return null;
		}
	}
}