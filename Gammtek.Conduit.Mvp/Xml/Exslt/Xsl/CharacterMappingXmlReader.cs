using System.Collections.Generic;
using System.Xml;
using System.Xml.Xsl;

namespace Gammtek.Conduit.Mvp.Xml.Common.Xsl
{
	public class CharacterMappingXmlReader : XmlWrappingReader
	{
		private readonly string _characterMapTag;
		private readonly string _characterTag;
		private readonly string _nameTag;
		private readonly string _nxsltNamespace;
		private readonly string _outputCharacterTag;
		private readonly string _outputTag;
		private readonly string _stringTag;
		private readonly string _useCharacterMapsTag;
		private CharacterMap _currMap;
		private string _currMapName;
		private CharacterMapping _mapping;
		private List<string> _useCharacterMaps;

		public CharacterMappingXmlReader(XmlReader baseReader)
			: base(baseReader)
		{
			var xmlNameTable = base.NameTable;
			if (xmlNameTable == null)
			{
				return;
			}

			_nxsltNamespace = xmlNameTable.Add("http://www.xmllab.net/nxslt");
			_characterMapTag = xmlNameTable.Add("character-map");
			_nameTag = xmlNameTable.Add("name");
			_outputCharacterTag = xmlNameTable.Add("output-character");
			_characterTag = xmlNameTable.Add("character");
			_stringTag = xmlNameTable.Add("string");
			_outputTag = xmlNameTable.Add("output");
			_useCharacterMapsTag = xmlNameTable.Add("use-character-maps");
		}

		public Dictionary<char, string> CompileCharacterMapping()
		{
			if (_mapping == null)
			{
				return new Dictionary<char, string>();
			}
			return _mapping.Compile(_useCharacterMaps);
		}

		public override bool Read()
		{
			var baseRead = base.Read();
			if (base.NodeType == XmlNodeType.Element && base.NamespaceURI == _nxsltNamespace &&
				base.LocalName == _characterMapTag)
			{
				//nxslt:character-map
				_currMapName = base[_nameTag];
				if (string.IsNullOrEmpty(_currMapName))
				{
					throw new XsltCompileException("Required 'name' attribute of nxslt:character-map element is missing.");
				}
				_currMap = new CharacterMap();
				var referencedMaps = base[_useCharacterMapsTag];
				if (!string.IsNullOrEmpty(referencedMaps))
				{
					_currMap.ReferencedCharacterMaps = referencedMaps.Split(' ');
				}
			}
			else if (base.NodeType == XmlNodeType.EndElement && base.NamespaceURI == _nxsltNamespace &&
				base.LocalName == _characterMapTag)
			{
				if (_mapping == null)
				{
					_mapping = new CharacterMapping();
				}
				_mapping.AddCharacterMap(_currMapName, _currMap);
			}
			else if (base.NodeType == XmlNodeType.Element && base.NamespaceURI == _nxsltNamespace
				&& base.LocalName == _outputCharacterTag)
			{
				//nxslt:output-character                        
				var character = base[_characterTag];
				if (string.IsNullOrEmpty(character))
				{
					throw new XsltCompileException("Required 'character' attribute of nxslt:output-character element is missing.");
				}
				if (character.Length > 1)
				{
					throw new XsltCompileException(
						"'character' attribute value of nxslt:output-character element is too long - must be a single character.");
				}
				var _string = base[_stringTag];
				if (string.IsNullOrEmpty(character))
				{
					throw new XsltCompileException("Required 'string' attribute of nxslt:output-character element is missing.");
				}
				_currMap.AddMapping(character[0], _string);
			}
			else if (base.NodeType == XmlNodeType.Element && base.NamespaceURI == _nxsltNamespace &&
				base.LocalName == _outputTag)
			{
				//nxslt:output
				var useMaps = base[_useCharacterMapsTag];
				if (string.IsNullOrEmpty(useMaps))
				{
					return baseRead;
				}
				if (_useCharacterMaps == null)
				{
					_useCharacterMaps = new List<string>();
				}
				_useCharacterMaps.AddRange(useMaps.Split(' '));
			}
			return baseRead;
		}
	}
}
