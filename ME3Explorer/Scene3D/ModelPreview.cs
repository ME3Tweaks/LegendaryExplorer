using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Direct3D11;
using ME3Explorer.Packages;


// MODEL RENDERING OVERVIEW:
// Construct a ModelPreview instance with an existing SkeletalMesh or StaticMesh.
// Call ModelPreview.Render(...) every frame. Boom.
namespace ME3Explorer.Scene3D
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
        /// Creates a ModelPreviewMaterial that renders as close to what the given <see cref="Unreal.Classes.MaterialInstanceConstant"/> looks like as possible. 
        /// </summary>
        /// <param name="texcache">The texture cache to request textures from.</param>
        /// <param name="mat">The material that this ModelPreviewMaterial will try to look like.</param>
        public ModelPreviewMaterial(PreviewTextureCache texcache, Unreal.Classes.MaterialInstanceConstant mat)
        {
            Properties.Add("Name", mat.pcc.Exports[mat.index].ObjectName);
            foreach (Unreal.Classes.MaterialInstanceConstant.TextureParam texparam in mat.Textures)
            {
                if (texparam.TexIndex != 0)
                {
                    Textures.Add(texparam.Desc, FindTexture(texcache, mat.pcc.getEntry(texparam.TexIndex).GetFullPath, mat.pcc.FileName));
                }
            }
        }

        /// <summary>
        /// A Dictionary of string properties. Useful because some materials have properties that others don't.
        /// </summary>
        public Dictionary<string, string> Properties = new Dictionary<string, string>();

        public Dictionary<string, PreviewTextureCache.PreviewTextureEntry> Textures = new Dictionary<string, PreviewTextureCache.PreviewTextureEntry>();

        /// <summary>
        /// Renders the given <see cref="ModelPreviewSection"/> of a <see cref="ModelPreviewLOD"/>. 
        /// </summary>
        /// <param name="lod">The LOD to render.</param>
        /// <param name="s">Which faces to render.</param>
        /// <param name="transform">The model transformation to be applied to the vertices.</param>
        /// <param name="view">The SceneRenderControl that the given LOD should be rendered into.</param>
        public abstract void RenderSection(ModelPreviewLOD lod, ModelPreviewSection s, Matrix transform, SceneRenderControl view);

        /// <summary>
        /// Disposes any outstanding resources.
        /// </summary>
        public virtual void Dispose()
        {

        }

        private PreviewTextureCache.PreviewTextureEntry FindTexture(PreviewTextureCache texcache, string FullTextureName, string ImportPCC)
        {
            string importfiledir = System.IO.Path.GetDirectoryName(ImportPCC).ToLower();
            string importfilename = System.IO.Path.GetFileName(ImportPCC).ToLower();
            string pccpath = "";
            int id = 0;

            // First, check the pcc that contains the material
            using (ME3Package pcc = MEPackageHandler.OpenME3Package(ImportPCC))
            {
                foreach (IExportEntry exp in pcc.Exports)
                {
                    if (exp.GetFullPath == FullTextureName && exp.ClassName == "Texture2D")
                    {
                        pccpath = ImportPCC;
                        id = exp.Index;
                        break;
                    }
                }
            }
            // Next, split the filename by underscores
            string[] parts = System.IO.Path.GetFileNameWithoutExtension(importfilename).Split('_');
            if (pccpath == "" && (importfilename.StartsWith("bioa") || importfilename.StartsWith("biod"))) {
                // Maybe go for the one with one less segment? ex. for input BioA_Nor_201CIC.pcc, look in BioA_Nor.pcc
                if (parts.Length == 3)
                {
                    using (ME3Package pcc = MEPackageHandler.OpenME3Package(importfiledir + "\\" + parts[0] + "_" + parts[1] + ".pcc"))
                    {
                        foreach (IExportEntry exp in pcc.Exports)
                        {
                            if (exp.GetFullPath == FullTextureName && exp.ClassName == "Texture2D")
                            {
                                pccpath = importfiledir + "\\" + parts[0] + "_" + parts[1] + ".pcc";
                                id = exp.Index;
                                break;
                            }
                        }
                    }
                }
                // Now go for the BioP one.
                if (pccpath == "" && parts.Length >= 2)
                {
                    using (ME3Package pcc = MEPackageHandler.OpenME3Package(importfiledir + "\\" + "BioP" + "_" + parts[1] + ".pcc"))
                    {
                        foreach (IExportEntry exp in pcc.Exports)
                        {
                            if (exp.GetFullPath == FullTextureName && exp.ClassName == "Texture2D")
                            {
                                pccpath = importfiledir + "\\" + "BioP" + "_" + parts[1] + ".pcc";
                                id = exp.Index;
                                break;
                            }
                        }
                    }
                }
            }

            if (id > 0)
            {
                return texcache.LoadTexture(pccpath, id);
            }
            else
            {
                Console.WriteLine("[TEXLOAD]: Could not find texture \"" + FullTextureName + "\", imported in \"" + ImportPCC + "\".");
                return null;
            }
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
        /// Creates a TexturedPreviewMaterial that renders as close to what the given <see cref="Unreal.Classes.MaterialInstanceConstant"/> looks like as possible. 
        /// </summary>
        /// <param name="texcache">The texture cache to request textures from.</param>
        /// <param name="mat">The material that this ModelPreviewMaterial will try to look like.</param>
        public TexturedPreviewMaterial(PreviewTextureCache texcache, Unreal.Classes.MaterialInstanceConstant mat) : base(texcache, mat)
        {
            foreach (Unreal.Classes.MaterialInstanceConstant.TextureParam texparam in mat.Textures)
            {
                if (texparam.Desc.ToLower().Contains("diff") || texparam.Desc.ToLower().Contains("tex"))
                {
                    // we have found the diffuse texture!
                    DiffuseTextureFullName = texparam.Desc;
                    //Console.WriteLine("Diffuse texture of new material <" + Properties["Name"] + "> is " + DiffuseTextureFullName);
                    return;
                }
            }
            foreach (Unreal.Classes.MaterialInstanceConstant.TextureParam texparam in mat.Textures)
            {
                if (texparam.Desc.ToLower().Contains("detail"))
                {
                    // I guess a detail texture is good enough if we didn't return for a diffuse texture earlier...
                    DiffuseTextureFullName = texparam.Desc;
                    //Console.WriteLine("Diffuse texture of new material <" + Properties["Name"] + "> is " + DiffuseTextureFullName);
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
        public override void RenderSection(ModelPreviewLOD lod, ModelPreviewSection s, Matrix transform, SceneRenderControl view)
        {
            view.DefaultEffect.PrepDraw(view.ImmediateContext);
            view.DefaultEffect.RenderObject(view.ImmediateContext, new SceneRenderControl.WorldConstants(Matrix.Transpose(view.Camera.ProjectionMatrix), Matrix.Transpose(view.Camera.ViewMatrix), Matrix.Transpose(transform)), lod.Mesh, (int) s.StartIndex, (int) s.TriangleCount * 3, Textures.ContainsKey(DiffuseTextureFullName) ? Textures[DiffuseTextureFullName]?.TextureView ?? view.DefaultTextureView : view.DefaultTextureView);
        }
    }

    /// <summary>
    /// Contains all the necessary resources (minus textures, which are cached in a <see cref="PreviewTextureCache"/>) needed to render a static preview of <see cref="Unreal.Classes.SkeletalMesh"/> or <see cref="Unreal.Classes.StaticMesh"/> instances.  
    /// </summary>
    public class ModelPreview : IDisposable
    {
        /// <summary>
        /// Contains the geometry and section information for each level-of-detail in the model.
        /// </summary>
        public List<ModelPreviewLOD> LODs;

        /// <summary>
        /// Stores materials for this preview, stored by material name.
        /// </summary>
        public Dictionary<string, ModelPreviewMaterial> Materials;

        /// <summary>
        /// Creates a preview of the given <see cref="Unreal.Classes.StaticMesh"/>.
        /// </summary>
        /// <param name="Device">The Direct3D device to use for buffer creation.</param>
        /// <param name="m">The mesh to generate a preview for.</param>
        /// <param name="texcache">The texture cache for loading textures.</param>
        public ModelPreview(Device Device, Unreal.Classes.StaticMesh m, PreviewTextureCache texcache)
        {
            // STEP 1: MESH
            List<Triangle> triangles = new List<Triangle>();
            List<WorldVertex> vertices = new List<WorldVertex>();
            // Gather all the vertex data
            // Only one LOD? odd but I guess that's just how it rolls.
            for (int i = 0; i < m.Mesh.Vertices.Points.Count; i++)
            {
                // Note the reversal of the Z and Y coordinates. Unreal seems to think that Z should be up.
                vertices.Add(new Scene3D.WorldVertex(new SharpDX.Vector3(-m.Mesh.Vertices.Points[i].X, m.Mesh.Vertices.Points[i].Z, m.Mesh.Vertices.Points[i].Y), SharpDX.Vector3.Zero, new SharpDX.Vector2(m.Mesh.Edges.UVSet[i].UVs[0].X, m.Mesh.Edges.UVSet[i].UVs[0].Y)));
            }

            // Sometimes there might not be an index buffer.
            // If there is one, use that. 
            // Otherwise, assume that each vertex is uesd exactly once.
            // Note that this is based on the earlier implementstion which didn't take LODs into consideration, which is odd considering that both the hit testing and the skeletalmesh class do.
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
            }

            // STEP 2: MATERIALS
            Materials = new Dictionary<string, ModelPreviewMaterial>();
            foreach (Unreal.Classes.MaterialInstanceConstant mat in m.Mesh.Mat.MatInst)
            {
                ModelPreviewMaterial material;
                // TODO: pick what material class best fits based on what properties the 
                // MaterialInstanceConstant mat has.
                // For now, just use the default material.
                material = new TexturedPreviewMaterial(texcache, mat);
                AddMaterial(material.Properties["Name"], material);
            }

            // STEP 3: SECTIONS
            List<ModelPreviewSection> sections = new List<ModelPreviewSection>();
            foreach (Unreal.Classes.StaticMesh.Section section in m.Mesh.Mat.Lods[0].Sections)
            {
                sections.Add(new ModelPreviewSection(m.pcc.getObjectName(section.Name), (uint)section.FirstIdx1, (uint)section.NumFaces1));
            }
            LODs = new List<ModelPreviewLOD>();
            LODs.Add(new ModelPreviewLOD(new WorldMesh(Device, triangles, vertices), sections));
        }

        /// <summary>
        /// Internal method for decoding UV values.
        /// </summary>
        /// <param name="val">The <see cref="Single"/> encoded as a <see cref="UInt16"/>.</param>
        /// <returns>The decoded <see cref="Single"/>.</returns>
        private float HalfToFloat(ushort val)
        {

            UInt16 u = val;
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
        /// Creates a preview of the given <see cref="Unreal.Classes.SkeletalMesh"/>.
        /// </summary>
        /// <param name="Device">The Direct3D device to use for buffer creation.</param>
        /// <param name="m">The mesh to generate a preview for.</param>
        /// <param name="texcache">The texture cache for loading textures.</param>
        public ModelPreview(Device Device, Unreal.Classes.SkeletalMesh m, PreviewTextureCache texcache)
        {
            // STEP 1: MATERIALS
            Materials = new Dictionary<string, ModelPreviewMaterial>();
            foreach (Unreal.Classes.MaterialInstanceConstant mat in m.MatInsts)
            {
                ModelPreviewMaterial material;
                // TODO: pick what material class best fits based on what properties the 
                // MaterialInstanceConstant mat has.
                // For now, just use the default material.
                material = new TexturedPreviewMaterial(texcache, mat);
                AddMaterial(material.Properties["Name"], material);
            }

            // STEP 2: LODS
            LODs = new List<ModelPreviewLOD>();
            foreach (Unreal.Classes.SkeletalMesh.LODModelStruct lodmodel in m.LODModels)
            {
                // Vertices
                List<WorldVertex> vertices = new List<WorldVertex>();
                foreach (Unreal.Classes.SkeletalMesh.GPUSkinVertexStruct vertex in lodmodel.VertexBufferGPUSkin.Vertices)
                {
                    // NOTE: note the switched Y and Z coordinates. Unreal seems to think that Z is up.
                    vertices.Add(new WorldVertex(new Vector3(-vertex.Position.X, vertex.Position.Z, vertex.Position.Y), Vector3.Zero, new Vector2(HalfToFloat(vertex.U), HalfToFloat(vertex.V))));
                }
                // Triangles
                List<Triangle> triangles = new List<Triangle>();
                for (int i = 0; i < lodmodel.IndexBuffer.Indexes.Count; i += 3)
                {
                    triangles.Add(new Triangle(lodmodel.IndexBuffer.Indexes[i], lodmodel.IndexBuffer.Indexes[i + 1], lodmodel.IndexBuffer.Indexes[i + 2]));
                }
                WorldMesh mesh = new WorldMesh(Device, triangles, vertices);
                // Sections
                List<ModelPreviewSection> sections = new List<ModelPreviewSection>();
                foreach (Unreal.Classes.SkeletalMesh.SectionStruct section in lodmodel.Sections)
                {
                    if (section.MaterialIndex < Materials.Count)
                    {
                        sections.Add(new ModelPreviewSection(Materials.Keys.ElementAt(section.MaterialIndex), (uint)section.BaseIndex, (uint)section.NumTriangles));
                    }
                }
                LODs.Add(new ModelPreviewLOD(mesh, sections));
            }
        }

        /// <summary>
        /// Renders the ModelPreview at the specified level of detail.
        /// </summary>
        /// <param name="view">The SceneRenderControl to render the preview into.</param>
        /// <param name="LOD">Which level of detail to render at. Level 0 is traditionally the most detailed.</param>
        /// <param name="transform">The model transformation to be applied to the vertices.</param>
        public void Render(SceneRenderControl view, int LOD, Matrix transform)
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
    }
}
