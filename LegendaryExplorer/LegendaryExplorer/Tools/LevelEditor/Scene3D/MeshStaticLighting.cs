using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// How a light's shadows on a primitive were precomputed. Determines which of the game's light shaders render it (the light's shadowing policy)
/// </summary>
public enum StaticShadowingType
{
    /// <summary>
    /// Not precomputed: the light is fully dynamic for this primitive. (FNoStaticShadowingPolicy)
    /// </summary>
    None,
    /// <summary>
    /// A texture of how much of the light reaches each texel (FShadowTexturePolicy)
    /// </summary>
    ShadowTexture,
    /// <summary>
    /// A texture of the distance to the shadow's edge (FSignedDistanceFieldShadowTexturePolicy)
    /// </summary>
    DistanceFieldShadowTexture,
    /// <summary>
    /// How much of the light reaches each vertex (FShadowVertexBufferPolicy)
    /// </summary>
    ShadowVertexBuffer,
}

/// <summary>
/// A light that's rendered on a primitive in its own pass, and how its shadows were precomputed. (UE3's FLightInteraction, minus the light-mapped and irrelevant kinds)
/// </summary>
/// <param name="ShadowMap">For <see cref="StaticShadowingType.ShadowTexture"/> and <see cref="StaticShadowingType.DistanceFieldShadowTexture"/></param>
/// <param name="ShadowVertexBuffer">For <see cref="StaticShadowingType.ShadowVertexBuffer"/>. Bound at <see cref="MeshStaticLighting.ShadowVertexStreamSlot"/></param>
public readonly record struct LightInteraction(SceneLight Light, StaticShadowingType Shadowing, MeshStaticLighting.ShadowMapTexture ShadowMap, Buffer ShadowVertexBuffer);

/// <summary>
/// Hemispherical ambient light from the level's sky lights, in linear color: <paramref name="Upper"/> from above (world +Z), <paramref name="Lower"/> from below
/// </summary>
public readonly record struct SkyLighting(Vector3 Upper, Vector3 Lower)
{
    public bool IsBlack => Upper == Vector3.Zero && Lower == Vector3.Zero;
}

/// <summary>
/// The precomputed lighting of one LOD of a static mesh component, in the form the game's shaders read it:
/// <list type="bullet">
/// <item>Its light-map, the baked lighting the base pass renders. Texture light-maps are atlases sampled with the mesh's light-map UVs; vertex light-maps have one sample per vertex.</item>
/// <item>Its shadow maps: the precomputed shadows of lights that are rendered dynamically, one pass per light (see <see cref="GetLightInteractions"/>).</item>
/// </list>
/// </summary>
public sealed class MeshStaticLighting : IDisposable
{
    /// <summary>
    /// The second vertex stream: the light-map UV (texture light-maps and shadow maps) and the light-map samples (vertex light-maps).
    /// Matches FLocalVertexFactory's LightMapCoordinate (COLOR0), LightMapA (TEXCOORD5) and LightMapB (TEXCOORD6) inputs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct StaticLightingVertex
    {
        public Vector2 LightMapCoordinate;
        //In the order the game stores them (B, G, R, A). The game binds these as R8G8B8A8, and its shaders swizzle them back (LightMapA.zyx)
        public uint SampleA;
        public uint SampleB;
    }

    public const int VertexStreamSlot = 1;
    public static readonly int VertexStreamStride = Marshal.SizeOf<StaticLightingVertex>();

    /// <summary>
    /// The third vertex stream, for a light with a <see cref="StaticShadowingType.ShadowVertexBuffer"/>: one float per vertex
    /// </summary>
    public const int ShadowVertexStreamSlot = 2;
    public const int ShadowVertexStreamStride = sizeof(float);

    public static readonly InputElement[] InputElements =
    [
        new InputElement("COLOR", 0, Format.R32G32_Float, 0, VertexStreamSlot),
        new InputElement("TEXCOORD", 5, Format.R8G8B8A8_UNorm, 8, VertexStreamSlot),
        new InputElement("TEXCOORD", 6, Format.R8G8B8A8_UNorm, 12, VertexStreamSlot),
        new InputElement("BLENDWEIGHT", 0, Format.R32_Float, 0, ShadowVertexStreamSlot),
    ];

    /// <summary>
    /// LMT_None if the mesh has no light-map (or it can't be used)
    /// </summary>
    public ELightMapType LightMapType { get; private set; }

    /// <summary>
    /// For texture light-maps
    /// </summary>
    public PreviewTextureCache.TextureEntry[] Textures { get; private set; } = [];

    /// <summary>
    /// For texture light-maps: where this component's light-map is in the atlas. (CoordinateScale.X, CoordinateScale.Y, CoordinateBias.Y, CoordinateBias.X), as UE3 passes it
    /// </summary>
    public Vector4 CoordinateScaleBias { get; private set; }

    /// <summary>
    /// Decodes the light-map: sample * Scale + Bias. One per texture, or per sample coefficient for vertex light-maps.
    /// </summary>
    public Vector4[] Scales { get; private set; } = [];

    /// <summary>
    /// Only used by BioWare's light-map types (LMT_4 and LMT_6)
    /// </summary>
    public Vector4[] Biases { get; private set; } = [];

    /// <summary>
    /// Bound at <see cref="VertexStreamSlot"/>, with <see cref="VertexStreamStride"/>. Null if the mesh has neither a light-map UV channel nor a vertex light-map
    /// </summary>
    public Buffer VertexStream { get; private set; }

    public bool IsVertexLightMap => LightMapType is ELightMapType.LMT_1D or ELightMapType.LMT_3 or ELightMapType.LMT_5;

    /// <summary>
    /// A precomputed shadow texture for one light
    /// </summary>
    /// <param name="CoordinateScaleBias">Where this component's shadows are in the texture, in the same form as <see cref="MeshStaticLighting.CoordinateScaleBias"/></param>
    public sealed record ShadowMapTexture(Guid LightGuid, PreviewTextureCache.TextureEntry Texture, Vector4 CoordinateScaleBias, bool IsDistanceField);

    private readonly record struct ShadowVertexBufferEntry(Guid LightGuid, Buffer Buffer);

    private readonly List<ShadowMapTexture> ShadowMaps = [];
    private readonly List<ShadowVertexBufferEntry> ShadowVertexBuffers = [];
    //lights baked into the light-map, by their LightmapGuid
    private readonly HashSet<Guid> LightMapLightGuids = [];
    //lights that the lighting build found don't reach this primitive
    private readonly HashSet<Guid> IrrelevantLights = [];
    private LightingChannels LightingChannels;
    //bAcceptsLights: if false, no light affects the primitive
    private bool AcceptsLights = true;
    //bAcceptsDynamicLights: if false, only lights with static shadowing affect the primitive
    private bool AcceptsDynamicLights = true;
    //the level (package) the primitive is in, and whether its lighting was built without other levels' lights (PKG_SelfContainedLighting)
    private string LevelName;
    private bool HasSelfContainedLighting;
    private PrimitiveLightAssignments SourceAssignments = PrimitiveLightAssignments.Empty;
    public bool HasCompatibleBakeMapping { get; private set; }

    /// <summary>
    /// The component is lit by a light environment (as dynamic actors are), which the level's lights don't affect directly
    /// </summary>
    public bool UsesLightEnvironment { get; private set; }

    /// <summary>
    /// The light environment that lights the component, if <see cref="UsesLightEnvironment"/>. Null if light environments aren't supported for the game
    /// </summary>
    public DynamicLightEnvironment LightEnvironment { get; internal set; }

    /// <summary>Why the component's light environment isn't rendered, if it <see cref="UsesLightEnvironment"/> but has none</summary>
    public string UnsupportedLightEnvironmentReason { get; private set; }

    /// <summary>
    /// Whether the component casts shadows (CastShadow)
    /// </summary>
    private bool CastsShadow;
    internal ShadowParticipation ShadowSettings { get; private set; }
    private bool HasNonWhiteVertexColors;
    private bool HasShadowParent;
    public int PreviewLod { get; private set; }

    /// <summary>
    /// The actor that owns the component. Null if its outer isn't an export
    /// </summary>
    internal ExportEntry OwnerActor { get; private set; }

    /// <summary>
    /// The triangles the mesh blocks line checks with, shared with the other instances of the mesh. Set by the mesh's component, if light environments are supported for the game
    /// </summary>
    internal LevelGeometry.TriangleMesh GeometryMesh;

    /// <summary>
    /// Whether the mesh is in the game's scene: its actor and component aren't hidden in game. Set by the mesh's component
    /// </summary>
    internal Func<bool> IsInScene = () => true;

    /// <summary>
    /// Whether the mesh is shown in the editor, so that it casts dynamic shadows (its actor's category isn't hidden). Set by the mesh's component
    /// </summary>
    internal Func<bool> IsShown = () => true;

    /// <summary>
    /// Whether the mesh can block light environments' visibility traces, whichever light they're to. Those traces are TRACE_ShadowCast line checks,
    /// which only consider primitives that CastShadow and have static shadowing, and that the light affects (see <see cref="BlocksVisibilityTraceTo"/>).
    /// Hidden primitives still block them
    /// </summary>
    internal bool CanBlockVisibilityTraces => CastsShadow && HasStaticShadowing && AcceptsLights && GetBounds is not null;

    /// <summary>
    /// Whether the mesh blocks a light environment's visibility trace to <paramref name="light"/>. LE1 applies AffectsPrimitive, channels
    /// included; the other games ignore channels and exclude environment primitives.
    /// </summary>
    internal bool BlocksVisibilityTraceTo(SceneLight light) => CanBlockVisibilityTraces
        && (!UsesLightEnvironment || light.BypassesLightEnvironments)
        && AffectsPrimitive(light, GetBounds(), compareLightingChannels: light.Export.Game is MEGame.LE1);

    /// <summary>
    /// CastShadow and bCastDynamicShadow, including inherited values: whether the mesh casts dynamic shadows, if something renders them (a light, or its light environment)
    /// </summary>
    internal bool CastsDynamicShadow { get; private set; }

    private readonly MeshRenderContext Context;

    /// <summary>
    /// The materials of the mesh this lighting is for. Set by the mesh's <see cref="ModelPreview{TVertex}"/>, so that their light shaders can be loaded ahead of time
    /// </summary>
    internal IEnumerable<MaterialRenderProxy> Materials = [];
    /// <summary>
    /// The materials of the sections that cast shadows (bEnableShadowCasting). Set by the mesh's <see cref="ModelPreview{TVertex}"/>
    /// </summary>
    internal IEnumerable<MaterialRenderProxy> ShadowCastingMaterials = [];
    internal Func<BoxSphereBounds> GetBounds;
    /// <summary>Where the mesh masks LightShadow_ModulateBetter shadows off itself</summary>
    internal ModulatedShadowCulling ModShadowCulling;
    /// <summary>
    /// Draws the mesh with its materials' FModShadowMesh shaders, to mask a LightShadow_ModulateBetter shadow off it (see
    /// <see cref="LightAttenuationRenderer.DrawModShadowReceiverSection"/>). Set by the mesh's <see cref="ModelPreview{TVertex}"/>
    /// </summary>
    internal Action<SharpDX.Direct3D11.DeviceContext, ModShadowReceiverPass> DrawModShadowReceiver;

    /// <summary>
    /// Whether the mesh can cast dynamic (projected) shadows from the lights that reach it: it casts dynamic shadows, and isn't lit by a light environment
    /// (which casts its own). Which lights it casts them from is <see cref="CastsDynamicShadowFrom"/>; see <see cref="LightAttenuationRenderer"/>
    /// </summary>
    public bool IsDynamicShadowCaster { get; private set; }

    /// <summary>
    /// FPrimitiveSceneInfo's bStaticShadowing (bUsePrecomputedShadows, which level static meshes have): the shadows of static-shadowing lights are precomputed for it
    /// </summary>
    internal bool HasStaticShadowing;

    /// <summary>
    /// FLightPrimitiveInteraction's bCastShadow, for a light that reaches the mesh: a mesh with precomputed shadows only casts dynamic shadows from lights
    /// without static shadowing (whose shadows weren't precomputed). Otherwise it casts them from any light that casts dynamic shadows
    /// </summary>
    public bool CastsDynamicShadowFrom(SceneLight light) => IsDynamicShadowCaster && light.CastsDynamicShadows && (!HasStaticShadowing || !light.HasStaticShadowing);

    /// <summary>
    /// Whether the bake builds static lighting for the mesh: it's a StaticMeshComponent (the bake exports that class only; skeletal meshes have none in UE3)
    /// with static shadowing that accepts lights (UStaticMeshComponent::GetStaticLightingInfo), whether or not a light environment lights it. Only such
    /// meshes get a lightmap, or cast static shadows
    /// </summary>
    public bool ReceivesStaticLighting => IsBakedClass && HasStaticShadowing && AcceptsLights;

    //the component's class is the one LightmassSceneBuilder exports
    private bool IsBakedClass;

    /// <summary>
    /// Whether the preview may draw shadows from the mesh: the static shadows its bake casts, and runtime shadows. Light environments render their own
    /// runtime shadows. UE3 previews uncached static-light interactions with projected shadows even when bCastDynamicShadow is false.
    /// </summary>
    internal bool IsPreviewShadowCaster => ReceivesStaticLighting && ShadowSettings.BakesShadow || IsDynamicShadowCaster;

    /// <summary>
    /// Whether the preview draws the light's shadow from the mesh: as baked, for a light with static shadowing and a mesh with static shadowing,
    /// whether or not the mesh is hidden. Otherwise the runtime shadow
    /// </summary>
    internal bool CastsPreviewShadowFrom(SceneLight light) => HasStaticShadowing && light.HasStaticShadowing
        ? ReceivesStaticLighting && ShadowSettings.CastsDirect(light, staticShadowing: true, hidden: false)
        : IsDynamicShadowCaster && ShadowSettings.CastsDirect(light, HasStaticShadowing, hidden: !IsInScene());

    /// <summary>
    /// Whether the light's shadow from this caster is worth rendering. A precomputed shadow, as the bake makes it, blocks the light wherever it
    /// reaches: Lightmass traces against every shadow-casting mesh (FStaticLightingAggregateMesh::AddMesh), whatever its lighting channels or level,
    /// and whatever its own bake omitted. A runtime shadow needs the light to affect the caster, as UE3 only casts it through a light-primitive interaction.
    /// </summary>
    internal bool IsShadowRelevant(SceneLight light, BoxSphereBounds bounds) => HasStaticShadowing && light.HasStaticShadowing
        ? !light.HasLightEnvironment && light.AffectsBounds(bounds)
        : LightAssignment.Evaluate(light, bounds, new LightPrimitiveSettings(LightingChannels, LevelName, AcceptsLights, AcceptsDynamicLights)) == LightEligibility.Affects;

    public IEnumerable<string> ShadowNotes()
    {
        if (HasShadowParent) yield return "Shadow-parent grouping is not supported; Preview treats static casters separately.";
        if (ShadowSettings.SelfShadowOnly) yield return "Self-shadow-only caster (bSelfShadowOnly): Preview doesn't draw its shadow, which only falls on itself.";
        if (HasNonWhiteVertexColors) yield return "Preview uses white vertex colors; vertex-color-driven masks or deformation may differ from the bake.";
        //(set only when the mesh received a LightShadow_ModulateBetter shadow)
        foreach (var material in Materials)
            if (material.ModShadowMaskError is { } maskError) yield return $"{material.InstancedFullPath}: {maskError}";
        //the rest are about the shadows the preview draws from the mesh: none with game shaders off (no level light is drawn), or if it casts none
        if (!Context.UseGameShaders || !IsPreviewShadowCaster) yield break;
        MaterialRenderProxy[] casting = ShadowCastingMaterials.ToArray();
        //only those cast with their own shaders: a masked material without them casts no shadow, which its own error says
        if (casting.Any(material => material.CastsMaskedShadow && material.ShadowShaders is not null))
            yield return "Masked shadows evaluate the live material. Animated/view-dependent coverage may differ from the bake's material capture.";
        if (casting.Any(material => material.ShadowShadersPending && material.CastsShadowDepth && (material.CastsMaskedShadow || !material.HasUndisplacedVertices)))
            yield return "Shadow shaders are still loading: until they're ready, masked sections cast no shadow, and vertex-offset sections cast undeformed ones.";
        if (ReceivesStaticLighting && ShadowSettings.BakesShadow && casting.Any(material => material.BakesTransmission && !material.CastsShadowDepth))
            yield return "Translucent sections cast no shadow in Preview, but a bake shadows light through them by their transmission (partial or colored shadows), if it can capture their material.";
        //the bake reads Lightmass's own bCastShadowAsMasked instead
        if (ReceivesStaticLighting && ShadowSettings.BakesShadow && casting.Any(material => material.BakesTransmission && material.CastsShadowDepth))
            yield return "Translucency cast as masked (bCastLitTranslucencyShadowAsMasked) casts a masked shadow in Preview, as at runtime, but a bake shadows light through it by its transmission, unless its Lightmass settings cast it as masked.";
        if (ShadowSettings.IndirectOnly) yield return "Indirect-only shadow caster: Preview has no bounced light, so it casts no static direct shadow.";
        if (PreviewLod != 0) yield return "Preview shadows use the displayed LOD; the baker uses LOD 0 to shadow other meshes.";
        foreach (var material in casting)
            if (material.ShadowDepthError is { } error) yield return $"{material.InstancedFullPath}: {error}";
    }

    /// <summary>
    /// Draws the mesh's shadow-casting sections into a shadow's depth (see <see cref="LightAttenuationRenderer.DrawShadowSection{TVertex}"/>). Set by the mesh's <see cref="ModelPreview{TVertex}"/>
    /// </summary>
    internal Action<SharpDX.Direct3D11.DeviceContext, ShadowDepthPass> DrawShadowCaster;
    internal Func<Matrix4x4> GetLocalToWorld;

    private readonly Lock InteractionsLock = new();
    private LightInteraction[] Interactions = [];
    private SkyLighting SkyLight;
    private int InteractionsLightsVersion = -1;
    private BoxSphereBounds InteractionsBounds;
    private bool InteractionsIgnorePrecomputed;

    private MeshStaticLighting(MeshRenderContext context)
    {
        Context = context;
    }

    /// <summary>A decal's own vertex lighting, or its receiver's texture lighting, as FDecalLightCache selects it.
    /// Light eligibility belongs to the receiving primitive. The decal owns its vertex streams and never registers as a shadow caster.</summary>
    internal static MeshStaticLighting CreateDecal(MeshRenderContext context, ExportEntry component, StaticReceiverData receiver, MeshStaticLighting source)
    {
        var lighting = new MeshStaticLighting(context);
        if (source is not null)
        {
            lighting.LightingChannels = source.LightingChannels;
            lighting.AcceptsLights = source.AcceptsLights;
            lighting.AcceptsDynamicLights = source.AcceptsDynamicLights;
            lighting.LevelName = source.LevelName;
            lighting.HasSelfContainedLighting = source.HasSelfContainedLighting;
            lighting.SourceAssignments = source.SourceAssignments;
            lighting.UsesLightEnvironment = source.UsesLightEnvironment;
            lighting.IrrelevantLights.UnionWith(source.IrrelevantLights);
        }
        else
        {
            lighting.AcceptsLights = false;
        }
        try
        {
            // Load only the decal's own vertex buffers: receiver vertex indices do not match projected vertices.
            lighting.Load(receiver.LightMap ?? new LightMap(), [], receiver.ShadowMap1D ?? [], component.FileRef,
                receiver.Vertices.Length, receiver.Vertices.Select(v => v.LightMapCoordinate).ToArray());
            if (lighting.LightMapType == ELightMapType.LMT_None && source is not null)
            {
                if (source.LightMapType is ELightMapType.LMT_2D or ELightMapType.LMT_4 or ELightMapType.LMT_6)
                {
                    lighting.LightMapType = source.LightMapType;
                    lighting.Textures = source.Textures;
                    lighting.Scales = source.Scales;
                    lighting.Biases = source.Biases;
                    lighting.CoordinateScaleBias = source.CoordinateScaleBias;
                    lighting.LightMapLightGuids.UnionWith(source.LightMapLightGuids);
                }
                // Texture views are owned by the context cache. Never borrow the receiver's vertex shadow buffers.
                lighting.ShadowMaps.AddRange(source.ShadowMaps);
            }
            return lighting;
        }
        catch { lighting.Dispose(); throw; }
    }

    /// <summary>
    /// Reads a static mesh component's precomputed lighting for one LOD. Returns null if that isn't possible
    /// </summary>
    public static MeshStaticLighting Create(MeshRenderContext context, ExportEntry componentExport, StaticMesh mesh, int lod, PropertyCollection condensedProps = null, bool unbakedPreview = false)
    {
        if (!componentExport.Game.IsLEGame() || lod >= mesh.LODModels.Length)
        {
            return null;
        }
        StaticMeshComponent component;
        try
        {
            component = unbakedPreview ? StaticMeshComponent.Create() : componentExport.GetBinaryData<StaticMeshComponent>();
        }
        catch
        {
            return null;
        }
        var staticLighting = new MeshStaticLighting(context)
        {
            OwnerActor = componentExport.Parent as ExportEntry,
            IsBakedClass = componentExport.ClassName == "StaticMeshComponent"
        };
        if (!unbakedPreview)
        {
            context.CaptureAssignments(componentExport.FileRef);
            staticLighting.SourceAssignments = context.AssignmentHistory.Get(componentExport, $"LOD{lod}");
        }
        staticLighting.PreviewLod = lod;
        PropertyCollection props = unbakedPreview ? condensedProps : componentExport.GetProperties();
        if (props.GetProp<ArrayProperty<StructProperty>>("IrrelevantLights") is { } irrelevantLights)
        {
            foreach (StructProperty guidProp in irrelevantLights)
            {
                staticLighting.IrrelevantLights.Add(CommonStructs.GetGuid(guidProp));
            }
        }
        condensedProps ??= EffectiveLightingProperties.ReadPrimitive(componentExport, context.PackageCache);
        //A disabled light environment (as InterpActors' are by default) isn't used, so the primitive is lit like any other
        staticLighting.UsesLightEnvironment = context.ResolveLightEnvironment(componentExport, condensedProps, staticLighting.OwnerActor,
            out DynamicLightEnvironment lightEnvironment, out string unsupportedReason);
        staticLighting.LightEnvironment = lightEnvironment;
        staticLighting.UnsupportedLightEnvironmentReason = unsupportedReason;
        //Static meshes in levels have static shadowing, so UE3 initializes unset channels to Static. Light environments' primitives don't, so they get Dynamic
        staticLighting.ReadPrimitiveSettings(componentExport, condensedProps,
            staticLighting.UsesLightEnvironment ? LightingChannels.DynamicPrimitiveDefault : LightingChannels.StaticPrimitiveDefault);
        //FPrimitiveSceneInfo's bStaticShadowing (bUsePrecomputedShadows) and bCastDynamicShadow (CastShadow && bCastDynamicShadow, which MeshComponent defaults to true).
        //StaticMeshActor's component template sets bUsePrecomputedShadows. If it isn't found (in case the archetype chain couldn't be resolved),
        //a mesh with any precomputed lighting, or in a StaticMeshCollectionActor, is taken to have it
        bool usesPrecomputedShadows = LightAssignment.HasStaticShadowing(componentExport, condensedProps, component);
        staticLighting.HasStaticShadowing = usesPrecomputedShadows;
        //(the buffers hold LegendaryExplorerCore's Color, not SharpDX's)
        staticLighting.HasNonWhiteVertexColors = mesh.LODModels[lod].ColorVertexBuffer?.VertexData?.Any(c => c != OpaqueWhite) == true
            || (lod < component.LODData.Length && component.LODData[lod].OverrideVertexColors?.VertexData?.Any(c => c != OpaqueWhite) == true);
        staticLighting.CastsDynamicShadow = staticLighting.CastsShadow && condensedProps.GetProp<BoolProperty>("bCastDynamicShadow") is not { Value: false };
        staticLighting.IsDynamicShadowCaster = staticLighting.CastsDynamicShadow
                                               //shadow groups (a shadow parent's children cast with it) aren't supported
                                               && condensedProps.GetProp<ObjectProperty>("ShadowParent") is not { Value: not 0 }
                                               && !staticLighting.UsesLightEnvironment;
        if (lod < component.LODData.Length)
        {
            staticLighting.Load(component.LODData[lod], componentExport.FileRef, mesh, lod);
        }
        //judged on LOD 0, whichever LOD is shown: it's the one other meshes are shadowed by, and the one a receiver is seen with up close
        staticLighting.HasCompatibleBakeMapping = staticLighting.ReceivesStaticLighting && component.LODData.Length > 0 && mesh.LODModels.Length > 0
            && (LightmapMapping.IsCompatible(componentExport.FileRef, component.LODData[0].LightMap, mesh.LODModels[0], ComponentGeometry.CoordinateIndex(mesh.Export))
                || component.LODData[0].LightMap.LightMapType == ELightMapType.LMT_None
                    && LightmapMapping.ShadowAllocation(new LightmapRef(componentExport, "LOD0", component.LODData[0].LightMap, component),
                        mesh.LODModels[0], ComponentGeometry.CoordinateIndex(mesh.Export)) != null);
        context.RegisterStaticLighting(staticLighting);
        return staticLighting;
    }

    /// <summary>
    /// For a primitive with no precomputed lighting or light environment, such as a skeletal mesh whose light environment is disabled:
    /// every light that reaches it is rendered dynamically, as UE3 does for a movable primitive. Returns null if that isn't possible
    /// </summary>
    /// <param name="condensedProps">The component's properties, including inherited ones</param>
    public static MeshStaticLighting CreateDynamic(MeshRenderContext context, ExportEntry componentExport, PropertyCollection condensedProps)
    {
        if (!componentExport.Game.IsLEGame())
        {
            return null;
        }
        var staticLighting = new MeshStaticLighting(context)
        {
            OwnerActor = componentExport.Parent as ExportEntry
        };
        if (componentExport.GetProperty<ArrayProperty<StructProperty>>("IrrelevantLights") is { } irrelevantLights)
        {
            foreach (StructProperty guidProp in irrelevantLights)
            {
                staticLighting.IrrelevantLights.Add(CommonStructs.GetGuid(guidProp));
            }
        }
        //Without static shadowing, UE3 initializes unset channels to Dynamic
        staticLighting.ReadPrimitiveSettings(componentExport, condensedProps, LightingChannels.DynamicPrimitiveDefault);
        //bUsePrecomputedShadows defaults to false
        staticLighting.HasStaticShadowing = condensedProps.GetProp<BoolProperty>("bUsePrecomputedShadows") is { Value: true };
        staticLighting.CastsDynamicShadow = staticLighting.CastsShadow && condensedProps.GetProp<BoolProperty>("bCastDynamicShadow") is not { Value: false };
        staticLighting.IsDynamicShadowCaster = staticLighting.CastsDynamicShadow && condensedProps.GetProp<ObjectProperty>("ShadowParent") is not { Value: not 0 };
        context.RegisterStaticLighting(staticLighting);
        return staticLighting;
    }

    /// <summary>
    /// A BSP element's precomputed lighting (one FModelElement of a ModelComponent): its light-map, shadow maps and irrelevant lights. Each
    /// vertex's ShadowTexCoord is its light-map UV (FModelVertex). BSP always has static shadowing
    /// </summary>
    /// <param name="channels">The lighting channels UE3 gives the component from its surfaces</param>
    internal static MeshStaticLighting CreateModelElement(MeshRenderContext context, ExportEntry componentExport, ModelElement element, int elementIndex,
        Vector2[] lightMapCoordinates, LightingChannels channels)
    {
        var lighting = new MeshStaticLighting(context) { OwnerActor = null, IsBakedClass = true };
        context.CaptureAssignments(componentExport.FileRef);
        //named as PackageLightmaps names BSP light-maps
        lighting.SourceAssignments = context.AssignmentHistory.Get(componentExport, $"Element{elementIndex}");
        lighting.IrrelevantLights.UnionWith(element.IrrelevantLights ?? []);
        PropertyCollection props = EffectiveLightingProperties.ReadPrimitive(componentExport, context.PackageCache);
        lighting.ReadPrimitiveSettings(componentExport, props, channels);
        lighting.HasStaticShadowing = true;
        lighting.CastsDynamicShadow = lighting.CastsShadow && props.GetProp<BoolProperty>("bCastDynamicShadow") is not { Value: false };
        lighting.IsDynamicShadowCaster = lighting.CastsDynamicShadow;
        lighting.Load(element.LightMap, element.ShadowMaps ?? [], [], componentExport.FileRef, lightMapCoordinates.Length, lightMapCoordinates);
        context.RegisterStaticLighting(lighting);
        return lighting;
    }

    /// <summary>
    /// A terrain section's precomputed lighting (a TerrainComponent): its light-map, shadow maps and irrelevant lights. The terrain vertex factory computes
    /// their UVs from the vertex positions (see <see cref="TerrainSection.LightMapCoordinateScaleBias"/>). Terrain always has static shadowing
    /// </summary>
    /// <param name="settings">The component's lighting settings, with those ATerrain::PostLoad copies from the terrain
    /// (see <see cref="TerrainHeightField.WithTerrainLightingSettings"/>)</param>
    internal static MeshStaticLighting CreateTerrainSection(MeshRenderContext context, ExportEntry componentExport, LightMap lightMap, PropertyCollection settings)
    {
        var lighting = new MeshStaticLighting(context) { OwnerActor = componentExport.Parent as ExportEntry, IsBakedClass = true };
        context.CaptureAssignments(componentExport.FileRef);
        //named as PackageLightmaps names terrain light-maps
        lighting.SourceAssignments = context.AssignmentHistory.Get(componentExport, "Terrain");
        if (settings.GetProp<ArrayProperty<StructProperty>>("IrrelevantLights") is { } irrelevantLights)
        {
            foreach (StructProperty guidProp in irrelevantLights)
            {
                lighting.IrrelevantLights.Add(CommonStructs.GetGuid(guidProp));
            }
        }
        lighting.ReadPrimitiveSettings(componentExport, settings, LightingChannels.StaticPrimitiveDefault);
        lighting.HasStaticShadowing = true;
        lighting.CastsDynamicShadow = lighting.CastsShadow && settings.GetProp<BoolProperty>("bCastDynamicShadow") is not { Value: false };
        lighting.IsDynamicShadowCaster = lighting.CastsDynamicShadow;
        int[] shadowMaps = settings.GetProp<ArrayProperty<ObjectProperty>>("ShadowMaps")?.Select(o => o.Value).ToArray() ?? [];
        lighting.Load(lightMap ?? new LightMap(), shadowMaps, [], componentExport.FileRef, 0, null, shaderComputesCoordinates: true);
        context.RegisterStaticLighting(lighting);
        return lighting;
    }

    /// <summary>
    /// The primitive component's settings that decide which lights affect it
    /// </summary>
    /// <param name="defaultChannels">The channels UE3 initializes unset lighting channels to</param>
    private void ReadPrimitiveSettings(ExportEntry componentExport, PropertyCollection condensedProps, LightingChannels defaultChannels)
    {
        ShadowSettings = ShadowParticipation.Read(condensedProps);
        ModShadowCulling = ModulatedShadowCullingFlags.Read(condensedProps,
            skeletal: LegendaryExplorerCore.Unreal.ObjectInfo.GlobalUnrealObjectInfo.IsA(componentExport.ClassName, "SkeletalMeshComponent", componentExport.Game));
        HasShadowParent = condensedProps.GetProp<ObjectProperty>("ShadowParent") is { Value: not 0 };
        CastsShadow = ShadowSettings.CastShadow;
        LightingChannels = LightingChannels.FromProperty(condensedProps.GetProp<StructProperty>("LightingChannels"), default, isInitialized: false, defaultChannels);
        //Component archetypes set bAcceptsLights. Only an explicit false is trusted, in case the archetype chain couldn't be resolved
        AcceptsLights = condensedProps.GetProp<BoolProperty>("bAcceptsLights") is not { Value: false };
        AcceptsDynamicLights = condensedProps.GetProp<BoolProperty>("bAcceptsDynamicLights") is not { Value: false };
        LevelName = componentExport.FileRef.FileNameNoExtension;
        HasSelfContainedLighting = componentExport.FileRef.Flags.Has(UnrealFlags.EPackageFlags.SelfContainedLighting);
    }

    private void Load(StaticMeshComponentLODInfo lodInfo, IMEPackage pcc, StaticMesh mesh, int lod)
    {
        StaticMeshRenderData lodModel = mesh.LODModels[lod];
        //Like the game's vertex factory, light-map UVs are only available if the mesh has that UV channel
        int lightMapCoordinateIndex = ComponentGeometry.CoordinateIndex(mesh.Export);
        StaticMeshVertexBuffer vertexBuffer = lodModel.VertexBuffer;
        Vector2[] lightMapCoordinates = null;
        if (lightMapCoordinateIndex >= 0 && lightMapCoordinateIndex < vertexBuffer.NumTexCoords)
        {
            lightMapCoordinates = new Vector2[lodModel.NumVertices];
            for (int i = 0; i < lightMapCoordinates.Length; i++)
            {
                StaticMeshVertexBuffer.StaticMeshFullVertex vertex = vertexBuffer.VertexData[i];
                lightMapCoordinates[i] = vertexBuffer.bUseFullPrecisionUVs ? vertex.FullPrecisionUVs[lightMapCoordinateIndex] : vertex.HalfPrecisionUVs[lightMapCoordinateIndex];
            }
        }
        Load(lodInfo.LightMap, lodInfo.ShadowMaps, lodInfo.ShadowVertexBuffers, pcc, (int)lodModel.NumVertices, lightMapCoordinates);
    }

    /// <param name="lightMapCoordinates">Each vertex's light-map UV, or null if the vertices have none</param>
    /// <param name="shaderComputesCoordinates">The vertex factory computes light-map UVs itself (terrain), so texture light-maps and shadow maps are used without per-vertex UVs</param>
    private void Load(LightMap lightMap, int[] shadowMapIndices, int[] shadowVertexBufferIndices, IMEPackage pcc, int numVertices, Vector2[] lightMapCoordinates,
        bool shaderComputesCoordinates = false)
    {
        uint[] samplesA = null;
        uint[] samplesB = null;
        Guid[] lightGuids = [];
        switch (lightMap)
        {
            case LightMap_1D lm1D:
                lightGuids = lm1D.LightGuids;
                samplesA = Array.ConvertAll(lm1D.DirectionalSamples, s => PackColor(s.Coefficient2));
                samplesB = Array.ConvertAll(lm1D.DirectionalSamples, s => PackColor(s.Coefficient3));
                Scales = [ToVector4(lm1D.ScaleVector1), ToVector4(lm1D.ScaleVector2)];
                break;
            case LightMap_3 lm3:
                lightGuids = lm3.LightGuids;
                samplesA = Array.ConvertAll(lm3.DirectionalSamples, s => PackColor(s.Coefficient2));
                samplesB = Array.ConvertAll(lm3.DirectionalSamples, s => PackColor(s.Coefficient3));
                Scales = [ToVector4(lm3.ScaleVector1), ToVector4(lm3.ScaleVector2)];
                break;
            case LightMap_5 lm5:
                lightGuids = lm5.LightGuids;
                samplesA = Array.ConvertAll(lm5.SimpleSamples, s => PackColor(s.Coefficient));
                Scales = [ToVector4(lm5.ScaleVector)];
                break;
            case LightMap_2D lm2D:
            {
                lightGuids = lm2D.LightGuids;
                //The two directional coefficient textures. The third texture slot holds the simple light-map, which the directional shaders don't use
                if (LoadPackageTexture(pcc, lm2D.Texture1) is { } texture1 && LoadPackageTexture(pcc, lm2D.Texture2) is { } texture2)
                {
                    Textures = [texture1, texture2];
                    Scales = [ToVector4(lm2D.ScaleVector1), ToVector4(lm2D.ScaleVector2)];
                    CoordinateScaleBias = ToScaleBias(lm2D.CoordinateScale, lm2D.CoordinateBias);
                    LightMapType = lm2D.LightMapType;
                }
                break;
            }
            case LightMap_4or6 lm4or6:
            {
                lightGuids = lm4or6.LightGuids;
                //LMT_4: color, then world-space direction. LMT_6: just color
                int numTextures = lm4or6.LightMapType is ELightMapType.LMT_4 ? 2 : 1;
                var textures = new PreviewTextureCache.TextureEntry[numTextures];
                var scales = new Vector4[numTextures];
                var biases = new Vector4[numTextures];
                bool loaded = true;
                for (int i = 0; i < numTextures; i++)
                {
                    (int texUIndex, Fixed8<float> scaleBias) = i == 0 ? (lm4or6.Texture1, lm4or6.unkFloats1) : (lm4or6.Texture2, lm4or6.unkFloats2);
                    textures[i] = LoadPackageTexture(pcc, texUIndex);
                    loaded &= textures[i] is not null;
                    scales[i] = new Vector4(scaleBias[0], scaleBias[1], scaleBias[2], scaleBias[3]);
                    biases[i] = new Vector4(scaleBias[4], scaleBias[5], scaleBias[6], scaleBias[7]);
                }
                if (loaded)
                {
                    (Textures, Scales, Biases) = (textures, scales, biases);
                    CoordinateScaleBias = ToScaleBias(lm4or6.CoordinateScale, lm4or6.CoordinateBias);
                    LightMapType = lm4or6.LightMapType;
                }
                break;
            }
        }
        //UE3 doesn't use a vertex light-map whose sample count doesn't match the mesh
        if (samplesA is not null && samplesA.Length == numVertices)
        {
            LightMapType = lightMap.LightMapType;
        }
        else
        {
            samplesA = samplesB = null;
        }
        //A light-map that can't be used is ignored entirely, so the lights in it are rendered dynamically instead, as UE3 would do for an unbuilt mesh
        if (LightMapType is not ELightMapType.LMT_None)
        {
            LightMapLightGuids.UnionWith(lightGuids);
        }

        foreach (int shadowMapUIndex in shadowMapIndices)
        {
            if (pcc.TryGetUExport(shadowMapUIndex, out ExportEntry shadowMapExport))
            {
                PropertyCollection props = shadowMapExport.GetProperties();
                if (props.GetProp<ObjectProperty>("Texture") is { } textureProp && LoadPackageTexture(pcc, textureProp.Value) is { } texture
                    && props.GetProp<StructProperty>("LightGuid") is { } lightGuidProp)
                {
                    Vector2 scale = props.GetProp<StructProperty>("CoordinateScale") is { } scaleProp ? CommonStructs.GetVector2(scaleProp) : Vector2.One;
                    Vector2 bias = props.GetProp<StructProperty>("CoordinateBias") is { } biasProp ? CommonStructs.GetVector2(biasProp) : Vector2.Zero;
                    //Default__ShadowMap2D has bIsShadowFactorTexture = true. Signed distance field shadow maps set it to false
                    bool isDistanceField = props.GetProp<BoolProperty>("bIsShadowFactorTexture") is { Value: false };
                    ShadowMaps.Add(new ShadowMapTexture(CommonStructs.GetGuid(lightGuidProp), texture, ToScaleBias(scale, bias), isDistanceField));
                }
            }
        }
        foreach (int shadowVertexBufferUIndex in shadowVertexBufferIndices)
        {
            if (pcc.TryGetUExport(shadowVertexBufferUIndex, out ExportEntry shadowMap1DExport))
            {
                ShadowMap1D shadowMap1D = shadowMap1DExport.GetBinaryData<ShadowMap1D>();
                //Samples are floats. UE3 doesn't use a shadow vertex buffer whose sample count doesn't match the mesh
                if (shadowMap1D.Samples.Length == numVertices && numVertices > 0)
                {
                    float[] samples = Array.ConvertAll(shadowMap1D.Samples, BitConverter.Int32BitsToSingle);
                    ShadowVertexBuffers.Add(new ShadowVertexBufferEntry(shadowMap1D.LightGuid, Buffer.Create(Context.Device, BindFlags.VertexBuffer, samples)));
                }
            }
        }

        bool hasLightMapCoordinates = lightMapCoordinates is not null;
        if (!hasLightMapCoordinates && !shaderComputesCoordinates)
        {
            if (!IsVertexLightMap)
            {
                LightMapType = ELightMapType.LMT_None;
                LightMapLightGuids.Clear();
            }
            ShadowMaps.Clear();
        }
        if (numVertices > 0 && (hasLightMapCoordinates || samplesA is not null))
        {
            var vertices = new StaticLightingVertex[numVertices];
            for (int i = 0; i < numVertices; i++)
            {
                if (hasLightMapCoordinates)
                {
                    vertices[i].LightMapCoordinate = lightMapCoordinates[i];
                }
                if (samplesA is not null)
                {
                    vertices[i].SampleA = samplesA[i];
                    vertices[i].SampleB = samplesB?[i] ?? 0;
                }
            }
            VertexStream = Buffer.Create(Context.Device, BindFlags.VertexBuffer, vertices);
        }
    }

    /// <summary>
    /// The lights rendered on this mesh in their own passes: those that reach it, and aren't baked into its light-map. Cached until the scene's lights, bounds, or preview mode change.
    /// </summary>
    public LightInteraction[] GetLightInteractions(BoxSphereBounds bounds, bool? ignorePrecomputed = null)
    {
        lock (InteractionsLock)
        {
            UpdateInteractions(bounds, ignorePrecomputed ?? Context.IsPreviewLighting);
            return Interactions;
        }
    }

    /// <summary>
    /// The sky lighting the base pass adds: the sum of the sky lights that reach the mesh and aren't baked into its light-map,
    /// as FSkyLightSceneInfo::AttachPrimitive accumulates it. Cached like <see cref="GetLightInteractions"/>.
    /// </summary>
    public SkyLighting GetSkyLighting(BoxSphereBounds bounds)
    {
        lock (InteractionsLock)
        {
            UpdateInteractions(bounds, Context.IsPreviewLighting);
            return SkyLight;
        }
    }

    private void UpdateInteractions(BoxSphereBounds bounds, bool ignorePrecomputed)
    {
        SceneLight[] lights = Context.GetLights(out int lightsVersion);
        if (lightsVersion == InteractionsLightsVersion && ignorePrecomputed == InteractionsIgnorePrecomputed && bounds.Origin == InteractionsBounds.Origin && bounds.SphereRadius == InteractionsBounds.SphereRadius
            && bounds.BoxExtent == InteractionsBounds.BoxExtent)
        {
            return;
        }
        var interactions = new List<LightInteraction>();
        var skyLight = new SkyLighting();
        if (AcceptsLights)
        {
            foreach (SceneLight light in lights)
            {
                if (AffectsPrimitive(light, bounds, ignorePrecomputed: ignorePrecomputed) && GetInteraction(light, ignorePrecomputed) is { } interaction)
                {
                    if (light.Type is SceneLightType.Sky)
                    {
                        //its precomputed shadows (if any) are ignored
                        skyLight = new SkyLighting(skyLight.Upper + light.Color, skyLight.Lower + light.LowerColor);
                    }
                    else
                    {
                        interactions.Add(interaction);
                    }
                }
            }
        }
        Interactions = interactions.ToArray();
        SkyLight = skyLight;
        InteractionsLightsVersion = lightsVersion;
        InteractionsBounds = bounds;
        InteractionsIgnorePrecomputed = ignorePrecomputed;
    }

    /// <summary>
    /// FLightSceneInfoCompact::AffectsPrimitive (ULightComponent::AffectsPrimitive) through the shared <see cref="LightAssignment.Evaluate"/>, minus the test of whether
    /// the primitive is lit by a light environment, which the callers make. When ignoring precomputed lighting, the assignment policy's history test follows
    /// </summary>
    /// <param name="compareLightingChannels">False for the LE2/LE3 preview's line checks; LE1 checks channels</param>
    private bool AffectsPrimitive(SceneLight light, BoxSphereBounds bounds, bool compareLightingChannels = true, bool ignorePrecomputed = false) =>
        GetPrimitiveExclusion(light, bounds, compareLightingChannels, ignorePrecomputed) is LightEligibility.Affects;

    /// <summary>
    /// <see cref="AffectsPrimitive"/>, returning the first test the light fails
    /// </summary>
    private LightEligibility GetPrimitiveExclusion(SceneLight light, BoxSphereBounds bounds, bool compareLightingChannels, bool ignorePrecomputed)
    {
        if (light.BakedGIOnly) return LightEligibility.BakedGIOnly;
        var result = LightAssignment.Evaluate(light, bounds,
            new LightPrimitiveSettings(LightingChannels, LevelName, AcceptsLights, AcceptsDynamicLights, HasSelfContainedLighting),
            compareLightingChannels, honorSelfContained: !ignorePrecomputed);
        if (result != LightEligibility.Affects || !ignorePrecomputed || !compareLightingChannels) return result;
        return LightAssignment.EvaluateHistory(light, Context.AssignmentPolicy, Context.AssignmentHistory.IsKnown(light), SourceAssignments);
    }

    /// <summary>
    /// For diagnostics: whether the light reaches this mesh under the same rules as <see cref="GetLightInteractions"/>, and if not, why.
    /// Not for meshes that <see cref="UsesLightEnvironment"/>; their light environment decides
    /// </summary>
    /// <param name="ignorePrecomputed">Preview the current scene without sampling baked lighting; the selected assignment policy still applies</param>
    public LightEligibility GetEligibility(SceneLight light, BoxSphereBounds bounds, bool ignorePrecomputed)
    {
        if (!AcceptsLights)
        {
            return LightEligibility.LightsNotAccepted;
        }
        LightEligibility exclusion = GetPrimitiveExclusion(light, bounds, compareLightingChannels: true, ignorePrecomputed);
        if (exclusion is not LightEligibility.Affects)
        {
            return exclusion;
        }
        //mirrors GetInteraction
        if (!ignorePrecomputed && light.HasStaticShadowing)
        {
            if (LightMapLightGuids.Contains(light.LightmapGuid))
            {
                return LightEligibility.BakedIntoLightMap;
            }
            if (!ShadowVertexBuffers.Any(buffer => buffer.LightGuid == light.LightGuid) && !ShadowMaps.Any(shadowMap => shadowMap.LightGuid == light.LightGuid)
                && IrrelevantLights.Contains(light.LightGuid))
            {
                return LightEligibility.BakedIrrelevant;
            }
        }
        return LightEligibility.Affects;
    }

    /// <summary>
    /// FStaticMeshSceneProxy::FLODInfo::GetInteraction, which LE3 doesn't change. Null if the light doesn't need its own pass.
    /// (FLightPrimitiveInteraction::Create also skips uncached interactions between static shadowing lights and primitives with bUsePrecomputedShadows
    /// whose owner is movable. That isn't checked: movable actors' meshes normally use a light environment instead)
    /// </summary>
    private LightInteraction? GetInteraction(SceneLight light, bool ignorePrecomputed)
    {
        //An unbuilt preview must also discard baked irrelevance: a moved light may now reach the mesh.
        if (!ignorePrecomputed && light.HasStaticShadowing)
        {
            if (LightMapLightGuids.Contains(light.LightmapGuid))
            {
                return null;
            }
            foreach (ShadowVertexBufferEntry shadowVertexBuffer in ShadowVertexBuffers)
            {
                if (shadowVertexBuffer.LightGuid == light.LightGuid)
                {
                    return new LightInteraction(light, StaticShadowingType.ShadowVertexBuffer, null, shadowVertexBuffer.Buffer);
                }
            }
            foreach (ShadowMapTexture shadowMap in ShadowMaps)
            {
                if (shadowMap.LightGuid == light.LightGuid)
                {
                    return new LightInteraction(light, shadowMap.IsDistanceField ? StaticShadowingType.DistanceFieldShadowTexture : StaticShadowingType.ShadowTexture, shadowMap, null);
                }
            }
            if (IrrelevantLights.Contains(light.LightGuid))
            {
                return null;
            }
        }
        return new LightInteraction(light, StaticShadowingType.None, null, null);
    }

    private static readonly LegendaryExplorerCore.SharpDX.Color OpaqueWhite = new(255, 255, 255, 255);

    //InvalidatePreviewPreparation clears it from the UI thread while shader preparation reads it on a worker
    private volatile bool PreparedForLevelLighting, PreparedForPreviewLighting;
    internal void InvalidatePreviewPreparation() => PreparedForPreviewLighting = false;

    /// <summary>
    /// Whether <see cref="PrepareLightShaders"/> has nothing left to do for the lighting mode
    /// </summary>
    internal bool IsPrepared(bool previewLighting) =>
        (previewLighting ? PreparedForPreviewLighting : PreparedForLevelLighting) || UsesLightEnvironment || GetBounds is null;

    /// <summary>
    /// Computes the light interactions and loads the light shaders they need, so that it doesn't happen during rendering. Thread-safe
    /// </summary>
    internal void PrepareLightShaders(bool previewLighting)
    {
        // Prepared once per lighting mode. Lights added or changed later (by edits) have their shaders loaded as they're first rendered,
        // so a preview refresh only prepares the meshes it rebuilt, rather than every mesh in every level
        if (IsPrepared(previewLighting))
        {
            return;
        }
        //set before preparing, so that an invalidation while it runs isn't lost. (Not through a ref, which would lose the volatile access)
        if (previewLighting) PreparedForPreviewLighting = true;
        else PreparedForLevelLighting = true;
        foreach (LightInteraction interaction in GetLightInteractions(GetBounds(), previewLighting))
        {
            foreach (MaterialRenderProxy material in Materials)
            {
                var (vertex, pixel) = material.GetLightShaders(interaction.Light.Type, interaction.Shadowing);
                if (Context.Device is not null && vertex is not null && pixel is not null)
                {
                    Context.GetCachedVertexShader(vertex.Guid, vertex.ShaderByteCode, material.VertexInputElements);
                    Context.GetCachedPixelShader(pixel.Guid, pixel.ShaderByteCode);
                }
            }
        }
    }

    /// <summary>
    /// The primitive's lighting channels, for a light environment it's lit by
    /// </summary>
    internal LightingChannels Channels => LightingChannels;

    private static Vector4 ToVector4(Vector3 v) => new(v, 1);

    private static Vector4 ToScaleBias(Vector2 scale, Vector2 bias) => new(scale.X, scale.Y, bias.Y, bias.X);

    //Back to the order the bytes are serialized in: B, G, R, A
    private static uint PackColor(LegendaryExplorerCore.SharpDX.Color c) => (uint)(c.B | c.G << 8 | c.R << 16 | c.A << 24);

    private PreviewTextureCache.TextureEntry LoadPackageTexture(IMEPackage pcc, int uIndex)
    {
        if (!pcc.TryGetUExport(uIndex, out ExportEntry textureExport))
        {
            return null;
        }
        //Light-map and shadow-map textures are top-level objects with generated names, but their memory paths include their level's name, so they're unique
        return Context.TextureCache.LoadTexture(textureExport, Context.PackageCache);
    }

    public void Dispose()
    {
        Context.UnregisterStaticLighting(this);
        VertexStream?.Dispose();
        VertexStream = null;
        foreach (ShadowVertexBufferEntry shadowVertexBuffer in ShadowVertexBuffers)
        {
            shadowVertexBuffer.Buffer.Dispose();
        }
        ShadowVertexBuffers.Clear();
    }
}
