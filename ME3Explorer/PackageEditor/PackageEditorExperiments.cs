using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gammtek.Conduit.Extensions.IO;
using ImageMagick;
using MassEffectModder.Images;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Explorer.Unreal.Classes;

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
        /// Shifts an ME1 AnimCutscene by specified X Y Z values. Only supports 96NoW (3 32-bit float) animations
        /// By Mgamerz 
        /// </summary>
        /// <param name="export"></param>
        public static void ShiftME1AnimCutscene(ExportEntry export)
        {
            var offsetX = int.Parse(PromptDialog.Prompt(null, "Enter X offset", "Offset X", "0", true));
            var offsetY = int.Parse(PromptDialog.Prompt(null, "Enter Y offset", "Offset Y", "0", true));
            var offsetZ = int.Parse(PromptDialog.Prompt(null, "Enter Z offset", "Offset Z", "0", true));
            var numFrames = export.GetProperty<IntProperty>("NumFrames");
            var TrackOffsets = export.GetProperty<ArrayProperty<IntProperty>>("CompressedTrackOffsets");
            var bin = export.GetBinaryData();
            var mem = new MemoryStream(bin);
            var len = mem.ReadInt32();
            var animBinStart = mem.Position;
            for (int i = 0; i < TrackOffsets.Count; i++)
            {
                var bonePosOffset = TrackOffsets[i].Value;
                i++;
                var bonePosCount = TrackOffsets[i].Value;
                //var BoneID = new BinInterpNode
                //{
                //    Header = $"0x{offset:X5} Bone: {bone} {boneList[bone].Value}",
                //    Name = "_" + offset,
                //    Tag = NodeType.Unknown
                //};
                //subnodes.Add(BoneID);

                for (int j = 0; j < bonePosCount; j++)
                {
                    var offset = mem.Position;

                    var posX = mem.ReadSingle();
                    mem.Position -= 4;
                    mem.WriteSingle(posX + offsetX);

                    var posY = mem.ReadSingle();
                    mem.Position -= 4;
                    mem.WriteSingle(posY + offsetY);

                    var posZ = mem.ReadSingle();
                    mem.Position -= 4;
                    mem.WriteSingle(posZ + offsetZ);

                    Debug.WriteLine($"PosKey {j}: X={posX},Y={posY},Z={posZ}");
                }

                //rotation
                i++;
                var boneRotOffset = TrackOffsets[i].Value;
                i++;
                var boneRotCount = TrackOffsets[i].Value;

                //only support 96NoW
                mem.Position = boneRotOffset + animBinStart;
                for (int j = 0; j < boneRotCount; j++)
                {
                    var offset = mem.Position;
                    var rotX = mem.ReadSingle();
                    var rotY = mem.ReadSingle();
                    var rotZ = mem.ReadSingle();
                    Debug.WriteLine($"RotKey {j}: X={rotX},Y={rotY},Z={rotZ}");
                }
            }

            export.SetBinaryData(mem.ToArray());
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
                    image.Modulate(new Percentage(82), new Percentage(100),new Percentage(0));
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
    }
}
