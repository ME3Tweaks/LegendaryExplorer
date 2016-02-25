using System;
using System.Xml;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	public class XmlFragmentReader : XmlWrappingReader
	{
		private bool _isRoot;
		private XmlNodeType _nodeType;
		private XmlQualifiedName _rootName;
		private ReadState _state = ReadState.Initial;

		public XmlFragmentReader(string rootElementName, XmlReader baseReader)
			: base(baseReader)
		{
			Guard.ArgumentNotNullOrEmptyString(rootElementName, "rootElementName");

			Initialize(new XmlQualifiedName(rootElementName, String.Empty));
		}

		public XmlFragmentReader(string rootElementName, string rootXmlNamespace, XmlReader baseReader)
			: base(baseReader)
		{
			Guard.ArgumentNotNullOrEmptyString(rootElementName, "rootElementName");
			Guard.ArgumentNotNull(rootXmlNamespace, "rootXmlNamespace");

			Initialize(new XmlQualifiedName(rootElementName, rootXmlNamespace));
		}

		public XmlFragmentReader(XmlQualifiedName rootName, XmlReader baseReader)
			: base(baseReader)
		{
			Guard.ArgumentNotNull(rootName, "rootName");

			Initialize(rootName);
		}

		public override ReadState ReadState
		{
			get
			{
				var baseState = base.ReadState;

				if (baseState == ReadState.Initial ||
					baseState == ReadState.EndOfFile)
				{
					return _state;
				}

				return baseState;
			}
		}

		public override XmlNodeType NodeType
		{
			get { return _isRoot ? _nodeType : base.NodeType; }
		}

		public override int Depth
		{
			get { return base.Depth + 1; }
		}

		public override string LocalName
		{
			get { return _isRoot ? _rootName.Name : base.LocalName; }
		}

		public override string NamespaceURI
		{
			get { return _isRoot ? _rootName.Namespace : base.NamespaceURI; }
		}

		public override string Prefix
		{
			get { return _isRoot ? String.Empty : base.Prefix; }
		}

		public override string Name
		{
			get
			{
				if (!_isRoot)
				{
					return base.Name;
				}
				if (NameTable != null)
				{
					return Prefix.Length == 0 ? LocalName : NameTable.Add(Prefix + ":" + LocalName);
				}
				return null;
			}
		}

		private void Initialize(XmlQualifiedName rootName)
		{
			_rootName = rootName;
		}

		public override bool Read()
		{
			if (_state == ReadState.Initial)
			{
				_state = ReadState.Interactive;
				_isRoot = true;
				_nodeType = XmlNodeType.Element;
				return true;
			}
			if (_state == ReadState.EndOfFile)
			{
				return false;
			}

			var read = base.Read();

			if (_isRoot)
			{
				if (!read)
				{
					_isRoot = false;
					_nodeType = XmlNodeType.None;
					_state = ReadState.EndOfFile;
				}
				else
				{
					_isRoot = false;
				}
			}
			else
			{
				if (read)
				{
					return true;
				}
				_isRoot = true;
				_nodeType = XmlNodeType.EndElement;
				return true;
			}

			return read;
		}
	}
}
