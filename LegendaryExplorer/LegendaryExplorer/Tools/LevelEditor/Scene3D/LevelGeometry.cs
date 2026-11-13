using System;
using System.Collections.Generic;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

/// <summary>
/// What a trace is told of the surfaces it meets (a BSP node's plane as it crosses it, a mesh's triangle as it hits it). A struct type argument, so that a
/// trace with no use for them (<see cref="IgnoresNormals"/>, every visibility trace) is compiled without them
/// </summary>
internal interface ITraceNormals
{
    void Met(Vector3 normal);
}

internal struct IgnoresNormals : ITraceNormals
{
    public readonly void Met(Vector3 normal) { }
}

/// <summary>The last normal met, if any</summary>
internal struct LastNormal : ITraceNormals
{
    public Vector3? Normal;
    public void Met(Vector3 normal) => Normal = normal;
}

/// <summary>
/// The level's static geometry, for testing whether a line is blocked, as UE3's line checks against level geometry do for light environments' visibility traces:
/// the levels' BSP, and a BVH of mesh instances, each with its mesh's serialized kDOP tree in local space
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
    /// A mesh's collision triangles and serialized kDOP tree, in local space
    /// </summary>
    public sealed class TriangleMesh
    {
        private readonly StaticMeshCollisionTree Tree;

        public readonly Vector3 BoundsMin;
        public readonly Vector3 BoundsMax;

        /// <summary>
        /// The triangles of <paramref name="indices"/>, skipping any with an index outside <paramref name="positions"/>. Null if none are left
        /// </summary>
        public static TriangleMesh Create(Vector3[] positions, int[] indices, kDOPTreeCompact collisionTree = null)
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
            return validIndices.Count > 0 ? new TriangleMesh(positions, validIndices.ToArray(), collisionTree) : null;
        }

        private TriangleMesh(Vector3[] positions, int[] indices, kDOPTreeCompact collisionTree)
        {
            Vector3 min = new(float.MaxValue), max = new(float.MinValue);
            foreach (int index in indices)
            {
                min = Vector3.Min(min, positions[index]);
                max = Vector3.Max(max, positions[index]);
            }
            (BoundsMin, BoundsMax) = (min, max);
            Tree = new StaticMeshCollisionTree(positions, indices, BoundsMin, BoundsMax, collisionTree);
        }

        /// <summary>
        /// Whether the segment from <paramref name="start"/> to <paramref name="end"/> hits a triangle from either side, contacts at its ends included
        /// </summary>
        public bool IntersectsSegment(Vector3 start, Vector3 end) => Tree.Intersects(start, end);

        /// <summary>The normal (unnormalized, in local space) of a triangle the segment hits, or null if it hits none</summary>
        public Vector3? HitNormal(Vector3 start, Vector3 end) => Tree.HitNormal(start, end);
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
            //The octree culls by component bounds before each mesh tests its root bounds (LE1 RVA 0x452160)
            BoxSphereBounds bounds = primitive.GetBounds();
            mins.Add(bounds.Origin - bounds.BoxExtent);
            maxs.Add(bounds.Origin + bounds.BoxExtent);
        }
        Instances = instances.ToArray();
        InstanceHierarchy = new Bvh(mins.ToArray(), maxs.ToArray());
    }

    /// <summary>
    /// Whether a light environment's visibility trace to <paramref name="light"/>, from <paramref name="start"/> to <paramref name="end"/>, is blocked by the level's geometry:
    /// the meshes that block traces to that light (<see cref="MeshStaticLighting.BlocksVisibilityTraceTo"/>), except <paramref name="ignoredActor"/>'s.
    /// With no light, every mesh blocks it
    /// </summary>
    public bool IsSegmentBlocked(Vector3 start, Vector3 end, SceneLight light, ExportEntry ignoredActor)
    {
        if (start == end)
        {
            return false;
        }
        //UWorld::BSPLineCheck: every level's BSP, whichever light it is (TRACE_Level)
        foreach (Model model in LevelModels)
        {
            var ignored = new IgnoresNormals();
            if (model.Nodes.Length > 0 && !IsOutsideAlong(model, 0, end, start, model.RootOutside, ref ignored))
            {
                return true;
            }
        }
        return InstanceHierarchy.AnyHit(start, end, (index, _, _) =>
        {
            Instance instance = Instances[index];
            if (ignoredActor is not null && instance.Owner.OwnerActor == ignoredActor || light is not null && !instance.Owner.BlocksVisibilityTraceTo(light))
            {
                return false;
            }
            return instance.Mesh.IntersectsSegment(Vector3.Transform(start, instance.WorldToLocal), Vector3.Transform(end, instance.WorldToLocal));
        });
    }

    /// <summary>
    /// Where the segment from <paramref name="start"/> to <paramref name="end"/> first meets the level's geometry (its BSP and every mesh in it, whichever
    /// lights they block), as a fraction of the way along it; null if it meets none. Found by halving the part of the segment it's known to be in
    /// </summary>
    /// <param name="normal">The unit normal of the surface met there, facing back along the segment, as a line check gives it (UE3's FCheckResult::Normal);
    /// null if it can't be told (as when the segment starts inside the BSP's solid space)</param>
    public float? FirstHit(Vector3 start, Vector3 end, out Vector3? normal)
    {
        normal = null;
        if (!IsSegmentBlocked(start, end, null, null)) return null;
        float clear = 0, blocked = 1;
        //to a float's precision: 2^-24 of the segment, a 64th of a unit along the longest placement ray (HALF_WORLD_MAX)
        for (int i = 0; i < 24; i++)
        {
            float middle = (clear + blocked) / 2;
            if (IsSegmentBlocked(start, Vector3.Lerp(start, end, middle), null, null)) blocked = middle;
            else clear = middle;
        }
        // What's met is between the last clear point and the first blocked one, give or take the triangle test's tolerance: a tenth of a unit either side
        Vector3 direction = Vector3.Normalize(end - start);
        normal = SurfaceNormal(Vector3.Lerp(start, end, clear) - direction * 0.1f, Vector3.Lerp(start, end, blocked) + direction * 0.1f, direction);
        return blocked;
    }

    // The unit normal of a surface the short segment from start to end crosses, facing back along it; null if none is found. The BSP's is taken first,
    // then the first mesh triangle found, not the nearest: a mesh within the segment's 0.2 units of a wall at an angle to it gives the wall's
    private Vector3? SurfaceNormal(Vector3 start, Vector3 end, Vector3 direction)
    {
        foreach (Model model in LevelModels)
        {
            var crossed = new LastNormal();
            if (model.Nodes.Length > 0 && !IsOutsideAlong(model, 0, end, start, model.RootOutside, ref crossed) && crossed.Normal is { } plane) return Facing(plane);
        }
        Vector3? found = null;
        InstanceHierarchy.AnyHit(start, end, (index, _, _) =>
        {
            Instance instance = Instances[index];
            //a local normal's world direction is by the inverse transpose of the local-to-world transform
            found = instance.Mesh.HitNormal(Vector3.Transform(start, instance.WorldToLocal), Vector3.Transform(end, instance.WorldToLocal)) is { } local
                ? Vector3.TransformNormal(local, Matrix4x4.Transpose(instance.WorldToLocal)) : null;
            return found is not null;
        });
        return found is { } normal ? Facing(normal) : null;

        Vector3? Facing(Vector3 normal)
        {
            Vector3 unit = Vector3.Normalize(normal);
            if (!float.IsFinite(unit.X) || !float.IsFinite(unit.Y) || !float.IsFinite(unit.Z)) return null;
            return Vector3.Dot(unit, direction) > 0 ? -unit : unit;
        }
    }

    //BSP node flags (UE3's EBspNodeFlags)
    private const byte NF_NotCsg = 0x01;
    private const byte NF_IsNew = 0x20;

    /// <summary>
    /// UE3's recursive minion of UModel::LineCheck, for a line (no extent) in world space: walks the BSP's solid and empty space along the segment.
    /// False if it enters solid space, which blocks it
    /// </summary>
    /// <param name="outside">Whether the space the segment starts in is empty</param>
    /// <param name="crossings">Told the plane of each node it crosses, in order: the last is where it entered solid space, if it returns false having crossed one</param>
    private static bool IsOutsideAlong<TNormals>(Model model, int iNode, Vector3 end, Vector3 start, bool outside, ref TNormals crossings)
        where TNormals : struct, ITraceNormals
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
                if (!IsOutsideAlong(model, frontFirst ? node.iFront : node.iBack, middle, start, ChildOutside(node, frontFirst, outside), ref crossings))
                {
                    return false;
                }
                crossings.Met(node.Plane.Normal);
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
