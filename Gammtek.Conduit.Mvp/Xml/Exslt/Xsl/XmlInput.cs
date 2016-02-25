using System;
using System.IO;
using System.Xml;
using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Common.Xsl
{
	public class XmlInput
	{
		internal XmlResolver Resolver;
		internal object Source;

		public XmlInput(XmlReader reader, XmlResolver resolver)
		{
			Source = reader;
			Resolver = resolver;
		}

		public XmlInput(TextReader reader, XmlResolver resolver)
		{
			Source = reader;
			Resolver = resolver;
		}

		public XmlInput(Stream stream, XmlResolver resolver)
		{
			Source = stream;
			Resolver = resolver;
		}

		public XmlInput(String uri, XmlResolver resolver)
		{
			Source = uri;
			Resolver = resolver;
		}

		public XmlInput(IXPathNavigable nav, XmlResolver resolver)
		{
			Source = nav;
			Resolver = resolver;
		}

		public XmlInput(XmlReader reader)
			: this(reader, new DefaultXmlResolver()) {}

		public XmlInput(TextReader reader)
			: this(reader, new DefaultXmlResolver()) {}

		public XmlInput(Stream stream)
			: this(stream, new DefaultXmlResolver()) {}

		public XmlInput(String uri)
			: this(uri, new DefaultXmlResolver()) {}

		public XmlInput(IXPathNavigable nav)
			: this(nav, new DefaultXmlResolver()) {}

		// We can add set of implicit constructors. 
		// I am not shre that this will be for good, so I commented them for now.
		//public static implicit operator XmlInput(XmlReader      reader) { return new XmlInput(reader); }
		//public static implicit operator XmlInput(TextReader     reader) { return new XmlInput(reader); }
		//public static implicit operator XmlInput(Stream         stream) { return new XmlInput(stream); }
		//public static implicit operator XmlInput(String         uri   ) { return new XmlInput(uri   ); }
		//public static implicit operator XmlInput(XPathNavigator nav   ) { return new XmlInput(nav   ); } // the trick doesn't work with interfaces
	}
}
