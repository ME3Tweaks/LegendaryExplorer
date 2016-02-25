using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	public class GdnRegularExpressions
	{
		public XPathNodeIterator Tokenize(string str, string regexp)
		{
			const RegexOptions options = RegexOptions.ECMAScript;

			var doc = new XmlDocument();
			doc.LoadXml("<matches/>");

			var regex = new Regex(regexp, options);

			foreach (var match in regex.Split(str))
			{
				var elem = doc.CreateElement("match");
				elem.InnerText = match;
				if (doc.DocumentElement != null)
				{
					doc.DocumentElement.AppendChild(elem);
				}
			}

			return doc.CreateNavigator().Select("//match");
		}

		public XPathNodeIterator Tokenize(string str, string regexp, string flags)
		{
			var options = RegexOptions.ECMAScript;

			if (flags.IndexOf('m') != -1)
			{
				options |= RegexOptions.Multiline;
			}

			if (flags.IndexOf('i') != -1)
			{
				options |= RegexOptions.IgnoreCase;
			}

			var doc = new XmlDocument();
			doc.LoadXml("<matches/>");

			var regex = new Regex(regexp, options);

			foreach (var match in regex.Split(str))
			{
				var elem = doc.CreateElement("match");
				elem.InnerText = match;
				if (doc.DocumentElement != null)
				{
					doc.DocumentElement.AppendChild(elem);
				}
			}

			return doc.CreateNavigator().Select("//match");
		}
	}
}
