using System;
using System.IO;
using System.Xml;

namespace Gammtek.Conduit.Mvp.Xml.Common.Xsl
{
	public class XmlOutput
	{
		internal object Destination;

		public XmlOutput(XmlWriter writer)
		{
			Destination = writer;
		}

		public XmlOutput(TextWriter writer)
		{
			Destination = writer;
		}

		public XmlOutput(Stream stream)
		{
			Destination = stream;
		}

		public XmlOutput(String uri)
		{
			Destination = uri;
		}

		public XmlResolver XmlResolver { get; set; }
	}
}
