using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Common.XPath
{
	public class EmptyXPathNodeIterator : XPathNodeIterator
	{
		public static EmptyXPathNodeIterator Instance = new EmptyXPathNodeIterator();

		private EmptyXPathNodeIterator() {}

		public override int Count
		{
			get { return 0; }
		}

		public override XPathNavigator Current
		{
			get { return null; }
		}

		public override int CurrentPosition
		{
			get { return 0; }
		}

		public override XPathNodeIterator Clone()
		{
			return this;
		}

		public override bool MoveNext()
		{
			return false;
		}
	}
}
