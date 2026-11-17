using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>
/// UE3's FTextureLayout (IB UnTextureLayout.h): a binary-tree (guillotine) packer. Element sizes are rounded up to a
/// multiple of 4 (DXT1 blocks); the texture starts at one block and grows in powers of two, and placement first tries
/// not to grow it.
/// </summary>
public sealed class TextureLayout
{
    struct Node
    {
        public int ChildA, ChildB, MinX, MinY, SizeX, SizeY;
        public bool Used;
    }

    readonly List<Node> nodes = [];
    readonly bool powerOfTwo, alignByFour;

    public int SizeX { get; private set; }
    public int SizeY { get; private set; }

    public TextureLayout(int minSizeX, int minSizeY, int maxSizeX, int maxSizeY, bool powerOfTwoSize = true, bool alignByFour = true)
    {
        SizeX = minSizeX; SizeY = minSizeY;
        powerOfTwo = powerOfTwoSize; this.alignByFour = alignByFour;
        nodes.Add(new Node { ChildA = -1, ChildB = -1, SizeX = maxSizeX, SizeY = maxSizeY });
    }

    public bool AddElement(int sizeX, int sizeY, out int baseX, out int baseY)
    {
        baseX = baseY = 0;
        if (sizeX == 0 || sizeY == 0) return true;
        if (alignByFour) { sizeX = (sizeX + 3) & ~3; sizeY = (sizeY + 3) & ~3; }
        int index = AddSurfaceInner(0, sizeX, sizeY, allowEnlargement: false);
        if (index < 0) index = AddSurfaceInner(0, sizeX, sizeY, allowEnlargement: true);
        if (index < 0) return false;
        var node = nodes[index];
        node.Used = true;
        nodes[index] = node;
        baseX = node.MinX; baseY = node.MinY;
        SizeX = Math.Max(SizeX, powerOfTwo ? RoundUpToPowerOfTwo(node.MinX + sizeX) : node.MinX + sizeX);
        SizeY = Math.Max(SizeY, powerOfTwo ? RoundUpToPowerOfTwo(node.MinY + sizeY) : node.MinY + sizeY);
        return true;
    }

    int AddSurfaceInner(int index, int sizeX, int sizeY, bool allowEnlargement)
    {
        var node = nodes[index];
        if (node.ChildA >= 0)
        {
            int result = AddSurfaceInner(node.ChildA, sizeX, sizeY, allowEnlargement);
            return result >= 0 ? result : AddSurfaceInner(node.ChildB, sizeX, sizeY, allowEnlargement);
        }
        if (node.Used || node.SizeX < sizeX || node.SizeY < sizeY) return -1;
        if (!allowEnlargement && (node.MinX + sizeX > SizeX || node.MinY + sizeY > SizeY)) return -1;
        if (node.SizeX == sizeX && node.SizeY == sizeY) return index;

        int excessWidth = node.SizeX - sizeX, excessHeight = node.SizeY - sizeY;
        var a = new Node { ChildA = -1, ChildB = -1, MinX = node.MinX, MinY = node.MinY };
        var b = new Node { ChildA = -1, ChildB = -1 };
        if (excessWidth > excessHeight)
        {
            a.SizeX = sizeX; a.SizeY = node.SizeY;
            b.MinX = node.MinX + sizeX; b.MinY = node.MinY; b.SizeX = node.SizeX - sizeX; b.SizeY = node.SizeY;
        }
        else
        {
            a.SizeX = node.SizeX; a.SizeY = sizeY;
            b.MinX = node.MinX; b.MinY = node.MinY + sizeY; b.SizeX = node.SizeX; b.SizeY = node.SizeY - sizeY;
        }
        node.ChildA = nodes.Count; nodes.Add(a);
        node.ChildB = nodes.Count; nodes.Add(b);
        nodes[index] = node;
        return AddSurfaceInner(node.ChildA, sizeX, sizeY, allowEnlargement);
    }

    static int RoundUpToPowerOfTwo(int x)
    {
        int p = 1;
        while (p < x) p <<= 1;
        return p;
    }
}

/// <summary>A lightmap that needs space in an atlas: its interior (mapped) resolution and what it may share a texture with.</summary>
/// <param name="Key">Caller's identifier (e.g. the component and LOD).</param>
/// <param name="Group">Only requests with the same group share textures: the lightmap type (LMT_2D, LMT_4, LMT_6), since each type has its own texture set.</param>
/// <param name="InteriorX">Mapped texels across; the mesh's lightmap UV 0..1 covers exactly these.</param>
/// <param name="Streamed">UE3 LMF_Streamed: streamed (TFC) and package-stored lightmaps never share a texture.</param>
/// <param name="Bounds">World bounding sphere, for the streaming distance rule; null skips it.</param>
/// <param name="Padding">1 for LE3's padded convention (static meshes), 0 for unpadded allocations (BSP surface groups,
/// whose padding lives around each surface inside the group).</param>
public sealed record AtlasRequest(object Key, string Group, int InteriorX, int InteriorY, bool Streamed = true, (Vector3 Center, float Radius)? Bounds = null, int Padding = 1);

/// <summary>Where a request landed: its atlas, padded rectangle, and the CoordinateScale/Bias to serialize.</summary>
public sealed record AtlasPlacement(AtlasRequest Request, int AtlasIndex, int OffsetX, int OffsetY, int PaddedX, int PaddedY)
{
    int Padding => Request.Padding;

    /// <summary>LE3 convention: bias = rectangle origin + padding, scale = interior size (padded: size − 2; unpadded: size).</summary>
    public (Vector2 Scale, Vector2 Bias) Coordinates(int textureSizeX, int textureSizeY) => (
        new Vector2(Request.InteriorX / (float)textureSizeX, Request.InteriorY / (float)textureSizeY),
        new Vector2((OffsetX + Padding) / (float)textureSizeX, (OffsetY + Padding) / (float)textureSizeY));

    /// <summary>
    /// The mesh lightmap UV that texel (x, y) of this placement's padded rectangle samples, in an atlas stored at
    /// 1/<paramref name="divisor"/> resolution (2 for LMT_4's direction atlas). The shader maps UV u to atlas texel
    /// coordinate u * InteriorX + OffsetX + 1 (in full-resolution texels), so a texel centre maps back as below; padding
    /// texels get UVs just outside 0..1.
    /// </summary>
    public Vector2 TexelToUV(int x, int y, int divisor = 1) => new(
        ((x + 0.5f) * divisor - Padding) / Request.InteriorX,
        ((y + 0.5f) * divisor - Padding) / Request.InteriorY);

    /// <summary>Padded rectangle size in an atlas stored at 1/<paramref name="divisor"/> resolution.</summary>
    public (int X, int Y) PaddedSize(int divisor = 1) => (PaddedX / divisor, PaddedY / divisor);
}

public sealed record Atlas(string Group, bool Streamed, int SizeX, int SizeY, List<AtlasPlacement> Placements)
{
    /// <summary>
    /// Builds a texture's top mip for this atlas at 1/<paramref name="divisor"/> resolution: each placement's padded
    /// rectangle of texels (with coverage; 0 on the padding ring and chart gaps) comes from <paramref name="texels"/>,
    /// which receives the placement and must return a level of <see cref="AtlasPlacement.PaddedSize"/>. Feed the result
    /// to MipChain.BuildDilated.
    /// </summary>
    public Codec.MipLevel Compose(Func<AtlasPlacement, Codec.MipLevel> texels, int divisor = 1)
    {
        var atlas = Codec.MipLevel.Create(Math.Max(1, SizeX / divisor), Math.Max(1, SizeY / divisor));
        foreach (var p in Placements)
        {
            var src = texels(p);
            var (w, h) = p.PaddedSize(divisor);
            if (src.Width != w || src.Height != h)
                throw new ArgumentException($"placement {p.Request.Key}: texels are {src.Width}x{src.Height}, expected {w}x{h}");
            int ox = p.OffsetX / divisor, oy = p.OffsetY / divisor;
            for (int y = 0; y < h; y++)
            {
                Array.Copy(src.Rgb, y * w * 3, atlas.Rgb, ((oy + y) * atlas.Width + ox) * 3, w * 3);
                Array.Copy(src.Coverage, y * w, atlas.Coverage, (oy + y) * atlas.Width + ox, w);
            }
        }
        return atlas;
    }
}

/// <summary>
/// Packs lightmaps into atlases the way stock UE3's FLightMap2D::EncodeTextures does: largest first, first texture that
/// accepts it, otherwise a new one. A texture accepts a lightmap of its own group and streaming flag, if a streamed
/// texture's bounding sphere stays within MaxRadius (GMaxLightmapRadius, 5000), and if the layout has room within
/// MaxSize (PackedLightAndShadowMapTextureSize; LE3's atlases never exceed 1024).
/// </summary>
public static class AtlasPacker
{
    /// <summary>
    /// Interior resolution for a requested one: LE3's padded lightmaps are always 4k - 2 texels across (interior plus a
    /// 1-texel border fills whole DXT blocks), e.g. 30, 62, 126, 254.
    /// </summary>
    public static int InteriorSize(int requested) => Math.Max(2, ((requested + 2 + 3) & ~3) - 2);

    public static List<Atlas> Pack(IEnumerable<AtlasRequest> requests, int maxSize = 1024, float maxRadius = 5000f)
    {
        var atlases = new List<(Atlas Atlas, TextureLayout Layout, (Vector3 Center, float Radius)? Bounds)>();
        // Stable largest-first order (UE3 sorts by max(TotalSizeX, TotalSizeY) descending).
        var ordered = requests.Select((r, i) => (r, i))
            .OrderByDescending(x => Math.Max(x.r.InteriorX, x.r.InteriorY) + 2 * x.r.Padding).ThenBy(x => x.i).Select(x => x.r);

        foreach (var request in ordered)
        {
            int paddedX = request.InteriorX + 2 * request.Padding, paddedY = request.InteriorY + 2 * request.Padding;
            bool placed = false;
            for (int i = 0; i < atlases.Count && !placed; i++)
            {
                var (atlas, layout, bounds) = atlases[i];
                if (atlas.Group != request.Group || atlas.Streamed != request.Streamed) continue;
                var merged = Merge(bounds, request.Bounds);
                if (request.Streamed && merged is { } m && bounds is { } b && m.Radius > maxRadius && m.Radius > b.Radius + 1e-4f) continue;
                if (!layout.AddElement(paddedX, paddedY, out int x, out int y)) continue;
                atlas.Placements.Add(new AtlasPlacement(request, i, x, y, paddedX, paddedY));
                atlases[i] = (atlas with { SizeX = layout.SizeX, SizeY = layout.SizeY }, layout, merged);
                placed = true;
            }
            if (placed) continue;

            int size = maxSize;
            int texX = paddedX > size ? RoundUp(paddedX) : size, texY = paddedY > size ? RoundUp(paddedY) : size;
            var newLayout = new TextureLayout(4, 4, texX, texY);
            if (!newLayout.AddElement(paddedX, paddedY, out int nx, out int ny))
                throw new InvalidOperationException($"lightmap {request.Key} ({paddedX}x{paddedY}) doesn't fit a {texX}x{texY} texture");
            var newAtlas = new Atlas(request.Group, request.Streamed, newLayout.SizeX, newLayout.SizeY, []);
            newAtlas.Placements.Add(new AtlasPlacement(request, atlases.Count, nx, ny, paddedX, paddedY));
            atlases.Add((newAtlas, newLayout, request.Bounds));
        }
        // Final sizes: the layout only grows, so re-read them after all placements.
        return atlases.Select(a => a.Atlas with { SizeX = a.Layout.SizeX, SizeY = a.Layout.SizeY }).ToList();
    }

    /// <summary>Smallest sphere containing both (UE3 FBoxSphereBounds addition is box-based; this is close enough for the rule).</summary>
    static (Vector3 Center, float Radius)? Merge((Vector3 Center, float Radius)? a, (Vector3 Center, float Radius)? b)
    {
        if (a is not { } x) return b;
        if (b is not { } y) return a;
        float d = Vector3.Distance(x.Center, y.Center);
        if (d + y.Radius <= x.Radius) return x;
        if (d + x.Radius <= y.Radius) return y;
        float r = (d + x.Radius + y.Radius) / 2;
        var c = d > 0 ? x.Center + (y.Center - x.Center) * ((r - x.Radius) / d) : x.Center;
        return (c, r);
    }

    static int RoundUp(int x)
    {
        int p = 1;
        while (p < x) p <<= 1;
        return p;
    }
}
