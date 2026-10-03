using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using LegendaryExplorerCore.Unreal.Classes;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

// MODEL RENDERING OVERVIEW:
// Construct a ModelPreview instance with an existing SkeletalMesh or StaticMesh.
// Call ModelPreview.Render(...) every frame. Boom.
namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// Stores the material information of triangles in a <see cref="ModelPreviewLOD"/> mesh.
/// </summary>
public struct ModelPreviewSection
{
    /// <summary>
    /// The name of the material to be applied to the triangles in this section.
    /// </summary>
    public string MaterialName;

    /// <summary>
    /// The first index into the LOD mesh index buffer that this section describes.
    /// </summary>
    public uint StartIndex;

    /// <summary>
    /// How many triangles, starting from the vertex at <see cref="StartIndex"/>, that this section describes.
    /// </summary>
    public uint TriangleCount;

    /// <summary>
    /// Constructs a new MaterialPreviewSection.
    /// </summary>
    /// <param name="materialname">The name of the material to be applied to the triangles in this section.</param>
    /// <param name="startindex">The first index into the LOD mesh index buffer that this section describes.</param>
    /// <param name="trianglecount">How many triangles, starting from the vertex at <see cref="StartIndex"/>, that this section describes.</param>
    public ModelPreviewSection(string materialname, uint startindex, uint trianglecount)
    {
        MaterialName = materialname;
        StartIndex = startindex;
        TriangleCount = trianglecount;
    }
}

/// <summary>
/// Stores the geometry and the associated material information for a single level-of-detail in a <see cref="ModelPreview"/>.
/// </summary>
public class ModelPreviewLOD<Vertex> where Vertex : IVertexBase
{
    /// <summary>
    /// The geometry of this level of detail.
    /// </summary>
    public Mesh<Vertex> Mesh;

    /// <summary>
    /// A list of which materials are applied to which triangles.
    /// </summary>
    public List<ModelPreviewSection> Sections;

    /// <summary>
    /// The precomputed lighting of this LOD, if it has any. Only used by the game's shaders
    /// </summary>
    public MeshStaticLighting StaticLighting;

    /// <summary>
    /// Creates a new ModelPreviewLOD.
    /// </summary>
    /// <param name="mesh">The geometry of this level of detail.</param>
    /// <param name="sections">A list of which materials are applied to which triangles.</param>
    public ModelPreviewLOD(Mesh<Vertex> mesh, List<ModelPreviewSection> sections)
    {
        Mesh = mesh;
        Sections = sections;
    }
}

public enum RenderPass
{
    //material types
    Base,
    Hair,
    //The level's lights, each added onto the opaque geometry they light in its own pass. Must be rendered after the opaque passes
    Lighting,
    //materials that blend with what's behind them. Must be rendered after the opaque passes, since they don't write depth
    Translucent,

    //special types
    Collision,

    //override, most always be last
    ANY
}

/// <summary>
/// ModelPreviewMaterial is responsible for rendering sections of meshes.
/// </summary>
public abstract class ModelPreviewMaterial<Vertex> where Vertex : IVertexBase
{

    public RenderPass Pass;

    public virtual bool CanCullByBounds(MeshRenderContext context) => true;

    protected readonly MaterialInstanceConstantLevelEditor Material;
    public string InstancedFullPath => Material.InstancedFullPath;

    /// <summary>
    /// A Dictionary of string properties. Useful because some materials have properties that others don't.
    /// </summary>
    public readonly Dictionary<string, string> Properties = [];

    /// <summary>
    /// Creates a ModelPreviewMaterial that renders as close to what the given <see cref="MaterialInstanceConstantLevelEditor"/> looks like as possible. 
    /// </summary>
    public ModelPreviewMaterial(MeshRenderContext renderContext, ExportEntry export)
    {
        Material = CreateMaterial(renderContext, export);
        Pass = RenderPass.Base;
    }

    /// <summary>
    /// Renders the given <see cref="ModelPreviewSection"/> of a <see cref="ModelPreviewLOD"/>. 
    /// </summary>
    /// <param name="lod">The LOD to render.</param>
    /// <param name="s">Which faces to render.</param>
    public abstract void RenderSection(ModelPreviewLOD<Vertex> lod, ModelPreviewSection s,  MeshRenderContext context);

    /// <summary>
    /// Renders the level's lights on the given <see cref="ModelPreviewSection"/>, for <see cref="RenderPass.Lighting"/>.
    /// </summary>
    public virtual void RenderLighting(ModelPreviewLOD<Vertex> lod, ModelPreviewSection s, MeshRenderContext context) { }

    ///Only call from constructor
    protected abstract MaterialInstanceConstantLevelEditor CreateMaterial(MeshRenderContext renderContext, ExportEntry export);
}

public class TexturedPreviewMaterial : ModelPreviewMaterial<WorldVertex>
{
    private readonly PreviewTextureCache.TextureEntry DiffTexture;

    public TexturedPreviewMaterial(MeshRenderContext renderContext, ExportEntry export) : base(renderContext, export)
    {
        Properties.Add("Name", export.ObjectName.Instanced);
        if (FindDiffuse(export, Material) is IEntry diffEntry)
        {
            DiffTexture = renderContext.TextureCache.LoadTexture(diffEntry, renderContext.PackageCache);
        }
    }

    /// <summary>
    /// Guesses which of a material's textures is the diffuse texture, based on texture names.
    /// </summary>
    internal static IEntry FindDiffuse(ExportEntry matExport, MaterialInstanceConstantLevelEditor mat)
    {
        string matPackage = matExport.Parent?.InstancedFullPath.ToLower();
        string matName = matExport.ObjectName.Instanced;
        foreach (var textureEntry in mat.Textures)
        {
            var texObjectName = textureEntry.InstancedFullPath.ToLower();
            if ((matPackage == null || texObjectName.StartsWith(matPackage)) && texObjectName.Contains("diff"))
            {
                // we have found the diffuse texture!
                Debug.WriteLine("Diffuse texture of new material <" + matName + "> is " + textureEntry.InstancedFullPath);
                return textureEntry;
            }
        }

        foreach (var textureEntry in mat.Textures.Reverse())
        {
            var texObjectName = textureEntry.ObjectName.Name.ToLower();
            if (texObjectName.Contains("diff") || texObjectName.Contains("tex"))
            {
                // we have found the diffuse texture!
                Debug.WriteLine("Diffuse texture of new material <" + matName + "> is " + textureEntry.InstancedFullPath);
                return textureEntry;
            }
        }
        foreach (var texparam in mat.Textures)
        {
            var texObjectName = texparam.ObjectName.Name.ToLower();

            if (texObjectName.Contains("detail"))
            {
                // I guess a detail texture is good enough if we didn't return for a diffuse texture earlier...
                Debug.WriteLine("Diffuse (Detail) texture of new material <" + matName + "> is " + texparam.InstancedFullPath);
                return texparam;
            }
        }
        foreach (var texparam in mat.Textures)
        {
            var texObjectName = texparam.ObjectName.Name.ToLower();
            if (!texObjectName.Contains("norm") && !texObjectName.Contains("opac"))
            {
                //Anything is better than nothing I suppose
                Debug.WriteLine("Using first found texture (last resort)  of new material <" + matName + "> as diffuse: " + texparam.InstancedFullPath);
                return texparam;
            }
        }
        return null;
    }

    /// <summary>
    /// Uses LEX's default shader to render the given <see cref="ModelPreviewSection"/> of a <see cref="ModelPreviewLOD"/>. 
    /// </summary>
    /// <param name="lod">The LOD to render.</param>
    /// <param name="s">Which faces to render.</param>
    public override void RenderSection(ModelPreviewLOD<WorldVertex> lod, ModelPreviewSection s, MeshRenderContext context)
    {
        context.DefaultEffect.PrepDraw(context.ImmediateContext, context.AlphaBlendState, context.GetWorldConstants(lod.Mesh.LocalToWorld));

        ShaderResourceView diffTextureView = DiffTexture?.TextureView ?? context.DefaultTextureView;

        context.DefaultEffect.RenderObject(
            context.ImmediateContext,
            lod.Mesh,
            (int)s.StartIndex,
            (int)s.TriangleCount * 3,
            context.Wireframe ? null : diffTextureView);
    }

    protected override MaterialInstanceConstantLevelEditor CreateMaterial(MeshRenderContext renderContext, ExportEntry export)
    {
        return new MaterialInstanceConstantLevelEditor(export, renderContext.PackageCache, true);
    }
}

file class LEShaderPreviewMaterial : ModelPreviewMaterial<LEVertex>
{
    private readonly RenderTargetBlendDescription BlendDescription;

    public readonly Dictionary<string, PreviewTextureCache.TextureEntry> TextureMap = [];

    //used when the material can't be rendered with the game's shaders
    private readonly PreviewTextureCache.TextureEntry FallbackDiffTexture;

    /// <param name="lightMapType">The type of static light-map the mesh has. Determines which of the material's shaders are used</param>
    public LEShaderPreviewMaterial(MeshRenderContext renderContext, ExportEntry export, ELightMapType lightMapType) : base(renderContext, export)
    {
        foreach (IEntry textureEntry in Material.Textures)
        {
            if (!TextureMap.ContainsKey(textureEntry.InstancedFullPath))
            {
                PreviewTextureCache.TextureEntry texture = renderContext.TextureCache.LoadTexture(textureEntry, renderContext.PackageCache);
                if (texture is not null)
                {
                    TextureMap.Add(textureEntry.InstancedFullPath, texture);
                }
            }
        }
        var mat = (MaterialRenderProxy)Material;
        mat.TextureMap = TextureMap;
        mat.QueueGameShaderLoad(lightMapType);
        if (TexturedPreviewMaterial.FindDiffuse(export, Material) is IEntry diffEntry)
        {
            TextureMap.TryGetValue(diffEntry.InstancedFullPath, out FallbackDiffTexture);
        }
        Pass = mat.UseHairPass ? RenderPass.Hair
            : IsTranslucent ? RenderPass.Translucent
            : RenderPass.Base;
        BlendDescription = mat.BlendMode switch
        {
            EBlendMode.BLEND_Opaque => new RenderTargetBlendDescription
            {
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
                BlendOperation = BlendOperation.Add,
                AlphaBlendOperation = BlendOperation.Add,
                SourceBlend = BlendOption.One,
                DestinationBlend = BlendOption.Zero,
                SourceAlphaBlend = BlendOption.One,
                DestinationAlphaBlend = BlendOption.Zero,
                IsBlendEnabled = false
            },
            EBlendMode.BLEND_Masked => new RenderTargetBlendDescription
            {
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
                BlendOperation = BlendOperation.Add,
                AlphaBlendOperation = BlendOperation.Add,
                SourceBlend = BlendOption.One,
                DestinationBlend = BlendOption.Zero,
                SourceAlphaBlend = BlendOption.One,
                DestinationAlphaBlend = BlendOption.Zero,
                IsBlendEnabled = false
            },
            EBlendMode.BLEND_Translucent => new RenderTargetBlendDescription
            {
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
                BlendOperation = BlendOperation.Add,
                AlphaBlendOperation = BlendOperation.Add,
                SourceBlend = BlendOption.SourceAlpha,
                DestinationBlend = BlendOption.InverseSourceAlpha,
                SourceAlphaBlend = BlendOption.SourceAlphaSaturate,
                DestinationAlphaBlend = BlendOption.InverseSourceAlpha,
                IsBlendEnabled = true
            },
            //TODO: the ones above this comment seem to work properly, but the rest need verifying
            EBlendMode.BLEND_Additive => new RenderTargetBlendDescription
            {
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
                BlendOperation = BlendOperation.Add,
                AlphaBlendOperation = BlendOperation.Add,
                SourceBlend = BlendOption.One,
                DestinationBlend = BlendOption.One,
                SourceAlphaBlend = BlendOption.Zero,
                DestinationAlphaBlend = BlendOption.One,
                IsBlendEnabled = true
            },
            EBlendMode.BLEND_Modulate => new RenderTargetBlendDescription
            {
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
                BlendOperation = BlendOperation.Add,
                AlphaBlendOperation = BlendOperation.Add,
                SourceBlend = BlendOption.DestinationColor,
                DestinationBlend = BlendOption.Zero,
                SourceAlphaBlend = BlendOption.Zero,
                DestinationAlphaBlend = BlendOption.One,
                IsBlendEnabled = true
            },
            EBlendMode.BLEND_SoftMasked => new RenderTargetBlendDescription
            {
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
                BlendOperation = BlendOperation.Add,
                AlphaBlendOperation = BlendOperation.Add,
                SourceBlend = BlendOption.SourceAlpha,
                DestinationBlend = BlendOption.InverseSourceAlpha,
                SourceAlphaBlend = BlendOption.Zero,
                DestinationAlphaBlend = BlendOption.InverseSourceAlpha,
                IsBlendEnabled = true
            },
            EBlendMode.BLEND_AlphaComposite => new RenderTargetBlendDescription
            {
                RenderTargetWriteMask = ColorWriteMaskFlags.All,
                BlendOperation = BlendOperation.Add,
                AlphaBlendOperation = BlendOperation.Add,
                SourceBlend = BlendOption.One,
                DestinationBlend = BlendOption.InverseSourceAlpha,
                SourceAlphaBlend = BlendOption.One,
                DestinationAlphaBlend = BlendOption.InverseSourceAlpha,
                IsBlendEnabled = false
            },
            _ => throw new ArgumentOutOfRangeException(),
        };
        //The game's base pass shaders write 0 to the alpha channel of the color target.
        //Don't let that through to the backbuffer, or the scene will be transparent when composited.
        BlendDescription.RenderTargetWriteMask = ColorWriteMaskFlags.Red | ColorWriteMaskFlags.Green | ColorWriteMaskFlags.Blue;
    }

    private bool LoggedFallback;

    public MaterialRenderProxy RenderProxy => (MaterialRenderProxy)Material;

    public override bool CanCullByBounds(MeshRenderContext context) =>
        !context.UseGameShaders || RenderProxy.HasUndisplacedVertices;

    //same as UE3's IsTranslucentBlendMode
    private bool IsTranslucent => ((MaterialRenderProxy)Material).BlendMode is not (EBlendMode.BLEND_Opaque or EBlendMode.BLEND_Masked);

    /// <summary>
    /// Renders the given <see cref="ModelPreviewSection"/> of a <see cref="ModelPreviewLOD"/> using the game's shader, if <see cref="MeshRenderContext.UseGameShaders"/> is set.
    /// Otherwise, or if that isn't possible, renders with LEX's shader.
    /// </summary>
    /// <param name="lod">The LOD to render.</param>
    /// <param name="s">Which faces to render.</param>
    /// <param name="context"></param>
    public override void RenderSection(ModelPreviewLOD<LEVertex> lod, ModelPreviewSection s, MeshRenderContext context)
    {
        if (context.UseGameShaders)
        {
            var material = (MaterialRenderProxy)Material;
            if (material.CanRenderWithGameShaders)
            {
                try
                {
                    RenderSectionWithGameShaders(lod, s, context);
                    return;
                }
                catch (Exception e)
                {
                    material.MarkGameShadersFailed(e);
                }
            }
            if (!LoggedFallback)
            {
                LoggedFallback = true;
                Debug.WriteLine($"{InstancedFullPath} will be rendered with the LEX shader: {material.GameShaderError}");
            }
        }
        context.RenderMeshWithDefaultEffect(lod.Mesh, s, FallbackDiffTexture?.TextureView);
    }

    /// <summary>
    /// Whether the mesh is lit by the level's lights in separate passes (see <see cref="RenderLighting"/>), as the game does for static meshes without a light-map.
    /// If not (and it has no light-map), it's lit by <see cref="MeshRenderContext.Lighting"/>
    /// </summary>
    private static bool UsesLevelLighting(ModelPreviewLOD<LEVertex> lod, MaterialRenderProxy material, MeshRenderContext context) =>
        context.IsLevelLightingActive && !material.IsUnlit && lod.StaticLighting is { UsesLightEnvironment: false };

    private void RenderSectionWithGameShaders(ModelPreviewLOD<LEVertex> lod, ModelPreviewSection s, MeshRenderContext context)
    {
        Mesh<LEVertex> mesh = lod.Mesh;
        var material = (MaterialRenderProxy)Material;
        LEEffect effect = context.LEEffect;
        effect.PixelShaderResources.Reset();
        (Shader vertexShader, Shader pixelShader) = (material.UnrealVertexShader, material.UnrealPixelShader);
        bool usePreviewLighting = !material.UsesLightMap;
        if (!material.UsesLightMap && material.NoLightMapVertexShader is not null && UsesLevelLighting(lod, material, context))
        {
            (vertexShader, pixelShader) = (material.NoLightMapVertexShader, material.NoLightMapPixelShader);
            usePreviewLighting = false;
        }
        PixelShader ps = context.GetCachedPixelShader(pixelShader.Guid, pixelShader.ShaderByteCode);
        (VertexShader vs, InputLayout inputLayout) = context.GetCachedVertexShader(vertexShader.Guid, vertexShader.ShaderByteCode);
        effect.PrepDraw(context.ImmediateContext, vs, ps, inputLayout, context.GetCachedBlendState(BlendDescription));
        //light-map UVs and samples. Unbound if there aren't any, since the shaders won't read them
        context.ImmediateContext.InputAssembler.SetVertexBuffers(MeshStaticLighting.VertexStreamSlot,
            new VertexBufferBinding(lod.StaticLighting?.VertexStream, MeshStaticLighting.VertexStreamStride, 0));
        //translucency is depth tested against the opaque geometry, but doesn't occlude anything itself
        context.ImmediateContext.OutputMerger.SetDepthStencilState(IsTranslucent ? context.TranslucentDepthState : context.DefaultDepthState);
        //meshes whose transform mirrors them have reversed winding
        bool reverseCulling = mesh.LocalToWorld.GetDeterminant() < 0;
        context.SetMeshRasterizerState(material.IsTwoSided, reverseCulling);

        try
        {
            material.UpdateShaderParams(effect.VertexShaderConstantBuffer, effect.PixelShaderConstantBuffer, context, mesh, vertexShader, pixelShader,
                lod.StaticLighting, usePreviewLighting);

            effect.RenderObject(context.ImmediateContext, mesh, (int)s.StartIndex, (int)s.TriangleCount * 3);

            var hitProxyConstants = new LEHitProxyConstants
            {
                HitProxyID = context.CurrentHitTestId,
                Flags = context.RenderFlags
            };
            effect.RenderHitProxy(context.ImmediateContext, hitProxyConstants, (int)s.StartIndex, (int)s.TriangleCount * 3);
        }
        finally
        {
            context.ImmediateContext.OutputMerger.SetDepthStencilState(context.DefaultDepthState);
            context.RestoreRasterizerState();
            //the material's textures may have changed some samplers' address modes, which LEX's shaders don't expect
            context.RestoreDefaultSamplers();
        }
    }

    //lights add to what's already been rendered
    private static readonly RenderTargetBlendDescription AdditiveBlendDescription = new()
    {
        RenderTargetWriteMask = ColorWriteMaskFlags.Red | ColorWriteMaskFlags.Green | ColorWriteMaskFlags.Blue,
        BlendOperation = BlendOperation.Add,
        AlphaBlendOperation = BlendOperation.Add,
        SourceBlend = BlendOption.One,
        DestinationBlend = BlendOption.One,
        SourceAlphaBlend = BlendOption.Zero,
        DestinationAlphaBlend = BlendOption.One,
        IsBlendEnabled = true
    };

    /// <summary>
    /// Adds each of the level's lights that this section isn't light-mapped for, as UE3's TMeshLightingDrawingPolicy does
    /// </summary>
    public override void RenderLighting(ModelPreviewLOD<LEVertex> lod, ModelPreviewSection s, MeshRenderContext context)
    {
        var material = (MaterialRenderProxy)Material;
        //UE3 renders lit translucency differently, and unlit materials have no light shaders
        if (!context.UseGameShaders || context.Wireframe || IsTranslucent || !UsesLevelLighting(lod, material, context) || !material.CanRenderWithGameShaders)
        {
            return;
        }
        LightInteraction[] interactions = lod.StaticLighting.GetLightInteractions(lod.Mesh.TransformedBounds);
        if (interactions.Length == 0)
        {
            return;
        }
        Mesh<LEVertex> mesh = lod.Mesh;
        LEEffect effect = context.LEEffect;
        effect.PixelShaderResources.Reset();
        context.ImmediateContext.InputAssembler.SetVertexBuffers(MeshStaticLighting.VertexStreamSlot,
            new VertexBufferBinding(lod.StaticLighting.VertexStream, MeshStaticLighting.VertexStreamStride, 0));
        context.ImmediateContext.OutputMerger.SetDepthStencilState(context.LightPassDepthState);
        context.SetMeshRasterizerState(material.IsTwoSided, mesh.LocalToWorld.GetDeterminant() < 0);
        BlendState blendState = context.GetCachedBlendState(AdditiveBlendDescription);
        try
        {
            foreach (LightInteraction interaction in interactions)
            {
                (Shader vertexShader, Shader pixelShader) = material.GetLightShaders(interaction.Light.Type, interaction.Shadowing);
                if (vertexShader is null)
                {
                    continue;
                }
                PixelShader ps = context.GetCachedPixelShader(pixelShader.Guid, pixelShader.ShaderByteCode);
                (VertexShader vs, InputLayout inputLayout) = context.GetCachedVertexShader(vertexShader.Guid, vertexShader.ShaderByteCode);
                effect.PrepDraw(context.ImmediateContext, vs, ps, inputLayout, blendState);
                context.ImmediateContext.InputAssembler.SetVertexBuffers(MeshStaticLighting.ShadowVertexStreamSlot,
                    new VertexBufferBinding(interaction.ShadowVertexBuffer, MeshStaticLighting.ShadowVertexStreamStride, 0));
                material.UpdateLightPassShaderParams(effect.VertexShaderConstantBuffer, effect.PixelShaderConstantBuffer, context, mesh, vertexShader, pixelShader, interaction);
                effect.RenderObject(context.ImmediateContext, mesh, (int)s.StartIndex, (int)s.TriangleCount * 3);
            }
        }
        finally
        {
            context.ImmediateContext.InputAssembler.SetVertexBuffers(MeshStaticLighting.ShadowVertexStreamSlot, new VertexBufferBinding(null, 0, 0));
            context.ImmediateContext.OutputMerger.SetDepthStencilState(context.DefaultDepthState);
            context.RestoreRasterizerState();
            context.RestoreDefaultSamplers();
        }
    }

    protected override MaterialInstanceConstantLevelEditor CreateMaterial(MeshRenderContext renderContext, ExportEntry export)
    {
        return new MaterialRenderProxy(renderContext, export);
    }
}

/// <summary>
/// Contains all the necessary resources (minus textures, which are cached in a <see cref="PreviewTextureCache"/>) needed to render a static preview of <see cref="SkeletalMesh"/> or <see cref="StaticMesh"/> instances.  
/// </summary>
public class ModelPreview<TVertex> : IDisposable where TVertex : IVertexBase
{
    /// <summary>
    /// Contains the geometry and section information for each level-of-detail in the model.
    /// </summary>
    public List<ModelPreviewLOD<TVertex>> LODs { get; } = [];

    /// <summary>
    /// Stores materials for this preview, stored by material name.
    /// </summary>
    public Dictionary<string, ModelPreviewMaterial<TVertex>> Materials { get; } = [];

    /// <summary>
    /// Creates a preview of a generic untextured mesh
    /// </summary>
    public ModelPreview(MeshRenderContext renderContext, Mesh<TVertex> mesh, PreloadedModelData preloadedData = null)
    {
        //Preloaded
        var sections = new List<ModelPreviewSection>();
        if (preloadedData != null)
        {
            sections = preloadedData.sections;
            foreach (ExportEntry mat in preloadedData.Materials.Distinct())
            {
                AddMaterial(renderContext, mat);
            }
        }
        LODs.Add(new ModelPreviewLOD<TVertex>(mesh, sections));
    }

    /// <summary>
    /// Creates a preview of the given <see cref="StaticMesh"/>.
    /// </summary>
    /// <param name="staticLighting">A component's precomputed lighting for this LOD. The preview takes ownership of it</param>
    public ModelPreview(MeshRenderContext renderContext, StaticMesh m, int selectedLOD, MeshStaticLighting staticLighting = null)
    {
        if (selectedLOD < 0)  //PREVIEW BUG WORKAROUND
            return;

        // STEP 1: MESH
        var lodModel = m.LODModels[selectedLOD];
        var triangles = new List<Triangle>(lodModel.IndexBuffer.Length / 3);
        var vertices = new List<TVertex>((int)lodModel.NumVertices);
        // Gather all the vertex data
        // Only one LOD? odd but I guess that's just how it rolls.

        StaticMeshVertexBuffer vertexBuffer = lodModel.VertexBuffer;
        for (int i = 0; i < lodModel.NumVertices; i++)
        {
            var position = lodModel.PositionVertexBuffer.VertexData[i];
            var vertex = vertexBuffer.VertexData[i];
            Fixed4<Vector4> uvs = default;
            if (vertexBuffer.bUseFullPrecisionUVs)
            {
                for (int j = 0; j < uvs.Length && j < vertex.FullPrecisionUVs.Length; j++)
                {
                    uvs[j] = new Vector4(vertex.FullPrecisionUVs[j], 0, 0);
                }
            }
            else
            {
                for (int j = 0; j < uvs.Length && j < vertex.HalfPrecisionUVs.Length; j++)
                {
                    uvs[j] = new Vector4(vertex.HalfPrecisionUVs[j], 0, 0);
                }
            }
            vertices.Add((TVertex)TVertex.Create(new Vector3(position.X, position.Y, position.Z), (Vector3)vertex.TangentX, (Vector4)vertex.TangentZ, uvs));
        }

        // Sometimes there might not be an index buffer.
        // If there is one, use that. 
        // Otherwise, assume that each vertex is used exactly once.
        // Note that this is based on the earlier implementation which didn't take LODs into consideration, which is odd considering that both the hit testing and the skeletalmesh class do.
        if (lodModel.IndexBuffer.Length > 0)
        {
            // Hey, we have indices all set up for us. How considerate.
            for (int i = 0; i < lodModel.IndexBuffer.Length; i += 3)
            {
                triangles.Add(new Triangle(lodModel.IndexBuffer[i], lodModel.IndexBuffer[i + 1], lodModel.IndexBuffer[i + 2]));
            }
        }
        else
        {
            // Gather all the vertex data from the raw triangles, not the Mesh.Vertices.Point list.
            if (m.Export.Game <= MEGame.ME2)
            {
                var kdop = m.kDOPTreeME1ME2;
                for (int i = 0; i < kdop.Triangles.Length; i++)
                {
                    triangles.Add(new Triangle(kdop.Triangles[i].Vertex1, kdop.Triangles[i].Vertex2, kdop.Triangles[i].Vertex3));
                }
            }
            else
            {
                var kdop = m.kDOPTreeME3UDKLE;
                for (int i = 0; i < kdop.Triangles.Length; i++)
                {
                    triangles.Add(new Triangle(kdop.Triangles[i].Vertex1, kdop.Triangles[i].Vertex2, kdop.Triangles[i].Vertex3));
                }
            }
        }

        // STEP 3: SECTIONS

        var sections = new List<ModelPreviewSection>();
        foreach (var element in lodModel.Elements)
        {
            if (element.Material is 0 || !m.Export.FileRef.IsEntry(element.Material))
            {
                sections.Add(new ModelPreviewSection(null, element.FirstIndex, element.NumTriangles));
            }
            else
            {
                IEntry matEntry = m.Export.FileRef.GetEntry(element.Material);
                AddMaterial(renderContext, matEntry, staticLighting?.LightMapType ?? ELightMapType.LMT_None);
                sections.Add(new ModelPreviewSection(matEntry.InstancedFullPath, element.FirstIndex, element.NumTriangles));
            }
        }
        var mesh = new Mesh<TVertex>(renderContext.Device, triangles, vertices);
        LODs.Add(new ModelPreviewLOD<TVertex>(mesh, sections) { StaticLighting = staticLighting });
        if (staticLighting is not null)
        {
            staticLighting.GetBounds = () => mesh.TransformedBounds;
            staticLighting.Materials = Materials.Values.Cast<object>().OfType<LEShaderPreviewMaterial>().Select(mat => mat.RenderProxy).ToArray();
        }
    }

    /// <summary>
    /// Creates a preview of the given <see cref="SkeletalMesh"/>.
    /// </summary>
    public ModelPreview(MeshRenderContext renderContext, SkeletalMesh m)
    {
        var mats = new string[m.Materials.Length];
        // STEP 1: MATERIALS
        for (int i = 0; i < m.Materials.Length; i++)
        {
            int materialUIndex = m.Materials[i];
            if (materialUIndex is not 0)
            {
                IEntry matEntry = m.Export.FileRef.GetEntry(materialUIndex);
                mats[i] = matEntry.InstancedFullPath;
                AddMaterial(renderContext, matEntry);
            }
        }

        // STEP 2: LODS
        foreach (var lodmodel in m.LODModels)
        {
            // Vertices
            var vertices = new List<TVertex>(m.Export.Game == MEGame.ME1 ? lodmodel.ME1VertexBufferGPUSkin.Length : lodmodel.VertexBufferGPUSkin.VertexData.Length);
            Fixed4<Vector4> uvs = default;
            if (m.Export.Game == MEGame.ME1)
            {
                foreach (SoftSkinVertex vertex in lodmodel.ME1VertexBufferGPUSkin)
                {
                    uvs[0] = new Vector4(vertex.UV, 0, 0);
                    vertices.Add((TVertex)TVertex.Create(new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z), (Vector3)vertex.TangentX, (Vector4)vertex.TangentZ, uvs));
                }
            }
            else
            {
                foreach (GPUSkinVertex vertex in lodmodel.VertexBufferGPUSkin.VertexData)
                {
                    uvs[0] = new Vector4(vertex.UV, 0, 0);
                    vertices.Add((TVertex)TVertex.Create(new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z), (Vector3)vertex.TangentX, (Vector4)vertex.TangentZ, uvs));
                }
            }
            // Triangles
            var triangles = new List<Triangle>(lodmodel.IndexBuffer.Length / 3);
            for (int i = 0; i < lodmodel.IndexBuffer.Length; i += 3)
            {
                triangles.Add(new Triangle(lodmodel.IndexBuffer[i], lodmodel.IndexBuffer[i + 1], lodmodel.IndexBuffer[i + 2]));
            }
            var mesh = new Mesh<TVertex>(renderContext.Device, triangles, vertices, isDynamic: true);
            // Sections
            var sections = new List<ModelPreviewSection>();
            foreach (var section in lodmodel.Sections)
            {
                if (section.MaterialIndex < Materials.Count)
                {
                    sections.Add(new ModelPreviewSection(mats[section.MaterialIndex], section.BaseIndex, (uint)section.NumTriangles));
                }
            }
            LODs.Add(new ModelPreviewLOD<TVertex>(mesh, sections));
        }
    }

    /// <summary>
    /// Adds a <see cref="ModelPreviewMaterial"/> to this model, or adds another reference of any conflicting material.
    /// </summary>
    private void AddMaterial(MeshRenderContext renderContext, IEntry matEntry, ELightMapType lightMapType = ELightMapType.LMT_None)
    {
        string ifp = matEntry.InstancedFullPath;
        if (!Materials.ContainsKey(ifp))
        {
            if (matEntry is not ExportEntry matExport)
            {
                matExport = EntryImporter.ResolveImport((ImportEntry)matEntry, renderContext.PackageCache);
                if (matExport is null)
                {
                    Debug.WriteLine("Could not find import material.");
                    Debug.WriteLine($"Import material: '{ifp}' from '{matEntry.FileRef.FilePath}'");
                    return;
                }
            }
            switch (Materials)
            {
                case Dictionary<string, ModelPreviewMaterial<WorldVertex>> worldVertMats:
                    worldVertMats.Add(ifp, new TexturedPreviewMaterial(renderContext, matExport));
                    break;
                case Dictionary<string, ModelPreviewMaterial<LEVertex>> leVertMats:
                    leVertMats.Add(ifp, new LEShaderPreviewMaterial(renderContext, matExport, lightMapType));
                    break;
            }
        }
    }
    /// <summary>
    /// Renders the ModelPreview at the specified level of detail
    /// </summary>
    /// <param name="renderPass">Only render materials that use this pass.</param>
    /// <param name="view">The SceneRenderControl to render the preview into.</param>
    /// <param name="lod">Which level of detail to render at. Level 0 is traditionally the most detailed.</param>
    public void Render(RenderPass renderPass, MeshRenderContext view, int lod)
    {
        if (lod >= LODs.Count) return;

        bool outsideView = view.CullingFrustum is { } frustum && !frustum.Intersects(LODs[lod].Mesh.TransformedBounds);
        foreach (ModelPreviewSection section in LODs[lod].Sections)
        {
            if (section.MaterialName is null)
            {
                if (outsideView) continue;
                if (view.Wireframe && LODs[lod].Mesh is Mesh<WorldVertex> mesh && renderPass is RenderPass.Base or RenderPass.ANY)
                {
                    view.RenderMeshAsWireframe(mesh, section);
                }
            }
            else if (Materials.TryGetValue(section.MaterialName, out ModelPreviewMaterial<TVertex> material))
            {
                // Test before shader setup, light interactions, and the base pass's hit-proxy draw.
                if (outsideView && material.CanCullByBounds(view)) continue;
                if (renderPass is RenderPass.Lighting)
                {
                    material.RenderLighting(LODs[lod], section, view);
                }
                else if (material.Pass == renderPass || renderPass is RenderPass.ANY)
                {
                    material.RenderSection(LODs[lod], section, view);
                }
            }
        }
    }

    public void UpdateLocalToWorld(Matrix4x4 ltw)
    {
        foreach (var lod in LODs)
        {
            lod.Mesh.LocalToWorld = ltw;
        }
    }

    /// <summary>
    /// Disposes any outstanding resources.
    /// </summary>
    public void Dispose()
    {
        Materials.Clear();
        foreach (var lod in LODs)
        {
            lod.Mesh.Dispose();
            lod.StaticLighting?.Dispose();
        }
        LODs.Clear();
    }
}

public class PreloadedModelData
{
    public object meshObject;
    public List<ModelPreviewSection> sections;
    public List<IEntry> Materials;

    public static PreloadedModelData LoadModel(ExportEntry export, PackageCache assetCache)
    {
        List<string> alreadyLoadedImportMaterials = [];
        var modelComp = ObjectBinary.From<Model>(export);
        var pmd = new PreloadedModelData
        {
            meshObject = modelComp,
            sections = [],
            Materials = [],
        };
        foreach (var mcExp in modelComp.Export.FileRef.Exports.Where(x =>
            x.ClassName == "ModelComponent" && !x.IsDefaultObject))
        {
            var mc = ObjectBinary.From<ModelComponent>(mcExp);
            if (mc.Model == modelComp.Self)
            {
                foreach (var element in mc.Elements)
                {
                    if (export == null) return pmd;
                    if (export.FileRef.IsUExport(element.Material))
                    {
                        ExportEntry entry = export.FileRef.GetUExport(element.Material);
                        AddMaterial(pmd.Materials, entry);
                    }
                    else if (export.FileRef.TryGetImport(element.Material, out var matImp) &&
                             alreadyLoadedImportMaterials.All(x => x != matImp.InstancedFullPath))
                    {
                        var extMaterialExport = EntryImporter.ResolveImport(matImp, assetCache);
                        if (extMaterialExport != null)
                        {
                            AddMaterial(pmd.Materials, extMaterialExport);
                            alreadyLoadedImportMaterials.Add(extMaterialExport.InstancedFullPath);
                        }
                        else
                        {
                            Debug.WriteLine("Could not find import material from FModelElement.");
                            Debug.WriteLine("Import material: " +
                                            export.FileRef.GetEntryString(element.Material));
                        }
                    }
                }
            }
        }
        return pmd;
    }
    public static PreloadedModelData LoadModelComponent(ExportEntry export, PackageCache assetCache)
    {
        var modelComp = ObjectBinary.From<ModelComponent>(export);
        var pmd = new PreloadedModelData
        {
            meshObject = modelComp,
            sections = [],
            Materials = [],
        };

        foreach (var element in modelComp.Elements)
        {
            if (export != null)
            {
                if (export.FileRef.TryGetUExport(element.Material, out var matExp))
                {
                    AddMaterial(pmd.Materials, matExp);
                    pmd.sections.Add(new ModelPreviewSection(matExp.InstancedFullPath, 0, 3)); //???
                }
                else if (export.FileRef.TryGetImport(element.Material, out var matImp))
                {
                    var extMaterialExport = EntryImporter.ResolveImport(matImp, assetCache);
                    //var extMaterialExport = ModelPreview.FindExternalAsset(matImp, pmd.texturePreviewMaterials.Select(x => x.Mip.Export).ToList(), cachedPackages);
                    if (extMaterialExport != null)
                    {
                        AddMaterial(pmd.Materials, extMaterialExport);
                    }
                    else
                    {
                        Debug.WriteLine("Could not find import material from section.");
                        Debug.WriteLine("Import material: " + export.FileRef.GetEntryString(element.Material));
                    }
                }
            }
        }
        return pmd;
    }

    private static void AddMaterial(List<IEntry> texturePreviewMaterials, ExportEntry entry)
    {
        if (texturePreviewMaterials.Any(x => x.InstancedFullPath == entry.InstancedFullPath))
            return; //already cached

        texturePreviewMaterials.Add(entry);
    }
}
