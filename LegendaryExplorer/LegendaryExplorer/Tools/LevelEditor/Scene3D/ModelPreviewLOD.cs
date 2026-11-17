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
using System.Linq;
using System.Numerics;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

// MODEL RENDERING OVERVIEW:
// Construct a ModelPreview instance with an existing SkeletalMesh or StaticMesh.
// Call ModelPreview.Render(...) every frame. Boom.
namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

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
    /// The light environment that lights this LOD, if it's lit by one (and they're supported for the game). Only used by the game's shaders
    /// </summary>
    public DynamicLightEnvironment LightEnvironment;

    /// <summary>This LOD's bounds, channels and acceptance in <see cref="LightEnvironment"/>, for directly drawn lights</summary>
    public LightEnvironmentPrimitive LightEnvironmentPrimitive;

    /// <summary>
    /// The material of each of <see cref="Sections"/> that has one. Null until the LOD is first drawn, by when its preview is complete
    /// </summary>
    internal ModelPreviewMaterial<Vertex>[] SectionMaterials;

    /// <summary>A bit for each <see cref="RenderPass"/> that draws something of this LOD, valid once <see cref="SectionMaterials"/> is set</summary>
    internal int DrawnPasses;

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
