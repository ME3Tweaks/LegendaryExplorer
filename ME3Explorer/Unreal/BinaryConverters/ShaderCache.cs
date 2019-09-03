using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Direct3D9_Shader_Model_3_Disassembler;
using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.Packages;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    //For reading only
    class ShaderCache : ObjectBinary
    {
        public Dictionary<Guid, Shader> Shaders;
        public Dictionary<Guid, MaterialShaderMap> MaterialShaderMaps;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.ms.SkipByte();
            int nameMapCount = sc.ms.ReadInt32();
            sc.ms.Skip(nameMapCount * 12);
            if (sc.Game == MEGame.ME3)
            {
                nameMapCount = sc.ms.ReadInt32();
                sc.ms.Skip(nameMapCount * 12);
            }

            int shaderCount = sc.ms.ReadInt32();
            Shaders = new Dictionary<Guid, Shader>(shaderCount);
            for (int i = 0; i < shaderCount; i++)
            {
                Shader shader = null;
                sc.Serialize(ref shader);
                Shaders.Add(shader.Guid, shader);
            }
            //Vertex Factory Name Map
            nameMapCount = sc.ms.ReadInt32();
            sc.ms.Skip(nameMapCount * 12);

            int msmCount = sc.ms.ReadInt32();
            MaterialShaderMaps = new Dictionary<Guid, MaterialShaderMap>(msmCount);
            for (int i = 0; i < msmCount; i++)
            {
                MaterialShaderMap msm = null;
                sc.Serialize(ref msm);
                //todo: There can be multiple shadermaps with the same ID, but different static parameters.
                //need to deal with this at some point, this is a quick fix.
                if(!MaterialShaderMaps.ContainsKey(msm.ID)) MaterialShaderMaps.Add(msm.ID, msm);
            }
        }

        /// <summary>
        /// Cannot write a ShaderCache
        /// </summary>
        public override void WriteTo(Stream ms, IMEPackage pcc, int fileOffset)
        {
            throw new InvalidOperationException();
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
        public StaticParameterSet StaticParameters;
        public Guid ID;
        //usually empty! Shaders are in MeshShaderMaps
        public List<Guid> Shaders;
        public Dictionary<NameReference, List<Guid>> MeshShaderMaps;
        public string FriendlyName;
        //ME3
        public MaterialUniformExpression[] UniformPixelVectorExpressions;
        public MaterialUniformExpression[] UniformPixelScalarExpressions;
        public MaterialUniformExpressionTexture[] Uniform2DTextureExpressions;
        public MaterialUniformExpressionTexture[] UniformCubeTextureExpressions;
        public MaterialUniformExpression[] UniformVertexVectorExpressions;
        public MaterialUniformExpression[] UniformVertexScalarExpressions;
    }

    public static class ShaderCacheSCExt
    {
        //only for reading
        public static void Serialize(this SerializingContainer2 sc, ref Shader shader)
        {
            shader = new Shader();
            sc.Serialize(ref shader.ShaderType);
            sc.Serialize(ref shader.Guid);
            int endOffset = sc.ms.ReadInt32();
            sc.ms.SkipByte();
            shader.Frequency = (ShaderFrequency)sc.ms.ReadByte();
            sc.Serialize(ref shader.ShaderByteCode, SCExt.Serialize);
            sc.ms.JumpTo(endOffset - sc.startOffset);
        }
        //only for reading
        public static void Serialize(this SerializingContainer2 sc, ref MaterialShaderMap msm)
        {
            msm = new MaterialShaderMap();
            sc.Serialize(ref msm.StaticParameters);
            if (sc.Game == MEGame.ME3)
            {
                sc.ms.Skip(8); //versions
            }

            sc.ms.SkipInt32(); //endoffset
            int shaderCount = sc.ms.ReadInt32();
            msm.Shaders = new List<Guid>(shaderCount);
            for (int j = 0; j < shaderCount; j++)
            {
                sc.ms.Skip(8);
                msm.Shaders.Add(sc.ms.ReadGuid());
                sc.ms.Skip(8);
            }
            int meshShaderMapsCount = sc.ms.ReadInt32();
            msm.MeshShaderMaps = new Dictionary<NameReference, List<Guid>>(meshShaderMapsCount);
            for (int i = 0; i < meshShaderMapsCount; i++)
            {
                int meshShaderCount = sc.ms.ReadInt32();
                var list = new List<Guid>(meshShaderCount);
                for (int j = 0; j < meshShaderCount; j++)
                {
                    sc.ms.Skip(8);
                    list.Add(sc.ms.ReadGuid());
                    sc.ms.Skip(8);
                }
                msm.MeshShaderMaps.Add(sc.ms.ReadNameReference(sc.Pcc), list);
            }
            sc.Serialize(ref msm.ID);
            sc.Serialize(ref msm.FriendlyName);
            StaticParameterSet duplicateSet = null;
            sc.Serialize(ref duplicateSet);

            if (sc.Game == MEGame.ME3)
            {
                sc.Serialize(ref msm.UniformPixelVectorExpressions, SCExt.Serialize);
                sc.Serialize(ref msm.UniformPixelScalarExpressions, SCExt.Serialize);
                sc.Serialize(ref msm.Uniform2DTextureExpressions, SCExt.Serialize);
                sc.Serialize(ref msm.UniformCubeTextureExpressions, SCExt.Serialize);
                sc.Serialize(ref msm.UniformVertexVectorExpressions, SCExt.Serialize);
                sc.Serialize(ref msm.UniformVertexScalarExpressions, SCExt.Serialize);
                sc.ms.SkipInt32();
            }
        }
    }
}
