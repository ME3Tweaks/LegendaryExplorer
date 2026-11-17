using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// A SplineMeshComponent's deformation, evaluated on the CPU exactly as LE3's FSplineMeshVertexFactory vertex shaders evaluate it
/// (CalcSliceTransform/CalcSliceRot in LocalVertexFactory.usf): the mesh's Z runs along a Hermite segment in component space, X and Y span a
/// frame built from SplineXDir, rolled, scaled and offset by values interpolated linearly, or with smoothstep if bSmoothInterpRollScale.
/// </summary>
public sealed class SplineMeshDeformation
{
    public Vector3 StartPos, StartTangent, EndPos, EndTangent, XDir;
    public Vector2 StartScale, EndScale, StartOffset, EndOffset;
    public float StartRoll, EndRoll;
    public bool SmoothInterpRollScale;
    /// <summary>FSplineMeshSceneProxy's MeshMinZ and MeshRangeZ, from the static mesh's bounds</summary>
    public float MeshMinZ, MeshRangeZ;

    /// <summary>Reads SplineParams, SplineXDir (class default (1,0,0)) and bSmoothInterpRollScale. SplineMeshParams has no struct defaults</summary>
    public static SplineMeshDeformation Read(PropertyCollection componentProps, BoxSphereBounds meshBounds)
    {
        var p = componentProps.GetProp<StructProperty>("SplineParams")?.Properties ?? [];
        Vector3 V3(string name) => p.GetProp<StructProperty>(name) is { } s ? CommonStructs.GetVector3(s) : Vector3.Zero;
        Vector2 V2(string name) => p.GetProp<StructProperty>(name) is { } s ? CommonStructs.GetVector2(s) : Vector2.Zero;
        float F(string name) => p.GetProp<FloatProperty>(name)?.Value ?? 0;
        return new SplineMeshDeformation
        {
            StartPos = V3("StartPos"), StartTangent = V3("StartTangent"), StartScale = V2("StartScale"), StartRoll = F("StartRoll"), StartOffset = V2("StartOffset"),
            EndPos = V3("EndPos"), EndTangent = V3("EndTangent"), EndScale = V2("EndScale"), EndRoll = F("EndRoll"), EndOffset = V2("EndOffset"),
            XDir = componentProps.GetProp<StructProperty>("SplineXDir") is { } x ? CommonStructs.GetVector3(x) : Vector3.UnitX,
            SmoothInterpRollScale = componentProps.GetProp<BoolProperty>("bSmoothInterpRollScale")?.Value ?? false,
            MeshMinZ = meshBounds.Origin.Z - meshBounds.BoxExtent.Z,
            MeshRangeZ = 2 * meshBounds.BoxExtent.Z,
        };
    }

    /// <summary>
    /// Writes SplineParams (fields in SplineMeshParams' order, leaving out zeros as the cooked components do), and SplineXDir and bSmoothInterpRollScale
    /// where they're already set or differ from the class defaults ((1,0,0) and false)
    /// </summary>
    public void Write(PropertyCollection componentProps)
    {
        var p = new PropertyCollection();
        void V3(string name, Vector3 v) { if (v != Vector3.Zero) p.Add(CommonStructs.Vector3Prop(v, name)); }
        void V2(string name, Vector2 v) { if (v != Vector2.Zero) p.Add(CommonStructs.Vector2Prop(v, name)); }
        void F(string name, float f) { if (f != 0) p.Add(new FloatProperty(f, name)); }
        V3("StartPos", StartPos); V3("StartTangent", StartTangent); V2("StartScale", StartScale); F("StartRoll", StartRoll); V2("StartOffset", StartOffset);
        V3("EndPos", EndPos); V3("EndTangent", EndTangent); V2("EndScale", EndScale); F("EndRoll", EndRoll); V2("EndOffset", EndOffset);
        componentProps.AddOrReplaceProp(new StructProperty("SplineMeshParams", p, "SplineParams"));
        if (XDir != Vector3.UnitX || componentProps.ContainsNamedProp("SplineXDir"))
            componentProps.AddOrReplaceProp(CommonStructs.Vector3Prop(XDir, "SplineXDir"));
        if (SmoothInterpRollScale || componentProps.ContainsNamedProp("bSmoothInterpRollScale"))
            componentProps.AddOrReplaceProp(new BoolProperty(SmoothInterpRollScale, "bSmoothInterpRollScale"));
    }

    /// <summary>The largest difference between this deformation's stored values and <paramref name="other"/>'s, relative to their magnitude (at least 1)</summary>
    public float Difference(SplineMeshDeformation other)
    {
        float d = 0;
        void V(Vector3 a, Vector3 b) => d = MathF.Max(d, (a - b).Length() / MathF.Max(1, MathF.Max(a.Length(), b.Length())));
        void V2(Vector2 a, Vector2 b) => V(new Vector3(a, 0), new Vector3(b, 0));
        V(StartPos, other.StartPos); V(StartTangent, other.StartTangent); V2(StartScale, other.StartScale); V2(StartOffset, other.StartOffset);
        V(EndPos, other.EndPos); V(EndTangent, other.EndTangent); V2(EndScale, other.EndScale); V2(EndOffset, other.EndOffset);
        V(XDir, other.XDir);
        d = MathF.Max(d, MathF.Abs(StartRoll - other.StartRoll));
        d = MathF.Max(d, MathF.Abs(EndRoll - other.EndRoll));
        return SmoothInterpRollScale == other.SmoothInterpRollScale ? d : float.PositiveInfinity;
    }

    public Vector3 EvalPosition(float a)
    {
        float a2 = a * a, a3 = a2 * a;
        return (2 * a3 - 3 * a2 + 1) * StartPos + (a3 - 2 * a2 + a) * StartTangent + (a3 - a2) * EndTangent + (-2 * a3 + 3 * a2) * EndPos;
    }

    public Vector3 EvalDirection(float a)
    {
        Vector3 c = 6 * StartPos + 3 * StartTangent + 3 * EndTangent - 6 * EndPos;
        Vector3 d = -6 * StartPos - 4 * StartTangent - 2 * EndTangent + 6 * EndPos;
        return Normalize(c * (a * a) + d * a + StartTangent);
    }

    /// <summary>The slice frame at a mesh-space Z: X and Y (rolled, unscaled), the spline direction, the offset spline point and the scale</summary>
    public (Vector3 X, Vector3 Y, Vector3 Direction, Vector3 Position, Vector2 Scale) Slice(float z)
    {
        float alpha = (z - MeshMinZ) / MeshRangeZ;
        float clamped = Math.Clamp(alpha, 0, 1);
        float hermiteAlpha = SmoothInterpRollScale ? clamped * clamped * (3 - 2 * clamped) : alpha;
        Vector3 position = EvalPosition(alpha);
        Vector3 direction = EvalDirection(alpha);
        Vector3 baseY = Normalize(Vector3.Cross(direction, XDir));
        Vector3 baseX = Normalize(Vector3.Cross(baseY, direction));
        Vector2 offset = Vector2.Lerp(StartOffset, EndOffset, hermiteAlpha);
        position += offset.X * baseX + offset.Y * baseY;
        float roll = StartRoll + (EndRoll - StartRoll) * hermiteAlpha;
        float cos = MathF.Cos(roll), sin = MathF.Sin(roll);
        Vector3 x = cos * baseX - sin * baseY;
        Vector3 y = cos * baseY + sin * baseX;
        return (x, y, direction, position, Vector2.Lerp(StartScale, EndScale, hermiteAlpha));
    }

    /// <summary>
    /// A vertex in component space. The position's Z is replaced by the slice's place on the spline; tangent and normal are rotated by the
    /// unscaled slice frame, as the shader does (it doesn't use the inverse transpose)
    /// </summary>
    public (Vector3 Position, Vector3 TangentX, Vector4 TangentZ) Deform(Vector3 position, Vector3 tangentX, Vector4 tangentZ)
    {
        var (x, y, direction, origin, scale) = Slice(position.Z);
        Vector3 deformed = position.X * scale.X * x + position.Y * scale.Y * y + origin;
        Vector3 Rotate(Vector3 v) => v.X * x + v.Y * y + v.Z * direction;
        Vector3 normal = Rotate(new Vector3(tangentZ.X, tangentZ.Y, tangentZ.Z));
        return (deformed, Rotate(tangentX), new Vector4(normal, tangentZ.W));
    }

    // HLSL normalize of a zero vector is NaN; keep the vertex finite instead
    private static Vector3 Normalize(Vector3 v) => v.LengthSquared() > 1e-20f ? Vector3.Normalize(v) : Vector3.Zero;
}
