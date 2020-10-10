using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal.Classes;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.ME1.Unreal.UnhoodBytecode;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3ExplorerCore.Unreal.Classes;
using Newtonsoft.Json;
using SharpDX; 
//using ImageMagick;

namespace ME3Explorer.PackageEditor.Experiments
{
    /// <summary>
    /// Class where Mgamerz can put debug/dev/experimental code
    /// </summary>
    class PackageEditorExperimentsM
    {

        public static void PortWiiUBSP()
        {


            //var me1emf = @"D:\Origin Games\Mass Effect\BioGame\CookedPC\Maps\entrymenu.sfm";
            //var me1em = MEPackageHandler.OpenMEPackage(me1emf);
            //var gmplanet01 = me1em.GetUExport(940);
            //var itm = me1em.GetUExport(966);
            //var moon = me1em.GetUExport(936);
            //var planetPos = SharedPathfinding.GetLocation(gmplanet01);
            //var moonPos = SharedPathfinding.GetLocation(moon);
            //var cameraPoint = SharedPathfinding.GetLocationFromVector(itm.GetProperty<StructProperty>("PosTrack").GetProp<ArrayProperty<StructProperty>>("Points")[0].GetProp<StructProperty>("OutVal"));
            //var cameraEuler = SharedPathfinding.GetLocationFromVector(itm.GetProperty<StructProperty>("EulerTrack").GetProp<ArrayProperty<StructProperty>>("Points")[0].GetProp<StructProperty>("OutVal"));

            //Point3D me2planetPos = new Point3D()
            //{
            //    X = -5402.598,
            //    Y = 13571.81,
            //    Z = -40187.2
            //};



            //Debug.WriteLine("Place moon at:");
            //var diff = moonPos.getDelta(planetPos);
            //var newpos = me2planetPos.applyDelta(diff);

            //Debug.WriteLine("X: " + newpos.X);
            //Debug.WriteLine("Y: " + newpos.Y);
            //Debug.WriteLine("Z: " + newpos.Z);

            //Debug.WriteLine("Set Camera rotation:");


            //Debug.WriteLine("Pitch: " + cameraEuler.X);
            //Debug.WriteLine("Yaw: " + cameraEuler.Y);
            //Debug.WriteLine("Roll: " + cameraEuler.Z);

            return;
            var inputfile = @"D:\Origin Games\Mass Effect 3\BIOGame\CookedPCConsole\BioD_Kro002_925shroud_LOC_INT.pcc";
            var pcc = MEPackageHandler.OpenMEPackage(inputfile, forceLoadFromDisk: true);
            var trackprops = pcc.Exports.Where(x => x.ClassName == "BioEvtSysTrackProp").ToList();
            foreach (var trackprop in trackprops)
            {
                var props = trackprop.GetProperties();
                var findActor = props.GetProp<NameProperty>("m_nmFindActor");
                if (findActor != null && findActor.Value.Name == "Player")
                {
                    var propKeys = props.GetProp<ArrayProperty<StructProperty>>("m_aPropKeys");
                    if (propKeys != null)
                    {
                        foreach (var trackdata in propKeys)
                        {
                            var prop = trackdata.GetProp<NameProperty>("nmProp");
                            if (prop != null && prop.Value.Name == "Pistol_Carnifex")
                            {
                                prop.Value = "Currently_Equipped_Weapon";
                                //maybe have to change weapon class. we'll see
                            }
                        }
                    }
                    Debug.WriteLine($"Wrote {trackprop.InstancedFullPath}");
                    trackprop.WriteProperties(props);
                }
            }
            pcc.Save();
            return;
            Debug.WriteLine("Opening packages");
            var pcEntry = MEPackageHandler.OpenMEPackage(@"X:\BSPPorting\entryMAT.pcc", forceLoadFromDisk: true);
            //var packageToPort = MEPackageHandler.OpenMEPackage(@"X:\BSPPorting\wiiuBSP\Bioa_Cat003_TEMP2.xxx", forceLoadFromDisk: true);
            var packageToPort = MEPackageHandler.OpenMEPackage(@"E:\UDKStuff\testmap.udk");
            //Locate PC level we will attach new exports to
            var pcLevel = pcEntry.Exports.FirstOrDefault(exp => exp.ClassName == "Level");

            //Locate WiiU level we will find assets to port from
            var wiiuLevel = packageToPort.Exports.FirstOrDefault(exp => exp.ClassName == "Level");

            //MODELS FIRST
            Debug.WriteLine("Porting Model");
            var wiiumodels = packageToPort.Exports.Where(x => x.Parent == wiiuLevel && x.ClassName == "Model").ToList();
            //take larger model
            var wiiumodel = wiiumodels.MaxBy(x => x.DataSize);
            var selfRefPositions = new List<(string, int)>();
            var leBinary = BinaryInterpreterWPF.EndianReverseModelScan(wiiumodel, pcEntry, selfRefPositions);
            var availableMaterialsToUse = new[]
            {
                //102, //grass
                //89, //rock
                //142, //night sandy rock //just white
                156 //tile
            };
            var random = new Random();
            var overrideMaterial = pcEntry.GetUExport(availableMaterialsToUse[random.Next(availableMaterialsToUse.Length)]);
            foreach (var selfref in selfRefPositions)
            {
                leBinary.Seek(selfref.Item2, SeekOrigin.Begin);
                switch (selfref.Item1)
                {
                    //case "Self":
                    //    leBinary.WriteInt32(existingExport.UIndex);
                    //    break;
                    //case "MasterModel":
                    //    leBinary.WriteInt32(masterPCModel.UIndex);
                    //    break;
                    case "DefaultMaterial":
                        leBinary.WriteInt32(overrideMaterial.UIndex);
                        break;
                }
            }
            //MemoryStream exportStream = new MemoryStream();
            ////export header
            //exportStream.WriteInt32(-1);
            //exportStream.WriteNameReference("None", pcEntry);
            //leBinary.CopyTo(exportStream);

            //Debug.WriteLine("Big Endian size: " + wiiumodel.DataSize);
            //Debug.WriteLine("LTL endian size: " + exportStream.Length);
            var masterPCModel = pcEntry.GetUExport(8);
            masterPCModel.SetBinaryData(leBinary.ToArray());
            if (masterPCModel.DataSize != wiiumodel.DataSize)
                Debug.WriteLine("ERROR: BINARY NOT SAME LEGNTH!");
            //Port model components
            var modelComponents = packageToPort.Exports.Where(x => x.Parent == wiiuLevel && x.ClassName == "ModelComponent").ToList();
            var availableExistingModelComponents = pcEntry.Exports.Where(x => x.Parent == pcLevel && x.ClassName == "ModelComponent").ToList();
            var modelComponentClass = pcEntry.Imports.First(x => x.ObjectName.Name == "ModelComponent");
            byte[] existingData = null; //hack to just setup new exports
            List<int> addedModelComponents = new List<int>();
            foreach (var modelcomp in modelComponents)
            {
                var existingExport = availableExistingModelComponents.FirstOrDefault();
                if (existingExport == null)
                {
                    //we have no more exports we can use
                    //ExportEntry exp = new ExportEntry()
                    existingExport = new ExportEntry(pcEntry)
                    {
                        Parent = pcLevel,
                        indexValue = modelcomp.indexValue,
                        Class = modelComponentClass,
                        ObjectName = "ModelComponent",
                        Data = existingData
                    };

                    pcEntry.AddExport(existingExport);
                    addedModelComponents.Add(existingExport.UIndex);
                }

                if (existingExport == null) continue; //just skip
                if (existingData == null) existingData = existingExport.Data;
                overrideMaterial = pcEntry.GetUExport(availableMaterialsToUse[random.Next(availableMaterialsToUse.Length)]);
                //overrideMaterial = pcEntry.GetUExport(156);
                availableExistingModelComponents.Remove(existingExport);
                Debug.WriteLine("Porting model component " + modelcomp.InstancedFullPath);
                selfRefPositions = new List<(string, int)>();

                var lightmapsToRemove = new List<(int, int)>();

                leBinary = BinaryInterpreterWPF.EndianReverseModelComponentScan(modelcomp, pcEntry, selfRefPositions, lightmapsToRemove);
                var binstart = existingExport.propsEnd();
                foreach (var selfref in selfRefPositions)
                {
                    leBinary.Seek(selfref.Item2 - binstart, SeekOrigin.Begin);
                    switch (selfref.Item1)
                    {
                        case "Self":
                            leBinary.WriteInt32(existingExport.UIndex);
                            break;
                        case "MasterModel":
                            leBinary.WriteInt32(masterPCModel.UIndex);
                            break;
                        case "DefaultMaterial":
                            leBinary.WriteInt32(overrideMaterial.UIndex);
                            break;
                    }
                }

                MemoryStream strippedLightmapStream = new MemoryStream();
                //strip out lightmaps. We must go in reverse order
                existingExport.SetBinaryData(leBinary.ToArray());
                leBinary.Position = 0;
                leBinary = new MemoryStream(existingExport.Data);

                foreach (var lightmapx in lightmapsToRemove)
                {
                    var datacountstart = lightmapx.Item1;
                    var dataend = lightmapx.Item2;
                    Debug.WriteLine($"Gutting lightmap DATA 0x{lightmapx.Item1:X4} to 0x{lightmapx.Item2:X4}");
                    if (leBinary.Position == 0)
                    {
                        strippedLightmapStream.WriteFromBuffer(leBinary.ReadToBuffer(datacountstart)); //write initial bytes up to first lightmap
                    }
                    else
                    {
                        var amountToRead = datacountstart - (int)leBinary.Position;
                        Debug.WriteLine($"Reading {amountToRead:X5} bytes from source pos 0x{leBinary.Position:X5} to output at 0x{strippedLightmapStream.Position:X6}");
                        strippedLightmapStream.WriteFromBuffer(leBinary.ReadToBuffer(amountToRead)); //write bytes between
                    }

                    Debug.WriteLine($"Copied to 0x{leBinary.Position:X4}");

                    strippedLightmapStream.WriteInt32(0); //LMT_NONE
                    Debug.WriteLine($"Wrote LMNONE DATA at output bin 0x{(strippedLightmapStream.Position - 4):X4}");

                    leBinary.Seek(dataend, SeekOrigin.Begin);
                }

                if (lightmapsToRemove.Count > 0)
                {
                    strippedLightmapStream.WriteFromBuffer(leBinary.ReadFully()); //write the rest of the stream
                }

                existingExport.Data = strippedLightmapStream.ToArray();
                //if (modelcomp.GetBinaryData().Length != leBinary.Length)
                //{
                //    Debug.WriteLine($"WRONG BINARY LENGTH FOR NEW DATA: OLD LEN: 0x{modelcomp.GetBinaryData().Length:X8} NEW LEN: 0x{leBinary.Length:X8}, Difference {(modelcomp.GetBinaryData().Length - leBinary.Length)}");
                //}
                //existingExport.SetBinaryData(leBinary.ToArray());
                existingExport.indexValue = modelcomp.indexValue;
            }

            //Update LEVEL list of ModelComponents
            var modelCompontentsOffset = 0x6A; //# of model components - DATA not BINARY DATA
            var levelBinary = pcLevel.Data;
            var curCount = BitConverter.ToInt32(levelBinary, modelCompontentsOffset);
            levelBinary.OverwriteRange(modelCompontentsOffset, BitConverter.GetBytes(curCount + addedModelComponents.Count)); //write new count

            var splitPoint = modelCompontentsOffset + ((curCount + 1) * 4);
            var preNewStuff = levelBinary.Slice(0, splitPoint);
            var postNewStuff = levelBinary.Slice(splitPoint, levelBinary.Length - splitPoint);
            MemoryStream nstuff = new MemoryStream();
            foreach (var n in addedModelComponents)
            {
                nstuff.WriteInt32(n);
            }

            byte[] newLevelBinary = new byte[levelBinary.Length + nstuff.Length];
            newLevelBinary.OverwriteRange(0, preNewStuff);
            newLevelBinary.OverwriteRange(splitPoint, nstuff.ToArray());
            newLevelBinary.OverwriteRange(splitPoint + (int)nstuff.Length, postNewStuff);

            pcLevel.Data = newLevelBinary;

            pcEntry.Save(@"D:\origin games\mass effect 3\biogame\cookedpcconsole\entrybsp.pcc");


            Debug.WriteLine("Done porting");
        }


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
                if(!eventbin.Links.IsEmpty() && !eventbin.Links[0].WwiseStreams.IsEmpty())
                {
                    var wwstream = Pcc.GetUExport(eventbin.Links[0].WwiseStreams[0]);
                    var streambin = wwstream?.GetBinaryData<WwiseStream>() ?? null;
                    if(streambin != null)
                    {
                        var duration = streambin.GetSoundLength();
                        var durtnMS = wwevent.GetProperty<FloatProperty>("DurationMilliseconds");
                        if (durtnMS != null && duration != null)
                        {
                            durtnMS.Value = (float)duration.Value.TotalMilliseconds;
                            wwevent.WriteProperty(durtnMS);
                        }
                    }
                }
            }
        }

        public static void BuildME1NativeFunctionsInfo()
        {
            if (ME1Directory.gamePath != null)
            {
                var newCachedInfo = new SortedDictionary<int, CachedNativeFunctionInfo>();
                var dir = new DirectoryInfo(ME1Directory.gamePath);
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

                                if (nativeIndex != 0)
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

                File.WriteAllText(Path.Combine(App.ExecFolder, "ME1NativeFunctionInfo.json"),
                    JsonConvert.SerializeObject(new { NativeFunctionInfo = newCachedInfo }, Formatting.Indented));
                Debug.WriteLine("Done");
            }
        }

        public static void FindME1ME22DATables()
        {
            if (ME1Directory.gamePath != null)
            {
                var newCachedInfo = new SortedDictionary<int, CachedNativeFunctionInfo>();
                var dir = new DirectoryInfo(Path.Combine(ME1Directory.gamePath /*, "BioGame", "CookedPC", "Maps"*/));
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
            if (ME3Directory.gamePath != null)
            {
                var newCachedInfo = new SortedDictionary<string, List<string>>();
                var dir = new DirectoryInfo(ME3Directory.gamePath);
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
            if (ME2Directory.gamePath != null)
            {
                var newCachedInfo = new SortedDictionary<string, List<string>>();
                var dir = new DirectoryInfo(ME2Directory.gamePath);
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
    }
}
