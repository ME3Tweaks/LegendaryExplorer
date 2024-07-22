using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LegendaryExplorerCore.Unreal.ObjectInfo
{
    /// <summary>
    /// Single class that is not static - for shared code in the UnrealObjectInfo classes. Each game should subclass this to provide custom methods for adding their own game-specific data
    /// </summary>
    public abstract class GameObjectInfo
    {
        public abstract MEGame Game { get; }
        public bool IsLoaded { get; private set; }

        [JsonProperty("Classes")]
        protected CaseInsensitiveDictionary<ClassInfo> _classes;
        [JsonIgnore]
        public CaseInsensitiveDictionary<ClassInfo> Classes
        {
            get
            {
                if (!IsLoaded)
                    LoadData();
                return _classes;
            }
        }

        [JsonProperty("Structs")]
        protected CaseInsensitiveDictionary<ClassInfo> _structs;
        [JsonIgnore]
        public CaseInsensitiveDictionary<ClassInfo> Structs
        {
            get
            {
                if (!IsLoaded)
                    LoadData();
                return _structs;
            }
        }

        [JsonProperty("Enums")]
        protected CaseInsensitiveDictionary<List<NameReference>> _enums;
        [JsonIgnore]
        public CaseInsensitiveDictionary<List<NameReference>> Enums
        {
            get
            {
                if (!IsLoaded)
                    LoadData();
                return _enums;
            }
        }

        [JsonProperty("SequenceObjects")]
        protected CaseInsensitiveDictionary<SequenceObjectInfo> _sequenceObjects;
        [JsonIgnore]
        public CaseInsensitiveDictionary<SequenceObjectInfo> SequenceObjects
        {
            get
            {
                if (!IsLoaded)
                    LoadData();
                return _sequenceObjects;
            }
        }

        protected GameObjectInfo()
        {
            _classes = new CaseInsensitiveDictionary<ClassInfo>();
            _enums = new CaseInsensitiveDictionary<List<NameReference>>();
            _structs = new CaseInsensitiveDictionary<ClassInfo>();
            _sequenceObjects = new CaseInsensitiveDictionary<SequenceObjectInfo>();
        }

        public void LoadData(string jsonTextOverride = null)
        {
            if (!IsLoaded)
            {
                try
                {
                    LECLog.Information($@"Loading property db for {Game}");
                    var infoText = jsonTextOverride ?? ObjectInfoLoader.LoadEmbeddedJSONText(Game);
                    if (infoText != null)
                    {
                        // Yes, it's ME1ObjectInfo. It is not abstract.
                        var blob = JsonConvert.DeserializeObject<ME1ObjectInfo>(infoText);
                        _sequenceObjects = blob._sequenceObjects;
                        _classes = blob._classes;
                        _structs = blob._structs;
                        _enums = blob._enums;

                        AddCustomAndNativeClasses();
                        foreach ((string className, ClassInfo classInfo) in _classes)
                        {
                            classInfo.ClassName = className;
                        }

                        foreach ((string className, ClassInfo classInfo) in _structs)
                        {
                            classInfo.ClassName = className;
                        }

                        IsLoaded = true;
                    }
                }
                catch (Exception ex)
                {
                    LECLog.Information($@"Property database load failed for {Game}: {ex.Message}");
                    return;
                }
            }
        }

        protected abstract void AddCustomAndNativeClasses();

        /// <summary>
        /// Clears all loaded data
        /// </summary>
        public void Reset()
        {
            _enums.Clear();
            _structs.Clear();
            _classes.Clear();
            _sequenceObjects.Clear();
        }
    }

    public class ME1ObjectInfo : GameObjectInfo
    {
        public override MEGame Game => MEGame.ME1;

        protected override void AddCustomAndNativeClasses()
        {
            //Native Classes
            _classes["LightMapTexture2D"] = new ClassInfo
            {
                baseClass = "Texture2D",
                exportIndex = 0,
                pccPath = @"CookedPC\Engine.u",
            };

            _classes["StaticMesh"] = new ClassInfo
            {
                baseClass = "Object",
                exportIndex = 0,
                pccPath = @"CookedPC\Engine.u",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleRigidBodyCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleLineCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleBoxCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ForceDoubleSidedShadowVolumes",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("BodySetup",
                        new PropertyInfo(PropertyType.ObjectProperty, "RB_BodySetup")),
                    new KeyValuePair<NameReference, PropertyInfo>("LODDistanceRatio",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapCoordinateIndex",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapResolution",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("SoundCue",
                        new PropertyInfo(PropertyType.ObjectProperty, "SoundCue")),
                }
            };

            GlobalUnrealObjectInfo.AddIntrinsicClasses(_classes, MEGame.ME1);
        }
    }

    public class ME2ObjectInfo : GameObjectInfo
    {
        public override MEGame Game => MEGame.ME2;

        protected override void AddCustomAndNativeClasses()
        {
            //CUSTOM ADDITIONS
            _classes["SeqAct_SendMessageToME3Explorer"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                exportIndex = 2,
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName
            };
            _sequenceObjects["SeqAct_SendMessageToME3Explorer"] = new SequenceObjectInfo { ObjInstanceVersion = 2 };

            _classes["SeqAct_ME3ExpDumpActors"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                exportIndex = 4,
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName
            };
            _sequenceObjects["SeqAct_ME3ExpDumpActors"] = new SequenceObjectInfo { ObjInstanceVersion = 2 };

            _classes["SeqAct_ME3ExpAcessDumpedActorsList"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                exportIndex = 6,
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName
            };
            _sequenceObjects["SeqAct_ME3ExpAcessDumpedActorsList"] = new SequenceObjectInfo { ObjInstanceVersion = 2 };

            _classes["SeqAct_ME3ExpGetPlayerCamPOV"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                exportIndex = 8,
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName
            };
            _sequenceObjects["SeqAct_ME3ExpGetPlayerCamPOV"] = new SequenceObjectInfo { ObjInstanceVersion = 2 };

            _classes["SeqAct_GetLocationAndRotation"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                exportIndex = 10,
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_oTarget",
                        new PropertyInfo(PropertyType.ObjectProperty, "Actor")),
                    new KeyValuePair<NameReference, PropertyInfo>("Location",
                        new PropertyInfo(PropertyType.StructProperty, "Vector")),
                    new KeyValuePair<NameReference, PropertyInfo>("RotationVector",
                        new PropertyInfo(PropertyType.StructProperty, "Vector"))
                }
            };
            _sequenceObjects["SeqAct_GetLocationAndRotation"] = new SequenceObjectInfo { ObjInstanceVersion = 0 };

            _classes["SeqAct_SetLocationAndRotation"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                exportIndex = 16,
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("bSetRotation",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bSetLocation",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_oTarget",
                        new PropertyInfo(PropertyType.ObjectProperty, "Actor")),
                    new KeyValuePair<NameReference, PropertyInfo>("Location",
                        new PropertyInfo(PropertyType.StructProperty, "Vector")),
                    new KeyValuePair<NameReference, PropertyInfo>("RotationVector",
                        new PropertyInfo(PropertyType.StructProperty, "Vector")),
                }
            };
            _sequenceObjects["SeqAct_SetLocationAndRotation"] = new SequenceObjectInfo { ObjInstanceVersion = 0 };

            GlobalUnrealObjectInfo.AddIntrinsicClasses(_classes, Game);

            _classes["LightMapTexture2D"] = new ClassInfo
            {
                baseClass = "Texture2D",
                pccPath = @"CookedPC\Engine.pcc"
            };

            _classes["StaticMesh"] = new ClassInfo
            {
                baseClass = "Object",
                pccPath = @"CookedPC\Engine.pcc",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleRigidBodyCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleLineCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleBoxCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bUsedForInstancing",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ForceDoubleSidedShadowVolumes",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseFullPrecisionUVs",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("BodySetup",
                        new PropertyInfo(PropertyType.ObjectProperty, "RB_BodySetup")),
                    new KeyValuePair<NameReference, PropertyInfo>("LODDistanceRatio",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapCoordinateIndex",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapResolution",
                        new PropertyInfo(PropertyType.IntProperty)),
                }
            };
        }
    }

    public class ME3ObjectInfo : GameObjectInfo
    {

        public override MEGame Game => MEGame.ME3;

        protected override void AddCustomAndNativeClasses()
        {
            //Custom additions
            //Custom additions are tweaks and additional classes either not automatically able to be determined
            //or new classes designed in the modding scene that must be present in order for parsing to work properly

            //Kinkojiro - New Class - BioSeqAct_ShowMedals
            //Sequence object for showing the medals UI
            _classes["BioSeqAct_ShowMedals"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 22, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("bFromMainMenu",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_oGuiReferenced",
                        new PropertyInfo(PropertyType.ObjectProperty, "GFxMovieInfo"))
                }
            };
            _sequenceObjects["BioSeqAct_ShowMedals"] = new SequenceObjectInfo();

            //Kinkojiro - New Class - SFXSeqAct_SetFaceFX
            _classes["SFXSeqAct_SetFaceFX"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 30, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_aoTargets",
                        new PropertyInfo(PropertyType.ArrayProperty, "Actor")),
                    new KeyValuePair<NameReference, PropertyInfo>("m_pDefaultFaceFXAsset",
                        new PropertyInfo(PropertyType.ObjectProperty, "FaceFXAsset"))
                }
            };
            _sequenceObjects["SFXSeqAct_SetFaceFX"] = new SequenceObjectInfo();

            //SirCxyrtyx - New Class - SeqAct_SendMessageToME3Explorer
            _classes["SeqAct_SendMessageToME3Explorer"] = new ClassInfo
            {
                baseClass = "SeqAct_Log",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 40, //in ME3Resources.pcc
            };
            _sequenceObjects["SeqAct_SendMessageToME3Explorer"] = new SequenceObjectInfo { ObjInstanceVersion = 5 };

            //SirCxyrtyx - New Class - SFXSeqAct_SetPrePivot
            _classes["SFXSeqAct_SetPrePivot"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 45, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("PrePivot",
                        new PropertyInfo(PropertyType.StructProperty, "Vector")),
                }
            };
            _sequenceObjects["SFXSeqAct_SetPrePivot"] = new SequenceObjectInfo();

            //Kinkojiro - New Class - SFXSeqAct_SetBodyMaterial
            _classes["SFXSeqAct_SetBodyMaterial"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 49, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("MaterialIndex",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("NewMaterial",
                        new PropertyInfo(PropertyType.ObjectProperty, "MaterialInterface"))
                }
            };
            _sequenceObjects["SFXSeqAct_SetBodyMaterial"] = new SequenceObjectInfo();

            //SirCxyrtyx - New Class - SeqAct_ME3ExpDumpActors
            _classes["SeqAct_ME3ExpDumpActors"] = new ClassInfo
            {
                baseClass = "SeqAct_Log",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 57, //in ME3Resources.pcc
            };
            _sequenceObjects["SeqAct_ME3ExpDumpActors"] = new SequenceObjectInfo { ObjInstanceVersion = 5 };

            //SirCxyrtyx - New Class - SeqAct_ME3ExpGetPlayerCamPOV
            _classes["SeqAct_ME3ExpGetPlayerCamPOV"] = new ClassInfo
            {
                baseClass = "SeqAct_Log",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 61, //in ME3Resources.pcc
            };
            _sequenceObjects["SeqAct_ME3ExpGetPlayerCamPOV"] = new SequenceObjectInfo { ObjInstanceVersion = 5 };

            //Kinkojiro - New Class - SFXSeqAct_SetStuntBodyMesh
            _classes["SFXSeqAct_SetStuntBodyMesh"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 65, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NewSkelMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("bPreserveAnimation",
                        new PropertyInfo(PropertyType.BoolProperty))
                }
            };
            _sequenceObjects["SFXSeqAct_SetStuntBodyMesh"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //Kinkojiro - New Class - SFXSeqAct_SetStuntMeshes
            _classes["SFXSeqAct_SetStuntMeshes"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 79, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NewBodyMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHeadMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHairMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("bPreserveAnimation",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewBodyMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHeadMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHairMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                }
            };
            _sequenceObjects["SFXSeqAct_SetStuntMeshes"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SeqAct_ME3ExpAcessDumpedActorsList
            _classes["SeqAct_ME3ExpAcessDumpedActorsList"] = new ClassInfo
            {
                baseClass = "SeqAct_Log",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 103, //in ME3Resources.pcc
            };
            _sequenceObjects["SeqAct_ME3ExpAcessDumpedActorsList"] = new SequenceObjectInfo { ObjInstanceVersion = 5 };

            //SirCxyrtyx - New Class - SFXSeqVar_Rotator
            _classes["SFXSeqVar_Rotator"] = new ClassInfo
            {
                baseClass = "SeqVar_Int",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 415, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_Rotator",
                        new PropertyInfo(PropertyType.StructProperty, "Rotator")),
                }
            };

            //SirCxyrtyx - New Class - SFXSeqAct_GetRotation
            _classes["SFXSeqAct_GetRotation"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 419, //in ME3Resources.pcc
            };
            _sequenceObjects["SFXSeqAct_GetRotation"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_SetRotation
            _classes["SFXSeqAct_SetRotation"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 424, //in ME3Resources.pcc
            };
            _sequenceObjects["SFXSeqAct_SetRotation"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_SetRotatorComponents
            _classes["SFXSeqAct_SetRotatorComponents"] = new ClassInfo
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
            _sequenceObjects["SFXSeqAct_SetRotatorComponents"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_GetRotatorComponents
            _classes["SFXSeqAct_GetRotatorComponents"] = new ClassInfo
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
            _sequenceObjects["SFXSeqAct_GetRotatorComponents"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_SetRotator
            _classes["SFXSeqAct_SetRotator"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 447, //in ME3Resources.pcc
            };
            _sequenceObjects["SFXSeqAct_SetRotator"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_SetPawnMeshes
            _classes["SFXSeqAct_SetPawnMeshes"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 452, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NewBodyMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHeadMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHairMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewGearMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("bPreserveAnimation",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewBodyMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHeadMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHairMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewGearMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                }
            };
            _sequenceObjects["SFXSeqAct_SetPawnMeshes"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_SetStuntGearMesh
            _classes["SFXSeqAct_SetStuntGearMesh"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 479, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NewGearMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("bPreserveAnimation",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewGearMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                }
            };
            _sequenceObjects["SFXSeqAct_SetStuntGearMesh"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_SpawnHenchmenWeapons
            _classes["SFXSeqAct_SpawnHenchmenWeapons"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 503, //in ME3Resources.pcc
            };
            _sequenceObjects["SFXSeqAct_SpawnHenchmenWeapons"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            //SirCxyrtyx - New Class - SFXSeqAct_OverrideCasualAppearance
            _classes["SFXSeqAct_OverrideCasualAppearance"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 510, //in ME3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("CasualAppearanceID",
                        new PropertyInfo(PropertyType.IntProperty)),
                }
            };
            _sequenceObjects["SFXSeqAct_OverrideCasualAppearance"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1,
                inputLinks = new List<string> { "Override", "Remove Override" }
            };

            //SirCxyrtyx - New Class - SFXSeqAct_SetEquippedWeaponVisibility
            _classes["SFXSeqAct_SetEquippedWeaponVisibility"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 515, //in ME3Resources.pcc
            };
            _sequenceObjects["SFXSeqAct_SetEquippedWeaponVisibility"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1,
                inputLinks = new List<string> { "Show", "Hide", "Toggle" }
            };

            //Native Classes: these classes are defined in C++ only

            GlobalUnrealObjectInfo.AddIntrinsicClasses(_classes, Game);

            _classes["LightMapTexture2D"] = new ClassInfo
            {
                baseClass = "Texture2D",
                pccPath = @"CookedPCConsole\Engine.pcc"
            };

            _classes["StaticMesh"] = new ClassInfo
            {
                baseClass = "Object",
                pccPath = @"CookedPCConsole\Engine.pcc",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleRigidBodyCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleLineCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleBoxCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bUsedForInstancing",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ForceDoubleSidedShadowVolumes",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseFullPrecisionUVs",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("BodySetup",
                        new PropertyInfo(PropertyType.ObjectProperty, "RB_BodySetup")),
                    new KeyValuePair<NameReference, PropertyInfo>("LODDistanceRatio",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapCoordinateIndex",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapResolution",
                        new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            _classes["FracturedStaticMesh"] = new ClassInfo
            {
                baseClass = "StaticMesh",
                pccPath = @"CookedPCConsole\Engine.pcc",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("LoseChunkOutsideMaterial",
                        new PropertyInfo(PropertyType.ObjectProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("bSpawnPhysicsChunks",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bCompositeChunksExplodeOnImpact",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ExplosionVelScale",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("FragmentMinHealth",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("FragmentDestroyEffects",
                        new PropertyInfo(PropertyType.ArrayProperty, "ParticleSystem")),
                    new KeyValuePair<NameReference, PropertyInfo>("FragmentMaxHealth",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bAlwaysBreakOffIsolatedIslands",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("DynamicOutsideMaterial",
                        new PropertyInfo(PropertyType.ObjectProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("ChunkLinVel",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ChunkAngVel",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ChunkLinHorizontalScale",
                        new PropertyInfo(PropertyType.FloatProperty)),
                }
            };
        }
    }

    public class LE1ObjectInfo : GameObjectInfo
    {
        public override MEGame Game => MEGame.LE1;

        protected override void AddCustomAndNativeClasses()
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
            _sequenceObjects["BioSeqAct_ShowMedals"] = new SequenceObjectInfo();
            */

            #region SFXSeqAct_GetGameOption

            _classes["SFXSeqAct_GetGameOption"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 2, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("Target", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("OptionType",
                        new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            _sequenceObjects["SFXSeqAct_GetGameOption"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_GetPlayerMaxGrenades

            _classes["SeqAct_GetPlayerMaxGrenades"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 11, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NumGrenades",
                        new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            _sequenceObjects["SeqAct_GetPlayerMaxGrenades"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_GetPlayerMaxMedigel

            _classes["SeqAct_GetPlayerMaxMedigel"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 18, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NumMedigel",
                        new PropertyInfo(PropertyType.FloatProperty)),
                }
            };

            _sequenceObjects["SeqAct_GetPlayerMaxMedigel"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_ActorFactoryWithOwner

            _classes["SeqAct_ActorFactoryWithOwner"] = new ClassInfo
            {
                baseClass = "BioSequenceLatentAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 25, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_ID", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bEnabled",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bIsSpawning",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("SpawnDelay",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("RemainingDelay",
                        new PropertyInfo(PropertyType.FloatProperty)),
                }
            };

            _sequenceObjects["SeqAct_ActorFactoryWithOwner"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_CopyFloatList

            _classes["SeqAct_CopyFloatList"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 51, // in LE1Resources.pcc
            };

            _sequenceObjects["SeqAct_CopyFloatList"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqVar_FloatList

            _classes["SeqVar_FloatList"] = new ClassInfo
            {
                baseClass = "SequenceVariable",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 57, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("FloatList",
                        new PropertyInfo(PropertyType.ArrayProperty, reference: "FloatProperty")),
                }
            };

            _sequenceObjects["SeqVar_FloatList"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_DiscardInventory

            _classes["SeqAct_DiscardInventory"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 63, // in LE1Resources.pcc
            };

            _sequenceObjects["SeqAct_DiscardInventory"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_Get2DAString

            _classes["SeqAct_Get2DAString"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 70, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("Package2DA",
                        new PropertyInfo(PropertyType.ObjectProperty, reference: "Bio2DA")),
                    new KeyValuePair<NameReference, PropertyInfo>("Index", new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("Reference",
                        new PropertyInfo(PropertyType.NameProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("Value", new PropertyInfo(PropertyType.StrProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("DefaultArray",
                        new PropertyInfo(PropertyType.ArrayProperty, reference: "StrProperty")),
                    new KeyValuePair<NameReference, PropertyInfo>("DefaultColumns",
                        new PropertyInfo(PropertyType.ArrayProperty, reference: "NameProperty")),
                }
            };

            _sequenceObjects["SeqAct_Get2DAString"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_GetDifficulty

            _classes["SeqAct_GetDifficulty"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 86, // in LE1Resources.pcc
            };

            _sequenceObjects["SeqAct_GetDifficulty"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_GetNthNearestSpawnPoint

            _classes["SeqAct_GetNthNearestSpawnPoint"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 95, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("WeightX",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("WeightY",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("WeightZ",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("PlayerPawn",
                        new PropertyInfo(PropertyType.ObjectProperty, reference: "Pawn")),
                }
            };

            _sequenceObjects["SeqAct_GetNthNearestSpawnPoint"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_SortFloatList

            _classes["SeqAct_SortFloatList"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 114, // in LE1Resources.pcc
            };

            _sequenceObjects["SeqAct_SortFloatList"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_GetPawnActorType

            _classes["SeqAct_GetPawnActorType"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 182, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_oActorType",
                        new PropertyInfo(PropertyType.ObjectProperty, reference: "BioActorType")),
                }
            };

            _sequenceObjects["SeqAct_GetPawnActorType"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_GetPlayerMaxGrenades

            _classes["SeqAct_GetPlayerMaxGrenades"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 188, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NumGrenades",
                        new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            _sequenceObjects["SeqAct_GetPlayerMaxGrenades"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_GetPlayerMaxMedigel

            _classes["SeqAct_GetPlayerMaxMedigel"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 195, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NumMedigel",
                        new PropertyInfo(PropertyType.FloatProperty)),
                }
            };

            _sequenceObjects["SeqAct_GetPlayerMaxMedigel"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_GetWeightedComponentDistance

            _classes["SeqAct_GetWeightedComponentDistance"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 202, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("WeightX",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("WeightY",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("WeightZ",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("Distance",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog",
                        new PropertyInfo(PropertyType.BoolProperty)),
                }
            };

            _sequenceObjects["SeqAct_GetWeightedComponentDistance"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_HealToxicDamage

            _classes["SeqAct_HealToxicDamage"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 218, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog",
                        new PropertyInfo(PropertyType.BoolProperty)),
                }
            };

            _sequenceObjects["SeqAct_HealToxicDamage"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_ModifyFloatList

            _classes["SeqAct_ModifyFloatList"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 226, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("InputOutputValue",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("InputIndex",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("OutputListLength",
                        new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            _sequenceObjects["SeqAct_ModifyFloatList"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_ModifyPawnMaxHealth

            _classes["SeqAct_ModifyPawnMaxHealth"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 233, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_fFactor",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog",
                        new PropertyInfo(PropertyType.BoolProperty)),
                }
            };

            _sequenceObjects["SeqAct_ModifyPawnMaxHealth"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_RestoreShields

            _classes["SeqAct_RestoreShields"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 242, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog",
                        new PropertyInfo(PropertyType.BoolProperty)),
                }
            };

            _sequenceObjects["SeqAct_RestoreShields"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_SetDifficulty

            _classes["SeqAct_SetDifficulty"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 249, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_nDifficulty",
                        new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            _sequenceObjects["SeqAct_SetDifficulty"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_UnapplyGameProperties

            _classes["SeqAct_UnapplyGameProperties"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 257, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog",
                        new PropertyInfo(PropertyType.BoolProperty)),
                }
            };

            _sequenceObjects["SeqAct_UnapplyGameProperties"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region SeqAct_ZeroAllCooldowns

            _classes["SeqAct_ZeroAllCooldowns"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 268, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("PrintToLog",
                        new PropertyInfo(PropertyType.BoolProperty)),
                }
            };

            _sequenceObjects["SeqAct_ZeroAllCooldowns"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region UIAction_PlaySound

            _classes["UIAction_PlaySound"] = new ClassInfo
            {
                baseClass = "SeqAct_PlaySound",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 277, // in LE1Resources.pcc
            };

            _sequenceObjects["UIAction_PlaySound"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region LEXSeqAct_GetControllerType

            _classes["LEXSeqAct_GetControllerType"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 283, // in LE1Resources.pcc
            };

            _sequenceObjects["LEXSeqAct_GetControllerType"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region LEXSeqAct_SetKeybind

            _classes["LEXSeqAct_SetKeybind"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 292, // in LE1Resources.pcc
            };

            _sequenceObjects["LEXSeqAct_SetKeybind"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region LEXSeqAct_RemoveKeybind

            _classes["LEXSeqAct_RemoveKeybind"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 305, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NumRemoved",
                        new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            _sequenceObjects["LEXSeqAct_RemoveKeybind"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region LEXSeqAct_SquadCommand

            _classes["LEXSeqAct_SquadCommand"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 319, // in LE1Resources.pcc
            };

            _sequenceObjects["LEXSeqAct_SquadCommand"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region LEXSeqAct_ToggleReachSpec

            _classes["LEXSeqAct_ToggleReachSpec"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 325, // in LE1Resources.pcc
            };

            _sequenceObjects["LEXSeqAct_ToggleReachSpec"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            #region LEXSeqAct_AttachGethFlashLight

            _classes["LEXSeqAct_AttachGethFlashLight"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 334, // in LE1Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("oEffectPrime",
                        new PropertyInfo(PropertyType.ObjectProperty, reference: "BioVFXTemplate")),
                    new KeyValuePair<NameReference, PropertyInfo>("oEffectDestroyer",
                        new PropertyInfo(PropertyType.ObjectProperty, reference: "BioVFXTemplate")),
                    new KeyValuePair<NameReference, PropertyInfo>("oEffect",
                        new PropertyInfo(PropertyType.ObjectProperty, reference: "BioVFXTemplate")),
                    new KeyValuePair<NameReference, PropertyInfo>("Target",
                        new PropertyInfo(PropertyType.ObjectProperty, reference: "BioPawn")),
                    new KeyValuePair<NameReference, PropertyInfo>("fLifeTime",
                        new PropertyInfo(PropertyType.FloatProperty)),
                }
            };

            _sequenceObjects["LEXSeqAct_AttachGethFlashLight"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            #endregion

            GlobalUnrealObjectInfo.AddIntrinsicClasses(_classes, Game);

            // Native classes 
            _classes["LightMapTexture2D"] = new ClassInfo
            {
                baseClass = "Texture2D",
                exportIndex = 0,
                pccPath = @"CookedPCConsole\Engine.pcc",
            };

            _classes["StaticMesh"] = new ClassInfo
            {
                baseClass = "Object",
                exportIndex = 0,
                pccPath = @"CookedPCConsole\Engine.pcc",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("BodySetup",
                        new PropertyInfo(PropertyType.ObjectProperty, "RB_BodySetup")),
                    new KeyValuePair<NameReference, PropertyInfo>("bUsedForInstancing",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ForceDoubleSidedShadowVolumes",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseFullPrecisionUVs",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapCoordinateIndex",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapResolution",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleboxCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleLineCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleRigidBodyCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                }
            };
            // Native properties

        }

    }

    public class LE2ObjectInfo : GameObjectInfo
    {
        public override MEGame Game => MEGame.LE2;

        protected override void AddCustomAndNativeClasses()
        {
            // Custom additions
            //Custom additions are tweaks and additional classes either not automatically able to be determined
            //or new classes designed in the modding scene that must be present in order for parsing to work properly

            _classes["SFXSeqAct_SetPawnMeshes"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 2, //in LE2Resources.pcc
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
            _sequenceObjects["SFXSeqAct_SetPawnMeshes"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            GlobalUnrealObjectInfo.AddIntrinsicClasses(_classes, Game);
            _classes["LightMapTexture2D"] = new ClassInfo
            {
                baseClass = "Texture2D",
                exportIndex = 0,
                pccPath = @"CookedPCConsole\Engine.pcc",
            };

            _classes["StaticMesh"] = new ClassInfo
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
        }
    }

    public class LE3ObjectInfo : GameObjectInfo
    {
        public override MEGame Game => MEGame.LE3;

        protected override void AddCustomAndNativeClasses()
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
            _sequenceObjects["BioSeqAct_ShowMedals"] = new SequenceObjectInfo();
            */

            _classes["SFXSeqAct_OverrideCasualAppearance"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 93, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("CasualAppearanceID",
                        new PropertyInfo(PropertyType.IntProperty)),
                }
            };
            _sequenceObjects["SFXSeqAct_OverrideCasualAppearance"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1,
                inputLinks = new List<string> { "Override", "Remove Override" }
            };

            _classes["SFXSeqAct_SetPawnMeshes"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 66, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NewBodyMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHeadMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHairMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewGearMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("bPreserveAnimation",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewBodyMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHeadMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHairMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewGearMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                }
            };
            _sequenceObjects["SFXSeqAct_SetPawnMeshes"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            _classes["SFXSeqAct_SetStuntMeshes"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 42, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("NewBodyMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHeadMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewHairMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("NewGearMesh",
                        new PropertyInfo(PropertyType.ObjectProperty, "SkeletalMesh")),
                    new KeyValuePair<NameReference, PropertyInfo>("bPreserveAnimation",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewBodyMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHeadMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewHairMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("aNewGearMaterials",
                        new PropertyInfo(PropertyType.ArrayProperty, "MaterialInterface")),
                }
            };
            _sequenceObjects["SFXSeqAct_SetStuntMeshes"] = new SequenceObjectInfo { ObjInstanceVersion = 1 };

            _classes["SFXSeqAct_CheckForNewGAWAssetsFixed"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 2, //in LE3Resources.pcc

            };
            _sequenceObjects["SFXSeqAct_CheckForNewGAWAssetsFixed"] = new SequenceObjectInfo();

            _classes["SFXSeqAct_SetEquippedWeaponVisibility"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 8, //in LE3Resources.pcc
            };
            _sequenceObjects["SFXSeqAct_SetEquippedWeaponVisibility"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1,
                inputLinks = new List<string> { "Show", "Hide", "Toggle" }
            };

            _classes["SFXSeqCond_IsCombatMode"] = new ClassInfo
            {
                baseClass = "SequenceCondition",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 17, //in LE3Resources.pcc
            };
            _sequenceObjects["SFXSeqCond_IsCombatMode"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            //Kinkojiro - New Class - SFXSeqAct_SetFaceFX
            _classes["SFXSeqAct_SetFaceFX"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 22, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_aoTargets",
                        new PropertyInfo(PropertyType.ArrayProperty, "Actor")),
                    new KeyValuePair<NameReference, PropertyInfo>("m_pDefaultFaceFXAsset",
                        new PropertyInfo(PropertyType.ObjectProperty, "FaceFXAsset"))
                }
            };
            _sequenceObjects["SFXSeqAct_SetFaceFX"] = new SequenceObjectInfo();

            //Kinkojiro - New Class - SFXSeqAct_SetAutoPlayerLookAt
            _classes["SFXSeqAct_SetAutoLookAtPlayer"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 32, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_aoTargets",
                        new PropertyInfo(PropertyType.ArrayProperty, "Actor")),
                    new KeyValuePair<NameReference, PropertyInfo>("bAutoLookAtPlayer",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("NoticeEnableDistance",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("NoticeDisableDistance",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ReNoticeMinTime",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ReNoticeMaxTime",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("NoticeDuration",
                        new PropertyInfo(PropertyType.FloatProperty))
                }
            };
            _sequenceObjects["SFXSeqAct_SetAutoLookAtPlayer"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            //Kinkojiro - New Class - this returns whether player is using Gamepad or KBM
            _classes["SFXSeqAct_GetControllerType"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 101, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("PlayerObject",
                        new PropertyInfo(PropertyType.ObjectProperty, "Player"))
                }
            };
            _sequenceObjects["SFXSeqAct_GetControllerType"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            //Kinkojiro - New Class - this sets the tlk strings for a GAW category in war assets gui
            _classes["SFXSeqAct_SetGAWCategoryTitles"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                exportIndex = 108, //in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("CategoryId",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("NewTitleRef",
                        new PropertyInfo(PropertyType.StringRefProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("NewDescriptionRef",
                        new PropertyInfo(PropertyType.StringRefProperty))
                }
            };
            _sequenceObjects["SFXSeqAct_SetGAWCategoryTitles"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            //Kinkojiro - New Class - only used in EGM
            _classes["SFXSeqAct_TerminalGUI_EGM"] = new ClassInfo
            {
                baseClass = "BioSequenceLatentAction",
                //pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                //exportIndex = 0, not in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("ExitRequestPin",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_TerminalGUIResouce",
                        new PropertyInfo(PropertyType.ObjectProperty, "GFxMovieInfo")),
                    new KeyValuePair<NameReference, PropertyInfo>("TerminalDataClass",
                        new PropertyInfo(PropertyType.ObjectProperty, "Class")),
                    new KeyValuePair<NameReference, PropertyInfo>("TerminalName",
                        new PropertyInfo(PropertyType.NameProperty))
                }
            };

            //Kinkojiro - New Class - only used in EGM
            _classes["BioSeqAct_ShowMedals"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                //pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                //exportIndex = 0, not in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("bFromMainMenu",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_oGuiReferenced",
                        new PropertyInfo(PropertyType.ObjectProperty, "GFxMovieInfo"))
                }
            };

            //Kinkojiro - New Class - only used in EGM
            _classes["SFXSeqAct_SetGalaxyMapOptions"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                //pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                //exportIndex = 0, not in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_aoGalaxyObjects",
                        new PropertyInfo(PropertyType.ArrayProperty, "SFXGalaxyMapObject")),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fFuelEfficiency",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fFuelTank",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fClusterSpeed",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fSystemSpeed",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fAcceleration",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fDeceleration ",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fClusterAcceleration",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fClusterDeceleration",
                        new PropertyInfo(PropertyType.FloatProperty))
                }
            };
            _sequenceObjects["SFXSeqAct_SetGalaxyMapOptions"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            //Kinkojiro - New Class - only used in EGM
            _classes["SFXSeqAct_SetReaperAggression"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                //pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                //exportIndex = 0, not in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_aoGalaxyObjects",
                        new PropertyInfo(PropertyType.ArrayProperty, "SFXGalaxyMapObject")),
                    new KeyValuePair<NameReference, PropertyInfo>("ScanParticleSystemRadius",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("MaxSpeed",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("Acceleration",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_fScanDetectionRange",
                        new PropertyInfo(PropertyType.FloatProperty))
                }
            };
            _sequenceObjects["SFXSeqAct_SetReaperAggression"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };
            _classes["SFXSeqAct_AwardWeaponByName"] = new ClassInfo
            {
                baseClass = "SequenceAction",
                //pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                //exportIndex = 0, not in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("WeaponClassName",
                        new PropertyInfo(PropertyType.NameProperty))
                }
            };
            _sequenceObjects["SFXSeqAct_AwardWeaponByName"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 1
            };

            //Kinkojiro - New GM Classes - only used in EGM
            _classes["SFXClusterEGM"] = new ClassInfo
            {
                baseClass = "SFXCluster",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("DisplayGAWCondition",
                        new PropertyInfo(PropertyType.IntProperty))
                }
            };
            _classes["SFXSystemEGM"] = new ClassInfo
            {
                baseClass = "SFXSystem",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("m_bCerberusSystem",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ShipChaseWwisePair",
                        new PropertyInfo(PropertyType.StructProperty, "WwiseAudioPair")),
                    new KeyValuePair<NameReference, PropertyInfo>("ShipChaseStopEvent",
                        new PropertyInfo(PropertyType.ObjectProperty, "WwiseEvent"))
                }
            };
            _classes["SFXPlanet_Invaded"] = new ClassInfo
            {
                baseClass = "BioPlanet",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("InvasionCondition",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("PlanetPreviewCondition",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("PreInvasionDescription",
                        new PropertyInfo(PropertyType.StringRefProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_bDestroyedbyReapers",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_bNoPlanetScan",
                        new PropertyInfo(PropertyType.BoolProperty))
                }
            };
            _classes["SFXGalaxyMapShipAppearance"] = new ClassInfo
            {
                baseClass = "SFXGalaxyMapPlanetAppearance",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("AmbientColor",
                        new PropertyInfo(PropertyType.StructProperty, "LinearColor")),
                    new KeyValuePair<NameReference, PropertyInfo>("m_bNeedsLightEnvironment",
                        new PropertyInfo(PropertyType.BoolProperty))
                }
            };
            _classes["SFXGalaxyMapFuelDepotDestroyable"] = new ClassInfo
            {
                baseClass = "SFXGalaxyMapDestroyedFuelDepot",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("DestructionCondition",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("HideFuelSettingCondition",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("EmptyAppearance",
                        new PropertyInfo(PropertyType.ObjectProperty, "SFXGalaxyMapObjectAppearanceBase")),
                    new KeyValuePair<NameReference, PropertyInfo>("EmptyDisplayName",
                        new PropertyInfo(PropertyType.StringRefProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("EmptyDescription",
                        new PropertyInfo(PropertyType.StringRefProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("EmptyTexture",
                        new PropertyInfo(PropertyType.ObjectProperty, "Texture2D")),
                    new KeyValuePair<NameReference, PropertyInfo>("m_bEmptyDepot",
                        new PropertyInfo(PropertyType.BoolProperty))
                }
            };
            _classes["SFXGalaxyMapReaperEGM"] = new ClassInfo
            {
                baseClass = "SFXGalaxyMapReaper",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("EGMSettingCondition",
                        new PropertyInfo(PropertyType.IntProperty))
                }
            };
            _classes["SFXGalaxyMapCerberusShip"] = new ClassInfo
            {
                baseClass = "SFXGalaxyMapReaperEGM",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("ArrowMaterialInstance",
                        new PropertyInfo(PropertyType.ObjectProperty, "MaterialInstanceConstant"))
                }
            };
            //Kinkojiro - New Class - not in resources as has Mail gui. Let me know if anyone wants.
            _classes["SFXSeqAct_MailGUI_Sorted"] = new ClassInfo
            {
                baseClass = "BioSequenceLatentAction",
                //pccPath = GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName,
                //exportIndex = 0, not in LE3Resources.pcc
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("Honorifics",
                        new PropertyInfo(PropertyType.ArrayProperty, "StrProperty")),
                    new KeyValuePair<NameReference, PropertyInfo>("m_bSortMail",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("m_MailGUIResource",
                        new PropertyInfo(PropertyType.ObjectProperty, "GFXMovieInfo")),
                    new KeyValuePair<NameReference, PropertyInfo>("MailDataClass",
                        new PropertyInfo(PropertyType.ObjectProperty, "SFXGUIData_Mail")),
                }
            };
            _sequenceObjects["SFXSeqAct_MailGUI_Sorted"] = new SequenceObjectInfo
            {
                ObjInstanceVersion = 3,
                inputLinks = new List<string> { "Send Mail", "Open UI" }
            };

            GlobalUnrealObjectInfo.AddIntrinsicClasses(_classes, Game);

            _classes["LightMapTexture2D"] = new ClassInfo
            {
                baseClass = "Texture2D",
                pccPath = @"CookedPCConsole\Engine.pcc"
            };

            _classes["StaticMesh"] = new ClassInfo
            {
                baseClass = "Object",
                pccPath = @"CookedPCConsole\Engine.pcc",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleRigidBodyCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleLineCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseSimpleBoxCollision",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bUsedForInstancing",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ForceDoubleSidedShadowVolumes",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("UseFullPrecisionUVs",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("BodySetup",
                        new PropertyInfo(PropertyType.ObjectProperty, "RB_BodySetup")),
                    new KeyValuePair<NameReference, PropertyInfo>("LODDistanceRatio",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapCoordinateIndex",
                        new PropertyInfo(PropertyType.IntProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("LightMapResolution",
                        new PropertyInfo(PropertyType.IntProperty)),
                }
            };

            _classes["FracturedStaticMesh"] = new ClassInfo
            {
                baseClass = "StaticMesh",
                pccPath = @"CookedPCConsole\Engine.pcc",
                properties =
                {
                    new KeyValuePair<NameReference, PropertyInfo>("LoseChunkOutsideMaterial",
                        new PropertyInfo(PropertyType.ObjectProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("bSpawnPhysicsChunks",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bCompositeChunksExplodeOnImpact",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ExplosionVelScale",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("FragmentMinHealth",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("FragmentDestroyEffects",
                        new PropertyInfo(PropertyType.ArrayProperty, "ParticleSystem")),
                    new KeyValuePair<NameReference, PropertyInfo>("FragmentMaxHealth",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("bAlwaysBreakOffIsolatedIslands",
                        new PropertyInfo(PropertyType.BoolProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("DynamicOutsideMaterial",
                        new PropertyInfo(PropertyType.ObjectProperty, "MaterialInterface")),
                    new KeyValuePair<NameReference, PropertyInfo>("ChunkLinVel",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ChunkAngVel",
                        new PropertyInfo(PropertyType.FloatProperty)),
                    new KeyValuePair<NameReference, PropertyInfo>("ChunkLinHorizontalScale",
                        new PropertyInfo(PropertyType.FloatProperty)),
                }
            };
        }
    }

    public class UDKObjectInfo : GameObjectInfo
    {
        public override MEGame Game => MEGame.UDK;
        public UDKObjectInfo() : base() { }
        protected override void AddCustomAndNativeClasses()
        {
            GlobalUnrealObjectInfo.AddIntrinsicClasses(_classes, Game);

            _classes["LightMapTexture2D"] = new ClassInfo
            {
                baseClass = "Texture2D",
                pccPath = @"CookedPCConsole\Engine.pcc"
            };

            _classes["StaticMesh"] = new ClassInfo
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

            _classes["FracturedStaticMesh"] = new ClassInfo
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
    }
}