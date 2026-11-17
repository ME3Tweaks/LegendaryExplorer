using LegendaryExplorer.Tools.AssetDatabase;
using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

//LEVertex meshes can be rendered with either the game's shaders or LEX's, see MeshRenderContext.UseGameShaders
using VertexType = LegendaryExplorer.Tools.LevelEditor.Scene3D.LEVertex;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// A SplineMeshComponent: its static mesh's LOD 0 deformed along its stored SplineParams, or along the ones its loft actor rebuilt after an edit
/// </summary>
public sealed class SplineMeshComponentProxy(MeshRenderContext context, ExportEntry componentExport, ActorProxy parent, PropertyCollection previewProperties = null,
    ExportEntry bakedLighting = null, EditableActorLighting lighting = null, PreviewAssets previewAssets = null)
    : StaticMeshComponentProxy(context, componentExport, parent, previewProperties, bakedLighting, lighting, previewAssets)
{
    /// <summary>The deformation the mesh is drawn with</summary>
    public SplineMeshDeformation Deformation { get; private set; }
    /// <summary>The stored SplineParams' deformation</summary>
    public SplineMeshDeformation StoredDeformation { get; private set; }
    /// <summary>The deformed static mesh's bounds, which place it along the spline</summary>
    public BoxSphereBounds MeshBounds { get; private set; }

    protected override SplineMeshDeformation ReadDeformation(StaticMesh mesh)
    {
        MeshBounds = mesh.Bounds;
        return Deformation = StoredDeformation = SplineMeshDeformation.Read(Properties, mesh.Bounds);
    }

    /// <summary>Draws the mesh deformed by <paramref name="deformation"/> (its stored one, or a rebuilt one) without reloading it</summary>
    internal void SetDeformation(SplineMeshDeformation deformation)
    {
        if (ReferenceEquals(deformation, Deformation)) return;
        Deformation = deformation;
        Mesh?.Redeform(deformation);
    }

    /// <summary>After a commit wrote <see cref="Deformation"/> to the component, it's the stored one</summary>
    internal void AcceptDeformation() => StoredDeformation = Deformation;
}
