using System;
using System.Linq;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.UDK
{
    /// <summary>Separates component vertex paint from lighting exchanged with UDK.</summary>
    public static class StaticMeshComponentLightingData
    {
        /// <summary>Removes lighting references while preserving painted colors at their original LOD indices.</summary>
        public static StaticMeshComponent CreateUdkInput(StaticMeshComponent source)
        {
            var lods = source.LODData ?? Array.Empty<StaticMeshComponentLODInfo>();
            // Preserve the previous empty-binary behavior for components without vertex paint.
            if (!lods.Any(lod => lod.bLoadVertexColorData != 0 && lod.OverrideVertexColors != null))
                return StaticMeshComponent.Create();

            return new StaticMeshComponent
            {
                LODData = lods.Select(lod => new StaticMeshComponentLODInfo
                {
                    ShadowMaps = Array.Empty<int>(),
                    ShadowVertexBuffers = Array.Empty<int>(),
                    LightMap = new LightMap { LightMapType = ELightMapType.LMT_None },
                    bLoadVertexColorData = lod.bLoadVertexColorData,
                    OverrideVertexColors = lod.OverrideVertexColors
                }).ToArray()
            };
        }

        /// <summary>Installs baked lighting, retaining target paint even when an older UDK map lacks it.</summary>
        public static StaticMeshComponentLODInfo[] MergeLighting(StaticMeshComponentLODInfo[] target,
            StaticMeshComponentLODInfo[] baked)
        {
            target ??= Array.Empty<StaticMeshComponentLODInfo>();
            baked ??= Array.Empty<StaticMeshComponentLODInfo>();
            var result = new StaticMeshComponentLODInfo[Math.Max(target.Length, baked.Length)];
            for (int i = 0; i < result.Length; i++)
            {
                var original = i < target.Length ? target[i] : null;
                var lighting = i < baked.Length ? baked[i] : null;
                // An unmatched target LOD keeps its paint but has no lighting from this bake.
                result[i] = new StaticMeshComponentLODInfo
                {
                    ShadowMaps = lighting?.ShadowMaps ?? Array.Empty<int>(),
                    ShadowVertexBuffers = lighting?.ShadowVertexBuffers ?? Array.Empty<int>(),
                    LightMap = lighting?.LightMap ?? new LightMap { LightMapType = ELightMapType.LMT_None },
                    bLoadVertexColorData = original?.bLoadVertexColorData ?? lighting?.bLoadVertexColorData ?? 0,
                    OverrideVertexColors = original != null ? original.OverrideVertexColors : lighting?.OverrideVertexColors
                };
            }
            return result;
        }
    }
}
