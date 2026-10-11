using System;
using System.Collections.Generic;
using System.Linq;
namespace LegendaryExplorer.Lightmaps.Codec;

/// <summary>One mip level of a lightmap atlas before block compression: RGB bytes plus per-texel coverage (0 = unmapped).</summary>
public sealed record MipLevel(int Width, int Height, byte[] Rgb, byte[] Coverage)
{
    public static MipLevel Create(int width, int height) => new(width, height, new byte[width * height * 3], new byte[width * height]);
}

/// <summary>How texels are averaged into the next mip.</summary>
public enum MipAveraging
{
    /// <summary>
    /// Stock UE3 (IB UnLightMap.cpp:938): bytes decoded through FLinearColor's pow(x, 2.2) table, weighted by coverage,
    /// averaged, and re-encoded by FColor(FLinearColor) as trunc(pow(x, 1/2.2) * 255).
    /// </summary>
    Linear,
    /// <summary>Coverage-weighted average of the raw bytes, rounded. A candidate for linear-stored atlases.</summary>
    Bytes,
}

/// <summary>How unmapped texels are filled so bilinear filtering doesn't pull in black.</summary>
public enum Dilation
{
    /// <summary>
    /// Stock UE3 (IB UnLightMap.cpp:996): one in-place raster pass; each unmapped texel takes the weighted average of
    /// mapped neighbours (255 for edges, 1 for corners). Its loop bounds are [d - 1, d + 1), so it only ever reads the
    /// texel above, the texel to the left and the diagonal between them: dilation spreads right and down by one texel.
    /// </summary>
    Ue3,
    /// <summary>All eight neighbours, as the stock code evidently intended.</summary>
    Full,
    None,
}

/// <summary>Lightmap mip-chain generation and dilation, following stock UE3's FLightMapPendingTexture::StartEncoding.</summary>
public static class MipChain
{
    static readonly float[] PowTable = Enumerable.Range(0, 256).Select(i => MathF.Pow(i / 255f, 2.2f)).ToArray();

    /// <summary>Smallest mip edge for a pixel format: one block (4) for DXT1, 1 for PF_A8R8G8B8 (the Dir atlas goes down to 1x1).</summary>
    public static int MinMipSize(string pixelFormat) => pixelFormat == "PF_DXT1" ? 4 : 1;

    /// <summary>UE3: max(ceil(log2(SizeX)), ceil(log2(SizeY))) + 1 levels, each at least one block of the format (see MinMipSize).</summary>
    public static int MipCount(int width, int height) => Math.Max(CeilLog2(width), CeilLog2(height)) + 1;

    public static (int Width, int Height) MipSize(int width, int height, int mip, int minSize = 4) =>
        (Math.Max(minSize, width >> mip), Math.Max(minSize, height >> mip));

    /// <summary>Builds the full chain from an undilated top mip; the returned levels are undilated too (dilate each afterwards, as UE3 does).</summary>
    public static List<MipLevel> Build(MipLevel top, MipAveraging averaging, int minSize = 4)
    {
        var levels = new List<MipLevel> { top };
        int count = MipCount(top.Width, top.Height);
        for (int mip = 1; mip < count; mip++)
        {
            var src = levels[^1];
            var (dw, dh) = MipSize(top.Width, top.Height, mip, minSize);
            var dst = MipLevel.Create(dw, dh);
            int fx = src.Width / dw, fy = src.Height / dh;
            for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
            {
                double r = 0, g = 0, b = 0; int coverage = 0;
                for (int sy = y * fy; sy < (y + 1) * fy; sy++)
                for (int sx = x * fx; sx < (x + 1) * fx; sx++)
                {
                    int si = sy * src.Width + sx;
                    int a = src.Coverage[si];
                    if (a == 0) continue;
                    Accumulate(src.Rgb, si * 3, a, averaging, ref r, ref g, ref b);
                    coverage += a;
                }
                if (coverage == 0) continue;
                int di = y * dw + x;
                Store(dst.Rgb, di * 3, r / coverage, g / coverage, b / coverage, averaging);
                dst.Coverage[di] = (byte)(coverage / (fx * fy));
            }
            levels.Add(dst);
        }
        return levels;
    }

    /// <summary>
    /// Builds a complete chain ready for block compression: coverage-weighted mips from the undilated top level, then
    /// each level dilated. For new bakes: <see cref="Dilation.Full"/> with a few rings, which keeps bilinear filtering and
    /// DXT1 blocks at chart edges from pulling in black (the originals' top mips are not dilated at all; LE3_Lightmaps.md §6.7).
    /// </summary>
    public static List<MipLevel> BuildDilated(MipLevel top, MipAveraging averaging, Dilation dilation = Dilation.Full, int rings = 4, int minSize = 4)
    {
        var levels = Build(top, averaging, minSize)
            .Select(l => new MipLevel(l.Width, l.Height, (byte[])l.Rgb.Clone(), (byte[])l.Coverage.Clone())).ToList();
        foreach (var level in levels)
        {
            if (dilation == Dilation.Full) DilateRings(level, rings, averaging);
            else Dilate(level, dilation, averaging);
        }
        return levels;
    }

    /// <summary>Repeated 8-neighbour dilation: each ring fills texels bordering the mapped-or-filled area.</summary>
    public static void DilateRings(MipLevel level, int rings, MipAveraging averaging)
    {
        int w = level.Width, h = level.Height;
        // Filled texels act as sources for later rings, with coverage 1 so mapped texels dominate where both are adjacent.
        var source = (byte[])level.Coverage.Clone();
        for (int ring = 0; ring < rings; ring++)
        {
            var filled = new List<int>();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int di = y * w + x;
                if (source[di] != 0) continue;
                double r = 0, g = 0, b = 0; long coverage = 0;
                for (int sy = Math.Max(y - 1, 0); sy < Math.Min(y + 2, h); sy++)
                for (int sx = Math.Max(x - 1, 0); sx < Math.Min(x + 2, w); sx++)
                {
                    int si = sy * w + sx;
                    int a = source[si];
                    if (a == 0) continue;
                    int weight = (sx != x && sy != y) ? 1 : 255;
                    Accumulate(level.Rgb, si * 3, a * weight, averaging, ref r, ref g, ref b);
                    coverage += a * weight;
                }
                if (coverage == 0) continue;
                Store(level.Rgb, di * 3, r / coverage, g / coverage, b / coverage, averaging);
                filled.Add(di);
            }
            if (filled.Count == 0) break;
            foreach (int i in filled) source[i] = 1;
        }
    }

    /// <summary>DXT1 blocks for every level (colour atlases).</summary>
    public static List<byte[]> EncodeDxt1(List<MipLevel> levels) => levels.Select(l => Dxt1.Encode(l.Rgb, l.Width, l.Height)).ToList();

    /// <summary>
    /// PF_A8R8G8B8 bytes (B, G, R, A in memory) for every level: the LMT_4 direction atlas. Its alpha is unused by the
    /// shader and is 0 in the originals.
    /// </summary>
    public static List<byte[]> EncodeBgra(List<MipLevel> levels, byte alpha = 0) => levels.Select(l =>
    {
        var bgra = new byte[l.Width * l.Height * 4];
        for (int i = 0; i < l.Width * l.Height; i++)
        {
            bgra[i * 4] = l.Rgb[i * 3 + 2]; bgra[i * 4 + 1] = l.Rgb[i * 3 + 1]; bgra[i * 4 + 2] = l.Rgb[i * 3]; bgra[i * 4 + 3] = alpha;
        }
        return bgra;
    }).ToList();

    /// <summary>Fills unmapped texels (coverage 0) from mapped neighbours, in place, one ring. Their coverage stays 0.</summary>
    public static void Dilate(MipLevel level, Dilation mode, MipAveraging averaging)
    {
        if (mode == Dilation.None) return;
        int w = level.Width, h = level.Height;
        // Full dilation reads a snapshot so filled texels don't feed each other; UE3's pass is in place, but it only
        // reads texels with nonzero coverage, which filled ones never get, so the result is the same either way.
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int di = y * w + x;
            if (level.Coverage[di] != 0) continue;
            double r = 0, g = 0, b = 0; long coverage = 0;
            int y0 = Math.Max(y - 1, 0), y1 = mode == Dilation.Ue3 ? Math.Min(y + 1, h) : Math.Min(y + 2, h);
            int x0 = Math.Max(x - 1, 0), x1 = mode == Dilation.Ue3 ? Math.Min(x + 1, w) : Math.Min(x + 2, w);
            for (int sy = y0; sy < y1; sy++)
            for (int sx = x0; sx < x1; sx++)
            {
                int si = sy * w + sx;
                int a = level.Coverage[si];
                if (a == 0) continue;
                int weight = (sx != x && sy != y) ? 1 : 255;
                Accumulate(level.Rgb, si * 3, a * weight, averaging, ref r, ref g, ref b);
                coverage += a * weight;
            }
            if (coverage == 0) continue;
            Store(level.Rgb, di * 3, r / coverage, g / coverage, b / coverage, averaging);
        }
    }

    static void Accumulate(byte[] rgb, int o, int weight, MipAveraging averaging, ref double r, ref double g, ref double b)
    {
        if (averaging == MipAveraging.Linear)
        {
            r += PowTable[rgb[o]] * (double)weight; g += PowTable[rgb[o + 1]] * (double)weight; b += PowTable[rgb[o + 2]] * (double)weight;
        }
        else
        {
            r += rgb[o] * (double)weight; g += rgb[o + 1] * (double)weight; b += rgb[o + 2] * (double)weight;
        }
    }

    static void Store(byte[] rgb, int o, double r, double g, double b, MipAveraging averaging)
    {
        if (averaging == MipAveraging.Linear)
        {
            // FColor(FLinearColor): trunc(pow(x, 1/2.2) * 255), clamped.
            rgb[o] = ToGammaByte(r); rgb[o + 1] = ToGammaByte(g); rgb[o + 2] = ToGammaByte(b);
        }
        else
        {
            rgb[o] = (byte)Math.Clamp((int)Math.Round(r), 0, 255); rgb[o + 1] = (byte)Math.Clamp((int)Math.Round(g), 0, 255); rgb[o + 2] = (byte)Math.Clamp((int)Math.Round(b), 0, 255);
        }
    }

    static byte ToGammaByte(double x) => (byte)Math.Clamp((int)(MathF.Pow((float)Math.Clamp(x, 0, 1), 1f / 2.2f) * 255f), 0, 255);

    static int CeilLog2(int x)
    {
        int n = 0;
        while ((1 << n) < x) n++;
        return n;
    }
}
