using System;
using System.Globalization;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.XInclude
{
	public class NonXmlCharacterException : FatalException
	{
		public NonXmlCharacterException(char c)
			: base(String.Format(CultureInfo.CurrentCulture, Resources.NonXmlCharacter, ((int) c).ToString("X2"))) {}
	}
}
