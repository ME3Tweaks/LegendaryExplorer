using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	public static class XmlNodeListFactory
	{
		public static XmlNodeList CreateNodeList(XPathNodeIterator iterator)
		{
			return new XmlNodeListIterator(iterator);
		}

		private class XmlNodeListIterator : XmlNodeList
		{
			private readonly XPathNodeIterator _iterator;
			private readonly IList<XmlNode> _nodes = new List<XmlNode>();
			private bool _done;

			public XmlNodeListIterator(XPathNodeIterator iterator)
			{
				_iterator = iterator.Clone();
			}

			public override int Count
			{
				get
				{
					if (!_done)
					{
						ReadToEnd();
					}
					return _nodes.Count;
				}
			}

			private bool Done
			{
				get { return _done; }
			}

			private int CurrentPosition
			{
				get { return _nodes.Count; }
			}

			public override IEnumerator GetEnumerator()
			{
				return new XmlNodeListEnumerator(this);
			}

			public override XmlNode Item(int index)
			{
				if (index >= _nodes.Count)
				{
					ReadTo(index);
				}
				// Compatible behavior with .NET
				if (index >= _nodes.Count || index < 0)
				{
					return null;
				}
				return _nodes[index];
			}

			private void ReadTo(int to)
			{
				while (_nodes.Count <= to)
				{
					if (_iterator.MoveNext())
					{
						var node = _iterator.Current as IHasXmlNode;
						// Check IHasXmlNode interface.
						if (node == null)
						{
							throw new ArgumentException(Resources.XmlNodeListFactory_IHasXmlNodeMissing);
						}
						_nodes.Add(node.GetNode());
					}
					else
					{
						_done = true;
						return;
					}
				}
			}

			private void ReadToEnd()
			{
				while (_iterator.MoveNext())
				{
					var node = _iterator.Current as IHasXmlNode;
					// Check IHasXmlNode interface.
					if (node == null)
					{
						throw new ArgumentException(Resources.XmlNodeListFactory_IHasXmlNodeMissing);
					}
					_nodes.Add(node.GetNode());
				}
				_done = true;
			}

			private class XmlNodeListEnumerator : IEnumerator
			{
				private readonly XmlNodeListIterator _iterator;
				private int _position = -1;

				public XmlNodeListEnumerator(XmlNodeListIterator iterator)
				{
					_iterator = iterator;
				}

				void IEnumerator.Reset()
				{
					_position = -1;
				}

				bool IEnumerator.MoveNext()
				{
					_position++;
					_iterator.ReadTo(_position);

					// If we reached the end and our index is still 
					// bigger, there're no more items.
					if (_iterator.Done && _position >= _iterator.CurrentPosition)
					{
						return false;
					}

					return true;
				}

				object IEnumerator.Current
				{
					get { return _iterator[_position]; }
				}
			}
		}
	}
}
