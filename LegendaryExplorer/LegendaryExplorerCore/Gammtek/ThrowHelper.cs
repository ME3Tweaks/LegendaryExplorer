using System;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;


namespace LegendaryExplorerCore.Gammtek
{
	public static class ThrowHelper
	{
        [DoesNotReturn]
		public static void ThrowArgumentNullException([InvokerParameterName] string paramName)
		{
			throw new ArgumentNullException(paramName);
		}
		
        [DoesNotReturn]
		public static void ThrowArgumentOutOfRangeException([InvokerParameterName] string paramName)
		{
			throw new ArgumentOutOfRangeException(paramName);
		}
		
        [DoesNotReturn]
		public static void ThrowDivideByZeroException()
		{
			throw new DivideByZeroException();
		}

		[ContractAnnotation("obj:null => halt")]
        public static void ThrowExceptionIfNull([InvokerParameterName] string paramName, object obj)
		{
			if (obj == null)
			{
				throw new ArgumentNullException(paramName);
			}
		}
	}
}
