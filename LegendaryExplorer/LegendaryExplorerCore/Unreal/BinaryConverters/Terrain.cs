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
        public TerrainMaterialResource[] CachedTerrainMaterials2;//not ME1, UDK
        public byte[] CachedDisplacements;//not ME1 and not UDK
        public float MaxCollisionDisplacement;//not ME1 and not UDK

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref Heights);
            sc.Serialize(ref InfoData, sc.Serialize);
            sc.Serialize(ref AlphaMaps, sc.Serialize);
            sc.Serialize(ref WeightedTextureMaps, sc.Serialize);
            sc.Serialize(ref CachedTerrainMaterials, sc.Serialize);
            if (sc.Game != MEGame.ME1 && sc.Game != MEGame.UDK)
            {
                // UDK doesn't serialize a second shader model 
                sc.Serialize(ref CachedTerrainMaterials2, sc.Serialize);
                sc.Serialize(ref CachedDisplacements);
                sc.Serialize(ref MaxCollisionDisplacement);
            }

            // Old code, left for posterity 08/23/2023
            //if (sc.Game != MEGame.ME1 && sc.Game != MEGame.UDK)
            //{
            //    sc.Serialize(ref CachedDisplacements);
            //    sc.Serialize(ref MaxCollisionDisplacement);
            //}
        }

        public static Terrain Create()
        {
            return new()
            {
                Heights = [],
                InfoData = [],
                AlphaMaps = [],
                WeightedTextureMaps = [],
                CachedTerrainMaterials = [],
                CachedTerrainMaterials2 = [],
                CachedDisplacements = [],
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
            if (game != MEGame.ME1 && game != MEGame.UDK)
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
        public bool bEnableSpecular; // UDK

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

    public partial class SerializingContainer
    {
        public void Serialize(ref TerrainInfoFlags flags)
        {
            byte b = (byte)flags;
            Serialize(ref b);
            flags = (TerrainInfoFlags)b;
        }
        public void Serialize(ref AlphaMap map)
        {
            if (IsLoading)
            {
                map = new AlphaMap();
            }
            Serialize(ref map.Data);
        }
        public void Serialize(ref TerrainMaterialMask mask)
        {
            if (IsLoading)
            {
                mask = new TerrainMaterialMask();
            }
            Serialize(ref mask.NumBits);
            Serialize(ref mask.BitMask);
        }
        public void Serialize(ref TerrainMaterialResource mres)
        {
            if (IsLoading)
            {
                mres = new TerrainMaterialResource();
            }

            // Original code serializes it as MaterialResource
            //MaterialResource materialResource = mres;
            //Serialize(ref materialResource);


            Serialize(ref mres.CompileErrors, Serialize);
            Serialize(ref mres.TextureDependencyLengthMap, Serialize, Serialize);
            Serialize(ref mres.MaxTextureDependencyLength);
            Serialize(ref mres.ID);
            Serialize(ref mres.NumUserTexCoords);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref mres.UniformExpressionTextures, Serialize);
            }
            else
            {
                Serialize(ref mres.UniformPixelVectorExpressions, Serialize);
                Serialize(ref mres.UniformPixelScalarExpressions, Serialize);
                Serialize(ref mres.Uniform2DTextureExpressions, Serialize);
                Serialize(ref mres.UniformCubeTextureExpressions, Serialize);

                if (IsLoading)
                {
                    mres.UniformExpressionTextures = mres.Uniform2DTextureExpressions.Select(texExpr => texExpr.TextureIndex).ToArray();
                }
            }
            Serialize(ref mres.bUsesSceneColor);
            Serialize(ref mres.bUsesSceneDepth);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref mres.bUsesDynamicParameter);
                Serialize(ref mres.bUsesLightmapUVs);
                Serialize(ref mres.bUsesMaterialVertexPositionOffset);
                if (Game == MEGame.ME3 || Game.IsLEGame())
                {
                    Serialize(ref mres.unkBool1);
                }
            }
            Serialize(ref mres.UsingTransforms);
            if (Game == MEGame.ME1)
            {
                Serialize(ref mres.Me1MaterialUniformExpressionsList, Serialize);
            }
            else
            {
                Serialize(ref mres.TextureLookups, Serialize);
                Serialize(ref mres.DummyDroppedFallbackComponents);

                // TERRAIN MATERIAL RESOURCE SPECIFIC ============================================
                // If we are porting a terrain, these are NOT used in it's CachedMaterials!
                // This will break porting from UDK
                //if (Game == MEGame.UDK) // These are not used in Terrain Cached Materials!
                //{
                //    Serialize(ref mres.udkUnk2);
                //    Serialize(ref mres.udkUnk3);
                //    Serialize(ref mres.udkUnk4);
                //}
                // END TERRAIN MATERIAL RESOURCE SPECIFIC ========================================
            }
            if (Game == MEGame.ME1)
            {
                Serialize(ref mres.unk1);
                int tmp = mres.unkCount;
                Serialize(ref tmp);
                mres.unkCount = tmp; //will create mr.unkList of unkCount size
                Serialize(ref mres.unkInt2);
                for (int i = 0; i < mres.unkCount; i++)
                {
                    Serialize(ref mres.unkList[i].Item1);
                    Serialize(ref mres.unkList[i].Item2);
                    Serialize(ref mres.unkList[i].Item3);
                }
            }

            // Extra data on terrain material
            Serialize(ref mres.Terrain);
            Serialize(ref mres.Mask);
            Serialize(ref mres.MaterialIds, Serialize);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref mres.LightingGuid);
            }

            if (Game == MEGame.UDK)
            {
                Serialize(ref mres.bEnableSpecular);
            }
        }
    }
}