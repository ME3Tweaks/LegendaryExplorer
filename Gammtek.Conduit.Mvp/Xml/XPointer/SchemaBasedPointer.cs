using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.XPointer
{
	internal class SchemaBasedPointer : Pointer
	{
		private readonly IList<PointerPart> _parts;
		private readonly string _xpointer;

		public SchemaBasedPointer(IList<PointerPart> parts, string xpointer)
		{
			_parts = parts;
			_xpointer = xpointer;
		}

		public override XPathNodeIterator Evaluate(XPathNavigator nav)
		{
			if (nav.NameTable == null)
			{
				throw new NoSubresourcesIdentifiedException(String.Format(CultureInfo.CurrentCulture, Resources.NoSubresourcesIdentifiedException,
					_xpointer));
			}
			var nm = new XmlNamespaceManager(nav.NameTable);
			foreach (var result in _parts.Select(part => part.Evaluate(nav, nm)).Where(result => result != null && result.MoveNext()))
			{
				return result;
			}
			throw new NoSubresourcesIdentifiedException(String.Format(CultureInfo.CurrentCulture, Resources.NoSubresourcesIdentifiedException,
				_xpointer));
		}
	}
}
