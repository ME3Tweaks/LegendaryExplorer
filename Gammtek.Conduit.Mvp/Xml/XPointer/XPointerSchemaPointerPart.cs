using System;
using System.Globalization;
using System.Xml;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Xml.Common.XPath;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.XPointer
{
	internal class XPointerSchemaPointerPart : PointerPart
	{
		private readonly string _xpath;

		public XPointerSchemaPointerPart(string xpath)
		{
			_xpath = xpath;
		}

		public override XPathNodeIterator Evaluate(XPathNavigator doc, XmlNamespaceManager nm)
		{
			try
			{
				return XPathCache.Select(_xpath, doc, nm);
			}
			catch
			{
				return null;
			}
		}

		public static XPointerSchemaPointerPart ParseSchemaData(XPointerLexer lexer)
		{
			try
			{
				return new XPointerSchemaPointerPart(lexer.ParseEscapedData());
			}
			catch (Exception e)
			{
				throw new XPointerSyntaxException(String.Format(CultureInfo.CurrentCulture, Resources.SyntaxErrorInXPointerSchemeData, e.Message));
			}
		}
	}
}
