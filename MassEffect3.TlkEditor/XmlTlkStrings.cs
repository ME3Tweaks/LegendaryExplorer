using System.Collections.Generic;
using System.Xml.Serialization;

namespace MassEffect3.TlkEditor
{
	[XmlType("XmlTlkStrings")]
	[XmlInclude(typeof(XmlTlkString))]
	public class XmlTlkStrings : List<XmlTlkString>
	{
		public XmlTlkStrings()
		{}

		public XmlTlkStrings(IEnumerable<XmlTlkString> collection)
			: base(collection)
		{}

		public XmlTlkStrings(int capacity)
			: base(capacity)
		{}
	}
}