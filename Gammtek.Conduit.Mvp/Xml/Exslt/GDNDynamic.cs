using System;
using System.Text.RegularExpressions;
using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	public class GdnDynamic
	{
		public object Evaluate(XPathNodeIterator contextNode, string expression)
		{
			return Evaluate(contextNode, expression, "");
		}

		public object Evaluate(XPathNodeIterator contextNode, string expression, string namespaces)
		{
			if (expression == String.Empty || contextNode == null)
			{
				return String.Empty;
			}
			if (!contextNode.MoveNext())
			{
				return String.Empty;
			}
			try
			{
				var expr = contextNode.Current.Compile(expression);
				var context = new ExsltContext(contextNode.Current.NameTable);
				var node = contextNode.Current.Clone();
				if (node.NodeType != XPathNodeType.Element)
				{
					node.MoveToParent();
				}
				if (node.MoveToFirstNamespace())
				{
					do
					{
						context.AddNamespace(node.Name, node.Value);
					} while (node.MoveToNextNamespace());
				}
				if (namespaces != String.Empty)
				{
					var regexp = new Regex(@"xmlns:(?<p>\w+)\s*=\s*(('(?<n>.+)')|(""(?<n>.+)""))\s*");
					var m = regexp.Match(namespaces);
					while (m.Success)
					{
						context.AddNamespace(m.Groups["p"].Value,
							m.Groups["n"].Value);

						m = m.NextMatch();
					}
				}
				expr.SetContext(context);
				return contextNode.Current.Evaluate(expr, contextNode);
			}
			catch
			{
				//Any exception such as syntax error in XPath
				return String.Empty;
			}
			//Empty nodeset as context node
		}
	}
}
