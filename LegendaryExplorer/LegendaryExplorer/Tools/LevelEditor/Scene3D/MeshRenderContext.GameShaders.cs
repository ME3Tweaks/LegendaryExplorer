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
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using Color = System.Windows.Media.Color;
using D2D = SharpDX.Direct2D1;
using DW = SharpDX.DirectWrite;
using LECTexture2D = LegendaryExplorerCore.Unreal.Classes.Texture2D;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

public partial class MeshRenderContext
{
    //parsing a package's ShaderCache is expensive, and every material in the package needs it
    private readonly Dictionary<IMEPackage, ShaderCache> SeekFreeShaderCaches = [];

    /// <summary>
    /// Render meshes with the game's shaders. Materials that can't be rendered with them fall back to LEX's shader.
    /// Only applies to meshes made of <see cref="LEVertex"/>.
    /// </summary>
    public bool UseGameShaders { get; set; }

    //Materials whose game shaders haven't been loaded yet
    private readonly List<MaterialRenderProxy> PendingGameShaderLoads = [];

    internal void AddPendingGameShaderLoad(MaterialRenderProxy material)
    {
        lock (PendingGameShaderLoads)
        {
            PendingGameShaderLoads.Add(material);
            material.QueuedAt = Interlocked.Increment(ref shaderLoadSequence);
        }
    }

    //stamps the materials queued and the meshes registered in order, for LoadGameShadersNow to tell what came after a MarkShaderLoads
    private long shaderLoadSequence;

    internal void RemovePendingGameShaderLoad(MaterialRenderProxy material)
    {
        lock (PendingGameShaderLoads)
        {
            PendingGameShaderLoads.Remove(material);
        }
    }

    /// <summary>
    /// Whether any materials have been created whose game shaders haven't been loaded by <see cref="LoadPendingGameShaders"/>
    /// </summary>
    public bool HasPendingGameShaderLoads
    {
        get
        {
            lock (PendingGameShaderLoads)
            {
                return PendingGameShaderLoads.Count > 0;
            }
        }
    }

    /// <summary>
    /// Whether <see cref="LoadPendingGameShaders"/> has anything to do for a lighting mode
    /// </summary>
    public bool NeedsGameShaderPreparation(ViewportLightingMode mode)
    {
        if (HasPendingGameShaderLoads) return true;
        if (mode is not (ViewportLightingMode.Level or ViewportLightingMode.Preview)) return false;
        lock (StaticLightings)
        {
            return StaticLightings.Any(staticLighting => !staticLighting.IsPrepared(mode == ViewportLightingMode.Preview));
        }
    }

    /// <summary>
    /// Loads the game shaders of every material created since the last call, and creates their D3D shaders, and prepares their shadow depth shaders.
    /// Then loads the light shaders that the level's lights need (see <see cref="MeshStaticLighting.GetLightInteractions"/>).
    /// Loading them can take seconds for a whole level, so call this on a background thread before turning on <see cref="UseGameShaders"/>,
    /// and after adding a level's lights. Otherwise shaders are loaded the first time they're needed.
    /// </summary>
    /// <param name="reportProgress">Called with the number of items loaded so far, and the total</param>
    /// <param name="lightingModeToPrepare">Load light shaders for this mode before switching to it. Defaults to the current mode.</param>
    /// <exception cref="AggregateException">Some materials' game shaders failed to load, one inner exception each. Everything else was still done,
    /// and those materials are retried when they're rendered, falling back to LEX's shader</exception>
    public void LoadPendingGameShaders(Action<int, int> reportProgress = null, ViewportLightingMode? lightingModeToPrepare = null)
    {
        MaterialRenderProxy[] materials;
        lock (PendingGameShaderLoads)
        {
            materials = [.. PendingGameShaderLoads];
            PendingGameShaderLoads.Clear();
        }
        LoadGameShaders(materials, GetStaticLightings(), lightingModeToPrepare ?? LightingMode, reportProgress);
    }

    /// <summary>A moment, for <see cref="LoadGameShadersNow"/> to load what was queued for <see cref="LoadPendingGameShaders"/> after it</summary>
    public long MarkShaderLoads() => Interlocked.Read(ref shaderLoadSequence);

    /// <summary>
    /// Loads now, on the calling thread, what's been queued for <see cref="LoadPendingGameShaders"/> since <paramref name="mark"/>: the game shaders of the
    /// materials made since, and the light shaders of the meshes registered since, for the current lighting mode. For meshes made while a drag holds the
    /// window's preparation batch back, which would be drawn with LEX's shader until it ends: an Alt+drag's copy, whose shaders are its source's, so are
    /// mostly found in the caches
    /// </summary>
    /// <exception cref="AggregateException">As <see cref="LoadPendingGameShaders"/> throws it</exception>
    public void LoadGameShadersNow(long mark)
    {
        MaterialRenderProxy[] materials;
        lock (PendingGameShaderLoads)
        {
            materials = [.. PendingGameShaderLoads.Where(material => material.QueuedAt > mark)];
            PendingGameShaderLoads.RemoveAll(material => material.QueuedAt > mark);
        }
        MeshStaticLighting[] registered;
        lock (StaticLightings)
        {
            registered = [.. StaticLightings.Where(lighting => lighting.RegisteredAt > mark)];
        }
        LoadGameShaders(materials, registered, LightingMode, null);
    }

    //the materials, off the queue, and the meshes' light shaders for the mode
    private void LoadGameShaders(MaterialRenderProxy[] materials, MeshStaticLighting[] staticLightings, ViewportLightingMode mode, Action<int, int> reportProgress)
    {
        try
        {
            if (mode is not (ViewportLightingMode.Level or ViewportLightingMode.Preview)) staticLightings = [];
            int total = materials.Length + staticLightings.Length;
            //one material failing doesn't stop the others, which were taken off the queue: its failure is reported after them
            List<Exception> failures = null;
            for (int i = 0; i < materials.Length; i++)
            {
                MaterialRenderProxy material = materials[i];
                try
                {
                    material.LoadGameShaders();
                    if (Device is not null && material.CanRenderWithGameShaders)
                    {
                        GetCachedVertexShader(material.UnrealVertexShader.Guid, material.UnrealVertexShader.ShaderByteCode, material.VertexInputElements);
                        GetCachedPixelShader(material.UnrealPixelShader.Guid, material.UnrealPixelShader.ShaderByteCode);
                        if (material.NoLightMapVertexShader is not null)
                        {
                            GetCachedVertexShader(material.NoLightMapVertexShader.Guid, material.NoLightMapVertexShader.ShaderByteCode, material.VertexInputElements);
                            GetCachedPixelShader(material.NoLightMapPixelShader.Guid, material.NoLightMapPixelShader.ShaderByteCode);
                        }
                        if (material.PreviewVertexShader is not null)
                        {
                            GetCachedVertexShader(material.PreviewVertexShader.Guid, material.PreviewVertexShader.ShaderByteCode, material.VertexInputElements);
                            GetCachedPixelShader(material.PreviewPixelShader.Guid, material.PreviewPixelShader.ShaderByteCode);
                        }
                    }
                }
                catch (Exception e)
                {
                    //usually creating its D3D shaders failed, after its shadow shaders were prepared. (If loading them threw, they stay pending until they're tried again, when it's drawn)
                    (failures ??= []).Add(new Exception($"{material.InstancedFullPath}: {e.Message}", e));
                }
                reportProgress?.Invoke(i + 1, total);
            }
            for (int i = 0; i < staticLightings.Length; i++)
            {
                staticLightings[i].PrepareLightShaders(mode == ViewportLightingMode.Preview);
                reportProgress?.Invoke(materials.Length + i + 1, total);
            }
            if (failures is not null)
            {
                throw new AggregateException("Some materials couldn't load their game shaders", failures);
            }
        }
        finally
        {
            // The snapshot is no longer in the queue. Release every material even if progress reporting or light preparation throws.
            foreach (MaterialRenderProxy material in materials) material.Dequeued();
        }
    }

    /// <summary>
    /// Gets the parsed SeekFreeShaderCache of a package, or null if it doesn't have one. Results are cached until <see cref="EmptyCaches"/>.
    /// </summary>
    public ShaderCache GetSeekFreeShaderCache(IMEPackage pcc)
    {
        lock (SeekFreeShaderCaches)
        {
            if (!SeekFreeShaderCaches.TryGetValue(pcc, out ShaderCache shaderCache))
            {
                if (pcc.FindExport("SeekFreeShaderCache", "ShaderCache") is { } seekFreeShaderCacheExport)
                {
                    shaderCache = ObjectBinary.From<ShaderCache>(seekFreeShaderCacheExport);
                }
                SeekFreeShaderCaches.Add(pcc, shaderCache);
            }
            return shaderCache;
        }
    }
}
