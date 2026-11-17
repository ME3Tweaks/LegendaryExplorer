using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// Parsed static meshes, kept during a load, and immutable geometry, kept while it has instances, for one render context.
/// Component materials, lighting, transforms and deformed vertices never belong to this cache.
/// Geometry is kept per file: copies of a mesh in two files can differ, and sections and light-maps come from the file's own copy.
/// </summary>
internal sealed class StaticMeshPreviewCache
{
    private readonly Lock sync = new();
    //by export, as a parsed mesh holds its package's UIndexes, which differ between copies. It holds its export anyway, and is only kept during a load
    private readonly Dictionary<ExportEntry, StaticMesh> parsed = [];
    private bool loading;
    //not by export, which would keep a closed package alive as long as another level uses geometry it imported
    private readonly Dictionary<(string File, string Mesh, int Lod, Type Vertex), ISharedGeometry> geometry = [];
    private readonly Dictionary<(string File, string Mesh), (Mesh<WorldVertex> Mesh, string BodySetup)> collision = [];

    /// <summary>The snapshot is read-only; edits must go through the export and invalidate it.</summary>
    public StaticMesh GetMesh(ExportEntry export)
    {
        lock (sync)
        {
            if (parsed.TryGetValue(export, out var mesh)) return mesh;
            mesh = export.GetBinaryData<StaticMesh>();
            if (loading) parsed.Add(export, mesh);
            return mesh;
        }
    }

    /// <summary>Parsed meshes are large, and a load is what reads the same ones repeatedly, so they're only kept during one.</summary>
    public void SetLoading(bool isLoading)
    {
        lock (sync)
        {
            loading = isLoading;
            if (loading) return;
            parsed.Clear();
            //not before: a load may be about to reuse what it released
            ReleaseUnused();
        }
    }

    public Mesh<TVertex> GetGeometry<TVertex>(StaticMesh mesh, int lod, Func<Mesh<TVertex>> create) where TVertex : IVertexBase
    {
        lock (sync)
        {
            var key = (mesh.Export.FileRef.FilePath, mesh.Export.MemoryFullPath, lod, typeof(TVertex));
            if (!geometry.TryGetValue(key, out var owner)) geometry.Add(key, owner = create());
            return ((Mesh<TVertex>)owner).CreateSharedInstance();
        }
    }

    public Mesh<WorldVertex> GetCollision(StaticMesh mesh, Func<Mesh<WorldVertex>> create)
    {
        lock (sync)
        {
            var key = (mesh.Export.FileRef.FilePath, mesh.Export.MemoryFullPath);
            if (!collision.TryGetValue(key, out var owner))
                collision.Add(key, owner = (create(), mesh.Export.FileRef.GetEntry(mesh.BodySetup)?.MemoryFullPath));
            return owner.Mesh?.CreateSharedInstance();
        }
    }

    public void Invalidate(IMEPackage package, IReadOnlySet<int> exports) =>
        Remove(package, exports.Select(uIndex => package.GetEntry(uIndex)?.MemoryFullPath).OfType<string>().ToHashSet().Contains);

    /// <summary>Forgets a closed package's meshes, and any other package's that only it used</summary>
    public void Forget(IMEPackage package)
    {
        Remove(package, static _ => true);
        lock (sync) ReleaseUnused();
    }

    private void ReleaseUnused()
    {
        foreach (var (key, owner) in geometry.Where(entry => !entry.Value.HasInstances).ToArray())
        {
            owner.Dispose();
            geometry.Remove(key);
        }
        foreach (var (mesh, owner) in collision.Where(entry => entry.Value.Mesh?.HasInstances != true).ToArray())
        {
            owner.Mesh?.Dispose();
            collision.Remove(mesh);
        }
    }

    /// <param name="affected">Whether what depends on the package's asset at a memory path is removed</param>
    private void Remove(IMEPackage package, Func<string, bool> affected)
    {
        lock (sync)
        {
            foreach (var export in parsed.Keys.Where(e => e.FileRef == package && affected(e.MemoryFullPath)).ToArray())
                parsed.Remove(export);
            foreach (var key in geometry.Keys.Where(k => k.File == package.FilePath && affected(k.Mesh)).ToArray())
            {
                geometry[key].Dispose();
                geometry.Remove(key);
            }
            foreach (var (key, owner) in collision.Where(c => c.Key.File == package.FilePath && (affected(c.Key.Mesh) || affected(c.Value.BodySetup))).ToArray())
            {
                owner.Mesh?.Dispose();
                collision.Remove(key);
            }
        }
    }

    public void Clear()
    {
        lock (sync)
        {
            foreach (var owner in geometry.Values) owner.Dispose();
            foreach (var owner in collision.Values) owner.Mesh?.Dispose();
            geometry.Clear();
            collision.Clear();
            parsed.Clear();
        }
    }
}
