using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Texture2D = LegendaryExplorerCore.Unreal.Classes.Texture2D;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>
/// One lightmap as stored on a component: a StaticMeshComponent LOD or a BSP (ModelComponent) element. Binary is the
/// component's parsed binary that LightMap belongs to; write it back with Owner.WriteBinary after changing the lightmap.
/// </summary>
public sealed record LightmapRef(ExportEntry Owner, string Slot, LightMap LightMap, ObjectBinary Binary = null)
{
    public string Describe() => $"{Owner.InstancedFullPath} {Slot}";
}

/// <summary>A texel rectangle of an atlas texture, including UE3's one-texel padding border.</summary>
public readonly record struct AtlasRect(int X0, int Y0, int X1, int Y1)
{
    public int Width => X1 - X0;
    public int Height => Y1 - Y0;

    /// <summary>
    /// UE3 FLightMap2D: CoordinateBias points at the first mapped texel and CoordinateScale spans the
    /// mapped size, with a one-texel border of dilated padding on each side (LMPT_NormalPadding).
    /// </summary>
    public static AtlasRect FromCoordinates(Vector2 scale, Vector2 bias, int width, int height, int padding = 1)
    {
        int x0 = (int)MathF.Round(bias.X * width) - padding, y0 = (int)MathF.Round(bias.Y * height) - padding;
        int x1 = (int)MathF.Round((bias.X + scale.X) * width) + padding, y1 = (int)MathF.Round((bias.Y + scale.Y) * height) + padding;
        return new AtlasRect(Math.Max(0, x0), Math.Max(0, y0), Math.Min(width, x1), Math.Min(height, y1));
    }

    public AtlasRect Inset(int n) => new(X0 + n, Y0 + n, Math.Max(X0 + n, X1 - n), Math.Max(Y0 + n, Y1 - n));

    /// <summary>
    /// Which allocation convention coordinates follow (LE3_Lightmaps.md §11.1): 1 = padded (bias = 4-aligned origin + 1,
    /// scale = size − 2; static meshes), 0 = unpadded (4-aligned bias = origin, scale = size: BSP surface groups and a
    /// few static meshes with sizes that are multiples of 4, terrain with odd sizes, (TrueSectionSize + 2·Expand)·Res + 1),
    /// −1 = neither.
    /// </summary>
    public static int Padding(Vector2 scale, Vector2 bias, int width, int height)
    {
        int bx = (int)MathF.Round(bias.X * width), by = (int)MathF.Round(bias.Y * height);
        int sx = (int)MathF.Round(scale.X * width), sy = (int)MathF.Round(scale.Y * height);
        if (bx % 4 == 1 && by % 4 == 1 && (sx + 2) % 4 == 0 && (sy + 2) % 4 == 0) return 1;
        if (bx % 4 == 0 && by % 4 == 0) return 0;
        return -1;
    }

    /// <summary>The allocation rectangle under its own convention (unknown conventions are treated as padded).</summary>
    public static AtlasRect ForAllocation(Vector2 scale, Vector2 bias, int width, int height) =>
        FromCoordinates(scale, bias, width, height, padding: Padding(scale, bias, width, height) == 0 ? 0 : 1);
}

/// <summary>A decoded top mip, 3 bytes (R,G,B) per texel, plus the stored bytes it came from.</summary>
public sealed record DecodedTexture(ExportEntry Export, string Format, int Width, int Height, byte[] Rgb, byte[] Stored);

public static class PackageLightmaps
{
    /// <summary>
    /// Every lightmap in the package: StaticMeshComponent and SplineMeshComponent LODs (LEC parses both with the
    /// StaticMeshComponent binary), BSP ModelComponent elements, and TerrainComponents. A game-wide census found no other
    /// component class that can carry one.
    /// </summary>
    public static IEnumerable<LightmapRef> Enumerate(IMEPackage package)
    {
        foreach (var export in package.Exports)
        {
            if (export.ClassName is "StaticMeshComponent" or "SplineMeshComponent")
            {
                var smc = ObjectBinary.From<StaticMeshComponent>(export);
                for (int i = 0; i < smc.LODData.Length; i++)
                    yield return new LightmapRef(export, $"LOD{i}", smc.LODData[i].LightMap, smc);
            }
            else if (export.ClassName == "ModelComponent")
            {
                var mc = ObjectBinary.From<ModelComponent>(export);
                for (int i = 0; i < mc.Elements.Length; i++)
                    yield return new LightmapRef(export, $"Element{i}", mc.Elements[i].LightMap, mc);
            }
            else if (export.ClassName == "TerrainComponent")
            {
                var tc = ObjectBinary.From<TerrainComponent>(export);
                yield return new LightmapRef(export, "Terrain", tc.LightMap, tc);
            }
        }
    }

    public static DecodedTexture ReadTopMip(IMEPackage package, int uIndex)
    {
        var export = package.GetUExport(uIndex);
        var texture = new Texture2D(export);
        var top = texture.GetTopMip();
        var data = texture.GetImageBytesForMip(top, package.Game, useLowerMipsIfTFCMissing: false, out _);
        string format = export.GetProperty<EnumProperty>("Format")?.Value.Name ?? "?";
        byte[] rgb = DecodeRgb(data, format, top.width, top.height)
            ?? throw new NotSupportedException($"{export.InstancedFullPath}: unsupported lightmap format {format}");
        return new DecodedTexture(export, format, top.width, top.height, rgb, data);
    }

    /// <summary>RGB bytes of a lightmap mip of any size, or null for a format lightmaps don't use.</summary>
    /// <exception cref="InvalidDataException">The data is too short for the mip.</exception>
    public static byte[] DecodeRgb(byte[] data, string format, int width, int height)
    {
        int blockBytes = format switch { "PF_DXT1" => 8, "PF_BC7" => 16, _ => 0 };
        if (format == "PF_A8R8G8B8")
        {
            RequireLength(data, width * height * 4, format, width, height);
            return BgraToRgb(data, width, width, height);
        }
        if (blockBytes == 0) return null;

        // Blocks cover the mip's texels rounded up to 4 on each side (a mip below 4x4 is one block): decode them all, keep the mip's
        int blocksX = (width + 3) / 4, blocksY = (height + 3) / 4;
        int length = blocksX * blocksY * blockBytes;
        RequireLength(data, length, format, width, height);
        int paddedWidth = blocksX * 4, paddedHeight = blocksY * 4;
        // Decoded as PF_A8R8G8B8 is stored: B, G, R, A
        var bgra = LegendaryExplorerCore.Textures.Image.convertRawToARGB(data.Length == length ? data : data[..length], ref paddedWidth, ref paddedHeight,
            LegendaryExplorerCore.Textures.Image.getPixelFormatType(format));
        return BgraToRgb(bgra, paddedWidth, width, height);
    }

    static void RequireLength(byte[] data, int length, string format, int width, int height)
    {
        if (data == null || data.Length < length)
            throw new InvalidDataException($"A {width}x{height} {format} mip needs {length} bytes, but has {data?.Length ?? 0}.");
    }

    /// <summary>The top-left width x height texels of a BGRA image whose rows are <paramref name="stride"/> texels long, as RGB.</summary>
    /// <remarks>PF_A8R8G8B8 is D3DFMT_A8R8G8B8: bytes B, G, R, A in memory.</remarks>
    static byte[] BgraToRgb(byte[] bgra, int stride, int width, int height)
    {
        var rgb = new byte[width * height * 3];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int s = (y * stride + x) * 4, d = (y * width + x) * 3;
            rgb[d] = bgra[s + 2];
            rgb[d + 1] = bgra[s + 1];
            rgb[d + 2] = bgra[s];
        }
        return rgb;
    }
}
