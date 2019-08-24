using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            //todo: read MaterialShaderMaps
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
        public NameReference Name;
        public Guid Guid;
        public ShaderFrequency Frequency;
        public byte[] ShaderFile;
    }

    public class MaterialShaderMap
    {
        public Guid ID;
        public Dictionary<NameReference, List<Guid>> MeshShaderMaps;
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
            sc.Serialize(ref shader.Name);
            sc.Serialize(ref shader.Guid);
            int endOffset = sc.ms.ReadInt32();
            sc.ms.SkipByte();
            shader.Frequency = (ShaderFrequency)sc.ms.ReadByte();
            sc.Serialize(ref shader.ShaderFile, SCExt.Serialize);
            sc.ms.JumpTo(endOffset - sc.startOffset);
        }
    }
}
