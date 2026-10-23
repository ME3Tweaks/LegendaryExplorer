using System;
using System.Numerics;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// UStaticMeshComponent::LineCheck's zero-extent kDOP path, which visibility traces take; identical in all three games.
/// RVAs (line check / root / setup / traversal / triangle):
/// LE1 0x5d3040 / 0x5acbb0 / 0x5c0ee0 / 0x5d2280 / 0x5c0350;
/// LE2 0x7dada0 / 0x52aa40 / 0x7cb480 / 0x7d9ff0 / 0x7ca8f0;
/// LE3 0x7d5770 / 0x534510 / 0x7c6a60 / 0x7d4980 / 0x7c5ed0.
/// </summary>
internal sealed class StaticMeshCollisionTree
{
    private readonly record struct Bounds(Vector3 Min, Vector3 Max);
    private readonly record struct Node(Vector3 MinCodes, Vector3 MaxCodes);
    private readonly Vector3[] Positions;
    private readonly int[] Indices;
    private readonly Bounds Root;
    private readonly Node[] Nodes;
    private const float EdgeTolerance = 0.001f;
    private const float BoundsPadding = 0.1f;

    public StaticMeshCollisionTree(Vector3[] positions, int[] indices, Vector3 min, Vector3 max, kDOPTreeCompact tree)
    {
        Positions = positions;
        Indices = indices;
        Root = new Bounds(min, max);
        Nodes = [];
        //Synthetic geometry has one leaf. Real package geometry supplies the serialized tree.
        if (tree is null) return;
        if (tree.RootBound is null || tree.Nodes is null || tree.Triangles is null || tree.Triangles.Length != indices.Length / 3)
            throw new ArgumentException("Collision tree does not match its triangle list.", nameof(tree));
        for (int i = 0; i < tree.Triangles.Length; i++)
        {
            var triangle = tree.Triangles[i];
            if (triangle.Vertex1 != indices[i * 3] || triangle.Vertex2 != indices[i * 3 + 1] || triangle.Vertex3 != indices[i * 3 + 2])
                throw new ArgumentException("Collision triangle order must match the serialized tree.", nameof(tree));
        }
        Root = new Bounds(new Vector3(tree.RootBound.Min[0], tree.RootBound.Min[1], tree.RootBound.Min[2]),
            new Vector3(tree.RootBound.Max[0], tree.RootBound.Max[1], tree.RootBound.Max[2]));
        Nodes = new Node[tree.Nodes.Length];
        for (int i = 0; i < Nodes.Length; i++)
        {
            var node = tree.Nodes[i];
            Nodes[i] = new Node(new Vector3(node.Min[0], node.Min[1], node.Min[2]), new Vector3(node.Max[0], node.Max[1], node.Max[2]));
        }
    }

    public bool Intersects(Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        if (direction == Vector3.Zero || !IntersectsRoot(start, direction)) return false;
        return Visit(0, Root, 0, Indices.Length / 3, start, end, direction);
    }

    private bool Visit(int nodeIndex, Bounds bounds, int first, int count, Vector3 start, Vector3 end, Vector3 direction)
    {
        int lastNode = Nodes.Length - 1;
        if (nodeIndex >= lastNode)
        {
            for (int i = first; i < first + count; i++)
            {
                if (IntersectsTriangle(start, end, direction, Positions[Indices[i * 3]], Positions[Indices[i * 3 + 1]], Positions[Indices[i * 3 + 2]]))
                    return true;
            }
            return false;
        }
        Node node = Nodes[nodeIndex];
        Bounds left = Decode(bounds, node, left: true);
        Bounds right = Decode(bounds, node, left: false);
        int leftCount = count - count / 2;
        int leftIndex = Math.Min(nodeIndex * 2 + 1, lastNode);
        //Any hit suffices; native traversal also stops at the first one (flag 0x200)
        return IntersectsChild(left, start, direction) && Visit(leftIndex, left, first, leftCount, start, end, direction)
            || IntersectsChild(right, start, direction) && Visit(Math.Min(leftIndex + 1, lastNode), right, first + leftCount, count / 2, start, end, direction);
    }

    private static Bounds Decode(Bounds parent, Node node, bool left)
    {
        //Native SIMD constants: 127.5, +/-0.5, +/-1/127, -0.5/127. Keep the two operations separate.
        Vector3 Factor(Vector3 code)
        {
            Vector3 centered = code - new Vector3(127.5f);
            return left
                ? Vector3.Max(centered, new Vector3(0.5f)) * (1f / 127) - new Vector3(0.5f / 127)
                : Vector3.Min(centered, new Vector3(-0.5f)) * (-1f / 127) - new Vector3(0.5f / 127);
        }
        return new Bounds(parent.Min + Factor(node.MinCodes) * (parent.Max - parent.Min),
            parent.Max + Factor(node.MaxCodes) * (parent.Min - parent.Max));
    }

    private bool IntersectsRoot(Vector3 start, Vector3 direction)
    {
        //The entry time comes from the unpadded box, then the other axes allow 0.1 (not a uniformly padded box)
        bool inside = true;
        float time = 0;
        for (int axis = 0; axis < 3; axis++)
        {
            float origin = Axis(start, axis), delta = Axis(direction, axis);
            float min = Axis(Root.Min, axis), max = Axis(Root.Max, axis);
            if (origin < min)
            {
                if (delta <= 0) return false;
                time = MathF.Max(time, (min - origin) / delta);
                inside = false;
            }
            else if (origin > max)
            {
                if (delta >= 0) return false;
                time = MathF.Max(time, (max - origin) / delta);
                inside = false;
            }
        }
        if (inside) return true;
        if (!float.IsFinite(time) || time < 0 || time > 1) return false;
        Vector3 hit = start + direction * time;
        for (int axis = 0; axis < 3; axis++)
            if (Axis(hit, axis) <= Axis(Root.Min, axis) - BoundsPadding || Axis(hit, axis) >= Axis(Root.Max, axis) + BoundsPadding)
                return false;
        return true;
    }

    private static bool IntersectsChild(Bounds bounds, Vector3 start, Vector3 direction)
    {
        float enter = 0, exit = 1;
        for (int axis = 0; axis < 3; axis++)
        {
            float min = Axis(bounds.Min, axis) - BoundsPadding, max = Axis(bounds.Max, axis) + BoundsPadding;
            float origin = Axis(start, axis), delta = Axis(direction, axis);
            if (delta == 0)
            {
                if (origin < min || origin > max) return false;
                continue;
            }
            float t0 = (min - origin) / delta, t1 = (max - origin) / delta;
            enter = MathF.Max(enter, MathF.Min(t0, t1));
            exit = MathF.Min(exit, MathF.Max(t0, t1));
            if (enter > exit) return false;
        }
        return true;
    }

    private static bool IntersectsTriangle(Vector3 start, Vector3 end, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = Vector3.Cross(b - c, a - c);
        float plane = Vector3.Dot(normal, a);
        float startDistance = Vector3.Dot(normal, start) - plane;
        float endDistance = Vector3.Dot(normal, end) - plane;
        if (startDistance < -EdgeTolerance && endDistance < -EdgeTolerance
            || startDistance > EdgeTolerance && endDistance > EdgeTolerance) return false;
        float time = startDistance / (startDistance - endDistance);
        //The best time begins at FLT_MAX, not 1; nonfinite results from degenerate input are rejected.
        if (!float.IsFinite(time) || time < 0 || time >= float.MaxValue) return false;
        Vector3 hit = start + direction * time;
        bool Outside(Vector3 from, Vector3 to)
        {
            Vector3 edgeNormal = Vector3.Cross(normal, to - from);
            return Vector3.Dot(edgeNormal, hit) - Vector3.Dot(edgeNormal, from) >= EdgeTolerance;
        }
        return !Outside(a, b) && !Outside(b, c) && !Outside(c, a);
    }

    private static float Axis(Vector3 value, int axis) => axis switch {0 => value.X, 1 => value.Y, _ => value.Z};
}
