using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.Common.XPath
{
	public class XPathNavigatorIterator : XPathNodeIterator
	{
		private readonly List<XPathNavigator> _navigators;
		private int _position = -1;

		public XPathNavigatorIterator()
		{
			_navigators = new List<XPathNavigator>();
		}

		public XPathNavigatorIterator(int capacity)
		{
			_navigators = new List<XPathNavigator>(capacity);
		}

		public XPathNavigatorIterator(XPathNavigator navigator)
			: this()
		{
			_navigators.Add(navigator);
		}

		public XPathNavigatorIterator(XPathNodeIterator iterator)
			: this(iterator, false) {}

		public XPathNavigatorIterator(XPathNodeIterator iterator, bool removeDuplicates)
			: this()
		{
			var it = iterator.Clone();

			while (it.MoveNext())
			{
				if (removeDuplicates)
				{
					if (Contains(it.Current))
					{
						continue;
					}
				}

				Add(it.Current.Clone());
			}
		}

		public XPathNavigatorIterator(List<XPathNavigator> navigators)
		{
			_navigators = navigators;
		}

		public XPathNavigator this[int index]
		{
			get { return _navigators[index]; }
			set { _navigators[index] = value; }
		}

		public override int Count
		{
			get { return _navigators.Count; }
		}

		public override XPathNavigator Current
		{
			get { return _position == -1 ? null : _navigators[_position]; }
		}

		public override int CurrentPosition
		{
			get { return _position + 1; }
		}

		public void Add(XPathNavigator navigator)
		{
			if (_position != -1)
			{
				throw new InvalidOperationException(Resources.XPathNavigatorIterator_CantAddAfterMove);
			}

			_navigators.Add(navigator.Clone());
		}

		public void Add(XPathNodeIterator iterator)
		{
			if (_position != -1)
			{
				throw new InvalidOperationException(
					Resources.XPathNavigatorIterator_CantAddAfterMove);
			}

			while (iterator.MoveNext())
			{
				_navigators.Add(iterator.Current.Clone());
			}
		}

		public void Add(IEnumerable<XPathNavigator> navigators)
		{
			if (_position != -1)
			{
				throw new InvalidOperationException(
					Resources.XPathNavigatorIterator_CantAddAfterMove);
			}

			foreach (var navigator in navigators)
			{
				_navigators.Add(navigator.Clone());
			}
		}

		public override XPathNodeIterator Clone()
		{
			return new XPathNavigatorIterator(
				new List<XPathNavigator>(_navigators));
		}

		public bool Contains(XPathNavigator value)
		{
			return _navigators.Any(nav => nav.IsSamePosition(value));
		}

		public bool ContainsValue(string value)
		{
			return _navigators.Any(nav => nav.Value.Equals(value));
		}

		public override bool MoveNext()
		{
			if (_navigators.Count == 0)
			{
				return false;
			}

			_position++;
			return _position < _navigators.Count;
		}

		public void RemoveAt(int index)
		{
			_navigators.RemoveAt(index);
		}

		public void Reset()
		{
			_position = -1;
		}
	}
}
