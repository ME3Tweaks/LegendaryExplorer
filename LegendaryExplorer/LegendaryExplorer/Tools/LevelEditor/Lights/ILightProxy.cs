using LegendaryExplorer.Tools.LevelEditor.Scene3D;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor;

/// <summary>
/// An actor proxy for a light the level editor can select and edit
/// </summary>
public interface ILightProxy
{
    EditableLight Light { get; }

    /// <summary>
    /// The owner's (or collection's) uncommitted transform. Null if it isn't moved, so previews use the saved one
    /// </summary>
    Matrix4x4? EditedOwnerTransform { get; }
}

/// <summary>
/// Draws light proxies in the viewport
/// </summary>
internal static class LightProxyVisuals
{
    /// <summary>
    /// Half the size of a light's sprite: a fixed world size, so it's part of the scene, but never smaller than a few pixels, so distant lights stay findable
    /// </summary>
    public static float SpriteHalfSize(LevelEditorRenderContext context, Vector3 position) => MathF.Max(16, 10 * context.WorldUnitsPerPixel(position));

    /// <summary>
    /// The light's icon: a camera-facing sprite for its type, and an arrow in the direction a spot or directional light shines, both tinted with its color.
    /// They're drawn into the scene, so geometry in front of them hides them, and clicking them selects the light
    /// </summary>
    public static void DrawIcon(LevelEditorRenderContext context, ActorProxy actor, EditableLight light)
    {
        (Vector3 position, Vector3 direction) = light.GetPlacement(actor.LocalToWorld);
        if (!context.WorldToPixel(position, out _)) return;
        float halfSize = SpriteHalfSize(context, position);
        Vector4 color = light.DisplayColor;
        int hitId = actor.Editor is null && context.IsActorSelected(actor) ? context.SelectedActor.HitID : actor.HitID;
        (Vector3 right, Vector3 up) = context.GetViewAxes();
        context.Sprites.Add(context.LightSprites.Get(context.Device, light.Type), position, right * halfSize, up * halfSize, color, hitId);

        if (light.Type is SceneLightType.Spot or SceneLightType.Directional)
        {
            DrawArrow(context, position, direction, halfSize, color, hitId);
        }

        //a light that affects the selected mesh
        if (context.HighlightedLights.Contains(light.Component))
        {
            DrawScreenSquare(context, context.WorldPrimitives, position, halfSize * 1.3f, LevelEditorRenderContext.HighlightColor, BatchedPrimitives.NoHitId);
        }
    }

    /// <summary>
    /// A shaded 3D arrow from the light's center, flat-shaded as if lit from over the viewer's shoulder so its direction reads in depth
    /// </summary>
    private static void DrawArrow(LevelEditorRenderContext context, Vector3 origin, Vector3 direction, float halfSize, Vector4 color, int hitId)
    {
        const int sides = 8;
        float shaftRadius = halfSize * 0.09f;
        float headRadius = halfSize * 0.3f;
        float headLength = halfSize * 0.75f;
        float length = halfSize * 2.6f;
        (Vector3 u, Vector3 v) = Perpendiculars(direction);
        Matrix4x4 view = context.Camera.ViewMatrix;
        Vector3 forward = Vector3.Normalize(new Vector3(view.M13, view.M23, view.M33));
        (_, Vector3 up) = context.GetViewAxes();
        Vector3 toLight = Vector3.Normalize(up * 0.6f - forward * 0.8f);
        Vector3 shaftEnd = origin + direction * (length - headLength);
        Vector3 tip = origin + direction * length;

        //each face with a direction pointing out of the arrow, to orient its normal
        Span<(Vector3 A, Vector3 B, Vector3 C, Vector3 Out)> triangles = stackalloc (Vector3, Vector3, Vector3, Vector3)[sides * 4];
        for (int i = 0, t = 0; i < sides; i++)
        {
            (float sinA, float cosA) = MathF.SinCos(MathF.Tau * i / sides);
            (float sinB, float cosB) = MathF.SinCos(MathF.Tau * (i + 1) / sides);
            Vector3 ringA = u * cosA + v * sinA, ringB = u * cosB + v * sinB;
            Vector3 a = ringA * shaftRadius, b = ringB * shaftRadius;
            triangles[t++] = (origin + a, shaftEnd + a, shaftEnd + b, a + b);
            triangles[t++] = (origin + a, shaftEnd + b, origin + b, a + b);
            Vector3 headA = ringA * headRadius, headB = ringB * headRadius;
            triangles[t++] = (shaftEnd + headA, tip, shaftEnd + headB, headA + headB);
            triangles[t++] = (shaftEnd + headA, shaftEnd + headB, shaftEnd, -direction);
        }

        var mesh = context.WorldPrimitives.BuildMesh(color, hitId, Matrix4x4.Identity);
        int vertexCount = 0;
        foreach ((Vector3 a, Vector3 b, Vector3 c, Vector3 outward) in triangles)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, outward) < 0) normal = -normal;
            float shade = normal.LengthSquared() > 0 ? 0.45f + 0.55f * MathF.Max(0, Vector3.Dot(Vector3.Normalize(normal), toLight)) : 1;
            var shaded = new Vector4(color.X * shade, color.Y * shade, color.Z * shade, 1);
            mesh.AddVertex(a, shaded);
            mesh.AddVertex(b, shaded);
            mesh.AddVertex(c, shaded);
            //both windings, so culling never hides a face
            mesh.AddTriangle(vertexCount, vertexCount + 1, vertexCount + 2);
            mesh.AddTriangle(vertexCount, vertexCount + 2, vertexCount + 1);
            vertexCount += 3;
        }
    }

    /// <summary>
    /// A square facing the camera
    /// </summary>
    public static void DrawScreenSquare(LevelEditorRenderContext context, BatchedPrimitives batch, Vector3 center, float halfSize, Vector4 color, int hitId)
    {
        (Vector3 right, Vector3 up) = context.GetViewAxes();
        right *= halfSize;
        up *= halfSize;
        ReadOnlySpan<Vector3> corners = [center - right - up, center + right - up, center + right + up, center - right + up];
        for (int i = 0; i < 4; i++)
        {
            batch.AddLine(corners[i], corners[(i + 1) % 4], color, hitId);
        }
    }

    /// <summary>
    /// Two unit vectors perpendicular to <paramref name="direction"/> and each other
    /// </summary>
    public static (Vector3 U, Vector3 V) Perpendiculars(Vector3 direction)
    {
        Vector3 reference = MathF.Abs(direction.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX;
        Vector3 u = Vector3.Normalize(Vector3.Cross(direction, reference));
        return (u, Vector3.Cross(direction, u));
    }
}

internal static class LightProxyRefresh
{
    /// <param name="owner">The actor, or for a collection's light, its component. Edits of it, or of a light's collection, can't be applied in place</param>
    public static bool TryRefresh(ActorProxy owner, EditableLight light, IMEPackage package, HashSet<int> updatedExports)
    {
        //a draft has nothing in the package to refresh from
        if (owner.IsDraft) return false;
        bool Updated(ExportEntry export) => export.FileRef == package && updatedExports.Contains(export.UIndex);
        if (owner.Export != light.Component && Updated(owner.Export)
            || owner is CollectionActorComponentProxy collection && Updated(collection.CollectionActorExport)
            || !Updated(light.Component))
        {
            return false;
        }
        light.Reload();
        return true;
    }
}
