using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Direct3D9_Shader_Model_3_Disassembler;
using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class ShaderCache : ObjectBinary
    {
        public OrderedMultiValueDictionary<NameReference, uint> ShaderTypeCRCMap;
        public OrderedMultiValueDictionary<Guid, Shader> Shaders;
        public OrderedMultiValueDictionary<NameReference, uint> VertexFactoryTypeCRCMap;
        public OrderedMultiValueDictionary<StaticParameterSet, MaterialShaderMap> MaterialShaderMaps;

        protected override void Serialize(SerializingContainer2 sc)
        {
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
                sc.ms.WriteInt32(0);
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
                sc.ms.WriteInt32(Shaders.Count);
                foreach ((_, Shader shader) in Shaders)
                {
                    var temp = shader;
                    sc.Serialize(ref temp);
                }
            }

            sc.Serialize(ref VertexFactoryTypeCRCMap, SCExt.Serialize, SCExt.Serialize);
            sc.Serialize(ref MaterialShaderMaps, SCExt.Serialize, SCExt.Serialize);
            int dummy = 0;
            sc.Serialize(ref dummy);
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
    }
}

namespace ME3Explorer
{
    using Unreal.BinaryConverters;

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
            sc.Serialize(ref shader.ShaderByteCode, Serialize);
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
                sc.ms.WriteFromBuffer(shader.unkBytes);
                endOffset = sc.FileOffset;
                long endPos = sc.ms.Position;
                sc.ms.JumpTo(endOffsetPos);
                sc.ms.WriteInt32(endOffset);
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
                int platform = 0;
                sc.Serialize(ref platform);
            }

            if (sc.IsSaving)
            {
                long endOffset = sc.ms.Position;
                int endOffsetInFile = sc.FileOffset;
                sc.ms.JumpTo(endOffsetPos);
                sc.ms.WriteInt32(endOffsetInFile);
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
        }

        public static void Serialize(this SerializingContainer2 sc, ref ShaderFrequency sf)
        {
            if (sc.IsLoading)
            {
                sf = (ShaderFrequency)sc.ms.ReadByte();
            }
            else
            {
                sc.ms.WriteByte((byte)sf);
            }
        }
    }
}
