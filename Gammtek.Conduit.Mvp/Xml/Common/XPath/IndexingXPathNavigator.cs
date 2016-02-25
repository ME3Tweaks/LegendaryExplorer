using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.Common.XPath
{
	public class IndexingXPathNavigator : XPathNavigator
	{
		private readonly XPathNavigatorIndexManager _manager;
		private readonly XPathNavigator _nav;

		public IndexingXPathNavigator(XPathNavigator navigator)
		{
			_nav = navigator;
			_manager = new XPathNavigatorIndexManager();
		}

		public override XPathNodeType NodeType
		{
			get { return _nav.NodeType; }
		}

		public override string LocalName
		{
			get { return _nav.LocalName; }
		}

		public override string Name
		{
			get { return _nav.Name; }
		}

		public override string NamespaceURI
		{
			get { return _nav.NamespaceURI; }
		}

		public override string Prefix
		{
			get { return _nav.Prefix; }
		}

		public override string Value
		{
			get { return _nav.Value; }
		}

		public override String BaseURI
		{
			get { return _nav.BaseURI; }
		}

		public override bool IsEmptyElement
		{
			get { return _nav.IsEmptyElement; }
		}

		public override string XmlLang
		{
			get { return _nav.XmlLang; }
		}

		public override XmlNameTable NameTable
		{
			get { return _nav.NameTable; }
		}

		public override bool HasAttributes
		{
			get { return _nav.HasAttributes; }
		}

		public override bool HasChildren
		{
			get { return _nav.HasChildren; }
		}

		public virtual void AddKey(string keyName, string match, string use)
		{
			var key = new KeyDef(_nav, match, use);
			_manager.AddKey(_nav, keyName, key);
		}

		public void BuildIndexes()
		{
			_manager.BuildIndexes();
		}

		public override XPathNavigator Clone()
		{
			return new IndexingXPathNavigator(_nav.Clone());
		}

		public override XPathExpression Compile(string xpath)
		{
			var expr = base.Compile(xpath);
			expr.SetContext(new IndexingXsltContext(_manager, _nav.NameTable));
			return expr;
		}

		public override string GetAttribute(string localName, string namespaceUri)
		{
			return _nav.GetAttribute(localName, namespaceUri);
		}

		public override string GetNamespace(string localname)
		{
			return _nav.GetNamespace(localname);
		}

		public override bool IsSamePosition(XPathNavigator other)
		{
			return _nav.IsSamePosition(other);
		}

		public override bool MoveTo(XPathNavigator other)
		{
			return _nav.MoveTo(other);
		}

		public override bool MoveToAttribute(string localName, string namespaceUri)
		{
			return _nav.MoveToAttribute(localName, namespaceUri);
		}

		public override bool MoveToFirst()
		{
			return _nav.MoveToFirst();
		}

		public override bool MoveToFirstAttribute()
		{
			return _nav.MoveToFirstAttribute();
		}

		public override bool MoveToFirstChild()
		{
			return _nav.MoveToFirstChild();
		}

		public override bool MoveToFirstNamespace(XPathNamespaceScope namespaceScope)
		{
			return _nav.MoveToFirstNamespace(namespaceScope);
		}

		public override bool MoveToId(string id)
		{
			return _nav.MoveToId(id);
		}

		public override bool MoveToNamespace(string @namespace)
		{
			return _nav.MoveToNamespace(@namespace);
		}

		public override bool MoveToNext()
		{
			return _nav.MoveToNext();
		}

		public override bool MoveToNextAttribute()
		{
			return _nav.MoveToNextAttribute();
		}

		public override bool MoveToNextNamespace(XPathNamespaceScope namespaceScope)
		{
			return _nav.MoveToNextNamespace(namespaceScope);
		}

		public override bool MoveToParent()
		{
			return _nav.MoveToParent();
		}

		public override bool MoveToPrevious()
		{
			return _nav.MoveToPrevious();
		}

		public override void MoveToRoot()
		{
			_nav.MoveToRoot();
		}

		public override XPathNodeIterator Select(string xpath)
		{
			var expr = Compile(xpath);
			return base.Select(expr);
		}

		private class IndexingXsltContext : XsltContext
		{
			private readonly KeyExtensionFunction _keyFuncImpl;

			public IndexingXsltContext(XPathNavigatorIndexManager manager, XmlNameTable nt)
				: base((NameTable) nt)
			{
				_keyFuncImpl = new KeyExtensionFunction(manager);
			}

			public override bool Whitespace
			{
				get { return true; }
			}

			public override int CompareDocument(string baseUri, string nextbaseUri)
			{
				return 0;
			}

			public override bool PreserveWhitespace(XPathNavigator node)
			{
				return true;
			}

			public override IXsltContextFunction ResolveFunction(string prefix, string name,
				XPathResultType[] argTypes)
			{
				if (prefix.Length != 0 || name != "key")
				{
					return null;
				}

				if (argTypes.Length != 2)
				{
					throw new ArgumentException(Resources.IndexingXPathNavigator_KeyWrongArguments);
				}

				if (argTypes[0] != XPathResultType.String)
				{
					throw new ArgumentException(Resources.IndexingXPathNavigator_KeyArgumentNotString);
				}

				return _keyFuncImpl;
			}

			public override IXsltContextVariable ResolveVariable(string prefix, string name)
			{
				return null;
			}
		}

		private class KeyDef
		{
			private readonly XPathNavigator _nav;
			private XPathExpression _matchExpr;
			private XPathExpression _useExpr;

			public KeyDef(XPathNavigator nav, string match, string use)
			{
				_nav = nav;
				Match = match;
				Use = use;
			}

			private string Match { get; set; }

			private string Use { get; set; }

			public XPathExpression MatchExpr
			{
				get { return _matchExpr ?? (_matchExpr = _nav.Compile(Match)); }
			}

			public XPathExpression UseExpr
			{
				get { return _useExpr ?? (_useExpr = _nav.Compile(Use)); }
			}
		}

		private class KeyExtensionFunction : IXsltContextFunction
		{
			private const int Args = 2;
			private static readonly XPathResultType[] ResultArgTypes = { XPathResultType.String, XPathResultType.Any };
			private readonly XPathNavigatorIndexManager _manager;

			public KeyExtensionFunction(XPathNavigatorIndexManager manager)
			{
				_manager = manager;
			}

			public int Minargs
			{
				get { return Args; }
			}

			public int Maxargs
			{
				get { return Args; }
			}

			public XPathResultType[] ArgTypes
			{
				get { return ResultArgTypes; }
			}

			public XPathResultType ReturnType
			{
				get { return XPathResultType.NodeSet; }
			}

			public object Invoke(XsltContext xsltContext, object[] args,
				XPathNavigator docContext)
			{
				return _manager.GetNodes((string) args[0], args[1]);
			}
		}

		private class XPathNavigatorIndex
		{
			private readonly IDictionary<string, List<XPathNavigator>> _index;
			private readonly List<KeyDef> _keys;

			public XPathNavigatorIndex()
			{
				_keys = new List<KeyDef>();
				_index = new Dictionary<string, List<XPathNavigator>>();
			}

			public void AddKey(KeyDef key)
			{
				_keys.Add(key);
			}

			private void AddNodeToIndex(XPathNavigator node, string key)
			{
				//Get slot
				List<XPathNavigator> indexedNodes;
				if (!_index.TryGetValue(key, out indexedNodes))
				{
					indexedNodes = new List<XPathNavigator>();
					_index.Add(key, indexedNodes);
				}
				indexedNodes.Add(node.Clone());
			}

			public XPathNodeIterator GetNodes(object keyValue)
			{
				//As per XSLT spec:
				//When the second argument to the key function is of type node-set, 
				//then the result is the union of the result of applying the key function 
				//to the string value of each of the nodes in the argument node-set. 
				//When the second argument to key is of any other type, the argument is 
				//converted to a string as if by a call to the string function; it 
				//returns a node-set containing the nodes in the same document as 
				//the context node that have a value for the named key equal to this string.      
				List<XPathNavigator> indexedNodes = null;
				if (keyValue is XPathNodeIterator)
				{
					var nodes = keyValue as XPathNodeIterator;
					while (nodes.MoveNext())
					{
						List<XPathNavigator> tmpIndexedNodes;
						if (!_index.TryGetValue(nodes.Current.Value, out tmpIndexedNodes))
						{
							continue;
						}

						if (indexedNodes == null)
						{
							indexedNodes = new List<XPathNavigator>();
						}

						indexedNodes.AddRange(tmpIndexedNodes);
					}
				}
				else
				{
					_index.TryGetValue(keyValue.ToString(), out indexedNodes);
				}
				if (indexedNodes == null)
				{
					indexedNodes = new List<XPathNavigator>(0);
				}

				return new XPathNavigatorIterator(indexedNodes);
			}

			public void MatchNode(XPathNavigator node)
			{
				foreach (var keyDef in _keys)
				{
					if (!node.Matches(keyDef.MatchExpr))
					{
						continue;
					}

					//Ok, let's calculate key value(s). As per XSLT spec:
					//If the result is a node-set, then for each node in the node-set, 
					//the node that matches the pattern has a key of the specified name whose 
					//value is the string-value of the node in the node-set; otherwise, the result 
					//is converted to a string, and the node that matches the pattern has a 
					//key of the specified name with value equal to that string.        
					var key = node.Evaluate(keyDef.UseExpr);
					var iterator = key as XPathNodeIterator;
					if (iterator != null)
					{
						var ni = iterator;
						while (ni.MoveNext())
						{
							AddNodeToIndex(node, ni.Current.Value);
						}
					}
					else
					{
						if (key == null)
						{
							continue;
						}

						AddNodeToIndex(node, key.ToString());
					}
				}
			}
		}

		private class XPathNavigatorIndexManager
		{
			private bool _indexed;
			private IDictionary<string, XPathNavigatorIndex> _indexes;
			private XPathNavigator _nav;

			public void AddKey(XPathNavigator nav, string indexName, KeyDef key)
			{
				_indexed = false;
				_nav = nav;
				//Named indexes are stored in a hashtable.
				if (_indexes == null)
				{
					_indexes = new Dictionary<string, XPathNavigatorIndex>();
				}
				XPathNavigatorIndex index;
				if (!_indexes.TryGetValue(indexName, out index))
				{
					index = new XPathNavigatorIndex();
					_indexes.Add(indexName, index);
				}
				index.AddKey(key);
			}

			public void BuildIndexes()
			{
				var doc = _nav.Clone();
				//Walk through the all document nodes adding each one matching 
				//'match' expression to the index.
				doc.MoveToRoot();
				//Select all nodes but namespaces and attributes
				var ni = doc.SelectDescendants(XPathNodeType.All, true);
				while (ni.MoveNext())
				{
					if (ni.Current.NodeType == XPathNodeType.Element)
					{
						var tempNav = ni.Current.Clone();
						//Processs namespace nodes
						for (var go = tempNav.MoveToFirstNamespace(); go; go = tempNav.MoveToNextNamespace())
						{
							foreach (var index in _indexes.Values)
							{
								index.MatchNode(tempNav);
							}
						}
						//ni.Current.MoveToParent();

						tempNav = ni.Current.Clone();
						//process attributes
						for (var go = tempNav.MoveToFirstAttribute(); go; go = tempNav.MoveToNextAttribute())
						{
							foreach (var index in _indexes.Values)
							{
								index.MatchNode(tempNav);
							}
						}
						//ni.Current.MoveToParent();
					}

					foreach (var index in _indexes.Values)
					{
						index.MatchNode(ni.Current);
					}
				}
				_indexed = true;
			}

			public XPathNodeIterator GetNodes(string indexName, object value)
			{
				if (!_indexed)
				{
					BuildIndexes();
				}
				XPathNavigatorIndex index;
				_indexes.TryGetValue(indexName, out index);
				return index == null ? null : index.GetNodes(value);
			}
		}
	}
}
