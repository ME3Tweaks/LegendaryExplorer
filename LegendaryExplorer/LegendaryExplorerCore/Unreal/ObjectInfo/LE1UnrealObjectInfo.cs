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
    public static class LE1UnrealObjectInfo
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
                LECLog.Information(@"Loading property db for LE1");

                try
                {
                    var infoText = jsonTextOverride ?? ObjectInfoLoader.LoadEmbeddedJSONText(MEGame.LE1);
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
                    LECLog.Information($@"Property database load failed for LE1: {ex.Message}");
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
                    // Cause the LE1 DB isn't 100% accurate for ME1/ME2 specific classes, like biopawn
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

        internal static readonly ConcurrentDictionary<string, PropertyCollection> defaultStructValuesLE1 = new();
        
        #region Generating
        //call this method to regenerate LE1ObjectInfo.json
        //Takes a long time (~5 minutes maybe?). Application will be completely unresponsive during that time.
        public static void generateInfo(string outpath, bool usePooledMemory = true, Action<int, int> progressDelegate = null)
        {
            MemoryManager.SetUsePooledMemory(usePooledMemory);
            Enums.Clear();
            Structs.Clear();
            Classes.Clear();
            SequenceObjects.Clear();
            var allFiles = MELoadedFiles.GetOfficialFiles(MEGame.LE1).Where(x => Path.GetExtension(x) == ".pcc").ToList();
            int totalFiles = allFiles.Count * 2;
            int numDone = 0;
            foreach (string filePath in allFiles)
            {
                //if (!filePath.EndsWith("Engine.pcc"))
                //    continue;
                using IMEPackage pcc = MEPackageHandler.OpenLE1Package(filePath);
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
                numDone++;
                progressDelegate?.Invoke(numDone, totalFiles);
                // System.Diagnostics.Debug.WriteLine($"{i} of {length} processed");
            }

            foreach (string filePath in allFiles)
            {
                using IMEPackage pcc = MEPackageHandler.OpenLE1Package(filePath);
                if (pcc.Localization != MELocalization.None && pcc.Localization != MELocalization.INT)
                    continue; // DO NOT LOOK AT NON-INT AS SOME GAMES WILL BE MISSING THESE FILES (due to backup/storage)
                foreach (ExportEntry exportEntry in pcc.Exports)
                {
                    if (exportEntry.IsA("SequenceObject"))
                    {
                        GlobalUnrealObjectInfo.GenerateSequenceObjectInfoForClassDefaults(exportEntry, SequenceObjects);
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
            loadfromJSON(jsonText); // Load the new information into memory
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

            #region SFXSeqAct_GetGameOption
            classes["SFXSeqAct_GetGameOption"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 2, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("Target", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("OptionType", new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            sequenceObjects["SFXSeqAct_GetGameOption"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_GetPlayerMaxGrenades
            classes["SeqAct_GetPlayerMaxGrenades"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 11, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NumGrenades", new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            sequenceObjects["SeqAct_GetPlayerMaxGrenades"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_GetPlayerMaxMedigel
            classes["SeqAct_GetPlayerMaxMedigel"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 18, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NumMedigel", new PropertyInfo(PropertyType.FloatProperty)),
                }
            };

            sequenceObjects["SeqAct_GetPlayerMaxMedigel"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_ActorFactoryWithOwner
            classes["SeqAct_ActorFactoryWithOwner"] = new ClassInfo
            {
                baseClass = "BioSequenceLatentAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 25, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_ID", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bEnabled", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bIsSpawning", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("SpawnDelay", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("RemainingDelay", new PropertyInfo(PropertyType.FloatProperty)),
                }
            };

            sequenceObjects["SeqAct_ActorFactoryWithOwner"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_CopyFloatList
            classes["SeqAct_CopyFloatList"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 51, // in LE1Resources.pcc
            };

            sequenceObjects["SeqAct_CopyFloatList"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqVar_FloatList
            classes["SeqVar_FloatList"] = new ClassInfo
            {
                baseClass = "SequenceVariable",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 57, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("FloatList", new PropertyInfo(PropertyType.ArrayProperty, reference: "FloatProperty")),
                }
            };

            sequenceObjects["SeqVar_FloatList"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_DiscardInventory
            classes["SeqAct_DiscardInventory"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 63, // in LE1Resources.pcc
            };

            sequenceObjects["SeqAct_DiscardInventory"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_Get2DAString
            classes["SeqAct_Get2DAString"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 70, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("Package2DA", new PropertyInfo(PropertyType.ObjectProperty, reference: "Bio2DA")),
                    new KeyValuePair<NameReference, PropertyInfo>("Index", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("Reference", new PropertyInfo(PropertyType.NameProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("Value", new PropertyInfo(PropertyType.StrProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("DefaultArray", new PropertyInfo(PropertyType.ArrayProperty, reference: "StrProperty")),
                    new KeyValuePair<NameReference, PropertyInfo>("DefaultColumns", new PropertyInfo(PropertyType.ArrayProperty, reference: "NameProperty")),
                }
            };

            sequenceObjects["SeqAct_Get2DAString"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_GetDifficulty
            classes["SeqAct_GetDifficulty"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 86, // in LE1Resources.pcc
            };

            sequenceObjects["SeqAct_GetDifficulty"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_GetNthNearestSpawnPoint
            classes["SeqAct_GetNthNearestSpawnPoint"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 95, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("WeightX", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("WeightY", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("WeightZ", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("PlayerPawn", new PropertyInfo(PropertyType.ObjectProperty, reference: "Pawn")),
                }
            };

            sequenceObjects["SeqAct_GetNthNearestSpawnPoint"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_SortFloatList
            classes["SeqAct_SortFloatList"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 114, // in LE1Resources.pcc
            };

            sequenceObjects["SeqAct_SortFloatList"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_GetPawnActorType
            classes["SeqAct_GetPawnActorType"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 182, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_oActorType", new PropertyInfo(PropertyType.ObjectProperty, reference: "BioActorType")),
                }
            };

            sequenceObjects["SeqAct_GetPawnActorType"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_GetPlayerMaxGrenades
            classes["SeqAct_GetPlayerMaxGrenades"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 188, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NumGrenades", new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            sequenceObjects["SeqAct_GetPlayerMaxGrenades"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_GetPlayerMaxMedigel
            classes["SeqAct_GetPlayerMaxMedigel"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 195, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NumMedigel", new PropertyInfo(PropertyType.FloatProperty)),
                }
            };

            sequenceObjects["SeqAct_GetPlayerMaxMedigel"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_GetWeightedComponentDistance
            classes["SeqAct_GetWeightedComponentDistance"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 202, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("WeightX", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("WeightY", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("WeightZ", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("Distance", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog", new PropertyInfo(PropertyType.BoolProperty)),
                }
            };

            sequenceObjects["SeqAct_GetWeightedComponentDistance"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_HealToxicDamage
            classes["SeqAct_HealToxicDamage"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 218, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog", new PropertyInfo(PropertyType.BoolProperty)),
                }
            };

            sequenceObjects["SeqAct_HealToxicDamage"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_ModifyFloatList
            classes["SeqAct_ModifyFloatList"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 226, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("InputOutputValue", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("InputIndex", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("OutputListLength", new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            sequenceObjects["SeqAct_ModifyFloatList"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_ModifyPawnMaxHealth
            classes["SeqAct_ModifyPawnMaxHealth"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 233, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_fFactor", new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog", new PropertyInfo(PropertyType.BoolProperty)),
                }
            };

            sequenceObjects["SeqAct_ModifyPawnMaxHealth"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_RestoreShields
            classes["SeqAct_RestoreShields"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 242, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog", new PropertyInfo(PropertyType.BoolProperty)),
                }
            };

            sequenceObjects["SeqAct_RestoreShields"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_SetDifficulty
            classes["SeqAct_SetDifficulty"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 249, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_nDifficulty", new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            sequenceObjects["SeqAct_SetDifficulty"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_UnapplyGameProperties
            classes["SeqAct_UnapplyGameProperties"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 257, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog", new PropertyInfo(PropertyType.BoolProperty)),
                }
            };

            sequenceObjects["SeqAct_UnapplyGameProperties"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region SeqAct_ZeroAllCooldowns
            classes["SeqAct_ZeroAllCooldowns"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 268, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog", new PropertyInfo(PropertyType.BoolProperty)),
                }
            };

            sequenceObjects["SeqAct_ZeroAllCooldowns"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region UIAction_PlaySound
            classes["UIAction_PlaySound"] = new ClassInfo
            {
                baseClass = "SeqAct_PlaySound",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 277, // in LE1Resources.pcc
            };

            sequenceObjects["UIAction_PlaySound"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region LEXSeqAct_GetControllerType
            classes["LEXSeqAct_GetControllerType"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 283, // in LE1Resources.pcc
            };

            sequenceObjects["LEXSeqAct_GetControllerType"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region LEXSeqAct_SetKeybind
            classes["LEXSeqAct_SetKeybind"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 292, // in LE1Resources.pcc
            };

            sequenceObjects["LEXSeqAct_SetKeybind"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region LEXSeqAct_RemoveKeybind
            classes["LEXSeqAct_RemoveKeybind"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 305, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NumRemoved", new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            sequenceObjects["LEXSeqAct_RemoveKeybind"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region LEXSeqAct_SquadCommand
            classes["LEXSeqAct_SquadCommand"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 319, // in LE1Resources.pcc
            };

            sequenceObjects["LEXSeqAct_SquadCommand"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region LEXSeqAct_ToggleReachSpec
            classes["LEXSeqAct_ToggleReachSpec"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 325, // in LE1Resources.pcc
            };

            sequenceObjects["LEXSeqAct_ToggleReachSpec"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion
            #region LEXSeqAct_AttachGethFlashLight
            classes["LEXSeqAct_AttachGethFlashLight"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 334, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("oEffectPrime", new PropertyInfo(PropertyType.ObjectProperty, reference: "BioVFXTemplate")),
                    new KeyValuePair<NameReference, PropertyInfo>("oEffectDestroyer", new PropertyInfo(PropertyType.ObjectProperty, reference: "BioVFXTemplate")),
                    new KeyValuePair<NameReference, PropertyInfo>("oEffect", new PropertyInfo(PropertyType.ObjectProperty, reference: "BioVFXTemplate")),
                    new KeyValuePair<NameReference, PropertyInfo>("Target", new PropertyInfo(PropertyType.ObjectProperty, reference: "BioPawn")),
                    new KeyValuePair<NameReference, PropertyInfo>("fLifeTime", new PropertyInfo(PropertyType.FloatProperty)),
                }
            };

            sequenceObjects["LEXSeqAct_AttachGethFlashLight"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            #endregion



            ME3UnrealObjectInfo.AddIntrinsicClasses(classes, MEGame.LE1);

            // Native classes 
            Classes["LightMapTexture2D"] = new ClassInfo
            {
                baseClass = "Texture2D",
                exportIndex = 0,
                pccPath = @"CookedPCConsole\Engine.pcc",
            };

            Classes["StaticMesh"] = new ClassInfo
            {
                baseClass = "Object",
                exportIndex = 0,
                pccPath = @"CookedPCConsole\Engine.pcc",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("BodySetup", new PropertyInfo(PropertyType.ObjectProperty, "RB_BodySetup")),
                    new KeyValuePair<NameReference, PropertyInfo>("bUsedForInstancing", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ForceDoubleSidedShadowVolumes", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseFullPrecisionUVs", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapCoordinateIndex", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapResolution", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleboxCollision", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleLineCollision", new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleRigidBodyCollision", new PropertyInfo(PropertyType.BoolProperty)),
                }
            };
            // Native properties

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
                var classBinary = ObjectBinary.From<UClass>(export);
                info.isAbstract = classBinary.ClassFlags.Has(UnrealFlags.EClassFlags.Abstract);
            }
            if (pcc.FilePath.Contains("BioGame"))
            {
                info.pccPath = new string(pcc.FilePath.Skip(pcc.FilePath.LastIndexOf("BioGame") + 8).ToArray());
            }
            else
            {
                info.pccPath = pcc.FilePath; //used for dynamic resolution of files outside the game directory.
            }

            // Is this code correct for console platforms?
            // Child Probe Start - find first node in child chain
            //if (isStruct)
            //    Debugger.Break();
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
                // Next Item in Compiling Chain
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
        /// List of all known classes that are only defined in native code that are LE1 specific
        /// </summary>
        public static readonly string[] NativeClasses =
        {
            @"Core.Package",
        };
    }
}
