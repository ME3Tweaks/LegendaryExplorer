using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MassEffect3.Coalesce
{
	public class Namespace : IDictionary<string, Namespace>
	{
		private readonly IDictionary<string, Namespace> _subNamespaces;
		private Namespace _parent;

		private Namespace(string nameOnLevel, Namespace parent = null)
		{
			if (string.IsNullOrWhiteSpace(nameOnLevel))
			{
				throw new ArgumentException("nameOfLevel");
			}

			Parent = parent;
			NameOnLevel = nameOnLevel;
			_subNamespaces = new Dictionary<string, Namespace>();

			if (Parent != null)
			{
				Parent.Add(NameOnLevel, this);
			}
		}

		public int Count
		{
			get { return _subNamespaces.Count; }
		}

		public string FullName
		{
			get
			{
				if (Parent == null)
				{
					return NameOnLevel;
				}

				return string.Format("{0}.{1}",
					Parent.FullName,
					NameOnLevel);
			}
		}

		public bool IsReadOnly
		{
			get { return false; }
		}

		public ICollection<string> Keys
		{
			get { return _subNamespaces.Keys; }
		}

		public String NameOnLevel { get; private set; }

		public Namespace Parent
		{
			get { return _parent; }
			private set
			{
				if (Parent != null)
				{
					Parent.Remove(NameOnLevel);
				}

				_parent = value;
			}
		}

		public ICollection<Namespace> Subnamespaces
		{
			get { return _subNamespaces.Values; }
		}

		public ICollection<Namespace> Values
		{
			get { return _subNamespaces.Values; }
		}

		public Namespace this[string nameOnLevel]
		{
			get { return _subNamespaces[nameOnLevel]; }
			set
			{
				if (value == null)
				{
					throw new ArgumentException("value");
				}

				Namespace toReplace;

				if (TryGetValue(nameOnLevel, out toReplace))
				{
					toReplace.Parent = null;
				}

				value.Parent = this;
			}
		}

		public static IEnumerable<Namespace> FromSplitStrings(Namespace root, IEnumerable<IEnumerable<String>> splitSubNamespaces)
		{
			if (splitSubNamespaces == null)
			{
				throw new ArgumentNullException("splitSubNamespaces");
			}

			return splitSubNamespaces
				// Remove those split sequences that have no elements
				.Where(splitSubNamespace =>
					splitSubNamespace.Any())
				// Group by the outermost namespace
				.GroupBy(splitNamespace =>
					splitNamespace.First())
				// Create Namespace for each group and prepare sequences that represent nested namespaces
				.Select(group =>
					new
					{
						Root = new Namespace(group.Key, root),
						SplitSubnamespaces = group
							.Select(splitNamespace =>
								splitNamespace.Skip(1))
					})
				// Select nested namespaces with recursive split call
				.Select(obj =>
					new
					{
						obj.Root,
						SubNamespaces = FromSplitStrings(obj.Root, obj.SplitSubnamespaces)
					})
				// Select only uppermost level namespaces to return
				.Select(obj =>
					obj.Root)
				// To avoid deferred execution problems when recursive function may not be able to create nested namespaces
				.ToArray();
		}

		public static IEnumerable<Namespace> FromStrings(IEnumerable<String> namespaceStrings)
		{
			// Split all strings
			var splitSubNamespaces = namespaceStrings
				.Select(fullNamespace =>
					fullNamespace.Split('.'));

			return FromSplitStrings(null, splitSubNamespaces);
		}

		public void Add(KeyValuePair<string, Namespace> item)
		{
			Add(item.Key, item.Value);
		}

		public void Clear()
		{
			foreach (var subNamespace in _subNamespaces.Select(kv => kv.Value))
			{
				subNamespace._parent = null;
			}

			_subNamespaces.Clear();
		}

		public bool Contains(KeyValuePair<string, Namespace> item)
		{
			return _subNamespaces.Contains(item);
		}

		public void CopyTo(KeyValuePair<string, Namespace>[] array, int arrayIndex)
		{
			_subNamespaces.CopyTo(array, arrayIndex);
		}

		public bool Remove(KeyValuePair<string, Namespace> item)
		{
			return _subNamespaces.Remove(item);
		}

		public void Add(string key, Namespace value)
		{
			if (ContainsKey(key))
			{
				throw new InvalidOperationException("Namespace already contains namespace with such name on level");
			}

			_subNamespaces.Add(key, value);
		}

		public bool ContainsKey(string key)
		{
			return _subNamespaces.ContainsKey(key);
		}

		public bool Remove(string key)
		{
			if (!ContainsKey(key))
			{
				throw new KeyNotFoundException();
			}

			this[key]._parent = null;

			return _subNamespaces.Remove(key);
		}

		public bool TryGetValue(string key, out Namespace value)
		{
			return _subNamespaces.TryGetValue(key, out value);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<KeyValuePair<string, Namespace>> GetEnumerator()
		{
			return _subNamespaces.GetEnumerator();
		}

		public bool IsNamespace { get; set; }
		public bool IsClass { get; set; }
		public bool IsProperty { get; set; }

		public static void ToXElements(XElement elementNode, IEnumerable<Namespace> namespaces)
		{
			foreach (var ns in namespaces)
			{
				var node = new XElement(ns.NameOnLevel);
				elementNode.Add(node);

				ToXElements(node, ns.Subnamespaces);
				/*TreeNode node = new TreeNode(aNamespace.NameOnLevel);
        nodeCollection.Add(node);

        AddNamespaces(node.Nodes, aNamespace.Subnamespaces);
        node.Expand();*/
			}
		}
	}
}
