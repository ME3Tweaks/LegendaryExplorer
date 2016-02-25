using System;
using System.Globalization;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	internal static class Guard
	{
		public static void ArgumentNotNull(object value, string argumentName)
		{
			if (value == null)
			{
				throw new ArgumentNullException(argumentName);
			}
		}

		public static void ArgumentNotNullOrEmptyString(string value, string argumentName)
		{
			ArgumentNotNull(value, argumentName);

			if (value.Length == 0)
			{
				throw new ArgumentException(String.Format(
					CultureInfo.CurrentCulture,
					Resources.Arg_NullOrEmpty),
					argumentName);
			}
		}
	}
}
