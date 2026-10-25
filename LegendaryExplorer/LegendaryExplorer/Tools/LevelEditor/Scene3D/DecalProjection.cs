using System;
using System.Numerics;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>The cooked projector's world-space frame. Receiver meshes are converted to world space once.</summary>
internal sealed class DecalProjection
{
    public Vector3 Location, Tangent, Binormal, Normal;
    public Vector2 Offset, BlendInterval;
    public Matrix4x4 Matrix;
    public float Width, Height, NearPlane, FarPlane, DepthBias, SlopeBias;
    public int SortOrder;

    public static DecalProjection Read(PropertyCollection props)
    {
        float F(string name, float fallback) => props.GetProp<FloatProperty>(name)?.Value ?? fallback;
        Vector3 V(string name) => props.GetProp<StructProperty>(name) is { } p
            ? CommonStructs.GetVector3(p) : throw new NotSupportedException($"Missing cooked {name}.");
        var p = new DecalProjection
        {
            Location = V("HitLocation"), Tangent = V("HitTangent"), Binormal = V("HitBinormal"), Normal = V("HitNormal"),
            Width = F("Width", 200), Height = F("Height", 200),
            NearPlane = F("NearPlane", 0), FarPlane = F("FarPlane", 300),
            Offset = new(F("OffsetX", 0), F("OffsetY", 0)),
            DepthBias = F("DepthBias", -0.00006f), SlopeBias = F("SlopeScaleDepthBias", 0),
            SortOrder = props.GetProp<IntProperty>("SortOrder")?.Value ?? 0
        };
        if (!Finite(p.Location) || !Finite(p.Tangent) || !Finite(p.Binormal) || !Finite(p.Normal)
            || p.Tangent.LengthSquared() < 0.5f || p.Binormal.LengthSquared() < 0.5f || p.Normal.LengthSquared() < 0.5f
            || !float.IsFinite(p.Width) || !float.IsFinite(p.Height) || p.Width <= 0 || p.Height <= 0)
            throw new NotSupportedException("Invalid cooked decal frame or dimensions.");
        float x = F("TileX", 1) / p.Width, y = F("TileY", 1) / p.Height;
        // UDecalComponent builds columns from HitTangent * TileX/Width, HitBinormal * TileY/Height, HitNormal.
        // FLocalDecalVertexFactory: UV = .5 + Offset - mul(Position - DecalLocation, DecalMatrix).xy.
        p.Matrix = new(p.Tangent.X*x, p.Binormal.X*y, p.Normal.X, 0,
            p.Tangent.Y*x, p.Binormal.Y*y, p.Normal.Y, 0,
            p.Tangent.Z*x, p.Binormal.Z*y, p.Normal.Z, 0, 0, 0, 0, 1);
        Vector2 range = props.GetProp<StructProperty>("BlendRange") is { } blend ? CommonStructs.GetVector2(blend) : new(89.5f, 180);
        float c0 = MathF.Cos(range.X * MathF.PI / 180), c1 = MathF.Cos(range.Y * MathF.PI / 180);
        float min = Math.Min(c0, c1), max = Math.Max(c0, c1);
        if (max - min < 0.0001f) min -= 0.001f;
        p.BlendInterval = new(Math.Clamp(min, -1, 1), Math.Clamp(max, -1, 1));
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(p.Offset.X) || !float.IsFinite(p.Offset.Y)
            || !float.IsFinite(p.DepthBias) || !float.IsFinite(p.SlopeBias) || !float.IsFinite(p.FarPlane)
            || !float.IsFinite(p.NearPlane) || !float.IsFinite(min) || !float.IsFinite(max))
            throw new NotSupportedException("Non-finite decal projection parameters.");
        return p;
    }

    internal Vector2 ProjectUV(Vector3 worldPosition)
    {
        Vector3 projected = Vector3.TransformNormal(worldPosition - Location, Matrix);
        return new Vector2(0.5f) + Offset - new Vector2(projected.X, projected.Y);
    }

    internal static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
