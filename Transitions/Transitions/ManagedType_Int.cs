using System;
using System.Collections.Generic;
using System.Text;

namespace Transitions
{
    /// <summary>
    /// Manages transitions for int properties.
    /// </summary>
    internal class ManagedType_Int : IManagedType
    {
		#region IManagedType Members

		/// <summary>
		/// Returns the type we are managing.
		/// </summary>
		public Type getManagedType()
		{
			return typeof(int);
		}

		/// <summary>
		/// Returns a copy of the int passed in.
		/// </summary>
		public object copy(object o)
		{
			int value = (int)o;
			return value;
		}

		/// <summary>
		/// Returns the value between the start and end for the percentage passed in.
		/// </summary>
		public object getIntermediateValue(object start, object end, double dPercentage)
		{
			int iStart = (int)start;
			int iEnd = (int)end;
			return Utility.interpolate(iStart, iEnd, dPercentage);
		}

		#endregion
	}
}
