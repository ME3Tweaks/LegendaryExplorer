using System;

namespace Gammtek.Conduit.Mvp.Xml.XPointer
{
	public abstract class XPointerException : Exception
	{
		protected XPointerException(string message)
			: base(message) {}

		protected XPointerException(string message, Exception innerException)
			: base(message, innerException) {}
	}
}
