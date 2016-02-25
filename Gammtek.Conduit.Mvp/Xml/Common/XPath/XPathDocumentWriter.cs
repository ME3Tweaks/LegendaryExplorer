using System;
using System.IO;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Xml;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.Common.XPath
{
	public class XPathDocumentWriter : XmlWrappingWriter
	{
		private static readonly ConstructorInfo DefaultConstructor;
		private static readonly MethodInfo LoadWriterMethod;

		private readonly XPathDocument _document;
		private bool _hasRoot;

		static XPathDocumentWriter()
		{
			var perm = new ReflectionPermission(PermissionState.Unrestricted)
			{
				Flags = ReflectionPermissionFlag.MemberAccess
			};
			try
			{
				perm.Assert();
				var t = typeof (XPathDocument);
				DefaultConstructor = t.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, new ParameterModifier[0]);
				LoadWriterMethod = t.GetMethod("LoadFromWriter", BindingFlags.NonPublic | BindingFlags.Instance);
				CodeAccessPermission.RevertAssert();
			}
			catch
			{
				CodeAccessPermission.RevertAssert();
				throw;
			}
		}

		public XPathDocumentWriter()
			: this(String.Empty) {}

		public XPathDocumentWriter(string baseUri)
			: base(Create(new StringWriter()))
		{
			Guard.ArgumentNotNull(baseUri, "baseUri");

			_document = CreateDocument();
			BaseWriter = GetWriter(_document, baseUri);
		}

		public override WriteState WriteState
		{
			get { return WriteState.Start; }
		}

		public new XPathDocument Close()
		{
			if (!_hasRoot)
			{
				throw new XmlException(Resources.Xml_MissingRoot);
			}

			base.Close();
			return _document;
		}

		private static XPathDocument CreateDocument()
		{
			return (XPathDocument) DefaultConstructor.Invoke(new object[0]);
		}

		protected override void Dispose(bool disposing)
		{
			Close();
			//base.Dispose(disposing);
		}

		private static XmlWriter GetWriter(XPathDocument document, string baseUri)
		{
			return (XmlWriter) LoadWriterMethod.Invoke(document, new object[] { 0, baseUri });
		}

		public override void WriteStartElement(string prefix, string localName, string ns)
		{
			base.WriteStartElement(prefix, localName, ns);
			if (!_hasRoot)
			{
				_hasRoot = true;
			}
		}
	}
}
