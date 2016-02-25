using System.Xml;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	public class XmlPrefix
	{
		public XmlPrefix(string prefix, string ns)
		{
			Prefix = prefix;
			NamespaceUri = ns;
		}

		public XmlPrefix(string prefix, string ns, XmlNameTable nameTable)
		{
			Prefix = nameTable.Add(prefix);
			NamespaceUri = nameTable.Add(ns);
		}

		public string Prefix { get; private set; }

		public string NamespaceUri { get; private set; }
	}
}
