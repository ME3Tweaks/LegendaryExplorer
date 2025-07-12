using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using LegendaryExplorerCore.Unreal.Collections;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class ShaderCache : ObjectBinary
    {
        /// <summary>
        /// If true, names are serialized as strings and objects cannot be serialized. Used by GlobalShaderCache.
        /// </summary>
        public bool Packageless;

        /// <summary>
        /// Do not set directly; use <see cref="GlobalShaderCache"/> instead. Changes serialization of the vertex factory map.
        /// </summary>
        public bool IsGlobalShaderCache; 

        public UMultiMap<NameReference, uint> ShaderTypeCRCMap; //TODO: Make this a UMap
        public UMultiMap<Guid, Shader> Shaders; //TODO: Make this a UMap
        public UMultiMap<NameReference, uint> VertexFactoryTypeCRCMap; //TODO: Make this a UMap
        public UMultiMap<NameReference, Guid> VertexFactoryTypeGuidMap; // GlobalShaderCache
        public UMultiMap<StaticParameterSet, MaterialShaderMap> MaterialShaderMaps; //TODO: Make this a UMap

        protected override void Serialize(SerializingContainer sc)
        {
            if (!Packageless)
            {
                if (sc.Pcc.Platform != MEPackage.GamePlatform.PC) return; //We do not support non-PC shader cache
            }

            if (sc.Game == MEGame.UDK)
            {
                // Just default to 0, we aren't going to use this in ME
                int shaderCachePriority = 0;
                sc.Serialize(ref shaderCachePriority);
            }

            byte platform = sc.Game.IsLEGame() ? (byte)5 : (byte)0;
            if (sc.Game == MEGame.UDK)
            {
                // We do not support SM3 in UDK
                // Serialize as SM5
                platform = 4; // UDK SM5 is '4'
            }
            sc.Serialize(ref platform);


            sc.Serialize(ref ShaderTypeCRCMap, sc.Serialize, sc.Serialize);
            if (sc.Game == MEGame.ME3 || sc.Game.IsLEGame())
            {
                if (sc.IsLoading)
                {
                    int nameMapCount = sc.ms.ReadInt32();
                    if (Packageless)
                    {
                        for (int i = 0; i < nameMapCount; i++)
                        {
                            string str = null;
                            sc.Serialize(ref str);
                        }
                    }
                    else
                    {
                        // Size is fixed
                        sc.ms.Skip(nameMapCount * 12);
                    }
                }
                else
                {
                    // should probably keep these... it is name to crc map
                    // technically not important, but we've seen that burn 
                    // us in the past
                    sc.ms.Writer.WriteInt32(0);
                }
            }

            if (sc.Game == MEGame.ME1)
            {
                if (IsGlobalShaderCache)
                {
                    sc.Serialize(ref VertexFactoryTypeGuidMap, sc.Serialize, sc.Serialize);
                }
                else
                {
                    sc.Serialize(ref VertexFactoryTypeCRCMap, sc.Serialize, sc.Serialize);
                }
            }

            // EMBEDDED SHADER FILES
            if (sc.IsLoading)
            {
                int shaderCount = sc.ms.ReadInt32();
                Shaders = new(shaderCount);
                for (int i = 0; i < shaderCount; i++)
                {
                    Shader shader = null;
                    sc.Serialize(ref shader);
                    Shaders.Add(shader.Guid, shader);
                }
            }
            else
            {
                sc.ms.Writer.WriteInt32(Shaders.Count);
                foreach ((_, Shader shader) in Shaders)
                {
                    var temp = shader;
                    sc.Serialize(ref temp);
                }
            }

            // VertexFactoryType to CRC maps
            if (sc.Game != MEGame.ME1 && sc.Game != MEGame.UDK)
            {
                if (IsGlobalShaderCache)
                {
                    NameReference prevNameRef = default;
                    sc.Serialize(ref VertexFactoryTypeGuidMap, (ref NameReference nameRef) =>
                    {
                        sc.Serialize(ref nameRef);
                        prevNameRef = nameRef;

                    }, (ref Guid guid) =>
                    {
                        sc.Serialize(ref guid);
                        sc.Serialize(ref prevNameRef);
                    });
                }
                else
                {
                    sc.Serialize(ref VertexFactoryTypeCRCMap, sc.Serialize, sc.Serialize);
                }
            }

            if (!IsGlobalShaderCache)
            {
                // Shader maps (maps material guids to a map of diff shader types located in the embedded shaders block)
                sc.Serialize(ref MaterialShaderMaps, sc.Serialize, sc.Serialize);

                if (sc.Game is not (MEGame.ME2 or MEGame.LE2 or MEGame.LE1 or MEGame.UDK))
                {
                    // Technically this matters on console
                    // Technically we don't support them
                    int shaderCachePayloadsSize = 0;
                    sc.Serialize(ref shaderCachePayloadsSize);
                }
            }
        }
        public static ShaderCache Create()
        {
            return new()
            {
                ShaderTypeCRCMap = [],
                Shaders = [],
                VertexFactoryTypeCRCMap = [],
                MaterialShaderMaps = []
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            names.AddRange(ShaderTypeCRCMap.Select((kvp, i) => (kvp.Key, $"ShaderTypeCRCMap[{i}]")));
            int i = 0;
            foreach ((_, Shader shader) in Shaders)
            {
                names.Add(shader.ShaderType, $"Shaders[{i}].ShaderType");
                if (shader.VertexFactoryType.HasValue)
                {
                    names.Add(shader.VertexFactoryType.Value, $"Shaders[{i}].VertexFactoryType");
                }
                i++;
            }
            if (Packageless)
            {
                names.AddRange(VertexFactoryTypeGuidMap.Select((kvp, i) => (kvp.Key, $"VertexFactoryTypeGuidMap[{i}]")));
            }
            else
            {
                names.AddRange(VertexFactoryTypeCRCMap.Select((kvp, i) => (kvp.Key, $"VertexFactoryTypeCRCMap[{i}]")));
            }

            int j = 0;
            foreach ((StaticParameterSet key, MaterialShaderMap msm) in MaterialShaderMaps)
            {
                names.AddRange(msm.GetNames(game).Select(tuple => (tuple.Item1, $"MaterialShaderMaps[{j}].{tuple.Item2}")));
                names.AddRange(key.GetNames(game, $"MaterialShaderMaps[{j}]."));
                ++j;
            }

            return names;
        }

        /// <summary>
        /// Writes this binary directly to the given serializing container. Make sure you've set it up correctly!
        /// </summary>
        /// <param name="container"></param>
        public void WriteTo(SerializingContainer container)
        {
            Serialize(container);
        }


        /// <summary>
        /// Merges shader maps from this cache into the target one. Shader files are also merged (and replaced if existing).
        /// </summary>
        /// <param name="target">The target to merge into</param>
        public void MergeInto(ShaderCache target, bool cleanup = false)
        {
            // Material shader maps
            foreach (var shaderMap in MaterialShaderMaps)
            {
                // copy to target
                target.MaterialShaderMaps[shaderMap.Key] = shaderMap.Value;
            }

            // Shader files
            foreach (var shader in Shaders)
            {
                target.Shaders[shader.Key] = shader.Value;
            }

            if (cleanup)
            {
                target.Compact();
            }
        }

        /// <summary>
        /// Removes unreferenced Shader files 
        /// </summary>
        /// <returns>The amount of shaders removed</returns>
        public int Compact()
        {
            int oldCount = Shaders.Count;
            List<Guid> guidsToKeep = new List<Guid>();
            foreach (var msm in MaterialShaderMaps)
            {
                foreach (var shaderRef in msm.Value.Shaders)
                {
                    guidsToKeep.Add(shaderRef.Value.Id);
                }
                foreach (var meshShaderMap in msm.Value.MeshShaderMaps)
                {
                    foreach (var meshShaderRef in meshShaderMap.Shaders)
                    {
                        guidsToKeep.Add(meshShaderRef.Value.Id);
                    }
                }
            }
            var newShaderList = Shaders.Where(x => guidsToKeep.Contains(x.Key));
            Shaders = new(newShaderList);
            return oldCount - Shaders.Count;
        }

        /// <summary>
        /// Allows access to Serialize() from outside the class.
        /// </summary>
        /// <param name="sc"></param>
        public void PublicSerialize(SerializingContainer sc)
        {
            Serialize(sc);
        }
    }


    public class GlobalShaderCache : ShaderCache
    {
        public GlobalShaderCache() : base()
        {
            IsGlobalShaderCache = true;
            Packageless = true;
        }

        /// <summary>
        /// GlobalShaderCache has different serialization than ShaderCache.
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        public static GlobalShaderCache ReadGlobalShaderCache(Stream fs, MEGame game)
        {
            GlobalShaderCache sc = new GlobalShaderCache();
            var container = new PackagelessSerializingContainer(fs, null, true)
            {
                Game = game
            };
            sc.Serialize(container);
            return sc;
        }

        protected override void Serialize(SerializingContainer sc)
        {
            // Global Shader Cache parsing.
            // GlobalShaderCache has a special header on it, BMSG, and then the licensee and version numbers.
            if (sc.IsLoading)
            {
                sc.ms.ReadStringASCII(4); // BMSG
            }
            else
            {
                sc.ms.Writer.WriteStringASCII("BMSG");
            }

            int version = UnrealPackageFile.UnrealVersion(sc.Game);
            sc.Serialize(ref version);
            int licensee = UnrealPackageFile.LicenseeVersion(sc.Game);
            sc.Serialize(ref licensee);

            base.Serialize(sc);
        }
    }

    public class MaterialShaderMap
    {
        //usually empty! Shaders are in MeshShaderMaps
        public UMultiMap<NameReference, ShaderReference> Shaders; //TODO: Make this a UMap
        public MeshShaderMap[] MeshShaderMaps;
        public Guid ID;
        public string FriendlyName;
        public StaticParameterSet StaticParameters;
        //ME3/LE
        public MaterialUniformExpression[] UniformPixelVectorExpressions;
        public MaterialUniformExpression[] UniformPixelScalarExpressions;
        public MaterialUniformExpressionTexture[] Uniform2DTextureExpressions;
        public MaterialUniformExpressionTexture[] UniformCubeTextureExpressions;
        public MaterialUniformExpression[] UniformVertexVectorExpressions;
        public MaterialUniformExpression[] UniformVertexScalarExpressions;

        public List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(Shaders.Select((kvp, i) => (kvp.Key, $"Shaders[{i}].ShaderType")));

            int j = 0;
            foreach (var msm in MeshShaderMaps)
            {
                names.Add((msm.VertexFactoryType, $"MeshShaderMaps[{j}].VertexFactoryType"));
                names.AddRange(msm.Shaders.Select((kvp, i) => (kvp.Key, $"MeshShaderMaps[{j}].Shaders[{i}].ShaderType")));
                ++j;
            }
            names.AddRange(StaticParameters.GetNames(game).Select(tuple => (tuple.Item1, $"StaticParameters.{tuple.Item2}")));

            if (game >= MEGame.ME3)
            {
                var uniformExpressionArrays = new List<(string, MaterialUniformExpression[])>
                {
                    (nameof(UniformPixelVectorExpressions), UniformPixelVectorExpressions),
                    (nameof(UniformPixelScalarExpressions), UniformPixelScalarExpressions),
                    (nameof(Uniform2DTextureExpressions), Uniform2DTextureExpressions),
                    (nameof(UniformCubeTextureExpressions), UniformCubeTextureExpressions),
                    (nameof(UniformCubeTextureExpressions), UniformVertexVectorExpressions),
                    (nameof(UniformCubeTextureExpressions), UniformVertexScalarExpressions),
                };

                foreach ((string prefix, MaterialUniformExpression[] expressions) in uniformExpressionArrays)
                {
                    for (int i = 0; i < expressions.Length; i++)
                    {
                        MaterialUniformExpression expression = expressions[i];
                        names.Add((expression.ExpressionType, $"{prefix}[{i}].ExpressionType"));
                        switch (expression)
                        {
                            case MaterialUniformExpressionTextureParameter texParamExpression:
                                names.Add((texParamExpression.ParameterName, $"{prefix}[{i}].ParameterName"));
                                break;
                            case MaterialUniformExpressionScalarParameter scalarParameterExpression:
                                names.Add((scalarParameterExpression.ParameterName, $"{prefix}[{i}].ParameterName"));
                                break;
                            case MaterialUniformExpressionVectorParameter vecParameterExpression:
                                names.Add((vecParameterExpression.ParameterName, $"{prefix}[{i}].ParameterName"));
                                break;
                        }
                    }
                }
            }

            return names;
        }

        /// <summary>
        /// Copies into ShaderCache, with new GUIDs for self and all shaders
        /// </summary>
        /// <param name="shaderCache"></param>
        /// <param name="newMsmGuid"></param>
        /// <returns>Map of shader guids belonging to the original MSM, to the new guids they correspond to</returns>
        public Dictionary<Guid, Guid> DeepCopyWithNewGuidsInto(ShaderCache shaderCache, out Guid newMsmGuid)
        {
            var newMSM = new MaterialShaderMap();

            newMsmGuid = Guid.NewGuid();
            newMSM.ID = newMsmGuid;
            newMSM.FriendlyName = FriendlyName;

            //no deep copying needed for these
            newMSM.UniformPixelVectorExpressions = UniformPixelVectorExpressions;
            newMSM.UniformPixelScalarExpressions = UniformPixelScalarExpressions;
            newMSM.Uniform2DTextureExpressions = Uniform2DTextureExpressions;
            newMSM.UniformCubeTextureExpressions = UniformCubeTextureExpressions;
            newMSM.UniformVertexVectorExpressions = UniformVertexVectorExpressions;
            newMSM.UniformVertexScalarExpressions = UniformVertexScalarExpressions;

            newMSM.StaticParameters = StaticParameters;
            newMSM.StaticParameters.BaseMaterialId = newMsmGuid;

            var guidMap = new Dictionary<Guid, Guid>();

            //Shaders
            newMSM.Shaders = new UMultiMap<NameReference, ShaderReference>(Shaders.Count);
            CopyShaderRefs(Shaders, newMSM.Shaders);

            newMSM.MeshShaderMaps = new MeshShaderMap[MeshShaderMaps.Length];
            for (int i = 0; i < MeshShaderMaps.Length; i++)
            {
                var meshShaderMap = newMSM.MeshShaderMaps[i] = new MeshShaderMap();
                meshShaderMap.VertexFactoryType = MeshShaderMaps[i].VertexFactoryType;
                meshShaderMap.unk = MeshShaderMaps[i].unk;
                meshShaderMap.Shaders = new UMultiMap<NameReference, ShaderReference>(MeshShaderMaps[i].Shaders.Count);
                CopyShaderRefs(MeshShaderMaps[i].Shaders, meshShaderMap.Shaders);
            }

            shaderCache.MaterialShaderMaps.Add(newMSM.StaticParameters, newMSM);

            return guidMap;

            void CopyShaderRefs(UMultiMap<NameReference, ShaderReference> source, UMultiMap<NameReference, ShaderReference> dest)
            {
                foreach ((NameReference type, ShaderReference shaderReference) in source)
                {
                    if (!guidMap.TryGetValue(shaderReference.Id, out Guid newShaderGuid))
                    {
                        newShaderGuid = Guid.NewGuid();
                        guidMap.Add(shaderReference.Id, newShaderGuid);
                    }
                    dest.Add(type, new ShaderReference { Id = newShaderGuid, ShaderType = shaderReference.ShaderType });
                }
            }
        }
    }

    public class ShaderReference
    {
        public Guid Id;
        public NameReference ShaderType;
    }

    public class MeshShaderMap
    {
        public UMultiMap<NameReference, ShaderReference> Shaders; //TODO: Make this a UMap
        public NameReference VertexFactoryType;
        public uint unk;//ME1
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref MaterialShaderMap msm)
        {
            if (IsLoading)
            {
                msm = new MaterialShaderMap();
            }
            if (Game >= MEGame.ME3)
            {
                uint unrealVersion = UnrealPackageFile.UnrealVersion(Game);
                uint licenseeVersion = UnrealPackageFile.LicenseeVersion(Game);
                Serialize(ref unrealVersion);
                Serialize(ref licenseeVersion);
            }
            long endOffsetPos = ms.Position;
            int dummy = 0;
            Serialize(ref dummy);//file offset of end of MaterialShaderMap
            Serialize(ref msm.Shaders, Serialize, Serialize);
            Serialize(ref msm.MeshShaderMaps, Serialize);
            Serialize(ref msm.ID);
            Serialize(ref msm.FriendlyName);
            Serialize(ref msm.StaticParameters);

            if (Game >= MEGame.ME3)
            {
                Serialize(ref msm.UniformPixelVectorExpressions, Serialize);
                Serialize(ref msm.UniformPixelScalarExpressions, Serialize);
                Serialize(ref msm.Uniform2DTextureExpressions, Serialize);
                Serialize(ref msm.UniformCubeTextureExpressions, Serialize);
                Serialize(ref msm.UniformVertexVectorExpressions, Serialize);
                Serialize(ref msm.UniformVertexScalarExpressions, Serialize);
            }

            if (Game == MEGame.UDK)
            {
                // UDK has 0x1C bytes of unknown data here, seems like it's always 0? 
                if (IsLoading)
                {
                    ms.Skip(0x1C);
                }
                else
                {
                    ms.Writer.WriteZeros(0x1C);
                }
            }

            if (Game is not MEGame.ME1)
            {
                int platform = Game.IsLEGame() ? 5 : 0;
                if (Game == MEGame.UDK)
                {
                    platform = 4; // SM5
                    Serialize(ref platform);
                }
                else
                {
                    Serialize(ref platform);
                }
            }


            if (IsSaving)
            {
                long endOffset = ms.Position;
                int endOffsetInFile = FileOffset;
                ms.JumpTo(endOffsetPos);
                ms.Writer.WriteInt32(endOffsetInFile);
                ms.JumpTo(endOffset);
            }
        }
        public void Serialize(ref ShaderReference shaderRef)
        {
            if (IsLoading)
            {
                shaderRef = new ShaderReference();
            }
            Serialize(ref shaderRef.Id);
            Serialize(ref shaderRef.ShaderType);
        }
        public void Serialize(ref MeshShaderMap msm)
        {
            if (IsLoading)
            {
                msm = new MeshShaderMap();
            }
            Serialize(ref msm.Shaders, Serialize, Serialize);
            Serialize(ref msm.VertexFactoryType);
            if (Game == MEGame.ME1)
            {
                Serialize(ref msm.unk);
            }
        }
    }
}
