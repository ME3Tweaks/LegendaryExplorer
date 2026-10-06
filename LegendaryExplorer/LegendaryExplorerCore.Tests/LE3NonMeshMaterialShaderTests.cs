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
public class LE3NonMeshMaterialShaderTests
{
    private static readonly string[] Names = ["FLightFunctionVertexShader", "FLightFunctionPixelShader",
        "FRadialBlurVertexShader", "FRadialBlurPixelShader", "FRadialBlurVelocityPixelShader"];

    [TestMethod]
    [DataRow("FLightFunctionVertexShader")]
    [DataRow("FLightFunctionPixelShader")]
    [DataRow("FRadialBlurVertexShader")]
    [DataRow("FRadialBlurPixelShader")]
    [DataRow("FRadialBlurVelocityPixelShader")]
    public void NativeMaterialClassificationHasNoFactoryOrFactoryMacros(string name)
    {
        Assert.IsTrue(TryGetKnownShaderTypeRegistration(name, out var registration));
        Assert.IsFalse(registration.IsMeshMaterial);
        Assert.IsTrue(TryGetKnownPermutationEnvironment(name, null, out var environment));
        var candidate = environment.CreateCandidate();
        Assert.IsNull(candidate.VertexFactoryContext);
        Assert.IsNull(candidate.Target.VertexFactoryTypeName); Assert.IsNull(candidate.Target.VertexFactoryShaderFileName);
        Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.CompileFlagsEvidence);
        Assert.AreEqual(0x9000u, candidate.Target.CompileFlags);
        Assert.AreEqual(5, candidate.EnvironmentMacros.Count);
        Assert.AreEqual(registration.EntryPoint, candidate.Target.EntryPoint);
        foreach (var factory in GetKnownVertexFactoryRegistrations())
            Assert.IsFalse(TryGetKnownPermutationEnvironment(name, factory.Name, out _));
        Assert.IsFalse(TryGetKnownPermutationEnvironment(name, "", out _));
    }

    [TestMethod]
    [DataRow("FLightFunctionVertexShader")]
    [DataRow("FLightFunctionPixelShader")]
    [DataRow("FRadialBlurVertexShader")]
    [DataRow("FRadialBlurPixelShader")]
    [DataRow("FRadialBlurVelocityPixelShader")]
    public void CacheTruthTableDoesNotInventFactoryPlatformBlendOrSpecialLightFunctionGates(string name)
    {
        for (int bits = 0; bits < 256; bits++)
        {
            bool Bit(int n) => (bits & (1 << n)) != 0;
            var context = new ShouldCacheMaterialContext(Bit(3), Bit(4), Bit(0), false, false, false, false, Bit(5))
            {
                IsLightFunction = Bit(1), UsedWithRadialBlur = Bit(2), UsedWithDecals = Bit(6), IsTerrainMaterial = Bit(7)
            };
            Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(name, context, out bool actual));
            Assert.AreEqual(name.StartsWith("FLightFunction", StringComparison.Ordinal) ? Bit(1) : Bit(2) || Bit(0), actual);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OrdinaryContextUsesStoredAndEffectiveNonMeshUsageMetadata(bool usage)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NonMeshMetadata.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var compiler = new LE3MaterialShaderCompiler(material);
        var context = compiler.BuildShouldCacheMaterialContext();
        Assert.IsFalse(context.IsLightFunction); Assert.IsFalse(context.UsedWithRadialBlur);
        var properties = new PropertyCollection { new BoolProperty(usage, "bUsedAsLightFunction"), new BoolProperty(usage, "bUsedWithRadialBlur") };
        context = compiler.BuildShouldCacheMaterialContext(properties);
        Assert.AreEqual(usage, context.IsLightFunction); Assert.AreEqual(usage, context.UsedWithRadialBlur);
        material.WriteProperties(properties);
        context = compiler.BuildShouldCacheMaterialContext();
        Assert.AreEqual(usage, context.IsLightFunction); Assert.AreEqual(usage, context.UsedWithRadialBlur);
    }

    [TestMethod]
    [DataRow("FLightFunctionVertexShader")]
    [DataRow("FLightFunctionPixelShader")]
    [DataRow("FRadialBlurVertexShader")]
    [DataRow("FRadialBlurPixelShader")]
    [DataRow("FRadialBlurVelocityPixelShader")]
    public void OptionalFieldsConstructAndRoundTripWithoutAnyFactoryPrefix(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NonMeshArchives.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        Assert.IsTrue(TryGetKnownPermutationEnvironment(name, null, out var environment));
        foreach (bool present in new[] { false, true })
        {
            var snapshot = Sources([environment.Target], present);
            var translation = compiler.TranslateMaterialHlsl(snapshot);
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
            var compiled = compiler.CompileShader(job, snapshot); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
            var result = compiler.ConstructKnownShader(translation, compiled);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(0, result.UnboundParameters.Count);
            Shader shader = result.Shader;
            Assert.IsNull(shader.VertexFactoryType);
            using var stream = new MemoryStream();
            new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0; Shader copy = null;
            new SerializingContainer(stream, package, true).Serialize(ref copy);
            Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(shader.GetType(), copy.GetType());
            Assert.AreEqual(name, copy.ShaderType.Name); Assert.AreEqual(shader.ParameterMapCRC, copy.ParameterMapCRC);
            CollectionAssert.AreEqual(shader.ShaderByteCode, copy.ShaderByteCode);
            Assert.IsNull(copy.VertexFactoryType);
            if (copy is FLightFunctionPixelShader light) Assert.AreEqual(present ? (ushort)16 : (ushort)0, light.ScreenToLight.NumBytes);
            if (copy is FRadialBlurVertexShader radialVertex) Assert.AreEqual(present ? (ushort)16 : (ushort)0, radialVertex.WorldCenterPos.NumBytes);
            if (copy is FRadialBlurPixelShader radialPixel) Assert.AreEqual(present ? (ushort)16 : (ushort)0, radialPixel.RadialBlurOpacity.NumBytes);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MaterialMapGroupsNonMeshDirectlyAndIncludesFactoryCrcOnlyForMeshJobs(bool mixed)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MixedMaterialMaps.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(true, "bUsedAsLightFunction"));
        material.WriteProperty(new BoolProperty(true, "bUsedWithRadialBlur"));
        var compiler = new LE3MaterialShaderCompiler(material);
        var candidates = Names.Select(name => { TryGetKnownPermutationEnvironment(name, null, out var env); return env.CreateCandidate(); }).ToList();
        if (mixed)
        {
            TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", "FLocalVertexFactory", out var env);
            candidates.Add(env.CreateCandidate());
        }
        var sources = Sources(candidates.Select(c => c.Target).ToArray(), true);
        var translation = compiler.TranslateMaterialHlsl(sources);
        var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), candidates);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(candidates.Count, plan.Jobs.Count);
        var compiled = plan.Jobs.Select(job => compiler.CompileShader(job, sources)).ToArray();
        foreach (var result in compiled) Assert.IsTrue(result.Success, result.Diagnostics);
        var fragment = compiler.BuildShaderCacheFragment(translation, plan, compiled, Guid.NewGuid(), "NonMeshSubset", sources);
        Assert.IsTrue(fragment.Success, string.Join("; ", fragment.Errors)); Assert.IsFalse(fragment.IsCompleteShaderSet);
        Assert.AreEqual(5, fragment.Cache.MaterialShaderMaps.Single().Value.Shaders.Count);
        Assert.AreEqual(mixed ? 1 : 0, fragment.Cache.MaterialShaderMaps.Single().Value.MeshShaderMaps.Length);
        Assert.AreEqual(mixed ? 1 : 0, fragment.Cache.VertexFactoryTypeCRCMap.Count);
        Assert.AreEqual(candidates.Count, fragment.Cache.Shaders.Count);
        using var stream = new MemoryStream(); fragment.Cache.PublicSerialize(new SerializingContainer(stream, package)); stream.Position = 0;
        var copy = ShaderCache.Create(); copy.PublicSerialize(new SerializingContainer(stream, package, isLoading: true));
        Assert.AreEqual(stream.Length, stream.Position);
        Assert.AreEqual(5, copy.MaterialShaderMaps.Single().Value.Shaders.Count);
        foreach (var reference in copy.MaterialShaderMaps.Single().Value.Shaders)
            Assert.AreEqual(reference.Key, copy.Shaders.Single(pair => pair.Key == reference.Value.Id).Value.ShaderType);
        var missing = compiler.BuildShaderCacheFragment(translation, plan, compiled.Skip(1).ToArray(), Guid.NewGuid(), "MissingJob", sources);
        Assert.IsFalse(missing.Success); Assert.IsNull(missing.Cache);
    }

    private static string Template() => "/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n*/";

    [TestMethod]
    [DataRow("FLightFunctionPixelShader")]
    [DataRow("FRadialBlurPixelShader")]
    [DataRow("FRadialBlurVelocityPixelShader")]
    public void PixelConstructorsBindSceneResourcesAndPackedMaterialUniformsWithoutTextureExports(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NonMeshUniforms.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var parameter = package.CreateExport("Scalar", "MaterialExpressionScalarParameter", material, indexed: false);
        parameter.WriteProperty(new NameProperty("LightStrength", "ParameterName"));
        parameter.WriteProperty(new FloatProperty(0.5f, "DefaultValue"));
        material.WriteProperty(new StructProperty("ColorMaterialInput", new PropertyCollection
            { new ObjectProperty(parameter.UIndex, "Expression") }, "EmissiveColor"));
        var compiler = new LE3MaterialShaderCompiler(material);
        TryGetKnownPermutationEnvironment(name, null, out var environment);
        var source = "float4 UniformPixelScalars_0; Texture2D SceneColorTexture; SamplerState SceneColorTextureSampler; " +
            "Texture2D SceneDepthTexture; SamplerState SceneDepthTextureSampler; " +
            $"float4 {environment.Target.EntryPoint}(float4 p:SV_Position):SV_Target {{ return UniformPixelScalars_0 + " +
            "SceneColorTexture.Sample(SceneColorTextureSampler,p.xy) + SceneDepthTexture.Sample(SceneDepthTextureSampler,p.xy); }";
        var sources = new LE3ShaderSourceSnapshot(new Dictionary<string, string>
            { ["MaterialTemplate.usf"] = Template(), [environment.Target.SourceFileName + ".usf"] = source });
        var translation = compiler.TranslateMaterialHlsl(sources);
        Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
        Assert.AreEqual(1, translation.UniformPixelScalarExpressions.Count);
        Assert.AreEqual(0, translation.UniformExpressionTextures.Count);
        var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
        var compiled = compiler.CompileShader(job, sources); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
        var result = compiler.ConstructKnownShader(translation, compiled);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(0, result.UnboundParameters.Count);
        Shader shader = result.Shader;
        using var stream = new MemoryStream(); new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0; Shader copy = null;
        new SerializingContainer(stream, package, true).Serialize(ref copy);
        Assert.AreEqual(stream.Length, stream.Position);
        var scene = copy is FLightFunctionPixelShader light ? light.SceneTextureParameters : ((FRadialBlurPixelShader)copy).SceneTextureParameters;
        var uniforms = copy is FLightFunctionPixelShader lightPixel ? lightPixel.MaterialParameters : ((FRadialBlurPixelShader)copy).MaterialParameters;
        Assert.AreEqual((ushort)1, scene.SceneColorTexture.NumResources);
        Assert.AreEqual((ushort)1, scene.SceneDepthTexture.NumResources);
        Assert.AreEqual(0, uniforms.UniformPixelScalarShaderParameters.Single().Index);
        Assert.AreEqual((ushort)16, uniforms.UniformPixelScalarShaderParameters.Single().Param.NumBytes);
        Assert.IsNull(copy.VertexFactoryType);
    }

    [TestMethod]
    [DataRow("FLightFunctionVertexShader")]
    [DataRow("FLightFunctionPixelShader")]
    [DataRow("FRadialBlurVertexShader")]
    [DataRow("FRadialBlurPixelShader")]
    [DataRow("FRadialBlurVelocityPixelShader")]
    public void NonMeshResourceGatePreservesInputAndWithholdsUnresolvedPlans(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NonMeshResourceGate.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var compiler = new LE3MaterialShaderCompiler(material);
        var translation = compiler.TranslateMaterialHlsl(Template());
        TryGetKnownPermutationEnvironment(name, null, out var environment);
        var context = new ShouldCacheMaterialContext(false, false, false, false, false, false, false, false)
            { IsLightFunction = true, UsedWithRadialBlur = true };
        foreach (bool? outcome in new bool?[] { null, false, true })
        {
            int calls = 0;
            var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), [environment.CreateCandidate()], context,
                materialResourceShouldCache: (input, target) =>
                {
                    calls++; Assert.AreSame(compiler.CompilationInput, input); Assert.AreEqual(environment.Target, target);
                    Assert.IsNull(target.VertexFactoryTypeName); return outcome;
                });
            Assert.AreEqual(1, calls);
            Assert.AreEqual(outcome != null, plan.Success);
            Assert.AreEqual(outcome == true ? 1 : 0, plan.Jobs.Count);
            Assert.AreEqual(outcome == true ? ShaderCompileGate.CompilationEnvironment : ShaderCompileGate.MaterialResourceShouldCache,
                plan.Decisions.Single().Gate);
            Assert.AreEqual(outcome == true ? ShaderCompileDisposition.Eligible : outcome == false ? ShaderCompileDisposition.Excluded :
                ShaderCompileDisposition.Unresolved, plan.Decisions.Single().Disposition);
        }
    }

    [TestMethod]
    [DataRow("factory")]
    [DataRow("factory-context")]
    [DataRow("source")]
    [DataRow("entry")]
    [DataRow("stage")]
    public void NonMeshPlanRejectsConflictingRegistrationMetadataAndWithholdsOtherwiseValidJobs(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3NonMeshInvalidTarget.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var translation = compiler.TranslateMaterialHlsl(Template());
        TryGetKnownPermutationEnvironment("FLightFunctionPixelShader", null, out var environment);
        var good = environment.CreateCandidate();
        var bad = good with { Target = good.Target with { } };
        bad = shape switch
        {
            "factory" => bad with { Target = good.Target with { VertexFactoryTypeName = "FLocalVertexFactory", VertexFactoryShaderFileName = "LocalVertexFactory" } },
            "factory-context" => bad with { VertexFactoryContext = new(true) },
            "source" => bad with { Target = good.Target with { SourceFileName = "OtherSource" } },
            "entry" => bad with { Target = good.Target with { EntryPoint = "OtherEntry" } },
            _ => bad with { Target = good.Target with { Stage = ShaderStage.Vertex } }
        };
        TryGetKnownPermutationEnvironment("FLightFunctionVertexShader", null, out var validOther);
        var context = new ShouldCacheMaterialContext(false, false, false, false, false, false, false, false) { IsLightFunction = true };
        var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), [validOther.CreateCandidate(), bad], context);
        Assert.IsFalse(plan.Success); Assert.AreEqual(0, plan.Jobs.Count);
        Assert.AreEqual(ShaderCompileDisposition.Eligible, plan.Decisions[0].Disposition);
        Assert.AreEqual(ShaderCompileDisposition.Unresolved, plan.Decisions[1].Disposition);
    }

    private static LE3ShaderSourceSnapshot Sources(IReadOnlyList<ShaderCompileTarget> targets, bool present)
    {
        var files = new Dictionary<string, string> { ["MaterialTemplate.usf"] = Template() };
        var declarations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            string[] fields = target.ShaderTypeName switch
            {
                "FLightFunctionPixelShader" => ["ScreenToLight", "MinZ_MaxZRatio", "ScreenPositionScaleBias"],
                "FRadialBlurPixelShader" or "FRadialBlurVelocityPixelShader" => ["RadialBlurScale", "RadialBlurFalloffExp", "RadialBlurOpacity", "MinZ_MaxZRatio", "ScreenPositionScaleBias"],
                "FRadialBlurVertexShader" => ["WorldCenterPos"], _ => []
            };
            string source = present ? string.Concat(fields.Where(f => declarations.Add(target.SourceFileName + "/" + f))
                .Select(f => "float4 " + f + ";\n")) : "";
            source += $"float4 {target.EntryPoint}(float4 p:{(target.Stage == ShaderStage.Vertex ? "POSITION" : "SV_Position")}):" +
                (target.Stage == ShaderStage.Vertex ? "SV_Position" : "SV_Target") + " { return p" +
                (present ? string.Concat(fields.Select(f => " + " + f)) : "") + "; }\n";
            files[target.SourceFileName + ".usf"] = files.GetValueOrDefault(target.SourceFileName + ".usf", "") + source;
            if (target.VertexFactoryShaderFileName != null) files[target.VertexFactoryShaderFileName + ".usf"] = "// fixture VF source identity";
        }
        return new LE3ShaderSourceSnapshot(files);
    }
}
