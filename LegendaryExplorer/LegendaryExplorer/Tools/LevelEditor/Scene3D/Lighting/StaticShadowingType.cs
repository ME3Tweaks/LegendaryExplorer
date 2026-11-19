using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
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
