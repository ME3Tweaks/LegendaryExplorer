using System;
using System.Collections.Generic;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// The level's static geometry, for testing whether a line is blocked, as UE3's line checks against level geometry do for light environments' visibility traces:
/// the levels' BSP, and a bounding volume hierarchy of the mesh instances, each with its own hierarchy of triangles in local space (shared between instances of a mesh)
/// </summary>
public sealed class LevelGeometry
{
    private sealed class Instance
    {
        public MeshStaticLighting Owner;
        public TriangleMesh Mesh;
        public Matrix4x4 WorldToLocal;
    }

    /// <summary>
    /// A mesh's triangles, in its local space, and their hierarchy
    /// </summary>
    public sealed class TriangleMesh
    {
        private readonly Vector3[] Positions;
        private readonly int[] Indices;
        private readonly Bvh Hierarchy;

        public readonly Vector3 BoundsMin;
        public readonly Vector3 BoundsMax;

        /// <summary>
        /// The triangles of <paramref name="indices"/>, skipping any with an index outside <paramref name="positions"/>. Null if none are left
        /// </summary>
        public static TriangleMesh Create(Vector3[] positions, int[] indices)
        {
            var validIndices = new List<int>(indices.Length - indices.Length % 3);
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if ((uint)a < (uint)positions.Length && (uint)b < (uint)positions.Length && (uint)c < (uint)positions.Length)
                {
                    validIndices.Add(a);
                    validIndices.Add(b);
                    validIndices.Add(c);
                }
            }
            return validIndices.Count > 0 ? new TriangleMesh(positions, validIndices.ToArray()) : null;
        }

        private TriangleMesh(Vector3[] positions, int[] indices)
        {
            Positions = positions;
            Indices = indices;
            int numTriangles = indices.Length / 3;
            var mins = new Vector3[numTriangles];
            var maxs = new Vector3[numTriangles];
            for (int i = 0; i < numTriangles; i++)
            {
                Vector3 a = positions[indices[i * 3]], b = positions[indices[i * 3 + 1]], c = positions[indices[i * 3 + 2]];
                mins[i] = Vector3.Min(a, Vector3.Min(b, c));
                maxs[i] = Vector3.Max(a, Vector3.Max(b, c));
            }
            Hierarchy = new Bvh(mins, maxs);
            (BoundsMin, BoundsMax) = Hierarchy.RootBounds;
        }

        /// <summary>
        /// Whether the segment from <paramref name="start"/> to <paramref name="end"/> crosses a triangle (from either side)
        /// </summary>
        /// <param name="tMargin">Crossings this close to either end, as a fraction of the segment, don't count</param>
        public bool IntersectsSegment(Vector3 start, Vector3 end, float tMargin) => Hierarchy.AnyHit(start, end, (triangle, origin, direction) =>
        {
            Vector3 a = Positions[Indices[triangle * 3]], b = Positions[Indices[triangle * 3 + 1]], c = Positions[Indices[triangle * 3 + 2]];
            return SegmentIntersectsTriangle(origin, direction, a, b, c, tMargin);
        });

        //Möller–Trumbore, two-sided, with t in the segment
        private static bool SegmentIntersectsTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c, float tMargin)
        {
            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            Vector3 p = Vector3.Cross(direction, edge2);
            float determinant = Vector3.Dot(edge1, p);
            if (MathF.Abs(determinant) < 1e-12f)
            {
                return false;
            }
            float invDeterminant = 1 / determinant;
            Vector3 s = origin - a;
            float u = Vector3.Dot(s, p) * invDeterminant;
            if (u < 0 || u > 1)
            {
                return false;
            }
            Vector3 q = Vector3.Cross(s, edge1);
            float v = Vector3.Dot(direction, q) * invDeterminant;
            if (v < 0 || u + v > 1)
            {
                return false;
            }
            float t = Vector3.Dot(edge2, q) * invDeterminant;
            return t > tMargin && t < 1 - tMargin;
        }
    }

    private readonly Instance[] Instances;
    private readonly Bvh InstanceHierarchy;

    private readonly Model[] LevelModels;

    /// <param name="levelModels">The BSP of each open level</param>
    internal LevelGeometry(IEnumerable<MeshStaticLighting> primitives, Model[] levelModels)
    {
        LevelModels = levelModels;
        var instances = new List<Instance>();
        var mins = new List<Vector3>();
        var maxs = new List<Vector3>();
        foreach (MeshStaticLighting primitive in primitives)
        {
            if (primitive.GeometryMesh is not { } mesh || !primitive.CanBlockVisibilityTraces || primitive.GetLocalToWorld is null)
            {
                continue;
            }
            Matrix4x4 localToWorld = primitive.GetLocalToWorld();
            if (!Matrix4x4.Invert(localToWorld, out Matrix4x4 worldToLocal))
            {
                continue;
            }
            instances.Add(new Instance { Owner = primitive, Mesh = mesh, WorldToLocal = worldToLocal });
            (Vector3 min, Vector3 max) = TransformBox(mesh.BoundsMin, mesh.BoundsMax, localToWorld);
            mins.Add(min);
            maxs.Add(max);
        }
        Instances = instances.ToArray();
        InstanceHierarchy = new Bvh(mins.ToArray(), maxs.ToArray());
    }

    //how close to either end of a segment, in world units, a triangle can be without blocking it
    private const float SegmentEndTolerance = 0.01f;

    /// <summary>
    /// Whether a light environment's visibility trace to <paramref name="light"/>, from <paramref name="start"/> to <paramref name="end"/>, is blocked by the level's geometry:
    /// the meshes that block traces to that light (<see cref="MeshStaticLighting.BlocksVisibilityTraceTo"/>), other than <paramref name="ignoredActor"/>'s, as UE3's
    /// SingleLineCheck ignores the light environment's owner
    /// </summary>
    public bool IsSegmentBlocked(Vector3 start, Vector3 end, SceneLight light, ExportEntry ignoredActor)
    {
        float length = Vector3.Distance(start, end);
        if (length <= SegmentEndTolerance * 2)
        {
            return false;
        }
        //UWorld::BSPLineCheck: every level's BSP, whichever light it is (TRACE_Level)
        foreach (Model model in LevelModels)
        {
            if (model.Nodes.Length > 0 && !IsOutsideAlong(model, 0, end, start, model.RootOutside))
            {
                return true;
            }
        }
        //as a fraction of the segment, which is the same in each mesh's local space, since the transforms are affine
        float tMargin = SegmentEndTolerance / length;
        return InstanceHierarchy.AnyHit(start, end, (index, _, _) =>
        {
            Instance instance = Instances[index];
            if (ignoredActor is not null && instance.Owner.OwnerActor == ignoredActor || !instance.Owner.BlocksVisibilityTraceTo(light))
            {
                return false;
            }
            return instance.Mesh.IntersectsSegment(Vector3.Transform(start, instance.WorldToLocal), Vector3.Transform(end, instance.WorldToLocal), tMargin);
        });
    }

    //BSP node flags (UE3's EBspNodeFlags)
    private const byte NF_NotCsg = 0x01;
    private const byte NF_IsNew = 0x20;

    /// <summary>
    /// UE3's recursive minion of UModel::LineCheck, for a line (no extent) in world space: walks the BSP's solid and empty space along the segment.
    /// False if it enters solid space, which blocks it
    /// </summary>
    /// <param name="outside">Whether the space the segment starts in is empty</param>
    private static bool IsOutsideAlong(Model model, int iNode, Vector3 end, Vector3 start, bool outside)
    {
        while (iNode >= 0 && iNode < model.Nodes.Length)
        {
            BspNode node = model.Nodes[iNode];
            float dist1 = PlaneDot(node.Plane, start);
            float dist2 = PlaneDot(node.Plane, end);
            if (dist1 > -0.001f && dist2 > -0.001f)
            {
                //both in front
                outside |= IsCsg(node);
                iNode = node.iFront;
            }
            else if (dist1 < 0.001f && dist2 < 0.001f)
            {
                //both behind
                outside = outside && !IsCsg(node);
                iNode = node.iBack;
            }
            else
            {
                //split: the part on the start's side first, then the rest
                Vector3 middle = start + (start - end) * (dist1 / (dist2 - dist1));
                bool frontFirst = dist1 > 0;
                if (!IsOutsideAlong(model, frontFirst ? node.iFront : node.iBack, middle, start, ChildOutside(node, frontFirst, outside)))
                {
                    return false;
                }
                outside = ChildOutside(node, !frontFirst, outside);
                iNode = frontFirst ? node.iBack : node.iFront;
                start = middle;
            }
        }
        return outside;

        static float PlaneDot(Plane plane, Vector3 point) => Vector3.Dot(plane.Normal, point) - plane.D;
        static bool IsCsg(BspNode node) => node.NumVertices > 0 && (node.NodeFlags & (NF_IsNew | NF_NotCsg)) == 0;
        static bool ChildOutside(BspNode node, bool front, bool outside) => front ? outside || IsCsg(node) : outside && !IsCsg(node);
    }

    private static (Vector3 min, Vector3 max) TransformBox(Vector3 min, Vector3 max, Matrix4x4 m)
    {
        Vector3 center = Vector3.Transform((min + max) / 2, m);
        Vector3 extent = (max - min) / 2;
        var worldExtent = new Vector3(
            MathF.Abs(m.M11) * extent.X + MathF.Abs(m.M21) * extent.Y + MathF.Abs(m.M31) * extent.Z,
            MathF.Abs(m.M12) * extent.X + MathF.Abs(m.M22) * extent.Y + MathF.Abs(m.M32) * extent.Z,
            MathF.Abs(m.M13) * extent.X + MathF.Abs(m.M23) * extent.Y + MathF.Abs(m.M33) * extent.Z);
        return (center - worldExtent, center + worldExtent);
    }

    /// <summary>
    /// A bounding volume hierarchy of items with axis-aligned bounds, split at the median along the longest axis of their centers
    /// </summary>
    private sealed class Bvh
    {
        private const int MaxLeafSize = 4;

        private readonly List<Vector3> NodeMins = [];
        private readonly List<Vector3> NodeMaxs = [];
        //internal nodes: the index of the second child (the first is the next node), and a count of 0. Leaves: the first item in Items, and their count
        private readonly List<int> NodeOffsets = [];
        private readonly List<int> NodeCounts = [];
        private readonly int[] Items;

        public (Vector3, Vector3) RootBounds => NodeMins.Count > 0 ? (NodeMins[0], NodeMaxs[0]) : (Vector3.Zero, Vector3.Zero);

        public Bvh(Vector3[] mins, Vector3[] maxs)
        {
            Items = new int[mins.Length];
            for (int i = 0; i < Items.Length; i++)
            {
                Items[i] = i;
            }
            if (Items.Length > 0)
            {
                var centers = new Vector3[mins.Length];
                for (int i = 0; i < centers.Length; i++)
                {
                    centers[i] = (mins[i] + maxs[i]) / 2;
                }
                Build(mins, maxs, centers, 0, Items.Length);
            }
        }

        private void Build(Vector3[] mins, Vector3[] maxs, Vector3[] centers, int start, int count)
        {
            Vector3 min = new(float.MaxValue), max = new(float.MinValue);
            Vector3 centerMin = new(float.MaxValue), centerMax = new(float.MinValue);
            for (int i = start; i < start + count; i++)
            {
                int item = Items[i];
                min = Vector3.Min(min, mins[item]);
                max = Vector3.Max(max, maxs[item]);
                centerMin = Vector3.Min(centerMin, centers[item]);
                centerMax = Vector3.Max(centerMax, centers[item]);
            }
            int node = NodeMins.Count;
            NodeMins.Add(min);
            NodeMaxs.Add(max);
            NodeOffsets.Add(start);
            NodeCounts.Add(count);
            Vector3 size = centerMax - centerMin;
            if (count <= MaxLeafSize || size == Vector3.Zero)
            {
                return;
            }
            int axis = size.X >= size.Y && size.X >= size.Z ? 0 : size.Y >= size.Z ? 1 : 2;
            int half = count / 2;
            Array.Sort(Items, start, count, Comparer<int>.Create((a, b) => GetAxis(centers[a], axis).CompareTo(GetAxis(centers[b], axis))));
            NodeCounts[node] = 0;
            Build(mins, maxs, centers, start, half);
            NodeOffsets[node] = NodeMins.Count;
            Build(mins, maxs, centers, start + half, count - half);
        }

        private static float GetAxis(Vector3 v, int axis) => axis switch { 0 => v.X, 1 => v.Y, _ => v.Z };

        /// <summary>
        /// Whether <paramref name="hit"/> returns true for any item whose bounds the segment crosses
        /// </summary>
        public bool AnyHit(Vector3 start, Vector3 end, Func<int, Vector3, Vector3, bool> hit)
        {
            if (NodeMins.Count == 0)
            {
                return false;
            }
            Vector3 direction = end - start;
            var invDirection = new Vector3(1 / direction.X, 1 / direction.Y, 1 / direction.Z);
            Span<int> stack = stackalloc int[64];
            int stackSize = 0;
            stack[stackSize++] = 0;
            while (stackSize > 0)
            {
                int node = stack[--stackSize];
                if (!SegmentIntersectsBox(start, invDirection, NodeMins[node], NodeMaxs[node]))
                {
                    continue;
                }
                int count = NodeCounts[node];
                if (count > 0)
                {
                    int first = NodeOffsets[node];
                    for (int i = first; i < first + count; i++)
                    {
                        if (hit(Items[i], start, direction))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    //median splits keep the depth at log2 of the item count, so the stack holds at most one entry per level, plus one
                    System.Diagnostics.Debug.Assert(stackSize + 2 <= stack.Length, "BVH is deeper than its traversal stack");
                    stack[stackSize++] = NodeOffsets[node];
                    stack[stackSize++] = node + 1;
                }
            }
            return false;
        }

        //slab test, with t in [0, 1]
        private static bool SegmentIntersectsBox(Vector3 origin, Vector3 invDirection, Vector3 min, Vector3 max)
        {
            Vector3 t0 = (min - origin) * invDirection;
            Vector3 t1 = (max - origin) * invDirection;
            Vector3 tMin = Vector3.Min(t0, t1);
            Vector3 tMax = Vector3.Max(t0, t1);
            //NaNs (from 0 * infinity, on a slab's plane) are ignored by MathF.Max/Min's handling below
            float enter = MaxNumber(MaxNumber(tMin.X, tMin.Y), MaxNumber(tMin.Z, 0));
            float exit = MinNumber(MinNumber(tMax.X, tMax.Y), MinNumber(tMax.Z, 1));
            return enter <= exit;
        }

        private static float MaxNumber(float a, float b) => float.IsNaN(a) ? b : float.IsNaN(b) ? a : MathF.Max(a, b);
        private static float MinNumber(float a, float b) => float.IsNaN(a) ? b : float.IsNaN(b) ? a : MathF.Min(a, b);
    }
}
