using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Tools.LevelEditor;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Color = LegendaryExplorerCore.SharpDX.Color;

namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>
/// Decals' copies of their static mesh receivers' vertex lighting. Lightmass doesn't bake decals: UE3 copies a receiver's lighting when it projects a decal
/// (UStaticMeshComponent::GenerateDecalRenderData, FLightMap1D and UShadowMap1D::DuplicateWithRemappedVerts) and saves the copy in the decal's
/// StaticReceivers. So after a bake has rewritten the receivers, each of their decals' records is made again from them:
/// <list type="bullet">
/// <item>A lit decal (its drawn material's LightingModel isn't MLM_Unlit) on a static mesh whose LOD 0 has a vertex light-map gets that light-map (its lights,
/// scales and owner), and a ShadowMap1D, under the decal's actor, for each of the LOD's vertex shadow maps, in the same order, sampled at its vertices.</item>
/// <item>Any other decal on a static mesh gets neither. It uses its receiver's texture lighting through its light-map coordinates.</item>
/// </list>
/// Every shipping decal record on a static mesh is made this way, in all three games. UE3 copies samples by vertex index, since it doesn't clip a lit decal on
/// a vertex-lit receiver. A decal vertex is found on its receiver instead, so a clipped one, such as the Level Editor projects, gets the receiver's lighting
/// interpolated where it lies: the same lighting the receiver draws there
/// </summary>
public static class DecalReceiverLighting
{
    public sealed class Summary
    {
        /// <summary>Decal records made again; how many of them got vertex lighting, and their vertex shadow maps</summary>
        public int Receivers, VertexLit, ShadowMaps;
        /// <summary>Decal shadow maps no record uses any more, which were trashed</summary>
        public int Removed;
        /// <summary>Decals whose material couldn't be found, which are taken to be lit, as UE3's default lighting model is</summary>
        public int UnknownMaterials;
        /// <summary>Records left as they were, and why</summary>
        public List<string> Problems = [];
    }

    /// <summary>Where a decal vertex lies on its receiver: a receiver triangle's three vertices and the decal vertex's weights in it</summary>
    readonly record struct Place(int A, int B, int C, Vector3 Weights);

    /// <summary>Makes the records of the package's decals on <paramref name="receivers"/> again from those receivers' current lighting (after a bake)</summary>
    /// <param name="receivers">Static mesh components, each with its mesh's LOD 0</param>
    public static Summary Write(IMEPackage package, IReadOnlyDictionary<ExportEntry, StaticMeshRenderData> receivers, PackageCache cache)
    {
        var run = new Run(package, receivers, cache);
        foreach (var decal in package.Exports.Where(e => e.IsA("DecalComponent")).ToList()) run.Remake(decal, []);
        return run.Finish();
    }

    /// <summary>
    /// Makes one decal's records on <paramref name="receivers"/> again from those receivers' current lighting, as a bake would: for a decal the Level Editor
    /// has just projected and written. Its new shadow maps reuse those its actor had for it before (<paramref name="released"/>); any left over are trashed
    /// </summary>
    /// <param name="released">The shadow maps its records had before it was written</param>
    public static Summary Write(ExportEntry decal, IReadOnlyDictionary<ExportEntry, StaticMeshRenderData> receivers, PackageCache cache, IEnumerable<int> released)
    {
        var run = new Run(decal.FileRef, receivers, cache);
        run.Remake(decal, released);
        return run.Finish();
    }

    /// <summary>One package's remade decal records, its new shadow map exports and those to trash</summary>
    sealed class Run(IMEPackage package, IReadOnlyDictionary<ExportEntry, StaticMeshRenderData> receivers, PackageCache cache)
    {
        readonly Summary summary = new();
        readonly NewExports newExports = new(package);
        readonly List<IEntry> trash = [];
        /// <summary>Each receiver's LOD 0 lighting, or why it couldn't be read</summary>
        readonly Dictionary<ExportEntry, (StaticMeshComponentLODInfo Lod, string Error)> lods = new();
        /// <summary>Each receiver mesh's triangles, found once for all the decals on it</summary>
        readonly Dictionary<StaticMeshRenderData, TriangleGrid> grids = new(ReferenceEqualityComparer.Instance);

        public void Remake(ExportEntry decal, IEnumerable<int> released)
        {
            DecalComponent binary;
            try
            {
                binary = decal.GetBinaryData<DecalComponent>();
            }
            catch (Exception e)
            {
                summary.Problems.Add($"{decal.InstancedFullPath}: its receivers couldn't be read ({e.Message})");
                return;
            }
            bool? lit = null;
            var remade = new List<(StaticReceiverData Record, LightMap LightMap, List<ShadowMap1D> Shadows)>();
            foreach (var record in binary.StaticReceivers)
            {
                if (!package.TryGetUExport(record.Component, out var component) || !receivers.TryGetValue(component, out var mesh)) continue;
                lit ??= IsLit(decal, cache, summary);
                if (!lods.TryGetValue(component, out var read)) lods[component] = read = ReadLod(component);
                if (read.Error is not null)
                {
                    summary.Problems.Add($"{decal.InstancedFullPath} on {component.InstancedFullPath}: {read.Error}, so it was left as it was");
                    continue;
                }
                var lod = read.Lod;
                LightMap lightMap = new() { LightMapType = ELightMapType.LMT_None };
                List<ShadowMap1D> shadows = [];
                if (lit == true && lod?.LightMap is LightMap_1D or LightMap_3 && record.Vertices.Length > 0)
                {
                    if (!grids.TryGetValue(mesh, out var grid)) grids[mesh] = grid = TriangleGrid.Of(mesh);
                    if (Copy(record, component, mesh, grid, lod, out string problem) is not { } copy)
                    {
                        summary.Problems.Add($"{decal.InstancedFullPath} on {component.InstancedFullPath}: {problem}, so it was left as it was");
                        continue;
                    }
                    (lightMap, shadows) = copy;
                    summary.VertexLit++;
                    summary.ShadowMaps += shadows.Count;
                }
                remade.Add((record, lightMap, shadows));
            }
            released = released.ToList();
            if (remade.Count == 0 && !released.Any()) return;

            // The shadow maps the remade records had, and those released, that no other record keeps: its actor's are rewritten in order, then trashed.
            // Those elsewhere aren't its own, so they're only let go
            var kept = binary.StaticReceivers.Except(remade.Select(r => r.Record)).SelectMany(r => r.ShadowMap1D ?? []).ToHashSet();
            var owned = new Queue<ExportEntry>(remade.SelectMany(r => r.Record.ShadowMap1D ?? []).Concat(released).Distinct().Where(u => !kept.Contains(u))
                .Select(u => package.TryGetUExport(u, out var e) ? e : null).Where(e => e is { ClassName: "ShadowMap1D" } && e.Parent == decal.Parent));
            foreach (var (record, lightMap, shadows) in remade)
            {
                record.LightMap = lightMap;
                record.ShadowMap1D = shadows.Select(shadow =>
                {
                    if (owned.TryDequeue(out var export)) export.WriteBinary(shadow);
                    else export = newExports.Add(decal.Parent, "ShadowMap1D", null, shadow);
                    return export.UIndex;
                }).ToArray();
            }
            trash.AddRange(owned);
            summary.Receivers += remade.Count;
            decal.WriteBinary(binary);
        }

        static (StaticMeshComponentLODInfo, string) ReadLod(ExportEntry component)
        {
            try
            {
                return (ObjectBinary.From<StaticMeshComponent>(component).LODData.FirstOrDefault(), null);
            }
            catch (Exception e)
            {
                return (null, $"its receiver's lighting couldn't be read ({e.Message})");
            }
        }

        public Summary Finish()
        {
            summary.Removed = trash.Count;
            if (trash.Count > 0) EntryPruner.TrashEntries(package, trash);
            return summary;
        }
    }

    /// <summary>Whether a decal's drawn material is lit (FDecalState::bDecalMaterialHasUnlitLightingModel)</summary>
    static bool IsLit(ExportEntry decal, PackageCache cache, Summary summary)
    {
        var (reference, package) = EffectiveLightingProperties.Reference(decal, "DecalMaterial", cache);
        var drawn = MaterialUsage.DecalDrawnMaterial(reference?.ResolveToEntry(package), decal.Game, cache);
        bool? unlit = drawn is null ? null : MaterialUsage.IsUnlit(drawn, cache);
        if (unlit is null) summary.UnknownMaterials++;
        return unlit != true;
    }

    /// <summary>The receiver's vertex light-map and vertex shadow maps at the decal's vertices, or null and why they can't be copied</summary>
    /// <param name="grid">The mesh's triangles (null: it has none to find vertices on)</param>
    static (LightMap LightMap, List<ShadowMap1D> Shadows)? Copy(StaticReceiverData record, ExportEntry component, StaticMeshRenderData mesh, TriangleGrid grid,
        StaticMeshComponentLODInfo lod, out string problem)
    {
        int vertices = mesh.PositionVertexBuffer.VertexData.Length;
        problem = "its vertices aren't on its receiver's LOD 0";
        if (grid is null || Locate(record, mesh, grid) is not { } places) return null;
        problem = "its receiver's vertex lighting doesn't have a sample per vertex";
        var lightMap = lod.LightMap switch
        {
            LightMap_1D s when s.DirectionalSamples.Length == vertices && (s.SimpleSamples.Length is 0 || s.SimpleSamples.Length == vertices) => new LightMap_1D
            {
                LightMapType = ELightMapType.LMT_1D, LightGuids = s.LightGuids.ToArray(), Owner = component.UIndex,
                ScaleVector1 = s.ScaleVector1, ScaleVector2 = s.ScaleVector2, ScaleVector3 = s.ScaleVector3, ScaleVector4 = s.ScaleVector4,
                DirectionalSamples = Remap(s.DirectionalSamples, places), SimpleSamples = s.SimpleSamples.Length == 0 ? [] : Remap(s.SimpleSamples, places),
            },
            LightMap_3 s when s.DirectionalSamples.Length == vertices => new LightMap_3
            {
                LightMapType = ELightMapType.LMT_3, LightGuids = s.LightGuids.ToArray(), unkInt = s.unkInt,
                ScaleVector1 = s.ScaleVector1, ScaleVector2 = s.ScaleVector2, DirectionalSamples = Remap(s.DirectionalSamples, places),
            },
            _ => (LightMap)null,
        };
        if (lightMap is null) return null;
        var shadows = new List<ShadowMap1D>();
        foreach (int u in lod.ShadowVertexBuffers)
        {
            problem = "one of its receiver's vertex shadow maps is missing, can't be read or doesn't have a sample per vertex";
            if (!component.FileRef.TryGetUExport(u, out var export)) return null;
            ShadowMap1D source;
            try
            {
                source = ObjectBinary.From<ShadowMap1D>(export);
            }
            catch (Exception)
            {
                return null;
            }
            if (source.Samples.Length != vertices) return null;
            shadows.Add(new ShadowMap1D { LightGuid = source.LightGuid, Samples = places.Select(p => Mix(source.Samples, p)).ToArray() });
        }
        return (lightMap, shadows);
    }

    // ================= where decal vertices lie =================

    /// <summary>
    /// Where each of the decal's vertices lies on the receiver's LOD 0, triangle by triangle, since all a decal triangle's vertices lie on the receiver
    /// triangle it was projected from. A vertex at a receiver vertex is that vertex: of receiver triangles that hold a decal triangle equally well (the
    /// two sides of a seam), the one whose vertices' tangents it has is the one UE3 copied. A vertex no decal triangle uses is never drawn, so it gets the
    /// nearest place found, wherever that is. Null if a drawn vertex isn't on the receiver
    /// </summary>
    static Place[] Locate(StaticReceiverData record, StaticMeshRenderData mesh, TriangleGrid grid)
    {
        var positions = mesh.PositionVertexBuffer.VertexData;
        var attributes = mesh.VertexBuffer.VertexData;
        var indices = mesh.IndexBuffer;
        var places = new Place?[record.Vertices.Length];
        var decal = record.Vertices;
        int corners = record.Indices.Length >= 3 ? record.Indices.Length / 3 * 3 : decal.Length / 3 * 3;
        int Corner(int i) => record.Indices.Length >= 3 ? record.Indices[i] : i;
        for (int i = 0; i < corners; i += 3)
            if (!Place3(Corner(i), Corner(i + 1), Corner(i + 2), drawn: true)) return null;
        for (int v = 0; v < decal.Length; v++)
            if (places[v] is null) Place3(v, v, v, drawn: false);
        return places.Select(p => p ?? new Place(indices[0], indices[0], indices[0], Vector3.UnitX)).ToArray();

        bool Place3(int a, int b, int c, bool drawn)
        {
            if (a >= decal.Length || b >= decal.Length || c >= decal.Length) return false;
            int[] corner = [a, b, c];
            var fits = new List<(int Triangle, float Error, int Matches)>();
            foreach (int t in grid.Near((decal[a].Position + decal[b].Position + decal[c].Position) / 3))
            {
                float error = 0;
                foreach (int v in corner) error = MathF.Max(error, Vector3.Distance(decal[v].Position, Closest(decal[v].Position, t, out _)));
                int matches = 0;
                foreach (int v in corner)
                    for (int k = 0; k < 3; k++)
                    {
                        int r = indices[t * 3 + k];
                        if (positions[r] == decal[v].Position && Same(attributes[r].TangentZ, decal[v].TangentZ) && Same(attributes[r].TangentX, decal[v].TangentX)) matches++;
                    }
                fits.Add((t, error, matches));
            }
            if (fits.Count == 0) return false;
            // The best fit; of those as good, up to float error, the one with most of the decal vertices' tangents (the first in mesh order, of equals)
            float bestError = fits.Min(f => f.Error);
            int best = fits.Where(f => f.Error <= bestError + grid.SameFit).MaxBy(f => f.Matches).Triangle;
            if (drawn && bestError > grid.Tolerance) return false;
            foreach (int v in corner)
            {
                if (places[v] is not null) continue;
                int ia = indices[best * 3], ib = indices[best * 3 + 1], ic = indices[best * 3 + 2];
                var p = decal[v].Position;
                // At a receiver vertex, exactly that vertex (UE3's copy)
                Closest(p, best, out var weights);
                if (p == positions[ia]) weights = Vector3.UnitX;
                else if (p == positions[ib]) weights = Vector3.UnitY;
                else if (p == positions[ic]) weights = Vector3.UnitZ;
                places[v] = new Place(ia, ib, ic, weights);
            }
            return true;
        }

        Vector3 Closest(Vector3 p, int t, out Vector3 weights) =>
            ClosestPoint(p, positions[indices[t * 3]], positions[indices[t * 3 + 1]], positions[indices[t * 3 + 2]], out weights);
    }

    /// <summary>The point of triangle abc closest to p, and its barycentric weights (Ericson, Real-Time Collision Detection 5.1.5)</summary>
    static Vector3 ClosestPoint(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 weights)
    {
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) { weights = Vector3.UnitX; return a; }
        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) { weights = Vector3.UnitY; return b; }
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            float v = d1 / (d1 - d3);
            weights = new Vector3(1 - v, v, 0);
            return a + v * ab;
        }
        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) { weights = Vector3.UnitZ; return c; }
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            float w = d2 / (d2 - d6);
            weights = new Vector3(1 - w, 0, w);
            return a + w * ac;
        }
        float va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
        {
            float w = (d4 - d3) / (d4 - d3 + (d5 - d6));
            weights = new Vector3(0, 1 - w, w);
            return b + w * (c - b);
        }
        float denominator = 1 / (va + vb + vc);
        float bv = vb * denominator, cw = vc * denominator;
        if (!float.IsFinite(bv) || !float.IsFinite(cw)) { weights = Vector3.UnitX; return a; } // degenerate
        weights = new Vector3(1 - bv - cw, bv, cw);
        return a + ab * bv + ac * cw;
    }

    /// <summary>A mesh's triangles by the cells of a uniform grid over its bounds that their bounds overlap</summary>
    sealed class TriangleGrid
    {
        const int Cells = 32;
        readonly Vector3 min, cell;
        readonly Dictionary<(int, int, int), List<int>> triangles = new();
        /// <summary>How far a decal vertex may be from its receiver triangle, for float error</summary>
        public readonly float Tolerance;
        /// <summary>How far apart two triangles' fits of a decal triangle may be and still be the same fit, with only float error between them</summary>
        public float SameFit => Tolerance * 1e-3f;

        /// <summary>The grid of a mesh's triangles, or null if it has none or its vertex buffers don't agree</summary>
        public static TriangleGrid Of(StaticMeshRenderData mesh)
        {
            var positions = mesh.PositionVertexBuffer.VertexData;
            return positions.Length == 0 || mesh.VertexBuffer.VertexData.Length != positions.Length || mesh.IndexBuffer.Length < 3
                ? null : new TriangleGrid(positions, mesh.IndexBuffer);
        }

        TriangleGrid(Vector3[] positions, ushort[] indices)
        {
            min = positions.Aggregate(Vector3.Min);
            var size = positions.Aggregate(Vector3.Max) - min;
            Tolerance = 1e-3f + 1e-4f * MathF.Max(size.X, MathF.Max(size.Y, size.Z));
            cell = Vector3.Max(size / Cells, new Vector3(Tolerance));
            for (int t = 0; t < indices.Length / 3; t++)
            {
                if (indices[t * 3] >= positions.Length || indices[t * 3 + 1] >= positions.Length || indices[t * 3 + 2] >= positions.Length) continue;
                Vector3 a = positions[indices[t * 3]], b = positions[indices[t * 3 + 1]], c = positions[indices[t * 3 + 2]];
                var (lo, hi) = (Cell(Vector3.Min(a, Vector3.Min(b, c)) - new Vector3(Tolerance)), Cell(Vector3.Max(a, Vector3.Max(b, c)) + new Vector3(Tolerance)));
                for (int x = lo.X; x <= hi.X; x++)
                for (int y = lo.Y; y <= hi.Y; y++)
                for (int z = lo.Z; z <= hi.Z; z++)
                {
                    if (!triangles.TryGetValue((x, y, z), out var list)) triangles[(x, y, z)] = list = [];
                    list.Add(t);
                }
            }
        }

        (int X, int Y, int Z) Cell(Vector3 p)
        {
            var c = (p - min) / cell;
            return (Math.Clamp((int)MathF.Floor(c.X), 0, Cells), Math.Clamp((int)MathF.Floor(c.Y), 0, Cells), Math.Clamp((int)MathF.Floor(c.Z), 0, Cells));
        }

        /// <summary>The triangles whose bounds, grown by the tolerance, may hold p</summary>
        public IEnumerable<int> Near(Vector3 p) => triangles.TryGetValue(Cell(p), out var list) ? list : [];
    }

    // (PackedNormal has no Equals of its own, so ValueType's would box both)
    static bool Same(PackedNormal a, PackedNormal b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;

    // ================= samples =================

    static QuantizedDirectionalLightSample[] Remap(QuantizedDirectionalLightSample[] samples, Place[] places) => places.Select(p => new QuantizedDirectionalLightSample
    {
        Coefficient1 = Mix(samples, p, s => s.Coefficient1),
        Coefficient2 = Mix(samples, p, s => s.Coefficient2),
        Coefficient3 = Mix(samples, p, s => s.Coefficient3),
    }).ToArray();

    static QuantizedSimpleLightSample[] Remap(QuantizedSimpleLightSample[] samples, Place[] places) =>
        places.Select(p => new QuantizedSimpleLightSample { Coefficient = Mix(samples, p, s => s.Coefficient) }).ToArray();

    /// <summary>
    /// A quantized colour at a place: the receiver's bytes interpolated, as the GPU interpolates them across the receiver triangle before decoding them,
    /// so a clipped decal vertex has the lighting the receiver draws there
    /// </summary>
    static Color Mix<T>(T[] samples, Place p, Func<T, Color> get)
    {
        Color a = get(samples[p.A]), b = get(samples[p.B]), c = get(samples[p.C]);
        var w = p.Weights;
        if (w == Vector3.UnitX) return a;
        if (w == Vector3.UnitY) return b;
        if (w == Vector3.UnitZ) return c;
        byte Channel(byte x, byte y, byte z) => (byte)Math.Clamp(MathF.Round(x * w.X + y * w.Y + z * w.Z), 0, 255);
        return new Color(Channel(a.R, b.R, c.R), Channel(a.G, b.G, c.G), Channel(a.B, b.B, c.B), Channel(a.A, b.A, c.A));
    }

    /// <summary>A shadow sample (a float's bits) at a place</summary>
    static int Mix(int[] samples, Place p)
    {
        var w = p.Weights;
        if (w == Vector3.UnitX) return samples[p.A];
        if (w == Vector3.UnitY) return samples[p.B];
        if (w == Vector3.UnitZ) return samples[p.C];
        return BitConverter.SingleToInt32Bits(BitConverter.Int32BitsToSingle(samples[p.A]) * w.X + BitConverter.Int32BitsToSingle(samples[p.B]) * w.Y
                                             + BitConverter.Int32BitsToSingle(samples[p.C]) * w.Z);
    }
}
