using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Gammtek.Extensions;
using ME3ExplorerCore.Gammtek.Extensions.Collections.Generic;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;

namespace ME3Explorer.PackageEditor.Experiments
{
    /// <summary>
    /// Class for Kinkojiro experimental code
    /// </summary>
    public class PackageEditorExperimentsK
    {
        /// <summary>
        /// Copy ME2 Static art and collision into an ME3 file.
        /// By Kinkojiro
        /// </summary>
        /// <param name="Game">Target Game</param>
        /// <param name="BioPSource">Source BioP</param>
        /// <param name="tgtOutputfolder">OutputFolder</param>
        /// <param name="BioArtsToCopy">List of level source file locations</param>
        /// <param name="ActorsToMove">Dicitionary: key Actors, value filename, entry uid</param>
        /// <param name="AssetsToMove">Dictionary key: AssetInstancedPath, value filename, isimport, entry uid</param>
        /// <param name="fromreload">is reloaded json</param>
        public static async Task<List<string>> ConvertLevelToGame(MEGame Game, IMEPackage BioPSource, string tgtOutputfolder, string tgttfc, Action<string> callbackAction, LevelConversionData conversionData = null, bool fromreload = false, bool createtestlevel = false)
        {
            //VARIABLES / VALIDATION
            var actorclassesToMove = new List<string>() { "BlockingVolume", "SpotLight", "SpotLightToggleable", "PointLight", "PointLightToggleable", "SkyLight", "HeightFog", "LenseFlareSource", "StaticMeshActor", "BioTriggerStream", "BioBlockingVolume" };
            var actorclassesToSubstitute = new Dictionary<string, string>()
            {
                { "BioBlockingVolume", "Engine.BlockingVolume" }
            };
            var archetypesToSubstitute = new Dictionary<string, string>()
            {
                { "Default__BioBlockingVolume", "Default__BlockingVolume" }
            };
            var fails = new List<string>();
            string busytext = null;
            if ((BioPSource.Game == MEGame.ME2 && ME2Directory.gamePath == null) || (BioPSource.Game == MEGame.ME1 && ME1Directory.gamePath == null) || (BioPSource.Game == MEGame.ME3 && ME3Directory.gamePath == null) || BioPSource.Game == MEGame.UDK)
            {
                fails.Add("Source Game Directory not found");
                return fails;
            }

            //Get filelist from BioP, Save a copy in outputdirectory, Collate Actors and asset information
            if (!fromreload)
            {
                busytext = "Collating level files...";
                callbackAction?.Invoke(busytext);
                conversionData = new LevelConversionData(Game, BioPSource.Game, null, null, null, new ConcurrentDictionary<string, string>(), new ConcurrentDictionary<string, (string, int)>(), new ConcurrentDictionary<string, (string, int, List<string>)>());
                var supportedExtensions = new List<string> { ".pcc", ".u", ".upk", ".sfm" };
                if (Path.GetFileName(BioPSource.FilePath).ToLowerInvariant().StartsWith("biop_") && BioPSource.Exports.FirstOrDefault(x => x.ClassName == "BioWorldInfo") is ExportEntry BioWorld)
                {
                    string biopname = Path.GetFileNameWithoutExtension(BioPSource.FilePath);
                    conversionData.GameLevelName = biopname.Substring(5, biopname.Length - 5);
                    var lsks = BioWorld.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels").ToList();
                    if (lsks.IsEmpty())
                    {
                        fails.Add("No files found in level.");
                        return fails;
                    }
                    foreach (var l in lsks)
                    {
                        var lskexp = BioPSource.GetUExport(l.Value);
                        var filename = lskexp.GetProperty<NameProperty>("PackageName");
                        if ((filename?.Value.ToString().ToLowerInvariant().StartsWith("bioa") ?? false) || (filename?.Value.ToString().ToLowerInvariant().StartsWith("biod") ?? false))
                        {
                            var filePath = Directory.GetFiles(ME2Directory.gamePath, $"{filename.Value.Instanced}.pcc", SearchOption.AllDirectories).FirstOrDefault();
                            conversionData.FilesToCopy.TryAdd(filename.Value.Instanced, filePath);
                        }
                    }
                    conversionData.BioPSource = $"{biopname}_{BioPSource.Game}";
                    BioPSource.Save(Path.Combine(tgtOutputfolder, $"{conversionData.BioPSource}.pcc"));
                    BioPSource.Dispose();
                }
                else
                {
                    fails.Add("Requires an BioP to work with.");
                    return fails;
                }

                busytext = "Collating actors and assets...";
                callbackAction?.Invoke(busytext);
                Parallel.ForEach(conversionData.FilesToCopy, (pccref) =>
                {
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(pccref.Value);
                    var sourcelevel = pcc.Exports.FirstOrDefault(l => l.ClassName == "Level");
                    if (ObjectBinary.From(sourcelevel) is Level levelbin)
                    {
                        foreach (var act in levelbin.Actors)
                        {
                            if (act < 1)
                                continue;
                            var actor = pcc.GetUExport(act);
                            if (actorclassesToMove.Contains(actor.ClassName))
                            {
                                conversionData.ActorsToMove.TryAdd($"{pccref.Key}.{actor.InstancedFullPath}", (pccref.Key, act));
                                HashSet<int> actorrefs = pcc.GetReferencedEntries(true, true, actor);
                                foreach (var r in actorrefs)
                                {
                                    var objref = pcc.GetEntry(r);
                                    if (objref != null)
                                    {
                                        if (objref.InstancedFullPath.Contains("PersistentLevel"))  //remove components of actors
                                            continue;
                                        string instancedPath = objref.InstancedFullPath;
                                        if (objref.idxLink == 0)
                                            instancedPath = $"{pccref.Key}.{instancedPath}";
                                        var added = conversionData.AssetsToMove.TryAdd(instancedPath, (pccref.Key, r, new List<string>() { pccref.Key }));
                                        if (!added)
                                        {
                                            conversionData.AssetsToMove[instancedPath].Item3.FindOrAdd(pccref.Key);
                                            if (r > 0 && conversionData.AssetsToMove[instancedPath].Item2 < 0) //Replace  imports with exports if possible
                                            {
                                                var currentlist = conversionData.AssetsToMove[instancedPath].Item3;
                                                conversionData.AssetsToMove[instancedPath] = (pccref.Key, r, currentlist);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        Debug.WriteLine($"File Scanned: {pccref.Key}");
                    }
                });

                //Add assets from BioP into list if exports are in source
                foreach (var assetImport in conversionData.AssetsToMove.Where(t => t.Value.Item2 < 0))
                {
                    var biopExport = BioPSource.Exports.FirstOrDefault(x => x.InstancedFullPath == assetImport.Key);
                    if (biopExport != null)
                    {
                        var currentlist = conversionData.AssetsToMove[assetImport.Key].Item3;
                        conversionData.AssetsToMove[assetImport.Key] = (conversionData.BioPSource, biopExport.UIndex, currentlist);
                    }
                }
            }


            //Create ME2TempAssetsFile & ME3TempAssetsFile
            var SRCTempFileName = Path.Combine(tgtOutputfolder, $"{conversionData.GameLevelName}_TempAssets_{conversionData.SourceGame}.pcc");
            var TGTTempFileName = Path.Combine(tgtOutputfolder, $"{conversionData.GameLevelName}_TempAssets_{conversionData.TargetGame}.pcc");
            if (!fromreload)
            {
                busytext = "Copying assets to temporary file...";
                callbackAction?.Invoke(busytext);
                MEPackageHandler.CreateAndSavePackage(SRCTempFileName, conversionData.SourceGame);
                using (var srcassetfile = MEPackageHandler.OpenMEPackage(SRCTempFileName))
                {
                    SortedDictionary<string, string> sortedfiles = new SortedDictionary<string, string>(conversionData.FilesToCopy);
                    var assetCloneSourceQueue = new Queue<(string, string)>(); //Queue of files to load and copy in - biop => bioa => biod => then rest
                    assetCloneSourceQueue.Enqueue((conversionData.BioPSource, Path.Combine(tgtOutputfolder, $"{conversionData.BioPSource}.pcc")));
                    string masterbioa = $"BioA_{conversionData.GameLevelName}";
                    if (sortedfiles.ContainsKey(masterbioa))
                    {
                        assetCloneSourceQueue.Enqueue((masterbioa, conversionData.FilesToCopy[masterbioa]));
                        sortedfiles.Remove(masterbioa);
                    }
                    string masterbiod = $"BioD_{conversionData.GameLevelName}";
                    if (conversionData.FilesToCopy.ContainsKey(masterbiod))
                    {
                        assetCloneSourceQueue.Enqueue((masterbiod, conversionData.FilesToCopy[masterbiod]));
                        sortedfiles.Remove(masterbiod);
                    }
                    foreach (var bioa in sortedfiles)
                    {
                        assetCloneSourceQueue.Enqueue((bioa.Key, bioa.Value));
                    }

                    while (!assetCloneSourceQueue.IsEmpty())
                    {
                        var fileref = assetCloneSourceQueue.Dequeue();  //fileref item1 = pcc name, item2 = path
                        var levelpkg = MEPackageHandler.OpenMEPackage(fileref.Item2);
                        var assetsinpkg = conversionData.AssetsToMove.Where(a => a.Value.Item1.ToLowerInvariant() == fileref.Item1.ToLowerInvariant()).ToList(); //assets item1 = fullinstancedpath, item2 = filename, item3 = export Uindex
                        if (!levelpkg.IsNull())
                        {
                            foreach (var asset in assetsinpkg)
                            {
                                try
                                {
                                    var gotentry = levelpkg.TryGetEntry(asset.Value.Item2, out IEntry assetexp);
                                    if (asset.Key.StartsWith(fileref.Item1) && gotentry)  //fix to local package if item is in class (prevents conflicts)  - any shadow/lightmaps need special handling.
                                    {
                                        var localpackagexp = levelpkg.Exports.FirstOrDefault(x => x.ObjectName.Instanced == fileref.Item1);
                                        if (localpackagexp != null)
                                        {
                                            IEntry srclocalpkg = srcassetfile.Exports.FirstOrDefault(x => x.ObjectName.Instanced == fileref.Item1);
                                            if (srclocalpkg.IsNull())
                                                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, localpackagexp, srcassetfile, null, false, out srclocalpkg);
                                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, assetexp, srcassetfile, srclocalpkg, true, out IEntry targetexp);
                                        }
                                        else
                                        {
                                            fails.Add($"Local Package not found on top level transfer: {fileref.Item2} : {asset.Key}");
                                        }
                                    }
                                    else if (gotentry)
                                    {
                                        var srcparent = EntryImporter.GetOrAddCrossImportOrPackage(assetexp.ParentFullPath, levelpkg, srcassetfile);
                                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, assetexp, srcassetfile, srcparent, true, out IEntry targetexp);
                                        Debug.WriteLine($"Asset Cloned Inbound: {fileref.Item1} : {asset.Key}");
                                    }
                                }
                                catch
                                {
                                    Debug.WriteLine($"Exception on asset {fileref.Item1} : {asset.Key}");
                                    fails.Add($"Exception on asset {fileref.Item1} : {asset.Key}");
                                }
                            }
                            levelpkg.Dispose();
                        }
                        else
                        {
                            Debug.WriteLine($"Asset Inbound Package load failure: {fileref.Item2}");
                            fails.Add($"Asset Inbound Package load failure: {fileref.Item2}");
                        }
                    }
                    srcassetfile.Save();

                    if (srcassetfile is MEPackage srcassetpack)
                    {
                        busytext = $"Converting to {conversionData.TargetGame}...";
                        callbackAction?.Invoke(busytext);
                        Debug.WriteLine($"Conversion started...");
                        if (tgttfc.IsNull())
                            tgttfc = $"Textures_{Path.GetFileName(tgtOutputfolder)}";
                        conversionData.TargetTFCName = tgttfc;
                        var tfcpath = Path.Combine(tgtOutputfolder, $"{tgttfc}.tfc");
                        srcassetpack.ConvertTo(conversionData.TargetGame, tfcpath, true);
                        srcassetpack.Save(TGTTempFileName);
                        srcassetpack.Dispose();
                        Debug.WriteLine($"Conversion finished.");
                    }
                    srcassetfile.Dispose();
                }

                //Export Settings to JSON
                busytext = "Exporting settings...";
                callbackAction?.Invoke(busytext);
                var srldata = JsonConvert.SerializeObject(conversionData);
                using (StreamWriter writer = File.CreateText(Path.Combine(tgtOutputfolder, $"{conversionData.GameLevelName}_Transfer.json")))
                {
                    writer.Write(srldata);
                }
                Debug.WriteLine($"Exported JSON");
            }

            //Create cooked files and populate with assets
            busytext = "Recooking assets out to individual files...";
            callbackAction?.Invoke(busytext);
            using (var assetfile = MEPackageHandler.OpenME3Package(TGTTempFileName))
            {
                foreach (var pccref in conversionData.FilesToCopy)
                {

                    var targetfile = Path.Combine(tgtOutputfolder, Path.GetFileName(pccref.Value));
                    if (createtestlevel)
                    {
                        if (pccref.Key.ToString().ToLowerInvariant().StartsWith("bioa"))
                            targetfile = Path.Combine(tgtOutputfolder, $"BioA_{conversionData.GameLevelName}_TEST.pcc");
                        else
                            targetfile = Path.Combine(tgtOutputfolder, $"BioD_{conversionData.GameLevelName}_TEST.pcc");
                        if (!File.Exists(targetfile))
                            File.Copy(Path.Combine(App.ExecFolder, "ME3EmptyLevel.pcc"), targetfile);
                    }
                    else
                    {
                        if (File.Exists(targetfile))
                        {
                            File.Delete(targetfile);
                        }
                        File.Copy(Path.Combine(App.ExecFolder, "ME3EmptyLevel.pcc"), targetfile);
                    }


                    using (var target = MEPackageHandler.OpenME3Package(targetfile))
                    using (var donor = MEPackageHandler.OpenME2Package(pccref.Value))
                    {
                        if (!createtestlevel)
                        {
                            for (int i = 0; i < target.Names.Count; i++)  //Setup new level file
                            {
                                string name = target.Names[i];
                                if (name.Equals("ME3EmptyLevel"))
                                {
                                    var newName = name.Replace("ME3EmptyLevel", Path.GetFileNameWithoutExtension(targetfile));
                                    target.replaceName(i, newName);
                                }
                            }
                            var packguid = Guid.NewGuid();
                            var package = target.GetUExport(1);
                            package.PackageGUID = packguid;
                            target.PackageGuid = packguid;
                            target.Save();
                        }
                        Debug.WriteLine($"Recooking outbound to {targetfile}");

                        var tgtlevel = target.GetUExport(target.Exports.FirstOrDefault(x => x.ClassName == "Level").UIndex);
                        //Get list of assets for this file & Process into
                        var sourceassets = conversionData.AssetsToMove.Where(s => s.Value.Item3.Contains(pccref.Key)).ToList();
                        foreach (var asset in sourceassets)
                        {
                            try
                            {
                                var assetexp = assetfile.Exports.FirstOrDefault(i => i.InstancedFullPath == asset.Key);
                                int assetUID = assetexp?.UIndex ?? 0;
                                if (assetUID == 0)
                                {
                                    var assetip = assetfile.Imports.FirstOrDefault(i => i.InstancedFullPath == asset.Key);
                                    assetUID = assetip?.UIndex ?? 0;
                                }
                                var gotasset = assetfile.TryGetEntry(assetUID, out IEntry assetent);
                                if (!gotasset)
                                {
                                    fails.Add($"Failure finding asset for outbound: {asset.Key}");
                                    continue;
                                }
                                IEntry tgtparent = null;
                                if (!asset.Key.StartsWith(pccref.Key))  //any shadow/lightmaps need special handling.
                                {
                                    tgtparent = EntryImporter.GetOrAddCrossImportOrPackage(assetent.ParentFullPath, assetfile, target);
                                }
                                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, assetent, target, tgtparent, true, out IEntry targetexp);
                            }
                            catch
                            {
                                Debug.WriteLine($"Failure cloning outbound: {asset.Key}");
                                fails.Add($"Failure cloning outbound: {asset.Key}");
                            }
                        }
                        target.Save();
                        target.Dispose();
                        donor.Dispose();
                    }
                }
                assetfile.Dispose();
            }

            //Create cooked files and populate with actors
            busytext = "Recooking actors out to individual files...";
            callbackAction?.Invoke(busytext);
            Parallel.ForEach(conversionData.FilesToCopy, (pccref) =>
            {
                var targetfile = Path.Combine(tgtOutputfolder, Path.GetFileName(pccref.Value));
                using (var target = MEPackageHandler.OpenME3Package(targetfile))
                using (var donor = MEPackageHandler.OpenME2Package(pccref.Value))
                {
                    Debug.WriteLine($"Recooking actors out to {pccref.Value}");
                    var tgtlevel = target.GetUExport(target.Exports.FirstOrDefault(x => x.ClassName == "Level").UIndex);
                    var sourceactors = conversionData.ActorsToMove.Where(a => a.Value.Item1 == pccref.Key).ToList();
                    var newactors = new List<ExportEntry>();
                    foreach (var sactor in sourceactors)
                    {
                        var sactorxp = donor.GetUExport(sactor.Value.Item2);
                        if (actorclassesToSubstitute.ContainsKey(sactorxp.ClassName))
                        {
                            var oldclass = sactorxp.ClassName;
                            var newclass = actorclassesToSubstitute[oldclass];
                            sactorxp.Class = donor.getEntryOrAddImport(newclass);
                            var stack = sactorxp.GetPrePropBinary();
                            stack.OverwriteRange(0, BitConverter.GetBytes(sactorxp.Class.UIndex));
                            stack.OverwriteRange(4, BitConverter.GetBytes(sactorxp.Class.UIndex));
                            sactorxp.SetPrePropBinary(stack);
                            var children = sactorxp.GetChildren();
                            foreach (var c in children)
                            {
                                if (c is ExportEntry child && archetypesToSubstitute.ContainsKey(child.Archetype?.ParentName ?? "None"))
                                {
                                    var oldarchlink = child.Archetype.ParentName;
                                    var n = donor.FindNameOrAdd(oldarchlink);
                                    donor.replaceName(n, archetypesToSubstitute[oldarchlink]);
                                }
                            }
                        }
                        if (sactorxp?.HasArchetype ?? false)
                        {
                            sactorxp.CondenseArchetypes(true);
                        }
                        if (sactorxp != null)
                        {
                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild, sactorxp, target, tgtlevel, true, out IEntry addedactor);
                            newactors.Add((ExportEntry)addedactor);
                        }

                    }
                    target.AddToLevelActorsIfNotThere(newactors.ToArray());
                    target.Save();
                    target.Dispose();
                    donor.Dispose();
                }
            });
            Debug.WriteLine($"Done.");
            return fails;
        }

        public async static Task<List<string>> RecookTransferLevelsFromJSON(string jsonfile, Action<string> callbackAction, bool CreateTestLevel = false)
        {
            var OutputDir = Path.GetDirectoryName(jsonfile);
            var conversionData = new LevelConversionData(MEGame.ME3, MEGame.ME2, null, null, null, new ConcurrentDictionary<string, string>(), new ConcurrentDictionary<string, (string, int)>(), new ConcurrentDictionary<string, (string, int, List<string>)>());
            IMEPackage sourcebiop = null;
            var fails = new List<string>();
            using (StreamReader file = File.OpenText(jsonfile))
            {
                JsonSerializer serializer = new JsonSerializer();
                conversionData = (LevelConversionData)serializer.Deserialize(file, typeof(LevelConversionData));
            }

            switch (conversionData.SourceGame)
            {
                case MEGame.ME2:
                    sourcebiop = MEPackageHandler.OpenME2Package(Path.Combine(OutputDir, $"{conversionData.BioPSource}.pcc"));
                    if (sourcebiop.IsNull())
                    {
                        fails.Add("Source BioP not found. Aborting.");
                        return fails;
                    }
                    break;
                default:
                    fails.Add("Currently level transfers only can be done ME2 to ME3");
                    return fails;
            }

            return await ConvertLevelToGame(conversionData.TargetGame, sourcebiop, OutputDir, conversionData.TargetTFCName, callbackAction, conversionData, true, CreateTestLevel);
        }

        public class LevelConversionData
        {
            public MEGame TargetGame = MEGame.ME3;
            public MEGame SourceGame = MEGame.ME2;
            public string GameLevelName = null;
            public string BioPSource = null;
            public string TargetTFCName = "Textures_DLC_MOD_";
            public ConcurrentDictionary<string, string> FilesToCopy = new ConcurrentDictionary<string, string>();
            public ConcurrentDictionary<string, (string, int)> ActorsToMove = new ConcurrentDictionary<string, (string, int)>();
            public ConcurrentDictionary<string, (string, int, List<string>)> AssetsToMove = new ConcurrentDictionary<string, (string, int, List<string>)>();

            public LevelConversionData(MEGame TargetGame, MEGame SourceGame, string GameLevelName, string BioPSource, string TargetTFCName, ConcurrentDictionary<string, string> FilesToCopy, ConcurrentDictionary<string, (string, int)> ActorsToMove, ConcurrentDictionary<string, (string, int, List<string>)> AssetsToMove)
            {
                this.TargetGame = TargetGame;
                this.SourceGame = SourceGame;
                this.GameLevelName = GameLevelName;
                this.BioPSource = BioPSource;
                this.TargetTFCName = TargetTFCName;
                this.FilesToCopy.AddRange(FilesToCopy);
                this.ActorsToMove.AddRange(ActorsToMove);
                this.AssetsToMove.AddRange(AssetsToMove);
            }

            public LevelConversionData()
            {

            }
        }

        public static void RestartTransferFromJSON(PackageEditorWPF pewpf, Action<EntryStringPair> entryDoubleClick)
        {
            CommonOpenFileDialog j = new CommonOpenFileDialog
            {
                DefaultExtension = ".json",
                EnsurePathExists = true,
                Title = "Select JSON with transfer details"
            };
            j.Filters.Add(new CommonFileDialogFilter("JSON files", "*.json"));
            if (j.ShowDialog(pewpf) == CommonFileDialogResult.Ok)
            {

                pewpf.BusyText = "Recook level files";
                pewpf.IsBusy = true;
                Task.Run(() =>
                        PackageEditorExperimentsK.RecookTransferLevelsFromJSON(j.FileName,
                            newText => pewpf.BusyText = newText))
                    .ContinueWithOnUIThread(prevTask =>
                    {
                        if (pewpf.Pcc != null)
                            pewpf.LoadFile(pewpf.Pcc.FilePath);
                        pewpf.IsBusy = false;
                        var dlg = new ListDialog(prevTask.Result, $"Conversion errors: ({prevTask.Result.Count})", "",
                            pewpf)
                        {
                            DoubleClickEntryHandler = entryDoubleClick
                        };
                        dlg.Show();
                    });
            }
        }

        public static void RecookLevelToTestFromJSON(PackageEditorWPF pewpf, Action<EntryStringPair> entryDoubleClick)
        {
            CommonOpenFileDialog j = new CommonOpenFileDialog
            {
                DefaultExtension = ".json",
                EnsurePathExists = true,
                Title = "Select JSON with transfer details"
            };
            j.Filters.Add(new CommonFileDialogFilter("JSON files", "*.json"));
            if (j.ShowDialog(pewpf) == CommonFileDialogResult.Ok)
            {

                pewpf.BusyText = "Recook level files";
                pewpf.IsBusy = true;
                Task.Run(() =>
                    PackageEditorExperimentsK.RecookTransferLevelsFromJSON(j.FileName, newText => pewpf.BusyText = newText,
                        true)).ContinueWithOnUIThread(prevTask =>
                {
                    if (pewpf.Pcc != null)
                        pewpf.LoadFile(pewpf.Pcc.FilePath);
                    pewpf.IsBusy = false;
                    var dlg = new ListDialog(prevTask.Result, $"Conversion errors: ({prevTask.Result.Count})", "", pewpf)
                    {
                        DoubleClickEntryHandler = entryDoubleClick
                    };
                    dlg.Show();
                });
            }
        }
    }
}
