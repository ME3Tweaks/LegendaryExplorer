
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System.Numerics;
using LegendaryExplorerCore.Gammtek;
using SharpDX.Direct3D11;
using StaticMesh = LegendaryExplorerCore.Unreal.BinaryConverters.StaticMesh;
using SkeletalMesh = LegendaryExplorerCore.Unreal.BinaryConverters.SkeletalMesh;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

// MODEL RENDERING OVERVIEW:
// Construct a ModelPreview instance with an existing SkeletalMesh or StaticMesh.
// Call ModelPreview.Render(...) every frame. Boom.
namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D
{
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
        Default,
        Hair
    }

    /// <summary>
    /// Classes that inherit from ModelPreviewMaterial are responsible for rendering sections of meshes.
    /// </summary>
    public abstract class ModelPreviewMaterial<Vertex> : IDisposable where Vertex : IVertexBase
    {
        public RenderPass Pass;
        /// <summary>
        /// Creates a ModelPreviewMaterial that renders as close to what the given <see cref="MaterialInstanceConstant"/> looks like as possible. 
        /// </summary>
        /// <param name="texcache">The texture cache to request textures from.</param>
        /// <param name="mat">The material that this ModelPreviewMaterial will try to look like.</param>
        /// <param name="assetCache"></param>
        protected ModelPreviewMaterial(PreviewTextureCache texcache, MaterialInstanceConstant mat, PackageCache assetCache, List<PreloadedTextureData> preloadedTextures = null)
        {
            if (mat == null) return;
            Material = mat;
            Properties.Add("Name", mat.Export.ObjectName);
            foreach (IEntry textureEntry in mat.Textures)
            {
                if (!TextureMap.TryGetValue(textureEntry.FullPath, out PreviewTextureCache.TextureEntry texture))
                {
                    if (preloadedTextures != null)
                    {
                        var preloadedInfo = preloadedTextures.FirstOrDefault(x =>
                            x.MaterialExport == mat.Export && x.TextureExport.ObjectName.Name == textureEntry.ObjectName.Name); //i don't like matching on object name but its export vs import here.

                        if (preloadedInfo != null)
                        {
                            texture = texcache.LoadTexture(preloadedInfo.TextureExport);
                        }
                        else
                        {
                            Debug.WriteLine("Preloading error");
                        }
                    }
                    else if (textureEntry is ImportEntry import)
                    {
                        var extAsset = EntryImporter.ResolveImport(import, assetCache);
                        if (extAsset != null)
                        {
                            texture = texcache.LoadTexture(extAsset);
                        }
                    }
                    else
                    {
                        texture = texcache.LoadTexture(textureEntry as ExportEntry);
                    }
                    if (texture is not null)
                    {
                        TextureMap.Add(textureEntry.FullPath, texture);
                    }
                }
            }
        }
        protected readonly MaterialInstanceConstant Material;

        /// <summary>
        /// A Dictionary of string properties. Useful because some materials have properties that others don't.
        /// </summary>
        public readonly Dictionary<string, string> Properties = [];

        public readonly Dictionary<string, PreviewTextureCache.TextureEntry> TextureMap = [];

        /// <summary>
        /// Renders the given <see cref="ModelPreviewSection"/> of a <see cref="ModelPreviewLOD"/>. 
        /// </summary>
        /// <param name="lod">The LOD to render.</param>
        /// <param name="s">Which faces to render.</param>
        /// <param name="transform">The model transformation to be applied to the vertices.</param>
        /// <param name="view">The SceneRenderControl that the given LOD should be rendered into.</param>
        public abstract void RenderSection(ModelPreviewLOD<Vertex> lod, ModelPreviewSection s, Matrix4x4 transform, MeshRenderContext context);

        /// <summary>
        /// Disposes any outstanding resources.
        /// </summary>
        public virtual void Dispose()
        {
        }
    }

    public class TexturedPreviewMaterial : ModelPreviewMaterial<WorldVertex>
    {
        /// <summary>
        /// The full name of the diffuse texture property.
        /// </summary>
        public string DiffuseTextureFullName = "";

        /// <summary>
        /// Creates a TexturedPreviewMaterial that renders as close to what the given <see cref="MaterialInstanceConstant"/> looks like as possible. 
        /// </summary>
        /// <param name="texcache">The texture cache to request textures from.</param>
        /// <param name="mat">The material that this ModelPreviewMaterial will try to look like.</param>
        /// <param name="assetCache"></param>
        public TexturedPreviewMaterial(PreviewTextureCache texcache, MaterialInstanceConstant mat, PackageCache assetCache, List<PreloadedTextureData> preloadedTextures = null) : base(texcache, mat, assetCache, preloadedTextures)
        {
            string matPackage = null;
            if (mat.Export.Parent != null)
            {
                matPackage = mat.Export.Parent.FullPath.ToLower();
            }
            foreach (var textureEntry in mat.Textures)
            {
                var texObjectName = textureEntry.FullPath.ToLower();
                if ((matPackage == null || texObjectName.StartsWith(matPackage)) && texObjectName.Contains("diff"))
                {
                    // we have found the diffuse texture!
                    DiffuseTextureFullName = textureEntry.FullPath;
                    Debug.WriteLine("Diffuse texture of new material <" + Properties["Name"] + "> is " + DiffuseTextureFullName);
                    return;
                }
            }

            foreach (var textureEntry in mat.Textures)
            {
                var texObjectName = textureEntry.ObjectName.Name.ToLower();
                if (texObjectName.Contains("diff") || texObjectName.Contains("tex"))
                {
                    // we have found the diffuse texture!
                    DiffuseTextureFullName = textureEntry.FullPath;
                    Debug.WriteLine("Diffuse texture of new material <" + Properties["Name"] + "> is " + DiffuseTextureFullName);
                    return;
                }
            }
            foreach (var texparam in mat.Textures)
            {
                var texObjectName = texparam.ObjectName.Name.ToLower();

                if (texObjectName.Contains("detail"))
                {
                    // I guess a detail texture is good enough if we didn't return for a diffuse texture earlier...
                    DiffuseTextureFullName = texparam.FullPath;
                    Debug.WriteLine("Diffuse (Detail) texture of new material <" + Properties["Name"] + "> is " + DiffuseTextureFullName);
                    return;
                }
            }
            foreach (var texparam in mat.Textures)
            {
                var texObjectName = texparam.ObjectName.Name.ToLower();
                if (!texObjectName.Contains("norm") && !texObjectName.Contains("opac"))
                {
                    //Anything is better than nothing I suppose
                    DiffuseTextureFullName = texparam.FullPath;
                    Debug.WriteLine("Using first found texture (last resort)  of new material <" + Properties["Name"] + "> as diffuse: " + DiffuseTextureFullName);
                    return;
                }
            }
        }

        /// <summary>
        /// Renders the given <see cref="ModelPreviewSection"/> of a <see cref="ModelPreviewLOD"/>. 
        /// </summary>
        /// <param name="lod">The LOD to render.</param>
        /// <param name="s">Which faces to render.</param>
        /// <param name="transform">The model transformation to be applied to the vertices.</param>
        /// <param name="view">The SceneRenderControl that the given LOD should be rendered into.</param>
        public override void RenderSection(ModelPreviewLOD<WorldVertex> lod, ModelPreviewSection s, Matrix4x4 transform, MeshRenderContext context)
        {
            SceneCamera camera = context.Camera;
            context.DefaultEffect.PrepDraw(context.ImmediateContext, context.AlphaBlendState);
            var worldConstants = new MeshRenderContext.WorldConstants(
                Matrix4x4.Transpose(camera.ProjectionMatrix),
                Matrix4x4.Transpose(camera.ViewMatrix),
                Matrix4x4.Transpose(transform),
                context.CurrentTextureViewFlags);

            TextureMap.TryGetValue(DiffuseTextureFullName, out PreviewTextureCache.TextureEntry diffTexture);
            ShaderResourceView diffTextureView = diffTexture?.TextureView ?? context.DefaultTextureView;

            context.DefaultEffect.RenderObject(
                context.ImmediateContext,
                worldConstants,
                lod.Mesh,
                (int)s.StartIndex,
                (int)s.TriangleCount * 3,
                diffTextureView);
        }
    }

    /// <summary>
    /// A material with only a diffuse texture.
    /// </summary>
    file class LEShaderPreviewMaterial : ModelPreviewMaterial<LEVertex>
    {
        private readonly RenderTargetBlendDescription BlendDescription;


        /// <summary>
        /// Creates a TexturedPreviewMaterial that renders as close to what the given <see cref="MaterialRenderProxy"/> looks like as possible. 
        /// </summary>
        /// <param name="texcache">The texture cache to request textures from.</param>
        /// <param name="mat">The material that this ModelPreviewMaterial will try to look like.</param>
        /// <param name="assetCache"></param>
        /// <param name="preloadedTextures"></param>
        public LEShaderPreviewMaterial(PreviewTextureCache texcache, MaterialRenderProxy mat, PackageCache assetCache, List<PreloadedTextureData> preloadedTextures = null) : base(texcache, mat, assetCache, preloadedTextures)
        {
            mat.TextureMap = TextureMap;
            Pass = mat.UseHairPass ? RenderPass.Hair : default;
            switch (mat.BlendMode)
            {
                case EBlendMode.BLEND_Opaque:
                    BlendDescription = (new RenderTargetBlendDescription
                    {
                        RenderTargetWriteMask = ColorWriteMaskFlags.All,
                        BlendOperation = BlendOperation.Add,
                        AlphaBlendOperation = BlendOperation.Add,
                        SourceBlend = BlendOption.One,
                        DestinationBlend = BlendOption.Zero,
                        SourceAlphaBlend = BlendOption.One,
                        DestinationAlphaBlend = BlendOption.Zero,
                        IsBlendEnabled = false
                    });
                    break;
                case EBlendMode.BLEND_Masked:
                    BlendDescription = (new RenderTargetBlendDescription
                    {
                        RenderTargetWriteMask = ColorWriteMaskFlags.All,
                        BlendOperation = BlendOperation.Add,
                        AlphaBlendOperation = BlendOperation.Add,
                        SourceBlend = BlendOption.One,
                        DestinationBlend = BlendOption.Zero,
                        SourceAlphaBlend = BlendOption.One,
                        DestinationAlphaBlend = BlendOption.Zero,
                        IsBlendEnabled = false
                    });
                    break;
                case EBlendMode.BLEND_Translucent:
                    BlendDescription = (new RenderTargetBlendDescription
                    {
                        RenderTargetWriteMask = ColorWriteMaskFlags.All,
                        BlendOperation = BlendOperation.Add,
                        AlphaBlendOperation = BlendOperation.Add,
                        SourceBlend = BlendOption.SourceAlpha,
                        DestinationBlend = BlendOption.InverseSourceAlpha,
                        SourceAlphaBlend = BlendOption.SourceAlphaSaturate,
                        DestinationAlphaBlend = BlendOption.InverseSourceAlpha,
                        IsBlendEnabled = true
                    });
                    break;
                //TODO: the ones above this comment seem to work properly, but the rest need verifying
                case EBlendMode.BLEND_Additive:
                    BlendDescription = (new RenderTargetBlendDescription
                    {
                        RenderTargetWriteMask = ColorWriteMaskFlags.All,
                        BlendOperation = BlendOperation.Add,
                        AlphaBlendOperation = BlendOperation.Add,
                        SourceBlend = BlendOption.One,
                        DestinationBlend = BlendOption.One,
                        SourceAlphaBlend = BlendOption.Zero,
                        DestinationAlphaBlend = BlendOption.One,
                        IsBlendEnabled = true
                    });
                    break;
                case EBlendMode.BLEND_Modulate:
                    BlendDescription = (new RenderTargetBlendDescription
                    {
                        RenderTargetWriteMask = ColorWriteMaskFlags.All,
                        BlendOperation = BlendOperation.Add,
                        AlphaBlendOperation = BlendOperation.Add,
                        SourceBlend = BlendOption.DestinationColor,
                        DestinationBlend = BlendOption.Zero,
                        SourceAlphaBlend = BlendOption.Zero,
                        DestinationAlphaBlend = BlendOption.One,
                        IsBlendEnabled = true
                    });
                    break;
                case EBlendMode.BLEND_SoftMasked:
                    BlendDescription = (new RenderTargetBlendDescription
                    {
                        RenderTargetWriteMask = ColorWriteMaskFlags.All,
                        BlendOperation = BlendOperation.Add,
                        AlphaBlendOperation = BlendOperation.Add,
                        SourceBlend = BlendOption.SourceAlpha,
                        DestinationBlend = BlendOption.InverseSourceAlpha,
                        SourceAlphaBlend = BlendOption.Zero,
                        DestinationAlphaBlend = BlendOption.InverseSourceAlpha,
                        IsBlendEnabled = true
                    });
                    break;
                case EBlendMode.BLEND_AlphaComposite:
                    BlendDescription = (new RenderTargetBlendDescription
                    {
                        RenderTargetWriteMask = ColorWriteMaskFlags.All,
                        BlendOperation = BlendOperation.Add,
                        AlphaBlendOperation = BlendOperation.Add,
                        SourceBlend = BlendOption.One,
                        DestinationBlend = BlendOption.InverseSourceAlpha,
                        SourceAlphaBlend = BlendOption.One,
                        DestinationAlphaBlend = BlendOption.InverseSourceAlpha,
                        IsBlendEnabled = false
                    });
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Renders the given <see cref="ModelPreviewSection"/> of a <see cref="ModelPreviewLOD{LEVertex}"/>. 
        /// </summary>
        /// <param name="lod">The LOD to render.</param>
        /// <param name="s">Which faces to render.</param>
        /// <param name="transform">The model transformation to be applied to the vertices.</param>
        /// <param name="context"></param>
        public override void RenderSection(ModelPreviewLOD<LEVertex> lod, ModelPreviewSection s, Matrix4x4 transform, MeshRenderContext context)
        {
            Mesh<LEVertex> mesh = lod.Mesh;
            SceneCamera camera = context.Camera;
            var material = (MaterialRenderProxy)Material;
            LEEffect effect = context.LEEffect;
            PixelShader ps = context.GetCachedPixelShader(material.PixelShader.Guid, material.PixelShader.ShaderByteCode);
            (VertexShader vs, InputLayout inputLayout) = context.GetCachedVertexShader(material.VertexShader.Guid, material.VertexShader.ShaderByteCode);
            effect.PrepDraw(context.ImmediateContext, vs, ps, inputLayout, context.GetCachedBlendState(BlendDescription));

            Matrix4x4 viewMatrix = camera.ViewMatrix;
            var vsConstants = new LEVSConstants
            {
                ViewProjectionMatrix = viewMatrix * camera.ProjectionMatrix,
                CameraPosition = new Vector4(camera.Position, 1),
                PreViewTranslation = Vector4.Zero,
            };
            float depthMul = camera.ProjectionMatrix[2, 2];
            float depthAdd = camera.ProjectionMatrix[3, 2];
            if (false) //TODO: check if Z is inverted, if so this should be true
            {
                depthMul = 1f - depthMul;
                depthAdd = -depthAdd;
            }
            var psConstants = new LEPSConstants
            {
                ScreenPositionScaleBias = new Vector4(1f / 2f, 1f / -2f, (context.Height / 2f + 0.5f) / context.Height, (context.Width / 2f + 0.5f) / context.Width),
                MinZ_MaxZRatio = new Vector4(depthAdd, depthMul, 1f / depthAdd, depthMul / depthAdd),
                DynamicScale = Vector4.One,
            };

            material.UpdateShaderParams(effect.VertexShaderConstantBuffer, effect.PixelShaderConstantBuffer, context, mesh);

            effect.RenderObject(context.ImmediateContext, vsConstants, psConstants, mesh, (int)s.StartIndex, (int)s.TriangleCount * 3);
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
        /// <param name="device"></param>
        /// <param name="mesh"></param>
        public ModelPreview(Device device, Mesh<TVertex> mesh, PreviewTextureCache texcache, PackageCache assetCache, PreloadedModelData preloadedData = null)
        {
            //Preloaded
            var sections = new List<ModelPreviewSection>();
            if (preloadedData != null)
            {
                sections = preloadedData.sections;
                var uniqueMaterials = preloadedData.texturePreviewMaterials.Select(x => x.MaterialExport).Distinct();
                foreach (ExportEntry mat in uniqueMaterials)
                {
                    AddMaterial(mat.ObjectName.Name, texcache, mat, assetCache, preloadedData.texturePreviewMaterials);
                }
            }
            LODs.Add(new ModelPreviewLOD<TVertex>(mesh, sections));
        }

        /// <summary>
        /// Creates a preview of the given <see cref="StaticMesh"/>.
        /// </summary>
        /// <param name="Device">The Direct3D device to use for buffer creation.</param>
        /// <param name="m">The mesh to generate a preview for.</param>
        /// <param name="texcache">The texture cache for loading textures.</param>
        public ModelPreview(Device Device, StaticMesh m, int selectedLOD, PreviewTextureCache texcache, PackageCache assetCache, PreloadedModelData preloadedData = null)
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
                vertices.Add((TVertex)TVertex.Create(new Vector3(-position.X, position.Z, position.Y), (Vector3)vertex.TangentX, (Vector4)vertex.TangentZ, uvs));
            }

            //OLD CODE
            //for (int i = 0; i < m.L.Vertices.Points.Count; i++)
            //{
            //    // Note the reversal of the Z and Y coordinates. Unreal seems to think that Z should be up.
            //    vertices.Add(new Scene3D.WorldVertex(new SharpDX.Vector3(-m.Mesh.Vertices.Points[i].X, m.Mesh.Vertices.Points[i].Z, m.Mesh.Vertices.Points[i].Y), SharpDX.Vector3.Zero, new SharpDX.Vector2(m.Mesh.Edges.UVSet[i].UVs[0].X, m.Mesh.Edges.UVSet[i].UVs[0].Y)));
            //}

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
            var sections = preloadedData?.sections;
            var assetLookupPackagesToDispose = new List<IMEPackage>();

            //This section exists for Meshplorer Winforms. WPF version preloads this in a background thread to improve performance
            if (sections == null)
            {
                throw new Exception("Sections was null, comment indicated this code should not have been reachable");
            }
            else
            {
                //Preloaded
                sections = preloadedData.sections;
                var uniqueMaterials = preloadedData.texturePreviewMaterials.Select(x => x.MaterialExport).Distinct();
                foreach (var mat in uniqueMaterials)
                {
                    AddMaterial(mat.ObjectName.Name, texcache, mat, assetCache, preloadedData.texturePreviewMaterials);
                }
            }

            foreach (var package in assetLookupPackagesToDispose)
            {
                package?.Dispose(); //Release
            }
            LODs.Add(new ModelPreviewLOD<TVertex>(new Mesh<TVertex>(Device, triangles, vertices), sections));
        }

        /// <summary>
        /// Adds a <see cref="ModelPreviewMaterial{Vertex}"/> to this model, or adds another reference of any conflicting material.
        /// </summary>
        private void AddMaterial(string name, PreviewTextureCache texcache, ExportEntry materialExport, PackageCache assetCache, List<PreloadedTextureData> preloadedTextures = null)
        {
            if (!Materials.ContainsKey(name))
            {
                switch (Materials)
                {
                    case Dictionary<string, ModelPreviewMaterial<WorldVertex>> worldVertMats:
                        worldVertMats.Add(name, new TexturedPreviewMaterial(texcache, new MaterialInstanceConstant(materialExport, assetCache), assetCache, preloadedTextures));
                        break;
                    case Dictionary<string, ModelPreviewMaterial<LEVertex>> leVertMats:
                        leVertMats.Add(name, new LEShaderPreviewMaterial(texcache, new MaterialRenderProxy(materialExport, assetCache), assetCache, preloadedTextures));
                        break;
                }
            }
        }

        /// <summary>
        /// Creates a preview of the given <see cref="SkeletalMesh"/>.
        /// </summary>
        /// <param name="Device">The Direct3D device to use for buffer creation.</param>
        /// <param name="m">The mesh to generate a preview for.</param>
        /// <param name="texcache">The texture cache for loading textures.</param>
        public ModelPreview(Device Device, SkeletalMesh m, PreviewTextureCache texcache, PackageCache assetCache, PreloadedModelData preloadedData = null)
        {
            // STEP 1: MATERIALS
            if (preloadedData == null)
            {
                foreach (int materialUIndex in m.Materials)
                {
                    ExportEntry matExport = null;
                    if (materialUIndex > 0)
                    {
                        matExport = m.Export.FileRef.GetUExport(materialUIndex);
                    }
                    else if (materialUIndex < 0)
                    {
                        // The material instance is an import!
                        ImportEntry matImport = m.Export.FileRef.GetImport(materialUIndex);
                        var externalAsset = EntryImporter.ResolveImport(matImport, assetCache);
                        if (externalAsset != null)
                        {
                            matExport = externalAsset;
                        }
                    }

                    if (matExport != null)
                    {
                        AddMaterial(matExport.ObjectName, texcache, matExport, assetCache);
                    }
                }
            }
            else
            {
                //Preloaded
                //sections = preloadedData.sections;
                var uniqueMaterials = preloadedData.texturePreviewMaterials.Select(x => x.MaterialExport).Distinct();
                foreach (var mat in uniqueMaterials)
                {
                    AddMaterial(mat.ObjectName.Name, texcache, mat, assetCache, preloadedData.texturePreviewMaterials);
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
                        vertices.Add((TVertex)TVertex.Create(new Vector3(-vertex.Position.X, vertex.Position.Z, vertex.Position.Y), (Vector3)vertex.TangentX, (Vector4)vertex.TangentZ, uvs));
                    }
                }
                else
                {
                    foreach (GPUSkinVertex vertex in lodmodel.VertexBufferGPUSkin.VertexData)
                    {
                        uvs[0] = new Vector4(vertex.UV, 0, 0);
                        vertices.Add((TVertex)TVertex.Create(new Vector3(-vertex.Position.X, vertex.Position.Z, vertex.Position.Y), (Vector3)vertex.TangentX, (Vector4)vertex.TangentZ, uvs));
                    }
                }
                // Triangles
                var triangles = new List<Triangle>(lodmodel.IndexBuffer.Length / 3);
                for (int i = 0; i < lodmodel.IndexBuffer.Length; i += 3)
                {
                    triangles.Add(new Triangle(lodmodel.IndexBuffer[i], lodmodel.IndexBuffer[i + 1], lodmodel.IndexBuffer[i + 2]));
                }
                var mesh = new Mesh<TVertex>(Device, triangles, vertices);
                // Sections
                var sections = new List<ModelPreviewSection>();
                foreach (var section in lodmodel.Sections)
                {
                    if (section.MaterialIndex < Materials.Count)
                    {
                        sections.Add(new ModelPreviewSection(Materials.Keys.ElementAt(section.MaterialIndex), section.BaseIndex, (uint)section.NumTriangles));
                    }
                }
                LODs.Add(new ModelPreviewLOD<TVertex>(mesh, sections));
            }
        }

        /// <summary>
        /// Renders the ModelPreview at the specified level of detail.
        /// </summary>
        /// <param name="renderPass">Only render materials that use this pass.</param>
        /// <param name="view">The SceneRenderControl to render the preview into.</param>
        /// <param name="lod">Which level of detail to render at. Level 0 is traditionally the most detailed.</param>
        /// <param name="transform">The model transformation to be applied to the vertices.</param>
        public void Render(RenderPass renderPass, MeshRenderContext view, int lod, Matrix4x4 transform)
        {
            foreach (ModelPreviewSection section in LODs[lod].Sections)
            {
                if (Materials.TryGetValue(section.MaterialName, out ModelPreviewMaterial<TVertex> material)
                    && material.Pass == renderPass)
                {
                    material.RenderSection(LODs[lod], section, transform, view);
                }
            }
        }

        /// <summary>
        /// Disposes any outstanding resources.
        /// </summary>
        public void Dispose()
        {
            foreach (ModelPreviewMaterial<TVertex> mat in Materials.Values)
            {
                mat.Dispose();
            }
            Materials.Clear();
            foreach (ModelPreviewLOD<TVertex> lod in LODs)
            {
                lod.Mesh.Dispose();
            }
            LODs.Clear();
        }
    }

    public class PreloadedModelData
    {
        public object meshObject;
        public List<ModelPreviewSection> sections;
        public List<PreloadedTextureData> texturePreviewMaterials;

    }

    public class PreloadedTextureData
    {
        public ExportEntry MaterialExport { get; internal init; }
        public ExportEntry TextureExport { get; internal init; }
    }
}
