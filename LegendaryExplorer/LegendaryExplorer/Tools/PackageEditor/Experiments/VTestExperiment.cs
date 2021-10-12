using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.PathfindingEditor;
using LegendaryExplorer.Tools.Sequence_Editor.Experiments;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    public class VTestExperiment
    {

        class VTestOptions
        {
            #region Configurable options
            /// <summary>
            /// List of levels to port. DO NOT INCLUDE BIOA_.
            /// </summary>
            public string[] vTestLevels = new[]
            {
                // Comment/uncomment these to select which files to run on
                "PRC2",
                "PRC2AA"
            };

            /// <summary>
            /// If lightmaps and shadowmaps should be stripped and dynamic lighting turned on
            /// </summary>
            public bool useDynamicLighting = true;

            /// <summary>
            /// If debug features should be enabled in the build
            /// </summary>
            public bool debugBuild = true;

            /// <summary>
            /// The cache that is passed through to sub operations. You can change the
            /// CacheMaxSize to tune memory usage vs performance.
            /// </summary>
            public PackageCache cache = new PackageCache() { CacheMaxSize = 20 };
            #endregion

            #region Autoset options - Do not change these
            public PackageEditorWindow packageEditorWindow;
            public IMEPackage vTestHelperPackage;
            public ObjectInstanceDB objectDB;
            #endregion
        }


        #region Vars
        /// <summary>
        /// List of things to port when porting a level with VTest
        /// </summary>
        private static string[] ClassesToVTestPort = new[]
        {
            "InterpActor",
            "BioInert",
            "BioUsable",
            "BioPawn",
            "SkeletalMeshActor",
            "PostProcessVolume",
            "BioMapNote",
            "Note",
            "BioTrigger",
            "BioSunActor",
            "BlockingVolume",
            "BioDoor",
            "StaticMeshCollectionActor",
            "StaticLightCollectionActor",
            "ReverbVolume",
            "BioAudioVolume",
            "AmbientSound",
            "BioLedgeMeshActor",
            "BioStage",
            "HeightFog",
            "PrefabInstance",
            "CameraActor",
            //"Terrain", // Do not port in - we will specifically port this with a special donor system

            // Pass 2
            "StaticMeshActor",
            "TriggerVolume",
            "BioSquadCombat",
            "PhysicsVolume",
            "BioWp_ActionStation",
            "BioLookAtTarget",
            "BioUsable",
            "BioContainer",

            // Pass 3
            "Brush",
            "PathNode",
            "BioCoverVolume",
            "BioTriggerVolume",
            "BioWp_AssaultPoint",
            "BioSquadPlayer",
            "BioUseable",
            "BioSquadSitAndShoot",
            "CoverLink",
            "BioWaypointSet",
            "BioPathPoint",
            "Emitter"

        };

        /// <summary>
        /// Classes to port only for master level files
        /// </summary>
        private static string[] ClassesToVTestPortMasterOnly = new[]
        {
            "PlayerStart",
            "BioTriggerStream"
        };

        // Files we know are referenced by name but do not exist
        private static string[] VTest_NonExistentBTSFiles =
        {
            "bioa_prc2_ccahern_l",
            "bioa_prc2_cccave01",
            "bioa_prc2_cccave02",
            "bioa_prc2_cccave03",
            "bioa_prc2_cccave04",
            "bioa_prc2_cccrate01",
            "bioa_prc2_cccrate02",
            "bioa_prc2_cclobby01",
            "bioa_prc2_cclobby02",
            "bioa_prc2_ccmid01",
            "bioa_prc2_ccmid02",
            "bioa_prc2_ccmid03",
            "bioa_prc2_ccmid04",
            "bioa_prc2_ccscoreboard",
            "bioa_prc2_ccsim01",
            "bioa_prc2_ccsim02",
            "bioa_prc2_ccsim03",
            "bioa_prc2_ccsim04",
            "bioa_prc2_ccspace02",
            "bioa_prc2_ccspace03",
            "bioa_prc2_ccthai01",
            "bioa_prc2_ccthai02",
            "bioa_prc2_ccthai03",
            "bioa_prc2_ccthai04",
            "bioa_prc2_ccthai05",
            "bioa_prc2_ccthai06",
        };

        // This is list of materials to run a conversion to a MaterialInstanceConstant
        // List is not long cause not a lot of materials support this...
        private static string[] vtest_DonorMaterials = new[]
        {
            "BIOA_MAR10_T.UNC_HORIZON_MAT_Dup",
        };

        #endregion

        #region Kinda Hacky Vars
        /// <summary>
        /// List of all actor classes that were encountered during the last VTest session. Resets at the start of every VTest.
        /// </summary>
        internal static List<string> actorTypesNotPorted;

        #endregion

        #region Main porting methods

        /// <summary>
        /// Runs the main VTest
        /// </summary>
        /// <param name="pe"></param>
        /// <param name="installAndBootGame"></param>
        public static async void VTest(PackageEditorWindow pe, bool? installAndBootGame = null)
        {
            // Prep
            EntryImporter.NonDonorMaterials.Clear();
            actorTypesNotPorted = new List<string>();

            if (installAndBootGame == null)
            {
                var result = MessageBox.Show(pe, "Install VTest and run the game when VTest completes? PAEMPaths must be set.", "Auto install and boot", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                installAndBootGame = result == MessageBoxResult.Yes;
            }

            // This object is passed through to all the methods so we don't have to constantly update the signatures
            var vTestOptions = new VTestOptions()
            {
                packageEditorWindow = pe,
            };

            pe.SetBusy("Performing VTest");
            await Task.Run(() =>
            {
                RunVTest(vTestOptions);
            }).ContinueWithOnUIThread(result =>
            {
                if (result.Exception != null)
                    throw result.Exception;
                pe.EndBusy();
                if (installAndBootGame != null && installAndBootGame.Value)
                {
                    var mmPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\ME3Tweaks", "ExecutableLocation", null);
                    if (mmPath != null && File.Exists(mmPath))
                    {

                        var moddesc = Path.Combine(Directory.GetParent(PAEMPaths.VTest_DonorsDir).FullName, "moddesc.ini");
                        if (File.Exists(moddesc))
                        {
                            ProcessStartInfo psi = new ProcessStartInfo(mmPath, $"--installmod \"{moddesc}\" --bootgame LE1");
                            Process.Start(psi);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Internal single-thread VTest session
        /// </summary>
        /// <param name="vTestOptions"></param>
        private static void RunVTest(VTestOptions vTestOptions)
        {
            Debug.WriteLine("Beginning VTest");
            Debug.WriteLine($"Cache GUID: {vTestOptions.cache.guid}");
            string dbPath = AppDirectories.GetObjectDatabasePath(MEGame.LE1);
            //string matPath = AppDirectories.GetMaterialGuidMapPath(MEGame.ME1);
            //Dictionary<Guid, string> me1MaterialMap = null;
            vTestOptions.packageEditorWindow.BusyText = "Loading databases";

            if (File.Exists(dbPath))
            {
                vTestOptions.objectDB = ObjectInstanceDB.DeserializeDB(File.ReadAllText(dbPath));
                vTestOptions.objectDB.BuildLookupTable(); // Lookup table is required as we are going to compile things
                vTestOptions.packageEditorWindow.BusyText = "Inventorying donors";

                // Add extra donors and VTestHelper package
                foreach (var file in Directory.GetFiles(PAEMPaths.VTest_DonorsDir))
                {
                    if (file.RepresentsPackageFilePath())
                    {
                        if (Path.GetFileNameWithoutExtension(file) == "VTestHelper")
                        {
                            // Load the VTestHelper, don't index it
                            vTestOptions.vTestHelperPackage = MEPackageHandler.OpenMEPackage(file, forceLoadFromDisk: true); // Do not put into cache
                        }
                        else
                        {
                            // Inventory
                            using var p = MEPackageHandler.OpenMEPackage(file);
                            PackageEditorExperimentsM.IndexFileForObjDB(vTestOptions.objectDB, MEGame.LE1, p);
                        }

                    }
                }
            }
            else
            {
                return;
            }


            // Unused for now, maybe forever
            //if (File.Exists(matPath))
            //{
            //    me1MaterialMap = JsonConvert.DeserializeObject<Dictionary<Guid, string>>(File.ReadAllText(matPath));
            //}hel
            //else
            //{
            //    return;
            //}

            vTestOptions.packageEditorWindow.BusyText = "Clearing mod folder";
            // Clear out dest dir
            foreach (var f in Directory.GetFiles(PAEMPaths.VTest_FinalDestDir))
            {
                File.Delete(f);
            }

            // Copy in precomputed files
            vTestOptions.packageEditorWindow.BusyText = "Copying precomputed files";
            foreach (var f in Directory.GetFiles(PAEMPaths.VTest_PrecomputedDir))
            {
                File.Copy(f, Path.Combine(PAEMPaths.VTest_FinalDestDir, Path.GetFileName(f)));
            }

            vTestOptions.packageEditorWindow.BusyText = "Running VTest";

            // VTest Level Loop ---------------------------------------
            foreach (var vTestLevel in vTestOptions.vTestLevels)
            {
                var levelFiles = Directory.GetFiles(Path.Combine(PAEMPaths.VTest_SourceDir, vTestLevel));
                foreach (var f in levelFiles)
                {
                    if (f.Contains("_LOC_", StringComparison.InvariantCultureIgnoreCase))
                    {
                        PortLOCFile(f, vTestOptions);
                    }
                    else
                    {
                        var levelName = Path.GetFileNameWithoutExtension(f);
                        //if (levelName.CaseInsensitiveEquals("BIOA_PRC2_CCSIM05_DSG"))
                        PortVTestLevel(vTestLevel, levelName, vTestOptions, levelName == "BIOA_" + vTestLevel, true);
                    }
                }
            }

            vTestOptions.cache.ReleasePackages(true); // Dump everything out of memory

            Debug.WriteLine("Non donated materials: ");
            foreach (var nonDonorMaterial in EntryImporter.NonDonorMaterials)
            {
                Debug.WriteLine(nonDonorMaterial);
            }

            Debug.WriteLine("Actor classes that were not ported:");
            foreach (var ac in actorTypesNotPorted)
            {
                Debug.WriteLine(ac);
            }

            // VTest post QA
            vTestOptions.packageEditorWindow.BusyText = "Performing checks";


            // TERRAIN DEBUGGING
            //using var testTerrainP = MEPackageHandler.OpenMEPackage(Path.Combine(PAEMPaths.VTest_SourceDir, "PRC2AA", "testterrain.sfm"));
            // You need to make sure you use the right filepath here
            //using var testTerrainP = MEPackageHandler.OpenMEPackage(Path.Combine(ME1Directory.BioGamePath, @"CookedPC\Maps\LAV\LAY\BIOA_LAV70_01_LAY.sfm"));
            //using var destTerrainP = MEPackageHandler.OpenMEPackage(Path.Combine(PAEMPaths.VTest_FinalDestDir, "BIOA_PRC2.pcc"));

            //var terrainExp = testTerrainP.FindExport("TheWorld.PersistentLevel.Terrain_1");
            ////PathEdUtils.SetLocation(terrainExp, -1115, 420, -845); // move to area we can see
            //PrePortingCorrections(testTerrainP);
            //EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, terrainExp,
            //    destTerrainP, destTerrainP.FindExport("TheWorld.PersistentLevel"), true,
            //    new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var destTerrainExp);

            //var terrBin = ObjectBinary.From<Terrain>(destTerrainExp as ExportEntry);
            ////terrBin.CachedDisplacements = new byte[terrBin.Heights.Length]; // just a big fat empty list
            //(destTerrainExp as ExportEntry).WriteBinary(terrBin);

            //RebuildPersistentLevelChildren(destTerrainP.FindExport("TheWorld.PersistentLevel"));
            //destTerrainP.Save();

            // Perform checks on all files
            foreach (var f in Directory.GetFiles(PAEMPaths.VTest_FinalDestDir))
            {
                if (f.RepresentsPackageFilePath())
                {
                    using var p = MEPackageHandler.OpenMEPackage(f);

                    VTestCheckImports(p, vTestOptions);
                    VTestCheckTextures(p, vTestOptions);
                }
            }

        }

        private static void VTestCheckTextures(IMEPackage mePackage, VTestOptions vTestOptions)
        {
            foreach (var exp in mePackage.Exports.Where(x => x.IsTexture()))
            {
                var texinfo = ObjectBinary.From<UTexture2D>(exp);
                if (texinfo.Mips.Any(x => x.StorageType == StorageTypes.empty))
                    Debug.WriteLine($@"FOUND EMPTY MIP: {exp.InstancedFullPath} IN {Path.GetFileNameWithoutExtension(mePackage.FilePath)}");
            }
        }

        private static void VTestCheckImports(IMEPackage p, VTestOptions vTestOptions)
        {
            foreach (var import in p.Imports)
            {
                if (import.IsAKnownNativeClass())
                    continue; //skip
                var resolvedExp = EntryImporter.ResolveImport(import, null, vTestOptions.cache, clipRootLevelPackage: false);
                if (resolvedExp == null)
                {
                    // Look in DB for objects that have same suffix
                    // This is going to be VERY slow

                    var instancedNameSuffix = "." + import.ObjectName.Instanced;
                    string similar = "";
                    foreach (var name in vTestOptions.objectDB.NameTable)
                    {
                        if (name.EndsWith(instancedNameSuffix, StringComparison.InvariantCultureIgnoreCase))
                        {
                            similar += ", " + name;
                        }
                    }

                    Debug.WriteLine($"Import not resolved in {Path.GetFileName(p.FilePath)}: {import.InstancedFullPath}, may be these ones instead: {similar}");
                }
            }
        }

        /// <summary>
        /// Ports a level file for VTest. Saves package at the end.
        /// </summary>
        /// <param name="mapName">Overarching map name</param>
        /// <param name="sourceName">Full map file name</param>
        /// <param name="finalDestDir"></param>
        /// <param name="sourceDir"></param>
        /// <param name="db"></param>
        /// <param name="pe"></param>
        /// <param name="syncBioWorldInfo"></param>
        /// <param name="portMainSequence"></param>
        private static void PortVTestLevel(string mapName, string sourceName, VTestOptions vTestOptions, bool syncBioWorldInfo = false, bool portMainSequence = false)
        {
            vTestOptions.cache.ReleasePackages(x => Path.GetFileNameWithoutExtension(x) != "SFXGame" && Path.GetFileNameWithoutExtension(x) != "Engine"); //Reduce memory overhead
            var outputFile = $@"{PAEMPaths.VTest_FinalDestDir}\{sourceName.ToUpper()}.pcc";
            CreateEmptyLevel(outputFile, MEGame.LE1);

            using var le1File = MEPackageHandler.OpenMEPackage(outputFile);
            using var me1File = MEPackageHandler.OpenMEPackage($@"{PAEMPaths.VTest_SourceDir}\{mapName}\{sourceName}.SFM");

            // BIOC_BASE -> SFXGame
            var bcBaseIdx = me1File.findName("BIOC_Base");
            me1File.replaceName(bcBaseIdx, "SFXGame");

            // BIOG_StrategicAI -> SFXStrategicAI
            var bgsaiBaseIdx = me1File.findName("BIOG_StrategicAI");
            if (bgsaiBaseIdx >= 0)
                me1File.replaceName(bgsaiBaseIdx, "SFXStrategicAI");

            // Once we are confident in porting we will just take the actor list from PersistentLevel
            // For now just port these
            var itemsToPort = new List<ExportEntry>();

            var me1PersistentLevel = ObjectBinary.From<Level>(me1File.FindExport(@"TheWorld.PersistentLevel"));
            itemsToPort.AddRange(me1PersistentLevel.Actors.Where(x => x.value != 0) // Skip blanks
                .Select(x => me1File.GetUExport(x.value))
                .Where(x => ClassesToVTestPort.Contains(x.ClassName) || (syncBioWorldInfo && ClassesToVTestPortMasterOnly.Contains(x.ClassName))));

            // WIP: Find which classes we have yet to port
            // BioWorldInfo is not ported except on the level master. Might need to see if there's things
            // like scene desaturation in it worth porting.
            foreach (var v in me1PersistentLevel.Actors)
            {
                var entry = v.value != 0 ? v.GetEntry(me1File) : null;
                if (entry != null && !actorTypesNotPorted.Contains(entry.ClassName) && !ClassesToVTestPort.Contains(entry.ClassName) && !ClassesToVTestPortMasterOnly.Contains(entry.ClassName) && entry.ClassName != "BioWorldInfo")
                {
                    actorTypesNotPorted.Add(entry.ClassName);
                }
            }

            // End WIP


            VTestFilePorting(me1File, le1File, itemsToPort, vTestOptions);

            RelinkerOptionsPackage rop = new RelinkerOptionsPackage()
            {
                Cache = vTestOptions.cache,
                IsCrossGame = true,
                ImportExportDependencies = true,
                TargetGameDonorDB = vTestOptions.objectDB
            };

            // Replace BioWorldInfo if requested
            if (syncBioWorldInfo)
            {
                var me1BWI = me1File.Exports.FirstOrDefault(x => x.ClassName == "BioWorldInfo");
                if (me1BWI != null)
                {
                    me1BWI.indexValue = 1;
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular, me1BWI, le1File, le1File.FindExport(@"TheWorld.PersistentLevel.BioWorldInfo_0"), true, rop, out _);
                }
            }

            // Replace Main_Sequence if requested

            if (portMainSequence)
            {
                vTestOptions.packageEditorWindow.BusyText = "Porting sequencing...";
                var dest = le1File.FindExport(@"TheWorld.PersistentLevel.Main_Sequence");
                var source = me1File.FindExport(@"TheWorld.PersistentLevel.Main_Sequence");
                if (source != null && dest != null)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular, source, le1File, dest, true, rop, out _);
                }
                else
                {
                    Debug.WriteLine($"No sequence to port in {sourceName}");
                }

            }

            PostPortingCorrections(me1File, le1File, vTestOptions);

            if (vTestOptions.useDynamicLighting)
            {
                PackageEditorExperimentsS.CreateDynamicLighting(le1File, true);
            }

            if (vTestOptions.debugBuild)
            {
                VTest_EnableDebugOptionsOnPackage(le1File, vTestOptions);
            }

            //if (le1File.Exports.Any(x => x.IsA("PathNode")))
            //{
            //    Debugger.Break();
            //}

            le1File.Save();

            Debug.WriteLine($"RCP CHECK FOR {Path.GetFileNameWithoutExtension(le1File.FilePath)} -------------------------");
            ReferenceCheckPackage rcp = new ReferenceCheckPackage();
            EntryChecker.CheckReferences(rcp, le1File, EntryChecker.NonLocalizedStringConverter);

            foreach (var err in rcp.GetBlockingErrors())
            {
                Debug.WriteLine($"RCP: [ERROR] {err.Entry.InstancedFullPath} {err.Message}");
            }

            foreach (var err in rcp.GetSignificantIssues())
            {
                Debug.WriteLine($"RCP: [WARN] {err.Entry.InstancedFullPath} {err.Message}");
            }
        }

        private static StructProperty ConvertCoverSlot(StructProperty me1CS, VTestOptions vTestOptions)
        {
            // How to convert a coverslot

            // 1. Draw some circles
            var csProps = GlobalUnrealObjectInfo.getDefaultStructValue(MEGame.LE1, "CoverSlot", false, vTestOptions.cache);

            // 2. Draw the rest of the fucking owl
            foreach (var me1Prop in me1CS.Properties.ToList())
            {
                switch (me1Prop)
                {
                    case IntProperty:
                    case FloatProperty:
                    case BoolProperty:
                    case EnumProperty:
                        if (TryUpdateProp(me1Prop, csProps))
                        {
                            me1CS.Properties.Remove(me1Prop);
                        }
                        break;
                    case ObjectProperty op:
                        if (op.Value == 0)
                            me1CS.Properties.Remove(me1Prop); // This doesn't have a value
                        break;
                    case StructProperty sp:
                        {
                            if (sp.Name == "LocationOffset" || sp.Name == "RotationOffset")
                            {
                                if (!sp.IsImmutable)
                                    Debugger.Break();
                                TryUpdateProp(me1Prop, csProps);
                                me1CS.Properties.Remove(sp);
                            }
                            break;
                        }
                }
            }

            if (me1CS.Properties.Count > 0)
            {
                // uncomment to debug these
                //Debug.WriteLine("The following properties were not translated:");
                foreach (var mp in me1CS.Properties)
                {
                    //Debug.WriteLine(mp.Name );
                }
            }


            return new StructProperty("CoverSlot", csProps, isImmutable: true);

            bool TryUpdateProp(Property p, PropertyCollection destCollection)
            {
                if (destCollection.ContainsNamedProp(p.Name))
                {
                    //destCollection.AddOrReplaceProp(p);
                    return true;
                }
                Debug.WriteLine($"Target doesn't have property named {p.Name}");
                return false;
            }
        }

        private static void VTest_EnableDebugOptionsOnPackage(IMEPackage le1File, VTestOptions vTestOptions)
        {
            // This is no longer necessary with m_aObjLog Enabler mod
            //SequenceEditorExperimentsM.ConvertSeqAct_Log_objComments(le1File, vTestOptions.cache);
        }

        /// <summary>
        /// Ports a list of actors between levels with VTest
        /// </summary>
        /// <param name="sourcePackage"></param>
        /// <param name="destPackage"></param>
        /// <param name="itemsToPort"></param>
        /// <param name="db"></param>
        /// <param name="pe"></param>
        private static void VTestFilePorting(IMEPackage sourcePackage, IMEPackage destPackage, IEnumerable<ExportEntry> itemsToPort, VTestOptions vTestOptions)
        {
            // PRECORRECTION - CORRECTIONS TO THE SOURCE FILE BEFORE PORTING
            PrePortingCorrections(sourcePackage);

            // PORTING ACTORS
            var le1PL = destPackage.FindExport("TheWorld.PersistentLevel");
            foreach (var e in itemsToPort)
            {
                vTestOptions.packageEditorWindow.BusyText = $"Porting {e.ObjectName}";
                RelinkerOptionsPackage rop = new RelinkerOptionsPackage()
                {
                    Cache = vTestOptions.cache,
                    ImportExportDependencies = true,
                    IsCrossGame = true,
                    TargetGameDonorDB = vTestOptions.objectDB
                };
                var report = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, e, destPackage,
                    le1PL, true, rop, out _);
            }
        }

        /// <summary>
        /// Ports a LOC file by porting the ObjectReferencer within it
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="db"></param>
        /// <param name="pc"></param>
        /// <param name="pe"></param>
        private static void PortLOCFile(string sourceFile, VTestOptions vTestOptions)
        {
            var packName = Path.GetFileNameWithoutExtension(sourceFile);
            vTestOptions.packageEditorWindow.BusyText = $"Porting {packName}";

            var destPackagePath = Path.Combine(PAEMPaths.VTest_FinalDestDir, $"{packName.ToUpper()}.pcc");
            MEPackageHandler.CreateAndSavePackage(destPackagePath, MEGame.LE1);
            using var package = MEPackageHandler.OpenMEPackage(destPackagePath);
            using var sourcePackage = MEPackageHandler.OpenMEPackage(sourceFile);

            PrePortingCorrections(sourcePackage);

            var bcBaseIdx = sourcePackage.findName("BIOC_Base");
            sourcePackage.replaceName(bcBaseIdx, "SFXGame");

            foreach (var e in sourcePackage.Exports.Where(x => x.ClassName == "ObjectReferencer"))
            {
                // Correct the object referencer to remove objects that shouldn't be be ported.
                var ro = e.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedObjects");
                foreach (var refObj in ro.ToList())
                {
                    if (refObj.ResolveToEntry(sourcePackage) is ExportEntry resolved)
                    {
                        bool removed = false; // for if we check more than just archetypes

                        // Prune items with bad archetypes
                        var archetypeObjectName = resolved.Archetype?.ObjectName.Name ?? null;
                        switch (archetypeObjectName) // Not instanced as these are typically sub items and have instance numbers
                        {
                            case "DistributionLPFMaxRadius":
                            case "DistributionLPFMinRadius":
                                ro.Remove(refObj);
                                removed = true;
                                break;
                        }

                        if (removed)
                            continue;

                    }
                }
                e.WriteProperty(ro);
                RelinkerOptionsPackage rop = new RelinkerOptionsPackage()
                {
                    IsCrossGame = true,
                    ImportExportDependencies = true,
                    Cache = vTestOptions.cache,
                    TargetGameDonorDB = vTestOptions.objectDB
                };

                var report = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, e, package, null, true, rop, out _);
                if (report.Any())
                {
                    //Debugger.Break();
                }
            }

            CorrectSequences(package, vTestOptions);
            vTestOptions.packageEditorWindow.BusyText = $"Saving {packName}";
            package.Save();
        }
        #endregion

        #region Utility methods
        public static void CreateEmptyLevel(string outpath, MEGame game)
        {
            var emptyLevelName = $"{game}EmptyLevel";
            File.Copy(Path.Combine(AppDirectories.ExecFolder, $"{emptyLevelName}.pcc"), outpath, true);
            using var Pcc = MEPackageHandler.OpenMEPackage(outpath);
            for (int i = 0; i < Pcc.Names.Count; i++)
            {
                string name = Pcc.Names[i];
                if (name.Equals(emptyLevelName))
                {
                    var newName = name.Replace(emptyLevelName, Path.GetFileNameWithoutExtension(outpath));
                    Pcc.replaceName(i, newName);
                }
            }

            var packguid = Guid.NewGuid();
            var package = Pcc.GetUExport(game switch
            {
                MEGame.LE1 => 4,
                MEGame.LE3 => 6,
                MEGame.ME2 => 7,
                _ => 1
            });
            package.PackageGUID = packguid;
            Pcc.PackageGuid = packguid;
            Pcc.Save();
        }

        private static StructProperty MakeLinearColorStruct(string propertyName, float r, float g, float b, float a)
        {
            PropertyCollection p = new PropertyCollection();
            p.AddOrReplaceProp(new FloatProperty(r, "R"));
            p.AddOrReplaceProp(new FloatProperty(g, "G"));
            p.AddOrReplaceProp(new FloatProperty(b, "B"));
            p.AddOrReplaceProp(new FloatProperty(a, "A"));
            return new StructProperty("LinearColor", p, propertyName, true);
        }
        #endregion

        #region Correction methods
        public static void PrePortingCorrections(IMEPackage sourcePackage)
        {
            // Strip static mesh light maps since they don't work crossgen. Strip them from
            // the source so they don't port
            foreach (var exp in sourcePackage.Exports)
            {

                PruneUnusedProperties(exp);
                #region Remove Light and Shadow Maps
                if (exp.ClassName == "StaticMeshComponent")
                {
                    var b = ObjectBinary.From<StaticMeshComponent>(exp);
                    foreach (var lod in b.LODData)
                    {
                        // Clear light and shadowmaps
                        lod.ShadowMaps = new UIndex[0];
                        lod.LightMap = new LightMap() { LightMapType = ELightMapType.LMT_None };
                    }

                    exp.WriteBinary(b);
                }
                #endregion
                // These are precomputed and stored in VTestHelper.pcc 
                else if (exp.ClassName == "Terrain")
                {
                    exp.RemoveProperty("TerrainComponents"); // Don't port the components; we will port them ourselves in post
                }
                else if (exp.ClassName == "BioTriggerStream")
                {
                    PreCorrectBioTriggerStream(exp);
                }
                else if (exp.ClassName == "BioWorldInfo")
                {
                    // Remove streaminglevels that don't do anything
                    //PreCorrectBioWorldInfoStreamingLevels(exp);
                }
                else if (exp.ClassName == "MaterialInstanceConstant")
                {
                    PreCorrectMaterialInstanceConstant(exp);
                }

                if (exp.IsA("Actor"))
                {
                    //exp.RemoveProperty("m_oAreaMap"); // Remove this when stuff is NOT borked up
                    //exp.RemoveProperty("Base"); // No bases
                    //exp.RemoveProperty("nextNavigationPoint"); // No bases
                }
            }
        }

        // Terrible performance, but i don't care
        private static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }

        private static Dictionary<Guid, Guid> expressionGuidMap = new()
        {
            { new Guid(StringToByteArray("AD2F8F9FB837D8499EF1FC9799289A3E")), new Guid(StringToByteArray("896318E56A762B4FAEA9AA29B4B968CD")) }, // Alpha_Map -> Texture (Scoreboard)
            { new Guid(StringToByteArray("32144A9CDE189141BC421589B7EF3C0A")), new Guid(StringToByteArray("A1A3A72858C9DC45A10D3E9967BE4EE8")) }, // Character_Color -> ColorSelected (Scoreboard)
            { new Guid(StringToByteArray("E1EC0FC0E38D07439505E7C1EBB17F6D")), new Guid(StringToByteArray("896318E56A762B4FAEA9AA29B4B968CD")) }, // Alpha_Map -> Texture (Scoreboard Pulse)

        };

        private static Dictionary<string, string> parameterNameMap = new()
        {
            { "Alpha_Map", "Texture" }, // PRC2 Scoreboard Materials
            { "Character_Color", "ColorSelected" } // PRC2 Scoreboard Materials
        };

        private static void PreCorrectMaterialInstanceConstant(ExportEntry exp)
        {
            // Some parameters need updated to match new materials

            // VECTORS
            var vectorParams = exp.GetProperty<ArrayProperty<StructProperty>>("VectorParameterValues");
            if (vectorParams != null)
            {
                foreach (var vp in vectorParams)
                {
                    var parameterName = vp.GetProp<NameProperty>("ParameterName").Value.Name;
                    var expressionGuid = CommonStructs.GetGuid(vp.GetProp<StructProperty>("ExpressionGUID"));
                    if (expressionGuidMap.TryGetValue(expressionGuid, out var newGuid) && parameterNameMap.TryGetValue(parameterName, out var newParameterName))
                    {
                        Debug.WriteLine($"Updating VP MIC {exp.InstancedFullPath}");
                        vp.GetProp<NameProperty>("ParameterName").Value = newParameterName;
                        vp.Properties.AddOrReplaceProp(CommonStructs.GuidProp(newGuid, "ExpressionGUID"));
                    }
                }
                exp.WriteProperty(vectorParams);
            }

            // TEXTURES
            var textureParams = exp.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues");
            if (textureParams != null)
            {
                foreach (var tp in textureParams)
                {
                    var parameterName = tp.GetProp<NameProperty>("ParameterName").Value.Name;
                    var expressionGuid = CommonStructs.GetGuid(tp.GetProp<StructProperty>("ExpressionGUID"));
                    if (expressionGuidMap.TryGetValue(expressionGuid, out var newGuid) && parameterNameMap.TryGetValue(parameterName, out var newParameterName))
                    {
                        Debug.WriteLine($"Updating TP MIC {exp.InstancedFullPath}");
                        tp.GetProp<NameProperty>("ParameterName").Value = newParameterName;
                        tp.Properties.AddOrReplaceProp(CommonStructs.GuidProp(newGuid, "ExpressionGUID"));
                    }
                }
                exp.WriteProperty(textureParams);
            }


        }

        public static void ConvertME1TerrainComponent(ExportEntry exp)
        {
            // Strip Lightmap
            var b = ObjectBinary.From<TerrainComponent>(exp);
            b.LightMap = new LightMap() { LightMapType = ELightMapType.LMT_None };
            // Convert the tesselation... something... idk something that makes it multiply
            // by what appears to be 16x16 (256)
            var props = exp.GetProperties();
            //var sizeX = props.GetProp<IntProperty>("SectionSizeX");
            //var sizeY = props.GetProp<IntProperty>("SectionSizeY");
            //var trueSizeX = props.GetProp<IntProperty>("TrueSectionSizeX");
            //var trueSizeY = props.GetProp<IntProperty>("TrueSectionSizeY");

            //var factorSize = sizeX * sizeY; // idk
            //for (int i = 0; i < trueSizeY; i++)
            //{
            //    for (int j = 0; j < trueSizeX; j++)
            //    {
            //        // uh... idk?
            //        var collisionIdx = (i * trueSizeY) + j;
            //        var vtx = b.CollisionVertices[collisionIdx];
            //        b.CollisionVertices[collisionIdx] = new Vector3(vtx.X * factorSize, vtx.Y * factorSize, vtx.Z);
            //        Debug.WriteLine(collisionIdx + " " + b.CollisionVertices[collisionIdx].ToString());
            //    }
            //}

            // Correct collision vertices as they've changed from local to world in LE
            float scaleX = 256; // Default DrawScale3D for terrain is 256
            float scaleY = 256;
            float scaleZ = 256;

            float basex = 0;
            float basey = 0;
            float basez = 0;

            var ds3d = (exp.Parent as ExportEntry).GetProperty<StructProperty>("DrawScale3D");
            if (ds3d != null)
            {
                scaleX = ds3d.GetProp<FloatProperty>("X").Value;
                scaleY = ds3d.GetProp<FloatProperty>("Y").Value;
                scaleZ = ds3d.GetProp<FloatProperty>("Z").Value;
            }

            var ds = (exp.Parent as ExportEntry).GetProperty<FloatProperty>("DrawScale");
            if (ds != null)
            {
                scaleX *= ds.Value;
                scaleY *= ds.Value;
                scaleZ *= ds.Value;
            }

            var loc = (exp.Parent as ExportEntry).GetProperty<StructProperty>("Location");
            if (loc != null)
            {
                basex = loc.GetProp<FloatProperty>("X").Value;
                basey = loc.GetProp<FloatProperty>("Y").Value;
                basez = loc.GetProp<FloatProperty>("Z").Value;
            }

            // COLLISION VERTICES
            for (int i = 0; i < b.CollisionVertices.Length; i++)
            {
                var cv = b.CollisionVertices[i];
                Vector3 newV = new Vector3();

                newV.X = basex - (cv.X * scaleX);
                newV.Y = basey - (cv.Y * scaleY);
                newV.Z = basez + (cv.Z * scaleZ); // Is this right?
                b.CollisionVertices[i] = newV;
            }

            // Bounding Volume Tree
            Vector3 dif = new Vector3(86806.1f, -70072.58f, -6896.561f);
            for (int i = 0; i < b.BVTree.Length; i++)
            {
                var box = b.BVTree[i].BoundingVolume;
                box.Min = new Vector3 { X = basex - (box.Min.X * scaleX), Y = basey /*- (box.Min.Y * scaleY)*/, Z = basez + (box.Min.Z * scaleZ) };
                box.Max = new Vector3 { X = basex /*+ (box.Max.X * scaleX)*/, Y = basey + (box.Max.Y * scaleY), Z = basez + (box.Max.Z * scaleZ) };
            }

            exp.WriteBinary(b);

            // Make dynamic lighting
            //var props = exp.GetProperties();
            //props.RemoveNamedProperty("BlockRigidBody"); // make collidable?
            props.RemoveNamedProperty("ShadowMaps");
            props.AddOrReplaceProp(new BoolProperty(false, "bForceDirectLightMap"));
            props.AddOrReplaceProp(new BoolProperty(true, "bCastDynamicShadow"));
            props.AddOrReplaceProp(new BoolProperty(true, "bAcceptDynamicLights"));

            var lightingChannels = props.GetProp<StructProperty>("LightingChannels") ??
                                   new StructProperty("LightingChannelContainer", false,
                                       new BoolProperty(true, "bIsInitialized"))
                                   {
                                       Name = "LightingChannels"
                                   };
            lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Static"));
            lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Dynamic"));
            lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "CompositeDynamic"));
            props.AddOrReplaceProp(lightingChannels);

            exp.WriteProperties(props);
        }

        private static void PruneUnusedProperties(ExportEntry exp)
        {
            // Lots of components are not used or don't exist and can't be imported in LE1
            // Get rid of them here
            PropertyCollection props = exp.GetProperties();

            // Might be better to enumerate all object properties and trim out ones that reference
            // known non-existent things
            if (exp.IsA("LightComponent"))
            {
                props.RemoveNamedProperty("PreviewInnerCone");
                props.RemoveNamedProperty("PreviewOuterCone");
                props.RemoveNamedProperty("PreviewLightRadius");
            }

            if (exp.IsA("NavigationPoint"))
            {
                props.RemoveNamedProperty("GoodSprite");
                props.RemoveNamedProperty("BadSprite");
            }

            if (exp.IsA("BioArtPlaceable"))
            {
                props.RemoveNamedProperty("CoverMesh"); // Property exists but is never set
            }

            if (exp.IsA("SoundNodeAttenuation"))
            {
                props.RemoveNamedProperty("LPFMinRadius");
                props.RemoveNamedProperty("LPFMaxRadius");
            }

            if (exp.IsA("BioAPCoverMeshComponent"))
            {
                exp.Archetype = null; // Remove the archetype. This is on BioDoor's and does nothing in practice, in ME1 there is nothing to copy from the archetype
            }

            if (exp.IsA("BioSquadCombat"))
            {
                props.RemoveNamedProperty("m_oSprite");
            }

            if (exp.IsA("CameraActor"))
            {
                props.RemoveNamedProperty("MeshComp"); // some actors have a camera mesh that was probably used to better visualize in-editor
            }

            exp.WriteProperties(props);
        }

        private static void PreCorrectBioWorldInfoStreamingLevels(ExportEntry exp)
        {
            // Older games (ME1 at least) can reference levels that don't exist. This didn't breka game. Later games this does break
            // has a bunch of level references that don't exist

            //if (triggerStream.ObjectName.Instanced == "BioTriggerStream_0")
            //    Debugger.Break();
            var streamingLevels = exp.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels");
            if (streamingLevels != null)
            {
                for (int i = streamingLevels.Count - 1; i >= 0; i--)
                {
                    var lsk = streamingLevels[i].ResolveToEntry(exp.FileRef) as ExportEntry;
                    var packageName = lsk.GetProperty<NameProperty>("PackageName");
                    if (VTest_NonExistentBTSFiles.Contains(packageName.Value.Instanced.ToLower()))
                    {
                        // Do not port this
                        Debug.WriteLine($@"Removed non-existent LSK package: {packageName.Value.Instanced} in {Path.GetFileNameWithoutExtension(exp.FileRef.FilePath)}");
                        streamingLevels.RemoveAt(i);
                    }
                    else
                    {
                        Debug.WriteLine($@"LSK package exists: {packageName.Value.Instanced} in {Path.GetFileNameWithoutExtension(exp.FileRef.FilePath)}");
                    }
                }

                exp.WriteProperty(streamingLevels);
            }
        }

        private static void PostCorrectMaterialsToInstanceConstants(IMEPackage me1Package, IMEPackage le1Package, VTestOptions vTestOptions)
        {
            // Oh lordy this is gonna suck

            // Donor materials need tweaks to behave like the originals
            // So we make a new MaterialInstanceConstant, copy in the relevant(?) values,
            // and then repoint all incoming references to the Material to use this MaterialInstanceConstant instead.
            // This is going to be slow and ugly code
            // Technically this could be done in the relinker but I don't want to stuff
            // something this ugly in there
            foreach (var le1Material in le1Package.Exports.Where(x => vtest_DonorMaterials.Contains(x.InstancedFullPath)).ToList())
            {
                Debug.WriteLine($"Correcting material inputs for donor material: {le1Material.InstancedFullPath}");
                var donorinputs = new List<string>();
                var expressions = le1Material.GetProperty<ArrayProperty<ObjectProperty>>("Expressions");
                foreach (var express in expressions.Select(x => x.ResolveToEntry(le1Package) as ExportEntry))
                {
                    if (express.ClassName == "MaterialExpressionVectorParameter")
                    {
                        donorinputs.Add(express.GetProperty<NameProperty>("ParameterName").Value.Name);
                    }
                }

                Debug.WriteLine(@"Donor has the following inputs:");
                foreach (var di in donorinputs)
                {
                    Debug.WriteLine(di);
                }

                var me1Material = me1Package.FindExport(le1Material.InstancedFullPath);

                var sourceMatInst = vTestOptions.vTestHelperPackage.Exports.First(x => x.ClassName == "MaterialInstanceConstant"); // cause it can change names here
                sourceMatInst.ObjectName = $"{le1Material.ObjectName}_MatInst";
                RelinkerOptionsPackage rop = new RelinkerOptionsPackage()
                {
                    Cache = vTestOptions.cache,
                    ImportExportDependencies = true,
                    IsCrossGame = true,
                    TargetGameDonorDB = vTestOptions.objectDB
                };
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceMatInst, le1Package, le1Material.Parent, true, rop, out var le1MatInstEntry);

                var le1MatInst = le1MatInstEntry as ExportEntry;
                var le1MatInstProps = le1MatInst.GetProperties();

                le1MatInstProps.AddOrReplaceProp(new ObjectProperty(le1Material, "Parent")); // Update the parent

                // VECTOR EXPRESSIONS
                var vectorExpressions = new ArrayProperty<StructProperty>("VectorParameterValues");
                foreach (var v in me1Material.GetProperty<ArrayProperty<ObjectProperty>>("Expressions").Select(x => x.ResolveToEntry(me1Package) as ExportEntry))
                {
                    if (v.ClassName == "MaterialExpressionVectorParameter")
                    {
                        var exprInput = v.GetProperty<NameProperty>("ParameterName").Value.Name;
                        if (donorinputs.Contains(exprInput))
                        {
                            var vpv = v.GetProperty<StructProperty>("DefaultValue");
                            PropertyCollection pc = new PropertyCollection();
                            pc.AddOrReplaceProp(MakeLinearColorStruct("ParameterValue", vpv.GetProp<FloatProperty>("R"), vpv.GetProp<FloatProperty>("G"), vpv.GetProp<FloatProperty>("B"), vpv.GetProp<FloatProperty>("A")));
                            pc.AddOrReplaceProp(new FGuid(Guid.Empty).ToStructProperty("ExpressionGUID"));
                            pc.AddOrReplaceProp(new NameProperty(exprInput, "ParameterName"));
                            vectorExpressions.Add(new StructProperty("VectorParameterValue", pc));
                            donorinputs.Remove(exprInput);
                        }
                    }
                    else
                    {
                        //Debugger.Break();
                    }
                }

                if (vectorExpressions.Any())
                {
                    le1MatInstProps.AddOrReplaceProp(vectorExpressions);
                }

                // SCALAR EXPRESSIONS
                var me1MatInfo = ObjectBinary.From<Material>(me1Material);
                var scalarExpressions = new ArrayProperty<StructProperty>("ScalarParameterValues");
                foreach (var v in me1MatInfo.SM3MaterialResource.UniformPixelScalarExpressions)
                {
                    if (v is MaterialUniformExpressionScalarParameter spv)
                    {
                        PropertyCollection pc = new PropertyCollection();
                        pc.AddOrReplaceProp(new FGuid(Guid.Empty).ToStructProperty("ExpressionGUID"));
                        pc.AddOrReplaceProp(new NameProperty(spv.ParameterName, "ParameterName"));
                        pc.AddOrReplaceProp(new FloatProperty(spv.DefaultValue, "ParameterValue"));
                        scalarExpressions.Add(new StructProperty("ScalarParameterValue", pc));
                    }
                }

                if (scalarExpressions.Any())
                {
                    le1MatInstProps.AddOrReplaceProp(scalarExpressions);
                }

                le1MatInst.WriteProperties(le1MatInstProps);

                // Find things that reference this material and repoint them
                var entriesToUpdate = le1Material.GetEntriesThatReferenceThisOne();
                foreach (var entry in entriesToUpdate.Keys)
                {
                    if (entry == le1MatInst)
                        continue;
                    le1MatInst.GetProperties();
                    var relinkDict = new ListenableDictionary<IEntry, IEntry>();
                    relinkDict[le1Material] = le1MatInst; // This is a ridiculous hack

                    rop = new RelinkerOptionsPackage()
                    {
                        CrossPackageMap = relinkDict,
                        Cache = vTestOptions.cache,
                        ImportExportDependencies = false // This is same-package so there's nothing to import.
                    };

                    Relinker.Relink(entry as ExportEntry, entry as ExportEntry, rop);
                    le1MatInst.GetProperties();
                }
            }
        }

        private static void PreCorrectBioTriggerStream(ExportEntry triggerStream)
        {
            // Older games (ME1 at least) can reference levels that don't exist. This didn't break game. Later games this does break. Maybe. IDK.

            //if (triggerStream.ObjectName.Instanced == "BioTriggerStream_0")
            //    Debugger.Break();
            // triggerStream.RemoveProperty("m_oAreaMapOverride"); // Remove this when stuff is NOT borked up
            //
            // return;
            var streamingStates = triggerStream.GetProperty<ArrayProperty<StructProperty>>("StreamingStates");
            if (streamingStates != null)
            {
                foreach (var ss in streamingStates)
                {
                    var inChunkName = ss.GetProp<NameProperty>("InChunkName").Value.Name.ToLower();

                    if (inChunkName != "none" && VTest_NonExistentBTSFiles.Contains(inChunkName))
                        Debugger.Break(); // Hmm....

                    var visibleChunks = ss.GetProp<ArrayProperty<NameProperty>>("VisibleChunkNames");
                    for (int i = visibleChunks.Count - 1; i >= 0; i--)
                    {
                        if (VTest_NonExistentBTSFiles.Contains(visibleChunks[i].Value.Name.ToLower()))
                        {
                            Debug.WriteLine($"PreCorrect: VS Remove BTS level {visibleChunks[i].Value}");
                            //visibleChunks.RemoveAt(i);
                        }
                    }

                    var loadChunks = ss.GetProp<ArrayProperty<NameProperty>>("LoadChunkNames");
                    for (int i = loadChunks.Count - 1; i >= 0; i--)
                    {
                        if (VTest_NonExistentBTSFiles.Contains(loadChunks[i].Value.Name.ToLower()))
                        {
                            Debug.WriteLine($"PreCorrect: LC Remove BTS level {loadChunks[i].Value}");
                            //loadChunks.RemoveAt(i);
                        }
                    }
                }

                triggerStream.WriteProperty(streamingStates);
            }
            else
            {
                //yDebug.WriteLine($"{triggerStream.InstancedFullPath} in {triggerStream} has NO StreamingStates!!");
            }
        }

        private static void PostPortingCorrections(IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            // Corrections to run AFTER porting is done
            //ReinstateCoverSlots(me1File, le1File, vTestOptions);
            CorrectTextures(le1File);
            CorrectPrefabSequenceClass(le1File);
            CorrectSequences(le1File, vTestOptions);
            CorrectPathfindingNetwork(me1File, le1File);
            PostCorrectMaterialsToInstanceConstants(me1File, le1File, vTestOptions);
            CorrectVFX(me1File, le1File, vTestOptions);
            //CorrectTerrainMaterials(le1File);


            var fName = Path.GetFileNameWithoutExtension(le1File.FilePath);
            // Port in the collision-corrected terrain
            if (fName.CaseInsensitiveEquals("BIOA_PRC2_CCLava"))
            {
                PortInCorrectedTerrain(le1File, "CCLava.Terrain_1", "BIOA_LAV60_00_LAY.pcc", vTestOptions);
            }
            else if (fName.CaseInsensitiveEquals("BIOA_PRC2_CCSIM05_DSG"))
            {
                // Port in the custom sequence used for switching UIs
                InstallVTestHelperSequence(le1File, "TheWorld.PersistentLevel.Main_Sequence.Play_Central_Scoreboard_Matinee.SeqEvent_RemoteEvent_0", "ScoreboardSequence.UISwitcherLogic", vTestOptions);

                // Port in the keybinding sequences
                InstallVTestHelperSequence(le1File, "TheWorld.PersistentLevel.Main_Sequence.Play_Central_Scoreboard_Matinee.SeqEvent_RemoteEvent_0", "ScoreboardSequence.KeybindsInstaller", vTestOptions);
                InstallVTestHelperSequence(le1File, "TheWorld.PersistentLevel.Main_Sequence.Play_Central_Scoreboard_Matinee.SeqAct_Gate_3", "ScoreboardSequence.KeybindsUninstaller", vTestOptions);
            }
            else if (fName.CaseInsensitiveEquals("BIOA_PRC2_CCSCOREBOARD_DSG"))
            {
                // Porting in ANY of these crashes the game. Why??

                // Port in the UI switching and keybinding for PC
                // Port in the custom sequence used for switching UIs. Should only run if not skipping the scoreboard
                //InstallVTestHelperSequence(le1File, "TheWorld.PersistentLevel.Main_Sequence.Play_Post_Scenario_Scoreboard_Matinee.UIAction_PlaySound_0", "ScoreboardSequence.UISwitcherLogic", vTestOptions);

                // Port in the keybinding sequences
                // Both of the following crashes the game currently, not sure why.
                //InstallVTestHelperSequence(le1File, "TheWorld.PersistentLevel.Main_Sequence.Play_Post_Scenario_Scoreboard_Matinee.BioSeqAct_MiniGame_1", "ScoreboardSequence.KeybindsInstaller", vTestOptions);
                //InstallVTestHelperSequence(le1File, "TheWorld.PersistentLevel.Main_Sequence.Play_Post_Scenario_Scoreboard_Matinee.UIAction_PlaySound_1", "ScoreboardSequence.KeybindsUninstaller", vTestOptions);

            }

            LevelSpecificPostCorrections(fName, me1File, le1File, vTestOptions);



            RebuildPersistentLevelChildren(le1File.FindExport("TheWorld.PersistentLevel"));

            //CorrectTriggerStreamsMaybe(me1File, le1File);
        }

        private static void CorrectVFX(IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            // These could probably use a bit of cleaning up to look better like fade in/out
            var glitchRandom = le1File.FindExport("BIOA_PRC2_MatFX.VFX.Glitch_Random");
            if (glitchRandom != null)
            {
                var props = glitchRandom.GetProperties();
                props.AddOrReplaceProp(new NameProperty("EMT_HoloWipe", "m_nmEffectsMaterial"));
                props.AddOrReplaceProp(new BoolProperty(true, "m_bIgnorePooling"));
                props.AddOrReplaceProp(new EnumProperty("BIO_VFX_PRIORITY_ALWAYS", "EBioVFXPriority", MEGame.LE1, "ePriority"));
                glitchRandom.WriteProperties(props);
            }

            var glitchedToDeath = le1File.FindExport("BIOA_PRC2_MatFX.DeathEffects.GlitchedToDeath");
            if (glitchedToDeath != null)
            {
                var props = glitchedToDeath.GetProperties();
                props.AddOrReplaceProp(new NameProperty("EMT_HoloWipe", "m_nmEffectsMaterial"));
                props.AddOrReplaceProp(new BoolProperty(true, "m_bIgnorePooling"));
                props.AddOrReplaceProp(new EnumProperty("BIO_VFX_PRIORITY_ALWAYS", "EBioVFXPriority", MEGame.LE1, "ePriority"));
                glitchedToDeath.WriteProperties(props);
            }
        }

        private static void InstallVTestHelperSequence(IMEPackage le1File, string sourceSequenceOpIFP, string vTestSequenceIFP, VTestOptions vTestOptions)
        {
            var sourceItemToOutFrom = le1File.FindExport(sourceSequenceOpIFP);
            var parentSequence = SeqTools.GetParentSequence(sourceItemToOutFrom, true);
            var donorSequence = vTestOptions.vTestHelperPackage.FindExport(vTestSequenceIFP);
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, donorSequence, le1File, parentSequence, true, new RelinkerOptionsPackage() { Cache = vTestOptions.cache }, out var newUiSeq);
            KismetHelper.AddObjectToSequence(newUiSeq as ExportEntry, parentSequence);

            // link it up
            KismetHelper.CreateOutputLink(sourceItemToOutFrom, "Out", newUiSeq as ExportEntry);
        }

        private static void AddWorldReferencedObjects(IMEPackage le1File, IEnumerable<ExportEntry> entriesToReference)
        {
            var world = le1File.FindExport("TheWorld");
            var worldBin = ObjectBinary.From<World>(world);
            var newItems = worldBin.ExtraReferencedObjects.ToList();
            newItems.AddRange(entriesToReference.Select(x => new UIndex(x.UIndex)));
            worldBin.ExtraReferencedObjects = newItems.ToArray();
            world.WriteBinary(worldBin);
        }

        private static string[] assetsToEnsureReferencedInSim = new[]
        {
            "BIOG_GTH_TRO_NKD_R.NKDa.GTH_TRO_NKDa_MDL", // Geth Trooper
            "BIOG_GTH_STP_NKD_R.NKDa.GTH_STP_NKDa_MDL", // Geth Prime
        };

        private static void LevelSpecificPostCorrections(string fName, IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            var upperFName = fName.ToUpper();

            // Semi-global
            switch (upperFName)
            {
                case "BIOA_PRC2_CCCAVE_DSG":
                case "BIOA_PRC2_CCLAVA_DSG":
                case "BIOA_PRC2_CCCRATE_DSG":
                case "BIOA_PRC2_CCTHAI_DSG":
                    // Might need Aherns
                    {
                        // Force the pawns that will spawn to have their meshes in memory
                        // They are not referenced directly

                        var assetsToReference = le1File.Exports.Where(x => assetsToEnsureReferencedInSim.Contains(x.InstancedFullPath));
                        AddWorldReferencedObjects(le1File, assetsToReference);

                        foreach (var exp in le1File.Exports.Where(x => x.ClassName == "Sequence"))
                        {
                            var seqName = exp.GetProperty<StrProperty>("ObjName")?.Value;

                            #region Skip broken SeqAct_ActorFactory for bit explosion :(

                            if (seqName == "Spawn_Single_Guy")
                            {
                                // These sequences need the 'bit explosion' effect removed because BioWare changed something in SeqAct_ActorFactory and completely broke it
                                // We are just going to use the crust effect instead
                                var sequenceObjects = exp.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                                foreach (var seqObjProp in sequenceObjects.ToList()) // ToList as we're going to modify it so we need a copy
                                {
                                    if (!sequenceObjects.Contains(seqObjProp))
                                        continue; // it's already been removed

                                    var seqObj = seqObjProp.ResolveToEntry(le1File) as ExportEntry;
                                    if (seqObj != null)
                                    {
                                        if (seqObj.ClassName == "SeqAct_ActorFactory")
                                        {
                                            var outLinks = SeqTools.GetOutboundLinksOfNode(seqObj);
                                            outLinks[0].RemoveAt(0); // Remove the first outlink, which goes to Delay
                                            SeqTools.WriteOutboundLinksToNode(seqObj, outLinks); // remove the link so we don't try to connect to it when skipping
                                            SeqTools.SkipSequenceElement(seqObj, "Finished");
                                            sequenceObjects.Remove(seqObjProp);
                                        }
                                        else if (seqObj.ClassName == "BioSeqAct_Delay")
                                        {
                                            // We can ID these by the position data since they are built from a template and thus always have the same positions
                                            // It also references destroying the spawned particle system
                                            var props = seqObj.GetProperties();
                                            if (props.GetProp<IntProperty>("ObjPosX")?.Value == 4440 && props.GetProp<IntProperty>("ObjPosY")?.Value == 2672)
                                            {
                                                // This needs removed too
                                                var nextNodes = SeqTools.GetOutboundLinksOfNode(seqObj);
                                                var nextNode = nextNodes[0][0].LinkedOp as ExportEntry;
                                                var subNodeOfNext = SeqTools.GetVariableLinksOfNode(nextNode)[0].LinkedNodes[0] as ExportEntry;

                                                // Remove all of them from the sequence
                                                sequenceObjects.Remove(seqObjProp); // Delay
                                                sequenceObjects.Remove(new ObjectProperty(subNodeOfNext.UIndex)); // Destroy
                                                sequenceObjects.Remove(new ObjectProperty(nextNode.UIndex)); // SeqVar_Object
                                            }
                                        }
                                    }
                                }

                                exp.WriteProperty(sequenceObjects);
                            }

                            #endregion

                            #region Fix dual-finishing sequence. Someone thought they were being clever, bet they didn't know it'd cause an annurysm 12 years later
                            else if (seqName == "Hench_Take_Damage")
                            {
                                var sequenceObjects = exp.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects").Select(x => x.ResolveToEntry(le1File) as ExportEntry).ToList();
                                var attachEvents = sequenceObjects.Where(x => x.ClassName == "SeqAct_AttachToEvent").ToList(); // We will route one of these to the other
                                var starting = sequenceObjects.First(x => x.ClassName == "SeqEvent_SequenceActivated");
                                //var ending = sequenceObjects.First(x => x.ClassName == "SeqEvent_FinishSequence");
                                KismetHelper.RemoveOutputLinks(starting); // prevent dual outs
                                KismetHelper.CreateOutputLink(starting, "Out", attachEvents[0]);
                                KismetHelper.RemoveOutputLinks(attachEvents[0]);
                                KismetHelper.CreateOutputLink(attachEvents[0], "Out", attachEvents[1]); // Make it serial
                            }
                            #endregion
                        }
                    }
                    break;
            }

            // Individual
            switch (upperFName)
            {
                case "BIOA_PRC2_CCLAVA_DSG":
                    {
                        // SeqAct_ChangeCollision changed and requires an additional property otherwise it doesn't work.
                        string[] collisionsToTurnOff = new[]
                        {
                            // Hut doors and kill volumes
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_1",
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_3",
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_5",
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_6",
                        };

                        foreach (var cto in collisionsToTurnOff)
                        {
                            var exp = le1File.FindExport(cto);
                            exp.WriteProperty(new EnumProperty("COLLIDE_NoCollision", "ECollisionType", MEGame.LE1, "CollisionType"));
                        }

                        string[] collisionsToTurnOn = new[]
                        {
                            // Hut doors and kill volumes
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_0",
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_9",
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_2",
                            "TheWorld.PersistentLevel.Main_Sequence.Set_Hut_Accessibility.SeqAct_ChangeCollision_4",
                        };

                        foreach (var cto in collisionsToTurnOn)
                        {
                            var exp = le1File.FindExport(cto);
                            exp.WriteProperty(new EnumProperty("COLLIDE_BlockAll", "ECollisionType", MEGame.LE1, "CollisionType"));
                        }
                    }
                    break;
            }
        }

        private static void ReinstateCoverSlots(IMEPackage me1File, IMEPackage le1File, VTestOptions vTestOptions)
        {
            var coverLinks = le1File.Exports.Where(x => x.ClassName == "CoverLink");
            foreach (var le1CoverLink in coverLinks)
            {
                var me1CoverLink = me1File.FindExport(le1CoverLink.InstancedFullPath);
                var me1Slots = me1CoverLink.GetProperty<ArrayProperty<StructProperty>>("Slots");
                if (me1Slots != null && me1Slots.Any())
                {
                    var le1Slots = new ArrayProperty<StructProperty>("Slots");

                    foreach (var slot in me1Slots)
                    {
                        le1Slots.Add(ConvertCoverSlot(slot, vTestOptions));
                    }

                    le1CoverLink.WriteProperty(le1Slots);
                    le1File.Save();
                }

            }
        }

        private static void PortInCorrectedTerrain(IMEPackage le1File, string vTestIFP, string materialsFile, VTestOptions vTestOptions)
        {
            // Port in the material's file terrain - but not it's subcomponents
            using var le1VanillaTerrainP = MEPackageHandler.OpenMEPackage(Path.Combine(LE1Directory.CookedPCPath, materialsFile));
            var le1VanillaTerrain = le1VanillaTerrainP.Exports.FirstOrDefault(x => x.ClassName == "Terrain");
            le1VanillaTerrain.RemoveProperty("TerrainComponents");

            var rop = new RelinkerOptionsPackage() { Cache = vTestOptions.cache };

            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, le1VanillaTerrain, le1File,
                le1File.FindExport("TheWorld.PersistentLevel"), true, rop, out var destTerrainEntry);
            var destTerrain = destTerrainEntry as ExportEntry;

            // Port in the precomputed components
            var sourceTerrain = vTestOptions.vTestHelperPackage.FindExport(vTestIFP);
            ArrayProperty<ObjectProperty> components = new ArrayProperty<ObjectProperty>("TerrainComponents");
            foreach (var subComp in sourceTerrain.GetProperty<ArrayProperty<ObjectProperty>>("TerrainComponents"))
            {
                rop.CrossPackageMap.Clear();
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, vTestOptions.vTestHelperPackage.GetUExport(subComp.Value), le1File,
                    destTerrain, true, rop, out var newSubComp);
                components.Add(new ObjectProperty(newSubComp.UIndex));
            }
            destTerrain.WriteProperty(components);

            // Manual fixes for VTest
            destTerrain.RemoveProperty("PrePivot"); // on lav60 donor terrain

            // Update the main terrain with our data, without touching anything about materials or layers
            PackageEditorExperimentsM.ImportUDKTerrainData(sourceTerrain, destTerrain, false);
        }


        private static void CorrectTextures(IMEPackage package)
        {
            foreach (var exp in package.Exports.Where(x => x.IsTexture()))
            {
                var props = exp.GetProperties();
                var texinfo = ObjectBinary.From<UTexture2D>(exp);
                var numMips = texinfo.Mips.Count;
                var ns = props.GetProp<BoolProperty>("NeverStream");
                int lowMipCount = 0;
                for (int i = numMips - 1; i >= 0; i--)
                {
                    if (lowMipCount > 6 && (ns == null || ns.Value == false) && texinfo.Mips[i].IsLocallyStored && texinfo.Mips[i].StorageType != StorageTypes.empty)
                    {
                        exp.WriteProperty(new BoolProperty(true, "NeverStream"));
                        lowMipCount = -100; // This prevents this block from running again
                    }

                    if (texinfo.Mips[i].StorageType == StorageTypes.empty && exp.Parent.ClassName != "TextureCube")
                    {
                        // Strip this empty mip
                        Debug.WriteLine($"Dropping empty mip {i} in {exp.InstancedFullPath}");
                        texinfo.Mips.RemoveAt(i);
                    }

                    lowMipCount++;
                }
                exp.WriteBinary(texinfo);

                // Correct the MipTailBaseIdx. It's an indexer thing so it starts at 0
                //exp.WriteProperty(new IntProperty(texinfo.Mips.Count - 1, "MipTailBaseIdx"));

                // Correct the size. Is this required?

            }
        }

        private static void CorrectSequences(IMEPackage le1File, VTestOptions vTestOptions)
        {
            // Find sequences that aren't in other sequences
            foreach (var seq in le1File.Exports.Where(e => e is { ClassName: "Sequence" } && !e.Parent.IsA("SequenceObject")))
            {
                CorrectSequenceObjects(seq, vTestOptions);
            }
        }


        public static void RebuildPersistentLevelChildren(ExportEntry pl)
        {
            ExportEntry[] actorsToAdd = pl.FileRef.Exports.Where(exp => exp.Parent == pl && exp.IsA("Actor")).ToArray();
            Level level = ObjectBinary.From<Level>(pl);
            level.Actors.Clear();
            foreach (var actor in actorsToAdd)
            {
                // Don't add things that are in collection actors

                var lc = actor.GetProperty<ObjectProperty>("LightComponent");
                if (lc != null && pl.FileRef.TryGetUExport(lc.Value, out var lightComp))
                {
                    if (lightComp.Parent != null && lightComp.Parent.ClassName == "StaticLightCollectionActor")
                        continue; // don't add this one
                }

                //var mc = actor.GetProperty<ObjectProperty>("MeshComponent");
                //if (mc != null && pl.FileRef.TryGetUExport(mc.Value, out var meshComp))
                //{
                //    if (meshComp.Parent != null && meshComp.Parent.ClassName == "StaticMeshCollectionActor")
                //        continue; // don't add this one
                //}

                level.Actors.Add(new UIndex(actor.UIndex));
            }

            //if (level.Actors.Count > 1)
            //{

            // BioWorldInfo will always be present
            // or at least, it better be!
            // Slot 2 has to be blank in LE. In ME1 i guess it was a brush.
            level.Actors.Insert(1, new UIndex(0)); // This is stupid
                                                   //}

            pl.WriteBinary(level);
        }




        private static void CorrectSequenceObjects(ExportEntry seq, VTestOptions vTestOptions)
        {
            // Set ObjInstanceVersions to LE value
            if (seq.IsA("SequenceObject"))
            {
                if (LE1UnrealObjectInfo.SequenceObjects.TryGetValue(seq.ClassName, out var soi))
                {
                    seq.WriteProperty(new IntProperty(soi.ObjInstanceVersion, "ObjInstanceVersion"));
                }
                else
                {
                    Debug.WriteLine($"SequenceCorrection: Didn't correct {seq.UIndex} {seq.ObjectName}, not in LE1 ObjectInfo SequenceObjects");
                }

                var children = seq.GetChildren();
                foreach (var child in children)
                {
                    if (child is ExportEntry chExp)
                    {
                        CorrectSequenceObjects(chExp, vTestOptions);
                    }
                }
            }

            // Fix extra four bytes after SeqAct_Interp
            if (seq.ClassName == "SeqAct_Interp")
            {
                seq.WriteBinary(Array.Empty<byte>());
            }

            if (seq.ClassName == "SeqAct_SetInt")
            {
                seq.WriteProperty(new BoolProperty(true, "bIsUpdated"));
            }


            // Fix missing PropertyNames on VariableLinks
            if (seq.IsA("SequenceOp"))
            {
                var varLinks = seq.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
                if (varLinks is null) return;
                foreach (var t in varLinks.Values)
                {
                    string desc = t.GetProp<StrProperty>("LinkDesc").Value;

                    if (desc == "Target" && seq.ClassName == "SeqAct_SetBool")
                    {
                        t.Properties.AddOrReplaceProp(new NameProperty("Target", "PropertyName"));
                    }

                    if (desc == "Value" && seq.ClassName == "SeqAct_SetInt")
                    {
                        t.Properties.AddOrReplaceProp(new NameProperty("Values", "PropertyName"));
                    }
                }

                seq.WriteProperty(varLinks);
            }
        }

        private static Guid? tempDonorGuid = null;
        private static void CorrectTerrainMaterials(IMEPackage le1File)
        {
            // Todo: Improve this... somehow
            if (tempDonorGuid == null)
            {
                using var donorMatP = MEPackageHandler.OpenMEPackage(Path.Combine(LE1Directory.CookedPCPath, "BIOA_PRO10_11_LAY.pcc"));
                var terrain = donorMatP.FindExport("TheWorld.PersistentLevel.Terrain_0");
                var terrbinD = ObjectBinary.From<Terrain>(terrain);
                tempDonorGuid = terrbinD.CachedTerrainMaterials[0].ID;
            }

            var fname = Path.GetFileNameWithoutExtension(le1File.FilePath);
            var terrains = le1File.Exports.Where(x => x.ClassName == "Terrain").ToList();
            foreach (var terrain in terrains)
            {
                var terrbin = ObjectBinary.From<Terrain>(terrain);

                foreach (var terrainMat in terrbin.CachedTerrainMaterials)
                {
                    terrainMat.ID = tempDonorGuid.Value;
                }

                terrain.WriteBinary(terrbin);
            }
        }

        // ME1 -> LE1 Prefab's Sequence class was changed to a subclass. No different props though.P
        private static void CorrectPrefabSequenceClass(IMEPackage le1File)
        {
            foreach (var le1Exp in le1File.Exports.Where(x => x.IsA("Prefab")))
            {
                var prefabSeqObj = le1Exp.GetProperty<ObjectProperty>("PrefabSequence");
                if (prefabSeqObj != null && prefabSeqObj.ResolveToEntry(le1File) is ExportEntry export)
                {
                    var prefabSeqClass = le1File.FindImport("Engine.PrefabSequence");
                    if (prefabSeqClass == null)
                    {
                        var seqClass = le1File.FindImport("Engine.Sequence");
                        prefabSeqClass = new ImportEntry(le1File, seqClass.Parent?.UIndex ?? 0, "PrefabSequence") { PackageFile = seqClass.PackageFile, ClassName = "Class" };
                        le1File.AddImport(prefabSeqClass);
                    }
                    Debug.WriteLine($"Corrected Sequence -> PrefabSequence class type for {le1Exp.InstancedFullPath}");
                    export.Class = prefabSeqClass;
                }
            }
        }

        private static void CorrectPathfindingNetwork(IMEPackage me1File, IMEPackage le1File)
        {
            var le1PL = le1File.FindExport("TheWorld.PersistentLevel");
            Level me1L = ObjectBinary.From<Level>(me1File.FindExport("TheWorld.PersistentLevel"));
            Level le1L = ObjectBinary.From<Level>(le1PL);

            PropertyCollection mcs = new PropertyCollection();
            mcs.AddOrReplaceProp(new FloatProperty(400, "Radius"));
            mcs.AddOrReplaceProp(new FloatProperty(400, "Height"));
            StructProperty maxPathSize = new StructProperty("Cylinder", mcs, "MaxPathSize");

            // Chain start and end
            if (me1L.NavListEnd.value != 0 && le1File.FindExport(me1File.GetUExport(me1L.NavListEnd.value).InstancedFullPath) is { } matchingNavEnd)
            {
                le1L.NavListEnd = new UIndex(matchingNavEnd.UIndex);

                if (me1L.NavListStart.value != 0 && le1File.FindExport(me1File.GetUExport(me1L.NavListStart.value).InstancedFullPath) is { } matchingNavStart)
                {
                    le1L.NavListStart = new UIndex(matchingNavStart.UIndex);

                    // TEST: Widen the size of each node to see if that's why BioActorFactory fires Cancelled
                    while (matchingNavStart != null)
                    {
                        int uindex = matchingNavStart.UIndex;
                        var props = matchingNavStart.GetProperties();
                        props.AddOrReplaceProp(maxPathSize);
                        var next = props.GetProp<ObjectProperty>("nextNavigationPoint");
                        matchingNavStart.WriteProperties(props);
                        matchingNavStart = next?.ResolveToEntry(le1File) as ExportEntry;
                        if (matchingNavStart == null && uindex != matchingNavEnd.UIndex)
                        {
                            Debugger.Break();
                        }
                    }
                }


            }

            // Cross level actors
            foreach (var exportIdx in me1L.CrossLevelActors)
            {
                var me1E = me1File.GetUExport(exportIdx.value);
                if (le1File.FindExport(me1E.InstancedFullPath) is { } crossLevelActor)
                {
                    le1L.CrossLevelActors.Add(new UIndex(crossLevelActor.UIndex));
                }
            }

            // Regenerate the 'End' struct cause it will have ported wrong
            CorrectReachSpecs(me1File, le1File);
            le1PL.WriteBinary(le1L);
        }

        /// <summary>
        /// Corrects UDK/ME1/ME2 reachspec system to ME3 / LE
        /// </summary>
        /// <param name="me1File"></param>
        /// <param name="le1File"></param>
        public static void CorrectReachSpecs(IMEPackage me1File, IMEPackage le1File)
        {

            // Have to do LE1 -> ME1 for references as not all reachspecs may have been ported
            foreach (var le1Exp in le1File.Exports.Where(x => x.IsA("ReachSpec")))
            {
                var le1End = le1Exp.GetProperty<StructProperty>("End");
                if (le1End != null)
                {
                    var me1Exp = me1File.FindExport(le1Exp.InstancedFullPath);
                    var me1End = me1Exp.GetProperty<StructProperty>("End");
                    var le1Props = le1Exp.GetProperties();
                    le1Props.RemoveNamedProperty("End");

                    PropertyCollection newEnd = new PropertyCollection();
                    newEnd.Add(me1End.GetProp<StructProperty>("Guid"));

                    var me1EndEntry = me1End.GetProp<ObjectProperty>("Nav");
                    me1EndEntry ??= me1End.GetProp<ObjectProperty>("Actor"); // UDK uses 'Actor' but it's in wrong position
                    if (me1EndEntry != null)
                    {
                        newEnd.Add(new ObjectProperty(le1File.FindExport(me1File.GetUExport(me1EndEntry.Value).InstancedFullPath).UIndex, "Actor"));
                    }
                    else
                    {
                        newEnd.Add(new ObjectProperty(0, "Actor")); // This is probably cross level or end of chain
                    }

                    StructProperty nes = new StructProperty("ActorReference", newEnd, "End", true);
                    le1Props.AddOrReplaceProp(nes);
                    le1Exp.WriteProperties(le1Props);
                    le1Exp.WriteBinary(new byte[0]); // When porting from UDK there's some binary data. This removes it

                    // Test properties
                    le1Exp.GetProperties();
                }
            }
        }

        #endregion

        #region QA Methods
        public static void VTest_Check()
        {
            var vtestFinalFiles = Directory.GetFiles(PAEMPaths.VTest_FinalDestDir);
            var vtestFinalFilesAvailable = vtestFinalFiles.Select(x => Path.GetFileNameWithoutExtension(x).ToLower()).ToList();
            foreach (var v in vtestFinalFiles)
            {
                using var package = MEPackageHandler.OpenMEPackage(v);

                #region Check BioTriggerStream files exists
                var triggerStraems = package.Exports.Where(x => x.ClassName == "BioTriggerStream").ToList();
                foreach (var triggerStream in triggerStraems)
                {
                    var streamingStates = triggerStream.GetProperty<ArrayProperty<StructProperty>>("StreamingStates");
                    if (streamingStates != null)
                    {
                        foreach (var ss in streamingStates)
                        {
                            List<NameProperty> namesToCheck = new List<NameProperty>();
                            var inChunkName = ss.GetProp<NameProperty>("InChunkName");

                            if (inChunkName.Value.Name != "None" && !vtestFinalFilesAvailable.Contains(inChunkName.Value.Name.ToLower()))
                            {
                                Debug.WriteLine($"LEVEL MISSING (ICN): {inChunkName} in {triggerStream.UIndex} {triggerStream.ObjectName.Instanced}");
                            }

                            foreach (var levelNameProperty in ss.GetProp<ArrayProperty<NameProperty>>("VisibleChunkNames"))
                            {
                                var levelName = levelNameProperty.Value.Name;
                                if (levelName != "None" && !vtestFinalFilesAvailable.Contains(levelName.ToLower()))
                                {
                                    Debug.WriteLine($"LEVEL MISSING (VC): {levelName} in {triggerStream.UIndex} {triggerStream.ObjectName.Instanced}");
                                }
                            }

                            foreach (var levelNameProperty in ss.GetProp<ArrayProperty<NameProperty>>("LoadChunkNames"))
                            {
                                var levelName = levelNameProperty.Value.Name;
                                if (levelName != "None" && !vtestFinalFilesAvailable.Contains(levelName.ToLower()))
                                {
                                    Debug.WriteLine($"LEVEL MISSING (LC): {levelName} in {triggerStream.UIndex} {triggerStream.ObjectName.Instanced}");
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"{triggerStream.InstancedFullPath} in {v} has NO StreamingStates!!");
                    }
                }
                #endregion

                #region Check Level has at least 2 actors

                var level = package.FindExport("TheWorld.PersistentLevel");
                {
                    if (level != null)
                    {
                        var levelBin = ObjectBinary.From<Level>(level);
                        Debug.WriteLine($"{Path.GetFileName(v)} actor list count: {levelBin.Actors.Count}");
                    }
                }

                #endregion

            }
        }
        #endregion
    }
}
