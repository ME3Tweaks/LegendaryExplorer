using System;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	public class ExsltContext : XsltContext
	{
		private readonly ExsltDatesAndTimes _exsltDatesAndTimes = new ExsltDatesAndTimes();
		private readonly ExsltMath _exsltMath = new ExsltMath();
		private readonly ExsltRandom _exsltRandom = new ExsltRandom();
		private readonly ExsltRegularExpressions _exsltRegularExpressions = new ExsltRegularExpressions();
		private readonly ExsltSets _exsltSets = new ExsltSets();
		private readonly ExsltStrings _exsltStrings = new ExsltStrings();
		private readonly GdnDatesAndTimes _gdnDatesAndTimes = new GdnDatesAndTimes();
		private readonly GdnDynamic _gdnDynamic = new GdnDynamic();
		private readonly GdnMath _gdnMath = new GdnMath();
		private readonly GdnRegularExpressions _gdnRegularExpressions = new GdnRegularExpressions();
		private readonly GdnSets _gdnSets = new GdnSets();
		private readonly GdnStrings _gdnStrings = new GdnStrings();
		private readonly XmlNameTable _nt;
		private ExsltFunctionNamespace _supportedFunctions = ExsltFunctionNamespace.All;

		public ExsltContext(XmlNameTable nt)
			: base((NameTable) nt)
		{
			_nt = nt;
			AddExtensionNamespaces();
		}

		public ExsltContext(NameTable nt, ExsltFunctionNamespace supportedFunctions)
			: this(nt)
		{
			SupportedFunctions = supportedFunctions;
		}

		public ExsltFunctionNamespace SupportedFunctions
		{
			set
			{
				if (Enum.IsDefined(typeof (ExsltFunctionNamespace), value))
				{
					_supportedFunctions = value;
				}
			}
			get { return _supportedFunctions; }
		}

		public override bool Whitespace
		{
			get { return true; }
		}

		private void AddExtensionNamespaces()
		{
			//remove all our extension objects in case the ExsltContext is being reused            
			RemoveNamespace("math", ExsltNamespaces.Math);
			RemoveNamespace("date", ExsltNamespaces.DatesAndTimes);
			RemoveNamespace("regexp", ExsltNamespaces.RegularExpressions);
			RemoveNamespace("str", ExsltNamespaces.Strings);
			RemoveNamespace("set", ExsltNamespaces.Sets);
			RemoveNamespace("random", ExsltNamespaces.Random);
			RemoveNamespace("date2", ExsltNamespaces.GdnDatesAndTimes);
			RemoveNamespace("math2", ExsltNamespaces.GdnMath);
			RemoveNamespace("regexp2", ExsltNamespaces.GdnRegularExpressions);
			RemoveNamespace("set2", ExsltNamespaces.GdnSets);
			RemoveNamespace("str2", ExsltNamespaces.GdnStrings);
			RemoveNamespace("dyn2", ExsltNamespaces.GdnDynamic);

			//add extension objects as specified by SupportedFunctions            
			if ((SupportedFunctions & ExsltFunctionNamespace.Math) > 0)
			{
				AddNamespace("math", ExsltNamespaces.Math);
			}

			if ((SupportedFunctions & ExsltFunctionNamespace.DatesAndTimes) > 0)
			{
				AddNamespace("date", ExsltNamespaces.DatesAndTimes);
			}

			if ((SupportedFunctions & ExsltFunctionNamespace.RegularExpressions) > 0)
			{
				AddNamespace("regexp", ExsltNamespaces.RegularExpressions);
			}

			if ((SupportedFunctions & ExsltFunctionNamespace.Strings) > 0)
			{
				AddNamespace("str", ExsltNamespaces.Strings);
			}

			if ((SupportedFunctions & ExsltFunctionNamespace.Sets) > 0)
			{
				AddNamespace("set", ExsltNamespaces.Sets);
			}

			if ((SupportedFunctions & ExsltFunctionNamespace.Random) > 0)
			{
				AddNamespace("random", ExsltNamespaces.Random);
			}

			if ((SupportedFunctions & ExsltFunctionNamespace.GdnDatesAndTimes) > 0)
			{
				AddNamespace("date2", ExsltNamespaces.GdnDatesAndTimes);
			}

			if ((SupportedFunctions & ExsltFunctionNamespace.GdnMath) > 0)
			{
				AddNamespace("math2", ExsltNamespaces.GdnMath);
			}

			if ((SupportedFunctions & ExsltFunctionNamespace.GdnRegularExpressions) > 0)
			{
				AddNamespace("regexp2", ExsltNamespaces.GdnRegularExpressions);
			}

			if ((SupportedFunctions & ExsltFunctionNamespace.GdnSets) > 0)
			{
				AddNamespace("set2", ExsltNamespaces.GdnSets);
			}

			if ((SupportedFunctions & ExsltFunctionNamespace.GdnStrings) > 0)
			{
				AddNamespace("str2", ExsltNamespaces.GdnStrings);
			}

			if ((SupportedFunctions & ExsltFunctionNamespace.GdnDynamic) > 0)
			{
				AddNamespace("dyn2", ExsltNamespaces.GdnDynamic);
			}
		}

		public override int CompareDocument(string baseUri, string nextbaseUri)
		{
			return 0;
		}

		public static XPathResultType ConvertToXPathType(Type type)
		{
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Boolean:
					return XPathResultType.Boolean;
				case TypeCode.String:
					return XPathResultType.String;
				case TypeCode.Object:
					if (typeof (IXPathNavigable).IsAssignableFrom(type) ||
						typeof (XPathNavigator).IsAssignableFrom(type))
					{
						return XPathResultType.Navigator;
					}
					return typeof (XPathNodeIterator).IsAssignableFrom(type) ? XPathResultType.NodeSet : XPathResultType.Any;
				case TypeCode.DateTime:
				case TypeCode.DBNull:
				case TypeCode.Empty:
					return XPathResultType.Error;
				default:
					return XPathResultType.Number;
			}
		}

		private ExsltContextFunction GetExtensionFunctionImplementation(object obj, string name, XPathResultType[] argTypes)
		{
			//For each method in object's type
			foreach (var mi in obj.GetType().GetMethods())
			{
				//We are interested in methods with given name
				if (mi.Name != name)
				{
					continue;
				}
				var parameters = mi.GetParameters();
				////We are interested in methods with given number of arguments
				if (parameters.Length == argTypes.Length)
				{
					var mismatch =
						parameters.Select(pi => ConvertToXPathType(pi.ParameterType)).Where(
							(paramType, i) => paramType != XPathResultType.Any && paramType != argTypes[i]).Any();
					//Now let's check out if parameter types are compatible with actual ones
					if (!mismatch)
					{
						//Create lightweight wrapper around method info
						return new ExsltContextFunction(mi, argTypes, obj);
					}
				}
			}
			throw new XPathException("Extension function not found: " + name, null);
		}

		public override bool PreserveWhitespace(XPathNavigator node)
		{
			return true;
		}

		public override IXsltContextFunction ResolveFunction(string prefix, string name,
			XPathResultType[] argTypes)
		{
			switch (LookupNamespace(_nt.Get(prefix)))
			{
				case ExsltNamespaces.DatesAndTimes:
					return GetExtensionFunctionImplementation(_exsltDatesAndTimes, name, argTypes);
				case ExsltNamespaces.Math:
					return GetExtensionFunctionImplementation(_exsltMath, name, argTypes);
				case ExsltNamespaces.RegularExpressions:
					return GetExtensionFunctionImplementation(_exsltRegularExpressions, name, argTypes);
				case ExsltNamespaces.Sets:
					return GetExtensionFunctionImplementation(_exsltSets, name, argTypes);
				case ExsltNamespaces.Strings:
					return GetExtensionFunctionImplementation(_exsltStrings, name, argTypes);
				case ExsltNamespaces.Random:
					return GetExtensionFunctionImplementation(_exsltRandom, name, argTypes);
				case ExsltNamespaces.GdnDatesAndTimes:
					return GetExtensionFunctionImplementation(_gdnDatesAndTimes, name, argTypes);
				case ExsltNamespaces.GdnMath:
					return GetExtensionFunctionImplementation(_gdnMath, name, argTypes);
				case ExsltNamespaces.GdnRegularExpressions:
					return GetExtensionFunctionImplementation(_gdnRegularExpressions, name, argTypes);
				case ExsltNamespaces.GdnSets:
					return GetExtensionFunctionImplementation(_gdnSets, name, argTypes);
				case ExsltNamespaces.GdnStrings:
					return GetExtensionFunctionImplementation(_gdnStrings, name, argTypes);
				case ExsltNamespaces.GdnDynamic:
					return GetExtensionFunctionImplementation(_gdnDynamic, name, argTypes);
				default:
					throw new XPathException(string.Format("Unrecognized extension function namespace: prefix='{0}', namespace URI='{1}'",
						prefix, LookupNamespace(_nt.Get(prefix))), null);
			}
		}

		public override IXsltContextVariable ResolveVariable(string prefix, string name)
		{
			return null;
		}
	}
}
