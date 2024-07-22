using System;
using LegendaryExplorerCore.Packages;
using System.Numerics;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class TerrainComponent : ObjectBinary
    {
        public Vector3[] CollisionVertices;
        public TerrainBVNode[] BVTree;
        public TerrainPatchBounds[] PatchBounds;
        public LightMap LightMap;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref CollisionVertices);
            sc.Serialize(ref BVTree, SCExt.Serialize);
            sc.Serialize(ref PatchBounds, SCExt.Serialize);
            sc.Serialize(ref LightMap);
        }

        public static TerrainComponent Create()
        {
            return new()
            {
                CollisionVertices = Array.Empty<Vector3>(),
                BVTree = Array.Empty<TerrainBVNode>(),
                PatchBounds = Array.Empty<TerrainPatchBounds>(),
                LightMap = new LightMap()
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            LightMap.ForEachUIndex(game, action);
        }
    }

    public class TerrainBVNode
    {
        public Box BoundingVolume;
        public bool bIsLeaf;
        //either node indexes or pos and size depending on bIsLeaf
        public ushort NodeIndex0;
        public ushort NodeIndex1;
        public ushort NodeIndex2;
        public ushort NodeIndex3;
        public float unk;
        public ushort XPos
        {
            get => NodeIndex0;
            set => NodeIndex0 = value;
        }
        public ushort YPos
        {
            get => NodeIndex1;
            set => NodeIndex1 = value;
        }
        public ushort XSize
        {
            get => NodeIndex2;
            set => NodeIndex2 = value;
        }
        public ushort YSize
        {
            get => NodeIndex3;
            set => NodeIndex3 = value;
        }
    }

    public class TerrainPatchBounds
    {
        public float MinHeight;
        public float MaxHeight;
        public float MaxDisplacement;
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref TerrainBVNode node)
        {
            if (sc.IsLoading)
            {
                node = new TerrainBVNode();
            }
            sc.Serialize(ref node.BoundingVolume);
            sc.Serialize(ref node.bIsLeaf);
            sc.Serialize(ref node.NodeIndex0);
            sc.Serialize(ref node.NodeIndex1);
            sc.Serialize(ref node.NodeIndex2);
            sc.Serialize(ref node.NodeIndex3);
            if (sc.Game != MEGame.UDK)
            {
                sc.Serialize(ref node.unk);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref TerrainPatchBounds bounds)
        {
            if (sc.IsLoading)
            {
                bounds = new TerrainPatchBounds();
            }
            sc.Serialize(ref bounds.MinHeight);
            sc.Serialize(ref bounds.MaxHeight);
            sc.Serialize(ref bounds.MaxDisplacement);
        }
    }
}