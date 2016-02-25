using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.XPointer
{
	public abstract class Pointer
	{
		public static Pointer Compile(string xpointer)
		{
			return XPointerParser.ParseXPointer(xpointer);
		}

		public abstract XPathNodeIterator Evaluate(XPathNavigator nav);
	}
}
