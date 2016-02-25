using System;
using System.Globalization;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.XInclude
{
	public class InvalidAcceptHttpHeaderValueError : FatalException
	{
		public InvalidAcceptHttpHeaderValueError(char c)
			: base(String.Format(CultureInfo.CurrentCulture, Resources.InvalidCharForAccept, ((int) c).ToString("X2"))) {}
	}
}
