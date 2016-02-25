using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using Gammtek.Conduit.Mvp.Xml.Common;
using Gammtek.Conduit.Mvp.Properties;
using Gammtek.Conduit.Mvp.Xml.XPointer;

namespace Gammtek.Conduit.Mvp.Xml.XInclude
{
	public class XIncludingReader : XmlReader, IXmlLineInfo
	{
		//XInclude keywords
		private static IDictionary<string, WeakReference> _cache;
		private readonly XIncludeKeywords _keywords;
		private readonly XmlNameTable _nameTable;
		private readonly Stack<XmlReader> _readers;
		private readonly Uri _topBaseUri;
		private bool _differentLang;
		private FallbackState _fallbackState;
		private bool _gotElement;
		private bool _gotTopIncludedElem;
		private FallbackState _prevFallbackState;
		//Current reader
		private XmlReader _reader;
		//Stack of readers
		private int _realXmlBaseIndex = -1;
		private XIncludingReaderState _state;
		//Top base URI
		//Top-level included item flag
		private bool _topLevel;
		//A top-level included element has been included already
		//Whitespace handling
		//Emit relative xml:base URIs
		//XmlResolver to resolve URIs
		//Expose text inclusions as CDATA

		public XIncludingReader(XmlReader reader)
		{
			MakeRelativeBaseUri = true;
			var xtr = reader as XmlTextReader;
			if (xtr != null)
			{
				//#pragma warning disable 0618
				//XmlValidatingReader vr = new XmlValidatingReader(reader);
				//vr.ValidationType = ValidationType.None;
				//vr.EntityHandling = EntityHandling.ExpandEntities;
				//vr.ValidationEventHandler += new ValidationEventHandler(
				//    ValidationCallback);
				//_whiteSpaceHandling = xtr.WhitespaceHandling;
				//_reader = vr;                                
				var s = new XmlReaderSettings
				{
					DtdProcessing = DtdProcessing.Parse,
					ValidationType = ValidationType.None
				};
				s.ValidationEventHandler += ValidationCallback;
				if (xtr.WhitespaceHandling == WhitespaceHandling.Significant)
				{
					s.IgnoreWhitespace = true;
				}
				_reader = Create(reader, s);
				//#pragma warning restore 0618
			}
			else
			{
				_reader = reader;
			}

			_nameTable = reader.NameTable;
			_keywords = new XIncludeKeywords(_nameTable);

			if (_reader.BaseURI != "")
			{
				if (_reader.BaseURI != null)
				{
					_topBaseUri = new Uri(_reader.BaseURI);
				}
			}
			else
			{
				MakeRelativeBaseUri = false;
				_topBaseUri = new Uri(Assembly.GetExecutingAssembly().Location);
			}
			_readers = new Stack<XmlReader>();
			_state = XIncludingReaderState.Default;
		}

		public XIncludingReader(string url)
			: this(new XmlBaseAwareXmlReader(url)) {}

		public XIncludingReader(string url, XmlResolver resolver)
			: this(new XmlBaseAwareXmlReader(url, resolver)) {}

		public XIncludingReader(string url, XmlNameTable nt)
			: this(new XmlBaseAwareXmlReader(url, nt)) {}

		public XIncludingReader(TextReader reader)
			: this(new XmlBaseAwareXmlReader(reader)) {}

		public XIncludingReader(string url, TextReader reader)
			: this(new XmlBaseAwareXmlReader(url, reader)) {}

		public XIncludingReader(TextReader reader, XmlNameTable nt)
			: this(new XmlBaseAwareXmlReader(reader, nt)) {}

		public XIncludingReader(string url, TextReader reader, XmlNameTable nt)
			: this(new XmlBaseAwareXmlReader(url, reader, nt)) {}

		public XIncludingReader(Stream input)
			: this(new XmlBaseAwareXmlReader(input)) {}

		public XIncludingReader(string url, Stream input)
			: this(new XmlBaseAwareXmlReader(url, input)) {}

		public XIncludingReader(string url, Stream input, XmlResolver resolver)
			: this(new XmlBaseAwareXmlReader(url, input, resolver)) {}

		public XIncludingReader(Stream input, XmlNameTable nt)
			: this(new XmlBaseAwareXmlReader(input, nt)) {}

		public XIncludingReader(string url, Stream input, XmlNameTable nt)
			: this(new XmlBaseAwareXmlReader(url, input, nt)) {}

		public override int AttributeCount
		{
			get
			{
				if (_topLevel)
				{
					var ac = _reader.AttributeCount;
					if (_reader.GetAttribute(_keywords.XmlBase) == null)
					{
						ac++;
					}
					if (_differentLang)
					{
						ac++;
					}
					return ac;
				}
				return _reader.AttributeCount;
			}
		}

		public override string BaseURI
		{
			get { return _reader.BaseURI; }
		}

		public override bool HasValue
		{
			get
			{
				if (_state == XIncludingReaderState.Default)
				{
					return _reader.HasValue;
				}
				return true;
			}
		}

		public override bool IsDefault
		{
			get
			{
				if (_state == XIncludingReaderState.Default)
				{
					return _reader.IsDefault;
				}
				return false;
			}
		}

		public override string Name
		{
			get
			{
				switch (_state)
				{
					case XIncludingReaderState.ExposingXmlBaseAttr:
						return _keywords.XmlBase;
					case XIncludingReaderState.ExposingXmlBaseAttrValue:
					case XIncludingReaderState.ExposingXmlLangAttrValue:
						return String.Empty;
					case XIncludingReaderState.ExposingXmlLangAttr:
						return _keywords.XmlLang;
					default:
						return _reader.Name;
				}
			}
		}

		public override string LocalName
		{
			get
			{
				switch (_state)
				{
					case XIncludingReaderState.ExposingXmlBaseAttr:
						return _keywords.Base;
					case XIncludingReaderState.ExposingXmlBaseAttrValue:
					case XIncludingReaderState.ExposingXmlLangAttrValue:
						return String.Empty;
					case XIncludingReaderState.ExposingXmlLangAttr:
						return _keywords.Lang;
					default:
						return _reader.LocalName;
				}
			}
		}

		public override string NamespaceURI
		{
			get
			{
				switch (_state)
				{
					case XIncludingReaderState.ExposingXmlBaseAttr:
					case XIncludingReaderState.ExposingXmlLangAttr:
						return _keywords.XmlNamespace;
					case XIncludingReaderState.ExposingXmlBaseAttrValue:
					case XIncludingReaderState.ExposingXmlLangAttrValue:
						return String.Empty;
					default:
						return _reader.NamespaceURI;
				}
			}
		}

		public override XmlNameTable NameTable
		{
			get { return _nameTable; }
		}

		public override XmlNodeType NodeType
		{
			get
			{
				switch (_state)
				{
					case XIncludingReaderState.ExposingXmlBaseAttr:
					case XIncludingReaderState.ExposingXmlLangAttr:
						return XmlNodeType.Attribute;
					case XIncludingReaderState.ExposingXmlBaseAttrValue:
					case XIncludingReaderState.ExposingXmlLangAttrValue:
						return XmlNodeType.Text;
					default:
						return _reader.NodeType;
				}
			}
		}

		public override string Prefix
		{
			get
			{
				switch (_state)
				{
					case XIncludingReaderState.ExposingXmlBaseAttr:
					case XIncludingReaderState.ExposingXmlLangAttr:
						return _keywords.Xml;
					case XIncludingReaderState.ExposingXmlBaseAttrValue:
					case XIncludingReaderState.ExposingXmlLangAttrValue:
						return String.Empty;
					default:
						return _reader.Prefix;
				}
			}
		}

		public override char QuoteChar
		{
			get
			{
				switch (_state)
				{
					case XIncludingReaderState.ExposingXmlBaseAttr:
					case XIncludingReaderState.ExposingXmlLangAttr:
						return '"';
					default:
						return _reader.QuoteChar;
				}
			}
		}

		public override int Depth
		{
			get
			{
				if (_readers.Count == 0)
				{
					return _reader.Depth;
				}

				return _readers.Peek().Depth + _reader.Depth;
			}
		}

		public override bool EOF
		{
			get { return _reader.EOF; }
		}

		public override bool IsEmptyElement
		{
			get { return _reader.IsEmptyElement; }
		}

		public override ReadState ReadState
		{
			get { return _reader.ReadState; }
		}

		public override String this[int i]
		{
			get { return GetAttribute(i); }
		}

		public override string this[string name]
		{
			get { return GetAttribute(name); }
		}

		public override string this[string name, string namespaceUri]
		{
			get { return GetAttribute(name, namespaceUri); }
		}

		public override string XmlLang
		{
			get { return _reader.XmlLang; }
		}

		public override XmlSpace XmlSpace
		{
			get { return _reader.XmlSpace; }
		}

		public override string Value
		{
			get
			{
				switch (_state)
				{
					case XIncludingReaderState.ExposingXmlBaseAttr:
					case XIncludingReaderState.ExposingXmlBaseAttrValue:
						return GetBaseUri();
					case XIncludingReaderState.ExposingXmlLangAttr:
					case XIncludingReaderState.ExposingXmlLangAttrValue:
						return _reader.XmlLang;
					default:
						return _reader.Value;
				}
			}
		}

		public WhitespaceHandling WhitespaceHandling { get; set; }

		public XmlResolver XmlResolver { private get; set; }

		public Encoding Encoding
		{
			get
			{
				var xtr = _reader as XmlTextReader;
				if (xtr != null)
				{
					return xtr.Encoding;
				}
				var xir = _reader as XIncludingReader;
				if (xir != null)
				{
					return xir.Encoding;
				}
				return null;
			}
		}

		public bool MakeRelativeBaseUri { get; set; }

		public bool ExposeTextInclusionsAsCData { get; set; }

		public bool HasLineInfo()
		{
			var core = _reader as IXmlLineInfo;
			return core != null && core.HasLineInfo();
		}

		public int LineNumber
		{
			get
			{
				var core = _reader as IXmlLineInfo;
				return core != null ? core.LineNumber : 0;
			}
		}

		public int LinePosition
		{
			get
			{
				var core = _reader as IXmlLineInfo;
				return core != null ? core.LinePosition : 0;
			}
		}

		private static bool AreDifferentLangs(string lang1, string lang2)
		{
			return !String.Equals(lang1, lang2, StringComparison.CurrentCultureIgnoreCase);
		}

		private void CheckAndSkipContent()
		{
			var depth = _reader.Depth;
			var fallbackElem = false;
			while (_reader.Read() && depth < _reader.Depth)
			{
				switch (_reader.NodeType)
				{
					case XmlNodeType.Element:
						if (IsIncludeElement())
						{
							//xi:include child of xi:include - fatal error
							var li = _reader as IXmlLineInfo;

							if (li == null || !li.HasLineInfo())
							{
								throw new XIncludeSyntaxError(String.Format(
									CultureInfo.CurrentCulture,
									Resources.IncludeChildOfInclude,
									_reader.BaseURI));
							}

							if (_reader.BaseURI != null)
							{
								throw new XIncludeSyntaxError(String.Format(
									CultureInfo.CurrentCulture,
									Resources.IncludeChildOfIncludeLong,
									_reader.BaseURI,
									li.LineNumber, li.LinePosition));
							}

							throw new XIncludeSyntaxError(String.Format(
								CultureInfo.CurrentCulture,
								Resources.IncludeChildOfInclude,
								_reader.BaseURI));
						}

						if (IsFallbackElement())
						{
							//Found xi:fallback
							if (fallbackElem)
							{
								//More than one xi:fallback
								var li = _reader as IXmlLineInfo;
								if (li != null && li.HasLineInfo())
								{
									throw new XIncludeSyntaxError(String.Format(
										CultureInfo.CurrentCulture,
										Resources.TwoFallbacksLong,
										_reader.BaseURI,
										li.LineNumber, li.LinePosition));
								}
								throw new XIncludeSyntaxError(String.Format(
									CultureInfo.CurrentCulture, Resources.TwoFallbacks, _reader.BaseURI));
							}

							fallbackElem = true;
							SkipContent();
						}
							//Check anything else in XInclude namespace
						else if (XIncludeKeywords.Equals(_reader.NamespaceURI, _keywords.XIncludeNamespace))
						{
							throw new XIncludeSyntaxError(String.Format(
								CultureInfo.CurrentCulture, Resources.UnknownXIncludeElement, _reader.Name));
						}
						else
						{
							//Ignore everything else
							SkipContent();
						}
						break;
				}
			}
		}

		private void CheckLoops(Uri url)
		{
			//Check circular inclusion  
			if (_reader.BaseURI == null)
			{
				return;
			}

			var baseUri = _reader.BaseURI == "" ? _topBaseUri : new Uri(_reader.BaseURI);

			if (baseUri.Equals(url))
			{
				ThrowCircularInclusionError(_reader, url);
			}

			foreach (var r in _readers)
			{
				if (r.BaseURI != null)
				{
					baseUri = r.BaseURI == "" ? _topBaseUri : new Uri(r.BaseURI);
				}

				if (baseUri.Equals(url))
				{
					ThrowCircularInclusionError(_reader, url);
				}
			}
		}

		public override void Close()
		{
			if (_reader != null)
			{
				_reader.Close();
			}
			//Close all readers in the stack
			while (_readers.Count > 0)
			{
				_reader = _readers.Pop();
				if (_reader != null)
				{
					_reader.Close();
				}
			}
		}

		private string CreateAcquiredInfoset(Uri includeLocation)
		{
			if (_cache == null)
			{
				_cache = new Dictionary<string, WeakReference>();
			}
			WeakReference wr;
			if (_cache.TryGetValue(includeLocation.AbsoluteUri, out wr) && wr.IsAlive)
			{
				return (string) wr.Target;
			}
			//Not cached or GCollected
			WebResponse wRes;
			string content;
			using (var stream = GetResource(includeLocation.AbsoluteUri,
				_reader.GetAttribute(_keywords.Accept),
				_reader.GetAttribute(_keywords.AcceptLanguage), out wRes))
			{
				using (var xir = new XIncludingReader(wRes.ResponseUri.AbsoluteUri, stream, _nameTable))
				{
					xir.WhitespaceHandling = WhitespaceHandling;

					using (var sw = new StringWriter())
					{
						using (var w = new XmlTextWriter(sw))
						{
							try
							{
								while (xir.Read())
								{
									w.WriteNode(xir, false);
								}
							}
							finally
							{
								xir.Close();
								w.Close();
							}

							content = sw.ToString();
						}
					}
				}
			}

			lock (_cache)
			{
				if (!_cache.ContainsKey(includeLocation.AbsoluteUri))
				{
					_cache.Add(includeLocation.AbsoluteUri, new WeakReference(content));
				}
			}

			return content;
		}

		private string CreateAcquiredInfoset(Uri includeLocation, TextReader reader)
		{
			return CreateAcquiredInfoset(
				new XmlBaseAwareXmlReader(includeLocation.AbsoluteUri, reader, _nameTable));
		}

		private string CreateAcquiredInfoset(XmlReader reader)
		{
			var xir = new XIncludingReader(reader)
			{
				XmlResolver = XmlResolver
			};
			var sw = new StringWriter();
			var w = new XmlTextWriter(sw);
			try
			{
				while (xir.Read())
				{
					w.WriteNode(xir, false);
				}
			}
			finally
			{
				xir.Close();
				w.Close();
			}

			return sw.ToString();
		}

		public override string GetAttribute(int i)
		{
			if (!_topLevel)
			{
				return _reader.GetAttribute(i);
			}

			var ac = _reader.AttributeCount;
			if (i < ac)
			{
				return i == _realXmlBaseIndex ? GetBaseUri() : _reader.GetAttribute(i);
				//case 2: it's real attribute and it's not xml:base
			}
			return i == ac ? GetBaseUri() : _reader.XmlLang;
			//case 4: it's virtual xml:lang - it comes last
		}

		public override string GetAttribute(string name)
		{
			if (!_topLevel)
			{
				return _reader.GetAttribute(name);
			}

			if (XIncludeKeywords.Equals(name, _keywords.XmlBase))
			{
				return GetBaseUri();
			}

			return XIncludeKeywords.Equals(name, _keywords.XmlLang) ? _reader.XmlLang : _reader.GetAttribute(name);
		}

		public override string GetAttribute(string name, string namespaceUri)
		{
			if (!_topLevel)
			{
				return _reader.GetAttribute(name, namespaceUri);
			}

			if (XIncludeKeywords.Equals(name, _keywords.Base) &&
				XIncludeKeywords.Equals(namespaceUri, _keywords.XmlNamespace))
			{
				return GetBaseUri();
			}

			if (XIncludeKeywords.Equals(name, _keywords.Lang) &&
				XIncludeKeywords.Equals(namespaceUri, _keywords.XmlNamespace))
			{
				return _reader.XmlLang;
			}

			return _reader.GetAttribute(name, namespaceUri);
		}

		private string GetBaseUri()
		{
			if (_reader.BaseURI == String.Empty)
			{
				return String.Empty;
			}

			if (!MakeRelativeBaseUri)
			{
				return _reader.BaseURI;
			}

			if (_reader.BaseURI == null)
			{
				return "";
			}

			var baseUri = new Uri(_reader.BaseURI);
			return _topBaseUri.MakeRelativeUri(baseUri).ToString();
		}

		internal static Stream GetResource(string includeLocation,
			string accept, string acceptLanguage, out WebResponse response)
		{
			WebRequest wReq;
			try
			{
				wReq = WebRequest.Create(includeLocation);
			}
			catch (NotSupportedException nse)
			{
				throw new ResourceException(String.Format(CultureInfo.CurrentCulture, Resources.URISchemaNotSupported, includeLocation), nse);
			}
			catch (SecurityException se)
			{
				throw new ResourceException(String.Format(CultureInfo.CurrentCulture, Resources.SecurityException, includeLocation), se);
			}
			//Add accept headers if this is HTTP request
			var httpReq = wReq as HttpWebRequest;
			if (httpReq != null)
			{
				if (accept != null)
				{
					TextUtils.CheckAcceptValue(accept);
					if (string.IsNullOrEmpty(httpReq.Accept))
					{
						httpReq.Accept = accept;
					}
					else
					{
						httpReq.Accept += "," + accept;
					}
				}
				if (acceptLanguage != null)
				{
					if (httpReq.Headers["Accept-Language"] == null)
					{
						httpReq.Headers.Add("Accept-Language", acceptLanguage);
					}
					else
					{
						httpReq.Headers["Accept-Language"] += "," + acceptLanguage;
					}
				}
			}
			try
			{
				response = wReq.GetResponse();
			}
			catch (WebException we)
			{
				throw new ResourceException(String.Format(CultureInfo.CurrentCulture, Resources.ResourceError, includeLocation), we);
			}
			return response.GetResponseStream();
		}

		private bool IsFallbackElement()
		{
			return (
				XIncludeKeywords.Equals(_reader.NamespaceURI, _keywords.XIncludeNamespace) ||
					XIncludeKeywords.Equals(_reader.NamespaceURI, _keywords.OldXIncludeNamespace)
				) &&
				XIncludeKeywords.Equals(_reader.LocalName, _keywords.Fallback);
		}

		private bool IsIncludeElement()
		{
			return (
				XIncludeKeywords.Equals(_reader.NamespaceURI, _keywords.XIncludeNamespace) ||
					XIncludeKeywords.Equals(_reader.NamespaceURI, _keywords.OldXIncludeNamespace)
				) &&
				XIncludeKeywords.Equals(_reader.LocalName, _keywords.Include);
		}

		public override String LookupNamespace(String prefix)
		{
			return _reader.LookupNamespace(prefix);
		}

		public override void MoveToAttribute(int i)
		{
			if (_topLevel)
			{
				if (i >= _reader.AttributeCount || i == _realXmlBaseIndex)
				{
					if (i > _reader.AttributeCount && _differentLang)
					{
						_state = XIncludingReaderState.ExposingXmlLangAttr;
					}
					else
					{
						_state = XIncludingReaderState.ExposingXmlBaseAttr;
					}
				}
				else
				{
					_state = XIncludingReaderState.Default;
					_reader.MoveToAttribute(i);
				}
			}
			else
			{
				_reader.MoveToAttribute(i);
			}
		}

		public override bool MoveToAttribute(string name)
		{
			if (_topLevel)
			{
				if (XIncludeKeywords.Equals(name, _keywords.XmlBase))
				{
					_state = XIncludingReaderState.ExposingXmlBaseAttr;
					return true;
				}
				if (XIncludeKeywords.Equals(name, _keywords.XmlLang))
				{
					_state = XIncludingReaderState.ExposingXmlLangAttr;
					return true;
				}
			}
			return _reader.MoveToAttribute(name);
		}

		public override bool MoveToAttribute(string name, string ns)
		{
			if (_topLevel)
			{
				if (XIncludeKeywords.Equals(name, _keywords.Base) &&
					XIncludeKeywords.Equals(ns, _keywords.XmlNamespace))
				{
					_state = XIncludingReaderState.ExposingXmlBaseAttr;
					return true;
				}
				if (XIncludeKeywords.Equals(name, _keywords.Lang) &&
					XIncludeKeywords.Equals(ns, _keywords.XmlNamespace))
				{
					_state = XIncludingReaderState.ExposingXmlLangAttr;
					return true;
				}
			}
			return _reader.MoveToAttribute(name, ns);
		}

		public override bool MoveToElement()
		{
			return _reader.MoveToElement();
		}

		public override bool MoveToFirstAttribute()
		{
			if (!_topLevel)
			{
				return _reader.MoveToFirstAttribute();
			}

			if (_reader.MoveToFirstAttribute())
			{
				//it might be xml:base or xml:lang
				if (_reader.Name == _keywords.XmlBase ||
					_reader.Name == _keywords.XmlLang)
				{
					//omit them - we expose virtual ones at the end
					return MoveToNextAttribute();
				}

				return true;
			}

			//No attrs? Expose xml:base
			_state = XIncludingReaderState.ExposingXmlBaseAttr;
			return true;
		}

		public override bool MoveToNextAttribute()
		{
			if (!_topLevel)
			{
				return _reader.MoveToNextAttribute();
			}

			switch (_state)
			{
				case XIncludingReaderState.ExposingXmlBaseAttr:
				case XIncludingReaderState.ExposingXmlBaseAttrValue:
					//Exposing xml:base already - switch to xml:lang                                                                            
					if (_differentLang)
					{
						_state = XIncludingReaderState.ExposingXmlLangAttr;
						return true;
					}
					//No need for xml:lang, stop
					_state = XIncludingReaderState.Default;
					return false;
				case XIncludingReaderState.ExposingXmlLangAttr:
				case XIncludingReaderState.ExposingXmlLangAttrValue:
					//Exposing xml:lang already - that's a last one
					_state = XIncludingReaderState.Default;
					return false;
				default:
					//1+ attrs, default mode
					if (_reader.MoveToNextAttribute())
					{
						//Still real attributes - it might be xml:base or xml:lang
						if (_reader.Name == _keywords.XmlBase ||
							_reader.Name == _keywords.XmlLang)
						{
							//omit them - we expose virtual ones at the end
							return MoveToNextAttribute();
						}
						return true;
					}
					//No more attrs - expose virtual xml:base                                
					_state = XIncludingReaderState.ExposingXmlBaseAttr;
					return true;
			}
		}

		private bool ProcessFallback(int depth, Exception e)
		{
			//Read to the xi:include end tag
			while (_reader.Read() && depth < _reader.Depth)
			{
				switch (_reader.NodeType)
				{
					case XmlNodeType.Element:
						if (IsIncludeElement())
						{
							//xi:include child of xi:include - fatal error
							var li = _reader as IXmlLineInfo;
							if (li != null && li.HasLineInfo())
							{
								throw new XIncludeSyntaxError(String.Format(
									CultureInfo.CurrentCulture,
									Resources.IncludeChildOfIncludeLong,
									BaseURI,
									li.LineNumber, li.LinePosition));
							}
							throw new XIncludeSyntaxError(String.Format(
								CultureInfo.CurrentCulture,
								Resources.IncludeChildOfInclude,
								BaseURI));
						}
						if (IsFallbackElement())
						{
							//Found xi:fallback
							if (_fallbackState.FallbackProcessed)
							{
								var li = _reader as IXmlLineInfo;
								if (li != null && li.HasLineInfo())
								{
									//Two xi:fallback                                 
									throw new XIncludeSyntaxError(String.Format(
										CultureInfo.CurrentCulture,
										Resources.TwoFallbacksLong,
										BaseURI,
										li.LineNumber, li.LinePosition));
								}
								throw new XIncludeSyntaxError(String.Format(
									CultureInfo.CurrentCulture, Resources.TwoFallbacks, BaseURI));
							}
							if (_reader.IsEmptyElement)
							{
								//Empty xi:fallback - nothing to include
								_fallbackState.FallbackProcessed = true;
								break;
							}
							_fallbackState.Fallbacking = true;
							_fallbackState.FallbackDepth = _reader.Depth;
							return Read();
						}
						//Ignore anything else along with its content
						SkipContent();
						break;
				}
			}
			//xi:include content is read
			if (!_fallbackState.FallbackProcessed)
			{
				//No xi:fallback, fatal error
				throw new FatalResourceException(e);
			}
			//End of xi:include content processing, reset and go forth
			_fallbackState = _prevFallbackState;
			return Read();
		}

		private bool ProcessIncludeElement()
		{
			var href = _reader.GetAttribute(_keywords.Href);
			var xpointer = _reader.GetAttribute(_keywords.Xpointer);
			var parse = _reader.GetAttribute(_keywords.Parse);

			if (string.IsNullOrEmpty(href))
			{
				//Intra-document inclusion                                                
				if (parse == null || parse.Equals(_keywords.Xml))
				{
					if (xpointer != null)
					{
						throw new InvalidOperationException(Resources.IntradocumentReferencesNotSupported);
					}

					//Both href and xpointer attributes are absent in xml mode, 
					// => critical error
					var li = _reader as IXmlLineInfo;
					if (li != null && li.HasLineInfo())
					{
						throw new XIncludeSyntaxError(String.Format(
							CultureInfo.CurrentCulture,
							Resources.MissingHrefAndXpointerExceptionLong,
							_reader.BaseURI,
							li.LineNumber, li.LinePosition));
					}
					throw new XIncludeSyntaxError(String.Format(
						CultureInfo.CurrentCulture,
						Resources.MissingHrefAndXpointerException,
						_reader.BaseURI));
					//No support for intra-document refs                    
				}
				if (parse.Equals(_keywords.Text))
				{
					//No support for intra-document refs                    
					throw new InvalidOperationException(Resources.IntradocumentReferencesNotSupported);
				}
			}
			else
			{
				//Inter-document inclusion
				if (parse == null || parse.Equals(_keywords.Xml))
				{
					return ProcessInterDocXmlInclusion(href, xpointer);
				}
				if (parse.Equals(_keywords.Text))
				{
					return ProcessInterDocTextInclusion(href);
				}
			}

			//Unknown "parse" attribute value, critical error
			var li2 = _reader as IXmlLineInfo;
			if (li2 != null && li2.HasLineInfo())
			{
				throw new XIncludeSyntaxError(String.Format(
					CultureInfo.CurrentCulture,
					Resources.UnknownParseAttrValueLong,
					parse,
					_reader.BaseURI,
					li2.LineNumber, li2.LinePosition));
			}
			throw new XIncludeSyntaxError(String.Format(
				CultureInfo.CurrentCulture, Resources.UnknownParseAttrValue, parse));
		}

		private bool ProcessInterDocTextInclusion(string href)
		{
			//Include document as text                            
			var encoding = GetAttribute(_keywords.Encoding);
			var includeLocation = ResolveHref(href);
			//No need to check loops when including as text
			//Push current reader to the stack
			_readers.Push(_reader);
			_reader = new TextIncludingReader(includeLocation, encoding,
				_reader.GetAttribute(_keywords.Accept),
				_reader.GetAttribute(_keywords.AcceptLanguage),
				ExposeTextInclusionsAsCData);
			return Read();
		}

		private bool ProcessInterDocXmlInclusion(string href, string xpointer)
		{
			//Include document as XML                                
			var includeLocation = ResolveHref(href);
			if (includeLocation.Fragment != String.Empty)
			{
				throw new XIncludeSyntaxError(Resources.FragmentIDInHref);
			}
			CheckLoops(includeLocation);
			if (XmlResolver == null)
			{
				//No custom resolver
				if (xpointer != null)
				{
					//Push current reader to the stack
					_readers.Push(_reader);
					//XPointers should be resolved against the acquired infoset, 
					//not the source infoset                                                                                          
					_reader = new XPointerReader(includeLocation.AbsoluteUri,
						CreateAcquiredInfoset(includeLocation),
						xpointer);
				}
				else
				{
					WebResponse wRes;
					var stream = GetResource(includeLocation.AbsoluteUri,
						_reader.GetAttribute(_keywords.Accept),
						_reader.GetAttribute(_keywords.AcceptLanguage), out wRes);
					//Push current reader to the stack
					_readers.Push(_reader);
					/*var settings = new XmlReaderSettings
					{
						XmlResolver = XmlResolver,
						IgnoreWhitespace = (WhitespaceHandling == WhitespaceHandling.None)
					};*/
					XmlReader r = new XmlBaseAwareXmlReader(wRes.ResponseUri.AbsoluteUri, stream, _nameTable);
					_reader = r;
				}
				return Read();
			}
			//Custom resolver provided, let's ask him
			object resource;
			try
			{
				resource = XmlResolver.GetEntity(includeLocation, null, null);
			}
			catch (Exception e)
			{
				throw new ResourceException(Resources.CustomXmlResolverError, e);
			}
			if (resource == null)
			{
				throw new ResourceException(Resources.CustomXmlResolverReturnedNull);
			}

			//Push current reader to the stack
			_readers.Push(_reader);

			//Ok, we accept Stream, TextReader and XmlReader only                    
			if (resource is Stream)
			{
				resource = new StreamReader((Stream) resource);
			}
			if (xpointer != null)
			{
				var reader = resource as TextReader;
				if (reader != null)
				{
					//XPointers should be resolved against the acquired infoset, 
					//not the source infoset                                     
					_reader = new XPointerReader(includeLocation.AbsoluteUri,
						CreateAcquiredInfoset(includeLocation, reader),
						xpointer);
				}
				else
				{
					var xmlReader = resource as XmlReader;
					if (xmlReader != null)
					{
						var r = xmlReader;
						_reader = new XPointerReader(r.BaseURI,
							CreateAcquiredInfoset(r), xpointer);
					}
					else
					{
						//Unsupported type
						throw new ResourceException(String.Format(
							CultureInfo.CurrentCulture,
							Resources.CustomXmlResolverReturnedUnsupportedType,
							resource.GetType()));
					}
				}
			}
			else
			{
				//No XPointer   
				var reader = resource as TextReader;
				if (reader != null)
				{
					_reader = new XmlBaseAwareXmlReader(includeLocation.AbsoluteUri, reader, _nameTable);
				}
				else
				{
					var xmlReader = resource as XmlReader;
					if (xmlReader != null)
					{
						_reader = xmlReader;
					}
					else
					{
						//Unsupported type
						throw new ResourceException(String.Format(
							CultureInfo.CurrentCulture,
							Resources.CustomXmlResolverReturnedUnsupportedType,
							resource.GetType()));
					}
				}
			}

			return Read();
		}

		public override bool Read()
		{
			_state = XIncludingReaderState.Default;
			//Read internal reader
			var baseRead = _reader.Read();
			if (baseRead)
			{
				//If we are including and including reader is at 0 depth - 
				//we are at a top level included item
				_topLevel = (_readers.Count > 0 && _reader.Depth == 0);
				if (_topLevel && _reader.NodeType == XmlNodeType.Attribute)
				{
					//Attempt to include an attribute or namespace node
					throw new AttributeOrNamespaceInIncludeLocationError(Resources.AttributeOrNamespaceInIncludeLocationError);
				}

				if (_topLevel && _readers.Peek().Depth == 0 &&
					_reader.NodeType == XmlNodeType.Element)
				{
					if (_gotTopIncludedElem)
					{
						//Attempt to include more than one element at the top level
						throw new MalformedXInclusionResultError(Resources.MalformedXInclusionResult);
					}
					_gotTopIncludedElem = true;
				}
				if (_topLevel)
				{
					//Check if included item has different language
					_differentLang = AreDifferentLangs(_reader.XmlLang, _readers.Peek().XmlLang);
					if (_reader.NodeType == XmlNodeType.Element)
					{
						//Record real xml:base index
						_realXmlBaseIndex = -1;
						var i = 0;
						while (_reader.MoveToNextAttribute())
						{
							if (_reader.Name == _keywords.XmlBase)
							{
								_realXmlBaseIndex = i;
								break;
							}
							i++;
						}
						_reader.MoveToElement();
					}
				}
				switch (_reader.NodeType)
				{
					case XmlNodeType.XmlDeclaration:
					case XmlNodeType.Document:
					case XmlNodeType.DocumentType:
					case XmlNodeType.DocumentFragment:
						//This stuff should not be included into resulting infoset,
						//but should be inclused into acquired infoset                        
						return _readers.Count <= 0 || Read();
					case XmlNodeType.Element:
						//Check for xi:include
						if (IsIncludeElement())
						{
							//xi:include element found
							//Save current reader to possible fallback processing
							var current = _reader;
							try
							{
								return ProcessIncludeElement();
							}
							catch (FatalException)
							{
								throw;
							}
							catch (Exception e)
							{
								//Let's be liberal - any exceptions other than fatal one 
								//should be treated as resource error
								//Console.WriteLine("Resource error has been detected: " + e.Message);
								//Start fallback processing
								if (!current.Equals(_reader))
								{
									_reader.Close();
									_reader = current;
								}
								_prevFallbackState = _fallbackState;
								return ProcessFallback(_reader.Depth, e);
							}
							//No, it's not xi:include, check it for xi:fallback    
						}
						if (IsFallbackElement())
						{
							//Found xi:fallback not child of xi:include
							var li = _reader as IXmlLineInfo;
							if (li != null && li.HasLineInfo())
							{
								throw new XIncludeSyntaxError(String.Format(
									CultureInfo.CurrentCulture,
									Resources.FallbackNotChildOfIncludeLong,
									_reader.BaseURI, li.LineNumber,
									li.LinePosition));
							}
							throw new XIncludeSyntaxError(String.Format(
								CultureInfo.CurrentCulture,
								Resources.FallbackNotChildOfInclude,
								_reader.BaseURI));
						}
						_gotElement = true;
						goto default;
					case XmlNodeType.EndElement:
						//Looking for end of xi:fallback
						if (_fallbackState.Fallbacking &&
							_reader.Depth == _fallbackState.FallbackDepth &&
							IsFallbackElement())
						{
							//End of fallback processing
							_fallbackState.FallbackProcessed = true;
							//Now read other ignored content till </xi:fallback>
							return ProcessFallback(_reader.Depth - 1, null);
						}
						goto default;
					default:
						return true;
				}
			}
			//No more input - finish possible xi:include processing
			if (_topLevel)
			{
				_topLevel = false;
			}
			if (_readers.Count > 0)
			{
				_reader.Close();
				//Pop previous reader
				_reader = _readers.Pop();
				//Successful include - skip xi:include content
				if (!_reader.IsEmptyElement)
				{
					CheckAndSkipContent();
				}
				return Read();
			}
			if (!_gotElement)
			{
				throw new MalformedXInclusionResultError(Resources.MalformedXInclusionResult);
			}
			//That's all, folks
			return false;
		}

		public override bool ReadAttributeValue()
		{
			switch (_state)
			{
				case XIncludingReaderState.ExposingXmlBaseAttr:
					_state = XIncludingReaderState.ExposingXmlBaseAttrValue;
					return true;
				case XIncludingReaderState.ExposingXmlBaseAttrValue:
					return false;
				case XIncludingReaderState.ExposingXmlLangAttr:
					_state = XIncludingReaderState.ExposingXmlLangAttrValue;
					return true;
				case XIncludingReaderState.ExposingXmlLangAttrValue:
					return false;
				default:
					return _reader.ReadAttributeValue();
			}
		}

		public override string ReadInnerXml()
		{
			switch (_state)
			{
				case XIncludingReaderState.ExposingXmlBaseAttr:
					return GetBaseUri();
				case XIncludingReaderState.ExposingXmlBaseAttrValue:
					return String.Empty;
				case XIncludingReaderState.ExposingXmlLangAttr:
					return _reader.XmlLang;
				case XIncludingReaderState.ExposingXmlLangAttrValue:
					return String.Empty;
				default:
					if (NodeType == XmlNodeType.Element)
					{
						var depth = Depth;
						if (Read())
						{
							var sw = new StringWriter();
							var xw = new XmlTextWriter(sw);
							while (Depth > depth)
							{
								xw.WriteNode(this, false);
							}
							xw.Close();
							return sw.ToString();
						}
						return String.Empty;
					}
					if (NodeType == XmlNodeType.Attribute)
					{
						return Value;
					}
					return String.Empty;
			}
		}

		public override string ReadOuterXml()
		{
			switch (_state)
			{
				case XIncludingReaderState.ExposingXmlBaseAttr:
					return @"xml:base="" + _reader.BaseURI + @""";
				case XIncludingReaderState.ExposingXmlBaseAttrValue:
					return String.Empty;
				case XIncludingReaderState.ExposingXmlLangAttr:
					return @"xml:lang="" + _reader.XmlLang + @""";
				case XIncludingReaderState.ExposingXmlLangAttrValue:
					return String.Empty;
				default:
					if (NodeType == XmlNodeType.Element)
					{
						var sw = new StringWriter();
						var xw = new XmlTextWriter(sw);
						xw.WriteNode(this, false);
						xw.Close();
						return sw.ToString();
					}
					return NodeType == XmlNodeType.Attribute ? String.Format("{{0=\"{0}\"}}", Value) : String.Empty;
			}
		}

		public override string ReadString()
		{
			switch (_state)
			{
				case XIncludingReaderState.ExposingXmlBaseAttr:
					return String.Empty;
				case XIncludingReaderState.ExposingXmlBaseAttrValue:
					return GetBaseUri();
				case XIncludingReaderState.ExposingXmlLangAttr:
					return String.Empty;
				case XIncludingReaderState.ExposingXmlLangAttrValue:
					return _reader.XmlLang;
				default:
					return _reader.ReadString();
			}
		}

		public override void ResolveEntity()
		{
			_reader.ResolveEntity();
		}

		private Uri ResolveHref(string href)
		{
			Uri includeLocation = null;

			try
			{
				if (_reader.BaseURI != null)
				{
					var baseUri = _reader.BaseURI == "" ? _topBaseUri : new Uri(_reader.BaseURI);
					includeLocation = XmlResolver == null ? new Uri(baseUri, href) : XmlResolver.ResolveUri(baseUri, href);
				}
			}
			catch (UriFormatException ufe)
			{
				throw new ResourceException(String.Format(CultureInfo.CurrentCulture, Resources.InvalidURI, href), ufe);
			}
			catch (Exception e)
			{
				throw new ResourceException(String.Format(CultureInfo.CurrentCulture, Resources.UnresolvableURI, href), e);
			}

			return includeLocation;
		}

		private void SkipContent()
		{
			if (_reader.IsEmptyElement)
			{
				return;
			}

			var depth = _reader.Depth;
			while (_reader.Read() && depth < _reader.Depth) {}
		}

		private void ThrowCircularInclusionError(XmlReader reader, Uri url)
		{
			var li = reader as IXmlLineInfo;
			if (li != null && li.HasLineInfo())
			{
				throw new CircularInclusionException(url,
					BaseURI,
					li.LineNumber, li.LinePosition);
			}
			throw new CircularInclusionException(url);
		}

		private static void ValidationCallback(object sender, ValidationEventArgs args)
		{
			//do nothing
		}
	}
}
