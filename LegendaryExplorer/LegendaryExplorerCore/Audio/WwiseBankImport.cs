using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Audio
{

    /// <summary>
    /// Used only for WwiseBank import
    /// </summary>
    internal class WwiseStreamedFileReference
    {
        public uint Id { get; set; }
        public string Language { get; set; }
        public string Shortname { get; set; }
        public string WemPath { get; set; }
    }

    /// <summary>
    /// Handles importing a .bnk file into a package file and setting up relevant data
    /// </summary>
    public static class WwiseBankImport
    {
        /// <summary>
        /// Handles importing a soundbank file from disk to the specified package. SoundbanksInfo.xml must exist next to the bank.
        /// </summary>
        /// <param name="bankPath">Path to the bnk file</param>
        /// <param name="useWwiseObjectNames">If we should use the wwise object names (in editor/left) (BankName.txt) or the on-disk filenames (SoundBankInfo.xml)</param>
        /// <param name="package">The target package to install to</param>
        /// <returns>String error message, or null if successful.</returns>
        public static string ImportBank(string bankPath, bool useWwiseObjectNames, IMEPackage package, string wwiseLanguage = null)
        {
            var bankName = Path.GetFileNameWithoutExtension(bankPath);
            var bankNameWithExtension = Path.GetFileName(bankPath);

            // TODO: Check if bank exists as an import - we can't import in this scenario!

            // Preprocessing
            var generatedDir = Directory.GetParent(bankPath).FullName;
            var soundBankInfo = Path.Combine(generatedDir, "SoundbanksInfo.xml");
            if (!File.Exists(soundBankInfo))
            {
                // Try parent. This may be localized
                soundBankInfo = Path.Combine(Directory.GetParent(soundBankInfo).Parent.FullName, "SoundbanksInfo.xml");
                if (!File.Exists(soundBankInfo))
                    return "SoundbanksInfo.xml file was not found next to the .bnk file or in the directory above it (localized)!";
            }


            // Get info about what we need to do
            var infoDoc = XDocument.Load(soundBankInfo);

            var allStreamedFiles = infoDoc.Root.Descendants("StreamedFiles").Descendants("File").Select(x => new WwiseStreamedFileReference()
            {
                Id = uint.Parse(x.Attribute("Id")?.Value),
                Language = x.Attribute("Language")?.Value,
                Shortname = x.Element("ShortName").Value,
                WemPath = x.Element("Path").Value
            }).ToList();

            if (useWwiseObjectNames)
            {
                var resultFile = Path.GetFileNameWithoutExtension(bankPath) + @".txt";
                var outputResults = Path.Combine(generatedDir, resultFile);
                if (!File.Exists(outputResults))
                    return $"{resultFile} file was not found next to the .bnk file!";

                var lines = File.ReadAllLines(outputResults);
                bool isParsing = false;
                foreach (var line in lines)
                {
                    if (!isParsing && line.StartsWith("Streamed Audio\tID\tName"))
                    {
                        // Start of results table
                        isParsing = true;
                        continue;
                    }

                    if (!isParsing || string.IsNullOrWhiteSpace(line))
                        continue; // Do not parse anything until we have hit the first line.

                    var info = line.Split('\t');
                    var id = uint.Parse(info[1]);
                    var streamedInfo = allStreamedFiles.FirstOrDefault(x => x.Id == id);
                    streamedInfo.Shortname = info[2];
                }
            }


            var xmlBankPath = wwiseLanguage == null ? bankNameWithExtension : $"{wwiseLanguage}\\{bankNameWithExtension}";
            var soundBankChunk = infoDoc.Root.Descendants("SoundBank").FirstOrDefault(x => x.Element("Path")?.Value == xmlBankPath);

            var eventInfos = soundBankChunk.Element("IncludedEvents").Descendants("Event").Select(x => new
            {
                Id = uint.Parse(x.Attribute("Id")?.Value),
                Name = x.Attribute("Name")?.Value
            }).ToList();

            var referencedStreamingAudioIds = soundBankChunk.Element("ReferencedStreamedFiles")?.Descendants("File")
                .Select(x => uint.Parse(x.Attribute("Id").Value));
            var referencedStreamingAudio = referencedStreamingAudioIds != null ? allStreamedFiles.Where(x => referencedStreamingAudioIds.Contains(x.Id)) : null;

            // Import the bank export 
            var parentPackage = package.FindEntry(bankName);
            if (parentPackage == null)
            {
                // Create container
                parentPackage = ExportCreator.CreatePackageExport(package, bankName);
            }

            ExportEntry bankExport = package.FindExport($"{bankName}.{bankName}");
            if (bankExport == null)
            {
                bankExport = ExportCreator.CreateExport(package, bankName, "WwiseBank", parentPackage, indexed: false);
            }

            bankExport.WriteProperty(new ObjectProperty(GetInitBankReference(package), "Parent"));
            // Id is stored as uint - we read as uint and then write as int as it's the same.
            bankExport.WriteProperty(new IntProperty((int)uint.Parse(soundBankChunk.Attribute("Id").Value), "Id"));
            WwiseBank.WriteBankRaw(File.ReadAllBytes(bankPath), bankExport);

            // Prepare the AFC
            var afcPath = Path.Combine(Directory.GetParent(package.FilePath).FullName, $"{bankName}.afc"); // Will need changed if localized!
            using var afcStream = File.Create(afcPath);


            // Import the streams
            List<ExportEntry> streamExports = new List<ExportEntry>();
            if (referencedStreamingAudio != null)
            {
                foreach (var streamInfo in referencedStreamingAudio)
                {
                    var exportName = GetExportNameFromShortname(streamInfo.Shortname);
                    var streamExport = package.FindExport($"{bankName}.{exportName}");
                    if (streamExport == null)
                    {
                        streamExport = ExportCreator.CreateExport(package, exportName, "WwiseStream", parentPackage,
                            indexed: false);
                    }

                    PropertyCollection p = new PropertyCollection();
                    if (package.Game == MEGame.LE3)
                    {
                        // LE3
                        p.Add(new NameProperty(bankName, "Filename"));
                        p.Add(new IntProperty((int)streamInfo.Id, "Id"));
                    }
                    else
                    {
                        // LE2
                        p.Add(new NameProperty(bankName, "Filename"));
                        p.Add(new NameProperty(bankName, "BankName"));
                        p.Add(new IntProperty((int)streamInfo.Id, "Id"));
                    }

                    var wemPath =
                        Path.Combine(generatedDir,
                            $"{streamInfo.Id}.wem"); // Seems to dump here for non-localized audio.
                    WwiseStream ws = new WwiseStream();
                    ws.Id = (int)streamInfo.Id;
                    ws.DataOffset = (int)afcStream.Position;
                    var wemData = File.ReadAllBytes(wemPath);
                    afcStream.Write(wemData);
                    ws.DataSize = wemData.Length;
                    ws.Filename = bankName; // This is needed internally for serialization
                    ws.BulkDataFlags = 0x1; // Stored externally, uncompressed

                    if (package.Game == MEGame.LE2)
                    {
                        // Not sure what these are but they are typically 0x1
                        ws.Unk1 = 0x1;
                        ws.Unk2 = 0x1;
                    }

                    streamExport.WritePropertiesAndBinary(p, ws);
                    streamExports.Add(streamExport);
                }
            }

            // Import the events
            foreach (var eventInfo in eventInfos)
            {
                var eventExport = package.FindExport($"{bankName}.{eventInfo.Name}");
                if (eventExport == null)
                {
                    eventExport = ExportCreator.CreateExport(package, eventInfo.Name, "WwiseEvent", parentPackage, indexed: false);
                }

                PropertyCollection p = new PropertyCollection();
                if (package.Game == MEGame.LE3)
                {
                    // LE3
                    p.Add(new StructProperty("WwiseRelationships", false,
                        new ObjectProperty(bankExport, "Bank"))
                    { Name = "Relationships" });
                    p.Add(new IntProperty((int)eventInfo.Id, "Id"));


                    p.Add(new FloatProperty(20, "DurationSeconds")); // TODO: FIGURE THIS OUT!!! THIS IS A PLACEHOLDER

                    // Todo: Write the WwiseStreams
                }
                else
                {
                    // LE2

                    var references = new ArrayProperty<StructProperty>("References");
                    var platProps = new PropertyCollection();

                    var platSpecificProps = new PropertyCollection();
                    platSpecificProps.Add(new ArrayProperty<ObjectProperty>(streamExports.Select(x => new ObjectProperty(x.UIndex)), "Streams"));
                    platSpecificProps.Add(new ObjectProperty(bankExport, "Bank"));
                    platProps.Add(new StructProperty("WwiseRelationships", platSpecificProps, "Relationships"));
                    platProps.Add(new IntProperty(1, "Platform"));
                    var platRef = new StructProperty("WwisePlatformRelationships", platProps);
                    references.Add(platRef);
                    p.Add(references);
                }

                WwiseEvent we = new WwiseEvent();
                we.WwiseEventID = eventInfo.Id;
                we.Links = new List<WwiseEvent.WwiseEventLink>();

                // LE3 puts this in binary instead of properties
                if (package.Game == MEGame.LE3)
                {
                    we.Links.Add(new WwiseEvent.WwiseEventLink()
                    { WwiseStreams = streamExports.Select(x => x.UIndex).ToList() });
                }
                else
                {
                    // LE2
                    we.WwiseEventID = eventInfo.Id; // ID is stored here
                }

                eventExport.WritePropertiesAndBinary(p, we);
            }

            return null;
        }

        private static string GetExportNameFromShortname(string shortname)
        {
            // Space, . -> _
            return shortname.Replace(".", "_").Replace(" ", "_"); // Add more rules here
        }

        /// <summary>
        /// Gets reference to Init bank
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        private static IEntry GetInitBankReference(IMEPackage package)
        {
            var entry = package.FindEntry("SFXWwise_Init.Init");
            if (entry != null) return entry; // Already exists

            var container = package.FindEntry("SFXWwise_Init");
            if (container == null)
            {
                container = new ImportEntry(package, null, "SFXWwise_Init")
                {
                    ClassName = "Package",
                    PackageFile = "Core"
                };
                package.AddImport((ImportEntry)container);
            }

            var init = new ImportEntry(package, container, "Init")
            {
                ClassName = "Wwisebank",
                PackageFile = "WwiseAudio",
            };
            package.AddImport((ImportEntry)init);
            return init;
        }
    }
}
