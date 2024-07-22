using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorerCore.Coalesced
{
	public enum CoalesceAssetType
	{
		Asset,
		Include,
		None,
		Unknown
	}

	[DebuggerDisplay("CoalesceAsset {Name} with {Sections.Count} sections")]
	public class CoalesceAsset
	{
		public CoalesceAsset(string name = null, CoalesceSections sections = null)
		{
			Name = name ?? "";
			Sections = sections ?? new CoalesceSections();
		}

		public string Id { get; set; }

		public string Name { get; set; }

		public CoalesceSections Sections { get; set; }

		public string Source { get; set; }

		public bool CompareId(CoalesceAsset asset, bool ignoreCase = false)
		{
			if (asset == null)
			{
				throw new ArgumentNullException(nameof(asset));
			}

			return CompareId(asset.Id, ignoreCase);
		}

		public bool CompareId(string id, bool ignoreCase = false)
		{
			if (!HasId || string.IsNullOrEmpty(id))
			{
				return false;
			}

			if (ignoreCase && Id.Equals(id, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return !ignoreCase && Id.Equals(id);
		}

		public bool CompareName(CoalesceAsset asset, bool ignoreCase = false)
		{
			if (asset == null)
			{
				throw new ArgumentNullException(nameof(asset));
			}

			return CompareName(asset.Name, ignoreCase);
		}

		public bool CompareName(string name, bool ignoreCase = false)
		{
			if (!HasName || string.IsNullOrEmpty(name))
			{
				return false;
			}

			if (ignoreCase && Name.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return !ignoreCase && Name.Equals(name);
		}

		public bool CompareSource(CoalesceAsset asset, bool ignoreCase = false)
		{
			if (asset == null)
			{
				throw new ArgumentNullException(nameof(asset));
			}

			return CompareSource(asset.Source, ignoreCase);
		}

		public bool CompareSource(string source, bool ignoreCase = false)
		{
			if (!HasSource || string.IsNullOrEmpty(source))
			{
				return false;
			}

			if (ignoreCase && Source.Equals(source, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return !ignoreCase && Source.Equals(source);
		}

		public bool HasId => !string.IsNullOrEmpty(Id);

	    public bool HasName => !string.IsNullOrEmpty(Name);

	    public bool HasSource => !string.IsNullOrEmpty(Source);

	    public void Combine(CoalesceAsset asset)
		{
			if (asset == null)
			{
				throw new ArgumentNullException(nameof(asset));
			}

			foreach (var section in asset.Sections)
			{
				if (!Sections.ContainsKey(section.Key))
				{
					Sections.Add(section.Key, section.Value);
				}
				else
				{
					Sections[section.Key].Combine(section.Value);
				}
			}
		}

		public void MergeRight(CoalesceAsset asset)
		{
			if (asset == null)
			{
				throw new ArgumentNullException(nameof(asset));
			}

			throw new NotImplementedException();
		}

		// CONVERTERS TO INI/XML
        /// <summary>
        /// Converts this asset to a game2 ini file. Note that Game 2 doesn't support type 0 New
        /// </summary>
        /// <param name="iniText"></param>
        /// <returns></returns>
        public static DuplicatingIni ToIni(CoalesceAsset asset)
        {
            DuplicatingIni ini = new DuplicatingIni();
            foreach (var sect in asset.Sections)
            {
                var section = ini.GetOrAddSection(sect.Key);
                foreach (var entry in sect.Value)
                {
                    foreach (var entryValue in entry.Value)
                    {
                        var key = GetIniPrefix(entryValue.ParseAction) + entry.Key;
                        section.Entries.Add(new DuplicatingIni.IniEntry(key, entryValue.Value));
                    }
                }

            }

            return ini;
        }

        private static string GetIniPrefix(CoalesceParseAction entryValueParseAction)
        {
            switch (entryValueParseAction)
            {
                case CoalesceParseAction.AddUnique:
                    return "+";
                case CoalesceParseAction.Remove:
                    return "-";
                case CoalesceParseAction.RemoveProperty:
                    return "!";
            }

            return "";
        }

		/// <summary>
		/// Converts this asset to an Xml representation (Game 3)
		/// </summary>
		/// <returns></returns>
		public string ToXmlString()
        {
			var fileId = Path.GetFileNameWithoutExtension(Name);
			fileId = CoalescedConverter.ProperNames.FirstOrDefault(s => s.Equals(fileId, StringComparison.InvariantCultureIgnoreCase));

			var xDoc = new XDocument();

			var rootElement = new XElement("CoalesceAsset");
			rootElement.SetAttributeValue("id", fileId);
			rootElement.SetAttributeValue("name", Path.GetFileName(Name));
			rootElement.SetAttributeValue("source", Source);

			var sectionsElement = new XElement("Sections");

			foreach (var section in Sections)
			{
				var sectionElement = new XElement("Section");
				sectionElement.SetAttributeValue("name", section.Key);

				//
				//var classes = Namespace.FromStrings(section.Value.Keys);

				//

				foreach (var property in section.Value)
				{
					var propertyElement = new XElement("Property");
					propertyElement.SetAttributeValue("name", property.Key);

					if (property.Value.Count > 1)
					{
						foreach (var value in property.Value)
						{
							var valueElement = new XElement("Value");
							var propertyValue = value.Value;
							valueElement.SetAttributeValue("type", ParseActionToType(value.ParseAction));

							if (!string.IsNullOrEmpty(propertyValue))
							{
								propertyValue = CoalescedConverter.SpecialCharacters.Aggregate(propertyValue, (current, c) => current.Replace(c.Key, c.Value));
							}

							valueElement.SetValue(propertyValue ?? "null");

							propertyElement.Add(valueElement);
						}
					}
					else
					{
						switch (property.Value.Count)
						{
							case 1:
								{
									propertyElement.SetAttributeValue("type", ParseActionToType(property.Value[0].ParseAction));
									var propertyValue = property.Value[0].Value;

									if (!string.IsNullOrEmpty(propertyValue))
									{
										propertyValue = CoalescedConverter.SpecialCharacters.Aggregate(propertyValue, (current, c) => current.Replace(c.Key, c.Value));
									}

									propertyElement.SetValue(propertyValue ?? "null");
									break;
								}
							case 0:
								{
									propertyElement.SetAttributeValue("type", CoalesceProperty.DefaultValueType);
									propertyElement.SetValue("");
									break;
								}
						}
					}

					sectionElement.Add(propertyElement);
				}

				sectionsElement.Add(sectionElement);
			}

			rootElement.Add(sectionsElement);
			xDoc.Add(rootElement);

			//
			using (StringWriter writer = new CoalescedConverter.Utf8StringWriter())
			{
				// Build Xml with xw.
				//xw.IndentChar = '\t';
				//xw.Indentation = 1;
				//xw.Formatting = Formatting.Indented;

				//xDoc.Save(xw);

				;
				xDoc.Save(writer, SaveOptions.None);
				return writer.ToString();
			}
		}

        private int ParseActionToType(CoalesceParseAction valueParseAction)
        {
            switch (valueParseAction)
            {
				case CoalesceParseAction.New:
                    return 0;
				case CoalesceParseAction.RemoveProperty:
                    return 1;
				case CoalesceParseAction.Add:
                    return 2;
				case CoalesceParseAction.AddUnique:
                    return 3;
				case CoalesceParseAction.Remove:
                    return 4;
            }

            return 2;
        }

		/// <summary>
		/// Fetches the named section, or adds a new one with no values if not found
		/// </summary>
		/// <param name="sectionName"></param>
		/// <returns></returns>
        public CoalesceSection GetOrAddSection(string sectionName)
        {
            if (Sections.TryGetValue(sectionName, out var v))
                return v;
            var sect = new CoalesceSection(sectionName);
            Sections.Add(sectionName, sect);
            return sect;
        }
    }
}