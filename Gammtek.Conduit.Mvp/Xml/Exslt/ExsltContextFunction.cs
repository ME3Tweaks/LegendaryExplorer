using System.Reflection;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	internal class ExsltContextFunction : IXsltContextFunction
	{
		private readonly XPathResultType[] _argTypes;
		private readonly MethodInfo _method;
		private readonly object _ownerObj;

		public ExsltContextFunction(MethodInfo mi, XPathResultType[] argTypes,
			object owner)
		{
			_method = mi;
			_argTypes = argTypes;
			_ownerObj = owner;
		}

		public int Minargs
		{
			get { return _argTypes.Length; }
		}

		public int Maxargs
		{
			get { return _argTypes.Length; }
		}

		public XPathResultType[] ArgTypes
		{
			get { return _argTypes; }
		}

		public XPathResultType ReturnType
		{
			get { return ExsltContext.ConvertToXPathType(_method.ReturnType); }
		}

		public object Invoke(XsltContext xsltContext, object[] args,
			XPathNavigator docContext)
		{
			return _method.Invoke(_ownerObj, args);
		}
	}
}
