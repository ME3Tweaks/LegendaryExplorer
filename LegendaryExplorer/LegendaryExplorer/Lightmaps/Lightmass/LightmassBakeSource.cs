using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Lightmaps.Codec;
using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>
/// Lightmass's results as lighting for <see cref="LightmapRewriter"/>.
///
/// Lightmass gathers each sample as irradiance along the three orthonormal HL2 basis directions (E_i, with direct light scaled by
/// DirectionalCoefficientScale·cos^0.85, which is what ME3's directional lightmaps hold) and along the normal (E_N, unscaled, what
/// simple lightmaps hold). Because of that scale the four aren't one linear field, so the lighting handed on depends on the target:
/// <list type="bullet">
/// <item>LMT_1D/2D/3/4: the linear field with b = Σ E_i B_i and no constant term, which reproduces every E_i exactly
/// (ToDirectional) and gives the Vector fit a sensible dominant direction.</item>
/// <item>LMT_6: uniform light of irradiance E_N, which ToSimple returns exactly.</item>
/// </list>
/// Lighting is expressed in tangent space with an identity frame, except for LMT_4, whose direction texture is in world space:
/// there it's rotated into the world with LE3's own (orthonormalised) tangent frame at the texel.
/// </summary>
public sealed class LightmassBakeSource : ILightingSource, IDisposable
{
    static readonly TangentFrame Identity = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

    readonly Dictionary<(ExportEntry, string), (LightmassMapping Mapping, LightmassMappingResult Result)> byLightmap = new();
    readonly Dictionary<IMEPackage, MeshResolver> resolvers = new();
    readonly Dictionary<Guid, Guid> lightGuids;

    public Dictionary<string, int> Report { get; } = new();
    /// <summary>Levels with a lightmap whose result couldn't be applied: what's changed since their last bake stays marked</summary>
    public HashSet<IMEPackage> Unfinished { get; } = [];
    public int Matched { get; private set; }

    public LightmassBakeSource(LightmassScene scene, IReadOnlyDictionary<Guid, LightmassMappingResult> results)
    {
        lightGuids = scene.Lights.Where(l => !l.ShadowOnly && !l.IndirectOnly).ToDictionary(l => l.Guid, l => l.Light.LightmapGuid);
        foreach (var mapping in scene.Mappings)
        {
            if (!results.TryGetValue(mapping.Guid, out var result)) continue;
            // BSP elements sharing an allocation all get the surface group's result.
            foreach (var target in mapping.Targets)
                byLightmap[(target.Owner, target.Slot)] = (mapping, result);
        }
    }

    void Count(string why) => Report[why] = Report.GetValueOrDefault(why) + 1;

    void CountUnfinished(LightmapRef r, string why)
    {
        Count(why);
        Unfinished.Add(r.Owner.FileRef);
    }

    /// <summary>The light GUIDs Lightmass reported for a lightmap, or null if it wasn't baked.</summary>
    public Guid[] LightGuids(LightmapRef r) => byLightmap.TryGetValue((r.Owner, r.Slot), out var e)
        ? e.Result.LightGuids.Where(lightGuids.ContainsKey).Select(g => lightGuids[g]).Distinct().ToArray() : null;

    public Func<Vector2, (Lighting Lighting, TangentFrame Frame)> For(LightmapRef r)
    {
        if (!byLightmap.TryGetValue((r.Owner, r.Slot), out var entry)) { Count("not in the bake"); return null; }
        var (mapping, result) = entry;
        if (!result.IsTextureMapping || result.SizeX != mapping.SizeX || result.SizeY != mapping.SizeY) { CountUnfinished(r, "result doesn't match its mapping"); return null; }
        bool simple = r.LightMap.LightMapType == ELightMapType.LMT_6;
        bool worldFrame = r.LightMap.LightMapType == ELightMapType.LMT_4;

        UvLocator locator = null;
        if (worldFrame)
        {
            if (mapping is not LightmassInstance instance) { CountUnfinished(r, "LMT_4 on something other than a static mesh"); return null; }
            if (!resolvers.TryGetValue(r.Owner.FileRef, out var resolver)) resolvers[r.Owner.FileRef] = resolver = new MeshResolver(r.Owner.FileRef);
            var g = ComponentGeometry.Load(r.Owner, resolver, out _, instance.LodIndex);
            if (g == null || g.LightMapCoordinateIndex < 0) { CountUnfinished(r, "no LE3 geometry for the world-space frame"); return null; }
            locator = new UvLocator(g);
        }

        var texels = Dilate(result, i => simple ? Lighting.Ambient(result.Simple[i]) : FromBasis(result.Directional[i]));
        // With bPadMappings Lightmass maps the lightmap UVs 0..1 onto the interior (Size - 2 texels), one texel in; terrain isn't padded.
        bool padded = mapping.Padded && result.SizeX > 2 && result.SizeY > 2;
        int interiorX = padded ? result.SizeX - 2 : result.SizeX, interiorY = padded ? result.SizeY - 2 : result.SizeY, pad = padded ? 1 : 0;
        Matched++;
        return uv =>
        {
            int x = Math.Clamp((int)MathF.Floor(uv.X * interiorX + pad), 0, result.SizeX - 1);
            int y = Math.Clamp((int)MathF.Floor(uv.Y * interiorY + pad), 0, result.SizeY - 1);
            var lighting = texels[y * result.SizeX + x];
            if (locator == null) return (lighting, Identity);
            var frame = Orthonormal(locator.Locate(uv).Frame);
            return (ToWorld(lighting, frame), frame);
        };
    }

    public (Lighting Lighting, TangentFrame Frame)[] ForVertices(LightmapRef r)
    {
        if (!byLightmap.TryGetValue((r.Owner, r.Slot), out var entry)) return null;
        var (_, result) = entry;
        if (result.IsTextureMapping) { CountUnfinished(r, "result doesn't match its mapping"); return null; }
        var samples = new (Lighting, TangentFrame)[result.SizeX];
        for (int i = 0; i < samples.Length; i++) samples[i] = (FromBasis(result.Directional[i]), Identity);
        Matched++;
        return samples;
    }

    /// <summary>The linear field b·n with b = Σ E_i B_i (the basis is orthonormal), as L1 lighting: Irradiance(n) = 2π/3 B·n.</summary>
    static Lighting FromBasis(Directional.Sample s)
    {
        var basis = Directional.Basis;
        Vector3 Component(int axis) => (s.E0 * basis[0].Get(axis) + s.E1 * basis[1].Get(axis) + s.E2 * basis[2].Get(axis)) * (3f / (2f * MathF.PI));
        return new Lighting(Vector3.Zero, Component(0), Component(1), Component(2));
    }

    /// <summary>Tangent-space lighting in world space: each colour's direction vector rotated by the frame.</summary>
    static Lighting ToWorld(in Lighting t, in TangentFrame f) => new(t.A,
        t.Bx * f.T.X + t.By * f.B.X + t.Bz * f.N.X,
        t.Bx * f.T.Y + t.By * f.B.Y + t.Bz * f.N.Y,
        t.Bx * f.T.Z + t.By * f.B.Z + t.Bz * f.N.Z);

    static TangentFrame Orthonormal(in TangentFrame f)
    {
        var n = Safe(f.N, Vector3.UnitZ);
        var t = Safe(f.T - n * Vector3.Dot(n, f.T), Perpendicular(n));
        var b = Vector3.Cross(n, t);
        if (Vector3.Dot(b, f.B) < 0) b = -b;
        return new TangentFrame(t, b, n);
    }

    static Vector3 Perpendicular(Vector3 n) => Vector3.Normalize(MathF.Abs(n.X) < 0.9f ? Vector3.Cross(n, Vector3.UnitX) : Vector3.Cross(n, Vector3.UnitY));
    static Vector3 Safe(Vector3 v, Vector3 fallback) => v.LengthSquared() > 1e-12f ? Vector3.Normalize(v) : fallback;

    /// <summary>
    /// Per-texel lighting with unmapped texels (chart gaps, the padding ring) filled from mapped neighbours, ring by ring, so the
    /// rewriter's bilinear-filtered atlases don't pull black in at chart edges.
    /// </summary>
    static Lighting[] Dilate(LightmassMappingResult result, Func<int, Lighting> value)
    {
        int w = result.SizeX, h = result.SizeY;
        var texels = new Lighting[w * h];
        var filled = new bool[w * h];
        var queued = new bool[w * h];
        for (int i = 0; i < texels.Length; i++)
            if (result.Coverage[i] != 0) { texels[i] = value(i); filled[i] = true; }

        // Each ring is the unfilled texels next to a filled one; only the last ring's neighbours can be in the next.
        var ring = new List<int>();
        void QueueNeighbours(int i)
        {
            int x = i % w, y = i / w;
            for (int sy = Math.Max(0, y - 1); sy <= Math.Min(h - 1, y + 1); sy++)
            for (int sx = Math.Max(0, x - 1); sx <= Math.Min(w - 1, x + 1); sx++)
            {
                int j = sy * w + sx;
                if (!filled[j] && !queued[j]) { queued[j] = true; ring.Add(j); }
            }
        }
        for (int i = 0; i < texels.Length; i++)
            if (filled[i]) QueueNeighbours(i);

        var values = new List<Lighting>();
        while (ring.Count > 0)
        {
            values.Clear();
            foreach (int i in ring)
            {
                int x = i % w, y = i / w;
                var sum = new Lighting(); int n = 0;
                for (int sy = Math.Max(0, y - 1); sy <= Math.Min(h - 1, y + 1); sy++)
                for (int sx = Math.Max(0, x - 1); sx <= Math.Min(w - 1, x + 1); sx++)
                    if (filled[sy * w + sx]) { sum += texels[sy * w + sx]; n++; }
                values.Add(new Lighting(sum.A / n, sum.Bx / n, sum.By / n, sum.Bz / n));
            }
            var current = ring.ToArray();
            for (int k = 0; k < current.Length; k++) { texels[current[k]] = values[k]; filled[current[k]] = true; }
            ring.Clear();
            foreach (int i in current) QueueNeighbours(i);
        }
        return texels;
    }

    public void Dispose()
    {
        foreach (var r in resolvers.Values) r.Dispose();
    }
}

static class VectorComponent
{
    public static float Get(this Vector3 v, int axis) => axis switch { 0 => v.X, 1 => v.Y, _ => v.Z };
}
