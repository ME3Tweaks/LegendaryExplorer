using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Common.XPath
{
	public class SingletonXPathNodeIterator : XPathNodeIterator
	{
		private readonly XPathNavigator _navigator;
		private int _position;

		public SingletonXPathNodeIterator(XPathNavigator nav)
		{
			_navigator = nav;
		}

		public override int Count
		{
			get { return 1; }
		}

		public override XPathNavigator Current
		{
			get { return _navigator; }
		}

		public override int CurrentPosition
		{
			get { return _position; }
		}

		public override XPathNodeIterator Clone()
		{
			return new SingletonXPathNodeIterator(_navigator.Clone());
		}

		public override bool MoveNext()
		{
			if (_position != 0)
			{
				return false;
			}

			_position = 1;
			return true;
		}
	}
}
