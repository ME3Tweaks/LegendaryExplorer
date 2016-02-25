using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	[Obsolete("This class has been deprecated. Please use Mvp.Xml.Common.Xsl.MvpXslTransform instead.")]
	public class ExsltTransform
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
		private readonly object _sync = new object();
		private readonly XslCompiledTransform _xslTransform;
		private bool _multiOutput;
		private ExsltFunctionNamespace _supportedFunctions = ExsltFunctionNamespace.All;

		public ExsltTransform()
		{
			_xslTransform = new XslCompiledTransform();
		}

		public ExsltTransform(bool debug)
		{
			_xslTransform = new XslCompiledTransform(debug);
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

		public bool MultiOutput
		{
			get { return _multiOutput; }
			set { _multiOutput = value; }
		}

		public XmlWriterSettings OutputSettings
		{
			get { return _xslTransform.OutputSettings; }
		}

		public TempFileCollection TemporaryFiles
		{
			get { return _xslTransform.TemporaryFiles; }
		}

		private XsltArgumentList AddExsltExtensionObjects(XsltArgumentList list)
		{
			if (list == null)
			{
				list = new XsltArgumentList();
			}

			lock (_sync)
			{
				//remove all our extension objects in case the XSLT argument list is being reused                
				list.RemoveExtensionObject(ExsltNamespaces.Math);
				list.RemoveExtensionObject(ExsltNamespaces.Random);
				list.RemoveExtensionObject(ExsltNamespaces.DatesAndTimes);
				list.RemoveExtensionObject(ExsltNamespaces.RegularExpressions);
				list.RemoveExtensionObject(ExsltNamespaces.Strings);
				list.RemoveExtensionObject(ExsltNamespaces.Sets);
				list.RemoveExtensionObject(ExsltNamespaces.GdnDatesAndTimes);
				list.RemoveExtensionObject(ExsltNamespaces.GdnMath);
				list.RemoveExtensionObject(ExsltNamespaces.GdnRegularExpressions);
				list.RemoveExtensionObject(ExsltNamespaces.GdnSets);
				list.RemoveExtensionObject(ExsltNamespaces.GdnStrings);
				list.RemoveExtensionObject(ExsltNamespaces.GdnDynamic);

				//add extension objects as specified by SupportedFunctions                

				if ((SupportedFunctions & ExsltFunctionNamespace.Math) > 0)
				{
					list.AddExtensionObject(ExsltNamespaces.Math, _exsltMath);
				}

				if ((SupportedFunctions & ExsltFunctionNamespace.Random) > 0)
				{
					list.AddExtensionObject(ExsltNamespaces.Random, _exsltRandom);
				}

				if ((SupportedFunctions & ExsltFunctionNamespace.DatesAndTimes) > 0)
				{
					list.AddExtensionObject(ExsltNamespaces.DatesAndTimes, _exsltDatesAndTimes);
				}

				if ((SupportedFunctions & ExsltFunctionNamespace.RegularExpressions) > 0)
				{
					list.AddExtensionObject(ExsltNamespaces.RegularExpressions, _exsltRegularExpressions);
				}

				if ((SupportedFunctions & ExsltFunctionNamespace.Strings) > 0)
				{
					list.AddExtensionObject(ExsltNamespaces.Strings, _exsltStrings);
				}

				if ((SupportedFunctions & ExsltFunctionNamespace.Sets) > 0)
				{
					list.AddExtensionObject(ExsltNamespaces.Sets, _exsltSets);
				}

				if ((SupportedFunctions & ExsltFunctionNamespace.GdnDatesAndTimes) > 0)
				{
					list.AddExtensionObject(ExsltNamespaces.GdnDatesAndTimes, _gdnDatesAndTimes);
				}

				if ((SupportedFunctions & ExsltFunctionNamespace.GdnMath) > 0)
				{
					list.AddExtensionObject(ExsltNamespaces.GdnMath, _gdnMath);
				}

				if ((SupportedFunctions & ExsltFunctionNamespace.GdnRegularExpressions) > 0)
				{
					list.AddExtensionObject(ExsltNamespaces.GdnRegularExpressions, _gdnRegularExpressions);
				}

				if ((SupportedFunctions & ExsltFunctionNamespace.GdnSets) > 0)
				{
					list.AddExtensionObject(ExsltNamespaces.GdnSets, _gdnSets);
				}

				if ((SupportedFunctions & ExsltFunctionNamespace.GdnStrings) > 0)
				{
					list.AddExtensionObject(ExsltNamespaces.GdnStrings, _gdnStrings);
				}

				if ((SupportedFunctions & ExsltFunctionNamespace.GdnDynamic) > 0)
				{
					list.AddExtensionObject(ExsltNamespaces.GdnDynamic, _gdnDynamic);
				}
			}

			return list;
		}

		public void Load(IXPathNavigable stylesheet)
		{
			_xslTransform.Load(stylesheet);
		}

		public void Load(string stylesheetUri)
		{
			_xslTransform.Load(stylesheetUri);
		}

		public void Load(XmlReader stylesheet)
		{
			_xslTransform.Load(stylesheet);
		}

		public void Load(IXPathNavigable stylesheet, XsltSettings settings, XmlResolver stylesheetResolver)
		{
			_xslTransform.Load(stylesheet, settings, stylesheetResolver);
		}

		public void Load(string stylesheetUri, XsltSettings settings, XmlResolver stylesheetResolver)
		{
			_xslTransform.Load(stylesheetUri, settings, stylesheetResolver);
		}

		public void Load(XmlReader stylesheet, XsltSettings settings, XmlResolver stylesheetResolver)
		{
			_xslTransform.Load(stylesheet, settings, stylesheetResolver);
		}

		public void Transform(IXPathNavigable input, XmlWriter results)
		{
			_xslTransform.Transform(input, AddExsltExtensionObjects(null), results);
		}

		public void Transform(string inputUri, string resultsFile)
		{
			// Use using so that the file is not held open after the call
			using (var outStream = File.OpenWrite(resultsFile))
			{
				if (_multiOutput)
				{
					_xslTransform.Transform(new XPathDocument(inputUri),
						AddExsltExtensionObjects(null),
						new MultiXmlTextWriter(outStream, OutputSettings.Encoding));
				}
				else
				{
					_xslTransform.Transform(new XPathDocument(inputUri),
						AddExsltExtensionObjects(null),
						outStream);
				}
			}
		}

		public void Transform(string inputUri, XmlWriter results)
		{
			_xslTransform.Transform(inputUri,
				AddExsltExtensionObjects(null), results);
		}

		public void Transform(XmlReader input, XmlWriter results)
		{
			_xslTransform.Transform(input,
				AddExsltExtensionObjects(null), results);
		}

		public void Transform(IXPathNavigable input, XsltArgumentList arguments, Stream results)
		{
			if (_multiOutput)
			{
				_xslTransform.Transform(input,
					AddExsltExtensionObjects(arguments),
					new MultiXmlTextWriter(results, OutputSettings.Encoding));
			}
			else
			{
				_xslTransform.Transform(input,
					AddExsltExtensionObjects(arguments), results);
			}
		}

		public void Transform(IXPathNavigable input, XsltArgumentList arguments, TextWriter results)
		{
			if (_multiOutput)
			{
				_xslTransform.Transform(input,
					AddExsltExtensionObjects(arguments),
					new MultiXmlTextWriter(results));
			}
			else
			{
				_xslTransform.Transform(input,
					AddExsltExtensionObjects(arguments), results);
			}
		}

		public void Transform(IXPathNavigable input, XsltArgumentList arguments, XmlWriter results)
		{
			_xslTransform.Transform(input,
				AddExsltExtensionObjects(arguments), results);
		}

		public void Transform(string inputUri, XsltArgumentList arguments, Stream results)
		{
			if (_multiOutput)
			{
				_xslTransform.Transform(inputUri,
					AddExsltExtensionObjects(arguments),
					new MultiXmlTextWriter(results, OutputSettings.Encoding));
			}
			else
			{
				_xslTransform.Transform(inputUri,
					AddExsltExtensionObjects(arguments), results);
			}
		}

		public void Transform(string inputUri, XsltArgumentList arguments, TextWriter results)
		{
			if (_multiOutput)
			{
				_xslTransform.Transform(inputUri,
					AddExsltExtensionObjects(arguments),
					new MultiXmlTextWriter(results));
			}
			else
			{
				_xslTransform.Transform(inputUri,
					AddExsltExtensionObjects(arguments), results);
			}
		}

		public void Transform(string inputUri, XsltArgumentList arguments, XmlWriter results)
		{
			_xslTransform.Transform(inputUri,
				AddExsltExtensionObjects(arguments), results);
		}

		public void Transform(XmlReader input, XsltArgumentList arguments, Stream results)
		{
			if (_multiOutput)
			{
				_xslTransform.Transform(input,
					AddExsltExtensionObjects(arguments),
					new MultiXmlTextWriter(results, OutputSettings.Encoding));
			}
			else
			{
				_xslTransform.Transform(input,
					AddExsltExtensionObjects(arguments), results);
			}
		}

		public void Transform(XmlReader input, XsltArgumentList arguments, TextWriter results)
		{
			if (_multiOutput)
			{
				_xslTransform.Transform(input,
					AddExsltExtensionObjects(arguments),
					new MultiXmlTextWriter(results));
			}
			else
			{
				_xslTransform.Transform(input,
					AddExsltExtensionObjects(arguments), results);
			}
		}

		public void Transform(XmlReader input, XsltArgumentList arguments, XmlWriter results)
		{
			_xslTransform.Transform(input,
				AddExsltExtensionObjects(arguments), results);
		}

		public void Transform(XmlReader input, XsltArgumentList arguments,
			XmlWriter results, XmlResolver documentResolver)
		{
			_xslTransform.Transform(input,
				AddExsltExtensionObjects(arguments), results, documentResolver);
		}
	}
}
