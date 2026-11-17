using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using LegendaryExplorerCore.Unreal.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

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
