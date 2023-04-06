using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using LegendaryExplorerCore.Coalesced.Xml;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorerCore.Coalesced
{
    /// <summary>
    /// Class for converting to and from editable text versions of Coalesced files (Game3, LE1/LE2)
    /// </summary>
    public static class CoalescedConverter
    {
        /// <summary>
        /// The magic number header for Game 3 Coalesced files
        /// </summary>
        public static readonly int CoalescedMagicNumber = 1718448749;

        /// <summary>
        /// The list of filenames supported by this compiler
        /// </summary>
        public static readonly SortedSet<string> ProperNames =
            new SortedSet<string>
            {
                "BioAI",
                "BioCompat",
                "BioCredits",
                "BioDifficulty",
                "BioEngine",
                "BioGame",
                "BioGuiResources", // PC Main Menu PC New Character
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

        /// <summary>
        /// Decompiles a Game3 Coalesced stream to a dictionary of strings that maps the filenames to their xml string contents 
        /// </summary>
        /// <param name="inputStream">Input stream</param>
        /// <returns>Dictionary of files mapped to their xml files</returns>
        public static CaseInsensitiveDictionary<string> DecompileGame3ToMemory(Stream inputStream)
        {
            var fileMapping = new CaseInsensitiveDictionary<string>();
            var coal = new CoalescedFileXml();
            coal.Deserialize(inputStream);
            XDocument xDoc;
            XElement rootElement;


            foreach (var file in coal.Files)
            {
                var fileId = Path.GetFileNameWithoutExtension(file.Name);
                fileId = ProperNames.FirstOrDefault(s => s.Equals(fileId, StringComparison.InvariantCultureIgnoreCase));

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
                using (StringWriter writer = new Utf8StringWriter())
                {
                    // Build Xml with xw.
                    //xw.IndentChar = '\t';
                    //xw.Indentation = 1;
                    //xw.Formatting = Formatting.Indented;

                    //xDoc.Save(xw);

                    ;
                    xDoc.Save(writer, SaveOptions.None);
                    fileMapping[$"{fileId}.xml"] = writer.ToString();
                }

                //

                //xDoc.Save(iniPath, SaveOptions.None);
            }
            return fileMapping;
        }

        internal class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding { get { return Encoding.UTF8; } }
        }

        // Todo: Make this method say Game3 to be less ambiguous
        /// <summary>
        /// Converts a Game3 Coalesced file between its binary and decompiled text formats
        /// </summary>
        /// <param name="SourceType">The type the <paramref name="SourcePath"/> file is in.</param>
        /// <param name="SourcePath">The input file</param>
        /// <param name="DestinationPath">The destination file or directory</param>
        /// <exception cref="FileNotFoundException">If input file is not found</exception>
        /// <exception cref="ArgumentOutOfRangeException">If invalid coalesced type is specified</exception>
        public static void Convert(CoalescedType SourceType, string SourcePath, string DestinationPath)
        {
            if (!Path.IsPathRooted(SourcePath))
            {
                SourcePath = Path.GetFullPath(SourcePath);
            }

            if (!Path.IsPathRooted(DestinationPath))
            {
                DestinationPath = Path.GetFullPath(DestinationPath);
            }

            if (!File.Exists(SourcePath))
            {
                throw new FileNotFoundException("Source file not found.");
            }

            switch (SourceType)
            {

                case CoalescedType.Binary:
                    var ConvertingLECoalesced = !CoalescedConverter.IsGame3Coalesced(SourcePath);
                    if (!Directory.Exists(Path.GetDirectoryName(DestinationPath) ?? DestinationPath))
                    {
                        Directory.CreateDirectory(DestinationPath);
                    }

                    if (ConvertingLECoalesced)
                    {
                        LECoalescedConverter.Unpack(SourcePath, DestinationPath);
                    }
                    else
                    {
                        CoalescedConverter.ConvertToXML(SourcePath, DestinationPath);
                    }
                    break;
                case CoalescedType.Xml:
                    CoalescedConverter.ConvertToBin(SourcePath, DestinationPath);
                    break;
                case CoalescedType.ExtractedBin:
                    var containingFolder = Path.GetDirectoryName(SourcePath);
                    LECoalescedConverter.Pack(containingFolder, DestinationPath);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }

        /// <summary>
        /// Enum describing a type of Coalesced file
        /// </summary>
        public enum CoalescedType
        {
            /// <summary>
            /// Compiled Coalesced file
            /// </summary>
            //[Display(Name = "Binary")]
            [Description("Binary Coalesced file.")]
            Binary,

            /// <summary>
            /// Decompiled Game 3 XML Coalesced manifest file
            /// </summary>
            // Todo: Make this say game3 to make it easier to infer at a glance
            //[Display(Name = "Xml")]
            [Description("Xml Coalesced file.")]
            Xml,

            // Todo: Make this say LE to make it easier to infer at a glance
            /// <summary>
            /// Unpacked LE1/LE2 Coalesced manifest file
            /// </summary>
            [Description("Unpacked LE Coalesced.")]
            ExtractedBin
        }

        // Todo: Make this method say Game3 to be less ambiguous
        /// <summary>
        /// Converts the Game3 Coalesced file to its XML representation (decompile)
        /// </summary>
        /// <param name="source">The input Coalesced file</param>
        /// <param name="destinationDirectory">The output directory</param>
        public static void ConvertToXML(string source, string destinationDirectory)
        {
            var sourcePath = source;
            var destinationPath = destinationDirectory;

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

        // Todo: Make this method say Game3 to be less ambiguous
        /// <summary>
        /// Serializes a Game3 Coalesced file from a mapping of text xml strings
        /// </summary>
        /// <param name="fileMapping">Mapping of filenames to their contents</param>
        /// <returns>Memorystream of the compiled Game3 Coalesced file</returns>
        public static MemoryStream CompileFromMemory(Dictionary<string, string> fileMapping)
        {
            var virtualizedXmlHeader = new XmlCoalesceFile();
            var assets = new List<CoalesceAsset>();
            //Virtual load assets.
            foreach (var include in fileMapping)
            {
                var asset = XmlCoalesceAsset.LoadFromMemory(include.Value);

                if (asset != null && !string.IsNullOrEmpty(asset.Source))
                {
                    assets.Add(asset);
                }
            }
            virtualizedXmlHeader.Assets = assets;

            var coal = new CoalescedFileXml
            {
                Version = 1
            };

            foreach (var asset in assets)
            {
                var entry =
                    new FileEntry(asset.Source)
                    {
                        Sections = new CaseInsensitiveDictionary<CaseInsensitiveDictionary<List<PropertyValue>>>()
                    };

                foreach (var section in asset.Sections)
                {
                    var eSection = new CaseInsensitiveDictionary<List<PropertyValue>>();

                    foreach (var property in section.Value)
                    {
                        var eProperty = new List<PropertyValue>();

                        foreach (var value in property.Value)
                        {
                            //if (!file.Settings.CompileTypes.Contains(value.ValueType))
                            //{
                            //    continue;
                            //}

                            var valueValue = value.Value;

                            if (!string.IsNullOrEmpty(valueValue))
                            {
                                valueValue = SpecialCharacters.Aggregate(valueValue, (current, c) => current.Replace(c.Value, c.Key));
                            }

                            eProperty.Add(new PropertyValue(value.ValueType, valueValue));
                        }

                        eSection.Add(property.Key.ToLower(), eProperty);
                    }

                    entry.Sections.Add(section.Key.ToLower(), eSection);
                }

                coal.Files.Add(entry);
            }

            MemoryStream outputStream = new MemoryStream();
            coal.Serialize(outputStream);
            outputStream.Position = 0;
            return outputStream;
        }

        // Todo: Make this method say Game3 to be less ambiguous
        /// <summary>
        /// Converts the source coalesced manifest xml to a Game3 Coalesced
        /// </summary>
        /// <param name="source">The source input file. Can be null if you are passing in an already-parsed XDocument on the <paramref name="preloadedDoc"/> paremeter</param>
        /// <param name="destination">Where the serialized file will be saved to</param>
        /// <param name="preloadedDoc">A preloaded XDocument object, in the event the document was already loaded by the caller for other purposes. If this value is set, <paramref name="source"/> is not used.</param>
        public static void ConvertToBin(string source, string destination, XDocument preloadedDoc = null)
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

            if (preloadedDoc == null) preloadedDoc = XDocument.Load(source);
            var file = XmlCoalesceFile.LoadXmlDocument(inputPath, preloadedDoc);

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
                        Sections = new CaseInsensitiveDictionary<CaseInsensitiveDictionary<List<PropertyValue>>>()
                    };

                foreach (var section in asset.Sections)
                {
                    var eSection = new CaseInsensitiveDictionary<List<PropertyValue>>();

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

                        eSection.Add(property.Key.ToLower(), eProperty);
                    }

                    entry.Sections.Add(section.Key.ToLower(), eSection);
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

        /// <summary>
        /// Returns true if this is a ME3/LE3 coalesced file, false if LE1/LE2
        /// </summary>
        /// <param name="sourceFile">Path to coalesced file</param>
        /// <returns></returns>
        public static bool IsGame3Coalesced(string sourceFile)
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

        /// <summary>
        /// Decompiles a LE1/LE2 Coalesced file to a memory map.
        /// </summary>
        /// <param name="inputStream">The input stream to read from</param>
        /// <param name="name">The name of the coalesced file - this is written to the manifest file and will be the name the file reserializes to (in tools such as M3)</param>
        /// <returns></returns>
        public static CaseInsensitiveDictionary<DuplicatingIni> DecompileLE1LE2ToMemory(Stream inputStream, string name)
        {
            return LECoalescedConverter.UnpackToMemory(inputStream, name).Files;
        }

        /// <summary>
        /// Compiles a LE1/LE2 Coalesced file from a memory map.
        /// </summary>
        /// <param name="iniFileMap">Mapping of filenames to the ini object that represents the file contents.</param>
        /// <returns>Memorystream of the compiled Coalesced file</returns>
        public static MemoryStream CompileLE1LE2FromMemory(Dictionary<string, DuplicatingIni> iniFileMap)
        {
            LECoalescedBundle cb = new LECoalescedBundle("");
            cb.Files.AddRange(iniFileMap);
            MemoryStream ms = new MemoryStream();
            cb.WriteToStream(ms);
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Decompiles the coalesced file (specified by the stream) to CoalescedAsset objects
        /// </summary>
        /// <param name="coalescedData">The data of the Coalesced</param>
        /// <param name="name">The name of the coalesced file - this is written to the manifest file data</param>
        /// <returns></returns>
        public static CaseInsensitiveDictionary<CoalesceAsset> DecompileLE1LE2ToAssets(Stream coalescedData, string name, bool stripExtensions = false)
        {
            var decompiled = DecompileLE1LE2ToMemory(coalescedData, name);
            var assets = new CaseInsensitiveDictionary<CoalesceAsset>();

            foreach (var decomp in decompiled)
            {
                if (stripExtensions)
                {
                    assets[Path.GetFileNameWithoutExtension(decomp.Key)] = ConfigFileProxy.ParseIni(decomp.Value.ToString()); // Technically this is extra work as we parsed from data -> DuplicatingIni -> string data -> Coalesced asset. This may be able to be improved by directly loading from data, but that would require a lot of API changes
                }
                else
                {
                    assets[decomp.Key] = ConfigFileProxy.ParseIni(decomp.Value.ToString()); // Technically this is extra work as we parsed from data -> DuplicatingIni -> string data -> Coalesced asset. This may be able to be improved by directly loading from data, but that would require a lot of API changes
                }
            }

            return assets;
        }

        /// <summary>
        /// Decompiles the ME3/LE3 coalesced file to CoalescedAsset objects
        /// </summary>
        /// <param name="coalescedData">The data of the Coalesced</param>
        /// <param name="name">The name of the coalesced file - this is written to the manifest file data</param>
        /// <returns></returns>
        public static CaseInsensitiveDictionary<CoalesceAsset> DecompileGame3ToAssets(string filePath, bool stripExtensions = false)
        {
            using var f = File.OpenRead(filePath);
            return DecompileGame3ToAssets(f, Path.GetFileName(filePath), stripExtensions: stripExtensions);
        }

        /// <summary>
        /// Decompiles the ME3/LE3 coalesced file (specified by the stream) to CoalescedAsset objects
        /// </summary>
        /// <param name="coalescedData">The data of the Coalesced</param>
        /// <param name="name">The name of the coalesced file - this is written to the manifest file data</param>
        /// <returns></returns>
        public static CaseInsensitiveDictionary<CoalesceAsset> DecompileGame3ToAssets(Stream coalescedData, string name, bool stripExtensions = false)
        {
            var decompiled = DecompileGame3ToMemory(coalescedData);
            var assets = new CaseInsensitiveDictionary<CoalesceAsset>();

            foreach (var decomp in decompiled)
            {
                if (stripExtensions)
                {
                    assets[Path.GetFileNameWithoutExtension(decomp.Key)] = XmlCoalesceAsset.LoadFromMemory(decomp.Value);
                }
                else
                {
                    assets[decomp.Key] = XmlCoalesceAsset.LoadFromMemory(decomp.Value);
                }
            }

            return assets;
        }
    }
}
