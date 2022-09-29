using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Audio
{
    class WwiseEventImport
    {

    }

    /// <summary>
    /// Handles importing a .bnk file into a package file and setting up relevant data
    /// </summary>
    public static class WwiseBankImport
    {
        /// <summary>
        /// Handles importing a soundbank file from disk to the specified package. SoundbanksInfo.xml must exist next to the bank.
        /// </summary>
        /// <param name="bankPath"></param>
        /// <param name="package"></param>
        /// <returns>String error message, or null if successful.</returns>
        public static string ImportBank(string bankPath, IMEPackage package)
        {
            var bankName = Path.GetFileNameWithoutExtension(bankPath);
            var bankNameWithExtension = Path.GetFileName(bankPath);

            // TODO: Check if bank exists as an import - we can't import in this scenario!

            // Preprocessing
            var generatedDir = Directory.GetParent(bankPath).FullName;
            var soundBankInfo = Path.Combine(generatedDir, "SoundbanksInfo.xml");
            if (!File.Exists(soundBankInfo))
                return "SoundbanksInfo.xml file was not found next to the .bnk file!";


            // Get info about what we need to do
            var infoDoc = XDocument.Load(soundBankInfo);

            var allStreamedFiles = infoDoc.Root.Descendants("StreamedFiles").Select(x => new
            {
                Id = uint.Parse(x.Attribute("Id")?.Value),
                Language = x.Attribute("Language")?.Value,
                Shortname = x.Element("ShortName").Value,
                WemPath = x.Element("Path").Value
            }).ToList();


            var soundBankChunk = infoDoc.Root.Descendants("SoundBank").FirstOrDefault(x=>x.Element("Path")?.Value == bankNameWithExtension);

            var eventInfos = soundBankChunk.Descendants("IncludedEvents").Select(x => new
            {
                Id = uint.Parse(x.Attribute("Id")?.Value),
                Name = x.Attribute("Name")?.Value
            }).ToList();

            var referencedStreamingAudioIds = soundBankChunk.Element("ReferencedStreamFiles")?.Descendants("File")
                .Select(x => uint.Parse(x.Attribute("Id").Value));
            var referencedStreamingAudio = referencedStreamingAudioIds != null ? allStreamedFiles.Where(x=> referencedStreamingAudioIds.Contains(x.Id)) : null;

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
                bankExport = ExportCreator.CreateExport(package, bankName, "Wwisebank", parentPackage);
            }

            bankExport.WriteProperty(new ObjectProperty(GetInitBankReference(package), "Parent"));
            // Id is stored as uint - we read as uint and then write as int as it's the same.
            bankExport.WriteProperty(new IntProperty((int)uint.Parse(soundBankChunk.Attribute("Id").Value), "Id"));


            // Import the streams



            // Import the events
            foreach (var eventInfo in eventInfos)
            {
                var eventExport = package.FindExport($"{bankName}.{eventInfo.Name}");
                if (eventExport == null)
                {
                    eventExport = ExportCreator.CreateExport(package, eventInfo.Name, "WwiseEvent", parentPackage);
                }

                PropertyCollection p = new PropertyCollection();
                if (package.Game == MEGame.LE3)
                {
                    // LE3
                    p.Add(new StructProperty("WwiseRelationShips", false,
                        new ObjectProperty(bankExport, "Bank"), new NoneProperty()));
                    p.Add(new IntProperty((int)eventInfo.Id, "Id"));
                    p.Add(new FloatProperty(9, "Duration")); // TODO: FIGURE THIS OUT!!! THIS IS A PLACEHOLDER
                 
                    // Todo: Write the WwiseStreams
                }
                else
                {
                    // LE2
                }
                eventExport.WriteProperties(p);


            }








            return null;
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
