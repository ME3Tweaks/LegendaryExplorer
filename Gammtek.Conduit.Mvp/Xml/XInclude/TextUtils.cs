using System.Linq;

namespace Gammtek.Conduit.Mvp.Xml.XInclude
{
	internal class TextUtils
	{
		public static void CheckAcceptValue(string accept)
		{
			foreach (var c in accept.Where(c => c < 0x0020 || c > 0x007E))
			{
				throw new InvalidAcceptHttpHeaderValueError(c);
			}
		}

		public static void CheckForNonXmlChars(string str)
		{
			var i = 0;
			while (i < str.Length)
			{
				var c = str[i];
				//Allowed unicode XML characters
				if (c >= 0x0020 && c <= 0xD7FF || c >= 0xE000 && c <= 0xFFFD ||
					c == 0xA || c == 0xD || c == 0x9)
				{
					//Ok, approved.
					i++;
					continue;
					//Check then surrogate pair
				}
				if (c >= 0xd800 && c <= 0xdbff)
				{
					//Looks like first char in a surrogate pair, check second one
					if (++i < str.Length)
					{
						if (str[i] >= 0xdc00 && str[i] <= 0xdfff)
						{
							//Ok, valid surrogate pair
							i++;
						}
						continue;
					}
				}
				throw new NonXmlCharacterException(str[i]);
			}
		}
	}
}
