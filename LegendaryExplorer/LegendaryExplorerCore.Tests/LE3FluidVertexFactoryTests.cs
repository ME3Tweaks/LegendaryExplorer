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
public class LE3FluidVertexFactoryTests
{
    private const string VertexType = "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy";
    private static readonly string[] Order = ["LocalToWorld", "LocalToWorldRotDeterminantFlip", "WorldToLocal", "GridSize",
        "TessellationParameters", "Heightmap", "TessellationFactors1", "TessellationFactors2", "TexcoordScaleBias", "SplineParameters"];

    [TestMethod]
    [DataRow("FFluidVertexFactory")]
    [DataRow("FFluidTessellationVertexFactory")]
    public void NativeCacheUsesSourceFilenameNotTypeNameAndRequiresItOnlyWhenRelevant(string name)
    {
        TryGetKnownVertexFactoryRegistration(name, out var factory);
        Assert.IsTrue(factory.UsedWithMaterials); Assert.IsTrue(factory.SupportsStaticLighting);
        Assert.AreEqual(true, factory.SupportsDynamicLighting); Assert.AreEqual(false, factory.SupportsPrecisePreviousPosition);
        Assert.AreEqual("FluidVertexFactory", factory.SourceFileName);
        foreach (string source in new[] { "BasePassVertexShader", "VelocityShader", "TestVelocityShader", "VelocityShaderExtra", "velocityshader" })
        for (int bits = 0; bits < 64; bits++)
        {
            bool Bit(int n) => (bits & (1 << n)) != 0;
            var context = new ShouldCacheMaterialContext(Bit(4), false, Bit(1), false, false, false, false, false)
            { UsedWithFluidSurfaces = Bit(0), UsedWithDecals = Bit(2), IsTerrainMaterial = Bit(3), UsedWithFoliage = Bit(5) };
            Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(name, context, factory.CreateCacheContext(), out bool actual, shaderSourceFileName: source));
            Assert.AreEqual((Bit(0) || Bit(1)) && !Bit(2) && !source.Contains("VelocityShader", StringComparison.Ordinal), actual, source + "/" + bits);
            bool resolved = TryEvaluateKnownVertexFactoryShouldCache(name, context, factory.CreateCacheContext(), out actual);
            Assert.AreEqual(!(Bit(0) || Bit(1)) || Bit(2), resolved); Assert.IsFalse(actual);
        }
        var eligible = new ShouldCacheMaterialContext(false, false, false, false, false, false, false, false) { UsedWithFluidSurfaces = true };
        foreach (string type in new[] { "FVelocityVertexShader", "FVelocityPixelShader" })
        {
            Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(name, eligible, factory.CreateCacheContext(), out bool actual, shaderTypeName: type));
            Assert.IsFalse(actual, "Velocity TYPE names resolve to the VelocityShader SOURCE.");
        }
        Assert.IsFalse(TryEvaluateKnownVertexFactoryShouldCache(name, eligible, factory.CreateCacheContext(), out _, shaderTypeName: "Unregistered"));
        Assert.IsFalse(TryEvaluateKnownVertexFactoryShouldCache(name, eligible, factory.CreateCacheContext(), out _,
            shaderTypeName: VertexType, shaderSourceFileName: "VelocityShader"), "Conflicting known registration metadata is unresolved.");
    }

    [TestMethod]
    [DataRow("FFluidVertexFactory")]
    [DataRow("FFluidTessellationVertexFactory")]
    public void Sm5NativeEnvironmentUsesZeroTessellationAndNoInventedFallback(string name)
    {
        foreach (var type in GetKnownShaderTypeRegistrations().Where(t => t.IsMeshMaterial))
        {
            TryGetKnownPermutationEnvironment(type.Name, name, out var environment);
            var macro = environment.MacroDefinitions.Single(m => m.Name == "XBOXTESSELLATION");
            Assert.AreEqual("0", macro.Definition); Assert.AreEqual(ShaderEnvironmentEvidence.LE3RegistrationCallback, macro.Evidence);
            Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.CompileFlagsEvidence);
            TryGetKnownPermutationEnvironment(type.Name, "FLocalVertexFactory", out var local);
            CollectionAssert.AreEquivalent(local.MacroDefinitions.ToArray(), environment.MacroDefinitions.Where(m => m.Name != "XBOXTESSELLATION").ToArray());
        }
    }

    [TestMethod]
    [DataRow("FFluidVertexFactory")]
    [DataRow("FFluidTessellationVertexFactory")]
    public void PlannerPassesShaderIdentityAndStoredOrEffectiveFluidUsage(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FluidMetadata.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var compiler = new LE3MaterialShaderCompiler(material);
        Assert.IsFalse(compiler.BuildShouldCacheMaterialContext().UsedWithFluidSurfaces);
        var supplied = new PropertyCollection { new BoolProperty(true, "bUsedWithFluidSurfaces") };
        Assert.IsTrue(compiler.BuildShouldCacheMaterialContext(supplied).UsedWithFluidSurfaces);
        TryGetKnownPermutationEnvironment(VertexType, name, out var environment);
        foreach (bool usage in new[] { false, true })
        {
            material.WriteProperty(new BoolProperty(usage, "bUsedWithFluidSurfaces"));
            Assert.AreEqual(usage, compiler.BuildShouldCacheMaterialContext().UsedWithFluidSurfaces);
            var translation = compiler.TranslateMaterialHlsl(Template());
            var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), [environment.CreateCandidate()]);
            Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(usage ? 1 : 0, plan.Jobs.Count);
            if (usage) Assert.AreSame(compiler.CompilationInput, plan.Jobs.Single().CompilationInput);
            else Assert.AreEqual(ShaderCompileGate.VertexFactoryShouldCache, plan.Decisions.Single().Gate);
            Assert.AreEqual(0, translation.UniformExpressionTextures.Count);
        }
    }

    [TestMethod]
    [DataRow("FFluidVertexFactory")]
    [DataRow("FFluidTessellationVertexFactory")]
    public void GridSizeIsRequiredEvenWithAllOptionalConstantAndResourceAllocations(string name)
    {
        var fields = Bindings(true).Where(f => f.Name != "GridSize").ToArray();
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => new LE3ShaderParameterBinder(fields).BindVertexFactoryParameters(name));
        StringAssert.Contains(error.Message, "GridSize");
    }

    [TestMethod]
    [DataRow("FFluidVertexFactory")]
    [DataRow("FFluidTessellationVertexFactory")]
    public void PlannerExcludesVelocitySourceAfterShaderTypeAndResourceGatesPass(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FluidVelocity.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var context = new ShouldCacheMaterialContext(false, false, false, false, false, false, true, false)
            { UsedWithFluidSurfaces = true, BlendMode = EBlendMode.BLEND_Opaque, IsTwoSided = true };
        var candidates = new[] { VertexType, "FVelocityVertexShader", "FVelocityPixelShader" }.Select(type =>
        {
            TryGetKnownPermutationEnvironment(type, name, out var environment);
            Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(type, context, name, environment.CreateCandidate().VertexFactoryContext, out bool caches));
            Assert.IsTrue(caches, type); return environment.CreateCandidate();
        }).ToArray();
        int resourceCalls = 0;
        var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(Template()), MaterialResource.Create(), candidates,
            context, materialResourceShouldCache: (_, _) => { resourceCalls++; return true; });
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(3, resourceCalls);
        Assert.AreEqual(1, plan.Jobs.Count); Assert.AreEqual(VertexType, plan.Jobs.Single().Target.ShaderTypeName);
        Assert.IsTrue(plan.Decisions.Where(d => d.Target.ShaderTypeName.StartsWith("FVelocity", StringComparison.Ordinal))
            .All(d => d.Disposition == ShaderCompileDisposition.Excluded && d.Gate == ShaderCompileGate.VertexFactoryShouldCache));
    }

    [TestMethod]
    [DataRow("FFluidVertexFactory", false)]
    [DataRow("FFluidVertexFactory", true)]
    [DataRow("FFluidTessellationVertexFactory", false)]
    [DataRow("FFluidTessellationVertexFactory", true)]
    public void NativeArchivePlacesHeightmapBeforeFactorsNotAtItsInMemoryOffset(string name, bool optional)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FluidArchive.pcc", MEGame.LE3);
        var fields = Bindings(optional); var binder = new LE3ShaderParameterBinder(fields);
        var reference = new FVertexFactoryParameterRef { VertexFactoryType = name, Parameters = binder.BindVertexFactoryParameters(name) };
        Assert.AreEqual(0, binder.UnboundParameters.Count);
        using var stream = new MemoryStream(); reference.Serialize(new SerializingContainer(stream, package));
        Assert.AreEqual(72L, stream.Length); stream.Position = 12;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        foreach (string fieldName in Order)
        {
            var field = fields.SingleOrDefault(f => f.Name == fieldName);
            Assert.AreEqual(field?.BaseIndex ?? 0, reader.ReadUInt16()); Assert.AreEqual(field?.Size ?? 0, reader.ReadUInt16());
            Assert.AreEqual(fieldName == "Heightmap" ? field?.SamplerIndex ?? 0 : field?.BufferIndex ?? 0, reader.ReadUInt16());
        }
        stream.Position = 0; var copy = new FVertexFactoryParameterRef(); copy.Serialize(new SerializingContainer(stream, package, true));
        Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(name, copy.VertexFactoryType.Name);
        Assert.IsInstanceOfType<FFluidTessellationVertexFactoryShaderParameters>(copy.Parameters);
        Assert.AreEqual(optional, ((FFluidTessellationVertexFactoryShaderParameters)copy.Parameters).Heightmap.IsBound());
    }

    [TestMethod]
    [DataRow("FFluidVertexFactory", false)]
    [DataRow("FFluidVertexFactory", true)]
    [DataRow("FFluidTessellationVertexFactory", false)]
    [DataRow("FFluidTessellationVertexFactory", true)]
    public void TypedFluidVertexObjectsRequireGridNotLocalAndPreserveArchive(string name, bool optional)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FluidTyped.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        TryGetKnownPermutationEnvironment(VertexType, name, out var environment);
        string[] constants = optional ? Order.Where(f => f != "Heightmap").ToArray() : ["GridSize"];
        string body = string.Concat(constants.Select(f => "float4 " + f + "; ")) +
            (optional ? "Texture2D Heightmap; SamplerState HeightmapSampler; " : "") +
            "float4 Main(float4 p:POSITION):SV_Position { return p" + string.Concat(constants.Select(f => " + " + f)) +
            (optional ? " + Heightmap.SampleLevel(HeightmapSampler,p.xy,0)" : "") + "; }";
        var snapshot = new LE3ShaderSourceSnapshot(new Dictionary<string, string> { ["MaterialTemplate.usf"] = Template(),
            [environment.Target.SourceFileName + ".usf"] = body, ["FluidVertexFactory.usf"] = "// fixture" });
        var translation = compiler.TranslateMaterialHlsl(snapshot);
        var compiled = compiler.CompileShader(compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(),
            environment.Target, environment.CreateCandidate().EnvironmentMacros), snapshot);
        Assert.IsTrue(compiled.Success, compiled.Diagnostics);
        var result = compiler.ConstructKnownShader(translation, compiled); Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(0, result.UnboundParameters.Count); Assert.AreSame(compiler.CompilationInput, compiled.CompilationInput);
        Shader shader = result.Shader;
        using var stream = new MemoryStream(); new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0;
        Shader copy = null; new SerializingContainer(stream, package, true).Serialize(ref copy); Assert.AreEqual(stream.Length, stream.Position);
        var reference = (FVertexFactoryParameterRef)copy.GetType().GetField("VertexFactoryParameters").GetValue(copy);
        Assert.IsInstanceOfType<FFluidTessellationVertexFactoryShaderParameters>(reference.Parameters);
        Assert.AreEqual(optional, ((FFluidTessellationVertexFactoryShaderParameters)reference.Parameters).Heightmap.IsBound());
        CollectionAssert.AreEqual(shader.ShaderByteCode, copy.ShaderByteCode);
    }

    private static ShaderParameterBinding[] Bindings(bool optional) => Order.Where(f => optional || f == "GridSize").Reverse().Select((f, i) =>
        new ShaderParameterBinding(f, f == "Heightmap" ? ShaderParameterBindingKind.Resource : ShaderParameterBindingKind.Constant,
            (ushort)(i + 1), (ushort)(i * 16 + 3), (ushort)(f == "Heightmap" ? 1 : 16 + i), f == "Heightmap" ? (ushort)2 : null)).ToArray();
    private static string Template() => "/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n*/";
}
