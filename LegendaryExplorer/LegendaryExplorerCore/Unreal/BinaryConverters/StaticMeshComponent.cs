using System;
using LegendaryExplorerCore.Packages;
using System.Numerics;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Gammtek;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class StaticMeshComponent : ObjectBinary
    {
        public StaticMeshComponentLODInfo[] LODData;

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref LODData, sc.Serialize);
        }

        public static StaticMeshComponent Create()
        {
            return new()
            {
                LODData = []
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
        public Fixed8<float> unkFloats1;
        public UIndex Texture2;
        public Fixed8<float> unkFloats2;
        public UIndex Texture3;
        public Fixed8<float> unkFloats3;
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

    public partial class SerializingContainer
    {
        public void Serialize(ref QuantizedSimpleLightSample samp)
        {
            if (IsLoading)
            {
                samp = new QuantizedSimpleLightSample();
            }

            Serialize(ref samp.Coefficient);
        }
        public void Serialize(ref QuantizedDirectionalLightSample samp)
        {
            if (IsLoading)
            {
                samp = new QuantizedDirectionalLightSample();
            }

            if (Game < MEGame.ME3)
            {
                Serialize(ref samp.Coefficient1);
            }
            Serialize(ref samp.Coefficient2);
            Serialize(ref samp.Coefficient3);
        }
        public void Serialize(ref LightMap lmap)
        {
            if (IsLoading)
            {
                var type = (ELightMapType)ms.ReadInt32();
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
                if (!Game.IsGame3() && lmap.LightMapType > ELightMapType.LMT_2D)
                {
                    lmap = new LightMap();
                }
                ms.Writer.WriteInt32((int)lmap.LightMapType);
            }

            switch (lmap.LightMapType)
            {
                case ELightMapType.LMT_None:
                    break;
                case ELightMapType.LMT_1D:
                    Serialize((LightMap_1D)lmap);
                    break;
                case ELightMapType.LMT_2D:
                    Serialize((LightMap_2D)lmap);
                    break;
                case ELightMapType.LMT_3:
                    Serialize((LightMap_3)lmap);
                    break;
                case ELightMapType.LMT_4:
                case ELightMapType.LMT_6:
                    Serialize((LightMap_4or6)lmap);
                    break;
                case ELightMapType.LMT_5:
                    Serialize((LightMap_5)lmap);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        private void Serialize(LightMap_1D lmap)
        {
            Serialize(ref lmap.LightGuids, Serialize);
            Serialize(ref lmap.Owner);
            SerializeBulkData(ref lmap.DirectionalSamples, Serialize);
            Serialize(ref lmap.ScaleVector1);
            Serialize(ref lmap.ScaleVector2);
            Serialize(ref lmap.ScaleVector3);
            if (Game < MEGame.ME3)
            {
                Serialize(ref lmap.ScaleVector4);
            }
            SerializeBulkData(ref lmap.SimpleSamples, Serialize);
        }
        private void Serialize(LightMap_2D lmap)
        {
            Serialize(ref lmap.LightGuids, Serialize);
            Serialize(ref lmap.Texture1);
            Serialize(ref lmap.ScaleVector1);
            Serialize(ref lmap.Texture2);
            Serialize(ref lmap.ScaleVector2);
            Serialize(ref lmap.Texture3);
            Serialize(ref lmap.ScaleVector3);
            if (Game < MEGame.ME3)
            {
                Serialize(ref lmap.Texture4);
                Serialize(ref lmap.ScaleVector4);
            }
            else if (IsLoading)
            {
                lmap.Texture4 = 0;
            }
            Serialize(ref lmap.CoordinateScale);
            Serialize(ref lmap.CoordinateBias);
        }
        private void Serialize(LightMap_3 lmap)
        {
            Serialize(ref lmap.LightGuids, Serialize);
            Serialize(ref lmap.unkInt);
            SerializeBulkData(ref lmap.DirectionalSamples, Serialize);
            Serialize(ref lmap.unkVector1);
            Serialize(ref lmap.unkVector2);
        }
        private void Serialize(LightMap_4or6 lmap)
        {
            Serialize(ref lmap.LightGuids, Serialize);
            Serialize(ref lmap.Texture1);
            for (int i = 0; i < 8; i++)
            {
                Serialize(ref lmap.unkFloats1[i]);
            }
            Serialize(ref lmap.Texture2);
            for (int i = 0; i < 8; i++)
            {
                Serialize(ref lmap.unkFloats2[i]);
            }
            Serialize(ref lmap.Texture3);
            for (int i = 0; i < 8; i++)
            {
                Serialize(ref lmap.unkFloats3[i]);
            }
            Serialize(ref lmap.CoordinateScale);
            Serialize(ref lmap.CoordinateBias);
        }
        private void Serialize(LightMap_5 lmap)
        {
            Serialize(ref lmap.LightGuids, Serialize);
            Serialize(ref lmap.unkInt);
            SerializeBulkData(ref lmap.SimpleSamples, Serialize);
            Serialize(ref lmap.unkVector);
        }
        public void Serialize(ref StaticMeshComponentLODInfo lod)
        {
            if (IsLoading)
            {
                lod = new StaticMeshComponentLODInfo();
            }

            Serialize(ref lod.ShadowMaps, Serialize);
            Serialize(ref lod.ShadowVertexBuffers, Serialize);
            Serialize(ref lod.LightMap);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref lod.bLoadVertexColorData);
                if (lod.bLoadVertexColorData > 0)
                {
                    Serialize(ref lod.OverrideVertexColors);
                }

                if (Game == MEGame.UDK)
                {
                    int dummy = 0;
                    Serialize(ref dummy);
                }
            }
        }
    }
}