using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using LegendaryExplorerCore.Coalesced.Config;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct3D11;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// Cinematic Bio's rim light (RVAs LE2 0x18d7c0, LE3 0x1a3c50): the horizontal direction from the viewer to the environment, turned by RimLightPitch (negated)
/// about its horizontal perpendicular and then by RimLightYaw about Z, both as UE3's table-based RotateAngleAxis does. The game takes the viewer
/// from the player's camera or a camera actor; the preview takes the viewport camera. Created as the other Bio lights are
/// </summary>
public sealed class BioRimLight
{
    private readonly DynamicLightEnvironment Environment;
    private readonly BoxSphereBounds Bounds;
    private readonly Vector4 Color;
    private readonly bool CastsShadow;
    private readonly Lock CacheLock = new();
    private Vector3 CachedViewLocation;
    private SceneLight CachedLight;
    private bool HasCachedLight;

    internal BioRimLight(DynamicLightEnvironment environment, BoxSphereBounds bounds, Vector4 color, bool castsShadow)
    {
        Environment = environment;
        Bounds = bounds;
        Color = color;
        CastsShadow = castsShadow;
    }

    /// <summary>The light for a view location. Null if the viewer is (nearly) straight above or below the environment</summary>
    public SceneLight GetLight(Vector3 viewLocation)
    {
        lock (CacheLock)
        {
            if (!HasCachedLight || viewLocation != CachedViewLocation)
            {
                CachedLight = GetDirection(Bounds.Origin, viewLocation, Environment.Settings.RimLightYaw, Environment.Settings.RimLightPitch) is { } direction
                    ? Environment.CreateBioLight(direction, new Vector3(Color.X, Color.Y, Color.Z), Color.W, CastsShadow, Bounds)
                    : null;
                CachedViewLocation = viewLocation;
                HasCachedLight = true;
            }
            return CachedLight;
        }
    }

    /// <summary>The direction towards the rim light</summary>
    internal static Vector3? GetDirection(Vector3 origin, Vector3 viewLocation, float yaw, float pitch)
    {
        float x = origin.X - viewLocation.X, y = origin.Y - viewLocation.Y;
        float lengthSquared = x * x + y * y;
        if (lengthSquared != 1)
        {
            float scale = lengthSquared >= 1e-8f ? 1 / MathF.Sqrt(lengthSquared) : 0;
            x *= scale;
            y *= scale;
        }
        if (!(MathF.Max(MathF.Abs(x), MathF.Abs(y)) > 1e-4f))
        {
            return null;
        }
        //degrees to Unreal rotation units
        const double toUnreal = 65536.0 / 360;
        Vector3 direction = RotateAngleAxis(new Vector3(x, y, 0), (int)(pitch * -toUnreal), new Vector3(-y, x, 0));
        return RotateAngleAxis(direction, (int)(yaw * toUnreal), Vector3.UnitZ);
    }

    //FGlobalMath's 16384-entry sine table
    private static readonly float[] SinTable = Enumerable.Range(0, 16384).Select(i => (float)Math.Sin(i * 2 * Math.PI / 16384)).ToArray();

    /// <summary>FVector::RotateAngleAxis</summary>
    internal static Vector3 RotateAngleAxis(Vector3 v, int angle, Vector3 axis)
    {
        float s = SinTable[(angle >> 2) & 0x3fff];
        float c = SinTable[((angle + 16384) >> 2) & 0x3fff];
        float xx = axis.X * axis.X, yy = axis.Y * axis.Y, zz = axis.Z * axis.Z;
        float xy = axis.X * axis.Y, yz = axis.Y * axis.Z, zx = axis.Z * axis.X;
        float xs = axis.X * s, ys = axis.Y * s, zs = axis.Z * s;
        float omc = 1 - c;
        return new Vector3(
            (omc * xx + c) * v.X + (omc * xy - zs) * v.Y + (omc * zx + ys) * v.Z,
            (omc * xy + zs) * v.X + (omc * yy + c) * v.Y + (omc * yz - xs) * v.Z,
            (omc * zx - ys) * v.X + (omc * yz + xs) * v.Y + (omc * zz + c) * v.Z);
    }
}
