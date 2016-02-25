using System;

namespace Gammtek.Conduit.Mvp.Xml.XInclude
{
	public abstract class FatalException : XIncludeException
	{
		protected FatalException(string message)
			: base(message) {}

		protected FatalException(string message, Exception innerException)
			: base(message, innerException) {}
	}
}
