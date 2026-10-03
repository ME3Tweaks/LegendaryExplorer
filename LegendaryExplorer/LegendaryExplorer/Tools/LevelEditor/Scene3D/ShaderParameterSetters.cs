using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using LegendaryExplorerCore.Gammtek;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using SharpDX;
using SharpDX.Direct3D11;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace LegendaryExplorer.Tools.LevelEditor.Scene3D;

internal static class ShaderParameterSetters
{
    /// <summary>
    /// Writes the parameters for one of the base pass vertex shaders that <see cref="MaterialRenderProxy.SelectShaders"/> can choose
    /// </summary>
    public static void WriteBasePassVertexShaderValues(Shader vertexShader, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
    {
        switch (vertexShader)
        {
            case TBasePassVertexShader<FNullPolicy, FNullPolicy> noLightMapShader:
                noLightMapShader.WriteValues(buffer, context, mesh, mat);
                break;
            //used for both FDirectionalLightLightMapPolicy and FSHLightLightMapPolicy
            case TBasePassVertexShader<FDirectionalLightPolicy.VertexParametersType, FNullPolicy> directionalLightShader:
                directionalLightShader.WriteValues(buffer, context, mesh, mat);
                //w = 1 means directional, rather than a point light
                buffer.WriteVal(directionalLightShader.LightMapVertexParams.LightDirection, new Vector4(context.Lighting.GetLightDirection(context.Camera), 1));
                break;
            case null:
                break;
            default:
                throw new NotSupportedException($"{vertexShader.ShaderType} is not supported by the renderer");
        }
    }

    /// <summary>
    /// Writes the parameters for one of the base pass pixel shaders that <see cref="MaterialRenderProxy.SelectShaders"/> can choose
    /// </summary>
    public static void WriteBasePassPixelShaderValues(Shader pixelShader, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
    {
        switch (pixelShader)
        {
            case TBasePassPixelShader<FNullPolicy> noLightMapShader:
                noLightMapShader.WriteValues(buffer, context, mesh, mat);
                break;
            case TBasePassPixelShader<FDirectionalLightLightMapPolicy.PixelParametersType> directionalLightShader:
                directionalLightShader.WriteValues(buffer, context, mesh, mat);
                WriteDirectionalLight(buffer, context, directionalLightShader.PixelParams.LightColorAndFalloffExponent, directionalLightShader.PixelParams.bReceiveDynamicShadows);
                break;
            case TBasePassPixelShader<FSHLightLightMapPolicy.PixelParametersType> shLightShader:
                shLightShader.WriteValues(buffer, context, mesh, mat);
                WriteDirectionalLight(buffer, context, shLightShader.PixelParams.LightColorAndFalloffExponent, shLightShader.PixelParams.bReceiveDynamicShadows);
                //All 0, so that the SH contributes nothing. The sky lighting provides ambient instead.
                //(Layout, if this is ever used: float4[7]. [0] is the RGB constant term, then 2 float4s each of the remaining 8 coefficients for R, G, then B)
                buffer.WriteVal(shLightShader.PixelParams.WorldIncidentLighting, new Fixed7<Vector4>());
                break;
            case null:
                break;
            default:
                throw new NotSupportedException($"{pixelShader.ShaderType} is not supported by the renderer");
        }

        static void WriteDirectionalLight(Span<byte> buffer, MeshRenderContext context, FShaderParameter lightColorParam, FShaderParameter bReceiveDynamicShadowsParam)
        {
            LinearColor lightColor = context.Lighting.LightColor;
            //w is the falloff exponent, which is unused for directional lights
            buffer.WriteVal(lightColorParam, new Vector4(lightColor.R, lightColor.G, lightColor.B, 0));
            buffer.WriteVal(bReceiveDynamicShadowsParam, 0);
        }
    }

    public static void WriteValues<LightMapPolicy, DensityPolicy>(this TBasePassVertexShader<LightMapPolicy, DensityPolicy> shader,
        Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
        where LightMapPolicy : struct, IVertexParametersType where DensityPolicy : struct, IVertexShaderParametersType
    {
        if (shader.VertexFactoryParameters.Parameters is not FLocalVertexFactoryShaderParameters vertexFactoryParams)
        {
            throw new NotSupportedException($"{shader.VertexFactoryParameters.VertexFactoryType} is not supported by the renderer");
        }
        vertexFactoryParams.WriteValues(buffer, context, mesh, mat);
        shader.HeightFogParameters.WriteValues(buffer, context, mesh, mat);
        shader.MaterialParameters.WriteValues(buffer, context, mesh, mat);
        //TODO: DensityPolicy params
    }
    public static void WriteValues<LightMapPolicy>(this TBasePassPixelShader<LightMapPolicy> shader,
        Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
        where LightMapPolicy : struct, IPixelParametersType
    {
        shader.MaterialParameters.WriteValues(buffer, context, mesh, mat);
        bool drawUnlit = mat.IsUnlit;
        //Matches UE3: lit materials get their ambient from the sky, unlit ones are just their emissive
        buffer.WriteVal(shader.AmbientColorAndSkyFactor, drawUnlit ? new LinearColor(1, 1, 1, 0) : new LinearColor(0, 0, 0, 1));
        Vector3 upperSkyColor = Vector3.Zero;
        Vector3 lowerSkyColor = Vector3.Zero;
        if (!drawUnlit)
        {
            LinearColor upper = context.Lighting.UpperSkyColor;
            LinearColor lower = context.Lighting.LowerSkyColor;
            upperSkyColor = new Vector3(upper.R, upper.G, upper.B);
            lowerSkyColor = new Vector3(lower.R, lower.G, lower.B);
        }
        buffer.WriteVal(shader.UpperSkyColor, upperSkyColor);
        buffer.WriteVal(shader.LowerSkyColor, lowerSkyColor);
        buffer.WriteVal(shader.CharacterMask, 1f);
        buffer.WriteVal(shader.MotionBlurMask, 0f);
        if (shader.TranslucencyDepth.IsBound())
        {
            //no idea what this should be
            buffer.WriteVal(shader.TranslucencyDepth, Vector4.One);
        }
    }

    public static void WriteValues(this ref FMaterialVertexShaderParameters p, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
    {
        buffer.WriteVal(p.CameraWorldPosition, context.Camera.EyePosition);
        buffer.WriteVal(p.ObjectWorldPositionAndRadius, new Vector4(mesh.TransformedBounds.Origin, mesh.TransformedBounds.SphereRadius));
        buffer.WriteVal(p.ObjectOrientation, mesh.LocalToWorld.GetAxis(2).Normal());
        buffer.WriteVal(p.WindDirectionAndSpeed, Vector4.Zero);
        buffer.WriteVal(p.FoliageImpulseDirection, Vector3.Zero);
        buffer.WriteVal(p.FoliageNormalizedRotationAxisAndAngle, Vector4.UnitZ);

        (List<Vector4> scalarParamValues, List<Vector4> vectorParamValues) = mat.GetCachedVertexParameters(context);
        foreach (TUniformParameter<FShaderParameter> scalarParam in p.UniformVertexScalarShaderParameters)
        {
            WriteScalarUniform(buffer, scalarParam, scalarParamValues);
        }
        foreach (TUniformParameter<FShaderParameter> vectorParam in p.UniformVertexVectorShaderParameters)
        {
            buffer.WriteVal(vectorParam.Param, vectorParamValues[vectorParam.Index]);
        }
    }
    public static void WriteValues(this ref FMaterialPixelShaderParameters p, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
    {
        buffer.WriteVal(p.CameraWorldPosition, context.Camera.EyePosition);
        buffer.WriteVal(p.ObjectWorldPositionAndRadius, new Vector4(mesh.TransformedBounds.Origin, mesh.TransformedBounds.SphereRadius));
        buffer.WriteVal(p.ObjectOrientation, mesh.LocalToWorld.GetAxis(2).Normal());
        buffer.WriteVal(p.WindDirectionAndSpeed, Vector4.Zero);
        buffer.WriteVal(p.FoliageImpulseDirection, Vector3.Zero);
        buffer.WriteVal(p.FoliageNormalizedRotationAxisAndAngle, Vector4.UnitZ);

        (List<Vector4> scalarParamValues, 
            List<Vector4> vectorParamValues, 
            List<PreviewTextureCache.TextureEntry> tex2dParamValues, 
            List<PreviewTextureCache.TextureEntry> cubeMapParamValues) = mat.GetCachedPixelParameters(context);

        foreach (TUniformParameter<FShaderParameter> scalarParam in p.UniformPixelScalarShaderParameters)
        {
            WriteScalarUniform(buffer, scalarParam, scalarParamValues);
        }
        foreach (TUniformParameter<FShaderParameter> vectorParam in p.UniformPixelVectorShaderParameters)
        {
            buffer.WriteVal(vectorParam.Param, vectorParamValues[vectorParam.Index]);
        }
        foreach (TUniformParameter<FShaderResourceParameter> texParam in p.UniformPixel2DShaderResourceParameters)
        {
            PreviewTextureCache.TextureEntry texture = tex2dParamValues[texParam.Index];
            context.ImmediateContext.PixelShader.SetShaderResource(texParam.Param.BaseIndex, texture?.LinearTextureView ?? context.WhiteTexView);
            if (texture is not null)
            {
                //materials can rely on clamping, e.g. to confine a decal-like texture to one region of the UVs
                context.ImmediateContext.PixelShader.SetSampler(texParam.Param.SamplerIndex, context.GetSamplerState(texture.AddressU, texture.AddressV));
            }
        }
        foreach (TUniformParameter<FShaderResourceParameter> cubeParam in p.UniformPixelCubeShaderResourceParameters)
        {
            ShaderResourceView view = cubeMapParamValues[cubeParam.Index]?.LinearTextureView ?? context.WhiteTextureCubeView;
            context.ImmediateContext.PixelShader.SetShaderResource(cubeParam.Param.BaseIndex, view);
        }

        SceneCamera camera = context.Camera;
        buffer.WriteVal(p.LocalToWorld, mesh.LocalToWorld);
        buffer.WriteVal(p.WorldToLocal, mesh.WorldToLocal);
        Matrix4x4 viewMatrix = camera.ViewMatrix;
        //float3x3 shader parameters have each row padded to 4 floats, so a Matrix4x4's layout matches (the 4th column lands in the padding)
        buffer.WriteVal(p.WorldToView, viewMatrix);
        Matrix4x4.Invert(viewMatrix, out Matrix4x4 inverseViewMatrix);
        Matrix4x4 projectionMatrix = camera.ProjectionMatrix;
        Matrix4x4.Invert(projectionMatrix, out Matrix4x4 inverseProjectionMatrix);
        buffer.WriteVal(p.InvViewProjection, inverseProjectionMatrix * inverseViewMatrix);
        buffer.WriteVal(p.ViewProjection, viewMatrix * projectionMatrix);

        p.SceneTextureParameters.WriteValues(buffer, context, mesh, mat);

        //UE3 flips this for meshes with reversed culling (see FMaterialPixelShaderParameters::SetMesh). Backfaces aren't rendered in a separate pass
        buffer.WriteVal(p.TwoSidedSign, mesh.LocalToWorld.GetDeterminant() < 0 ? -1f : 1f);
        buffer.WriteVal(p.InvGamma, 1f / (1f /*GammaCorrection*/ ));
        buffer.WriteVal(p.DecalFarPlaneDistance, 65536f); //actual value is stored on the BioDecalComponent

        //these are used for ParticleSystem rendering
        buffer.WriteVal(p.ObjectPostProjectionPosition, Vector3.Zero);
        buffer.WriteVal(p.ObjectMacroUVScales, Vector4.Zero);
        buffer.WriteVal(p.ObjectNDCPosition, Vector3.Zero);
        buffer.WriteVal(p.OcclusionPercentage, 0f);

        const int isFading = 0;
        buffer.WriteVal(p.EnableScreenDoorFade, isFading);
        if (isFading > 0)
        {
            buffer.WriteVal(p.ScreenDoorFadeSettings, Vector4.Zero);
            buffer.WriteVal(p.ScreenDoorFadeSettings2, Vector4.Zero);
        }
        if (p.ScreenDoorNoiseTexture.IsBound())
        {
            //only sampled when EnableScreenDoorFade is set, which it never is
            context.ImmediateContext.PixelShader.SetShaderResource(p.ScreenDoorNoiseTexture.BaseIndex, null);
        }
        //Bioware addition: (DirectScale, IndirectScale, 0, 0), see FMaterialPixelShaderParameters::SetMesh in LE3
        buffer.WriteVal(p.WrapLightingParameters, new Vector4(PreviewLighting.WrapLightingDirectScale, PreviewLighting.WrapLightingIndirectScale, 0, 0));
    }

    public static void WriteValues(this ref FSceneTextureShaderParameters p, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
    {
        //TODO: SceneColor and SceneDepth aren't available to materials yet. Unbound textures sample as 0
        if (p.SceneColorTexture.IsBound())
        {
            context.ImmediateContext.PixelShader.SetShaderResource(p.SceneColorTexture.BaseIndex, null);
        }
        if (p.SceneDepthTexture.IsBound())
        {
            context.ImmediateContext.PixelShader.SetShaderResource(p.SceneDepthTexture.BaseIndex, null);
        }

        if (p.ScreenPositionScaleBias.IsBound())
        {
            buffer.WriteVal(p.ScreenPositionScaleBias, new Vector4(1f / 2f, 1f / -2f, (context.Height / 2f + 0.5f) / context.Height, (context.Width / 2f + 0.5f) / context.Width));

        }
        if (p.MinZ_MaxZRatio.IsBound())
        {
            //The projection matrix is already reversed (see SceneCamera.ProjectionMatrix), so these are the values UE3 computes for inverted Z
            float depthMul = context.Camera.ProjectionMatrix[2, 2];
            float depthAdd = context.Camera.ProjectionMatrix[3, 2];
            buffer.WriteVal(p.MinZ_MaxZRatio, new Vector4(depthAdd, depthMul, 1f / depthAdd, depthMul / depthAdd));
        }
    }

    public static void WriteValues(this ref FHeightFogVertexShaderParameters p, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
    {
        //these values disable fog
        buffer.WriteVal(p.FogExtinctionDistance, new Vector4(float.MaxValue));
        var fogInScatteringValue = new Fixed4<LinearColor>();
        fogInScatteringValue[0] = LinearColor.Black;
        fogInScatteringValue[1] = LinearColor.Black;
        fogInScatteringValue[2] = LinearColor.Black;
        fogInScatteringValue[3] = LinearColor.Black;
        buffer.WriteVal(p.FogInScattering, fogInScatteringValue);
        buffer.WriteVal(p.FogDistanceScale, Vector4.Zero);
        buffer.WriteVal(p.FogMinHeight, Vector4.Zero);
        buffer.WriteVal(p.FogMaxHeight, Vector4.Zero);
        buffer.WriteVal(p.FogStartDistance, Vector4.Zero);
    }

    public static void WriteValues(this FLocalVertexFactoryShaderParameters p, Span<byte> buffer, MeshRenderContext context, Mesh<LEVertex> mesh, MaterialRenderProxy mat)
    {
            buffer.WriteVal(p.LocalToWorld, mesh.LocalToWorld);
            buffer.WriteVal(p.WorldToLocal, mesh.WorldToLocal);
            buffer.WriteVal(p.LocalToWorldRotDeterminantFlip, mesh.LocalToWorld.GetDeterminant() >= 0 ? 1f : -1f);
    }

    //Scalar uniform expressions are packed 4 to a float4 (UniformPixelScalars_N etc.). A parameter bound to a whole float4
    //has that float4's index, so all 4 components must be written, not just the first.
    private static void WriteScalarUniform(Span<byte> buffer, TUniformParameter<FShaderParameter> scalarParam, List<Vector4> scalarValues)
    {
        if (scalarParam.Param.NumBytes > sizeof(float))
        {
            buffer.WriteVal(scalarParam.Param, scalarValues[scalarParam.Index]);
        }
        else
        {
            buffer.WriteVal(scalarParam.Param, scalarValues[scalarParam.Index / 4][scalarParam.Index % 4]);
        }
    }

    private static unsafe void WriteVal<T>(this Span<byte> buff, FShaderParameter param, T val) where T : unmanaged
    {
        if (!param.IsBound())
        {
            return;
        }
        //if (sizeof(T) != param.NumBytes 
        //    && !(typeof(T) == typeof(Matrix3x3) && param.NumBytes == 44) 
        //    && Debugger.IsAttached)
        //{
        //    Debugger.Break();
        //}
        int bytesToWrite = Math.Min(sizeof(T), param.NumBytes);
        val.AsBytes()[..bytesToWrite].CopyTo(buff[param.BaseIndex..]);
    }
}
