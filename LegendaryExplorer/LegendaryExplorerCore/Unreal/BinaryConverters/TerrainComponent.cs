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

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref CollisionVertices);
            sc.Serialize(ref BVTree, sc.Serialize);
            sc.Serialize(ref PatchBounds, sc.Serialize);
            sc.Serialize(ref LightMap);
        }

        public static TerrainComponent Create()
        {
            return new()
            {
                CollisionVertices = [],
                BVTree = [],
                PatchBounds = [],
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

    public partial class SerializingContainer
    {
        public void Serialize(ref TerrainBVNode node)
        {
            if (IsLoading)
            {
                node = new TerrainBVNode();
            }
            Serialize(ref node.BoundingVolume);
            Serialize(ref node.bIsLeaf);
            Serialize(ref node.NodeIndex0);
            Serialize(ref node.NodeIndex1);
            Serialize(ref node.NodeIndex2);
            Serialize(ref node.NodeIndex3);
            if (Game != MEGame.UDK)
            {
                Serialize(ref node.unk);
            }
        }
        public void Serialize(ref TerrainPatchBounds bounds)
        {
            if (IsLoading)
            {
                bounds = new TerrainPatchBounds();
            }
            Serialize(ref bounds.MinHeight);
            Serialize(ref bounds.MaxHeight);
            Serialize(ref bounds.MaxDisplacement);
        }
    }
}