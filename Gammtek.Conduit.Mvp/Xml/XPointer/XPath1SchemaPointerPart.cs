using System;
using System.Globalization;
using System.Xml;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Xml.Common.XPath;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.XPointer
{
	internal class XPath1SchemaPointerPart : PointerPart
	{
		private string _xpath;

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

		public static XPath1SchemaPointerPart ParseSchemaData(XPointerLexer lexer)
		{
			var part = new XPath1SchemaPointerPart();
			try
			{
				part._xpath = lexer.ParseEscapedData();
			}
			catch (Exception e)
			{
				throw new XPointerSyntaxException(String.Format(
					CultureInfo.CurrentCulture,
					Resources.SyntaxErrorInXPath1SchemeData,
					e.Message));
			}
			return part;
		}
	}
}
