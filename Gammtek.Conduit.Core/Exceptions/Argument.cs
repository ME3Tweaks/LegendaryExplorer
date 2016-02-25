using System;

namespace Gammtek.Conduit.Exceptions
{
	public static class Argument
	{
		[ContractAnnotation("obj:null => halt")]
		public static void IsNotNull([InvokerParameterName] string paramName, object obj)
		{
			if (obj == null)
			{
				throw new ArgumentNullException(paramName);
			}
		}

		public static void IsNotOutOfRange<T>([InvokerParameterName] string paramName, T obj, T min, T max)
			where T : IComparable<T>
		{
			IsNotNull("obj", obj);

			if (obj.CompareTo(min) < 0 || obj.CompareTo(max) > 0)
			{
				throw new ArgumentOutOfRangeException(paramName);
			}
		}

		[ContractAnnotation("=> halt")]
		public static void ThrowNull([InvokerParameterName] string paramName)
		{
			throw new ArgumentNullException(paramName);
		}

		[ContractAnnotation("=> halt")]
		public static void ThrowOutOfRange([InvokerParameterName] string paramName)
		{
			throw new ArgumentOutOfRangeException(paramName);
		}
	}
}
