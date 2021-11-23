using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

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

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            var uIndexes = new List<(UIndex, string)>();

            uIndexes.AddRange(WeightedTextureMaps.Select((u, i) => (u, $"WeightedTextureMaps[{i}]")));
            uIndexes.AddRange(CachedTerrainMaterials.SelectMany((mat, i) =>
            {
                return mat.GetUIndexes(game).Select(tuple => (tuple.Item1, $"CachedTerrainMaterials[{i}].{tuple.Item2}"));
            }));
            if (game != MEGame.ME1 && game != MEGame.UDK)
            {
                uIndexes.AddRange(CachedTerrainMaterials2.SelectMany((mat, i) =>
                {
                    return mat.GetUIndexes(game).Select(tuple => (tuple.Item1, $"CachedTerrainMaterials2[{i}].{tuple.Item2}"));
                }));
            }
            return uIndexes;
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

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex uIndex, string)> uIndexes = base.GetUIndexes(game);
            uIndexes.Add((Terrain, nameof(Terrain)));
            return uIndexes;
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