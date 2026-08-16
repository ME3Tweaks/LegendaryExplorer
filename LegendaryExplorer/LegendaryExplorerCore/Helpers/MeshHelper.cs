using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendaryExplorerCore.Helpers
{
    public static class MeshHelper
    {
        private static readonly float[] DisplayFactors = [1.0f, 0.25f, 0.1f];

        public static ArrayProperty<StructProperty> GetLodInfoForSkeletalMesh(SkeletalMesh mesh, MEGame game)
        {
            ArrayProperty<StructProperty> lodInfoProp = new("LODInfo");

            for (int i = 0; i < mesh.LODModels.Length; i++)
            {
                var currentLod = mesh.LODModels[i];
                var displayFactorProp = new FloatProperty(DisplayFactors[Math.Min(i, DisplayFactors.Length - 1)], "DisplayFactor");
                var bEnableShadowCastingProp = new ArrayProperty<BoolProperty>(Enumerable.Repeat(new BoolProperty(true), mesh.Materials.Length), "bEnableShadowCasting");
                var TriangleSortingProp = new ArrayProperty<EnumProperty>(Enumerable.Repeat(new EnumProperty("TRISORT_None", "TriangleSortOption", game), mesh.Materials.Length), "TriangleSorting");

                var LODMaterialMapProp = new ArrayProperty<IntProperty>(Enumerable.Range(0, mesh.Materials.Length).Select(x => new IntProperty(x)), "LODMaterialMap");

                var lodInfo = new StructProperty("SkeletalMeshLODInfo", false,
                    displayFactorProp,
                    new FloatProperty(0.2f, "LODHysteresis"),
                    LODMaterialMapProp,
                    bEnableShadowCastingProp,
                    TriangleSortingProp);
                lodInfoProp.Add(lodInfo);
            }
            return lodInfoProp;
        }

        // vanilla meshes where a lower LOD drops a material remap them to start at 0. this is annoying and hard to work with. I am going to un map them so they all use the normal indices. It works fine in game
        public static SkeletalMesh UnMapMaterials(ExportEntry meshEntry)
        {
            var lodInfoProp = meshEntry.GetProperty<ArrayProperty<StructProperty>>("LODInfo");
            
            var meshBin = meshEntry.GetBinaryData<SkeletalMesh>();
            if (lodInfoProp == null)
            {
                return meshBin;
            }
            List<(int, int[])> LODsToFix = [];
            for (int i = 1; i < lodInfoProp.Count; i++)
            {
                var lodInfo = lodInfoProp[i];
                var materialMap = lodInfo.GetProp<ArrayProperty<IntProperty>>("LODMaterialMap");
                for (int j = 0; j < materialMap.Count; j++)
                {
                    if (materialMap[j] != j)
                    {
                        LODsToFix.Add((i, materialMap.Select(x => x.Value).ToArray()));
                        break;
                    }
                }
            }
            if (LODsToFix.Count > 0)
            {
                
                foreach (var (lodIndex, mapping) in LODsToFix)
                {
                    var lod = meshBin.LODModels[lodIndex];
                    foreach (var section in lod.Sections)
                    {
                        section.MaterialIndex = (ushort)mapping[section.MaterialIndex];
                    }
                }
                meshEntry.WriteBinary(meshBin);
            }
            return meshBin;
        }
    }
}
