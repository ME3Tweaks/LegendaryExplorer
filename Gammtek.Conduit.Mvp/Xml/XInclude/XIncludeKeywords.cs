using System.Xml;

namespace Gammtek.Conduit.Mvp.Xml.XInclude
{
	internal class XIncludeKeywords
	{
		//
		// Keyword strings
		private const string XIncludeNamespaceStr = "http://www.w3.org/2001/XInclude";
		private const string OldXIncludeNamespaceStr = "http://www.w3.org/2003/XInclude";
		private const string IncludeStr = "include";
		private const string HrefStr = "href";
		private const string ParseStr = "parse";
		private const string XmlStr = "xml";
		private const string TextStr = "text";
		private const string XPointerStr = "xpointer";
		private const string AcceptStr = "accept";
		private const string AcceptLanguageStr = "accept-language";
		private const string EncodingStr = "encoding";
		private const string FallbackStr = "fallback";
		private const string XmlNamespaceStr = "http://www.w3.org/XML/1998/namespace";
		private const string BaseStr = "base";
		private const string XmlBaseStr = "xml:base";
		private const string LangStr = "lang";
		private const string XmlLangStr = "xml:lang";

		private readonly string _href;
		private readonly string _include;
		private readonly XmlNameTable _nameTable;
		private readonly string _oldXIncludeNamespace;
		private readonly string _parse;
		private readonly string _xIncludeNamespace;
		private string _accept;
		private string _acceptLanguage;
		private string _base;
		private string _encoding;
		private string _fallback;
		private string _lang;
		private string _text;
		private string _xml;
		private string _xmlBase;
		private string _xmlLang;
		private string _xmlNamespace;
		private string _xpointer;

		public XIncludeKeywords(XmlNameTable nt)
		{
			_nameTable = nt;
			//Preload some keywords
			_xIncludeNamespace = _nameTable.Add(XIncludeNamespaceStr);
			_oldXIncludeNamespace = _nameTable.Add(OldXIncludeNamespaceStr);
			_include = _nameTable.Add(IncludeStr);
			_href = _nameTable.Add(HrefStr);
			_parse = _nameTable.Add(ParseStr);
		}

		// http://www.w3.org/2003/XInclude
		public string XIncludeNamespace
		{
			get { return _xIncludeNamespace; }
		}

		// http://www.w3.org/2001/XInclude
		public string OldXIncludeNamespace
		{
			get { return _oldXIncludeNamespace; }
		}

		// include
		public string Include
		{
			get { return _include; }
		}

		// href
		public string Href
		{
			get { return _href; }
		}

		// parse
		public string Parse
		{
			get { return _parse; }
		}

		// xml
		public string Xml
		{
			get
			{
				if (_xml == null)
				{
					_xml = _nameTable.Add(XmlStr);
				}
				return _xml;
			}
		}

		// text
		public string Text
		{
			get
			{
				if (_text == null)
				{
					_text = _nameTable.Add(TextStr);
				}
				return _text;
			}
		}

		// xpointer
		public string Xpointer
		{
			get
			{
				if (_xpointer == null)
				{
					_xpointer = _nameTable.Add(XPointerStr);
				}
				return _xpointer;
			}
		}

		// accept
		public string Accept
		{
			get
			{
				if (_accept == null)
				{
					_accept = _nameTable.Add(AcceptStr);
				}
				return _accept;
			}
		}

		// accept-language
		public string AcceptLanguage
		{
			get
			{
				if (_acceptLanguage == null)
				{
					_acceptLanguage = _nameTable.Add(AcceptLanguageStr);
				}
				return _acceptLanguage;
			}
		}

		// encoding
		public string Encoding
		{
			get
			{
				if (_encoding == null)
				{
					_encoding = _nameTable.Add(EncodingStr);
				}
				return _encoding;
			}
		}

		// fallback
		public string Fallback
		{
			get
			{
				if (_fallback == null)
				{
					_fallback = _nameTable.Add(FallbackStr);
				}
				return _fallback;
			}
		}

		// Xml namespace
		public string XmlNamespace
		{
			get
			{
				if (_xmlNamespace == null)
				{
					_xmlNamespace = _nameTable.Add(XmlNamespaceStr);
				}
				return _xmlNamespace;
			}
		}

		// Base
		public string Base
		{
			get
			{
				if (_base == null)
				{
					_base = _nameTable.Add(BaseStr);
				}
				return _base;
			}
		}

		// xml:base
		public string XmlBase
		{
			get
			{
				if (_xmlBase == null)
				{
					_xmlBase = _nameTable.Add(XmlBaseStr);
				}
				return _xmlBase;
			}
		}

		// Lang
		public string Lang
		{
			get
			{
				if (_lang == null)
				{
					_lang = _nameTable.Add(LangStr);
				}
				return _lang;
			}
		}

		// xml:lang
		public string XmlLang
		{
			get
			{
				if (_xmlLang == null)
				{
					_xmlLang = _nameTable.Add(XmlLangStr);
				}
				return _xmlLang;
			}
		}

		// Comparison
		public static bool Equals(string keyword1, string keyword2)
		{
			return keyword1 == keyword2;
		}
	}
}
