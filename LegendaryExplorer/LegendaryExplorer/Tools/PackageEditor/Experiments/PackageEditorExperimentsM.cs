using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Numerics;
using System.Windows.Input;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.Sequence_Editor;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
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
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using Function = LegendaryExplorerCore.Unreal.Classes.Function;

//using ImageMagick;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    /// <summary>
    /// Class where Mgamerz can put debug/dev/experimental code
    /// </summary>
    public class PackageEditorExperimentsM
    {
        public static void CompareISB()
        {

        }

        public static void OverrideVignettes(PackageEditorWindow pewpf)
        {

            Task.Run(() =>
            {
                pewpf.BusyText = "Enumerating exports for PPS...";
                pewpf.IsBusy = true;
                var allFiles = MELoadedFiles.GetOfficialFiles(MEGame.LE3).Where(x => Path.GetExtension(x) == ".pcc").ToList();
                int totalFiles = allFiles.Count;
                int numDone = 0;
                foreach (string filePath in allFiles)
                {
                    //if (!filePath.EndsWith("Engine.pcc"))
                    //    continue;
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                    foreach (var f in pcc.Exports)
                    {
                        var props = f.GetProperties();
                        foreach (var prop in props)
                        {
                            if (prop is StructProperty sp && sp.StructType == "PostProcessSettings")
                            {
                                var vignette = sp.GetProp<BoolProperty>("bEnableVignette");
                                var vigOverride = sp.GetProp<BoolProperty>("bOverride_EnableVignette");

                                if (vigOverride != null && vignette != null)
                                {
                                    vignette.Value = false;
                                    vigOverride.Value = true;
                                    f.WriteProperty(sp);
                                }
                            }
                        }

                    }

                    if (pcc.IsModified)
                        pcc.Save();

                    numDone++;
                    pewpf.BusyText = $"Enumerating exports for PPS [{numDone}/{totalFiles}]";
                }
            }).ContinueWithOnUIThread(foundCandidates => { pewpf.IsBusy = false; });
        }

        public static void UpdateTexturesMatsToGame(PackageEditorWindow pewpf)
        {
            Task.Run(() =>
            {
                pewpf.BusyText = "Updating objects...";
                pewpf.IsBusy = true;
                var packages = MELoadedFiles.GetFilesLoadedInGame(MEGame.LE3);

                //var updatableObjects = pewpf.Pcc.Exports.Where(x => x.IsTexture());
                //updatableObjects = updatableObjects.Concat(pewpf.Pcc.Exports.Where(x => x.ClassName == @"Material"));

                var lookupPackages = new[] { @"BIOG_HMF_ARM_HVY_R.pcc", @"BIOG_HMF_ARM_CTH_R.pcc", @"Startup.pcc" };
                foreach (var lookupPackage in lookupPackages)
                {
                    using var importPackage = MEPackageHandler.OpenMEPackage(packages[lookupPackage]);

                    foreach (var sourceObj in importPackage.Exports)
                    {
                        var matchingObj =
                            pewpf.Pcc.Exports.FirstOrDefault(x => x.InstancedFullPath == sourceObj.InstancedFullPath);
                        if (matchingObj != null && !matchingObj.DataChanged)
                        {
                            if (!shouldUpdateObject(matchingObj))
                                continue;

                            var resultst = EntryImporter.ImportAndRelinkEntries(
                                EntryImporter.PortingOption.ReplaceSingular,
                                sourceObj, matchingObj.FileRef, matchingObj, true, out _,
                                errorOccuredCallback: x => throw new Exception(x),
                                importExportDependencies: true);
                            if (resultst.Any())
                            {
                                Debug.WriteLine("MERGE FAILED!");
                            }

                            if (matchingObj.DataChanged)
                                Debug.WriteLine($@"Updated {matchingObj.InstancedFullPath}");
                        }
                        else
                        {
                            //Debug.WriteLine($@"Did not update {sourceObj.InstancedFullPath}");
                        }
                    }


                }

            }).ContinueWithOnUIThread(x => { pewpf.IsBusy = false; });
        }

        private static bool shouldUpdateObject(ExportEntry matchingObj)
        {
            if (matchingObj.ClassName == @"ObjectReferencer") return false;
            if (matchingObj.ClassName == @"ObjectRedirector") return false;

            return true;
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
                return pewpf.GetUnmoddedCandidatesForPackage();
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
                        sourcePackage.GetUExport(exp.UIndex).Header = origExp.Header;
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

        public static void CompactFileViaExternalFile(IMEPackage sourcePackage)
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "*.pcc|*.pcc" };
            if (d.ShowDialog() == true)
            {

                using var compactedAlready = MEPackageHandler.OpenMEPackage(d.FileName);
                var fname = Path.GetFileNameWithoutExtension(sourcePackage.FilePath);
                var exportsToKeep = sourcePackage.Exports
                    .Where(x => x.FullPath == fname || x.FullPath == @"SeekFreeShaderCache" ||
                                x.FullPath.StartsWith("ME3ExplorerTrashPackage")).ToList();

                var entriesToTrash = new ConcurrentBag<ExportEntry>();
                Parallel.ForEach(sourcePackage.Exports, export =>
                {
                    var matchingExport = exportsToKeep.FirstOrDefault(x => x.FullPath == export.FullPath);
                    if (matchingExport == null)
                    {
                        matchingExport = compactedAlready.Exports.FirstOrDefault(x => x.FullPath == export.FullPath);
                    }

                    if (matchingExport == null)
                    {
                        //Debug.WriteLine($"Trash {export.FullPath}");
                        entriesToTrash.Add(export);
                    }
                });

                EntryPruner.TrashEntries(sourcePackage, entriesToTrash);
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

        public static void ShiftInterpTrackMove(ExportEntry interpTrackMove)
        {
            var offsetX = int.Parse(PromptDialog.Prompt(null, "Enter X shift offset", "Offset X", "0", true));
            var offsetY = int.Parse(PromptDialog.Prompt(null, "Enter Y shift offset", "Offset Y", "0", true));
            var offsetZ = int.Parse(PromptDialog.Prompt(null, "Enter Z shift offset", "Offset Z", "0", true));

            var props = interpTrackMove.GetProperties();
            var posTrack = props.GetProp<StructProperty>("PosTrack");
            var points = posTrack.GetProp<ArrayProperty<StructProperty>>("Points");
            foreach (var point in points)
            {
                var outval = point.GetProp<StructProperty>("OutVal");
                outval.GetProp<FloatProperty>("X").Value += offsetX;
                outval.GetProp<FloatProperty>("Y").Value += offsetY;
                outval.GetProp<FloatProperty>("Z").Value += offsetZ;
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
                        var package =
                            MEPackageHandler.OpenMEPackageFromStream(packageStream, Path.GetFileName(f.FileName));
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
        /// <param name="Pcc"></param>
        public static void ValidateNavpointChain(IMEPackage Pcc)
        {
            var pl = Pcc.Exports.FirstOrDefault(x => x.ClassName == "Level" && x.ObjectName == "PersistentLevel");
            if (pl != null)
            {
                var persistentLevel = ObjectBinary.From<Level>(pl);
                var nlSU = persistentLevel.NavListStart;
                var nlS = Pcc.GetUExport(nlSU.value);
                List<ExportEntry> navList = new List<ExportEntry>();
                List<ExportEntry> itemsMissingFromWorldNPC = new List<ExportEntry>();
                if (!persistentLevel.NavPoints.Any(x => x.value == nlS.UIndex))
                {
                    itemsMissingFromWorldNPC.Add(nlS);
                }

                var nnP = nlS.GetProperty<ObjectProperty>("nextNavigationPoint");
                navList.Add(nlS);
                Debug.WriteLine($"{nlS.UIndex} {nlS.InstancedFullPath}");
                while (nnP != null)
                {
                    var nextNavigationPoint = nnP.ResolveToEntry(Pcc) as ExportEntry;
                    Debug.WriteLine($"{nextNavigationPoint.UIndex} {nextNavigationPoint.InstancedFullPath}");
                    if (!persistentLevel.NavPoints.Any(x => x.value == nextNavigationPoint.UIndex))
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
                    var streambin = wwstream?.GetBinaryData<WwiseStream>() ?? null;
                    if (streambin != null)
                    {
                        var duration = streambin.GetAudioInfo().GetLength();
                        var durtnMS = wwevent.GetProperty<FloatProperty>("DurationMilliseconds");
                        if (durtnMS != null && duration != null)
                        {
                            durtnMS.Value = (float)duration.TotalMilliseconds;
                            wwevent.WriteProperty(durtnMS);
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
                return pewpf.GetUnmoddedCandidatesForPackage();
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
                        imp.Header = origImp.Header;
                    }
                }

                foreach (var exp in sourcePackage.Exports)
                {
                    var origExp = restorePackage.FindExport(exp.InstancedFullPath);
                    if (origExp != null)
                    {
                        exp.Data = origExp.Data;
                        exp.Header = origExp.GetHeader();
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
                "GetPEWindow() process applies immediately and cannot be undone.\nEnsure the file you are going to regenerate is not open in Legendary Explorer in any tools.\nBe absolutely sure you know what you're doing before you use GetPEWindow()!");
            OpenFileDialog d = new OpenFileDialog
            {
                Title = "Select file to regen guid for",
                Filter = "*.pcc|*.pcc"
            };
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
                        foreach (var v in package.LocalTalkFiles)
                        {
                            if (!string.IsNullOrWhiteSpace(langFilter) && !v.Name.EndsWith($"_{langFilter}"))
                            {
                                continue;
                            }

                            var outPath = Path.Combine(dlg.FileName,
                                $"{Path.GetFileNameWithoutExtension(f.Key)}.{package.GetEntry(v.UIndex).InstancedFullPath}.xml");
                            v.saveToFile(outPath);
                        }

                    }
                }).ContinueWithOnUIThread(x => { pewpf.IsBusy = false; });
            }
        }

        /// <summary>
        /// RUN ON THE SOURCE FILE, NOT THE DEST
        /// </summary>
        /// <param name="package"></param>
        private static void PreCorrectReachSpecEnd(ExportEntry exp)
        {
            // RUN 
            // Since it converts to immutable we have to make sure we have these in the right order
            var end = exp.GetProperty<StructProperty>("End");
            if (end != null)
            {
                var guid = end.GetProp<StructProperty>("Guid");
                end.Properties.RemoveNamedProperty("Guid");
                end.Properties.Insert(0, guid); // Guid must go first. When it's written it should be read as immutable... I think...
                exp.WriteProperty(end);
            }
        }

        private static void CorrectNeverStream(IMEPackage package)
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
                        break;
                    }
                    lowMipCount++;
                }
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

        public static void MScanner(PackageEditorWindow pe)
        {
            MEGame game = MEGame.LE1;
            StringBuilder sb = new StringBuilder();
            using var p = MEPackageHandler.OpenMEPackage(@"Y:\ModLibrary\LE1\V Test\Donors\LE1Resources.pcc");
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
                    sb.AppendLine("\t{");
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

                        sb.AppendLine($"\t\tnew KeyValuePair<string, PropertyInfo>(\"{prop.Key}\", {propInfoStr}),");
                    }
                    sb.AppendLine("\t}");

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
            return;

            // just dump whatever shit you want to find here
            foreach (string filePath in MELoadedFiles.GetOfficialFiles(MEGame.LE1 /*, MEGame.LE2, MEGame.LE3*/))
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                foreach (ExportEntry export in pcc.Exports)
                {
                    // code here
                    if (export.ClassName == "BioInert" && !export.IsDefaultObject && !export.IsClass)
                    {
                        if (export.DataSize > export.propsEnd() + 4)
                            Debug.WriteLine($"BIOINERT WITH BINARY {export.UIndex} {export.InstancedFullPath} in {filePath}");
                    }

                }
            }
        }

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

        public static async void VTest(PackageEditorWindow pe)
        {
            // Paths are in PAEMPaths.cs

            bool prc2aa = false;
            bool prc2 = true;

            pe.SetBusy("Performing VTest");
            await Task.Run(() =>
            {
                ObjectInstanceDB db = null;
                string dbPath = AppDirectories.GetObjectDatabasePath(MEGame.LE1);
                string matPath = AppDirectories.GetMaterialGuidMapPath(MEGame.ME1);
                Dictionary<Guid, string> me1MaterialMap = null;
                pe.BusyText = "Loading databases";

                if (File.Exists(dbPath))
                {
                    db = ObjectInstanceDB.DeserializeDB(File.ReadAllText(dbPath));
                    db.BuildLookupTable(); // Lookup table is required as we are going to compile things

                    // Add extra donors
                    foreach (var file in Directory.GetFiles(PAEMPaths.VTest_DonorsDir))
                    {
                        if (file.RepresentsPackageFilePath())
                        {
                            using var p = MEPackageHandler.OpenMEPackage(file);
                            IndexFileForObjDB(db, MEGame.LE1, p);
                        }
                    }
                }
                else
                {
                    return;
                }

                if (File.Exists(matPath))
                {
                    me1MaterialMap = JsonConvert.DeserializeObject<Dictionary<Guid, string>>(File.ReadAllText(matPath));
                }
                else
                {
                    return;
                }

                pe.BusyText = "Preparing...";
                // Clear out dest dir
                foreach (var f in Directory.GetFiles(PAEMPaths.VTest_FinalDestDir))
                {
                    File.Delete(f);
                }

                // Copy in precomputed files
                foreach (var f in Directory.GetFiles(PAEMPaths.VTest_PrecomputedDir))
                {
                    File.Copy(f, Path.Combine(PAEMPaths.VTest_FinalDestDir, Path.GetFileName(f)));
                }

                pe.BusyText = "Loading packages";

                using var sequencePackageCache = new PackageCache();
                // BIOA_PRC2AA ---------------------------------------
                if (prc2aa)
                {
                    // BIOA_PRC2AA
                    //{
                    //    var sourceName = "BIOA_PRC2AA";
                    //    var outputFile = $@"{PAEMPaths.VTest_FinalDestDir}\{sourceName}.pcc";
                    //    CreateEmptyLevel(outputFile, MEGame.LE1);

                    //    using var le1File = MEPackageHandler.OpenMEPackage(outputFile);
                    //    using var me1File = MEPackageHandler.OpenMEPackage($@"{PAEMPaths.VTest_SourceDir}\PRC2AA\{sourceName}.SFM");

                    //    var itemsToPort = new ExportEntry[]
                    //    {
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.BioMapNote_26"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.BioMapNote_27"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.BioMapNote_28"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.BioTriggerStream_32"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.PlayerStart_0"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.StaticLightCollectionActor_15"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.StaticMeshCollectionActor_44"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.Note_0"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.Note_1")
                    //    };

                    //    VTestFilePorting(me1File, le1File, itemsToPort, db, pe);

                    //    // Replace BioWorldInfo
                    //    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular, me1File.FindExport(@"TheWorld.PersistentLevel.BioWorldInfo_0"), le1File, le1File.FindExport(@"TheWorld.PersistentLevel.BioWorldInfo_0"), true, out _, importExportDependencies: true, targetGameDonorDB: db);

                    //    PostPortingCorrections(me1File, le1File);
                    //    RebuildPersistentLevelChildren(le1File.FindExport("TheWorld.PersistentLevel"));
                    //    CorrectNeverStream(le1File);
                    //    le1File.Save();
                    //}

                    // BIOA_PRC2AA_00_LAY
                    //{
                    //    var sourceName = "BIOA_PRC2AA_00_LAY";
                    //    var outputFile = $@"{PAEMPaths.VTest_FinalDestDir}\{sourceName}.pcc";
                    //    CreateEmptyLevel(outputFile, MEGame.LE1);

                    //    using var le1File = MEPackageHandler.OpenMEPackage(outputFile);
                    //    using var me1File = MEPackageHandler.OpenMEPackage($@"{PAEMPaths.VTest_SourceDir}\PRC2AA\{sourceName}.SFM");

                    //    PortVTestLevel("PRC2AA", sourceName, PAEMPaths.VTest_FinalDestDir, PAEMPaths.VTest_SourceDir, db, pe, false);
                    //    RebuildPersistentLevelChildren(le1File.FindExport("TheWorld.PersistentLevel"));
                    //    CorrectNeverStream(le1File);

                    //    // Correct terrain (doesn't seem to work)
                    //    var terrainExp = le1File.FindExport(@"TheWorld.PersistentLevel.Terrain_0");
                    //    var terrain = ObjectBinary.From<Terrain>(terrainExp);
                    //    terrain.CachedDisplacements = new byte[terrain.Heights.Length];

                    //    // Update the GUIDs of the materials
                    //    foreach (var cm in terrain.CachedTerrainMaterials)
                    //    {
                    //        for (int i = 0; i < cm.MaterialIds.Length; i++)
                    //        {
                    //            var origId = cm.MaterialIds[i];
                    //            if (me1MaterialMap.TryGetValue(origId, out var matIFP))
                    //            {
                    //                var inFileMat = le1File.FindExport(matIFP);
                    //                var matObjBin = ObjectBinary.From<Material>(inFileMat);
                    //                cm.MaterialIds[i] = matObjBin.SM3MaterialResource.ID;
                    //            }
                    //            else
                    //            {
                    //                Debug.WriteLine($@"UNMAPPED MATERIAL: {origId}");
                    //            }
                    //        }
                    //    }

                    //    terrainExp.WriteBinary(terrain);

                    //    // Terrain testing - crashes game
                    //    //var preTerrainProps = terrainExp.GetProperties();
                    //    //var donorTerrainF = Path.Combine(LE1Directory.CookedPCPath, @"BIOA_UNC10_00_LAY.pcc");
                    //    //using var donorTP = MEPackageHandler.OpenMEPackage(donorTerrainF);
                    //    //var donorTerrain = donorTP.Exports.First(x => x.ClassName == "Terrain");
                    //    //EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular, donorTerrain, le1File, terrainExp, false, out _);

                    //    //foreach (var p in preTerrainProps)
                    //    //{
                    //    //    if (p is IntProperty ip)
                    //    //    {
                    //    //        terrainExp.WriteProperty(ip);
                    //    //    } else if (p.Name == "TerrainComponents" || p.Name == "Location")
                    //    //    {
                    //    //        terrainExp.WriteProperty(p);
                    //    //    }
                    //    //}

                    //    le1File.Save();
                    //}

                    // BIOA_PRC2AA_00_DSG
                    //{
                    PortVTestLevel("PRC2AA", "BIOA_PRC2AA", PAEMPaths.VTest_FinalDestDir, PAEMPaths.VTest_SourceDir, db, pe, syncBioWorldInfo: true, portMainSequence: false);
                    PortVTestLevel("PRC2AA", "bioa_prc2aa_00_lay", PAEMPaths.VTest_FinalDestDir, PAEMPaths.VTest_SourceDir, db, pe, portMainSequence: false);
                    PortVTestLevel("PRC2AA", "bioa_prc2aa_00_dsg", PAEMPaths.VTest_FinalDestDir, PAEMPaths.VTest_SourceDir, db, pe, portMainSequence: true);
                    PortVTestLevel("PRC2AA", "bioa_prc2aa_00_snd", PAEMPaths.VTest_FinalDestDir, PAEMPaths.VTest_SourceDir, db, pe, portMainSequence: true);
                    //    var outputFile = $@"{PAEMPaths.VTest_FinalDestDir}\{sourceName}.pcc";
                    //    CreateEmptyLevel(outputFile, MEGame.LE1);

                    //    using var le1File = MEPackageHandler.OpenMEPackage(outputFile);
                    //    using var me1File = MEPackageHandler.OpenMEPackage($@"{PAEMPaths.VTest_SourceDir}\PRC2AA\{sourceName}.SFM");

                    //    var itemsToPort = new ExportEntry[]
                    //    {
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.BioDoor_1"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.BioInert_0"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.BioInert_3"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.BioInert_4"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.BioInert_5"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.InterpActor_33"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.InterpActor_34"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.InterpActor_35"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.InterpActor_36"),
                    //    };
                    //    VTestFilePorting(me1File, le1File, itemsToPort, db, pe);

                    //    CorrectSequenceObjects(dest, sequencePackageCache);
                    //    RebuildPersistentLevelChildren(le1File.FindExport("TheWorld.PersistentLevel"));
                    //    CorrectNeverStream(le1File);
                    //    le1File.Save(); // Save again
                    //}

                    // BIOA_PRC2AA_00_SND
                    //{
                    //    var sourceName = "BIOA_PRC2AA_00_SND";
                    //    var outputFile = $@"{PAEMPaths.VTest_FinalDestDir}\{sourceName}.pcc";
                    //    CreateEmptyLevel(outputFile, MEGame.LE1);

                    //    using var le1File = MEPackageHandler.OpenMEPackage(outputFile);
                    //    using var me1File = MEPackageHandler.OpenMEPackage($@"{PAEMPaths.VTest_SourceDir}\PRC2AA\{sourceName}.SFM");

                    //    var itemsToPort = new ExportEntry[]
                    //    {
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.Brush_20"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.ReverbVolume_1"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.ReverbVolume_0"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.BioAudioVolume_1"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.BioAudioVolume_0"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.AmbientSound_3"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.AmbientSound_4"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.AmbientSound_2"),
                    //    me1File.FindExport(@"TheWorld.PersistentLevel.AmbientSound_1"),
                    //    };
                    //    VTestFilePorting(me1File, le1File, itemsToPort, db, pe);

                    //    // Port sequence in
                    //    pe.BusyText = "Porting sequencing...";
                    //    var dest = le1File.FindExport(@"TheWorld.PersistentLevel.Main_Sequence");
                    //    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular, me1File.FindExport(@"TheWorld.PersistentLevel.Main_Sequence"), le1File, dest, true, out _, importExportDependencies: true, targetGameDonorDB: db);
                    //    CorrectSequenceObjects(dest, sequencePackageCache);
                    //    RebuildPersistentLevelChildren(le1File.FindExport("TheWorld.PersistentLevel"));
                    //    CorrectNeverStream(le1File);
                    //    le1File.Save(); // Save again
                    //}

                    // LOC files
                    {
                        var pathSource = Path.Combine(PAEMPaths.VTest_SourceDir, "PRC2AA");
                        var files = Directory.GetFiles(pathSource).Where(x => x.RepresentsPackageFilePath() && x.Contains("_LOC_", StringComparison.InvariantCultureIgnoreCase));
                        foreach (var file in files)
                        {
                            PortLOCFile(file, db, pe);
                        }
                    }
                }

                // BIOA_PRC2 ------------------------------------------
                if (prc2)
                {
                    var prc2Files = Directory.GetFiles(Path.Combine(PAEMPaths.VTest_SourceDir, "PRC2"));
                    foreach (var f in prc2Files)
                    {
                        if (f.Contains("_LOC_", StringComparison.InvariantCultureIgnoreCase))
                            continue; // Skip for now
                        var levelName = Path.GetFileNameWithoutExtension(f);
                        PortVTestLevel("PRC2", levelName, PAEMPaths.VTest_FinalDestDir, PAEMPaths.VTest_SourceDir, db, pe, levelName == "BIOA_PRC2"/*true*/, /*levelName == "BIOA_PRC2"*/true, enableDynamicLighting: true);
                    }
                    // Port LOC files
                    foreach (var f in prc2Files)
                    {
                        if (!f.Contains("_LOC_", StringComparison.InvariantCultureIgnoreCase))
                            continue; // Only include LOC files
                        PortLOCFile(f, db, pe); // breaks the game currently
                    }

                    Debug.WriteLine("Checking BTS....");
                    VTest_Check();
                }
            }).ContinueWithOnUIThread(result =>
            {
                if (result.Exception != null)
                    Debugger.Break();
                pe.EndBusy();
            });
        }

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

        private static void PortLOCFile(string sourceFile, ObjectInstanceDB db, PackageEditorWindow pe)
        {
            var packName = Path.GetFileNameWithoutExtension(sourceFile);
            var destPackagePath = Path.Combine(PAEMPaths.VTest_FinalDestDir, $"{packName.ToUpper()}.pcc");
            MEPackageHandler.CreateAndSavePackage(destPackagePath, MEGame.LE1);
            using var package = MEPackageHandler.OpenMEPackage(destPackagePath);
            using var sourcePackage = MEPackageHandler.OpenMEPackage(sourceFile);

            var bcBaseIdx = sourcePackage.findName("BIOC_Base");
            sourcePackage.replaceName(bcBaseIdx, "SFXGame");

            foreach (var e in sourcePackage.Exports.Where(x => x.ClassName == "ObjectReferencer"))
            {
                pe.BusyText = $"Porting {e.ObjectName}";
                var report = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, e, package, null, true, out _, targetGameDonorDB: db);
                if (report.Any())
                {
                    //Debugger.Break();
                }
            }

            CorrectSequences(package, new PackageCache());

            pe.BusyText = $"Saving {packName}";
            package.Save();
        }

        private static void PostPortingCorrections(IMEPackage me1File, IMEPackage le1File)
        {
            // Corrections to run AFTER porting is done
            using PackageCache pc = new PackageCache();
            CorrectNeverStream(le1File);
            CorrectPrefabSequenceClass(le1File);
            CorrectSequences(le1File, pc);
            CorrectPathfindingNetwork(me1File, le1File);
            RebuildPersistentLevelChildren(le1File.FindExport("TheWorld.PersistentLevel"));

            //CorrectTriggerStreamsMaybe(me1File, le1File);
        }

        private static void CorrectTriggerStreamsMaybe(IMEPackage me1File, IMEPackage le1File)
        {
            foreach (var lExp in le1File.Exports.Where(x => x.HasStack))
            {
                var mExp = me1File.FindExport(lExp.InstancedFullPath);
                if (mExp != null)
                {
                    var lData = lExp.Data;
                    var mData = mExp.Data;
                    lData.OverwriteRange(0x10, mData.Slice(0x10, 2)); // LatentAction?
                    lExp.Data = lData;
                }
            }
        }

        // ME1 -> LE1 Prefab's Sequence class was changed to a subclass. No different props though.
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
                    export.Class = prefabSeqClass;
                }
            }
        }

        private static void CorrectPathfindingNetwork(IMEPackage me1File, IMEPackage le1File)
        {
            var le1PL = le1File.FindExport("TheWorld.PersistentLevel");
            Level me1L = ObjectBinary.From<Level>(me1File.FindExport("TheWorld.PersistentLevel"));
            Level le1L = ObjectBinary.From<Level>(le1PL);

            // Chain start and end
            if (me1L.NavListStart.value != 0 && le1File.FindExport(me1File.GetUExport(me1L.NavListStart.value).InstancedFullPath) is { } matchingNavStart)
            {
                le1L.NavListStart = new UIndex(matchingNavStart.UIndex);
            }

            if (me1L.NavListEnd.value != 0 && le1File.FindExport(me1File.GetUExport(me1L.NavListEnd.value).InstancedFullPath) is { } matchingNavEnd)
            {
                le1L.NavListEnd = new UIndex(matchingNavEnd.UIndex);
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
            #region ReachSpecs

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

                    // Test properties
                    le1Exp.GetProperties();
                }
            }
            #endregion

            le1PL.WriteBinary(le1L);
        }

        private static void VTestFilePorting(IMEPackage sourcePackage, IMEPackage destPackage, IEnumerable<ExportEntry> itemsToPort, ObjectInstanceDB db, PackageEditorWindow pe)
        {
            // PRECORRECTION - CORRECTIONS TO THE SOURCE FILE BEFORE PORTING
            PrePortingCorrections(sourcePackage);

            // PORTING ACTORS
            var le1PL = destPackage.FindExport("TheWorld.PersistentLevel");
            foreach (var e in itemsToPort)
            {

                pe.BusyText = $"Porting {e.ObjectName}";
                var report = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, e, destPackage,
                    le1PL, true, out _, targetGameDonorDB: db);
            }

            // POSTCORRECTION - CORECTIONS AFTER EVERYTHING HAS BEEN PORTED
            PostPortingCorrections(sourcePackage, destPackage);

            pe.BusyText = "Saving package";
            destPackage.Save();
        }

        private static void PrePortingCorrections(IMEPackage sourcePackage)
        {
            // Strip static mesh light maps since they don't work crossgen. Strip them from
            // the source so they don't port
            foreach (var exp in sourcePackage.Exports)
            {
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
                else if (exp.ClassName == "TerrainComponent")
                {
                    // Strip Lightmap
                    var b = ObjectBinary.From<TerrainComponent>(exp);
                    b.LightMap = new LightMap() { LightMapType = ELightMapType.LMT_None };
                    exp.WriteBinary(b);

                    // Make dynamic lighting
                    var props = exp.GetProperties();
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
                #endregion
                else if (exp.ClassName == "BioTriggerStream")
                {
                    PreCorrectBioTriggerStream(exp);
                }
                else if (exp.ClassName == "BioWorldInfo")
                {
                    // Remove streaminglevels that don't do anything
                    //PreCorrectBioWorldInfoStreamingLevels(exp);
                }



                if (exp.IsA("Actor"))
                {
                    exp.RemoveProperty("m_oAreaMap"); // Remove this when stuff is NOT borked up
                    exp.RemoveProperty("Base"); // No bases
                    exp.RemoveProperty("nextNavigationPoint"); // No bases
                }
            }
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


        // Files we know are referenced by do not exist
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

        private static void PreCorrectBioTriggerStream(ExportEntry triggerStream)
        {
            // Older games (ME1 at least) can reference levels that don't exist. This didn't breka game. Later games this does break. Maybe. IDK. Game dies a lot for no apparent reason
            // has a bunch of level references that don't exist

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

        private static void RebuildPersistentLevelChildren(ExportEntry pl)
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


        /// <summary>
        /// Ports a level for VTest. Saves package.
        /// </summary>
        /// <param name="mapName"></param>
        /// <param name="sourceName"></param>
        /// <param name="finalDestDir"></param>
        /// <param name="sourceDir"></param>
        /// <param name="db"></param>
        /// <param name="pe"></param>
        /// <param name="syncBioWorldInfo"></param>
        /// <param name="portMainSequence"></param>

        private static void PortVTestLevel(string mapName, string sourceName, string finalDestDir, string sourceDir, ObjectInstanceDB db, PackageEditorWindow pe, bool syncBioWorldInfo = false, bool portMainSequence = false, bool enableDynamicLighting = false)
        {
            var outputFile = $@"{finalDestDir}\{sourceName.ToUpper()}.pcc";
            CreateEmptyLevel(outputFile, MEGame.LE1);

            using var le1File = MEPackageHandler.OpenMEPackage(outputFile);
            using var me1File = MEPackageHandler.OpenMEPackage($@"{sourceDir}\{mapName}\{sourceName}.SFM");

            // BIOC_BASE -> SFXGame
            var bcBaseIdx = me1File.findName("BIOC_Base");
            me1File.replaceName(bcBaseIdx, "SFXGame");

            var itemsToPort = new List<ExportEntry>();

            if (syncBioWorldInfo)
            {
                itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "PlayerStart"));
                itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "BioTriggerStream"));
            }

            // Once we are confident in porting we will just take the actor list from PersistentLevel
            // For now just port these
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "InterpActor"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "BioInert"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "BioUsable"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "BioPawn"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "SkeletalMeshActor"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "PostProcessVolume"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "BioMapNote"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "Note"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "BioTrigger"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "BioSunActor"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "BlockingVolume"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "BioDoor"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "StaticMeshCollectionActor"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "StaticLightCollectionActor"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "ReverbVolume"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "BioAudioVolume"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "AmbientSound"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "BioLedgeMeshActor"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "BioStage"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "HeightFog"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "PrefabInstance"));
            itemsToPort.AddRange(me1File.Exports.Where(x => x.indexValue != 0 && x.ClassName == "CameraActor"));

            VTestFilePorting(me1File, le1File, itemsToPort, db, pe);

            // Replace BioWorldInfo if requested
            if (syncBioWorldInfo)
            {
                var me1BWI = me1File.Exports.FirstOrDefault(x => x.ClassName == "BioWorldInfo");
                if (me1BWI != null)
                {
                    me1BWI.indexValue = 1;
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular, me1BWI, le1File, le1File.FindExport(@"TheWorld.PersistentLevel.BioWorldInfo_0"), true, out _, importExportDependencies: true, targetGameDonorDB: db);
                }
            }

            // Replace Main_Sequence if requested
            if (portMainSequence)
            {
                pe.BusyText = "Porting sequencing...";
                var dest = le1File.FindExport(@"TheWorld.PersistentLevel.Main_Sequence");
                EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.ReplaceSingular, me1File.FindExport(@"TheWorld.PersistentLevel.Main_Sequence"), le1File, dest, true, out _, importExportDependencies: true, targetGameDonorDB: db);
            }

            PostPortingCorrections(me1File, le1File);

            if (enableDynamicLighting)
            {
                PackageEditorExperimentsS.CreateDynamicLighting(le1File, true);
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

        private static void CorrectSequences(IMEPackage le1File, PackageCache pc)
        {
            // Find sequences that aren't in other sequences
            foreach (var seq in le1File.Exports.Where(e => e is { ClassName: "Sequence" } && !e.Parent.IsA("SequenceObject")))
            {
                CorrectSequenceObjects(seq, pc);
            }
        }

        private static void CorrectSequenceObjects(ExportEntry seq, PackageCache pc = null)
        {
            pc ??= new PackageCache();
            // Set ObjInstanceVersions to LE value
            if (seq.IsA("SequenceObject"))
            {
                if (LE1UnrealObjectInfo.SequenceObjects.TryGetValue(seq.ClassName, out var soi))
                {
                    seq.WriteProperty(new IntProperty(soi.ObjInstanceVersion, "ObjInstanceVersion"));
                }

                var children = seq.GetChildren();
                foreach (var child in children)
                {
                    if (child is ExportEntry chExp)
                    {
                        CorrectSequenceObjects(chExp, pc);
                    }
                }
            }

            // Fix extra four bytes after SeqAct_Interp
            if (seq.ClassName == "SeqAct_Interp")
            {
                seq.WriteBinary(Array.Empty<byte>());
            }

            // Fix missing PropertyNames on VariableLinks
            if (seq.IsA("SequenceOp"))
            {
                var defaultProperties =
                    SequenceObjectCreator.GetSequenceObjectDefaults(seq.FileRef, seq.ClassName, seq.Game, pc);
                var defaultVarLinks = defaultProperties.GetProp<ArrayProperty<StructProperty>>("VariableLinks");

                var varLinks = seq.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
                if (varLinks is null || defaultVarLinks is null) return;
                foreach (var t in varLinks.Values)
                {
                    string desc = t.GetProp<StrProperty>("LinkDesc").Value;
                    var defaultLink = defaultVarLinks.Values.FirstOrDefault(property =>
                        property.GetProp<StrProperty>("LinkDesc").Value == desc);
                    if (defaultLink != null)
                    {
                        var propertyName = defaultLink.GetProp<NameProperty>("PropertyName");
                        var existingLink = t.GetProp<NameProperty>("PropertyName");
                        if (existingLink.Value == "None" && propertyName.Value != "None")
                        {
                            t.Properties.AddOrReplaceProp(propertyName);
                        }
                    }
                }

                seq.WriteProperty(varLinks);
            }
        }

        private static void CreateEmptyLevel(string outpath, MEGame game)
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
            var objectDB = new ObjectInstanceDB();
            Task.Run(() =>
            {
                pe.SetBusy("Building chonky DB");
                var allPackages = MELoadedFiles.GetFilesLoadedInGame(game).ToList();
                int numDone = 0;
                foreach (var f in allPackages)
                {
                    pe.BusyText = $"Indexing file [{++numDone}/{allPackages.Count}]";
                    using var package = MEPackageHandler.OpenMEPackage(f.Value);

                    IndexFileForObjDB(objectDB, game, package);
                }

                // Compile the database
                pe.BusyText = "Compiling database";
                File.WriteAllText(AppDirectories.GetObjectDatabasePath(game), objectDB.Serialize());

            }).ContinueWithOnUIThread(result => { pe.EndBusy(); });
        }

        private static void IndexFileForObjDB(ObjectInstanceDB objectDB, MEGame game, IMEPackage package)
        {
            // Index package path
            int packageNameIndex;
            if (package.FilePath.StartsWith(MEDirectories.GetDefaultGamePath(game)))
            {
                // Get relative path
                packageNameIndex = objectDB.GetNameTableIndex(package.FilePath.Substring(MEDirectories.GetDefaultGamePath(game).Length + 1));
            }
            else
            {
                // Store full path
                packageNameIndex = objectDB.GetNameTableIndex(package.FilePath);
            }

            // Index objects
            foreach (var exp in package.Exports)
            {
                var ifp = exp.InstancedFullPath;

                // Things to ignore
                if (ifp.StartsWith(@"TheWorld"))
                    continue;
                if (ifp.StartsWith(@"ObjectReferencer"))
                    continue;

                // Index it
                objectDB.AddRecord(ifp, packageNameIndex);
            }
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

                var packageName = Path.GetFileNameWithoutExtension(sourceFile);
                var link = ExportCreator.CreatePackageExport(destP, packageName);

                List<EntryStringPair> results = new List<EntryStringPair>();
                foreach (var v in vegasP.Exports.Where(x => x.IsClass))
                {
                    results.AddRange(EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, v, destP, null, true, out _));
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
    }
}