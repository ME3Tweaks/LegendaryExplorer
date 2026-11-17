using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Collections;
using System;
using System.Linq;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

public enum SplineTangentHandle
{
    None,
    LeaveDirection,
    LeaveLength,
    ArriveDirection,
    ArriveLength,
}

public class SplineTangentHitProxy(SplineTangentHandle handle) : IHitProxy
{
    public SplineTangentHandle Handle { get; } = handle;
    public int HitID { get; set; }
    public int HitPriority => IHitProxy.UIPriority;
}

/// <summary>How the tangent gizmo turns mouse rays into a tangent's direction or length</summary>
public static class SplineTangentDrag
{
    /// <summary>
    /// The unit direction from <paramref name="center"/> to where the ray meets the sphere around it, on its near side or its far side. A ray that misses
    /// the sphere gives the direction of the closest point of its outline, so a drag past the edge stays on the edge
    /// </summary>
    public static Vector3 SphereDirection(Vector3 center, float radius, Vector3 rayOrigin, Vector3 rayDirection, bool nearSide)
    {
        Vector3 toOrigin = rayOrigin - center;
        float b = Vector3.Dot(toOrigin, rayDirection);
        float discriminant = b * b - (toOrigin.LengthSquared() - radius * radius);
        float t = -b;
        if (discriminant > 0)
        {
            float root = MathF.Sqrt(discriminant);
            //from inside the sphere, the near side is behind the ray
            t = nearSide && root < -b ? -b - root : -b + root;
        }
        Vector3 offset = rayOrigin + rayDirection * t - center;
        return offset.LengthSquared() > 0 ? Vector3.Normalize(offset) : Vector3.Zero;
    }

    /// <summary>Whether a point on a sphere is on the side of it facing a viewer looking along <paramref name="rayDirection"/></summary>
    public static bool FacesViewer(Vector3 center, Vector3 point, Vector3 rayDirection) => Vector3.Dot(point - center, rayDirection) <= 0;

    /// <summary>
    /// How far along an axis (a unit direction from <paramref name="axisOrigin"/>) its closest point to the ray is, or null when the ray runs along the axis
    /// </summary>
    public static float? AxisDistance(Vector3 axisOrigin, Vector3 axisDirection, Vector3 rayOrigin, Vector3 rayDirection)
    {
        Vector3 w = axisOrigin - rayOrigin;
        float b = Vector3.Dot(axisDirection, rayDirection);
        float denominator = 1 - b * b;
        if (denominator < 1e-4f) return null;
        return (b * Vector3.Dot(rayDirection, w) - Vector3.Dot(axisDirection, w)) / denominator;
    }

    /// <summary>The rotation turning one unit vector into another</summary>
    public static Quaternion Rotation(Vector3 from, Vector3 to)
    {
        Vector3 axis = Vector3.Cross(from, to);
        if (axis.LengthSquared() < 1e-12f) return Quaternion.Identity;
        return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(Math.Clamp(Vector3.Dot(from, to), -1, 1)));
    }
}

/// <summary>
/// The selected spline point's tangent, with two grabbers on each side at the Bézier control point of the curves leaving and arriving at it (the point
/// ± a third of the tangent). The sphere only turns the tangent: it moves over a sphere around the point, staying on the half it was grabbed on (the half
/// facing the viewer, unless the tangent points away), so each mouse position means one direction. The arrow beyond it only changes the length, sliding
/// along the tangent. Moving and rotating the point is the transform widget's job
/// </summary>
public class SplineTangentGizmo : UIElement
{
    public SplineActorProxy Attach;
    public SplineTangentHandle HoveredHandle;
    public SplineTangentHandle DragHandle { get; private set; }
    public bool IsDragging => DragHandle is not SplineTangentHandle.None;
    private SplineLoftSettings DragStartSettings;
    private Vector3 DragStartTangent;
    //turning: the sphere it moves on, which half, and the turn from where the mouse ray first met the sphere to the grabber, so it doesn't jump
    private float DragRadius;
    private bool DragNearSide;
    private Quaternion DragGrabOffset;
    //lengthening: where along the tangent the mouse ray first passed
    private float DragStartDistance;

    private readonly int[] HandleHitIds = new int[Enum.GetValues<SplineTangentHandle>().Length];

    /// <summary>Called when a drag ends with the settings before and after it. Wired by LevelEditor to push undo actions</summary>
    public Action<SplineActorProxy, SplineLoftSettings, SplineLoftSettings> OnDragComplete;

    private static readonly Vector4 HandleColor = new(1, 1, 1, 1);
    private static readonly Vector4 ActiveHandleColor = new(1, 1, 0, 1);
    //sizes in pixels
    private const float SphereRadius = 5, ArrowGap = 3, ArrowLength = 14, ArrowRadius = 5, MinimumReach = 16;
    /// <summary>The shortest tangent the arrow drags to</summary>
    private const float MinimumTangent = 1;

    public void GetHandleHitProxies(ref USparseArray<IHitProxy> hitProxies)
    {
        foreach (SplineTangentHandle handle in Enum.GetValues<SplineTangentHandle>())
            HandleHitIds[(int)handle] = handle is SplineTangentHandle.None ? BatchedPrimitives.NoHitId : hitProxies.Add(new SplineTangentHitProxy(handle));
    }

    public int HandleHitId(SplineTangentHandle handle) => HandleHitIds[(int)handle];

    private static bool IsArrive(SplineTangentHandle handle) => handle is SplineTangentHandle.ArriveDirection or SplineTangentHandle.ArriveLength;
    private static bool IsDirection(SplineTangentHandle handle) => handle is SplineTangentHandle.LeaveDirection or SplineTangentHandle.ArriveDirection;

    /// <summary>The tangent's unit direction, or the point's X axis when it has none</summary>
    private Vector3 TangentDirection(Vector3 worldTangent)
    {
        if (worldTangent.LengthSquared() > 1e-8f) return Vector3.Normalize(worldTangent);
        Vector3 x = Vector3.TransformNormal(Vector3.UnitX, Attach.Node.LocalToWorld);
        return x.LengthSquared() > 0 ? Vector3.Normalize(x) : Vector3.UnitX;
    }

    /// <summary>A side's unit direction, and how far out its sphere is: at the control point, but never closer to the point than a few pixels</summary>
    private (Vector3 Direction, float Reach) Placement(LevelEditorRenderContext context, bool arrive)
    {
        Vector3 tangent = Attach.Node.WorldTangent;
        Vector3 direction = TangentDirection(tangent);
        return (arrive ? -direction : direction, MathF.Max(tangent.Length() / 3, MinimumReach * context.WorldUnitsPerPixel(Attach.Location)));
    }

    public override void Draw(LevelEditorRenderContext context)
    {
        if (Attach is null || !context.IsActorVisible(Attach)) return;
        //the selected point's spline network over the scene, since the points lie inside their meshes. Each marker selects its point
        foreach (var point in Attach.Network())
        {
            foreach (var curve in point.ConnectionCurves())
                SplineActorProxy.DrawCurve(context, context.UIPrimitives, curve, LevelEditorRenderContext.HighlightColor with { W = 0.75f });
        }
        foreach (var point in Attach.LoadedPoints)
        {
            if (!context.IsActorVisible(point)) continue;
            Vector4 color = point == Attach ? LevelEditorRenderContext.HighlightColor : SplineActorProxy.CurveColor with { W = 1 };
            DrawBox(context, point.Location, 5, color, point.PointHitProxy.HitID);
        }
        Vector3 position = Attach.Location;
        Vector3 toLight = ToLight(context);
        foreach (bool arrive in new[] { false, true })
        {
            (SplineTangentHandle turn, SplineTangentHandle lengthen) = arrive
                ? (SplineTangentHandle.ArriveDirection, SplineTangentHandle.ArriveLength)
                : (SplineTangentHandle.LeaveDirection, SplineTangentHandle.LeaveLength);
            (Vector3 direction, float reach) = Placement(context, arrive);
            Vector3 sphere = position + direction * reach;
            float pixel = context.WorldUnitsPerPixel(sphere);
            Vector3 arrowBase = sphere + direction * (SphereRadius + ArrowGap) * pixel;
            context.UIPrimitives.AddLine(position, arrowBase, (IsActive(turn) || IsActive(lengthen) ? ActiveHandleColor : HandleColor) with { W = 0.8f }, BatchedPrimitives.NoHitId);
            if (Attach.IsReadOnly) continue;
            //the sphere the turning grabber moves on
            if (IsActive(turn)) DrawCircle(context, position, IsDragging ? DragRadius : reach, ActiveHandleColor with { W = 0.35f });
            DrawSphere(context, sphere, SphereRadius * pixel, IsActive(turn) ? ActiveHandleColor : HandleColor, toLight, HandleHitIds[(int)turn]);
            DrawCone(context, arrowBase, direction, ArrowLength * pixel, ArrowRadius * pixel, IsActive(lengthen) ? ActiveHandleColor : HandleColor, toLight,
                HandleHitIds[(int)lengthen]);
        }
    }

    private bool IsActive(SplineTangentHandle handle) => DragHandle == handle || DragHandle is SplineTangentHandle.None && HoveredHandle == handle;

    /// <summary>A cube <paramref name="pixels"/> pixels across, over the scene</summary>
    private static void DrawBox(LevelEditorRenderContext context, Vector3 center, float pixels, Vector4 color, int hitId)
    {
        float size = pixels * context.WorldUnitsPerPixel(center);
        var mesh = context.UIPrimitives.BuildMesh(color, hitId, Matrix4x4.CreateScale(size) * Matrix4x4.CreateTranslation(center));
        for (int i = 0; i < 8; i++) mesh.AddVertex((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
        int[] faces = [0, 2, 3, 3, 1, 0, 4, 5, 7, 7, 6, 4, 0, 1, 5, 5, 4, 0, 2, 6, 7, 7, 3, 2, 0, 4, 6, 6, 2, 0, 1, 3, 7, 7, 5, 1];
        for (int i = 0; i < faces.Length; i += 3) mesh.AddTriangle(faces[i], faces[i + 1], faces[i + 2]);
    }

    /// <summary>Lit from over the viewer's shoulder, like the light arrows, so the grabbers read as solid</summary>
    private static Vector3 ToLight(LevelEditorRenderContext context)
    {
        (_, Vector3 up) = context.GetViewAxes();
        return Vector3.Normalize(up * 0.6f - context.Camera.CameraForward * 0.8f);
    }

    private static Vector4 Shade(Vector4 color, Vector3 normal, Vector3 toLight)
    {
        float shade = 0.45f + 0.55f * MathF.Max(0, Vector3.Dot(normal, toLight));
        return new Vector4(color.X * shade, color.Y * shade, color.Z * shade, color.W);
    }

    private static void DrawSphere(LevelEditorRenderContext context, Vector3 center, float radius, Vector4 color, Vector3 toLight, int hitId)
    {
        const int rings = 6, segments = 10;
        var mesh = context.UIPrimitives.BuildMesh(color, hitId, Matrix4x4.Identity);
        for (int ring = 0; ring <= rings; ring++)
        {
            (float sinPolar, float cosPolar) = MathF.SinCos(MathF.PI * ring / rings);
            for (int segment = 0; segment <= segments; segment++)
            {
                (float sinAzimuth, float cosAzimuth) = MathF.SinCos(MathF.Tau * segment / segments);
                var normal = new Vector3(sinPolar * cosAzimuth, sinPolar * sinAzimuth, cosPolar);
                mesh.AddVertex(center + normal * radius, Shade(color, normal, toLight));
            }
        }
        //both windings, so culling never hides a face
        for (int ring = 0; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int a = ring * (segments + 1) + segment, b = a + segments + 1;
                mesh.AddTriangle(a, b, a + 1); mesh.AddTriangle(a, a + 1, b);
                mesh.AddTriangle(a + 1, b, b + 1); mesh.AddTriangle(a + 1, b + 1, b);
            }
        }
    }

    private static void DrawCone(LevelEditorRenderContext context, Vector3 baseCenter, Vector3 direction, float length, float radius, Vector4 color,
        Vector3 toLight, int hitId)
    {
        const int sides = 10;
        (Vector3 u, Vector3 v) = LightProxyVisuals.Perpendiculars(direction);
        Vector3 tip = baseCenter + direction * length;
        Vector4 baseColor = Shade(color, -direction, toLight);
        var mesh = context.UIPrimitives.BuildMesh(color, hitId, Matrix4x4.Identity);
        for (int i = 0, vertex = 0; i < sides; i++, vertex += 6)
        {
            (float sinA, float cosA) = MathF.SinCos(MathF.Tau * i / sides);
            (float sinB, float cosB) = MathF.SinCos(MathF.Tau * (i + 1) / sides);
            Vector3 ringA = u * cosA + v * sinA, ringB = u * cosB + v * sinB;
            Vector3 a = baseCenter + ringA * radius, b = baseCenter + ringB * radius;
            Vector4 sideColor = Shade(color, Vector3.Normalize(Vector3.Normalize(ringA + ringB) * length + direction * radius), toLight);
            mesh.AddVertex(a, sideColor); mesh.AddVertex(b, sideColor); mesh.AddVertex(tip, sideColor);
            mesh.AddVertex(a, baseColor); mesh.AddVertex(b, baseColor); mesh.AddVertex(baseCenter, baseColor);
            for (int face = vertex; face <= vertex + 3; face += 3)
            {
                mesh.AddTriangle(face, face + 1, face + 2);
                mesh.AddTriangle(face, face + 2, face + 1);
            }
        }
    }

    /// <summary>A circle around <paramref name="center"/> facing the viewer: the outline of a sphere</summary>
    private static void DrawCircle(LevelEditorRenderContext context, Vector3 center, float radius, Vector4 color)
    {
        const int segments = 48;
        (Vector3 right, Vector3 up) = context.GetViewAxes();
        Vector3 previous = center + right * radius;
        for (int i = 1; i <= segments; i++)
        {
            (float sin, float cos) = MathF.SinCos(MathF.Tau * i / segments);
            Vector3 point = center + (right * cos + up * sin) * radius;
            context.UIPrimitives.AddLine(previous, point, color, BatchedPrimitives.NoHitId);
            previous = point;
        }
    }

    /// <summary>Starts dragging a grabber, from where the mouse grabbed it</summary>
    public void BeginDrag(LevelEditorRenderContext context, SplineTangentHandle handle, int x, int y)
    {
        if (Attach is null || Attach.IsReadOnly || handle is SplineTangentHandle.None) return;
        (Vector3 direction, float reach) = Placement(context, IsArrive(handle));
        (Vector3 rayOrigin, Vector3 rayDirection) = context.GetPixelRay(x, y);
        Vector3 center = Attach.Location;
        if (IsDirection(handle))
        {
            DragRadius = reach;
            DragNearSide = SplineTangentDrag.FacesViewer(center, center + direction * reach, rayDirection);
            Vector3 grabbed = SplineTangentDrag.SphereDirection(center, reach, rayOrigin, rayDirection, DragNearSide);
            DragGrabOffset = grabbed == Vector3.Zero ? Quaternion.Identity : SplineTangentDrag.Rotation(grabbed, direction);
        }
        else if (SplineTangentDrag.AxisDistance(center, direction, rayOrigin, rayDirection) is { } along)
        {
            DragStartDistance = along;
        }
        else return;
        DragHandle = handle;
        DragStartSettings = Attach.Settings;
        DragStartTangent = Attach.Node.WorldTangent;
    }

    public void Drag(LevelEditorRenderContext context, int x, int y)
    {
        if (Attach is null || DragHandle is SplineTangentHandle.None) return;
        (Vector3 rayOrigin, Vector3 rayDirection) = context.GetPixelRay(x, y);
        Vector3 center = Attach.Location;
        bool arrive = IsArrive(DragHandle);
        float length = DragStartTangent.Length();
        Vector3 side;
        if (IsDirection(DragHandle))
        {
            Vector3 hit = SplineTangentDrag.SphereDirection(center, DragRadius, rayOrigin, rayDirection, DragNearSide);
            if (hit == Vector3.Zero) return;
            side = Vector3.Normalize(Vector3.Transform(hit, DragGrabOffset));
            //a point without a tangent gets one as long as the grabber's reach
            if (length < MinimumTangent) length = DragRadius * 3;
        }
        else
        {
            Vector3 startSide = TangentDirection(DragStartTangent) * (arrive ? -1 : 1);
            if (SplineTangentDrag.AxisDistance(center, startSide, rayOrigin, rayDirection) is not { } along) return;
            side = startSide;
            length = MathF.Max(length + (along - DragStartDistance) * 3, MinimumTangent);
        }
        Attach.SetWorldTangent((arrive ? -side : side) * length, "tangent");
    }

    public void EndDrag(DragEndReason reason = DragEndReason.Completed)
    {
        if (DragHandle is not SplineTangentHandle.None && Attach is not null && Attach.Settings != DragStartSettings)
        {
            //restored while still dragging, so the editor doesn't record it as an edit
            if (reason is DragEndReason.Cancelled) Attach.RestoreSettings(DragStartSettings);
            else OnDragComplete?.Invoke(Attach, DragStartSettings, Attach.Settings);
        }
        DragHandle = SplineTangentHandle.None;
    }
}
