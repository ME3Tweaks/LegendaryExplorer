using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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

        [DoesNotReturn]
        public static void ThrowArgumentException([InvokerParameterName] string paramName, string message)
        {
            throw new ArgumentException(message, paramName);
        }

        public static void ThrowIfNotInBounds(int index, int length, [CallerArgumentExpression("index")] string paramName = null)
        {
            if (unchecked((uint)index >= (uint)length))
            {
                ThrowArgumentOutOfRangeException(paramName);
            }
        }
    }
}
