using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.Common.XPath
{
	public static class XPathCache
	{
		//static IDictionary<string, XPathExpression> _cache = new Dictionary<string, XPathExpression>();

		static XPathCache()
		{
			Cache = new Dictionary<string, XPathExpression>();
		}

		private static Dictionary<string, XPathExpression> Cache { get; set; }

		public static object Evaluate(string expression, XPathNavigator source)
		{
			return source.Evaluate(GetCompiledExpression(expression, source));
		}

		public static object Evaluate(string expression, XPathNavigator source,
			params XPathVariable[] variables)
		{
			var expr = GetCompiledExpression(expression, source);
			expr.SetContext(PrepareContext(source, null, null, variables));
			return source.Evaluate(expr);
		}

		public static object Evaluate(string expression, XPathNavigator source,
			XmlNamespaceManager context)
		{
			var expr = GetCompiledExpression(expression, source);
			expr.SetContext(context);
			return source.Evaluate(expr);
		}

		public static object Evaluate(string expression, XPathNavigator source,
			params XmlPrefix[] prefixes)
		{
			var expr = GetCompiledExpression(expression, source);
			expr.SetContext(PrepareContext(source, null, prefixes, null));
			return source.Evaluate(expr);
		}

		public static object Evaluate(string expression, XPathNavigator source,
			XmlNamespaceManager context, params XPathVariable[] variables)
		{
			var expr = GetCompiledExpression(expression, source);
			expr.SetContext(PrepareContext(source, context, null, variables));
			return source.Evaluate(expr);
		}

		public static object Evaluate(string expression, XPathNavigator source,
			XmlPrefix[] prefixes, params XPathVariable[] variables)
		{
			var expr = GetCompiledExpression(expression, source);
			expr.SetContext(PrepareContext(source, null, prefixes, variables));
			return source.Evaluate(expr);
		}

		private static XPathExpression GetCompiledExpression(string expression, XPathNavigator source)
		{
			XPathExpression expr;

			if (Cache.TryGetValue(expression, out expr))
			{
				return expr.Clone();
			}

			// No double checks. At most we will compile twice. No big deal.			  
			expr = source.Compile(expression);
			Cache[expression] = expr;

			return expr.Clone();
		}

		private static XmlNamespaceManager PrepareContext(XPathNavigator source,
			XmlNamespaceManager context, IEnumerable<XmlPrefix> prefixes, IEnumerable<XPathVariable> variables)
		{
			var ctx = context;

			// If we have variables, we need the dynamic context. 
			if (variables != null)
			{
				var dyn = ctx != null ? new DynamicContext(ctx) : new DynamicContext();

				// Add the variables we received.
				foreach (var var in variables)
				{
					dyn.AddVariable(var.Name, var.Value);
				}

				ctx = dyn;
			}

			// If prefixes were added, append them to context.
			if (prefixes == null)
			{
				return ctx;
			}
			if (ctx == null && source.NameTable != null)
			{
				ctx = new XmlNamespaceManager(source.NameTable);
			}

			if (ctx == null)
			{
				return null;
			}

			foreach (var prefix in prefixes)
			{
				ctx.AddNamespace(prefix.Prefix, prefix.NamespaceUri);
			}

			return ctx;
		}

		private static void PrepareSort(XPathExpression expression, XPathNavigator source,
			object sortExpression, XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType)
		{
			var s = sortExpression as string;
			if (s != null)
			{
				expression.AddSort(
					GetCompiledExpression(s, source),
					order, caseOrder, lang, dataType);
			}
			else if (sortExpression is XPathExpression)
			{
				expression.AddSort(sortExpression, order, caseOrder, lang, dataType);
			}
			else
			{
				throw new XPathException(Resources.XPathCache_BadSortObject, null);
			}
		}

		private static void PrepareSort(XPathExpression expression, XPathNavigator source, object sortExpression,
			XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType,
			XmlNamespaceManager context)
		{
			XPathExpression se;

			var s = sortExpression as string;
			if (s != null)
			{
				se = GetCompiledExpression(s, source);
			}
			else
			{
				var pathExpression = sortExpression as XPathExpression;
				if (pathExpression != null)
				{
					se = pathExpression;
				}
				else
				{
					throw new XPathException(Resources.XPathCache_BadSortObject, null);
				}
			}

			se.SetContext(context);
			expression.AddSort(se, order, caseOrder, lang, dataType);
		}

		private static void PrepareSort(XPathExpression expression, XPathNavigator source,
			object sortExpression, IComparer comparer)
		{
			var s = sortExpression as string;
			if (s != null)
			{
				expression.AddSort(
					GetCompiledExpression(s, source), comparer);
			}
			else if (sortExpression is XPathExpression)
			{
				expression.AddSort(sortExpression, comparer);
			}
			else
			{
				throw new XPathException(Resources.XPathCache_BadSortObject, null);
			}
		}

		private static void PrepareSort(XPathExpression expression, XPathNavigator source,
			object sortExpression, IComparer comparer, XmlNamespaceManager context)
		{
			XPathExpression se;

			var s = sortExpression as string;
			if (s != null)
			{
				se = GetCompiledExpression(s, source);
			}
			else
			{
				var pathExpression = sortExpression as XPathExpression;
				if (pathExpression != null)
				{
					se = pathExpression;
				}
				else
				{
					throw new XPathException(Resources.XPathCache_BadSortObject, null);
				}
			}

			se.SetContext(context);
			expression.AddSort(se, comparer);
		}

		public static XPathNodeIterator Select(string expression, XPathNavigator source)
		{
			return source.Select(GetCompiledExpression(expression, source));
		}

		public static XPathNodeIterator Select(string expression, XPathNavigator source,
			params XPathVariable[] variables)
		{
			var expr = GetCompiledExpression(expression, source);
			expr.SetContext(PrepareContext(source, null, null, variables));
			return source.Select(expr);
		}

		public static XPathNodeIterator Select(string expression, XPathNavigator source,
			XmlNamespaceManager context)
		{
			var expr = GetCompiledExpression(expression, source);
			expr.SetContext(context);
			return source.Select(expr);
		}

		public static XPathNodeIterator Select(string expression, XPathNavigator source,
			params XmlPrefix[] prefixes)
		{
			var expr = GetCompiledExpression(expression, source);
			expr.SetContext(PrepareContext(source, null, prefixes, null));
			return source.Select(expr);
		}

		public static XPathNodeIterator Select(string expression, XPathNavigator source,
			XmlNamespaceManager context, params XPathVariable[] variables)
		{
			var expr = GetCompiledExpression(expression, source);
			expr.SetContext(PrepareContext(source, context, null, variables));
			return source.Select(expr);
		}

		public static XPathNodeIterator Select(string expression, XPathNavigator source,
			XmlPrefix[] prefixes, params XPathVariable[] variables)
		{
			var expr = GetCompiledExpression(expression, source);
			expr.SetContext(PrepareContext(source, null, prefixes, variables));
			return source.Select(expr);
		}

		public static XmlNodeList SelectNodes(string expression, XmlNode source)
		{
			var it = Select(expression, source.CreateNavigator());
			return XmlNodeListFactory.CreateNodeList(it);
		}

		public static XmlNodeList SelectNodes(string expression, XmlNode source, params XPathVariable[] variables)
		{
			var it = Select(expression, source.CreateNavigator(), variables);
			return XmlNodeListFactory.CreateNodeList(it);
		}

		public static XmlNodeList SelectNodes(string expression, XmlNode source, XmlNamespaceManager context)
		{
			var it = Select(expression, source.CreateNavigator(), context);
			return XmlNodeListFactory.CreateNodeList(it);
		}

		public static XmlNodeList SelectNodes(string expression, XmlNode source, params XmlPrefix[] prefixes)
		{
			var it = Select(expression, source.CreateNavigator(), prefixes);
			return XmlNodeListFactory.CreateNodeList(it);
		}

		public static XmlNodeList SelectNodes(string expression, XmlNode source, XmlNamespaceManager context, params XPathVariable[] variables)
		{
			var it = Select(expression, source.CreateNavigator(), context, variables);
			return XmlNodeListFactory.CreateNodeList(it);
		}

		public static XmlNodeList SelectNodes(string expression, XmlNode source, XmlPrefix[] prefixes, params XPathVariable[] variables)
		{
			var it = Select(expression, source.CreateNavigator(), prefixes, variables);
			return XmlNodeListFactory.CreateNodeList(it);
		}

		public static XmlNodeList SelectNodesSorted(string expression, XmlNode source,
			object sortExpression, IComparer comparer)
		{
			return XmlNodeListFactory.CreateNodeList(
				SelectSorted(expression, source.CreateNavigator(), sortExpression, comparer));
		}

		public static XmlNodeList SelectNodesSorted(string expression, XmlNode source,
			object sortExpression, XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType)
		{
			return XmlNodeListFactory.CreateNodeList(
				SelectSorted(expression, source.CreateNavigator(), sortExpression,
					order, caseOrder, lang, dataType));
		}

		public static XmlNodeList SelectNodesSorted(string expression, XmlNode source,
			object sortExpression, IComparer comparer, params XPathVariable[] variables)
		{
			return XmlNodeListFactory.CreateNodeList(
				SelectSorted(expression, source.CreateNavigator(), sortExpression,
					comparer, variables));
		}

		public static XmlNodeList SelectNodesSorted(string expression, XmlNode source,
			object sortExpression, XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType,
			params XPathVariable[] variables)
		{
			return XmlNodeListFactory.CreateNodeList(
				SelectSorted(expression, source.CreateNavigator(), sortExpression,
					order, caseOrder, lang, dataType, variables));
		}

		public static XmlNodeList SelectNodesSorted(string expression, XmlNode source,
			object sortExpression, XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType,
			XmlNamespaceManager context)
		{
			return XmlNodeListFactory.CreateNodeList(
				SelectSorted(expression, source.CreateNavigator(), sortExpression,
					order, caseOrder, lang, dataType, context));
		}

		public static XmlNodeList SelectNodesSorted(string expression, XmlNode source,
			object sortExpression, IComparer comparer,
			params XmlPrefix[] prefixes)
		{
			return XmlNodeListFactory.CreateNodeList(
				SelectSorted(expression, source.CreateNavigator(), sortExpression,
					comparer, prefixes));
		}

		public static XmlNodeList SelectNodesSorted(string expression, XmlNode source,
			object sortExpression, XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType,
			params XmlPrefix[] prefixes)
		{
			return XmlNodeListFactory.CreateNodeList(
				SelectSorted(expression, source.CreateNavigator(), sortExpression,
					order, caseOrder, lang, dataType, prefixes));
		}

		public static XmlNodeList SelectNodesSorted(string expression, XmlNode source,
			object sortExpression, XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType,
			XmlNamespaceManager context, params XPathVariable[] variables)
		{
			return XmlNodeListFactory.CreateNodeList(
				SelectSorted(expression, source.CreateNavigator(), sortExpression,
					order, caseOrder, lang, dataType, context, variables));
		}

		public static XmlNodeList SelectNodesSorted(string expression, XmlNode source,
			object sortExpression, IComparer comparer,
			XmlNamespaceManager context, params XPathVariable[] variables)
		{
			return XmlNodeListFactory.CreateNodeList(
				SelectSorted(expression, source.CreateNavigator(), sortExpression,
					comparer, context, variables));
		}

		public static XmlNodeList SelectNodesSorted(string expression, XmlNode source,
			object sortExpression, XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType,
			XmlPrefix[] prefixes, params XPathVariable[] variables)
		{
			return XmlNodeListFactory.CreateNodeList(
				SelectSorted(expression, source.CreateNavigator(), sortExpression,
					order, caseOrder, lang, dataType, prefixes, variables));
		}

		public static XmlNodeList SelectNodesSorted(string expression, XmlNode source,
			object sortExpression, IComparer comparer,
			XmlPrefix[] prefixes, params XPathVariable[] variables)
		{
			return XmlNodeListFactory.CreateNodeList(
				SelectSorted(expression, source.CreateNavigator(), sortExpression,
					comparer, prefixes, variables));
		}

		public static XmlNode SelectSingleNode(string expression, XmlNode source)
		{
			return SelectNodes(expression, source).Cast<XmlNode>().FirstOrDefault();
		}

		public static XmlNode SelectSingleNode(string expression, XmlNode source, params XPathVariable[] variables)
		{
			return SelectNodes(expression, source, variables).Cast<XmlNode>().FirstOrDefault();
		}

		public static XmlNode SelectSingleNode(string expression, XmlNode source, XmlNamespaceManager context)
		{
			return SelectNodes(expression, source, context).Cast<XmlNode>().FirstOrDefault();
		}

		public static XmlNode SelectSingleNode(string expression, XmlNode source, params XmlPrefix[] prefixes)
		{
			return SelectNodes(expression, source, prefixes).Cast<XmlNode>().FirstOrDefault();
		}

		public static XmlNode SelectSingleNode(string expression, XmlNode source, XmlNamespaceManager context, params XPathVariable[] variables)
		{
			return SelectNodes(expression, source, context, variables).Cast<XmlNode>().FirstOrDefault();
		}

		public static XmlNode SelectSingleNode(string expression, XmlNode source, XmlPrefix[] prefixes, params XPathVariable[] variables)
		{
			return SelectNodes(expression, source, prefixes, variables).Cast<XmlNode>().FirstOrDefault();
		}

		public static XPathNodeIterator SelectSorted(string expression, XPathNavigator source,
			object sortExpression, IComparer comparer)
		{
			var expr = GetCompiledExpression(expression, source);
			PrepareSort(expr, source, sortExpression, comparer);
			return source.Select(expr);
		}

		public static XPathNodeIterator SelectSorted(string expression, XPathNavigator source,
			object sortExpression, XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType)
		{
			var expr = GetCompiledExpression(expression, source);
			PrepareSort(expr, source, sortExpression, order, caseOrder, lang, dataType);
			return source.Select(expr);
		}

		public static XPathNodeIterator SelectSorted(string expression, XPathNavigator source,
			object sortExpression, IComparer comparer, params XPathVariable[] variables)
		{
			var expr = GetCompiledExpression(expression, source);
			expr.SetContext(PrepareContext(source, null, null, variables));
			PrepareSort(expr, source, sortExpression, comparer);
			return source.Select(expr);
		}

		public static XPathNodeIterator SelectSorted(string expression, XPathNavigator source,
			object sortExpression, XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType,
			params XPathVariable[] variables)
		{
			var expr = GetCompiledExpression(expression, source);
			expr.SetContext(PrepareContext(source, null, null, variables));
			PrepareSort(expr, source, sortExpression, order, caseOrder, lang, dataType);
			return source.Select(expr);
		}

		public static XPathNodeIterator SelectSorted(string expression, XPathNavigator source,
			object sortExpression, XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType,
			XmlNamespaceManager context)
		{
			var expr = GetCompiledExpression(expression, source);
			expr.SetContext(context);
			PrepareSort(expr, source, sortExpression, order, caseOrder, lang, dataType, context);
			return source.Select(expr);
		}

		public static XPathNodeIterator SelectSorted(string expression, XPathNavigator source,
			object sortExpression, IComparer comparer,
			params XmlPrefix[] prefixes)
		{
			var expr = GetCompiledExpression(expression, source);
			var ctx = PrepareContext(source, null, prefixes, null);
			expr.SetContext(ctx);
			PrepareSort(expr, source, sortExpression, comparer, ctx);
			return source.Select(expr);
		}

		public static XPathNodeIterator SelectSorted(string expression, XPathNavigator source,
			object sortExpression, XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType,
			params XmlPrefix[] prefixes)
		{
			var expr = GetCompiledExpression(expression, source);
			var ctx = PrepareContext(source, null, prefixes, null);
			expr.SetContext(ctx);
			PrepareSort(expr, source, sortExpression, order, caseOrder, lang, dataType, ctx);
			return source.Select(expr);
		}

		public static XPathNodeIterator SelectSorted(string expression, XPathNavigator source,
			object sortExpression, XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType,
			XmlNamespaceManager context, params XPathVariable[] variables)
		{
			var expr = GetCompiledExpression(expression, source);
			var ctx = PrepareContext(source, context, null, variables);
			expr.SetContext(ctx);
			PrepareSort(expr, source, sortExpression, order, caseOrder, lang, dataType, ctx);
			return source.Select(expr);
		}

		public static XPathNodeIterator SelectSorted(string expression, XPathNavigator source,
			object sortExpression, IComparer comparer,
			XmlNamespaceManager context, params XPathVariable[] variables)
		{
			var expr = GetCompiledExpression(expression, source);
			var ctx = PrepareContext(source, context, null, variables);
			expr.SetContext(ctx);
			PrepareSort(expr, source, sortExpression, comparer, ctx);
			return source.Select(expr);
		}

		public static XPathNodeIterator SelectSorted(string expression, XPathNavigator source,
			object sortExpression, XmlSortOrder order, XmlCaseOrder caseOrder, string lang, XmlDataType dataType,
			XmlPrefix[] prefixes, params XPathVariable[] variables)
		{
			var expr = GetCompiledExpression(expression, source);
			var ctx = PrepareContext(source, null, prefixes, variables);
			expr.SetContext(ctx);
			PrepareSort(expr, source, sortExpression, order, caseOrder, lang, dataType, ctx);
			return source.Select(expr);
		}

		public static XPathNodeIterator SelectSorted(string expression, XPathNavigator source,
			object sortExpression, IComparer comparer,
			XmlPrefix[] prefixes, params XPathVariable[] variables)
		{
			var expr = GetCompiledExpression(expression, source);
			var ctx = PrepareContext(source, null, prefixes, variables);
			expr.SetContext(ctx);
			PrepareSort(expr, source, sortExpression, comparer, ctx);
			return source.Select(expr);
		}
	}
}
