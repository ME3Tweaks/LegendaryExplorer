using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Unreal.Collections;
using System;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

public enum LightHandle
{
    None,
    Radius,
    InnerCone,
    OuterCone,
}

public class LightHandleHitProxy(LightHandle handle) : IHitProxy
{
    public LightHandle Handle { get; } = handle;
    public int HitID { get; set; }
    public int HitPriority => IHitProxy.UIPriority;
}

/// <summary>
/// Shows the selected light's reach (its radius, and a spot light's cones), with handles that drag them.
/// Moving and rotating the light is the transform widget's job
/// </summary>
public class LightGizmo : UIElement
{
    /// <summary>
    /// The selected light. Null if the selection isn't a light
    /// </summary>
    public ILightProxy Attach;

    public LightHandle HoveredHandle;
    public LightHandle DragHandle { get; private set; }
    public bool IsDragging => DragHandle is not LightHandle.None;
    private LightSettings DragStartSettings;

    private readonly int[] HandleHitIds = new int[Enum.GetValues<LightHandle>().Length];

    /// <summary>
    /// Called when a drag ends with the settings before and after it. Wired by LevelEditor to push undo actions
    /// </summary>
    public Action<ILightProxy, LightSettings, LightSettings> OnDragComplete;

    private static readonly Vector4 HandleColor = new(1, 1, 1, 1);
    private static readonly Vector4 ActiveHandleColor = new(1, 1, 0, 1);
    private const int NoHitId = 0xFFFFFF;

    public void GetHandleHitProxies(ref USparseArray<IHitProxy> hitProxies)
    {
        foreach (LightHandle handle in Enum.GetValues<LightHandle>())
        {
            HandleHitIds[(int)handle] = handle is LightHandle.None ? NoHitId : hitProxies.Add(new LightHandleHitProxy(handle));
        }
    }

    public override void Draw(LevelEditorRenderContext context)
    {
        if (Attach is not ActorProxy actor || !context.IsActorVisible(actor)) return;
        EditableLight light = Attach.Light;
        (Vector3 position, Vector3 direction) = light.GetPlacement(actor.LocalToWorld);
        float pixel = context.WorldUnitsPerPixel(position);
        Vector4 color = light.DisplayColor;
        Vector4 faint = color with { W = 0.45f };
        float radius = light.Settings.Radius;
        bool editable = !light.IsReadOnly;

        //the selection outline around the sprite
        LightProxyVisuals.DrawScreenSquare(context, context.UIPrimitives, position, LightProxyVisuals.SpriteHalfSize(context, position) * 1.15f,
            new Vector4(1, 1, 0, 1), NoHitId);

        switch (light.Type)
        {
            case SceneLightType.Point:
                DrawCircle(context, position, context.Camera.CameraRight, context.Camera.CameraUp, radius, color);
                DrawCircle(context, position, Vector3.UnitX, Vector3.UnitY, radius, faint);
                DrawCircle(context, position, Vector3.UnitX, Vector3.UnitZ, radius, faint);
                DrawCircle(context, position, Vector3.UnitY, Vector3.UnitZ, radius, faint);
                if (editable)
                {
                    DrawHandle(context, LightHandle.Radius, position + context.Camera.CameraRight * radius, pixel);
                }
                break;
            case SceneLightType.Spot:
            {
                (Vector3 u, Vector3 v) = LightProxyVisuals.Perpendiculars(direction);
                float outer = light.Settings.OuterConeAngle * MathF.PI / 180;
                float inner = light.Settings.InnerConeAngle * MathF.PI / 180;
                DrawCone(context, position, direction, u, v, radius, outer, color, true);
                if (inner > 0)
                {
                    DrawCone(context, position, direction, u, v, radius, inner, faint, false);
                }
                //the sphere cap at the light's radius
                DrawArc(context, position, direction, u, radius, -outer, outer, color);
                DrawArc(context, position, direction, v, radius, -outer, outer, color);
                if (editable)
                {
                    Vector3 side = HandleSide(context, direction);
                    DrawHandle(context, LightHandle.Radius, position + direction * radius, pixel);
                    DrawHandle(context, LightHandle.OuterCone, position + ConePoint(direction, side, outer) * radius, pixel);
                    DrawHandle(context, LightHandle.InnerCone, position + ConePoint(direction, side, inner) * (radius * 0.7f), pixel);
                }
                break;
            }
            case SceneLightType.Directional:
            {
                Vector3 tip = position + direction * (90 * pixel);
                context.UIPrimitives.AddLine(position, tip, color, NoHitId);
                (Vector3 u, Vector3 v) = LightProxyVisuals.Perpendiculars(direction);
                foreach (Vector3 side in new[] { u, -u, v, -v })
                {
                    context.UIPrimitives.AddLine(tip, tip - direction * (14 * pixel) + side * (6 * pixel), color, NoHitId);
                }
                break;
            }
        }
    }

    /// <summary>
    /// The direction from a spot light's axis that the cone handles are placed in: towards the camera's right, so they stay on the cone's silhouette
    /// </summary>
    private static Vector3 HandleSide(LevelEditorRenderContext context, Vector3 direction)
    {
        Vector3 side = context.Camera.CameraRight - direction * Vector3.Dot(context.Camera.CameraRight, direction);
        return side.LengthSquared() > 1e-6f ? Vector3.Normalize(side) : LightProxyVisuals.Perpendiculars(direction).U;
    }

    private static Vector3 ConePoint(Vector3 direction, Vector3 side, float angle) => direction * MathF.Cos(angle) + side * MathF.Sin(angle);

    private void DrawHandle(LevelEditorRenderContext context, LightHandle handle, Vector3 position, float pixel)
    {
        Vector4 color = DragHandle == handle || DragHandle is LightHandle.None && HoveredHandle == handle ? ActiveHandleColor : HandleColor;
        int hitId = HandleHitIds[(int)handle];
        var mesh = context.UIPrimitives.BuildMesh(color, hitId, Matrix4x4.CreateScale(4 * pixel) * Matrix4x4.CreateTranslation(position));
        for (int i = 0; i < 8; i++)
        {
            mesh.AddVertex((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
        }
        int[] faces = [0, 2, 3, 3, 1, 0, 4, 5, 7, 7, 6, 4, 0, 1, 5, 5, 4, 0, 2, 6, 7, 7, 3, 2, 0, 4, 6, 6, 2, 0, 1, 3, 7, 7, 5, 1];
        for (int i = 0; i < faces.Length; i += 3)
        {
            mesh.AddTriangle(faces[i], faces[i + 1], faces[i + 2]);
        }
    }

    private static void DrawCircle(LevelEditorRenderContext context, Vector3 center, Vector3 axisA, Vector3 axisB, float radius, Vector4 color)
    {
        const int segments = 64;
        Vector3 previous = center + axisA * radius;
        for (int i = 1; i <= segments; i++)
        {
            (float sin, float cos) = MathF.SinCos(MathF.Tau * i / segments);
            Vector3 point = center + (axisA * cos + axisB * sin) * radius;
            context.UIPrimitives.AddLine(previous, point, color, NoHitId);
            previous = point;
        }
    }

    private static void DrawArc(LevelEditorRenderContext context, Vector3 center, Vector3 direction, Vector3 side, float radius, float from, float to, Vector4 color)
    {
        const int segments = 24;
        Vector3 previous = center + ConePoint(direction, side, from) * radius;
        for (int i = 1; i <= segments; i++)
        {
            Vector3 point = center + ConePoint(direction, side, from + (to - from) * i / segments) * radius;
            context.UIPrimitives.AddLine(previous, point, color, NoHitId);
            previous = point;
        }
    }

    private static void DrawCone(LevelEditorRenderContext context, Vector3 apex, Vector3 direction, Vector3 u, Vector3 v, float radius, float angle, Vector4 color, bool drawEdges)
    {
        (float sin, float cos) = MathF.SinCos(angle);
        Vector3 rimCenter = apex + direction * (radius * cos);
        DrawCircle(context, rimCenter, u, v, radius * sin, color);
        if (!drawEdges) return;
        for (int i = 0; i < 8; i++)
        {
            (float s, float c) = MathF.SinCos(MathF.Tau * i / 8);
            context.UIPrimitives.AddLine(apex, rimCenter + (u * c + v * s) * (radius * sin), color, NoHitId);
        }
    }

    public void BeginDrag(LightHandle handle)
    {
        if (Attach is null || Attach.Light.IsReadOnly) return;
        DragHandle = handle;
        DragStartSettings = Attach.Light.Settings;
    }

    public void Drag(LevelEditorRenderContext context, int x, int y)
    {
        if (Attach is not ActorProxy actor || DragHandle is LightHandle.None) return;
        EditableLight light = Attach.Light;
        (Vector3 position, Vector3 direction) = light.GetPlacement(actor.LocalToWorld);
        (Vector3 rayOrigin, Vector3 rayDirection) = context.GetPixelRay(x, y);
        if (DragHandle is LightHandle.Radius)
        {
            //on the plane through the light that faces the viewer
            if (IntersectPlane(rayOrigin, rayDirection, position, rayDirection) is { } hit)
            {
                light.Radius = MathF.Round(Vector3.Distance(hit, position));
            }
            return;
        }
        //on the plane through the light's axis that faces the viewer most
        Vector3 normal = Vector3.Cross(direction, Vector3.Cross(rayDirection, direction));
        if (normal.LengthSquared() < 1e-8f || IntersectPlane(rayOrigin, rayDirection, position, Vector3.Normalize(normal)) is not { } point)
        {
            return;
        }
        Vector3 toPoint = point - position;
        if (toPoint.LengthSquared() < 1e-6f) return;
        float angle = MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(toPoint), direction), -1, 1)) * 180 / MathF.PI;
        angle = MathF.Round(Math.Clamp(angle, 0, 89), 1);
        if (DragHandle is LightHandle.OuterCone)
        {
            light.OuterConeAngle = angle;
        }
        else
        {
            //the inner cone can't be wider than the outer one
            light.Apply(light.Settings with { InnerConeAngle = Math.Min(angle, light.Settings.OuterConeAngle) });
        }
    }

    private static Vector3? IntersectPlane(Vector3 rayOrigin, Vector3 rayDirection, Vector3 planePoint, Vector3 planeNormal)
    {
        float denominator = Vector3.Dot(rayDirection, planeNormal);
        if (MathF.Abs(denominator) < 1e-6f) return null;
        float t = Vector3.Dot(planePoint - rayOrigin, planeNormal) / denominator;
        return t < 0 ? null : rayOrigin + rayDirection * t;
    }

    public void EndDrag()
    {
        if (DragHandle is not LightHandle.None && Attach is not null && Attach.Light.Settings != DragStartSettings)
        {
            OnDragComplete?.Invoke(Attach, DragStartSettings, Attach.Light.Settings);
        }
        DragHandle = LightHandle.None;
    }
}
