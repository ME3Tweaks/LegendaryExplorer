using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Unreal.Classes
{
    public class BioMorphFace
    {
        public ExportEntry Export { get; }
        public BinaryConverters.BioMorphFace MorphFace { get; }
        public SkeletalMesh BaseHead { get; }
        public SkeletalMesh HairMesh { get; }
        public List<MorphFeature> MorphFeatures { get; } = new();
        public List<BoneOffset> BoneOffsets { get; } = new();

        public IEntry m_oBaseHead;
        public IEntry m_oHairMesh;

        public bool IsExportable => m_oBaseHead != null;
        public BioMorphFace(ExportEntry morphExp)
        {
            Export = morphExp;
            MorphFace = ObjectBinary.From<BinaryConverters.BioMorphFace>(morphExp);
            ParseProperties(Export.GetProperties());

            BaseHead = LoadMeshFromEntry(m_oBaseHead);
            HairMesh = LoadMeshFromEntry(m_oHairMesh);
        }

        private void ParseProperties(PropertyCollection props)
        {
            var headProp = props.GetProp<ObjectProperty>("m_oBaseHead");
            m_oBaseHead = headProp?.ResolveToEntry(Export.FileRef);
            var hairProp = props.GetProp<ObjectProperty>("m_oHairMesh");
            m_oHairMesh = hairProp?.ResolveToEntry(Export.FileRef);

            MorphFeatures.AddRange(props.GetProp<ArrayProperty<StructProperty>>("m_aMorphFeatures").Select(e => new MorphFeature(e)));
            BoneOffsets.AddRange(props.GetProp<ArrayProperty<StructProperty>>("m_aFinalSkeleton").Select(e => new BoneOffset(e)));
        }

        private SkeletalMesh LoadMeshFromEntry(IEntry mOBaseHead)
        {
            if (mOBaseHead is null) return null;
            if (mOBaseHead.ClassName != "SkeletalMesh") throw new ArgumentException("Entry is not SkeletalMesh!");
            if (mOBaseHead is ExportEntry exp)
            {
                return ObjectBinary.From<SkeletalMesh>(exp);
            }
            else if (mOBaseHead is ImportEntry imp)
            {
                var resolveExp = EntryImporter.ResolveImport(imp, null);
                return ObjectBinary.From<SkeletalMesh>(resolveExp);
            }
            return null;
        }

        /// <summary>
        /// Applies the vertexes from the MorphFace onto the BaseHead SkeletalMesh
        /// </summary>
        /// <returns>The applied head skeletal mesh</returns>
        public SkeletalMesh Apply()
        {
            // apply vertices morph first
            // in skeletalMesh, we load only LOD0, so we only apply for lod0
            for (int lod = 0; lod < 1; lod++)
            {
                for (int v=0; v < BaseHead.LODModels[lod].VertexBufferGPUSkin.VertexData.Length; v++)
                {
                    var vertex = BaseHead.LODModels[lod].VertexBufferGPUSkin.VertexData[v];
                    vertex.Position.X = MorphFace.LODs[lod][v].X;
                    vertex.Position.Y = MorphFace.LODs[lod][v].Y;
                    vertex.Position.Z = MorphFace.LODs[lod][v].Z;
                    BaseHead.LODModels[lod].VertexBufferGPUSkin.VertexData[v] = vertex;
                }
            }

            // return mesh
            return BaseHead;
        }
    }

    public readonly struct MorphFeature
    {
        public string Name { get; }
        public float Offset { get; }

        public MorphFeature(StructProperty featureStruct)
        {
            Name = featureStruct.GetProp<NameProperty>("sFeatureName")?.Value ?? "";
            Offset = featureStruct.GetProp<FloatProperty>("Offset")?.Value ?? 0f;
        }
    }

    public readonly struct BoneOffset
    {
        public string Name { get; }
        public Vector3 Position { get; }

        public BoneOffset(StructProperty boneOffsetStruct)
        {
            Name = boneOffsetStruct.GetProp<NameProperty>("nName")?.Value ?? "";
            var vectorStruct = boneOffsetStruct.GetProp<StructProperty>("vPos");
            Position = CommonStructs.GetVector3(vectorStruct);
        }
    }
}