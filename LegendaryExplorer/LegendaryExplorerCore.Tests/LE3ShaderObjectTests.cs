using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3ShaderObjectTests
{
    [TestMethod]
    [DataRow("TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFConeDensityPolicy")]
    [DataRow("TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFConstantDensityPolicy")]
    [DataRow("TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFConeDensityPolicy")]
    [DataRow("TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFConstantDensityPolicy")]
    [DataRow("TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFLinearHalfspaceDensityPolicy")]
    [DataRow("TBasePassVertexShaderFDirectionalVertexLightMapPolicyFConeDensityPolicy")]
    [DataRow("TBasePassVertexShaderFDirectionalVertexLightMapPolicyFConstantDensityPolicy")]
    [DataRow("TBasePassVertexShaderFSimpleVertexLightMapPolicyFConeDensityPolicy")]
    public void FoggedVertexLightmapScaleRemainsRequiredWhenMissingFromReflection(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FoggedRequiredPolicy.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        Assert.IsTrue(TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
        var sources = new LE3ShaderSourceSnapshot(new Dictionary<string, string>
        {
            ["MaterialTemplate.usf"] = CommentTemplate(), ["BasePassVertexShader.usf"] = Source(true),
            ["LocalVertexFactory.usf"] = "// retained VF CRC input"
        });
        var translation = compiler.TranslateMaterialHlsl(sources);
        var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
        var compiled = compiler.CompileShader(job, sources); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
        var result = compiler.ConstructKnownShader(translation, compiled);
        Assert.IsFalse(result.Success); Assert.IsNull(result.Shader);
        StringAssert.Contains(string.Join("; ", result.Errors), "LightMapScale");
    }


    [TestMethod]
    [DataRow("FCustomSimpleLightMapTexturePolicyFConeDensityPolicy", "Texture")]
    [DataRow("FCustomSimpleLightMapTexturePolicyFConstantDensityPolicy", "Texture")]
    [DataRow("FCustomSimpleVertexLightMapPolicyFConeDensityPolicy", "Vertex")]
    [DataRow("FCustomSimpleVertexLightMapPolicyFConstantDensityPolicy", "Vertex")]
    [DataRow("FCustomVectorLightMapTexturePolicyFConeDensityPolicy", "Texture")]
    [DataRow("FCustomVectorLightMapTexturePolicyFConstantDensityPolicy", "Texture")]
    [DataRow("FCustomVectorVertexLightMapPolicyFConeDensityPolicy", "Vertex")]
    [DataRow("FCustomVectorVertexLightMapPolicyFConstantDensityPolicy", "Vertex")]
    [DataRow("FCustomVectorVertexLightMapPolicyFLinearHalfspaceDensityPolicy", "Vertex")]
    [DataRow("FDirectionalLightLightMapPolicyFConeDensityPolicy", "Directional")]
    [DataRow("FDirectionalLightLightMapPolicyFConstantDensityPolicy", "Directional")]
    [DataRow("FDirectionalLightMapTexturePolicyFConeDensityPolicy", "Texture")]
    [DataRow("FDirectionalLightMapTexturePolicyFConstantDensityPolicy", "Texture")]
    [DataRow("FDirectionalVertexLightMapPolicyFConeDensityPolicy", "Vertex")]
    [DataRow("FDirectionalVertexLightMapPolicyFConstantDensityPolicy", "Vertex")]
    [DataRow("FNoLightMapPolicyFConeDensityPolicy", "None")]
    [DataRow("FSHLightLightMapPolicyFConeDensityPolicy", "Directional")]
    [DataRow("FSHLightLightMapPolicyFConstantDensityPolicy", "Directional")]
    [DataRow("FSimpleLightMapTexturePolicyFConeDensityPolicy", "Texture")]
    [DataRow("FSimpleVertexLightMapPolicyFConeDensityPolicy", "Vertex")]
    public void FoggedBasePassConstructorsPreservePolicyFogAndDensityArchives(string suffix, string kind)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FoggedBasePass.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        foreach (bool gpu in new[] { false, true })
        foreach (bool present in new[] { false, true })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment("TBasePassVertexShader" + suffix,
                gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", out var environment));
            string policy = kind switch { "Texture" => "LightmapCoordinateScaleBias", "Vertex" => "LightMapScale",
                "Directional" => "LightDirectionAndbDirectional", _ => null };
            string source = Source(true, gpu);
            bool policyPresent = policy != null && (present || kind == "Vertex");
            if (policyPresent)
                source = "float4 " + policy + "; " + source.Replace(" + FogMinHeight", " + FogMinHeight + " + policy, StringComparison.Ordinal);
            if (present)
                source = "float4 FirstDensityFunctionParameters, SecondDensityFunctionParameters, FogVolumeBoxMin, FogVolumeBoxMax, ApproxFogColor; float StartDistance; " +
                    source.Replace(" + FogMinHeight", " + FogMinHeight + FirstDensityFunctionParameters + SecondDensityFunctionParameters + StartDistance + FogVolumeBoxMin + FogVolumeBoxMax + ApproxFogColor", StringComparison.Ordinal);
            else source = source.Replace(" + FogMinHeight", "", StringComparison.Ordinal);
            var sources = new LE3ShaderSourceSnapshot(new Dictionary<string, string>
            {
                ["MaterialTemplate.usf"] = CommentTemplate(), ["BasePassVertexShader.usf"] = source,
                [environment.Target.VertexFactoryShaderFileName + ".usf"] = "// retained VF CRC input"
            });
            var translation = compiler.TranslateMaterialHlsl(sources);
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
            var compiled = compiler.CompileShader(job, sources); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
            var result = compiler.ConstructKnownShader(translation, compiled);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(0, result.UnboundParameters.Count);
            Shader shader = result.Shader; using var stream = new MemoryStream();
            new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0; Shader copy = null;
            new SerializingContainer(stream, package, true).Serialize(ref copy);
            Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(shader.GetType(), copy.GetType());
            Assert.AreEqual(shader.ParameterMapCRC, copy.ParameterMapCRC); CollectionAssert.AreEqual(shader.ShaderByteCode, copy.ShaderByteCode);
            var type = copy.GetType();
            var density = (FConstantDensityPolicy.VertexShaderParametersType)type.GetField("DensityVertexShaderParams").GetValue(copy);
            Assert.AreEqual(present ? (ushort)16 : (ushort)0, density.FirstDensityFunction.NumBytes);
            Assert.AreEqual(present ? (ushort)16 : (ushort)0, density.SecondDensityFunction.NumBytes);
            Assert.AreEqual(present ? (ushort)4 : (ushort)0, density.StartDistance.NumBytes);
            Assert.AreEqual(present ? (ushort)16 : (ushort)0, density.FogVolumeBoxMin.NumBytes);
            Assert.AreEqual(present ? (ushort)16 : (ushort)0, density.FogVolumeBoxMax.NumBytes);
            Assert.AreEqual(present ? (ushort)16 : (ushort)0, density.ApproxFogColor.NumBytes);
            if (policy != null)
            {
                object parameters = type.GetField("LightMapVertexParams").GetValue(copy);
                Assert.AreEqual(policyPresent ? (ushort)16 : (ushort)0,
                    ((FShaderParameter)parameters.GetType().GetField(policy).GetValue(parameters)).NumBytes);
            }
            // Explicit construction of Cone validates layout, not its disabled native cache gate.
        }
    }


    [TestMethod]
    [DataRow("FConeDensityPolicy", true)]
    [DataRow("FConeDensityPolicy", false)]
    [DataRow("FConstantDensityPolicy", true)]
    [DataRow("FConstantDensityPolicy", false)]
    [DataRow("FLinearHalfspaceDensityPolicy", true)]
    [DataRow("FLinearHalfspaceDensityPolicy", false)]
    [DataRow("FSphereDensityPolicy", true)]
    [DataRow("FSphereDensityPolicy", false)]
    public void FogIntegralConstructorsPreserveOptionalDensityAndHeightFogArchives(string policy, bool vertex)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FogIntegral.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package); var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        foreach (bool gpu in new[] { false, true })
        foreach (bool optimizedOut in new[] { false, true })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment($"TFogIntegral{(vertex ? "Vertex" : "Pixel")}Shader<{policy}>",
                gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", out var environment));
            string source;
            if (vertex)
            {
                source = Source(true, gpu);
                source = optimizedOut ? source.Replace(" + FogMinHeight", "", StringComparison.Ordinal) :
                    "float4 FogMaxHeight, FogDistanceScale, FogExtinctionDistance, FogInScattering, FogStartDistance; " +
                    source.Replace(" + FogMinHeight", " + FogMinHeight + FogMaxHeight + FogDistanceScale + FogExtinctionDistance + FogInScattering + FogStartDistance", StringComparison.Ordinal);
            }
            else
            {
                source = "float4 UniformPixelScalars_0, UniformPixelScalars_1; " +
                    (optimizedOut ? "" : "float4 DepthFilterSampleOffsets[2], FirstDensityFunctionParameters, SecondDensityFunctionParameters; " +
                        "float4x4 ScreenToWorld; float3 FogCameraPosition; float FaceScale, StartDistance, InvMaxIntegral; ") +
                    $"float4 {environment.Target.EntryPoint}(float4 p:SV_Position):SV_Target {{ return UniformPixelScalars_0 + UniformPixelScalars_1" +
                    (optimizedOut ? "" : " + DepthFilterSampleOffsets[0] + DepthFilterSampleOffsets[1] + FirstDensityFunctionParameters + SecondDensityFunctionParameters" +
                        " + mul(ScreenToWorld,p) + float4(FogCameraPosition,0) + FaceScale + StartDistance + InvMaxIntegral") + "; }";
            }
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
            string directory = CreateSourceDirectory(job.Target, source);
            try
            {
                var compiled = compiler.CompileShader(job, directory); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
                var result = compiler.ConstructKnownShader(translation, compiled);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(0, result.UnboundParameters.Count);
                Shader shader = result.Shader; using var stream = new MemoryStream();
                new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0; Shader copy = null;
                new SerializingContainer(stream, package, true).Serialize(ref copy);
                Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(shader.GetType(), copy.GetType());
                Assert.AreEqual(shader.ParameterMapCRC, copy.ParameterMapCRC);
                if (vertex)
                {
                    var vs = (TFogIntegralVertexShader)copy;
                    Assert.AreEqual(environment.Target.VertexFactoryTypeName, vs.VertexFactoryParameters.VertexFactoryType.Name);
                    Assert.AreEqual(2, vs.MaterialParameters.UniformVertexScalarShaderParameters.Length);
                    Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)4, vs.HeightFogParameters.FogMinHeight.NumBytes);
                    Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)16, vs.HeightFogParameters.FogDistanceScale.NumBytes);
                }
                else
                {
                    var ps = (TFogIntegralPixelShader)copy;
                    Assert.AreEqual(2, ps.MaterialParameters.UniformPixelScalarShaderParameters.Length);
                    Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)32, ps.DepthFilterSampleOffsets.NumBytes);
                    Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)64, ps.ScreenToWorld.NumBytes);
                    Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)12, ps.FogCameraPosition.NumBytes);
                    Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)4, ps.InvMaxIntegral.NumBytes);
                }
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void FogApplyConstructorsPreserveOptionalParametersAndArchiveOrder(bool vertex)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FogApply.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package); var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        foreach (bool gpu in new[] { false, true })
        foreach (bool optimizedOut in new[] { false, true })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(vertex ? "FFogVolumeApplyVertexShader" : "FFogVolumeApplyPixelShader",
                gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", out var environment));
            string source = vertex ? Source(true, gpu) :
                "float4 UniformPixelScalars_0, UniformPixelScalars_1; " +
                (optimizedOut ? "" : "float MaxIntegral; Texture2D AccumulatedFrontfacesLineIntegralTexture, AccumulatedBackfacesLineIntegralTexture; " +
                    "SamplerState AccumulatedFrontfacesLineIntegralTextureSampler, AccumulatedBackfacesLineIntegralTextureSampler; ") +
                "float4 Main(float4 p:SV_Position):SV_Target { return UniformPixelScalars_0 + UniformPixelScalars_1" +
                (optimizedOut ? "" : " + MaxIntegral + AccumulatedFrontfacesLineIntegralTexture.Sample(AccumulatedFrontfacesLineIntegralTextureSampler,p.xy)" +
                    " + AccumulatedBackfacesLineIntegralTexture.Sample(AccumulatedBackfacesLineIntegralTextureSampler,p.xy)") + "; }";
            // The shared material-only VS does not own height-fog parameters.
            if (vertex) source = source.Replace(" + FogMinHeight", "", StringComparison.Ordinal);
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
            string directory = CreateSourceDirectory(job.Target, source);
            try
            {
                var compiled = compiler.CompileShader(job, directory); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
                var result = compiler.ConstructKnownShader(translation, compiled);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(0, result.UnboundParameters.Count);
                Shader shader = result.Shader; using var stream = new MemoryStream();
                new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0; Shader copy = null;
                new SerializingContainer(stream, package, true).Serialize(ref copy);
                Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(shader.GetType(), copy.GetType());
                Assert.AreEqual(shader.ParameterMapCRC, copy.ParameterMapCRC);
                if (vertex)
                {
                    var vs = (FFogVolumeApplyVertexShader)copy;
                    Assert.AreEqual(environment.Target.VertexFactoryTypeName, vs.VertexFactoryParameters.VertexFactoryType.Name);
                    Assert.AreEqual(2, vs.MaterialParameters.UniformVertexScalarShaderParameters.Length);
                }
                else
                {
                    var ps = (FFogVolumeApplyPixelShader)copy;
                    Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)4, ps.MaxIntegral.NumBytes);
                    Assert.AreEqual(2, ps.MaterialParameters.UniformPixelScalarShaderParameters.Length);
                    Assert.AreEqual(!optimizedOut, ps.AccumulatedFrontfacesLineIntegralTexture.IsBound());
                    Assert.AreEqual(!optimizedOut, ps.AccumulatedBackfacesLineIntegralTexture.IsBound());
                }
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void AoMeshConstructorsBindVfAndOptionalMatricesWithoutMaterialParameters(int permutation)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3AoMesh.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package); var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        foreach (bool gpu in new[] { false, true })
        foreach (bool optimizedOut in new[] { false, true })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment($"TAOMeshVertexShader<{permutation}>", gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", out var environment));
            string source = "float4x4 LocalToWorld; " + (gpu ? "float4 BoneMatrices[3]; " : "") +
                (optimizedOut ? "" : "float4x4 PrevViewProjectionMatrix, PreviousLocalToWorld; ") +
                $"float4 {environment.Target.EntryPoint}(float4 p:POSITION):SV_Position {{ return mul(LocalToWorld,p)" +
                (gpu ? " + BoneMatrices[0]" : "") + (optimizedOut ? "" : " + mul(PrevViewProjectionMatrix,p) + mul(PreviousLocalToWorld,p)") + "; }";
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
            string directory = CreateSourceDirectory(job.Target, source);
            try
            {
                var compiled = compiler.CompileShader(job, directory); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
                var result = compiler.ConstructKnownShader(translation, compiled);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(0, result.UnboundParameters.Count);
                Shader shader = result.Shader; using var stream = new MemoryStream();
                new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0; Shader copy = null;
                new SerializingContainer(stream, package, true).Serialize(ref copy);
                Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(shader.ParameterMapCRC, copy.ParameterMapCRC);
                var vs = (TAOMeshVertexShader)copy;
                Assert.AreEqual(environment.Target.VertexFactoryTypeName, vs.VertexFactoryParameters.VertexFactoryType.Name);
                Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)64, vs.PrevViewProjectionMatrix.NumBytes);
                Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)64, vs.PreviousLocalToWorld.NumBytes);
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void HitMaskConstructorsPreserveOptionalNativeBindingsAndTypedArchives(bool vertex)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3HitMask.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package); var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        foreach (bool gpu in new[] { false, true })
        foreach (bool optimizedOut in new[] { false, true })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(vertex ? "FHitMaskVertexShader" : "FHitMaskPixelShader",
                gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", out var environment));
            string stage = vertex ? "Vertex" : "Pixel";
            string source = $"float4 Uniform{stage}Scalars_0, Uniform{stage}Scalars_1; " +
                (vertex ? "float4x4 LocalToWorld; " + (gpu ? "float4 BoneMatrices[3]; " : "") : "") +
                (optimizedOut ? "" : vertex ? "float2 PixelCenterOffset; " :
                    "float3 HitLocation, HitStartLocation; float HitRadius, HitCullDistance; Texture2D CurrentMaskTexture; SamplerState CurrentMaskTextureSampler; ") +
                $"float4 Main(float4 p:{(vertex ? "POSITION" : "SV_Position")}):{(vertex ? "SV_Position" : "SV_Target")} {{ return " +
                (vertex ? "mul(LocalToWorld,p)" + (gpu ? " + BoneMatrices[0]" : "") : "p") +
                $" + Uniform{stage}Scalars_0 + Uniform{stage}Scalars_1" +
                (optimizedOut ? "" : vertex ? " + float4(PixelCenterOffset,0,0)" :
                    " + float4(HitLocation+HitStartLocation,1) + HitRadius + HitCullDistance + CurrentMaskTexture.Sample(CurrentMaskTextureSampler,p.xy)") + "; }";
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
            string directory = CreateSourceDirectory(job.Target, source);
            try
            {
                var compiled = compiler.CompileShader(job, directory); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
                var result = compiler.ConstructKnownShader(translation, compiled);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(0, result.UnboundParameters.Count);
                Shader shader = result.Shader; using var stream = new MemoryStream();
                new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0; Shader copy = null;
                new SerializingContainer(stream, package, true).Serialize(ref copy);
                Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(shader.GetType(), copy.GetType());
                Assert.AreEqual(shader.ParameterMapCRC, copy.ParameterMapCRC);
                if (vertex)
                {
                    var vs = (FHitMaskVertexShader)copy;
                    Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)8, vs.PixelCenterOffset.NumBytes);
                    Assert.AreEqual(environment.Target.VertexFactoryTypeName, vs.VertexFactoryParameters.VertexFactoryType.Name);
                    Assert.AreEqual(2, vs.MaterialParameters.UniformVertexScalarShaderParameters.Length);
                }
                else
                {
                    var ps = (FHitMaskPixelShader)copy;
                    Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)12, ps.HitLocation.NumBytes);
                    Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)12, ps.HitStartLocation.NumBytes);
                    Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)4, ps.HitRadius.NumBytes);
                    Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)4, ps.HitCullDistance.NumBytes);
                    Assert.AreEqual(!optimizedOut, ps.CurrentMaskTexture.IsBound());
                    Assert.AreEqual(2, ps.MaterialParameters.UniformPixelScalarShaderParameters.Length);
                }
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    [TestMethod]
    [DataRow("FNoLightMapPolicy", true)]
    [DataRow("FNoLightMapPolicy", false)]
    [DataRow("FDirectionalLightMapTexturePolicy", true)]
    [DataRow("FDirectionalLightMapTexturePolicy", false)]
    [DataRow("FSimpleLightMapTexturePolicy", true)]
    [DataRow("FSimpleLightMapTexturePolicy", false)]
    [DataRow("FDummyLightMapTexturePolicy", true)]
    [DataRow("FDummyLightMapTexturePolicy", false)]
    public void LightmapDensityConstructorsRetainOptionalNativeFieldsAndResources(string policy, bool vertex)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3LightmapDensity.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package); var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        bool texture = policy != "FNoLightMapPolicy";
        string stage = vertex ? "Vertex" : "Pixel", name = $"TLightMapDensity{stage}Shader<{policy}>";
        string[] densityFields = ["LightMapDensityParameters", "BuiltLightingAndSelectedFlags", "DensitySelectedColor",
            "LightMapResolutionScale", "LightMapDensityDisplayOptions", "VertexMappedColor"];
        foreach (bool gpu in new[] { false, true })
        foreach (bool optimizedOut in new[] { false, true })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", out var environment));
            string source = $"float4 Uniform{stage}Scalars_0, Uniform{stage}Scalars_1; " +
                (vertex ? "float4x4 LocalToWorld; " + (gpu ? "float4 BoneMatrices[3]; " : "") : "") +
                (optimizedOut ? "" : vertex ? texture ? "float4 LightmapCoordinateScaleBias; " : "" :
                    "float4 " + string.Join(",", densityFields) + "; Texture2D GridTexture; SamplerState GridTextureSampler; " +
                    (texture ? "float4 LightMapScale; Texture2D LightMapTextures; SamplerState LightMapTexturesSampler; " : "")) +
                $"float4 {environment.Target.EntryPoint}(float4 p:{(vertex ? "POSITION" : "SV_Position")}):{(vertex ? "SV_Position" : "SV_Target")} {{ return " +
                (vertex ? "mul(LocalToWorld,p)" + (gpu ? " + BoneMatrices[0]" : "") : "p") +
                $" + Uniform{stage}Scalars_0 + Uniform{stage}Scalars_1" +
                (optimizedOut ? "" : vertex ? texture ? " + LightmapCoordinateScaleBias" : "" :
                    " + " + string.Join(" + ", densityFields) + " + GridTexture.Sample(GridTextureSampler,p.xy)" +
                    (texture ? " + LightMapTextures.Sample(LightMapTexturesSampler,p.xy)*LightMapScale" : "")) + "; }";
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
            string directory = CreateSourceDirectory(job.Target, source);
            try
            {
                var compiled = compiler.CompileShader(job, directory); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
                var result = compiler.ConstructKnownShader(translation, compiled);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(0, result.UnboundParameters.Count);
                Shader shader = result.Shader; using var stream = new MemoryStream();
                new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0; Shader copy = null;
                new SerializingContainer(stream, package, true).Serialize(ref copy);
                Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(shader.GetType(), copy.GetType());
                Assert.AreEqual(shader.ParameterMapCRC, copy.ParameterMapCRC); CollectionAssert.AreEqual(shader.ShaderByteCode, copy.ShaderByteCode);
                if (vertex && texture)
                {
                    var vs = (TLightMapDensityVertexShader<FLightMapTexturePolicy.VertexParametersType>)copy;
                    Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)16, vs.LightTypeVertexParams.LightmapCoordinateScaleBias.NumBytes);
                    Assert.AreEqual(environment.Target.VertexFactoryTypeName, vs.VertexFactoryParameters.VertexFactoryType.Name);
                    Assert.AreEqual(2, vs.MaterialParameters.UniformVertexScalarShaderParameters.Length);
                }
                else if (vertex)
                {
                    var vs = (TLightMapDensityVertexShader<FNullPolicy>)copy;
                    Assert.AreEqual(environment.Target.VertexFactoryTypeName, vs.VertexFactoryParameters.VertexFactoryType.Name);
                    Assert.AreEqual(2, vs.MaterialParameters.UniformVertexScalarShaderParameters.Length);
                }
                else
                {
                    if (texture)
                    {
                        var ps = (TLightMapDensityPixelShader<FLightMapTexturePolicy.PixelParametersType>)copy;
                        Assert.AreEqual(!optimizedOut, ps.PixelParams.LightMapScale.IsBound());
                        Assert.AreEqual(!optimizedOut, ps.PixelParams.LightMapTextures.IsBound());
                        Validate(ps);
                    }
                    else Validate((TLightMapDensityPixelShader<FNullPolicy>)copy);
                    void Validate<TPolicy>(TLightMapDensityPixelShader<TPolicy> ps) where TPolicy : struct, IPixelParametersType
                    {
                        Assert.AreEqual(2, ps.MaterialParameters.UniformPixelScalarShaderParameters.Length);
                        foreach (var field in new[] { ps.LightMapDensityParameters, ps.BuiltLightingAndSelectedFlags,
                            ps.DensitySelectedColor, ps.LightMapResolutionScale, ps.LightMapDensityDisplayOptions, ps.VertexMappedColor })
                            Assert.AreEqual(optimizedOut ? (ushort)0 : (ushort)16, field.NumBytes);
                        Assert.AreEqual(!optimizedOut, ps.GridTexture.IsBound());
                    }
                }
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    [TestMethod]
    [DataRow("DirectionalLightLightMap", "Vertex", false)]
    [DataRow("DirectionalLightLightMap", "Pixel", false)]
    [DataRow("DirectionalLightLightMap", "Pixel", true)]
    [DataRow("SHLightLightMap", "Vertex", false)]
    [DataRow("SHLightLightMap", "Pixel", false)]
    [DataRow("SHLightLightMap", "Pixel", true)]
    public void DynamicLightmapConstructorsBindOptionalPolicyFieldsAndBothFactories(string policy, string stage, bool sky)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DynamicLightmap.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        string name = $"TBasePass{stage}ShaderF{policy}Policy" + (stage == "Vertex" ? "FNoDensityPolicy" : sky ? "SkyLight" : "NoSkyLight");
        bool vertex = stage == "Vertex";
        foreach (bool gpu in new[] { false, true })
        foreach (bool optimizedOut in new[] { false, true })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", out var environment));
            string[] policyFields = vertex ? ["LightDirectionAndbDirectional"] : policy == "SHLightLightMap"
                ? ["LightColorAndFalloffExponent", "bReceiveDynamicShadows", "WorldIncidentLighting"]
                : ["LightColorAndFalloffExponent", "bReceiveDynamicShadows"];
            string source = $"float4 Uniform{stage}Scalars_0, Uniform{stage}Scalars_1; " +
                (vertex ? "float4x4 LocalToWorld; " + (gpu ? "float4 BoneMatrices[3]; " : "") : "") +
                (optimizedOut ? "" : "float4 " + string.Join(",", policyFields) + "; ") +
                $"float4 Main(float4 p:{(vertex ? "POSITION" : "SV_Position")}):{(vertex ? "SV_Position" : "SV_Target")} {{ return " +
                (vertex ? "mul(LocalToWorld,p)" + (gpu ? " + BoneMatrices[0]" : "") : "p") +
                $" + Uniform{stage}Scalars_0 + Uniform{stage}Scalars_1" +
                (optimizedOut ? "" : " + " + string.Join(" + ", policyFields)) + "; }";
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
            string directory = CreateSourceDirectory(job.Target, source);
            try
            {
                var compiled = compiler.CompileShader(job, directory);
                Assert.IsTrue(compiled.Success, compiled.Diagnostics);
                var result = compiler.ConstructKnownShader(translation, compiled);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Assert.AreEqual(0, result.UnboundParameters.Count);
                Shader shader = result.Shader;
                using var stream = new MemoryStream(); new SerializingContainer(stream, package).Serialize(ref shader);
                stream.Position = 0; Shader copy = null;
                new SerializingContainer(stream, package, true).Serialize(ref copy);
                Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(shader.GetType(), copy.GetType());
                Assert.AreEqual(shader.ParameterMapCRC, copy.ParameterMapCRC);
                CollectionAssert.AreEqual(shader.ShaderByteCode, copy.ShaderByteCode);
                ushort expectedSize = optimizedOut ? (ushort)0 : (ushort)16;
                if (vertex)
                {
                    var vs = (TBasePassVertexShader<FDirectionalLightPolicy.VertexParametersType, FNullPolicy>)copy;
                    Assert.AreEqual(expectedSize, vs.LightMapVertexParams.LightDirection.NumBytes);
                    Assert.AreEqual(((TBasePassVertexShader<FDirectionalLightPolicy.VertexParametersType, FNullPolicy>)shader).LightMapVertexParams.LightDirection,
                        vs.LightMapVertexParams.LightDirection);
                    Assert.AreEqual(environment.Target.VertexFactoryTypeName, vs.VertexFactoryParameters.VertexFactoryType.Name);
                    Assert.AreEqual(2, vs.MaterialParameters.UniformVertexScalarShaderParameters.Length);
                }
                else if (policy == "SHLightLightMap")
                {
                    var ps = (TBasePassPixelShader<FSHLightLightMapPolicy.PixelParametersType>)copy;
                    Assert.AreEqual(expectedSize, ps.PixelParams.LightColorAndFalloffExponent.NumBytes);
                    Assert.AreEqual(expectedSize, ps.PixelParams.bReceiveDynamicShadows.NumBytes);
                    Assert.AreEqual(expectedSize, ps.PixelParams.WorldIncidentLighting.NumBytes);
                    Assert.AreEqual(((TBasePassPixelShader<FSHLightLightMapPolicy.PixelParametersType>)shader).PixelParams.WorldIncidentLighting, ps.PixelParams.WorldIncidentLighting);
                    Assert.AreEqual(2, ps.MaterialParameters.UniformPixelScalarShaderParameters.Length);
                }
                else
                {
                    var ps = (TBasePassPixelShader<FDirectionalLightLightMapPolicy.PixelParametersType>)copy;
                    Assert.AreEqual(expectedSize, ps.PixelParams.LightColorAndFalloffExponent.NumBytes);
                    Assert.AreEqual(expectedSize, ps.PixelParams.bReceiveDynamicShadows.NumBytes);
                    Assert.AreEqual(2, ps.MaterialParameters.UniformPixelScalarShaderParameters.Length);
                }
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    [TestMethod]
    [DataRow(EBlendMode.BLEND_Opaque)]
    [DataRow(EBlendMode.BLEND_Masked)]
    [DataRow(EBlendMode.BLEND_Translucent)]
    [DataRow(EBlendMode.BLEND_Additive)]
    [DataRow(EBlendMode.BLEND_Modulate)]
    [DataRow(EBlendMode.BLEND_SoftMasked)]
    [DataRow(EBlendMode.BLEND_AlphaComposite)]
    public void ModShadowCacheRequiresLitOpaqueFamilyEvenForSpecialMaterials(EBlendMode blend)
    {
        foreach (bool unlit in new[] { false, true })
        foreach (bool special in new[] { false, true })
        foreach (bool masked in new[] { false, true })
        foreach (string name in new[] { "FModShadowMeshVertexShader", "FModShadowMeshPixelShader" })
        {
            var context = new ShouldCacheMaterialContext(unlit, masked, special, true, true, true, true, true,
                BlendMode: blend, IsTwoSided: true);
            Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(name, context, "FLocalVertexFactory", null,
                out bool caches, new(false)));
            Assert.AreEqual(!unlit && blend is EBlendMode.BLEND_Opaque or EBlendMode.BLEND_Masked or EBlendMode.BLEND_SoftMasked, caches);
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
            Assert.AreEqual("Main", environment.Target.EntryPoint);
            Assert.AreEqual("1", environment.CreateCandidate().EnvironmentMacros["SIMPLE_VERTEX_LIGHTMAP"]);
        }
    }

    [TestMethod]
    public void ScreenDoorCacheIncludesSpecialMaterialButNotWpoOrPostpassAlone()
    {
        for (int flags = 0; flags < 64; flags++)
        {
            var context = new ShouldCacheMaterialContext(false, (flags & 1) != 0, (flags & 2) != 0,
                (flags & 4) != 0, (flags & 8) != 0, false, (flags & 16) != 0, false,
                UsesLitTranslucencyDepthPostpass: (flags & 32) != 0);
            Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache("TDepthOnlyScreenDoorPixelShader", context, out bool caches));
            Assert.AreEqual((flags & 15) != 0, caches);
        }
        Assert.IsTrue(TryGetKnownPermutationEnvironment("TDepthOnlyScreenDoorPixelShader", "FLocalVertexFactory", out var environment));
        Assert.AreEqual("1", environment.CreateCandidate().EnvironmentMacros["MATERIAL_USE_SCREEN_DOOR_FADE"]);
    }
    [TestMethod]
    [DataRow(0u)]
    [DataRow(1u)]
    [DataRow(uint.MaxValue)]
    public void DensityAndHitProxyCacheGatesKeepCoordinateAndWpoRequirementsDistinct(uint coordinates)
    {
        foreach (bool special in new[] { false, true })
        foreach (bool masked in new[] { false, true })
        foreach (bool wpo in new[] { false, true })
        foreach (bool debug in new[] { false, true })
        foreach (string name in new[] { "FTextureDensityVertexShader", "FTextureDensityPixelShader", "FHitProxyVertexShader", "FHitProxyPixelShader" })
        {
            var context = new ShouldCacheMaterialContext(false, masked, special, false, false, false, wpo, false, NumUserTexCoords: coordinates);
            Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(name, context, "FLocalVertexFactory", null, out bool caches, new(debug)));
            Assert.AreEqual(name.StartsWith("FTextureDensity", StringComparison.Ordinal)
                ? coordinates != 0 && (special || masked) && debug : special || masked || wpo, caches);
        }
    }

    [TestMethod]
    public void DensityPlannerUsesTranslationCoordinateCountInsteadOfSerializedGuess()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DensityCoordinates.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(true, "bIsMasked"));
        var coordinate = package.CreateExport("Coordinate", "MaterialExpressionTextureCoordinate", material, indexed: false);
        material.WriteProperty(Input("Distortion", coordinate.UIndex));
        var compiler = new LE3MaterialShaderCompiler(material);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        Assert.AreEqual(1u, translation.NumUserTexCoords);
        var enabled = compiler.BuildKnownSubsetCompilePlan(translation, MaterialResource.Create());
        Assert.IsTrue(enabled.Success, string.Join("; ", enabled.Errors));
        Assert.AreEqual(2, enabled.Jobs.Count(j => j.Target.ShaderTypeName.StartsWith("FTextureDensity", StringComparison.Ordinal)));
        var disabled = compiler.BuildKnownSubsetCompilePlan(translation, MaterialResource.Create(), new(false));
        Assert.IsTrue(disabled.Success);
        Assert.AreEqual(0, disabled.Jobs.Count(j => j.Target.ShaderTypeName.StartsWith("FTextureDensity", StringComparison.Ordinal)));
        Assert.AreEqual(2, disabled.Jobs.Count(j => j.Target.ShaderTypeName.StartsWith("FHitProxy", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(EBlendMode.BLEND_Opaque)]
    [DataRow(EBlendMode.BLEND_Masked)]
    [DataRow(EBlendMode.BLEND_Translucent)]
    [DataRow(EBlendMode.BLEND_Additive)]
    [DataRow(EBlendMode.BLEND_Modulate)]
    [DataRow(EBlendMode.BLEND_SoftMasked)]
    [DataRow(EBlendMode.BLEND_AlphaComposite)]
    public void VelocityAndWorldNormalCacheGatesRespectSpecialBlendAndDebugBoundaries(EBlendMode blend)
    {
        bool translucent = blend is not (EBlendMode.BLEND_Opaque or EBlendMode.BLEND_Masked or EBlendMode.BLEND_SoftMasked);
        foreach (bool special in new[] { false, true })
        foreach (bool masked in new[] { false, true })
        foreach (bool twoSided in new[] { false, true })
        foreach (bool debug in new[] { false, true })
        {
            var context = new ShouldCacheMaterialContext(false, masked, special, false, false, false, false, false,
                BlendMode: blend, IsTwoSided: twoSided);
            foreach (string name in new[] { "FVelocityVertexShader", "FVelocityPixelShader", "FSFXWorldNormalVertexShader", "FSFXWorldNormalPixelShader" })
            {
                Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(name, context, "FLocalVertexFactory", null, out bool caches,
                    new ShouldCacheEnvironmentContext(debug)));
                bool expected = name.StartsWith("FVelocity", StringComparison.Ordinal)
                    ? special || masked || (twoSided && !translucent) : (special || !translucent) && debug;
                Assert.AreEqual(expected, caches, $"{name}/{blend}/special={special}/masked={masked}/two={twoSided}/debug={debug}");
            }
        }
    }

    [TestMethod]
    public void PlannerPropagatesDebugEnvironmentWithoutTreatingItAsMaterialProperty()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DebugCache.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        compiler.CompilationInput.BaseMaterial.WriteProperty(new BoolProperty(true, "TwoSided"));
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        Assert.IsTrue(compiler.BuildShouldCacheMaterialContext().IsTwoSided);
        var enabled = compiler.BuildKnownSubsetCompilePlan(translation, MaterialResource.Create());
        var disabled = compiler.BuildKnownSubsetCompilePlan(translation, MaterialResource.Create(), new(false));
        Assert.IsTrue(enabled.Success, string.Join("; ", enabled.Errors));
        Assert.IsTrue(disabled.Success, string.Join("; ", disabled.Errors));
        Assert.AreEqual(2, enabled.Jobs.Count(j => j.Target.ShaderTypeName.StartsWith("FSFXWorldNormal", StringComparison.Ordinal)));
        Assert.AreEqual(0, disabled.Jobs.Count(j => j.Target.ShaderTypeName.StartsWith("FSFXWorldNormal", StringComparison.Ordinal)));
        Assert.AreEqual(enabled.Jobs.Count - 2, disabled.Jobs.Count);
        Assert.AreEqual(2, disabled.Jobs.Count(j => j.Target.ShaderTypeName.StartsWith("FVelocity", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("FVelocityVertexShader")]
    [DataRow("FVelocityPixelShader")]
    [DataRow("FSFXWorldNormalVertexShader")]
    [DataRow("FSFXWorldNormalPixelShader")]
    [DataRow("FTextureDensityVertexShader")]
    [DataRow("FTextureDensityPixelShader")]
    [DataRow("FHitProxyVertexShader")]
    [DataRow("FHitProxyPixelShader")]
    [DataRow("FModShadowMeshVertexShader")]
    [DataRow("FModShadowMeshPixelShader")]
    public void DiagnosticAndVelocityConstructorsRetainNativeBindings(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3VelocityNormal.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        foreach (bool gpu in new[] { false, true })
        foreach (bool extras in new[] { false, true })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", out var environment));
            bool vertex = environment.Target.Stage == ShaderStage.Vertex;
            bool velocity = name.StartsWith("FVelocity", StringComparison.Ordinal);
            bool densityPixel = name == "FTextureDensityPixelShader", hitPixel = name == "FHitProxyPixelShader";
            string modBinding = name == "FModShadowMeshVertexShader" ? "LightPosition" : name == "FModShadowMeshPixelShader" ? "AttenAllowed" : null;
            string stage = vertex ? "Vertex" : "Pixel";
            string source = $"float4 Uniform{stage}Scalars_0, Uniform{stage}Scalars_1; " +
                (vertex ? "float4x4 LocalToWorld; " + (gpu ? "float4 BoneMatrices[3]; " : "") : "") +
                (densityPixel ? "float4 TextureDensityParameters, TextureLookupInfo; " : hitPixel ? "float4 HitProxyId; " : "") +
                (modBinding == null ? "" : $"float4 {modBinding}; ") +
                (velocity ? vertex ? "float4x4 PrevViewProjectionMatrix; " + (extras ? "float4x4 PreviousLocalToWorld; float4 StretchTimeScale; " : "")
                    : extras ? "float4 VelocityScaleOffset, IndividualVelocityScale, ObjectVelocity; " : "" : "") +
                $"float4 {environment.Target.EntryPoint}(float4 p : {(vertex ? "POSITION" : "SV_Position")}) : {(vertex ? "SV_Position" : "SV_Target")} {{ return " +
                (vertex ? "mul(LocalToWorld,p)" : "p") + $" + Uniform{stage}Scalars_0 + Uniform{stage}Scalars_1" +
                (vertex && gpu ? " + BoneMatrices[0]" : "") +
                (densityPixel ? " + TextureDensityParameters + TextureLookupInfo" : hitPixel ? " + HitProxyId" : "") +
                (modBinding == null ? "" : $" + {modBinding}") +
                (velocity ? vertex ? " + mul(PrevViewProjectionMatrix,p)" + (extras ? " + mul(PreviousLocalToWorld,p) + StretchTimeScale" : "")
                    : extras ? " + VelocityScaleOffset + IndividualVelocityScale + ObjectVelocity" : "" : "") + "; }";
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
            string directory = CreateSourceDirectory(job.Target, source);
            try
            {
                var compiled = compiler.CompileShader(job, directory);
                Assert.IsTrue(compiled.Success, compiled.Diagnostics);
                var construction = compiler.ConstructKnownShader(translation, compiled);
                Assert.IsTrue(construction.Success, string.Join("; ", construction.Errors));
                Assert.AreEqual(0, construction.UnboundParameters.Count);
                Shader shader = construction.Shader;
                using var stream = new MemoryStream();
                new SerializingContainer(stream, package).Serialize(ref shader);
                stream.Position = 0; Shader copy = null;
                new SerializingContainer(stream, package, isLoading: true).Serialize(ref copy);
                Assert.AreEqual(stream.Length, stream.Position);
                Assert.AreEqual(shader.GetType(), copy.GetType());
                Assert.AreEqual(shader.ParameterMapCRC, copy.ParameterMapCRC);
                if (copy is FVelocityVertexShader vs)
                {
                    Assert.IsTrue(vs.PrevViewProjectionMatrix.IsBound());
                    Assert.AreEqual(extras, vs.PreviousLocalToWorld.IsBound());
                    Assert.AreEqual(extras, vs.StretchTimeScale.IsBound());
                }
                if (copy is FVelocityPixelShader ps)
                {
                    Assert.AreEqual(extras, ps.VelocityScaleOffset.IsBound());
                    Assert.AreEqual(extras, ps.IndividualVelocityScale.IsBound());
                    Assert.AreEqual(extras, ps.ObjectVelocity.IsBound());
                }
                if (copy is FTextureDensityPixelShader ds)
                {
                    Assert.IsTrue(ds.TextureDensityParameters.IsBound()); Assert.IsTrue(ds.TextureLookupInfo.IsBound());
                }
                if (copy is FHitProxyPixelShader hs) Assert.IsTrue(hs.HitProxyId.IsBound());
                if (copy is FModShadowMeshVertexShader mvs)
                {
                    Assert.AreEqual((ushort)16, mvs.LightPosition.NumBytes);
                    Assert.AreEqual(((FModShadowMeshVertexShader)shader).LightPosition, mvs.LightPosition);
                    Assert.AreEqual(environment.Target.VertexFactoryTypeName, mvs.VertexFactoryParameters.VertexFactoryType.Name);
                    Assert.AreEqual(2, mvs.MaterialParameters.UniformVertexScalarShaderParameters.Length);
                }
                if (copy is FModShadowMeshPixelShader mps)
                {
                    Assert.AreEqual((ushort)16, mps.AttenAllowed.NumBytes);
                    Assert.AreEqual(((FModShadowMeshPixelShader)shader).AttenAllowed, mps.AttenAllowed);
                    Assert.AreEqual(2, mps.MaterialParameters.UniformPixelScalarShaderParameters.Length);
                }
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    [TestMethod]
    [DataRow("FTextureDensityPixelShader", "TextureDensityParameters", "float4 TextureLookupInfo;")]
    [DataRow("FTextureDensityPixelShader", "TextureLookupInfo", "float4 TextureDensityParameters;")]
    [DataRow("FHitProxyPixelShader", "HitProxyId", "")]
    [DataRow("FModShadowMeshPixelShader", "AttenAllowed", "")]
    public void DiagnosticPixelShadersWithholdObjectWhenRequiredAllocationIsMissing(string name, string missing, string declaration)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DiagnosticRequired.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package); var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        Assert.IsTrue(TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
        string directory = CreateSourceDirectory(environment.Target, declaration + $"float4 {environment.Target.EntryPoint}(float4 p:SV_Position):SV_Target {{ return p " +
            (declaration.Length == 0 ? "" : declaration.Contains("TextureLookupInfo") ? "+ TextureLookupInfo" : "+ TextureDensityParameters") + "; }");
        try
        {
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target);
            var compiled = compiler.CompileShader(job, directory); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
            var result = compiler.ConstructKnownShader(translation, compiled);
            Assert.IsFalse(result.Success); Assert.IsNull(result.Shader);
            Assert.IsTrue(result.Errors.Any(e => e.Contains(missing, StringComparison.Ordinal)));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void VelocityVertexWithholdsObjectWithoutRequiredPreviousViewProjection()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3VelocityRequired.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        Assert.IsTrue(TryGetKnownPermutationEnvironment("FVelocityVertexShader", "FLocalVertexFactory", out var environment));
        string directory = CreateSourceDirectory(environment.Target,
            "float4x4 LocalToWorld; float4 MainVertexShader(float4 p:POSITION):SV_Position { return mul(LocalToWorld,p); }");
        try
        {
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target);
            var compiled = compiler.CompileShader(job, directory);
            Assert.IsTrue(compiled.Success, compiled.Diagnostics);
            var result = compiler.ConstructKnownShader(translation, compiled);
            Assert.IsFalse(result.Success); Assert.IsNull(result.Shader);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("PrevViewProjectionMatrix", StringComparison.Ordinal)));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void ModShadowVertexWithholdsObjectWithoutLightPosition()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ModShadowRequired.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        foreach (string factory in new[] { "FLocalVertexFactory", "FGPUSkinVertexFactory" })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment("FModShadowMeshVertexShader", factory, out var environment));
            string directory = CreateSourceDirectory(environment.Target,
                "float4x4 LocalToWorld; " + (factory == "FGPUSkinVertexFactory" ? "float4 BoneMatrices[3]; " : "") +
                "float4 Main(float4 p:POSITION):SV_Position { return mul(LocalToWorld,p)" +
                (factory == "FGPUSkinVertexFactory" ? " + BoneMatrices[0]" : "") + "; }");
            try
            {
                var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target);
                var compiled = compiler.CompileShader(job, directory);
                Assert.IsTrue(compiled.Success, compiled.Diagnostics);
                var result = compiler.ConstructKnownShader(translation, compiled);
                Assert.IsFalse(result.Success); Assert.IsNull(result.Shader);
                Assert.IsTrue(result.Errors.Any(e => e.Contains("LightPosition", StringComparison.Ordinal)));
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, true, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, true)]
    public void CompiledNoLightMapShaderBindsAndRoundTripsAsTypedLE3Object(bool vertex, bool gpu, bool sky)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShaderObjects.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        var target = Target(vertex) with
        {
            VertexFactoryTypeName = gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory",
            VertexFactoryShaderFileName = gpu ? "GpuSkinVertexFactory" : "LocalVertexFactory",
            ShaderTypeName = sky ? "TBasePassPixelShaderFNoLightMapPolicySkyLight" : Target(vertex).ShaderTypeName
        };
        var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), target);
        string directory = CreateSourceDirectory(target, Source(vertex, gpu));
        try
        {
            var compiled = compiler.CompileShader(job, directory);
            Assert.IsTrue(compiled.Success, compiled.Diagnostics);
            var construction = compiler.ConstructKnownShader(translation, compiled);
            Assert.IsTrue(construction.Success, string.Join("; ", construction.Errors));
            Assert.AreEqual(0, construction.UnboundParameters.Count);
            Assert.AreEqual(vertex ? 2 : 0, construction.RendererOwnedParameters.Count);
            Shader shader = construction.Shader;
            Assert.AreEqual(target.ShaderTypeName, shader.ShaderType.Name);
            Assert.AreEqual((byte)5, shader.Platform);
            Assert.AreEqual(vertex ? Shader.ShaderFrequency.Vertex : Shader.ShaderFrequency.PixelSM5, shader.Frequency);
            Assert.AreEqual(vertex ? (byte)0 : (byte)3, (byte)shader.Frequency);
            Assert.AreNotEqual(Guid.Empty, shader.Guid);
            Assert.AreEqual(compiled.InstructionCount, shader.InstructionCount);
            Assert.AreEqual(new LE3ShaderParameterBinder(compiled.ParameterBindings).ParameterMapCRC, shader.ParameterMapCRC);
            CollectionAssert.AreEqual(compiled.Bytecode, shader.ShaderByteCode);
            Assert.AreNotSame(compiled.Bytecode, shader.ShaderByteCode);
            using var stream = new MemoryStream();
            new SerializingContainer(stream, package).Serialize(ref shader);
            stream.Position = 0;
            Shader loaded = null;
            new SerializingContainer(stream, package, isLoading: true).Serialize(ref loaded);
            Assert.AreEqual(stream.Length, stream.Position);
            Assert.AreEqual(shader.GetType(), loaded.GetType());
            Assert.AreEqual(shader.ShaderType, loaded.ShaderType);
            Assert.AreEqual(shader.Guid, loaded.Guid);
            Assert.AreEqual(shader.ParameterMapCRC, loaded.ParameterMapCRC);
            CollectionAssert.AreEqual(shader.ShaderByteCode, loaded.ShaderByteCode);
            if (vertex)
            {
                var original = (TBasePassVertexShader<FNullPolicy, FNullPolicy>)shader;
                var copy = (TBasePassVertexShader<FNullPolicy, FNullPolicy>)loaded;
                Assert.AreEqual(gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", copy.VertexFactoryType.Value.Name);
                Assert.AreEqual((ushort)2, copy.MaterialParameters.UniformVertexScalarShaderParameters[0].Param.BufferIndex);
                Assert.AreEqual(2, copy.MaterialParameters.UniformVertexScalarShaderParameters.Length);
                Assert.AreEqual(original.HeightFogParameters.FogMinHeight, copy.HeightFogParameters.FogMinHeight);
                if (gpu) Assert.AreEqual(((FGPUSkinVertexFactoryShaderParameters)original.VertexFactoryParameters.Parameters).BoneMatrices,
                    ((FGPUSkinVertexFactoryShaderParameters)copy.VertexFactoryParameters.Parameters).BoneMatrices);
                else Assert.AreEqual(((FLocalVertexFactoryShaderParameters)original.VertexFactoryParameters.Parameters).LocalToWorld,
                    ((FLocalVertexFactoryShaderParameters)copy.VertexFactoryParameters.Parameters).LocalToWorld);
            }
            else
            {
                var original = (TBasePassPixelShader<FNullPolicy>)shader;
                var copy = (TBasePassPixelShader<FNullPolicy>)loaded;
                Assert.AreEqual((ushort)3, copy.MaterialParameters.UniformPixelScalarShaderParameters[0].Param.BufferIndex);
                Assert.AreEqual(2, copy.MaterialParameters.UniformPixelScalarShaderParameters.Length);
                Assert.IsTrue(copy.MaterialParameters.HasContiguousPixelScalarParameters);
                Assert.AreEqual(original.TranslucencyDepth, copy.TranslucencyDepth);
                Assert.AreEqual(original.MaterialParameters.SceneTextureParameters.SceneDepthTexture,
                    copy.MaterialParameters.SceneTextureParameters.SceneDepthTexture);
                Assert.IsNull(copy.VertexFactoryType, "Pixel shaders do not serialize a VF parameter reference.");
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void UnexplainedParametersWithholdTheShaderInsteadOfSilentlyDiscardingBindings()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3UnboundShader.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        var target = Target(false);
        var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), target);
        string directory = CreateSourceDirectory(target,
            "float4 UnrecoveredPolicyParameter; float4 Main(float4 p : SV_Position) : SV_Target { return UnrecoveredPolicyParameter; }");
        try
        {
            var compiled = compiler.CompileShader(job, directory);
            Assert.IsTrue(compiled.Success, compiled.Diagnostics);
            var result = compiler.ConstructKnownShader(translation, compiled);
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Shader);
            Assert.AreEqual("UnrecoveredPolicyParameter", result.UnboundParameters[0].Name);
            StringAssert.Contains(result.Errors[0], "UnrecoveredPolicyParameter");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    [DataRow("TDepthOnlyVertexShader<0>")]
    [DataRow("TDepthOnlyVertexShader<1>")]
    [DataRow("TDepthOnlySolidPixelShader")]
    [DataRow("TDepthOnlyScreenDoorPixelShader")]
    [DataRow("FTranslucencyPostRenderDepthPixelShader")]
    [DataRow("TDistortionMeshVertexShader<FDistortMeshAccumulatePolicy>")]
    [DataRow("TDistortionMeshPixelShader<FDistortMeshAccumulatePolicy>")]
    [DataRow("TShadowDepthVertexShader<ShadowDepth_OutputDepth>")]
    [DataRow("TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>")]
    [DataRow("TShadowDepthVertexShader<ShadowDepth_OutputDepthToColor>")]
    [DataRow("TShadowDepthPixelShaderTRUETRUE")]
    [DataRow("TShadowDepthPixelShaderFALSETRUE")]
    [DataRow("TShadowDepthPixelShaderTRUEFALSE")]
    [DataRow("TShadowDepthPixelShaderFALSEFALSE")]
    public void MaterialMeshAndDepthConstructorsBindAndRoundTripNativeFieldsForBothFactories(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DepthObjects.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        foreach (bool gpu in new[] { false, true })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", out var environment));
            var candidate = environment.CreateCandidate();
            bool vertex = candidate.Target.Stage == ShaderStage.Vertex, shadow = name.StartsWith("TShadowDepth", StringComparison.Ordinal);
            string stage = vertex ? "Vertex" : "Pixel";
            string source = $"float4 Uniform{stage}Scalars_0, Uniform{stage}Scalars_1; " +
                (vertex ? "float4x4 LocalToWorld; " + (gpu ? "float4 BoneMatrices[3]; " : "") : "") +
                (shadow ? "float InvMaxSubjectDepth, DepthBias; " + (vertex ? "float4x4 ProjectionMatrix; float bClampToNearPlane; " : "") : "") +
                (vertex ? "float4 Main(float4 p:POSITION):SV_Position { return mul(LocalToWorld,p)" : "float4 Main(float4 p:SV_Position):SV_Target { return p") +
                $" + Uniform{stage}Scalars_0 + Uniform{stage}Scalars_1" +
                (gpu && vertex ? " + BoneMatrices[0]" : "") +
                (shadow ? " + InvMaxSubjectDepth + DepthBias" + (vertex ? " + mul(ProjectionMatrix,p) + bClampToNearPlane" : "") : "") + "; }";
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), candidate.Target, candidate.EnvironmentMacros);
            string directory = CreateSourceDirectory(candidate.Target, source);
            try
            {
                var compiled = compiler.CompileShader(job, directory);
                Assert.IsTrue(compiled.Success, compiled.Diagnostics);
                var construction = compiler.ConstructKnownShader(translation, compiled);
                Assert.IsTrue(construction.Success, string.Join("; ", construction.Errors));
                Shader shader = construction.Shader;
                using var stream = new MemoryStream();
                new SerializingContainer(stream, package).Serialize(ref shader);
                stream.Position = 0;
                Shader loaded = null;
                new SerializingContainer(stream, package, isLoading: true).Serialize(ref loaded);
                Assert.AreEqual(stream.Length, stream.Position);
                Assert.AreEqual(shader.GetType(), loaded.GetType());
                Assert.AreEqual(vertex ? (byte)0 : (byte)3, (byte)loaded.Frequency);
                Assert.AreEqual(shader.ParameterMapCRC, loaded.ParameterMapCRC);
                CollectionAssert.AreEqual(shader.ShaderByteCode, loaded.ShaderByteCode);
                if (vertex)
                {
                    var vf = shadow ? ((TShadowDepthVertexShader)loaded).VertexFactoryParameters : ((MaterialVertexShader)loaded).VertexFactoryParameters;
                    Assert.AreEqual(candidate.Target.VertexFactoryTypeName, vf.VertexFactoryType.Name);
                    Assert.AreEqual(candidate.Target.VertexFactoryTypeName, loaded.VertexFactoryType.Value.Name);
                    var material = shadow ? ((TShadowDepthVertexShader)loaded).MaterialParameters : ((MaterialVertexShader)loaded).MaterialParameters;
                    Assert.AreEqual(2, material.UniformVertexScalarShaderParameters.Length);
                    if (shadow)
                    {
                        var original = (TShadowDepthVertexShader)shader;
                        var copy = (TShadowDepthVertexShader)loaded;
                        Assert.AreEqual((ushort)64, copy.ProjectionMatrix.NumBytes);
                        Assert.AreEqual(original.ProjectionMatrix, copy.ProjectionMatrix);
                        Assert.AreEqual(original.DepthBias, copy.DepthBias);
                        Assert.AreEqual((ushort)4, copy.bClampToNearPlane.NumBytes);
                    }
                }
                else
                {
                    Assert.IsNull(loaded.VertexFactoryType);
                    Assert.AreEqual(2, ((MaterialPixelShader)loaded).MaterialParameters.UniformPixelScalarShaderParameters.Length);
                    if (shadow)
                    {
                        Assert.AreEqual(((TShadowDepthPixelShader)shader).InvMaxSubjectDepth, ((TShadowDepthPixelShader)loaded).InvMaxSubjectDepth);
                        Assert.AreEqual((ushort)4, ((TShadowDepthPixelShader)loaded).DepthBias.NumBytes);
                    }
                }
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    [TestMethod]
    [DataRow("TShadowDepthVertexShader<ShadowDepth_OutputDepth>")]
    [DataRow("TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>")]
    [DataRow("TShadowDepthVertexShader<ShadowDepth_OutputDepthToColor>")]
    public void ShadowDepthProjectionIsRequiredButOtherDepthFieldsAreOptional(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShadowRequired.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        Assert.IsTrue(TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
        var candidate = environment.CreateCandidate();
        var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), candidate.Target, candidate.EnvironmentMacros);
        string directory = CreateSourceDirectory(candidate.Target, "float4 Main(float4 p:POSITION):SV_Position { return p; }");
        try
        {
            var result = compiler.ConstructKnownShader(translation, compiler.CompileShader(job, directory));
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Shader);
            StringAssert.Contains(result.Errors.Single(), "ProjectionMatrix");
            File.WriteAllText(Path.Combine(directory, candidate.Target.SourceFileName + ".usf"),
                "float4x4 ProjectionMatrix; float4 Main(float4 p:POSITION):SV_Position { return mul(ProjectionMatrix,p); }");
            result = compiler.ConstructKnownShader(translation, compiler.CompileShader(job, directory));
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            var shader = (TShadowDepthVertexShader)result.Shader;
            Assert.AreEqual((ushort)0, shader.InvMaxSubjectDepth.NumBytes);
            Assert.AreEqual((ushort)0, shader.DepthBias.NumBytes);
            Assert.AreEqual((ushort)0, shader.bClampToNearPlane.NumBytes);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    [DataRow("Constant", false)]
    [DataRow("Constant", true)]
    [DataRow("LinearHalfspace", false)]
    [DataRow("LinearHalfspace", true)]
    [DataRow("Sphere", false)]
    [DataRow("Sphere", true)]
    public void FogDensityConstructorsPreserveDistinctHeightAndDensityBindings(string density, bool optimizedOut)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FogObjects.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        string name = $"TBasePassVertexShaderFNoLightMapPolicyF{density}DensityPolicy";
        string[] fields = ["FirstDensityFunctionParameters", "SecondDensityFunctionParameters", "StartDistance", "FogVolumeBoxMin", "FogVolumeBoxMax", "ApproxFogColor",
            "FogDistanceScale", "FogExtinctionDistance", "FogMinHeight", "FogMaxHeight", "FogInScattering", "FogStartDistance"];
        foreach (bool gpu in new[] { false, true })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", out var environment));
            var candidate = environment.CreateCandidate();
            string source = "float4x4 LocalToWorld; " + (gpu ? "float4 BoneMatrices[3]; " : "") +
                string.Join(" ", fields.Select(f => "float4 " + f + ";")) +
                " float4 Main(float4 p:POSITION):SV_Position { return mul(LocalToWorld,p)" + (gpu ? "+BoneMatrices[0]" : "") +
                (optimizedOut ? "" : "+" + string.Join("+", fields)) + "; }";
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), candidate.Target, candidate.EnvironmentMacros);
            string directory = CreateSourceDirectory(candidate.Target, source);
            try
            {
                var compiled = compiler.CompileShader(job, directory);
                Assert.IsTrue(compiled.Success, compiled.Diagnostics);
                var result = compiler.ConstructKnownShader(translation, compiled);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Shader shader = result.Shader;
                using var stream = new MemoryStream();
                new SerializingContainer(stream, package).Serialize(ref shader);
                stream.Position = 0;
                Shader loaded = null;
                new SerializingContainer(stream, package, isLoading: true).Serialize(ref loaded);
                Assert.AreEqual(stream.Length, stream.Position);
                var copy = (TBasePassVertexShader<FNullPolicy, FConstantDensityPolicy.VertexShaderParametersType>)loaded;
                var original = (TBasePassVertexShader<FNullPolicy, FConstantDensityPolicy.VertexShaderParametersType>)shader;
                Assert.AreEqual(candidate.Target.VertexFactoryTypeName, copy.VertexFactoryType.Value.Name);
                Assert.AreEqual(original.DensityVertexShaderParams, copy.DensityVertexShaderParams);
                Assert.AreEqual(original.HeightFogParameters, copy.HeightFogParameters);
                Assert.AreEqual((ushort)(optimizedOut ? 0 : 16), copy.DensityVertexShaderParams.FirstDensityFunction.NumBytes);
                Assert.AreEqual((ushort)(optimizedOut ? 0 : 16), copy.DensityVertexShaderParams.SecondDensityFunction.NumBytes);
                Assert.AreEqual((ushort)(optimizedOut ? 0 : 16), copy.DensityVertexShaderParams.ApproxFogColor.NumBytes);
                Assert.AreEqual((ushort)(optimizedOut ? 0 : 16), copy.DensityVertexShaderParams.StartDistance.NumBytes);
                Assert.AreEqual((ushort)(optimizedOut ? 0 : 16), copy.HeightFogParameters.FogStartDistance.NumBytes);
                if (!optimizedOut) Assert.AreNotEqual(copy.DensityVertexShaderParams.StartDistance.BaseIndex, copy.HeightFogParameters.FogStartDistance.BaseIndex);
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    [TestMethod]
    [DataRow("DirectionalLightMapTexture", "Vertex", false)]
    [DataRow("SimpleLightMapTexture", "Vertex", false)]
    [DataRow("SimpleVertexLightMap", "Vertex", false)]
    [DataRow("DirectionalLightMapTexture", "Pixel", false)]
    [DataRow("DirectionalLightMapTexture", "Pixel", true)]
    [DataRow("SimpleLightMapTexture", "Pixel", false)]
    [DataRow("SimpleLightMapTexture", "Pixel", true)]
    [DataRow("SimpleVertexLightMap", "Pixel", false)]
    [DataRow("SimpleVertexLightMap", "Pixel", true)]
    [DataRow("DirectionalVertexLightMap", "Vertex", false)]
    [DataRow("DirectionalVertexLightMap", "Pixel", false)]
    [DataRow("DirectionalVertexLightMap", "Pixel", true)]
    [DataRow("CustomVectorVertexLightMap", "Vertex", false)]
    [DataRow("CustomVectorVertexLightMap", "Pixel", false)]
    [DataRow("CustomVectorVertexLightMap", "Pixel", true)]
    [DataRow("CustomSimpleVertexLightMap", "Vertex", false)]
    [DataRow("CustomSimpleVertexLightMap", "Pixel", false)]
    [DataRow("CustomSimpleVertexLightMap", "Pixel", true)]
    [DataRow("CustomVectorLightMapTexture", "Vertex", false)]
    [DataRow("CustomVectorLightMapTexture", "Pixel", false)]
    [DataRow("CustomVectorLightMapTexture", "Pixel", true)]
    [DataRow("CustomSimpleLightMapTexture", "Vertex", false)]
    [DataRow("CustomSimpleLightMapTexture", "Pixel", false)]
    [DataRow("CustomSimpleLightMapTexture", "Pixel", true)]
    public void LightmapPolicyConstructorsBindAndRoundTripNativeFields(string policy, string stage, bool sky)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3LightmapObjects.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        bool vertex = stage == "Vertex", texture = policy.Contains("Texture", StringComparison.Ordinal);
        bool customTexture = texture && policy.StartsWith("Custom", StringComparison.Ordinal);
        string name = $"TBasePass{stage}ShaderF{policy}" + (vertex ? "PolicyFNoDensityPolicy" : sky ? "PolicySkyLight" : "PolicyNoSkyLight");
        Assert.IsTrue(TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
        var candidate = environment.CreateCandidate();
        var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), candidate.Target, candidate.EnvironmentMacros);
        string source = vertex
            ? "float4 " + (texture ? "LightmapCoordinateScaleBias" : "LightMapScale") + "; float4 Main(float4 p:POSITION):SV_Position { return p + " +
                (texture ? "LightmapCoordinateScaleBias" : "LightMapScale") + "; }"
            : texture ? "float4 LightMapScale; " + (customTexture ? "float4 LightMapBias; " : "") + "Texture2D LightMapTextures; SamplerState LightMapTexturesSampler; float4 Main(float4 p:SV_Position):SV_Target { return LightMapTextures.Sample(LightMapTexturesSampler,p.xy)*LightMapScale" + (customTexture ? "+LightMapBias" : "") + "; }"
                : "float4 Main(float4 p:SV_Position):SV_Target { return 1; }";
        string directory = CreateSourceDirectory(candidate.Target, source);
        try
        {
            var compiled = compiler.CompileShader(job, directory);
            Assert.IsTrue(compiled.Success, compiled.Diagnostics);
            var result = compiler.ConstructKnownShader(translation, compiled);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Shader shader = result.Shader;
            using var stream = new MemoryStream();
            new SerializingContainer(stream, package).Serialize(ref shader);
            stream.Position = 0;
            Shader loaded = null;
            new SerializingContainer(stream, package, isLoading: true).Serialize(ref loaded);
            Assert.AreEqual(stream.Length, stream.Position);
            Assert.AreEqual(shader.GetType(), loaded.GetType());
            if (vertex && texture) Assert.AreEqual((ushort)16, ((TBasePassVertexShader<FLightMapTexturePolicy.VertexParametersType, FNullPolicy>)loaded).LightMapVertexParams.LightmapCoordinateScaleBias.NumBytes);
            else if (vertex) Assert.AreEqual((ushort)16, ((TBasePassVertexShader<FVertexLightMapPolicy.VertexParametersType, FNullPolicy>)loaded).LightMapVertexParams.LightMapScale.NumBytes);
            else if (customTexture)
            {
                var parameters = ((TBasePassPixelShader<FCustomLightMapTexturePolicy.PixelParametersType>)loaded).PixelParams;
                Assert.AreEqual((ushort)1, parameters.LightMapTextures.NumResources);
                Assert.AreEqual((ushort)16, parameters.LightMapScale.NumBytes);
                Assert.AreEqual((ushort)16, parameters.LightMapBias.NumBytes);
                Assert.AreNotEqual(parameters.LightMapScale.BaseIndex, parameters.LightMapBias.BaseIndex);
            }
            else if (texture) Assert.AreEqual((ushort)1, ((TBasePassPixelShader<FLightMapTexturePolicy.PixelParametersType>)loaded).PixelParams.LightMapTextures.NumResources);
            else Assert.IsInstanceOfType<TBasePassPixelShader<FNullPolicy>>(loaded);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void ConstructorRejectsDifferentTranslationAndWithholdsFailedOrUnregisteredCompiles()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShaderProvenance.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        var target = Target(false) with { ShaderTypeName = "UnregisteredPixelShader", SourceFileName = "DepthOnlyPixelShader" };
        var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), target);
        string directory = CreateSourceDirectory(target, "float4 Main(float4 p : SV_Position) : SV_Target { return 1; }");
        try
        {
            var compiled = compiler.CompileShader(job, directory);
            Assert.IsTrue(compiled.Success, compiled.Diagnostics);
            var unsupported = compiler.ConstructKnownShader(translation, compiled);
            Assert.IsFalse(unsupported.Success);
            StringAssert.Contains(unsupported.Errors[0], "registration metadata");
            var secondTranslation = compiler.TranslateMaterialHlsl(CommentTemplate());
            Assert.ThrowsExactly<ArgumentException>(() => compiler.ConstructKnownShader(secondTranslation, compiled));
            File.WriteAllText(Path.Combine(directory, target.SourceFileName + ".usf"), "invalid hlsl");
            var failed = compiler.CompileShader(job, directory);
            Assert.IsFalse(failed.Success);
            var result = compiler.ConstructKnownShader(translation, failed);
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Shader);
            StringAssert.Contains(result.Errors[0], "failed bytecode compilation");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    [DataRow("SimpleVertexLightMap", false)]
    [DataRow("DirectionalVertexLightMap", false)]
    [DataRow("CustomVectorVertexLightMap", false)]
    [DataRow("CustomSimpleVertexLightMap", false)]
    [DataRow("DirectionalLightMapTexture", true)]
    [DataRow("SimpleLightMapTexture", true)]
    [DataRow("CustomVectorLightMapTexture", true)]
    [DataRow("CustomSimpleLightMapTexture", true)]
    public void MissingLightmapVertexScaleRespectsNativeRequiredAndOptionalFlags(string policy, bool success)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3RequiredLightmapScale.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        string name = "TBasePassVertexShaderF" + policy + "PolicyFNoDensityPolicy";
        Assert.IsTrue(TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
        var target = environment.Target;
        var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), target);
        string directory = CreateSourceDirectory(target, "float4 Main(float4 p:POSITION):SV_Position { return p; }");
        try
        {
            var compiled = compiler.CompileShader(job, directory);
            Assert.IsTrue(compiled.Success, compiled.Diagnostics);
            var constructed = compiler.ConstructKnownShader(translation, compiled);
            Assert.AreEqual(success, constructed.Success);
            if (!success) StringAssert.Contains(constructed.Errors[0], "Missing required LE3 shader parameter: LightMapScale");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void ShaderMapFragmentGroupsPixelAndVertexJobsAndRoundTripsResolvedStaticKey()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MapFragment.pcc", MEGame.LE3);
        var baseId = Guid.NewGuid();
        var staticParameters = (StaticParameterSet)baseId;
        staticParameters.StaticSwitchParameters = [new() { ParameterName = "InheritedSwitch", Value = true, bOverride = false, ExpressionGUID = Guid.NewGuid() }];
        var compiler = CreateCompiler(package, staticParameters);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        var plan = CreatePlan(compiler, translation);
        string directory = CreateSourceDirectory(Target(true), Source(true));
        File.WriteAllText(Path.Combine(directory, "BasePassPixelShader.usf"), Source(false));
        try
        {
            var results = plan.Jobs.Select(j => compiler.CompileShader(j, directory)).ToArray();
            Guid resourceId = Guid.NewGuid();
            var fragment = compiler.BuildShaderMapFragment(translation, plan, results, resourceId, "CallerResolvedInstance");
            Assert.IsTrue(fragment.Success, string.Join("; ", fragment.Errors));
            Assert.IsFalse(fragment.IsCompleteShaderSet, "An explicit subset plan is not engine-registry coverage.");
            Assert.AreEqual(2, fragment.Shaders.Count);
            Assert.AreEqual(0, fragment.Map.Shaders.Count);
            Assert.AreEqual(1, fragment.Map.MeshShaderMaps.Length);
            var meshMap = fragment.Map.MeshShaderMaps.Single();
            Assert.AreEqual("FLocalVertexFactory", meshMap.VertexFactoryType.Name);
            Assert.AreEqual(2, meshMap.Shaders.Count, "Pixel objects have null VF fields but still belong to the recorded job's mesh map.");
            Assert.AreEqual(resourceId, fragment.Map.ID);
            Assert.AreEqual(baseId, fragment.Map.StaticParameters.BaseMaterialId, "Resource ID and resolved static key must remain independent for future instance work.");
            Assert.IsFalse(fragment.Map.StaticParameters.StaticSwitchParameters[0].bOverride);
            Assert.AreNotSame(staticParameters.StaticSwitchParameters[0], fragment.Map.StaticParameters.StaticSwitchParameters[0]);
            Assert.AreEqual(5, fragment.Map.UniformPixelScalarExpressions.Length);
            Assert.AreEqual(5, fragment.Map.UniformVertexScalarExpressions.Length);
            Assert.AreSame(fragment.Map.UniformPixelScalarExpressions[0], fragment.Map.UniformVertexScalarExpressions[0]);
            foreach (var pair in meshMap.Shaders)
            {
                Assert.IsTrue(fragment.Shaders.TryGetValue(pair.Value.Id, out var shader));
                Assert.AreEqual(pair.Key, shader.ShaderType);
                Assert.AreEqual(pair.Value.ShaderType, shader.ShaderType);
            }
            MaterialShaderMap map = fragment.Map;
            using var stream = new MemoryStream();
            new SerializingContainer(stream, package).Serialize(ref map);
            stream.Position = 0;
            MaterialShaderMap loaded = null;
            new SerializingContainer(stream, package, isLoading: true).Serialize(ref loaded);
            Assert.AreEqual(stream.Length, stream.Position);
            Assert.AreEqual(resourceId, loaded.ID);
            Assert.AreEqual("CallerResolvedInstance", loaded.FriendlyName);
            Assert.AreEqual(baseId, loaded.StaticParameters.BaseMaterialId);
            Assert.AreEqual(2, loaded.MeshShaderMaps.Single().Shaders.Count);
            Assert.AreEqual(5, loaded.UniformPixelScalarExpressions.Length);
            Assert.AreEqual(5, loaded.UniformVertexScalarExpressions.Length);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("duplicate")]
    [DataRow("failed")]
    public void ShaderMapFragmentWithholdsAllObjectsWhenAnyPlanJobIsMissingDuplicateOrFailed(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FragmentFailures.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        var plan = CreatePlan(compiler, translation);
        string directory = CreateSourceDirectory(Target(true), Source(true));
        File.WriteAllText(Path.Combine(directory, "BasePassPixelShader.usf"), shape == "failed" ? "invalid hlsl" : Source(false));
        try
        {
            var compiled = plan.Jobs.Select(j => compiler.CompileShader(j, directory)).ToArray();
            var selected = shape == "missing" ? compiled.Take(1).ToArray() :
                shape == "duplicate" ? compiled.Concat(compiled.Take(1)).ToArray() : compiled;
            var fragment = compiler.BuildShaderMapFragment(translation, plan, selected, Guid.NewGuid(), "Incomplete");
            Assert.IsFalse(fragment.Success);
            Assert.IsNull(fragment.Map);
            Assert.AreEqual(0, fragment.Shaders.Count, "Do not expose successfully constructed partial objects through a failed fragment.");
            StringAssert.Contains(string.Join("; ", fragment.Errors), shape == "missing" ? "Missing compiled" : shape == "duplicate" ? "Duplicate compiled" : "failed bytecode");
            Assert.ThrowsExactly<ArgumentException>(() => compiler.BuildShaderMapFragment(translation, plan, compiled, Guid.Empty, "Invalid"));
            var other = compiler.TranslateMaterialHlsl(CommentTemplate());
            Assert.ThrowsExactly<ArgumentException>(() => compiler.BuildShaderMapFragment(other, plan, compiled, Guid.NewGuid(), "WrongTranslation"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static ShaderCompilePlan CreatePlan(LE3MaterialShaderCompiler compiler, TranslationResult translation)
    {
        var candidates = new[] { false, true }.Select(vertex =>
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(Target(vertex).ShaderTypeName, "FLocalVertexFactory", out var environment));
            return environment.CreateCandidate();
        }).ToArray();
        var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), candidates);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors));
        Assert.AreEqual(2, plan.Jobs.Count);
        return plan;
    }

    [TestMethod]
    public void KnownSubsetPlannerMatchesExplicitCandidatesAndRetainsEveryDecision()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3KnownPlan.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        var candidates = GetKnownShaderTypeRegistrations().SelectMany(shader => GetKnownVertexFactoryRegistrations().Select(factory =>
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(shader.Name, factory.Name, out var environment));
            return environment.CreateCandidate();
        })).ToArray();
        var explicitPlan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), candidates);
        var known = compiler.BuildKnownSubsetCompilePlan(translation, MaterialResource.Create());
        Assert.IsTrue(known.Success, string.Join("; ", known.Errors));
        Assert.AreEqual(candidates.Length, known.Decisions.Count);
        Assert.AreEqual(candidates.Length, known.Jobs.Count + known.ExcludedTargets.Count);
        CollectionAssert.AreEqual(explicitPlan.Jobs.Select(j => j.Target).ToArray(), known.Jobs.Select(j => j.Target).ToArray());
        CollectionAssert.AreEqual(explicitPlan.ExcludedTargets.ToArray(), known.ExcludedTargets.ToArray());
        CollectionAssert.AreEqual(explicitPlan.Decisions.ToArray(), known.Decisions.ToArray());
    }

    [TestMethod]
    [DataRow("valid")]
    [DataRow("missingSource")]
    [DataRow("missingJob")]
    [DataRow("pixelOnly")]
    public void ShaderCacheFragmentIncludesSourceMapsAndWithholdsIncompleteInputs(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CacheFragment.pcc", MEGame.LE3);
        var baseId = Guid.NewGuid();
        var compiler = CreateCompiler(package, (StaticParameterSet)baseId);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        var plan = CreatePlan(compiler, translation);
        if (shape == "pixelOnly")
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(Target(false).ShaderTypeName, "FLocalVertexFactory", out var env));
            plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), new[] { env.CreateCandidate() });
        }
        string directory = CreateSourceDirectory(Target(true), Source(true));
        File.WriteAllText(Path.Combine(directory, "BasePassPixelShader.usf"), Source(false));
        File.WriteAllText(Path.Combine(directory, "LocalVertexFactory.usf"), "// exact factory source snapshot for this fixture\r\n");
        try
        {
            var results = plan.Jobs.Select(j => compiler.CompileShader(j, directory)).ToArray();
            if (shape == "missingJob") results = results.Take(1).ToArray();
            var crcSession = shape == "missingSource" ? new LE3ShaderSourceCrc(_ => null) : LE3ShaderSourceCrc.FromDirectory(directory);
            Guid resourceId = Guid.NewGuid();
            var fragment = compiler.BuildShaderCacheFragment(translation, plan, results, resourceId, "CallerStaticKey", crcSession);
            if (shape is "missingSource" or "missingJob")
            {
                Assert.IsFalse(fragment.Success);
                Assert.IsNull(fragment.Cache);
                Assert.AreEqual(0, fragment.UniformExpressionTextures.Count);
                StringAssert.Contains(string.Join("; ", fragment.Errors), shape == "missingSource" ? "source CRC maps" : "Missing compiled");
                return;
            }
            Assert.IsTrue(fragment.Success, string.Join("; ", fragment.Errors));
            Assert.IsFalse(fragment.IsCompleteShaderSet);
            var cache = fragment.Cache;
            Assert.AreEqual(results.Length, cache.Shaders.Count);
            Assert.AreEqual(results.Length, cache.ShaderTypeCRCMap.Count);
            Assert.AreEqual(1, cache.VertexFactoryTypeCRCMap.Count);
            Assert.AreEqual(crcSession.Compute("LocalVertexFactory").Crc, cache.VertexFactoryTypeCRCMap.Single().Value,
                "Even a PS-only fragment must carry its recorded job's vertex-factory CRC.");
            foreach (var pair in cache.ShaderTypeCRCMap)
            {
                Assert.IsTrue(TryGetKnownShaderTypeRegistration(pair.Key.Name, out var registration));
                Assert.AreEqual(crcSession.Compute(registration.SourceFileName).Crc, pair.Value);
            }
            using var stream = new MemoryStream();
            cache.PublicSerialize(new SerializingContainer(stream, package));
            stream.Position = 0;
            var loaded = ShaderCache.Create();
            loaded.PublicSerialize(new SerializingContainer(stream, package, isLoading: true));
            Assert.AreEqual(stream.Length, stream.Position);
            Assert.AreEqual(cache.ShaderTypeCRCMap.Count, loaded.ShaderTypeCRCMap.Count);
            Assert.AreEqual(cache.VertexFactoryTypeCRCMap.Single(), loaded.VertexFactoryTypeCRCMap.Single());
            Assert.AreEqual(results.Length, loaded.Shaders.Count);
            Assert.AreEqual(baseId, loaded.MaterialShaderMaps.Single().Key.BaseMaterialId);
            Assert.AreEqual(resourceId, loaded.MaterialShaderMaps.Single().Value.ID);
            Assert.AreEqual(results.Length, loaded.MaterialShaderMaps.Single().Value.MeshShaderMaps.Single().Shaders.Count);
            foreach (var pair in loaded.Shaders) Assert.AreEqual(pair.Key, pair.Value.Guid);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SnapshotTranslationCompilationAndCacheRequireTheSameSourceIdentity(bool missingInclude)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SnapshotCompiler.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var texts = new Dictionary<string, string>
        {
            ["MaterialTemplate.usf"] = CommentTemplate(),
            ["BasePassVertexShader.usf"] = "#include \"Material.usf\"\n#include \"VertexFactory.usf\"\n#include \"Shared.usf\"\n" + Source(true),
            ["BasePassPixelShader.usf"] = "#include \"Material.usf\"\n#include \"Shared.usf\"\n" + Source(false),
            ["LocalVertexFactory.usf"] = "// original VF\r\n"
        };
        if (!missingInclude) texts.Add("Shared.usf", "// original shared include\r\n");
        var snapshot = new LE3ShaderSourceSnapshot(texts);
        var translation = compiler.TranslateMaterialHlsl(snapshot);
        Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
        Assert.AreSame(snapshot, translation.SourceSnapshot);
        var plan = CreatePlan(compiler, translation);
        var otherSnapshot = new LE3ShaderSourceSnapshot(texts);
        Assert.ThrowsExactly<InvalidOperationException>(() => compiler.CompileShader(plan.Jobs[0], otherSnapshot));
        Assert.ThrowsExactly<InvalidOperationException>(() => compiler.CompileShader(plan.Jobs[0], "ignored"));
        texts.Clear(); // Includes, root and template must all come from the copied set.
        var results = plan.Jobs.Select(job => compiler.CompileShader(job, snapshot)).ToArray();
        foreach (var result in results) Assert.AreSame(snapshot, result.SourceSnapshot);
        var fragment = compiler.BuildShaderCacheFragment(translation, plan, results, Guid.NewGuid(), "SnapshotSubset", snapshot);
        if (missingInclude)
        {
            Assert.IsTrue(results.All(result => !result.Success));
            Assert.IsFalse(fragment.Success);
            Assert.IsNull(fragment.Cache);
            return;
        }
        Assert.IsTrue(results.All(result => result.Success), string.Join("; ", results.Select(result => result.Diagnostics)));
        Assert.IsTrue(fragment.Success, string.Join("; ", fragment.Errors));
        Assert.AreEqual(results.Length, fragment.Cache.Shaders.Count);
        var rejected = compiler.BuildShaderCacheFragment(translation, plan, results, Guid.NewGuid(), "WrongSnapshot", otherSnapshot);
        Assert.IsFalse(rejected.Success);
        Assert.IsNull(rejected.Cache);
        StringAssert.Contains(string.Join("; ", rejected.Errors), "same LE3 source snapshot");
        var oldTranslation = compiler.TranslateMaterialHlsl(CommentTemplate());
        var oldPlan = CreatePlan(compiler, oldTranslation);
        Assert.ThrowsExactly<InvalidOperationException>(() => compiler.CompileShader(oldPlan.Jobs[0], snapshot));
        Assert.IsFalse(compiler.BuildShaderCacheFragment(oldTranslation, oldPlan, results, Guid.NewGuid(), "MissingTemplateIdentity", snapshot).Success);
    }

    [TestMethod]
    public void KnownSM5PlannerRejectsPS3HalfTypeTranslationBeforeEmittingJobs()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SM5HalfGuard.pcc", MEGame.LE3);
        var compiler = CreateCompiler(package);
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate(), useHalfTypes: true);
        Assert.IsTrue(translation.Success);
        Assert.IsTrue(translation.UsesHalfTypes);
        var plan = compiler.BuildKnownSubsetCompilePlan(translation, MaterialResource.Create());
        Assert.IsFalse(plan.Success);
        Assert.AreEqual(0, plan.Jobs.Count);
        Assert.AreEqual(0, plan.ExcludedTargets.Count);
        StringAssert.Contains(string.Join("; ", plan.Errors), "PS3-only");
        Assert.IsTrue(TryGetKnownPermutationEnvironment(Target(false).ShaderTypeName, "FLocalVertexFactory", out var environment));
        var candidate = environment.CreateCandidate();
        Assert.ThrowsExactly<InvalidOperationException>(() => compiler.BuildProvisionalCompileJob(translation,
            MaterialResource.Create(), candidate.Target, candidate.EnvironmentMacros));
        var explicitPlan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), new[] { candidate });
        Assert.IsFalse(explicitPlan.Success);
        Assert.AreEqual(0, explicitPlan.Jobs.Count);
        Assert.AreEqual(ShaderCompileGate.CompilationEnvironment, explicitPlan.Decisions.Single().Gate);
        var floating = compiler.TranslateMaterialHlsl(CommentTemplate());
        Assert.IsFalse(floating.UsesHalfTypes);
        Assert.IsTrue(compiler.BuildKnownSubsetCompilePlan(floating, MaterialResource.Create()).Success);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MetadataOnlyTextureCacheOmitsOptimizedBindingsButKeepsExpressionSlots(bool cubeOnly)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TextureCacheBindings.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var colorTexture = new ImportEntry(package, 0, "ColorTexture") { ClassName = "Texture2D", PackageFile = "NoDonor" };
        var normalTexture = new ImportEntry(package, 0, "NormalTexture") { ClassName = "Texture2D", PackageFile = "NoDonor" };
        var cubeTexture = new ImportEntry(package, 0, "CubeTexture") { ClassName = "TextureCube", PackageFile = "NoDonor" };
        foreach (var texture in new[] { colorTexture, normalTexture, cubeTexture }) package.AddImport(texture);
        var color = Sample("Color", "MaterialExpressionTextureSampleParameter2D", colorTexture);
        var normal = Sample("Normal", "MaterialExpressionTextureSampleParameterNormal", normalTexture);
        var cube = Sample("Cube", "MaterialExpressionTextureSampleParameterCube", cubeTexture);
        var coordinates = package.CreateExport("CubeCoordinates", "MaterialExpressionConstant3Vector", material, indexed: false);
        coordinates.WriteProperty(new FloatProperty(1, "R"));
        cube.WriteProperty(Input("Coordinates", coordinates.UIndex));
        material.WriteProperty(Masked("DiffuseColor", color.UIndex));
        material.WriteProperty(Masked("Normal", normal.UIndex));
        material.WriteProperty(Masked("EmissiveColor", cube.UIndex));
        int metadataReads = 0;
        var compiler = new LE3MaterialShaderCompiler(material,
            resolveTextureImport: _ => throw new AssertFailedException("Metadata-only path must not resolve a donor."),
            getTextureMetadata: entry =>
            {
                metadataReads++;
                return entry == normalTexture ? new(TextureDimension.Texture2D, "TC_NormalmapBC5", new System.Numerics.Vector4(-1, -1, -1, 0)) :
                    new(entry == cubeTexture ? TextureDimension.TextureCube : TextureDimension.Texture2D);
            });
        string source = "Texture2D Texture2D_0:register(t3); SamplerState Texture2D_0Sampler:register(s6); " +
            "Texture2D Texture2D_1:register(t8); SamplerState Texture2D_1Sampler:register(s2); " +
            "TextureCube TextureCube_0:register(t12); SamplerState TextureCube_0Sampler:register(s9); " +
            "float4 Main(float4 p:SV_Position):SV_Target { return TextureCube_0.Sample(TextureCube_0Sampler,p.xyz)" +
            (cubeOnly ? "" : "+Texture2D_0.Sample(Texture2D_0Sampler,p.xy)+Texture2D_1.Sample(Texture2D_1Sampler,p.xy)") + "; }";
        var sources = new LE3ShaderSourceSnapshot(new Dictionary<string, string>
        {
            ["MaterialTemplate.usf"] = CommentTemplate(), ["BasePassPixelShader.usf"] = source,
            ["LocalVertexFactory.usf"] = "// retained for PS job CRC\r\n"
        });
        var translation = compiler.TranslateMaterialHlsl(sources);
        Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
        Assert.AreEqual(3, metadataReads);
        Assert.IsTrue(TryGetKnownPermutationEnvironment(Target(false).ShaderTypeName, "FLocalVertexFactory", out var environment));
        var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), new[] { environment.CreateCandidate() });
        var compiled = compiler.CompileShader(plan.Jobs.Single(), sources);
        Assert.IsTrue(compiled.Success, compiled.Diagnostics);
        var fragment = compiler.BuildShaderCacheFragment(translation, plan, new[] { compiled }, Guid.NewGuid(), "TextureBindingSubset", sources);
        Assert.IsTrue(fragment.Success, string.Join("; ", fragment.Errors));
        CollectionAssert.AreEqual(translation.UniformExpressionTextures.ToArray(), fragment.UniformExpressionTextures.ToArray());
        using var stream = new MemoryStream();
        fragment.Cache.PublicSerialize(new SerializingContainer(stream, package));
        stream.Position = 0;
        var loaded = ShaderCache.Create();
        loaded.PublicSerialize(new SerializingContainer(stream, package, isLoading: true));
        Assert.AreEqual(stream.Length, stream.Position);
        var parameters = ((TBasePassPixelShader<FNullPolicy>)loaded.Shaders.Single().Value).MaterialParameters;
        Assert.AreEqual(cubeOnly ? 0 : 2, parameters.UniformPixel2DShaderResourceParameters.Length);
        Assert.AreEqual(1, parameters.UniformPixelCubeShaderResourceParameters.Length);
        var environmentTexture = parameters.UniformPixelCubeShaderResourceParameters[0];
        if (!cubeOnly)
        {
            var first = parameters.UniformPixel2DShaderResourceParameters[0];
            var second = parameters.UniformPixel2DShaderResourceParameters[1];
            Assert.AreEqual(0, first.Index); Assert.AreEqual(1, second.Index);
            Assert.AreEqual((ushort)1, first.Param.NumResources); Assert.AreEqual((ushort)1, second.Param.NumResources);
            Assert.AreEqual((ushort)3, first.Param.BaseIndex); Assert.AreEqual((ushort)6, first.Param.SamplerIndex);
            Assert.AreEqual((ushort)8, second.Param.BaseIndex); Assert.AreEqual((ushort)2, second.Param.SamplerIndex);
        }
        Assert.AreEqual((ushort)12, environmentTexture.Param.BaseIndex);
        Assert.AreEqual((ushort)9, environmentTexture.Param.SamplerIndex);
        var map = loaded.MaterialShaderMaps.Single().Value;
        Assert.AreEqual(2, map.Uniform2DTextureExpressions.Length);
        Assert.AreEqual(1, map.UniformCubeTextureExpressions.Length);
        Assert.AreEqual(translation.UniformCubeTextureExpressions.Single().TextureIndex, map.UniformCubeTextureExpressions.Single().TextureIndex);
        Assert.AreEqual(3, metadataReads, "Binding/cache serialization may not reread textures.");

        ExportEntry Sample(string name, string className, ImportEntry texture)
        {
            var sample = package.CreateExport(name, className, material, indexed: false);
            sample.WriteProperty(new NameProperty(name, "ParameterName"));
            sample.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
            return sample;
        }
        static StructProperty Masked(string name, int expression) => new("ExpressionInput", new PropertyCollection
        {
            new ObjectProperty(expression, "Expression"), new BoolProperty(true, "Mask"),
            new BoolProperty(true, "MaskR"), new BoolProperty(true, "MaskG"), new BoolProperty(true, "MaskB")
        }, name);
    }

    private static LE3MaterialShaderCompiler CreateCompiler(IMEPackage package, StaticParameterSet staticParameters = null)
    {
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("If", "MaterialExpressionIf", material, indexed: false);
        foreach (string name in new[] { "A", "B", "AGreaterThanB", "AEqualsB", "ALessThanB" })
        {
            var parameter = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
            parameter.WriteProperty(new NameProperty(name, "ParameterName"));
            node.WriteProperty(Input(name, parameter.UIndex));
        }
        material.WriteProperty(Input("Opacity", node.UIndex));
        material.WriteProperty(Input("WorldPositionOffset", node.UIndex));
        return new LE3MaterialShaderCompiler(material, staticParameters);
    }

    private static ShaderCompileTarget Target(bool vertex) => new(
        vertex ? "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy" : "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight",
        vertex ? "BasePassVertexShader" : "BasePassPixelShader", "Main", vertex ? ShaderStage.Vertex : ShaderStage.Pixel,
        CapturedDefaultCompileFlags, "FLocalVertexFactory", "LocalVertexFactory");

    private static string Source(bool vertex, bool gpu = false) => vertex
        ? "cbuffer C : register(b2) { float4 UniformVertexScalars_0; float4 UniformVertexScalars_1; float4x4 LocalToWorld; " +
          (gpu ? "float4 BoneMatrices[3]; " : "") + "float FogMinHeight; };\n" +
          "cbuffer View : register(b1) { float4x4 ViewProjectionMatrix; float4 CameraPosition; };\n" +
          "float4 Main(float4 p : POSITION) : SV_Position { return mul(ViewProjectionMatrix,mul(LocalToWorld,p)) + CameraPosition + UniformVertexScalars_0 + UniformVertexScalars_1 + FogMinHeight" +
          (gpu ? " + BoneMatrices[0]" : "") + "; }"
        : "cbuffer C : register(b3) { float4 UniformPixelScalars_0; float4 UniformPixelScalars_1; float MatInverseGamma; float4 TranslucencyDepthParameters; };\n" +
          "Texture2D SceneDepthTexture : register(t5); SamplerState SceneDepthTextureSampler : register(s6);\n" +
          "float4 Main(float4 p : SV_Position) : SV_Target { return UniformPixelScalars_0 + UniformPixelScalars_1 + MatInverseGamma + TranslucencyDepthParameters + SceneDepthTexture.Sample(SceneDepthTextureSampler,p.xy); }";

    private static string CreateSourceDirectory(ShaderCompileTarget target, string source)
    {
        string path = Path.Combine(Path.GetTempPath(), "LE3ShaderObjectTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, target.SourceFileName + ".usf"), source);
        return path;
    }

    private static StructProperty Input(string name, int expression) =>
        new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression, "Expression") }, name);

    private static string CommentTemplate() => "/*\n%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n*/";
}
