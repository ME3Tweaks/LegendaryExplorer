using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	public class ExsltStrings
	{
		private static readonly char[] HexDigit = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

		public string Align(string str, string padding, string alignment)
		{
			if (str.Length > padding.Length)
			{
				return str.Substring(0, padding.Length);
			}
			if (str.Length == padding.Length)
			{
				return str;
			}
			switch (alignment)
			{
				case "right":
					return padding.Substring(0, padding.Length - str.Length) + str;
				case "center":
					var space = (padding.Length - str.Length) / 2;
					return padding.Substring(0, space) + str +
						padding.Substring(str.Length + space);
				default:
					//Align to left by default
					return str + padding.Substring(str.Length);
			}
		}

		public string Align(string str, string padding)
		{
			return Align(str, padding, "left");
		}

		public string Concat(XPathNodeIterator nodeset)
		{
			var sb = new StringBuilder();

			while (nodeset.MoveNext())
			{
				sb.Append(nodeset.Current.Value);
			}

			return sb.ToString();
		}

		public string DecodeUri(string str)
		{
			return DecodeUriImpl(str, Encoding.UTF8);
		}

		public string DecodeUri(string str, string encoding)
		{
			if (encoding == String.Empty)
			{
				return String.Empty;
			}
			Encoding enc;
			try
			{
				enc = Encoding.GetEncoding(encoding);
			}
			catch
			{
				//Not supported encoding, return empty string
				return String.Empty;
			}
			return DecodeUriImpl(str, enc);
		}

		private static string DecodeUriImpl(string str, Encoding enc)
		{
			return str == string.Empty ? str : HttpUtility.UrlDecode(str, enc);
		}

		private static void EncodeChar(StringBuilder res, Encoding enc, char[] str, int index)
		{
			foreach (var b in enc.GetBytes(str, index, 1))
			{
				res.AppendFormat("%{0}{1}", HexDigit[b >> 4], HexDigit[b & 15]);
			}
		}

		public string EncodeUri(string str, bool encodeReserved)
		{
			return EncodeUriImpl(str, encodeReserved, Encoding.UTF8);
		}

		public string EncodeUri(string str, bool encodeReserved, string encoding)
		{
			Encoding enc;
			try
			{
				enc = Encoding.GetEncoding(encoding);
			}
			catch
			{
				//Not supported encoding, return empty string
				return String.Empty;
			}
			return EncodeUriImpl(str, encodeReserved, enc);
		}

		private static string EncodeUriImpl(string str, bool encodeReserved, Encoding enc)
		{
			if (str == string.Empty)
			{
				return str;
			}
			var res = new StringBuilder(str.Length);
			var chars = str.ToCharArray();
			if (encodeReserved)
			{
				for (var i = 0; i < chars.Length; i++)
				{
					var c = chars[i];
					if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
					{
						res.Append(c);
					}
					else
					{
						switch (c)
						{
							case '-':
							case '_':
							case '.':
							case '!':
							case '~':
							case '*':
							case '\'':
							case '(':
							case ')':
								res.Append(c);
								break;
							case '%':
								if (i < chars.Length - 2 && IsHexDigit(chars[i + 1]) && IsHexDigit(chars[i + 2]))
								{
									res.Append(c);
								}
								else
								{
									EncodeChar(res, enc, chars, i);
								}
								break;
							default:
								EncodeChar(res, enc, chars, i);
								break;
						}
					}
				}
			}
			else
			{
				for (var i = 0; i < chars.Length; i++)
				{
					var c = chars[i];
					if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
					{
						res.Append(c);
					}
					else
					{
						switch (c)
						{
							case '-':
							case '_':
							case '.':
							case '!':
							case '~':
							case '*':
							case '\'':
							case '(':
							case ')':
							case ';':
							case '/':
							case '?':
							case ':':
							case '@':
							case '&':
							case '=':
							case '+':
							case '$':
							case ',':
							case '[':
							case ']':
								res.Append(c);
								break;
							case '%':
								if (i < chars.Length - 2 && IsHexDigit(chars[i + 1]) && IsHexDigit(chars[i + 2]))
								{
									res.Append(c);
								}
								else
								{
									EncodeChar(res, enc, chars, i);
								}
								break;
							default:
								EncodeChar(res, enc, chars, i);
								break;
						}
					}
				}
			}
			return res.ToString();
		}

		private static bool IsHexDigit(char c)
		{
			return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
		}

		public string Padding(int number)
		{
			var s = String.Empty;

			return number < 0 ? s : s.PadLeft(number);
		}

		public string Padding(int number, string s)
		{
			if (number < 0 || s == string.Empty)
			{
				return String.Empty;
			}
			var sb = new StringBuilder(s);

			while (sb.Length < number)
			{
				sb.Append(s);
			}

			return sb.Length > number ? sb.Remove(number, sb.Length - number).ToString() : sb.ToString();
		}

		public string Replace(string str, string oldValue, string newValue)
		{
			return str.Replace(oldValue, newValue);
		}

		public XPathNodeIterator Split(string str)
		{
			var doc = new XmlDocument();
			doc.LoadXml("<tokens/>");

			foreach (var match in str.Split(new[] { ' ' }))
			{
				if (match.Equals(String.Empty))
				{
					continue;
				}
				var elem = doc.CreateElement("token");
				elem.InnerText = match;
				if (doc.DocumentElement != null)
				{
					doc.DocumentElement.AppendChild(elem);
				}
			}

			return doc.CreateNavigator().Select("//token");
		}

		public XPathNodeIterator Split(string str, string delimiter)
		{
			var doc = new XmlDocument();
			doc.LoadXml("<tokens/>");

			if (delimiter.Equals(String.Empty))
			{
				foreach (var match in str)
				{
					var elem = doc.CreateElement("token");
					elem.InnerText = match.ToString(CultureInfo.InvariantCulture);
					if (doc.DocumentElement != null)
					{
						doc.DocumentElement.AppendChild(elem);
					}
				}
			}
			else
			{
				//since there is no String.Split(string) method we use the Regex class 
				//and escape special characters. 
				//. $ ^ { [ ( | ) * + ? \
				delimiter = delimiter.Replace("\\", "\\\\").Replace("$", "\\$").Replace("^", "\\^");
				delimiter = delimiter.Replace("{", "\\{").Replace("[", "\\[").Replace("(", "\\(");
				delimiter = delimiter.Replace("*", "\\*").Replace(")", "\\)").Replace("|", "\\|");
				delimiter = delimiter.Replace("+", @"\+").Replace("?", "\\?").Replace(".", "\\.");

				var regex = new Regex(delimiter);

				foreach (var match in regex.Split(str))
				{
					if ((match.Equals(String.Empty)) || (match.Equals(delimiter)))
					{
						continue;
					}
					var elem = doc.CreateElement("token");
					elem.InnerText = match;
					if (doc.DocumentElement != null)
					{
						doc.DocumentElement.AppendChild(elem);
					}
				}
			}

			return doc.CreateNavigator().Select("//token");
		}

		public XPathNodeIterator Tokenize(string str, string delimiters)
		{
			var doc = new XmlDocument();
			doc.LoadXml("<tokens/>");

			if (delimiters == String.Empty)
			{
				foreach (var c in str)
				{
					var elem = doc.CreateElement("token");
					elem.InnerText = c.ToString(CultureInfo.InvariantCulture);
					if (doc.DocumentElement != null)
					{
						doc.DocumentElement.AppendChild(elem);
					}
				}
			}
			else
			{
				foreach (var token in str.Split(delimiters.ToCharArray()))
				{
					var elem = doc.CreateElement("token");
					elem.InnerText = token;
					if (doc.DocumentElement != null)
					{
						doc.DocumentElement.AppendChild(elem);
					}
				}
			}

			return doc.CreateNavigator().Select("//token");
		}

		public XPathNodeIterator Tokenize(string str)
		{
			var regex = new Regex("\\s+");

			var doc = new XmlDocument();
			doc.LoadXml("<tokens/>");

			foreach (var token in regex.Split(str))
			{
				var elem = doc.CreateElement("token");
				elem.InnerText = token;
				if (doc.DocumentElement != null)
				{
					doc.DocumentElement.AppendChild(elem);
				}
			}

			return doc.CreateNavigator().Select("//token");
		}

		public string decodeUri_RENAME_ME(string str)
		{
			return DecodeUri(str);
		}

		public string decodeUri_RENAME_ME(string str, string encoding)
		{
			return DecodeUri(str, encoding);
		}

		public string encodeUri_RENAME_ME(string str, bool encodeReserved)
		{
			return EncodeUri(str, encodeReserved);
		}

		public string encodeUri_RENAME_ME(string str, bool encodeReserved, string encoding)
		{
			return EncodeUri(str, encodeReserved, encoding);
		}
	}
}
