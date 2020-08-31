using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Shaders;

namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public class ShaderCache : ObjectBinary
    {
        public OrderedMultiValueDictionary<NameReference, uint> ShaderTypeCRCMap;
        public OrderedMultiValueDictionary<Guid, Shader> Shaders;
        public OrderedMultiValueDictionary<NameReference, uint> VertexFactoryTypeCRCMap;
        public OrderedMultiValueDictionary<StaticParameterSet, MaterialShaderMap> MaterialShaderMaps;

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Pcc.Platform != MEPackage.GamePlatform.PC) return; //We do not support non-PC shader cache
            byte platform = 0;
            sc.Serialize(ref platform);
            sc.Serialize(ref ShaderTypeCRCMap, SCExt.Serialize, SCExt.Serialize);
            if (sc.Game == MEGame.ME3 && sc.IsLoading)
            {
                int nameMapCount = sc.ms.ReadInt32();
                sc.ms.Skip(nameMapCount * 12);
            }
            else if (sc.Game == MEGame.ME3 && sc.IsSaving)
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
                Shaders = new OrderedMultiValueDictionary<Guid, Shader>(shaderCount);
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
            if (sc.Game != MEGame.ME2)
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
            }
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            names.AddRange(ShaderTypeCRCMap.Select((kvp, i) => (kvp.Key, $"ShaderTypeCRCMap[{i}]")));
            names.AddRange(Shaders.Select((kvp, i) => (kvp.Value.ShaderType, $"Shaders[{i}].ShaderType")));
            names.AddRange(VertexFactoryTypeCRCMap.Select((kvp, i) => (kvp.Key, $"VertexFactoryTypeCRCMap[{i}]")));

            int j = 0;
            foreach ((StaticParameterSet key, MaterialShaderMap msm) in MaterialShaderMaps)
            {
                names.AddRange(msm.GetNames(game).Select(tuple => (tuple.Item1, $"MaterialShaderMaps[{j}].{tuple.Item2}")));
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
    }

    public class MaterialShaderMap
    {
        //usually empty! Shaders are in MeshShaderMaps
        public OrderedMultiValueDictionary<NameReference, ShaderReference> Shaders;
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
    }

    public class ShaderReference
    {
        public Guid Id;
        public NameReference ShaderType;
    }

    public class MeshShaderMap
    {
        public OrderedMultiValueDictionary<NameReference, ShaderReference> Shaders;
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
            byte platform = 0;
            sc.Serialize(ref platform);
            sc.Serialize(ref shader.Frequency);
            sc.Serialize(ref shader.ShaderByteCode, Unreal.SCExt.Serialize);
            sc.Serialize(ref shader.ParameterMapCRC);
            sc.Serialize(ref shader.Guid);//intentional duplicate
            sc.Serialize(ref shader.ShaderType);//intentional duplicate
            sc.Serialize(ref shader.InstructionCount);
            if (sc.IsLoading)
            {
                shader.unkBytes = sc.ms.ReadToBuffer(endOffset - sc.FileOffset);
            }
            else
            {
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
            if (sc.Game == MEGame.ME3)
            {
                uint unrealVersion = MEPackage.ME3UnrealVersion;
                uint licenseeVersion = MEPackage.ME3LicenseeVersion;
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

            if (sc.Game == MEGame.ME3)
            {
                sc.Serialize(ref msm.UniformPixelVectorExpressions, Serialize);
                sc.Serialize(ref msm.UniformPixelScalarExpressions, Serialize);
                sc.Serialize(ref msm.Uniform2DTextureExpressions, Serialize);
                sc.Serialize(ref msm.UniformCubeTextureExpressions, Serialize);
                sc.Serialize(ref msm.UniformVertexVectorExpressions, Serialize);
                sc.Serialize(ref msm.UniformVertexScalarExpressions, Serialize);
            }
            if (sc.Game == MEGame.ME2 || sc.Game == MEGame.ME3)
            {
                int platform = 0;
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