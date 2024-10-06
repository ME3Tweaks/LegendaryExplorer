using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public abstract class ObjectBinary
    {
        [JsonIgnore]
        public ExportEntry Export { get; init; }
        public static T From<T>(ExportEntry export, PackageCache packageCache = null, int? dataOffsetOverride = null) where T : ObjectBinary, new()
        {
            var t = new T { Export = export };
            t.Serialize(new SerializingContainer(export.GetReadOnlyBinaryStream(), export.FileRef, true, (dataOffsetOverride ?? export.DataOffset) + export.propsEnd(), packageCache));
            return t;
        }

        public static ObjectBinary From(ExportEntry export, PackageCache packageCache = null, int? dataOffsetOverride = null)
        {
            if (export.IsDefaultObject)
            {
                //DefaultObjects don't have binary
                return null;
            }
            string className = export.ClassName;
            
            switch (className)
            {
                case "AnimSequence":
                    return From<AnimSequence>(export, packageCache, dataOffsetOverride);
                case "BioStage":
                    return From<BioStage>(export, packageCache, dataOffsetOverride);
                case "Level":
                    return From<Level>(export, packageCache, dataOffsetOverride);
                case "World":
                    return From<World>(export, packageCache, dataOffsetOverride);
                case "Model":
                    return From<Model>(export, packageCache, dataOffsetOverride);
                case "Polys":
                    return From<Polys>(export, packageCache, dataOffsetOverride);
                case "DecalMaterial":
                case "Material":
                    return From<Material>(export, packageCache, dataOffsetOverride);
                case "MaterialInstanceConstant":
                case "MaterialInstanceTimeVarying":
                    if (export.GetProperty<BoolProperty>("bHasStaticPermutationResource")?.Value == true)
                    {
                        return From<MaterialInstance>(export, packageCache, dataOffsetOverride);
                    }
                    return Array.Empty<byte>();
                case "FracturedStaticMesh":
                    return From<FracturedStaticMesh>(export, packageCache, dataOffsetOverride);
                case "StaticMesh":
                    return From<StaticMesh>(export, packageCache, dataOffsetOverride);
                case "SkeletalMesh":
                case "BioSocketSupermodel":
                    return From<SkeletalMesh>(export, packageCache, dataOffsetOverride);
                case "CoverMeshComponent":
                case "InteractiveFoliageComponent":
                case "SplineMeshComponent":
                case "FracturedStaticMeshComponent":
                case "FracturedSkinnedMeshComponent":
                case "StaticMeshComponent":
                    return From<StaticMeshComponent>(export, packageCache, dataOffsetOverride);
                case "DecalComponent":
                    return From<DecalComponent>(export, packageCache, dataOffsetOverride);
                case "Terrain":
                    return From<Terrain>(export, packageCache, dataOffsetOverride);
                case "TerrainComponent":
                    return From<TerrainComponent>(export, packageCache, dataOffsetOverride);
                case "FluidSurfaceComponent":
                    return From<FluidSurfaceComponent>(export, packageCache, dataOffsetOverride);
                case "ModelComponent":
                    return From<ModelComponent>(export, packageCache, dataOffsetOverride);
                case "BioDynamicAnimSet":
                    return From<BioDynamicAnimSet>(export, packageCache, dataOffsetOverride);
                case "PrefabInstance":
                    return From<PrefabInstance>(export, packageCache, dataOffsetOverride);
                case "Class":
                    return From<UClass>(export, packageCache, dataOffsetOverride);
                case "State":
                    return From<UState>(export, packageCache, dataOffsetOverride);
                case "Function":
                    return From<UFunction>(export, packageCache, dataOffsetOverride);
                case "Enum":
                    return From<UEnum>(export, packageCache, dataOffsetOverride);
                case "Const":
                    return From<UConst>(export, packageCache, dataOffsetOverride);
                case "ScriptStruct":
                    return From<UScriptStruct>(export, packageCache, dataOffsetOverride);
                case "IntProperty":
                    return From<UIntProperty>(export, packageCache, dataOffsetOverride);
                case "BoolProperty":
                    return From<UBoolProperty>(export, packageCache, dataOffsetOverride);
                case "FloatProperty":
                    return From<UFloatProperty>(export, packageCache, dataOffsetOverride);
                case "NameProperty":
                    return From<UNameProperty>(export, packageCache, dataOffsetOverride);
                case "StrProperty":
                    return From<UStrProperty>(export, packageCache, dataOffsetOverride);
                case "StringRefProperty":
                    return From<UStringRefProperty>(export, packageCache, dataOffsetOverride);
                case "ByteProperty":
                    return From<UByteProperty>(export, packageCache, dataOffsetOverride);
                case "ObjectProperty":
                    return From<UObjectProperty>(export, packageCache, dataOffsetOverride);
                case "ComponentProperty":
                    return From<UComponentProperty>(export, packageCache, dataOffsetOverride);
                case "InterfaceProperty":
                    return From<UInterfaceProperty>(export, packageCache, dataOffsetOverride);
                case "ArrayProperty":
                    return From<UArrayProperty>(export, packageCache, dataOffsetOverride);
                case "StructProperty":
                    return From<UStructProperty>(export, packageCache, dataOffsetOverride);
                case "BioMask4Property":
                    return From<UBioMask4Property>(export, packageCache, dataOffsetOverride);
                case "MapProperty":
                    return From<UMapProperty>(export, packageCache, dataOffsetOverride);
                case "ClassProperty":
                    return From<UClassProperty>(export, packageCache, dataOffsetOverride);
                case "DelegateProperty":
                    return From<UDelegateProperty>(export, packageCache, dataOffsetOverride);
                case "ShaderCache":
                    return From<ShaderCache>(export, packageCache, dataOffsetOverride);
                case "StaticMeshCollectionActor":
                    return From<StaticMeshCollectionActor>(export, packageCache, dataOffsetOverride);
                case "StaticLightCollectionActor":
                    return From<StaticLightCollectionActor>(export, packageCache, dataOffsetOverride);
                case "WwiseEvent":
                    return From<WwiseEvent>(export, packageCache, dataOffsetOverride);
                case "WwiseStream":
                    return From<WwiseStream>(export, packageCache, dataOffsetOverride);
                case "WwiseBank":
                    return From<WwiseBank>(export, packageCache, dataOffsetOverride);
                case "BioGestureRuntimeData":
                    return From<BioGestureRuntimeData>(export, packageCache, dataOffsetOverride);
                case "TextureRenderTarget2D":
                    return From<UTextureRenderTarget2D>(export, packageCache, dataOffsetOverride);
                case "LightMapTexture2D":
                    return From<LightMapTexture2D>(export, packageCache, dataOffsetOverride);
                case "TextureCube":
                    return From<UTextureCube>(export, packageCache, dataOffsetOverride);
                case "Texture2D":
                case "ShadowMapTexture2D":
                case "TerrainWeightMapTexture":
                case "TextureFlipBook":
                    return From<UTexture2D>(export, packageCache, dataOffsetOverride);
                case "GuidCache":
                    return From<GuidCache>(export, packageCache, dataOffsetOverride);
                case "FaceFXAnimSet":
                    return From<FaceFXAnimSet>(export, packageCache, dataOffsetOverride);
                case "Bio2DA":
                case "Bio2DANumberedRows":
                    return From<Bio2DABinary>(export, packageCache, dataOffsetOverride);
                case "BioMorphFace":
                    return From<BioMorphFace>(export, packageCache, dataOffsetOverride);
                case "MorphTarget":
                    return From<MorphTarget>(export, packageCache, dataOffsetOverride);
                case "SFXMorphFaceFrontEndDataSource":
                    return From<SFXMorphFaceFrontEndDataSource>(export, packageCache, dataOffsetOverride);
                case "PhysicsAssetInstance":
                    return From<PhysicsAssetInstance>(export, packageCache, dataOffsetOverride);
                case "DirectionalLightComponent":
                case "PointLightComponent":
                case "SkyLightComponent":
                case "SphericalHarmonicLightComponent":
                case "SpotLightComponent":
                case "DominantSpotLightComponent":
                case "DominantPointLightComponent":
                case "DominantDirectionalLightComponent":
                    return From<LightComponent>(export, packageCache, dataOffsetOverride);
                case "ShadowMap1D":
                    return From<ShadowMap1D>(export, packageCache, dataOffsetOverride);
                case "BioTlkFileSet":
                    return From<BioTlkFileSet>(export, packageCache, dataOffsetOverride);
                case "RB_BodySetup":
                    return From<RB_BodySetup>(export, packageCache, dataOffsetOverride);
                case "BrushComponent":
                    return From<BrushComponent>(export, packageCache, dataOffsetOverride);
                case "ForceFeedbackWaveform":
                    return From<ForceFeedbackWaveform>(export, packageCache, dataOffsetOverride);
                case "SoundCue":
                    return From<SoundCue>(export, packageCache, dataOffsetOverride);
                case "SoundNodeWave":
                    return From<SoundNodeWave>(export, packageCache, dataOffsetOverride);
                case "ObjectRedirector":
                    return From<ObjectRedirector>(export, packageCache, dataOffsetOverride);
                case "TextureMovie":
                    return From<TextureMovie>(export, packageCache, dataOffsetOverride);
                case "BioCodexMap":
                    return From<BioCodexMap>(export, packageCache, dataOffsetOverride);
                case "BioQuestMap":
                    return From<BioQuestMap>(export, packageCache, dataOffsetOverride);
                case "BioStateEventMap":
                    return From<BioStateEventMap>(export, packageCache, dataOffsetOverride);
                case "BioSoundNodeWaveStreamingData":
                    return From<BioSoundNodeWaveStreamingData>(export, packageCache, dataOffsetOverride);
                case "FaceFXAsset" when export.Game != MEGame.ME2:
                    return From<FaceFXAsset>(export, packageCache, dataOffsetOverride);
                case "BioInert":
                    return From<BioInert>(export, packageCache, dataOffsetOverride);
                case "BioSquadCombat":
                    return From<BioSquadCombat>(export, packageCache, dataOffsetOverride);
                case "BioGestureAnimSetMgr":
                    return From<BioGestureAnimSetMgr>(export, packageCache, dataOffsetOverride);
                case "BioQuestProgressionMap":
                    return From<BioQuestProgressionMap>(export, packageCache, dataOffsetOverride);
                case "BioDiscoveredCodexMap":
                    return From<BioDiscoveredCodexMap>(export, packageCache, dataOffsetOverride);
                case "SpeedTreeComponent":
                    return From<SpeedTreeComponent>(export, packageCache, dataOffsetOverride);
                case "BioGamePropertyEventDispatcher":
                    return From<BioGamePropertyEventDispatcher>(export, packageCache, dataOffsetOverride);
                default:
                    //way, waaay too many subclasses of BioPawn and BioActorBehavior to put in the switch statement, so we take care of it here
                    if (IsEither(className, export.Game, "BioPawn", "BioActorBehavior"))
                    {
                        //export actually being a subclass of BioPawn or BioActorBehavior is rare, so it's simpler and not very costly to just do the lookup again
                        if (export.IsA("BioPawn"))
                        {
                            return From<BioPawn>(export, packageCache, dataOffsetOverride);
                        }
                        if (export.IsA("BioActorBehavior"))
                        {
                            return From<BioActorBehavior>(export, packageCache, dataOffsetOverride);
                        }
                    }
                    return null;
            }
        }

        //special purpose version of IsA used to avoid multiple lookups
        private static bool IsEither(string className, MEGame game, string baseClass1, string baseClass2)
        {
            if (className == baseClass1 || className == baseClass2) return true;
            var classes = game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.ObjectInfo.Classes,
                MEGame.ME2 => ME2UnrealObjectInfo.ObjectInfo.Classes,
                MEGame.ME3 => ME3UnrealObjectInfo.ObjectInfo.Classes,
                MEGame.UDK => ME3UnrealObjectInfo.ObjectInfo.Classes,
                MEGame.LE1 => LE1UnrealObjectInfo.ObjectInfo.Classes,
                MEGame.LE2 => LE2UnrealObjectInfo.ObjectInfo.Classes,
                MEGame.LE3 => LE3UnrealObjectInfo.ObjectInfo.Classes,
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
                    return FaceFXAnimSet.Create(game);
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
                        return FaceFXAsset.Create(game);
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
                case "MaterialInstanceConstant":
                case "MaterialInstanceTimeVarying":
                    if (props?.GetProp<BoolProperty>("bHasStaticPermutationResource")?.Value is true)
                    {
                        return MaterialInstance.Create();
                    }
                    break;
            }
            if (GlobalUnrealObjectInfo.IsA(className, "BioPawn", game))
            {
                return BioPawn.Create();
            }
            if (GlobalUnrealObjectInfo.IsA(className, "BioActorBehavior", game))
            {
                return BioActorBehavior.Create();
            }
            return GenericObjectBinary.Create();
        }

        protected abstract void Serialize(SerializingContainer sc);

        /// <summary>
        /// Gets a list of entry references made in this ObjectBinary.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public List<int> GetUIndexes(MEGame game)
        {
            var result = new List<int>();
            ForEachUIndex(game, new UIndexCollector(result));
            return result;
        }
        public virtual List<(NameReference, string)> GetNames(MEGame game) => new();

        /// <summary>
        /// Calls <see cref="IUIndexAction.Invoke"/> on every UIndex in this object. This can be used to change the UIndex, or to perform some action based on each one.
        /// </summary>
        /// <typeparam name="TAction">A readonly struct that implements <see cref="IUIndexAction"/>. Examples: <see cref="UIndexZeroer"/>, <see cref="UIndexAndPropNameCollector"/></typeparam>
        /// <param name="game">Restricts the UIndexes to the ones that exist on this game's version of the object.</param>
        /// <param name="action">The <see cref="IUIndexAction"/> implementation whose Invoke method will be called for every uIndex.</param>
        public virtual void ForEachUIndex<TAction>(MEGame game, in TAction action) where TAction : struct, IUIndexAction
        {
            //Not every Object has UIndexes. For those that don't, we do nothing.
        }

        public virtual void WriteTo(EndianWriter ms, IMEPackage pcc, int fileOffset = 0)
        {
            Serialize(new SerializingContainer(ms.BaseStream, pcc, false, fileOffset));
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

        #region ForEachUIndex Helper methods
        internal static void ForEachUIndexKeyInMultiMap<TAction, TValue>(in TAction action, UMultiMap<int, TValue> multiMap, string name) where TAction : struct, IUIndexAction
        {
            var refEnumerator = multiMap.DangerousGetRefEnumerator();
            int i = 0;
            bool isUpdated = false;
            while (refEnumerator.MoveNext())
            {
                ref KeyValuePair<int, TValue> kvp = ref refEnumerator.CurrentRef;
                int key = kvp.Key;
                int originalValue = key;
                Unsafe.AsRef(in action).Invoke(ref key, $"{name}[{i}]");
                if (key != originalValue)
                {
                    isUpdated = true;
                    kvp = new KeyValuePair<int, TValue>(key, kvp.Value);
                }
                ++i;
            }
            if (isUpdated)
            {
                multiMap.Rehash();
            }
        }
        internal static void ForEachUIndexValueInMultiMap<TAction, TKey>(in TAction action, UMultiMap<TKey, int> multiMap, string name) where TAction : struct, IUIndexAction
        {
            var refEnumerator = multiMap.DangerousGetRefEnumerator();
            int i = 0;
            while (refEnumerator.MoveNext())
            {
                ref var kvp = ref refEnumerator.CurrentRef;
                int value = kvp.Value;
                int originalValue = value;
                Unsafe.AsRef(in action).Invoke(ref value, $"{name}[{i}]");
                if (value != originalValue)
                {
                    kvp = new (kvp.Key, value);
                }
                ++i;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ForEachUIndexInSpan<TAction>(in TAction action, Span<int> span, string name) where TAction : struct, IUIndexAction
        {
            for (int i = 0; i < span.Length; i++)
            {
                Unsafe.AsRef(in action).Invoke(ref span[i], $"{name}[{i}]");
            }
        }
        #endregion
    }

    /// <summary>
    /// See documentation for <see cref="ObjectBinary.ForEachUIndex"/>.
    /// </summary>
    //this should only be implemented by readonly structs!
    public interface IUIndexAction
    {
        void Invoke(ref int uIndex, string propName);
    }

    /// <summary>
    /// Sets all UIndexes to 0
    /// </summary>
    public readonly struct UIndexZeroer : IUIndexAction
    {
        public void Invoke(ref int uIndex, string propName) => uIndex = 0;
    }

    /// <summary>
    /// Puts all the UIndexes in a List
    /// </summary>
    public readonly struct UIndexCollector(List<int> uIndexes) : IUIndexAction
    {
        public void Invoke(ref int uIndex, string propName)
        {
            uIndexes.Add(uIndex);
        }
    }

    /// <summary>
    /// Puts all the UIndexes and PropNames in a List
    /// </summary>
    public readonly struct UIndexAndPropNameCollector(List<(int, string)> uindexAndPropNames) : IUIndexAction
    {
        public void Invoke(ref int uIndex, string propName)
        {
            uindexAndPropNames.Add((uIndex, propName));
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
        protected override void Serialize(SerializingContainer sc)
        {
            data = sc.ms.BaseStream.ReadFully();
        }

        public static GenericObjectBinary Create() => new([]);

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