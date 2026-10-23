using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>
/// World-space geometry of one LOD of a StaticMeshComponent as LE3 renders it: positions, the per-vertex
/// tangent frame the lightmap shaders work in, and the lightmap UV channel.
/// </summary>
public sealed class ComponentGeometry
{
    public ExportEntry Component { get; private init; }
    public ExportEntry Mesh { get; private init; }
    public Matrix4x4 LocalToWorld { get; private init; }
    /// <summary>Mesh UV channel used for lightmaps (StaticMesh.LightMapCoordinateIndex), or -1 when the mesh lacks it.</summary>
    public int LightMapCoordinateIndex { get; private init; }

    public Vector3[] Positions { get; private init; }
    /// <summary>World-space tangent frame per vertex (normalised): X = TangentX, Y = binormal, Z = normal.</summary>
    public Vector3[] TangentX { get; private init; }
    public Vector3[] TangentY { get; private init; }
    public Vector3[] TangentZ { get; private init; }
    public Vector2[] LightMapUVs { get; private init; }
    public int[] Indices { get; private init; }

    public int TriangleCount => Indices.Length / 3;

    /// <summary>Rotates a tangent-space vector at vertex <paramref name="v"/> into world space.</summary>
    public Vector3 TangentToWorld(int v, Vector3 t) => t.X * TangentX[v] + t.Y * TangentY[v] + t.Z * TangentZ[v];

    public Vector3 WorldToTangent(int v, Vector3 w) => new(Vector3.Dot(w, TangentX[v]), Vector3.Dot(w, TangentY[v]), Vector3.Dot(w, TangentZ[v]));

    /// <summary>
    /// StaticMesh.LightMapCoordinateIndex. It is serialized only when it differs from the class default, which is 0:
    /// LE3's UStaticMesh intrinsic-defaults function (RVA 0x7d3610, passed to UClass::Constructor by
    /// GetUStaticMeshStaticClass) zeroes +0x98 with one 8-byte store, i.e. LightMapResolution and LightMapCoordinateIndex,
    /// as IB's UStaticMesh::InitializeIntrinsicPropertyValues does. LE3 doesn't clamp it: the vertex factory binds the
    /// lightmap UV stream only when 0 &lt;= index &lt; NumTexCoords (RVA 0x7cf480), so an out-of-range index means no
    /// lightmap UVs. Game data agrees: texture lightmaps exist on one-UV meshes that don't serialize the index.
    /// </summary>
    public static int CoordinateIndex(ExportEntry meshExport) =>
        meshExport.GetCondensedProperties().GetProp<IntProperty>("LightMapCoordinateIndex")?.Value ?? 0;

    /// <summary>Loads one LOD (LOD0 by default) of a StaticMeshComponent, or returns null (with a reason) when it can't be used.</summary>
    public static ComponentGeometry Load(ExportEntry component, MeshResolver meshes, out string skipReason, int lodIndex = 0)
    {
        skipReason = null;
        var props = component.GetCondensedProperties();
        if (props.GetProp<ObjectProperty>("StaticMesh") is not { Value: not 0 } meshRef)
        {
            skipReason = "no StaticMesh";
            return null;
        }
        var (meshExport, mesh) = meshes.Get(component.FileRef.GetEntry(meshRef.Value));
        if (mesh == null)
        {
            skipReason = "StaticMesh import not resolved";
            return null;
        }
        if (mesh.LODModels.Length <= lodIndex)
        {
            skipReason = lodIndex == 0 ? "mesh has no LODs" : $"mesh has no LOD{lodIndex}";
            return null;
        }
        var lod = mesh.LODModels[lodIndex];
        // Vertex lightmaps don't use UVs, so a mesh without a lightmap UV channel is still usable; its UVs are left zero.
        int lmIndex = CoordinateIndex(meshExport);
        bool hasLightMapUVs = lmIndex < lod.VertexBuffer.NumTexCoords;

        var l2w = WorldTransforms.Component(component);
        int n = lod.PositionVertexBuffer.VertexData.Length;
        var positions = new Vector3[n];
        var tx = new Vector3[n]; var ty = new Vector3[n]; var tz = new Vector3[n];
        var uvs = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            positions[i] = Vector3.Transform(lod.PositionVertexBuffer.VertexData[i], l2w);
            var vtx = lod.VertexBuffer.VertexData[i];
            // UE3 CalcTangentBasis: TangentY = cross(TangentZ, TangentX) * TangentZ.w, built in local space,
            // then all three axes go through LocalToWorld (LocalVertexFactory.usf).
            var x = (Vector3)vtx.TangentX;
            var z4 = (Vector4)vtx.TangentZ;
            var z = new Vector3(z4.X, z4.Y, z4.Z);
            var y = Vector3.Cross(z, x) * (z4.W < 0 ? -1f : 1f);
            tx[i] = SafeNormalize(Vector3.TransformNormal(x, l2w));
            ty[i] = SafeNormalize(Vector3.TransformNormal(y, l2w));
            tz[i] = SafeNormalize(Vector3.TransformNormal(z, l2w));
            if (hasLightMapUVs)
                uvs[i] = lod.VertexBuffer.bUseFullPrecisionUVs
                    ? vtx.FullPrecisionUVs[lmIndex]
                    : new Vector2(vtx.HalfPrecisionUVs[lmIndex].X, vtx.HalfPrecisionUVs[lmIndex].Y);
        }

        return new ComponentGeometry
        {
            Component = component,
            Mesh = meshExport,
            LocalToWorld = l2w,
            LightMapCoordinateIndex = hasLightMapUVs ? lmIndex : -1,
            Positions = positions,
            TangentX = tx, TangentY = ty, TangentZ = tz,
            LightMapUVs = uvs,
            Indices = lod.IndexBuffer.Select(i => (int)i).ToArray(),
        };
    }

    public (int A, int B, int C) Triangle(int t) => (Indices[t * 3], Indices[t * 3 + 1], Indices[t * 3 + 2]);

    /// <summary>Geometric normal of a triangle in world space, oriented to agree with its vertex normals.</summary>
    public Vector3 FaceNormal(int t)
    {
        var (a, b, c) = Triangle(t);
        var n = SafeNormalize(Vector3.Cross(Positions[b] - Positions[a], Positions[c] - Positions[a]));
        return Vector3.Dot(n, TangentZ[a] + TangentZ[b] + TangentZ[c]) < 0 ? -n : n;
    }

    public (Vector3 Min, Vector3 Max) Bounds()
    {
        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        foreach (var p in Positions) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
        return (min, max);
    }

    static Vector3 SafeNormalize(Vector3 v) => v.LengthSquared() > 1e-20f ? Vector3.Normalize(v) : Vector3.Zero;
}

/// <summary>
/// Loads StaticMesh binaries by reference, following imports into the other packages of the game (cooked levels
/// often reference meshes that live in a sibling BioD/BioP package). Decoded meshes are cached by full path.
/// </summary>
public sealed class MeshResolver : IDisposable
{
    readonly PackageCache packages = new();
    readonly string[] localFiles;
    readonly Dictionary<string, (ExportEntry, StaticMesh)> meshes = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> failures = new(StringComparer.OrdinalIgnoreCase);

    public MeshResolver(IMEPackage package)
    {
        string dir = Path.GetDirectoryName(package.FilePath);
        localFiles = dir != null ? Directory.GetFiles(dir, "*.pcc") : [];
    }

    public (ExportEntry Export, StaticMesh Mesh) Get(IEntry entry)
    {
        if (entry == null) return (null, null);
        string key = entry.InstancedFullPath;
        if (meshes.TryGetValue(key, out var found)) return found;
        var export = entry as ExportEntry;
        if (entry is ImportEntry import)
        {
            try { export = EntryImporter.ResolveImport(import, packages, localDirFiles: localFiles); }
            catch (Exception e)
            {
                failures[key] = e.GetType().Name;
                export = null;
            }
        }
        var result = export?.ClassName == "StaticMesh" ? (export, ObjectBinary.From<StaticMesh>(export)) : (null, null);
        meshes[key] = result;
        return result;
    }

    /// <summary>The exception type that stopped an import passed to <see cref="Get"/> being followed, if it threw.</summary>
    public string Failure(IEntry entry) => entry != null && failures.TryGetValue(entry.InstancedFullPath, out var why) ? why : null;

    public void Dispose() => packages.Dispose();
}

/// <summary>World transforms of level components, composed the way LEX's level editor does.</summary>
public static class WorldTransforms
{
    /// <summary>
    /// A component's own Translation/Rotation/Scale/Scale3D, composed onto its parent: the per-component matrix
    /// of a Static*CollectionActor, or the owning actor's transform.
    /// </summary>
    public static Matrix4x4 Component(ExportEntry component)
    {
        var p = component.GetCondensedProperties();
        var t = p.GetProp<StructProperty>("Translation") is { } tp ? CommonStructs.GetVector3(tp) : Vector3.Zero;
        var r = p.GetProp<StructProperty>("Rotation") is { } rp ? CommonStructs.GetRotator(rp) : new Rotator(0, 0, 0);
        var s3 = p.GetProp<StructProperty>("Scale3D") is { } sp ? CommonStructs.GetVector3(sp) : Vector3.One;
        float s = p.GetProp<FloatProperty>("Scale")?.Value ?? 1f;
        return ActorUtils.ComposeLocalToWorld(t, r, s * s3) * Parent(component);
    }

    static Matrix4x4 Parent(ExportEntry component)
    {
        if (component.Parent is not ExportEntry parent) return Matrix4x4.Identity;
        if (parent.ClassName is "StaticMeshCollectionActor" or "StaticLightCollectionActor")
        {
            StaticCollectionActor sca = parent.ClassName == "StaticMeshCollectionActor"
                ? ObjectBinary.From<StaticMeshCollectionActor>(parent)
                : ObjectBinary.From<StaticLightCollectionActor>(parent);
            int i = sca.Components.FindIndex(u => u == component.UIndex);
            return i >= 0 ? sca.LocalToWorldTransforms[i] : Matrix4x4.Identity;
        }
        return ActorUtils.GetLocalToWorld(parent);
    }
}
