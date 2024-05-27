using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class DecalComponent : ObjectBinary
    {
        public StaticReceiverData[] StaticReceivers;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref StaticReceivers, SCExt.Serialize);
        }

        public static DecalComponent Create()
        {
            return new()
            {
                StaticReceivers = Array.Empty<StaticReceiverData>()
            };
        }
        
        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            for (int i = 0; i < StaticReceivers.Length; i++)
            {
                StaticReceiverData data = StaticReceivers[i];
                Unsafe.AsRef(in action).Invoke(ref data.PrimitiveComponent, $"StaticReceiver[{i}].PrimitiveComponent");
                if (game >= MEGame.ME3)
                {
                    ForEachUIndexInSpan(action, data.ShadowMap1D.AsSpan(), $"StaticReceiver[{i}].ShadowMap1D");
                }
            }
        }
    }

    public class StaticReceiverData
    {
        public UIndex PrimitiveComponent;
        public DecalVertex[] Vertices;
        public ushort[] Indices;
        public uint NumTriangles;
        public LightMap LightMap;
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

    public static partial class SCExt
    {
        public static void  Serialize(this SerializingContainer2 sc, ref DecalVertex vert)
        {
            if (sc.IsLoading)
            {
                vert = new DecalVertex();
            }
            sc.Serialize(ref vert.Position);
            sc.Serialize(ref vert.TangentX);
            sc.Serialize(ref vert.TangentZ);
            if (sc.Game < MEGame.ME3)
            {
                sc.Serialize(ref vert.ProjectedUVs);
            }
            sc.Serialize(ref vert.LightMapCoordinate);
            if (sc.Game < MEGame.ME3)
            {
                sc.Serialize(ref vert.NormalTransform1);
                sc.Serialize(ref vert.NormalTransform2);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticReceiverData dat)
        {
            if (sc.IsLoading)
            {
                dat = new StaticReceiverData();
            }
            sc.Serialize(ref dat.PrimitiveComponent);
            sc.BulkSerialize(ref dat.Vertices, Serialize, sc.Game >= MEGame.ME3 ? 28 : 52);
            sc.BulkSerialize(ref dat.Indices, Serialize, 2);
            sc.Serialize(ref dat.NumTriangles);
            sc.Serialize(ref dat.LightMap);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref dat.ShadowMap1D, Serialize);
                sc.Serialize(ref dat.Data);
                sc.Serialize(ref dat.InstanceIndex);
            }
        }
    }
}