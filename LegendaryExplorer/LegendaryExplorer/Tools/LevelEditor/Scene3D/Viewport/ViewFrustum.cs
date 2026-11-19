using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Numerics;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>Conservative world-space AABB tests against a Direct3D view frustum.</summary>
public sealed class ViewFrustum
{
    private readonly Plane[] planes = new Plane[6];
    private bool valid;
    private int planeCount;

    public void Update(Matrix4x4 m, bool depthClipEnabled = true)
    {
        planeCount = depthClipEnabled ? 6 : 4;
        // System.Numerics uses row vectors. Clip space is -w <= x,y <= w and 0 <= z <= w.
        // The two depth planes also work with reversed depth and orthographic projections.
        planes[0] = new Plane(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41);
        planes[1] = new Plane(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41);
        planes[2] = new Plane(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42);
        planes[3] = new Plane(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42);
        planes[4] = new Plane(m.M13, m.M23, m.M33, m.M43);
        planes[5] = new Plane(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43);
        valid = true;
        for (int i = 0; i < planeCount; i++)
        {
            float length = planes[i].Normal.Length();
            if (!(length > 0) || !float.IsFinite(length) || !float.IsFinite(planes[i].D))
            {
                valid = false;
                return;
            }
            planes[i] = new Plane(planes[i].Normal / length, planes[i].D / length);
        }
    }

    /// <summary>Returns true for intersecting or invalid bounds, so uncertain geometry stays visible.</summary>
    public bool Intersects(BoxSphereBounds bounds)
    {
        if (!valid || !IsFinite(bounds.Origin) || !IsFinite(bounds.BoxExtent)
            || bounds.BoxExtent.X < 0 || bounds.BoxExtent.Y < 0 || bounds.BoxExtent.Z < 0)
        {
            return true;
        }
        for (int i = 0; i < planeCount; i++)
        {
            Plane plane = planes[i];
            float radius = Vector3.Dot(Vector3.Abs(plane.Normal), bounds.BoxExtent);
            float distance = Vector3.Dot(plane.Normal, bounds.Origin) + plane.D;
            // A small world-space margin keeps geometry touching a plane from flickering.
            if (distance + radius < -0.1f) return false;
        }
        return true;
    }

    private static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
