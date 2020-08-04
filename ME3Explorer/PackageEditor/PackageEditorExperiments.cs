using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Gammtek.Conduit;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.Extensions.Collections.Generic;
using Gammtek.Conduit.Extensions.IO;
using ImageMagick;
using MassEffectModder.Images;
using ME3Explorer.AssetDatabase;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Explorer.Unreal.Classes;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using SharpDX;
using SQLite.Extensions;

namespace ME3Explorer.PackageEditor
{
    /// <summary>
    /// Class where toolset devs dump their testing code that may or may not be useful in the future.
    /// </summary>
    class PackageEditorExperiments
    {
        /// <summary>
        /// Builds a comparison of TESTPATCH functions against their original design. View the difference with WinMerge Folder View.
        /// By Mgamerz
        /// </summary>
        public static void BuildTestPatchComparison()
        {
            var oldPath = ME3Directory.gamePath;
            // To run this change these values

            // Point to unpacked path.
            ME3Directory.gamePath = @"Z:\Mass Effect 3";
            var patchedOutDir = Directory.CreateDirectory(@"C:\users\mgamerz\desktop\patchcomp\patch").FullName;
            var origOutDir = Directory.CreateDirectory(@"C:\users\mgamerz\desktop\patchcomp\orig").FullName;
            var patchFiles = Directory.GetFiles(@"C:\Users\Mgamerz\Desktop\ME3CMM\data\Patch_001_Extracted\BIOGame\DLC\DLC_TestPatch\CookedPCConsole", "Patch_*.pcc");

            // End variables

            //preload these packages to speed up lookups
            using var package1 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "SFXGame.pcc"));
            using var package2 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "Engine.pcc"));
            using var package3 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "Core.pcc"));
            using var package4 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "Startup.pcc"));
            using var package5 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "GameFramework.pcc"));
            using var package6 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "GFxUI.pcc"));
            using var package7 = MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "BIOP_MP_COMMON.pcc"));

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
{"SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_KroganPassive_Vanguard", "SFXPower_KroganVanguardPassive"},
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
{"SFXGameContentDLC_CON_MP1.SFXPowerCustomActionMP_AsariCommandoPassive", "SFXPower_AsariCommandoPassive"},
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
{"SFXGameContentDLC_CON_GUN01.SFXWeapon_SniperRifle_Turian_GUN01", "SFXWeapon_SniperRifle_Turian_GUN01"},
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
                    ImportEntry ie = new ImportEntry(classExp.FileRef)
                    {
                        ClassName = classExp.ClassName,
                        ObjectName = classExp.ObjectName,
                        PackageFile = classExp.ParentName,
                        idxLink = classExp.idxLink
                    };
                    Debug.WriteLine("Looking up patch source " + classExp.InstancedFullPath);
                    ExportEntry matchingExport = null;
                    if (extraMappings.TryGetValue(classExp.FullPath, out var lookAtFname) && gameFiles.TryGetValue(lookAtFname + ".pcc", out var fullpath))
                    {
                        using var newP = MEPackageHandler.OpenMEPackage(fullpath);
                        var lookupCE = newP.Exports.FirstOrDefault(x => x.FullPath == classExp.FullPath);
                        if (lookupCE != null)
                        {
                            matchingExport = lookupCE;
                        }
                    }
                    else if (gameFiles.TryGetValue(classExp.ObjectName.Name.Replace("SFXPowerCustomAction", "SFXPower") + ".pcc", out var fullpath2))
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
                    else if (gameFiles.TryGetValue(classExp.ObjectName.Name.Replace("SFXPowerCustomActionMP", "SFXPower") + ".pcc", out var fullpath3))
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
                                    var outname = $"{localFunc.FullPath} {Path.GetFileName(pf)}_{localFunc.UIndex}__{Path.GetFileName(v.FileRef.FilePath)}_{v.UIndex}.txt";
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
            ME3Directory.gamePath = oldPath;
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

            var lines = exportNameSignatureMapping.Select(x => $"{x.Key}============================================================\n{x.Value}");
            File.WriteAllLines(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "fullfunctionsignatures.txt"), lines);
        }


        /// <summary>
        /// Extracts all NoramlizedAverateColors, tints them, and then reinstalls them to the export they came from
        /// </summary>
        /// <param name="Pcc"></param>
        public static void TintAllNormalizedAverageColors(IMEPackage Pcc)
        {
            var normalizedExports = Pcc.Exports
                .Where(x => x.ClassName == "LightMapTexture2D" && x.ObjectName.Name.StartsWith("NormalizedAverageColor")).ToList();
            foreach (var v in normalizedExports)
            {
                MemoryStream pngImage = new MemoryStream();
                Texture2D t2d = new Texture2D(v);
                t2d.ExportToPNG(outStream: pngImage);
                pngImage.Position = 0; //reset
                MemoryStream outStream = new MemoryStream();
                using (var image = new MagickImage(pngImage))
                {

                    var tintColor = MagickColor.FromRgb((byte)128, (byte)0, (byte)0);
                    //image.Colorize(tintColor, new Percentage(80), new Percentage(5), new Percentage(5) );
                    //image.Settings.FillColor = tintColor;
                    //image.Tint("30%", tintColor);
                    image.Modulate(new Percentage(82), new Percentage(100), new Percentage(0));
                    //image.Colorize(tintColor, new Percentage(100), new Percentage(0), new Percentage(0) );
                    image.Write(outStream, MagickFormat.Png32);
                }
                //outStream = pngImage;
                outStream.Position = 0;
                outStream.WriteToFile(Path.Combine(Directory.CreateDirectory(@"C:\users\mgame\desktop\normalizedCols").FullName, v.ObjectName.Instanced + ".png"));
                var convertedBackImage = new MassEffectModder.Images.Image(outStream, Image.ImageFormat.PNG);
                t2d.Replace(convertedBackImage, t2d.Export.GetProperties());
            }
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


        /// <summary>
        /// Copy ME2 Static art and collision into an ME3 file.
        /// By Kinkojiro
        /// </summary>
        /// <param name="Game">Target Game</param>
        /// <param name="BioPSource">Source BioP</param>
        /// <param name="me3Outputfolder">OutputFolder</param>
        /// <param name="BioArtsToCopy">List of level source file locations</param>
        /// <param name="ActorsToMove">Dicitionary: key Actors, value filename, entry uid</param>
        /// <param name="AssetsToMove">Dictionary key: AssetInstancedPath, value filename, isimport, entry uid</param>
        /// <param name="fromreload">is reloaded json</param>
        public async static Task<bool> ConvertLevelToGame(MEGame Game, IMEPackage BioPSource, string me3Outputfolder, LevelConversionData conversionData = null, bool fromreload = false)
        {
            //VARIABLES / VALIDATION
            var actorclassesToMove = new List<string>() { "BlockingVolume", "SpotLight", "SpotLightToggleable", "PointLight", "PointLightToggleable", "SkyLight", "HeightFog", "LenseFlareSource", "StaticMeshActor", "BioTriggerStream" };
            var actorclassesToSubstitue = new List<(string, string)>() { ("BioBlockingVolume", "BlockingVolume") };

            if (BioPSource.Game != MEGame.ME2 || ME2Directory.gamePath == null)
            {
                MessageBox.Show("Currently art can only be copied from ME2 to ME3");
                return false;
            }

                        
            //STAGE 1-A: Get filelist from BioP, Save a copy in outputdirectory
            if (!fromreload)
            {
                conversionData = new LevelConversionData(Game, BioPSource.Game, null, null, new ConcurrentDictionary<string, string>(), new ConcurrentDictionary<string, (string, int)>(), new ConcurrentDictionary<string, (string, int)>());
                var supportedExtensions = new List<string> { ".pcc", ".u", ".upk", ".sfm" };
                if (Path.GetFileName(BioPSource.FilePath).ToLowerInvariant().StartsWith("biop_") && BioPSource.Exports.FirstOrDefault(x => x.ClassName == "BioWorldInfo") is ExportEntry BioWorld)
                {
                    string biopname = Path.GetFileNameWithoutExtension(BioPSource.FilePath);
                    conversionData.GameLevelName = biopname.Substring(5, biopname.Length - 5);
                    var lsks = BioWorld.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels").ToList();
                    foreach (var l in lsks)
                    {
                        var lskexp = BioPSource.GetUExport(l.Value);
                        var filename = lskexp.GetProperty<NameProperty>("PackageName");
                        if ((filename?.Value.ToString().ToLowerInvariant().StartsWith("bioa") ?? false) || (filename?.Value.ToString().ToLowerInvariant().StartsWith("biod") ?? false))
                        {
                            var filePath = Directory.GetFiles(ME2Directory.gamePath, $"{filename.Value}.pcc", SearchOption.AllDirectories).FirstOrDefault();
                            conversionData.FilesToCopy.TryAdd(filename.Value, filePath);
                        }
                    }
                    conversionData.BioPSource = $"{biopname}_{BioPSource.Game}.pcc";
                    BioPSource.Save(Path.Combine(me3Outputfolder, conversionData.BioPSource));
                }
                else
                {
                    MessageBox.Show("Requires an ME2 BioP to work with.");
                    return false;
                }

                //STAGE 1-B: Check each file in List

                Parallel.ForEach(conversionData.FilesToCopy, (pccref) => {
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
                                //Get asset references
                                HashSet<int> actorrefs = pcc.GetUnReferencedEntries(true, actor);
                                foreach (var r in actorrefs)
                                {
                                    var objref = pcc.GetEntry(r);
                                    if (objref != null)
                                    {
                                        if (objref.InstancedFullPath.Contains("PersistentLevel"))  //remove components of actors
                                            continue;
                                        var added = conversionData.AssetsToMove.TryAdd<string, (string, int)>(objref.InstancedFullPath, (pccref.Key, r));
                                        if (!added && r > 0 && conversionData.AssetsToMove[objref.InstancedFullPath].Item2 < 0) //Replace  imports with exports if possible
                                        {
                                            conversionData.AssetsToMove[objref.InstancedFullPath] = (pccref.Key, r);
                                        }
                                    }
                                }
                            }
                        }
                    }
                });

                //Add assets from BioP into list if exports are in source
                foreach( var assetImport in conversionData.AssetsToMove.Where(t => t.Value.Item2 < 0))
                {
                    var biopExport = BioPSource.Exports.FirstOrDefault(x => x.InstancedFullPath == assetImport.Key);
                    if(biopExport != null)
                    {
                        conversionData.AssetsToMove[assetImport.Key] = (BioPSource.FilePath, biopExport.UIndex);
                    }
                }


            }

            var SRCTempFileName = Path.Combine(me3Outputfolder, $"{conversionData.GameLevelName}_TempAssets_{conversionData.SourceGame}.pcc");
            var TGTTempFileName = Path.Combine(me3Outputfolder, $"{conversionData.GameLevelName}_TempAssets_{conversionData.TargetGame}.pcc");
            if (!fromreload)
            {
                //Create ME2TempAssetsFile & ME3TempAssetsFile
                MEPackageHandler.CreateAndSavePackage(SRCTempFileName, conversionData.SourceGame);

                using (var me2assetfile = MEPackageHandler.OpenME2Package(SRCTempFileName))
                {
                    if(me2assetfile is MEPackage me2assetpack)
                    {
                        //CopyAssets


                        //ConvertAssetFiletoME3  --> how to feed back changes to AssetsToMove list
                        me2assetpack.ConvertTo(conversionData.TargetGame);
                        me2assetpack.Save(TGTTempFileName);
                    }
                }

                //Export Settings to JSON
                var srldata = JsonConvert.SerializeObject(conversionData);
                using (StreamWriter writer = File.CreateText(Path.Combine(me3Outputfolder, $"{conversionData.GameLevelName}_Transfer.json")))
                {
                    writer.Write(srldata);
                }
            }

            //Create cooked files and populate with actors/assets
            Parallel.ForEach(conversionData.FilesToCopy, (pccref) =>
            {
                var targetfile = Path.Combine(me3Outputfolder, Path.GetFileName(pccref.Value));
                if (File.Exists(targetfile))
                {
                    File.Delete(targetfile);
                }


                File.Copy(Path.Combine(App.ExecFolder, "ME3EmptyLevel.pcc"), targetfile);
                using (var target = MEPackageHandler.OpenME3Package(targetfile))
                using (var donor = MEPackageHandler.OpenME2Package(pccref.Value))
                using (var assetfile = MEPackageHandler.OpenME3Package(TGTTempFileName))
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

                    //Get list of assets for this file & Process into
                    var sourceassets = conversionData.AssetsToMove.Where(s => s.Value.Item1 == pccref.Key).ToList();
                    foreach(var asset in sourceassets)
                    {
                        var assetxp = assetfile.Exports.FirstOrDefault(x => x.InstancedFullPath == asset.Key);
                        if(assetxp != null)
                        {
                            //Clone to target [Including refs as these will have to be added later anyway].
                        }
                    }

                    //Get list of actors and process
                    var sourceactors = conversionData.ActorsToMove.Where(a => a.Value.Item1 == pccref.Key).ToList();
                    foreach(var sactor in sourceactors)
                    {
                        var sactorxp = donor.GetEntry(sactor.Value.Item2);
                        if (sactorxp != null)
                        {
                            //Clone to target, add to level (only component tree).  Assets should already be there and relink. 
                            //sactorxp.Clone()
                        }

                    }

                    target.Save();
                }
            });



            return true;

        }

        public async static Task<bool> RecookTransferLevelsFromJSON(string jsonfile)
        {
            var OutputDir = Path.GetDirectoryName(jsonfile);
            var conversionData = new LevelConversionData(MEGame.ME3, MEGame.ME2, null, null, new ConcurrentDictionary<string, string>(), new ConcurrentDictionary<string, (string, int)>(), new ConcurrentDictionary<string, (string, int)>());
            IMEPackage sourcebiop = null;

            using (StreamReader file = File.OpenText(jsonfile))
            {
                JsonSerializer serializer = new JsonSerializer();
                conversionData = (LevelConversionData)serializer.Deserialize(file, typeof(LevelConversionData));
            }

            switch(conversionData.SourceGame)
            {
                case MEGame.ME2:
                    sourcebiop = MEPackageHandler.OpenME2Package(Path.Combine(OutputDir, conversionData.BioPSource));
                    if(sourcebiop.IsNull())
                    {
                        MessageBox.Show("Source BioP not found.  Aborting.");
                        return false;
                    }
                    break;
                default:
                    MessageBox.Show("Currently level transfers only can be done ME2 to ME3");
                    return false;
            }


            return await ConvertLevelToGame(conversionData.TargetGame, sourcebiop, OutputDir, conversionData, true);
        }

        public class LevelConversionData
        {
            public MEGame TargetGame = MEGame.ME3;
            public MEGame SourceGame = MEGame.ME2;
            public string GameLevelName = null;
            public string BioPSource = null;
            public ConcurrentDictionary<string, string> FilesToCopy = new ConcurrentDictionary<string, string>();
            public ConcurrentDictionary<string, (string, int)> ActorsToMove = new ConcurrentDictionary<string, (string, int)>();
            public ConcurrentDictionary<string, (string, int)> AssetsToMove = new ConcurrentDictionary<string, (string, int)>();

            public LevelConversionData(MEGame TargetGame, MEGame SourceGame, string GameLevelName, string BioPSource, ConcurrentDictionary<string, string> FilesToCopy, ConcurrentDictionary<string, (string, int)> ActorsToMove, ConcurrentDictionary<string, (string, int)> AssetsToMove)
            {
                this.TargetGame = TargetGame;
                this.SourceGame = SourceGame;
                this.GameLevelName = GameLevelName;
                this.BioPSource = BioPSource;
                this.FilesToCopy.AddRange(FilesToCopy);
                this.ActorsToMove.AddRange(ActorsToMove);
                this.AssetsToMove.AddRange(AssetsToMove);
            }

            public LevelConversionData()
            {

            }
        }
        
    }
}
