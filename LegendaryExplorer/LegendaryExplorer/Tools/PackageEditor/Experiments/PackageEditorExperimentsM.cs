using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Numerics;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Packages;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.PathfindingEditor;
using LegendaryExplorer.Tools.Sequence_Editor;
using LegendaryExplorer.Tools.WwiseEditor;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Audio;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Misc.ME3Tweaks;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using Function = LegendaryExplorerCore.Unreal.Classes.Function;
using static LegendaryExplorerCore.Packages.CloningImportingAndRelinking.EntryImporter;

//using ImageMagick;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    /// <summary>
    /// Class where Mgamerz can put debug/dev/experimental code
    /// </summary>
    public class PackageEditorExperimentsM
    {
        private static MaterialScreenshotLE1 msLE1; // Don't fall out of scope
        public static void StartMatScreenshot(PackageEditorWindow pe)
        {
            msLE1 = new MaterialScreenshotLE1();
            msLE1.StartWorkflow(pe);
        }

        public static void FindBadReference(PackageEditorWindow pe)
        {
            if (pe.Pcc == null)
            {
                MessageBox.Show("Must have package open first");
                return;
            }

            var clipBoardText = Clipboard.GetText();
            bool nameRef = false;
            bool importRef = false;
            bool exportRef = false;
            int index = 0;
            if (!string.IsNullOrWhiteSpace(clipBoardText))
            {
                parseErrorLine(clipBoardText);
            }

            if (!importRef && !exportRef && !nameRef)
            {
                // Prompt
                var errorLine = PromptDialog.Prompt(pe, "Paste the entire error line into this dialog box.", "Search by pattern");
                parseErrorLine(errorLine);
            }

            if (!importRef && !exportRef && !nameRef)
            {
                MessageBox.Show("Can't parse the input string. Make sure it's the whole line starting with 'Bad <X> index i/j.");
                return;
            }

            if (exportRef)
                index += 1;
            if (importRef)
                index = (index * -1) - 1;

            var searchStream = pe.Pcc.SaveToStream(false);
            var matches = new List<int>();
            while (searchStream.Position < searchStream.Position - 4)
            {
                if (searchStream.ReadInt32() != index)
                {
                    searchStream.Position -= 3;
                    continue;
                }

                matches.Add((int)searchStream.Position - 4);
            }

            ListDialog ld = new ListDialog(matches.Select(x => x.ToString("X8")), "Matches", "The following areas in the file have the specified integer value.", pe);
            ld.Show();

            void parseErrorLine(string text)
            {
                if (text.StartsWith("Bad import index "))
                {
                    importRef = true;
                    string str = text.Substring("Bad import index ".Length);
                    str = str.Substring(0, str.IndexOf('/'));
                    index = int.Parse(str); // will be corrected later
                }
                else if (text.StartsWith("Bad export index "))
                {
                    exportRef = true;
                    string str = text.Substring("Bad export index ".Length);
                    str = str.Substring(0, str.IndexOf('/'));
                    index = int.Parse(str); // will be corrected later
                }
                else if (text.StartsWith("Bad name index "))
                {
                    nameRef = true;
                    string str = text.Substring("Bad name index ".Length);
                    str = str.Substring(0, str.IndexOf('/'));
                    index = int.Parse(str); // will be corrected later
                }
            }
        }

        public static void UpdateMaterialExpressionsList(PackageEditorWindow pe)
        {
            if (pe.Pcc != null && pe.GetSelected(out var idx) && idx > 0)
            {
                var exp = pe.Pcc.GetUExport(idx);
                if (exp.ClassName != "Material")
                    return;

                exp.WriteProperty(new ArrayProperty<ObjectProperty>(pe.Pcc.Exports.Where(x => x.idxLink == exp.UIndex && x.InheritsFrom("MaterialExpression")).Select(x => new ObjectProperty(x.UIndex)), "Expressions"));
            }
        }

        public static void CoalesceBioActorTypes(PackageEditorWindow pewpf)
        {

            Task.Run(() =>
            {
                MEPackageHandler.GlobalSharedCacheEnabled = false;
                PackageCache globalCache = new PackageCache();

                // Load global files into the cache to speed this process up.
                globalCache.InsertIntoCache(MEPackageHandler.OpenMEPackages(EntryImporter.FilesSafeToImportFrom(MEGame.LE1).Select(x => Path.Combine(MEDirectories.GetCookedPath(MEGame.LE1), x))));
                using var actorTypesPackage = MEPackageHandler.CreateAndOpenPackage(Path.Combine(MEDirectories.GetCookedPath(MEGame.LE1), "LE1ActorTypes.pcc"), MEGame.LE1);
                pewpf.BusyText = "Coalescing actor types...";
                pewpf.IsBusy = true;
                var allFiles = MELoadedFiles.GetOfficialFiles(MEGame.LE1).Where(x => Path.GetExtension(x) == ".pcc").ToList();
                int totalFiles = allFiles.Count;
                int numDone = 0;
                foreach (string filePath in allFiles)
                {
                    //if (!filePath.EndsWith("Engine.pcc"))
                    //    continue;
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                    foreach (var f in pcc.Exports.Where(x => !x.IsDefaultObject && x.IsA("BioActorType")))
                    {
                        EntryExporter.ExportExportToPackage(f, actorTypesPackage, out var _, globalCache);

                    }

                    numDone++;
                    pewpf.BusyText = $"Coalescing actor types [{numDone}/{totalFiles}]";
                }
                actorTypesPackage.Save();
                MEPackageHandler.GlobalSharedCacheEnabled = true;

            }).ContinueWithOnUIThread(foundCandidates => { pewpf.IsBusy = false; });
        }

        public static void EnumerateAllFunctions(PackageEditorWindow pewpf)
        {

            Task.Run(() =>
            {
                pewpf.BusyText = "Enumerating functions...";
                pewpf.IsBusy = true;
                var allFiles = MELoadedFiles.GetOfficialFiles(MEGame.LE3).Where(x => Path.GetExtension(x) == ".pcc")
                    .ToList();
                int totalFiles = allFiles.Count;
                int numDone = 0;
                foreach (string filePath in allFiles)
                {
                    //if (!filePath.EndsWith("Engine.pcc"))
                    //    continue;
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                    foreach (var f in pcc.Exports.Where(x => x.ClassName is "Function" or "State"))
                    {
                        if (pcc.Game is MEGame.ME1 or MEGame.ME2)
                        {
                            var func = f.ClassName == "State"
                                ? UE3FunctionReader.ReadState(f, f.Data)
                                : UE3FunctionReader.ReadFunction(f, f.Data);
                            func.Decompile(new TextBuilder(), false, true); //parse bytecode
                        }
                        else
                        {
                            var func = new Function(f.Data, f);
                            func.ParseFunction();
                        }
                    }

                    numDone++;
                    pewpf.BusyText = $"Enumerating functions [{numDone}/{totalFiles}]";
                }
            }).ContinueWithOnUIThread(foundCandidates => { pewpf.IsBusy = false; });
        }

        public static void ShaderCacheResearch(PackageEditorWindow pewpf)
        {
            Dictionary<string, int> mapCount = new Dictionary<string, int>();

            bool ScanForNames(byte[] bytes, IMEPackage package)
            {
                bool result = false;
                int pos = 0;
                //while (pos < bytes.Length - 8)
                //{
                var nameP1 = BitConverter.ToInt32(bytes, pos);
                var nameP2 = BitConverter.ToInt32(bytes, pos + 4);

                if (nameP1 != 0 && nameP2 == 0 && package.IsName(nameP1))
                {
                    var name = package.GetNameEntry(nameP1);
                    if (!mapCount.TryGetValue(name, out var count))
                    {
                        count = 1;
                    }
                    else
                    {
                        count++;
                    }

                    mapCount[name] = count;
                    result = name.StartsWith("F");
                }

                pos++;
                //}

                return result;
            }

            Task.Run(() =>
            {
                pewpf.BusyText = "Scanning ShaderCache files...";
                pewpf.IsBusy = true;
                Dictionary<string, int> typeCount = new Dictionary<string, int>();

                var files = Directory.GetFiles(@"X:\Downloads\f", "*.pcc");
                foreach (var f in files)
                {
                    var package = MEPackageHandler.OpenMEPackage(f, forceLoadFromDisk: true);
                    var sfsce = package.FindExport("SeekFreeShaderCache");
                    if (sfsce != null)
                    {
                        var sfsc = ObjectBinary.From<ShaderCache>(sfsce);
                        foreach (var shaderPair in sfsc.Shaders)
                        {
                            var isF = ScanForNames(shaderPair.Value.unkBytes, package);
                            if (isF)
                            {
                                if (!typeCount.TryGetValue(shaderPair.Value.ShaderType, out var count))
                                {
                                    count = 1;
                                }
                                else
                                {
                                    count++;
                                }

                                typeCount[shaderPair.Value.ShaderType] = count;
                            }
                        }
                    }
                }

                Debug.WriteLine("");
                foreach (var kp in mapCount.OrderByDescending(x => x.Value))
                {
                    Debug.WriteLine($"{kp.Key}: {kp.Value}");
                }

                Debug.WriteLine("");
                Debug.WriteLine("Type counts:");
                foreach (var kp in typeCount.OrderByDescending(x => x.Value))
                {
                    Debug.WriteLine($"{kp.Key}: {kp.Value}");
                }

                return true;
            }).ContinueWithOnUIThread(foundCandidates => { pewpf.IsBusy = false; });
        }

        public static void CramLevelFullOfStuff(IMEPackage startPackage, PackageEditorWindow pewpf)
        {
            if (pewpf.Pcc.Game != MEGame.LE3)
            {
                MessageBox.Show(pewpf, "Not an LE3 file!");
                return;
            }

            var btsGlobal = pewpf.Pcc.Exports.FirstOrDefault(x => x.ClassName == "BioTriggerStream" && x.GetProperty<NameProperty>("TierName")?.Value.Name == "TIER_Global");
            if (btsGlobal == null)
            {
                return;
            }

            var lskPackageNames = pewpf.Pcc.Exports.Where(x => x.ClassName == "LevelStreamingKismet").Select(x => x.GetProperty<NameProperty>("PackageName").Value).ToList();

            var addedLSKs = new List<NameReference>();
            //addedLSKs.Add(new NameReference("BioA_GthLeg", 201));
            //addedLSKs.Add(new NameReference("BioA_GthLeg", 211));
            //addedLSKs.Add(new NameReference("BioA_GthLeg", 216));
            //addedLSKs.Add(new NameReference("BioA_GthLeg", 251));
            //addedLSKs.Add(new NameReference("BioA_GthLeg", 261));
            //addedLSKs.Add(new NameReference("BioA_GthLeg", 301));
            addedLSKs.Add(new NameReference("BioA_GthLeg300BSP"));
            addedLSKs.Add(new NameReference("BioA_GthLeg325Temp"));
            //addedLSKs.Add(new NameReference("BioA_GthLeg",351));

            //addedLSKs.Add(new NameReference("BioA_GthLeg", 500));
            //addedLSKs.Add(new NameReference("BioA_GthLeg", 550));

            var ss = btsGlobal.GetProperty<ArrayProperty<StructProperty>>("StreamingStates");
            lskPackageNames.AddRange(addedLSKs);

            var existingStuff = ss[0].GetProp<ArrayProperty<NameProperty>>("VisibleChunkNames");
            existingStuff.AddRange(addedLSKs.Select(x => new NameProperty(x)));
            btsGlobal.WriteProperty(ss);

            var sourceToClone = pewpf.Pcc.Exports.First(x => x.ClassName == "LevelStreamingKismet");
            foreach (var added in addedLSKs)
            {
                var newEntry = EntryCloner.CloneEntry(sourceToClone) as ExportEntry;
                newEntry.WriteProperty(new NameProperty(added, "PackageName"));
            }

            // Step 2: Rebuild StreamingLevels
            RebuildStreamingLevels(pewpf.Pcc);
        }

        private static void RebuildStreamingLevels(IMEPackage Pcc)
        {
            try
            {
                var levelStreamingKismets = new List<ExportEntry>();
                ExportEntry bioworldinfo = null;
                foreach (ExportEntry exp in Pcc.Exports)
                {
                    switch (exp.ClassName)
                    {
                        case "BioWorldInfo" when exp.ObjectName == "BioWorldInfo":
                            bioworldinfo = exp;
                            continue;
                        case "LevelStreamingKismet" when exp.ObjectName == "LevelStreamingKismet":
                            levelStreamingKismets.Add(exp);
                            continue;
                    }
                }

                levelStreamingKismets = levelStreamingKismets
                    .OrderBy(o => o.GetProperty<NameProperty>("PackageName").ToString()).ToList();
                if (bioworldinfo != null)
                {
                    var streamingLevelsProp =
                        bioworldinfo.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels") ??
                        new ArrayProperty<ObjectProperty>("StreamingLevels");

                    streamingLevelsProp.Clear();
                    foreach (ExportEntry exp in levelStreamingKismets)
                    {
                        streamingLevelsProp.Add(new ObjectProperty(exp.UIndex));
                    }

                    bioworldinfo.WriteProperty(streamingLevelsProp);
                    //MessageBox.Show("Done.");
                }
                else
                {
                    MessageBox.Show("No BioWorldInfo object found in this file.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error setting streaming levels:\n" + ex.Message);
            }
        }

        public static void ResetTexturesInFile(IMEPackage sourcePackage, PackageEditorWindow pewpf)
        {
            if (sourcePackage.Game != MEGame.ME1 && sourcePackage.Game != MEGame.ME2 &&
                sourcePackage.Game != MEGame.ME3)
            {
                MessageBox.Show(pewpf, "Not a trilogy file!");
                return;
            }

            Task.Run(() =>
            {
                pewpf.BusyText = "Finding unmodded candidates...";
                pewpf.IsBusy = true;
                return SharedPackageTools.GetUnmoddedCandidatesForPackage(pewpf);
            }).ContinueWithOnUIThread(foundCandidates =>
            {
                pewpf.IsBusy = false;
                if (!foundCandidates.Result.Any())
                {
                    MessageBox.Show(pewpf, "Cannot find any candidates for this file!");
                    return;
                }

                var choices = foundCandidates.Result.DiskFiles.ToList(); //make new list
                choices.AddRange(foundCandidates.Result.SFARPackageStreams.Select(x => x.Key));

                var choice = InputComboBoxDialog.GetValue(pewpf, "Choose file to reset to:", "Texture reset", choices,
                    choices.Last());
                if (string.IsNullOrEmpty(choice))
                {
                    return;
                }

                var restorePackage = MEPackageHandler.OpenMEPackage(choice, forceLoadFromDisk: true);

                // Get classes
                var differences = restorePackage.CompareToPackage(sourcePackage);

                // Classes
                var classNames = differences.Where(x => x.Entry != null).Select(x => x.Entry.ClassName).Distinct().OrderBy(x => x).ToList();
                if (classNames.Any())
                {
                    var allDiffs = "[ALL DIFFERENCES]";
                    classNames.Insert(0, allDiffs);
                    var restoreClass = InputComboBoxDialog.GetValue(pewpf, "Select class type to restore instances of:",
                        "Data reset", classNames, classNames.FirstOrDefault());
                    if (string.IsNullOrEmpty(restoreClass))
                    {
                        return;
                    }

                    foreach (var exp in restorePackage.Exports.Where(x =>
                        x.ClassName != "BioMaterialInstanceConstant" || restoreClass == allDiffs ||
                        x.ClassName == restoreClass))
                    {
                        var origExp = restorePackage.GetUExport(exp.UIndex);
                        sourcePackage.GetUExport(exp.UIndex).Data = origExp.Data;
                        sourcePackage.GetUExport(exp.UIndex).SetHeaderValuesFromByteArray(origExp.GenerateHeader());
                    }
                }
            });
        }

        public static void DumpPackageTextures(IMEPackage sourcePackage, PackageEditorWindow pewpf)
        {
            var dlg = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select Folder Containing Localized Files"
            };
            if (dlg.ShowDialog(pewpf) == CommonFileDialogResult.Ok)
            {
                foreach (var t2dx in sourcePackage.Exports.Where(x => x.IsTexture()))
                {
                    var outF = Path.Combine(dlg.FileName, t2dx.ObjectName + ".png");
                    var t2d = new Texture2D(t2dx);
                    t2d.ExportToPNG(outF);
                }
            }

            MessageBox.Show("Done");
        }

        public static void FindExternalizableTextures(PackageEditorWindow pewpf)
        {
            MessageBox.Show(pewpf,
                "WARNING: THIS WILL MODIFY ALL PACKAGES IN THE FOLDER YOU SELECT.\nMake a backup of this folder as this operation cannot be undone!\nMake sure there are no tool windows open in Legendary Explorer.");
            var dlg = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select Folder Containing Package Files"
            };
            if (dlg.ShowDialog(pewpf) == CommonFileDialogResult.Ok)
            {
                TFCTools.FindExternalizableTextures(dlg.FileName);
            }
        }


        /// <summary>
        /// Builds a comparison of TESTPATCH functions against their original design. View the difference with WinMerge Folder View.
        /// By Mgamerz
        /// </summary>
        public static void BuildTestPatchComparison()
        {
            var oldPath = ME3Directory.DefaultGamePath;
            // To run this change these values

            // Point to unpacked path.
            ME3Directory.DefaultGamePath = @"Z:\Mass Effect 3";
            var patchedOutDir = Directory.CreateDirectory(@"C:\users\mgamerz\desktop\patchcomp\patch").FullName;
            var origOutDir = Directory.CreateDirectory(@"C:\users\mgamerz\desktop\patchcomp\orig").FullName;
            var patchFiles =
                Directory.GetFiles(
                    @"C:\Users\Mgamerz\Desktop\ME3CMM\data\Patch_001_Extracted\BIOGame\DLC\DLC_TestPatch\CookedPCConsole",
                    "Patch_*.pcc");

            // End variables

            //preload these packages to speed up lookups
            using var package1 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "SFXGame.pcc"));
            using var package2 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "Engine.pcc"));
            using var package3 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "Core.pcc"));
            using var package4 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "Startup.pcc"));
            using var package5 =
                MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "GameFramework.pcc"));
            using var package6 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "GFxUI.pcc"));
            using var package7 =
                MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "BIOP_MP_COMMON.pcc"));

            // These paths can't be easily determined so just manually build list
            // Some are empty paths cause they could be determined with code updates 
            // and i was too lazy to remove them.
            Dictionary<string, string> extraMappings = new Dictionary<string, string>()
            {
                {"SFXGameContent.SFXAICmd_Base_GethPrimeShieldDrone", "SFXPawn_GethPrime"},
                {"SFXGameMPContent.SFXGameEffect_MatchConsumable_AmmoPower_ArmorPiercing", "SFXGE_MatchConsumables"},
                {"SFXGameMPContent.SFXGameEffect_MatchConsumable_AmmoPower_Disruptor", "SFXGE_MatchConsumables"},
                {"SFXGameMPContent.SFXObjective_Retrieve_PickupObject", "SFXEngagement_Retrieve"},
                {"SFXGameContentDLC_CON_MP2.SFXObjective_Retrieve_PickupObject_DLC", "SFXEngagement_RetrieveDLC"},
                {"SFXGameContentDLC_CON_MP2.SFXObjective_Retrieve_DropOffLocation_DLC", "SFXEngagement_RetrieveDLC"},
                {"SFXGameContent.SFXPowerCustomAction_GethPrimeTurret", "SFXPawn_GethPrime"},
                {"SFXGameContent.SFXPowerCustomAction_ConcussiveShot", ""},
                {"SFXGameContent.SFXPowerCustomAction_BioticCharge", ""},
                {"SFXGameContentDLC_CON_MP1.SFXProjectile_BatarianSniperRound", "SFXWeapon_SniperRifle_BatarianDLC"},
                {"SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_BioticCharge_Krogan", "SFXPower_KroganBioticCharge"},
                {"SFXGameMPContent.SFXPowerCustomActionMP_FemQuarianPassive", "SFXPowerMP_FemQuarPassive"},
                {
                    "SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_KroganPassive_Vanguard",
                    "SFXPower_KroganVanguardPassive"
                },
                {"SFXGameContentDLC_CON_MP2.SFXPowerCustomActionMP_MaleQuarianPassive", "SFXPower_MQPassive"},
                {"SFXGameMPContent.SFXPowerCustomActionMP_AsariPassive", ""},
                {"SFXGameMPContent.SFXPowerCustomActionMP_DrellPassive", ""},
                {"SFXGameMPContent.SFXPowerCustomActionMP_HumanPassive", ""},
                {"SFXGameMPContent.SFXPowerCustomActionMP_KroganPassive", ""},
                {"SFXGameMPContent.SFXPowerCustomActionMP_PassiveBase", ""},
                {"SFXGameMPContent.SFXPowerCustomActionMP_SalarianPassive", ""},
                {"SFXGameMPContent.SFXPowerCustomActionMP_TurianPassive", ""},
                {"SFXGameContentDLC_CON_MP2.SFXPowerCustomActionMP_VorchaPassive", "SFXPower_VorchaPassive"},
                {"SFXGameContentDLC_CON_MP2.SFXPowerCustomActionMP_WhipManPassive", "SFXPower_WhipManPassive"},
                {"SFXGameContent.SFXAICmd_Banshee_Aggressive", "SFXpawn_Banshee"},
                {"SFXGameContent.SFXAI_GethPrimeShieldDrone", "SFXPawn_GethPrime"},
                {"SFXGameContent.SFXAI_ProtectorDrone", "SFXPower_ProtectorDrone"},
                {"SFXGameContent.SFXAmmoContainer", "Biod_MPTowr"},
                {"SFXGameContentDLC_CON_MP3.SFXCustomAction_N7TeleportPunchBase", "N7_Vanguard_MP"},
                {"SFXGameContentDLC_CON_MP3.SFXCustomAction_N7VanguardEvadeBase", "N7_Vanguard_MP"},
                {"SFXGameContent.SFXCustomAction_SimpleMoveBase", "SFXPawn_GethPyro"},
                {"SFXGameContent.SFXCustomAction_BansheeDeath", "SFXPawn_Banshee"},
                {"SFXGameContent.SFXCustomAction_BansheePhase", "SFXPawn_Banshee"},
                {"SFXGameContent.SFXCustomAction_DeployTurret", "SFXPawn_Gunner"},
                {"SFXGameMPContent.SFXCustomAction_KroganRoar", "Krogan_Soldier_MP"},
                {"SFXGameContent.SFXCustomAction_Revive", "SFXCharacterClass_Infiltrator"},
                {"SFXGameContent.SFXDroppedGrenade", "Biod_MPTowr"},
                {"SFXGameContentDLC_CON_MP2_Retrieve.SFXEngagement_Retrieve_DLC", "Startup_DLC_CON_MP2_INT"},
                {"SFXGameContent.SFXGameEffect_WeaponMod_PenetrationDamageBonus", "SFXWeaponMods_AssaultRifles"},
                {"SFXGameContent.SFXGameEffect_WeaponMod_WeightBonus", "SFXWeaponMods_SMGs"},
                {"SFXGameContentDLC_CON_MP1.SFXGameEffect_BatarianBladeDamageOverTime", "Batarian_Soldier_MP"},
                {"SFXGameContent.SFXGrenadeContainer", "Biod_MPTowr"},
                {"SFXGameMPContent.SFXObjective_Retrieve_DropOffLocation", "SFXEngagement_Retrieve"},
                {"SFXGameMPContent.SFXObjective_Annex_DefendZone", "SFXEngagement_Annex_Upload"},
                {"SFXGameMPContent.SFXObjective_Disarm_Base", "SFXEngagement_Disarm_Disable"},
                {"SFXGameContentDLC_CON_MP3.SFXObjective_MobileAnnex", "SFXMobileAnnex"},
                {"SFXOnlineFoundation.SFXOnlineComponentAchievementPC", ""}, //totes new
                {"SFXGameContentDLC_CON_MP2.SFXPawn_PlayerMP_Sentinel_Vorcha", "Vorcha_Sentinel_MP"},
                {"SFXGameContentDLC_CON_MP2.SFXPawn_PlayerMP_Soldier_Vorcha", "Vorcha_Soldier_MP"},
                {"SFXGameContent.SFXPawn_GethPrimeShieldDrone", "SFXPawn_gethPrime"},
                {"SFXGameContent.SFXPawn_GethPrimeTurret", "SFXPawn_GethPrime"},
                {"SFXGameContent.SFXPawn_GunnerTurret", "SFXPawn_Gunner"},
                {"SFXGameMPContent.SFXPawn_Krogan_MP", "Krogan_Soldier_MP"},
                {"SFXGameContentDLC_CON_MP3.SFXPawn_PlayerMP_Sentinel_N7", "N7_Sentinel_MP"},
                {"SFXGameContent.SFXPawn_Swarmer", "SFXPawn_Ravager"},
                {"SFXGameContentDLC_CON_MP2.SFXPowerCustomActionMP_Damping", ""},
                {"SFXGameContent.SFXPowerCustomAction_AIHacking", ""},
                {"SFXGameContentDLC_CON_MP2.SFXPowerCustomActionMP_Flamer", ""},
                {"SFXGameMPContent.SFXPowerCustomActionMP_Reave", ""},
                {"SFXGameContentDLC_CON_MP3.SFXPowerCustomActionMP_Slash", ""},
                {"SFXGameContent.SFXPowerCustomAction_Carnage", "SFXPower_Carnage"},
                {"SFXGameContent.SFXPowerCustomAction_Marksman", "SFXPower_Marksman"},
                {"SFXGameContent.SFXPowerCustomAction_Reave", "SFXPower_Reave"},
                {"SFXGameContent.SFXPowerCustomAction_Stasis", "SFXPower_Stasis"},
                {"SFXGameContent.SFXProjectile_BansheePhase", "SFXPawn_Banshee"},
                {"SFXGameContentDLC_CON_MP1.SFXPawn_PlayerMP_Sentinel_Batarian", "Batarian_Sentinel_MP"},
                {"SFXGameContentDLC_CON_MP1.SFXPawn_PlayerMP_Soldier_Batarian", "Batarian_Soldier_MP"},
                {"SFXGameContent.SFXPowerCustomAction_AdrenalineRush", "SFXPower_AdrenalineRush"},
                {"SFXGameContent.SFXPowerCustomAction_DefensiveShield", ""},
                {"SFXGameContent.SFXPowerCustomAction_Fortification", ""},
                {
                    "SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_AsariCommandoPassive",
                    "SFXPower_AsariCommandoPassive"
                },
                {"SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_BatarianAttack", "SFXPower_BatarianAttack"},
                {"SFXGameMPContent.SFXPowerCustomActionMP_BioticCharge", ""},
                {"SFXGameMPContent.SFXPowerCustomActionMP_ConcussiveShot", ""},
                {"SFXGameMPContent.SFXPowerCustomActionMP_Marksman", ""},
                {"SFXGameContentDLC_CON_MP3.SFXPowerCustomActionMP_ShadowStrike", ""},
                {"SFXGameMPContent.SFXPowerCustomActionMP_Singularity", ""},
                {"SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_BatarianPassive", "SFXPower_BatarianPassive"},
                {"SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_GethPassive", "SFXPower_GethPassive"},
                {"SFXGameContent.SFXPowerCustomAction_Singularity", "SFXPower_Singularity"},
                {"SFXGameContent.SFXPowerCustomAction_Incinerate", "SFXPower_Incinerate"},
                {"SFXGameContent.SFXSeqAct_OpenWeaponSelection", "BioP_Cat002"},
                {"SFXGameContent.SFXSeqAct_ClearParticlePools", "BioD_KroGar_500Gate"},
                {"SFXGameContentLiveKismet.SFXSeqAct_SetAreaMap", "BioD_Cithub_Dock"},
                {"SFXGameContent.SFXShield_EVA", "Biod_promar_710chase"},
                {"SFXGameContent.SFXShield_Phantom", "SFXPawn_Phantom"},
                {"SFXGameContentDLC_CON_MP2.SFXWeapon_Shotgun_Quarian", "SFXWeapon_Shotgun_QuarianDLC"},
                {"SFXGameContentDLC_CON_MP2.SFXWeapon_SniperRifle_Turian", "SFXWeapon_SniperRifle_TurianDLC"},
                {
                    "SFXGameContentDLC_CON_GUN01.SFXWeapon_SniperRifle_Turian_GUN01",
                    "SFXWeapon_SniperRifle_Turian_GUN01"
                },
                {"SFXGameContentDLC_CON_MP1.SFXWeapon_Heavy_FlameThrower_GethTurret", "SFXPower_GethSentryTurret"}
            };
            var gameFiles = MELoadedFiles.GetFilesLoadedInGame(MEGame.ME3);
            List<string> outs = new List<string>();

            foreach (var pf in patchFiles)
            {
                using var package = MEPackageHandler.OpenMEPackage(pf);
                var classExp = package.Exports.FirstOrDefault(x => x.ClassName == "Class");
                if (classExp != null)
                {
                    // attempt to find base class?
                    // use resolver code so just fake an import
                    var ie = new ImportEntry(classExp.FileRef, classExp.idxLink, classExp.ObjectName)
                    {
                        ClassName = classExp.ClassName,
                        PackageFile = classExp.ParentName,
                    };
                    Debug.WriteLine("Looking up patch source " + classExp.InstancedFullPath);
                    ExportEntry matchingExport = null;
                    if (extraMappings.TryGetValue(classExp.FullPath, out var lookAtFname) &&
                        gameFiles.TryGetValue(lookAtFname + ".pcc", out var fullpath))
                    {
                        using var newP = MEPackageHandler.OpenMEPackage(fullpath);
                        var lookupCE = newP.Exports.FirstOrDefault(x => x.FullPath == classExp.FullPath);
                        if (lookupCE != null)
                        {
                            matchingExport = lookupCE;
                        }
                    }
                    else if (gameFiles.TryGetValue(
                        classExp.ObjectName.Name.Replace("SFXPowerCustomAction", "SFXPower") + ".pcc",
                        out var fullpath2))
                    {
                        using var newP = MEPackageHandler.OpenMEPackage(fullpath2);
                        // sfxgame.sfxgame is special case
                        if (classExp.ObjectName == "SFXGame")
                        {
                            var lookupCE = newP.Exports.FirstOrDefault(x => x.FullPath == "SFXGame");
                            if (lookupCE != null)
                            {
                                matchingExport = lookupCE;
                            }
                        }
                        else
                        {
                            var lookupCE = newP.Exports.FirstOrDefault(x => x.FullPath == classExp.FullPath);
                            if (lookupCE != null)
                            {
                                matchingExport = lookupCE;
                            }
                        }
                    }
                    else if (gameFiles.TryGetValue(
                        classExp.ObjectName.Name.Replace("SFXPowerCustomActionMP", "SFXPower") + ".pcc",
                        out var fullpath3))
                    {
                        using var newP = MEPackageHandler.OpenMEPackage(fullpath3);
                        var lookupCE = newP.Exports.FirstOrDefault(x => x.FullPath == classExp.FullPath);
                        if (lookupCE != null)
                        {
                            matchingExport = lookupCE;
                        }
                    }
                    else
                    {
                        matchingExport = EntryImporter.ResolveImport(ie);

                        if (matchingExport == null)
                        {
                            outs.Add(classExp.InstancedFullPath);
                        }
                    }


                    if (matchingExport != null)
                    {
                        //outs.Add(" >> Found original definition: " + matchingExport.ObjectName + " in " +
                        //                matchingExport.FileRef.FilePath);

                        var childrenFuncs = matchingExport.FileRef.Exports.Where(x =>
                            x.idxLink == matchingExport.UIndex && x.ClassName == "Function");
                        foreach (var v in childrenFuncs)
                        {
                            var localFunc = package.Exports.FirstOrDefault(x => x.FullPath == v.FullPath);
                            if (localFunc != null)
                            {
                                // Decomp original func
                                Function func3 = new Function(v.Data, v);
                                func3.ParseFunction();
                                StringBuilder stringoutput = new StringBuilder();
                                stringoutput.AppendLine(func3.GetSignature());
                                foreach (var t in func3.ScriptBlocks)
                                {
                                    stringoutput.AppendLine(t.text);
                                }

                                string originalFunc = stringoutput.ToString();

                                func3 = new Function(localFunc.Data, localFunc);
                                func3.ParseFunction();
                                stringoutput = new StringBuilder();
                                stringoutput.AppendLine(func3.GetSignature());
                                foreach (var t in func3.ScriptBlocks)
                                {
                                    stringoutput.AppendLine(t.text);
                                }

                                string newFunc = stringoutput.ToString();

                                if (newFunc != originalFunc)
                                {
                                    // put into files for winmerge to look at.
                                    var outname =
                                        $"{localFunc.FullPath} {Path.GetFileName(pf)}_{localFunc.UIndex}__{Path.GetFileName(v.FileRef.FilePath)}_{v.UIndex}.txt";
                                    File.WriteAllText(Path.Combine(origOutDir, outname), originalFunc);
                                    File.WriteAllText(Path.Combine(patchedOutDir, outname), newFunc);
                                    Debug.WriteLine("   ============= DIFFERENCE " + localFunc.FullPath);
                                }
                            }
                        }


                    }
                    else
                    {
                        outs.Add(" XX Could not find " + classExp.ObjectName);
                    }
                }
            }

            foreach (var o in outs)
            {
                Debug.WriteLine(o);
            }

            //Restore path.
            ME3Directory.DefaultGamePath = oldPath;
        }

        /// <summary>
        /// Rebuilds all netindexes based on the AdditionalPackageToCook list in the listed file's header
        /// </summary>
        public static void RebuildFullLevelNetindexes()
        {
            string pccPath = @"X:\SteamLibrary\steamapps\common\Mass Effect 3\BIOGame\CookedPCConsole\BioP_MPTowr.pcc";
            //string pccPath = @"X:\m3modlibrary\ME3\Redemption\DLC_MOD_MPMapPack - NetIndexing\CookedPCConsole\BioP_MPCron2.pcc";
            string[] subFiles =
            {
                "BioA_Cat004_000Global",
                "BioA_Cat004_100HangarBay",
                "BioD_Cat004_050Landing",
                "BioD_Cat004_100HangarBay",
                "BioD_MPCron_SubMaster",
                "BioSnd_MPCron"

            };
            Dictionary<int, List<string>> indices = new Dictionary<int, List<string>>();
            using var package = (MEPackage)MEPackageHandler.OpenMEPackage(pccPath);
            //package.AdditionalPackagesToCook = subFiles.ToList();
            //package.Save();
            //return;
            int currentNetIndex = 1;

            var netIndexedObjects = package.Exports.Where(x => x.NetIndex >= 0).OrderBy(x => x.NetIndex).ToList();

            foreach (var v in netIndexedObjects)
            {
                List<string> usages = null;
                if (!indices.TryGetValue(v.NetIndex, out usages))
                {
                    usages = new List<string>();
                    indices[v.NetIndex] = usages;
                }

                usages.Add($"{Path.GetFileNameWithoutExtension(v.FileRef.FilePath)} {v.InstancedFullPath}");
            }

            foreach (var f in package.AdditionalPackagesToCook)
            {
                var packPath = Path.Combine(Path.GetDirectoryName(pccPath), f + ".pcc");
                using var sPackage = (MEPackage)MEPackageHandler.OpenMEPackage(packPath);

                netIndexedObjects = sPackage.Exports.Where(x => x.NetIndex >= 0).OrderBy(x => x.NetIndex).ToList();
                foreach (var v in netIndexedObjects)
                {
                    List<string> usages = null;
                    if (!indices.TryGetValue(v.NetIndex, out usages))
                    {
                        usages = new List<string>();
                        indices[v.NetIndex] = usages;
                    }

                    usages.Add($"{Path.GetFileNameWithoutExtension(v.FileRef.FilePath)} {v.InstancedFullPath}");
                }
            }

            foreach (var i in indices)
            {
                Debug.WriteLine($"NetIndex {i.Key}");
                foreach (var s in i.Value)
                {
                    Debug.WriteLine("   " + s);
                }
            }
        }

        public static void ShiftInterpTrackMovesInPackage(IMEPackage package, Func<ExportEntry, bool> predicate)
        {
            var offsetX = int.Parse(PromptDialog.Prompt(null, "Enter X shift offset", "Offset X", "0", true));
            var offsetY = int.Parse(PromptDialog.Prompt(null, "Enter Y shift offset", "Offset Y", "0", true));
            var offsetZ = int.Parse(PromptDialog.Prompt(null, "Enter Z shift offset", "Offset Z", "0", true));
            foreach (var exp in package.Exports.Where(x => x.ClassName == "InterpTrackMove"))
            {
                if (predicate == null || predicate.Invoke(exp))
                {
                    ShiftInterpTrackMove(exp, offsetX, offsetY, offsetZ);
                }
            }
        }

        public static void ShiftInterpTrackMove(ExportEntry interpTrackMove, int? offsetX = null, int? offsetY = null, int? offsetZ = null)
        {
            offsetX ??= int.Parse(PromptDialog.Prompt(null, "Enter X shift offset", "Offset X", "0", true));
            offsetY ??= int.Parse(PromptDialog.Prompt(null, "Enter Y shift offset", "Offset Y", "0", true));
            offsetZ ??= int.Parse(PromptDialog.Prompt(null, "Enter Z shift offset", "Offset Z", "0", true));

            var props = interpTrackMove.GetProperties();
            var posTrack = props.GetProp<StructProperty>("PosTrack");
            var points = posTrack.GetProp<ArrayProperty<StructProperty>>("Points");
            foreach (var point in points)
            {
                var outval = point.GetProp<StructProperty>("OutVal");
                outval.GetProp<FloatProperty>("X").Value += offsetX.Value;
                outval.GetProp<FloatProperty>("Y").Value += offsetY.Value;
                outval.GetProp<FloatProperty>("Z").Value += offsetZ.Value;
            }

            interpTrackMove.WriteProperties(props);
        }

        /// <summary>
        /// Shifts an ME1 AnimCutscene by specified X Y Z values. Only supports 96NoW (3 32-bit float) animations
        /// By Mgamerz 
        /// </summary>
        /// <param name="export"></param>
        public static void ShiftME1AnimCutscene(ExportEntry export)
        {
            if (ObjectBinary.From(export) is AnimSequence animSeq)
            {
                var offsetX = int.Parse(PromptDialog.Prompt(null, "Enter X offset", "Offset X", "0", true));
                var offsetY = int.Parse(PromptDialog.Prompt(null, "Enter Y offset", "Offset Y", "0", true));
                var offsetZ = int.Parse(PromptDialog.Prompt(null, "Enter Z offset", "Offset Z", "0", true));
                var offsetVec = new Vector3(offsetX, offsetY, offsetZ);

                animSeq.DecompressAnimationData();
                foreach (AnimTrack track in animSeq.RawAnimationData)
                {
                    for (int i = 0; i < track.Positions.Count; i++)
                    {
                        track.Positions[i] = Vector3.Add(track.Positions[i], offsetVec);
                    }
                }

                PropertyCollection props = export.GetProperties();
                animSeq.UpdateProps(props, export.Game);
                export.WritePropertiesAndBinary(props, animSeq);
            }
        }

        public static void DumpAllExecFunctionsFromGame()
        {
            Dictionary<string, string> exportNameSignatureMapping = new Dictionary<string, string>();
            string gameDir = @"Z:\ME3-Backup\BioGame";

            var packages = Directory.GetFiles(gameDir, "*.pcc", SearchOption.AllDirectories);
            var sfars = Directory.GetFiles(gameDir + "\\DLC", "Default.sfar", SearchOption.AllDirectories).ToList();
            sfars.Insert(0, gameDir + "\\Patches\\PCConsole\\Patch_001.sfar");
            foreach (var sfar in sfars)
            {
                Debug.WriteLine("Loading " + sfar);
                DLCPackage dlc = new DLCPackage(sfar);
                foreach (var f in dlc.Files)
                {
                    if (f.isActualFile && Path.GetExtension(f.FileName) == ".pcc")
                    {
                        Debug.WriteLine(" >> Reading " + f.FileName);
                        var packageStream = dlc.DecompressEntry(f);
                        packageStream.Position = 0;
                        var package = MEPackageHandler.OpenMEPackageFromStream(packageStream, Path.GetFileName(f.FileName));
                        package.IsMemoryPackage = true;
                        foreach (var exp in package.Exports.Where(x => x.ClassName == "Function"))
                        {
                            Function func = new Function(exp.Data, exp);
                            if (func.HasFlag("Exec") && !exportNameSignatureMapping.ContainsKey(exp.FullPath))
                            {
                                func.ParseFunction();
                                StringWriter sw = new StringWriter();
                                sw.WriteLine(func.GetSignature());
                                foreach (var v in func.ScriptBlocks)
                                {
                                    sw.WriteLine($"(MemPos 0x{v.memPosStr}) {v.text}");
                                }

                                exportNameSignatureMapping[exp.FullPath] = sw.ToString();
                            }
                        }
                    }
                }
            }

            foreach (var file in packages)
            {
                Debug.WriteLine(" >> Reading " + file);
                using var package = MEPackageHandler.OpenMEPackage(file);
                foreach (var exp in package.Exports.Where(x => x.ClassName == "Function"))
                {
                    Function func = new Function(exp.Data, exp);
                    if (func.HasFlag("Exec") && !exportNameSignatureMapping.ContainsKey(exp.FullPath))
                    {
                        func.ParseFunction();
                        StringWriter sw = new StringWriter();
                        sw.WriteLine(func.GetSignature());
                        foreach (var v in func.ScriptBlocks)
                        {
                            sw.WriteLine($"(MemPos 0x{v.memPosStr}) {v.text}");
                        }

                        exportNameSignatureMapping[exp.FullPath] = sw.ToString();
                    }
                }
            }

            var lines = exportNameSignatureMapping.Select(x =>
                $"{x.Key}============================================================\n{x.Value}");
            File.WriteAllLines(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "fullfunctionsignatures.txt"), lines);
        }


        /// <summary>
        /// Extracts all NoramlizedAverateColors, tints them, and then reinstalls them to the export they came from
        /// </summary>
        /// <param name="Pcc"></param>
        public static void TintAllNormalizedAverageColors(IMEPackage Pcc)
        {
            MessageBox.Show("This is not implemented, code must be uncommented out");
            //var normalizedExports = Pcc.Exports
            //    .Where(x => x.ClassName == "LightMapTexture2D" && x.ObjectName.Name.StartsWith("NormalizedAverageColor")).ToList();
            //foreach (var v in normalizedExports)
            //{
            //    MemoryStream pngImage = new MemoryStream();
            //    Texture2D t2d = new Texture2D(v);
            //    t2d.ExportToPNG(outStream: pngImage);
            //    pngImage.Position = 0; //reset
            //    MemoryStream outStream = new MemoryStream();
            //    using (var image = new MagickImage(pngImage))
            //    {

            //        var tintColor = MagickColor.FromRgb((byte)128, (byte)0, (byte)0);
            //        //image.Colorize(tintColor, new Percentage(80), new Percentage(5), new Percentage(5) );
            //        //image.Settings.FillColor = tintColor;
            //        //image.Tint("30%", tintColor);
            //        image.Modulate(new Percentage(82), new Percentage(100), new Percentage(0));
            //        //image.Colorize(tintColor, new Percentage(100), new Percentage(0), new Percentage(0) );
            //        image.Write(outStream, MagickFormat.Png32);
            //    }
            //    //outStream = pngImage;
            //    outStream.Position = 0;
            //    outStream.WriteToFile(Path.Combine(Directory.CreateDirectory(@"C:\users\mgame\desktop\normalizedCols").FullName, v.ObjectName.Instanced + ".png"));
            //    var convertedBackImage = new MassEffectModder.Images.Image(outStream, Image.ImageFormat.PNG);
            //    t2d.Replace(convertedBackImage, t2d.Export.GetProperties());
            //}
        }

        /// <summary>
        /// Traverses the Level object's navigation point start to its end and finds which objecst are not in the NavList of the Level
        /// By Mgamerz
        /// </summary>
        /// <param name="pcc"></param>
        public static void ValidateNavpointChain(IMEPackage pcc)
        {
            var pl = pcc.Exports.FirstOrDefault(x => x.ClassName == "Level" && x.ObjectName == "PersistentLevel");
            if (pl != null)
            {
                var persistentLevel = ObjectBinary.From<Level>(pl);
                var nlSU = persistentLevel.NavListStart;
                var nlS = pcc.GetUExport(nlSU);
                var navList = new List<ExportEntry>();
                var itemsMissingFromWorldNPC = new List<ExportEntry>();
                if (persistentLevel.NavRefs.All(x => x != nlS.UIndex))
                {
                    itemsMissingFromWorldNPC.Add(nlS);
                }

                var nnP = nlS.GetProperty<ObjectProperty>("nextNavigationPoint");
                navList.Add(nlS);
                Debug.WriteLine($"{nlS.UIndex} {nlS.InstancedFullPath}");
                while (nnP != null)
                {
                    var nextNavigationPoint = nnP.ResolveToEntry(pcc) as ExportEntry;
                    Debug.WriteLine($"{nextNavigationPoint.UIndex} {nextNavigationPoint.InstancedFullPath}");
                    if (persistentLevel.NavRefs.All(x => x != nextNavigationPoint.UIndex))
                    {
                        itemsMissingFromWorldNPC.Add(nextNavigationPoint);
                    }

                    navList.Add(nextNavigationPoint);
                    nnP = nextNavigationPoint.GetProperty<ObjectProperty>("nextNavigationPoint");
                }

                Debug.WriteLine($"{navList.Count} items in actual nav chain");
                foreach (var v in itemsMissingFromWorldNPC)
                {
                    Debug.WriteLine($"Item missing from NavPoints list: {v.UIndex} {v.InstancedFullPath}");
                }
            }
        }

        public static void SetAllWwiseEventDurations(IMEPackage Pcc)
        {
            var wwevents = Pcc.Exports.Where(x => x.ClassName == "WwiseEvent").ToList();
            foreach (var wwevent in wwevents)
            {
                var eventbin = wwevent.GetBinaryData<WwiseEvent>();
                if (!eventbin.Links.IsEmpty() && !eventbin.Links[0].WwiseStreams.IsEmpty())
                {
                    var wwstream = Pcc.GetUExport(eventbin.Links[0].WwiseStreams[0]);
                    if (eventbin.Links[0].WwiseStreams.Count > 1 && wwevent.ObjectNameString.Length == 16)  //must be standard VO_123456_m_Play wwiseevent name format
                    {
                        var tlkref = wwevent.ObjectNameString.Remove(9).Remove(0, 3);
                        var genderref = wwevent.ObjectNameString.ToLower().Remove(11).Remove(0, 10);
                        foreach (var stream in eventbin.Links[0].WwiseStreams)
                        {
                            var potentialStream = Pcc.GetUExport(stream);
                            if (potentialStream.ObjectNameString.Contains(tlkref))
                            {
                                if (potentialStream.ObjectNameString.ToLower().Contains("player"))
                                {
                                    if (!potentialStream.ObjectNameString.ToLower()
                                                                         .Contains("_" + genderref + "_"))
                                        continue;
                                }
                                wwstream = potentialStream;
                                break;
                            }
                        }
                    }
                    var streambin = wwstream?.GetBinaryData<WwiseStream>() ?? null;
                    if (streambin != null)
                    {
                        var duration = streambin.GetAudioInfo()?.GetLength();
                        switch (Pcc.Game)
                        {
                            case MEGame.ME3:
                                var durtnMS = wwevent.GetProperty<FloatProperty>("DurationMilliseconds");
                                if (durtnMS != null && duration != null)
                                {
                                    durtnMS.Value = (float)duration.Value.TotalMilliseconds;
                                    wwevent.WriteProperty(durtnMS);
                                }
                                break;
                            case MEGame.LE3:
                                var durtnSec = wwevent.GetProperty<FloatProperty>("DurationSeconds");
                                if (durtnSec != null && duration != null)
                                {
                                    durtnSec.Value = (float)duration.Value.TotalSeconds;
                                    wwevent.WriteProperty(durtnSec);
                                }
                                break;
                        }
                    }
                }
            }
        }

        public static void PrintAllNativeFuncsToDebug(IMEPackage package)
        {
            var newCachedInfo = new SortedDictionary<int, CachedNativeFunctionInfo>();
            foreach (ExportEntry export in package.Exports)
            {
                if (export.ClassName == "Function")
                {

                    BinaryReader reader = new EndianReader(new MemoryStream(export.Data)) { Endian = package.Endian };
                    reader.ReadBytes(12); // skip props
                    int super = reader.ReadInt32();
                    int nextItemInCompChain = reader.ReadInt32();
                    int childProbe = reader.ReadInt32();
                    if (package.Game is MEGame.ME1 or MEGame.ME2)
                    {
                        reader.ReadBytes(8); // some name
                        int line = reader.ReadInt32();
                        int textPos = reader.ReadInt32();
                    }
                    else
                    {
                        reader.ReadInt32(); // memorySize
                    }

                    int scriptSize = reader.ReadInt32();
                    byte[] bytecode = reader.ReadBytes(scriptSize);
                    int nativeIndex = reader.ReadInt16();
                    if (package.Game is MEGame.ME1 or MEGame.ME2)
                    {
                        int operatorPrecedence = reader.ReadByte();
                    }

                    int functionFlags = reader.ReadInt32();
                    if ((functionFlags & UE3FunctionReader._flagSet.GetMask("Net")) != 0)
                    {
                        reader.ReadInt16(); // repOffset
                    }

                    if (package.Game is MEGame.ME1 or MEGame.ME2)
                    {
                        int friendlyNameIndex = reader.ReadInt32();
                        reader.ReadInt32();
                    }

                    var function = new UnFunction(export, export.ObjectName,
                        new FlagValues(functionFlags, UE3FunctionReader._flagSet), bytecode, nativeIndex,
                        1); // USES PRESET 1 DO NOT TRUST

                    if (nativeIndex != 0 /*&& CachedNativeFunctionInfo.GetNativeFunction(nativeIndex) == null*/)
                    {
                        Debug.WriteLine($">>NATIVE Function {nativeIndex} {export.ObjectName}");
                        var newInfo = new CachedNativeFunctionInfo
                        {
                            nativeIndex = nativeIndex,
                            Name = export.ObjectName,
                            Filename = Path.GetFileName(package.FilePath),
                            Operator = function.Operator,
                            PreOperator = function.PreOperator,
                            PostOperator = function.PostOperator
                        };
                        newCachedInfo[nativeIndex] = newInfo;
                    }
                }
            }
            //Debug.WriteLine(JsonConvert.SerializeObject(new { NativeFunctionInfo = newCachedInfo }, Formatting.Indented));

            //Dictionary<int, string> nativeMap = new Dictionary<int, string>();
            //foreach (var ee in package.Exports.Where(x => x.ClassName == "Function"))
            //{
            //    int nativeIndex = 0;
            //    var data = ee.Data;
            //    var offset = data.Length - (package.Game == MEGame.ME3 || package.Platform == MEPackage.GamePlatform.PS3 ? 4 : 12);
            //    if (package.Platform == MEPackage.GamePlatform.Xenon && package.Game == MEGame.ME1)
            //    {
            //        if (ee.ObjectName.Name == "ClientWeaponSet")
            //            Debugger.Break();
            //        // It's byte aligned. We have to read front to back
            //        int scriptSize = EndianReader.ToInt32(data, 0x28, ee.FileRef.Endian);
            //        nativeIndex = EndianReader.ToInt16(data, scriptSize + 0x2C, ee.FileRef.Endian);
            //        if (nativeIndex == 0) nativeIndex = -1;
            //    }
            //    var flags = nativeIndex == 0 ? EndianReader.ToInt32(data, offset, ee.FileRef.Endian) : 0; // if we calced it don't use it's value
            //    FlagValues fs = new FlagValues(flags, UE3FunctionReader._flagSet);
            //    if (nativeIndex >= 0 || fs.HasFlag("Native"))
            //    {
            //        if (nativeIndex == 0)
            //        {
            //            var nativeBackOffset = ee.FileRef.Game == MEGame.ME3 ? 6 : 7;
            //            if (ee.Game < MEGame.ME3 && ee.FileRef.Platform != MEPackage.GamePlatform.PS3) nativeBackOffset = 0xF;
            //            nativeIndex = EndianReader.ToInt16(data, data.Length - nativeBackOffset, ee.FileRef.Endian);
            //        }
            //        if (nativeIndex > 0)
            //        {
            //            nativeMap[nativeIndex] = ee.ObjectName;
            //        }
            //    }
            //}

            //var natives = nativeMap.OrderBy(x => x.Key).Select(x => $"NATIVE_{x.Value} = 0x{x.Key:X2}");
            //foreach (var n in nativeMap)
            //{
            //    var function = CachedNativeFunctionInfo.GetNativeFunction(n.Key); //have to figure out how to do this, it's looking up name of native function
            //    if (function == null)
            //    {
            //        Debug.WriteLine($"NATIVE_{n.Value} = 0x{n.Key:X2}");
            //    }
            //}
        }

        public static void BuildME1NativeFunctionsInfo()
        {
            if (ME1Directory.DefaultGamePath != null)
            {
                var newCachedInfo = new SortedDictionary<int, CachedNativeFunctionInfo>();
                var dir = new DirectoryInfo(ME1Directory.DefaultGamePath);
                var filesToSearch = dir.GetFiles( /*"*.sfm", SearchOption.AllDirectories).Union(dir.GetFiles(*/"*.u",
                    SearchOption.AllDirectories).ToArray();
                Debug.WriteLine("Number of files: " + filesToSearch.Length);
                foreach (FileInfo fi in filesToSearch)
                {
                    using (var package = MEPackageHandler.OpenME1Package(fi.FullName))
                    {
                        Debug.WriteLine(fi.Name);
                        foreach (ExportEntry export in package.Exports)
                        {
                            if (export.ClassName == "Function")
                            {

                                BinaryReader reader = new BinaryReader(new MemoryStream(export.Data));
                                reader.ReadBytes(12);
                                int super = reader.ReadInt32();
                                int children = reader.ReadInt32();
                                reader.ReadBytes(12);
                                int line = reader.ReadInt32();
                                int textPos = reader.ReadInt32();
                                int scriptSize = reader.ReadInt32();
                                byte[] bytecode = reader.ReadBytes(scriptSize);
                                int nativeIndex = reader.ReadInt16();
                                int operatorPrecedence = reader.ReadByte();
                                int functionFlags = reader.ReadInt32();
                                if ((functionFlags & UE3FunctionReader._flagSet.GetMask("Net")) != 0)
                                {
                                    reader.ReadInt16(); // repOffset
                                }

                                int friendlyNameIndex = reader.ReadInt32();
                                reader.ReadInt32();
                                var function = new UnFunction(export, package.GetNameEntry(friendlyNameIndex),
                                    new FlagValues(functionFlags, UE3FunctionReader._flagSet), bytecode, nativeIndex,
                                    operatorPrecedence);

                                if (nativeIndex != 0 && CachedNativeFunctionInfo.GetNativeFunction(nativeIndex) == null)
                                {
                                    Debug.WriteLine($">>NATIVE Function {nativeIndex} {export.ObjectName}");
                                    var newInfo = new CachedNativeFunctionInfo
                                    {
                                        nativeIndex = nativeIndex,
                                        Name = export.ObjectName,
                                        Filename = fi.Name,
                                        Operator = function.Operator,
                                        PreOperator = function.PreOperator,
                                        PostOperator = function.PostOperator
                                    };
                                    newCachedInfo[nativeIndex] = newInfo;
                                }
                            }
                        }
                    }
                }

                Debug.WriteLine(JsonConvert.SerializeObject(new { NativeFunctionInfo = newCachedInfo },
                    Formatting.Indented));

                //File.WriteAllText(Path.Combine(App.ExecFolder, "ME1NativeFunctionInfo.json"),
                //    JsonConvert.SerializeObject(new { NativeFunctionInfo = newCachedInfo }, Formatting.Indented));
                Debug.WriteLine("Done");
            }
        }

        public static void FindME1ME22DATables()
        {
            if (ME1Directory.DefaultGamePath != null)
            {
                var newCachedInfo = new SortedDictionary<int, CachedNativeFunctionInfo>();
                var dir = new DirectoryInfo(
                    Path.Combine(ME1Directory.DefaultGamePath /*, "BioGame", "CookedPC", "Maps"*/));
                var filesToSearch = dir.GetFiles("*.sfm", SearchOption.AllDirectories)
                    .Union(dir.GetFiles("*.u", SearchOption.AllDirectories))
                    .Union(dir.GetFiles("*.upk", SearchOption.AllDirectories)).ToArray();
                Debug.WriteLine("Number of files: " + filesToSearch.Length);
                foreach (FileInfo fi in filesToSearch)
                {
                    using (var package = MEPackageHandler.OpenME1Package(fi.FullName))
                    {
                        foreach (ExportEntry export in package.Exports)
                        {
                            if ((export.ClassName == "BioSWF"))
                            //|| export.ClassName == "Bio2DANumberedRows") && export.ObjectName.Contains("BOS"))
                            {
                                Debug.WriteLine(
                                    $"{export.ClassName}({export.ObjectName.Instanced}) in {fi.Name} at export {export.UIndex}");
                            }
                        }
                    }
                }

                //File.WriteAllText(System.Windows.Forms.Application.StartupPath + "//exec//ME1NativeFunctionInfo.json", JsonConvert.SerializeObject(new { NativeFunctionInfo = newCachedInfo }, Formatting.Indented));
                Debug.WriteLine("Done");
            }
        }

        public static void FindAllME3PowerCustomActions()
        {
            if (ME3Directory.DefaultGamePath != null)
            {
                var newCachedInfo = new SortedDictionary<string, List<string>>();
                var dir = new DirectoryInfo(ME3Directory.DefaultGamePath);
                var filesToSearch = dir.GetFiles("*.pcc", SearchOption.AllDirectories).ToArray();
                Debug.WriteLine("Number of files: " + filesToSearch.Length);
                foreach (FileInfo fi in filesToSearch)
                {
                    using (var package = MEPackageHandler.OpenME3Package(fi.FullName))
                    {
                        foreach (ExportEntry export in package.Exports)
                        {
                            if (export.SuperClassName == "SFXPowerCustomAction")
                            {
                                Debug.WriteLine(
                                    $"{export.ClassName}({export.ObjectName}) in {fi.Name} at export {export.UIndex}");
                                if (newCachedInfo.TryGetValue(export.ObjectName, out List<string> instances))
                                {
                                    instances.Add($"{fi.Name} at export {export.UIndex}");
                                }
                                else
                                {
                                    newCachedInfo[export.ObjectName] = new List<string>
                                        {$"{fi.Name} at export {export.UIndex}"};
                                }
                            }
                        }
                    }
                }


                string outstr = "";
                foreach (KeyValuePair<string, List<string>> instancelist in newCachedInfo)
                {
                    outstr += instancelist.Key;
                    outstr += "\n";
                    foreach (string str in instancelist.Value)
                    {
                        outstr += " - " + str + "\n";
                    }
                }

                File.WriteAllText(@"C:\users\public\me3powers.txt", outstr);
                Debug.WriteLine("Done");
            }
        }

        public static void FindAllME2Powers()
        {
            if (ME2Directory.DefaultGamePath != null)
            {
                var newCachedInfo = new SortedDictionary<string, List<string>>();
                var dir = new DirectoryInfo(ME2Directory.DefaultGamePath);
                var filesToSearch = dir.GetFiles("*.pcc", SearchOption.AllDirectories).ToArray();
                Debug.WriteLine("Number of files: " + filesToSearch.Length);
                foreach (FileInfo fi in filesToSearch)
                {
                    using var package = MEPackageHandler.OpenMEPackage(fi.FullName);
                    foreach (ExportEntry export in package.Exports)
                    {
                        if (export.SuperClassName == "SFXPower")
                        {
                            Debug.WriteLine(
                                $"{export.ClassName}({export.ObjectName}) in {fi.Name} at export {export.UIndex}");
                            if (newCachedInfo.TryGetValue(export.ObjectName, out List<string> instances))
                            {
                                instances.Add($"{fi.Name} at export {export.UIndex}");
                            }
                            else
                            {
                                newCachedInfo[export.ObjectName] = new List<string>
                                    {$"{fi.Name} at export {export.UIndex}"};
                            }
                        }
                    }
                }


                string outstr = "";
                foreach (KeyValuePair<string, List<string>> instancelist in newCachedInfo)
                {
                    outstr += instancelist.Key;
                    outstr += "\n";
                    foreach (string str in instancelist.Value)
                    {
                        outstr += " - " + str + "\n";
                    }
                }

                File.WriteAllText(@"C:\users\public\me2powers.txt", outstr);
                Debug.WriteLine("Done");
            }
        }

        /// <summary>
        /// Asset Database doesn't search by memory entry, so if I'm looking to see if another entry exists I can't find it. For example I'm trying to find all copies of a specific FaceFX anim set.
        /// </summary>
        /// <param name="packageEditorWpf"></param>
        public static void FindNamedObject(PackageEditorWindow packageEditorWpf)
        {
            var namedObjToFind = PromptDialog.Prompt(packageEditorWpf,
                "Enter the name of the object you want to search for in files", "Object finder");
            if (!string.IsNullOrWhiteSpace(namedObjToFind))
            {
                var dlg = new CommonOpenFileDialog("Pick a folder to scan (includes subdirectories)")
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true
                };
                if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    packageEditorWpf.IsBusy = true;
                    Task.Run(() =>
                    {
                        ConcurrentDictionary<string, string>
                            threadSafeList = new ConcurrentDictionary<string, string>();
                        packageEditorWpf.BusyText = "Getting list of all package files";
                        int numPackageFiles = 0;
                        var files = Directory.GetFiles(dlg.FileName, "*.pcc", SearchOption.AllDirectories)
                            .Where(x => x.RepresentsPackageFilePath()).ToList();
                        var totalfiles = files.Count;
                        long filesDone = 0;
                        Parallel.ForEach(files, pf =>
                        {
                            try
                            {
                                using var package = MEPackageHandler.OpenMEPackage(pf);
                                var hasObject = package.Exports.Any(x =>
                                    x.ObjectName.Name.Equals(namedObjToFind,
                                        StringComparison.InvariantCultureIgnoreCase));
                                if (hasObject)
                                {
                                    threadSafeList.TryAdd(pf, pf);
                                }
                            }
                            catch
                            {

                            }

                            long v = Interlocked.Increment(ref filesDone);
                            packageEditorWpf.BusyText = $"Scanning files [{v}/{totalfiles}]";
                        });
                        return threadSafeList;
                    }).ContinueWithOnUIThread(filesWithObjName =>
                    {
                        packageEditorWpf.IsBusy = false;
                        ListDialog ld = new ListDialog(filesWithObjName.Result.Select(x => x.Value), "Object name scan",
                            "Here is the list of files that have this objects of this name within them.",
                            packageEditorWpf);
                        ld.Show();
                    });
                }
            }
        }

        public static void CheckImports(IMEPackage Pcc, PackageCache globalCache = null)
        {
            if (Pcc == null) return;
            PackageCache pc = new PackageCache();
            // Enumerate and resolve all imports.
            foreach (var import in Pcc.Imports)
            {
                if (import.InstancedFullPath.StartsWith("Core."))
                    continue; // Most of these are native-native
                if (GlobalUnrealObjectInfo.IsAKnownNativeClass(import))
                    continue; // Native is always loaded iirc
                //Debug.WriteLine($@"Resolving {import.FullPath}");
                var export = EntryImporter.ResolveImport(import, globalCache, pc);
                if (export != null)
                {

                }
                else
                {
                    Debug.WriteLine($@" >>> UNRESOLVABLE IMPORT: {import.FullPath}!");
                }
            }

            pc.ReleasePackages();
        }

        public static void RandomizeTerrain(IMEPackage Pcc)
        {
            ExportEntry terrain = Pcc.Exports.FirstOrDefault(x => x.ClassName == "Terrain");
            if (terrain != null)
            {
                Random r = new Random();

                var terrainBin = terrain.GetBinaryData<Terrain>();
                for (int i = 0; i < terrainBin.Heights.Length; i++)
                {
                    terrainBin.Heights[i] = (ushort)(r.Next(2000) + 13000);
                }

                terrain.WriteBinary(terrainBin);
            }
        }

        public static void ResetPackageVanillaPart(IMEPackage sourcePackage, PackageEditorWindow pewpf)
        {
            if (sourcePackage.Game != MEGame.ME1 && sourcePackage.Game != MEGame.ME2 &&
                sourcePackage.Game != MEGame.ME3)
            {
                MessageBox.Show(pewpf, "Not a trilogy file!");
                return;
            }

            Task.Run(() =>
            {
                pewpf.BusyText = "Finding unmodded candidates...";
                pewpf.IsBusy = true;
                return SharedPackageTools.GetUnmoddedCandidatesForPackage(pewpf);
            }).ContinueWithOnUIThread(foundCandidates =>
            {
                pewpf.IsBusy = false;
                if (!foundCandidates.Result.Any()) MessageBox.Show(pewpf, "Cannot find any candidates for this file!");

                var choices = foundCandidates.Result.DiskFiles.ToList(); //make new list
                choices.AddRange(foundCandidates.Result.SFARPackageStreams.Select(x => x.Key));

                var choice = InputComboBoxDialog.GetValue(pewpf, "Choose file to reset to:", "Package reset", choices,
                    choices.Last());
                if (string.IsNullOrEmpty(choice))
                {
                    return;
                }

                var restorePackage = MEPackageHandler.OpenMEPackage(choice, forceLoadFromDisk: true);
                for (int i = 0; i < restorePackage.NameCount; i++)
                {
                    sourcePackage.replaceName(i, restorePackage.GetNameEntry(i));
                }

                foreach (var imp in sourcePackage.Imports)
                {
                    var origImp = restorePackage.FindImport(imp.InstancedFullPath);
                    if (origImp != null)
                    {
                        imp.SetHeaderValuesFromByteArray(origImp.GenerateHeader());
                    }
                }

                foreach (var exp in sourcePackage.Exports)
                {
                    var origExp = restorePackage.FindExport(exp.InstancedFullPath);
                    if (origExp != null)
                    {
                        exp.Data = origExp.Data;
                        exp.SetHeaderValuesFromByteArray(origExp.GenerateHeader());
                    }
                }
            });
        }

        public static void TestLODBias(PackageEditorWindow pew)
        {
            string[] extensions = { ".pcc" };
            FileInfo[] files = new DirectoryInfo(LE3Directory.CookedPCPath)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(f => f.Name.Contains("Cat002") && extensions.Contains(f.Extension.ToLower()))
                .ToArray();
            foreach (var f in files)
            {
                var p = MEPackageHandler.OpenMEPackage(f.FullName, forceLoadFromDisk: true);
                foreach (var tex in p.Exports.Where(x => x.ClassName == "Texture2D"))
                {
                    tex.WriteProperty(new IntProperty(-5, "InternalFormatLODBias"));
                }

                p.Save();
            }
        }

        public static void FindEmptyMips(PackageEditorWindow pew)
        {
            string[] extensions = { ".pcc" };
            FileInfo[] files = new DirectoryInfo(LE3Directory.CookedPCPath)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(f.Extension.ToLower()))
                .ToArray();
            foreach (var f in files)
            {
                var p = MEPackageHandler.OpenMEPackage(f.FullName, forceLoadFromDisk: true);
                foreach (var tex in p.Exports.Where(x => x.ClassName == "Texture2D"))
                {
                    var t = ObjectBinary.From<UTexture2D>(tex);
                    if (t.Mips[0].StorageType == StorageTypes.empty)
                        Debugger.Break();
                }
            }
        }

        public static void ListNetIndexes(PackageEditorWindow pew)
        {
            // Not sure this works
            var strs = new List<string>();
            var Pcc = pew.Pcc;
            foreach (ExportEntry exp in Pcc.Exports)
            {
                if (exp.ParentName == "PersistentLevel")
                {
                    strs.Add($"{exp.NetIndex} {exp.InstancedFullPath}");
                }
            }

            var d = new ListDialog(strs, "NetIndexes", "Here are the netindexes in Package Editor's loaded file", pew);
            d.Show();
        }

        public static void GenerateNewGUIDForFile(PackageEditorWindow pew)
        {
            MessageBox.Show(
                "Generate New GUID for file process applies immediately and cannot be undone.\nEnsure the file you are going to regenerate is not open in Legendary Explorer in any tools.\nBe absolutely sure you know what you're doing before you use GetPEWindow()!");
            OpenFileDialog d = AppDirectories.GetOpenPackageDialog();
            if (d.ShowDialog() == true)
            {
                using (IMEPackage sourceFile = MEPackageHandler.OpenMEPackage(d.FileName))
                {
                    string fname = Path.GetFileNameWithoutExtension(d.FileName);
                    Guid newGuid = Guid.NewGuid();
                    ExportEntry selfNamingExport = null;
                    foreach (ExportEntry exp in sourceFile.Exports)
                    {
                        if (exp.ClassName == "Package"
                            && exp.idxLink == 0
                            && string.Equals(exp.ObjectName.Name, fname, StringComparison.InvariantCultureIgnoreCase))
                        {
                            selfNamingExport = exp;
                            break;
                        }
                    }

                    if (selfNamingExport == null)
                    {
                        MessageBox.Show(
                            "Selected package does not contain a self-naming package export.\nCannot regenerate package file-level GUID if it doesn't contain self-named export.");
                        return;
                    }

                    selfNamingExport.PackageGUID = newGuid;
                    sourceFile.PackageGuid = newGuid;
                    sourceFile.Save();
                }

                MessageBox.Show("Generated a new GUID for package.");
            }
        }

        public static void GenerateGUIDCacheForFolder(PackageEditorWindow pew)
        {
            CommonOpenFileDialog m = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select folder to generate GUID cache on"
            };
            if (m.ShowDialog(pew) == CommonFileDialogResult.Ok)
            {
                string dir = m.FileName;
                string[] files = Directory.GetFiles(dir, "*.pcc");
                if (Enumerable.Any(files))
                {
                    var packageGuidMap = new Dictionary<string, Guid>();
                    var GuidPackageMap = new Dictionary<Guid, string>();

                    pew.IsBusy = true;
                    string guidcachefile = null;
                    foreach (string file in files)
                    {
                        string fname = Path.GetFileNameWithoutExtension(file);
                        if (fname.StartsWith("GuidCache"))
                        {
                            guidcachefile = file;
                            continue;
                        }

                        if (fname.Contains("_LOC_"))
                        {
                            Debug.WriteLine("--> Skipping " + fname);
                            continue; //skip localizations
                        }

                        Debug.WriteLine(Path.GetFileName(file));
                        bool hasPackageNamingItself = false;
                        using (var package = MEPackageHandler.OpenMEPackage(file))
                        {
                            var filesToSkip = new[]
                            {
                                "BioD_Cit004_270ShuttleBay1", "BioD_Cit003_600MechEvent", "CAT6_Executioner",
                                "SFXPawn_Demo", "SFXPawn_Sniper", "SFXPawn_Heavy", "GethAssassin",
                                "BioD_OMG003_125LitExtra"
                            };
                            foreach (ExportEntry exp in package.Exports)
                            {
                                if (exp.ClassName == "Package" && exp.idxLink == 0 &&
                                    !filesToSkip.Contains(exp.ObjectName.Name))
                                {
                                    if (string.Equals(exp.ObjectName.Name, fname,
                                        StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        hasPackageNamingItself = true;
                                    }

                                    Guid guid = exp.PackageGUID;
                                    if (guid != Guid.Empty)
                                    {
                                        GuidPackageMap.TryGetValue(guid, out string packagename);
                                        if (packagename != null && packagename != exp.ObjectName.Name)
                                        {
                                            Debug.WriteLine(
                                                $"-> {exp.UIndex} {exp.ObjectName.Name} has a guid different from already found one ({packagename})! {guid}");
                                        }

                                        if (packagename == null)
                                        {
                                            GuidPackageMap[guid] = exp.ObjectName.Name;
                                        }
                                    }
                                }
                            }
                        }

                        if (!hasPackageNamingItself)
                        {
                            Debug.WriteLine("----HAS NO SELF NAMING EXPORT");
                        }
                    }

                    foreach (KeyValuePair<Guid, string> entry in GuidPackageMap)
                    {
                        // do something with entry.Value or entry.Key
                        Debug.WriteLine($"  {entry.Value} {entry.Key}");
                    }

                    if (guidcachefile != null)
                    {
                        Debug.WriteLine("Opening GuidCache file " + guidcachefile);
                        using (var package = MEPackageHandler.OpenMEPackage(guidcachefile))
                        {
                            var cacheExp = package.Exports.FirstOrDefault(x => x.ObjectName == "GuidCache");
                            if (cacheExp != null)
                            {
                                var data = new MemoryStream();
                                var expPre = cacheExp.Data.Take(12).ToArray();
                                data.Write(expPre, 0, 12); //4 byte header, None
                                data.WriteInt32(GuidPackageMap.Count);
                                foreach (KeyValuePair<Guid, string> entry in GuidPackageMap)
                                {
                                    int nametableIndex = cacheExp.FileRef.FindNameOrAdd(entry.Value);
                                    data.WriteInt32(nametableIndex);
                                    data.WriteInt32(0);
                                    data.Write(entry.Key.ToByteArray(), 0, 16);
                                }

                                cacheExp.Data = data.ToArray();
                            }

                            package.Save();
                        }
                    }

                    Debug.WriteLine("Done. Cache size: " + GuidPackageMap.Count);
                    pew.IsBusy = false;
                }
            }
        }

        public static void PrintTerrainsBySize(PackageEditorWindow pewpf)
        {
            pewpf.SetBusy("Inventorying terrains...");
            Task.Run(() =>
            {
                List<(int numComponents, string file)> items = new List<(int numComponents, string file)>();
                var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(MEGame.LE1);
                int done = 0;
                foreach (var v in loadedFiles)
                {
                    pewpf.BusyText = $"Inventorying terrains [{done++}/{loadedFiles.Count}]";
                    using var p = MEPackageHandler.OpenMEPackage(v.Value);
                    foreach (var t in p.Exports.Where(x => x.ClassName == "Terrain" && !x.IsDefaultObject))
                    {
                        var tcSize = t.GetProperty<ArrayProperty<ObjectProperty>>("TerrainComponents");
                        if (tcSize != null)
                        {
                            items.Add((tcSize.Count, v.Key));
                        }
                    }
                }
                items = items.OrderBy(x => x.numComponents).ToList();
                foreach (var item in items)
                {
                    Debug.WriteLine($"{item.numComponents} terrain components in {item.file}");
                }
            }).ContinueWithOnUIThread(foundCandidates => { pewpf.IsBusy = false; });




        }

        public static void MakeAllGrenadesAndAmmoRespawn(PackageEditorWindow pew)
        {
            var ammoGrenades = pew.Pcc.Exports.Where(x =>
                x.ClassName != "Class" && !x.IsDefaultObject && (x.ObjectName == "SFXAmmoContainer" ||
                                                                 x.ObjectName == "SFXGrenadeContainer" ||
                                                                 x.ObjectName == "SFXAmmoContainer_Simulator"));
            foreach (var container in ammoGrenades)
            {
                BoolProperty respawns = new BoolProperty(true, "bRespawns");
                float respawnTimeVal = 20;
                if (container.ObjectName == "SFXGrenadeContainer")
                {
                    respawnTimeVal = 8;
                }

                if (container.ObjectName == "SFXAmmoContainer")
                {
                    respawnTimeVal = 3;
                }

                if (container.ObjectName == "SFXAmmoContainer_Simulator")
                {
                    respawnTimeVal = 5;
                }

                FloatProperty respawnTime = new FloatProperty(respawnTimeVal, "RespawnTime");
                var currentprops = container.GetProperties();
                currentprops.AddOrReplaceProp(respawns);
                currentprops.AddOrReplaceProp(respawnTime);
                container.WriteProperties(currentprops);
            }
        }

        public static void CheckAllGameImports(IMEPackage pewPcc)
        {
            if (pewPcc == null)
                return;

            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(pewPcc.Game);

            PackageCache pc = new PackageCache();
            var safeFiles = EntryImporter.FilesSafeToImportFrom(pewPcc.Game).ToList();
            safeFiles.AddRange(loadedFiles
                .Where(x => x.Key.StartsWith("Startup_") && (!pewPcc.Game.IsGame2() || x.Key.Contains("_INT")))
                .Select(x => x.Key));
            if (pewPcc.Game.IsGame3())
            {
                // SP ONLY
                safeFiles.Add(@"BIO_COMMON.pcc");
            }

            foreach (var f in safeFiles.Distinct())
            {
                pc.GetCachedPackage(loadedFiles[f]);
            }

            foreach (var f in loadedFiles)
            {
                using var p = MEPackageHandler.OpenMEPackage(f.Value);
                CheckImports(p, pc);
            }
        }

        public static void DumpAllLE1TLK(PackageEditorWindow pewpf)
        {
            var dlg = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select output folder"
            };
            if (dlg.ShowDialog(pewpf) == CommonFileDialogResult.Ok)
            {
                var langFilter = PromptDialog.Prompt(pewpf,
                    "Enter the language suffix to filter, or blank to dump INT. For example, PLPC, DE, FR.",
                    "Enter language filter", "", true);
                if (string.IsNullOrWhiteSpace(langFilter)) langFilter = null;
                Task.Run(() =>
                {
                    pewpf.BusyText = "Dumping TLKs...";
                    pewpf.IsBusy = true;
                    var allPackages = MELoadedFiles.GetFilesLoadedInGame(MEGame.LE1).ToList();
                    int numDone = 0;
                    foreach (var f in allPackages)
                    {
                        //if (!f.Key.Contains("Startup"))
                        //    continue;
                        pewpf.BusyText = $"Dumping TLKs [{++numDone}/{allPackages.Count}]";
                        using var package = MEPackageHandler.OpenMEPackage(f.Value);
                        foreach (var v in ((MEPackage)package).ReadLocalTLKs(langFilter, getAllGenders: true))
                        {
                            if (!string.IsNullOrWhiteSpace(langFilter) && !v.Name.EndsWith($"_{langFilter}"))
                            {
                                continue;
                            }

                            var outPath = Path.Combine(dlg.FileName,
                                $"{Path.GetFileNameWithoutExtension(f.Key)}.{package.GetEntry(v.UIndex).InstancedFullPath}.xml");
                            v.SaveToXML(outPath);
                        }

                    }
                }).ContinueWithOnUIThread(x => { pewpf.IsBusy = false; });
            }
        }

        // For making testing materials faster
        public static void ConvertMaterialToDonor(PackageEditorWindow pe)
        {
            if (pe.Pcc != null && pe.TryGetSelectedExport(out var exp) && (exp.ClassName == "Material" || exp.ClassName == "MaterialInstanceConstant"))
            {
                var donorFullName = PromptDialog.Prompt(pe, "Enter instanced full path of donor this is for", "Material Donor");
                if (string.IsNullOrEmpty(donorFullName))
                    return;
                var donorPath = Path.Combine(ModManagerIntegration.GetDonorOutputPath(), $"{donorFullName}.pcc");
                MEPackageHandler.CreateAndSavePackage(donorPath, pe.Pcc.Game);
                using var donorPackage = MEPackageHandler.OpenMEPackage(donorPath);

                var parts = donorFullName.Split('.');
                ExportEntry parent = null;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i < parts.Length - 1)
                    {
                        parent = ExportCreator.CreatePackageExport(donorPackage, parts[i], parent);
                        parent.indexValue = 0;
                    }
                    else
                    {
                        exp.ObjectName = parts[i];
                        EntryExporter.ExportExportToPackage(exp, donorPackage, out var portedEntry);
                        portedEntry.idxLink = parent?.UIndex ?? 0;
                        //EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, exp, donorPackage, parent, true, new RelinkerOptionsPackage() { ImportExportDependencies = true }, out var newDonor);
                    }
                }
                donorPackage.Save();
            }
        }

        public static void GenerateMaterialInstanceConstantFromMaterial(PackageEditorWindow pe)
        {
            if (pe.Pcc != null && pe.TryGetSelectedExport(out var matExp) && (matExp.ClassName == "Material" || matExp.ClassName == "MaterialInstanceConstant"))
            {
                var matExpProps = matExp.GetProperties();

                // Create the export
                var matInstConst = ExportCreator.CreateExport(pe.Pcc, matExp.ObjectName.Name + "_matInst", "MaterialInstanceConstant", matExp.Parent);
                matInstConst.indexValue--; // Decrement it by one so it starts at 0

                var matInstConstProps = matInstConst.GetProperties();
                var lightingParent = matExpProps.GetProp<StructProperty>("LightingGuid");
                if (lightingParent != null)
                {
                    lightingParent.Name = "ParentLightingGuid"; // we aren't writing to parent so this is fine
                    matInstConstProps.AddOrReplaceProp(lightingParent);
                }

                matInstConstProps.AddOrReplaceProp(new ObjectProperty(matExp.UIndex, "Parent"));
                matInstConstProps.AddOrReplaceProp(CommonStructs.GuidProp(Guid.NewGuid(), "m_Guid")); // IDK if this is used but we're gonna do it anyways

                ArrayProperty<StructProperty> vectorParameters = new ArrayProperty<StructProperty>("VectorParameterValues");
                ArrayProperty<StructProperty> scalarParameters = new ArrayProperty<StructProperty>("ScalarParameterValues");
                ArrayProperty<StructProperty> textureParameters = new ArrayProperty<StructProperty>("TextureParameterValues");

                var expressions = matExpProps.GetProp<ArrayProperty<ObjectProperty>>("Expressions");
                if (expressions != null)
                {
                    foreach (var expressionOP in expressions)
                    {
                        if (expressionOP.Value <= 0)
                            continue; // It's null
                        var expression = expressionOP.ResolveToEntry(pe.Pcc) as ExportEntry;
                        switch (expression.ClassName)
                        {
                            case "MaterialExpressionScalarParameter":
                                {
                                    var spvP = expression.GetProperties();
                                    var paramValue = spvP.GetProp<FloatProperty>("DefaultValue");
                                    if (paramValue == null)
                                    {
                                        spvP.Add(new FloatProperty(0, "ParameterValue"));

                                    }
                                    else
                                    {
                                        paramValue.Name = "ParameterValue";
                                        spvP.RemoveAt(0);
                                        spvP.AddOrReplaceProp(paramValue); // This value goes on the end
                                    }
                                    scalarParameters.Add(new StructProperty("ScalarParameterValue", spvP));
                                }
                                break;
                            case "MaterialExpressionVectorParameter":
                                {
                                    var vpvP = expression.GetProperties();
                                    var paramValue = vpvP.GetProp<StructProperty>("DefaultValue");
                                    if (paramValue == null)
                                    {
                                        vectorParameters.Add(CommonStructs.Vector3Prop(0, 0, 0, "DefaultValue"));

                                    }
                                    else
                                    {
                                        paramValue.Name = "ParameterValue";
                                        vectorParameters.Add(new StructProperty("VectorParameterValue", vpvP));
                                    }
                                }
                                break;
                            case "MaterialExpressionTextureSampleParameter2D":
                                {
                                    var tpvP = expression.GetProperties();
                                    var paramValue = tpvP.GetProp<ObjectProperty>("Texture");
                                    paramValue.Name = "ParameterValue";
                                    textureParameters.Add(new StructProperty("TextureParameterValue", tpvP));
                                }
                                break;
                        }
                    }
                }

                if (vectorParameters.Any()) matInstConstProps.AddOrReplaceProp(vectorParameters);
                if (scalarParameters.Any()) matInstConstProps.AddOrReplaceProp(scalarParameters);
                if (textureParameters.Any()) matInstConstProps.AddOrReplaceProp(textureParameters);

                matInstConst.WriteProperties(matInstConstProps);
            }
        }

        public static void CheckNeverstream(PackageEditorWindow pe)
        {
            List<ExportEntry> badNST = new List<ExportEntry>();
            foreach (var exp in pe.Pcc.Exports.Where(x => x.IsTexture()))
            {
                var props = exp.GetProperties();
                var texinfo = ObjectBinary.From<UTexture2D>(exp);
                var numMips = texinfo.Mips.Count;
                var ns = props.GetProp<BoolProperty>("NeverStream");
                int lowMipCount = 0;
                for (int i = numMips - 1; i > 0; i--)
                {
                    if (lowMipCount > 6 && (ns == null || ns.Value == false) && texinfo.Mips[i].IsLocallyStored && texinfo.Mips[i].StorageType != StorageTypes.empty)
                    {
                        exp.WriteProperty(new BoolProperty(true, "NeverStream"));
                        badNST.Add(exp);
                        break;
                    }
                    lowMipCount++;
                }
            }

            var ld = new ListDialog(badNST.Select(x => new EntryStringPair(x, $"{x.InstancedFullPath} has incorrect neverstream")),
                "Bad NeverStream settings", "The following textures have incorrect NeverStream values:", pe)
            {
                DoubleClickEntryHandler = pe.GetEntryDoubleClickAction()
            };
            ld.Show();
        }

        public static void MapMaterialIDs(PackageEditorWindow pe)
        {
            MEGame game = MEGame.ME1;
            var materialGuidMap = new Dictionary<Guid, string>();
            //foreach (var exp in pe.Pcc.Exports.Where(x => x.IsTexture()))
            //{
            //    var props = exp.GetProperties();
            //    var format = props.GetProp<EnumProperty>("Format");
            //    badNST.Add(new EntryStringPair(exp, $"{format.Value} | {exp.InstancedFullPath}"));
            //}
            Task.Run(() =>
            {
                pe.SetBusy("Checking materials");

                var allPackages = MELoadedFiles.GetFilesLoadedInGame(game).ToList();
                int numDone = 0;
                foreach (var f in allPackages)
                {
                    pe.BusyText = $"Indexing file [{++numDone}/{allPackages.Count}]";
                    using var package = MEPackageHandler.OpenMEPackage(f.Value);

                    // Index objects
                    foreach (var exp in package.Exports.Where(x => x.ClassName == "Material" && !x.IsDefaultObject))
                    {
                        var material = ObjectBinary.From<Material>(exp);
                        var guid = material.SM3MaterialResource.ID;
                        materialGuidMap[guid] = exp.InstancedFullPath;
                    }

                    File.WriteAllText(Path.Combine(AppDirectories.ObjectDatabasesFolder, $"{game}MaterialMap.json"), JsonConvert.SerializeObject(materialGuidMap));
                }
            }).ContinueWithOnUIThread(list =>
            {
                pe.EndBusy();
            });
        }

        public static void MakeAllConversationsLinear(PackageEditorWindow pe)
        {
            var conversations = pe.Pcc.Exports.Where(x => x.ClassName == "BioConversation" && !x.IsDefaultObject).ToList();
            foreach (var convExp in conversations)
            {
                PropertyCollection convProps = convExp.GetProperties();

                // OH BOY

                // ONLY 1 ENTRY POINT
                var startingList = convProps.GetProp<ArrayProperty<IntProperty>>("m_StartingList");
                startingList.Clear();
                startingList.Add(0);

                // REMOVE ALL REPLIES TO ENTRIES
                var entryList = convProps.GetProp<ArrayProperty<StructProperty>>("m_EntryList");
                var replyList = convProps.GetProp<ArrayProperty<StructProperty>>("m_ReplyList");

                foreach (var entry in entryList)
                {
                    var entryReplyList = entry.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");

                }


                convExp.WriteProperties(convProps);
            }
        }

        public static void CompareVerticeCountBetweenGames(PackageEditorWindow pe)
        {
            var me1Vertices = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<int, uint>>>(File.ReadAllText(@"Y:\ModLibrary\LE1\V Test\Donors\Mappings\ME1VertexMap.json"));
            var le1Vertices = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<int, uint>>>(File.ReadAllText(@"Y:\ModLibrary\LE1\V Test\Donors\Mappings\LE1VertexMap.json"));

            foreach (var me1Mesh in me1Vertices)
            {
                if (le1Vertices.TryGetValue(me1Mesh.Key, out var matchingLE1Models))
                {
                    var matchingME1Models = me1Mesh.Value;
                    if (matchingME1Models.Count == matchingLE1Models.Count)
                    {
                        // The number of LODs are identical
                        bool works = true;
                        for (int i = 0; i < matchingLE1Models.Count; i++)
                        {
                            if (matchingME1Models[i] != matchingLE1Models[i])
                            {
                                works = false; //vertex count has changed
                            }
                        }

                        if (works)
                        {
                            Debug.WriteLine($"MATCHING VERTEX COUNT: {me1Mesh.Key}");
                        }
                    }
                }
            }
            return;
        }

        // GENERATING
        public static void GenerateVerticeCount(PackageEditorWindow pe)
        {
            Dictionary<string, Dictionary<int, uint>> me1VertexMap = new();
            {
                var me1Files = Directory.GetFiles(@"Y:\ModLibrary\LE1\V Test\ModdedSource", "*", SearchOption.AllDirectories);
                foreach (var me1FilePath in me1Files)
                {
                    using var me1File = MEPackageHandler.OpenMEPackage(me1FilePath);
                    foreach (var export in me1File.Exports.Where(x => x.ClassName == "StaticMesh"))
                    {
                        if (!me1VertexMap.ContainsKey(export.InstancedFullPath))
                        {
                            var sm = ObjectBinary.From<StaticMesh>(export);
                            Dictionary<int, uint> lodVerticeMap = new();
                            for (int i = 0; i < sm.LODModels.Length; i++)
                                lodVerticeMap[i] = sm.LODModels[i].NumVertices;
                            me1VertexMap[export.InstancedFullPath] = lodVerticeMap;
                        }
                    }
                }

                var outText = JsonConvert.SerializeObject(me1VertexMap);
                File.WriteAllText(@"Y:\ModLibrary\LE1\V Test\Donors\Mappings\ME1VertexMap.json", outText);
            }

            // Calculate LE1
            var le1Files = MELoadedFiles.GetOfficialFiles(MEGame.LE1);
            Dictionary<string, Dictionary<int, uint>> le1VertexMap = new();

            foreach (var le1FilePath in le1Files)
            {
                using var le1File = MEPackageHandler.OpenMEPackage(le1FilePath);
                foreach (var export in le1File.Exports.Where(x => x.ClassName == "StaticMesh"))
                {
                    if (me1VertexMap.ContainsKey(export.InstancedFullPath) && !le1VertexMap.ContainsKey(export.InstancedFullPath))
                    {
                        var sm = ObjectBinary.From<StaticMesh>(export);
                        Dictionary<int, uint> lodVerticeMap = new();
                        for (int i = 0; i < sm.LODModels.Length; i++)
                            lodVerticeMap[i] = sm.LODModels[i].NumVertices;
                        le1VertexMap[export.InstancedFullPath] = lodVerticeMap;
                    }
                }
            }
            File.WriteAllText(@"Y:\ModLibrary\LE1\V Test\Donors\Mappings\LE1VertexMap.json", JsonConvert.SerializeObject(le1VertexMap));
        }

        public static void ShowTextureFormats(PackageEditorWindow pe)
        {
            List<string> texFormats = new List<string>();
            //foreach (var exp in pe.Pcc.Exports.Where(x => x.IsTexture()))
            //{
            //    var props = exp.GetProperties();
            //    var format = props.GetProp<EnumProperty>("Format");
            //    badNST.Add(new EntryStringPair(exp, $"{format.Value} | {exp.InstancedFullPath}"));
            //}
            Task.Run(() =>
            {
                pe.SetBusy("Checking textures");

                if (pe.Pcc == null)
                {
                    var allPackages = MELoadedFiles.GetFilesLoadedInGame(MEGame.LE1).ToList();
                    int numDone = 0;
                    foreach (var f in allPackages)
                    {
                        pe.BusyText = $"Indexing file [{++numDone}/{allPackages.Count}]";
                        using var package = MEPackageHandler.OpenMEPackage(f.Value);

                        // Index objects
                        foreach (var exp in package.Exports.Where(x => x.IsTexture()))
                        {
                            var format = exp.GetProperty<EnumProperty>("Format");
                            if (format != null && !texFormats.Contains(format.Value))
                            {
                                texFormats.Add(format.Value);
                            }
                        }
                    }
                }
                else
                {
                    foreach (var exp in pe.Pcc.Exports.Where(x => x.IsTexture()))
                    {
                        var format = exp.GetProperty<EnumProperty>("Format");
                        if (format != null && !texFormats.Contains(format.Value))
                        {
                            texFormats.Add(format.Value);
                        }
                    }
                }

                return texFormats;
            }).ContinueWithOnUIThread(list =>
            {
                pe.EndBusy();
                var ld = new ListDialog(list.Result, "Texture formats", "The game uses the following texture formats:", pe);
                ld.Show();
            });
        }


        public static void RebuildInternalResourceClassInformations(PackageEditorWindow pe)
        {
            MEGame game = MEGame.LE1;
            StringBuilder sb = new StringBuilder();
            var loadStream = LegendaryExplorerCoreUtilities.LoadFileFromCompressedResource("GameResources.zip",
                LegendaryExplorerCoreLib.CustomResourceFileName(MEGame.LE1));

            using var p = MEPackageHandler.OpenMEPackageFromStream(loadStream,
                GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName);
            foreach (var c in p.Exports.Where(x => x.IsClass))
            {
                sb.AppendLine($"#region {c.ObjectName}");
                sb.AppendLine($"classes[\"{c.ObjectName}\"] = new ClassInfo");
                sb.AppendLine("{");
                sb.AppendLine($"\tbaseClass = \"{c.SuperClassName}\",");
                sb.AppendLine($"\tpccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,");
                sb.AppendLine($"\texportIndex = {c.UIndex}, // in {game}Resources.pcc");

                // Properties
                var ci = GlobalUnrealObjectInfo.generateClassInfo(c);
                if (ci.properties.Any())
                {
                    sb.AppendLine("\tproperties =");
                    sb.AppendLine("\t\t\t\t{"); // stupid intellisense
                    foreach (var prop in ci.properties)
                    {
                        var propInfoStr = $"new PropertyInfo(PropertyType.{prop.Value.Type.ToString()}";
                        // If this is an array it needs a reference type
                        // Probably on objectproperty too?
                        if (prop.Value.Reference != null)
                        {
                            propInfoStr += $", reference: \"{prop.Value.Reference}\"";
                        }

                        if (prop.Value.Transient)
                        {
                            propInfoStr += ", transient: true";
                        }

                        propInfoStr += ")";

                        sb.AppendLine($"\t\t\t\t\tnew KeyValuePair<NameReference, PropertyInfo>(\"{prop.Key}\", {propInfoStr}),");
                    }

                    sb.AppendLine("\t\t\t\t}"); // stupid intellisense

                }

                sb.AppendLine("};");
                if (c.SuperClass.InheritsFrom("SequenceObject"))
                {
                    sb.AppendLine();
                    sb.AppendLine($"sequenceObjects[\"{c.ObjectName}\"] = new SequenceObjectInfo");
                    sb.AppendLine("{");
                    sb.AppendLine($"\tObjInstanceVersion = 1"); // Not sure if this is correct...
                    sb.AppendLine("};");
                }

                sb.AppendLine("#endregion");
            }

            Clipboard.SetText(sb.ToString());
        }

        public static void ConvertSLCALightToNonSLCA(PackageEditorWindow pe)
        {
            if (pe.Pcc != null && pe.TryGetSelectedExport(out var exp) && exp.IsA("LightComponent") && exp.Parent.ClassName == "StaticLightCollectionActor")
            {
                var parent = ObjectBinary.From<StaticLightCollectionActor>(exp.Parent as ExportEntry);
                var slcaIndex = parent.Components.IndexOf(exp.UIndex);

                var pl = pe.Pcc.FindExport("TheWorld.PersistentLevel");
                var lightType = exp.ObjectName.Name.Substring(0, exp.ObjectName.Name.IndexOf("_"));
                var newExport = ExportCreator.CreateExport(pe.Pcc, lightType, lightType, pl);

                var positioning = parent.LocalToWorldTransforms[slcaIndex].UnrealDecompose();

                var newProps = newExport.GetProperties();
                newProps.AddOrReplaceProp(new ObjectProperty(exp, "LightComponent"));
                newProps.AddOrReplaceProp(CommonStructs.Vector3Prop(positioning.translation, "Location"));
                newProps.AddOrReplaceProp(CommonStructs.RotatorProp(positioning.rotation, "Rotation"));
                //newProps.AddOrReplaceProp(CommonStructs.Vector3Prop(parent.LocalToWorldTransforms[slcaIndex].M41, parent.LocalToWorldTransforms[slcaIndex].M42, parent.LocalToWorldTransforms[slcaIndex].M43, "Rotation")); // No idea which var is this...
                newProps.AddOrReplaceProp(new NameProperty(lightType, "Tag"));
                newExport.WriteProperties(newProps);
            }
        }

        // This depended on CrossGenV which was removed. Might see if there is way we can support it in the future at some point...
        //public static void TerrainLevelMaker(PackageEditorWindow pe)
        //{
        //    // Open base
        //    using var terrainBaseP = MEPackageHandler.OpenMEPackage(Path.Combine(LE1Directory.CookedPCPath, "BIOA_TERRAINTEST_BASE.pcc"));
        //    var tbpPL = terrainBaseP.FindExport("TheWorld.PersistentLevel");

        //    // SET STARTING LOCATION
        //    PathEdUtils.SetLocation(terrainBaseP.FindExport("TheWorld.PersistentLevel.PlayerStart_0"), 62863, -91054, -7000);
        //    PathEdUtils.SetLocation(terrainBaseP.FindExport("TheWorld.PersistentLevel.BlockingVolume_21"), 62863, -91054, -7200); // -200 Z from player start to make base

        //    // DONOR TERRAIN TO TEST
        //    using var meTerrainP = MEPackageHandler.OpenMEPackage(Path.Combine(ME1Directory.CookedPCPath, @"Maps\LAV\LAY\BIOA_LAV70_01_LAY.sfm"));
        //    var meTerrain = meTerrainP.FindExport("TheWorld.PersistentLevel.Terrain_1");

        //    // Precorrect and port
        //    VTestExperiment.PrePortingCorrections(meTerrainP, null);
        //    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, meTerrain, tbpPL.FileRef, tbpPL, true, new RelinkerOptionsPackage(), out var newTerrain);
        //    VTestExperiment.RebuildPersistentLevelChildren(tbpPL, null);
        //    terrainBaseP.Save(Path.Combine(LE1Directory.CookedPCPath, "BIOA_TERRAINTEST.pcc"));

        //    // Check values...
        //    using var leSameTerrainP = MEPackageHandler.OpenMEPackage(Path.Combine(LE1Directory.CookedPCPath, @"BIOA_LAV70_00_ART.pcc"));
        //    var leSameTerrain = leSameTerrainP.FindExport("TheWorld.PersistentLevel.Terrain_1");
        //    var leSameTerrainComponents = leSameTerrain.GetProperty<ArrayProperty<ObjectProperty>>("TerrainComponents");
        //    var portTerrainComponents = (newTerrain as ExportEntry).GetProperty<ArrayProperty<ObjectProperty>>("TerrainComponents");

        //    for (int i = 0; i < leSameTerrainComponents.Count; i++)
        //    {
        //        var leTC = leSameTerrainP.GetUExport(leSameTerrainComponents[i].Value);
        //        var portedTC = terrainBaseP.GetUExport(portTerrainComponents[i].Value);

        //        var leC = ObjectBinary.From<TerrainComponent>(leTC);
        //        var portedC = ObjectBinary.From<TerrainComponent>(portedTC);

        //        for (int j = 0; j < leC.CollisionVertices.Length; j++)
        //        {
        //            var leCol = leC.CollisionVertices[j];
        //            var portedCol = portedC.CollisionVertices[j];
        //            if (leCol.X != portedCol.X || leCol.Y != portedCol.Y || leCol.Z != portedCol.Z)
        //                Debug.WriteLine($"{i}-{j}\t({leCol.X}, {leCol.Y}, {leCol.Z}) | ({portedCol.X}, {portedCol.Y} ,{portedCol.Z}) | DIFF [LE-PORTED]: ({leCol.X - portedCol.X}, {leCol.Y - portedCol.Y}, {leCol.Z - portedCol.Z})");
        //        }

        //        // Bounding volume
        //        for (int j = 0; j < leC.BVTree.Length; j++)
        //        {
        //            var leColMin = leC.BVTree[j].BoundingVolume.Min;
        //            var leColMax = leC.BVTree[j].BoundingVolume.Max;
        //            var portedColMin = portedC.BVTree[j].BoundingVolume.Min;
        //            var portedColMax = portedC.BVTree[j].BoundingVolume.Max;

        //            if (leColMin.X != portedColMin.X || leColMin.Y != portedColMin.Y || leColMin.Z != portedColMin.Z)
        //                Debug.WriteLine($"{i}-{j} MIN\t({leColMin.X}, {leColMin.Y}, {leColMin.Z}) | ({portedColMin.X}, {portedColMin.Y} ,{portedColMin.Z}) | DIFF [LE-PORTED]: ({leColMin.X - portedColMin.X}, {leColMin.Y - portedColMin.Y}, {leColMin.Z - portedColMin.Z})");

        //            if (leColMax.X != portedColMax.X || leColMax.Y != portedColMax.Y || leColMax.Z != portedColMax.Z)
        //                Debug.WriteLine($"{i}-{j} MAX\t({leColMax.X}, {leColMax.Y}, {leColMax.Z}) | ({portedColMax.X}, {portedColMax.Y} ,{portedColMax.Z}) | DIFF [LE-PORTED]: ({leColMax.X - portedColMax.X}, {leColMax.Y - portedColMax.Y}, {leColMax.Z - portedColMax.Z})");


        //        }
        //    }
        //}

        public static void CreatePowerMaster()
        {
            MEPackageHandler.CreateAndSavePackage(@"C:\Users\Mgamerz\Desktop\LE2Powers.pcc", MEGame.LE2);
            using var masterFile = MEPackageHandler.OpenMEPackage(@"C:\Users\Mgamerz\Desktop\LE2Powers.pcc");

            PackageCache globalCache = new PackageCache();
            PackageCache localCache = new PackageCache();

            var allPowers = new List<string>();
            foreach (var f in MELoadedFiles.GetFilesLoadedInGame(MEGame.LE2, true))
            {
                using var p = MEPackageHandler.OpenMEPackage(f.Value);
                foreach (var powerExp in p.Exports.Where(x => x.InheritsFrom("SFXPower") && !allPowers.Contains(x.InstancedFullPath)))
                {
                    EntryExporter.ExportExportToPackage(powerExp, masterFile, out var newEntry, globalCache, localCache);
                    allPowers.Add(powerExp.InstancedFullPath);
                }
            }

            masterFile.Save();
            File.WriteAllLines(@"C:\users\mgamerz\desktop\le2powers.txt", allPowers);
        }

        public static void ImportBankTest(PackageEditorWindow pe)
        {
            if (pe.Pcc == null)
                return;
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = "WwiseBank files|*.bnk",
                Title = "Select generated soundbank",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            if (ofd.ShowDialog() == true)
            {
                var askResult = Xceed.Wpf.Toolkit.MessageBox.Show(pe, "Are your streamed Wwise audio samples named correctly in the editor? If not, the filenames of the wav files when imported will be used.",
                       "Choose export names", MessageBoxButton.YesNoCancel, MessageBoxImage.Question,
                       MessageBoxResult.Cancel);
                if (askResult == MessageBoxResult.Cancel)
                    return;
                WwiseBankImport.ImportBank(ofd.FileName, askResult == MessageBoxResult.Yes, pe.Pcc);
            }
        }

        public static void MaterializeModel(PackageEditorWindow pe)
        {
            var pcc = pe.Pcc;
            if (pcc == null)
                return;

            var sel = EntrySelector.GetEntry<ExportEntry>(pe, pcc, "Select a material to set on this object", x => x.ClassName == @"Material" || x.ClassName == @"MaterialInstanceConstant");
            if (sel == null) return;
            var matValue = sel.UIndex;

            if (pe.TryGetSelectedExport(out var exp) && exp.ClassName == @"Model")
            {
                var model = ObjectBinary.From<Model>(exp);

                foreach (var v in model.Surfs)
                {
                    v.Material = matValue;
                }

                exp.WriteBinary(model);
            }

            if (pe.TryGetSelectedExport(out var exp2) && exp2.ClassName == @"ModelComponent")
            {
                var model = ObjectBinary.From<ModelComponent>(exp2);

                foreach (var v in model.Elements)
                {
                    v.Material = matValue;
                }

                exp2.WriteBinary(model);
            }
        }

        /// <summary>
        /// Converts an instance of a BioPawn to a class SFXPawn that can be spawned directly with BioPawnType
        /// </summary>
        /// <param name="pe"></param>
        public static void LE2ConvertBioPawnToSFXPawn(PackageEditorWindow pe)
        {
            if (pe.Pcc == null)
                return;

            if (!pe.TryGetSelectedExport(out var bioPawnExport) && bioPawnExport.ClassName == "BioPawn" && bioPawnExport.InstancedFullPath.StartsWith("TheWorld.PersistentLevel."))
            {
                return;
            }

            var pcc = pe.Pcc;
            var merGamePawns = pcc.FindExport("MERGamePawns");
            if (merGamePawns == null)
            {
                merGamePawns = ExportCreator.CreatePackageExport(pcc, "MERGamePawns");
            }

            var tag = bioPawnExport.GetProperty<NameProperty>("Tag");
            FileLib lib = new FileLib(pcc);
            lib.Initialize();
            var exportName = $"SFXPawn_{tag.Value.Instanced}";
            var classIFP = $"MERGamePawns.{exportName}";

            var bodyMesh = bioPawnExport.GetProperty<ObjectProperty>("Mesh").ResolveToEntry(pcc) as ExportEntry;
            var mdl = bodyMesh.GetProperty<ObjectProperty>("SkeletalMesh").ResolveToEntry(pcc);
            var bodyMeshIFP = mdl.InstancedFullPath;
            var bodyMaterials = bodyMesh.GetProperty<ArrayProperty<ObjectProperty>>("Materials");

            var headMesh = bioPawnExport.GetProperty<ObjectProperty>("HeadMesh").ResolveToEntry(pcc) as ExportEntry;
            var hmdl = headMesh.GetProperty<ObjectProperty>("SkeletalMesh").ResolveToEntry(pcc);
            var headMeshIFP = hmdl.InstancedFullPath;
            //var headMaterials = headMesh.GetProperty<ArrayProperty<ObjectProperty>>("Materials");

            var actorType = bioPawnExport.GetProperty<ObjectProperty>("ActorType").ResolveToEntry(pcc) as IEntry;
            var actorTypeIFP = actorType.InstancedFullPath;

            var classText = $@"Class {exportName} extends SFXPawn
    placeable
    config(Game);

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{{
        Begin Template Class=BioPawnBehavior Name=Behavior
        Begin Template Class=BioInterface_Appearance_Pawn Name=PawnApperInterface
            Begin Template Class=Bio_Appr_Character_Settings Name=oSettings
                Begin Template Class=Bio_Appr_Character_Body_Settings Name=oBodySettings
                    Begin Template Class=Bio_Appr_Character_HeadGear_Settings Name=oHeadGearSettings
                    End Template
                    m_oHeadGearSettings = oHeadGearSettings
                End Template
                Begin Template Class=Bio_Appr_Character_Head_Settings Name=oHeadSettings
                    Begin Template Class=Bio_Appr_Character_Hair_Settings Name=oHairSettings
                    End Template
                    m_oHairSettings = oHairSettings
                End Template
                m_oBodySettings = oBodySettings
                m_oHeadSettings = oHeadSettings
            End Template
            Begin Template Class=BioMaterialOverride Name=s_pDefaultMaterialParameters
            End Template
            m_oSettings = oSettings
            m_pMaterialParameters = s_pDefaultMaterialParameters
        End Template
        Begin Template Class=BioAttributesPawn Name=AttributesPawn
        End Template
        Begin Template Class=BioPawnCameraShakeInterface Name=CameraShakeInterface
        End Template
        m_PawnAttributes = AttributesPawn
        m_oAppearanceType = PawnApperInterface
        m_cameraShakeInterface = CameraShakeInterface
    End Template
    Begin Template Class=BioDynamicLightEnvironmentComponent Name=BioLightEnvComponent0
    End Template
    Begin Template Class=BioGestureAnimSetMgr Name=oAnimSetMgr
    End Template
    Begin Template Class=CylinderComponent Name=CollisionCylinder
        ReplacementPrimitive = None
    End Template
    Begin Template Class=ForceFeedbackWaveform Name=FootstepShakeFF0
    End Template
    Begin Template Class=SFXModule_Damage Name=DmgMod0
    End Template
    Begin Template Class=SFXModule_GameEffectManager Name=GEMod0
    End Template
    Begin Template Class=SFXModule_Radar Name=RadarModule
    End Template
    Begin Template Class=SkeletalMeshComponent Name=HeadMesh0
        SkeletalMesh = SkeletalMesh'{headMeshIFP}'
        ParentAnimComponent = BioPawnSkeletalMeshComponent
        ShadowParent = BioPawnSkeletalMeshComponent
        ReplacementPrimitive = None
        LightEnvironment = BioLightEnvComponent0
    End Template
    Begin Template Class=SkeletalMeshComponent Name=BioPawnSkeletalMeshComponent
        SkeletalMesh = SkeletalMesh'{bodyMeshIFP}'
        ReplacementPrimitive = None
        LightEnvironment = BioLightEnvComponent0
    End Template
    Begin Template Class=SFXPowerManager Name=PowerMgr
    End Template
    FootstepForceFeedback = FootstepShakeFF0
    HeadMesh = HeadMesh0
    LightEnvironment = BioLightEnvComponent0
    m_pAnimSetMgr = oAnimSetMgr
    PowerManager = PowerMgr
    Mesh = BioPawnSkeletalMeshComponent
    CylinderComponent = CollisionCylinder
    Components = (CollisionCylinder, None, BioLightEnvComponent0, BioPawnSkeletalMeshComponent, HeadMesh0)
    Modules = (DmgMod0, GEMod0, RadarModule)
    CollisionComponent = CollisionCylinder
    oBioComponent = Behavior
    ActorType = BioPawnType'{actorTypeIFP}'
}}";

            UnrealScriptCompiler.CompileClass(pcc, classText, lib, parent: merGamePawns);
            var classExport = pcc.FindExport(classIFP);

            var newMats = new ArrayProperty<ObjectProperty>("Materials");
            var skelMeshComp = pcc.FindExport(pcc.GetUExport(ObjectBinary.From<UClass>(classExport).Defaults).InstancedFullPath + ".BioPawnSkeletalMeshComponent");
            if (bodyMaterials != null)
            {
                foreach (var bm in bodyMaterials)
                {
                    var bodyMatExp = bm.ResolveToEntry(pcc);
                    var clone = EntryCloner.CloneEntry(bodyMatExp);
                    clone.idxLink = skelMeshComp.UIndex;
                    newMats.Add(new ObjectProperty(clone));
                }
            }

            if (newMats.Any())
            {
                skelMeshComp.WriteProperty(newMats);
            }
        }

        public static void MakeInterpTrackMoveEndAtZero(PackageEditorWindow pe)
        {
            if (pe.TryGetSelectedExport(out var interpTrackMove) && interpTrackMove.ClassName == "InterpTrackMove")
            {
                // This moves an interptrackmove to end at 0,0,0 by shifiting it all - which makes it so you can do relativetoinitial instead of world
                var posTrack = interpTrackMove.GetProperty<StructProperty>("PosTrack");
                var points = posTrack.GetProp<ArrayProperty<StructProperty>>("Points");
                var lastPoint = points.Last();
                var lastOut = lastPoint.GetProp<StructProperty>("OutVal");

                var offsetX = -(lastOut.GetProp<FloatProperty>("X"));
                var offsetY = -(lastOut.GetProp<FloatProperty>("Y"));
                var offsetZ = -(lastOut.GetProp<FloatProperty>("Z"));

                foreach (var p in points)
                {
                    var outVal = p.GetProp<StructProperty>("OutVal");
                    outVal.GetProp<FloatProperty>("X").Value = outVal.GetProp<FloatProperty>("X").Value + offsetX;
                    outVal.GetProp<FloatProperty>("Y").Value = outVal.GetProp<FloatProperty>("Y").Value + offsetY;
                    outVal.GetProp<FloatProperty>("Z").Value = outVal.GetProp<FloatProperty>("Z").Value + offsetZ;
                }

                interpTrackMove.WriteProperty(posTrack);
            }
            return;
        }

        internal class MatContainer
        {
            public string MatIFP { get; set; }
            public List<string> MatVectors { get; } = new List<string>();
            public List<string> MatScalars { get; } = new List<string>();
        }

        public static void ConvertExportToImport(PackageEditorWindow pe)
        {
            if (pe.TryGetSelectedExport(out var exp2))
            {
                var import = new ImportEntry(exp2, exp2.Parent?.UIndex ?? 0, exp2.FileRef);
                if (EntryImporter.ResolveImport(import) != null)
                {
                    EntryImporter.ConvertExportToImport(exp2);
                }
                else
                {
                    MessageBox.Show($"Could not resolve import: {exp2.InstancedFullPath}.\nFix your setup and try again.\nOr maybe this is just importable?\nOr maybe the code is just bugged.");
                }
            }
        }

        public static void MScanner(PackageEditorWindow pe)
        {
            if (pe.TryGetSelectedExport(out var exp2))
            {
                var referencingEntries = exp2.GetEntriesThatReferenceThisOne();
                if (referencingEntries.Any())
                {
                    // Create a temporary import
                    var convertedItem = new ImportEntry(exp2.FileRef, exp2.Parent, new NameReference(exp2.ObjectName.Name + "_TMP", exp2.ObjectName.Number))
                    {
                        ClassName = exp2.ClassName,
                        PackageFile = "Core", // Do a better lookup for this - it's possible to find where class of class is stored.
                                              // This works for LEX merges (import finds existing export in package)
                                              // but will not work in game if it gets turned into an actual import for use
                    };
                    exp2.FileRef.AddImport((ImportEntry)convertedItem);

                    foreach (var f in referencingEntries)
                    {
                        // Make a new map for every iteration since this technically could add some items... somehow...
                        var objectMap = new ListenableDictionary<IEntry, IEntry>();
                        objectMap.Add(exp2, convertedItem); // Convert references to import
                        objectMap.Add(f.Key, f.Key); // Force this to relink on itself.
                        RelinkerOptionsPackage rop = new RelinkerOptionsPackage()
                        {
                            CrossPackageMap = objectMap
                        };
                        Relinker.RelinkAll(rop);
                    }

                    // Cleanup temporary stuff
                    convertedItem.ObjectName = exp2.ObjectName;
                    EntryPruner.TrashEntries(exp2.FileRef, new[] { exp2 });
                }

            }

            return;
            //if (pe.TryGetSelectedExport(out var exp2))
            //{
            //    var targets = exp2.GetProperty<ArrayProperty<ObjectProperty>>("Targets");
            //    foreach (var t in targets.Select(x => x.ResolveToEntry(pe.Pcc) as ExportEntry))
            //    {
            //        var bin = ObjectBinary.From<MorphTarget>(t);
            //        if (bin.BoneOffsets.Length > 0)
            //        {
            //            for (int i = 0; i < bin.BoneOffsets.Length; i++)
            //            {
            //                bin.BoneOffsets[i].Offset.X *= 10;
            //                bin.BoneOffsets[i].Offset.Y *= 10;
            //                bin.BoneOffsets[i].Offset.Z *= 10;
            //            }
            //            t.WriteBinary(bin);
            //        }
            //    }
            //}
            //return;
            var merEyes =
                "B:\\UserProfile\\source\\repos\\ME2Randomizer\\Randomizer\\Randomizers\\Game2\\Assets\\Binary\\Packages\\LE2\\Always_MEREyes\\BIOG_MEREyes.pcc";
            using var merEyesP = MEPackageHandler.OpenMEPackage(merEyes);
            foreach (var v in merEyesP.Exports)
            {
                v.ExportFlags = v.ExportFlags & ~UnrealFlags.EExportFlags.ForcedExport;
            }
            merEyesP.Save();
            return;
            if (pe.TryGetSelectedExport(out var matInst))
            {
                var tpv = matInst.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues");
                if (tpv == null) return;
                foreach (var v in tpv)
                {
                    var parm = v.GetProp<NameProperty>("ParameterName").Value.Name;
                    ExportEntry valueExp;
                    var value = v.GetProp<ObjectProperty>("ParameterValue").ResolveToEntry(pe.Pcc);
                    if (value is ImportEntry ie)
                    {
                        valueExp = EntryImporter.ResolveImport(ie, null, null);
                    }
                    else
                    {
                        valueExp = value as ExportEntry;
                    }

                    var packageParent = merEyesP.FindExport(matInst.ObjectName) ?? ExportCreator.CreatePackageExport(merEyesP, matInst.ObjectName);
                    if (parm.Contains("EYE_Iris_Norm"))
                    {
                        valueExp.ObjectName = "EYE_Iris_Norm";
                    }
                    else if (parm.Contains("EYE_Mask"))
                    {
                        valueExp.ObjectName = "EYE_Mask";

                    }
                    else if (parm.Contains("EYE_Diff"))
                    {
                        valueExp.ObjectName = "EYE_Diff";

                    }
                    else if (parm.Contains("EYE_Lens_Norm"))
                    {
                        valueExp.ObjectName = "EYE_Lens_Norm";
                    }
                    else
                    {
                        Debugger.Break();
                    }

                    EntryImporter.ImportAndRelinkEntries(PortingOption.AddSingularAsChild, valueExp, packageParent.FileRef, packageParent, true, new RelinkerOptionsPackage(), out var _);
                }
                merEyesP.Save();
            }

            return;

            CaseInsensitiveDictionary<MatContainer> map = new CaseInsensitiveDictionary<MatContainer>();
            foreach (var f in MELoadedFiles.GetFilesLoadedInGame(MEGame.LE2))
            {
                using var pack = MEPackageHandler.UnsafePartialLoad(f.Value, x => x.ClassName is "Material" || x.Parent?.ClassName == "Material");
                foreach (var c in pack.Exports.Where(x => x.ClassName == "Material"))
                {
                    if (map.ContainsKey(c.InstancedFullPath))
                        continue;

                    var container = new MatContainer() { MatIFP = c.InstancedFullPath };
                    var expressions = c.GetProperty<ArrayProperty<ObjectProperty>>("Expressions");
                    if (expressions == null) continue;
                    foreach (var expr in expressions.Select(x => x.ResolveToEntry(pack)).OfType<ExportEntry>())
                    {
                        var parmName = expr.GetProperty<NameProperty>("ParameterName");
                        if (parmName == null)
                        {
                            continue;
                        }
                        if (expr.ClassName == "MaterialExpressionVectorParameter")
                        {
                            container.MatVectors.Add(parmName.Value);
                        }
                        else if (expr.ClassName == "MaterialExpressionScalarParameter")
                        {
                            container.MatScalars.Add(parmName.Value);
                        }
                    }

                    map[c.InstancedFullPath] = container;
                }
            }

            var text = JsonConvert.SerializeObject(map.Values, Formatting.Indented);
            File.WriteAllText(@"C:\users\public\LE2MatInfo.txt", text);

            return;

            //            Debug.WriteLine("Done");

            //// Generate BioP stuff
            var destDir = Path.Combine(LE3Directory.DLCPath, "DLC_MOD_SquadmateCheeseburger", "CookedPCConsole");

            //var bioP = Path.Combine(LE2Directory.CookedPCPath, "BioP_BchLmL.pcc");
            //var destBioP = Path.Combine(destDir, "BioP_BchLmL.pcc");

            //using var bioP1 = MEPackageHandler.OpenMEPackage(bioP);
            //using var destBioP2 = MEPackageHandler.OpenMEPackage(destBioP);

            ///c
            //var world2 = destBioP2.FindExport("TheWorld.PersistentLevel.BioWorldInfo_0");

            //EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink, world1,
            //    destBioP2, world2, true, new RelinkerOptionsPackage(), out _);

            //destBioP2.Save();

            //return;

            string objectDBPath = AppDirectories.GetObjectDatabasePath(MEGame.LE3);
            using FileStream fs = File.OpenRead(objectDBPath);
            var objectDB = ObjectInstanceDB.Deserialize(MEGame.LE3, fs);

            //var sourceFiles = new[] { "BioP_BchLmL.pcc", "BioA_BchLmL.pcc", "BioD_BchLmL.pcc",
            //    // ART
            //    //"BioA_BchLmL_100Landing.pcc",
            //    //"BioA_BchLmL_101Beach.pcc",
            //    //"BioA_BchLmL_102Village.pcc",
            //    //"BioA_BchLmL_103Temple.pcc",
            //    //"BioA_BchLmL_200VillagePath.pcc",
            //    //"BioA_BchLmL_300Ship.pcc",
            //    //"BioA_BchLmL_400PathTwo.pcc",
            //    //"BioA_BchLmL_500VillageTwo.pcc",
            //    //"BioA_BchLmL_600PathThree.pcc",

            //    //"BioA_BchLmL_100BSP.pcc",
            //    //"BioA_BchLmL_200BSP.pcc",
            //    //"BioA_BchLmL_300BSP.pcc",

            //    // DESIGN
            //    "BioD_BchLml_101Ship.pcc",
            //    "BioD_BchLml_102BeachFight.pcc",
            //    "BioD_BchLml_201BeachPath.pcc",
            //    "BioD_BchLml_202Village.pcc",
            //    "BioD_BchLmL_301TemplePath.pcc",
            //    "BioD_BchLmL_302MechFight.pcc",
            //    "BioD_BchLmL_303TempleInterior.pcc",
            //};

            var sourcePersistentFiles = new[] {/* "BioP_BchLmL.pcc", "BioP_BlbGtl.pcc", "BioP_CitAsL.pcc", "BioP_CitHub.pcc", "BioP_EndGm2.pcc",*//* "BioP_HorCr1.pcc", "BioP_JunCvL.pcc", "BioP_KroPrL.pcc", "BioP_N7NorCrash.pcc"*/ /*"BioP_Exp1Lvl4.pcc", "BioP_ArvLvl5.pcc" */ "BioP_OmgHub.pcc", "BioP_SunTlA.pcc", "BioP_ProCer.pcc" };

            foreach (var sourceP in sourcePersistentFiles)
            {
                var sourcePackage = MELoadedFiles.GetFilesLoadedInGame(MEGame.LE2)[sourceP];
                var package = MEPackageHandler.UnsafePartialLoad(sourcePackage, x => false);

                foreach (var sf in ((MEPackage)package).AdditionalPackagesToCook.Concat(new[] { Path.GetFileNameWithoutExtension(sourceP) }))
                {
                    if (sf == "BioP_Global")
                        continue; // Skip

                    //if (sf != "BioD_EndGm2_420CombatZone")
                    //    continue; // Skip

                    var sf2 = sf + ".pcc";
                    // Debug
                    // sf2 = "BioD_CitGrL_120Warehouse1.pcc";

                    var savePath = Path.Combine(destDir, Path.GetFileName(sf2));
                    using var srcPackage =
                        MEPackageHandler.OpenMEPackage(MELoadedFiles.GetFilesLoadedInGame(MEGame.LE2)[sf2]);

                    MEPackageHandler.CreateEmptyLevel(savePath, MEGame.LE3);
                    using var destPackage = MEPackageHandler.OpenMEPackage(savePath);

                    var srcLevelExp = srcPackage.FindExport("TheWorld.PersistentLevel");
                    var srcWorldExp = srcPackage.FindExport("TheWorld");
                    var destLevelExp = destPackage.FindExport("TheWorld.PersistentLevel");

                    var srcLevel = ObjectBinary.From<Level>(srcLevelExp);
                    var srcWorld = ObjectBinary.From<World>(srcWorldExp);

                    // PRE PORTING
                    var srcExtraRefs = srcWorld.ExtraReferencedObjects;

                    srcWorld.ExtraReferencedObjects = srcExtraRefs.Where(x =>
                    {
                        if (x == 0) return false;
                        var entry = srcPackage.GetEntry(x);
                        if (!LE3UnrealObjectInfo.Classes.ContainsKey(entry.ClassName))
                        {
                            Debug.WriteLine($"Not porting missing class {entry.ClassName} ({entry.ObjectName})");
                            return false;
                        }

                        if (entry.IsA("SequenceObject"))
                        {
                            return false; // We are not porting any sequencing.
                        }
                        return true;
                    }).ToArray();
                    srcWorldExp.WriteBinary(srcWorld);

                    var srcActors = srcLevel.Actors;
                    srcLevel.Actors.ReplaceAll(srcActors.Where(x =>
                    {
                        if (x == 0) return false;
                        var entry = srcPackage.GetEntry(x);
                        if (entry.IsA("Pawn"))
                            return false;
                        if (entry.IsA("BioStage"))
                            return false;
                        if (entry.IsA("BioBaseSquadDesign"))
                            return false;
                        if (entry.IsA("PrefabInstance")) // This might cut out some stuff...
                            return false;
                        return true;
                    }).ToList());
                    srcLevelExp.WriteBinary(srcLevel);

                    foreach (var exp in srcPackage.Exports.Where(x => x.ClassName == "Prefab"))
                    {
                        exp.RemoveProperty("PrefabSequence"); // This has script which is probably game specific.
                    }

                    var bwi = srcPackage.Exports.FirstOrDefault(x => x.ClassName == @"BioWorldInfo");
                    bwi.RemoveProperty("ClientDestroyedActorContent");

                    var autoPersist = bwi.GetProperty<ArrayProperty<ObjectProperty>>("m_AutoPersistentObjects");
                    if (autoPersist != null)
                    {
                        autoPersist.RemoveAll(x =>
                        {
                            if (x.Value == 0) return true;
                            var entry = srcPackage.GetEntry(x.Value);

                            if (!LE3UnrealObjectInfo.Classes.ContainsKey(entry.ClassName))
                            {
                                Debug.WriteLine($"Not porting missing class {entry.ClassName} ({entry.ObjectName})");
                                return true;
                            }

                            // Do not port any of this crap.
                            if (entry.IsA("SequenceObject"))
                                return true;
                            if (entry.IsA("BioStage"))
                                return true;

                            return false;
                        });

                        bwi.WriteProperty(autoPersist);
                    }

                    // Clear sequences
                    var seq = srcPackage.FindExport(@"TheWorld.PersistentLevel.Main_Sequence");
                    seq?.WriteProperty(new ArrayProperty<ObjectProperty>("SequenceObjects")); // Do not port these

                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink,
                        srcLevelExp, destPackage, destLevelExp, true,
                        new RelinkerOptionsPackage()
                        { PortImportsMemorySafe = true, IsCrossGame = true, TargetGameDonorDB = objectDB }, out _);

                    var destWorld = destPackage.FindExport("TheWorld");


                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingularWithRelink,
                        srcWorldExp, destPackage, destWorld, true,
                        new RelinkerOptionsPackage()
                        { PortImportsMemorySafe = true, IsCrossGame = true, TargetGameDonorDB = objectDB }, out _);

                    ConvertCover(srcPackage, destPackage);

                    destPackage.Save();
                }
            }

            Debug.WriteLine("Done with MScanner()");
            return;

            //var sfxGameME3 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, @"SFXGame.pcc"));
            //var sfxGameLE3 = MEPackageHandler.OpenMEPackage(Path.Combine(LE3Directory.CookedPCPath, @"SFXGame.pcc"));

            //FileLib le3Lib = new FileLib(sfxGameLE3);
            //le3Lib.Initialize();

            //FileLib me3Lib = new FileLib(sfxGameME3);
            //me3Lib.Initialize();

            //foreach (var func in sfxGameLE3.Exports.Where(x => x.ClassName == "Function"))
            //{
            //    var matchingFuncME3 = sfxGameME3.FindExport(func.InstancedFullPath);
            //    if (matchingFuncME3 != null)
            //    {
            //        var le3Script = UnrealScriptCompiler.DecompileExport(func, le3Lib);
            //        var me3Script = UnrealScriptCompiler.DecompileExport(matchingFuncME3, me3Lib);
            //        if (me3Script.text != le3Script.text)
            //        {
            //            Debug.WriteLine($"Script differs: {matchingFuncME3.InstancedFullPath}");
            //        }
            //    }
            //}
            //Debug.WriteLine("Done");
            //return;


            // Strip cached collision data
            //foreach (var v in pe.Pcc.Exports.Where(x => x.IsA("BrushComponent")))
            //{
            //    var brush = ObjectBinary.From<BrushComponent>(v);
            //    brush.CachedPhysBrushData = new KCachedConvexData(); // Blank it out
            //    v.WriteBinary(brush);
            //    v.RemoveProperty("CachedPhysBrushDataVersion");
            //}

            //return;
            Debug.WriteLine("ME1");

            GenerateAllMemoryPathedObjects(MEGame.ME1);
            Debug.WriteLine("LE1");

            GenerateAllMemoryPathedObjects(MEGame.LE1);
            Debug.WriteLine("Done");
            return;
            SortedSet<string> configNames = new SortedSet<string>();
            foreach (var f in MELoadedFiles.GetFilesLoadedInGame(MEGame.LE1))
            {
                using var pack = MEPackageHandler.UnsafePartialLoad(f.Value, x => x.ClassName == "Class"); // Only load class files
                foreach (var c in pack.Exports.Where(x => x.ClassName == "Class"))
                {
                    var uclass = ObjectBinary.From<UClass>(c);
                    configNames.Add("Bio" + uclass.ClassConfigName);
                    Debug.WriteLine($"{c.ObjectName}: {uclass.ClassConfigName}");
                }
            }

            Debug.WriteLine("ALL ITEMS:");
            foreach (var v in configNames)
            {
                Debug.WriteLine(v);
            }

            //using var package = MEPackageHandler.OpenMEPackage(@"Y:\ModLibrary\ME3\Customizable EDI Armor\DLC_MOD_CustomizableEDIArmor\CookedPCConsole\BIOG_HMF_ARM_SHP_R_X.pcc");


            //var inputFilesDir = @"C:\Program Files (x86)\Mass Effect\DLC\DLC_Vegas\CookedPC\Maps\PRC2AA";
            //var destFileDir = @"Y:\ModLibrary\LE1\V Test\ModdedSource\PRC2AA";
            //var outDir = @"Y:\ModLibrary\LE1\V Test\ModdedSource\PRC2-LOCUpdate";

            //var langs = new[] { "RA", "RU" };
            //foreach (var inputFile in Directory.GetFiles(inputFilesDir))
            //{
            //    var inFileName = Path.GetFileName(inputFile);
            //    var matchingVtestFile = Directory.GetFiles(destFileDir, "*", SearchOption.AllDirectories)
            //        .FirstOrDefault(x => Path.GetFileName(x) == inFileName);
            //    if (matchingVtestFile == null)
            //        continue;
            //    using var rusP = MEPackageHandler.OpenMEPackage(inputFile);
            //    using var vtestP = MEPackageHandler.OpenMEPackage(matchingVtestFile);

            //    foreach (var rusTlkSet in rusP.Exports.Where(x => x.ClassName == "BioTlkFileSet"))
            //    {
            //        var vtestTlkSet = vtestP.FindExport(rusTlkSet.InstancedFullPath);

            //        var rusBin = ObjectBinary.From<BioTlkFileSet>(rusTlkSet);
            //        var vtestBin = ObjectBinary.From<BioTlkFileSet>(vtestTlkSet);

            //        foreach (var lang in langs)
            //        {
            //            var newLangInfo = rusBin.TlkSets[lang];
            //            // Male
            //            var maleExport = rusP.GetUExport(newLangInfo.Male);
            //            EntryExporter.ExportExportToPackage(maleExport, vtestP, out var maleEntry);
            //            // Female
            //            var femaleExport = rusP.GetUExport(newLangInfo.Female);
            //            EntryExporter.ExportExportToPackage(femaleExport, vtestP, out var femaleEntry);

            //            vtestBin.TlkSets[lang] = new BioTlkFileSet.BioTlkSet() {Female = femaleEntry.UIndex, Male = maleEntry.UIndex};
            //        }
            //        vtestTlkSet.WriteBinary(vtestBin);
            //    }

            //    var outPath = Path.Combine(outDir, inFileName);
            //    vtestP.Save(outPath);
            //}
            //return;

            CompareVerticeCountBetweenGames(pe);
            return;

            // Pain and suffering
            var inputCookedISB = @"X:\Downloads\ChocolateLabStuff\VTEST\output\vtest.isb";
            using var ms = new MemoryStream(File.ReadAllBytes(inputCookedISB));
            using var outStr = new MemoryStream();
            //var riff = ms.ReadStringASCII(4);
            //var isbSize = ms.ReadInt32();
            //var isbf = ms.ReadStringASCII(4); 

            ms.CopyToEx(outStr, 12);

            // Strip 'data' chunk
            long currentListSizeOffset = -1;
            while (ms.Position + 1 < ms.Length)
            {
                var chunk = ms.ReadStringASCII(4);
                var len = ms.ReadInt32();
                ms.Position -= 8;

                if (chunk == "data")
                {
                    Debug.WriteLine($"Skip data len {len}");
                    ms.Position += len + 8;
                    long returnPos = outStr.Position;
                    if (currentListSizeOffset < 0)
                    {
                        throw new Exception("WRONG PARSING!");
                    }

                    outStr.Position = currentListSizeOffset + 4;
                    var size = outStr.ReadInt32();
                    outStr.Position -= 4;
                    outStr.WriteInt32(size - (len + 8)); // Gut 'data'
                    outStr.Position = returnPos;
                }
                else if (chunk == "LIST")
                {
                    currentListSizeOffset = outStr.Position;
                    // LIST / size / samp
                    ms.CopyToEx(outStr, 12); // we will update the size later
                }
                else
                {
                    Debug.WriteLine($"Copy {chunk} {len}");
                    ms.CopyToEx(outStr, len + 8);
                }
            }

            outStr.WriteToFile(@"X:\Downloads\ChocolateLabStuff\VTEST\output\bsnwsd_vtest.isb");


            //var pl = pe.Pcc.FindExport("TheWorld.PersistentLevel");
            //var bin = ObjectBinary.From<Level>(pl);
            //foreach (var ti in bin.TextureToInstancesMap.ToList())
            //{
            //    var texEntry = pe.Pcc.GetEntry(ti.Key);
            //    ExportEntry tex = texEntry as ExportEntry;
            //    if (tex == null)
            //    {
            //        tex = EntryImporter.ResolveImport(texEntry as ImportEntry);
            //    }

            //    var texBin = ObjectBinary.From<UTexture2D>(tex);
            //    var neverSTream = tex.GetProperty<BoolProperty>("NeverStream");
            //    if (texBin.Mips.Count(x => x.StorageType != StorageTypes.empty) <= 6 || neverSTream is {Value: true})
            //    {
            //        Debug.WriteLine($@"Removing item {tex.InstancedFullPath}");
            //        bin.TextureToInstancesMap.Remove(ti);
            //        //Debugger.Break();
            //    }
            //}
            //pl.WriteBinary(bin);
            return;
            #region GlobalShaderCache.bin parsing

            /*var infile = @"D:\Steam\steamapps\common\Mass Effect Legendary Edition\Game\ME1\BioGame\CookedPCConsole\GlobalShaderCache-PC-D3D-SM5.bin"; ;
            var stream = new MemoryStream(File.ReadAllBytes(infile));
            Debug.WriteLine($"Magic: {stream.ReadStringASCII(4)}");
            Debug.WriteLine($"Unreal version: {stream.ReadInt32()}");
            Debug.WriteLine($"Licensee version: {stream.ReadInt32()}");

            var shaderModelNum = stream.ReadByte();
            Debug.WriteLine($"Shader model version?: {shaderModelNum}");


            // CRC MAP
            int crcMapCount = stream.ReadInt32();
            Debug.WriteLine($"CRC MAP COUNT: {crcMapCount}");
            for (int i = 0; i < crcMapCount; i++)
            {
                Debug.WriteLine($"String[{i}]: {stream.ReadUnrealString()}");
                Debug.WriteLine($"CRC[{i}]?: 0x{(stream.ReadInt32()):X8}");
            }

            // Some Zero
            Debug.WriteLine($"Some zero: {stream.ReadInt32()}");

            // Shaders
            var shaderCount = stream.ReadInt32();
            Debug.WriteLine($"Shader file count: {shaderCount}");
            for (int i = 0; i < shaderCount; i++)
            {
                if (i == 348)
                    Debug.WriteLine("ok");
                Debug.WriteLine($"Shader name[{i}]: {stream.ReadUnrealString()}");
                Debug.WriteLine($"Shader guid[{i}]: {stream.ReadGuid()}");
                var nextShaderStart = stream.ReadInt32();
                Debug.WriteLine($"Shader next offset[{i}]: 0x{nextShaderStart:X8}");
                if (i == 348)
                {
                    Debug.WriteLine($"SPECIAL UNKNOWN: {stream.ReadUInt32()}");
                    Debug.WriteLine($"SPECIAL UNKNOWN: {stream.ReadUInt16()}");
                }
                else if (i == 349)
                {
                    // No idea what this is
                    Debug.WriteLine("UNKNOWN STUFF BLOCK");
                    stream.Skip(0x30);
                }

                Debug.WriteLine($"Shader Platform ID[{i}]: 0x{stream.ReadByte():X2}");
                Debug.WriteLine($"Shader Frequency[{i}]: 0x{stream.ReadByte():X2}");
                var shaderFileSize = stream.ReadInt32();
                Debug.WriteLine($"Shader file[{i}]: 0x{stream.Position:X8} to 0x{nextShaderStart:X8} ({shaderFileSize} bytes)");
                stream.Skip(shaderFileSize);
                Debug.WriteLine($"Shader parameter map crc[{i}]: {stream.ReadInt32():X8}");
                Debug.WriteLine($"Shader guid clone[{i}]: {stream.ReadGuid()}");
                Debug.WriteLine($"Shader name clone[{i}]: {stream.ReadUnrealString()}");

                stream.Position = nextShaderStart;
            }

            var countAgain = stream.ReadInt32();
            for (int i = 0; i < countAgain; i++)
            {
                Debug.WriteLine($"Shader name/guid[{i}]: {stream.ReadUnrealString()} {stream.ReadGuid()}");
            }

            Debug.WriteLine($"Position: 0x{(stream.Position):X8}");


            return;*/

            #endregion

            //just dump whatever shit you want to find here
            ConcurrentDictionary<string, string> dupes = new ConcurrentDictionary<string, string>();
            Parallel.ForEach(MELoadedFiles.GetOfficialFiles(MEGame.LE1 /*, MEGame.LE2, MEGame.LE3*/), filePath =>
            {
                {
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                    Dictionary<string, string> expMap = new();

                    foreach (ExportEntry export in pcc.Exports)
                    {
                        if (expMap.TryGetValue(export.InstancedFullPath, out _))
                        {
                            Debug.WriteLine($"FOUND A DUPLICATE: {export.InstancedFullPath} in {Path.GetFileName(filePath)}");
                            dupes[export.InstancedFullPath] = filePath;
                        }

                        expMap[export.InstancedFullPath] = export.InstancedFullPath;

                    }
                }
            });

            foreach (var duplicate in dupes)
            {
                Debug.WriteLine($"DUPLICATE IFP: {duplicate.Key} in {duplicate.Value}");
            }
        }

        private static void ConvertCover(IMEPackage srcPackage, IMEPackage destPackage)
        {
            var levelDestExp = destPackage.FindExport("TheWorld.PersistentLevel");
            var destLevel = ObjectBinary.From<Level>(levelDestExp);
            var sourceLevel = ObjectBinary.From<Level>(srcPackage.FindExport("TheWorld.PersistentLevel"));

            // Enumerate the CoverLink chain
            if (destLevel.CoverListStart <= 0)
                return; // Nothing

            // Build the CoverLinkRefs list.
            Dictionary<int, int> uindexToCoverRefIdx = new Dictionary<int, int>(); // used to map UIndex -> position in the list
            var currentCoverLink = destPackage.GetUExport(destLevel.CoverListStart);

            while (currentCoverLink != null)
            {
                uindexToCoverRefIdx[currentCoverLink.UIndex] = destLevel.CoverLinkRefs.Count; // Cache for lookup later
                destLevel.CoverLinkRefs.Add(currentCoverLink.UIndex);

                // Go to the next node
                var ncl = currentCoverLink.GetProperty<ObjectProperty>("NextCoverLink");
                if (ncl == null)
                {
                    currentCoverLink = null; // Nothing left to do
                }
                else
                {
                    currentCoverLink = destPackage.GetUExport(ncl.Value);
                }
            }

            // Enumerate each cover and build the data
            var sourceCoverLink = srcPackage.GetUExport(sourceLevel.CoverListStart);

            Dictionary<string, uint> coverRefPairLookup = new Dictionary<string, uint>(); // "CoverLinkRefsIndex-SlotIdx" -> Index
            while (sourceCoverLink != null)
            {
                var matchingDestCoverLink = destPackage.FindExport(sourceCoverLink.InstancedFullPath);

                var sourceSlots = sourceCoverLink.GetProperty<ArrayProperty<StructProperty>>("Slots");
                var destSlots = matchingDestCoverLink.GetProperty<ArrayProperty<StructProperty>>("Slots");

                // This code is for LE2 -> LE3
                if (sourceCoverLink.Game == MEGame.LE2)
                {
                    for (int i = 0; i < sourceSlots.Count; i++)
                    {
                        var sourceSlot = sourceSlots[i];
                        var destSlot = destSlots[i];

                        #region FIRELINKS
                        {
                            var sourceFireLinks = sourceSlot.GetProp<ArrayProperty<StructProperty>>("FireLinks");
                            var destFireLinks = destSlot.GetProp<ArrayProperty<StructProperty>>("FireLinks");
                            foreach (var sourceFireLink in sourceFireLinks)
                            {
                                var targetActor = sourceFireLink.GetProp<StructProperty>("TargetActor");
                                var actor = targetActor.GetProp<ObjectProperty>("Actor");
                                // Todo: Support cross-level refs via GUID
                                var slotIdx = (uint)targetActor.GetProp<IntProperty>("SlotIdx").Value;

                                uint packedCoverPairRefAndDynamicLinkInfo = 0;

                                if (actor.Value != 0)
                                {
                                    var actorExp = srcPackage.GetUExport(actor.Value);
                                    var destActorExp = destPackage.FindExport(actorExp.InstancedFullPath);

                                    var clrIdx =
                                        destLevel.CoverLinkRefs.IndexOf(destActorExp.UIndex); // Index into CoverLinkRefs
                                    var lookupStr = $"{clrIdx}-{slotIdx}";


                                    if (!coverRefPairLookup.TryGetValue(lookupStr, out var covRefIdx))
                                    {
                                        // Cache result for faster lookup
                                        coverRefPairLookup[lookupStr] = (uint)destLevel.CoverIndexPairs.Count;
                                        if (covRefIdx == 32)
                                            Debugger.Break();
                                        destLevel.CoverIndexPairs.Add(new CoverIndexPair()
                                        { CoverIndexIdx = (uint)clrIdx, SlotIdx = (byte)slotIdx });
                                    }

                                    // Set the cover reference
                                    covRefIdx &= 0x0000FFFF;
                                    packedCoverPairRefAndDynamicLinkInfo &= ~(0x0000FFFFu);
                                    packedCoverPairRefAndDynamicLinkInfo |= covRefIdx;

                                    // Set the dynamic link info
                                    var dynamicLinkInfoIndex =
                                        (uint)sourceFireLink.GetProp<ByteProperty>("DynamicLinkInfoIndex").Value;
                                    dynamicLinkInfoIndex &= 0x0000FFFF;
                                    packedCoverPairRefAndDynamicLinkInfo &= ~(0xFFFF0000);
                                    packedCoverPairRefAndDynamicLinkInfo |= (dynamicLinkInfoIndex << 16);
                                    //}

                                    // Convert the 'Items' to byte-packed Interactions
                                    ArrayProperty<ByteProperty> interactions =
                                        new ArrayProperty<ByteProperty>("Interactions");
                                    foreach (var item in sourceFireLink.GetProp<ArrayProperty<StructProperty>>("Items"))
                                    {
                                        var srcType = item.GetProp<EnumProperty>("SrcType");
                                        var srcAction = item.GetProp<EnumProperty>("SrcAction");
                                        var destType = item.GetProp<EnumProperty>("DestType");
                                        var destAction = item.GetProp<EnumProperty>("DestAction");

                                        byte packedByte = 0;

                                        // Pack Source
                                        if (srcType.Value == "CT_MidLevel") packedByte |= (1 << 0); // 0 = Standing
                                        if (srcAction.Value == "CA_LeanLeft") packedByte |= (1 << 1);
                                        else if (srcAction.Value == "CA_LeanRight") packedByte |= (1 << 2);
                                        else if (srcAction.Value == "CA_LeanPopUp")
                                            packedByte |= (1 << 3); // No bits set 1-3: CA_Default

                                        // Pack Dest
                                        if (destType.Value == "CT_MidLevel") packedByte |= (1 << 4); // 0 = Standing
                                        if (destAction.Value == "CA_LeanLeft") packedByte |= (1 << 5);
                                        else if (destAction.Value == "CA_LeanRight") packedByte |= (1 << 6);
                                        else if (destAction.Value == "CA_LeanPopUp")
                                            packedByte |= (1 << 7); // No bits set 1-3: CA_Default

                                        interactions.Add(new ByteProperty(packedByte));
                                    }


                                    // Generate new struct
                                    PropertyCollection newFireLinkProps = new PropertyCollection();
                                    newFireLinkProps.Add(interactions);
                                    newFireLinkProps.Add(new IntProperty((int)packedCoverPairRefAndDynamicLinkInfo,
                                        "PackedProperties_CoverPairRefAndDynamicInfo"));
                                    newFireLinkProps.Add(sourceFireLink.GetProp<BoolProperty>("bFallbackLink"));
                                    newFireLinkProps.Add(sourceFireLink.GetProp<BoolProperty>("bDynamicIndexInited"));

                                    destFireLinks.Add(new StructProperty("FireLink", newFireLinkProps, isImmutable: true));
                                }
                                else
                                {
                                    Debug.WriteLine("CROSS-LEVEL FIRELINK FOUND - TODO");
                                }
                            }
                        }

                        #endregion

                        #region EXPOSED FIRELINKS
                        {
                            var sourceExposedLinks = sourceSlot.GetProp<ArrayProperty<StructProperty>>("ExposedFireLinks");
                            var destExposedLinks = destSlot.GetProp<ArrayProperty<IntProperty>>("ExposedCoverPackedProperties");
                            foreach (var sourceFireLink in sourceExposedLinks)
                            {
                                var targetActor = sourceFireLink.GetProp<StructProperty>("TargetActor");
                                var actor = targetActor.GetProp<ObjectProperty>("Actor");
                                // Todo: Support cross-level refs via GUID
                                var slotIdx = (uint)targetActor.GetProp<IntProperty>("SlotIdx").Value;

                                uint packedExposureData = 0;

                                if (actor.Value != 0)
                                {
                                    var actorExp = srcPackage.GetUExport(actor.Value);
                                    var destActorExp = destPackage.FindExport(actorExp.InstancedFullPath);

                                    var clrIdx = destLevel.CoverLinkRefs.IndexOf(destActorExp
                                            .UIndex); // Index into CoverLinkRefs
                                    var lookupStr = $"{clrIdx}-{slotIdx}";

                                    if (!coverRefPairLookup.TryGetValue(lookupStr, out var covRefIdx))
                                    {
                                        // Cache result for faster lookup
                                        coverRefPairLookup[lookupStr] = (uint)destLevel.CoverIndexPairs.Count;
                                        destLevel.CoverIndexPairs.Add(new CoverIndexPair()
                                        { CoverIndexIdx = (uint)clrIdx, SlotIdx = (byte)slotIdx });
                                    }

                                    // Set the cover reference
                                    covRefIdx &= 0x0000FFFF;
                                    packedExposureData &= ~(0x0000FFFFu);
                                    packedExposureData |= covRefIdx;

                                    // Set the dynamic link info
                                    var exposureScale = (uint)sourceFireLink.GetProp<ByteProperty>("ExposedScale").Value;
                                    exposureScale &= 0x0000FFFF;
                                    packedExposureData &= ~(0xFFFF0000);
                                    packedExposureData |= (exposureScale << 16);
                                    //}


                                    // Generate new struct

                                    destExposedLinks.Add(new IntProperty((int)packedExposureData));
                                }
                                else
                                {
                                    Debug.WriteLine("CROSS-LEVEL EXPOSED FIRELINK FOUND - TODO");
                                }
                            }
                        }

                        #endregion

                        #region DANGER LINKS
                        {
                            var sourceDangerLinks = sourceSlot.GetProp<ArrayProperty<StructProperty>>("DangerLinks");
                            var destDangerLinks = destSlot.GetProp<ArrayProperty<IntProperty>>("DangerCoverPackedProperties");
                            foreach (var sourceDangerLink in sourceDangerLinks)
                            {
                                var targetNav = sourceDangerLink.GetProp<StructProperty>("DangerNav");
                                var actor = targetNav.GetProp<ObjectProperty>("Actor");
                                // Todo: Support cross-level refs via GUID

                                uint packedDangerData = 0;

                                if (actor.Value != 0)
                                {
                                    var actorExp = srcPackage.GetUExport(actor.Value);
                                    var destActorExp = destPackage.FindExport(actorExp.InstancedFullPath);

                                    var navRefIdx = (uint)destLevel.NavRefs.IndexOf(destActorExp
                                            .UIndex); // Index into NavRefs

                                    if (navRefIdx == uint.MaxValue) // '-1'
                                    {
                                        // Add to list
                                        navRefIdx = (uint)destLevel.NavRefs.Count;
                                        destLevel.NavRefs.Add(destActorExp.UIndex);
                                    }

                                    // Set the nav reference
                                    navRefIdx &= 0x0000FFFF;
                                    packedDangerData &= ~(0x0000FFFFu);
                                    packedDangerData |= navRefIdx;

                                    // Set the danger cost
                                    var dangercost = (uint)sourceDangerLink.GetProp<IntProperty>("DangerCost").Value;
                                    dangercost &= 0x0000FFFF;
                                    packedDangerData &= ~(0xFFFF0000);
                                    packedDangerData |= (dangercost << 16);
                                    //}

                                    // Generate new struct
                                    destDangerLinks.Add(new IntProperty((int)packedDangerData));
                                }
                                else
                                {
                                    Debug.WriteLine("CROSS-LEVEL EXPOSED DANGER LINK FOUND - TODO");
                                }
                            }
                        }
                        #endregion

                    }

                    matchingDestCoverLink.WriteProperty(destSlots);
                }


                // Go to the next node
                var ncl = sourceCoverLink.GetProperty<ObjectProperty>("NextCoverLink");
                if (ncl == null)
                {
                    sourceCoverLink = null; // Nothing left to do
                }
                else
                {
                    sourceCoverLink = srcPackage.GetUExport(ncl.Value);
                }
            }

            levelDestExp.WriteBinary(destLevel);
        }

        private static void GenerateAllMemoryPathedObjects(MEGame game)
        {
            SortedSet<string> objects = new SortedSet<string>();

            var packages = MELoadedFiles.GetFilesLoadedInGame(game);

            foreach (var p in packages)
            {
                var pack = MEPackageHandler.UnsafePartialLoad(p.Value, x => false); // tables only
                foreach (var e in pack.Exports)
                {
                    if (e.InstancedFullPath.StartsWith("TheWorld"))
                        continue; // Do not do these

                    if (e.IsForcedExport)
                    {
                        objects.Add(e.FullPath);
                    }
                    else
                    {
                        objects.Add($"{e.FileRef.FileNameNoExtension}.{e.FullPath}");
                    }
                }
            }

            var objectsSorted = objects.ToList();
            objectsSorted.Sort();
            File.WriteAllLines($@"C:\users\public\{game}-memorypaths.txt", objectsSorted);
        }

        public static void OrganizeParticleSystems(PackageEditorWindow pe)
        {
            if (pe.Pcc == null)
                return;

            foreach (var ps in pe.Pcc.Exports.Where(x => x.ClassName == "ParticleSystem"))
            {
                var emitters = ps.GetProperty<ArrayProperty<ObjectProperty>>("Emitters");
                foreach (var emitter in emitters.Select(x => x.ResolveToEntry(pe.Pcc)).OfType<ExportEntry>())
                {
                    var lodLevels = emitter.GetProperty<ArrayProperty<ObjectProperty>>("LODLevels");
                    if (lodLevels == null)
                        continue; // bCookedOut
                    foreach (var lodLevel in lodLevels.Select(x => x.ResolveToEntry(pe.Pcc)).OfType<ExportEntry>())
                    {
                        lodLevel.idxLink = emitter.UIndex;
                        var modules = lodLevel.GetProperty<ArrayProperty<ObjectProperty>>("Modules");
                        foreach (var module in modules.Select(x => x.ResolveToEntry(pe.Pcc)).OfType<ExportEntry>())
                        {
                            module.idxLink = lodLevel.UIndex;
                        }

                        foreach (var objProp in lodLevel.GetProperties().OfType<ObjectProperty>().Select(x => x.ResolveToEntry(pe.Pcc)).OfType<ExportEntry>())
                        {
                            objProp.idxLink = lodLevel.UIndex;
                        }
                    }
                }
            }
        }

        public static void ImportUDKTerrain(PackageEditorWindow pe)
        {
            if (pe.Pcc == null)
                return;

            var localTerrain = pe.Pcc.Exports.FirstOrDefault(x => x.ClassName == "Terrain");
            if (localTerrain == null)
            {
                MessageBox.Show("The local file must have a terrain.");
                return;
            }

            OpenFileDialog d = new OpenFileDialog
            {
                Title = "Select UDK file with terrain",
                Filter = "*.udk|*.udk",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            if (d.ShowDialog() == true)
            {
                using var udkP = MEPackageHandler.OpenUDKPackage(d.FileName);
                var udkTerrain = udkP.Exports.FirstOrDefault(x => x.ClassName == "Terrain");
                if (udkTerrain == null)
                {
                    MessageBox.Show("Selected UDK file doesn't contain a Terrain.");
                    return;
                }

                ImportUDKTerrainData(udkTerrain, localTerrain);
            }
        }

        public static void ImportUDKTerrainData(ExportEntry udkTerrain, ExportEntry targetTerrain, bool removeExistingComponents = true)
        {
            // Binary (Terrain)
            var udkBin = ObjectBinary.From<Terrain>(udkTerrain);
            var destBin = ObjectBinary.From<Terrain>(targetTerrain);
            destBin.Heights = udkBin.Heights;
            destBin.InfoData = udkBin.InfoData;
            destBin.CachedDisplacements = new byte[udkBin.Heights.Length];
            targetTerrain.WriteBinary(destBin);

            // Properties (Terrain)
            var terrainProps = targetTerrain.GetProperties();
            var udkProps = udkTerrain.GetProperties();
            terrainProps.RemoveNamedProperty("DrawScale3D");
            var udkDS3D = udkProps.GetProp<StructProperty>("DrawScale3D");
            if (udkDS3D != null)
            {
                terrainProps.AddOrReplaceProp(udkDS3D);
            }

            terrainProps.RemoveNamedProperty("DrawScale");
            var udkDS = udkProps.GetProp<FloatProperty>("DrawScale");
            if (udkDS != null)
            {
                terrainProps.AddOrReplaceProp(udkDS);
            }

            terrainProps.RemoveNamedProperty("Location");
            var loc = udkProps.GetProp<StructProperty>("Location");
            if (loc != null)
            {
                terrainProps.AddOrReplaceProp(loc);
            }

            // All Ints
            terrainProps.RemoveAll(x => x is IntProperty);
            terrainProps.AddRange(udkProps.Where(x => x is IntProperty));

            // Components
            if (removeExistingComponents)
            {
                var components = terrainProps.GetProp<ArrayProperty<ObjectProperty>>("TerrainComponents");
                EntryPruner.TrashEntries(targetTerrain.FileRef, components.Select(x => x.ResolveToEntry(targetTerrain.FileRef))); // Trash the components
                components.Clear();

                // Port over the UDK ones
                var udkComponents = udkTerrain.GetProperty<ArrayProperty<ObjectProperty>>("TerrainComponents");
                foreach (var tc in udkComponents)
                {
                    var entry = tc.ResolveToEntry(udkTerrain.FileRef);
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, entry, targetTerrain.FileRef, targetTerrain, true, new RelinkerOptionsPackage(), out var portedComp);
                    components.Add(new ObjectProperty(portedComp.UIndex));
                }
            }

            targetTerrain.WriteProperties(terrainProps);
        }

        public static void ExpertTerrainDataToUDK(PackageEditorWindow pe)
        {
            {
                OpenFileDialog d = new OpenFileDialog
                {
                    Title = "Select UDK file to export to (MUST HAVE TERRAIN ALREADY)",
                    Filter = "*.udk|*.udk",
                    CustomPlaces = AppDirectories.GameCustomPlaces
                };
                if (d.ShowDialog() != true)
                    return;
                var udkDestFile = d.FileName;
                using var udkP = MEPackageHandler.OpenUDKPackage(udkDestFile);

                OpenFileDialog f = new OpenFileDialog { Title = "Select source file to export from", Filter = GameFileFilters.OpenFileFilter, CustomPlaces = AppDirectories.GameCustomPlaces };
                if (f.ShowDialog() != true)
                    return;

                var sourcePackage = f.FileName;
                using var sourceP = MEPackageHandler.OpenMEPackage(sourcePackage);

                var udkT = udkP.Exports.FirstOrDefault(x => x.ClassName == "Terrain");

                // Might need changed if files have multiple terrains... is that normal?
                var sourceT = sourceP.Exports.FirstOrDefault(x => x.ClassName == "Terrain");

                var udkTerrBin = ObjectBinary.From<Terrain>(udkT);
                var sourceTerrBin = ObjectBinary.From<Terrain>(sourceT);

                udkTerrBin.Heights = sourceTerrBin.Heights;
                udkTerrBin.InfoData = sourceTerrBin.InfoData;
                udkT.WriteBinary(udkTerrBin);

                var udkComponents = udkT.GetProperty<ArrayProperty<ObjectProperty>>("TerrainComponents");
                var le1Components = sourceT.GetProperty<ArrayProperty<ObjectProperty>>("TerrainComponents");
                for (int i = 0; i < udkComponents.Count; i++)
                {
                    var udkComp = udkComponents[i].ResolveToEntry(udkP) as ExportEntry;
                    var le1Comp = le1Components[i].ResolveToEntry(sourceP) as ExportEntry;

                    var udkCompBin = ObjectBinary.From<TerrainComponent>(udkComp);
                    var le1CompBin = ObjectBinary.From<TerrainComponent>(le1Comp);

                    udkCompBin.CollisionVertices = le1CompBin.CollisionVertices;
                    udkComp.WriteBinary(udkCompBin);
                }


                udkP.Save(udkDestFile);
            }
        }

        // This method depended on VTest which was moved to CrossGenV. It may be re-instated later if we ever want to make mako maps again
        /*
        public static void MakeMakoLevel(PackageEditorWindow pe)
        {
            //if (pe.Pcc == null)
            //    return;

            //var localTerrain = pe.Pcc.Exports.FirstOrDefault(x => x.ClassName == "Terrain");
            //if (localTerrain == null)
            //{
            //    MessageBox.Show("There's no terrain export in this file!", "Terrain required");
            //    return;
            //}

            var outputFile = Path.Combine(LE1Directory.CookedPCPath, @"BIOA_TERRAINTEST.pcc");
            MEPackageHandler.CreateEmptyLevel(outputFile, MEGame.LE1);
            using var destPackage = MEPackageHandler.OpenMEPackage(outputFile);
            var destLevel = destPackage.FindExport("TheWorld.PersistentLevel");

            // Select file to donate from
            var preselected = @"B:\SteamLibrary\steamapps\common\Mass Effect Legendary Edition\Game\ME1\BioGame\CookedPCConsole\BIOA_ICE20_08_ART.pcc";

            OpenFileDialog d = new OpenFileDialog { Title = "Select donor file with terrain", Filter = "*.pcc|*.pcc" };
            if (preselected == null && d.ShowDialog() == false)
                return;
            using var donorFile = MEPackageHandler.OpenMEPackage(preselected ?? d.FileName);
            var donorTerrain = donorFile.Exports.FirstOrDefault(x => x.ClassName == "Terrain");
            if (donorTerrain == null)
            {
                MessageBox.Show("There's no terrain export in the selected file!", "Terrain required");
                return;
            }

            // Port in the donor terrain to begin with
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, donorTerrain, destPackage, destLevel, true, new RelinkerOptionsPackage(), out var destTerrainEntry);
            var destTerrain = (ExportEntry)destTerrainEntry;

            // Overwrite the terrain with our data
            var inputPath = @"B:\Documents\SpiralMountain.udk";
            using var udkP = MEPackageHandler.OpenUDKPackage(inputPath);
            var udkTerrain = udkP.FindExport("TheWorld.PersistentLevel.Terrain_1");

            // Binary (Terrain)
            var udkBin = ObjectBinary.From<Terrain>(udkTerrain);
            var destBin = ObjectBinary.From<Terrain>(destTerrain);
            destBin.Heights = udkBin.Heights;
            destBin.InfoData = udkBin.InfoData;
            destBin.CachedDisplacements = new byte[udkBin.Heights.Length];
            destTerrain.WriteBinary(destBin);

            // Properties (Terrain)
            var terrainProps = destTerrain.GetProperties();
            var udkProps = udkTerrain.GetProperties();
            terrainProps.RemoveNamedProperty("DrawScale3D");
            terrainProps.RemoveAll(x => x is IntProperty);
            terrainProps.AddRange(udkProps.Where(x => x is IntProperty));
            terrainProps.RemoveNamedProperty("Location");
            var loc = udkProps.GetProp<StructProperty>("Location");
            if (loc != null)
            {
                terrainProps.AddOrReplaceProp(loc);
            }


            // Components
            var components = terrainProps.GetProp<ArrayProperty<ObjectProperty>>("TerrainComponents");
            EntryPruner.TrashEntries(destPackage, components.Select(x => x.ResolveToEntry(destPackage))); // Trash the components
            components.Clear();

            // Port over the UDK ones
            var udkComponents = udkTerrain.GetProperty<ArrayProperty<ObjectProperty>>("TerrainComponents");
            foreach (var tc in udkComponents)
            {
                var entry = tc.ResolveToEntry(udkP);
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, entry, destPackage, destTerrain, true, new RelinkerOptionsPackage(), out var portedComp);
                components.Add(new ObjectProperty(portedComp.UIndex));
            }


            destTerrain.WriteProperties(terrainProps);

            //localBin.CachedTerrainMaterials = donorBin.CachedTerrainMaterials;
            //localBin.CachedTerrainMaterials2 = donorBin.CachedTerrainMaterials2;


            //// Correct the material parent ref (I don't think the game uses these)
            //foreach (var ctm in localBin.CachedTerrainMaterials)
            //{
            //    foreach (var utex in ctm.UniformExpressionTextures)
            //    {
            //        var sourceTex = donorFile.GetEntry(utex.value);
            //        if (sourceTex is ImportEntry imp)
            //        {
            //            utex.value = EntryImporter.GetOrAddCrossImportOrPackage(imp.InstancedFullPath, donorFile, destPackage, new RelinkerOptionsPackage()).UIndex;
            //        }
            //        else if (sourceTex is ExportEntry exp)
            //        {
            //            EntryExporter.ExportExportToPackage(exp, destPackage, out var newExp);
            //            utex.value = newExp.UIndex;
            //        }
            //    }

            //    ctm.Terrain.value = localTerrain.UIndex;
            //}
            //localTerrain.WriteBinary(localBin);

            //// Port over the TerrainLayers and relink them
            //localTerrain.WriteProperty(donorTerrain.GetProperty<ArrayProperty<StructProperty>>("Layers"));
            //var localLayers = localTerrain.GetProperty<ArrayProperty<StructProperty>>("Layers");
            //foreach (var layer in localLayers)
            //{
            //    var setup = layer.GetProp<ObjectProperty>("Setup");
            //    var donorObj = donorFile.GetUExport(setup.Value);
            //    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, donorObj, destPackage, null, true, new RelinkerOptionsPackage(), out var newSetup);
            //    setup.Value = newSetup.UIndex;
            //}
            //localTerrain.WriteProperty(localLayers);


            // Port in a PlayerStart
            //using var psDonor = MEPackageHandler.OpenMEPackage(Path.Combine(LE1Directory.CookedPCPath, @"BIOA_STA00.pcc"));
            //var psStart = psDonor.FindExport("TheWorld.PersistentLevel.PlayerStart_0");
            //psStart.RemoveProperty("nextNavigationPoint");
            //EntryExporter.ExportExportToPackage(psStart, destPackage, out var newPStart);

            // Map already has a player start, just port it in too
            string[] otherClassesToPortIn = new[] { "PlayerStart", "HeightFog"};
            Point3D startLoc = null;
            foreach (var exp in udkP.Exports.Where(x => otherClassesToPortIn.Contains(x.ClassName)))
            {
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, exp, destPackage, destLevel, true, new RelinkerOptionsPackage(), out var playerStart);
                if (exp.ClassName == "PlayerStart")
                {
                    startLoc = PathEdUtils.GetLocation(playerStart as ExportEntry);
                }
            }

            // Import the pathfinding network
            var udkPLBin = ObjectBinary.From<Level>(udkP.FindExport("TheWorld.PersistentLevel"));
            var le1PLBin = ObjectBinary.From<Level>(destLevel);

            var pathfindingSource = udkPLBin.NavListStart;
            if (pathfindingSource != null && pathfindingSource.value != 0)
            {
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, udkP.GetUExport(pathfindingSource.value), destPackage, destLevel, true, new RelinkerOptionsPackage(), out var chainStart);
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, udkP.GetUExport(udkPLBin.NavListEnd.value), destPackage, destLevel, true, new RelinkerOptionsPackage(), out var chainEnd);
                le1PLBin.NavListStart.value = chainStart.UIndex;
                le1PLBin.NavListEnd.value = chainEnd.UIndex;
                destLevel.WriteBinary(le1PLBin);
            }

            VTestExperiment.CorrectReachSpecs(udkP, destPackage);

            // Port in a skybox
            using var skyDonor = MEPackageHandler.OpenMEPackage(Path.Combine(LE1Directory.CookedPCPath, @"BIOA_WAR00.pcc"));
            var sky = skyDonor.FindExport("TheWorld.PersistentLevel.StaticMeshActor_1");
            //psStart.RemoveProperty("nextNavigationPoint");
            EntryExporter.ExportExportToPackage(sky, destPackage, out var newSky);
            PathEdUtils.SetLocation(newSky as ExportEntry, 2000, 2000, 92);

            // Put in a mako because why not
            using var makoDonor = MEPackageHandler.OpenMEPackage(Path.Combine(LE1Directory.CookedPCPath, @"BIOA_LOS00.pcc"));
            var mako = makoDonor.FindExport("TheWorld.PersistentLevel.BioVehicleWheeled_0");
            //psStart.RemoveProperty("nextNavigationPoint");
            EntryExporter.ExportExportToPackage(mako, destPackage, out var newMako);
            PathEdUtils.SetLocation(newMako as ExportEntry, (float)startLoc.X + 500f, (float)startLoc.Y + 500, (float)startLoc.Z + 50);

            VTestExperiment.RebuildPersistentLevelChildren(destLevel, null);
            destPackage.Save();
        }
        */
        public static void TestCurrentPackageForUnknownBinary(PackageEditorWindow pe)
        {
            if (pe.Pcc == null)
                return;

            List<EntryStringPair> unknowns = new List<EntryStringPair>();

            foreach (var export in pe.Pcc.Exports)
            {
                if (ObjectBinary.From(export) is ObjectBinary bin)
                {
                    var original = export.Data;
                    export.WriteBinary(bin);
                    if (!export.DataReadOnly.SequenceEqual(original))
                    {
                        unknowns.Add(new EntryStringPair(export, $"({export.ClassName}) {export.UIndex} {export.ObjectName} has binary that didn't reserialize back to itself"));
                    }
                }
                else
                {
                    // Binary class is not defined for this
                    // Check to make sure there is in fact no binary so 
                    // we aren't missing anything
                    if (export.propsEnd() != export.DataSize)
                    {
                        unknowns.Add(new EntryStringPair(export, $"({export.ClassName}) {export.UIndex} {export.ObjectName} has unparsed binary"));
                    }
                }
            }

            if (unknowns.Any())
            {
                ListDialog ld = new ListDialog(unknowns, "Unknown binary found", "The following items are not parsed by LEX but appear to have binary following the properties:", pe) { DoubleClickEntryHandler = pe.GetEntryDoubleClickAction() };
                ld.Show();
            }
        }

        private static void FindTexture2D(AssetDB db, ExportEntry exp)
        {
            var dbName = TexToDbName(exp);
            var found = db.Textures.FirstOrDefault(x => dbName == x.TextureName);
            if (found == null)
            {
                if (dbName.EndsWith("_dup", StringComparison.InvariantCultureIgnoreCase))
                {
                    dbName = dbName.Substring(0, dbName.Length - 4);
                }

                found = db.Textures.FirstOrDefault(x => dbName == x.TextureName);
                if (found == null)
                {
                    Debug.WriteLine($@"Didn't find {exp.InstancedFullPath} ({dbName})");
                }
            }
        }

        private static string TexToDbName(ExportEntry exp)
        {
            IEntry currentItem = exp;
            Stack<IEntry> entries = new Stack<IEntry>();

            while (true)
            {
                if (currentItem != null && currentItem.ClassName != "Package")
                {
                    entries.Push(currentItem);
                    currentItem = currentItem.Parent;
                }
                else
                    break;
            }

            return string.Join("_", entries.Select(x => x.ObjectName.Name));
        }

        public static void BuildAllObjectsGameDB(MEGame game, PackageEditorWindow pe)
        {
            Task.Run(() =>
            {
                //using (var sw = new DebugStopWatch("bin deserialization"))
                //using (var fileStream = File.OpenRead(Path.Combine(AppDirectories.ObjectDatabasesFolder, $"{game}.bin")))
                //{
                //    var oldDb = ObjectInstanceDB.Deserialize(fileStream);
                //}
                //return;
                pe.SetBusy("Building Object IFP DB");
                var allPackages = MELoadedFiles.GetFilesLoadedInGame(game).Values.ToList();
                if (game == MEGame.ME3)
                {
                    allPackages.AddRange(Directory.GetFiles(ME3Directory.DLCPath, "*.sfar", SearchOption.AllDirectories));
                }
                int fullCount = allPackages.Count;

                void addMoreItems(int count)
                {
                    fullCount += count;
                }


                var objectDB = ObjectInstanceDB.Create(game, allPackages, numDone => pe.BusyText = $"Indexed [{numDone}/{fullCount}] files", addMoreItems);

                // Compile the database
                pe.BusyText = "Compiling database";
                var oldDbpath = Path.Combine(AppDirectories.ObjectDatabasesFolder, $"{game}.json");
                if (File.Exists(oldDbpath))
                {
                    File.Delete(oldDbpath);
                }
                using FileStream fs = File.Create(AppDirectories.GetObjectDatabasePath(game));
                objectDB.Serialize(fs);
            }).ContinueWithOnUIThread(_ => { pe.EndBusy(); });
        }



        public static void PortSequenceObjectClassAcrossGame(PackageEditorWindow pe)
        {
            var seqObjsToPort = pe.Pcc.Exports.Where(x => !x.IsDefaultObject && x.SuperClassName == "SequenceAction" && x.IsClass).ToList();
            var sourceDir = PAEMPaths.VTest_DonorsDir;

            List<string> createdPackages = new List<string>();
            foreach (var seqObjClass in seqObjsToPort)
            {
                var package = seqObjClass.ParentName;
                var donorDest = Path.Combine(PAEMPaths.VTest_DonorsDir, $"{package}.pcc");
                if (!createdPackages.Contains(package))
                {
                    createdPackages.Add(package);
                    MEPackageHandler.CreateAndSavePackage(donorDest, pe.Pcc.Game.ToLEVersion());
                }

                using var p = MEPackageHandler.OpenMEPackage(donorDest);
            }

        }

        public static void SearchObjectInfos(PackageEditorWindow pe)
        {
            var searchTerm = PromptDialog.Prompt(pe, "Enter key value to search", "ObjectInfos Search");
            if (searchTerm != null)
            {
                string searchResult = "";
                MEGame[] games = new[] { MEGame.ME1, MEGame.ME2, MEGame.ME3, MEGame.LE1, MEGame.LE2, MEGame.LE3 };

                foreach (var game in games)
                {
                    if (GlobalUnrealObjectInfo.GetClasses(game).TryGetValue(searchTerm, out _)) searchResult += $"Key found in {game} Classes\n";
                    if (GlobalUnrealObjectInfo.GetStructs(game).TryGetValue(searchTerm, out _)) searchResult += $"Key found in {game} Classes\n";
                    if (GlobalUnrealObjectInfo.GetEnums(game).TryGetValue(searchTerm, out _)) searchResult += $"Key found in {game} Classes\n";
                }

                if (searchResult == "")
                {
                    searchResult = "Key " + searchTerm +
                                   " not found in any ObjectInfo Structs/Classes/Enums dictionaries for any games";
                }
                else
                {
                    searchResult = "Key " + searchTerm + " found in the following:\n" + searchResult;
                }

                MessageBox.Show(searchResult);
            }
        }

        public static void TestCrossGenClassPorting(PackageEditorWindow pe)
        {
            var destFile = Path.Combine(PAEMPaths.VTest_DonorsDir, "BIOC_BaseDLC_Vegas.pcc");
            var sourceFile = "BIOC_BaseDLC_Vegas.u";

            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(MEGame.ME1);
            if (loadedFiles.TryGetValue(sourceFile, out var vegasU))
            {
                using var vegasP = MEPackageHandler.OpenMEPackage(vegasU);
                MEPackageHandler.CreateAndSavePackage(destFile, MEGame.LE1);
                using var destP = MEPackageHandler.OpenMEPackage(destFile);

                // BIOC_BASE -> SFXGame
                var bcBaseIdx = vegasP.findName("BIOC_Base");
                vegasP.replaceName(bcBaseIdx, "SFXGame");

                // Should probably make method for name correction

                var packageName = Path.GetFileNameWithoutExtension(sourceFile);
                var link = ExportCreator.CreatePackageExport(destP, packageName);

                List<EntryStringPair> results = new List<EntryStringPair>();
                RelinkerOptionsPackage rop = new RelinkerOptionsPackage()
                {
                    IsCrossGame = vegasP.Game != destP.Game,
                    ImportExportDependencies = true
                };

                foreach (var v in vegasP.Exports.Where(x => x.IsClass))
                {
                    results.AddRange(EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, v, destP, null, true, rop, out _));
                }

                foreach (var v in destP.Exports.Where(x => x.ObjectName != packageName && x.Parent == null))
                {
                    v.idxLink = link.UIndex;
                }

                destP.Save();

                if (results.Any())
                {
                    var b = new ListDialog(results, "Errors porting classes", "The following errors occurred porting classes.", pe);
                    b.Show();
                }


            }
        }

        /// <summary>
        /// Converts a WwiseBank to a basic Wwise project with events.
        /// </summary>
        /// <param name="getPeWindow"></param>
        /// <exception cref="NotImplementedException"></exception>
        public static void ConvertWwiseBankToProject(PackageEditorWindow peWindow)
        {
            // This is just experimental version for now
            var pcc = peWindow.Pcc;
            if (pcc == null || (pcc.Game != MEGame.LE2 && pcc.Game != MEGame.LE3))
            {
                MessageBox.Show("Unsupported game - only can support LE2/LE3");
                return;
            }

            //if (!peWindow.TryGetSelectedExport(out var exp) || (exp.IsDefaultObject || exp.ClassName != "WwiseBank"))
            //{
            //    MessageBox.Show("Unsupported export - must select a WwiseBank");
            //    return;
            //}

            //var dlg = new CommonOpenFileDialog("Select directory to output project to") { IsFolderPicker = true };
            //if (dlg.ShowDialog(peWindow) != CommonFileDialogResult.Ok) { return; }

            // Debug: Just do first bank (in our BankTest.pcc) file
            var exp = pcc.Exports.FirstOrDefault(x => x.ClassName == "WwiseBank");
            var outDir = Path.Combine(@"B:\Documents\WwiseExports", exp.ObjectName);
            WwiseIO.ExportBankToProject(exp, Directory.CreateDirectory(outDir).FullName);
        }

        public static void StripLightmap(PackageEditorWindow peWindow)
        {
            if (!peWindow.TryGetSelectedExport(out var exp) || (exp.IsDefaultObject || exp.ClassName != "StaticMeshComponent"))
            {
                MessageBox.Show("Unsupported export - must select a StaticMeshComponent");
                return;
            }

            var smc = ObjectBinary.From<StaticMeshComponent>(exp);
            foreach (var lod in smc.LODData)
            {
                lod.LightMap = new LightMap(); // This means no lightmap.
            }
            exp.WriteBinary(smc);
        }

        public static void FixFXAMemoryNames(PackageEditorWindow peWindow)
        {
            if (peWindow == null || peWindow.Pcc == null)
                return;

            foreach (var fxa in peWindow.Pcc.Exports.Where(x => x.ClassName == "FaceFXAnimSet"))
            {
                var fxaO = ObjectBinary.From<FaceFXAnimSet>(fxa);

                if (fxa.ObjectName.Name.EndsWith("_F") || fxa.ObjectName.Name.EndsWith("_M"))
                {
                    // Gendered
                    fxaO.Names[0] = fxa.ObjectName.Name[..^2]; // Cut off _F / _M
                }
                else
                {
                    // Non gendered
                    fxaO.Names[0] = fxa.ObjectName.Name; // Exact name
                }

                fxa.WriteBinary(fxaO);

            }
        }
    }
}