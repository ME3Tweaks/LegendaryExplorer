using System;

namespace Gammtek.Conduit.Mvp.Xml.XInclude
{
	internal class ResourceException : XIncludeException
	{
		public ResourceException(string message)
			: base(message) {}

		public ResourceException(string message, Exception innerException)
			: base(message, innerException) {}
	}
}
