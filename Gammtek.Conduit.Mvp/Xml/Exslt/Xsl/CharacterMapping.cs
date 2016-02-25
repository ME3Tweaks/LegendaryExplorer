using System;
using System.Collections.Generic;
using System.Xml.Xsl;

namespace Gammtek.Conduit.Mvp.Xml.Common.Xsl
{
	internal class CharacterMapping
	{
		private readonly Dictionary<string, CharacterMap> _maps = new Dictionary<string, CharacterMap>();

		public void AddCharacterMap(string name, CharacterMap map)
		{
			if (_maps.ContainsKey(name))
			{
				throw new XsltCompileException("Duplicate character map '" + name + "'.");
			}
			_maps.Add(name, map);
		}

		public Dictionary<char, string> Compile(List<string> charMapsToBeUsed)
		{
			var usedmaps = new List<string>(charMapsToBeUsed);
			var compiledMap = new Dictionary<char, string>();
			var mapStack = new Stack<string>();
			foreach (var mapName in usedmaps)
			{
				CompileMap(mapName, compiledMap, mapStack);
			}
			return compiledMap;
		}

		private void CompileMap(string mapName, Dictionary<char, string> compiledMap, Stack<string> mapStack)
		{
			if (!_maps.ContainsKey(mapName))
			{
				throw new XsltCompileException("Unknown character map '" + mapName + "'");
			}
			if (mapStack.Contains(mapName))
			{
				throw new XsltCompileException("Character map " + mapName + " references itself, directly or indirectly.");
			}
			var map = _maps[mapName];
			foreach (var c in map.Map.Keys)
			{
				if (compiledMap.ContainsKey(c))
				{
					Console.WriteLine(@"Replacing {0}: {1} with {2}", (int) c, compiledMap[c], map.Map[c]);
					compiledMap[c] = map.Map[c];
				}
				else
				{
					compiledMap.Add(c, map.Map[c]);
				}
			}
			if (map.ReferencedCharacterMaps == null)
			{
				return;
			}
			mapStack.Push(mapName);
			foreach (var name in map.ReferencedCharacterMaps)
			{
				CompileMap(name, compiledMap, mapStack);
			}
			mapStack.Pop();
		}
	}
}
