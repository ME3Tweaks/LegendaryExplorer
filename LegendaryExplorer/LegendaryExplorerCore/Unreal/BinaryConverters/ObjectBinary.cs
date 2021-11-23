using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public abstract class ObjectBinary
    {
        public ExportEntry Export { get; init; }
        public static T From<T>(ExportEntry export, PackageCache packageCache = null) where T : ObjectBinary, new()
        {
            var t = new T { Export = export };
            t.Serialize(new SerializingContainer2(export.GetReadOnlyBinaryStream(), export.FileRef, true, export.DataOffset + export.propsEnd(), packageCache));
            return t;
        }

        public static ObjectBinary From(ExportEntry export, PackageCache packageCache = null)
        {
            if (export.IsDefaultObject)
            {
                //DefaultObjects don't have binary
                return null;
            }
            string className = export.ClassName;
            if (IsEither(className, export.Game, "BioPawn", "BioActorBehavior"))
            {
                //export actually being a subclass of BioPawn or BioActorBehavior is rare, so it's simpler and not very costly to just do the lookup again
                if (export.IsA("BioPawn"))
                {
                    //way, waaay too many subclasses of BioPawn to put in the switch statement, so we take care of it here
                    className = "BioPawn";
                }
                else if (export.IsA("BioActorBehavior"))
                {
                    className = "BioActorBehavior";
                }
            }
            switch (className)
            {
                case "AnimSequence":
                    return From<AnimSequence>(export, packageCache);
                case "BioStage":
                    return From<BioStage>(export, packageCache);
                case "Level":
                    return From<Level>(export, packageCache);
                case "World":
                    return From<World>(export, packageCache);
                case "Model":
                    return From<Model>(export, packageCache);
                case "Polys":
                    return From<Polys>(export, packageCache);
                case "DecalMaterial":
                case "Material":
                    return From<Material>(export, packageCache);
                case "MaterialInstanceConstant":
                case "MaterialInstanceTimeVarying":
                    if (export.GetProperty<BoolProperty>("bHasStaticPermutationResource")?.Value == true)
                    {
                        return From<MaterialInstance>(export, packageCache);
                    }
                    return Array.Empty<byte>();
                case "FracturedStaticMesh":
                    return From<FracturedStaticMesh>(export, packageCache);
                case "StaticMesh":
                    return From<StaticMesh>(export, packageCache);
                case "SkeletalMesh":
                case "BioSocketSupermodel":
                    return From<SkeletalMesh>(export, packageCache);
                case "CoverMeshComponent":
                case "InteractiveFoliageComponent":
                case "SplineMeshComponent":
                case "FracturedStaticMeshComponent":
                case "FracturedSkinnedMeshComponent":
                case "StaticMeshComponent":
                    return From<StaticMeshComponent>(export, packageCache);
                case "DecalComponent":
                    return From<DecalComponent>(export, packageCache);
                case "Terrain":
                    return From<Terrain>(export, packageCache);
                case "TerrainComponent":
                    return From<TerrainComponent>(export, packageCache);
                case "FluidSurfaceComponent":
                    return From<FluidSurfaceComponent>(export, packageCache);
                case "ModelComponent":
                    return From<ModelComponent>(export, packageCache);
                case "BioDynamicAnimSet":
                    return From<BioDynamicAnimSet>(export, packageCache);
                case "BioPawn":
                    return From<BioPawn>(export, packageCache);
                case "PrefabInstance":
                    return From<PrefabInstance>(export, packageCache);
                case "Class":
                    return From<UClass>(export, packageCache);
                case "State":
                    return From<UState>(export, packageCache);
                case "Function":
                    return From<UFunction>(export, packageCache);
                case "Enum":
                    return From<UEnum>(export, packageCache);
                case "Const":
                    return From<UConst>(export, packageCache);
                case "ScriptStruct":
                    return From<UScriptStruct>(export, packageCache);
                case "IntProperty":
                    return From<UIntProperty>(export, packageCache);
                case "BoolProperty":
                    return From<UBoolProperty>(export, packageCache);
                case "FloatProperty":
                    return From<UFloatProperty>(export, packageCache);
                case "NameProperty":
                    return From<UNameProperty>(export, packageCache);
                case "StrProperty":
                    return From<UStrProperty>(export, packageCache);
                case "StringRefProperty":
                    return From<UStringRefProperty>(export, packageCache);
                case "ByteProperty":
                    return From<UByteProperty>(export, packageCache);
                case "ObjectProperty":
                    return From<UObjectProperty>(export, packageCache);
                case "ComponentProperty":
                    return From<UComponentProperty>(export, packageCache);
                case "InterfaceProperty":
                    return From<UInterfaceProperty>(export, packageCache);
                case "ArrayProperty":
                    return From<UArrayProperty>(export, packageCache);
                case "StructProperty":
                    return From<UStructProperty>(export, packageCache);
                case "BioMask4Property":
                    return From<UBioMask4Property>(export, packageCache);
                case "MapProperty":
                    return From<UMapProperty>(export, packageCache);
                case "ClassProperty":
                    return From<UClassProperty>(export, packageCache);
                case "DelegateProperty":
                    return From<UDelegateProperty>(export, packageCache);
                case "ShaderCache":
                    return From<ShaderCache>(export, packageCache);
                case "StaticMeshCollectionActor":
                    return From<StaticMeshCollectionActor>(export, packageCache);
                case "StaticLightCollectionActor":
                    return From<StaticLightCollectionActor>(export, packageCache);
                case "WwiseEvent":
                    return From<WwiseEvent>(export, packageCache);
                case "WwiseStream":
                    return From<WwiseStream>(export, packageCache);
                case "WwiseBank":
                    return From<WwiseBank>(export, packageCache);
                case "BioGestureRuntimeData":
                    return From<BioGestureRuntimeData>(export, packageCache);
                case "LightMapTexture2D":
                    return From<LightMapTexture2D>(export, packageCache);
                case "TextureCube":
                    return From<UTextureCube>(export, packageCache);
                case "Texture2D":
                case "ShadowMapTexture2D":
                case "TerrainWeightMapTexture":
                case "TextureFlipBook":
                    return From<UTexture2D>(export, packageCache);
                case "GuidCache":
                    return From<GuidCache>(export, packageCache);
                case "FaceFXAnimSet":
                    return From<FaceFXAnimSet>(export, packageCache);
                case "Bio2DA":
                case "Bio2DANumberedRows":
                    return From<Bio2DABinary>(export, packageCache);
                case "BioMorphFace":
                    return From<BioMorphFace>(export, packageCache);
                case "MorphTarget":
                    return From<MorphTarget>(export, packageCache);
                case "SFXMorphFaceFrontEndDataSource":
                    return From<SFXMorphFaceFrontEndDataSource>(export, packageCache);
                case "PhysicsAssetInstance":
                    return From<PhysicsAssetInstance>(export, packageCache);
                case "DirectionalLightComponent":
                case "PointLightComponent":
                case "SkyLightComponent":
                case "SphericalHarmonicLightComponent":
                case "SpotLightComponent":
                case "DominantSpotLightComponent":
                case "DominantPointLightComponent":
                case "DominantDirectionalLightComponent":
                    return From<LightComponent>(export, packageCache);
                case "ShadowMap1D":
                    return From<ShadowMap1D>(export, packageCache);
                case "BioTlkFileSet":
                    return From<BioTlkFileSet>(export, packageCache);
                case "RB_BodySetup":
                    return From<RB_BodySetup>(export, packageCache);
                case "BrushComponent":
                    return From<BrushComponent>(export, packageCache);
                case "ForceFeedbackWaveform":
                    return From<ForceFeedbackWaveform>(export, packageCache);
                case "SoundCue":
                    return From<SoundCue>(export, packageCache);
                case "SoundNodeWave":
                    return From<SoundNodeWave>(export, packageCache);
                case "ObjectRedirector":
                    return From<ObjectRedirector>(export, packageCache);
                case "TextureMovie":
                    return From<TextureMovie>(export, packageCache);
                case "BioCodexMap":
                    return From<BioCodexMap>(export, packageCache);
                case "BioQuestMap":
                    return From<BioQuestMap>(export, packageCache);
                case "BioStateEventMap":
                    return From<BioStateEventMap>(export, packageCache);
                case "BioSoundNodeWaveStreamingData":
                    return From<BioSoundNodeWaveStreamingData>(export, packageCache);
                case "FaceFXAsset" when export.Game != MEGame.ME2:
                    return From<FaceFXAsset>(export, packageCache);
                case "BioInert":
                    return From<BioInert>(export, packageCache);
                case "BioSquadCombat":
                    return From<BioSquadCombat>(export, packageCache);
                case "BioGestureAnimSetMgr":
                    return From<BioGestureAnimSetMgr>(export, packageCache);
                case "BioQuestProgressionMap":
                    return From<BioQuestProgressionMap>(export, packageCache);
                case "BioDiscoveredCodexMap":
                    return From<BioDiscoveredCodexMap>(export, packageCache);
                case "SpeedTreeComponent":
                    return From<SpeedTreeComponent>(export, packageCache);
                case "BioGamePropertyEventDispatcher":
                    return From<BioGamePropertyEventDispatcher>(export, packageCache);
                case "BioActorBehavior":
                    return From<BioActorBehavior>(export, packageCache);
                default:
                    return null;
            }
        }

        //special purpose version of IsA used to avoid multiple lookups
        private static bool IsEither(string className, MEGame game, string baseClass1, string baseClass2)
        {
            if (className == baseClass1 || className == baseClass2) return true;
            var classes = game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.Classes,
                MEGame.ME2 => ME2UnrealObjectInfo.Classes,
                MEGame.ME3 => ME3UnrealObjectInfo.Classes,
                MEGame.UDK => ME3UnrealObjectInfo.Classes,
                MEGame.LE1 => LE1UnrealObjectInfo.Classes,
                MEGame.LE2 => LE2UnrealObjectInfo.Classes,
                MEGame.LE3 => LE3UnrealObjectInfo.Classes,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
            };
            while (true)
            {
                if (className == baseClass1 || className == baseClass2)
                {
                    return true;
                }
                if (classes.TryGetValue(className, out ClassInfo info))
                {
                    className = info.baseClass;
                }
                else
                {
                    break;
                }
            }
            return false;
        }

        public static ObjectBinary Create(string className, MEGame game, PropertyCollection props = null)
        {
            if (GlobalUnrealObjectInfo.IsA(className, "BioPawn", game))
            {
                //way, waaay too many subclasses of BioPawn to put in the switch statement, so we take care of it here
                className = "BioPawn";
            }
            else if (GlobalUnrealObjectInfo.IsA(className, "BioActorBehavior", game))
            {
                className = "BioActorBehavior";
            }
            switch (className)
            {
                case "AnimSequence":
                    return AnimSequence.Create();
                case "BioStage":
                    return BioStage.Create();
                case "Level":
                    return Level.Create(game);
                case "World":
                    return World.Create();
                case "Model":
                    return Model.Create();
                case "Polys":
                    return Polys.Create();
                case "DecalMaterial":
                case "Material":
                    return Material.Create();
                case "FracturedStaticMesh":
                    return FracturedStaticMesh.Create();
                case "StaticMesh":
                    return StaticMesh.Create();
                case "SkeletalMesh":
                case "BioSocketSupermodel":
                    return SkeletalMesh.Create();
                case "CoverMeshComponent":
                case "InteractiveFoliageComponent":
                case "SplineMeshComponent":
                case "FracturedStaticMeshComponent":
                case "FracturedSkinnedMeshComponent":
                case "StaticMeshComponent":
                    return StaticMeshComponent.Create();
                case "DecalComponent":
                    return DecalComponent.Create();
                case "Terrain":
                    return Terrain.Create();
                case "TerrainComponent":
                    return TerrainComponent.Create();
                case "FluidSurfaceComponent":
                    return FluidSurfaceComponent.Create();
                case "ModelComponent":
                    return ModelComponent.Create();
                case "BioDynamicAnimSet":
                    return BioDynamicAnimSet.Create();
                case "BioPawn":
                    return BioPawn.Create();
                case "PrefabInstance":
                    return PrefabInstance.Create();
                case "Class":
                    return UClass.Create();
                case "State":
                    return UState.Create();
                case "Function":
                    return UFunction.Create();
                case "Enum":
                    return UEnum.Create();
                case "Const":
                    return UConst.Create();
                case "ScriptStruct":
                    return UScriptStruct.Create();
                case "IntProperty":
                    return UIntProperty.Create();
                case "BoolProperty":
                    return UBoolProperty.Create();
                case "FloatProperty":
                    return UFloatProperty.Create();
                case "NameProperty":
                    return UNameProperty.Create();
                case "StrProperty":
                    return UStrProperty.Create();
                case "StringRefProperty":
                    return UStringRefProperty.Create();
                case "ByteProperty":
                    return UByteProperty.Create();
                case "ObjectProperty":
                    return UObjectProperty.Create();
                case "ComponentProperty":
                    return UComponentProperty.Create();
                case "InterfaceProperty":
                    return UInterfaceProperty.Create();
                case "ArrayProperty":
                    return UArrayProperty.Create();
                case "StructProperty":
                    return UStructProperty.Create();
                case "BioMask4Property":
                    return UBioMask4Property.Create();
                case "MapProperty":
                    return UMapProperty.Create();
                case "ClassProperty":
                    return UClassProperty.Create();
                case "DelegateProperty":
                    return UDelegateProperty.Create();
                case "ShaderCache":
                    return ShaderCache.Create();
                case "StaticMeshCollectionActor":
                    return StaticMeshCollectionActor.Create();
                case "StaticLightCollectionActor":
                    return StaticLightCollectionActor.Create();
                case "WwiseEvent":
                    return WwiseEvent.Create();
                case "WwiseStream":
                    return WwiseStream.Create();
                case "WwiseBank":
                    return WwiseBank.Create();
                case "BioGestureRuntimeData":
                    return BioGestureRuntimeData.Create();
                case "LightMapTexture2D":
                    return LightMapTexture2D.Create();
                case "Texture2D":
                case "ShadowMapTexture2D":
                case "TerrainWeightMapTexture":
                case "TextureFlipBook":
                    return UTexture2D.Create();
                case "GuidCache":
                    return GuidCache.Create();
                case "FaceFXAnimSet":
                    return FaceFXAnimSet.Create();
                case "Bio2DA":
                case "Bio2DANumberedRows":
                    return Bio2DABinary.Create();
                case "BioMorphFace":
                    return BioMorphFace.Create();
                case "MorphTarget":
                    return MorphTarget.Create();
                case "SFXMorphFaceFrontEndDataSource":
                    return SFXMorphFaceFrontEndDataSource.Create();
                case "PhysicsAssetInstance":
                    return PhysicsAssetInstance.Create();
                case "DirectionalLightComponent":
                case "PointLightComponent":
                case "SkyLightComponent":
                case "SphericalHarmonicLightComponent":
                case "SpotLightComponent":
                case "DominantSpotLightComponent":
                case "DominantPointLightComponent":
                case "DominantDirectionalLightComponent":
                    return LightComponent.Create();
                case "ShadowMap1D":
                    return ShadowMap1D.Create();
                case "BioTlkFileSet":
                    return BioTlkFileSet.Create();
                case "RB_BodySetup":
                    return RB_BodySetup.Create();
                case "BrushComponent":
                    return BrushComponent.Create();
                case "ForceFeedbackWaveform":
                    return ForceFeedbackWaveform.Create(props);
                case "SoundCue":
                    return SoundCue.Create();
                case "SoundNodeWave":
                    return SoundNodeWave.Create();
                case "ObjectRedirector":
                    return ObjectRedirector.Create();
                case "TextureMovie":
                    return TextureMovie.Create();
                case "BioCodexMap":
                    return BioCodexMap.Create();
                case "BioQuestMap":
                    return BioQuestMap.Create();
                case "BioStateEventMap":
                    return BioStateEventMap.Create();
                case "BioSoundNodeWaveStreamingData":
                    return BioSoundNodeWaveStreamingData.Create();
                case "FaceFXAsset":
                    if (game != MEGame.ME2)
                    {
                        return FaceFXAsset.Create();
                    }
                    break;
                case "BioInert":
                    return BioInert.Create();
                case "BioGestureAnimSetMgr":
                    return BioGestureAnimSetMgr.Create();
                case "BioQuestProgressionMap":
                    return BioQuestProgressionMap.Create();
                case "BioDiscoveredCodexMap":
                    return BioDiscoveredCodexMap.Create();
                case "SpeedTreeComponent":
                    return SpeedTreeComponent.Create();
                case "BioGamePropertyEventDispatcher":
                    return BioGamePropertyEventDispatcher.Create();
                case "BioActorBehavior":
                    return BioActorBehavior.Create();
                case "MaterialInstanceConstant":
                case "MaterialInstanceTimeVarying":
                    if (props?.GetProp<BoolProperty>("bHasStaticPermutationResource")?.Value is true)
                    {
                        return MaterialInstance.Create();
                    }
                    break;
            }
            return GenericObjectBinary.Create();
        }

        protected abstract void Serialize(SerializingContainer2 sc);

        /// <summary>
        /// Gets a list of entry references made in this ObjectBinary. Values are UIndex mapped to the name of the variable name of that list item, e.g. (25, "DataOffset")
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public virtual List<(UIndex, string)> GetUIndexes(MEGame game) => new();
        public virtual List<(NameReference, string)> GetNames(MEGame game) => new();

        public virtual void WriteTo(EndianWriter ms, IMEPackage pcc, int fileOffset = 0)
        {
            Serialize(new SerializingContainer2(ms.BaseStream, pcc, false, fileOffset));
        }

        public virtual byte[] ToBytes(IMEPackage pcc, int fileOffset = 0)
        {
            using var ms = new EndianReader(MemoryManager.GetMemoryStream()) { Endian = pcc.Endian };
            WriteTo(ms.Writer, pcc, fileOffset);
            return ms.ToArray();
        }

        public static implicit operator ObjectBinary(byte[] buff)
        {
            return new GenericObjectBinary(buff);
        }
    }

    public sealed class GenericObjectBinary : ObjectBinary
    {
        private byte[] data;

        public GenericObjectBinary(byte[] buff)
        {
            data = buff;
        }

        //should never be called
        protected override void Serialize(SerializingContainer2 sc)
        {
            data = sc.ms.BaseStream.ReadFully();
        }

        public static GenericObjectBinary Create() => new(Array.Empty<byte>());

        public override void WriteTo(EndianWriter ms, IMEPackage pcc, int fileOffset = 0)
        {
            ms.WriteFromBuffer(data);
        }

        public override byte[] ToBytes(IMEPackage pcc, int fileOffset = 0)
        {
            return data;
        }
    }
}