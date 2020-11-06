using System;

namespace ME3ExplorerCore.Gammtek.Collections.Specialized
{
	public class UnregisteredEnumException : Exception
	{
		public UnregisteredEnumException(string message)
			: base(message)
		{}
	}
}
