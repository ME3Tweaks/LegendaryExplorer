using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Coalesced.Config;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// A primitive lit by a light environment. The light environment's bounds are those of its primitives, and its shadow is cast by all of them
/// </summary>
public sealed class LightEnvironmentPrimitive
{
    public Func<BoxSphereBounds> GetBounds;
    public Func<Matrix4x4> GetLocalToWorld;
    /// <summary>
    /// Draws the primitive's shadow-casting sections into a shadow's depth (see <see cref="LightAttenuationRenderer.DrawShadowSection{TVertex}"/>). Null if it doesn't cast shadows
    /// </summary>
    public Action<DeviceContext, ShadowDepthPass> DrawShadowCaster;
    /// <summary>
    /// Draws the primitive with its materials' FModShadowMesh shaders, to mask a LightShadow_ModulateBetter shadow off it (see
    /// <see cref="LightAttenuationRenderer.DrawModShadowReceiverSection"/>)
    /// </summary>
    public Action<DeviceContext, ModShadowReceiverPass> DrawModShadowReceiver;
    /// <summary>Where the primitive masks LightShadow_ModulateBetter shadows off itself</summary>
    public ModulatedShadowCulling ModShadowCulling;
    /// <summary>
    /// bCastShadowAsTwoSided: its one-sided sections aren't culled in the shadow's depth
    /// </summary>
    public bool CastShadowAsTwoSided;
    /// <summary>
    /// Whether the primitive is in the game's scene (its actor and component aren't hidden in game)
    /// </summary>
    public Func<bool> IsInScene = () => true;
    /// <summary>
    /// Whether the primitive is shown in the editor, so that it casts its shadow (its actor's category isn't hidden)
    /// </summary>
    public Func<bool> IsShown = () => true;
    public LightingChannels LightingChannels;
    /// <summary>bAcceptsLights and bAcceptsDynamicLights, for <see cref="LightEnvironmentLighting.DirectLights"/></summary>
    public bool AcceptsLights = true;
    public bool AcceptsDynamicLights = true;
    /// <summary>Precomputed-shadow participation and grouping, for directly drawn lights' shadows</summary>
    public bool HasStaticShadowing;
    /// <summary>Whether it's shadowed as part of its ShadowParent's group, which isn't deleted or pending deletion</summary>
    public Func<bool> HasShadowParent = () => false;
    public bool SelfShadowOnly;
    /// <summary>The primitive's component, to identify the owner's main mesh</summary>
    public ExportEntry Component;
    /// <summary>For skeletal primitives: whether the mesh has a named bone (USkeletalMeshComponent::MatchRefBone)</summary>
    public Func<string, bool> HasBone;
}
