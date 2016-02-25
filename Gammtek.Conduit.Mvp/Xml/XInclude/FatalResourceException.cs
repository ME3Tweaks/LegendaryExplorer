using System;
using System.Globalization;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.XInclude
{
	public class FatalResourceException : FatalException
	{
		public FatalResourceException(Exception re)
			: base(String.Format(CultureInfo.CurrentCulture, Resources.FatalResourceException, re.Message), re) {}
	}
}
