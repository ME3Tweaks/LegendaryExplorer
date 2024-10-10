using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Textures.Studio;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;

namespace LegendaryExplorer.UserControls.SharedToolControls.Scene3D
{
    //update the name strings too
    using VertexShaderType = TBasePassVertexShader<FNullPolicy, FNullPolicy>;
    using PixelShaderType = TBasePassPixelShader<FNullPolicy>;
    public class MaterialRenderProxy(ExportEntry export, PackageCache assetCache = null)
        : MaterialInstanceConstant(export, assetCache, true)
    {
        private const string VERTEX_SHADER_TYPE_NAME = "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy";
        private const string PIXEL_SHADER_TYPE_NAME = "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight";

        public EBlendMode BlendMode;
        private readonly Dictionary<string, float> ScalarParameterValues = [];
        private readonly Dictionary<string, LinearColor> VectorParameterValues = [];
        private readonly Dictionary<string, string> TextureParameterValues = [];
        public readonly Dictionary<string, PreviewTextureCache.TextureEntry> TextureMap = new();
        private MaterialShaderMap ShaderMap;
        private uint CachedPixelFrameNumber = uint.MaxValue;
        private uint CachedVertexFrameNumber = uint.MaxValue;
        private readonly List<Vector4> CachedVertexScalarParameters = [];
        private readonly List<Vector4> CachedVertexVectorParameters = [];
        private readonly List<Vector4> CachedPixelScalarParameters = [];
        private readonly List<Vector4> CachedPixelVectorParameters = [];
        private readonly List<PreviewTextureCache.TextureEntry> CachedTexture2DParameters = [];
        private readonly List<PreviewTextureCache.TextureEntry> CachedCubeTextureParameters = [];

        public VertexShaderType VertexShader;
        public PixelShaderType PixelShader;

        protected override void ReadBaseMaterial(ExportEntry mat, PackageCache assetCache, Material parsedMaterial)
        {
            base.ReadBaseMaterial(mat, assetCache, parsedMaterial);

            Enum.TryParse(mat.GetProperty<EnumProperty>("BlendMode", assetCache)?.Value ?? "BLEND_Opaque", out BlendMode);

            if (mat.Game is MEGame.LE3)
            {
                (ShaderMap, Shader[] shaders) = ShaderCacheManipulator.GetMaterialShaderMapAndShaders(mat, VERTEX_SHADER_TYPE_NAME, PIXEL_SHADER_TYPE_NAME);
                if (shaders is [VertexShaderType vertexShader, PixelShaderType pixelShader])
                {
                    VertexShader = vertexShader;
                    PixelShader = pixelShader;
                }
            }
        }

        protected override void ReadMaterialInstanceConstant(ExportEntry matInst, PropertyCollection props)
        {
            base.ReadMaterialInstanceConstant(matInst, props);
            if (props.GetProp<ArrayProperty<StructProperty>>("ScalarParameterValues") is { } scalarValues)
            {
                foreach (StructProperty scalarValue in scalarValues)
                {
                    if (scalarValue.GetProp<StrProperty>("ParameterName") is { } paramNameProp
                        && scalarValue.GetProp<FloatProperty>("ParameterValue") is { } valProp)
                    {
                        ScalarParameterValues[paramNameProp.Value] = valProp.Value;
                    }
                }
            }
            if (props.GetProp<ArrayProperty<StructProperty>>("VectorParameterValues") is { } vectorValues)
            {
                foreach (StructProperty vectorValue in vectorValues)
                {
                    if (vectorValue.GetProp<StrProperty>("ParameterName") is { } paramNameProp
                        && vectorValue.GetProp<StructProperty>("ParameterValue") is { } valProp)
                    {
                        VectorParameterValues[paramNameProp.Value] = CommonStructs.GetLinearColor(valProp);
                    }
                }
            }
            if (props.GetProp<ArrayProperty<StructProperty>>("TextureParameterValues") is { } textureValues)
            {
                foreach (StructProperty textureValue in textureValues)
                {
                    if (textureValue.GetProp<StrProperty>("ParameterName") is { } paramNameProp
                        && textureValue.GetProp<ObjectProperty>("ParameterValue") is { } valProp)
                    {
                        TextureParameterValues[paramNameProp.Value] = valProp.ResolveToEntry(matInst.FileRef)?.InstancedFullPath;
                    }
                }
            }
        }

        public void UpdateShaderParams(Span<byte> vertexConstantBuffer, Span<byte> pixelConstantBuffer, MeshRenderContext context, Mesh<LEVertex> mesh)
        {
            VertexShader?.WriteValues(vertexConstantBuffer, context, mesh, this);
            PixelShader?.WriteValues(pixelConstantBuffer, context, mesh, this);
        }

        public (List<Vector4> scalar, List<Vector4> vector) GetCachedVertexParameters(MeshRenderContext context)
        {
            UpdateUniformVertexParameters(context);
            return (CachedVertexScalarParameters, CachedVertexVectorParameters);
        }

        public (List<Vector4> scalar, List<Vector4> vector, 
            List<PreviewTextureCache.TextureEntry> tex2d, List<PreviewTextureCache.TextureEntry> cube)
            GetCachedPixelParameters(MeshRenderContext context)
        {
            UpdateUniformPixelParameters(context);
            return (CachedPixelScalarParameters, CachedPixelVectorParameters, CachedTexture2DParameters, CachedCubeTextureParameters);
        }

        private void UpdateUniformVertexParameters(MeshRenderContext context)
        {
            if (CachedVertexFrameNumber == context.NumFrames) return;
            CachedVertexFrameNumber = context.NumFrames;
            CachedVertexScalarParameters.Clear();
            CachedVertexVectorParameters.Clear();

            var uniformContext = new UniformExpressionRenderContext(ScalarParameterValues, VectorParameterValues, context.Time, context.Time);

            UpdateExpressions(uniformContext,
                ShaderMap.UniformVertexVectorExpressions, ShaderMap.UniformVertexScalarExpressions,
                CachedVertexScalarParameters, CachedVertexVectorParameters);
        }

        private void UpdateUniformPixelParameters(MeshRenderContext context)
        {
            if (CachedPixelFrameNumber == context.NumFrames) return;
            CachedPixelFrameNumber = context.NumFrames;
            CachedPixelScalarParameters.Clear();
            CachedPixelVectorParameters.Clear();
            CachedTexture2DParameters.Clear();
            CachedCubeTextureParameters.Clear();

            var uniformContext = new UniformExpressionRenderContext(
                ScalarParameterValues, VectorParameterValues, 
                context.Time, context.Time);

            UpdateExpressions(uniformContext,
                ShaderMap.UniformPixelVectorExpressions, ShaderMap.UniformPixelScalarExpressions,
                CachedPixelScalarParameters, CachedPixelVectorParameters);

            UpdateTextureExpressions(ShaderMap.Uniform2DTextureExpressions, CachedTexture2DParameters);
            UpdateTextureExpressions(ShaderMap.UniformCubeTextureExpressions, CachedCubeTextureParameters);
        }

        private void UpdateTextureExpressions(MaterialUniformExpressionTexture[] textureExpressions, List<PreviewTextureCache.TextureEntry> textureCache)
        {
            foreach (MaterialUniformExpressionTexture texExpression in textureExpressions)
            {
                PreviewTextureCache.TextureEntry texture = null;
                switch (texExpression)
                {
                    case MaterialUniformExpressionTextureParameter texParamExpression:
                        if (TextureParameterValues.TryGetValue(texParamExpression.ParameterName.Instanced, out string texIfp))
                        {
                            TextureMap.TryGetValue(texIfp, out texture);
                        }
                        break;
                    default:
                        if (Export.FileRef.TryGetEntry(texExpression.TextureIndex, out IEntry texEntry))
                        {
                            TextureMap.TryGetValue(texEntry.InstancedFullPath, out texture);
                        }
                        break;
                }
                textureCache.Add(texture);
            }
        }

        private void UpdateExpressions(UniformExpressionRenderContext uniformContext, 
            MaterialUniformExpression[] vertexExpressions, MaterialUniformExpression[] scalarExpressions, 
            List<Vector4> scalarCache, List<Vector4> vectorCache)
        {
            var enumerator = scalarExpressions.ChunkBySpan(4);
            foreach (ReadOnlySpan<MaterialUniformExpression> scalerExpression in enumerator)
            {
                LinearColor xVal = default;
                LinearColor yVal = default;
                LinearColor zVal = default;
                LinearColor wVal = default;
                scalerExpression[0].GetNumberValue(uniformContext, ref xVal);
                scalerExpression[1].GetNumberValue(uniformContext, ref yVal);
                scalerExpression[2].GetNumberValue(uniformContext, ref zVal);
                scalerExpression[3].GetNumberValue(uniformContext, ref wVal);
                scalarCache.Add(new Vector4(xVal.R, yVal.R, zVal.R, wVal.R));
            }
            if (enumerator.Current is { Length: > 0 } remainder)
            {
                LinearColor xVal = default;
                LinearColor yVal = default;
                LinearColor zVal = default;
                LinearColor wVal = default;
                remainder[0].GetNumberValue(uniformContext, ref xVal);
                if(remainder.Length > 1)
                {
                    remainder[1].GetNumberValue(uniformContext, ref yVal);
                    if (remainder.Length > 2)
                    {
                        remainder[2].GetNumberValue(uniformContext, ref zVal);
                        if (remainder.Length > 3)
                        {
                            remainder[3].GetNumberValue(uniformContext, ref wVal);
                        }
                    }
                }
                scalarCache.Add(new Vector4(xVal.R, yVal.R, zVal.R, wVal.R));
            }
            foreach (MaterialUniformExpression vectorExpression in vertexExpressions)
            {
                LinearColor val = default;
                vectorExpression.GetNumberValue(uniformContext, ref val);
                vectorCache.Add((Vector4)val);
            }
        }
    }
}
