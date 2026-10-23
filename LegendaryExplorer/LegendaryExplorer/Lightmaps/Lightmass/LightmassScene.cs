using static LegendaryExplorer.Lightmaps.Game.LightmapMapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.Lightmaps.Game;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>
/// A light as Lightmass gets it (FLightData plus its type's struct). A level light can be exported twice: once to light lightmaps, and
/// once as a <see cref="ShadowOnly"/> twin for the primitives that get a shadow map of it instead (LightmassSceneBuilder.AddLights).
/// </summary>
public sealed class LightmassLight
{
    public SceneLight Light;
    /// <summary>
    /// What Lightmass names it by. For the lighting light, its LightmapGuid (what LE3 lightmaps list, LightGuids); for a shadow-only twin, a
    /// GUID derived from its LightGuid (shadow maps are written with the LightGuid itself).
    /// </summary>
    public Guid Guid;
    /// <summary>
    /// Only casts precomputed shadows: no static lighting, so Lightmass makes shadow maps of it rather than lighting lightmaps, and no
    /// indirect light (IndirectLightingScale 0), which the lighting light already bounces.
    /// </summary>
    public bool ShadowOnly;
    /// <summary>LE1 bBakedGIOnly: bounce lighting without a direct lightmap or runtime shadow-map contribution.</summary>
    public bool IndirectOnly;
    /// <summary>Its shadow maps are signed distance fields rather than visibility.</summary>
    public bool SignedDistanceField;
    public uint Flags;
    public (byte R, byte G, byte B) Color;
    public float Brightness, LightSourceRadius, IndirectLightingScale, IndirectLightingSaturation, ShadowExponent, LightSourceAngle;
    public float InnerConeAngle, OuterConeAngle, LowerBrightness;
    public (byte R, byte G, byte B) LowerColor;
    public int LevelId;
}

/// <summary>A StaticMesh exported to a persistent channel (all its LODs), named by a hash of its content.</summary>
public sealed class LightmassMesh
{
    public Guid Guid;
    public ExportEntry Export;
    public StaticMesh Mesh;
    public int LightMapCoordinateIndex;
}

/// <summary>A square of FFloat16Color samples of one material property over the material's UV space (row-major, RGBA).</summary>
public sealed class MaterialSamples
{
    public int Size;
    public Half[] Rgba;

    public static MaterialSamples Constant(Vector3 rgb, float a) => new() { Size = 1, Rgba = [(Half)rgb.X, (Half)rgb.Y, (Half)rgb.Z, (Half)a] };

    /// <summary>The samples, or a single one if they're all the same (as UE3 exports a property that doesn't vary).</summary>
    public static MaterialSamples FromTexels(int size, Func<int, Vector4> texel)
    {
        var rgba = new Half[size * size * 4];
        bool uniform = true;
        for (int i = 0; i < size * size; i++)
        {
            var v = texel(i);
            rgba[i * 4] = (Half)v.X; rgba[i * 4 + 1] = (Half)v.Y; rgba[i * 4 + 2] = (Half)v.Z; rgba[i * 4 + 3] = (Half)v.W;
            uniform &= i == 0 || rgba.AsSpan(i * 4, 4).SequenceEqual(rgba.AsSpan(0, 4));
        }
        return uniform ? new MaterialSamples { Size = 1, Rgba = rgba[..4] } : new MaterialSamples { Size = size, Rgba = rgba };
    }
}

/// <summary>
/// A material as Lightmass gets it (FMaterialData and its property samples). The diffuse samples already include the level, primitive and
/// material DiffuseBoost, clamped to 1 per channel (stock Lightmass clamps only luminance, which tints bounce light), so the element and
/// material diffuse boosts are written as 1. Null samples aren't exported (size 0).
/// </summary>
public sealed class LightmassMaterial
{
    public Guid Guid;
    /// <summary>UDK numbering (see LightmassSceneBuilder.IsTranslucent).</summary>
    public int BlendMode;
    public bool TwoSided, CastShadowAsMasked;
    public float OpacityMaskClipValue;
    public float EmissiveBoost = 1;
    public MaterialSamples Emissive, Diffuse, Specular, Transmission;
}

/// <summary>
/// A primitive's LightmassSettings as each of its elements' FMaterialElementData carries them (the emissive boost times the level's; the
/// diffuse boost goes into the material's samples instead).
/// </summary>
public sealed class LightmassElementSettings
{
    public bool TwoSidedLighting, ShadowIndirectOnly, UseEmissiveForStaticLighting;
    public float EmissiveLightFalloffExponent = 2, EmissiveLightExplicitInfluenceRadius, EmissiveBoost = 1, FullyOccludedSamplesFraction = 1, DiffuseBoost = 1;

    /// <summary>From a component's LightmassSettings property (FLightmassPrimitiveSettings; null: the defaults).</summary>
    public static LightmassElementSettings From(StructProperty settings, float worldEmissiveBoost) => new()
    {
        TwoSidedLighting = settings?.GetProp<BoolProperty>("bUseTwoSidedLighting") is { Value: true },
        ShadowIndirectOnly = settings?.GetProp<BoolProperty>("bShadowIndirectOnly") is { Value: true },
        UseEmissiveForStaticLighting = settings?.GetProp<BoolProperty>("bUseEmissiveForStaticLighting") is { Value: true },
        EmissiveLightFalloffExponent = settings?.GetProp<FloatProperty>("EmissiveLightFalloffExponent")?.Value ?? 2,
        EmissiveLightExplicitInfluenceRadius = settings?.GetProp<FloatProperty>("EmissiveLightExplicitInfluenceRadius")?.Value ?? 0,
        EmissiveBoost = (settings?.GetProp<FloatProperty>("EmissiveBoost")?.Value ?? 1) * worldEmissiveBoost,
        FullyOccludedSamplesFraction = settings?.GetProp<FloatProperty>("FullyOccludedSamplesFraction")?.Value ?? 1,
        DiffuseBoost = settings?.GetProp<FloatProperty>("DiffuseBoost")?.Value ?? 1,
    };

    /// <summary>From one of a Model's LightmassSettings (BSP surfaces index them with iLightmassIndex).</summary>
    public static LightmassElementSettings From(LightmassPrimitiveSettings settings, float worldEmissiveBoost) => settings == null ? new() { EmissiveBoost = worldEmissiveBoost } : new()
    {
        TwoSidedLighting = settings.bUseTwoSidedLighting,
        ShadowIndirectOnly = settings.bShadowIndirectOnly,
        UseEmissiveForStaticLighting = settings.bUseEmissiveForStaticLighting,
        EmissiveLightFalloffExponent = settings.EmissiveLightFalloffExponent,
        EmissiveLightExplicitInfluenceRadius = settings.EmissiveLightExplicitInfluenceRadius,
        EmissiveBoost = settings.EmissiveBoost * worldEmissiveBoost,
        FullyOccludedSamplesFraction = settings.FullyOccludedSamplesFraction,
        DiffuseBoost = settings.DiffuseBoost,
    };
}

/// <summary>A mesh element as Lightmass gets it (FMaterialElementData): its material and its primitive's settings.</summary>
public sealed record LightmassElement(LightmassMaterial Material, LightmassElementSettings Settings);

/// <summary>Something Lightmass bakes as one task: a static mesh LOD's texture or vertex mapping, or a BSP surface group.</summary>
public abstract class LightmassMapping
{
    /// <summary>The mapping's GUID, which is also its mesh's and its task's (UE3 names all three alike).</summary>
    public Guid Guid;
    /// <summary>Texture mappings: the padded size Lightmass bakes (bPadMappings: the interior is two texels smaller). 0 for vertex mappings.</summary>
    public int SizeX, SizeY;
    public bool IsTextureMapping => SizeX > 0;
    /// <summary>The lightmaps the result goes into (several for a shared BSP allocation); empty for occluders and shadow-only receivers.</summary>
    public abstract IReadOnlyList<LightmapRef> Targets { get; }
    /// <summary>Primitives receiving baked output, including receivers that only have shadow maps.</summary>
    public virtual IReadOnlyList<LightmapRef> OutputTargets => Targets;
    /// <summary>Allocate output only after a complete result arrives; scene construction never changes packages.</summary>
    public bool AllocateLightmap;
    /// <summary>The Swarm task type (FLightmassProcessor::Run).</summary>
    public abstract string TaskType { get; }
    /// <summary>How much work the task is, for ordering (texels or vertices).</summary>
    public abstract int Cost { get; }
    /// <summary>Whether Lightmass pads the texture mapping (bPadMappings: the lightmap UVs' 0..1 covers the interior). Terrain isn't padded.</summary>
    public virtual bool Padded => true;
}

/// <summary>One LOD of a static mesh component: a Lightmass mesh instance, with a solve task when it has compatible precomputed output.</summary>
public sealed class LightmassInstance : LightmassMapping
{
    public ExportEntry Component;
    public int LodIndex;
    public LightmassMesh Mesh;
    public Matrix4x4 LocalToWorld;
    public Vector3 BoundsMin, BoundsMax;
    public int MeshIndex, LevelId;
    public bool CastShadow, CastShadowAsTwoSided;
    /// <summary>bSelfShadowOnly: the mesh only shadows itself (GI_INSTANCE_SELFSHADOWONLY)</summary>
    public bool SelfShadowOnly;
    public LightmassElementSettings Settings = new();
    public List<LightmassLight> RelevantLights = [];
    public List<LightmassMaterial> ElementMaterials = [];
    /// <summary>The lightmap to rebake, or null for a shadow-only receiver or occluder.</summary>
    public LightmapRef Target;
    /// <summary>An existing shadow-only receiver. Its lightmap stays LMT_None.</summary>
    public LightmapRef ShadowTarget;
    public bool HasOutput => Target != null || ShadowTarget != null;

    public override IReadOnlyList<LightmapRef> Targets => Target == null ? [] : [Target];
    public override IReadOnlyList<LightmapRef> OutputTargets => ShadowTarget == null ? Targets : [ShadowTarget];
    public override string TaskType => IsTextureMapping ? "SMTextureMapping" : "SMVertexMapping";
    public override int Cost => IsTextureMapping ? SizeX * SizeY : (int)Mesh.Mesh.LODModels[LodIndex].NumVertices;
}

/// <summary>A vertex of a BSP mapping (FStaticLightingVertexData): world position, tangent basis, material UV and lightmap UV.</summary>
public readonly record struct LightmassBspVertex(Vector3 Position, Vector3 TangentX, Vector3 TangentY, Vector3 TangentZ, Vector2 TextureUV, Vector2 LightmapUV);

/// <summary>
/// BSP surfaces as one Lightmass BSP mapping, which is both a mesh and a texture mapping (FBSPSurfaceStaticLighting). A baked one is an LE3
/// surface group: the BSP nodes of every ModelComponent element sharing one lightmap allocation, with each vertex's lightmap UV its
/// ShadowTexCoord, which already places it in the group's lightmap. Baking the group's whole allocation keeps BioWare's layout. Without
/// targets it only casts shadows: Lightmass gets no task for it.
/// </summary>
public sealed class LightmassBspMapping : LightmassMapping
{
    public ExportEntry ModelExport;
    /// <summary>New charts, applied to both model vertex representations only when this mapping is written.</summary>
    public List<(int Vertex, int BufferVertex, Vector2 UV)> NewCoordinates = [];
    public List<LightmapRef> Lightmaps = [];
    public int LevelId;
    public bool CastShadowAsTwoSided;
    public Vector3 BoundsMin, BoundsMax;
    public List<LightmassLight> RelevantLights = [];
    public List<LightmassElement> Elements = [];
    public List<LightmassBspVertex> Vertices = [];
    public List<int> Indices = [];
    /// <summary>Each triangle's index into <see cref="Elements"/>.</summary>
    public List<int> TriangleElements = [];

    public override IReadOnlyList<LightmapRef> Targets => Lightmaps;
    public override string TaskType => "BSPMapping";
    public override int Cost => SizeX * SizeY;
}

/// <summary>A Terrain actor as Lightmass gets it in its own persistent channel (FBaseTerrainData, FTerrainData and its height field).</summary>
public sealed class LightmassTerrain
{
    /// <summary>Named by content, so Swarm's cache reuses the channel until the terrain changes.</summary>
    public Guid Guid;
    public ExportEntry Export;
    public PropertyCollection Properties;
    public Terrain Binary;
    public Matrix4x4 LocalToWorld;
    public int NumVerticesX, NumVerticesY, NumPatchesX, NumPatchesY, StaticLightingResolution;

    /// <summary>The height at a vertex of the terrain's grid, clamped to the grid as Lightmass clamps it (FTerrain::Height).</summary>
    public ushort Height(int x, int y) => Binary.Heights[Math.Clamp(y, 0, NumVerticesY - 1) * NumVerticesX + Math.Clamp(x, 0, NumVerticesX - 1)];

    /// <summary>TID_Visibility_Off: a hole in the terrain.</summary>
    public bool IsQuadVisible(int x, int y) =>
        ((byte)Binary.InfoData[Math.Clamp(y, 0, NumVerticesY - 1) * NumVerticesX + Math.Clamp(x, 0, NumVerticesX - 1)] & 1) == 0;
}

/// <summary>
/// A TerrainComponent's section as one Lightmass terrain mapping, which is both a mesh and a texture mapping (FTerrainComponentStaticLighting):
/// its visible quads, extended by ExpandQuads on each side, over an unpadded lightmap of (TrueSectionSize + 2·Expand)·Resolution + 1
/// texels, the size of its LE3 allocation. Without a target it only casts shadows: Lightmass gets no task for it.
/// </summary>
public sealed class LightmassTerrainMapping : LightmassMapping
{
    public LightmassTerrain Terrain;
    public ExportEntry Component;
    public LightmapRef Target;
    public int LevelId;
    public bool CastShadow, SelfShadowOnly, BilinearFilter;
    public int SectionBaseX, SectionBaseY, SectionSizeX, SectionSizeY, TrueSectionSizeX, TrueSectionSizeY, ExpandQuadsX, ExpandQuadsY;
    public Matrix4x4 LocalToWorld;
    public Vector3 BoundsMin, BoundsMax;
    public TerrainPatchBounds[] PatchBounds;
    /// <summary>The section's visible quads, relative to its base, including the expanded border.</summary>
    public List<(int X, int Y)> Quads = [];
    public List<LightmassLight> RelevantLights = [];
    public LightmassElement Element;

    public override IReadOnlyList<LightmapRef> Targets => Target == null ? [] : [Target];
    public override string TaskType => "TerrainMapping";
    public override int Cost => SizeX * SizeY;
    public override bool Padded => false;
}

/// <summary>Everything one Lightmass job needs, built from same-game Legendary Edition levels that are lit together.</summary>
public sealed class LightmassScene
{
    public List<IMEPackage> Levels = [];
    public List<LightmassLight> Lights = [];
    public List<LightmassMesh> Meshes = [];
    public List<LightmassMaterial> Materials = [];
    public List<LightmassInstance> Instances = [];
    public List<LightmassBspMapping> BspMappings = [];
    public List<LightmassTerrain> Terrains = [];
    public List<LightmassTerrainMapping> TerrainMappings = [];
    public List<(Vector3 Min, Vector3 Max)> ImportanceVolumes = [];
    public LightmassWorldSettings World = new();
    /// <summary>What was left out of the bake and why, with counts.</summary>
    public SortedDictionary<string, int> Report = new();

    /// <summary>The mappings to bake: those with a lightmap or shadow-only output to write.</summary>
    public IEnumerable<LightmassMapping> Mappings => BspMappings.Where(b => b.Lightmaps.Count > 0).Cast<LightmassMapping>()
        .Concat(Instances.Where(i => i.HasOutput)).Concat(TerrainMappings.Where(t => t.Target != null));

    public void Count(string why) => Report[why] = Report.GetValueOrDefault(why) + 1;
}

public sealed class LightmassSceneOptions
{
    public LightAssignmentPolicy AssignmentPolicy = LightAssignmentPolicy.RecomputeAssignments;
    /// <summary>
    /// Renders materials with the game's shaders for their diffuse, emissive and opacity (see <see cref="LightmassMaterialCapture"/>).
    /// Null: every material is a constant <see cref="DefaultAlbedo"/>.
    /// </summary>
    public LightmassMaterialCapture MaterialCapture;

    /// <summary>The material sample resolutions ([DevOptions.StaticLightingMaterial] in Lightmass's ini), before each material's ExportResolutionScale.</summary>
    public int EmissiveSampleSize = 128, DiffuseSampleSize = 128, TransmissionSampleSize = 256;

    /// <summary>Linear albedo of materials that can't be rendered (or of all of them, without <see cref="MaterialCapture"/>).</summary>
    public float DefaultAlbedo = 0.18f;

    /// <summary>Levels that are part of the scene (they cast shadows and bounce light) but whose lightmaps aren't baked.</summary>
    public HashSet<IMEPackage> ContextOnly = [];
}

/// <summary>
/// Builds a <see cref="LightmassScene"/> the way UE3's lighting build gathers one (UnStaticLightingSystem.cpp, UnStaticMeshLight.cpp):
/// static lights, static mesh components with static shadowing, per-LOD meshes that don't shadow each other's LODs, and the lights
/// each primitive accepts (ULightComponent::AffectsPrimitive). Missing mappings are allocated where geometry and cooked shader
/// policies permit it; otherwise existing compatible shadow-only mappings can still receive shadows.
/// </summary>
public sealed class LightmassSceneBuilder : IDisposable
{
    /// <summary>UE3 WORLD_MAX: how far away a directional light's Lightmass position is.</summary>
    const float WorldMax = 524288f;

    /// <summary>
    /// The texture mapping size of a mesh that only casts shadows. Lightmass needs a mapping for every mesh (FStaticLightingSystem checks
    /// Meshes.Num() == AllMappings.Num()) and caches irradiance photons on all of them, but an occluder's gets no task, so a token size.
    /// </summary>
    public const int OccluderMappingSize = 4;

    readonly LightmassSceneOptions options;
    readonly PackageCache packageCache = new();
    readonly Dictionary<IMEPackage, MeshResolver> meshResolvers = new();
    readonly Dictionary<string, LightmassMesh> meshes = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<Guid, LightmassMaterial> materials = new();
    readonly Dictionary<string, MaterialInfo> materialInfos = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Every light GUID some lightmap in the levels lists (see <see cref="RelevantLights"/>).</summary>
    readonly HashSet<Guid> vanillaListed = [];
    /// <summary>The lights (LightGuid) some shadow map in the levels is for, and whether their 2D ones are signed distance fields.</summary>
    readonly Dictionary<Guid, bool> vanillaShadowed = [];
    bool hasShadowOnlyReceivers;
    LightmassScene scene;

    /// <summary>Why lights do or don't reach primitives, tallied per light type and added to the report once the scene is built.</summary>
    readonly Dictionary<(SceneLightType Type, string Why), int> relevance = new();

    public LightmassSceneBuilder(LightmassSceneOptions options = null) => this.options = options ?? new LightmassSceneOptions();

    /// <summary>Builds the scene. A builder builds one scene: its caches are for the levels it was given.</summary>
    /// <param name="levels">The persistent level first, then its streaming levels.</param>
    public LightmassScene Build(IReadOnlyList<IMEPackage> levels, Action<string> log = null)
    {
        if (scene != null) throw new InvalidOperationException("A LightmassSceneBuilder builds one scene; use a new builder for another.");
        foreach (var package in levels) LightmassBakeJob.ValidateGame(package.Game, levels[0].Game);
        scene = new LightmassScene { Levels = levels.ToList() };
        log?.Invoke($"Light assignment policy: {options.AssignmentPolicy.Describe()}");
        if (levels.Count > 0 && levels[0].Exports.FirstOrDefault(e => e.ClassName.EndsWith("WorldInfo") && e.ObjectName.Name.EndsWith("WorldInfo")) is { } worldInfo)
            scene.World = LightmassWorldSettings.FromWorldInfo(worldInfo);

        var levelBinaries = new List<(IMEPackage Package, Level Level)>();
        foreach (var package in levels)
        {
            hasShadowOnlyReceivers |= package.Exports.Where(e => e.ClassName == "StaticMeshComponent")
                .Any(e => ObjectBinary.From<StaticMeshComponent>(e).LODData.Any(l => l.LightMap.LightMapType == ELightMapType.LMT_None
                    && (l.ShadowMaps.Length > 0 || l.ShadowVertexBuffers.Length > 0)));
            var levelExport = package.Exports.FirstOrDefault(e => e.ClassName == "Level" && e.ObjectName.Name == "PersistentLevel");
            if (levelExport == null) { scene.Count($"{package.FileNameNoExtension}: no PersistentLevel"); continue; }
            levelBinaries.Add((package, ObjectBinary.From<Level>(levelExport)));
        }

        foreach (var package in levels)
        {
            foreach (var r in PackageLightmaps.Enumerate(package))
                vanillaListed.UnionWith(PrimitiveLightAssignments.Guids(r.LightMap));
            foreach (var (guid, sdf) in PackageShadowMaps.Lights(package))
                vanillaShadowed[guid] = vanillaShadowed.GetValueOrDefault(guid) || sdf;
        }
        for (int levelId = 0; levelId < levelBinaries.Count; levelId++)
            AddLights(levelBinaries[levelId].Level, levelId);
        log?.Invoke($"{scene.Lights.Count(l => !l.ShadowOnly)} static lights, {scene.Lights.Count(l => l.ShadowOnly || l.Light.IsDominant)} with shadow maps");

        int meshIndex = 0;
        for (int levelId = 0; levelId < levelBinaries.Count; levelId++)
        {
            var (package, level) = levelBinaries[levelId];
            AddImportanceVolumes(package, level);
            foreach (var (component, ownerClass) in StaticMeshComponents(package, level))
            {
                try { AddComponent(component, ownerClass, levelId, ref meshIndex); }
                catch (Exception e) { scene.Count($"component failed to load: {e.GetType().Name}"); log?.Invoke($"{component.InstancedFullPath}: {e.Message}"); }
            }
            try { AddModel(package, level, levelId); }
            catch (Exception e) { scene.Count($"BSP failed to load: {e.GetType().Name}"); log?.Invoke($"{package.FileNameNoExtension} BSP: {e.Message}"); }
            try { AddTerrains(package, level, levelId); }
            catch (Exception e) { scene.Count($"terrain failed to load: {e.GetType().Name}"); log?.Invoke($"{package.FileNameNoExtension} terrain: {e.Message}"); }
        }
        scene.Meshes = meshes.Values.ToList();
        scene.Materials = materials.Values.ToList();
        foreach (var ((type, why), n) in relevance) scene.Report[$"relevance: {type} {why}"] = n;
        foreach (var (min, max) in scene.ImportanceVolumes) log?.Invoke($"importance volume {min} .. {max}");
        log?.Invoke($"{scene.Instances.Count} mesh instances ({scene.Instances.Count(i => i.HasOutput)} to bake) of {scene.Meshes.Count} meshes, " +
                    $"{scene.BspMappings.Count} BSP mappings ({scene.BspMappings.Count(b => b.Lightmaps.Count > 0)} to bake), " +
                    $"{scene.TerrainMappings.Count} terrain sections ({scene.TerrainMappings.Count(t => t.Target != null)} to bake), {scene.Materials.Count} materials, " +
                    $"{scene.ImportanceVolumes.Count} importance volumes");
        return scene;
    }

    /// <summary>
    /// An object's properties with everything it inherits: its archetypes', then its class default object's. A cooked template's
    /// archetype chain can end before the class default, where UE3 falls back to it; e.g. sky lights' CastShadows=False is only on
    /// Default__SkyLightComponent, so without it every sky light would cast shadows.
    /// </summary>
    PropertyCollection Properties(ExportEntry export) =>
        effectiveProperties.TryGetValue(export, out var props) ? props : effectiveProperties[export] = EffectiveLightingProperties.Read(export, packageCache);

    //the levels don't change during a build, so each object's properties are read once (a collection actor's for each of its many components).
    //Class defaults are read once per package cache
    readonly Dictionary<ExportEntry, PropertyCollection> effectiveProperties = [];

    // ================= lights =================

    void AddLights(Level level, int levelId)
    {
        var used = scene.Lights.Select(l => l.Guid).ToHashSet();
        foreach (var light in SceneLight.LoadLevelLights(level, packageCache))
        {
            string kind = light.Export.ClassName;
            if (light.HasLightEnvironment) { scene.Count($"light: {kind} in a light environment"); continue; }
            var props = Properties(light.Export);
            bool castShadows = props.GetProp<BoolProperty>("CastShadows") is not { Value: false };
            bool castStaticShadows = light.CastsStaticShadows;
            bool vanillaShadowMapped = vanillaShadowed.TryGetValue(light.LightGuid, out bool vanillaSdf);
            // Lights with static lighting bake their direct and bounce light. Dominant lights render direct light dynamically over
            // precomputed shadows, and bake only their bounce light (UE3 Lightmass's GI_LIGHT_DOMINANT handling).
            bool lit = light.HasStaticLighting || light.IsDominant && light.HasStaticShadowing;
            // Shadow maps: the light is rendered dynamically over precomputed shadows. UE3 gives them to lights with static shadowing but no
            // static lighting (toggleable lights); BioWare's bakes also gave them to static lights on some primitives.
            bool shadowTwin = LightAssignment.HasShadowMapEntry(light, options.AssignmentPolicy, vanillaShadowMapped, hasShadowOnlyReceivers);
            bool indirectOnly = light.Export.Game == MEGame.LE1 && props.GetProp<BoolProperty>("bBakedGIOnly") is { Value: true };
            lit |= indirectOnly && light.HasStaticShadowing;
            if (!lit && !shadowTwin) { scene.Count($"light: {kind} without static lighting or shadowing"); continue; }
            if (indirectOnly) shadowTwin = false;
            if (shadowTwin)
            {
                var twin = Light(light, props, levelId);
                twin.Guid = LightmassFormat.HashGuid($"shadow|{light.LightGuid}|{light.LightmapGuid}|{light.Export.InstancedFullPath}");
                if (!used.Add(twin.Guid)) twin.Guid = Guid.NewGuid();
                twin.ShadowOnly = true;
                twin.IndirectLightingScale = 0;
                twin.SignedDistanceField = vanillaShadowMapped && vanillaSdf;
                twin.Flags = LightFlags.CastShadows | LightFlags.CastStaticShadows | LightFlags.HasStaticShadowing
                    | (twin.SignedDistanceField ? LightFlags.UseSignedDistanceFieldShadows : 0);
                scene.Lights.Add(twin);
                scene.Count($"light: {light.Type} shadow maps{(twin.SignedDistanceField ? " (distance field)" : "")}");
            }
            if (!lit) continue;
            var result = Light(light, props, levelId);
            // A light copied without a new GUID would merge with the original in Lightmass.
            if (result.Guid == Guid.Empty || !used.Add(result.Guid))
            {
                scene.Count("light: missing or duplicate LightmapGuid (given a new one for the bake)");
                result.Guid = Guid.NewGuid();
                used.Add(result.Guid);
            }

            if (castShadows) result.Flags |= LightFlags.CastShadows;
            if (castStaticShadows) result.Flags |= LightFlags.CastStaticShadows;
            if (light.HasStaticShadowing) result.Flags |= LightFlags.HasStaticShadowing;
            if (light.IsDominant)
            {
                // UE3 always gives dominant lights distance field shadows; BioWare's bakes gave some visibility ones.
                result.SignedDistanceField = !vanillaShadowMapped || vanillaSdf;
                result.Flags |= LightFlags.HasStaticLighting | LightFlags.Dominant | (result.SignedDistanceField ? LightFlags.UseSignedDistanceFieldShadows : 0);
            }
            else
            {
                // LE3's lightmaps hold their lights' direct lighting (they list them in LightGuids), so it goes in the direct lightmap
                // rather than a shadow map, whatever UseDirectLightMap says. Primitives that get a shadow map instead get the twin.
                result.Flags |= LightFlags.HasStaticLighting | LightFlags.UseDirectLightMap;
            }
            ApplyGiOverrides(result, props);
            if (result.IndirectOnly && light.Type == SceneLightType.Sky)
                scene.Count("light: GI-only sky has no contribution (UDK sky lights do not bounce)");
            scene.Lights.Add(result);
            scene.Count($"light: {light.Type}{(light.IsDominant ? " (dominant)" : "")} baked");
        }
    }

    /// <summary>LE1 editor flag interpretation: exclude GI independently of excluding direct light.</summary>
    public static void ApplyGiOverrides(LightmassLight light, PropertyCollection props)
    {
        if (light.Light.Export.Game != MEGame.LE1) return;
        if (props.GetProp<BoolProperty>("bIncludeInGI") is { Value: false }) light.IndirectLightingScale = 0;
        if (props.GetProp<BoolProperty>("bBakedGIOnly") is not { Value: true }) return;
        light.IndirectOnly = true;
        // FLight::UseStaticLighting(false) is false with this combination, while photons still carry indirect energy.
        light.Flags = (light.Flags | LightFlags.HasStaticLighting | LightFlags.HasStaticShadowing)
            & ~(LightFlags.UseDirectLightMap | LightFlags.Dominant | LightFlags.UseSignedDistanceFieldShadows);
        light.SignedDistanceField = false;
        // UDK sky lighting bypasses UseStaticLighting and has no bounced sky contribution.
        if (light.Light.Type == SceneLightType.Sky) light.Brightness = light.LowerBrightness = 0;
    }

    /// <summary>A level light's Lightmass fields (FLightData and its type's struct) without flags, named by its LightmapGuid.</summary>
    static LightmassLight Light(SceneLight light, PropertyCollection props, int levelId)
    {
        var lm = props.GetProp<StructProperty>("LightmassSettings");
        float L(string name, float fallback) => lm?.GetProp<FloatProperty>(name)?.Value ?? fallback;
        static (byte, byte, byte) Bytes(StructProperty c) => c == null ? ((byte)255, (byte)255, (byte)255)
            : (c.GetProp<ByteProperty>("R")?.Value ?? 255, c.GetProp<ByteProperty>("G")?.Value ?? 255, c.GetProp<ByteProperty>("B")?.Value ?? 255);
        return new LightmassLight
        {
            Light = light,
            Guid = light.LightmapGuid,
            LevelId = levelId,
            Color = Bytes(props.GetProp<StructProperty>("LightColor")),
            Brightness = props.GetProp<FloatProperty>("Brightness")?.Value ?? 1,
            LightSourceRadius = L("LightSourceRadius", 100),
            IndirectLightingScale = L("IndirectLightingScale", 1),
            IndirectLightingSaturation = L("IndirectLightingSaturation", 1),
            ShadowExponent = L("ShadowExponent", 2),
            LightSourceAngle = L("LightSourceAngle", 3) * MathF.PI / 180f,
            InnerConeAngle = props.GetProp<FloatProperty>("InnerConeAngle")?.Value ?? 0,
            OuterConeAngle = props.GetProp<FloatProperty>("OuterConeAngle")?.Value ?? 44,
            LowerBrightness = props.GetProp<FloatProperty>("LowerBrightness")?.Value ?? 0,
            LowerColor = Bytes(props.GetProp<StructProperty>("LowerColor")),
        };
    }

    // ================= meshes =================

    /// <summary>Static mesh components of the level's actors, with their owner's class (collection actors list theirs in their binary).</summary>
    static IEnumerable<(ExportEntry Component, string OwnerClass)> StaticMeshComponents(IMEPackage package, Level level)
    {
        var actors = level.Actors.Where(u => u != 0 && package.IsUExport(u)).Select(u => package.GetUExport(u)).ToHashSet();
        var seen = new HashSet<ExportEntry>();
        foreach (var actor in actors)
        {
            if (actor.ClassName != "StaticMeshCollectionActor") continue;
            foreach (int u in ObjectBinary.From<StaticMeshCollectionActor>(actor).Components)
                if (package.TryGetUExport(u, out var c) && c.ClassName == "StaticMeshComponent" && seen.Add(c))
                    yield return (c, actor.ClassName);
        }
        foreach (var export in package.Exports)
        {
            if (export.ClassName != "StaticMeshComponent" || export.Parent is not ExportEntry owner || !actors.Contains(owner) || !seen.Add(export)) continue;
            yield return (export, owner.ClassName);
        }
    }

    MeshResolver Resolver(IMEPackage package) => meshResolvers.TryGetValue(package, out var r) ? r : meshResolvers[package] = new MeshResolver(package);

    void AddComponent(ExportEntry component, string ownerClass, int levelId, ref int meshIndex)
    {
        var props = Properties(component);
        var smc = ObjectBinary.From<StaticMeshComponent>(component);
        bool hasLightmap = smc.LODData.Any(l => IsRebakeable(l.LightMap));
        // UE3 builds static lighting only for primitives with static shadowing that accept lights (UStaticMeshComponent::GetStaticLightingInfo): the
        // others neither receive nor cast static shadows. Unless bUsePrecomputedShadows says otherwise, static mesh actors, collections and anything
        // BioWare baked (InterpActors that never move, ...) have static shadowing. Neither visibility nor a light environment changes that; a light
        // environment only keeps the level's lights off the primitive (ULightComponent::AffectsPrimitive).
        bool staticShadowing = LightAssignment.HasStaticShadowing(component, props, smc);
        bool acceptsLights = props.GetProp<BoolProperty>("bAcceptsLights") is not { Value: false };
        bool contextOnly = options.ContextOnly.Contains(component.FileRef);
        if (!staticShadowing || !acceptsLights)
        {
            // An existing lightmap stays as it is, and the report says why.
            scene.Count(hasLightmap && !contextOnly
                ? $"mesh: existing lightmap kept, {(staticShadowing ? "doesn't accept lights" : "without static shadowing")}"
                : $"mesh: {ownerClass} without static lighting");
            return;
        }
        bool usesEnvironment = EffectiveLightingProperties.UsesLightEnvironment(component, props, component.Parent as ExportEntry, packageCache, out _);
        var (meshProp, meshPropertyPackage) = EffectiveLightingProperties.Reference(component, "StaticMesh", packageCache);
        if (meshProp is not { Value: not 0 }) { scene.Count("mesh: no StaticMesh"); return; }

        var resolver = Resolver(component.FileRef);
        var meshEntry = meshPropertyPackage.GetEntry(meshProp.Value);
        var (meshExport, staticMesh) = resolver.Get(meshEntry);
        if (staticMesh == null || staticMesh.LODModels.Length == 0)
        {
            scene.Count(resolver.Failure(meshEntry) is { } why ? $"mesh: StaticMesh import failed ({why})" : "mesh: StaticMesh not resolved");
            return;
        }
        var mesh = GetMesh(meshExport, staticMesh);

        var shadowSettings = ShadowParticipation.Read(props);
        bool castShadow = shadowSettings.BakesShadow;
        var channels = LightingChannels.FromProperty(props.GetProp<StructProperty>("LightingChannels"), default, isInitialized: false, LightingChannels.StaticPrimitiveDefault);
        var settings = LightmassElementSettings.From(props.GetProp<StructProperty>("LightmassSettings"), scene.World.EmissiveBoost);
        // Inherited like the mesh, from archetypes in other packages too
        var (materialOverrides, materialOverridePackage) = EffectiveLightingProperties.Inherited<ArrayProperty<ObjectProperty>>(component, "Materials", packageCache);
        var l2w = WorldTransforms.Component(component);
        int thisMeshIndex = meshIndex++;
        string levelName = component.FileRef.FileNameNoExtension;
        bool bakesEnvironmentLitMesh = false, castsEnvironmentLitMesh = false;

        for (int lod = 0; lod < staticMesh.LODModels.Length; lod++)
        {
            var lodModel = staticMesh.LODModels[lod];
            var lightmap = lod < smc.LODData.Length ? smc.LODData[lod].LightMap : null;
            bool allocate = !contextOnly && !usesEnvironment && (lightmap == null || lightmap.LightMapType == ELightMapType.LMT_None);
            var receiver = new LightmapRef(component, $"LOD{lod}", lightmap, smc);
            var shadowLayout = !contextOnly && lightmap?.LightMapType == ELightMapType.LMT_None
                ? LightmapMapping.ShadowAllocation(receiver, lodModel, mesh.LightMapCoordinateIndex) : null;
            if (!contextOnly && lightmap?.LightMapType == ELightMapType.LMT_None && shadowLayout == null && PackageShadowMaps.Of(receiver).Count > 0)
                scene.Count("mapping: incompatible shadow-only allocation, UV channel or vertex count (kept)");
            bool bake = lightmap != null && IsRebakeable(lightmap) && !contextOnly;
            // UE3 makes a mesh per LOD; LODs other than 0 neither shadow other meshes nor need exporting without a lightmap.
            if (lod > 0 && !bake && shadowLayout == null && !allocate) continue;
            if (!bake && shadowLayout == null && !allocate && !castShadow) continue;

            var (min, max, receiverBounds) = MeshBounds(lodModel.PositionVertexBuffer.VertexData, l2w);
            var instance = new LightmassInstance
            {
                Guid = Guid.NewGuid(),
                Component = component,
                LodIndex = lod,
                Mesh = mesh,
                LocalToWorld = l2w,
                BoundsMin = min, BoundsMax = max,
                MeshIndex = thisMeshIndex,
                LevelId = levelId,
                CastShadow = castShadow,
                CastShadowAsTwoSided = shadowSettings.TwoSided,
                SelfShadowOnly = shadowSettings.SelfShadowOnly,
                Settings = settings,
            };

            var materialEntries = new List<IEntry>();
            for (int e = 0; e < lodModel.Elements.Length; e++)
            {
                var element = lodModel.Elements[e];
                int materialIndex = element.MaterialIndex;
                IEntry materialEntry = null;
                if (materialOverrides != null && materialIndex >= 0 && materialIndex < materialOverrides.Count && materialOverrides[materialIndex].Value != 0)
                    materialEntry = materialOverridePackage.GetEntry(materialOverrides[materialIndex].Value);
                else if (element.Material != 0)
                    materialEntry = meshExport.FileRef.GetEntry(element.Material);
                instance.ElementMaterials.Add(GetMaterial(materialEntry, settings.DiffuseBoost));
                materialEntries.Add(materialEntry);
            }

            if (allocate && lodModel.PositionVertexBuffer.VertexData.Length > 0)
            {
                var size = LightmassMappingAllocator.MeshSize(props, Properties(meshExport), lod,
                    LightmassMappingAllocator.HasUsableUv(lodModel, mesh.LightMapCoordinateIndex));
                bool supported = materialEntries.Count > 0 && materialEntries.All(m => SupportsNewLightmap(m, size.Width > 0));
                // A valid vertex policy is a useful fallback when a texture policy was not cooked for this material.
                if (!supported && size.Width > 0 && materialEntries.Count > 0 && materialEntries.All(m => SupportsNewLightmap(m, false)))
                {
                    size = (0, 0);
                    supported = true;
                }
                if (supported)
                {
                    instance.AllocateLightmap = true;
                    instance.Target = receiver;
                    (instance.SizeX, instance.SizeY) = size;
                    shadowLayout = null;
                    scene.Count(instance.IsTextureMapping ? "mapping: new LMT_2D" : "mapping: new LMT_1D");
                }
                else scene.Count("mapping: new lightmap unavailable (material has no compatible cooked shaders)");
            }

            if (bake)
            {
                if (!IsCompatible(component.FileRef, lightmap, lodModel, mesh.LightMapCoordinateIndex))
                    scene.Count("mapping: incompatible allocation, UV channel or vertex sample count (kept)");
                else
                {
                    instance.Target = new LightmapRef(component, $"LOD{lod}", lightmap, smc);
                    if (TextureAllocation(component.FileRef, lightmap) is { } size)
                        (instance.SizeX, instance.SizeY) = size;
                    scene.Count($"mapping: {lightmap.LightMapType}");
                }
            }
            if (shadowLayout is { } shadowSize)
            {
                instance.ShadowTarget = receiver;
                (instance.SizeX, instance.SizeY) = shadowSize;
                scene.Count(instance.IsTextureMapping ? "mapping: texture shadows only" : "mapping: vertex shadows only");
            }
            if (!instance.HasOutput && (lod > 0 || !castShadow)) continue;
            if (!instance.HasOutput) instance.SizeX = instance.SizeY = OccluderMappingSize;

            instance.RelevantLights = RelevantLights(receiverBounds, lightmap, PackageShadowMaps.LightGuids(new LightmapRef(component, $"LOD{lod}", lightmap, smc)),
                new LightPrimitiveSettings(channels, levelName, AcceptsDynamicLights: props.GetProp<BoolProperty>("bAcceptsDynamicLights") is not { Value: false },
                    UsesLightEnvironment: usesEnvironment), shadowOnlyTarget: instance.ShadowTarget != null);
            scene.Instances.Add(instance);
            bakesEnvironmentLitMesh |= usesEnvironment && instance.Target != null;
            castsEnvironmentLitMesh |= usesEnvironment && instance.Target == null;
        }
        // per exported component; level lights don't reach a primitive a light environment lights (ULightComponent::AffectsPrimitive)
        if (bakesEnvironmentLitMesh) scene.Count("mesh: lit by a light environment (baked with bounced light only)");
        else if (castsEnvironmentLitMesh) scene.Count("mesh: lit by a light environment (shadow caster only)");
    }

    readonly Dictionary<(IEntry Material, bool Texture), bool> allocationPolicies = new();
    readonly Dictionary<IMEPackage, ShaderCache> allocationShaderCaches = new();

    bool SupportsNewLightmap(IEntry entry, bool texture)
    {
        if (entry == null) return false;
        if (allocationPolicies.TryGetValue((entry, texture), out bool supported)) return supported;
        try
        {
            if (ReadMaterial(entry).Unlit) return allocationPolicies[(entry, texture)] = true;
            var current = Resolve(entry);
            for (int depth = 0; current != null && depth < 16; depth++)
            {
                var props = current.GetProperties();
                if (current.ClassName == "Material" || props.GetProp<BoolProperty>("bHasStaticPermutationResource") is { Value: true })
                {
                    string policy = texture ? "FDirectionalLightMapTexturePolicy" : "FDirectionalVertexLightMapPolicy";
                    ShaderCache Cache(IMEPackage p)
                    {
                        if (!allocationShaderCaches.TryGetValue(p, out var cache))
                            allocationShaderCaches[p] = cache = p.FindExport("SeekFreeShaderCache", "ShaderCache") is { } e ? ObjectBinary.From<ShaderCache>(e) : null;
                        return cache;
                    }
                    var (_, shaders) = LegendaryExplorerCore.Shaders.ShaderCacheManipulator.GetMaterialShaderMapAndShaders(current, Cache,
                        $"TBasePassVertexShader{policy}FNoDensityPolicy", $"TBasePassPixelShader{policy}NoSkyLight");
                    return allocationPolicies[(entry, texture)] = shaders.All(s => s != null);
                }
                current = props.GetProp<ObjectProperty>("Parent") is { Value: not 0 } parent ? Resolve(current.FileRef.GetEntry(parent.Value)) : null;
            }
        }
        catch (Exception e) { scene.Count($"mapping: shader policy lookup failed ({e.GetType().Name})"); }
        return allocationPolicies[(entry, texture)] = false;
    }

    /// <summary>
    /// Shared preview/bake eligibility followed by bake output routing. Preserve mode retains BioWare's per-primitive
    /// assignments for compatibility; recompute uses current settings and lets Lightmass resolve current occlusion.
    /// Allocation is independent of assignment policy. Static lights bake direct light, dominant lights bake bounce and
    /// shadows, and toggleable lights bake shadows without adding a duplicate direct contribution.
    /// </summary>
    /// <param name="vanillaLightmap">The primitive's LE3 lightmap; null when it has none to go by (an occluder).</param>
    /// <param name="vanillaShadows">The lights (LightGuid) its LE3 shadow maps are for.</param>
    /// <param name="primitive">The primitive's settings. Only primitives that accept lights get static lighting</param>
    List<LightmassLight> RelevantLights(BoxSphereBounds bounds, LightMap vanillaLightmap, IReadOnlySet<Guid> vanillaShadows, LightPrimitiveSettings primitive,
        bool shadowOnlyTarget = false)
    {
        var source = PrimitiveLightAssignments.Create(vanillaLightmap, vanillaShadows);
        var result = new List<LightmassLight>();
        foreach (var light in scene.Lights)
        {
            var l = light.Light;
            bool known = vanillaListed.Contains(l.LightmapGuid) || vanillaShadowed.ContainsKey(l.LightGuid);
            var eligibility = LightAssignment.Evaluate(l, bounds, primitive);
            if (eligibility == LightEligibility.Affects)
                eligibility = LightAssignment.EvaluateHistory(l, options.AssignmentPolicy, known, source);
            string why = eligibility.Describe();
            if (eligibility == LightEligibility.Affects)
            {
                // A light is in the scene once per output (its main entry and any shadow-only twin); a primitive gets at most one of them.
                if (!light.IndirectOnly && !LightAssignment.RoutesToBake(l, light.ShadowOnly, options.AssignmentPolicy, known, source, shadowOnlyTarget))
                    why = light.ShadowOnly ? "affects, but its shadow map isn't baked here" : "affects, but its main entry isn't baked here";
                else
                {
                    if (light.ShadowOnly) why = "shadow map affects";
                    result.Add(light);
                }
            }
            relevance[(l.Type, why)] = relevance.GetValueOrDefault((l.Type, why)) + 1;
        }
        return result;
    }

    /// <summary>
    /// A mesh's world-space box, and its bounds as UE3 gives a primitive (its local bounds transformed, as UPrimitiveComponent::Bounds) for
    /// deciding which lights reach it
    /// </summary>
    static (Vector3 Min, Vector3 Max, BoxSphereBounds Receiver) MeshBounds(Vector3[] positions, Matrix4x4 l2w)
    {
        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        var local = new Box();
        foreach (var p in positions)
        {
            local.Add(p);
            var w = Vector3.Transform(p, l2w);
            min = Vector3.Min(min, w); max = Vector3.Max(max, w);
        }
        return (min, max, new BoxSphereBounds(local).TransformBy(l2w));
    }

    static BoxSphereBounds BoundsOf(Vector3 min, Vector3 max) =>
        new() { Origin = (min + max) / 2, BoxExtent = (max - min) / 2, SphereRadius = Vector3.Distance(min, max) / 2 };

    LightmassMesh GetMesh(ExportEntry export, StaticMesh staticMesh)
    {
        string key = export.FileRef.FilePath + "|" + export.InstancedFullPath;
        if (meshes.TryGetValue(key, out var mesh)) return mesh;
        // Named by content, so Swarm's cache reuses the channel across bakes until the mesh changes.
        var data = export.Data;
        mesh = new LightmassMesh
        {
            Guid = LightmassFormat.HashGuid("mesh|" + export.InstancedFullPath + "|" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data))),
            Export = export,
            Mesh = staticMesh,
            LightMapCoordinateIndex = ComponentGeometry.CoordinateIndex(export),
        };
        return meshes[key] = mesh;
    }

    // ================= BSP =================

    const int PF_Portal = 0x04000000;

    /// <summary>
    /// The level's BSP: every node of its ModelComponents' elements, as UE3 lights them (UModel::GroupAllNodes, FBSPSurfaceStaticLighting).
    /// Elements with an LE3 lightmap to rebake are grouped by the allocation they share, one mapping each; all other nodes (unlit, in a
    /// context-only level) still cast shadows, through one occluder mapping per level.
    /// </summary>
    void AddModel(IMEPackage package, Level level, int levelId)
    {
        if (level.Model == 0 || !package.TryGetUExport(level.Model, out var modelExport)) return;
        var model = ObjectBinary.From<Model>(modelExport);
        if (model.Nodes.Length == 0) return;
        bool contextOnly = options.ContextOnly.Contains(package);

        var groups = new Dictionary<(int Texture, Vector2 Scale, Vector2 Bias), (List<LightmapRef> Lightmaps, List<int> Nodes)>();
        var occluderNodes = new List<int>();
        var seen = new HashSet<int>();
        foreach (int u in level.ModelComponents)
        {
            if (!package.TryGetUExport(u, out var componentExport) || componentExport.ClassName != "ModelComponent") continue;
            var component = ObjectBinary.From<ModelComponent>(componentExport);
            for (int e = 0; e < component.Elements.Length; e++)
            {
                var element = component.Elements[e];
                var nodes = element.Nodes.Select(n => (int)n).Where(n => n < model.Nodes.Length && seen.Add(n)).ToList();
                // BSP lightmaps are only ever LMT_2D in LE3.
                if (contextOnly || element.LightMap is not LightMap_2D { LightMapType: ELightMapType.LMT_2D, Texture1: not 0 } lightmap)
                {
                    if (!contextOnly && element.LightMap.LightMapType == ELightMapType.LMT_None
                        && nodes.Count > 0 && nodes.All(n => SupportsNewLightmap(package.GetEntry(model.Surfs[model.Nodes[n].iSurf].Material), true)))
                    {
                        var allocation = BspMapping(package, model, nodes, levelId);
                        if (AllocateBspCharts(allocation, model, nodes))
                        {
                            allocation.ModelExport = modelExport;
                            allocation.AllocateLightmap = true;
                            allocation.Lightmaps.Add(new LightmapRef(componentExport, $"Element{e}", element.LightMap, component));
                            allocation.RelevantLights = RelevantLights(BoundsOf(allocation.BoundsMin, allocation.BoundsMax), element.LightMap,
                                PackageShadowMaps.LightGuids(allocation.Lightmaps[0]), new LightPrimitiveSettings(BspChannels(model, nodes), package.FileNameNoExtension));
                            scene.BspMappings.Add(allocation);
                            scene.Count("mapping: new BSP LMT_2D surface group");
                            continue;
                        }
                        scene.Count("mapping: BSP new chart layout unavailable (kept)");
                    }
                    if (!contextOnly && element.LightMap is { LightMapType: not ELightMapType.LMT_None }) scene.Count($"mapping: BSP {element.LightMap.LightMapType} (kept)");
                    occluderNodes.AddRange(nodes);
                    continue;
                }
                var key = (lightmap.Texture1, lightmap.CoordinateScale, lightmap.CoordinateBias);
                if (!groups.TryGetValue(key, out var group)) groups[key] = group = ([], []);
                // Named as PackageLightmaps names it, which is how the rewriter asks for its lighting.
                group.Lightmaps.Add(new LightmapRef(componentExport, $"Element{e}", element.LightMap, component));
                group.Nodes.AddRange(nodes);
            }
        }

        foreach (var ((texture, scale, bias), (lightmaps, nodes)) in groups)
        {
            var mapping = BspMapping(package, model, nodes, levelId);
            if (mapping.Indices.Count == 0 || !package.TryGetUExport(texture, out var textureExport))
            {
                scene.Count("mapping: BSP surface group without triangles or texture (kept)");
                continue;
            }
            // Each vertex's ShadowTexCoord spans the allocation, which becomes the mapping's interior; Lightmass adds the padding ring.
            var p = textureExport.GetProperties();
            mapping.SizeX = (int)MathF.Round(scale.X * (p.GetProp<IntProperty>("SizeX")?.Value ?? 0)) + 2;
            mapping.SizeY = (int)MathF.Round(scale.Y * (p.GetProp<IntProperty>("SizeY")?.Value ?? 0)) + 2;
            mapping.Lightmaps = lightmaps;
            mapping.RelevantLights = RelevantLights(BoundsOf(mapping.BoundsMin, mapping.BoundsMax), lightmaps[0].LightMap,
                lightmaps.SelectMany(PackageShadowMaps.LightGuids).ToHashSet(), new LightPrimitiveSettings(BspChannels(model, nodes), package.FileNameNoExtension));
            scene.BspMappings.Add(mapping);
            scene.Count("mapping: BSP LMT_2D surface group");
        }

        if (occluderNodes.Count > 0 && BspMapping(package, model, occluderNodes, levelId) is { Indices.Count: > 0 } occluder)
        {
            occluder.SizeX = occluder.SizeY = OccluderMappingSize;
            scene.BspMappings.Add(occluder);
            scene.Count("BSP: shadow-casting surfaces without a lightmap to rebake");
        }
    }

    /// <summary>One padded planar chart per BSP node, using its surface's world-units-per-texel setting.</summary>
    public static bool AllocateBspCharts(LightmassBspMapping mapping, Model model, List<int> nodes)
    {
        var layout = new TextureLayout(4, 4, 2048, 2048);
        var charts = new List<(int First, int Node, Vector2[] UV)>();
        int first = 0;
        foreach (int index in nodes)
        {
            var node = model.Nodes[index];
            if (node.NumVertices < 3 || node.iSurf < 0 || node.iSurf >= model.Surfs.Length) continue;
            var surface = model.Surfs[node.iSurf];
            if ((surface.PolyFlags & PF_Portal) != 0) continue;
            // Two-sided BSP has separate front/back charts and triangles; do not alias their lighting.
            if ((surface.PolyFlags & 0x100) != 0 || node.iVertexIndex < 0 || node.iVertexIndex + node.NumVertices > model.VertexBuffer.Length) return false;
            var normal = SafeNormalize(model.Vectors[surface.vNormal]);
            var xAxis = SafeNormalize(model.Vectors[surface.vTextureU]);
            var yAxis = SafeNormalize(Vector3.Cross(normal, xAxis));
            if (xAxis.LengthSquared() < 0.5f || yAxis.LengthSquared() < 0.5f) return false;
            float density = float.IsFinite(surface.ShadowMapScale) && surface.ShadowMapScale > 0 ? surface.ShadowMapScale : 32;
            var uv = Enumerable.Range(0, node.NumVertices).Select(v =>
            {
                var position = model.Points[model.Verts[node.iVertPool + v].pVertex];
                return new Vector2(Vector3.Dot(position, xAxis), Vector3.Dot(position, yAxis)) / density;
            }).ToArray();
            var min = uv.Aggregate(Vector2.Min); var max = uv.Aggregate(Vector2.Max);
            if (!float.IsFinite(min.X + min.Y + max.X + max.Y)) return false;
            var span = Vector2.Max(max - min, Vector2.One);
            int width = (int)MathF.Ceiling(span.X) + 2, height = (int)MathF.Ceiling(span.Y) + 2;
            if (!layout.AddElement(width, height, out int x, out int y)) return false;
            for (int v = 0; v < uv.Length; v++) uv[v] = uv[v] - min + new Vector2(x + 1, y + 1);
            charts.Add((first, index, uv));
            first += node.NumVertices;
        }
        if (first == 0 || first != mapping.Vertices.Count) return false;
        mapping.SizeX = layout.SizeX + 2; mapping.SizeY = layout.SizeY + 2;
        foreach (var chart in charts)
        {
            var node = model.Nodes[chart.Node];
            for (int v = 0; v < chart.UV.Length; v++)
            {
                var uv = chart.UV[v] / new Vector2(layout.SizeX, layout.SizeY);
                mapping.Vertices[chart.First + v] = mapping.Vertices[chart.First + v] with { LightmapUV = uv };
                mapping.NewCoordinates.Add((node.iVertPool + v, node.iVertexIndex + v, uv));
            }
        }
        return true;
    }

    /// <summary>
    /// A BSP mapping of the given nodes, built as UE3 builds a node group's (ULevel::BuildStaticLighting setup in UnLevel.cpp): world-space
    /// vertices with the surface's texture axes and normal as their tangent basis, the surface's texture coordinates, the vertex's
    /// ShadowTexCoord as lightmap UV, a triangle fan per node, and an element per surface material and Lightmass settings pair.
    /// </summary>
    LightmassBspMapping BspMapping(IMEPackage package, Model model, List<int> nodes, int levelId)
    {
        var mapping = new LightmassBspMapping { Guid = Guid.NewGuid(), LevelId = levelId };
        var elements = new Dictionary<(int LightmassIndex, int Material), int>();
        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        foreach (int n in nodes)
        {
            var node = model.Nodes[n];
            if (node.NumVertices < 3 || node.iSurf < 0 || node.iSurf >= model.Surfs.Length) continue;
            var surf = model.Surfs[node.iSurf];
            if ((surf.PolyFlags & PF_Portal) != 0) continue;
            if (!elements.TryGetValue((surf.iLightmassIndex, surf.Material), out int element))
            {
                var settings = LightmassElementSettings.From(model.LightmassSettings?.ElementAtOrDefault(surf.iLightmassIndex), scene.World.EmissiveBoost);
                var material = GetMaterial(surf.Material != 0 ? package.GetEntry(surf.Material) : null, settings.DiffuseBoost);
                elements[(surf.iLightmassIndex, surf.Material)] = element = mapping.Elements.Count;
                mapping.Elements.Add(new LightmassElement(material, settings));
            }
            var textureBase = model.Points[surf.pBase];
            var textureU = model.Vectors[surf.vTextureU];
            var textureV = model.Vectors[surf.vTextureV];
            var tangentX = SafeNormalize(textureU);
            var tangentY = SafeNormalize(textureV);
            var tangentZ = SafeNormalize(model.Vectors[surf.vNormal]);
            int baseVertex = mapping.Vertices.Count;
            for (int v = 0; v < node.NumVertices; v++)
            {
                var vert = model.Verts[node.iVertPool + v];
                var position = model.Points[vert.pVertex];
                var uv = new Vector2(Vector3.Dot(position - textureBase, textureU) / 128f, Vector3.Dot(position - textureBase, textureV) / 128f);
                mapping.Vertices.Add(new LightmassBspVertex(position, tangentX, tangentY, tangentZ, uv, vert.ShadowTexCoord));
                min = Vector3.Min(min, position); max = Vector3.Max(max, position);
            }
            for (int v = 2; v < node.NumVertices; v++)
            {
                mapping.Indices.Add(baseVertex);
                mapping.Indices.Add(baseVertex + v);
                mapping.Indices.Add(baseVertex + v - 1);
                mapping.TriangleElements.Add(element);
            }
        }
        (mapping.BoundsMin, mapping.BoundsMax) = (min, max);
        return mapping;
    }

    /// <summary>
    /// The lighting channels of the nodes' surfaces (UE3 gives each ModelComponent its surfaces' channels). FBspSurf stores the
    /// FLightingChannelContainer bitfield, bInitialized first; an uninitialized one is BSP only.
    /// </summary>
    static LightingChannels BspChannels(Model model, List<int> nodes)
    {
        uint channels = 0;
        foreach (int n in nodes)
        {
            int raw = model.Surfs[model.Nodes[n].iSurf].LightingChannels;
            channels |= (raw & 1) != 0 ? (uint)raw >> 1 : 1u;
        }
        return new LightingChannels(channels);
    }

    static Vector3 SafeNormalize(Vector3 v) => v.LengthSquared() > 1e-12f ? Vector3.Normalize(v) : Vector3.Zero;

    // ================= terrain =================

    /// <summary>TERRAIN_ZSCALE: local Z per height unit (heights are centred on 32768).</summary>
    public const float TerrainZScale = 1f / 128;

    /// <summary>
    /// The level's terrains, as UE3 lights them (UTerrainComponent::GetStaticLightingInfo, FLightmassExporter::WriteTerrainMapping): a
    /// mapping per component, baked when it has an LE3 lightmap of the size UE3 gives the section, else casting shadows only. Terrain
    /// materials blend layers per vertex, so they get the constant albedo rather than a rendered material.
    /// </summary>
    void AddTerrains(IMEPackage package, Level level, int levelId)
    {
        bool contextOnly = options.ContextOnly.Contains(package);
        foreach (int u in level.Actors)
        {
            if (u == 0 || !package.TryGetUExport(u, out var actor) || actor.ClassName != "Terrain") continue;
            var props = Properties(actor);
            int Int(string name, int fallback = 0) => props.GetProp<IntProperty>(name)?.Value ?? fallback;
            var terrain = new LightmassTerrain
            {
                Export = actor,
                Properties = props,
                Binary = ObjectBinary.From<Terrain>(actor),
                LocalToWorld = ActorUtils.GetLocalToWorld(actor),
                NumVerticesX = Int("NumVerticesX"), NumVerticesY = Int("NumVerticesY"),
                NumPatchesX = Int("NumPatchesX"), NumPatchesY = Int("NumPatchesY"),
                StaticLightingResolution = Math.Max(1, Int("StaticLightingResolution", 4)),
            };
            if (!HasMatchingHeightField(props, terrain.Binary))
            {
                scene.Count("terrain: height field doesn't match its size (left out)");
                continue;
            }
            using (var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256))
            {
                hash.AppendData(System.Text.Encoding.UTF8.GetBytes($"terrain|{actor.InstancedFullPath}|{terrain.NumPatchesX}|{terrain.NumPatchesY}|{terrain.StaticLightingResolution}"));
                var localToWorld = terrain.LocalToWorld;
                hash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(ref localToWorld)));
                hash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(terrain.Binary.Heights.AsSpan()));
                hash.AppendData(terrain.Binary.InfoData.Select(f => (byte)f).ToArray());
                terrain.Guid = LightmassFormat.HashGuid(Convert.ToHexString(hash.GetHashAndReset()));
            }

            var settings = LightmassElementSettings.From(props.GetProp<StructProperty>("LightmassSettings"), scene.World.EmissiveBoost);
            var element = new LightmassElement(GetMaterial(null, settings.DiffuseBoost), settings);
            int res = terrain.StaticLightingResolution;
            // TERRAIN_PATCH_EXPAND_SCALAR (1) times DXT1's 4-texel block, in quads at the lighting resolution
            int expand = Math.Max(1, 4 / res);
            bool bilinear = props.GetProp<BoolProperty>("bBilinearFilterLightmapGeneration") is not { Value: false };
            bool added = false;
            foreach (var componentProp in props.GetProp<ArrayProperty<ObjectProperty>>("TerrainComponents") ?? Enumerable.Empty<ObjectProperty>())
            {
                if (!package.TryGetUExport(componentProp.Value, out var componentExport)) continue;
                var cprops = Properties(componentExport);
                int C(string name) => cprops.GetProp<IntProperty>(name)?.Value ?? 0;
                var component = ObjectBinary.From<TerrainComponent>(componentExport);
                if (TerrainSectionExclusion(cprops) is { } excluded)
                {
                    if (!contextOnly && component.LightMap is { LightMapType: not ELightMapType.LMT_None })
                        scene.Count($"mapping: terrain lightmap kept, {excluded}");
                    continue;
                }
                var shadowSettings = ShadowParticipation.Read(cprops);
                var mapping = new LightmassTerrainMapping
                {
                    Guid = Guid.NewGuid(),
                    Terrain = terrain,
                    Component = componentExport,
                    LevelId = levelId,
                    CastShadow = shadowSettings.BakesShadow,
                    SelfShadowOnly = shadowSettings.SelfShadowOnly,
                    BilinearFilter = bilinear,
                    SectionBaseX = C("SectionBaseX"), SectionBaseY = C("SectionBaseY"),
                    SectionSizeX = C("SectionSizeX"), SectionSizeY = C("SectionSizeY"),
                    TrueSectionSizeX = C("TrueSectionSizeX"), TrueSectionSizeY = C("TrueSectionSizeY"),
                    ExpandQuadsX = expand, ExpandQuadsY = expand,
                    Element = element,
                };
                mapping.SizeX = (2 * expand + mapping.TrueSectionSizeX) * res + 1;
                mapping.SizeY = (2 * expand + mapping.TrueSectionSizeY) * res + 1;
                // The component's frame: the terrain's, moved to the section's base (UTerrainComponent::SetParentToWorld)
                mapping.LocalToWorld = Matrix4x4.CreateTranslation(mapping.SectionBaseX, mapping.SectionBaseY, 0) * terrain.LocalToWorld;
                // Lightmass reads SectionSizeX · SectionSizeY patch bounds.
                mapping.PatchBounds = new TerrainPatchBounds[mapping.SectionSizeX * mapping.SectionSizeY];
                for (int i = 0; i < mapping.PatchBounds.Length; i++)
                    mapping.PatchBounds[i] = i < component.PatchBounds.Length ? component.PatchBounds[i] : new TerrainPatchBounds();

                if (!contextOnly && component.LightMap is LightMap_2D { LightMapType: ELightMapType.LMT_2D, Texture1: not 0 } lightmap)
                {
                    if (TextureSize(package, lightmap.Texture1) is var (w, h)
                        && (int)MathF.Round(lightmap.CoordinateScale.X * w) == mapping.SizeX && (int)MathF.Round(lightmap.CoordinateScale.Y * h) == mapping.SizeY)
                        mapping.Target = new LightmapRef(componentExport, "Terrain", lightmap, component);
                    else scene.Count("mapping: terrain lightmap of another size than its section's (kept)");
                }
                else if (!contextOnly && component.LightMap is { LightMapType: not ELightMapType.LMT_None }) scene.Count($"mapping: terrain lightmap kept, {component.LightMap.LightMapType} isn't rebaked");
                else if (!contextOnly && component.LightMap.LightMapType == ELightMapType.LMT_None
                    && mapping.SizeX > 0 && mapping.SizeY > 0 && mapping.SizeX <= 4096 && mapping.SizeY <= 4096)
                {
                    mapping.Target = new LightmapRef(componentExport, "Terrain", component.LightMap, component);
                    mapping.AllocateLightmap = true;
                    scene.Count("mapping: new terrain LMT_2D");
                }
                if (mapping.Target == null && !mapping.CastShadow) continue;

                var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
                for (int y = -expand; y < mapping.TrueSectionSizeY + expand; y++)
                for (int x = -expand; x < mapping.TrueSectionSizeX + expand; x++)
                {
                    if (!terrain.IsQuadVisible(mapping.SectionBaseX + x, mapping.SectionBaseY + y)) continue;
                    mapping.Quads.Add((x, y));
                    for (int corner = 0; corner < 4; corner++)
                    {
                        var p = TerrainVertex(mapping, x + (corner & 1), y + (corner >> 1));
                        min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                    }
                }
                if (mapping.Quads.Count == 0) continue;
                (mapping.BoundsMin, mapping.BoundsMax) = (min, max);
                if (mapping.Target != null)
                {
                    var channels = LightingChannels.FromProperty(cprops.GetProp<StructProperty>("LightingChannels"), default, isInitialized: false, LightingChannels.StaticPrimitiveDefault);
                    mapping.RelevantLights = RelevantLights(BoundsOf(min, max), mapping.Target.LightMap, PackageShadowMaps.LightGuids(mapping.Target),
                        new LightPrimitiveSettings(channels, package.FileNameNoExtension,
                            AcceptsDynamicLights: cprops.GetProp<BoolProperty>("bAcceptsDynamicLights") is not { Value: false }));
                    scene.Count("mapping: terrain LMT_2D");
                }
                else scene.Count("terrain: shadow-casting sections without a lightmap to rebake");
                scene.TerrainMappings.Add(mapping);
                added = true;
            }
            if (added) scene.Terrains.Add(terrain);
        }
    }

    /// <summary>Whether a terrain's height field has a height and info flags for each of its vertices; Lightmass can't use one that doesn't</summary>
    public static bool HasMatchingHeightField(PropertyCollection terrainProps, Terrain binary)
    {
        int heights = (terrainProps.GetProp<IntProperty>("NumVerticesX")?.Value ?? 0) * (terrainProps.GetProp<IntProperty>("NumVerticesY")?.Value ?? 0);
        return heights > 0 && binary.Heights.Length == heights && binary.InfoData.Length == heights;
    }

    /// <summary>
    /// Why a terrain section isn't in the bake, or null if it is: as for meshes, UE3 only builds static lighting for sections with static shadowing
    /// that accept lights (UTerrainComponent::GetStaticLightingInfo)
    /// </summary>
    public static string TerrainSectionExclusion(PropertyCollection sectionProps) =>
        sectionProps.GetProp<BoolProperty>("bUsePrecomputedShadows") is { Value: false } ? "without static shadowing"
        : sectionProps.GetProp<BoolProperty>("bAcceptsLights") is { Value: false } ? "doesn't accept lights"
        : null;

    /// <summary>
    /// How many of the level's terrain sections the bake exports as shadow casters: those of its Terrain actors with a usable height field that take part
    /// in static lighting and cast shadows. (A section with no visible quads is counted, though the bake then leaves it out.)
    /// </summary>
    public static int ShadowCastingTerrainSections(IMEPackage package, Level level, PackageCache cache) =>
        ShadowCastingTerrainSections(TerrainActors(package, level), cache);

    /// <summary>The level's Terrain actors, which the bake gets terrain sections from</summary>
    public static ExportEntry[] TerrainActors(IMEPackage package, Level level) =>
        level.Actors.Where(u => u != 0).Select(u => package.TryGetUExport(u, out var actor) ? actor : null).Where(actor => actor?.ClassName == "Terrain").ToArray();

    /// <inheritdoc cref="ShadowCastingTerrainSections(IMEPackage, Level, PackageCache)"/>
    public static int ShadowCastingTerrainSections(IEnumerable<ExportEntry> terrainActors, PackageCache cache)
    {
        int sections = 0;
        foreach (var actor in terrainActors)
        {
            var package = actor.FileRef;
            var props = EffectiveLightingProperties.Read(actor, cache);
            if (!HasMatchingHeightField(props, ObjectBinary.From<Terrain>(actor))) continue;
            foreach (var componentProp in props.GetProp<ArrayProperty<ObjectProperty>>("TerrainComponents") ?? Enumerable.Empty<ObjectProperty>())
            {
                if (package.TryGetUExport(componentProp.Value, out var section) && EffectiveLightingProperties.Read(section, cache) is var sectionProps
                    && TerrainSectionExclusion(sectionProps) is null && ShadowParticipation.Read(sectionProps).BakesShadow)
                    sections++;
            }
        }
        return sections;
    }

    /// <summary>A terrain vertex's world position, as Lightmass places it (FTerrainComponentStaticLighting::GetVertex, without displacement).</summary>
    public static Vector3 TerrainVertex(LightmassTerrainMapping mapping, int x, int y)
    {
        var terrain = mapping.Terrain;
        int patchX = mapping.SectionBaseX + x, patchY = mapping.SectionBaseY + y;
        float localX = Math.Clamp(patchX, 0, terrain.NumVerticesX - 1) - mapping.SectionBaseX;
        float localY = Math.Clamp(patchY, 0, terrain.NumVerticesY - 1) - mapping.SectionBaseY;
        return Vector3.Transform(new Vector3(localX, localY, (terrain.Height(patchX, patchY) - 32768f) * TerrainZScale), mapping.LocalToWorld);
    }

    static (int, int)? TextureSize(IMEPackage package, int texture)
    {
        if (!package.TryGetUExport(texture, out var export)) return null;
        var p = export.GetProperties();
        return (p.GetProp<IntProperty>("SizeX")?.Value ?? 0, p.GetProp<IntProperty>("SizeY")?.Value ?? 0);
    }

    // ================= materials =================

    /// <summary>A material's settings, from its instance chain (UMaterialInstance's Get* overrides) and base Material.</summary>
    sealed record MaterialInfo(int BlendMode, bool TwoSided, bool Unlit, float OpacityMaskClipValue, float DiffuseBoost, float EmissiveBoost,
        float ExportResolutionScale, bool CastShadowAsMasked)
    {
        public static readonly MaterialInfo Default = new(0, false, false, 1f / 3, 1, 1, 1, false);
    }

    /// <summary>
    /// UDK-2015-02's EBlendMode numbering, which Lightmass's material channel uses (it gained ModulateAndAdd and DitheredTranslucent
    /// and moved SoftMasked and AlphaComposite, hence the new LM_MATERIAL_VERSION). Matched by name from LE3's materials.
    /// </summary>
    static readonly Dictionary<string, int> UdkBlendModes = new()
    {
        ["BLEND_Opaque"] = 0, ["BLEND_Masked"] = 1, ["BLEND_Translucent"] = 2, ["BLEND_Additive"] = 3, ["BLEND_Modulate"] = 4,
        ["BLEND_ModulateAndAdd"] = 5, ["BLEND_SoftMasked"] = 6, ["BLEND_AlphaComposite"] = 7, ["BLEND_DitheredTranslucent"] = 8,
    };

    /// <summary>UDK blend modes whose materials Lightmass treats as translucent (they need a transmission sample).</summary>
    public static bool IsTranslucent(int udkBlendMode) => udkBlendMode is 2 or 3 or 4 or 5 or 7 or 8;

    /// <summary>
    /// The Lightmass material for an element: the material's properties, as UE3's FLightmassMaterialProxy compiles them per blend mode
    /// (LightmassRender.cpp), with the diffuse boosted by the material, primitive and level DiffuseBoost.
    /// </summary>
    LightmassMaterial GetMaterial(IEntry entry, float primitiveDiffuseBoost)
    {
        var info = entry == null ? MaterialInfo.Default : ReadMaterial(entry);
        float boost = info.DiffuseBoost * primitiveDiffuseBoost * scene.World.DiffuseBoost;
        var material = new LightmassMaterial
        {
            BlendMode = info.BlendMode, TwoSided = info.TwoSided, CastShadowAsMasked = info.CastShadowAsMasked,
            OpacityMaskClipValue = info.OpacityMaskClipValue, EmissiveBoost = info.EmissiveBoost,
        };
        int Size(int sampleSize) => Math.Clamp((int)(sampleSize * info.ExportResolutionScale), 1, 1024);
        int emissiveSize = Size(options.EmissiveSampleSize), diffuseSize = Size(options.DiffuseSampleSize), transmissionSize = Size(options.TransmissionSampleSize);
        bool opaque = info.BlendMode is 0 or 1 or 6; // Opaque, Masked, SoftMasked
        var captured = entry == null ? null : Capture(entry, opaque ? [emissiveSize, diffuseSize, transmissionSize] : [emissiveSize, transmissionSize]);

        if (captured == null)
        {
            // Without the material's opacity, a masked material shadows as opaque, and translucency lets all light through.
            if (info.BlendMode is 1 or 6) material.BlendMode = 0;
            material.Emissive = MaterialSamples.Constant(Vector3.Zero, 0);
            if (material.BlendMode is 0) material.Diffuse = MaterialSamples.Constant(Vector3.Min(new Vector3(options.DefaultAlbedo * boost), Vector3.One), 0);
            else material.Transmission = MaterialSamples.Constant(Vector3.One, 0);
        }
        else
        {
            var e = captured[emissiveSize];
            material.Emissive = MaterialSamples.FromTexels(e.Size, i => new Vector4(Vector3.Max(e.Emissive(i), Vector3.Zero), 0));
            if (opaque)
            {
                var d = captured[diffuseSize];
                material.Diffuse = MaterialSamples.FromTexels(d.Size, i => new Vector4(Vector3.Clamp(d.Diffuse(i) * boost, Vector3.Zero, Vector3.One), 0));
            }
            var t = captured[transmissionSize];
            Vector3 Colored(int i) => Vector3.Clamp(info.Unlit ? t.Emissive(i) : t.Diffuse(i), Vector3.Zero, Vector3.One);
            float Opacity(int i) => t.Covered[i] ? Math.Clamp(t.Opacity(i), 0, 1) : 0;
            switch (info.BlendMode)
            {
                case 1:
                    // The opacity mask itself isn't in the shader's output, only whether it clipped: transmission 1 where it didn't
                    material.Transmission = MaterialSamples.FromTexels(t.Size, i => t.Covered[i] ? Vector4.One : Vector4.Zero);
                    material.OpacityMaskClipValue = 0.5f;
                    break;
                case 6:
                    material.Transmission = MaterialSamples.FromTexels(t.Size, i => new Vector4(new Vector3(Opacity(i)), 0));
                    break;
                case 2 or 7 when info.CastShadowAsMasked:
                    material.Transmission = MaterialSamples.FromTexels(t.Size, i => new Vector4(new Vector3(Opacity(i)), 0));
                    break;
                case 2 or 7:
                    material.Transmission = MaterialSamples.FromTexels(t.Size, i => new Vector4(Vector3.Lerp(Vector3.One, Colored(i), Opacity(i)), 0));
                    break;
                case 4:
                    material.Transmission = MaterialSamples.FromTexels(t.Size, i => new Vector4(Colored(i), 0));
                    break;
                case not 0:
                    // Additive: the shader outputs color times opacity, so the opacity can't be separated. It lets all light through.
                    material.Transmission = MaterialSamples.Constant(Vector3.One, 0);
                    break;
            }
        }
        if (opaque) material.Specular = MaterialSamples.Constant(Vector3.Zero, 1);

        material.Guid = MaterialGuid(material);
        if (materials.TryGetValue(material.Guid, out var existing)) return existing;
        return materials[material.Guid] = material;
    }

    /// <summary>Named by content, so Swarm's cache reuses the channel until the material (or its boosts) change.</summary>
    static Guid MaterialGuid(LightmassMaterial m)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        hash.AppendData(System.Text.Encoding.UTF8.GetBytes(string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"mtrl|{m.BlendMode}|{m.TwoSided}|{m.CastShadowAsMasked}|{m.OpacityMaskClipValue:R}|{m.EmissiveBoost:R}")));
        foreach (var samples in new[] { m.Emissive, m.Diffuse, m.Specular, m.Transmission })
        {
            hash.AppendData(BitConverter.GetBytes(samples?.Size ?? 0));
            if (samples != null) hash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples.Rgba.AsSpan()));
        }
        return LightmassFormat.HashGuid(Convert.ToHexString(hash.GetHashAndReset()));
    }

    readonly Dictionary<string, Dictionary<int, CapturedMaterial>> captures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A material rendered at the given sizes (once per material), or null if it can't be rendered with the game's shaders.</summary>
    Dictionary<int, CapturedMaterial> Capture(IEntry entry, int[] sizes)
    {
        if (options.MaterialCapture == null) return null;
        string key = entry.FileRef.FilePath + "|" + entry.InstancedFullPath;
        if (!captures.TryGetValue(key, out var captured))
        {
            captured = new Dictionary<int, CapturedMaterial>();
            captures[key] = captured;
        }
        else if (captured.Count == 0) return null; // failed before
        var missing = sizes.Where(s => !captured.ContainsKey(s)).Distinct().ToArray();
        if (missing.Length == 0) return captured;
        bool first = captured.Count == 0;
        try
        {
            foreach (var (size, c) in options.MaterialCapture.Capture(entry, missing)) captured[size] = c;
            if (missing.FirstOrDefault(s => !captured.ContainsKey(s)) is > 0 and var skipped)
                throw new InvalidOperationException($"the capture has no {skipped}x{skipped} rendering");
            if (first) scene.Count("material: rendered with the game's shaders");
            return captured;
        }
        catch (Exception e)
        {
            captured.Clear();
            scene.Count($"material: not rendered, constant albedo instead ({e.Message})");
            return null;
        }
    }

    MaterialInfo ReadMaterial(IEntry entry)
    {
        string key = entry.FileRef.FilePath + "|" + entry.InstancedFullPath;
        if (materialInfos.TryGetValue(key, out var info)) return info;
        info = MaterialInfo.Default;
        float? diffuseBoost = null, emissiveBoost = null, exportResolutionScale = null;
        bool? castShadowAsMasked = null;
        var current = Resolve(entry);
        for (int depth = 0; current != null && depth < 16; depth++)
        {
            var props = current.GetProperties();
            // UMaterialInstance::Get*: an instance's own LightmassSettings value only with its bOverride flag, otherwise its parent's.
            if (props.GetProp<StructProperty>("LightmassSettings") is { } lightmass)
            {
                bool isMaterial = current.ClassName == "Material";
                bool Has(string name) => isMaterial || lightmass.GetProp<BoolProperty>("bOverride" + name) is { Value: true };
                if (diffuseBoost == null && Has("DiffuseBoost")) diffuseBoost = lightmass.GetProp<FloatProperty>("DiffuseBoost")?.Value;
                if (emissiveBoost == null && Has("EmissiveBoost")) emissiveBoost = lightmass.GetProp<FloatProperty>("EmissiveBoost")?.Value;
                if (exportResolutionScale == null && Has("ExportResolutionScale")) exportResolutionScale = lightmass.GetProp<FloatProperty>("ExportResolutionScale")?.Value;
                if (castShadowAsMasked == null && Has("CastShadowAsMasked")) castShadowAsMasked = lightmass.GetProp<BoolProperty>("bCastShadowAsMasked")?.Value;
            }
            if (current.ClassName == "Material")
            {
                int blend = props.GetProp<EnumProperty>("BlendMode") is { } b ? UdkBlendModes.GetValueOrDefault(b.Value.Instanced) : 0;
                info = new MaterialInfo(blend, props.GetProp<BoolProperty>("TwoSided") is { Value: true },
                    props.GetProp<EnumProperty>("LightingModel")?.Value.Instanced == "MLM_Unlit",
                    props.GetProp<FloatProperty>("OpacityMaskClipValue")?.Value ?? 1f / 3, diffuseBoost ?? 1, emissiveBoost ?? 1,
                    exportResolutionScale ?? 1, castShadowAsMasked ?? false);
                break;
            }
            current = props.GetProp<ObjectProperty>("Parent") is { Value: not 0 } parent ? Resolve(current.FileRef.GetEntry(parent.Value)) : null;
        }
        return materialInfos[key] = info;
    }

    ExportEntry Resolve(IEntry entry)
    {
        try
        {
            return entry switch
            {
                ExportEntry e => e,
                ImportEntry i => EntryImporter.ResolveImport(i, packageCache),
                _ => null,
            };
        }
        catch (Exception e)
        {
            scene.Count($"import not resolved: {e.GetType().Name}");
            return null;
        }
    }

    // ================= importance volumes =================

    void AddImportanceVolumes(IMEPackage package, Level level)
    {
        foreach (int u in level.Actors)
        {
            if (u == 0 || !package.TryGetUExport(u, out var actor) || actor.ClassName != "LightmassImportanceVolume") continue;
            // Cooked brushes keep no BSP points or polygons; their collision's convex elements keep a local bounding box each.
            var points = new List<Vector3>();
            if (actor.GetProperty<ObjectProperty>("BrushComponent") is { Value: not 0 } brushComponent && package.TryGetUExport(brushComponent.Value, out var componentExport)
                && componentExport.GetProperty<StructProperty>("BrushAggGeom")?.GetProp<ArrayProperty<StructProperty>>("ConvexElems") is { } elems)
            {
                foreach (var elem in elems)
                {
                    if (elem.GetProp<StructProperty>("ElemBox") is not { } box) continue;
                    var lo = CommonStructs.GetVector3(box.GetProp<StructProperty>("Min"));
                    var hi = CommonStructs.GetVector3(box.GetProp<StructProperty>("Max"));
                    for (int corner = 0; corner < 8; corner++)
                        points.Add(new Vector3((corner & 1) != 0 ? hi.X : lo.X, (corner & 2) != 0 ? hi.Y : lo.Y, (corner & 4) != 0 ? hi.Z : lo.Z));
                }
            }
            if (points.Count == 0 && actor.GetProperty<ObjectProperty>("Brush") is { Value: not 0 } brush && package.TryGetUExport(brush.Value, out var modelExport))
                points.AddRange(ObjectBinary.From<Model>(modelExport).Points);
            if (points.Count == 0) { scene.Count("importance volume without geometry"); continue; }
            var localToWorld = ActorUtils.GetLocalToWorld(actor);
            var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
            foreach (var point in points)
            {
                var world = Vector3.Transform(point, localToWorld);
                min = Vector3.Min(min, world); max = Vector3.Max(max, world);
            }
            scene.ImportanceVolumes.Add((min, max));
        }
    }

    public void Dispose()
    {
        foreach (var r in meshResolvers.Values) r.Dispose();
        packageCache.Dispose();
    }
}

/// <summary>EDawnLightFlags (SceneExport.h).</summary>
public static class LightFlags
{
    public const uint CastShadows = 0x1, HasStaticLighting = 0x2, UseDirectLightMap = 0x4, HasStaticShadowing = 0x8, CastStaticShadows = 0x10,
        UseSignedDistanceFieldShadows = 0x20, Dominant = 0x40;
}
