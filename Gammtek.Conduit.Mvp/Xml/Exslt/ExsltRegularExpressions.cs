using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	public class ExsltRegularExpressions
	{
		public XPathNodeIterator Match(string str, string regexp)
		{
			const RegexOptions options = RegexOptions.ECMAScript;

			var doc = new XmlDocument();
			doc.LoadXml("<matches/>");

			var regex = new Regex(regexp, options);

			foreach (Group g in regex.Match(str).Groups)
			{
				var elem = doc.CreateElement("match");
				elem.InnerText = g.Value;
				if (doc.DocumentElement != null)
				{
					doc.DocumentElement.AppendChild(elem);
				}
			}

			return doc.CreateNavigator().Select("//match");
		}

		public XPathNodeIterator Match(string str, string regexp, string flags)
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

			if (flags.IndexOf('g') == -1)
			{
				foreach (Group g in regex.Match(str).Groups)
				{
					var elem = doc.CreateElement("match");
					elem.InnerText = g.Value;
					if (doc.DocumentElement != null)
					{
						doc.DocumentElement.AppendChild(elem);
					}
				}
			}
			else
			{
				foreach (Match m in regex.Matches(str))
				{
					var elem = doc.CreateElement("match");
					elem.InnerText = m.Value;
					if (doc.DocumentElement != null)
					{
						doc.DocumentElement.AppendChild(elem);
					}
				} //foreach(match m...)				
			}

			return doc.CreateNavigator().Select("//match");
		}

		public string Replace(string input, string regexp, string flags, string replacement)
		{
			var options = RegexOptions.ECMAScript;

			if (flags.IndexOf('i') != -1)
			{
				options |= RegexOptions.IgnoreCase;
			}

			var regex = new Regex(regexp, options);

			if (flags.IndexOf('g') != -1)
			{
				return regex.Replace(input, replacement);
			}
			return regex.Replace(input, replacement, 1);
		}

		public bool Test(string str, string regexp)
		{
			const RegexOptions options = RegexOptions.ECMAScript;

			var regex = new Regex(regexp, options);
			return regex.IsMatch(str);
		}

		public bool Test(string str, string regexp, string flags)
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

			var regex = new Regex(regexp, options);
			return regex.IsMatch(str);
		}
	}
}
