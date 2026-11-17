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
using System.Threading;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// Hemispherical ambient light from the level's sky lights, in linear color: <paramref name="Upper"/> from above (world +Z), <paramref name="Lower"/> from below
/// </summary>
public readonly record struct SkyLighting(Vector3 Upper, Vector3 Lower)
{
    public bool IsBlack => Upper == Vector3.Zero && Lower == Vector3.Zero;
}
