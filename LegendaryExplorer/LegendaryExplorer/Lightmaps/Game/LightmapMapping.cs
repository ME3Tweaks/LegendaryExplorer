using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>Mappings the baker can rewrite without allocating a new lightmap or changing cooked material shaders.</summary>
public static class LightmapMapping
{
    /// <summary>Reuse a shadow-only receiver's mapping kind and resolution without allocating a lightmap.
    /// Mixed vertex/texture policies and inconsistent layouts cannot be represented by a single solve.</summary>
    public static (int Width, int Height)? ShadowAllocation(LightmapRef receiver, StaticMeshRenderData lod, int coordinateIndex)
    {
        var shadows = PackageShadowMaps.Of(receiver);
        if (shadows.Count == 0) return null;
        if (shadows.All(s => s.PerVertex))
            return shadows.All(s => ObjectBinary.From<ShadowMap1D>(s.Export).Samples.Length == lod.PositionVertexBuffer.VertexData.Length)
                && lod.PositionVertexBuffer.VertexData.Length > 0 ? (0, 0) : null;
        if (shadows.Any(s => s.PerVertex) || coordinateIndex < 0 || coordinateIndex >= lod.VertexBuffer.NumTexCoords) return null;
        (int Width, int Height)? size = null;
        foreach (var shadow in shadows)
        {
            var props = shadow.Export.GetProperties();
            if (props.GetProp<ObjectProperty>("Texture") is not { Value: > 0 } texture
                || !receiver.Owner.FileRef.TryGetUExport(texture.Value, out var export)) return null;
            int width = export.GetProperty<IntProperty>("SizeX")?.Value ?? 0;
            int height = export.GetProperty<IntProperty>("SizeY")?.Value ?? 0;
            if (width <= 0 || height <= 0 || props.GetProp<StructProperty>("CoordinateScale") is not { } scaleProp
                || props.GetProp<StructProperty>("CoordinateBias") is not { } biasProp) return null;
            static Vector2 Vector(StructProperty p) => new(p.GetProp<FloatProperty>("X")?.Value ?? 0, p.GetProp<FloatProperty>("Y")?.Value ?? 0);
            var scale = Vector(scaleProp);
            var bias = Vector(biasProp);
            if (!float.IsFinite(scale.X + scale.Y + bias.X + bias.Y) || scale.X <= 0 || scale.Y <= 0
                || bias.X < 0 || bias.Y < 0 || bias.X + scale.X > 1.00001f || bias.Y + scale.Y > 1.00001f) return null;
            var rect = AtlasRect.FromCoordinates(scale, bias, width, height, padding: 1);
            if (rect.Width <= 2 || rect.Height <= 2 || size is { } previous && previous != (rect.Width, rect.Height)) return null;
            size = (rect.Width, rect.Height);
        }
        return size;
    }

    public static bool IsRebakeable(LightMap map) => map?.LightMapType is ELightMapType.LMT_1D or ELightMapType.LMT_2D
        or ELightMapType.LMT_3 or ELightMapType.LMT_4 or ELightMapType.LMT_6;

    public static int VertexSampleCount(LightMap map) => map switch
    {
        LightMap_1D m => m.DirectionalSamples?.Length ?? 0,
        LightMap_3 m => m.DirectionalSamples?.Length ?? 0,
        _ => -1
    };

    public static (int Width, int Height)? TextureAllocation(IMEPackage package, LightMap map)
    {
        var (texture, scale, bias) = map switch
        {
            LightMap_2D m => (m.Texture1, m.CoordinateScale, m.CoordinateBias),
            LightMap_4or6 m => (m.Texture1, m.CoordinateScale, m.CoordinateBias),
            _ => (0, default, default)
        };
        if (texture == 0 || !package.TryGetUExport(texture, out var export)) return null;
        var props = export.GetProperties();
        int width = props.GetProp<IntProperty>("SizeX")?.Value ?? 0, height = props.GetProp<IntProperty>("SizeY")?.Value ?? 0;
        var rect = AtlasRect.FromCoordinates(scale, bias, width, height, padding: 1);
        return (rect.Width, rect.Height);
    }

    public static bool IsCompatible(IMEPackage package, LightMap map, StaticMeshRenderData lod, int coordinateIndex) =>
        IsRebakeable(map) && FitsMesh(map, lod, coordinateIndex)
        && (VertexSampleCount(map) > 0 || TextureAllocation(package, map) is { Width: > 2, Height: > 2 });

    /// <summary>
    /// Whether the game can draw the lightmap on the mesh LOD, whatever its type: a vertex lightmap has a sample for each vertex, and a texture lightmap's
    /// coordinate channel is one the mesh has
    /// </summary>
    public static bool FitsMesh(LightMap map, StaticMeshRenderData lod, int coordinateIndex) => VertexSampleCount(map) is >= 0 and var samples
        ? samples == lod.PositionVertexBuffer.VertexData.Length
        : coordinateIndex >= 0 && coordinateIndex < lod.VertexBuffer.NumTexCoords;
}
