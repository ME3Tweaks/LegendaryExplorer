using System;
using System.Numerics;
using LegendaryExplorer.Lightmaps.Codec;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>
/// New lighting for <see cref="LightmapRewriter"/>: a UE5 bake (Helios), or a Lightmass bake run from LEX. Every method returns
/// null for a lightmap the source doesn't cover, which then keeps its original lighting.
/// </summary>
public interface ILightingSource
{
    /// <summary>The package lightmap GUIDs contributing to a baked mapping; null preserves its existing list.</summary>
    Guid[] LightGuids(LightmapRef r);

    /// <summary>
    /// A lookup from mesh lightmap UV (texture lightmaps: the shader's coordinate is uv * CoordinateScale + CoordinateBias) to
    /// world-space L1 lighting and the LE3 tangent frame at that UV.
    /// </summary>
    Func<Vector2, (Lighting Lighting, TangentFrame Frame)> For(LightmapRef r);

    /// <summary>
    /// For a per-vertex lightmap (LMT_1D, LMT_3): lighting and tangent frame per vertex of the lightmap's LOD, indexed like the
    /// mesh's vertices, which is how LE3 orders a vertex lightmap's samples.
    /// </summary>
    (Lighting Lighting, TangentFrame Frame)[] ForVertices(LightmapRef r);
}
