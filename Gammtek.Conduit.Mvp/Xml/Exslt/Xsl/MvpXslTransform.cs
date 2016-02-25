using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using Gammtek.Conduit.Mvp.Xml.Exslt;

namespace Gammtek.Conduit.Mvp.Xml.Common.Xsl
{
	public class MvpXslTransform : IXmlTransform
	{
		protected static XmlReaderSettings DefaultReaderSettings;

		private readonly object _sync = new object();

		private Dictionary<char, string> _characterMap;

		private ExsltFunctionNamespace _supportedFunctions = ExsltFunctionNamespace.All;

		static MvpXslTransform()
		{
			DefaultReaderSettings = new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Parse
			};
		}

		public MvpXslTransform()
		{
			CompiledTransform = new XslCompiledTransform();
		}

		public MvpXslTransform(bool debug)
		{
			CompiledTransform = new XslCompiledTransform(debug);
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

		public bool MultiOutput { get; set; }

		public bool EnforceXHtmlOutput { get; set; }

		public bool SupportCharacterMaps { get; set; }

		public TempFileCollection TemporaryFiles
		{
			get { return CompiledTransform.TemporaryFiles; }
		}

		internal XslCompiledTransform CompiledTransform { get; private set; }

		public XmlOutput Transform(XmlInput input, XsltArgumentList arguments, XmlOutput output)
		{
			if (input == null)
			{
				throw new ArgumentNullException("input");
			}
			var xmlWriter = output.Destination as XmlWriter;
			var closeWriter = false;
			if (xmlWriter == null)
			{
				closeWriter = true;
				while (true)
				{
					var txtWriter = output.Destination as TextWriter;
					if (txtWriter != null)
					{
						if (MultiOutput)
						{
							var mw = new MultiXmlTextWriter(txtWriter, output.XmlResolver);

							if (CompiledTransform.OutputSettings.Indent)
							{
								mw.Formatting = Formatting.Indented;
							}
							xmlWriter = mw;
						}
						else
						{
							xmlWriter = XmlWriter.Create(txtWriter, CompiledTransform.OutputSettings);
						}
						break;
					}
					var strm = output.Destination as Stream;
					if (strm != null)
					{
						if (MultiOutput)
						{
							var mw = new MultiXmlTextWriter(strm, CompiledTransform.OutputSettings.Encoding, output.XmlResolver);
							if (CompiledTransform.OutputSettings.Indent)
							{
								mw.Formatting = Formatting.Indented;
							}
							xmlWriter = mw;
						}
						else
						{
							xmlWriter = XmlWriter.Create(strm, CompiledTransform.OutputSettings);
						}
						break;
					}
					var str = output.Destination as String;
					if (str != null)
					{
						if (MultiOutput)
						{
							var mw = new MultiXmlTextWriter(str, CompiledTransform.OutputSettings.Encoding);
							if (CompiledTransform.OutputSettings.Indent)
							{
								mw.Formatting = Formatting.Indented;
							}
							xmlWriter = mw;
						}
						else
						{
							var outputSettings = CompiledTransform.OutputSettings.Clone();
							outputSettings.CloseOutput = true;
							// BugBug: We should read doc before creating output file in case they are the same
							xmlWriter = XmlWriter.Create(str, outputSettings);
						}
						break;
					}
					throw new Exception("Unexpected XmlOutput");
				}
			}
			try
			{
				TransformToWriter(input, arguments, xmlWriter);
			}
			finally
			{
				if (closeWriter)
				{
					xmlWriter.Close();
				}
			}
			return output;
		}

		public XmlWriterSettings OutputSettings
		{
			get { return CompiledTransform != null ? CompiledTransform.OutputSettings : new XmlWriterSettings(); }
		}

		public static XsltArgumentList AddExsltExtensionObjects(XsltArgumentList list)
		{
			return AddExsltExtensionObjects(list, ExsltFunctionNamespace.All);
		}

		public static XsltArgumentList AddExsltExtensionObjects(XsltArgumentList list, ExsltFunctionNamespace supportedFunctions)
		{
			if (list == null)
			{
				list = new XsltArgumentList();
			}

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

			if ((supportedFunctions & ExsltFunctionNamespace.Math) > 0)
			{
				list.AddExtensionObject(ExsltNamespaces.Math, new ExsltMath());
			}

			if ((supportedFunctions & ExsltFunctionNamespace.Random) > 0)
			{
				list.AddExtensionObject(ExsltNamespaces.Random, new ExsltRandom());
			}

			if ((supportedFunctions & ExsltFunctionNamespace.DatesAndTimes) > 0)
			{
				list.AddExtensionObject(ExsltNamespaces.DatesAndTimes, new ExsltDatesAndTimes());
			}

			if ((supportedFunctions & ExsltFunctionNamespace.RegularExpressions) > 0)
			{
				list.AddExtensionObject(ExsltNamespaces.RegularExpressions, new ExsltRegularExpressions());
			}

			if ((supportedFunctions & ExsltFunctionNamespace.Strings) > 0)
			{
				list.AddExtensionObject(ExsltNamespaces.Strings, new ExsltStrings());
			}

			if ((supportedFunctions & ExsltFunctionNamespace.Sets) > 0)
			{
				list.AddExtensionObject(ExsltNamespaces.Sets, new ExsltSets());
			}

			if ((supportedFunctions & ExsltFunctionNamespace.GdnDatesAndTimes) > 0)
			{
				list.AddExtensionObject(ExsltNamespaces.GdnDatesAndTimes, new GdnDatesAndTimes());
			}

			if ((supportedFunctions & ExsltFunctionNamespace.GdnMath) > 0)
			{
				list.AddExtensionObject(ExsltNamespaces.GdnMath, new GdnMath());
			}

			if ((supportedFunctions & ExsltFunctionNamespace.GdnRegularExpressions) > 0)
			{
				list.AddExtensionObject(ExsltNamespaces.GdnRegularExpressions, new GdnRegularExpressions());
			}

			if ((supportedFunctions & ExsltFunctionNamespace.GdnSets) > 0)
			{
				list.AddExtensionObject(ExsltNamespaces.GdnSets, new GdnSets());
			}

			if ((supportedFunctions & ExsltFunctionNamespace.GdnStrings) > 0)
			{
				list.AddExtensionObject(ExsltNamespaces.GdnStrings, new GdnStrings());
			}

			if ((supportedFunctions & ExsltFunctionNamespace.GdnDynamic) > 0)
			{
				list.AddExtensionObject(ExsltNamespaces.GdnDynamic, new GdnDynamic());
			}

			return list;
		}

		protected XsltArgumentList AddExsltExtensionObjectsSync(XsltArgumentList list)
		{
			lock (_sync)
			{
				list = AddExsltExtensionObjects(list, SupportedFunctions);
			}
			return list;
		}

		protected XmlReaderSettings GetReaderSettings(XmlInput defaultDocument)
		{
			if (defaultDocument.Resolver is DefaultXmlResolver)
			{
				return DefaultReaderSettings;
			}
			var settings = DefaultReaderSettings.Clone();
			settings.XmlResolver = defaultDocument.Resolver;
			return settings;
		}

		public void Load(IXPathNavigable stylesheet)
		{
			var xPathNavigator = stylesheet.CreateNavigator();
			if (xPathNavigator != null)
			{
				LoadStylesheetFromReader(xPathNavigator.ReadSubtree());
			}
		}

		public void Load(string stylesheetUri)
		{
			LoadStylesheetFromReader(XmlReader.Create(stylesheetUri));
		}

		public void Load(XmlReader stylesheet)
		{
			LoadStylesheetFromReader(stylesheet);
		}

		public void Load(IXPathNavigable stylesheet, XsltSettings settings, XmlResolver stylesheetResolver)
		{
			var xPathNavigator = stylesheet.CreateNavigator();
			if (xPathNavigator != null)
			{
				LoadStylesheetFromReader(xPathNavigator.ReadSubtree(), settings, stylesheetResolver);
			}
		}

		public void Load(string stylesheetUri, XsltSettings settings, XmlResolver stylesheetResolver)
		{
			var readerSettings = new XmlReaderSettings
			{
				XmlResolver = stylesheetResolver
			};

			using (var reader = XmlReader.Create(stylesheetUri, readerSettings))
			{
				LoadStylesheetFromReader(reader, settings, stylesheetResolver);
			}
		}

		public void Load(XmlReader stylesheet, XsltSettings settings, XmlResolver stylesheetResolver)
		{
			LoadStylesheetFromReader(stylesheet, settings, stylesheetResolver);
		}

		protected void LoadStylesheetFromReader(XmlReader reader)
		{
			LoadStylesheetFromReader(reader, XsltSettings.Default, new XmlUrlResolver());
		}

		protected void LoadStylesheetFromReader(XmlReader reader, XsltSettings settings, XmlResolver resolver)
		{
			if (SupportCharacterMaps)
			{
				var cmr = new CharacterMappingXmlReader(reader);
				CompiledTransform.Load(cmr, settings, resolver);
				_characterMap = cmr.CompileCharacterMapping();
			}
			else
			{
				CompiledTransform.Load(reader, settings, resolver);
			}
		}

		public XmlReader Transform(XmlInput input, XsltArgumentList arguments)
		{
			var r = new XslReader(CompiledTransform);
			r.StartTransform(input, AddExsltExtensionObjectsSync(arguments));
			return r;
		}

		public XmlReader Transform(XmlInput input, XsltArgumentList arguments, bool multiThread, int initialBufferSize)
		{
			var r = new XslReader(CompiledTransform, multiThread, initialBufferSize);
			r.StartTransform(input, AddExsltExtensionObjectsSync(arguments));
			return r;
		}

		protected void TransformToWriter(XmlInput defaultDocument, XsltArgumentList xsltArgs, XmlWriter targetWriter)
		{
			XmlWriter xmlWriter;
			if (SupportCharacterMaps && _characterMap != null && _characterMap.Count > 0)
			{
				xmlWriter = new CharacterMappingXmlWriter(targetWriter, _characterMap);
			}
			else
			{
				xmlWriter = targetWriter;
			}
			if (EnforceXHtmlOutput)
			{
				xmlWriter = new XhtmlWriter(xmlWriter);
			}
			var args = AddExsltExtensionObjectsSync(xsltArgs);
			var xmlReader = defaultDocument.Source as XmlReader;
			if (xmlReader != null)
			{
				CompiledTransform.Transform(xmlReader, args, xmlWriter, defaultDocument.Resolver);
				return;
			}
			var nav = defaultDocument.Source as IXPathNavigable;
			if (nav != null)
			{
				if (defaultDocument.Resolver is DefaultXmlResolver)
				{
					CompiledTransform.Transform(nav, args, xmlWriter);
				}
				else
				{
					TransformXPathNavigable(nav, args, xmlWriter, defaultDocument.Resolver);
				}
				return;
			}
			var str = defaultDocument.Source as string;
			if (str != null)
			{
				using (var reader = XmlReader.Create(str, GetReaderSettings(defaultDocument)))
				{
					CompiledTransform.Transform(reader, args, xmlWriter, defaultDocument.Resolver);
				}
				return;
			}
			var strm = defaultDocument.Source as Stream;
			if (strm != null)
			{
				using (var reader = XmlReader.Create(strm, GetReaderSettings(defaultDocument)))
				{
					CompiledTransform.Transform(reader, args, xmlWriter, defaultDocument.Resolver);
				}
				return;
			}
			var txtReader = defaultDocument.Source as TextReader;
			if (txtReader != null)
			{
				using (var reader = XmlReader.Create(txtReader, GetReaderSettings(defaultDocument)))
				{
					CompiledTransform.Transform(reader, args, xmlWriter, defaultDocument.Resolver);
				}
				return;
			}
			throw new Exception("Unexpected XmlInput");
		}

		protected void TransformXPathNavigable(IXPathNavigable nav, XsltArgumentList args, XmlWriter xmlWriter, XmlResolver resolver)
		{
			var fieldInfo = CompiledTransform.GetType().GetField(
				"command", BindingFlags.Instance | BindingFlags.NonPublic);
			if (fieldInfo == null)
			{
				return;
			}
			var command = fieldInfo.GetValue(CompiledTransform);
			var executeMethod = command.GetType().GetMethod("Execute", BindingFlags.Instance | BindingFlags.Public,
				null, new[] { typeof (IXPathNavigable), typeof (XmlResolver), typeof (XsltArgumentList), typeof (XmlWriter) }, null);
			executeMethod.Invoke(command,
				new object[] { nav, resolver, AddExsltExtensionObjectsSync(args), xmlWriter });
		}
	}
}
