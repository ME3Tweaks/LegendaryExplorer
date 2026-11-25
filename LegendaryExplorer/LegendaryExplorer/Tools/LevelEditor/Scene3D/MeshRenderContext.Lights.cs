using LegendaryExplorer.Lightmaps.Lightmass;
using LegendaryExplorer.Resources;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
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
    private readonly Lock LightsLock = new();
    //replaced rather than modified, so it can be read without locking
    private SceneLight[] Lights = [];
    private int LightsVersion;
    //replaced under LightsLock rather than modified, like Lights
    private LightAssignmentHistory assignmentHistory = LightAssignmentHistory.Empty;
    internal LightAssignmentHistory AssignmentHistory => Volatile.Read(ref assignmentHistory);
    private LightAssignmentPolicy assignmentPolicy = LightAssignmentPolicy.RecomputeAssignments;
    public LightAssignmentPolicy AssignmentPolicy
    {
        get => assignmentPolicy;
        set
        {
            if (assignmentPolicy == value) return;
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            lock (LightsLock)
            {
                assignmentPolicy = value;
                LightsVersion++;
            }
            // Preparation will run on the editor's background worker before rendering resumes.
            lock (StaticLightings)
                foreach (var lighting in StaticLightings) lighting.InvalidatePreviewPreparation();
        }
    }

    /// <summary>Whether ForgetLevel dropped the level since it was last opened, and how many times either happened</summary>
    private sealed class LevelCaptureState
    {
        public bool Forgotten;
        public int Generation;
    }

    //per level (weakly, so closed levels aren't kept alive), so that a capture that was reading a level that was closed, or closed and reopened,
    //doesn't add the old records back; and how many times ClearLights dropped every level. Guarded by LightsLock
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<IMEPackage, LevelCaptureState> LevelCaptures = new();
    private int LightsClears;

    /// <summary>
    /// Records the level's light assignments as they are now, unless it has them already
    /// </summary>
    /// <param name="opening">The level is being opened, so it's captured even if it was forgotten before (an open package can be reopened)</param>
    internal void CaptureAssignments(IMEPackage package, bool opening = false)
    {
        int clears, generation;
        LevelCaptureState state;
        lock (LightsLock)
        {
            state = LevelCaptures.GetValue(package, static _ => new LevelCaptureState());
            if (opening && state.Forgotten)
            {
                state.Forgotten = false;
                state.Generation++;
            }
            if (assignmentHistory.Contains(package) || state.Forgotten) return;
            (clears, generation) = (LightsClears, state.Generation);
        }
        //parsing the package's primitives takes a while, so it's done outside the lock
        var records = LightAssignmentHistory.Read(package);
        lock (LightsLock)
        {
            //closed (and maybe reopened) meanwhile, or captured by another thread
            if (clears != LightsClears || state.Generation != generation || assignmentHistory.Contains(package)) return;
            Volatile.Write(ref assignmentHistory, assignmentHistory.With(records));
            LightsVersion++;
        }
    }

    /// <summary>
    /// Select level lighting, a neutral preview light, or material colors without lighting.
    /// </summary>
    public ViewportLightingMode LightingMode { get; set; } = ViewportLightingMode.Level;

    public bool UseDynamicLighting { get; set; } = true;
    public bool UseLightMaps { get; set; } = true;
    public bool IsPreviewLighting => LightingMode == ViewportLightingMode.Preview;
    public bool UsesLevelLighting => LightingMode is ViewportLightingMode.Level or ViewportLightingMode.Preview;
    public bool IsUnlit => LightingMode == ViewportLightingMode.Unlit;
    public bool AreLightMapsActive => LightingMode == ViewportLightingMode.Level && UseLightMaps;
    public bool IsDynamicLightingActive => LightingMode is ViewportLightingMode.Neutral or ViewportLightingMode.Preview
        || (LightingMode == ViewportLightingMode.Level && UseDynamicLighting);

    /// <summary>
    /// Whether the level has lights to render in additive passes.
    /// </summary>
    public bool IsLevelLightingActive => UsesLevelLighting && (IsPreviewLighting || UseDynamicLighting) && Lights.Length > 0;

    /// <summary>
    /// The level lights in the scene, and a number that changes whenever they do. Thread-safe
    /// </summary>
    public SceneLight[] GetLights(out int version)
    {
        lock (LightsLock)
        {
            version = LightsVersion;
            return Lights;
        }
    }

    /// <summary>
    /// Adds lights from a level. Thread-safe
    /// </summary>
    public void AddLights(IEnumerable<SceneLight> lights)
    {
        lock (LightsLock)
        {
            Lights = [.. Lights, .. lights];
            LightsVersion++;
        }
    }

    /// <summary>
    /// Thread-safe
    /// </summary>
    public void ClearLights()
    {
        lock (LightsLock)
        {
            Lights = [];
            Volatile.Write(ref assignmentHistory, LightAssignmentHistory.Empty);
            LightsClears++;
            LightsVersion++;
        }
    }

    /// <summary>
    /// Thread-safe
    /// </summary>
    public void RemoveLights(IEnumerable<SceneLight> lights)
    {
        var toRemove = new HashSet<SceneLight>(lights);
        lock (LightsLock)
        {
            Lights = Lights.Where(light => !toRemove.Contains(light)).ToArray();
            LightsVersion++;
        }
    }

    /// <summary>
    /// Replaces one level's lights atomically, invalidating mesh interactions and light environments once.
    /// Other open levels keep their lights. Thread-safe.
    /// </summary>
    public void ReplaceLights(IEnumerable<SceneLight> previous, IEnumerable<SceneLight> replacement)
    {
        var toRemove = new HashSet<SceneLight>(previous);
        SceneLight[] newLights = replacement.ToArray();
        lock (LightsLock)
        {
            Lights = [.. Lights.Where(light => !toRemove.Contains(light)), .. newLights];
            LightsVersion++;
        }
    }

    internal readonly LightAttenuationRenderer LightAttenuations;

    //light passes queued during RenderPass.Lighting, drawn by EndLightingPass
    private readonly List<(SceneLight Light, Action<ShaderResourceView> Draw)> PendingLightPasses = [];

    /// <summary>
    /// For <see cref="RenderPass.Lighting"/>: queues a light pass, which <see cref="EndLightingPass"/> draws with the light's attenuation (null if it has none).
    /// The draw sets up all its own render state
    /// </summary>
    internal void QueueLightPass(SceneLight light, Action<ShaderResourceView> draw)
    {
        ShaderFlags tint = RenderFlags & SelectionTintFlags;
        PendingLightPasses.Add((light, attenuation => DrawTinted(tint, () => draw(attenuation))));
    }

    //a queued pass is drawn after its mesh's flags are cleared, so it's drawn with the selection tint it was queued with
    private void DrawTinted(ShaderFlags tint, Action draw)
    {
        ShaderFlags flags = RenderFlags;
        RenderFlags = (flags & ~SelectionTintFlags) | tint;
        try
        {
            draw();
        }
        finally
        {
            RenderFlags = flags;
        }
    }

    /// <summary>Draw a decal's light immediately after its base pass, before the next sorted decal.
    /// Attenuation uses the opaque receiver's scene depth; decals never write depth or cast shadows.</summary>
    internal void DrawDecalLightPass(SceneLight light, Action<ShaderResourceView> draw) => draw(GetLightAttenuation(light));

    //light passes that the modulated shadows don't darken, drawn after them by EndLightingPass
    private readonly List<Action> PendingPostModulatedShadowPasses = [];

    /// <summary>
    /// Queues a light pass to be drawn by <see cref="EndLightingPass"/> after the modulated shadows, so that they don't darken it. Only call if
    /// <see cref="AreModulatedShadowsActive"/>. The draw sets up all its own render state
    /// </summary>
    internal void QueuePostModulatedShadowPass(Action draw)
    {
        ShaderFlags tint = RenderFlags & SelectionTintFlags;
        PendingPostModulatedShadowPasses.Add(() => DrawTinted(tint, draw));
    }

    /// <summary>
    /// Whether light environments' modulated shadows are rendered (by <see cref="EndLightingPass"/>)
    /// </summary>
    internal bool AreModulatedShadowsActive => UseGameShaders && IsLevelLightingActive && DepthBufferResourceView is not null && !Camera.IsOrthographic && !Wireframe
                                               && !ModulatedShadowsFailed;

    /// <summary>
    /// Call after <see cref="RenderPass.Lighting"/>, before <see cref="RenderPass.Translucent"/>. Draws the queued light passes grouped by light, as UE3 does,
    /// so each light's dynamic shadows and light function are rendered once, then the light environments' modulated shadows, then the passes they don't darken.
    /// The scene's render targets are restored.
    /// </summary>
    public void EndLightingPass()
    {
        try
        {
            //GroupBy keeps the order lights were first queued in, and each light's passes in order
            foreach (IGrouping<SceneLight, (SceneLight Light, Action<ShaderResourceView> Draw)> lightPasses in PendingLightPasses.GroupBy(pass => pass.Light))
            {
                ShaderResourceView attenuation = GetLightAttenuation(lightPasses.Key);
                foreach ((_, Action<ShaderResourceView> draw) in lightPasses)
                {
                    draw(attenuation);
                }
            }
            RenderModulatedShadows();
            foreach (Action draw in PendingPostModulatedShadowPasses)
            {
                draw();
            }
        }
        finally
        {
            PendingLightPasses.Clear();
            PendingPostModulatedShadowPasses.Clear();
        }
    }

    /// <summary>
    /// Renders the light's dynamic shadows and light function into a texture of its attenuation across the screen. Changes the render state, then restores
    /// the scene's render targets and default states
    /// </summary>
    /// <returns>Null if the light has neither, or they can't be rendered. Only valid until the next light's is rendered</returns>
    private ShaderResourceView GetLightAttenuation(SceneLight light)
    {
        //positions are reconstructed from the scene depth with a perspective projection
        if (!LightAttenuationRenderer.MayHaveAttenuation(light, IsPreviewLighting) || DepthBufferResourceView is null || Camera.IsOrthographic)
        {
            return null;
        }
        try
        {
            return LightAttenuations.GetAttenuation(light, DepthBufferResourceView);
        }
        catch (Exception e)
        {
            //(the renderer won't retry it)
            System.Diagnostics.Debug.WriteLine($"Could not render the attenuation of {light.Export.InstancedFullPath}: {e.Message}");
            return null;
        }
        finally
        {
            RestoreSceneRenderState();
        }
    }

    private static readonly ShaderResourceView[] NoShaderResources = new ShaderResourceView[CommonShaderStage.InputResourceSlotCount];

    /// <summary>
    /// Restores what rendering lights' attenuations or modulated shadows changes, even if it failed partway:
    /// the scene's render targets and viewport, and the default depth, blend, rasterizer and sampler states
    /// </summary>
    private void RestoreSceneRenderState()
    {
        ImmediateContext.PixelShader.SetShaderResources(0, NoShaderResources);
        LEEffect.PixelShaderResources.Reset();
        ImmediateContext.OutputMerger.SetRenderTargets(DepthBufferView, SceneColorView, HitBufferView);
        ImmediateContext.Rasterizer.SetViewport(0, 0, Width, Height);
        ImmediateContext.OutputMerger.SetDepthStencilState(DefaultDepthState);
        ImmediateContext.OutputMerger.SetBlendState(null);
        RestoreRasterizerState();
        RestoreDefaultSamplers();
    }

    private bool ModulatedShadowsFailed;

    /// <summary>
    /// For the modulated shadows of light environments: multiplies them into the scene color. The scene's render targets are restored.
    /// </summary>
    private void RenderModulatedShadows()
    {
        if (!AreModulatedShadowsActive)
        {
            return;
        }
        DynamicLightEnvironment[] lightEnvironments = GetLightEnvironments();
        if (lightEnvironments.Length == 0)
        {
            return;
        }
        try
        {
            LightAttenuations.RenderModulatedShadows(lightEnvironments, DepthBufferResourceView, SceneColorView);
        }
        catch (Exception e)
        {
            //not retried every frame. EmptyCaches and ForgetLevel reset it, for when the level changes
            ModulatedShadowsFailed = true;
            System.Diagnostics.Debug.WriteLine($"Could not render light environment shadows: {e.Message}");
        }
        finally
        {
            RestoreSceneRenderState();
        }
    }

    /// <summary>
    /// Forgets the lights' light function materials and failed attenuations, so they're reloaded. For when a package edit may have changed them
    /// </summary>
    public void InvalidateLightFunctions()
    {
        LightAttenuations.ClearMaterials();
    }

    //every mesh with static lighting, so their light shaders can be loaded ahead of time
    private readonly HashSet<MeshStaticLighting> StaticLightings = [];

    internal void RegisterStaticLighting(MeshStaticLighting staticLighting)
    {
        lock (StaticLightings)
        {
            StaticLightings.Add(staticLighting);
            staticLighting.RegisteredAt = Interlocked.Increment(ref shaderLoadSequence);
        }
    }

    internal MeshStaticLighting[] GetStaticLightings()
    {
        lock (StaticLightings)
        {
            return [.. StaticLightings];
        }
    }

    internal void UnregisterStaticLighting(MeshStaticLighting staticLighting)
    {
        lock (StaticLightings)
        {
            StaticLightings.Remove(staticLighting);
        }
        if (staticLighting.GeometryMesh is not null)
        {
            //it no longer blocks visibility. (The old geometry keeps it alive until it's rebuilt, or the level is forgotten)
            InvalidateLevelGeometry();
        }
    }
}
