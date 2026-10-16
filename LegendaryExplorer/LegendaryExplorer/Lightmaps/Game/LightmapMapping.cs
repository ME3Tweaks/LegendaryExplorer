using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>Mappings the baker can rewrite without allocating a new lightmap or changing cooked material shaders.</summary>
public static class LightmapMapping
{
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

    public static bool IsCompatible(IMEPackage package, LightMap map, StaticMeshRenderData lod, int coordinateIndex)
    {
        if (!IsRebakeable(map)) return false;
        if (TextureAllocation(package, map) is { } size)
            return size.Width > 2 && size.Height > 2 && coordinateIndex >= 0 && coordinateIndex < lod.VertexBuffer.NumTexCoords;
        return VertexSampleCount(map) > 0 && VertexSampleCount(map) == lod.PositionVertexBuffer.VertexData.Length;
    }
}
