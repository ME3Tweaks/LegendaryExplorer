using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Xml.Common;
using Gammtek.Conduit.Mvp.Xml.Common.XPath;

namespace Gammtek.Conduit.Mvp.Xml.XPointer
{
	public class XPointerReader : XmlReader, IHasXPathNavigator, IXmlLineInfo
	{
		//Underlying reader
		//Document cache
		private static IDictionary<string, WeakReference> _cache;
		private XPathNodeIterator _pointedNodes;
		private XmlReader _reader;

		public XPointerReader(IXPathNavigable doc, string xpointer)
			: this(doc.CreateNavigator(), xpointer) {}

		public XPointerReader(XPathNavigator nav, string xpointer)
		{
			Init(nav, xpointer);
		}

		public XPointerReader(string uri, string xpointer)
			: this(new XmlBaseAwareXmlReader(uri), xpointer) {}

		public XPointerReader(string uri, XmlNameTable nt, string xpointer)
			: this(new XmlBaseAwareXmlReader(uri, nt), xpointer) {}

		public XPointerReader(string uri, Stream stream, XmlNameTable nt, string xpointer)
			: this(new XmlBaseAwareXmlReader(uri, stream, nt), xpointer) {}

		public XPointerReader(string uri, Stream stream, string xpointer)
			: this(uri, stream, new NameTable(), xpointer) {}

		public XPointerReader(XmlReader reader, string xpointer)
		{
			XPathDocument doc;
			if (_cache == null)
			{
				_cache = new Dictionary<string, WeakReference>();
			}
			WeakReference wr;
			if (reader.BaseURI != null && (!string.IsNullOrEmpty(reader.BaseURI) &&
				_cache.TryGetValue(reader.BaseURI, out wr) &&
				wr.IsAlive))
			{
				doc = (XPathDocument) wr.Target;
				reader.Close();
			}
			else
			{
				//Not cached or GCollected or no base Uri                
				doc = CreateAndCacheDocument(reader);
			}
			Init(doc.CreateNavigator(), xpointer);
		}

		public XPointerReader(string uri, string content, string xpointer)
		{
			XPathDocument doc;
			if (_cache == null)
			{
				_cache = new Dictionary<string, WeakReference>();
			}
			WeakReference wr;
			if (_cache.TryGetValue(uri, out wr) && wr.IsAlive)
			{
				doc = (XPathDocument) wr.Target;
			}
			else
			{
				//Not cached or GCollected                        
				//XmlReader r = new XmlBaseAwareXmlReader(uri, new StringReader(content));
				var settings = new XmlReaderSettings
				{
					DtdProcessing = DtdProcessing.Parse
				};
				var r = Create(new StringReader(content), settings, uri);
				doc = CreateAndCacheDocument(r);
			}
			Init(doc.CreateNavigator(), xpointer);
		}

		public override int AttributeCount
		{
			get { return _reader.AttributeCount; }
		}

		public override string BaseURI
		{
			get { return _reader.BaseURI; }
		}

		public override bool HasValue
		{
			get { return _reader.HasValue; }
		}

		public override bool IsDefault
		{
			get { return _reader.IsDefault; }
		}

		public override string Name
		{
			get { return _reader.Name; }
		}

		public override string LocalName
		{
			get { return _reader.LocalName; }
		}

		public override string NamespaceURI
		{
			get { return _reader.NamespaceURI; }
		}

		public override XmlNameTable NameTable
		{
			get { return _reader.NameTable; }
		}

		public override XmlNodeType NodeType
		{
			get { return _reader.NodeType; }
		}

		public override string Prefix
		{
			get { return _reader.Prefix; }
		}

		public override char QuoteChar
		{
			get { return _reader.QuoteChar; }
		}

		public override int Depth
		{
			get { return _reader.Depth; }
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
			get { return _reader[i]; }
		}

		public override string this[string name]
		{
			get { return _reader[name]; }
		}

		public override string this[string name, string namespaceUri]
		{
			get { return _reader[name, namespaceUri]; }
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
			get { return _reader.Value; }
		}

		public XPathNavigator GetNavigator()
		{
			return _pointedNodes.Current.Clone();
		}

		public bool HasLineInfo()
		{
			var core = _reader as IXmlLineInfo;
			if (core != null)
			{
				return core.HasLineInfo();
			}
			return false;
		}

		public int LineNumber
		{
			get
			{
				var core = _reader as IXmlLineInfo;
				if (core != null)
				{
					return core.LineNumber;
				}
				return 0;
			}
		}

		public int LinePosition
		{
			get
			{
				var core = _reader as IXmlLineInfo;
				if (core != null)
				{
					return core.LinePosition;
				}
				return 0;
			}
		}

		public override void Close()
		{
			if (_reader != null)
			{
				_reader.Close();
			}
		}

		private XPathDocument CreateAndCacheDocument(XmlReader r)
		{
			var uri = r.BaseURI;
			var doc = new XPathDocument(r, XmlSpace.Preserve);
			r.Close();

			//Can't cache documents with empty base URI
			if (!string.IsNullOrEmpty(uri))
			{
				lock (_cache)
				{
					if (!_cache.ContainsKey(uri))
					{
						_cache.Add(uri, new WeakReference(doc));
					}
				}
			}
			return doc;
		}

		public override string GetAttribute(int i)
		{
			return _reader.GetAttribute(i);
		}

		public override string GetAttribute(string name)
		{
			return _reader.GetAttribute(name);
		}

		public override string GetAttribute(string name, string namespaceUri)
		{
			return _reader.GetAttribute(name, namespaceUri);
		}

		private void Init(XPathNavigator nav, string xpointer)
		{
			var pointer = XPointerParser.ParseXPointer(xpointer);
			_pointedNodes = pointer.Evaluate(nav);
			//There is always at least one identified node
			//XPathNodeIterator is already at the first node
			_reader = new SubtreeXPathNavigator(_pointedNodes.Current).ReadSubtree();
		}

		public override String LookupNamespace(String prefix)
		{
			return _reader.LookupNamespace(prefix);
		}

		public override void MoveToAttribute(int i)
		{
			_reader.MoveToAttribute(i);
		}

		public override bool MoveToAttribute(string name)
		{
			return _reader.MoveToAttribute(name);
		}

		public override bool MoveToAttribute(string name, string ns)
		{
			return _reader.MoveToAttribute(name, ns);
		}

		public override bool MoveToElement()
		{
			return _reader.MoveToElement();
		}

		public override bool MoveToFirstAttribute()
		{
			return _reader.MoveToFirstAttribute();
		}

		public override bool MoveToNextAttribute()
		{
			return _reader.MoveToNextAttribute();
		}

		public override bool Read()
		{
			var baseRead = _reader.Read();
			if (baseRead)
			{
				return true;
			}
			if (_pointedNodes != null)
			{
				if (_pointedNodes.MoveNext())
				{
					_reader = new SubtreeXPathNavigator(_pointedNodes.Current).ReadSubtree();
					return _reader.Read();
				}
			}
			return false;
		}

		public override bool ReadAttributeValue()
		{
			return _reader.ReadAttributeValue();
		}

		public override string ReadInnerXml()
		{
			return _reader.ReadInnerXml();
		}

		public override string ReadOuterXml()
		{
			return _reader.ReadOuterXml();
		}

		public override string ReadString()
		{
			return _reader.ReadString();
		}

		public override void ResolveEntity()
		{
			_reader.ResolveEntity();
		}
	}
}
