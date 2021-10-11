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
        public ExportEntry Export { get; set; }
        public static T From<T>(ExportEntry export, PackageCache packageCache = null) where T : ObjectBinary, new()
        {
            var t = new T { Export = export };
            t.Serialize(new SerializingContainer2(export.GetReadOnlyBinaryStream(), export.FileRef, true, export.DataOffset + export.propsEnd(), packageCache));
            return t;
        }

        public static T FromDEBUG<T>(ExportEntry export, PackageCache packageCache) where T : ObjectBinary, new()
        {
            var t = new T { Export = export };
            var dataStartPosition = export.DataOffset;
            var binaryStartPos = dataStartPosition + export.propsEnd();
            var serializer = new SerializingContainer2(export.GetReadOnlyBinaryStream(), export.FileRef, true, binaryStartPos);
            t.Serialize(serializer);
            if (serializer.FileOffset - dataStartPosition != export.DataSize)
            {
                Debug.WriteLine($@"Serial size mismatch on {export.InstancedFullPath}! Parsing stopped at {(serializer.FileOffset - dataStartPosition)} bytes, but export is {export.DataSize} bytes");
                Debugger.Break();
            }

            return t;
        }

        public static ObjectBinary FromDEBUG(ExportEntry export, PackageCache packageCache = null)
        {
            if (export.IsDefaultObject)
            {
                //DefaultObjects don't have binary
                return null;
            }
            string className = export.ClassName;
            if (export.IsA("BioPawn"))
            {
                //way, waaay too many subclasses of BioPawn to put in the switch statement, so we take care of it here
                className = "BioPawn";
            }
            switch (className)
            {
                case "AnimSequence":
                    return FromDEBUG<AnimSequence>(export, packageCache);
                case "BioStage":
                    return FromDEBUG<BioStage>(export, packageCache);
                case "Level":
                    return FromDEBUG<Level>(export, packageCache);
                case "World":
                    return FromDEBUG<World>(export, packageCache);
                case "Model":
                    return FromDEBUG<Model>(export, packageCache);
                case "Polys":
                    return FromDEBUG<Polys>(export, packageCache);
                case "DecalMaterial":
                case "Material":
                    return FromDEBUG<Material>(export, packageCache);
                case "MaterialInstanceConstant":
                case "MaterialInstanceTimeVarying":
                    if (export.GetProperty<BoolProperty>("bHasStaticPermutationResource")?.Value == true)
                    {
                        return FromDEBUG<MaterialInstance>(export, packageCache);
                    }
                    return Array.Empty<byte>();
                case "FracturedStaticMesh":
                    return FromDEBUG<FracturedStaticMesh>(export, packageCache);
                case "StaticMesh":
                    return FromDEBUG<StaticMesh>(export, packageCache);
                case "SkeletalMesh":
                case "BioSocketSupermodel":
                    return FromDEBUG<SkeletalMesh>(export, packageCache);
                case "CoverMeshComponent":
                case "InteractiveFoliageComponent":
                case "SplineMeshComponent":
                case "FracturedStaticMeshComponent":
                case "StaticMeshComponent":
                    return FromDEBUG<StaticMeshComponent>(export, packageCache);
                case "DecalComponent":
                    return FromDEBUG<DecalComponent>(export, packageCache);
                case "Terrain":
                    return FromDEBUG<Terrain>(export, packageCache);
                case "TerrainComponent":
                    return FromDEBUG<TerrainComponent>(export, packageCache);
                case "FluidSurfaceComponent":
                    return FromDEBUG<FluidSurfaceComponent>(export, packageCache);
                case "ModelComponent":
                    return FromDEBUG<ModelComponent>(export, packageCache);
                case "BioDynamicAnimSet":
                    return FromDEBUG<BioDynamicAnimSet>(export, packageCache);
                case "BioPawn":
                    return FromDEBUG<BioPawn>(export, packageCache);
                case "PrefabInstance":
                    return FromDEBUG<PrefabInstance>(export, packageCache);
                case "Class":
                    return FromDEBUG<UClass>(export, packageCache);
                case "State":
                    return FromDEBUG<UState>(export, packageCache);
                case "Function":
                    return FromDEBUG<UFunction>(export, packageCache);
                case "Enum":
                    return FromDEBUG<UEnum>(export, packageCache);
                case "Const":
                    return FromDEBUG<UConst>(export, packageCache);
                case "ScriptStruct":
                    return FromDEBUG<UScriptStruct>(export, packageCache);
                case "IntProperty":
                    return FromDEBUG<UIntProperty>(export, packageCache);
                case "BoolProperty":
                    return FromDEBUG<UBoolProperty>(export, packageCache);
                case "FloatProperty":
                    return FromDEBUG<UFloatProperty>(export, packageCache);
                case "NameProperty":
                    return FromDEBUG<UNameProperty>(export, packageCache);
                case "StrProperty":
                    return FromDEBUG<UStrProperty>(export, packageCache);
                case "StringRefProperty":
                    return FromDEBUG<UStringRefProperty>(export, packageCache);
                case "ByteProperty":
                    return FromDEBUG<UByteProperty>(export, packageCache);
                case "ObjectProperty":
                    return FromDEBUG<UObjectProperty>(export, packageCache);
                case "ComponentProperty":
                    return FromDEBUG<UComponentProperty>(export, packageCache);
                case "InterfaceProperty":
                    return FromDEBUG<UInterfaceProperty>(export, packageCache);
                case "ArrayProperty":
                    return FromDEBUG<UArrayProperty>(export, packageCache);
                case "StructProperty":
                    return FromDEBUG<UStructProperty>(export, packageCache);
                case "BioMask4Property":
                    return FromDEBUG<UBioMask4Property>(export, packageCache);
                case "MapProperty":
                    return FromDEBUG<UMapProperty>(export, packageCache);
                case "ClassProperty":
                    return FromDEBUG<UClassProperty>(export, packageCache);
                case "DelegateProperty":
                    return FromDEBUG<UDelegateProperty>(export, packageCache);
                case "ShaderCache":
                    return FromDEBUG<ShaderCache>(export, packageCache);
                case "StaticMeshCollectionActor":
                    return FromDEBUG<StaticMeshCollectionActor>(export, packageCache);
                case "StaticLightCollectionActor":
                    return FromDEBUG<StaticLightCollectionActor>(export, packageCache);
                case "WwiseEvent":
                    return FromDEBUG<WwiseEvent>(export, packageCache);
                case "WwiseStream":
                    return FromDEBUG<WwiseStream>(export, packageCache);
                case "WwiseBank":
                    return FromDEBUG<WwiseBank>(export, packageCache);
                case "BioGestureRuntimeData":
                    return FromDEBUG<BioGestureRuntimeData>(export, packageCache);
                case "LightMapTexture2D":
                    return FromDEBUG<LightMapTexture2D>(export, packageCache);
                case "Texture2D":
                case "ShadowMapTexture2D":
                case "TerrainWeightMapTexture":
                case "TextureFlipBook":
                    return FromDEBUG<UTexture2D>(export, packageCache);
                case "GuidCache":
                    return FromDEBUG<GuidCache>(export, packageCache);
                case "FaceFXAnimSet":
                    return FromDEBUG<FaceFXAnimSet>(export, packageCache);
                case "Bio2DA":
                case "Bio2DANumberedRows":
                    return FromDEBUG<Bio2DABinary>(export, packageCache);
                case "BioMorphFace":
                    return FromDEBUG<BioMorphFace>(export, packageCache);
                case "MorphTarget":
                    return FromDEBUG<MorphTarget>(export, packageCache);
                case "SFXMorphFaceFrontEndDataSource":
                    return FromDEBUG<SFXMorphFaceFrontEndDataSource>(export, packageCache);
                case "PhysicsAssetInstance":
                    return FromDEBUG<PhysicsAssetInstance>(export, packageCache);
                case "DirectionalLightComponent":
                case "PointLightComponent":
                case "SkyLightComponent":
                case "SphericalHarmonicLightComponent":
                case "SpotLightComponent":
                case "DominantSpotLightComponent":
                case "DominantPointLightComponent":
                case "DominantDirectionalLightComponent":
                    return FromDEBUG<LightComponent>(export, packageCache);
                case "ShadowMap1D":
                    return FromDEBUG<ShadowMap1D>(export, packageCache);
                case "BioTlkFileSet":
                    return FromDEBUG<BioTlkFileSet>(export, packageCache);
                case "RB_BodySetup":
                    return FromDEBUG<RB_BodySetup>(export, packageCache);
                case "BrushComponent":
                    return FromDEBUG<BrushComponent>(export, packageCache);
                case "ForceFeedbackWaveform":
                    return FromDEBUG<ForceFeedbackWaveform>(export, packageCache);
                case "SoundCue":
                    return FromDEBUG<SoundCue>(export, packageCache);
                case "SoundNodeWave":
                    return FromDEBUG<SoundNodeWave>(export, packageCache);
                case "ObjectRedirector":
                    return FromDEBUG<ObjectRedirector>(export, packageCache);
                case "TextureMovie":
                    return FromDEBUG<TextureMovie>(export, packageCache);
                case "BioSoundNodeWaveStreamingData":
                    return FromDEBUG<BioSoundNodeWaveStreamingData>(export, packageCache);
                case "BioInert":
                    return FromDEBUG<BioInert>(export, packageCache);
                default:
                    return null;
            }
        }

        public static ObjectBinary From(ExportEntry export, PackageCache packageCache = null)
        {
            if (export.IsDefaultObject)
            {
                //DefaultObjects don't have binary
                return null;
            }
            string className = export.ClassName;
            if (export.IsA("BioPawn"))
            {
                //way, waaay too many subclasses of BioPawn to put in the switch statement, so we take care of it here
                className = "BioPawn";
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
                default:
                    return null;
            }
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