using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// A spline point's own spline values: SplineActorTangent (actor space), and for a SplineLoftActor Roll (degrees), ScaleX/ScaleY, Offset, WorldXDir
/// and bSmoothInterpRollAndScale. Unset properties take the class defaults, so a plain SplineActor never writes the loft values
/// </summary>
public readonly record struct SplineLoftSettings(Vector3 Tangent, float Roll, Vector2 Scale, Vector2 Offset, Vector3 WorldXDir, bool SmoothInterpRollAndScale)
{
    /// <summary>Default__SplineActor and Default__SplineLoftActor (LE3 Engine.pcc, as in UE3)</summary>
    public static readonly SplineLoftSettings Defaults = new(new Vector3(300, 0, 0), 0, Vector2.One, Vector2.Zero, Vector3.UnitX, true);

    /// <param name="defaults">The class defaults, used for what <paramref name="props"/> doesn't set</param>
    public static SplineLoftSettings Read(PropertyCollection props, SplineLoftSettings defaults)
    {
        Vector3 V3(string name, Vector3 fallback) => props.GetProp<StructProperty>(name) is { } s ? CommonStructs.GetVector3(s) : fallback;
        float F(string name, float fallback) => props.GetProp<FloatProperty>(name)?.Value ?? fallback;
        return new SplineLoftSettings(V3("SplineActorTangent", defaults.Tangent), F("Roll", defaults.Roll),
            new Vector2(F("ScaleX", defaults.Scale.X), F("ScaleY", defaults.Scale.Y)),
            props.GetProp<StructProperty>("Offset") is { } offset ? CommonStructs.GetVector2(offset) : defaults.Offset,
            V3("WorldXDir", defaults.WorldXDir), props.GetProp<BoolProperty>("bSmoothInterpRollAndScale")?.Value ?? defaults.SmoothInterpRollAndScale);
    }

    /// <summary>Writes each value the properties already set or that differs from its default</summary>
    public void Write(PropertyCollection props, SplineLoftSettings defaults)
    {
        void Set(Property prop, bool differs)
        {
            if (differs || props.ContainsNamedProp(prop.Name)) props.AddOrReplaceProp(prop);
        }
        Set(CommonStructs.Vector3Prop(Tangent, "SplineActorTangent"), Tangent != defaults.Tangent);
        Set(new FloatProperty(Roll, "Roll"), Roll != defaults.Roll);
        Set(new FloatProperty(Scale.X, "ScaleX"), Scale.X != defaults.Scale.X);
        Set(new FloatProperty(Scale.Y, "ScaleY"), Scale.Y != defaults.Scale.Y);
        Set(CommonStructs.Vector2Prop(Offset, "Offset"), Offset != defaults.Offset);
        Set(CommonStructs.Vector3Prop(WorldXDir, "WorldXDir"), WorldXDir != defaults.WorldXDir);
        Set(new BoolProperty(SmoothInterpRollAndScale, "bSmoothInterpRollAndScale"), SmoothInterpRollAndScale != defaults.SmoothInterpRollAndScale);
    }
}

/// <summary>One end of a spline connection: a SplineActor's Location, LocalToWorld and spline values</summary>
public readonly record struct SplineLoftNode(Vector3 Location, Matrix4x4 LocalToWorld, SplineLoftSettings Settings)
{
    /// <summary>ASplineActor::GetWorldSpaceTangent</summary>
    public Vector3 WorldTangent => Vector3.TransformNormal(Settings.Tangent, LocalToWorld);
}

/// <summary>How UE3's editor rebuilds a SplineLoftActor's connections when it or a neighbour changes</summary>
public static class SplineLoftGeometry
{
    //FGlobalMath's table: NUM_ANGLES sines over a full turn, looked up by rotation units >> ANGLE_SHIFT
    private static readonly float[] TrigTable = CreateTrigTable();
    private static float[] CreateTrigTable()
    {
        var table = new float[16384];
        for (int i = 0; i < table.Length; i++) table[i] = MathF.Sin(i * 2f * MathF.PI / table.Length);
        return table;
    }
    private static float SinTab(int angle) => TrigTable[(angle >> 2) & 16383];
    private static float CosTab(int angle) => TrigTable[((angle + 16384) >> 2) & 16383];

    /// <summary>
    /// AActor::LocalToWorld as UE3 computes it: the rotation's sines and cosines come from FGlobalMath's lookup table (in steps of 4 rotation units),
    /// so a rotated actor's matrix differs slightly from an exact one. The editor built the cooked SplineParams with it
    /// </summary>
    public static Matrix4x4 ActorLocalToWorld(Vector3 location, Rotator rotation, Vector3 scale, Vector3 prePivot)
    {
        float sr = SinTab(rotation.Roll), sp = SinTab(rotation.Pitch), sy = SinTab(rotation.Yaw);
        float cr = CosTab(rotation.Roll), cp = CosTab(rotation.Pitch), cy = CosTab(rotation.Yaw);
        //FRotationMatrix's rows
        var rotationMatrix = new Matrix4x4(
            cp * cy, cp * sy, sp, 0,
            sr * sp * cy - cr * sy, sr * sp * sy + cr * cy, -sr * cp, 0,
            -(cr * sp * cy + sr * sy), cy * sr - cr * sp * sy, cr * cp, 0,
            0, 0, 0, 1);
        return Matrix4x4.CreateTranslation(-prePivot) * Matrix4x4.CreateScale(scale) * rotationMatrix * Matrix4x4.CreateTranslation(location);
    }

    /// <summary>
    /// ASplineLoftActor::UpdateSplineParams: the SplineMeshComponent of the connection from <paramref name="start"/> to <paramref name="end"/>, in the
    /// start actor's space. Roll, scale and offset come from each end, SplineXDir and the interpolation from the start
    /// </summary>
    /// <param name="meshBounds">The deformed static mesh's bounds (FSplineMeshSceneProxy's MeshMinZ and MeshRangeZ)</param>
    public static SplineMeshDeformation Segment(SplineLoftNode start, SplineLoftNode end, BoxSphereBounds meshBounds)
    {
        if (!Matrix4x4.Invert(start.LocalToWorld, out Matrix4x4 worldToMesh))
            throw new InvalidOperationException("The spline actor's transform can't be inverted.");
        const float degreesToRadians = MathF.PI / 180f;
        return new SplineMeshDeformation
        {
            StartPos = Vector3.Transform(start.Location, worldToMesh),
            StartTangent = Vector3.TransformNormal(start.WorldTangent, worldToMesh),
            StartRoll = start.Settings.Roll * degreesToRadians,
            StartOffset = start.Settings.Offset,
            StartScale = start.Settings.Scale,
            EndPos = Vector3.Transform(end.Location, worldToMesh),
            EndTangent = Vector3.TransformNormal(end.WorldTangent, worldToMesh),
            EndRoll = end.Settings.Roll * degreesToRadians,
            EndScale = end.Settings.Scale,
            EndOffset = end.Settings.Offset,
            XDir = Vector3.TransformNormal(start.Settings.WorldXDir, worldToMesh),
            SmoothInterpRollScale = start.Settings.SmoothInterpRollAndScale,
            MeshMinZ = meshBounds.Origin.Z - meshBounds.BoxExtent.Z,
            MeshRangeZ = 2 * meshBounds.BoxExtent.Z,
        };
    }

    /// <summary>ASplineActor::UpdateSplineComponents: the connection's world-space curve, from the start actor to the end, with user tangents</summary>
    public static InterpCurve<Vector3> SplineInfo(SplineLoftNode start, SplineLoftNode end, MEGame game)
    {
        var curve = new InterpCurve<Vector3>
        {
            InterpMethod = game is MEGame.ME1 or MEGame.ME2 ? EInterpMethodType.IMT_UseFixedTangentEval : EInterpMethodType.IMT_UseFixedTangentEvalAndNewAutoTangents
        };
        curve.AddPoint(0, start.Location, start.WorldTangent, start.WorldTangent, EInterpCurveMode.CIM_CurveUser);
        curve.AddPoint(1, end.Location, end.WorldTangent, end.WorldTangent, EInterpCurveMode.CIM_CurveUser);
        return curve;
    }

    /// <summary>
    /// USplineComponent::UpdateSplineReparamTable (distance along the curve to its parameter, at 10 even parameter steps) and UpdateSplineCurviness
    /// (the straight distance between the ends over the length). The parameter is stepped in float, as UE3 does
    /// </summary>
    public static (InterpCurve<float> ReparamTable, float Curviness) Reparameterize(InterpCurve<Vector3> splineInfo)
    {
        var table = new InterpCurve<float>();
        if (splineInfo.Points.Count < 2) return (table, 0);
        const int numSteps = 10;
        float param = splineInfo.Points[0].InVal;
        float maxInput = splineInfo.Points[^1].InVal;
        float interval = (maxInput - param) / (numSteps - 1);
        Vector3 oldPos = splineInfo.Eval(param, Vector3.Zero);
        float totalDist = 0;
        table.AddPoint(totalDist, param);
        param += interval;
        for (int i = 1; i < numSteps; i++)
        {
            Vector3 newPos = splineInfo.Eval(param, Vector3.Zero);
            totalDist += (newPos - oldPos).Length();
            oldPos = newPos;
            table.AddPoint(totalDist, param);
            param += interval;
        }
        Vector3 start = splineInfo.Eval(table.Eval(0, 0), Vector3.Zero);
        Vector3 end = splineInfo.Eval(table.Eval(totalDist, 0), Vector3.Zero);
        return (table, (start - end).Length() / totalDist);
    }

    /// <summary>Writes a connection's curve, reparameterization table and curviness to its SplineComponent's properties</summary>
    public static void WriteSplineComponent(PropertyCollection props, SplineLoftNode start, SplineLoftNode end, MEGame game)
    {
        InterpCurve<Vector3> info = SplineInfo(start, end, game);
        (InterpCurve<float> table, float curviness) = Reparameterize(info);
        props.AddOrReplaceProp(info.ToStructProperty(game, "SplineInfo"));
        props.AddOrReplaceProp(table.ToStructProperty(game, "SplineReparamTable"));
        props.AddOrReplaceProp(new FloatProperty(curviness, "SplineCurviness"));
    }
}
