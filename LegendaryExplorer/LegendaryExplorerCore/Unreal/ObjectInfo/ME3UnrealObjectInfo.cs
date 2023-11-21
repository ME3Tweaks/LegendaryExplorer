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
    public static class ME3UnrealObjectInfo
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
                LECLog.Information(@"Loading property db for ME3");

                try
                {
                    var infoText = jsonTextOverride ?? ObjectInfoLoader.LoadEmbeddedJSONText(MEGame.ME3);
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
                    LECLog.Information($@"Property database load failed for ME3: {ex.Message}");
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
                    // Cause the ME3 DB isn't 100% accurate for ME1/ME2 specific classes, like biopawn
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

        internal static readonly ConcurrentDictionary<string, PropertyCollection> defaultStructValuesME3 = new();
        
        #region Generating
        //call this method to regenerate ME3ObjectInfo.json
        //Takes a long time (~5 minutes maybe?). Application will be completely unresponsive during that time.
        public static void generateInfo(string outpath, bool usePooledMemory = true, Action<int, int> progressDelegate = null)
        {
            MemoryManager.SetUsePooledMemory(usePooledMemory);
            Enums.Clear();
            Structs.Clear();
            Classes.Clear();
            SequenceObjects.Clear();

            var allFiles = MELoadedFiles.GetOfficialFiles(MEGame.ME3).Where(x => Path.GetExtension(x) == ".pcc").ToList();
            int totalFiles = allFiles.Count * 2;
            int numDone = 0;
            foreach (var filePath in allFiles)
            {
                using IMEPackage pcc = MEPackageHandler.OpenME3Package(filePath);
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
                using IMEPackage pcc = MEPackageHandler.OpenME3Package(filePath);
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

            //Kinkojiro - New Class - BioSeqAct_ShowMedals
            //Sequence object for showing the medals UI
            classes["BioSeqAct_ShowMedals"] = new ClassInfo
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

            //Kinkojiro - New Class - SFXSeqAct_SetFaceFX
            classes["SFXSeqAct_SetFaceFX"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 30, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_aoTargets", new PropertyInfo(PropertyType.ArrayProperty, "Actor")),
                    new KeyValuePair<NameReference, PropertyInfo>("m_pDefaultFaceFXAsset", new PropertyInfo(PropertyType.ObjectProperty, "FaceFXAsset"))
                }
            };
            sequenceObjects["SFXSeqAct_SetFaceFX"] = new SequenceObjectInfo();

            //SirCxyrtyx - New Class - SeqAct_SendMessageToME3Explorer
            classes["SeqAct_SendMessageToME3Explorer"] = new ClassInfo
            {
                baseClass = "SeqAct_Log",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 40, //in ME3Resources.pcc
            };
            sequenceObjects["SeqAct_SendMessageToME3Explorer"] = new SequenceObjectInfo { ObjInstanceVersion = 5 };

            //SirCxyrtyx - New Class - SFXSeqAct_SetPrePivot
            classes["SFXSeqAct_SetPrePivot"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 45, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("PrePivot", new PropertyInfo(PropertyType.StructProperty, "Vector")),
                }
            };
            sequenceObjects["SFXSeqAct_SetPrePivot"] = new SequenceObjectInfo();

            //Kinkojiro - New Class - SFXSeqAct_SetBodyMaterial
            classes["SFXSeqAct_SetBodyMaterial"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 49, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("MaterialIndex", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("NewMaterial", new PropertyInfo(PropertyType.ObjectProperty, "MaterialInterface"))
                }
            };
            sequenceObjects["SFXSeqAct_SetBodyMaterial"] = new SequenceObjectInfo();

            //SirCxyrtyx - New Class - SeqAct_ME3ExpDumpActors
            classes["SeqAct_ME3ExpDumpActors"] = new ClassInfo
            {
                baseClass = "SeqAct_Log",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 57, //in ME3Resources.pcc
            };
            sequenceObjects["SeqAct_ME3ExpDumpActors"] = new SequenceObjectInfo { ObjInstanceVersion = 5 };

            //SirCxyrtyx - New Class - SeqAct_ME3ExpGetPlayerCamPOV
            classes["SeqAct_ME3ExpGetPlayerCamPOV"] = new ClassInfo
            {
                baseClass = "SeqAct_Log",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 61, //in ME3Resources.pcc
            };
            sequenceObjects["SeqAct_ME3ExpGetPlayerCamPOV"] = new SequenceObjectInfo { ObjInstanceVersion = 5 };

            //Kinkojiro - New Class - SFXSeqAct_SetStuntBodyMesh
            classes["SFXSeqAct_SetStuntBodyMesh"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 65, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NewSkelMesh", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("bPreserveAnimation", new PropertyInfo(PropertyType.BoolProperty))
                }
            };
            sequenceObjects["SFXSeqAct_SetStuntBodyMesh"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //Kinkojiro - New Class - SFXSeqAct_SetStuntMeshes
            classes["SFXSeqAct_SetStuntMeshes"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 79, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NewBodyMesh", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHeadMesh", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHairMesh", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("bPreserveAnimation", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewBodyMaterials", new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHeadMaterials", new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHairMaterials", new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                }
            };
            sequenceObjects["SFXSeqAct_SetStuntMeshes"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SeqAct_ME3ExpAcessDumpedActorsList
            classes["SeqAct_ME3ExpAcessDumpedActorsList"] = new ClassInfo
            {
                baseClass = "SeqAct_Log",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 103, //in ME3Resources.pcc
            };
            sequenceObjects["SeqAct_ME3ExpAcessDumpedActorsList"] = new SequenceObjectInfo { ObjInstanceVersion = 5 };

            //SirCxyrtyx - New Class - SFXSeqVar_Rotator
            classes["SFXSeqVar_Rotator"] = new ClassInfo
            {
                baseClass = "SeqVar_Int",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 415, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_Rotator", new PropertyInfo(PropertyType.StructProperty, "Rotator")),
                }
            };

            //SirCxyrtyx - New Class - SFXSeqAct_GetRotation
            classes["SFXSeqAct_GetRotation"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 419, //in ME3Resources.pcc
            };
            sequenceObjects["SFXSeqAct_GetRotation"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_SetRotation
            classes["SFXSeqAct_SetRotation"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 424, //in ME3Resources.pcc
            };
            sequenceObjects["SFXSeqAct_SetRotation"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_SetRotatorComponents
            classes["SFXSeqAct_SetRotatorComponents"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 431, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("Pitch", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("Yaw", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("Roll", new PropertyInfo(PropertyType.IntProperty)),
                }
            };
            sequenceObjects["SFXSeqAct_SetRotatorComponents"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_GetRotatorComponents
            classes["SFXSeqAct_GetRotatorComponents"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 439, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("Pitch", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("Yaw", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("Roll", new PropertyInfo(PropertyType.IntProperty)),
                }
            };
            sequenceObjects["SFXSeqAct_GetRotatorComponents"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_SetRotator
            classes["SFXSeqAct_SetRotator"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 447, //in ME3Resources.pcc
            };
            sequenceObjects["SFXSeqAct_SetRotator"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_SetPawnMeshes
            classes["SFXSeqAct_SetPawnMeshes"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 452, //in ME3Resources.pcc
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

            //SirCxyrtyx - New Class - SFXSeqAct_SetStuntGearMesh
            classes["SFXSeqAct_SetStuntGearMesh"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 479, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NewGearMesh", new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("bPreserveAnimation", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewGearMaterials", new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                }
            };
            sequenceObjects["SFXSeqAct_SetStuntGearMesh"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_SpawnHenchmenWeapons
            classes["SFXSeqAct_SpawnHenchmenWeapons"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 503, //in ME3Resources.pcc
            };
            sequenceObjects["SFXSeqAct_SpawnHenchmenWeapons"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_OverrideCasualAppearance
            classes["SFXSeqAct_OverrideCasualAppearance"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 510, //in ME3Resources.pcc
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

            //SirCxyrtyx - New Class - SFXSeqAct_SetEquippedWeaponVisibility
            classes["SFXSeqAct_SetEquippedWeaponVisibility"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 515, //in ME3Resources.pcc
            };
            sequenceObjects["SFXSeqAct_SetEquippedWeaponVisibility"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1,
                inputLinks = new List<string> { "Show", "Hide", "Toggle" }
            };

            //Native Classes: these classes are defined in C++ only
            
            AddIntrinsicClasses(classes, MEGame.ME3);
            
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

        public static void AddIntrinsicClasses(Dictionary<string, ClassInfo> classes, MEGame game)
        {
            string corePath = game switch
            {
                MEGame.UDK => @"Script\Core.u",
                MEGame.ME1 => @"CookedPC\Core.u",
                MEGame.ME2 => @"CookedPC\Core.pcc",
                _ => @"CookedPCConsole\Core.pcc"
            };
            string enginePath = game switch
            {
                MEGame.UDK => @"Script\Engine.u",
                MEGame.ME1 => @"CookedPC\Engine.u",
                MEGame.ME2 => @"CookedPC\Engine.pcc",
                _ => @"CookedPCConsole\Engine.pcc"
            };
           
            if (game >= MEGame.ME3)
            {
                AddCore("Package", "Object");
            }

            AddCore("Field", "Object");
            AddCore("Struct", "Field");
            AddCore("ScriptStruct", "Struct");
            AddCore("State", "Struct");
            AddCore("Function", "Struct");
            AddCore("Enum", "Field");
            AddCore("Const", "Field");
            //AddCore("Class", "State"); Causes infinite loop

            AddCore("Property", "Field");
            AddCore("ByteProperty", "Property");
            AddCore("IntProperty", "Property");
            AddCore("BoolProperty", "Property");
            AddCore("FloatProperty", "Property");
            AddCore("ObjectProperty", "Property");
            AddCore("ComponentProperty", "Property");
            AddCore("ClassProperty", "Property");
            AddCore("InterfaceProperty", "Property");
            AddCore("NameProperty", "Property");
            AddCore("StrProperty", "Property");
            AddCore("ArrayProperty", "Property");
            AddCore("MapProperty", "Property");
            AddCore("StructProperty", "Property");
            AddCore("DelegateProperty", "Property");
            AddCore("StringRefProperty", "Property");


            classes["LevelBase"] = new ClassInfo
            {
                baseClass = "Object",
                pccPath = enginePath
            };
            classes["Level"] = new ClassInfo
            {
                baseClass = "LevelBase",
                pccPath = enginePath
            };
            classes["PendingLevel"] = new ClassInfo
            {
                baseClass = "LevelBase",
                pccPath = enginePath
            };
            classes["Model"] = new ClassInfo
            {
                baseClass = "Object",
                pccPath = enginePath
            };
            classes["World"] = new ClassInfo
            {
                baseClass = "Object",
                pccPath = enginePath
            };
            classes["Polys"] = new ClassInfo
            {
                baseClass = "Object",
                pccPath = enginePath
            };

            void AddCore(string className, string baseClass)
            {
                classes[className] = new ClassInfo
                {
                    baseClass = baseClass,
                    pccPath = corePath
                };
            }
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
                ClassName = export.ObjectName
            };
            if (export.IsClass)
            {
                UClass classBinary = ObjectBinary.From<UClass>(export);
                info.isAbstract = classBinary.ClassFlags.Has(UnrealFlags.EClassFlags.Abstract);
            }
            if (pcc.FilePath.Contains("BIOGame"))
            {
                info.pccPath = new string(pcc.FilePath.Skip(pcc.FilePath.LastIndexOf("BIOGame") + 8).ToArray());
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

#if ME3EXPLORERAPP
        #region CodeGen
        public static void GenerateCode()
        {
            GenerateEnums();
            GenerateStructs();
            GenerateClasses();
        }
        private static void GenerateClasses()
        {
            using (var fileStream = new FileStream(Path.Combine(App.ExecFolder, "ME3Classes.cs"), FileMode.Create))
            using (var writer = new CodeWriter(fileStream))
            {
                writer.WriteLine("using ME3Explorer.Unreal.ME3Enums;");
                writer.WriteLine("using ME3Explorer.Unreal.ME3Structs;");
                writer.WriteLine("using NameReference = ME3Explorer.Unreal.NameReference;");
                writer.WriteLine();
                writer.WriteBlock("namespace ME3Explorer.Unreal.ME3Classes", () =>
                {
                    writer.WriteBlock("public class Level", () =>
                    {
                        writer.WriteLine("public float ShadowmapTotalSize;");
                        writer.WriteLine("public float LightmapTotalSize;");
                    });
                    foreach ((string className, ClassInfo info) in Classes)
                    {
                        writer.WriteBlock($"public class {className}{(info.baseClass != "Class" ? $" : {info.baseClass}" : "")}", () =>
                        {
                            foreach ((string propName, PropertyInfo propInfo) in Enumerable.Reverse(info.properties))
                            {
                                if (propInfo.Transient || propInfo.Type == PropertyType.None)
                                {
                                    continue;
                                }
                                if (propName.Contains(":") || propName == className)
                                {
                                    writer.WriteLine($"public {CSharpTypeFromUnrealType(propInfo)} _{propName.Replace(":", "")};");
                                }
                                else
                                {
                                    writer.WriteLine($"public {CSharpTypeFromUnrealType(propInfo)} {propName};");
                                }
                            }
                        });
                    }
                });
            }
        }

        // TODO: MOVE THIS OUT OF ME3 GlobalUnrealObjectInfo
        private static void GenerateStructs()
        {
            using (var fileStream = new FileStream(Path.Combine(App.ExecFolder, "ME3Structs.cs"), FileMode.Create))
            using (var writer = new CodeWriter(fileStream))
            {
                writer.WriteLine("using ME3Explorer.Unreal.ME3Enums;");
                writer.WriteLine("using ME3Explorer.Unreal.ME3Classes;");
                writer.WriteLine("using NameReference = ME3Explorer.Unreal.NameReference;");
                writer.WriteLine();
                writer.WriteBlock("namespace ME3Explorer.Unreal.ME3Structs", () =>
                {
                    foreach ((string structName, ClassInfo info) in Structs)
                    {
                        writer.WriteBlock($"public class {structName}{(info.baseClass != "Class" ? $" : {info.baseClass}" : "")}", () =>
                        {
                            foreach ((string propName, PropertyInfo propInfo) in Enumerable.Reverse(info.properties))
                            {
                                if (propInfo.Transient || propInfo.Type == PropertyType.None)
                                {
                                    continue;
                                }
                                writer.WriteLine($"public {CSharpTypeFromUnrealType(propInfo)} {propName.Replace(":", "")};");
                            }
                        });
                    }
                });
            }
        }

        private static void GenerateEnums()
        {
            using (var fileStream = new FileStream(Path.Combine(App.ExecFolder, "ME3Enums.cs"), FileMode.Create))
            using (var writer = new CodeWriter(fileStream))
            {
                writer.WriteBlock("namespace ME3Explorer.Unreal.ME3Enums", () =>
                {
                    foreach ((string enumName, List<NameReference> values) in Enums)
                    {
                        writer.WriteBlock($"public enum {enumName}", () =>
                        {
                            foreach (NameReference val in values)
                            {
                                writer.WriteLine($"{val.Instanced},");
                            }
                        });
                    }
                });
            }
        }
        static string CSharpTypeFromUnrealType(PropertyInfo propInfo)
        {
            switch (propInfo.Type)
            {
                case PropertyType.StructProperty:
                    return propInfo.Reference;
                case PropertyType.IntProperty:
                    return "int";
                case PropertyType.FloatProperty:
                    return "float";
                case PropertyType.DelegateProperty:
                    return nameof(ScriptDelegate);
                case PropertyType.ObjectProperty:
                    return "int";
                case PropertyType.NameProperty:
                    return nameof(NameReference);
                case PropertyType.BoolProperty:
                    return "bool";
                case PropertyType.BioMask4Property:
                    return "byte";
                case PropertyType.ByteProperty when propInfo.IsEnumProp():
                    return propInfo.Reference;
                case PropertyType.ByteProperty:
                    return "byte";
                case PropertyType.ArrayProperty:
                    {
                        string type;
                        if (Enum.TryParse(propInfo.Reference, out PropertyType arrayType))
                        {
                            type = CSharpTypeFromUnrealType(new PropertyInfo(arrayType));
                        }
                        else if (Classes.ContainsKey(propInfo.Reference))
                        {
                            //ObjectProperty
                            type = "int";
                        }
                        else
                        {
                            type = propInfo.Reference;
                        }

                        return $"{type}[]";
                    }
                case PropertyType.StrProperty:
                    return "string";
                case PropertyType.StringRefProperty:
                    return "int";
                case PropertyType.None:
                case PropertyType.Unknown:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        #endregion
#endif

        public static bool IsAKnownGameSpecificNativeClass(string className) => NativeClasses.Contains(className);

        /// <summary>
        /// List of all known classes that are only defined in native code that are ME3 specific
        /// </summary>
        public static readonly string[] NativeClasses =
        {
            @"Core.Package",
        };
    }
}
