using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.Audio
{
    public class AFCCompactor
    {
        /// <summary>
        /// Class for getting list of AFCs and their locations.
        /// </summary>
        private class AFCInventory
        {
            public readonly List<string> LocalFolderAFCFiles = new();
            public readonly List<string> BasegameAFCFiles = new();
            /// <summary>
            /// AFCs that reside in official DLC. This includes SFAR files.
            /// </summary>
            public List<string> OfficialDLCAFCFiles = new();

            /// <summary>
            /// Mapping of DLC foldername to a list of AFCs in that SFAR
            /// </summary>
            public readonly CaseInsensitiveDictionary<List<string>> SFARAFCsMap = new();

            public static AFCInventory GetInventory(string inputPath, MEGame game, Action<string> currentScanningFileCallback = null, Action<string> debugOut = null)
            {
                var inventory = new AFCInventory();
                inventory.LocalFolderAFCFiles.ReplaceAll(Directory.GetFiles(inputPath, "*.afc", SearchOption.AllDirectories));


                debugOut?.Invoke($"Beginning AFC references scan. Input path: {inputPath}, game {game}");
                inventory.BasegameAFCFiles.ReplaceAll(MELoadedFiles.GetCookedFiles(game, GameFilesystem.MEDirectories.GetBioGamePath(game), includeAFCs: true).Where(x => Path.GetExtension(x) == ".afc"));
                foreach (var oafc in inventory.BasegameAFCFiles)
                {
                    debugOut?.Invoke($@" >> Found Basegame AFC {oafc}");
                }
                inventory.OfficialDLCAFCFiles = MELoadedDLC.GetOfficialDLCFolders(game).SelectMany(x => Directory.GetFiles(x, "*.afc", SearchOption.AllDirectories)).ToList();
                foreach (var oafc in inventory.OfficialDLCAFCFiles)
                {
                    debugOut?.Invoke($@" >> Found AFC in DLC directory {oafc}");
                }

                // ME3: Inspect SFARs for AFCs
                if (game == MEGame.ME3 && Directory.Exists(ME3Directory.DLCPath))
                {
                    debugOut?.Invoke($@"DLC directory: {ME3Directory.DLCPath}");
                    foreach (var officialDLC in ME3Directory.OfficialDLC)
                    {
                        var sfarPath = Path.Combine(ME3Directory.DLCPath, officialDLC, "CookedPCConsole", "Default.sfar");
                        if (File.Exists(sfarPath))
                        {
                            currentScanningFileCallback?.Invoke(ME3Directory.OfficialDLCNames[officialDLC]);
                            debugOut?.Invoke($@"{ME3Directory.OfficialDLCNames[officialDLC]} is installed ({officialDLC})");

                            var dlc = new DLCPackage(sfarPath);
                            inventory.SFARAFCsMap[officialDLC] = dlc.Files.Where(x => x.FileName.EndsWith(".afc")).Select(x => x.FileName).ToList();
                            foreach (var oafc in inventory.SFARAFCsMap[officialDLC])
                            {
                                debugOut?.Invoke($@" >> Found SFAR AFC {oafc}");
                            }

                            inventory.OfficialDLCAFCFiles.AddRange(inventory.SFARAFCsMap[officialDLC]);
                        }
                    }
                }
                return inventory;
            }
        }

        [DebuggerDisplay("RA {afcName} @ 0x{audioOffset.ToString(\"X8\")}")]
        public class ReferencedAudio
        {
            protected bool Equals(ReferencedAudio other)
            {
                return afcName.Equals(other.afcName, StringComparison.InvariantCultureIgnoreCase) && audioOffset == other.audioOffset && audioSize == other.audioSize;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ReferencedAudio)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (afcName != null ? afcName.ToLower().GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ audioOffset.GetHashCode();
                    hashCode = (hashCode * 397) ^ audioSize.GetHashCode();
                    return hashCode;
                }
            }

            public string afcName { get; set; }
            public long audioOffset { get; set; }
            public long audioSize { get; set; }
            public string uiOriginatingExportName { get; set; }
            public string uiAFCSourceType { get; set; }
            /// <summary>
            /// If this reference does not lie in any known AFC boundaries from the vanilla game
            /// </summary>
            public bool isModified { get; set; }
            /// <summary>
            /// If AFC is available. If not, this is broken audio
            /// </summary>
            public bool isAvailable { get; set; }
        }

        public static (List<ReferencedAudio> missingAFCReferences, List<ReferencedAudio> availableAFCReferences) GetReferencedAudio(MEGame game, string inputPath,
            Action<long,long> notifyProgress = null,
            Action<string> currentScanningFileCallback = null, 
            Action<string> debugOut = null)
        {
            var sizesJsonStr = new StreamReader(LegendaryExplorerCoreUtilities.LoadFileFromCompressedResource("Infos.zip", $"{game}-vanillaaudiosizes.json")).ReadToEnd();
            var vanillaSizesMap = JsonConvert.DeserializeObject<CaseInsensitiveDictionary<int>>(sizesJsonStr);
            var pccFiles = Directory.GetFiles(inputPath, "*.pcc", SearchOption.AllDirectories);

            AFCInventory afcInventory = AFCInventory.GetInventory(inputPath, game, debugOut);
            var referencedAFCAudio = new List<ReferencedAudio>();
            var missingAFCReferences = new List<ReferencedAudio>();
            int i = 1;

            foreach (string pccPath in pccFiles)
            {
                notifyProgress?.Invoke(i - 1, pccFiles.Count());
                debugOut?.Invoke($@"SCANNING {pccPath}");
                currentScanningFileCallback?.Invoke(pccPath);
                //NotifyStatusUpdate?.Invoke($"Finding all referenced audio ({i}/{pccFiles.Length})");
                using (var pack = MEPackageHandler.OpenMEPackage(pccPath))
                {
                    List<ExportEntry> wwiseStreamExports = pack.Exports.Where(x => x.ClassName == "WwiseStream").ToList();
                    foreach (ExportEntry exp in wwiseStreamExports)
                    {
                        debugOut?.Invoke($@" >> WwiseStream {exp.UIndex} {exp.ObjectName}");

                        var afcNameProp = exp.GetProperty<NameProperty>("Filename");
                        if (afcNameProp != null)
                        {
                            debugOut?.Invoke($@" >>>> AFC filename: {afcNameProp.Value.Name}");

                            bool isBasegame = false;
                            bool isOfficialDLC = false;
                            var afcFile = afcInventory.LocalFolderAFCFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(afcNameProp.Value, StringComparison.InvariantCultureIgnoreCase));

                            bool logged = false;
                            if (afcFile != null)
                            {
                                debugOut?.Invoke($@" >>>> AFC found locally: {afcFile}");
                                logged = true;
                            }
                            if (afcFile == null)
                            {
                                // Try to find basegame version
                                afcFile = afcInventory.BasegameAFCFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(afcNameProp.Value, StringComparison.InvariantCultureIgnoreCase));
                                isBasegame = afcFile != null;
                            }
                            if (afcFile != null && !logged)
                            {
                                debugOut?.Invoke($@" >>>> AFC found in basegame: {afcFile}");
                                logged = true;
                            }


                            if (afcFile == null)
                            {
                                // Try to find official DLC version
                                afcFile = afcInventory.OfficialDLCAFCFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(afcNameProp.Value, StringComparison.InvariantCultureIgnoreCase));
                                isOfficialDLC = afcFile != null;
                            }
                            if (afcFile != null && !logged)
                            {
                                debugOut?.Invoke($@" >>>> AFC found in official DLC: {afcFile}");
                                logged = true;
                            }

                            string afcName = afcNameProp.ToString().ToLower();
                            int audioSize = EndianReader.ToInt32(exp.DataReadOnly, exp.DataSize - 8, exp.FileRef.Endian);
                            int audioOffset = EndianReader.ToInt32(exp.DataReadOnly, exp.DataSize - 4, exp.FileRef.Endian);
                            bool isModified = false;
                            if (afcFile != null)
                            {
                                var source = !isBasegame && !isOfficialDLC ? "Modified" : null;
                                if (isBasegame || isOfficialDLC)
                                {
                                    // Check if offset indicates this is official bioware afc territory
                                    if (vanillaSizesMap.TryGetValue(afcName, out var vanillaSize))
                                    {
                                        if (audioOffset < vanillaSize)
                                        {
                                            if (isOfficialDLC)
                                            {
                                                source = "Official DLC";
                                            }
                                            else if (isBasegame)
                                            {
                                                debugOut?.Invoke($" >>>> Dropping fully basegame audio reference: {exp.ObjectName}");
                                                continue; //Fully basegame audio is never returned as it will always be available
                                            }
                                        }
                                        else
                                        {
                                            // Out of range?
                                            isModified = true;
                                            source = "Modified official DLC";
                                        }
                                    }
                                    else
                                    {
                                        debugOut?.Invoke($"!!! Vanilla sizes map doesn't include file being checked: {afcName}");
                                    }
                                }
                                else
                                {
                                    isModified = true;
                                    source = "Modified unofficial";
                                }

                                referencedAFCAudio.Add(new ReferencedAudio()
                                {
                                    afcName = afcName,
                                    audioSize = audioSize,
                                    audioOffset = audioOffset,
                                    uiOriginatingExportName = exp.ObjectName,
                                    uiAFCSourceType = source,
                                    isModified = isModified
                                });
                            }
                            else
                            {
                                debugOut?.Invoke($@" !!!! AFC NOT FOUND: {afcNameProp.Value}");

                                missingAFCReferences.Add(new ReferencedAudio()
                                {
                                    afcName = afcNameProp.Value,
                                    audioSize = audioSize,
                                    audioOffset = audioOffset,
                                    uiOriginatingExportName = exp.ObjectName,
                                    uiAFCSourceType = "AFC unavailable"
                                });
                            }
                        }
                    }
                }
                i++;
            }
            referencedAFCAudio = referencedAFCAudio.Distinct().ToList();
            return (missingAFCReferences, referencedAFCAudio);
        }

        public static bool CompactAFC(MEGame game, string inputPath, string newAFCBaseName, List<ReferencedAudio> referencesToCompact,
            Func<List<(ReferencedAudio, string)>, bool> notifyBrokenAudio,
            Action<long, long> notifyProgress,
            Action<string> notifyStatusUpdate = null,
            Action<string> notifyFinalAfcPath = null,
            Action<string> debugOut = null)
        {
            notifyStatusUpdate?.Invoke("Preparing to compact AFC");

            var afcInventory = AFCInventory.GetInventory(inputPath, game, notifyStatusUpdate, null); //Null lets me know there's something here to add for debug
            // Order by AFC name so we can just open a single stream to pull from rather than 800 times
            referencesToCompact = referencesToCompact.OrderBy(x => x.afcName).ToList();

            #region EXTRACT AND BUILD NEW AFC FILE
            string currentOpenAfc = null;
            Stream currentOpenAfcStream = null;

            notifyStatusUpdate?.Invoke("Creating reference map to new AFC");

            // Mapping of old reference => new reference
            var referenceMap = new Dictionary<AFCCompactor.ReferencedAudio, AFCCompactor.ReferencedAudio>();

            using MemoryStream memoryNewAfc = MemoryManager.GetMemoryStream();
            var brokenAudio = new List<(ReferencedAudio brokenRef, string brokenReason)>();
            int i = 0;
            foreach (var referencedAudio in referencesToCompact)
            {
                notifyProgress?.Invoke(i, referencesToCompact.Count);
                i++;
                if (referencedAudio.afcName != currentOpenAfc)
                {
                    currentOpenAfcStream?.Dispose();
                    currentOpenAfcStream = fetchAfcStream(referencedAudio.afcName, afcInventory, debugOut);
                    currentOpenAfc = referencedAudio.afcName;
                }

                if (currentOpenAfcStream == null)
                {
                    Debug.WriteLine($"AFC could not be found: {referencedAudio.afcName}");
                    brokenAudio.Add((referencedAudio, $"AFC could not be found:  {referencedAudio.afcName}"));
                    continue;
                }

                var referencePos = memoryNewAfc.Position;
                try
                {
                    if (currentOpenAfcStream.Length <= referencedAudio.audioOffset)
                    {
                        brokenAudio.Add((referencedAudio, $"Audio pointer is outside of AFC {referencedAudio.afcName} @ 0x{referencedAudio.audioOffset:X8}"));
                        continue;
                    }

                    if (currentOpenAfcStream.Length < referencedAudio.audioOffset + referencedAudio.audioSize)
                    {
                        brokenAudio.Add((referencedAudio, $"Audio size causes reference to extend beyond end of AFC {referencedAudio.afcName} @ 0x{referencedAudio.audioOffset:X8} for length 0x{referencedAudio.audioSize:X6}. The AFC is only 0x{currentOpenAfcStream.Length:X8} in size"));
                        continue;
                    }

                    // Read header
                    currentOpenAfcStream.Position = referencedAudio.audioOffset;
                    var header = currentOpenAfcStream.ReadStringLatin1(4);
                    if (header != "RIFF")
                    {
                        brokenAudio.Add((referencedAudio, $"Audio pointer doesn't point to data that doesn't start with the RIFF tag. This is an invalid pointer as all audio will start with RIFF. AFC {referencedAudio.afcName} @ 0x{referencedAudio.audioOffset:X8}"));
                        continue;
                    }

                    currentOpenAfcStream.Position = referencedAudio.audioOffset;
                    currentOpenAfcStream.CopyToEx(memoryNewAfc, (int)referencedAudio.audioSize);

                    referenceMap[referencedAudio] = new ReferencedAudio()
                    {
                        afcName = newAFCBaseName,
                        audioOffset = referencePos,
                        audioSize = referencedAudio.audioSize,
                    };
                }
                catch (Exception e)
                {
                    brokenAudio.Add((referencedAudio, $"AFC could not be found: {referencedAudio.afcName} (Error: {e.Message})"));
                }

                var test = referenceMap[referencedAudio];
            }
            currentOpenAfcStream?.Dispose();
            Debug.WriteLine($"New AFC size: 0x{memoryNewAfc.Length:X8} ({FileSize.FormatSize(memoryNewAfc.Length)})");

            if (brokenAudio.Any())
            {
                var shouldContinue = notifyBrokenAudio?.Invoke(brokenAudio);
                if (!shouldContinue.HasValue || !shouldContinue.Value)
                {
                    return false;
                }
            }

            // Write temp to make sure we don't update references and then find out we can't actually write to disk
            var finalAfcPath = inputPath;
            if (!Path.GetFileName(finalAfcPath).StartsWith("CookedPC", StringComparison.InvariantCultureIgnoreCase))
            {
                finalAfcPath = Path.Combine(finalAfcPath, MEDirectories.CookedName(game));
            }
            finalAfcPath = Path.Combine(finalAfcPath, $"{newAFCBaseName}.afc");
            var tempAfcPath = Path.Combine(inputPath, $"TEMP_{newAFCBaseName}.afc");
            memoryNewAfc.WriteToFile(tempAfcPath);
            #endregion

            #region UPDATE AUDIO REFERENCES
            notifyStatusUpdate?.Invoke("Updating audio references to point to new AFC");
            var pccFiles = Directory.GetFiles(inputPath, "*.pcc", SearchOption.AllDirectories);

            // Update audio references
            i = 0;
            foreach (string pccPath in pccFiles)
            {
                notifyProgress?.Invoke(i, pccFiles.Count());
                i++;
                notifyStatusUpdate?.Invoke($"Updating {Path.GetFileName(pccPath)}");
                using var pack = MEPackageHandler.OpenMEPackage(pccPath);
                bool shouldSave = false;
                List<ExportEntry> wwiseStreamExports = pack.Exports.Where(x => x.ClassName == "WwiseStream").ToList();
                foreach (ExportEntry exp in wwiseStreamExports)
                {
                    // Check if this needs updated by finding it in the reference map
                    var wwiseStream = ObjectBinary.From<WwiseStream>(exp);
                    if (wwiseStream.IsPCCStored) continue; //Nothing to update here

                    var key = new ReferencedAudio()
                    { afcName = wwiseStream.Filename, audioSize = wwiseStream.DataSize, audioOffset = wwiseStream.DataOffset };

                    if (referenceMap.TryGetValue(key, out var newInfo))
                    {
                        //Write new filename
                        exp.WriteProperty(new NameProperty(newInfo.afcName, "FileName"));
                        byte[] newData = exp.Data;
                        // Write new offset
                        Buffer.BlockCopy(BitConverter.GetBytes((int)newInfo.audioOffset), 0, newData,
                            newData.Length - 4, 4); //update AFC audio offset
                        exp.Data = newData;

                        //don't mark for saving if the data didn't actually change (e.g. trying to compact a compacted AFC).
                        shouldSave |= exp.DataChanged;
                    }
                }
                if (shouldSave)
                {
                    pack.Save();
                }
            }
            #endregion

            // write final afc
            if (File.Exists(finalAfcPath))
                File.Delete(finalAfcPath);
            File.Move(tempAfcPath, finalAfcPath);
            notifyFinalAfcPath?.Invoke(finalAfcPath);
            return true;
        }

        private static Stream fetchAfcStream(string referencedAudioAfcName, AFCInventory inventory, Action<string> debugOut = null)
        {
            var fname = referencedAudioAfcName.ToLower() + ".afc";

            var localAFCFile = inventory.LocalFolderAFCFiles.FirstOrDefault(x => Path.GetFileName(x).ToLower() == fname);

            if (localAFCFile != null)
            {
                return File.OpenRead(localAFCFile);
            }


            var basegameAFCFile = inventory.BasegameAFCFiles.FirstOrDefault(x => Path.GetFileName(x).ToLower() == fname);
            if (basegameAFCFile != null)
            {
                return File.OpenRead(basegameAFCFile);
            }

            if (inventory.SFARAFCsMap.Any())
            {
                var relevantDLC = inventory.SFARAFCsMap.FirstOrDefault(x => x.Value.Exists(y => Path.GetFileName(y).Equals(fname, StringComparison.InvariantCultureIgnoreCase)));
                if (relevantDLC.Key != null)
                {
                    DLCPackage dlc = new DLCPackage(Path.Combine(ME3Directory.DLCPath, relevantDLC.Key, "CookedPCConsole", "Default.sfar"));
                    return dlc.DecompressEntry(dlc.Files.First(x => Path.GetFileName(x.FileName).ToLower() == fname));
                }
            }

            var officialDlcFile = inventory.OfficialDLCAFCFiles.FirstOrDefault(x => Path.GetFileName(x).ToLower() == fname);
            if (officialDlcFile != null)
            {
                return File.OpenRead(officialDlcFile);
            }

            debugOut?.Invoke($"!!! Could not find AFC {referencedAudioAfcName} for compaction!");
            // Could not find file! This shouldn't happen, technically...
            return null;
        }
    }
}
