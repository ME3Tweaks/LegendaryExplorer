using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Gammtek.Conduit.Mvp.Xml.Common.Xsl
{
	public class CharacterMappingXmlWriter : XmlWrappingWriter
	{
		private readonly CharacterMappingXmlReader _reader;
		private Dictionary<char, string> _mapping;

		public CharacterMappingXmlWriter(XmlWriter baseWriter, Dictionary<char, string> mapping)
			: base(baseWriter)
		{
			_mapping = mapping;
		}

		public CharacterMappingXmlWriter(CharacterMappingXmlReader reader, XmlWriter baseWriter)
			: base(baseWriter)
		{
			_reader = reader;
		}

		private void FlushBuffer(StringBuilder buf)
		{
			if (buf.Length <= 0)
			{
				return;
			}
			base.WriteString(buf.ToString());
			buf.Length = 0;
		}

		public override void WriteString(string text)
		{
			if (_mapping == null && _reader != null)
			{
				_mapping = _reader.CompileCharacterMapping();
			}
			if (_mapping != null && _mapping.Count > 0)
			{
				var buf = new StringBuilder();
				foreach (var c in text)
				{
					if (_mapping.ContainsKey(c))
					{
						FlushBuffer(buf);
						base.WriteRaw(_mapping[c]);
					}
					else
					{
						buf.Append(c);
					}
				}
				FlushBuffer(buf);
			}
			else
			{
				base.WriteString(text);
			}
		}
	}
}
