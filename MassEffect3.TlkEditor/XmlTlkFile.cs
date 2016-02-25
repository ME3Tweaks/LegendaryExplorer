using System.Xml.Serialization;

namespace MassEffect3.TlkEditor
{
	[XmlRoot("TlkFile", IsNullable = false, Namespace = "")]
	[XmlInclude(typeof(XmlTlkString))]
	[XmlInclude(typeof(XmlTlkStrings))]
	public class XmlTlkFile
	{
		public XmlTlkFile()
			: this(null)
		{}

		public XmlTlkFile(XmlTlkStrings strings)
		{
			Strings = strings ?? new XmlTlkStrings();
		}

		[XmlArray("Strings")]
		[XmlArrayItem("String", typeof(XmlTlkString))]
		public XmlTlkStrings Strings { get; set; }

		//[XmlIgnore]
		public XmlTlkString this[int index]
		{
			get { return Strings[index]; }
			set { Strings[index] = value; }
		}
	}
}