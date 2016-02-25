using System.Collections.Generic;

namespace Gammtek.Conduit.Mvp.Xml.Common.Xsl
{
	internal class CharacterMap
	{
		private readonly Dictionary<char, string> _map;

		public CharacterMap()
		{
			_map = new Dictionary<char, string>();
		}

		public Dictionary<char, string> Map
		{
			get { return _map; }
		}

		public string[] ReferencedCharacterMaps { get; set; }

		public void AddMapping(char character, string replace)
		{
			if (_map.ContainsKey(character))
			{
				_map[character] = replace;
			}
			else
			{
				_map.Add(character, replace);
			}
		}
	}
}
