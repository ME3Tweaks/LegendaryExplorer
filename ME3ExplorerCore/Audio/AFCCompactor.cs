using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using Newtonsoft.Json;

namespace ME3ExplorerCore.Audio
{
    public class AFCCompactor
    {
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
            /// If AFC is available. If not, this is broken audio
            /// </summary>
            public bool isAvailable { get; set; }
        }

        public static (List<ReferencedAudio> missingAFCReferences, List<ReferencedAudio> availableAFCReferences) GetReferencedAudio(MEGame game, string inputPath, Action<string> currentScanningFileCallback = null, Action<string> debugOut = null)
        {
            var sizesJsonStr = new StreamReader(Utilities.LoadFileFromCompressedResource("Infos.zip", $"{game}-vanillaaudiosizes.json")).ReadToEnd();
            var vanillaSizesMap = JsonConvert.DeserializeObject<CaseInsensitiveDictionary<int>>(sizesJsonStr);
            var pccFiles = Directory.GetFiles(inputPath, "*.pcc", SearchOption.AllDirectories);
            var localFolderAFCFiles = Directory.GetFiles(inputPath, "*.afc", SearchOption.AllDirectories);
            var referencedAFCAudio = new List<ReferencedAudio>();
            var missingAFCReferences = new List<ReferencedAudio>();
            int i = 1;

            debugOut?.Invoke($"Beginning AFC references scan. Input path: {inputPath}, game {game}");
            var basegameAFCFiles = MELoadedFiles.GetCookedFiles(game, MEDirectories.MEDirectories.BioGamePath(game), includeAFCs: true).Where(x => Path.GetExtension(x) == ".afc").ToList();
            foreach (var oafc in basegameAFCFiles)
            {
                debugOut?.Invoke($@" >> Found Basegame AFC {oafc}");
            }
            var officialDLCAFCFiles = MELoadedFiles.GetOfficialDLCFiles(game).Where(x => Path.GetExtension(x) == ".afc").ToList();

            foreach (var oafc in officialDLCAFCFiles)
            {
                debugOut?.Invoke($@" >> Found AFC in DLC directory {oafc}");
            }

            CaseInsensitiveDictionary<List<string>> sfarAFCFiles = new CaseInsensitiveDictionary<List<string>>();
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

                        DLCPackage dlc = new DLCPackage(sfarPath);
                        sfarAFCFiles[officialDLC] = dlc.Files.Where(x => x.FileName.EndsWith(".afc")).Select(x => x.FileName).ToList();
                        foreach (var oafc in sfarAFCFiles[officialDLC])
                        {
                            debugOut?.Invoke($@" >> Found SFAR AFC {oafc}");
                        }

                        officialDLCAFCFiles.AddRange(sfarAFCFiles[officialDLC]);
                    }
                }
            }

            foreach (string pccPath in pccFiles)
            {
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
                            var afcFile = localFolderAFCFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(afcNameProp.Value, StringComparison.InvariantCultureIgnoreCase));

                            bool logged = false;
                            if (afcFile != null)
                            {
                                debugOut?.Invoke($@" >>>> AFC found locally: {afcFile}");
                                logged = true;
                            }
                            if (afcFile == null)
                            {
                                // Try to find basegame version
                                afcFile = basegameAFCFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(afcNameProp.Value, StringComparison.InvariantCultureIgnoreCase));
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
                                afcFile = officialDLCAFCFiles.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(afcNameProp.Value, StringComparison.InvariantCultureIgnoreCase));
                                isOfficialDLC = afcFile != null;
                            }
                            if (afcFile != null && !logged)
                            {
                                debugOut?.Invoke($@" >>>> AFC found in official DLC: {afcFile}");
                                logged = true;
                            }

                            string afcName = afcNameProp.ToString().ToLower();
                            int audioSize = BitConverter.ToInt32(exp.Data, exp.Data.Length - 8);
                            int audioOffset = BitConverter.ToInt32(exp.Data, exp.Data.Length - 4);

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
                                    source = "Modified unofficial";
                                }

                                referencedAFCAudio.Add(new ReferencedAudio()
                                {
                                    afcName = afcName,
                                    audioSize = audioSize,
                                    audioOffset = audioOffset,
                                    uiOriginatingExportName = exp.ObjectName,
                                    uiAFCSourceType = source
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

        public static bool CompactAFC(MEGame game, string inputPath, string newAFCBaseName, List<ReferencedAudio> referencesToCompact, Func<List<(ReferencedAudio, string)>, bool> notifyBrokenAudio, Action<string> notifyStatusUpdate = null)
        {
            notifyStatusUpdate?.Invoke("Preparing to compact AFC");
            var localFolderAFCFiles = Directory.GetFiles(inputPath, "*.afc", SearchOption.AllDirectories).ToList();
            var basegameAFCFiles = MELoadedFiles.GetCookedFiles(game, MEDirectories.MEDirectories.BioGamePath(game), includeAFCs: true).Where(x => Path.GetExtension(x) == ".afc").ToList();
            var officialDLCAFCFiles = MELoadedFiles.GetOfficialDLCFiles(game).Where(x => Path.GetExtension(x) == ".afc").ToList();

            CaseInsensitiveDictionary<List<string>> sfarAFCFiles = new CaseInsensitiveDictionary<List<string>>();
            if (game == MEGame.ME3 && Directory.Exists(ME3Directory.DLCPath))
            {
                foreach (var officialDLC in ME3Directory.OfficialDLC)
                {
                    var sfarPath = Path.Combine(ME3Directory.DLCPath, officialDLC, "CookedPCConsole", "Default.sfar");
                    if (File.Exists(sfarPath))
                    {
                        DLCPackage dlc = new DLCPackage(sfarPath);
                        sfarAFCFiles[officialDLC] = dlc.Files.Where(x => x.FileName.EndsWith(".afc")).Select(x => x.FileName).ToList();
                        officialDLCAFCFiles.AddRange(sfarAFCFiles[officialDLC]);
                    }
                }
            }

            // Order by AFC name so we can just open a single stream to pull from rather than 800 times
            referencesToCompact = referencesToCompact.OrderBy(x => x.afcName).ToList();

            #region EXTRACT AND BUILD NEW AFC FILE
            string currentOpenAfc = null;
            Stream currentOpenAfcStream = null;

            notifyStatusUpdate?.Invoke("Creating reference map to new AFC");

            // Mapping of old reference => new reference
            var referenceMap = new Dictionary<AFCCompactor.ReferencedAudio, AFCCompactor.ReferencedAudio>();

            MemoryStream memoryNewAfc = new MemoryStream();
            var brokenAudio = new List<(ReferencedAudio brokenRef, string brokenReason)>();
            foreach (var referencedAudio in referencesToCompact)
            {
                if (referencedAudio.afcName != currentOpenAfc)
                {
                    currentOpenAfcStream?.Dispose();
                    currentOpenAfcStream = fetchAfcStream(referencedAudio.afcName, localFolderAFCFiles, basegameAFCFiles, sfarAFCFiles, officialDLCAFCFiles);
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
                    var header = currentOpenAfcStream.ReadStringASCII(4);
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
                    brokenAudio.Add((referencedAudio, $"AFC could not be found:  {referencedAudio.afcName}"));
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
            var finalAfcPath = Path.Combine(inputPath, $"{newAFCBaseName}.afc");
            var tempAfcPath = Path.Combine(inputPath, $"TEMP_{newAFCBaseName}.afc");
            memoryNewAfc.WriteToFile(tempAfcPath);
            #endregion

            #region UPDATE AUDIO REFERENCES
            notifyStatusUpdate?.Invoke("Updating audio references to point to new AFC");
            var pccFiles = Directory.GetFiles(inputPath, "*.pcc", SearchOption.AllDirectories);

            // Update audio references
            foreach (string pccPath in pccFiles)
            {
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

            return true;
        }

        private static Stream fetchAfcStream(string referencedAudioAfcName, List<string> localAfcFiles, List<string> basegameAfcFiles, CaseInsensitiveDictionary<List<string>> sfarAfcFiles, List<string> officialDlcafcFiles)
        {
            var fname = referencedAudioAfcName.ToLower() + ".afc";

            var localAFCFile = localAfcFiles.FirstOrDefault(x => Path.GetFileName(x).ToLower() == fname);

            if (localAFCFile != null)
            {
                return File.OpenRead(localAFCFile);
            }


            var basegameAFCFile = basegameAfcFiles.FirstOrDefault(x => Path.GetFileName(x).ToLower() == fname);
            if (basegameAFCFile != null)
            {
                return File.OpenRead(basegameAFCFile);
            }

            if (sfarAfcFiles != null)
            {
                var relevantDLC = sfarAfcFiles.FirstOrDefault(x => x.Value.Exists(y => Path.GetFileName(y).Equals(fname, StringComparison.InvariantCultureIgnoreCase)));
                if (relevantDLC.Key != null)
                {
                    DLCPackage dlc = new DLCPackage(Path.Combine(ME3Directory.DLCPath, relevantDLC.Key, "CookedPCConsole", "Default.sfar"));
                    return dlc.DecompressEntry(dlc.Files.First(x => Path.GetFileName(x.FileName).ToLower() == fname));
                }
            }

            var officialDlcFile = officialDlcafcFiles.FirstOrDefault(x => Path.GetFileName(x).ToLower() == fname);
            if (officialDlcFile != null)
            {
                return File.OpenRead(officialDlcFile);
            }

            // Could not find file! This shouldn't happen, technically...
            return null;
        }
    }
}
