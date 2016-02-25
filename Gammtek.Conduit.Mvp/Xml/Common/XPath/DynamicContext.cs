using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Gammtek.Conduit.Mvp.Xml.Common.XPath
{
	public class DynamicContext : XsltContext
	{
		private readonly IDictionary<string, IXsltContextVariable> _variables =
			new Dictionary<string, IXsltContextVariable>();

		public DynamicContext()
			: base(new NameTable()) {}

		public DynamicContext(NameTable table)
			: base(table) {}

		public DynamicContext(XmlNamespaceManager context)
			: this(context, new NameTable()) {}

		public DynamicContext(XmlNamespaceManager context, NameTable table)
			: base(table)
		{
			object xml = table.Add(XmlNamespaces.Xml);
			object xmlns = table.Add(XmlNamespaces.XmlNs);

			if (context == null)
			{
				return;
			}

			foreach (string prefix in context)
			{
				var uri = context.LookupNamespace(prefix);
				// Use fast object reference comparison to omit forbidden namespace declarations.
				if (Equals(uri, xml) || Equals(uri, xmlns))
				{
					continue;
				}

				if (uri == null)
				{
					continue;
				}

				base.AddNamespace(prefix, uri);
			}
		}

		public override bool Whitespace
		{
			get { return true; }
		}

		public void AddVariable(string name, object value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			_variables[name] = new DynamicVariable(value);
		}

		public override int CompareDocument(string baseUri, string nextbaseUri)
		{
			return String.Compare(baseUri, nextbaseUri, false, CultureInfo.InvariantCulture);
		}

		public static XPathExpression Compile(string xpath)
		{
			return new XmlDocument().CreateNavigator().Compile(xpath);
		}

		public override string LookupNamespace(string prefix)
		{
			var key = NameTable.Get(prefix);
			if (key == null)
			{
				return null;
			}
			return base.LookupNamespace(key);
		}

		public override string LookupPrefix(string uri)
		{
			var key = NameTable.Get(uri);
			return key == null ? null : base.LookupPrefix(key);
		}

		public override bool PreserveWhitespace(XPathNavigator node)
		{
			return true;
		}

		public override IXsltContextFunction ResolveFunction(string prefix, string name, XPathResultType[] argTypes)
		{
			return null;
		}

		public override IXsltContextVariable ResolveVariable(string prefix, string name)
		{
			IXsltContextVariable var;
			_variables.TryGetValue(name, out var);
			return var;
		}

		internal class DynamicVariable : IXsltContextVariable
		{
			private readonly XPathResultType _type;
			private readonly object _value;

			public DynamicVariable(object value)
			{
				_value = value;

				if (value is String)
				{
					_type = XPathResultType.String;
				}
				else if (value is bool)
				{
					_type = XPathResultType.Boolean;
				}
				else if (value is XPathNavigator)
				{
					_type = XPathResultType.Navigator;
				}
				else if (value is XPathNodeIterator)
				{
					_type = XPathResultType.NodeSet;
				}
				else
				{
					// Try to convert to double (native XPath numeric type)
					if (value is double)
					{
						_type = XPathResultType.Number;
					}
					else
					{
						if (value is IConvertible)
						{
							try
							{
								_value = Convert.ToDouble(value);
								// We suceeded, so it's a number.
								_type = XPathResultType.Number;
							}
							catch (FormatException)
							{
								_type = XPathResultType.Any;
							}
							catch (OverflowException)
							{
								_type = XPathResultType.Any;
							}
						}
						else
						{
							_type = XPathResultType.Any;
						}
					}
				}
			}

			XPathResultType IXsltContextVariable.VariableType
			{
				get { return _type; }
			}

			object IXsltContextVariable.Evaluate(XsltContext context)
			{
				return _value;
			}

			bool IXsltContextVariable.IsLocal
			{
				get { return false; }
			}

			bool IXsltContextVariable.IsParam
			{
				get { return false; }
			}
		}
	}
}
