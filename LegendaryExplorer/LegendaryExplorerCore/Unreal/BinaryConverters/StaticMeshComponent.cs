using System;
using LegendaryExplorerCore.Packages;
using System.Numerics;
using System.Runtime.CompilerServices;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class StaticMeshComponent : ObjectBinary
    {
        public StaticMeshComponentLODInfo[] LODData;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref LODData, SCExt.Serialize);
        }

        public static StaticMeshComponent Create()
        {
            return new()
            {
                LODData = Array.Empty<StaticMeshComponentLODInfo>()
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            for (int i = 0; i < LODData.Length; i++)
            {
                StaticMeshComponentLODInfo lodInfo = LODData[i];
                ForEachUIndexInSpan(action, lodInfo.ShadowMaps.AsSpan(), $"LODData[{i}].ShadowMaps");
                ForEachUIndexInSpan(action, lodInfo.ShadowVertexBuffers.AsSpan(), $"LODData[{i}].ShadowVertexBuffers");
                lodInfo.LightMap.ForEachUIndex(game, action, $"LODData[{i}].");
            }
        }
    }

    public class StaticMeshComponentLODInfo
    {
        public UIndex[] ShadowMaps;
        public UIndex[] ShadowVertexBuffers;
        public LightMap LightMap;
        public byte bLoadVertexColorData; //ME3
        public ColorVertexBuffer OverrideVertexColors; //ME3, only serialized if bLoadVertexColorData == 1
    }

    public enum ELightMapType
    {
        LMT_None,
        LMT_1D,
        LMT_2D,
        //ME3 only after this
        LMT_3, //speculative name. No idea what the ones after LMT_2D are actually called 
        LMT_4,
        LMT_5,
        LMT_6
    }

    public class LightMap
    {
        public ELightMapType LightMapType;

        public void ForEachUIndex<TAction>(MEGame game, in TAction action, string prefix = "") where TAction : struct, IUIndexAction
        {
            switch (this)
            {
                case LightMap_1D lightMap1D:
                    Unsafe.AsRef(in action).Invoke(ref lightMap1D.Owner, $"{prefix}LightMap.Owner");
                    break;
                case LightMap_2D lightMap2D:
                    Unsafe.AsRef(in action).Invoke(ref lightMap2D.Texture1, $"{prefix}LightMap.Texture1");
                    Unsafe.AsRef(in action).Invoke(ref lightMap2D.Texture2, $"{prefix}LightMap.Texture2");
                    Unsafe.AsRef(in action).Invoke(ref lightMap2D.Texture3, $"{prefix}LightMap.Texture3");
                    if (game < MEGame.ME3)
                    {
                        Unsafe.AsRef(in action).Invoke(ref lightMap2D.Texture4, $"{prefix}LightMap.Texture4");
                    }
                    break;
                case LightMap_4or6 lightMap4Or6:
                    Unsafe.AsRef(in action).Invoke(ref lightMap4Or6.Texture1, $"{prefix}LightMap.Texture1");
                    Unsafe.AsRef(in action).Invoke(ref lightMap4Or6.Texture2, $"{prefix}LightMap.Texture2");
                    Unsafe.AsRef(in action).Invoke(ref lightMap4Or6.Texture3, $"{prefix}LightMap.Texture3");
                    break;
            }
        }
    }

    public class LightMap_1D : LightMap
    {
        public Guid[] LightGuids;
        public UIndex Owner;
        public QuantizedDirectionalLightSample[] DirectionalSamples; //BULKDATA
        public Vector3 ScaleVector1;
        public Vector3 ScaleVector2;
        public Vector3 ScaleVector3;
        public Vector3 ScaleVector4;//< ME3
        public QuantizedSimpleLightSample[] SimpleSamples; //BULKDATA
    }

    public class LightMap_2D : LightMap
    {
        public Guid[] LightGuids;
        public UIndex Texture1;
        public Vector3 ScaleVector1;
        public UIndex Texture2;
        public Vector3 ScaleVector2;
        public UIndex Texture3;
        public Vector3 ScaleVector3;
        public UIndex Texture4;//< ME3
        public Vector3 ScaleVector4;//< ME3
        public Vector2 CoordinateScale;
        public Vector2 CoordinateBias;
    }

    public class LightMap_3 : LightMap
    {
        public Guid[] LightGuids;
        public int unkInt;
        public QuantizedDirectionalLightSample[] DirectionalSamples; //BULKDATA
        public Vector3 unkVector1;
        public Vector3 unkVector2;
    }

    public class LightMap_4or6 : LightMap
    {
        public Guid[] LightGuids;
        public UIndex Texture1;
        public float[] unkFloats1 = new float[8];
        public UIndex Texture2;
        public float[] unkFloats2 = new float[8];
        public UIndex Texture3;
        public float[] unkFloats3 = new float[8];
        public Vector2 CoordinateScale;
        public Vector2 CoordinateBias;
    }

    public class LightMap_5 : LightMap
    {
        public Guid[] LightGuids;
        public int unkInt;
        public QuantizedSimpleLightSample[] SimpleSamples; //BULKDATA
        public Vector3 unkVector;
    }

    public class QuantizedDirectionalLightSample
    {
        public SharpDX.Color Coefficient1;//not ME3
        public SharpDX.Color Coefficient2;
        public SharpDX.Color Coefficient3;
    }

    public class QuantizedSimpleLightSample
    {
        public SharpDX.Color Coefficient;
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref QuantizedSimpleLightSample samp)
        {
            if (sc.IsLoading)
            {
                samp = new QuantizedSimpleLightSample();
            }

            sc.Serialize(ref samp.Coefficient);
        }
        public static void Serialize(this SerializingContainer2 sc, ref QuantizedDirectionalLightSample samp)
        {
            if (sc.IsLoading)
            {
                samp = new QuantizedDirectionalLightSample();
            }

            if (sc.Game < MEGame.ME3)
            {
                sc.Serialize(ref samp.Coefficient1);
            }
            sc.Serialize(ref samp.Coefficient2);
            sc.Serialize(ref samp.Coefficient3);
        }
        public static void Serialize(this SerializingContainer2 sc, ref LightMap lmap)
        {
            if (sc.IsLoading)
            {
                var type = (ELightMapType)sc.ms.ReadInt32();
                switch (type)
                {
                    case ELightMapType.LMT_None:
                        lmap = new LightMap();
                        break;
                    case ELightMapType.LMT_1D:
                        lmap = new LightMap_1D();
                        break;
                    case ELightMapType.LMT_2D:
                        lmap = new LightMap_2D();
                        break;
                    case ELightMapType.LMT_3:
                        lmap = new LightMap_3();
                        break;
                    case ELightMapType.LMT_4:
                    case ELightMapType.LMT_6:
                        lmap = new LightMap_4or6();
                        break;
                    case ELightMapType.LMT_5:
                        lmap = new LightMap_5();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                lmap.LightMapType = type;
            }
            else
            {
                if (!sc.Game.IsGame3() && lmap.LightMapType > ELightMapType.LMT_2D)
                {
                    lmap = new LightMap();
                }
                sc.ms.Writer.WriteInt32((int)lmap.LightMapType);
            }

            switch (lmap.LightMapType)
            {
                case ELightMapType.LMT_None:
                    break;
                case ELightMapType.LMT_1D:
                    sc.Serialize((LightMap_1D)lmap);
                    break;
                case ELightMapType.LMT_2D:
                    sc.Serialize((LightMap_2D)lmap);
                    break;
                case ELightMapType.LMT_3:
                    sc.Serialize((LightMap_3)lmap);
                    break;
                case ELightMapType.LMT_4:
                case ELightMapType.LMT_6:
                    sc.Serialize((LightMap_4or6)lmap);
                    break;
                case ELightMapType.LMT_5:
                    sc.Serialize((LightMap_5)lmap);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        private static void Serialize(this SerializingContainer2 sc, LightMap_1D lmap)
        {
            sc.Serialize(ref lmap.LightGuids, SCExt.Serialize);
            sc.Serialize(ref lmap.Owner);
            sc.SerializeBulkData(ref lmap.DirectionalSamples, Serialize);
            sc.Serialize(ref lmap.ScaleVector1);
            sc.Serialize(ref lmap.ScaleVector2);
            sc.Serialize(ref lmap.ScaleVector3);
            if (sc.Game < MEGame.ME3)
            {
                sc.Serialize(ref lmap.ScaleVector4);
            }
            sc.SerializeBulkData(ref lmap.SimpleSamples, Serialize);
        }
        private static void Serialize(this SerializingContainer2 sc, LightMap_2D lmap)
        {
            sc.Serialize(ref lmap.LightGuids, SCExt.Serialize);
            sc.Serialize(ref lmap.Texture1);
            sc.Serialize(ref lmap.ScaleVector1);
            sc.Serialize(ref lmap.Texture2);
            sc.Serialize(ref lmap.ScaleVector2);
            sc.Serialize(ref lmap.Texture3);
            sc.Serialize(ref lmap.ScaleVector3);
            if (sc.Game < MEGame.ME3)
            {
                sc.Serialize(ref lmap.Texture4);
                sc.Serialize(ref lmap.ScaleVector4);
            }
            else if (sc.IsLoading)
            {
                lmap.Texture4 = 0;
            }
            sc.Serialize(ref lmap.CoordinateScale);
            sc.Serialize(ref lmap.CoordinateBias);
        }
        private static void Serialize(this SerializingContainer2 sc, LightMap_3 lmap)
        {
            sc.Serialize(ref lmap.LightGuids, SCExt.Serialize);
            sc.Serialize(ref lmap.unkInt);
            sc.SerializeBulkData(ref lmap.DirectionalSamples, Serialize);
            sc.Serialize(ref lmap.unkVector1);
            sc.Serialize(ref lmap.unkVector2);
        }
        private static void Serialize(this SerializingContainer2 sc, LightMap_4or6 lmap)
        {
            sc.Serialize(ref lmap.LightGuids, SCExt.Serialize);
            sc.Serialize(ref lmap.Texture1);
            for (int i = 0; i < 8; i++)
            {
                sc.Serialize(ref lmap.unkFloats1[i]);
            }
            sc.Serialize(ref lmap.Texture2);
            for (int i = 0; i < 8; i++)
            {
                sc.Serialize(ref lmap.unkFloats2[i]);
            }
            sc.Serialize(ref lmap.Texture3);
            for (int i = 0; i < 8; i++)
            {
                sc.Serialize(ref lmap.unkFloats3[i]);
            }
            sc.Serialize(ref lmap.CoordinateScale);
            sc.Serialize(ref lmap.CoordinateBias);
        }
        private static void Serialize(this SerializingContainer2 sc, LightMap_5 lmap)
        {
            sc.Serialize(ref lmap.LightGuids, SCExt.Serialize);
            sc.Serialize(ref lmap.unkInt);
            sc.SerializeBulkData(ref lmap.SimpleSamples, Serialize);
            sc.Serialize(ref lmap.unkVector);
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticMeshComponentLODInfo lod)
        {
            if (sc.IsLoading)
            {
                lod = new StaticMeshComponentLODInfo();
            }

            sc.Serialize(ref lod.ShadowMaps, Serialize);
            sc.Serialize(ref lod.ShadowVertexBuffers, Serialize);
            sc.Serialize(ref lod.LightMap);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref lod.bLoadVertexColorData);
                if (lod.bLoadVertexColorData > 0)
                {
                    sc.Serialize(ref lod.OverrideVertexColors);
                }

                if (sc.Game == MEGame.UDK)
                {
                    int dummy = 0;
                    sc.Serialize(ref dummy);
                }
            }
        }
    }
}