using System;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	public class XmlFirstUpperReader : XmlTextReader
	{
		public XmlFirstUpperReader(Stream input)
			: base(input) {}

		public XmlFirstUpperReader(TextReader input)
			: base(input) {}

		public XmlFirstUpperReader(string url)
			: base(url) {}

		public XmlFirstUpperReader(Stream input, XmlNameTable nt)
			: base(input, nt) {}

		public XmlFirstUpperReader(TextReader input, XmlNameTable nt)
			: base(input, nt) {}

		public XmlFirstUpperReader(string url, Stream input)
			: base(url, input) {}

		public XmlFirstUpperReader(string url, TextReader input)
			: base(url, input) {}

		public XmlFirstUpperReader(string url, XmlNameTable nt)
			: base(url, nt) {}

		public XmlFirstUpperReader(Stream xmlFragment, XmlNodeType fragType, XmlParserContext context)
			: base(xmlFragment, fragType, context) {}

		public XmlFirstUpperReader(string url, Stream input, XmlNameTable nt)
			: base(url, input, nt) {}

		public XmlFirstUpperReader(string url, TextReader input, XmlNameTable nt)
			: base(url, input, nt) {}

		public XmlFirstUpperReader(string xmlFragment, XmlNodeType fragType, XmlParserContext context)
			: base(xmlFragment, fragType, context) {}

		public override string this[string name, string namespaceUri]
		{
			get
			{
				if (NameTable != null)
				{
					return base[
						NameTable.Add(XmlFirstLowerWriter.MakeFirstLower(name)), namespaceUri];
				}

				return null;
			}
		}

		public override string this[string name]
		{
			get { return this[name, String.Empty]; }
		}

		public override string LocalName
		{
			get
			{
				// Capitalize elements and attributes.
				if (base.NodeType == XmlNodeType.Element ||
					base.NodeType == XmlNodeType.EndElement ||
					base.NodeType == XmlNodeType.Attribute)
				{
					return base.NamespaceURI == XmlNamespaces.XmlNs
						? // Except if the attribute is a namespace declaration
						base.LocalName
						: MakeFirstUpper(base.LocalName);
				}
				return base.LocalName;
			}
		}

		public override string Name
		{
			get
			{
				// Again, if this is a NS declaration, pass as-is.
				if (base.NamespaceURI == XmlNamespaces.XmlNs)
				{
					return base.Name;
				}
				// If there's no prefix, capitalize it directly.
				if (base.Name.IndexOf(':') == -1)
				{
					return MakeFirstUpper(base.Name);
				}
				// Turn local name into upper, not the prefix.
				var name = base.Name.Substring(0, base.Name.IndexOf(':') + 1);
				name += MakeFirstUpper(base.Name.Substring(base.Name.IndexOf(':') + 1));
				return NameTable != null ? NameTable.Add(name) : null;
			}
		}

		private string MakeFirstUpper(string name)
		{
			// Don't process empty strings.
			if (name.Length == 0)
			{
				return name;
			}
			// If the first is already upper, don't process.
			if (Char.IsUpper(name[0]))
			{
				return name;
			}
			// If there's just one char, make it lower directly.
			if (name.Length == 1)
			{
				return name.ToUpper(CultureInfo.CurrentCulture);
			}
			// Finally, modify and create a string. 
			var letters = name.ToCharArray();
			letters[0] = Char.ToUpper(letters[0], CultureInfo.CurrentUICulture);
			return NameTable != null ? NameTable.Add(new string(letters)) : null;
		}

		public override bool MoveToAttribute(string name, string ns)
		{
			return NameTable != null && base.MoveToAttribute(
				NameTable.Add(XmlFirstLowerWriter.MakeFirstLower(name)), ns);
		}
	}
}
