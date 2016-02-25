using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.XInclude
{
	internal class TextIncludingReader : XmlReader
	{
		private readonly string _accept;
		private readonly string _acceptLanguage;
		private readonly string _encoding;
		private readonly bool _exposeCData;
		private readonly string _href;
		private readonly Uri _includeLocation;
		private ReadState _state;
		private string _value;

		public TextIncludingReader(Uri includeLocation, string encoding,
			string accept, string acceptLanguage, bool exposeCData)
		{
			_includeLocation = includeLocation;
			_href = includeLocation.AbsoluteUri;
			_encoding = encoding;
			_state = ReadState.Initial;
			_accept = accept;
			_acceptLanguage = acceptLanguage;
			_exposeCData = exposeCData;
		}

		public TextIncludingReader(string value, bool exposeCData)
		{
			_state = ReadState.Initial;
			_exposeCData = exposeCData;
			_value = value;
		}

		public override int AttributeCount
		{
			get { return 0; }
		}

		public override string BaseURI
		{
			get { return _href; }
		}

		public override int Depth
		{
			get { return _state == ReadState.Interactive ? 1 : 0; }
		}

		public override bool EOF
		{
			get { return _state == ReadState.EndOfFile; }
		}

		public override bool HasValue
		{
			get { return _state == ReadState.Interactive; }
		}

		public override bool IsDefault
		{
			get { return false; }
		}

		public override bool IsEmptyElement
		{
			get { return false; }
		}

		public override string this[int index]
		{
			get { return String.Empty; }
		}

		public override string this[string qname]
		{
			get { return String.Empty; }
		}

		public override string this[string localname, string nsuri]
		{
			get { return String.Empty; }
		}

		public override string LocalName
		{
			get { return String.Empty; }
		}

		public override string Name
		{
			get { return String.Empty; }
		}

		public override string NamespaceURI
		{
			get { return String.Empty; }
		}

		public override XmlNameTable NameTable
		{
			get { return null; }
		}

		public override XmlNodeType NodeType
		{
			get
			{
				return _state == ReadState.Interactive
					? _exposeCData ? XmlNodeType.CDATA : XmlNodeType.Text
					: XmlNodeType.None;
			}
		}

		public override string Prefix
		{
			get { return String.Empty; }
		}

		public override char QuoteChar
		{
			get { return '"'; }
		}

		public override ReadState ReadState
		{
			get { return _state; }
		}

		public override string Value
		{
			get { return _state == ReadState.Interactive ? _value : String.Empty; }
		}

		public override string XmlLang
		{
			get { return String.Empty; }
		}

		public override XmlSpace XmlSpace
		{
			get { return XmlSpace.None; }
		}

		public override void Close()
		{
			_state = ReadState.Closed;
		}

		public override string GetAttribute(int index)
		{
			throw new ArgumentOutOfRangeException("index", index, @"No attributes exposed");
		}

		public override string GetAttribute(string qname)
		{
			return null;
		}

		public override string GetAttribute(string localname, string nsuri)
		{
			return null;
		}

		private static Encoding GetEncodingFromXmlDecl(string href)
		{
			var tmpReader = new XmlTextReader(href)
			{
				DtdProcessing = DtdProcessing.Parse,
				WhitespaceHandling = WhitespaceHandling.None
			};
			try
			{
				while (tmpReader.Read() && tmpReader.Encoding == null) {}
				var enc = tmpReader.Encoding;
				return enc ?? Encoding.UTF8;
			}
			finally
			{
				tmpReader.Close();
			}
		}

		public override string LookupNamespace(string prefix)
		{
			return null;
		}

		public override void MoveToAttribute(int index) {}

		public override bool MoveToAttribute(string qname)
		{
			return false;
		}

		public override bool MoveToAttribute(string localname, string nsuri)
		{
			return false;
		}

		public override bool MoveToElement()
		{
			return false;
		}

		public override bool MoveToFirstAttribute()
		{
			return false;
		}

		public override bool MoveToNextAttribute()
		{
			return false;
		}

		public override bool Read()
		{
			switch (_state)
			{
				case ReadState.Initial:
					if (_value == null)
					{
						WebResponse wRes;
						var stream = XIncludingReader.GetResource(_includeLocation.AbsoluteUri,
							_accept, _acceptLanguage, out wRes);
						/* According to the spec, encoding should be determined as follows:
							* external encoding information, if available, otherwise
							* if the media type of the resource is text/xml, application/xml, 
							  or matches the conventions text/*+xml or application/*+xml as 
							  described in XML Media Types [IETF RFC 3023], the encoding is 
							  recognized as specified in XML 1.0, otherwise
							* the value of the encoding attribute if one exists, otherwise  
							* UTF-8.
						*/
						try
						{
							//If mime type is xml-aware, get resource encoding as per XML 1.0
							var contentType = wRes.ContentType.ToLower();
							StreamReader reader;
							if (contentType == "text/xml" ||
								contentType == "application/xml" ||
								contentType.StartsWith("text/") && contentType.EndsWith("+xml") ||
								contentType.StartsWith("application/") && contentType.EndsWith("+xml"))
							{
								//Yes, that's xml, let's read encoding from the xml declaration                    
								reader = new StreamReader(stream, GetEncodingFromXmlDecl(_href));
							}
							else if (_encoding != null)
							{
								//Try to use user-specified encoding
								Encoding enc;
								try
								{
									enc = Encoding.GetEncoding(_encoding);
								}
								catch (Exception e)
								{
									throw new ResourceException(String.Format(
										CultureInfo.CurrentCulture,
										Resources.NotSupportedEncoding,
										_encoding), e);
								}
								reader = new StreamReader(stream, enc);
							}
							else
							{
								//Fallback to UTF-8
								reader = new StreamReader(stream, Encoding.UTF8);
							}
							_value = reader.ReadToEnd();
							TextUtils.CheckForNonXmlChars(_value);
						}
						catch (OutOfMemoryException oome)
						{
							//Crazy include - memory is out
							throw new ResourceException(String.Format(CultureInfo.CurrentCulture, Resources.OutOfMemoryWhileFetchingResource, _href), oome);
						}
						catch (IOException ioe)
						{
							throw new ResourceException(String.Format(CultureInfo.CurrentCulture, Resources.IOErrorWhileFetchingResource, _href), ioe);
						}
					}
					_state = ReadState.Interactive;
					return true;
				case ReadState.Interactive:
					//No more input
					_state = ReadState.EndOfFile;
					return false;
				default:
					return false;
			}
		}

		public override bool ReadAttributeValue()
		{
			return false;
		}

		public override string ReadInnerXml()
		{
			return _state == ReadState.Interactive ? _value : String.Empty;
		}

		public override string ReadOuterXml()
		{
			return _state == ReadState.Interactive ? _value : String.Empty;
		}

		public override string ReadString()
		{
			return _state == ReadState.Interactive ? _value : String.Empty;
		}

		public override void ResolveEntity() {}
	}
}
