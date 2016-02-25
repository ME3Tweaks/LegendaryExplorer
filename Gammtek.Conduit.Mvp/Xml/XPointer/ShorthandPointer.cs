using System;
using System.Globalization;
using System.Xml;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Xml.Common.XPath;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.XPointer
{
	internal class ShorthandPointer : Pointer
	{
		private readonly string _ncName;

		public ShorthandPointer(string n)
		{
			_ncName = n;
		}

		public override XPathNodeIterator Evaluate(XPathNavigator nav)
		{
			var result = XPathCache.Select("id('" + _ncName + "')", nav, (XmlNamespaceManager) null);
			if (result != null && result.MoveNext())
			{
				return result;
			}
			throw new NoSubresourcesIdentifiedException(String.Format(CultureInfo.CurrentCulture, Resources.NoSubresourcesIdentifiedException,
				_ncName));
		}
	}
}
