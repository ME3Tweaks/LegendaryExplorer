using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	public class XmlFirstLowerWriter : XmlTextWriter
	{
		public XmlFirstLowerWriter(TextWriter w)
			: base(w) {}

		public XmlFirstLowerWriter(Stream w, Encoding encoding)
			: base(w, encoding) {}

		public XmlFirstLowerWriter(string filename, Encoding encoding)
			: base(filename, encoding) {}

		internal static string MakeFirstLower(string name)
		{
			// Don't process empty strings.
			if (name.Length == 0)
			{
				return name;
			}
			// If the first is already lower, don't process.
			if (Char.IsLower(name[0]))
			{
				return name;
			}
			// If there's just one char, make it lower directly.
			if (name.Length == 1)
			{
				return name.ToLower(CultureInfo.CurrentCulture);
			}
			// Finally, modify and create a string. 
			var letters = name.ToCharArray();
			letters[0] = Char.ToLower(letters[0], CultureInfo.CurrentCulture);
			return new string(letters);
		}

		public override void WriteQualifiedName(string localName, string ns)
		{
			base.WriteQualifiedName(MakeFirstLower(localName), ns);
		}

		public override void WriteStartAttribute(string prefix, string localName, string ns)
		{
			base.WriteStartAttribute(prefix, MakeFirstLower(localName), ns);
		}

		public override void WriteStartElement(string prefix, string localName, string ns)
		{
			base.WriteStartElement(prefix, MakeFirstLower(localName), ns);
		}
	}
}
