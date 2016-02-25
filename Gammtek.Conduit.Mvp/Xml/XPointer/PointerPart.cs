using System.Xml;
using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.XPointer
{
	internal abstract class PointerPart
	{
		public abstract XPathNodeIterator Evaluate(XPathNavigator doc, XmlNamespaceManager nm);
	}
}
