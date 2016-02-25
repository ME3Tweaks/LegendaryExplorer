using System;
using System.Xml;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	public abstract class XmlWrappingReader : XmlReader, IXmlLineInfo
	{
		private XmlReader _baseReader;

		protected XmlWrappingReader(XmlReader baseReader)
		{
			Guard.ArgumentNotNull(baseReader, "baseReader");

			_baseReader = baseReader;
		}

		protected XmlReader BaseReader
		{
			get { return _baseReader; }
			set
			{
				Guard.ArgumentNotNull(value, "value");
				_baseReader = value;
			}
		}

		public override bool CanReadBinaryContent
		{
			get { return _baseReader.CanReadBinaryContent; }
		}

		public override bool CanReadValueChunk
		{
			get { return _baseReader.CanReadValueChunk; }
		}

		public override bool CanResolveEntity
		{
			get { return _baseReader.CanResolveEntity; }
		}

		public override int AttributeCount
		{
			get { return _baseReader.AttributeCount; }
		}

		public override string BaseURI
		{
			get { return _baseReader.BaseURI; }
		}

		public override int Depth
		{
			get { return _baseReader.Depth; }
		}

		public override bool EOF
		{
			get { return _baseReader.EOF; }
		}

		public override bool HasValue
		{
			get { return _baseReader.HasValue; }
		}

		public override bool IsDefault
		{
			get { return _baseReader.IsDefault; }
		}

		public override bool IsEmptyElement
		{
			get { return _baseReader.IsEmptyElement; }
		}

		public override string this[int i]
		{
			get { return _baseReader[i]; }
		}

		public override string this[string name]
		{
			get { return _baseReader[name]; }
		}

		public override string this[string name, string namespaceUri]
		{
			get { return _baseReader[name, namespaceUri]; }
		}

		public override string LocalName
		{
			get { return _baseReader.LocalName; }
		}

		public override string Name
		{
			get { return _baseReader.Name; }
		}

		public override string NamespaceURI
		{
			get { return _baseReader.NamespaceURI; }
		}

		public override XmlNameTable NameTable
		{
			get { return _baseReader.NameTable; }
		}

		public override XmlNodeType NodeType
		{
			get { return _baseReader.NodeType; }
		}

		public override string Prefix
		{
			get { return _baseReader.Prefix; }
		}

		public override char QuoteChar
		{
			get { return _baseReader.QuoteChar; }
		}

		public override ReadState ReadState
		{
			get { return _baseReader.ReadState; }
		}

		public override string Value
		{
			get { return _baseReader.Value; }
		}

		public override string XmlLang
		{
			get { return _baseReader.XmlLang; }
		}

		public override XmlSpace XmlSpace
		{
			get { return _baseReader.XmlSpace; }
		}

		public bool HasLineInfo()
		{
			var info = _baseReader as IXmlLineInfo;
			if (info != null)
			{
				return info.HasLineInfo();
			}

			return false;
		}

		public int LineNumber
		{
			get
			{
				var info = _baseReader as IXmlLineInfo;
				if (info != null)
				{
					return info.LineNumber;
				}

				return 0;
			}
		}

		public int LinePosition
		{
			get
			{
				var info = _baseReader as IXmlLineInfo;
				if (info != null)
				{
					return info.LinePosition;
				}

				return 0;
			}
		}

		public override void Close()
		{
			_baseReader.Close();
		}

		protected override void Dispose(bool disposing)
		{
			if (ReadState != ReadState.Closed)
			{
				Close();
			}

			((IDisposable) _baseReader).Dispose();
		}

		public override string GetAttribute(int i)
		{
			return _baseReader.GetAttribute(i);
		}

		public override string GetAttribute(string name)
		{
			return _baseReader.GetAttribute(name);
		}

		public override string GetAttribute(string localName, string namespaceUri)
		{
			{
				return _baseReader.GetAttribute(localName, namespaceUri);
			}
		}

		public override string LookupNamespace(string prefix)
		{
			return _baseReader.LookupNamespace(prefix);
		}

		public override void MoveToAttribute(int i)
		{
			_baseReader.MoveToAttribute(i);
		}

		public override bool MoveToAttribute(string name)
		{
			return _baseReader.MoveToAttribute(name);
		}

		public override bool MoveToAttribute(string localName, string namespaceUri)
		{
			return _baseReader.MoveToAttribute(localName, namespaceUri);
		}

		public override bool MoveToElement()
		{
			return _baseReader.MoveToElement();
		}

		public override bool MoveToFirstAttribute()
		{
			return _baseReader.MoveToFirstAttribute();
		}

		public override bool MoveToNextAttribute()
		{
			return _baseReader.MoveToNextAttribute();
		}

		public override bool Read()
		{
			return _baseReader.Read();
		}

		public override bool ReadAttributeValue()
		{
			return _baseReader.ReadAttributeValue();
		}

		public override int ReadValueChunk(char[] buffer, int index, int count)
		{
			return _baseReader.ReadValueChunk(buffer, index, count);
		}

		public override void ResolveEntity()
		{
			_baseReader.ResolveEntity();
		}
	}
}
