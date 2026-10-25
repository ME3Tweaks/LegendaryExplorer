using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Lightmaps.Codec;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>
/// Re-encodes a level's static/spline-mesh lightmap atlases through this tool's codecs (per-allocation scales/ranges,
/// gamma bytes, dilated mip chains, DXT1, a mod TFC) and patches the components.
///
/// In place: same layout, same texture exports. Repack: every allocation is placed anew by AtlasPacker, new
/// LightMapTexture2D exports are cloned from existing ones, components are pointed at them, the level's texture
/// streaming map is updated and the old textures are trashed. Either way the lighting comes from the original atlases
/// here (an end-to-end test of the write path); a rebake swaps in its own lighting per texel.
///
/// BSP surface groups are unpadded allocations shared by several ModelComponent elements; each is encoded once and every
/// element sharing it is updated. Terrain lightmaps are unpadded too (odd sizes). Atlases with an allocation that follows
/// neither convention are left untouched.
/// </summary>
public sealed class LightmapRewriter(IMEPackage package, TfcWriter tfc, ByteEncoding textureEncoding, ILightingSource bake = null, float vectorBrightness = 1f)
{
    public sealed class Summary
    {
        public int TextureSets, Textures, Allocations, SharedLightmaps, SkippedSets, NewAtlases, TrashedTextures;
        /// <summary>Atlases left as they were because the bake has nothing for any of their allocations.</summary>
        public int UnbakedSets;
        /// <summary>DXT1 loss on the written colour atlases: squared byte error summed over mapped top-mip texel channels, and the count.</summary>
        public double DxtSquaredError; public long DxtSamples;
        public List<string> Notes = [];
        /// <summary>Per-vertex lightmaps (LMT_1D, LMT_3) written from the bake, and those whose sample count isn't the mesh's vertex count.</summary>
        public int VertexLightmaps, VertexCountMismatches;
        public int AllocatedLightmaps, SkippedNewMappings;
        /// <summary>Targets whose baked lighting was applied, keyed independently of their parsed component binaries.</summary>
        public HashSet<(ExportEntry Owner, string Slot)> AppliedLightmaps { get; } = [];
        /// <summary>New mappings solved without any direct lighting in an LE1/LE2 lightmap: they get only their shadow maps (LightmassMappingAllocator.KeepsNewLightmap).</summary>
        public HashSet<(ExportEntry Owner, string Slot)> ShadowOnlyAllocations { get; } = [];
        /// <summary>
        /// A check on vertex order: per written vertex lightmap (8+ vertices lit in both), the correlation of log brightness at
        /// the vertex normal between the new and the original samples, and the same with the new samples shifted by half the
        /// vertex count as a control. Right order: clearly positive, the control near zero.
        /// </summary>
        public List<(float R, int Lit)> VertexCorrelations = [], VertexShiftedCorrelations = [];
    }

    readonly Dictionary<int, DecodedTexture> cache = new();
    DecodedTexture Read(int uIndex) => cache.TryGetValue(uIndex, out var t) ? t : cache[uIndex] = PackageLightmaps.ReadTopMip(package, uIndex);

    /// <summary>A destination texel: its index in the atlas being built, its value, and whether it is mapped (coverage).</summary>
    readonly record struct Texel<T>(int Dest, T Value, bool Mapped);

    public Summary RewriteAll(bool repack)
    {
        var summary = new Summary();
        var refs = PackageLightmaps.Enumerate(package).ToList();
        var eligible = new List<List<Allocation>>();
        foreach (var group in refs.Where(r => r.LightMap is LightMap_2D or LightMap_4or6).GroupBy(r => TextureKey(r.LightMap)))
        {
            var list = group.ToList();
            // Every lightmap carrier (static/spline meshes, BSP, terrain).
            if (list.Any(r => r.Owner.ClassName is not ("StaticMeshComponent" or "SplineMeshComponent" or "ModelComponent" or "TerrainComponent"))) { summary.SkippedSets++; continue; }
            var allocations = Allocations(list);
            if (allocations.Any(a => a.Padding < 0))
            {
                summary.SkippedSets++;
                summary.Notes.Add($"{Read(TextureKey(list[0].LightMap)).Export.ObjectName.Instanced}: an allocation follows neither convention; left untouched");
                continue;
            }
            // Re-encoding an atlas the bake doesn't touch would only add DXT1 loss (and, streamed, another copy in the TFC).
            if (bake != null && !allocations.Any(a => Baked(a.Refs[0]) != null)) { summary.UnbakedSets++; continue; }
            eligible.Add(allocations);
        }
        summary.TextureSets = eligible.Count;
        summary.Allocations = eligible.Sum(l => l.Count);
        summary.SharedLightmaps = eligible.Sum(l => l.Sum(a => a.Refs.Count - 1));

        if (repack) Repack(eligible, summary);
        else foreach (var list in eligible) RewriteInPlace(list, summary);

        if (bake != null) RewriteVertexLightmaps(refs, summary);

        // Each LightMap belongs to its component's parsed binary; write every component back once.
        foreach (var r in refs.GroupBy(r => r.Owner).Select(g => g.First()))
            r.Owner.WriteBinary(r.Binary);
        return summary;
    }

    static int TextureKey(LightMap lm) => lm switch { LightMap_2D d => d.Texture1, LightMap_4or6 v => v.Texture1, _ => 0 };

    /// <summary>
    /// One atlas allocation and every lightmap that uses it. BSP surface groups share one allocation (and identical
    /// lightmap data) among all the ModelComponent elements with nodes in the group; it is encoded once and each
    /// element's LightMap is updated. Padding is the allocation's convention (AtlasRect.Padding).
    /// </summary>
    sealed record Allocation(List<LightmapRef> Refs, LightMap First, int Padding);

    List<Allocation> Allocations(List<LightmapRef> list) => list
        .GroupBy(r => r.LightMap switch
        {
            LightMap_2D d => (d.CoordinateScale, d.CoordinateBias),
            LightMap_4or6 v => (v.CoordinateScale, v.CoordinateBias),
            _ => default,
        })
        .Select(g =>
        {
            var first = g.First().LightMap;
            var (scale, bias, tex) = first switch
            {
                LightMap_2D d => (d.CoordinateScale, d.CoordinateBias, d.Texture1),
                LightMap_4or6 v => (v.CoordinateScale, v.CoordinateBias, v.Texture1),
                _ => default,
            };
            var t = Read(tex);
            return new Allocation(g.ToList(), first, AtlasRect.Padding(scale, bias, t.Width, t.Height));
        })
        .ToList();

    readonly Dictionary<LightmapRef, Func<Vector2, (Lighting Lighting, TangentFrame Frame)>> lookups = new();

    /// <summary>The bake's UV lookup for an allocation (its first lightmap), or null to keep the original lighting.</summary>
    Func<Vector2, (Lighting Lighting, TangentFrame Frame)> Baked(LightmapRef first)
    {
        if (bake == null) return null;
        if (!lookups.TryGetValue(first, out var f)) lookups[first] = f = bake.For(first);
        return f;
    }

    void RecordAppliedLightmap(LightmapRef target, LightmapRef source, Summary summary)
    {
        summary.AppliedLightmaps.Add((target.Owner, target.Slot));
        if (bake?.LightGuids(source) is not { } guids) return;
        switch (target.LightMap)
        {
            case LightMap_1D lm: lm.LightGuids = guids; break;
            case LightMap_2D lm: lm.LightGuids = guids; break;
            case LightMap_3 lm: lm.LightGuids = guids; break;
            case LightMap_4or6 lm: lm.LightGuids = guids; break;
        }
    }

    /// <summary>Mesh lightmap UV of a texel centre, given the allocation's coordinates (the shader's inverse).</summary>
    static Vector2 UvOfTexel(int i, int width, int height, Vector2 scale, Vector2 bias)
    {
        var coord = new Vector2((i % width + 0.5f) / width, (i / width + 0.5f) / height);
        return (coord - bias) / scale;
    }

    /// <summary>A per-texel value: from the bake at the texel's mesh UV when available, else the original atlas.</summary>
    Func<int, T> Value<T>(Allocation a, int width, int height, Vector2 scale, Vector2 bias, Func<int, T> original, Func<(Lighting Lighting, TangentFrame Frame), T> convert)
    {
        var lookup = Baked(a.Refs[0]);
        return lookup == null ? original : i => convert(lookup(UvOfTexel(i, width, height, scale, bias)));
    }

    static Directional.Sample ToDirectional((Lighting Lighting, TangentFrame Frame) x) => LightingConversion.ToDirectional(x.Lighting, x.Frame);
    Vector3 ToVectorColour((Lighting Lighting, TangentFrame Frame) x) => LightingConversion.ToVector(x.Lighting, x.Frame, brightness: vectorBrightness).Colour;
    Vector3 ToVectorDirection((Lighting Lighting, TangentFrame Frame) x) => LightingConversion.ToVector(x.Lighting, x.Frame, brightness: vectorBrightness).Direction;
    static Vector3 ToSimple((Lighting Lighting, TangentFrame Frame) x) => LightingConversion.ToSimple(x.Lighting, x.Frame);

    static AtlasRect Rect(Allocation a, Vector2 scale, Vector2 bias, int width, int height) =>
        AtlasRect.FromCoordinates(scale, bias, width, height, padding: a.Padding);

    // ================= in place =================

    void RewriteInPlace(List<Allocation> list, Summary summary)
    {
        switch (list[0].First)
        {
            case LightMap_2D first:
            {
                var (nac, dmc) = (Read(first.Texture1), Read(first.Texture2));
                var (nacTop, dmcTop) = (MipLevel.Create(nac.Width, nac.Height), MipLevel.Create(nac.Width, nac.Height));
                foreach (var a in list)
                {
                    var lm = (LightMap_2D)a.First;
                    var rect = Rect(a, lm.CoordinateScale, lm.CoordinateBias, nac.Width, nac.Height);
                    var texels = InPlace(rect, a.Padding, nac.Width,
                        Value(a, nac.Width, nac.Height, lm.CoordinateScale, lm.CoordinateBias, i => DecodeDirectional(nac, dmc, i, lm), ToDirectional));
                    var (s1, s2) = EncodeDirectional(texels, Floor(lm), nacTop, dmcTop);
                    foreach (var r in a.Refs)
                    {
                        var live = (LightMap_2D)r.LightMap;
                        (live.ScaleVector1, live.ScaleVector2) = (s1, s2);
                    }
                }
                WriteTexture(nac.Export, nac.Format, nacTop, MipAveraging.Linear, null, summary);
                WriteTexture(dmc.Export, dmc.Format, dmcTop, MipAveraging.Linear, null, summary);
                break;
            }
            case LightMap_4or6 { LightMapType: ELightMapType.LMT_4 } first:
            {
                var (colour, dir) = (Read(first.Texture1), Read(first.Texture2));
                if (colour.Width != dir.Width * 2 || colour.Height != dir.Height * 2)
                {
                    summary.Notes.Add($"{colour.Export.ObjectName}: direction atlas not half resolution; left untouched");
                    return;
                }
                var (colourTop, dirTop) = (MipLevel.Create(colour.Width, colour.Height), MipLevel.Create(dir.Width, dir.Height));
                foreach (var a in list)
                {
                    var lm = (LightMap_4or6)a.First;
                    var cRect = Rect(a, lm.CoordinateScale, lm.CoordinateBias, colour.Width, colour.Height);
                    var colours = InPlace(cRect, a.Padding, colour.Width,
                        Value(a, colour.Width, colour.Height, lm.CoordinateScale, lm.CoordinateBias, i => DecodeColour(colour, i, lm), ToVectorColour));
                    var dirs = InPlaceHalf(cRect, a.Padding, dir.Width,
                        Value(a, dir.Width, dir.Height, lm.CoordinateScale, lm.CoordinateBias, i => DecodeDirection(dir, i, lm), ToVectorDirection));
                    var (cRange, dRange) = EncodeVector(colours, dirs, colourTop, dirTop);
                    foreach (var r in a.Refs)
                    {
                        var live = (LightMap_4or6)r.LightMap;
                        Set(ref live.unkFloats1, cRange.Scale, cRange.Bias);
                        Set(ref live.unkFloats2, dRange.Scale, dRange.Bias);
                    }
                }
                WriteTexture(colour.Export, colour.Format, colourTop, MipAveraging.Linear, null, summary);
                WriteTexture(dir.Export, dir.Format, dirTop, MipAveraging.Bytes, false, summary);
                break;
            }
            case LightMap_4or6 { LightMapType: ELightMapType.LMT_6 } first:
            {
                var colour = Read(first.Texture1);
                var top = MipLevel.Create(colour.Width, colour.Height);
                foreach (var a in list)
                {
                    var lm = (LightMap_4or6)a.First;
                    var rect = Rect(a, lm.CoordinateScale, lm.CoordinateBias, colour.Width, colour.Height);
                    var range = EncodeColourRange(InPlace(rect, a.Padding, colour.Width,
                        Value(a, colour.Width, colour.Height, lm.CoordinateScale, lm.CoordinateBias, i => DecodeColour(colour, i, lm), ToSimple)), top);
                    foreach (var r in a.Refs)
                    {
                        var live = (LightMap_4or6)r.LightMap;
                        Set(ref live.unkFloats1, range.Scale, range.Bias);
                    }
                }
                WriteTexture(colour.Export, colour.Format, top, MipAveraging.Linear, null, summary);
                break;
            }
        }
        foreach (var a in list.Where(a => Baked(a.Refs[0]) != null))
            foreach (var r in a.Refs) RecordAppliedLightmap(r, a.Refs[0], summary);
    }

    /// <summary>
    /// Every texel of an allocation's rectangle, same position in and out. Mapped: the interior inside the padding ring;
    /// for unpadded allocations (BSP groups) the whole rectangle, whose surfaces carry their own padding inside it.
    /// </summary>
    static List<Texel<T>> InPlace<T>(AtlasRect rect, int padding, int width, Func<int, T> value)
    {
        var inner = rect.Inset(padding);
        var result = new List<Texel<T>>(rect.Width * rect.Height);
        for (int y = rect.Y0; y < rect.Y1; y++)
        for (int x = rect.X0; x < rect.X1; x++)
            result.Add(new Texel<T>(y * width + x, value(y * width + x), x >= inner.X0 && x < inner.X1 && y >= inner.Y0 && y < inner.Y1));
        return result;
    }

    /// <summary>The half-resolution direction texels of a colour allocation; mapped if any colour texel they cover is.</summary>
    static List<Texel<T>> InPlaceHalf<T>(AtlasRect cRect, int padding, int dirWidth, Func<int, T> value)
    {
        var inner = cRect.Inset(padding);
        var result = new List<Texel<T>>();
        for (int y = cRect.Y0 / 2; y < (cRect.Y1 + 1) / 2; y++)
        for (int x = cRect.X0 / 2; x < (cRect.X1 + 1) / 2; x++)
        {
            bool mapped = x * 2 + 1 >= inner.X0 && x * 2 < inner.X1 && y * 2 + 1 >= inner.Y0 && y * 2 < inner.Y1;
            result.Add(new Texel<T>(y * dirWidth + x, value(y * dirWidth + x), mapped));
        }
        return result;
    }

    // ================= repack =================

    void Repack(List<List<Allocation>> sets, Summary summary)
    {
        using var meshes = new MeshResolver(package);
        var level = package.Exports.FirstOrDefault(e => e.ClassName == "Level");
        var levelBinary = level != null ? ObjectBinary.From<Level>(level) : null;

        // One request per lightmap; its original texture set and coordinates are kept for fetching texels.
        var requests = new List<AtlasRequest>();
        var oldTextures = new HashSet<int>();
        foreach (var list in sets)
        foreach (var a in list)
        {
            var r = a.Refs[0];
            var (tex, scale, group) = a.First switch
            {
                LightMap_2D d => (d.Texture1, d.CoordinateScale, "LMT_2D"),
                LightMap_4or6 v => (v.Texture1, v.CoordinateScale, v.LightMapType.ToString()),
                _ => throw new InvalidOperationException(),
            };
            var t = Read(tex);
            bool streamed = t.Export.GetProperty<NameProperty>("TextureFileCacheName") is { } n && n.Value.Name != "None";
            (Vector3, float)? bounds = null;
            if (r.Owner.ClassName == "StaticMeshComponent" && ComponentGeometry.Load(r.Owner, meshes, out _) is { } g)
            {
                var (min, max) = g.Bounds();
                bounds = ((min + max) / 2, Vector3.Distance(min, max) / 2);
            }
            requests.Add(new AtlasRequest(new Source(a.Refs, Snapshot(a.First)), group,
                (int)MathF.Round(scale.X * t.Width), (int)MathF.Round(scale.Y * t.Height), streamed, bounds, a.Padding));
            foreach (int u in Textures(a.First)) oldTextures.Add(u);
        }

        var atlases = AtlasPacker.Pack(requests);
        summary.NewAtlases = atlases.Count;
        var newToOld = new Dictionary<int, HashSet<int>>();
        foreach (var atlas in atlases)
        {
            var sources = atlas.Placements.Select(p => (Source)p.Request.Key).ToList();
            var created = atlas.Group switch
            {
                "LMT_2D" => BuildDirectionalAtlas(atlas, summary),
                "LMT_4" => BuildVectorAtlas(atlas, summary),
                _ => BuildSimpleAtlas(atlas, summary),
            };
            foreach (int u in created)
                newToOld[u] = sources.SelectMany(s => Textures(s.Original)).ToHashSet();
        }

        // Texture streaming: each new texture inherits the instances of the textures its allocations came from.
        if (levelBinary != null)
        {
            var map = levelBinary.TextureToInstancesMap;
            var old = map.Where(kv => oldTextures.Contains(kv.Key)).ToList();
            foreach (var (u, from) in newToOld)
            {
                var instances = old.Where(kv => from.Contains(kv.Key)).SelectMany(kv => kv.Value.Instances)
                    .GroupBy(i => (i.BoundingSphere.Center, i.BoundingSphere.W, i.TexelFactor)).Select(g => g.First()).ToArray();
                if (instances.Length > 0) map.Add(u, new StreamableTextureInstanceList { Instances = instances });
            }
            foreach (int u in oldTextures) map.Remove(u);
            level.WriteBinary(levelBinary);
        }

        // The old atlases are no longer referenced by any component; trash them so they don't ship.
        var trash = oldTextures.Select(u => (IEntry)package.GetUExport(u)).ToList();
        EntryPruner.TrashEntries(package, trash);
        summary.TrashedTextures = trash.Count;
    }

    /// <summary>A repacked allocation: the lightmaps to repoint (several for a shared BSP group) and a copy of its original fields.</summary>
    sealed record Source(List<LightmapRef> Refs, LightMap Original);

    /// <summary>A copy of the fields needed to decode the original after the live LightMap has been repointed.</summary>
    static LightMap Snapshot(LightMap lm) => lm switch
    {
        LightMap_2D d => new LightMap_2D
        {
            LightMapType = d.LightMapType, Texture1 = d.Texture1, Texture2 = d.Texture2, ScaleVector1 = d.ScaleVector1, ScaleVector2 = d.ScaleVector2,
            CoordinateScale = d.CoordinateScale, CoordinateBias = d.CoordinateBias,
        },
        LightMap_4or6 v => new LightMap_4or6
        {
            LightMapType = v.LightMapType, Texture1 = v.Texture1, Texture2 = v.Texture2, unkFloats1 = v.unkFloats1, unkFloats2 = v.unkFloats2,
            CoordinateScale = v.CoordinateScale, CoordinateBias = v.CoordinateBias,
        },
        _ => throw new InvalidOperationException(),
    };

    static IEnumerable<int> Textures(LightMap lm) => lm switch
    {
        LightMap_2D d => new[] { d.Texture1, d.Texture2 },
        LightMap_4or6 { LightMapType: ELightMapType.LMT_4 } v => new[] { v.Texture1, v.Texture2 },
        LightMap_4or6 v => new[] { v.Texture1 },
        _ => [],
    };

    int[] BuildDirectionalAtlas(Atlas atlas, Summary summary)
    {
        var (nacTop, dmcTop) = (MipLevel.Create(atlas.SizeX, atlas.SizeY), MipLevel.Create(atlas.SizeX, atlas.SizeY));
        var template = (LightMap_2D)((Source)atlas.Placements[0].Request.Key).Original;
        var (nacExport, dmcExport) = (Clone(template.Texture1), Clone(template.Texture2));
        foreach (var p in atlas.Placements)
        {
            var src = (Source)p.Request.Key;
            var orig = (LightMap_2D)src.Original;
            var (nac, dmc) = (Read(orig.Texture1), Read(orig.Texture2));
            var baked = Baked(src.Refs[0]);
            var texels = Repacked<Directional.Sample>(p, atlas.SizeX, 1, baked != null
                ? uv => ToDirectional(baked(uv))
                : uv => DecodeDirectional(nac, dmc, Index(uv, orig.CoordinateScale, orig.CoordinateBias, nac.Width, nac.Height), orig));
            var (s1, s2) = EncodeDirectional(texels, Floor(orig), nacTop, dmcTop);
            foreach (var r in src.Refs)
            {
                var live = (LightMap_2D)r.LightMap;
                (live.ScaleVector1, live.ScaleVector2) = (s1, s2);
                (live.Texture1, live.Texture2) = (nacExport.UIndex, dmcExport.UIndex);
                (live.CoordinateScale, live.CoordinateBias) = p.Coordinates(atlas.SizeX, atlas.SizeY);
                if (baked != null) RecordAppliedLightmap(r, src.Refs[0], summary);
            }
        }
        var format = Read(template.Texture1).Format;
        WriteTexture(nacExport, format, nacTop, MipAveraging.Linear, null, summary, atlas.Streamed);
        WriteTexture(dmcExport, format, dmcTop, MipAveraging.Linear, null, summary, atlas.Streamed);
        return [nacExport.UIndex, dmcExport.UIndex];
    }

    int[] BuildVectorAtlas(Atlas atlas, Summary summary)
    {
        var colourTop = MipLevel.Create(atlas.SizeX, atlas.SizeY);
        var dirTop = MipLevel.Create(Math.Max(1, atlas.SizeX / 2), Math.Max(1, atlas.SizeY / 2));
        var template = (LightMap_4or6)((Source)atlas.Placements[0].Request.Key).Original;
        var (colourExport, dirExport) = (Clone(template.Texture1), Clone(template.Texture2));
        foreach (var p in atlas.Placements)
        {
            var src = (Source)p.Request.Key;
            var orig = (LightMap_4or6)src.Original;
            var (colour, dir) = (Read(orig.Texture1), Read(orig.Texture2));
            var baked = Baked(src.Refs[0]);
            var colours = Repacked<Vector3>(p, atlas.SizeX, 1, baked != null
                ? uv => ToVectorColour(baked(uv))
                : uv => DecodeColour(colour, Index(uv, orig.CoordinateScale, orig.CoordinateBias, colour.Width, colour.Height), orig));
            var dirs = Repacked<Vector3>(p, dirTop.Width, 2, baked != null
                ? uv => ToVectorDirection(baked(uv))
                : uv => DecodeDirection(dir, Index(uv, orig.CoordinateScale, orig.CoordinateBias, dir.Width, dir.Height), orig));
            var (cRange, dRange) = EncodeVector(colours, dirs, colourTop, dirTop);
            foreach (var r in src.Refs)
            {
                var live = (LightMap_4or6)r.LightMap;
                Set(ref live.unkFloats1, cRange.Scale, cRange.Bias);
                Set(ref live.unkFloats2, dRange.Scale, dRange.Bias);
                (live.Texture1, live.Texture2) = (colourExport.UIndex, dirExport.UIndex);
                (live.CoordinateScale, live.CoordinateBias) = p.Coordinates(atlas.SizeX, atlas.SizeY);
                if (baked != null) RecordAppliedLightmap(r, src.Refs[0], summary);
            }
        }
        WriteTexture(colourExport, Read(template.Texture1).Format, colourTop, MipAveraging.Linear, null, summary, atlas.Streamed);
        WriteTexture(dirExport, Read(template.Texture2).Format, dirTop, MipAveraging.Bytes, false, summary, atlas.Streamed);
        return [colourExport.UIndex, dirExport.UIndex];
    }

    int[] BuildSimpleAtlas(Atlas atlas, Summary summary)
    {
        var top = MipLevel.Create(atlas.SizeX, atlas.SizeY);
        var template = (LightMap_4or6)((Source)atlas.Placements[0].Request.Key).Original;
        var export = Clone(template.Texture1);
        foreach (var p in atlas.Placements)
        {
            var src = (Source)p.Request.Key;
            var orig = (LightMap_4or6)src.Original;
            var colour = Read(orig.Texture1);
            var baked = Baked(src.Refs[0]);
            var range = EncodeColourRange(Repacked<Vector3>(p, atlas.SizeX, 1, baked != null
                ? uv => ToSimple(baked(uv))
                : uv => DecodeColour(colour, Index(uv, orig.CoordinateScale, orig.CoordinateBias, colour.Width, colour.Height), orig)), top);
            foreach (var r in src.Refs)
            {
                var live = (LightMap_4or6)r.LightMap;
                Set(ref live.unkFloats1, range.Scale, range.Bias);
                live.Texture1 = export.UIndex;
                (live.CoordinateScale, live.CoordinateBias) = p.Coordinates(atlas.SizeX, atlas.SizeY);
                if (baked != null) RecordAppliedLightmap(r, src.Refs[0], summary);
            }
        }
        WriteTexture(export, Read(template.Texture1).Format, top, MipAveraging.Linear, null, summary, atlas.Streamed);
        return [export.UIndex];
    }

    /// <summary>
    /// Every texel of a placement's padded rectangle in an atlas stored at 1/divisor resolution, with its value fetched
    /// through the mesh UV the texel samples (AtlasPlacement.TexelToUV). The interior is mapped; at half resolution a
    /// texel is mapped if either colour texel it covers is.
    /// </summary>
    static List<Texel<T>> Repacked<T>(AtlasPlacement p, int atlasWidth, int divisor, Func<Vector2, T> valueAtUV)
    {
        var (w, h) = p.PaddedSize(divisor);
        int ox = p.OffsetX / divisor, oy = p.OffsetY / divisor;
        var result = new List<Texel<T>>(w * h);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int pad = p.Request.Padding;
            bool mapped = x * divisor + divisor - 1 >= pad && x * divisor < p.PaddedX - pad && y * divisor + divisor - 1 >= pad && y * divisor < p.PaddedY - pad;
            result.Add(new Texel<T>((oy + y) * atlasWidth + ox + x, valueAtUV(p.TexelToUV(x, y, divisor)), mapped));
        }
        return result;
    }

    /// <summary>The original atlas texel holding a mesh UV (the shader's coordinate is uv * scale + bias).</summary>
    static int Index(Vector2 uv, Vector2 scale, Vector2 bias, int width, int height)
    {
        int x = Math.Clamp((int)MathF.Floor((uv.X * scale.X + bias.X) * width), 0, width - 1);
        int y = Math.Clamp((int)MathF.Floor((uv.Y * scale.Y + bias.Y) * height), 0, height - 1);
        return y * width + x;
    }

    ExportEntry Clone(int templateUIndex)
    {
        var template = package.GetUExport(templateUIndex);
        var clone = EntryCloner.CloneEntry(template);
        clone.ObjectName = package.GetNextIndexedName($"{template.ClassName}_LE3LM");
        return clone;
    }

    // ================= shared encoding =================

    Directional.Sample DecodeDirectional(DecodedTexture nac, DecodedTexture dmc, int i, LightMap_2D lm)
    {
        int o = i * 3;
        return Directional.Combine(
            Quantization.Decode(nac.Rgb[o], nac.Rgb[o + 1], nac.Rgb[o + 2], lm.ScaleVector1, textureEncoding),
            Quantization.Decode(dmc.Rgb[o], dmc.Rgb[o + 1], dmc.Rgb[o + 2], lm.ScaleVector2, textureEncoding));
    }

    Vector3 DecodeColour(DecodedTexture t, int i, LightMap_4or6 lm) =>
        new SimpleLightmap.ColourRange(V(lm.unkFloats1, 0), V(lm.unkFloats1, 4)).Decode(t.Rgb[i * 3], t.Rgb[i * 3 + 1], t.Rgb[i * 3 + 2], textureEncoding);

    static Vector3 DecodeDirection(DecodedTexture t, int i, LightMap_4or6 lm) =>
        new VectorLightmap.DirectionRange(V(lm.unkFloats2, 0), V(lm.unkFloats2, 4)).Decode(t.Rgb[i * 3], t.Rgb[i * 3 + 1], t.Rgb[i * 3 + 2]);

    /// <summary>Keeps each lightmap's scale rule (ME3's floor of 1, or Epic's plain max; LE3_Lightmaps.md §6.1).</summary>
    // ================= per vertex =================

    /// <summary>
    /// LMT_1D and LMT_3 from the bake, one sample per vertex (ILightingSource.ForVertices): LMT_1D through the directional
    /// split with gamma-2.2 bytes and the lightmap's per-channel scales (ME3's floor of 1 where the original has it), LMT_3
    /// through the Vector fit with its direction in the vertex's tangent space. Lightmaps the bake can't supply, or whose
    /// sample count isn't the vertex count, keep their samples.
    /// </summary>
    void RewriteVertexLightmaps(List<LightmapRef> refs, Summary summary)
    {
        foreach (var r in refs)
        {
            var samples = r.LightMap switch
            {
                LightMap_1D { LightMapType: ELightMapType.LMT_1D } d => d.DirectionalSamples,
                LightMap_3 v => v.DirectionalSamples,
                _ => null,
            };
            if (samples == null || samples.Length == 0) continue;
            var baked = bake.ForVertices(r);
            if (baked == null) continue;
            if (baked.Length != samples.Length) { summary.VertexCountMismatches++; continue; }

            if (r.LightMap is LightMap_1D lm)
            {
                var original = samples.Select(x => Directional.Combine(
                    Quantization.Decode(x.Coefficient2.R, x.Coefficient2.G, x.Coefficient2.B, lm.ScaleVector1, ByteEncoding.Gamma22),
                    Quantization.Decode(x.Coefficient3.R, x.Coefficient3.G, x.Coefficient3.B, lm.ScaleVector2, ByteEncoding.Gamma22))).ToArray();
                var split = baked.Select(b => Directional.Split(ToDirectional(b))).ToArray();
                var (before, after) = (new Vector3[samples.Length], new Vector3[samples.Length]);
                float floor = MathF.Min(MinChannel(lm.ScaleVector1), MinChannel(lm.ScaleVector2)) >= 0.9999f ? 1f : 0f;
                var s1 = Quantization.ChooseScale(split.Select(x => x.Colour), floor);
                var s2 = Quantization.ChooseScale(split.Select(x => x.MaxComponents), floor);
                for (int i = 0; i < samples.Length; i++)
                {
                    var c = Quantization.Encode(split[i].Colour, s1, ByteEncoding.Gamma22);
                    var m = Quantization.Encode(split[i].MaxComponents, s2, ByteEncoding.Gamma22);
                    samples[i].Coefficient2 = new LegendaryExplorerCore.SharpDX.Color(c.R, c.G, c.B, samples[i].Coefficient2.A);
                    samples[i].Coefficient3 = new LegendaryExplorerCore.SharpDX.Color(m.R, m.G, m.B, samples[i].Coefficient3.A);
                    var written = Directional.Combine(Quantization.Decode(c.R, c.G, c.B, s1, ByteEncoding.Gamma22),
                        Quantization.Decode(m.R, m.G, m.B, s2, ByteEncoding.Gamma22));
                    (before[i], after[i]) = (Directional.Evaluate(original[i], Vector3.UnitZ), Directional.Evaluate(written, Vector3.UnitZ));
                }
                (lm.ScaleVector1, lm.ScaleVector2) = (s1, s2);
                CompareVertices(before, after, summary);
            }
            else if (r.LightMap is LightMap_3 v3)
            {
                var original = samples.Select(x => VectorLightmap.DecodeVertex(x.Coefficient2.R, x.Coefficient2.G, x.Coefficient2.B,
                    x.Coefficient3.R, x.Coefficient3.G, x.Coefficient3.B, v3.ScaleVector1)).ToArray();
                var vector = baked.Select(b =>
                {
                    var world = LightingConversion.ToVector(b.Lighting, b.Frame, brightness: vectorBrightness);
                    var f = b.Frame;
                    var t = new Vector3(Vector3.Dot(world.Direction, f.T), Vector3.Dot(world.Direction, f.B), Vector3.Dot(world.Direction, f.N));
                    return new VectorLightmap.Sample(world.Colour, t.LengthSquared() > 1e-12f ? Vector3.Normalize(t) : Vector3.UnitZ);
                }).ToArray();
                var scale = VectorLightmap.ChooseVertexColourScale(vector);
                var (before, after) = (new Vector3[samples.Length], new Vector3[samples.Length]);
                for (int i = 0; i < samples.Length; i++)
                {
                    var (c, d) = VectorLightmap.EncodeVertex(vector[i], scale);
                    samples[i].Coefficient2 = new LegendaryExplorerCore.SharpDX.Color(c.Item1, c.Item2, c.Item3, samples[i].Coefficient2.A);
                    samples[i].Coefficient3 = new LegendaryExplorerCore.SharpDX.Color(d.Item1, d.Item2, d.Item3, samples[i].Coefficient3.A);
                    var written = VectorLightmap.DecodeVertex(c.Item1, c.Item2, c.Item3, d.Item1, d.Item2, d.Item3, scale);
                    (before[i], after[i]) = (VectorLightmap.Evaluate(original[i], Vector3.UnitZ, true), VectorLightmap.Evaluate(written, Vector3.UnitZ, true));
                }
                v3.ScaleVector1 = scale;
                CompareVertices(before, after, summary);
            }
            RecordAppliedLightmap(r, r, summary);
            summary.VertexLightmaps++;
        }
    }

    static void CompareVertices(Vector3[] original, Vector3[] written, Summary summary)
    {
        int n = original.Length;
        if (Correlation(i => i) is { } r) summary.VertexCorrelations.Add(r);
        if (n >= 16 && Correlation(i => (i + n / 2) % n) is { } control) summary.VertexShiftedCorrelations.Add(control);

        (float, int)? Correlation(Func<int, int> pair)
        {
            var (xs, ys) = (new List<double>(), new List<double>());
            for (int i = 0; i < n; i++)
            {
                float a = Lighting.Luminance(original[i]), b = Lighting.Luminance(written[pair(i)]);
                if (a > 1e-3f && b > 1e-3f) { xs.Add(Math.Log(a)); ys.Add(Math.Log(b)); }
            }
            if (xs.Count < 8) return null;
            double mx = xs.Average(), my = ys.Average(), sxy = 0, sxx = 0, syy = 0;
            for (int i = 0; i < xs.Count; i++) { sxy += (xs[i] - mx) * (ys[i] - my); sxx += (xs[i] - mx) * (xs[i] - mx); syy += (ys[i] - my) * (ys[i] - my); }
            return sxx > 1e-12 && syy > 1e-12 ? ((float)(sxy / Math.Sqrt(sxx * syy)), xs.Count) : null;
        }
    }

    static float Floor(LightMap_2D lm) => MathF.Min(MinChannel(lm.ScaleVector1), MinChannel(lm.ScaleVector2)) >= 0.9999f ? 1f : 0f;

    (Vector3 Scale1, Vector3 Scale2) EncodeDirectional(List<Texel<Directional.Sample>> texels, float floor, MipLevel nacTop, MipLevel dmcTop)
    {
        var split = texels.Select(t => Directional.Split(t.Value)).ToList();
        var scale1 = Quantization.ChooseScale(split.Select(s => s.Colour), floor);
        var scale2 = Quantization.ChooseScale(split.Select(s => s.MaxComponents), floor);
        for (int k = 0; k < texels.Count; k++)
        {
            int i = texels[k].Dest;
            (nacTop.Rgb[i * 3], nacTop.Rgb[i * 3 + 1], nacTop.Rgb[i * 3 + 2]) = Quantization.Encode(split[k].Colour, scale1, textureEncoding);
            (dmcTop.Rgb[i * 3], dmcTop.Rgb[i * 3 + 1], dmcTop.Rgb[i * 3 + 2]) = Quantization.Encode(split[k].MaxComponents, scale2, textureEncoding);
            nacTop.Coverage[i] = dmcTop.Coverage[i] = texels[k].Mapped ? (byte)255 : (byte)0;
        }
        return (scale1, scale2);
    }

    (SimpleLightmap.ColourRange, VectorLightmap.DirectionRange) EncodeVector(List<Texel<Vector3>> colours, List<Texel<Vector3>> dirs, MipLevel colourTop, MipLevel dirTop)
    {
        var cRange = EncodeColourRange(colours, colourTop);
        var dRange = VectorLightmap.DirectionRange.Choose(dirs.Select(d => d.Value));
        foreach (var d in dirs)
        {
            (dirTop.Rgb[d.Dest * 3], dirTop.Rgb[d.Dest * 3 + 1], dirTop.Rgb[d.Dest * 3 + 2]) = dRange.Encode(d.Value);
            dirTop.Coverage[d.Dest] = d.Mapped ? (byte)255 : (byte)0;
        }
        return (cRange, dRange);
    }

    SimpleLightmap.ColourRange EncodeColourRange(List<Texel<Vector3>> colours, MipLevel top)
    {
        var range = SimpleLightmap.ColourRange.Choose(colours.Select(c => c.Value));
        foreach (var c in colours)
        {
            (top.Rgb[c.Dest * 3], top.Rgb[c.Dest * 3 + 1], top.Rgb[c.Dest * 3 + 2]) = range.Encode(c.Value, textureEncoding);
            top.Coverage[c.Dest] = c.Mapped ? (byte)255 : (byte)0;
        }
        return range;
    }

    /// <param name="streamed">Whether the texture goes to the mod TFC; null keeps the export's current storage.</param>
    void WriteTexture(ExportEntry export, string format, MipLevel top, MipAveraging averaging, bool? srgb, Summary summary, bool? streamed = null)
    {
        var levels = MipChain.BuildDilated(top, averaging, minSize: MipChain.MinMipSize(format));
        var encoded = LightmapTextureWriter.Encode(levels, format);
        if (format == "PF_DXT1")
        {
            var decoded = Dxt1.Decode(encoded[0], top.Width, top.Height);
            for (int i = 0; i < top.Coverage.Length; i++)
            {
                if (top.Coverage[i] == 0) continue;
                for (int c = 0; c < 3; c++) { double d = decoded[i * 3 + c] - levels[0].Rgb[i * 3 + c]; summary.DxtSquaredError += d * d; }
                summary.DxtSamples += 3;
            }
        }
        bool toTfc = streamed ?? (export.GetProperty<NameProperty>("TextureFileCacheName") is { } n && n.Value.Name != "None");
        LightmapTextureWriter.Write(export, format, top.Width, top.Height, encoded, toTfc ? tfc : null, srgb);
        summary.Textures++;
    }

    static float MinChannel(Vector3 v) => MathF.Min(v.X, MathF.Min(v.Y, v.Z));
    static Vector3 V(LegendaryExplorerCore.Gammtek.Fixed8<float> f, int start) => new(f[start], f[start + 1], f[start + 2]);

    static void Set(ref LegendaryExplorerCore.Gammtek.Fixed8<float> f, Vector3 scale, Vector3 bias)
    {
        f[0] = scale.X; f[1] = scale.Y; f[2] = scale.Z;
        f[4] = bias.X; f[5] = bias.Y; f[6] = bias.Z;
    }
}
