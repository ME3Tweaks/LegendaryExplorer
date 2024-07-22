using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class Terrain : ObjectBinary
    {
        public ushort[] Heights;
        public TerrainInfoFlags[] InfoData;
        public AlphaMap[] AlphaMaps;
        public UIndex[] WeightedTextureMaps;
        public TerrainMaterialResource[] CachedTerrainMaterials;
        public TerrainMaterialResource[] CachedTerrainMaterials2;//not ME1
        public byte[] CachedDisplacements;//not ME1 and not UDK
        public float MaxCollisionDisplacement;//not ME1 and not UDK

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref Heights);
            sc.Serialize(ref InfoData, SCExt.Serialize);
            sc.Serialize(ref AlphaMaps, SCExt.Serialize);
            sc.Serialize(ref WeightedTextureMaps, SCExt.Serialize);
            sc.Serialize(ref CachedTerrainMaterials, SCExt.Serialize);
            if (sc.Game != MEGame.ME1)
            {
                sc.Serialize(ref CachedTerrainMaterials2, SCExt.Serialize);
            }
            if (sc.Game != MEGame.ME1 && sc.Game != MEGame.UDK)
            {
                sc.Serialize(ref CachedDisplacements);
                sc.Serialize(ref MaxCollisionDisplacement);
            }
        }

        public static Terrain Create()
        {
            return new()
            {
                Heights = Array.Empty<ushort>(),
                InfoData = Array.Empty<TerrainInfoFlags>(),
                AlphaMaps = Array.Empty<AlphaMap>(),
                WeightedTextureMaps = Array.Empty<UIndex>(),
                CachedTerrainMaterials = Array.Empty<TerrainMaterialResource>(),
                CachedTerrainMaterials2 = Array.Empty<TerrainMaterialResource>(),
                CachedDisplacements = Array.Empty<byte>(),
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            ForEachUIndexInSpan(action, WeightedTextureMaps.AsSpan(), nameof(WeightedTextureMaps));
            for (int i = 0; i < CachedTerrainMaterials.Length; i++)
            {
                CachedTerrainMaterials[i].ForEachUIndex(game, action, $"CachedTerrainMaterials[{i}].");
            }
            if (game is not MEGame.ME1 && game is not MEGame.UDK)
            {
                for (int i = 0; i < CachedTerrainMaterials2.Length; i++)
                {
                    CachedTerrainMaterials[i].ForEachUIndex(game, action, $"CachedTerrainMaterials2[{i}].");
                }
            }
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(CachedTerrainMaterials.SelectMany((mat, i) =>
            {
                return mat.GetNames(game).Select(tuple => (tuple.Item1, $"CachedTerrainMaterials[{i}].{tuple.Item2}"));
            }));
            if (game != MEGame.ME1)
            {
                names.AddRange(CachedTerrainMaterials2.SelectMany((mat, i) =>
                {
                    return mat.GetNames(game).Select(tuple => (tuple.Item1, $"CachedTerrainMaterials2[{i}].{tuple.Item2}"));
                }));
            }

            return names;
        }
    }

    [Flags]
    public enum TerrainInfoFlags : byte
    {
        TID_None = 0,
        TID_Visibility_Off = 0x0001,
        TID_OrientationFlip = 0x0002,
        TID_Unreachable = 0x0004,
        TID_Locked = 0x0008,
    }

    public class AlphaMap
    {
        public byte[] Data;
    }

    public class TerrainMaterialResource : MaterialResource
    {
        public UIndex Terrain;
        public TerrainMaterialMask Mask;
        public Guid[] MaterialIds;
        public Guid LightingGuid; // >= ME3

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action, string prefix)
        {
            base.ForEachUIndex(game, in action, prefix);
            Unsafe.AsRef(in action).Invoke(ref Terrain, $"{prefix}Terrain");
        }
    }

    public class TerrainMaterialMask
    {
        public int NumBits;
        public ulong BitMask;
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref TerrainInfoFlags flags)
        {
            byte b = (byte)flags;
            sc.Serialize(ref b);
            flags = (TerrainInfoFlags)b;
        }
        public static void Serialize(this SerializingContainer2 sc, ref AlphaMap map)
        {
            if (sc.IsLoading)
            {
                map = new AlphaMap();
            }
            sc.Serialize(ref map.Data);
        }
        public static void Serialize(this SerializingContainer2 sc, ref TerrainMaterialMask mask)
        {
            if (sc.IsLoading)
            {
                mask = new TerrainMaterialMask();
            }
            sc.Serialize(ref mask.NumBits);
            sc.Serialize(ref mask.BitMask);
        }
        public static void Serialize(this SerializingContainer2 sc, ref TerrainMaterialResource mat)
        {
            if (sc.IsLoading)
            {
                mat = new TerrainMaterialResource();
            }

            MaterialResource materialResource = mat;
            sc.Serialize(ref materialResource);

            sc.Serialize(ref mat.Terrain);
            sc.Serialize(ref mat.Mask);
            sc.Serialize(ref mat.MaterialIds, SCExt.Serialize);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref mat.LightingGuid);
            }
        }
    }
}