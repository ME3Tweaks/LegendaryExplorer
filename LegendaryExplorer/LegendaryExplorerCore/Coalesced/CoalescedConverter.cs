using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using LegendaryExplorerCore.Coalesced.Xml;
using LegendaryExplorerCore.Gammtek.IO;

namespace LegendaryExplorerCore.Coalesced
{
    public static class CoalescedConverter
    {
        public static readonly int CoalescedMagicNumber = 1718448749;

        public static readonly SortedSet<string> ProperNames =
			new SortedSet<string>
			{
				"BioAI",
				"BioCompat",
				"BioCredits",
				"BioDifficulty",
				"BioEngine",
				"BioGame",
				"BioInput",
				"BioLightmass",
				"BioTest",
				"BioUI",
                "BioQA",
				"BioWeapon",
				"Core",
				"Descriptions",
				"EditorTips",
				"Engine",
				"GFxUI",
				"IpDrv",
				"Launch",
				"OnlineSubsystemGamespy",
				"Startup",
				"Subtitles",
				"UnrealEd",
				"WinDrv",
				"XWindow"
			};

		public static readonly Dictionary<string, string> SpecialCharacters =
			new Dictionary<string, string>
			{
				{ "\t", "\\t" },
				{ "\r", "\\r" },
				{ "\n", "\\n" }
			};

		public static void ConvertToXML(string source, string destination)
		{
            var sourcePath = source;
            var destinationPath = destination;

            if (string.IsNullOrEmpty(destinationPath))
            {
                destinationPath = Path.ChangeExtension(sourcePath, null);
            }

            if (!Path.IsPathRooted(destinationPath))
            {
                destinationPath = Path.Combine(GetExePath(), destinationPath);
            }

            if (!File.Exists(sourcePath))
            {
                return;
            }

            using (var input = File.OpenRead(sourcePath))
            {
                var coal = new CoalescedFileXml();
                coal.Deserialize(input);

                var inputId = Path.GetFileNameWithoutExtension(sourcePath) ?? "Coalesced";
                var inputName = Path.GetFileName(sourcePath) ?? "Coalesced.bin";

                List<string> OutputFileNames = new List<string>();

                XDocument xDoc;
                XElement rootElement;

                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                }

                foreach (var file in coal.Files)
                {
                    var fileId = Path.GetFileNameWithoutExtension(file.Name);
                    fileId = ProperNames.FirstOrDefault(s => s.Equals(fileId, StringComparison.InvariantCultureIgnoreCase)) ?? fileId;

                    var iniPath = $"{destinationPath}/{fileId}.xml";

                    OutputFileNames.Add(Path.GetFileName(iniPath));

                    xDoc = new XDocument();

                    rootElement = new XElement("CoalesceAsset");
                    rootElement.SetAttributeValue("id", fileId);
                    rootElement.SetAttributeValue("name", Path.GetFileName(file.Name));
                    rootElement.SetAttributeValue("source", file.Name);

                    var sectionsElement = new XElement("Sections");

                    foreach (var section in file.Sections)
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
                                    valueElement.SetAttributeValue("type", value.Type);

                                    if (!string.IsNullOrEmpty(propertyValue))
                                    {
                                        propertyValue = SpecialCharacters.Aggregate(propertyValue, (current, c) => current.Replace(c.Key, c.Value));
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
                                            propertyElement.SetAttributeValue("type", property.Value[0].Type);
                                            var propertyValue = property.Value[0].Value;

                                            if (!string.IsNullOrEmpty(propertyValue))
                                            {
                                                propertyValue = SpecialCharacters.Aggregate(propertyValue, (current, c) => current.Replace(c.Key, c.Value));
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
                    using (var writer = new XmlTextWriter(iniPath, Encoding.UTF8))
                    {
                        writer.IndentChar = '\t';
                        writer.Indentation = 1;
                        writer.Formatting = Formatting.Indented;

                        xDoc.Save(writer);
                    }

                    //

                    //xDoc.Save(iniPath, SaveOptions.None);
                }

                xDoc = new XDocument();

                rootElement = new XElement("CoalesceFile");
                rootElement.SetAttributeValue("id", inputId);
                rootElement.SetAttributeValue("name", inputName);

                //rootElement.SetAttributeValue("Source", "");

                var assetsElement = new XElement("Assets");

                foreach (var file in OutputFileNames)
                {
                    var assetElement = new XElement("Asset");
                    var path = $"{file}";
                    assetElement.SetAttributeValue("source", path);

                    assetsElement.Add(assetElement);
                }

                rootElement.Add(assetsElement);
                xDoc.Add(rootElement);

                //
                using (var writer = new XmlTextWriter(Path.Combine(destinationPath, $"{inputId}.xml"), Encoding.UTF8))
                {
                    writer.IndentChar = '\t';
                    writer.Indentation = 1;
                    writer.Formatting = Formatting.Indented;

                    xDoc.Save(writer);
                }

                //

                //xDoc.Save(Path.Combine(destinationPath, string.Format("{0}.xml", inputId)), SaveOptions.None);
            }
        }

        public static void ConvertToBin(string source, string destination)
        {
            var inputPath = Path.IsPathRooted(source) ? source : Path.Combine(GetExePath(), source);
            var outputPath = !string.IsNullOrEmpty(destination) ? destination : Path.ChangeExtension(inputPath, ".bin");

            if (!Path.IsPathRooted(outputPath))
            {
                outputPath = Path.Combine(GetExePath(), outputPath);
            }

            if (!File.Exists(inputPath))
            {
                return;
            }

            var file = XmlCoalesceFile.Load(inputPath);

            var coal = new CoalescedFileXml
            {
                ByteOrder = ByteOrder.LittleEndian,
                Version = 1
            };

            foreach (var asset in file.Assets)
            {
                var entry =
                    new FileEntry(asset.Source)
                    {
                        Sections = new Dictionary<string, Dictionary<string, List<PropertyValue>>>()
                    };

                foreach (var section in asset.Sections)
                {
                    var eSection = new Dictionary<string, List<PropertyValue>>();

                    foreach (var property in section.Value)
                    {
                        var eProperty = new List<PropertyValue>();

                        foreach (var value in property.Value)
                        {
                            if (!file.Settings.CompileTypes.Contains(value.ValueType))
                            {
                                continue;
                            }

                            var valueValue = value.Value;

                            if (!string.IsNullOrEmpty(valueValue))
                            {
                                valueValue = SpecialCharacters.Aggregate(valueValue, (current, c) => current.Replace(c.Value, c.Key));
                            }

                            eProperty.Add(new PropertyValue(value.ValueType, valueValue));
                        }

                        eSection.Add(property.Key, eProperty);
                    }

                    entry.Sections.Add(section.Key, eSection);
                }

                coal.Files.Add(entry);
            }

            using (var output = File.Create(outputPath))
            {
                if (file.Settings != null)
                {
                    coal.OverrideCompileValueTypes = file.Settings.OverrideCompileValueTypes;
                    coal.CompileTypes = file.Settings.CompileTypes;
                }

                coal.Serialize(output);
            }
        }

        private static string GetExePath()
		{
			return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		}

        public static bool IsOTCoalesced(string sourceFile)
        {
            if (File.Exists(sourceFile))
            {
                byte[] bytes = new byte[4];
                using FileStream fs = new FileStream(sourceFile, FileMode.Open);
                fs.Read(bytes, 0, 4);
                return (BitConverter.ToInt32(bytes, 0) == CoalescedMagicNumber);
            }
            return true;
        }
	}
}
