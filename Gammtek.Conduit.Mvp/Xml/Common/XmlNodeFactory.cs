using System;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	public class XmlNodeFactory
	{
		private XmlNodeFactory() {}

		public static XmlNode Create(object value)
		{
			return new ObjectNode(value);
		}

		public static XmlNode Create(XPathNavigator navigator)
		{
			return new XPathNavigatorNode(navigator);
		}

		public static XmlNode Create(XmlReader reader)
		{
			return Create(reader, false);
		}

		public static XmlNode Create(XmlReader reader, bool defaultAttrs)
		{
			return new XmlReaderNode(reader, defaultAttrs);
		}

		private class ObjectNode : SerializableNode
		{
			private readonly object _serializableObject;

			public ObjectNode(object serializableObject)
			{
				_serializableObject = serializableObject;
			}

			public override void WriteTo(XmlWriter w)
			{
				var ser = new XmlSerializer(_serializableObject.GetType());
				ser.Serialize(w, _serializableObject);
			}
		}

		private abstract class SerializableNode : XmlElement
		{
			protected SerializableNode()
				: base("", "dummy", "", new XmlDocument()) {}

			public override XmlAttributeCollection Attributes
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override string BaseURI
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override XmlNodeList ChildNodes
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override XmlNode FirstChild
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override bool HasChildNodes
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override string InnerText
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
				set { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override string InnerXml
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
				set { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override bool IsReadOnly
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override XmlNode LastChild
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override string LocalName
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override string Name
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override string NamespaceURI
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override XmlNode NextSibling
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override XmlNodeType NodeType
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override string OuterXml
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override XmlDocument OwnerDocument
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override XmlNode ParentNode
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override string Prefix
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
				set { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override XmlNode PreviousSibling
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override XmlElement this[string localname, string ns]
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override XmlElement this[string name]
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override string Value
			{
				get { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
				set { throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM); }
			}

			public override XmlNode AppendChild(XmlNode newChild)
			{
				throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM);
			}

			public override XmlNode Clone()
			{
				throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM);
			}

			public override XmlNode CloneNode(bool deep)
			{
				throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM);
			}

			public override string GetNamespaceOfPrefix(string prefix)
			{
				throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM);
			}

			public override string GetPrefixOfNamespace(string namespaceUri)
			{
				throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM);
			}

			public override XmlNode InsertAfter(XmlNode newChild, XmlNode refChild)
			{
				throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM);
			}

			public override XmlNode InsertBefore(XmlNode newChild, XmlNode refChild)
			{
				throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM);
			}

			public override void Normalize()
			{
				throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM);
			}

			public override XmlNode PrependChild(XmlNode newChild)
			{
				throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM);
			}

			public override void RemoveAll()
			{
				throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM);
			}

			public override XmlNode RemoveChild(XmlNode oldChild)
			{
				throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM);
			}

			public override XmlNode ReplaceChild(XmlNode newChild, XmlNode oldChild)
			{
				throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM);
			}

			public override bool Supports(string feature, string version)
			{
				throw new NotSupportedException(Resources.XmlDocumentFactory_NotImplementedDOM);
			}

			public override void WriteContentTo(XmlWriter w)
			{
				WriteTo(w);
			}

			public abstract override void WriteTo(XmlWriter w);
		}

		private class XPathNavigatorNode : SerializableNode
		{
			private readonly XPathNavigator _navigator;

			public XPathNavigatorNode(XPathNavigator navigator)
			{
				_navigator = navigator;
			}

			public override void WriteTo(XmlWriter w)
			{
				w.WriteNode(_navigator.ReadSubtree(), false);
			}
		}

		private class XmlReaderNode : SerializableNode
		{
			private readonly bool _default;
			private readonly XmlReader _reader;

			public XmlReaderNode(XmlReader reader, bool defaultAttrs)
			{
				_reader = reader;
				_reader.MoveToContent();
				_default = defaultAttrs;
			}

			public override void WriteTo(XmlWriter w)
			{
				w.WriteNode(_reader, _default);
				_reader.Close();
			}
		}
	}
}
