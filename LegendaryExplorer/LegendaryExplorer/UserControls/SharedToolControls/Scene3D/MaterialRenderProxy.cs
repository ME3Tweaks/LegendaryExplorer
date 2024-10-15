using System;
using System.Collections.Generic;
using System.Numerics;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Shaders;
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
        private const string LIT_PIXEL_SHADER_TYPE_NAME = "TBasePassPixelShaderFNoLightMapPolicySkyLight";
        private const string UNLIT_PIXEL_SHADER_TYPE_NAME = "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight";

        public EBlendMode BlendMode;
        public bool UseHairPass;
        public bool IsUnlit;
        private readonly Dictionary<string, float> ScalarParameterValues = [];
        private readonly Dictionary<string, LinearColor> VectorParameterValues = [];
        private readonly Dictionary<string, string> TextureParameterValues = [];
        private readonly List<string> Uniform2DTextureExpressions = [];
        public Dictionary<string, PreviewTextureCache.TextureEntry> TextureMap;
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

            var props = mat.GetProperties(packageCache: assetCache);
            Enum.TryParse(props.GetProp<EnumProperty>("BlendMode")?.Value ?? "BLEND_Opaque", out BlendMode);

            //if the MIC had a StaticPermutationResource, this is already set
            if (Uniform2DTextureExpressions.IsEmpty())
            {
                foreach (int uIndex in parsedMaterial.SM3MaterialResource.UniformExpressionTextures)
                {
                    Uniform2DTextureExpressions.Add(mat.FileRef.GetEntry(uIndex)?.InstancedFullPath);
                }
            }

            UseHairPass = props.GetProp<BoolProperty>("bHairPass") is { Value: true };
            IsUnlit = props.GetProp<EnumProperty>("LightingModel") is {} lightingModelProp && lightingModelProp.Value == "MLM_Unlit";

            var expressionsProp = props.GetProp<ArrayProperty<ObjectProperty>>("Expressions");
            if (expressionsProp is not null)
            {
                foreach (ObjectProperty expressionProp in expressionsProp)
                {
                    ExportEntry expressionExport = expressionProp.ResolveToExport(mat.FileRef, assetCache);
                    var expressionProps = expressionExport.GetProperties(packageCache: assetCache);
                    if (expressionProps.GetProp<NameProperty>("ParameterName") is {} paramNameProp)
                    {
                        //this will run after ReadMaterialInstanceConstant, so we don't want to overwrite any values specified there
                        if (expressionProps.GetProp<FloatProperty>("DefaultValue") is { } defaultfloatProp)
                        {
                            ScalarParameterValues.TryAdd(paramNameProp.Value.Instanced, defaultfloatProp.Value);
                        }
                        else if (expressionProps.GetProp<StructProperty>("DefaultValue") is {} defaultVectorProp)
                        {
                            VectorParameterValues.TryAdd(paramNameProp.Value.Instanced, CommonStructs.GetLinearColor(defaultVectorProp));
                        }
                        else if (expressionProps.GetProp<ObjectProperty>("Texture") is {} textureProp)
                        {
                            if (!TextureParameterValues.ContainsKey(paramNameProp.Value.Instanced) 
                                && mat.FileRef.GetEntry(textureProp.Value) is {} texEntry)
                            {
                                Textures.Add(texEntry);
                                TextureParameterValues.Add(paramNameProp.Value.Instanced, texEntry.InstancedFullPath);
                            }
                        }
                    }
                }
            }

            //if the MIC had a StaticPermutationResource, this is already set
            if (ShaderMap is null)
            {
                LoadShaders(mat);
            }
        }

        private void LoadShaders(ExportEntry mat)
        {
            (ShaderMap, Shader[] shaders) = ShaderCacheManipulator.GetMaterialShaderMapAndShaders(mat, VERTEX_SHADER_TYPE_NAME, LIT_PIXEL_SHADER_TYPE_NAME, UNLIT_PIXEL_SHADER_TYPE_NAME);

            VertexShader = (VertexShaderType)shaders[0];
            PixelShader = (PixelShaderType)(shaders[1] ?? shaders[2]);
        }

        protected override void ReadMaterialInstanceConstant(ExportEntry matInst, PropertyCollection props)
        {
            base.ReadMaterialInstanceConstant(matInst, props);
            if (props.GetProp<ArrayProperty<StructProperty>>("ScalarParameterValues") is { } scalarValues)
            {
                foreach (StructProperty scalarValue in scalarValues)
                {
                    if (scalarValue.GetProp<NameProperty>("ParameterName") is { } paramNameProp
                        && scalarValue.GetProp<FloatProperty>("ParameterValue") is { } valProp)
                    {
                        ScalarParameterValues[paramNameProp.Value.Instanced] = valProp.Value;
                    }
                }
            }
            if (props.GetProp<ArrayProperty<StructProperty>>("VectorParameterValues") is { } vectorValues)
            {
                foreach (StructProperty vectorValue in vectorValues)
                {
                    if (vectorValue.GetProp<NameProperty>("ParameterName") is { } paramNameProp
                        && vectorValue.GetProp<StructProperty>("ParameterValue") is { } valProp)
                    {
                        VectorParameterValues[paramNameProp.Value.Instanced] = CommonStructs.GetLinearColor(valProp);
                    }
                }
            }
            if (props.GetProp<ArrayProperty<StructProperty>>("TextureParameterValues") is { } textureValues)
            {
                foreach (StructProperty textureValue in textureValues)
                {
                    if (textureValue.GetProp<NameProperty>("ParameterName") is { } paramNameProp
                        && textureValue.GetProp<ObjectProperty>("ParameterValue") is { } valProp)
                    {
                        TextureParameterValues[paramNameProp.Value.Instanced] = valProp.ResolveToEntry(matInst.FileRef)?.InstancedFullPath;
                    }
                }
            }

            if (ObjectBinary.From(matInst) is MaterialInstance binary)
            {
                foreach (int uIndex in binary.SM3StaticPermutationResource.UniformExpressionTextures)
                {
                    Uniform2DTextureExpressions.Add(matInst.FileRef.GetEntry(uIndex)?.InstancedFullPath);
                }
                LoadShaders(matInst);
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
                        if (TextureParameterValues.TryGetValue(texParamExpression.ParameterName.Instanced, out string texIfp)
                            && texIfp is not null)
                        {
                            TextureMap.TryGetValue(texIfp, out texture);
                        }
                        break;
                    default:
                        if ((uint)texExpression.TextureIndex < Uniform2DTextureExpressions.Count
                            && Uniform2DTextureExpressions[texExpression.TextureIndex] is {} texifp)
                        {
                            TextureMap.TryGetValue(texifp, out texture);
                        }
                        break;
                }
                textureCache.Add(texture);
            }
        }

        private void UpdateExpressions(UniformExpressionRenderContext uniformContext, 
            MaterialUniformExpression[] vectorExpressions, MaterialUniformExpression[] scalarExpressions, 
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
            foreach (MaterialUniformExpression vectorExpression in vectorExpressions)
            {
                LinearColor val = default;
                vectorExpression.GetNumberValue(uniformContext, ref val);
                vectorCache.Add((Vector4)val);
            }
        }
    }
}
