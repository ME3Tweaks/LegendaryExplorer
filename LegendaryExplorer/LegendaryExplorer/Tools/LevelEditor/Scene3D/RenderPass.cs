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

public enum RenderPass
{
    //material types
    Base,
    Hair,
    //The level's lights, each added onto the opaque geometry they light in its own pass. Must be rendered after the opaque passes
    Lighting,
    // Cooked surface overlays, after opaque lighting and before scene translucency.
    Decals,
    //materials that blend with what's behind them. Must be rendered after the opaque passes, since they don't write depth
    Translucent,

    //special types
    Collision,

    //override, most always be last
    ANY
}
