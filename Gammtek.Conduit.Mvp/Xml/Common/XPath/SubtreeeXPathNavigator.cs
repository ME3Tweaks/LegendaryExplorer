using System;
using System.Xml;
using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Common.XPath
{
	public class SubtreeXPathNavigator : XPathNavigator
	{
		// The current location.
		// The node that limits the scope.
		private readonly bool _fragment;
		private readonly XPathNavigator _root;
		// Whether we're at the root node (parent of the first child).
		private bool _atroot = true;
		private XPathNavigator _navigator;
		// Whether XML fragment navigation is enabled.

		public SubtreeXPathNavigator(XPathNavigator navigator)
			: this(navigator, false) {}

		public SubtreeXPathNavigator(XPathNavigator navigator, bool enableFragment)
		{
			_navigator = navigator.Clone();
			_root = navigator.Clone();
			_fragment = enableFragment;
		}

		private SubtreeXPathNavigator(XPathNavigator root, XPathNavigator current,
			bool atRoot, bool enableFragment)
		{
			_root = root.Clone();
			_navigator = current.Clone();
			_atroot = atRoot;
			_fragment = enableFragment;
		}

		private bool AtRoot
		{
			get { return _atroot; }
		}

		private bool IsTop
		{
			get { return _navigator.IsSamePosition(_root); }
		}

		public override String BaseURI
		{
			get { return AtRoot ? String.Empty : _navigator.BaseURI; }
		}

		public override bool HasAttributes
		{
			get { return !AtRoot && _navigator.HasAttributes; }
		}

		public override bool HasChildren
		{
			get { return AtRoot || _navigator.HasChildren; }
		}

		public override bool IsEmptyElement
		{
			get { return !AtRoot && _navigator.IsEmptyElement; }
		}

		public override string LocalName
		{
			get { return AtRoot ? String.Empty : _navigator.LocalName; }
		}

		public override string Name
		{
			get { return AtRoot ? String.Empty : _navigator.Name; }
		}

		public override string NamespaceURI
		{
			get { return AtRoot ? String.Empty : _navigator.NamespaceURI; }
		}

		public override XmlNameTable NameTable
		{
			get { return _navigator.NameTable; }
		}

		public override XPathNodeType NodeType
		{
			get { return AtRoot ? XPathNodeType.Root : _navigator.NodeType; }
		}

		public override string Prefix
		{
			get { return AtRoot ? String.Empty : _navigator.Prefix; }
		}

		public override string Value
		{
			get { return AtRoot ? String.Empty : _navigator.Value; }
		}

		public override string XmlLang
		{
			get { return AtRoot ? String.Empty : _navigator.XmlLang; }
		}

		public override XPathNavigator Clone()
		{
			return new SubtreeXPathNavigator(_root, _navigator, _atroot, _fragment);
		}

		public override string GetAttribute(string localName, string namespaceUri)
		{
			return AtRoot ? String.Empty : _navigator.GetAttribute(localName, namespaceUri);
		}

		public override string GetNamespace(string localName)
		{
			return AtRoot ? String.Empty : _navigator.GetNamespace(localName);
		}

		public override bool IsSamePosition(XPathNavigator other)
		{
			if (!(other is SubtreeXPathNavigator))
			{
				return false;
			}

			var nav = (SubtreeXPathNavigator) other;
			return nav._atroot == _atroot &&
				nav._navigator.IsSamePosition(_navigator) &&
				nav._root.IsSamePosition(_root);
		}

		public override bool MoveTo(XPathNavigator other)
		{
			if (!(other is SubtreeXPathNavigator))
			{
				return false;
			}

			return _navigator.MoveTo(((SubtreeXPathNavigator) other)._navigator);
		}

		public override bool MoveToAttribute(string localName, string namespaceUri)
		{
			return !AtRoot && _navigator.MoveToAttribute(localName, namespaceUri);
		}

		public override bool MoveToFirst()
		{
			if (AtRoot)
			{
				return false;
			}
			if (IsTop)
			{
				if (!_fragment)
				{
					return false;
				}
				if (_root.MoveToFirst())
				{
					_navigator.MoveToFirst();
					return true;
				}
			}

			return _navigator.MoveToNext();
		}

		public override bool MoveToFirstAttribute()
		{
			return !AtRoot && _navigator.MoveToFirstAttribute();
		}

		public override bool MoveToFirstChild()
		{
			if (AtRoot)
			{
				_atroot = false;
				return true;
			}

			return _navigator.MoveToFirstChild();
		}

		public override bool MoveToFirstNamespace(XPathNamespaceScope namespaceScope)
		{
			return !AtRoot && _navigator.MoveToFirstNamespace(namespaceScope);
		}

		public override bool MoveToId(string id)
		{
			return _navigator.MoveToId(id);
		}

		public override bool MoveToNamespace(string @namespace)
		{
			return !AtRoot && _navigator.MoveToNamespace(@namespace);
		}

		public override bool MoveToNext()
		{
			if (AtRoot)
			{
				return false;
			}
			if (IsTop)
			{
				if (!_fragment)
				{
					return false;
				}
				if (_root.MoveToNext())
				{
					_navigator.MoveToNext();
					return true;
				}
			}

			return _navigator.MoveToNext();
		}

		public override bool MoveToNextAttribute()
		{
			return !AtRoot && _navigator.MoveToNextAttribute();
		}

		public override bool MoveToNextNamespace(XPathNamespaceScope namespaceScope)
		{
			return !AtRoot && _navigator.MoveToNextNamespace(namespaceScope);
		}

		public override bool MoveToParent()
		{
			if (AtRoot)
			{
				return false;
			}

			if (!IsTop)
			{
				return _navigator.MoveToParent();
			}

			_atroot = true;
			return true;
		}

		public override bool MoveToPrevious()
		{
			if (AtRoot)
			{
				return false;
			}
			if (!IsTop)
			{
				return _navigator.MoveToPrevious();
			}

			if (!_fragment)
			{
				return false;
			}

			if (!_root.MoveToPrevious())
			{
				return _navigator.MoveToPrevious();
			}

			_navigator.MoveToPrevious();
			return true;
		}

		public override void MoveToRoot()
		{
			_navigator = _root.Clone();
			_atroot = true;
		}
	}
}
