using System;

namespace LegendaryExplorerCore.Gammtek.Collections.Specialized
{
	public class UnregisteredEnumException : Exception
	{
		public UnregisteredEnumException(string message)
			: base(message)
		{}
	}
}
