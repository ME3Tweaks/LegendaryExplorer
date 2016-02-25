using System;
using System.Xml;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	public class XmlNormalizingReader : XmlWrappingReader
	{
		private readonly XmlNamespaceManager _nsManager;
		private readonly string _xmlNsNamespace;

		public XmlNormalizingReader(XmlReader baseReader)
			: base(baseReader)
		{
			if (baseReader.NameTable != null)
			{
				_nsManager = new XmlNamespaceManager(baseReader.NameTable);
			}

			_xmlNsNamespace = _nsManager.LookupNamespace("xmlns");
		}

		public override int AttributeCount
		{
			get
			{
				var count = 0;
				for (var go = MoveToFirstAttribute(); go; go = MoveToNextAttribute())
				{
					count++;
				}

				return count;
			}
		}

		private bool IsXmlNs
		{
			get { return NamespaceURI == _xmlNsNamespace; }
		}

		private bool IsLocalXmlNs
		{
			get
			{
				var namespacesInScope = _nsManager.GetNamespacesInScope(XmlNamespaceScope.Local);
				return namespacesInScope != null && namespacesInScope.ContainsKey(GetNamespacePrefix());
			}
		}

		private string GetNamespacePrefix()
		{
			// This is not very intuitive, but it's how it works.
			// In the first case, a non-empty prefix is represented 
			// as an xmlns:foo="bar" declaration, where xmlns is the 
			// actual attribute prefix, and where the real prefix 
			// being declared is the reader localname (foo in this case).
			// If no prefix is being declared for the namespace, 
			// it's an xmlns="foo" declaration, therefore we pass empty string.
			return Prefix.Length > 0 ? LocalName : String.Empty;
		}

		public override bool MoveToFirstAttribute()
		{
			var moved = base.MoveToFirstAttribute();
			while (moved && IsXmlNs && !IsLocalXmlNs)
			{
				moved = MoveToNextAttribute();
			}

			if (!moved)
			{
				base.MoveToElement();
			}

			return moved;
		}

		public override bool MoveToNextAttribute()
		{
			var moved = base.MoveToNextAttribute();
			while (moved && IsXmlNs && !IsLocalXmlNs)
			{
				moved = MoveToNextAttribute();
			}

			return moved;
		}

		public override bool Read()
		{
			var read = base.Read();

			switch (base.NodeType)
			{
				case XmlNodeType.Element:
					_nsManager.PushScope();
					for (var go = BaseReader.MoveToFirstAttribute(); go; go = BaseReader.MoveToNextAttribute())
					{
						if (BaseReader.NamespaceURI != _xmlNsNamespace)
						{
							continue;
						}
						var prefix = GetNamespacePrefix();

						// Only push if it's not already defined.
						if (_nsManager.LookupNamespace(prefix) == null)
						{
							_nsManager.AddNamespace(prefix, Value);
						}
					}
					if (BaseReader.HasAttributes)
					{
						BaseReader.MoveToElement();
					}
					break;
				case XmlNodeType.EndElement:
					_nsManager.PopScope();
					break;
			}

			return read;
		}
	}
}
