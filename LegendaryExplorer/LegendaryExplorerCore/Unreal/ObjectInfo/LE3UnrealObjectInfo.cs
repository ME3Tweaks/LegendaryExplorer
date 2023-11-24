using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.Unreal.ObjectInfo
{
    public static class LE3UnrealObjectInfo
    {

        public static Dictionary<string, ClassInfo> Classes = new();
        public static Dictionary<string, ClassInfo> Structs = new();
        public static Dictionary<string, SequenceObjectInfo> SequenceObjects = new();
        public static Dictionary<string, List<NameReference>> Enums = new();

        private static readonly string[] ImmutableStructs = { "Vector", "Color", "LinearColor", "TwoVectors", "Vector4", "Vector2D", "Rotator", "Guid", "Plane", "Box",
            "Quat", "Matrix", "IntPoint", "ActorReference", "ActorReference", "ActorReference", "PolyReference", "AimComponent", "AimTransform", "AimOffsetProfile", "FontCharacter",
            "CoverReference", "CoverInfo", "CoverSlot", "BioRwBox", "BioMask4Property", "RwVector2", "RwVector3", "RwVector4", "RwPlane", "RwQuat", "BioRwBox44" };

        public static bool IsImmutableStruct(string structName)
        {
            return ImmutableStructs.Contains(structName);
        }

        public static bool IsLoaded;
        public static void loadfromJSON(string jsonTextOverride = null)
        {
            if (!IsLoaded)
            {
                LECLog.Information(@"Loading property db for LE3");
                try
                {
                    var infoText = jsonTextOverride ?? ObjectInfoLoader.LoadEmbeddedJSONText(MEGame.LE3);
                    if (infoText != null)
                    {
                        var blob = JsonConvert.DeserializeAnonymousType(infoText, new { SequenceObjects, Classes, Structs, Enums });
                        SequenceObjects = blob.SequenceObjects;
                        Classes = blob.Classes;
                        Structs = blob.Structs;
                        Enums = blob.Enums;
                        AddCustomAndNativeClasses(Classes, SequenceObjects);
                        foreach ((string className, ClassInfo classInfo) in Classes)
                        {
                            classInfo.ClassName = className;
                        }
                        foreach ((string className, ClassInfo classInfo) in Structs)
                        {
                            classInfo.ClassName = className;
                        }
                        IsLoaded = true;
                    }
                }
                catch (Exception ex)
                {
                    LECLog.Error($@"Property database load failed for LE3: {ex.Message}");
                    return;
                }
            }
        }
        
        public static PropertyInfo getPropertyInfo(string className, NameReference propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null, bool reSearch = true, ExportEntry containingExport = null)
        {
            if (className.StartsWith("Default__", StringComparison.OrdinalIgnoreCase))
            {
                className = className.Substring(9);
            }
            Dictionary<string, ClassInfo> temp = inStruct ? Structs : Classes;
            bool infoExists = temp.TryGetValue(className, out ClassInfo info);
            if (!infoExists && nonVanillaClassInfo != null)
            {
                info = nonVanillaClassInfo;
                infoExists = true;
            }
            
            // 07/18/2022 - If during property lookup we are passed a class 
            // that we don't know about, generate and use it, since it will also have superclass info
            // For example looking at a custom subclass in Interpreter, this code will resolve the ???'s
            // //09/23/2022 - Fixed Classes[] = assignment using the class name rather than the containing name. This would make future
            // lookups wrong.
            // - Mgamerz
            if (!infoExists && !inStruct && containingExport != null && containingExport.IsDefaultObject && containingExport.Class is ExportEntry classExp)
            {
                info = generateClassInfo(classExp, false);
                Classes[containingExport.ClassName] = info;
                infoExists = true;
            }

            if (infoExists) //|| (temp = !inStruct ? Structs : Classes).ContainsKey(className))
            {
                //look in class properties
                if (info.properties.TryGetValue(propName, out var propInfo))
                {
                    return propInfo;
                }
                else if (nonVanillaClassInfo != null && nonVanillaClassInfo.properties.TryGetValue(propName, out var nvPropInfo))
                {
                    // This is called if the built info has info the pre-parsed one does. This especially is important for PS3 files 
                    // Cause the LE3 DB isn't 100% accurate for ME1/ME2 specific classes, like biopawn
                    return nvPropInfo;
                }
                //look in structs

                if (inStruct)
                {
                    foreach (PropertyInfo p in info.properties.Values())
                    {
                        if ((p.Type is PropertyType.StructProperty or PropertyType.ArrayProperty) && reSearch)
                        {
                            reSearch = false;
                            PropertyInfo val = getPropertyInfo(p.Reference, propName, true, nonVanillaClassInfo, reSearch);
                            if (val != null)
                            {
                                return val;
                            }
                        }
                    }
                }
                //look in base class
                if (temp.ContainsKey(info.baseClass))
                {
                    PropertyInfo val = getPropertyInfo(info.baseClass, propName, inStruct, nonVanillaClassInfo);
                    if (val != null)
                    {
                        return val;
                    }
                }
                else
                {
                    //Baseclass may be modified as well...
                    if (containingExport?.SuperClass is ExportEntry parentExport)
                    {
                        //Class parent is in this file. Generate class parent info and attempt refetch
                        return getPropertyInfo(parentExport.SuperClassName, propName, inStruct, generateClassInfo(parentExport), reSearch: true, parentExport);
                    }
                }
            }

            //if (reSearch)
            //{
            //    PropertyInfo reAttempt = getPropertyInfo(className, propName, !inStruct, nonVanillaClassInfo, reSearch: false);
            //    return reAttempt; //will be null if not found.
            //}
            return null;
        }

        internal static readonly ConcurrentDictionary<string, PropertyCollection> defaultStructValuesLE3 = new();

        #region Generating
        //call this method to regenerate LE3ObjectInfo.json
        //Takes a long time (~5 minutes maybe?). Application will be completely unresponsive during that time.
        public static void generateInfo(string outpath, bool usePooledMemory = true, Action<int, int> progressDelegate = null)
        {
            MemoryManager.SetUsePooledMemory(usePooledMemory);
            Enums.Clear();
            Structs.Clear();
            Classes.Clear();
            SequenceObjects.Clear();

            var allFiles = MELoadedFiles.GetOfficialFiles(MEGame.LE3).Where(x => Path.GetExtension(x) == ".pcc").ToList();
            int totalFiles = allFiles.Count * 2;
            int numDone = 0;
            foreach (string filePath in allFiles)
            {
                using IMEPackage pcc = MEPackageHandler.OpenLE3Package(filePath);
                if (pcc.Localization != MELocalization.None && pcc.Localization != MELocalization.INT)
                    continue; // DO NOT LOOK AT NON-INT AS SOME GAMES WILL BE MISSING THESE FILES (due to backup/storage)
                for (int i = 1; i <= pcc.ExportCount; i++)
                {
                    ExportEntry exportEntry = pcc.GetUExport(i);
                    string className = exportEntry.ClassName;
                    string objectName = exportEntry.ObjectName.Instanced;
                    if (className == "Enum")
                    {
                        generateEnumValues(exportEntry, Enums);
                    }
                    else if (className == "Class" && !Classes.ContainsKey(objectName))
                    {
                        Classes.Add(objectName, generateClassInfo(exportEntry));
                    }
                    else if (className == "ScriptStruct")
                    {
                        if (!Structs.ContainsKey(objectName))
                        {
                            Structs.Add(objectName, generateClassInfo(exportEntry, isStruct: true));
                        }
                    }
                }
                // System.Diagnostics.Debug.WriteLine($"{i} of {length} processed");
                numDone++;
                progressDelegate?.Invoke(numDone, totalFiles);
            }

            foreach (string filePath in allFiles)
            {
                using IMEPackage pcc = MEPackageHandler.OpenLE3Package(filePath);
                if (pcc.Localization != MELocalization.None && pcc.Localization != MELocalization.INT)
                    continue; // DO NOT LOOK AT NON-INT AS SOME GAMES WILL BE MISSING THESE FILES (due to backup/storage)
                foreach (ExportEntry exportEntry in pcc.Exports)
                {
                    if (exportEntry.IsA("SequenceObject"))
                    {
                        string className = exportEntry.ClassName;
                        if (!SequenceObjects.TryGetValue(className, out SequenceObjectInfo seqObjInfo))
                        {
                            seqObjInfo = new SequenceObjectInfo();
                            SequenceObjects.Add(className, seqObjInfo);
                        }

                        int objInstanceVersion = exportEntry.GetProperty<IntProperty>("ObjInstanceVersion");
                        if (objInstanceVersion > seqObjInfo.ObjInstanceVersion)
                        {
                            seqObjInfo.ObjInstanceVersion = objInstanceVersion;
                        }

                        if (seqObjInfo.inputLinks is null && exportEntry.IsDefaultObject)
                        {
                            List<string> inputLinks = generateSequenceObjectInfo(exportEntry);
                            seqObjInfo.inputLinks = inputLinks;
                        }
                    }
                }
                numDone++;
                progressDelegate?.Invoke(numDone, totalFiles);
            }

            var jsonText = JsonConvert.SerializeObject(new { SequenceObjects, Classes, Structs, Enums }, Formatting.Indented);
            File.WriteAllText(outpath, jsonText);
            MemoryManager.SetUsePooledMemory(false);
            Enums.Clear();
            Structs.Clear();
            Classes.Clear();
            SequenceObjects.Clear();
            loadfromJSON(jsonText);
        }

        private static void AddCustomAndNativeClasses(Dictionary<string, ClassInfo> classes, Dictionary<string, SequenceObjectInfo> sequenceObjects)
        {
            //Custom additions
            //Custom additions are tweaks and additional classes either not automatically able to be determined
            //or new classes designed in the modding scene that must be present in order for parsing to work properly


            // The following is left only as examples if you are building new ones
            /*classes["BioSeqAct_ShowMedals"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 22, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("bFromMainMenu", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_oGuiReferenced", new PropertyInfo(PropertyType.ObjectProperty, "GFxMovieInfo"))
                }
            };
            sequenceObjects["BioSeqAct_ShowMedals"] = new SequenceObjectInfo();
            */

            classes["SFXSeqAct_OverrideCasualAppearance"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 93, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("CasualAppearanceID", new PropertyInfo(PropertyType.IntProperty)),
                }
            };
            sequenceObjects["SFXSeqAct_OverrideCasualAppearance"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1,
                inputLinks = new List<string> { "Override", "Remove Override" }
            };

            classes["SFXSeqAct_SetPawnMeshes"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 66, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NewBodyMesh", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHeadMesh", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHairMesh", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewGearMesh", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("bPreserveAnimation", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewBodyMaterials", new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHeadMaterials", new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHairMaterials", new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewGearMaterials", new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                }
            };
            sequenceObjects["SFXSeqAct_SetPawnMeshes"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            classes["SFXSeqAct_SetStuntMeshes"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 42, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NewBodyMesh", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHeadMesh", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHairMesh", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewGearMesh", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("bPreserveAnimation", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewBodyMaterials", new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHeadMaterials", new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHairMaterials", new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewGearMaterials", new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                }
            };
            sequenceObjects["SFXSeqAct_SetStuntMeshes"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            classes["SFXSeqAct_CheckForNewGAWAssetsFixed"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 2, //in LE3Resources.pcc

            };
            sequenceObjects["SFXSeqAct_CheckForNewGAWAssetsFixed"] = new SequenceObjectInfo();


            classes["SFXSeqAct_SetEquippedWeaponVisibility"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 8, //in LE3Resources.pcc
            };
            sequenceObjects["SFXSeqAct_SetEquippedWeaponVisibility"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1,
                inputLinks = new List<string> { "Show", "Hide", "Toggle" }
            };

            classes["SFXSeqCond_IsCombatMode"] = new ClassInfo
            {
                baseClass = "SequenceCondition",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 17, //in LE3Resources.pcc
            };
            sequenceObjects["SFXSeqCond_IsCombatMode"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            //Kinkojiro - New Class - SFXSeqAct_SetFaceFX
            classes["SFXSeqAct_SetFaceFX"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 22, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_aoTargets", new PropertyInfo(PropertyType.ArrayProperty, "Actor")),
                    new KeyValuePair<NameReference, PropertyInfo>("m_pDefaultFaceFXAsset", new PropertyInfo(PropertyType.ObjectProperty, "FaceFXAsset"))
                }
            };
            sequenceObjects["SFXSeqAct_SetFaceFX"] = new SequenceObjectInfo();

            //Kinkojiro - New Class - SFXSeqAct_SetAutoPlayerLookAt
            classes["SFXSeqAct_SetAutoLookAtPlayer"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 32, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_aoTargets", new PropertyInfo(PropertyType.ArrayProperty, "Actor")),
                    new KeyValuePair<NameReference, PropertyInfo>("bAutoLookAtPlayer", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("NoticeEnableDistance", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("NoticeDisableDistance", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ReNoticeMinTime", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ReNoticeMaxTime", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("NoticeDuration", new PropertyInfo(PropertyType.FloatProperty))
                }
            };
            sequenceObjects["SFXSeqAct_SetAutoLookAtPlayer"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            //Kinkojiro - New Class - this returns whether player is using Gamepad or KBM
            classes["SFXSeqAct_GetControllerType"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 101, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("PlayerObject", new PropertyInfo(PropertyType.ObjectProperty, "Player"))
                }
            };
            sequenceObjects["SFXSeqAct_GetControllerType"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            //Kinkojiro - New Class - this sets the tlk strings for a GAW category in war assets gui
            classes["SFXSeqAct_SetGAWCategoryTitles"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 108, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("CategoryId", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("NewTitleRef", new PropertyInfo(PropertyType.StringRefProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("NewDescriptionRef", new PropertyInfo(PropertyType.StringRefProperty))
                }
            };
            sequenceObjects["SFXSeqAct_SetGAWCategoryTitles"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            //Kinkojiro - New Class - only used in EGM
            classes["SFXSeqAct_TerminalGUI_EGM"] = new ClassInfo
            {
                baseClass = "BioSequenceLatentAction",
                //pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                //exportIndex = 0, not in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("ExitRequestPin", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_TerminalGUIResouce", new PropertyInfo(PropertyType.ObjectProperty, "GFxMovieInfo" )),
                    new KeyValuePair<NameReference, PropertyInfo>("TerminalDataClass", new PropertyInfo(PropertyType.ObjectProperty, "Class" )),
                    new KeyValuePair<NameReference, PropertyInfo>("TerminalName", new PropertyInfo(PropertyType.NameProperty))
                }
            };

            //Kinkojiro - New Class - only used in EGM
            classes["BioSeqAct_ShowMedals"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                //pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                //exportIndex = 0, not in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("bFromMainMenu", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_oGuiReferenced", new PropertyInfo(PropertyType.ObjectProperty, "GFxMovieInfo" ))
                }
            };

            //Kinkojiro - New Class - only used in EGM
            classes["SFXSeqAct_SetGalaxyMapOptions"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                //pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                //exportIndex = 0, not in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_aoGalaxyObjects", new PropertyInfo(PropertyType.ArrayProperty, "SFXGalaxyMapObject")),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fFuelEfficiency", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fFuelTank", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fClusterSpeed", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fSystemSpeed", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fAcceleration", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fDeceleration ", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fClusterAcceleration", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fClusterDeceleration", new PropertyInfo(PropertyType.FloatProperty))
                }
            };
            sequenceObjects["SFXSeqAct_SetGalaxyMapOptions"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            //Kinkojiro - New Class - only used in EGM
            classes["SFXSeqAct_SetReaperAggression"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                //pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                //exportIndex = 0, not in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_aoGalaxyObjects", new PropertyInfo(PropertyType.ArrayProperty, "SFXGalaxyMapObject")),
                    new KeyValuePair<NameReference, PropertyInfo>("ScanParticleSystemRadius", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("MaxSpeed", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("Acceleration", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fScanDetectionRange", new PropertyInfo(PropertyType.FloatProperty))
                }
            };
            sequenceObjects["SFXSeqAct_SetReaperAggression"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            classes["SFXSeqAct_AwardWeaponByName"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                //pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                //exportIndex = 0, not in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("WeaponClassName", new PropertyInfo(PropertyType.NameProperty))
                }
            };
            sequenceObjects["SFXSeqAct_AwardWeaponByName"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            //Kinkojiro - New GM Classes - only used in EGM
            classes["SFXClusterEGM"] = new ClassInfo
            {
                baseClass = "SFXCluster",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("DisplayGAWCondition", new PropertyInfo(PropertyType.IntProperty))
                }
            };
            classes["SFXSystemEGM"] = new ClassInfo
            {
                baseClass = "SFXSystem",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_bCerberusSystem", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ShipChaseWwisePair", new PropertyInfo(PropertyType.StructProperty, "WwiseAudioPair")),
                    new KeyValuePair<NameReference, PropertyInfo>("ShipChaseStopEvent", new PropertyInfo(PropertyType.ObjectProperty, "WwiseEvent"))
                }
            };
            classes["SFXPlanet_Invaded"] = new ClassInfo
            {
                baseClass = "BioPlanet",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("InvasionCondition", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("PlanetPreviewCondition", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("PreInvasionDescription", new PropertyInfo(PropertyType.StringRefProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_bDestroyedbyReapers", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_bNoPlanetScan", new PropertyInfo(PropertyType.BoolProperty))
                }
            };
            classes["SFXGalaxyMapShipAppearance"] = new ClassInfo
            {
                baseClass = "SFXGalaxyMapPlanetAppearance",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("AmbientColor", new PropertyInfo(PropertyType.StructProperty, "LinearColor")),
                    new KeyValuePair<NameReference, PropertyInfo>("m_bNeedsLightEnvironment", new PropertyInfo(PropertyType.BoolProperty))
                }
            };
            classes["SFXGalaxyMapFuelDepotDestroyable"] = new ClassInfo
            {
                baseClass = "SFXGalaxyMapDestroyedFuelDepot",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("DestructionCondition", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("HideFuelSettingCondition", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("EmptyAppearance", new PropertyInfo(PropertyType.ObjectProperty, "SFXGalaxyMapObjectAppearanceBase")),
                    new KeyValuePair<NameReference, PropertyInfo>("EmptyDisplayName", new PropertyInfo(PropertyType.StringRefProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("EmptyDescription", new PropertyInfo(PropertyType.StringRefProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("EmptyTexture", new PropertyInfo(PropertyType.ObjectProperty, "Texture2D")),
                    new KeyValuePair<NameReference, PropertyInfo>("m_bEmptyDepot", new PropertyInfo(PropertyType.BoolProperty))
                }
            };
            classes["SFXGalaxyMapReaperEGM"] = new ClassInfo
            {
                baseClass = "SFXGalaxyMapReaper",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("EGMSettingCondition", new PropertyInfo(PropertyType.IntProperty))
                }
            };
            classes["SFXGalaxyMapCerberusShip"] = new ClassInfo
            {
                baseClass = "SFXGalaxyMapReaperEGM",
                properties =
                {

                   new KeyValuePair<NameReference, PropertyInfo>("ArrowMaterialInstance", new PropertyInfo(PropertyType.ObjectProperty, "MaterialInstanceConstant"))
                }
            };
            //Kinkojiro - New Class - not in resources as has Mail gui. Let me know if anyone wants.
            classes["SFXSeqAct_MailGUI_Sorted"] = new ClassInfo
            {
                baseClass = "BioSequenceLatentAction",
                //pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                //exportIndex = 0, not in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("Honorifics", new PropertyInfo(PropertyType.ArrayProperty, "StrProperty")),
                    new KeyValuePair<NameReference, PropertyInfo>("m_bSortMail", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_MailGUIResource", new PropertyInfo(PropertyType.ObjectProperty, "GFXMovieInfo")),
                    new KeyValuePair<NameReference, PropertyInfo>("MailDataClass", new PropertyInfo(PropertyType.ObjectProperty, "SFXGUIData_Mail")),
                }
            };
            sequenceObjects["SFXSeqAct_MailGUI_Sorted"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 3,
                inputLinks = new List<string> { "Send Mail", "Open UI" }    
            };

            ME3UnrealObjectInfo.AddIntrinsicClasses(classes, MEGame.LE3);

            classes["LightMapTexture2D"] = new ClassInfo
            {
                baseClass = "Texture2D",
                pccPath = @"CookedPCConsole\Engine.pcc"
            };

            classes["StaticMesh"] = new ClassInfo
            {
                baseClass = "Object",
                pccPath = @"CookedPCConsole\Engine.pcc",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleRigidBodyCollision", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleLineCollision", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleBoxCollision", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bUsedForInstancing", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ForceDoubleSidedShadowVolumes", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseFullPrecisionUVs", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("BodySetup", new PropertyInfo(PropertyType.ObjectProperty, "RB_BodySetup")),
                    new KeyValuePair<NameReference, PropertyInfo>("LODDistanceRatio", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapCoordinateIndex", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapResolution", new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            classes["FracturedStaticMesh"] = new ClassInfo
            {
                baseClass = "StaticMesh",
                pccPath = @"CookedPCConsole\Engine.pcc",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("LoseChunkOutsideMaterial", new PropertyInfo(PropertyType.ObjectProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("bSpawnPhysicsChunks", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bCompositeChunksExplodeOnImpact", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ExplosionVelScale", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("FragmentMinHealth", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("FragmentDestroyEffects", new PropertyInfo(PropertyType.ArrayProperty, "ParticleSystem")),
                    new KeyValuePair<NameReference, PropertyInfo>("FragmentMaxHealth", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bAlwaysBreakOffIsolatedIslands", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("DynamicOutsideMaterial", new PropertyInfo(PropertyType.ObjectProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("ChunkLinVel", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ChunkAngVel", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ChunkLinHorizontalScale", new PropertyInfo(PropertyType.FloatProperty)),
                }
            };
        }

        //call on the _Default object
        private static List<string> generateSequenceObjectInfo(ExportEntry export)
        {
            var inLinks = export.GetProperty<ArrayProperty<StructProperty>>("InputLinks");
            if (inLinks != null)
            {
                var inputLinks = new List<string>();
                foreach (var seqOpInputLink in inLinks)
                {
                    inputLinks.Add(seqOpInputLink.GetProp<StrProperty>("LinkDesc").Value);
                }
                return inputLinks;
            }

            return null;
        }

        public static ClassInfo generateClassInfo(ExportEntry export, bool isStruct = false)
        {
            IMEPackage pcc = export.FileRef;
            ClassInfo info = new()
            {
                baseClass = export.SuperClassName,
                exportIndex = export.UIndex,
                ClassName = export.ObjectName.Instanced
            };
            if (export.IsClass)
            {
                UClass classBinary = ObjectBinary.From<UClass>(export);
                info.isAbstract = classBinary.ClassFlags.Has(UnrealFlags.EClassFlags.Abstract);
            }
            if (pcc.FilePath.Contains("BioGame", StringComparison.InvariantCultureIgnoreCase))
            {
                info.pccPath = new string(pcc.FilePath.Skip(pcc.FilePath.LastIndexOf("BIOGame", StringComparison.InvariantCultureIgnoreCase) + 8).ToArray());
            }
            else
            {
                info.pccPath = pcc.FilePath; //used for dynamic resolution of files outside the game directory.
            }

            // Is this code correct for console platforms?
            int nextExport = EndianReader.ToInt32(export.DataReadOnly, isStruct ? 0x14 : 0xC, export.FileRef.Endian);
            while (nextExport > 0)
            {
                var entry = pcc.GetUExport(nextExport);
                //Debug.WriteLine($"GenerateClassInfo parsing child {nextExport} {entry.InstancedFullPath}");
                if (entry.ClassName != "ScriptStruct" && entry.ClassName != "Enum"
                    && entry.ClassName != "Function" && entry.ClassName != "Const" && entry.ClassName != "State")
                {
                    if (!info.properties.ContainsKey(entry.ObjectName))
                    {
                        PropertyInfo p = getProperty(entry);
                        if (p != null)
                        {
                            info.properties.Add(entry.ObjectName, p);
                        }
                    }
                }
                nextExport = EndianReader.ToInt32(entry.DataReadOnly, 0x10, export.FileRef.Endian);
            }
            return info;
        }

        private static void generateEnumValues(ExportEntry export, Dictionary<string, List<NameReference>> NewEnums = null)
        {
            var enumTable = NewEnums ?? Enums;
            string enumName = export.ObjectName.Instanced;
            if (!enumTable.ContainsKey(enumName))
            {
                var values = new List<NameReference>();
                var buff = export.DataReadOnly;
                //subtract 1 so that we don't get the MAX value, which is an implementation detail
                int count = EndianReader.ToInt32(buff, 20, export.FileRef.Endian) - 1;
                for (int i = 0; i < count; i++)
                {
                    int enumValIndex = 24 + i * 8;
                    values.Add(new NameReference(export.FileRef.Names[EndianReader.ToInt32(buff, enumValIndex, export.FileRef.Endian)], EndianReader.ToInt32(buff, enumValIndex + 4, export.FileRef.Endian)));
                }
                enumTable.Add(enumName, values);
            }
        }

        private static PropertyInfo getProperty(ExportEntry entry)
        {
            IMEPackage pcc = entry.FileRef;

            string reference = null;
            PropertyType type;
            switch (entry.ClassName)
            {
                case "IntProperty":
                    type = PropertyType.IntProperty;
                    break;
                case "StringRefProperty":
                    type = PropertyType.StringRefProperty;
                    break;
                case "FloatProperty":
                    type = PropertyType.FloatProperty;
                    break;
                case "BoolProperty":
                    type = PropertyType.BoolProperty;
                    break;
                case "StrProperty":
                    type = PropertyType.StrProperty;
                    break;
                case "NameProperty":
                    type = PropertyType.NameProperty;
                    break;
                case "DelegateProperty":
                    type = PropertyType.DelegateProperty;
                    break;
                case "ObjectProperty":
                case "ClassProperty":
                case "ComponentProperty":
                    type = PropertyType.ObjectProperty;
                    reference = pcc.getObjectName(EndianReader.ToInt32(entry.DataReadOnly, entry.DataSize - 4, entry.FileRef.Endian));
                    break;
                case "StructProperty":
                    type = PropertyType.StructProperty;
                    reference = pcc.getObjectName(EndianReader.ToInt32(entry.DataReadOnly, entry.DataSize - 4, entry.FileRef.Endian));
                    break;
                case "BioMask4Property":
                case "ByteProperty":
                    type = PropertyType.ByteProperty;
                    reference = pcc.getObjectName(EndianReader.ToInt32(entry.DataReadOnly, entry.DataSize - 4, entry.FileRef.Endian));
                    break;
                case "ArrayProperty":
                    type = PropertyType.ArrayProperty;
                    // 44 is not correct on other platforms besides PC
                    PropertyInfo arrayTypeProp = getProperty(pcc.GetUExport(EndianReader.ToInt32(entry.DataReadOnly, entry.FileRef.Platform == MEPackage.GamePlatform.PC ? 44 : 32, entry.FileRef.Endian)));
                    if (arrayTypeProp != null)
                    {
                        switch (arrayTypeProp.Type)
                        {
                            case PropertyType.ObjectProperty:
                            case PropertyType.StructProperty:
                            case PropertyType.ArrayProperty:
                                reference = arrayTypeProp.Reference;
                                break;
                            case PropertyType.ByteProperty:
                                if (arrayTypeProp.Reference == "Class")
                                    reference = arrayTypeProp.Type.ToString();
                                else
                                    reference = arrayTypeProp.Reference;
                                break;
                            case PropertyType.IntProperty:
                            case PropertyType.FloatProperty:
                            case PropertyType.NameProperty:
                            case PropertyType.BoolProperty:
                            case PropertyType.StrProperty:
                            case PropertyType.StringRefProperty:
                            case PropertyType.DelegateProperty:
                                reference = arrayTypeProp.Type.ToString();
                                break;
                            case PropertyType.None:
                            case PropertyType.Unknown:
                            default:
                                Debugger.Break();
                                return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                    break;
                case "InterfaceProperty":
                default:
                    return null;
            }

            bool transient = ((UnrealFlags.EPropertyFlags)EndianReader.ToUInt64(entry.DataReadOnly, 24, entry.FileRef.Endian)).Has(UnrealFlags.EPropertyFlags.Transient);
            int arrayLength = EndianReader.ToInt32(entry.DataReadOnly, 20, entry.FileRef.Endian);
            return new PropertyInfo(type, reference, transient, arrayLength);
        }
        #endregion

        public static bool IsAKnownGameSpecificNativeClass(string className) => NativeClasses.Contains(className);

        /// <summary>
        /// List of all known classes that are only defined in native code that are LE3 specific
        /// </summary>
        public static readonly string[] NativeClasses =
        {
            @"Core.Package",
        };
    }
}
