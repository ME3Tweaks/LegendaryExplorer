using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public partial class DecalComponent : ObjectBinary
    {
        [UIndexRefContainer]
        public StaticReceiverData[] StaticReceivers;

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref StaticReceivers, sc.Serialize);
        }

        public static DecalComponent Create()
        {
            return new()
            {
                StaticReceivers = []
            };
        }
        
        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            for (int i = 0; i < StaticReceivers.Length; i++)
            {
                StaticReceiverData data = StaticReceivers[i];
                Unsafe.AsRef(in action).Invoke(ref data.Component, $"StaticReceiver[{i}].Component");
                if (game >= MEGame.ME3)
                {
                    ForEachUIndexInSpan(action, data.ShadowMap1D.AsSpan(), $"StaticReceiver[{i}].ShadowMap1D");
                }
            }
        }
    }

    public partial class StaticReceiverData
    {
        [UIndexRef("PrimitiveComponent")]
        public UIndex Component;
        public DecalVertex[] Vertices;
        public ushort[] Indices;
        public uint NumTriangles;
        [UIndexRefContainer]
        public LightMap LightMap;
        [UIndexRef("ShadowMap1D")]
        public UIndex[] ShadowMap1D;//ME3/LE
        public int Data;//ME3/LE
        public int InstanceIndex;//ME3/LE
    }

    public class DecalVertex
    {
        public Vector3 Position;
        public PackedNormal TangentX;
        public PackedNormal TangentZ;
        public Vector2 ProjectedUVs;//< ME3
        public Vector2 LightMapCoordinate;
        public Vector2 NormalTransform1;//< ME3
        public Vector2 NormalTransform2;//< ME3
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref DecalVertex vert)
        {
            if (IsLoading)
            {
                vert = new DecalVertex();
            }
            Serialize(ref vert.Position);
            Serialize(ref vert.TangentX);
            Serialize(ref vert.TangentZ);
            if (Game < MEGame.ME3)
            {
                Serialize(ref vert.ProjectedUVs);
            }
            Serialize(ref vert.LightMapCoordinate);
            if (Game < MEGame.ME3)
            {
                Serialize(ref vert.NormalTransform1);
                Serialize(ref vert.NormalTransform2);
            }
        }
        public void Serialize(ref StaticReceiverData dat)
        {
            if (IsLoading)
            {
                dat = new StaticReceiverData();
            }
            Serialize(ref dat.Component);
            BulkSerialize(ref dat.Vertices, Serialize, Game >= MEGame.ME3 ? 28 : 52);
            BulkSerialize(ref dat.Indices, Serialize, 2);
            Serialize(ref dat.NumTriangles);
            Serialize(ref dat.LightMap);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref dat.ShadowMap1D, Serialize);
                Serialize(ref dat.Data);
                Serialize(ref dat.InstanceIndex);
            }
        }
    }
}