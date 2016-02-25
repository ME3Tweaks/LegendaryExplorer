using System;

namespace Gammtek.Conduit.Mvp.Xml.XInclude
{
	public abstract class XIncludeException : Exception
	{
		protected XIncludeException(string message)
			: base(message) {}

		protected XIncludeException(string message, Exception innerException)
			: base(message, innerException) {}
	}
}
