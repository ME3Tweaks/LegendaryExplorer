using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Common.XPath
{
	public class XPathIteratorReader : XmlTextReader, IXmlSerializable
	{
		// Holds the current child being read.
		private XmlReader _current;

		// Holds the iterator passed to the ctor. 
		private XPathNodeIterator _iterator;

		// The name for the root element.
		private XmlQualifiedName _rootname;

		[EditorBrowsable(EditorBrowsableState.Never)]
		public XPathIteratorReader() {}

		public XPathIteratorReader(XPathNodeIterator iterator)
			: this(iterator, "root", String.Empty) {}

		public XPathIteratorReader(XPathNodeIterator iterator, string rootName)
			: this(iterator, rootName, String.Empty) {}

		public XPathIteratorReader(XPathNodeIterator iterator, string rootName, string ns)
			: base(new StringReader(String.Empty))
		{
			_iterator = iterator.Clone();
			_current = new FakedRootReader(rootName, ns, XmlNodeType.Element);
			_rootname = new XmlQualifiedName(rootName, ns);
		}

		public override int AttributeCount
		{
			get { return _current.AttributeCount; }
		}

		public override string BaseURI
		{
			get { return _current.BaseURI; }
		}

		public override int Depth
		{
			get { return _current.Depth + 1; }
		}

		public override bool EOF
		{
			get { return _current.ReadState == ReadState.EndOfFile || _current.ReadState == ReadState.Closed; }
		}

		public override bool HasValue
		{
			get { return _current.HasValue; }
		}

		public override bool IsDefault
		{
			get { return false; }
		}

		public override bool IsEmptyElement
		{
			get { return _current.IsEmptyElement; }
		}

		public override string this[string name, string ns]
		{
			get { return _current[name, ns]; }
		}

		public override string this[string name]
		{
			get { return _current[name, String.Empty]; }
		}

		public override string this[int i]
		{
			get { return _current[i]; }
		}

		public override string LocalName
		{
			get { return _current.LocalName; }
		}

		public override string Name
		{
			get { return _current.Name; }
		}

		public override string NamespaceURI
		{
			get { return _current.NamespaceURI; }
		}

		public override XmlNameTable NameTable
		{
			get { return _current.NameTable; }
		}

		public override XmlNodeType NodeType
		{
			get { return _current.NodeType; }
		}

		public override string Prefix
		{
			get { return _current.Prefix; }
		}

		public override char QuoteChar
		{
			get { return _current.QuoteChar; }
		}

		public override ReadState ReadState
		{
			get { return _current.ReadState; }
		}

		public override string Value
		{
			get { return _current.Value; }
		}

		public override string XmlLang
		{
			get { return _current.XmlLang; }
		}

		public override XmlSpace XmlSpace
		{
			get { return XmlSpace.Default; }
		}

		public void WriteXml(XmlWriter writer)
		{
			writer.WriteNode(this, false);
		}

		public XmlSchema GetSchema()
		{
			return null;
		}

		public void ReadXml(XmlReader reader)
		{
			var doc = new XPathDocument(reader);
			var nav = doc.CreateNavigator();

			// Pull the faked root out.
			nav.MoveToFirstChild();
			_rootname = new XmlQualifiedName(nav.LocalName, nav.NamespaceURI);

			// Get iterator for all child nodes.
			_iterator = nav.SelectChildren(XPathNodeType.All);
		}

		public override void Close()
		{
			_current.Close();
		}

		public override string GetAttribute(string name, string ns)
		{
			if (name == null)
			{
				throw new ArgumentNullException("name");
			}

			if (ns == null)
			{
				throw new ArgumentNullException("ns");
			}

			return _current.GetAttribute(name, ns);
		}

		public override string GetAttribute(string name)
		{
			if (name == null)
			{
				throw new ArgumentNullException("name");
			}

			return _current.GetAttribute(name);
		}

		public override string GetAttribute(int i)
		{
			return _current.GetAttribute(i);
		}

		public override string LookupNamespace(string prefix)
		{
			if (prefix == null)
			{
				throw new ArgumentNullException("prefix");
			}

			return _current.LookupNamespace(prefix);
		}

		public override bool MoveToAttribute(string name, string ns)
		{
			if (name == null)
			{
				throw new ArgumentNullException("name");
			}

			if (ns == null)
			{
				throw new ArgumentNullException("ns");
			}

			return _current.MoveToAttribute(name, ns);
		}

		public override bool MoveToAttribute(string name)
		{
			if (name == null)
			{
				throw new ArgumentNullException("name");
			}

			return _current.MoveToAttribute(name);
		}

		public override void MoveToAttribute(int i)
		{
			_current.MoveToAttribute(i);
		}

		public override bool MoveToElement()
		{
			return _current.MoveToElement();
		}

		public override bool MoveToFirstAttribute()
		{
			return _current.MoveToFirstAttribute();
		}

		public override bool MoveToNextAttribute()
		{
			return _current.MoveToNextAttribute();
		}

		public override bool Read()
		{
			// Return fast if state is no appropriate.
			if (_current.ReadState == ReadState.Closed || _current.ReadState == ReadState.EndOfFile)
			{
				return false;
			}

			var read = _current.Read();

			if (read)
			{
				return true;
			}

			read = _iterator.MoveNext();

			if (read)
			{
				// Just move to the next node and create the reader.
				_current = new SubtreeXPathNavigator(_iterator.Current).ReadSubtree();
				return _current.Read();
			}

			if (_current is FakedRootReader && _current.NodeType == XmlNodeType.EndElement)
			{
				// We're done!
				return false;
			}

			// We read all nodes in the iterator. Return to faked root end element.
			_current = new FakedRootReader(_rootname.Name, _rootname.Namespace, XmlNodeType.EndElement);
			return true;
		}

		public override bool ReadAttributeValue()
		{
			return _current.ReadAttributeValue();
		}

		public override string ReadInnerXml()
		{
			return Read() ? Serialize() : String.Empty;
		}

		public override string ReadOuterXml()
		{
			return _current.ReadState != ReadState.Interactive ? String.Empty : Serialize();
		}

		public override void ResolveEntity()
		{
			// Not supported.
		}

		private string Serialize()
		{
			var sw = new StringWriter(CultureInfo.CurrentCulture);
			var tw = new XmlTextWriter(sw);
			tw.WriteNode(this, false);

			sw.Flush();
			return sw.ToString();
		}

		private class FakedRootReader : XmlReader
		{
			private readonly string _name;
			private readonly string _namespace;
			private readonly XmlNodeType _nodetype;
			private ReadState _state;

			public FakedRootReader(string name, string ns, XmlNodeType nodeType)
			{
				_name = name;
				_namespace = ns;
				_nodetype = nodeType;
				_state = nodeType == XmlNodeType.Element
					? ReadState.Initial
					: ReadState.Interactive;
			}

			public override int AttributeCount
			{
				get { return 0; }
			}

			public override string BaseURI
			{
				get { return String.Empty; }
			}

			public override int Depth
			{
				// Undo the depth increment of the outer reader.
				get { return -1; }
			}

			public override bool EOF
			{
				get { return _state == ReadState.EndOfFile; }
			}

			public override bool HasValue
			{
				get { return false; }
			}

			public override bool IsDefault
			{
				get { return false; }
			}

			public override bool IsEmptyElement
			{
				get { return false; }
			}

			public override string this[string name, string ns]
			{
				get { return null; }
			}

			public override string this[string name]
			{
				get { return null; }
			}

			public override string this[int i]
			{
				get { return null; }
			}

			public override string LocalName
			{
				get { return _name; }
			}

			public override string Name
			{
				get { return _name; }
			}

			public override string NamespaceURI
			{
				get { return _namespace; }
			}

			public override XmlNameTable NameTable
			{
				get { return null; }
			}

			public override XmlNodeType NodeType
			{
				get { return _state == ReadState.Initial ? XmlNodeType.None : _nodetype; }
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
				get { return String.Empty; }
			}

			public override string XmlLang
			{
				get { return String.Empty; }
			}

			public override XmlSpace XmlSpace
			{
				get { return XmlSpace.Default; }
			}

			public override void Close()
			{
				_state = ReadState.Closed;
			}

			public override string GetAttribute(string name, string ns)
			{
				return null;
			}

			public override string GetAttribute(string name)
			{
				return null;
			}

			public override string GetAttribute(int i)
			{
				return null;
			}

			public override string LookupNamespace(string prefix)
			{
				return null;
			}

			public override bool MoveToAttribute(string name, string ns)
			{
				return false;
			}

			public override bool MoveToAttribute(string name)
			{
				return false;
			}

			public override void MoveToAttribute(int i) {}

			public override XmlNodeType MoveToContent()
			{
				if (_state == ReadState.Initial)
				{
					_state = ReadState.Interactive;
				}
				return _nodetype;
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
				if (_state == ReadState.Initial)
				{
					_state = ReadState.Interactive;
					return true;
				}
				if (_state == ReadState.Interactive && _nodetype == XmlNodeType.EndElement)
				{
					_state = ReadState.EndOfFile;
					return false;
				}

				return false;
			}

			public override bool ReadAttributeValue()
			{
				return false;
			}

			public override string ReadInnerXml()
			{
				return String.Empty;
			}

			public override string ReadOuterXml()
			{
				return String.Empty;
			}

			public override void ResolveEntity()
			{
				// Not supported.
			}
		}
	}
}
