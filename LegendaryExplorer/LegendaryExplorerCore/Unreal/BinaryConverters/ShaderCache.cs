using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal.Collections;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class ShaderCache : ObjectBinary
    {
        public UMultiMap<NameReference, uint> ShaderTypeCRCMap; //TODO: Make this a UMap
        public UMultiMap<Guid, Shader> Shaders; //TODO: Make this a UMap
        public UMultiMap<NameReference, uint> VertexFactoryTypeCRCMap; //TODO: Make this a UMap
        public UMultiMap<StaticParameterSet, MaterialShaderMap> MaterialShaderMaps; //TODO: Make this a UMap

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Pcc.Platform != MEPackage.GamePlatform.PC) return; //We do not support non-PC shader cache
            byte platform = sc.Game.IsLEGame() ? (byte)5 : (byte)0;
            sc.Serialize(ref platform);
            sc.Serialize(ref ShaderTypeCRCMap, SCExt.Serialize, SCExt.Serialize);
            if (sc.Game is MEGame.ME3 or MEGame.LE1 or MEGame.LE2 or MEGame.LE3 && sc.IsLoading)
            {
                int nameMapCount = sc.ms.ReadInt32();
                sc.ms.Skip(nameMapCount * 12);
            }
            else if (sc.Game is MEGame.ME3 or MEGame.LE1 or MEGame.LE2 or MEGame.LE3 && sc.IsSaving)
            {
                sc.ms.Writer.WriteInt32(0);
            }

            if (sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref VertexFactoryTypeCRCMap, SCExt.Serialize, SCExt.Serialize);
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

            if (sc.Game != MEGame.ME1)
            {
                sc.Serialize(ref VertexFactoryTypeCRCMap, SCExt.Serialize, SCExt.Serialize);
            }
            sc.Serialize(ref MaterialShaderMaps, SCExt.Serialize, SCExt.Serialize);
            if (sc.Game is not (MEGame.ME2 or MEGame.LE2 or MEGame.LE1))
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
            }
        }

        public static ShaderCache Create()
        {
            return new()
            {
                ShaderTypeCRCMap = new (),
                Shaders = new (),
                VertexFactoryTypeCRCMap = new (),
                MaterialShaderMaps = new ()
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


        private string dissassembly;
        private ShaderInfo info;

        public ShaderInfo ShaderInfo => info ?? DisassembleShader();

        public string ShaderDisassembly
        {
            get {
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

        public  List<(NameReference, string)> GetNames(MEGame game)
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
                    dest.Add(type, new ShaderReference{Id = newShaderGuid, ShaderType = shaderReference.ShaderType});
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

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref Shader shader)
        {
            if (sc.IsLoading)
            {
                shader = new Shader();
            }
            sc.Serialize(ref shader.ShaderType);
            sc.Serialize(ref shader.Guid);
            int endOffset = 0;
            long endOffsetPos = sc.ms.Position;
            sc.Serialize(ref endOffset);
            byte platform = sc.Game.IsLEGame() ? (byte)5 : (byte)0;
            sc.Serialize(ref platform);
            sc.Serialize(ref shader.Frequency);
            sc.Serialize(ref shader.ShaderByteCode);
            sc.Serialize(ref shader.ParameterMapCRC);
            sc.Serialize(ref shader.Guid);//intentional duplicate
            sc.Serialize(ref shader.ShaderType);//intentional duplicate
            sc.Serialize(ref shader.InstructionCount);
            if (sc.IsLoading)
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
                        shader.VertexFactoryType = sc.ms.ReadNameReference(sc.Pcc);
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
                        shader.unkBytesPreName = sc.ms.ReadBytes(6);
                        shader.VertexFactoryType = sc.ms.ReadNameReference(sc.Pcc);
                        break;
                    case "TLightVertexShaderFDirectionalLightPolicyFShadowTexturePolicy":
                    case "TLightVertexShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    case "TLightVertexShaderFPointLightPolicyFShadowTexturePolicy":
                    case "TLightVertexShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    case "TLightVertexShaderFSpotLightPolicyFShadowTexturePolicy":
                    case "TLightVertexShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    case "TLightVertexShaderFSFXPointLightPolicyFNoStaticShadowingPolicy":
                        shader.unkBytesPreName = sc.ms.ReadBytes(12);
                        shader.VertexFactoryType = sc.ms.ReadNameReference(sc.Pcc);
                        break;
                    default:
                        shader.unkBytesPreName = null;
                        shader.VertexFactoryType = null;
                        break;
                }
                shader.unkBytes = sc.ms.ReadToBuffer(endOffset - sc.FileOffset);
            }
            else
            {
                if (shader.VertexFactoryType is NameReference vertexFactoryType)
                {
                    if (shader.unkBytesPreName is not null)
                    {
                        sc.ms.Writer.WriteFromBuffer(shader.unkBytesPreName);
                    }
                    sc.ms.Writer.WriteNameReference(vertexFactoryType, sc.Pcc);
                }
                sc.ms.Writer.WriteFromBuffer(shader.unkBytes);
                endOffset = sc.FileOffset;
                long endPos = sc.ms.Position;
                sc.ms.JumpTo(endOffsetPos);
                sc.ms.Writer.WriteInt32(endOffset);
                sc.ms.JumpTo(endPos);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref MaterialShaderMap msm)
        {
            if (sc.IsLoading)
            {
                msm = new MaterialShaderMap();
            }
            if (sc.Game >= MEGame.ME3)
            {
                uint unrealVersion = UnrealPackageFile.UnrealVersion(sc.Game);
                uint licenseeVersion = UnrealPackageFile.LicenseeVersion(sc.Game);
                sc.Serialize(ref unrealVersion);
                sc.Serialize(ref licenseeVersion);
            }
            long endOffsetPos = sc.ms.Position;
            int dummy = 0;
            sc.Serialize(ref dummy);//file offset of end of MaterialShaderMap
            sc.Serialize(ref msm.Shaders, Serialize, Serialize);
            sc.Serialize(ref msm.MeshShaderMaps, Serialize);
            sc.Serialize(ref msm.ID);
            sc.Serialize(ref msm.FriendlyName);
            sc.Serialize(ref msm.StaticParameters);

            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref msm.UniformPixelVectorExpressions, Serialize);
                sc.Serialize(ref msm.UniformPixelScalarExpressions, Serialize);
                sc.Serialize(ref msm.Uniform2DTextureExpressions, Serialize);
                sc.Serialize(ref msm.UniformCubeTextureExpressions, Serialize);
                sc.Serialize(ref msm.UniformVertexVectorExpressions, Serialize);
                sc.Serialize(ref msm.UniformVertexScalarExpressions, Serialize);
            }
            if (sc.Game is not MEGame.ME1)
            {
                int platform = sc.Game.IsLEGame() ? 5 : 0;
                sc.Serialize(ref platform);
            }

            if (sc.IsSaving)
            {
                long endOffset = sc.ms.Position;
                int endOffsetInFile = sc.FileOffset;
                sc.ms.JumpTo(endOffsetPos);
                sc.ms.Writer.WriteInt32(endOffsetInFile);
                sc.ms.JumpTo(endOffset);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref ShaderReference shaderRef)
        {
            if (sc.IsLoading)
            {
                shaderRef = new ShaderReference();
            }
            sc.Serialize(ref shaderRef.Id);
            sc.Serialize(ref shaderRef.ShaderType);
        }
        public static void Serialize(this SerializingContainer2 sc, ref MeshShaderMap msm)
        {
            if (sc.IsLoading)
            {
                msm = new MeshShaderMap();
            }
            sc.Serialize(ref msm.Shaders, Serialize, Serialize);
            sc.Serialize(ref msm.VertexFactoryType);
            if (sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref msm.unk);
            }
        }

        public static void Serialize(this SerializingContainer2 sc, ref ShaderFrequency sf)
        {
            if (sc.IsLoading)
            {
                sf = (ShaderFrequency)sc.ms.ReadByte();
            }
            else
            {
                sc.ms.Writer.WriteByte((byte)sf);
            }
        }
    }
}