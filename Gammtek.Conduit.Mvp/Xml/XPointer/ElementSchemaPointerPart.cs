using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Xml.Common.XPath;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.XPointer
{
	internal class ElementSchemaPointerPart : PointerPart
	{
		public string XPath { get; set; }

		public override XPathNodeIterator Evaluate(XPathNavigator doc,
			XmlNamespaceManager nm)
		{
			return XPathCache.Select(XPath, doc, nm);
		}

		public static ElementSchemaPointerPart ParseSchemaData(XPointerLexer lexer)
		{
			//Productions:
			//[1]   	ElementSchemeData	   ::=   	(NCName ChildSequence?) | ChildSequence
			//[2]   	ChildSequence	   ::=   	('/' [1-9] [0-9]*)+                        
			var xpathBuilder = new StringBuilder();
			var part = new ElementSchemaPointerPart();
			lexer.NextLexeme();
			if (lexer.Kind == XPointerLexer.LexKind.NcName)
			{
				xpathBuilder.Append("id('");
				xpathBuilder.Append(lexer.NcName);
				xpathBuilder.Append("')");
				lexer.NextLexeme();
			}
			var childSequenceLen = 0;
			while (lexer.Kind == XPointerLexer.LexKind.Slash)
			{
				lexer.NextLexeme();
				if (lexer.Kind != XPointerLexer.LexKind.Number)
				{
					Debug.WriteLine(Resources.InvalidTokenInElementSchemeWhileNumberExpected);
					return null;
				}
				if (lexer.Number == 0)
				{
					Debug.WriteLine(Resources.ZeroIndexInElementSchemechildSequence);
					return null;
				}
				childSequenceLen++;
				xpathBuilder.Append("/*[");
				xpathBuilder.Append(lexer.Number);
				xpathBuilder.Append("]");
				lexer.NextLexeme();
			}
			if (lexer.Kind != XPointerLexer.LexKind.RrBracket)
			{
				throw new XPointerSyntaxException(Resources.InvalidTokenInElementSchemeWhileClosingRoundBracketExpected);
			}
			if (xpathBuilder.Length == 0 && childSequenceLen == 0)
			{
				Debug.WriteLine(Resources.EmptyElementSchemeXPointer);
				return null;
			}
			part.XPath = xpathBuilder.ToString();
			return part;
		}
	}
}
