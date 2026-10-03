using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
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
        //In the order the game stores them (B, G, R, A), read as B8G8R8A8 like the game's D3DCOLOR streams
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
        new InputElement("TEXCOORD", 5, Format.B8G8R8A8_UNorm, 8, VertexStreamSlot),
        new InputElement("TEXCOORD", 6, Format.B8G8R8A8_UNorm, 12, VertexStreamSlot),
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

    /// <summary>
    /// The component is lit by a light environment (as dynamic actors are), which the level's lights don't affect directly
    /// </summary>
    public bool UsesLightEnvironment { get; private set; }

    private readonly MeshRenderContext Context;

    /// <summary>
    /// The materials of the mesh this lighting is for. Set by the mesh's <see cref="ModelPreview{TVertex}"/>, so that their light shaders can be loaded ahead of time
    /// </summary>
    internal IEnumerable<MaterialRenderProxy> Materials = [];
    internal Func<BoxSphereBounds> GetBounds;

    private readonly object InteractionsLock = new();
    private LightInteraction[] Interactions = [];
    private int InteractionsLightsVersion = -1;
    private BoxSphereBounds InteractionsBounds;

    private MeshStaticLighting(MeshRenderContext context)
    {
        Context = context;
    }

    /// <summary>
    /// Reads a static mesh component's precomputed lighting for one LOD. Returns null if that isn't possible
    /// </summary>
    public static MeshStaticLighting Create(MeshRenderContext context, ExportEntry componentExport, StaticMesh mesh, int lod)
    {
        if (!componentExport.Game.IsLEGame() || lod >= mesh.LODModels.Length)
        {
            return null;
        }
        StaticMeshComponent component;
        try
        {
            component = componentExport.GetBinaryData<StaticMeshComponent>();
        }
        catch
        {
            return null;
        }
        var staticLighting = new MeshStaticLighting(context);
        PropertyCollection props = componentExport.GetProperties();
        if (props.GetProp<ArrayProperty<StructProperty>>("IrrelevantLights") is { } irrelevantLights)
        {
            foreach (StructProperty guidProp in irrelevantLights)
            {
                staticLighting.IrrelevantLights.Add(CommonStructs.GetGuid(guidProp));
            }
        }
        staticLighting.UsesLightEnvironment = props.GetProp<ObjectProperty>("LightEnvironment") is { Value: not 0 };
        //Static meshes in levels have static shadowing, so UE3 initializes unset channels to Static
        var condensedProps = componentExport.GetCondensedProperties(context.PackageCache, resolveImports: true, mergeStructs: true);
        staticLighting.LightingChannels = LightingChannels.FromProperty(condensedProps.GetProp<StructProperty>("LightingChannels"), default, isInitialized: false,
            LightingChannels.StaticPrimitiveDefault);
        if (lod < component.LODData.Length)
        {
            staticLighting.Load(component.LODData[lod], componentExport.FileRef, mesh, lod);
        }
        context.RegisterStaticLighting(staticLighting);
        return staticLighting;
    }

    private void Load(StaticMeshComponentLODInfo lodInfo, IMEPackage pcc, StaticMesh mesh, int lod)
    {
        StaticMeshRenderData lodModel = mesh.LODModels[lod];
        int numVertices = (int)lodModel.NumVertices;

        uint[] samplesA = null;
        uint[] samplesB = null;
        Guid[] lightGuids = [];
        switch (lodInfo.LightMap)
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
                Scales = [ToVector4(lm3.unkVector1), ToVector4(lm3.unkVector2)];
                break;
            case LightMap_5 lm5:
                lightGuids = lm5.LightGuids;
                samplesA = Array.ConvertAll(lm5.SimpleSamples, s => PackColor(s.Coefficient));
                Scales = [ToVector4(lm5.unkVector)];
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
            LightMapType = lodInfo.LightMap.LightMapType;
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

        foreach (int shadowMapUIndex in lodInfo.ShadowMaps)
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
        foreach (int shadowVertexBufferUIndex in lodInfo.ShadowVertexBuffers)
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

        //Like the game's vertex factory, light-map UVs are only available if the mesh has that UV channel
        int lightMapCoordinateIndex = mesh.Export.GetProperty<IntProperty>("LightMapCoordinateIndex")?.Value ?? 0;
        StaticMeshVertexBuffer vertexBuffer = lodModel.VertexBuffer;
        bool hasLightMapCoordinates = lightMapCoordinateIndex >= 0 && lightMapCoordinateIndex < vertexBuffer.NumTexCoords;
        if (!hasLightMapCoordinates)
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
                    StaticMeshVertexBuffer.StaticMeshFullVertex vertex = vertexBuffer.VertexData[i];
                    vertices[i].LightMapCoordinate = vertexBuffer.bUseFullPrecisionUVs
                        ? vertex.FullPrecisionUVs[lightMapCoordinateIndex]
                        : vertex.HalfPrecisionUVs[lightMapCoordinateIndex];
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
    /// The lights rendered on this mesh in their own passes: those that reach it, and aren't baked into its light-map. Cached until the scene's lights or the bounds change.
    /// </summary>
    public LightInteraction[] GetLightInteractions(BoxSphereBounds bounds)
    {
        lock (InteractionsLock)
        {
            SceneLight[] lights = Context.GetLights(out int lightsVersion);
            if (lightsVersion != InteractionsLightsVersion || bounds.Origin != InteractionsBounds.Origin || bounds.SphereRadius != InteractionsBounds.SphereRadius)
            {
                var interactions = new List<LightInteraction>();
                foreach (SceneLight light in lights)
                {
                    if (light.LightingChannels.OverlapsWith(LightingChannels) && light.AffectsBounds(bounds) && GetInteraction(light) is { } interaction)
                    {
                        interactions.Add(interaction);
                    }
                }
                Interactions = interactions.ToArray();
                InteractionsLightsVersion = lightsVersion;
                InteractionsBounds = bounds;
            }
            return Interactions;
        }
    }

    /// <summary>
    /// FStaticMeshSceneProxy::FLODInfo::GetInteraction. Null if the light doesn't need its own pass
    /// </summary>
    private LightInteraction? GetInteraction(SceneLight light)
    {
        if (light.HasStaticShadowing)
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

    /// <summary>
    /// Computes the light interactions and loads the light shaders they need, so that it doesn't happen during rendering. Thread-safe
    /// </summary>
    internal void PrepareLightShaders()
    {
        if (UsesLightEnvironment || GetBounds is null)
        {
            return;
        }
        foreach (LightInteraction interaction in GetLightInteractions(GetBounds()))
        {
            foreach (MaterialRenderProxy material in Materials)
            {
                material.GetLightShaders(interaction.Light.Type, interaction.Shadowing);
            }
        }
    }

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
        //Light-map and shadow-map textures are usually top-level objects with generated names, which aren't unique between levels.
        //So they're cached by file and export, rather than by path like other textures
        return Context.TextureCache.LoadTexture(textureExport, Context.PackageCache, $"{pcc.FilePath}|{uIndex}");
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
