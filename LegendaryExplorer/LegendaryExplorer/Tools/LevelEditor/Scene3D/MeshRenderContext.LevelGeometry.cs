using LegendaryExplorer.Misc;
using LegendaryExplorer.Resources;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Windows.Input;
using Color = System.Windows.Media.Color;
using D2D = SharpDX.Direct2D1;
using DW = SharpDX.DirectWrite;
using LECTexture2D = LegendaryExplorerCore.Unreal.Classes.Texture2D;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public partial class MeshRenderContext
{
    private int levelGeometryVersion;
    private readonly Lock LevelGeometryLock = new();
    private LevelGeometry LevelGeometry;
    private int BuiltLevelGeometryVersion = -1;
    //how many times the geometry has been built, which light environments' cached lighting depends on
    private int LevelGeometryGeneration;
    private long LastLevelGeometryBuildTimestamp;
    //While meshes are loading, unloading or being dragged, the geometry changes every frame. It's rebuilt at most this often, so that the
    //light environments that trace against it aren't all recomputed every frame
    private static readonly TimeSpan MinLevelGeometryRebuildInterval = TimeSpan.FromSeconds(0.25);
    //The triangles of each mesh, so that its instances share them. Weak, since the instances own them: they're freed once every instance is unloaded.
    //Meshes with no triangles are only remembered by key. Both are guarded by LevelGeometryLock
    private readonly Dictionary<string, WeakReference<LevelGeometry.TriangleMesh>> LevelGeometryMeshes = [];
    private readonly HashSet<string> MeshesWithoutLevelGeometry = [];
    //each open level's BSP, which also blocks visibility traces. Guarded by LevelGeometryLock
    private readonly Dictionary<IMEPackage, Model> LevelModels = [];

    /// <summary>
    /// Sets the BSP of a level, which blocks light environments' visibility traces. Only read if light environments are supported for the game
    /// </summary>
    /// <param name="modelUIndex">The level's Model</param>
    public void SetLevelModel(IMEPackage level, int modelUIndex)
    {
        Model model = null;
        if (SupportsLightEnvironments(level.Game) && level.TryGetUExport(modelUIndex, out ExportEntry modelExport))
        {
            try
            {
                model = modelExport.GetBinaryData<Model>();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Could not read {modelExport.InstancedFullPath}, so it won't block light environments' visibility: {e.Message}");
            }
        }
        lock (LevelGeometryLock)
        {
            if (model is null)
            {
                LevelModels.Remove(level);
            }
            else
            {
                LevelModels[level] = model;
            }
        }
        InvalidateLevelGeometry();
    }

    /// <summary>
    /// Call when a static mesh that blocks light environments' visibility traces moves, so that <see cref="GetLevelGeometry"/> is rebuilt
    /// </summary>
    public void InvalidateLevelGeometry() => Interlocked.Increment(ref levelGeometryVersion);

    /// <summary>
    /// The level's geometry, for light environments' visibility traces. Rebuilt when it's needed after it changes, at most every <see cref="MinLevelGeometryRebuildInterval"/>
    /// </summary>
    /// <param name="generation">Changes whenever the returned geometry does</param>
    /// <param name="current">Rebuilt if it changed, however recently it was: for a one-off question (where a click meets the level), not a frame's</param>
    internal LevelGeometry GetLevelGeometry(out int generation, bool current = false)
    {
        lock (LevelGeometryLock)
        {
            int version = levelGeometryVersion;
            if (LevelGeometry is null || BuiltLevelGeometryVersion != version
                && (current || System.Diagnostics.Stopwatch.GetElapsedTime(LastLevelGeometryBuildTimestamp) >= MinLevelGeometryRebuildInterval))
            {
                LevelGeometry = new LevelGeometry(GetStaticLightings(), [.. LevelModels.Values]);
                BuiltLevelGeometryVersion = version;
                LevelGeometryGeneration++;
                LastLevelGeometryBuildTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                PruneLevelGeometryMeshes();
            }
            generation = LevelGeometryGeneration;
            return LevelGeometry;
        }
    }

    //forgets the meshes whose instances have all been unloaded. Call under LevelGeometryLock
    private void PruneLevelGeometryMeshes()
    {
        List<string> freed = null;
        foreach ((string key, WeakReference<LevelGeometry.TriangleMesh> mesh) in LevelGeometryMeshes)
        {
            if (!mesh.TryGetTarget(out _))
            {
                (freed ??= []).Add(key);
            }
        }
        freed?.ForEach(key => LevelGeometryMeshes.Remove(key));
    }

    /// <summary>
    /// Forget collision triangles for edited exports before rebuilding their component previews.
    /// Other instances can then share the new geometry instead of recovering the old weakly cached mesh.
    /// </summary>
    public void InvalidateLevelGeometryMeshes(IMEPackage package, IReadOnlySet<int> updatedExports)
    {
        bool changed = false;
        lock (LevelGeometryLock)
        {
            foreach (int index in updatedExports)
            {
                string key = $"{package.FilePath}|{index}";
                changed |= LevelGeometryMeshes.Remove(key);
                changed |= MeshesWithoutLevelGeometry.Remove(key);
            }
        }
        if (changed) InvalidateLevelGeometry();
    }

    /// <summary>
    /// The triangles a mesh blocks line checks with, read by <paramref name="readTriangles"/> the first time the mesh is seen. Null if it has none, or they can't be read
    /// </summary>
    /// <param name="key">Identifies the mesh</param>
    internal LevelGeometry.TriangleMesh GetLevelGeometryMesh(string key, Func<(Vector3[] Positions, int[] Indices)> readTriangles, kDOPTreeCompact collisionTree = null)
    {
        LevelGeometry.TriangleMesh mesh;
        lock (LevelGeometryLock)
        {
            if (LevelGeometryMeshes.TryGetValue(key, out WeakReference<LevelGeometry.TriangleMesh> cached) && cached.TryGetTarget(out mesh))
            {
                return mesh;
            }
            if (MeshesWithoutLevelGeometry.Contains(key))
            {
                return null;
            }
        }
        //read outside the lock, since it can be slow. Another thread may read the same mesh meanwhile; the first one stored wins
        try
        {
            (Vector3[] positions, int[] indices) = readTriangles();
            mesh = LevelGeometry.TriangleMesh.Create(positions, indices, collisionTree);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Could not read the collision of {key}, so it won't block light environments' visibility: {e.Message}");
            mesh = null;
        }
        lock (LevelGeometryLock)
        {
            if (mesh is null)
            {
                MeshesWithoutLevelGeometry.Add(key);
                return null;
            }
            if (LevelGeometryMeshes.TryGetValue(key, out WeakReference<LevelGeometry.TriangleMesh> cached) && cached.TryGetTarget(out LevelGeometry.TriangleMesh stored))
            {
                return stored;
            }
            LevelGeometryMeshes[key] = new WeakReference<LevelGeometry.TriangleMesh>(mesh);
            return mesh;
        }
    }
}
