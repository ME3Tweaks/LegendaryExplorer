using System.Collections.Generic;

namespace Gammtek.Conduit.MassEffect.Localization.Tlk.Xml
{
	public class XmlTlkFile : TlkFile
	{
		public XmlTlkFile(IList<TlkString> strings = null, IList<string> includes = null)
			: base(strings)
		{
			Includes = includes ?? new List<string>();
		}

		public IList<string> Includes { get; set; }
	}
}