using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	public class XhtmlWriter : XmlWrappingWriter
	{
		private const string XHtmlNamespace = "http://www.w3.org/1999/xhtml";
		private readonly Stack<QName> _elementStack;

		public XhtmlWriter(XmlWriter baseWriter)
			: base(baseWriter)
		{
			_elementStack = new Stack<QName>();
		}

		public override void WriteEndElement()
		{
			WriteXHtmlEndElement(false);
		}

		public override void WriteFullEndElement()
		{
			WriteXHtmlEndElement(true);
		}

		public override void WriteStartElement(string prefix, string localName, string ns)
		{
			_elementStack.Push(new QName(localName, ns, prefix));
			base.WriteStartElement(prefix, localName, ns);
		}

		private void WriteXHtmlEndElement(bool fullEndTag)
		{
			var writeFullEndTag = fullEndTag;
			var elementName = _elementStack.Pop();
			if (elementName.NsUri == XHtmlNamespace)
			{
				switch (elementName.Local.ToLower(CultureInfo.InvariantCulture))
				{
					case "area":
					case "base":
					case "basefont":
					case "br":
					case "col":
					case "frame":
					case "hr":
					case "img":
					case "input":
					case "isindex":
					case "link":
					case "meta":
					case "param":
						writeFullEndTag = false;
						break;
					default:
						writeFullEndTag = true;
						break;
				}
			}
			if (writeFullEndTag)
			{
				base.WriteFullEndElement();
			}
			else
			{
				base.WriteEndElement();
			}
		}
	}
}
