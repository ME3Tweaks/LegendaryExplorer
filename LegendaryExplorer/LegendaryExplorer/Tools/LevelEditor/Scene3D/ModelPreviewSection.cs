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
    public bool CastsShadow;

    /// <summary>
    /// Constructs a new MaterialPreviewSection.
    /// </summary>
    /// <param name="materialname">The name of the material to be applied to the triangles in this section.</param>
    /// <param name="startindex">The first index into the LOD mesh index buffer that this section describes.</param>
    /// <param name="trianglecount">How many triangles, starting from the vertex at <see cref="StartIndex"/>, that this section describes.</param>
    /// <param name="castsShadow">The static mesh section's bEnableShadowCasting flag.</param>
    public ModelPreviewSection(string materialname, uint startindex, uint trianglecount, bool castsShadow = true)
    {
        MaterialName = materialname;
        StartIndex = startindex;
        TriangleCount = trianglecount;
        CastsShadow = castsShadow;
    }
}
