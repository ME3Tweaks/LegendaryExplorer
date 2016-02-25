using System;
using System.Diagnostics;
using System.Globalization;
using System.Xml;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.XPointer
{
	internal class XmlnsSchemaPointerPart : PointerPart
	{
		private string _prefix, _uri;

		public XmlnsSchemaPointerPart(string prefix, string uri)
		{
			_prefix = prefix;
			_uri = uri;
		}

		public string Prefix
		{
			get { return _prefix; }
			set { _prefix = value; }
		}

		public string Uri
		{
			get { return _uri; }
			set { _uri = value; }
		}

		public override XPathNodeIterator Evaluate(XPathNavigator doc, XmlNamespaceManager nm)
		{
			nm.AddNamespace(_prefix, _uri);
			return null;
		}

		public static XmlnsSchemaPointerPart ParseSchemaData(XPointerLexer lexer)
		{
			//[1]   	XmlnsSchemeData	   ::=   	 NCName S? '=' S? EscapedNamespaceName
			//[2]   	EscapedNamespaceName	   ::=   	EscapedData*                      	                    
			//Read prefix as NCName
			lexer.NextLexeme();
			if (lexer.Kind != XPointerLexer.LexKind.NcName)
			{
				Debug.WriteLine(Resources.InvalidTokenInXmlnsSchemeWhileNCNameExpected);
				return null;
			}
			var prefix = lexer.NcName;
			lexer.SkipWhiteSpace();
			lexer.NextLexeme();
			if (lexer.Kind != XPointerLexer.LexKind.Eq)
			{
				Debug.WriteLine(Resources.InvalidTokenInXmlnsSchemeWhileEqualsSignExpected);
				return null;
			}
			lexer.SkipWhiteSpace();
			string nsUri;
			try
			{
				nsUri = lexer.ParseEscapedData();
			}
			catch (Exception e)
			{
				throw new XPointerSyntaxException(String.Format(CultureInfo.CurrentCulture, Resources.SyntaxErrorInXmlnsSchemeData, e.Message));
			}
			return new XmlnsSchemaPointerPart(prefix, nsUri);
		}
	}
}
