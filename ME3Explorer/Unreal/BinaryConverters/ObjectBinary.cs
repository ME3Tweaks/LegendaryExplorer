using System;
using System.Collections.Generic;
using System.IO;
using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.Packages;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public abstract class ObjectBinary
    {
        public ExportEntry Export { get; set; }
        public static T From<T>(ExportEntry export) where T : ObjectBinary, new()
        {
            var t = new T {Export = export};
            t.Serialize(new SerializingContainer2(new MemoryStream(export.getBinaryData()), export.FileRef, true, export.DataOffset + export.propsEnd()));
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
            if (export.IsOrInheritsFrom("BioPawn"))
            {
                //way, waaay too many subclasses of BioPawn to put in the switch statement, so we take care of it here
                className = "BioPawn";
            }
            switch (className)
            {
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
                case "BoolProperty":
                case "FloatProperty":
                case "NameProperty":
                case "StrProperty":
                case "StringRefProperty":
                    return From<UValueProperty>(export);
                case "ByteProperty":
                case "ObjectProperty":
                case "ComponentProperty":
                case "InterfaceProperty":
                case "ArrayProperty":
                case "StructProperty":
                case "BioMask4Property":
                    return From<UReferenceProperty>(export);
                case "MapProperty":
                case "ClassProperty":
                case "DelegateProperty":
                    return From<UTwoReferenceProperty>(export);
                case "ShaderCache":
                    return From<ShaderCache>(export);
                default:
                    return null;
            }
        }

        protected abstract void Serialize(SerializingContainer2 sc);

        public virtual List<(UIndex, string)> GetUIndexes(MEGame game) => new List<(UIndex, string)>();

        public virtual void WriteTo(Stream ms, IMEPackage pcc, int fileOffset = 0)
        {
            Serialize(new SerializingContainer2(ms, pcc, false, fileOffset));
        }

        public virtual byte[] ToBytes(IMEPackage pcc, int fileOffset = 0)
        {
            var ms = new MemoryStream();
            WriteTo(ms, pcc, fileOffset);
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
            data = sc.ms.ReadFully();
        }

        public override void WriteTo(Stream ms, IMEPackage pcc, int fileOffset)
        {
            ms.WriteFromBuffer(data);
        }

        public override byte[] ToBytes(IMEPackage pcc, int fileOffset = 0)
        {
            return data;
        }
    }
}