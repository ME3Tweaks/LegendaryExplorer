using System;
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
    /// <summary>
    /// Tools to scan for AFCs and references to AFCs, and perform AFC compaction of DLC mods
    /// </summary>
    public class AFCCompactor
    {
        /// <summary>
        /// Class for getting list of AFCs and their locations.
        /// </summary>
        private class AFCInventory
        {
            /// <summary>AFCs that reside in the given input path</summary>
            public readonly List<string> LocalFolderAFCFiles = new();
            /// <summary>AFCs that reside in the basegame</summary>
            public readonly List<string> BasegameAFCFiles = new();
            /// <summary>AFCs that reside in official DLC. This includes SFAR files.</summary>
            public List<string> OfficialDLCAFCFiles = new();

            /// <summary>Mapping of DLC folder name to a list of AFCs in that SFAR</summary>
            public readonly CaseInsensitiveDictionary<List<string>> SFARAFCsMap = new();

            /// <summary>
            /// Creates an inventory of AFC file paths for the given game and input folder
            /// </summary>
            /// <param name="inputPath">Folder to search for AFCs in. This would typically be a DLC_MOD folder.</param>
            /// <param name="game">Game to search for AFCs in. Basegame folder and all official DLC folders (including SFARs) will be searched</param>
            /// <param name="currentScanningFileCallback">Callback invoked on every AFC found with the path to the AFC</param>
            /// <param name="debugOut">Callback invoked with debug information on every AFC found</param>
            /// <returns>An <see cref="AFCInventory"/> of every .afc file found</returns>
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

        /// <summary>
        /// Represents a reference to a piece of audio in an AFC
        /// </summary>
        [DebuggerDisplay("RA {afcName} @ 0x{audioOffset.ToString(\"X8\")}")]
        public class ReferencedAudio
        {
            /// <summary>The name of the AFC containing this audio</summary>
            public string AFCName { get; set; }
            /// <summary>The offset into the AFC where this audio starts</summary>
            public long AudioOffset { get; set; }
            /// <summary>The size in bytes of this audio</summary>
            public long AudioSize { get; set; }
            /// <summary>The name of the export that references this audio</summary>
            public string OriginatingExportName { get; set; }
            /// <summary>A string representation of the source type of the AFC, for binding to a UI</summary>
            public string AFCSourceType { get; set; }
            /// <summary>If true, this reference does not lie in any known AFC boundaries from the vanilla game</summary>
            public bool IsModified { get; set; }
            /// <summary>If AFC is available. If not, this is broken audio</summary>
            public bool IsAvailable { get; set; }

            protected bool Equals(ReferencedAudio other)
            {
                return AFCName.Equals(other.AFCName, StringComparison.InvariantCultureIgnoreCase) && AudioOffset == other.AudioOffset && AudioSize == other.AudioSize;
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
                    var hashCode = (AFCName != null ? AFCName.ToLower().GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ AudioOffset.GetHashCode();
                    hashCode = (hashCode * 397) ^ AudioSize.GetHashCode();
                    return hashCode;
                }
            }
        }

        /// <summary>
        /// Compiles <see cref="ReferencedAudio"/> for every WwiseStream export in a given folder of package files
        /// </summary>
        /// <remarks>This method will look for AFCs in the game's basegame folder, all official DLC folders, and the input folder</remarks>
        /// <param name="game">Game you are operating on</param>
        /// <param name="inputPath">Path to folder to scan for WwiseStreams. All subdirectories will be searched for .pcc files</param>
        /// <param name="notifyProgress">Callback invoked after every package file is opened with (currentFileIndex, totalFileCount)</param>
        /// <param name="currentScanningFileCallback">Callback invoked after every package file is opened with the path to the current file</param>
        /// <param name="debugOut">Callback invoked with debug information on every export scanned and every AFC found</param>
        /// <returns>A tuple of ReferencedAudios for which no AFC was able to be found, and ReferencedAudios with verified AFCs</returns>
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
                                    AFCName = afcName,
                                    AudioSize = audioSize,
                                    AudioOffset = audioOffset,
                                    OriginatingExportName = exp.ObjectName,
                                    AFCSourceType = source,
                                    IsModified = isModified
                                });
                            }
                            else
                            {
                                debugOut?.Invoke($@" !!!! AFC NOT FOUND: {afcNameProp.Value}");

                                missingAFCReferences.Add(new ReferencedAudio()
                                {
                                    AFCName = afcNameProp.Value,
                                    AudioSize = audioSize,
                                    AudioOffset = audioOffset,
                                    OriginatingExportName = exp.ObjectName,
                                    AFCSourceType = "AFC unavailable"
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
            referencesToCompact = referencesToCompact.OrderBy(x => x.AFCName).ToList();

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
                if (referencedAudio.AFCName != currentOpenAfc)
                {
                    currentOpenAfcStream?.Dispose();
                    currentOpenAfcStream = fetchAfcStream(referencedAudio.AFCName, afcInventory, debugOut);
                    currentOpenAfc = referencedAudio.AFCName;
                }

                if (currentOpenAfcStream == null)
                {
                    Debug.WriteLine($"AFC could not be found: {referencedAudio.AFCName}");
                    brokenAudio.Add((referencedAudio, $"AFC could not be found:  {referencedAudio.AFCName}"));
                    continue;
                }

                var referencePos = memoryNewAfc.Position;
                try
                {
                    if (currentOpenAfcStream.Length <= referencedAudio.AudioOffset)
                    {
                        brokenAudio.Add((referencedAudio, $"Audio pointer is outside of AFC {referencedAudio.AFCName} @ 0x{referencedAudio.AudioOffset:X8}"));
                        continue;
                    }

                    if (currentOpenAfcStream.Length < referencedAudio.AudioOffset + referencedAudio.AudioSize)
                    {
                        brokenAudio.Add((referencedAudio, $"Audio size causes reference to extend beyond end of AFC {referencedAudio.AFCName} @ 0x{referencedAudio.AudioOffset:X8} for length 0x{referencedAudio.AudioSize:X6}. The AFC is only 0x{currentOpenAfcStream.Length:X8} in size"));
                        continue;
                    }

                    // Read header
                    currentOpenAfcStream.Position = referencedAudio.AudioOffset;
                    var header = currentOpenAfcStream.ReadStringLatin1(4);
                    if (header != "RIFF")
                    {
                        brokenAudio.Add((referencedAudio, $"Audio pointer doesn't point to data that doesn't start with the RIFF tag. This is an invalid pointer as all audio will start with RIFF. AFC {referencedAudio.AFCName} @ 0x{referencedAudio.AudioOffset:X8}"));
                        continue;
                    }

                    currentOpenAfcStream.Position = referencedAudio.AudioOffset;
                    currentOpenAfcStream.CopyToEx(memoryNewAfc, (int)referencedAudio.AudioSize);

                    referenceMap[referencedAudio] = new ReferencedAudio()
                    {
                        AFCName = newAFCBaseName,
                        AudioOffset = referencePos,
                        AudioSize = referencedAudio.AudioSize,
                    };
                }
                catch (Exception e)
                {
                    brokenAudio.Add((referencedAudio, $"AFC could not be found: {referencedAudio.AFCName} (Error: {e.Message})"));
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
                    { AFCName = wwiseStream.Filename, AudioSize = wwiseStream.DataSize, AudioOffset = wwiseStream.DataOffset };

                    if (referenceMap.TryGetValue(key, out var newInfo))
                    {
                        //Write new filename
                        exp.WriteProperty(new NameProperty(newInfo.AFCName, "FileName"));
                        byte[] newData = exp.Data;
                        // Write new offset
                        Buffer.BlockCopy(BitConverter.GetBytes((int)newInfo.AudioOffset), 0, newData,
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
