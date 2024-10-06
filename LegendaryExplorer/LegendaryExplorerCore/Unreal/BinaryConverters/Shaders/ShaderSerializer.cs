using System;
using System.Diagnostics;
using System.IO;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public partial class SerializingContainer
    {
        public void Serialize(ref Shader shader)
        {
            if (IsSaving)
            {
                Serialize(ref shader.ShaderType);
                Serialize(ref shader.Guid);
                shader.Serialize(this).SetPosition(this);
                return;
            }
            NameReference shaderType = default;
            Guid id = default;
            Serialize(ref shaderType);
            Serialize(ref id);
            
            if (Game is not MEGame.LE3)
            {
                shader = new UnparsedShader
                {
                    ShaderType = shaderType,
                    Guid = id,
                };
                shader.Serialize(this);
                return;
            }
            switch (shaderType.Name)
            {
                case "FGFxPixelShaderSDRGFx_PS_CxformMultiply2Texture":
                case "FGFxPixelShaderSDRGFx_PS_CxformGouraudMultiplyTexture":
                case "FGFxPixelShaderSDRGFx_PS_CxformGouraud":
                case "FGFxPixelShaderSDRGFx_PS_TextTexture":
                case "FGFxPixelShaderSDRGFx_PS_CxformGouraudMultiplyNoAddAlpha":
                case "FGFxPixelShaderSDRGFx_PS_CxformGouraudMultiply":
                case "FGFxPixelShaderSDRGFx_PS_CxformGouraudNoAddAlpha":
                case "FGFxPixelShaderSDRGFx_PS_Cxform2Texture":
                case "FGFxPixelShaderSDRGFx_PS_CxformGouraudTexture":
                case "FGFxPixelShaderSDRGFx_PS_TextTextureSRGBMultiply":
                case "FGFxPixelShaderSDRGFx_PS_TextTextureSRGB":
                case "FGFxPixelShaderSDRGFx_PS_TextTextureColorMultiply":
                case "FGFxPixelShaderSDRGFx_PS_TextTextureColor":
                case "FGFxPixelShaderSDRGFx_PS_CxformTextureMultiply":
                case "FGFxPixelShaderSDRGFx_PS_CxformTexture":
                case "FGFxPixelShaderSDRGFx_PS_SolidColor":
                    shader = new FGFxPixelShader();
                    break;
                case "THeightFogPixelShader<4>":
                case "THeightFogPixelShader<1>":
                    shader = new THeightFogPixelShader();
                    break;
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFHighQualityManualPCF": // Verified LE2
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFHighQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFHighQualityHwPCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFMediumQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFMediumQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFMediumQualityHwPCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFLowQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFLowQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFLowQualityHwPCF":
                    shader = new TBranchingPCFModProjectionPixelShader<FSpotLightPolicy.ModShadowPixelParamsType>();
                    break;
                case "FGFxVertexShader<GFx_VS_Glyph>":
                case "FGFxVertexShader<GFx_VS_XY16iCF32_T2>":
                case "FGFxVertexShader<GFx_VS_XY16iCF32_NoTexNoAlpha>":
                case "FGFxVertexShader<GFx_VS_Strip>":
                case "FGFxVertexShader<GFx_VS_XY16iCF32_NoTex>":
                case "FGFxVertexShader<GFx_VS_XY16iCF32>":
                case "FGFxVertexShader<GFx_VS_XY16iC32>":
                    shader = new FGFxVertexShader();
                    break;
                case "FResolveVertexShader":
                    shader = new FResolveVertexShader();
                    break;
                case "FReconstructHDRVertexShader":
                    shader = new FReconstructHDRVertexShader();
                    break;
                case "FLDRExtractVertexShader":
                    shader = new FLDRExtractVertexShader();
                    break;
                case "FMotionBlurVertexShader":
                    shader = new FMotionBlurVertexShader();
                    break;
                case "FBinkVertexShader":
                    shader = new FBinkVertexShader();
                    break;
                case "FOneColorVertexShader":
                    shader = new FOneColorVertexShader();
                    break;
                case "FGammaCorrectionVertexShader":
                    shader = new FGammaCorrectionVertexShader();
                    break;
                case "FNULLPixelShader":
                    shader = new FNULLPixelShader();
                    break;
                case "FHorizonBasedAOVertexShader":
                    shader = new FHorizonBasedAOVertexShader();
                    break;
                case "FModShadowVolumeVertexShader":
                    shader = new FModShadowVolumeVertexShader();
                    break;
                case "FOcclusionQueryVertexShader<0>":
                case "FOcclusionQueryVertexShader<NUM_CUBE_VERTICES>":
                    shader = new FOcclusionQueryVertexShader();
                    break;
                case "FModShadowProjectionVertexShader":
                    shader = new FModShadowProjectionVertexShader();
                    break;
                case "FLUTBlenderVertexShader":
                    shader = new FLUTBlenderVertexShader();
                    break;
                case "FPostProcessAAVertexShader":
                    shader = new FPostProcessAAVertexShader();
                    break;
                case "FShadowProjectionVertexShader":
                    shader = new FShadowProjectionVertexShader();
                    break;
                case "FScreenVertexShader":
                    shader = new FScreenVertexShader();
                    break;
                case "FFluidVertexShader":
                    shader = new FFluidVertexShader();
                    break;
                case "FEdgePreservingFilterVertexShader":
                    shader = new FEdgePreservingFilterVertexShader();
                    break;
                case "FLightFunctionVertexShader":
                    shader = new FLightFunctionVertexShader();
                    break;
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFMediumQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFHighQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFLowQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFHighQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFHighQualityHwPCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFMediumQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFMediumQualityHwPCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFLowQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFLowQualityHwPCF":
                    shader = new TBranchingPCFModProjectionPixelShader<FNullPolicy>();
                    break;
                case "FResolveSingleSamplePixelShader":
                    shader = new FResolveSingleSamplePixelShader();
                    break;
                case "FResolveDepthPixelShader":
                    shader = new FResolveDepthPixelShader();
                    break;
                case "TModShadowVolumePixelShaderFPointLightPolicy":
                    shader = new TModShadowVolumePixelShader<FPointLightPolicy.ModShadowPixelParamsType>();
                    break;
                case "FGammaCorrectionPixelShader":
                    shader = new FGammaCorrectionPixelShader();
                    break;
                case "FGFxPixelShaderHDRGFx_PS_CxformMultiply2Texture":
                case "FGFxPixelShaderHDRGFx_PS_CxformGouraudMultiplyTexture":
                case "FGFxPixelShaderHDRGFx_PS_CxformGouraud":
                case "FGFxPixelShaderHDRGFx_PS_CxformGouraudMultiplyNoAddAlpha":
                case "FGFxPixelShaderHDRGFx_PS_CxformGouraudNoAddAlpha":
                case "FGFxPixelShaderHDRGFx_PS_CxformGouraudMultiply":
                case "FGFxPixelShaderHDRGFx_PS_Cxform2Texture":
                case "FGFxPixelShaderHDRGFx_PS_CxformGouraudTexture":
                case "FGFxPixelShaderHDRGFx_PS_CxformTexture":
                case "FGFxPixelShaderHDRGFx_PS_TextTextureSRGBMultiply":
                case "FGFxPixelShaderHDRGFx_PS_CxformTextureMultiply":
                case "FGFxPixelShaderHDRGFx_PS_TextTextureSRGB":
                case "FGFxPixelShaderHDRGFx_PS_TextTextureColorMultiply":
                case "FGFxPixelShaderHDRGFx_PS_TextTextureColor":
                case "FGFxPixelShaderHDRGFx_PS_TextTexture":
                case "FGFxPixelShaderHDRGFx_PS_SolidColor":
                    shader = new FGFxPixelShaderHDR();
                    break;
                case "FDownsampleSceneDepthPixelShader":
                    shader = new FDownsampleSceneDepthPixelShader();
                    break;
                case "FFXAA3BlendPixelShader":
                    shader = new FFXAA3BlendPixelShader();
                    break;
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFLowQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFLowQualityHwPCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFLowQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFMediumQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFMediumQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFMediumQualityHwPCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFHighQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFHighQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFHighQualityHwPCF":
                    shader = new TBranchingPCFModProjectionPixelShader<FPointLightPolicy.ModShadowPixelParamsType>();
                    break;
                case "FTexturedCalibrationBoxHDRPixelShader":
                    shader = new FTexturedCalibrationBoxHDRPixelShader();
                    break;
                case "FScreenPixelShader":
                    shader = new FScreenPixelShader();
                    break;
                case "FHBAOApplyPixelShader":
                    shader = new FHBAOApplyPixelShader();
                    break;
                case "FCopyVariancePixelShader":
                    shader = new FCopyVariancePixelShader();
                    break;
                case "FSimpleElementHitProxyPixelShader":
                    shader = new FSimpleElementHitProxyPixelShader();
                    break;
                case "FMotionBlurPixelShader":
                case "FMotionBlurPixelShaderDynamicVelocitiesOnly":
                    shader = new FMotionBlurPixelShader();
                    break;
                case "FDownsampleDepthVertexShader":
                    shader = new FDownsampleDepthVertexShader();
                    break;
                case "FAmbientOcclusionVertexShader":
                    shader = new FAmbientOcclusionVertexShader();
                    break;
                case "FCalibrationBoxHDRPixelShader":
                    shader = new FCalibrationBoxHDRPixelShader();
                    break;
                case "TFilterVertexShader<16>":
                case "TFilterVertexShader<15>":
                case "TFilterVertexShader<14>":
                case "TFilterVertexShader<13>":
                case "TFilterVertexShader<12>":
                case "TFilterVertexShader<11>":
                case "TFilterVertexShader<10>":
                case "TFilterVertexShader<9>":
                case "TFilterVertexShader<8>":
                case "TFilterVertexShader<7>":
                case "TFilterVertexShader<6>":
                case "TFilterVertexShader<5>":
                case "TFilterVertexShader<4>":
                case "TFilterVertexShader<3>":
                case "TFilterVertexShader<2>":
                case "TFilterVertexShader<1>":
                    shader = new TFilterVertexShader();
                    break;
                case "TDOFAndBloomGatherVertexShader<MAX_FILTER_SAMPLES>":
                case "TDOFAndBloomGatherVertexShader<NumFPFilterSamples>":
                case "TDOFAndBloomGatherVertexShader<1>":
                    shader = new TDOFAndBloomGatherVertexShader();
                    break;
                case "FShaderComplexityAccumulatePixelShader":
                    shader = new FShaderComplexityAccumulatePixelShader();
                    break;
                case "FDistortionApplyScreenVertexShader":
                    shader = new FDistortionApplyScreenVertexShader();
                    break;
                case "FSimpleElementVertexShader":
                    shader = new FSimpleElementVertexShader();
                    break;
                case "FDownsampleLightShaftsVertexShader":
                    shader = new FDownsampleLightShaftsVertexShader();
                    break;
                case "FRadialBlurVertexShader":
                    shader = new FRadialBlurVertexShader();
                    break;
                case "FOneColorPixelShader":
                    shader = new FOneColorPixelShader();
                    break;
                case "FDOFAndBloomBlendVertexShader":
                    shader = new FDOFAndBloomBlendVertexShader();
                    break;
                case "FHistoryUpdateVertexShader":
                    shader = new FHistoryUpdateVertexShader();
                    break;
                case "FReconstructHDRPixelShader<FALSE>":
                case "FReconstructHDRPixelShader<TRUE>":
                    shader = new FReconstructHDRPixelShader();
                    break;
                case "FSimpleElementPixelShader":
                    shader = new FSimpleElementPixelShader();
                    break;
                case "TFilterPixelShader<16>":
                case "TFilterPixelShader<15>":
                case "TFilterPixelShader<14>":
                case "TFilterPixelShader<13>":
                case "TFilterPixelShader<12>":
                case "TFilterPixelShader<11>":
                case "TFilterPixelShader<10>":
                case "TFilterPixelShader<9>":
                case "TFilterPixelShader<8>":
                case "TFilterPixelShader<7>":
                case "TFilterPixelShader<6>":
                case "TFilterPixelShader<5>":
                case "TFilterPixelShader<4>":
                case "TFilterPixelShader<3>":
                case "TFilterPixelShader<2>":
                case "TFilterPixelShader<1>":
                    shader = new TFilterPixelShader();
                    break;
                case "FShadowVolumeVertexShader":
                    shader = new FShadowVolumeVertexShader();
                    break;
                case "FSFXUberPostProcessBlendPixelShader0011111":
                case "FSFXUberPostProcessBlendPixelShader0101001":
                case "FSFXUberPostProcessBlendPixelShader1111001":
                case "FSFXUberPostProcessBlendPixelShader1010001":
                case "FSFXUberPostProcessBlendPixelShader0110010":
                case "FSFXUberPostProcessBlendPixelShader1100010":
                case "FSFXUberPostProcessBlendPixelShader0011100":
                case "FSFXUberPostProcessBlendPixelShader0101010":
                case "FSFXUberPostProcessBlendPixelShader1111010":
                case "FSFXUberPostProcessBlendPixelShader1010010":
                case "FSFXUberPostProcessBlendPixelShader0010110":
                case "FSFXUberPostProcessBlendPixelShader0101111":
                case "FSFXUberPostProcessBlendPixelShader1111111":
                case "FSFXUberPostProcessBlendPixelShader1010111":
                case "FSFXUberPostProcessBlendPixelShader0100011":
                case "FSFXUberPostProcessBlendPixelShader1110011":
                case "FSFXUberPostProcessBlendPixelShader1011011":
                case "FSFXUberPostProcessBlendPixelShader1001111":
                case "FSFXUberPostProcessBlendPixelShader1000101":
                case "FSFXUberPostProcessBlendPixelShader0101100":
                case "FSFXUberPostProcessBlendPixelShader1111100":
                case "FSFXUberPostProcessBlendPixelShader1010100":
                case "FSFXUberPostProcessBlendPixelShader0011110":
                case "FSFXUberPostProcessBlendPixelShader0101000":
                case "FSFXUberPostProcessBlendPixelShader1111000":
                case "FSFXUberPostProcessBlendPixelShader1010000":
                case "FSFXUberPostProcessBlendPixelShader0110011":
                case "FSFXUberPostProcessBlendPixelShader1100011":
                case "FSFXUberPostProcessBlendPixelShader0011101":
                case "FSFXUberPostProcessBlendPixelShader0101011":
                case "FSFXUberPostProcessBlendPixelShader1111011":
                case "FSFXUberPostProcessBlendPixelShader1010011":
                case "FSFXUberPostProcessBlendPixelShader0010111":
                case "FSFXUberPostProcessBlendPixelShader0101110":
                case "FSFXUberPostProcessBlendPixelShader1111110":
                case "FSFXUberPostProcessBlendPixelShader1010110":
                case "FSFXUberPostProcessBlendPixelShader0100010":
                case "FSFXUberPostProcessBlendPixelShader1110010":
                case "FSFXUberPostProcessBlendPixelShader1011010":
                case "FSFXUberPostProcessBlendPixelShader1001110":
                case "FSFXUberPostProcessBlendPixelShader1000100":
                case "FSFXUberPostProcessBlendPixelShader0101101":
                case "FSFXUberPostProcessBlendPixelShader1111101":
                case "FSFXUberPostProcessBlendPixelShader1010101":
                case "FSFXUberPostProcessBlendPixelShader0100111":
                case "FSFXUberPostProcessBlendPixelShader0111001":
                case "FSFXUberPostProcessBlendPixelShader1110111":
                case "FSFXUberPostProcessBlendPixelShader1101001":
                case "FSFXUberPostProcessBlendPixelShader1011111":
                case "FSFXUberPostProcessBlendPixelShader1001011":
                case "FSFXUberPostProcessBlendPixelShader0100110":
                case "FSFXUberPostProcessBlendPixelShader0111000":
                case "FSFXUberPostProcessBlendPixelShader1110110":
                case "FSFXUberPostProcessBlendPixelShader1101000":
                case "FSFXUberPostProcessBlendPixelShader1011110":
                case "FSFXUberPostProcessBlendPixelShader1001010":
                case "FSFXUberPostProcessBlendPixelShader0100101":
                case "FSFXUberPostProcessBlendPixelShader0110100":
                case "FSFXUberPostProcessBlendPixelShader0111011":
                case "FSFXUberPostProcessBlendPixelShader0111110":
                case "FSFXUberPostProcessBlendPixelShader1110101":
                case "FSFXUberPostProcessBlendPixelShader1101110":
                case "FSFXUberPostProcessBlendPixelShader1101011":
                case "FSFXUberPostProcessBlendPixelShader1100100":
                case "FSFXUberPostProcessBlendPixelShader1011101":
                case "FSFXUberPostProcessBlendPixelShader1001001":
                case "FSFXUberPostProcessBlendPixelShader0100100":
                case "FSFXUberPostProcessBlendPixelShader0110101":
                case "FSFXUberPostProcessBlendPixelShader0111010":
                case "FSFXUberPostProcessBlendPixelShader0111111":
                case "FSFXUberPostProcessBlendPixelShader1110100":
                case "FSFXUberPostProcessBlendPixelShader1101111":
                case "FSFXUberPostProcessBlendPixelShader1101010":
                case "FSFXUberPostProcessBlendPixelShader1100101":
                case "FSFXUberPostProcessBlendPixelShader1011100":
                case "FSFXUberPostProcessBlendPixelShader1001000":
                case "FSFXUberPostProcessBlendPixelShader0011000":
                case "FSFXUberPostProcessBlendPixelShader0100001":
                case "FSFXUberPostProcessBlendPixelShader1110001":
                case "FSFXUberPostProcessBlendPixelShader1011001":
                case "FSFXUberPostProcessBlendPixelShader1001101":
                case "FSFXUberPostProcessBlendPixelShader1000111":
                case "FSFXUberPostProcessBlendPixelShader1000010":
                case "FSFXUberPostProcessBlendPixelShader0011001":
                case "FSFXUberPostProcessBlendPixelShader0100000":
                case "FSFXUberPostProcessBlendPixelShader1110000":
                case "FSFXUberPostProcessBlendPixelShader1011000":
                case "FSFXUberPostProcessBlendPixelShader1001100":
                case "FSFXUberPostProcessBlendPixelShader1000110":
                case "FSFXUberPostProcessBlendPixelShader1000011":
                case "FSFXUberPostProcessBlendPixelShader0011010":
                case "FSFXUberPostProcessBlendPixelShader0110111":
                case "FSFXUberPostProcessBlendPixelShader0111101":
                case "FSFXUberPostProcessBlendPixelShader1101101":
                case "FSFXUberPostProcessBlendPixelShader1100111":
                case "FSFXUberPostProcessBlendPixelShader1000000":
                case "FSFXUberPostProcessBlendPixelShader0011011":
                case "FSFXUberPostProcessBlendPixelShader0110110":
                case "FSFXUberPostProcessBlendPixelShader0111100":
                case "FSFXUberPostProcessBlendPixelShader1101100":
                case "FSFXUberPostProcessBlendPixelShader1100110":
                case "FSFXUberPostProcessBlendPixelShader1000001":
                case "FSFXUberPostProcessBlendPixelShader0110001":
                case "FSFXUberPostProcessBlendPixelShader1100001":
                case "FSFXUberPostProcessBlendPixelShader0110000":
                case "FSFXUberPostProcessBlendPixelShader1100000":
                case "FSFXUberPostProcessBlendPixelShader0000100":
                case "FSFXUberPostProcessBlendPixelShader0010101":
                case "FSFXUberPostProcessBlendPixelShader0000101":
                case "FSFXUberPostProcessBlendPixelShader0010100":
                case "FSFXUberPostProcessBlendPixelShader0010011":
                case "FSFXUberPostProcessBlendPixelShader0010010":
                case "FSFXUberPostProcessBlendPixelShader0010001":
                case "FSFXUberPostProcessBlendPixelShader0010000":
                case "FSFXUberPostProcessBlendPixelShader0001111":
                case "FSFXUberPostProcessBlendPixelShader0001110":
                case "FSFXUberPostProcessBlendPixelShader0001101":
                case "FSFXUberPostProcessBlendPixelShader0001100":
                case "FSFXUberPostProcessBlendPixelShader0001011":
                case "FSFXUberPostProcessBlendPixelShader0001010":
                case "FSFXUberPostProcessBlendPixelShader0001001":
                case "FSFXUberPostProcessBlendPixelShader0001000":
                case "FSFXUberPostProcessBlendPixelShader0000111":
                case "FSFXUberPostProcessBlendPixelShader0000110":
                case "FSFXUberPostProcessBlendPixelShader0000011":
                case "FSFXUberPostProcessBlendPixelShader0000010":
                case "FSFXUberPostProcessBlendPixelShader0000001":
                case "FSFXUberPostProcessBlendPixelShader0000000":
                    shader = new FSFXUberPostProcessBlendPixelShader();
                    break;
                case "FUberPostProcessVertexShader":
                    shader = new FUberPostProcessVertexShader();
                    break;
                case "TModShadowVolumePixelShaderFSpotLightPolicy":
                    shader = new TModShadowVolumePixelShader<FSpotLightPolicy.ModShadowPixelParamsType>();
                    break;
                case "FSFXUberHalfResPixelShader0001":
                case "FSFXUberHalfResPixelShader0010":
                case "FSFXUberHalfResPixelShader1000":
                case "FSFXUberHalfResPixelShader1011":
                case "FSFXUberHalfResPixelShader0000":
                case "FSFXUberHalfResPixelShader0011":
                case "FSFXUberHalfResPixelShader1001":
                case "FSFXUberHalfResPixelShader1010":
                case "FUberHalfResPixelShader101":
                case "FUberHalfResPixelShader100":
                case "FUberHalfResPixelShader001":
                case "FUberHalfResPixelShader000":
                    shader = new FUberHalfResPixelShader();
                    break;
                case "FUberPostProcessBlendPixelShader001":
                case "FUberPostProcessBlendPixelShader010":
                case "FUberPostProcessBlendPixelShader100":
                case "FUberPostProcessBlendPixelShader111":
                case "FUberPostProcessBlendPixelShader000":
                case "FUberPostProcessBlendPixelShader011":
                case "FUberPostProcessBlendPixelShader101":
                case "FUberPostProcessBlendPixelShader110":
                    shader = new FUberPostProcessBlendPixelShader();
                    break;
                case "TAOApplyPixelShader<AOApply_ReadFromAOHistory>":
                case "TAOApplyPixelShader<AOApply_Normal>":
                    shader = new TAOApplyPixelShader();
                    break;
                case "TModShadowProjectionPixelShaderFSpotLightPolicyF16SampleManualPCF":
                case "TModShadowProjectionPixelShaderFSpotLightPolicyF16SampleFetch4PCF":
                case "TModShadowProjectionPixelShaderFSpotLightPolicyF16SampleHwPCF":
                case "TModShadowProjectionPixelShaderFSpotLightPolicyF4SampleManualPCF":
                case "TModShadowProjectionPixelShaderFSpotLightPolicyF4SampleHwPCF":
                    shader = new TModShadowProjectionPixelShader<FSpotLightPolicy.ModShadowPixelParamsType>();
                    break;
                case "FFluidApplyPixelShader":
                    shader = new FFluidApplyPixelShader();
                    break;
                case "TModShadowProjectionPixelShaderFDirectionalLightPolicyF16SampleManualPCF":
                case "TModShadowProjectionPixelShaderFDirectionalLightPolicyF16SampleFetch4PCF":
                case "TModShadowProjectionPixelShaderFDirectionalLightPolicyF16SampleHwPCF":
                case "TModShadowProjectionPixelShaderFDirectionalLightPolicyF4SampleManualPCF":
                case "TModShadowProjectionPixelShaderFDirectionalLightPolicyF4SampleHwPCF":
                    shader = new TModShadowProjectionPixelShader<FNullPolicy>();
                    break;
                case "FXAAFilterComputeShaderVerticalDebug":
                case "FXAAFilterComputeShaderVertical":
                case "FXAAFilterComputeShaderHorizontalDebug":
                case "FXAAFilterComputeShaderHorizontal":
                    shader = new FXAAFilterComputeShader();
                    break;
                case "TShadowProjectionPixelShader<F16SampleManualPCF>":
                case "TShadowProjectionPixelShader<F16SampleFetch4PCF>":
                case "TShadowProjectionPixelShader<F16SampleHwPCF>":
                case "TShadowProjectionPixelShader<F4SampleManualPCF>":
                case "TShadowProjectionPixelShader<F4SampleHwPCF>":
                    shader = new TShadowProjectionPixelShader();
                    break;
                case "FBlurLightShaftsPixelShader":
                    shader = new FBlurLightShaftsPixelShader();
                    break;
                case "FFilterVSMComputeShader":
                    shader = new FFilterVSMComputeShader();
                    break;
                case "FDistortionApplyScreenPixelShader":
                    shader = new FDistortionApplyScreenPixelShader();
                    break;
                case "FSRGBMLAABlendPixelShader":
                    shader = new FSRGBMLAABlendPixelShader();
                    break;
                case "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy":
                    shader = new TBasePassVertexShader<FNullPolicy, FNullPolicy>();
                    break;
                case "FShadowProjectionMaskPixelShader":
                    shader = new FShadowProjectionMaskPixelShader();
                    break;
                case "TModShadowProjectionPixelShaderFPointLightPolicyF16SampleManualPCF":
                case "TModShadowProjectionPixelShaderFPointLightPolicyF16SampleFetch4PCF":
                case "TModShadowProjectionPixelShaderFPointLightPolicyF16SampleHwPCF":
                case "TModShadowProjectionPixelShaderFPointLightPolicyF4SampleManualPCF":
                case "TModShadowProjectionPixelShaderFPointLightPolicyF4SampleHwPCF":
                    shader = new TModShadowProjectionPixelShader<FPointLightPolicy.ModShadowPixelParamsType>();
                    break;
                case "FShaderComplexityApplyPixelShader":
                    shader = new FShaderComplexityApplyPixelShader();
                    break;
                case "FCopyTranslucencyDepthPixelShader":
                    shader = new FCopyTranslucencyDepthPixelShader();
                    break;
                case "TDownsampleDepthPixelShaderTRUE":
                case "TDownsampleDepthPixelShaderFALSE":
                    shader = new TDownsampleDepthPixelShader();
                    break;
                case "FApplyLightShaftsPixelShader":
                    shader = new FApplyLightShaftsPixelShader();
                    break;
                case "FFXAAResolveComputeShader":
                    shader = new FFXAAResolveComputeShader();
                    break;
                case "FDownsampleSceneDepthAndNormalsPixelShader":
                    shader = new FDownsampleSceneDepthAndNormalsPixelShader();
                    break;
                case "FFXAAPrepComputeShader":
                    shader = new FFXAAPrepComputeShader();
                    break;
                case "Fetch4PCFMediumQualityShaderName":
                case "HwPCFMediumQualityShaderName":
                case "Fetch4PCFHighQualityShaderName":
                case "HwPCFHighQualityShaderName":
                case "Fetch4PCFLowQualityShaderName":
                case "HwPCFLowQualityShaderName":
                case "HighQualityShaderName":
                case "MediumQualityShaderName":
                case "LowQualityShaderName":
                    shader = new FBranchingPCFProjectionPixelShader();
                    break;
                case "FSRGBMLAAEdgeDetectionPixelShader":
                    shader = new FSRGBMLAAEdgeDetectionPixelShader();
                    break;
                case "THeightFogVertexShader<4>":
                case "THeightFogVertexShader<1>":
                    shader = new THeightFogVertexShader();
                    break;
                case "FApplyLightShaftsVertexShader":
                    shader = new FApplyLightShaftsVertexShader();
                    break;
                case "TBasePassPixelShaderFSHLightLightMapPolicySkyLight":
                case "TBasePassPixelShaderFSHLightLightMapPolicyNoSkyLight":
                    shader = new TBasePassPixelShader<FSHLightLightMapPolicy.PixelParametersType>();
                    break;
                case "FLUTBlenderPixelShader<1>":
                    shader = new FLUTBlenderPixelShader_1();
                    break;
                case "FLUTBlenderPixelShader<2>":
                    shader = new FLUTBlenderPixelShader_2();
                    break;
                case "FLUTBlenderPixelShader<3>":
                    shader = new FLUTBlenderPixelShader_3();
                    break;
                case "FLUTBlenderPixelShader<4>":
                    shader = new FLUTBlenderPixelShader_4();
                    break;
                case "FLUTBlenderPixelShader<5>":
                    shader = new FLUTBlenderPixelShader_5();
                    break;
                case "FFluidNormalPixelShader":
                    shader = new FFluidNormalPixelShader();
                    break;
                case "TDownsampleLightShaftsPixelShader<LS_Spot>":
                case "TDownsampleLightShaftsPixelShader<LS_Directional>":
                case "TDownsampleLightShaftsPixelShader<LS_Point>":
                    shader = new TDownsampleLightShaftsPixelShader();
                    break;
                case "FModShadowMeshPixelShader":
                    shader = new FModShadowMeshPixelShader();
                    break;
                case "FFluidSimulatePixelShader":
                    shader = new FFluidSimulatePixelShader();
                    break;
                case "FApplyForcePixelShader":
                    shader = new FApplyForcePixelShader();
                    break;
                case "FDOFAndBloomBlendPixelShader":
                    shader = new FDOFAndBloomBlendPixelShader();
                    break;
                case "TDOFBoxBlurMinPixelShader<3>":
                case "TDOFBoxBlurMaxPixelShader<5>":
                case "TDOFBlur1PixelShader<4>":
                case "TDOFBoxBlurMinPixelShader<2>":
                case "TDOFBoxBlurMaxPixelShader<4>":
                case "TDOFBlur1PixelShader<3>":
                case "TDOFBoxBlurMinPixelShader<5>":
                case "TDOFBoxBlurMaxPixelShader<3>":
                case "TDOFBoxBlurMinPixelShader<4>":
                case "TDOFBoxBlurMaxPixelShader<2>":
                case "TDOFBlur2PixelShader<8>":
                case "TDOFBlur2PixelShader<6>":
                case "TDOFBlur2PixelShader<4>":
                case "TDOFBlur2PixelShader<3>":
                case "TDOFBlur1PixelShader<8>":
                case "TDOFBlur1PixelShader<6>":
                    shader = new TDOFBlurPixelShader();
                    break;
                case "TDOFAndBloomGatherPixelShader<MAX_FILTER_SAMPLES>":
                case "TBloomGatherPixelShader<NumFPFilterSamples>":
                case "TDOFAndBloomGatherPixelShader<NumFPFilterSamples>":
                    shader = new TDOFAndBloomGatherPixelShader();
                    break;
                case "TLightMapDensityPixelShader<FDirectionalLightMapTexturePolicy>":
                case "TLightMapDensityPixelShader<FDummyLightMapTexturePolicy>":
                case "TLightMapDensityPixelShader<FSimpleLightMapTexturePolicy>":
                    shader = new TLightMapDensityPixelShader<FLightMapTexturePolicy.PixelParametersType>();
                    break;
                case "TDOFGatherPixelShader<NumFPFilterSamples>":
                    shader = new TDOFGatherPixelShader();
                    break;
                case "TModShadowVolumePixelShaderFDirectionalLightPolicy":
                    shader = new TModShadowVolumePixelShader<FNullPolicy>();
                    break;
                case "FHBAOBlurComputeShader":
                    shader = new FHBAOBlurComputeShader();
                    break;
                case "FHBAODeinterleaveComputeShader":
                    shader = new FHBAODeinterleaveComputeShader();
                    break;
                case "FFXAA3VertexShader":
                    shader = new FFXAA3VertexShader();
                    break;
                case "FSimpleElementDistanceFieldGammaPixelShader":
                    shader = new FSimpleElementDistanceFieldGammaPixelShader();
                    break;
                case "TShadowDepthVertexShader<ShadowDepth_OutputDepthToColor>":
                case "TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>":
                case "TShadowDepthVertexShader<ShadowDepth_OutputDepth>":
                    shader = new TShadowDepthVertexShader();
                    break;
                case "FSimpleElementMaskedGammaPixelShader":
                    shader = new FSimpleElementMaskedGammaPixelShader();
                    break;
                case "FSimpleElementGammaPixelShader":
                    shader = new FSimpleElementGammaPixelShader();
                    break;
                case "FGenerateDeinterleavedHBAOComputeShader":
                    shader = new FGenerateDeinterleavedHBAOComputeShader();
                    break;
                case "FHBAOReconstructNormalsComputeShader":
                    shader = new FHBAOReconstructNormalsComputeShader();
                    break;
                case "TAOMaskPixelShader<AO_HistoryUpdateManualDepthTest>":
                case "TAOMaskPixelShader<AO_HistoryMaskManualDepthTest>":
                case "TAOMaskPixelShader<AO_HistoryUpdate>":
                case "TAOMaskPixelShader<AO_HistoryMask>":
                case "TAOMaskPixelShader<AO_OcclusionMask>":
                    shader = new TAOMaskPixelShader();
                    break;
                case "FStaticHistoryUpdatePixelShader":
                    shader = new FStaticHistoryUpdatePixelShader();
                    break;
                case "TEdgePreservingFilterPixelShader<30>":
                case "TEdgePreservingFilterPixelShader<20>":
                case "TEdgePreservingFilterPixelShader<28>":
                case "TEdgePreservingFilterPixelShader<26>":
                case "TEdgePreservingFilterPixelShader<24>":
                case "TEdgePreservingFilterPixelShader<22>":
                case "TEdgePreservingFilterPixelShader<1>":
                case "TEdgePreservingFilterPixelShader<18>":
                case "TEdgePreservingFilterPixelShader<16>":
                case "TEdgePreservingFilterPixelShader<14>":
                case "TEdgePreservingFilterPixelShader<12>":
                case "TEdgePreservingFilterPixelShader<10>":
                case "TEdgePreservingFilterPixelShader<8>":
                case "TEdgePreservingFilterPixelShader<6>":
                case "TEdgePreservingFilterPixelShader<4>":
                case "TEdgePreservingFilterPixelShader<2>":
                    shader = new FStaticHistoryUpdatePixelShader();
                    break;
                case "TAmbientOcclusionPixelShaderFDefaultQualityAOTRUEFALSE":
                case "TAmbientOcclusionPixelShaderFDefaultQualityAOFALSETRUE":
                case "TAmbientOcclusionPixelShaderFDefaultQualityAOFALSEFALSE":
                case "TAmbientOcclusionPixelShaderFDefaultQualityAOTRUETRUE":
                    shader = new TAmbientOcclusionPixelShader();
                    break;
                case "FBinkGpuShaderYCrCbToRGBNoAlpha":
                case "FBinkGpuShaderYCrCbToRGB":
                    shader = new FBinkGpuShaderYCrCbToRGB();
                    break;
                case "FBinkGpuShaderHDRNoAlpha":
                case "FBinkGpuShaderHDR":
                    shader = new FBinkGpuShaderHDR();
                    break;
                case "FBinkYCrCbAToRGBAPixelShader":
                    shader = new FBinkYCrCbAToRGBAPixelShader();
                    break;
                case "FBinkYCrCbToRGBNoPixelAlphaPixelShader":
                    shader = new FBinkYCrCbToRGBNoPixelAlphaPixelShader();
                    break;
                case "TLightPixelShaderFSpotLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFSpotLightPolicyFShadowVertexBufferPolicy":
                    shader = new TLightPixelShader<FSpotLightPolicy.PixelParametersType, FNullPolicy>();
                    break;
                case "TLightMapDensityVertexShader<FDummyLightMapTexturePolicy>":
                case "TLightMapDensityVertexShader<FDirectionalLightMapTexturePolicy>":
                case "TLightMapDensityVertexShader<FSimpleLightMapTexturePolicy>":
                    shader = new TLightMapDensityVertexShader<FLightMapTexturePolicy.VertexParametersType>();
                    break;
                case "TLightVertexShaderFDirectionalLightPolicyFNoStaticShadowingPolicy":
                case "TLightVertexShaderFDirectionalLightPolicyFShadowVertexBufferPolicy":
                    shader = new TLightVertexShader<FDirectionalLightPolicy.VertexParametersType, FNullPolicy>();
                    break;
                case "TLightVertexShaderFSpotLightPolicyFNoStaticShadowingPolicy":
                case "TLightVertexShaderFSpotLightPolicyFShadowVertexBufferPolicy":
                    shader = new TLightVertexShader<FSpotLightPolicy.VertexParametersType, FNullPolicy>();
                    break;
                case "TLightVertexShaderFPointLightPolicyFNoStaticShadowingPolicy":
                case "TLightVertexShaderFPointLightPolicyFShadowVertexBufferPolicy":
                    shader = new TLightVertexShader<FPointLightPolicy.VertexParametersType, FNullPolicy>();
                    break;
                case "TLightPixelShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy":
                    shader = new TLightPixelShader<FSphericalHarmonicLightPolicy.PixelParametersType, FNullPolicy>();
                    break;
                case "TLightPixelShaderFPointLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFPointLightPolicyFShadowVertexBufferPolicy":
                    shader = new TLightPixelShader<FPointLightPolicy.PixelParametersType, FNullPolicy>();
                    break;
                case "TLightVertexShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy":
                    shader = new TLightVertexShader<FNullPolicy, FNullPolicy>();
                    break;
                case "FModShadowMeshVertexShader":
                    shader = new FModShadowMeshVertexShader();
                    break;
                case "TLightPixelShaderFDirectionalLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFDirectionalLightPolicyFShadowVertexBufferPolicy":
                    shader = new TLightPixelShader<FDirectionalLightPolicy.PixelParametersType, FNullPolicy>();
                    break;
                case "FSFXWorldNormalPixelShader":
                    shader = new FSFXWorldNormalPixelShader();
                    break;
                case "TDepthOnlyScreenDoorPixelShader":
                    shader = new TDepthOnlyScreenDoorPixelShader();
                    break;
                case "FTranslucencyPostRenderDepthPixelShader":
                    shader = new FTranslucencyPostRenderDepthPixelShader();
                    break;
                case "TDistortionMeshPixelShader<FDistortMeshAccumulatePolicy>":
                    shader = new TDistortionMeshPixelShader();
                    break;
                case "TDepthOnlySolidPixelShader":
                    shader = new TDepthOnlySolidPixelShader();
                    break;
                case "FSFXWorldNormalVertexShader":
                    shader = new FSFXWorldNormalVertexShader();
                    break;
                case "TLightMapDensityVertexShader<FNoLightMapPolicy>":
                    shader = new TLightMapDensityVertexShader<FNullPolicy>();
                    break;
                case "FTextureDensityVertexShader":
                    shader = new FTextureDensityVertexShader();
                    break;
                case "FHitProxyVertexShader":
                    shader = new FHitProxyVertexShader();
                    break;
                case "TDistortionMeshVertexShader<FDistortMeshAccumulatePolicy>":
                    shader = new TDistortionMeshVertexShader();
                    break;
                case "FFogVolumeApplyVertexShader":
                    shader = new FFogVolumeApplyVertexShader();
                    break;
                case "TDepthOnlyVertexShader<0>":
                case "TDepthOnlyVertexShader<1>":
                    shader = new TDepthOnlyVertexShader();
                    break;
                case "TLightPixelShaderFSFXPointLightPolicyFNoStaticShadowingPolicy":
                    shader = new TLightPixelShader<FSFXPointLightPolicy.PixelParametersType, FNullPolicy>();
                    break;
                case "TLightVertexShaderFSFXPointLightPolicyFNoStaticShadowingPolicy":
                    shader = new TLightVertexShader<FSFXPointLightPolicy.VertexParametersType, FNullPolicy>();
                    break;
                case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFNoDensityPolicy":
                    shader = new TBasePassVertexShader<FVertexLightMapPolicy.VertexParametersType, FNullPolicy>();
                    break;
                case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy":
                    shader = new TBasePassVertexShader<FLightMapTexturePolicy.VertexParametersType, FNullPolicy>();
                    break;
                case "TBasePassVertexShaderFSHLightLightMapPolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFNoDensityPolicy":
                    shader = new TBasePassVertexShader<FDirectionalLightPolicy.VertexParametersType, FNullPolicy>();
                    break;
                case "TBasePassPixelShaderFDirectionalLightLightMapPolicySkyLight":
                case "TBasePassPixelShaderFDirectionalLightLightMapPolicyNoSkyLight":
                    shader = new TBasePassPixelShader<FDirectionalLightLightMapPolicy.PixelParametersType>();
                    break;
                case "TBasePassPixelShaderFNoLightMapPolicySkyLight":
                case "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFCustomSimpleVertexLightMapPolicySkyLight":
                case "TBasePassPixelShaderFCustomSimpleVertexLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFCustomVectorVertexLightMapPolicySkyLight":
                case "TBasePassPixelShaderFCustomVectorVertexLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFSimpleVertexLightMapPolicySkyLight":
                case "TBasePassPixelShaderFSimpleVertexLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFDirectionalVertexLightMapPolicySkyLight":
                case "TBasePassPixelShaderFDirectionalVertexLightMapPolicyNoSkyLight":
                    shader = new TBasePassPixelShader<FNullPolicy>();
                    break;
                case "FVelocityPixelShader":
                    shader = new FVelocityPixelShader();
                    break;
                case "FVelocityVertexShader":
                    shader = new FVelocityVertexShader();
                    break;
                case "FTextureDensityPixelShader":
                    shader = new FTextureDensityPixelShader();
                    break;
                case "TShadowDepthPixelShaderTRUETRUE":
                case "TShadowDepthPixelShaderFALSEFALSE":
                case "TShadowDepthPixelShaderFALSETRUE":
                case "TShadowDepthPixelShaderTRUEFALSE":
                    shader = new TShadowDepthPixelShader();
                    break;
                case "FHitProxyPixelShader":
                    shader = new FHitProxyPixelShader();
                    break;
                case "TLightMapDensityPixelShader<FNoLightMapPolicy>":
                    shader = new TLightMapDensityPixelShader<FNullPolicy>();
                    break;
                case "TBasePassVertexShaderFNoLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFNoLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFNoLightMapPolicyFConstantDensityPolicy":
                    shader = new TBasePassVertexShader<FNullPolicy, FConstantDensityPolicy.VertexShaderParametersType>();
                    break;
                case "TLightPixelShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    shader = new TLightPixelShader<FSpotLightPolicy.PixelParametersType, FSignedDistanceFieldShadowTexturePolicy.PixelParametersType>();
                    break;
                case "TLightVertexShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightVertexShaderFPointLightPolicyFShadowTexturePolicy":
                    shader = new TLightVertexShader<FPointLightPolicy.VertexParametersType, FShadowTexturePolicy.VertexParametersType>();
                    break;
                case "TLightVertexShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightVertexShaderFDirectionalLightPolicyFShadowTexturePolicy":
                    shader = new TLightVertexShader<FDirectionalLightPolicy.VertexParametersType, FShadowTexturePolicy.VertexParametersType>();
                    break;
                case "TLightVertexShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightVertexShaderFSpotLightPolicyFShadowTexturePolicy":
                    shader = new TLightVertexShader<FSpotLightPolicy.VertexParametersType, FShadowTexturePolicy.VertexParametersType>();
                    break;
                case "TLightPixelShaderFSpotLightPolicyFShadowTexturePolicy":
                    shader = new TLightPixelShader<FSpotLightPolicy.PixelParametersType, FShadowTexturePolicy.PixelParametersType>();
                    break;
                case "TLightPixelShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    shader = new TLightPixelShader<FPointLightPolicy.PixelParametersType, FSignedDistanceFieldShadowTexturePolicy.PixelParametersType>();
                    break;
                case "TLightPixelShaderFPointLightPolicyFShadowTexturePolicy":
                    shader = new TLightPixelShader<FPointLightPolicy.PixelParametersType, FShadowTexturePolicy.PixelParametersType>();
                    break;
                case "TLightPixelShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    shader = new TLightPixelShader<FDirectionalLightPolicy.PixelParametersType, FSignedDistanceFieldShadowTexturePolicy.PixelParametersType>();
                    break;
                case "TLightPixelShaderFDirectionalLightPolicyFShadowTexturePolicy":
                    shader = new TLightPixelShader<FDirectionalLightPolicy.PixelParametersType, FShadowTexturePolicy.PixelParametersType>();
                    break;
                case "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicySkyLight":
                case "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicyNoSkyLight":
                case "TBasePassPixelShaderFCustomVectorLightMapTexturePolicySkyLight":
                case "TBasePassPixelShaderFCustomVectorLightMapTexturePolicyNoSkyLight":
                    shader = new TBasePassPixelShader<FCustomLightMapTexturePolicy.PixelParametersType>();
                    break;
                case "TBasePassPixelShaderFSimpleLightMapTexturePolicySkyLight":
                case "TBasePassPixelShaderFSimpleLightMapTexturePolicyNoSkyLight":
                case "TBasePassPixelShaderFDirectionalLightMapTexturePolicySkyLight":
                case "TBasePassPixelShaderFDirectionalLightMapTexturePolicyNoSkyLight":
                    shader = new TBasePassPixelShader<FLightMapTexturePolicy.PixelParametersType>();
                    break;
                case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFConstantDensityPolicy":
                    shader = new TBasePassVertexShader<FVertexLightMapPolicy.VertexParametersType, FConstantDensityPolicy.VertexShaderParametersType>();
                    break;
                case "TBasePassVertexShaderFSHLightLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFSHLightLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFSHLightLightMapPolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFConstantDensityPolicy":
                    shader = new TBasePassVertexShader<FDirectionalLightLightMapPolicy.VertexParametersType, FConstantDensityPolicy.VertexShaderParametersType>();
                    break;
                case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFConstantDensityPolicy":
                    shader = new TBasePassVertexShader<FLightMapTexturePolicy.VertexParametersType, FConstantDensityPolicy.VertexShaderParametersType>();
                    break;
                case "FFogVolumeApplyPixelShader":
                    shader = new FFogVolumeApplyPixelShader();
                    break;
                case "TFogIntegralPixelShader<FSphereDensityPolicy>":
                case "TFogIntegralPixelShader<FLinearHalfspaceDensityPolicy>":
                case "TFogIntegralPixelShader<FConstantDensityPolicy>":
                    shader = new TFogIntegralPixelShader();
                    break;
                case "TFogIntegralVertexShader<FSphereDensityPolicy>":
                case "TFogIntegralVertexShader<FLinearHalfspaceDensityPolicy>":
                case "TFogIntegralVertexShader<FConstantDensityPolicy>":
                    shader = new TFogIntegralVertexShader();
                    break;
                case "FRadialBlurVelocityPixelShader":
                case "FRadialBlurPixelShader":
                    shader = new FRadialBlurPixelShader();
                    break;
                case "FHitMaskPixelShader":
                    shader = new FHitMaskPixelShader();
                    break;
                case "FHitMaskVertexShader":
                    shader = new FHitMaskVertexShader();
                    break;
                case "TAOMeshVertexShader<0>":
                case "TAOMeshVertexShader<1>":
                    shader = new TAOMeshVertexShader();
                    break;
                case "FLightFunctionPixelShader":
                    shader = new FLightFunctionPixelShader();
                    break;
                default:
                    throw new InvalidDataException($"Unexpected shader type: '{shaderType.Name}'");
            }
            shader.Serialize(this);
            //for debugging
            //var offsetWriter = shader.Serialize(this);
            //var pos = ms.Position;
            //ms.JumpTo(offsetWriter.WritePos);
            //var endOffset = ms.ReadInt32();
            //ms.JumpTo(pos);
            //if (endOffset != FileOffset)
            //{
            //    Debugger.Break();
            //}
        }

        public void Serialize(ref Shader.ShaderFrequency sf)
        {
            if (IsLoading)
            {
                sf = (Shader.ShaderFrequency)ms.ReadByte();
            }
            else
            {
                ms.Writer.WriteByte((byte)sf);
            }
        }

        //Ignores Endianness! Only use for Shader serialization
        public void SerializeUnmanaged<T>(ref T val) where T : unmanaged
        {
            if (IsLoading)
            {
                ms.Read(val.AsBytes());
            }
            else
            {
                ms.Writer.Write(val.AsBytes());
            }
        }
    }
}