using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal.Collections;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class ShaderCache : ObjectBinary
    {
        public bool IsGlobalShaderCache;
        public UMultiMap<NameReference, uint> ShaderTypeCRCMap; //TODO: Make this a UMap
        public UMultiMap<Guid, Shader> Shaders; //TODO: Make this a UMap
        public UMultiMap<NameReference, uint> VertexFactoryTypeCRCMap; //TODO: Make this a UMap
        public UMultiMap<NameReference, Guid> VertexFactoryTypeGuidMap; // GlobalShaderCache
        public UMultiMap<StaticParameterSet, MaterialShaderMap> MaterialShaderMaps; //TODO: Make this a UMap

        public static ShaderCache ReadGlobalShaderCache(Stream fs)
        {
            ShaderCache sc = new ShaderCache
            {
                IsGlobalShaderCache = true,
            };
            var magic = fs.ReadStringASCII(4);
            var unrealver = fs.ReadInt32();
            var licenseever = fs.ReadInt32();
            var container = new GlobalShaderCacheSerializingContainer(fs, null, true);
            sc.Serialize(container);
            if (fs.Position != fs.Length)
                Debugger.Break(); // Did not fully read!
            return sc;
        }

        public class GlobalShaderCacheSerializingContainer(Stream stream, IMEPackage pcc, bool isLoading = false, int offset = 0, PackageCache packageCache = null) : SerializingContainer(stream, pcc, isLoading, offset, packageCache)
        {
            // Global shader cache is not in a package. Thus name references are directly written.
            public override void Serialize(ref NameReference name)
            {
                if (IsLoading)
                {
                    name = NameReference.FromInstancedString(ms.ReadUnrealString());
                }
                else
                {
                    ms.Writer.WriteUnrealString(name.Instanced, MEGame.ME3); // Unicode.
                }
            }
        }

        protected override void Serialize(SerializingContainer sc)
        {
            if (!IsGlobalShaderCache)
            {
                if (sc.Pcc.Platform != MEPackage.GamePlatform.PC) return; //We do not support non-PC shader cache
                byte platform = sc.Game.IsLEGame() ? (byte)5 : (byte)0;
                sc.Serialize(ref platform);
            }
            else
            {
                byte platform = 5;
                sc.Serialize(ref platform);
            }

            sc.Serialize(ref ShaderTypeCRCMap, sc.Serialize, sc.Serialize);
            if (IsGlobalShaderCache || (sc.Game is MEGame.ME3 or MEGame.LE1 or MEGame.LE2 or MEGame.LE3) && sc.IsLoading)
            {
                int nameMapCount = sc.ms.ReadInt32();
                sc.ms.Skip(nameMapCount * 12);
            }
            else if (IsGlobalShaderCache || (sc.Game is MEGame.ME3 or MEGame.LE1 or MEGame.LE2 or MEGame.LE3) && sc.IsSaving)
            {
                sc.ms.Writer.WriteInt32(0);
            }

            if (!IsGlobalShaderCache && sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref VertexFactoryTypeCRCMap, sc.Serialize, sc.Serialize);
            }

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


            if (IsGlobalShaderCache)
            {
                int count = 0;
                sc.Serialize(ref count);
                if (sc.IsLoading)
                {
                    VertexFactoryTypeGuidMap = new UMultiMap<NameReference, Guid>(count);
                }
                
                int i = 0;
                while (i < count)
                {
                    NameReference name = default;
                    sc.Serialize(ref name);
                    Guid value = default;
                    sc.Serialize(ref value);
                    sc.Serialize(ref name); // duplicate
                    VertexFactoryTypeGuidMap.Add(name, value);
                    i++;
                }
            }
            else
            {
                if (sc.Game != MEGame.ME1)
                {
                    sc.Serialize(ref VertexFactoryTypeCRCMap, sc.Serialize, sc.Serialize);
                }

                // Global has no material shaders.
                sc.Serialize(ref MaterialShaderMaps, sc.Serialize, sc.Serialize);

                if (sc.Game is not (MEGame.ME2 or MEGame.LE2 or MEGame.LE1))
                {
                    int dummy = 0;
                    sc.Serialize(ref dummy);
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
            names.AddRange(VertexFactoryTypeCRCMap.Select((kvp, i) => (kvp.Key, $"VertexFactoryTypeCRCMap[{i}]")));

            int j = 0;
            foreach ((StaticParameterSet key, MaterialShaderMap msm) in MaterialShaderMaps)
            {
                names.AddRange(msm.GetNames(game).Select(tuple => (tuple.Item1, $"MaterialShaderMaps[{j}].{tuple.Item2}")));
                names.AddRange(key.GetNames(game, $"MaterialShaderMaps[{j}]."));
                ++j;
            }

            return names;
        }
    }

    public enum ShaderFrequency : byte
    {
        Vertex = 0,
        Pixel = 1,
    }

    public class Shader
    {
        public NameReference ShaderType;
        public Guid Guid;
        public ShaderFrequency Frequency;
        public byte[] ShaderByteCode;
        public uint ParameterMapCRC;
        public int InstructionCount;
        public byte[] unkBytesPreName; //only exists in some Shaders with a FVertexFactoryParameterRef
        public NameReference? VertexFactoryType; //only exists in Shaders with a FVertexFactoryParameterRef
        public byte[] unkBytes;
        public byte[] unkSM2Bytes;

        private string dissassembly;
        private ShaderInfo info;
        public byte[] SM3UnkBinkBytes;

        public ShaderInfo ShaderInfo => info ?? DisassembleShader();

        public string ShaderDisassembly
        {
            get
            {
                if (dissassembly == null)
                {
                    DisassembleShader();
                }
                return dissassembly;
            }
        }

        private ShaderInfo DisassembleShader()
        {
            return info = ShaderReader.DisassembleShader(ShaderByteCode, out dissassembly);
        }

        public Shader Clone()
        {
            var newShader = new Shader
            {
                ShaderType = ShaderType,
                Guid = Guid,
                Frequency = Frequency,
                ShaderByteCode = ShaderByteCode.ArrayClone(),
                ParameterMapCRC = ParameterMapCRC,
                InstructionCount = InstructionCount,
                unkBytesPreName = unkBytesPreName?.ArrayClone(),
                VertexFactoryType = VertexFactoryType,
                unkBytes = unkBytes.ArrayClone()
            };
            return newShader;
        }

        // replace shader bytes
        public void Replace(byte[] newShaderByteCode)
        {
            // insert new binary
            ShaderByteCode = newShaderByteCode;

            // return
            return;
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
        //ME3
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
                    var newShaderGuid = Guid.NewGuid();
                    if (!guidMap.TryAdd(shaderReference.Id, newShaderGuid))
                    {
                        newShaderGuid = shaderReference.Id;
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
        public void Serialize(ref Shader shader)
        {
            if (IsLoading)
            {
                shader = new Shader();
            }
            Serialize(ref shader.ShaderType);
            Serialize(ref shader.Guid);
            int endOffset = 0;
            long endOffsetPos = ms.Position;
            Serialize(ref endOffset);
            byte platform = Game.IsLEGame() ? (byte)5 : (byte)0;
            Serialize(ref platform);
            Serialize(ref shader.Frequency);
            if (platform == 0x3)
            {
                // Shader Model 2 - global shader cache in LE seems to have this for bink
                // seems to have extra 6 unknown bytes.
                if (IsLoading)
                {
                    shader.unkSM2Bytes = ms.ReadBytes(0x6);
                }
                else
                {
                    ms.Writer.WriteBytes(shader.unkSM2Bytes);
                }
            }

            if (platform == 0x0 && shader.ShaderType == "FBinkYCrCbToRGBNoPixelAlphaPixelShader")
            {
                // Found in LE3's global shader cache.
                if (IsLoading)
                {
                    shader.SM3UnkBinkBytes = ms.ReadBytes(0x30);
                }
                else
                {
                    ms.Writer.WriteBytes(shader.SM3UnkBinkBytes);
                }
            }

            Serialize(ref shader.ShaderByteCode);
            Serialize(ref shader.ParameterMapCRC);
            Serialize(ref shader.Guid);//intentional duplicate
            Serialize(ref shader.ShaderType);//intentional duplicate
            Serialize(ref shader.InstructionCount);
            if (IsLoading)
            {
                switch (shader.ShaderType.Name)
                {
                    case "FFogVolumeApplyVertexShader":
                    case "FHitMaskVertexShader":
                    case "FHitProxyVertexShader":
                    case "FModShadowMeshVertexShader":
                    case "FSFXWorldNormalVertexShader":
                    case "FTextureDensityVertexShader":
                    case "TDepthOnlyVertexShader<0>":
                    case "TDepthOnlyVertexShader<1>":
                    case "FVelocityVertexShader":
                    case "TFogIntegralVertexShader<FConstantDensityPolicy>":
                    case "TFogIntegralVertexShader<FLinearHalfspaceDensityPolicy>":
                    case "TFogIntegralVertexShader<FSphereDensityPolicy>":
                    case "FShadowDepthVertexShader":
                    case "TShadowDepthVertexShader<ShadowDepth_OutputDepth>":
                    case "TShadowDepthVertexShader<ShadowDepth_OutputDepthToColor>":
                    case "TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>":
                    case "TAOMeshVertexShader<0>":
                    case "TAOMeshVertexShader<1>":
                    case "TDistortionMeshVertexShader<FDistortMeshAccumulatePolicy>":
                    case "TLightMapDensityVertexShader<FNoLightMapPolicy>":
                    case "TLightVertexShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy":
                    case "TBasePassVertexShaderFNoLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFNoLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFNoLightMapPolicyFSphereDensityPolicy":
                    case "FShadowDepthNoPSVertexShader":
                        shader.unkBytesPreName = null;
                        shader.VertexFactoryType = ms.ReadNameReference(Pcc);
                        break;
                    case "TLightMapDensityVertexShader<FDirectionalLightMapTexturePolicy>":
                    case "TLightMapDensityVertexShader<FDummyLightMapTexturePolicy>":
                    case "TLightMapDensityVertexShader<FSimpleLightMapTexturePolicy>":
                    case "TLightVertexShaderFDirectionalLightPolicyFNoStaticShadowingPolicy":
                    case "TLightVertexShaderFDirectionalLightPolicyFShadowVertexBufferPolicy":
                    case "TLightVertexShaderFPointLightPolicyFNoStaticShadowingPolicy":
                    case "TLightVertexShaderFPointLightPolicyFShadowVertexBufferPolicy":
                    case "TLightVertexShaderFSpotLightPolicyFNoStaticShadowingPolicy":
                    case "TLightVertexShaderFSpotLightPolicyFShadowVertexBufferPolicy":
                    case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFSHLightLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFSHLightLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFSHLightLightMapPolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFSHLightLightMapPolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFPointLightLightMapPolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFSphereDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFConstantDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFNoDensityPolicy":
                    case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFSphereDensityPolicy":
                        shader.unkBytesPreName = ms.ReadBytes(6);
                        shader.VertexFactoryType = ms.ReadNameReference(Pcc);
                        break;
                    case "TLightVertexShaderFDirectionalLightPolicyFShadowTexturePolicy":
                    case "TLightVertexShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    case "TLightVertexShaderFPointLightPolicyFShadowTexturePolicy":
                    case "TLightVertexShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    case "TLightVertexShaderFSpotLightPolicyFShadowTexturePolicy":
                    case "TLightVertexShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    case "TLightVertexShaderFSFXPointLightPolicyFNoStaticShadowingPolicy":
                        shader.unkBytesPreName = ms.ReadBytes(12);
                        shader.VertexFactoryType = ms.ReadNameReference(Pcc);
                        break;
                    default:
                        shader.unkBytesPreName = null;
                        shader.VertexFactoryType = null;
                        break;
                }
                shader.unkBytes = ms.ReadToBuffer(endOffset - FileOffset);
            }
            else
            {
                if (shader.VertexFactoryType is NameReference vertexFactoryType)
                {
                    if (shader.unkBytesPreName is not null)
                    {
                        ms.Writer.WriteFromBuffer(shader.unkBytesPreName);
                    }
                    ms.Writer.WriteNameReference(vertexFactoryType, Pcc);
                }
                ms.Writer.WriteFromBuffer(shader.unkBytes);
                endOffset = FileOffset;
                long endPos = ms.Position;
                ms.JumpTo(endOffsetPos);
                ms.Writer.WriteInt32(endOffset);
                ms.JumpTo(endPos);
            }
        }

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
            if (Game is not MEGame.ME1)
            {
                int platform = Game.IsLEGame() ? 5 : 0;
                Serialize(ref platform);
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

        public void Serialize(ref ShaderFrequency sf)
        {
            if (IsLoading)
            {
                sf = (ShaderFrequency)ms.ReadByte();
            }
            else
            {
                ms.Writer.WriteByte((byte)sf);
            }
        }
    }
}
