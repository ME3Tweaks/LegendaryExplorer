using System;
using System.Collections.Generic;
using System.IO;
using ME3ExplorerCore.Gammtek.IO;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public abstract class ObjectBinary
    {
        public ExportEntry Export { get; set; }
        public static T From<T>(ExportEntry export) where T : ObjectBinary, new()
        {
            var t = new T { Export = export };
            t.Serialize(new SerializingContainer2(export.GetReadOnlyBinaryStream(), export.FileRef, true, export.DataOffset + export.propsEnd()));
            return t;
        }

        public static ObjectBinary From(ExportEntry export)
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
                    return From<AnimSequence>(export);
                case "BioStage":
                    return From<BioStage>(export);
                case "Level":
                    return From<Level>(export);
                case "World":
                    return From<World>(export);
                case "Model":
                    return From<Model>(export);
                case "Polys":
                    return From<Polys>(export);
                case "DecalMaterial":
                case "Material":
                    return From<Material>(export);
                case "MaterialInstanceConstant":
                case "MaterialInstanceTimeVarying":
                    if (export.GetProperty<BoolProperty>("bHasStaticPermutationResource")?.Value == true)
                    {
                        return From<MaterialInstance>(export);
                    }
                    return Array.Empty<byte>();
                case "FracturedStaticMesh":
                    return From<FracturedStaticMesh>(export);
                case "StaticMesh":
                    return From<StaticMesh>(export);
                case "SkeletalMesh":
                case "BioSocketSupermodel":
                    return From<SkeletalMesh>(export);
                case "CoverMeshComponent":
                case "InteractiveFoliageComponent":
                case "SplineMeshComponent":
                case "FracturedStaticMeshComponent":
                case "StaticMeshComponent":
                    return From<StaticMeshComponent>(export);
                case "DecalComponent":
                    return From<DecalComponent>(export);
                case "Terrain":
                    return From<Terrain>(export);
                case "TerrainComponent":
                    return From<TerrainComponent>(export);
                case "FluidSurfaceComponent":
                    return From<FluidSurfaceComponent>(export);
                case "ModelComponent":
                    return From<ModelComponent>(export);
                case "BioDynamicAnimSet":
                    return From<BioDynamicAnimSet>(export);
                case "BioPawn":
                    return From<BioPawn>(export);
                case "PrefabInstance":
                    return From<PrefabInstance>(export);
                case "Class":
                    return From<UClass>(export);
                case "State":
                    return From<UState>(export);
                case "Function":
                    return From<UFunction>(export);
                case "Enum":
                    return From<UEnum>(export);
                case "Const":
                    return From<UConst>(export);
                case "ScriptStruct":
                    return From<UScriptStruct>(export);
                case "IntProperty":
                    return From<UIntProperty>(export);
                case "BoolProperty":
                    return From<UBoolProperty>(export);
                case "FloatProperty":
                    return From<UFloatProperty>(export);
                case "NameProperty":
                    return From<UNameProperty>(export);
                case "StrProperty":
                    return From<UStrProperty>(export);
                case "StringRefProperty":
                    return From<UStringRefProperty>(export);
                case "ByteProperty":
                    return From<UByteProperty>(export);
                case "ObjectProperty":
                    return From<UObjectProperty>(export);
                case "ComponentProperty":
                    return From<UComponentProperty>(export);
                case "InterfaceProperty":
                    return From<UInterfaceProperty>(export);
                case "ArrayProperty":
                    return From<UArrayProperty>(export);
                case "StructProperty":
                    return From<UStructProperty>(export);
                case "BioMask4Property":
                    return From<UBioMask4Property>(export);
                case "MapProperty":
                    return From<UMapProperty>(export);
                case "ClassProperty":
                    return From<UClassProperty>(export);
                case "DelegateProperty":
                    return From<UDelegateProperty>(export);
                case "ShaderCache":
                    return From<ShaderCache>(export);
                case "StaticMeshCollectionActor":
                    return From<StaticMeshCollectionActor>(export);
                case "StaticLightCollectionActor":
                    return From<StaticLightCollectionActor>(export);
                case "WwiseEvent":
                    return From<WwiseEvent>(export);
                case "WwiseStream":
                    return From<WwiseStream>(export);
                case "WwiseBank":
                    return From<WwiseBank>(export);
                case "BioGestureRuntimeData":
                    return From<BioGestureRuntimeData>(export);
                case "LightMapTexture2D":
                    return From<LightMapTexture2D>(export);
                case "Texture2D":
                case "ShadowMapTexture2D":
                case "TerrainWeightMapTexture":
                case "TextureFlipBook":
                    return From<UTexture2D>(export);
                case "GuidCache":
                    return From<GuidCache>(export);
                case "FaceFXAnimSet":
                    return From<FaceFXAnimSet>(export);
                case "Bio2DA":
                case "Bio2DANumberedRows":
                    return From<Bio2DABinary>(export);
                case "BioMorphFace":
                    return From<BioMorphFace>(export);
                case "MorphTarget":
                    return From<MorphTarget>(export);
                case "SFXMorphFaceFrontEndDataSource":
                    return From<SFXMorphFaceFrontEndDataSource>(export);
                case "PhysicsAssetInstance":
                    return From<PhysicsAssetInstance>(export);
                case "DirectionalLightComponent":
                case "PointLightComponent":
                case "SkyLightComponent":
                case "SphericalHarmonicLightComponent":
                case "SpotLightComponent":
                case "DominantSpotLightComponent":
                case "DominantPointLightComponent":
                case "DominantDirectionalLightComponent":
                    return From<LightComponent>(export);
                case "ShadowMap1D":
                    return From<ShadowMap1D>(export);
                case "BioTlkFileSet":
                    return From<BioTlkFileSet>(export);
                case "RB_BodySetup":
                    return From<RB_BodySetup>(export);
                case "BrushComponent":
                    return From<BrushComponent>(export);
                case "ForceFeedbackWaveform":
                    return From<ForceFeedbackWaveform>(export);
                case "SoundCue":
                    return From<SoundCue>(export);
                case "SoundNodeWave":
                    return From<SoundNodeWave>(export);
                case "ObjectRedirector":
                    return From<ObjectRedirector>(export);
                case "TextureMovie":
                    return From<TextureMovie>(export);
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
        public virtual List<(UIndex, string)> GetUIndexes(MEGame game) => new List<(UIndex, string)>();
        public virtual List<(NameReference, string)> GetNames(MEGame game) => new List<(NameReference, string)>();

        public virtual void WriteTo(EndianWriter ms, IMEPackage pcc, int fileOffset = 0)
        {
            Serialize(new SerializingContainer2(ms.BaseStream, pcc, false, fileOffset));
        }

        public virtual byte[] ToBytes(IMEPackage pcc, int fileOffset = 0)
        {
            var ms = new EndianReader(new MemoryStream()) { Endian = pcc.Endian };
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