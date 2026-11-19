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
/// A light that's rendered on a primitive in its own pass, and how its shadows were precomputed. (UE3's FLightInteraction, minus the light-mapped and irrelevant kinds)
/// </summary>
/// <param name="ShadowMap">For <see cref="StaticShadowingType.ShadowTexture"/> and <see cref="StaticShadowingType.DistanceFieldShadowTexture"/></param>
/// <param name="ShadowVertexBuffer">For <see cref="StaticShadowingType.ShadowVertexBuffer"/>. Bound at <see cref="MeshStaticLighting.ShadowVertexStreamSlot"/></param>
public readonly record struct LightInteraction(SceneLight Light, StaticShadowingType Shadowing, MeshStaticLighting.ShadowMapTexture ShadowMap, Buffer ShadowVertexBuffer);
