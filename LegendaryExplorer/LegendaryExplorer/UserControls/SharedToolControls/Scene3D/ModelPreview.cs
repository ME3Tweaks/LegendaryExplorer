using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System.Numerics;
using SharpDX.Direct3D11;
using StaticMesh = LegendaryExplorerCore.Unreal.BinaryConverters.StaticMesh;
using static LegendaryExplorer.UserControls.SharedToolControls.Scene3D.ModelPreview;
using SkeletalMesh = LegendaryExplorerCore.Unreal.BinaryConverters.SkeletalMesh;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.Classes;

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
    public class ModelPreviewLOD
    {
        /// <summary>
        /// The geometry of this level of detail.
        /// </summary>
        public WorldMesh Mesh;

        /// <summary>
        /// A list of which materials are applied to which triangles.
        /// </summary>
        public List<ModelPreviewSection> Sections;

        /// <summary>
        /// Creates a new ModelPreviewLOD.
        /// </summary>
        /// <param name="mesh">The geometry of this level of detail.</param>
        /// <param name="sections">A list of which materials are applied to which triangles.</param>
        public ModelPreviewLOD(WorldMesh mesh, List<ModelPreviewSection> sections)
        {
            Mesh = mesh;
            Sections = sections;
        }
    }

    /// <summary>
    /// Classes that inherit from ModelPreviewMaterial are responsible for rendering sections of meshes.
    /// </summary>
    public abstract class ModelPreviewMaterial : IDisposable
    {
        /// <summary>
        /// Creates a ModelPreviewMaterial that renders as close to what the given <see cref="MaterialInstanceConstant"/> looks like as possible. 
        /// </summary>
        /// <param name="texcache">The texture cache to request textures from.</param>
        /// <param name="mat">The material that this ModelPreviewMaterial will try to look like.</param>
        /// <param name="assetCache"></param>
        protected ModelPreviewMaterial(PreviewTextureCache texcache, MaterialInstanceConstant mat, PackageCache assetCache, List<PreloadedTextureData> preloadedTextures = null)
        {
            if (mat == null) return;
            Properties.Add("Name", mat.Export.ObjectName);
            foreach (var textureEntry in mat.Textures)
            {
                if (!Textures.ContainsKey(textureEntry.FullPath) && textureEntry.ClassName == "Texture2D")  //Apparently some assets are cubemaps, we don't want these.
                {
                    if (preloadedTextures != null)
                    {
                        var preloadedInfo = preloadedTextures.FirstOrDefault(x => x.MaterialExport == mat.Export && x.Mip.Export.ObjectName.Name == textureEntry.ObjectName.Name); //i don't like matching on object name but its export vs import here.
                        if (preloadedInfo != null)
                        {
                            Textures.Add(textureEntry.FullPath, texcache.LoadTexture(preloadedInfo.Mip.Export, preloadedInfo.Mip, preloadedInfo.decompressedTextureData));
                        }
                        else
                        {
                            Debug.WriteLine("Preloading error");
                        }
                        //if (textureEntry is ExportEntry texPort && preloadedMipInfo.Export != texPort) throw new Exception();
                        continue; //Don't further parse
                    }

                    if (textureEntry is ImportEntry import)
                    {
                        var extAsset = EntryImporter.ResolveImport(import, null, assetCache);
                        if (extAsset != null)
                        {
                            Textures.Add(textureEntry.FullPath, texcache.LoadTexture(extAsset));
                        }
                    }
                    else
                    {
                        Textures.Add(textureEntry.FullPath, texcache.LoadTexture(textureEntry as ExportEntry));
                    }
                }
            }
        }

        /// <summary>
        /// A Dictionary of string properties. Useful because some materials have properties that others don't.
        /// </summary>
        public Dictionary<string, string> Properties = new();

        public Dictionary<string, PreviewTextureCache.PreviewTextureEntry> Textures = new();

        /// <summary>
        /// Renders the given <see cref="ModelPreviewSection"/> of a <see cref="ModelPreviewLOD"/>. 
        /// </summary>
        /// <param name="lod">The LOD to render.</param>
        /// <param name="s">Which faces to render.</param>
        /// <param name="transform">The model transformation to be applied to the vertices.</param>
        /// <param name="view">The SceneRenderControl that the given LOD should be rendered into.</param>
        public abstract void RenderSection(ModelPreviewLOD lod, ModelPreviewSection s, Matrix4x4 transform, MeshRenderContext context);

        /// <summary>
        /// Disposes any outstanding resources.
        /// </summary>
        public virtual void Dispose()
        {
        }
    }

    /// <summary>
    /// A material with only a diffuse texture.
    /// </summary>
    public class TexturedPreviewMaterial : ModelPreviewMaterial
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
        public override void RenderSection(ModelPreviewLOD lod, ModelPreviewSection s, Matrix4x4 transform, MeshRenderContext context)
        {
            context.DefaultEffect.PrepDraw(context.ImmediateContext);
            context.DefaultEffect.RenderObject(context.ImmediateContext, new MeshRenderContext.WorldConstants(Matrix4x4.Transpose(context.Camera.ProjectionMatrix), Matrix4x4.Transpose(context.Camera.ViewMatrix), Matrix4x4.Transpose(transform), context.CurrentTextureViewFlags), lod.Mesh, (int)s.StartIndex, (int)s.TriangleCount * 3, Textures.ContainsKey(DiffuseTextureFullName) ? Textures[DiffuseTextureFullName]?.TextureView ?? context.DefaultTextureView : context.DefaultTextureView);
        }
    }

    /// <summary>
    /// Contains all the necessary resources (minus textures, which are cached in a <see cref="PreviewTextureCache"/>) needed to render a static preview of <see cref="SkeletalMesh"/> or <see cref="StaticMesh"/> instances.  
    /// </summary>
    public class ModelPreview : IDisposable
    {
        /// <summary>
        /// Contains the geometry and section information for each level-of-detail in the model.
        /// </summary>
        public List<ModelPreviewLOD> LODs = new();

        /// <summary>
        /// Stores materials for this preview, stored by material name.
        /// </summary>
        public Dictionary<string, ModelPreviewMaterial> Materials = new();

        /// <summary>
        /// Creates a preview of a generic untextured mesh
        /// </summary>
        /// <param name="device"></param>
        /// <param name="mesh"></param>
        public ModelPreview(Device device, WorldMesh mesh, PreviewTextureCache texcache, PackageCache assetCache, PreloadedModelData preloadedData = null)
        {
            //Preloaded
            var sections = new List<ModelPreviewSection>();
            if (preloadedData != null)
            {
                sections = preloadedData.sections;
                var uniqueMaterials = preloadedData.texturePreviewMaterials.Select(x => x.MaterialExport).Distinct();
                foreach (var mat in uniqueMaterials)
                {
                    var material = new TexturedPreviewMaterial(texcache, new MaterialInstanceConstant(mat), assetCache, preloadedData.texturePreviewMaterials);
                    AddMaterial(mat.ObjectName.Name, material);
                }
            }
            LODs.Add(new ModelPreviewLOD(mesh, sections));
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
            var vertices = new List<WorldVertex>((int)lodModel.NumVertices);
            // Gather all the vertex data
            // Only one LOD? odd but I guess that's just how it rolls.

            for (int i = 0; i < lodModel.NumVertices; i++)
            {
                var v = lodModel.PositionVertexBuffer.VertexData[i];
                if (lodModel.VertexBuffer.bUseFullPrecisionUVs)
                {
                    var uvVector = lodModel.VertexBuffer.VertexData[i].FullPrecisionUVs;
                    //SharpDX takes items differently than unreal.
                    vertices.Add(new Scene3D.WorldVertex(new Vector3(-v.X, v.Z, v.Y), Vector3.Zero, new Vector2(uvVector[0].X, uvVector[0].Y)));
                }
                else
                {
                    var uvVector = lodModel.VertexBuffer.VertexData[i].HalfPrecisionUVs;
                    //SharpDX takes items differently than unreal.
                    vertices.Add(new Scene3D.WorldVertex(new Vector3(-v.X, v.Z, v.Y), Vector3.Zero, new Vector2(uvVector[0].X, uvVector[0].Y)));
                }

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

            /*
            if (m.Mesh.IdxBuf.Indexes != null && m.Mesh.IdxBuf.Indexes.Count > 0)
            {
                // Hey, we have indices all set up for us. How considerate.
                for (int i = 0; i < m.Mesh.IdxBuf.Indexes.Count; i += 3)
                {
                    triangles.Add(new Triangle(m.Mesh.IdxBuf.Indexes[i], m.Mesh.IdxBuf.Indexes[i + 1], m.Mesh.IdxBuf.Indexes[i + 2]));
                }
            }
            else
            {
                // Gather all the vertex data from the raw triangles, not the Mesh.Vertices.Point list.
                for (int i = 0; i < m.Mesh.RawTris.RawTriangles.Count; i++)
                {
                    triangles.Add(new Triangle((uint)m.Mesh.RawTris.RawTriangles[i].v0, (uint)m.Mesh.RawTris.RawTriangles[i].v1, (uint)m.Mesh.RawTris.RawTriangles[i].v2));
                }
            }*/

            //OLD CODE
            /* if (m.Mesh.IdxBuf.Indexes != null && m.Mesh.IdxBuf.Indexes.Count > 0)
                        {
                            // Hey, we have indices all set up for us. How considerate.
                            for (int i = 0; i < m.Mesh.IdxBuf.Indexes.Count; i += 3)
                            {
                                triangles.Add(new Triangle(m.Mesh.IdxBuf.Indexes[i], m.Mesh.IdxBuf.Indexes[i + 1], m.Mesh.IdxBuf.Indexes[i + 2]));
                            }
                        }
                        else
                        {
                            // Gather all the vertex data from the raw triangles, not the Mesh.Vertices.Point list.
                            for (int i = 0; i < m.Mesh.RawTris.RawTriangles.Count; i++)
                            {
                                triangles.Add(new Triangle((uint)m.Mesh.RawTris.RawTriangles[i].v0, (uint)m.Mesh.RawTris.RawTriangles[i].v1, (uint)m.Mesh.RawTris.RawTriangles[i].v2));
                            }
                        }*/

            // STEP 2: MATERIALS
            //foreach (var v in lodModel.Elements)

            //foreach (Unreal.Classes.MaterialInstanceConstant mat in m.Mesh.Mat.MatInst)
            //{
            //    ModelPreviewMaterial material;
            //    // TODO: pick what material class best fits based on what properties the 
            //    // MaterialInstanceConstant mat has.
            //    // For now, just use the default material.
            //    material = new TexturedPreviewMaterial(texcache, mat);
            //    AddMaterial(material.Properties["Name"], material);
            //}

            // STEP 3: SECTIONS
            var sections = preloadedData?.sections;
            var assetLookupPackagesToDispose = new List<IMEPackage>();

            //This section exists for Meshplorer Winforms. WPF version preloads this in a background thread to improve performance
            if (sections == null)
            {
                throw new Exception("Sections was null, comment indicated this code should not have been reachable");
                //sections = new List<ModelPreviewSection>();
                //foreach (var section in lodModel.Elements)
                //{
                //    if (section.Material.value > 0)
                //    {
                //        // TODO: pick what material class best fits based on what properties the 
                //        // MaterialInstanceConstant mat has.
                //        // For now, just use the default material.
                //        ExportEntry entry = m.Export.FileRef.GetUExport(section.Material.value);
                //        ModelPreviewMaterial material = new TexturedPreviewMaterial(texcache, new MaterialInstanceConstant(entry), assetCache);
                //        AddMaterial(material.Properties["Name"], material);
                //    }
                //    else if (section.Material.value < 0)
                //    {
                //        var extMaterialExport = FindExternalAsset(m.Export.FileRef.GetImport(section.Material.value), texcache.AssetCache.Select(x => x.TextureExport).ToList(), assetLookupPackagesToDispose);
                //        if (extMaterialExport != null)
                //        {
                //            // TODO: pick what material class best fits based on what properties the 
                //            // MaterialInstanceConstant mat has.
                //            // For now, just use the default material.
                //            ModelPreviewMaterial material = new TexturedPreviewMaterial(texcache, new MaterialInstanceConstant(extMaterialExport), assetCache);
                //            AddMaterial(material.Properties["Name"], material);
                //        }
                //        else
                //        {

                //            Debug.WriteLine("Could not find import material from section.");
                //            Debug.WriteLine("Import material: " + m.Export.FileRef.GetEntryString(section.Material.value));
                //        }
                //    }

                //    sections.Add(new ModelPreviewSection(m.Export.FileRef.getObjectName(section.Material.value), section.FirstIndex, section.NumTriangles));
                //}
            }
            else
            {
                //Preloaded
                sections = preloadedData.sections;
                var uniqueMaterials = preloadedData.texturePreviewMaterials.Select(x => x.MaterialExport).Distinct();
                foreach (var mat in uniqueMaterials)
                {
                    var material = new TexturedPreviewMaterial(texcache, new MaterialInstanceConstant(mat), assetCache, preloadedData.texturePreviewMaterials);
                    AddMaterial(mat.ObjectName.Name, material);
                }
            }

            foreach (var package in assetLookupPackagesToDispose)
            {
                package?.Dispose(); //Release
            }
            //List<ModelPreviewSection> sections = new List<ModelPreviewSection>();
            //foreach (var section in lodModel.Elements)
            //{
            //    sections.Add(new ModelPreviewSection(m.Export.FileRef.getObjectName(section.Material.value), section.FirstIndex, section.NumTriangles));
            //}
            LODs.Add(new ModelPreviewLOD(new WorldMesh(Device, triangles, vertices), sections));
        }

        /// <summary>
        /// Internal method for decoding UV values.
        /// </summary>
        /// <param name="val">The <see cref="float"/> encoded as a <see cref="ushort"/>.</param>
        /// <returns>The decoded <see cref="float"/>.</returns>
        public static float HalfToFloat(ushort val)
        {
            ushort u = val;
            int sign = (u >> 15) & 0x00000001;
            int exp = (u >> 10) & 0x0000001F;
            int mant = u & 0x000003FF;
            exp = exp + (127 - 15);
            int i = (sign << 31) | (exp << 23) | (mant << 13);
            byte[] buff = BitConverter.GetBytes(i);
            return BitConverter.ToSingle(buff, 0);
        }

        /// <summary>
        /// Adds a <see cref="ModelPreviewMaterial"/> to this model, or adds another reference of any conflicting material.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="material"></param>
        private void AddMaterial(string name, ModelPreviewMaterial material)
        {
            if (Materials.ContainsKey(name))
            {
                material.Dispose(); // We'll use the existing version of the duplicate, so this one is no longer needed.
                AddMaterial(name + "_duplicate", Materials[name]);
            }
            else
            {
                Materials.Add(name, material);
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
                    MaterialInstanceConstant mat = null;
                    if (materialUIndex > 0)
                    {
                        mat = new MaterialInstanceConstant(m.Export.FileRef.GetUExport(materialUIndex));
                    }
                    else if (materialUIndex < 0)
                    {
                        // The material instance is an import!
                        ImportEntry matImport = m.Export.FileRef.GetImport(materialUIndex);
                        var externalAsset = EntryImporter.ResolveImport(matImport, null, assetCache);
                        if (externalAsset != null)
                        {
                            mat = new MaterialInstanceConstant(externalAsset);
                        }
                    }

                    if (mat != null)
                    {
                        // TODO: pick what material class best fits based on what properties the 
                        // MaterialInstanceConstant mat has.
                        // For now, just use the default material.
                        ModelPreviewMaterial material = new TexturedPreviewMaterial(texcache, mat, assetCache);
                        AddMaterial(material.Properties["Name"], material);
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
                    var material = new TexturedPreviewMaterial(texcache, new MaterialInstanceConstant(mat, assetCache), assetCache, preloadedData.texturePreviewMaterials);
                    AddMaterial(mat.ObjectName.Name, material);
                }
            }

            // STEP 2: LODS
            foreach (var lodmodel in m.LODModels)
            {
                // Vertices
                var vertices = new List<WorldVertex>(m.Export.Game == MEGame.ME1 ? lodmodel.ME1VertexBufferGPUSkin.Length : lodmodel.VertexBufferGPUSkin.VertexData.Length);
                if (m.Export.Game == MEGame.ME1)
                {
                    foreach (var vertex in lodmodel.ME1VertexBufferGPUSkin)
                    {
                        vertices.Add(new WorldVertex(new Vector3(-vertex.Position.X, vertex.Position.Z, vertex.Position.Y), Vector3.Zero, new Vector2(vertex.UV.X, vertex.UV.Y)));
                    }
                }
                else
                {
                    foreach (var vertex in lodmodel.VertexBufferGPUSkin.VertexData)
                    {
                        vertices.Add(new WorldVertex(new Vector3(-vertex.Position.X, vertex.Position.Z, vertex.Position.Y), Vector3.Zero, new Vector2(vertex.UV.X, vertex.UV.Y)));
                    }
                }
                // Triangles
                var triangles = new List<Triangle>(lodmodel.IndexBuffer.Length / 3);
                for (int i = 0; i < lodmodel.IndexBuffer.Length; i += 3)
                {
                    triangles.Add(new Triangle(lodmodel.IndexBuffer[i], lodmodel.IndexBuffer[i + 1], lodmodel.IndexBuffer[i + 2]));
                }
                var mesh = new WorldMesh(Device, triangles, vertices);
                // Sections
                var sections = new List<ModelPreviewSection>();
                foreach (var section in lodmodel.Sections)
                {
                    if (section.MaterialIndex < Materials.Count)
                    {
                        sections.Add(new ModelPreviewSection(Materials.Keys.ElementAt(section.MaterialIndex), section.BaseIndex, (uint)section.NumTriangles));
                    }
                }
                LODs.Add(new ModelPreviewLOD(mesh, sections));
            }
        }

        ///// <summary>
        ///// Creates a preview of the given <see cref="SkeletalMesh"/>.
        ///// </summary>
        ///// <param name="Device">The Direct3D device to use for buffer creation.</param>
        ///// <param name="m">The mesh to generate a preview for.</param>
        ///// <param name="texcache">The texture cache for loading textures.</param>
        //public ModelPreview(Device Device, LegendaryExplorerCore.Unreal.BinaryConverters.SkeletalMesh m, PreviewTextureCache texcache)
        //{
        //    // STEP 1: MATERIALS
        //    for (int i = 0; i < m.Materials.Count; i++)
        //    {
        //        MaterialInstanceConstant mat = m.MatInsts[i];
        //        if (mat == null && m.Materials[i] < 0)
        //        {
        //            // The material instance is an import!
        //            ImportEntry matImport = m.Export.FileRef.GetImport(m.Materials[i]);
        //            var externalAsset = FindExternalAsset(matImport, texcache.cache.Select(x => x.TextureExport).ToList());
        //            if (externalAsset != null)
        //            {
        //                mat = new MaterialInstanceConstant(externalAsset);
        //            }
        //        }

        //        if (mat != null)
        //        {
        //            ModelPreviewMaterial material;
        //            // TODO: pick what material class best fits based on what properties the 
        //            // MaterialInstanceConstant mat has.
        //            // For now, just use the default material.
        //            material = new TexturedPreviewMaterial(texcache, mat);
        //            AddMaterial(material.Properties["Name"], material);
        //        }
        //    }

        //    // STEP 2: LODS
        //    foreach (LegendaryExplorerCore.Unreal.Classes.SkeletalMesh.LODModelStruct lodmodel in m.LODModels)
        //    {
        //        // Vertices
        //        List<WorldVertex> vertices = new List<WorldVertex>();
        //        if (m.Export.Game == MEGame.ME1)
        //        {
        //            foreach (LegendaryExplorerCore.Unreal.Classes.SkeletalMesh.GPUSkinVertexStruct vertex in lodmodel.VertexBufferGPUSkin.Vertices)
        //            {
        //                vertices.Add(new WorldVertex(new Vector3(-vertex.Position.X, vertex.Position.Z, vertex.Position.Y), Vector3.Zero, new Vector2(vertex.UFullPrecision, vertex.VFullPrecision)));
        //            }
        //        }
        //        else
        //        {
        //            foreach (LegendaryExplorerCore.Unreal.Classes.SkeletalMesh.GPUSkinVertexStruct vertex in lodmodel.VertexBufferGPUSkin.Vertices)
        //            {
        //                // NOTE: note the switched Y and Z coordinates. Unreal seems to think that Z is up.
        //                vertices.Add(new WorldVertex(new Vector3(-vertex.Position.X, vertex.Position.Z, vertex.Position.Y), Vector3.Zero, new Vector2(HalfToFloat(vertex.U), HalfToFloat(vertex.V))));
        //            }
        //        }
        //        // Triangles
        //        List<Triangle> triangles = new List<Triangle>();
        //        for (int i = 0; i < lodmodel.IndexBuffer.Indexes.Count; i += 3)
        //        {
        //            triangles.Add(new Triangle(lodmodel.IndexBuffer.Indexes[i], lodmodel.IndexBuffer.Indexes[i + 1], lodmodel.IndexBuffer.Indexes[i + 2]));
        //        }
        //        WorldMesh mesh = new WorldMesh(Device, triangles, vertices);
        //        // Sections
        //        List<ModelPreviewSection> sections = new List<ModelPreviewSection>();
        //        foreach (LegendaryExplorerCore.Unreal.Classes.SkeletalMesh.SectionStruct section in lodmodel.Sections)
        //        {
        //            if (section.MaterialIndex < Materials.Count)
        //            {
        //                sections.Add(new ModelPreviewSection(Materials.Keys.ElementAt(section.MaterialIndex), (uint)section.BaseIndex, (uint)section.NumTriangles));
        //            }
        //        }
        //        LODs.Add(new ModelPreviewLOD(mesh, sections));
        //    }
        //}

        /// <summary>
        /// Renders the ModelPreview at the specified level of detail.
        /// </summary>
        /// <param name="view">The SceneRenderControl to render the preview into.</param>
        /// <param name="LOD">Which level of detail to render at. Level 0 is traditionally the most detailed.</param>
        /// <param name="transform">The model transformation to be applied to the vertices.</param>
        public void Render(MeshRenderContext view, int LOD, Matrix4x4 transform)
        {
            foreach (ModelPreviewSection section in LODs[LOD].Sections)
            {
                if (Materials.ContainsKey(section.MaterialName))
                {
                    Materials[section.MaterialName].RenderSection(LODs[LOD], section, transform, view);
                }
            }
        }

        /// <summary>
        /// Disposes any outstanding resources.
        /// </summary>
        public void Dispose()
        {
            foreach (ModelPreviewMaterial mat in Materials.Values)
            {
                mat.Dispose();
            }
            Materials.Clear();
            foreach (ModelPreviewLOD lod in LODs)
            {
                lod.Mesh.Dispose();
            }
            LODs.Clear();
        }

        public class PreloadedModelData
        {
            public object meshObject;
            public List<ModelPreviewSection> sections;
            public List<PreloadedTextureData> texturePreviewMaterials;

        }

        public class PreloadedTextureData
        {
            public byte[] decompressedTextureData;
            public ExportEntry MaterialExport { get; internal set; }
            public Texture2DMipInfo Mip { get; internal set; }
        }
    }
}
