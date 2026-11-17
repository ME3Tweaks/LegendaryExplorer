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
public class LE3LensFlareVertexFactoryTests
{
    private const string Factory = "FLensFlareVertexFactory";
    private const string VertexType = "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy";

    [TestMethod]
    public void NativeCapabilitiesAndCacheTruthTableDoNotInheritLocalTerrainGate()
    {
        Assert.IsTrue(TryGetKnownVertexFactoryRegistration(Factory, out var factory));
        Assert.AreEqual("LensFlareVertexFactory", factory.SourceFileName);
        Assert.IsTrue(factory.UsedWithMaterials); Assert.IsFalse(factory.SupportsStaticLighting);
        Assert.AreEqual(false, factory.SupportsDynamicLighting); Assert.AreEqual(false, factory.SupportsPrecisePreviousPosition);
        Assert.AreEqual(factory, GetObservedVertexFactoryRegistrations().Single(v => v.Name == Factory));
        for (int bits = 0; bits < 128; bits++)
        {
            bool Bit(int n) => (bits & (1 << n)) != 0;
            var context = new ShouldCacheMaterialContext(Bit(4), false, Bit(1), false, false, false, false, false)
            { UsedWithLensFlare = Bit(0), UsedWithDecals = Bit(2), IsTerrainMaterial = Bit(3),
                UsedWithBeamTrails = Bit(5), UsesDynamicParameter = Bit(6) };
            Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(Factory, context, factory.CreateCacheContext(), out bool actual));
            Assert.AreEqual((Bit(0) || Bit(1)) && !Bit(2), actual, "bits=" + bits);
        }
    }

    [TestMethod]
    public void NativeEnvironmentAndReconstructedFallbackRemainSeparatelyAttributed()
    {
        foreach (var type in GetKnownShaderTypeRegistrations().Where(t => t.IsMeshMaterial))
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(type.Name, Factory, out var environment));
            foreach (string key in new[] { "USE_LENSFLARE", "USE_OCCLUSION_PERCENTAGE" })
            {
                var macro = environment.MacroDefinitions.Single(m => m.Name == key);
                Assert.AreEqual("1", macro.Definition); Assert.AreEqual(ShaderEnvironmentEvidence.LE3RegistrationCallback, macro.Evidence);
            }
            Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.CompileFlagsEvidence);
            Assert.IsFalse(environment.CreateCandidate().EnvironmentMacros.ContainsKey("QUAT_SKINNING"));
        }
        TryGetKnownPermutationEnvironment(VertexType, Factory, out var noLight);
        Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction,
            noLight.MacroDefinitions.Single(m => m.Name == "SIMPLE_VERTEX_LIGHTMAP").Evidence);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PlannerReadsStoredOrResolvedMetadataWithoutTexturePayload(bool usage)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3LensMetadata.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var compiler = new LE3MaterialShaderCompiler(material);
        Assert.IsFalse(compiler.BuildShouldCacheMaterialContext().UsedWithLensFlare);
        var properties = new PropertyCollection { new BoolProperty(usage, "bUsedWithLensFlare") };
        Assert.AreEqual(usage, compiler.BuildShouldCacheMaterialContext(properties).UsedWithLensFlare);
        material.WriteProperties(properties);
        Assert.AreEqual(usage, compiler.BuildShouldCacheMaterialContext().UsedWithLensFlare);
        TryGetKnownPermutationEnvironment(VertexType, Factory, out var environment);
        var translation = compiler.TranslateMaterialHlsl(Template());
        var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), [environment.CreateCandidate()]);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(usage ? 1 : 0, plan.Jobs.Count);
        if (usage) Assert.AreSame(compiler.CompilationInput, plan.Jobs.Single().CompilationInput);
        else Assert.AreEqual(ShaderCompileGate.VertexFactoryShouldCache, plan.Decisions.Single().Gate);
        Assert.AreEqual(0, translation.UniformExpressionTextures.Count);
    }

    [TestMethod]
    public void MissingTransformFailsEvenWhenBothOptionalCamerasArePresent()
    {
        var fields = new[] { "CameraRight", "CameraUp" }.Select((name, i) => Constant(name, (ushort)(i * 16))).ToArray();
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => new LE3ShaderParameterBinder(fields).BindVertexFactoryParameters(Factory));
        StringAssert.Contains(error.Message, "LocalToWorld");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NativeThreeRecordArchiveOrderIsNotTheLocalFieldOrder(bool cameras)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3LensArchive.pcc", MEGame.LE3);
        string[] order = ["CameraRight", "CameraUp", "LocalToWorld"];
        var fields = order.Where(f => cameras || f == "LocalToWorld").Reverse().Select((name, i) =>
            new ShaderParameterBinding(name, ShaderParameterBindingKind.Constant, (ushort)(i + 1), (ushort)(i * 32 + 3), (ushort)(16 + i), null)).ToArray();
        var binder = new LE3ShaderParameterBinder(fields);
        var reference = new FVertexFactoryParameterRef { VertexFactoryType = Factory, Parameters = binder.BindVertexFactoryParameters(Factory) };
        Assert.AreEqual(0, binder.UnboundParameters.Count);
        using var stream = new MemoryStream(); reference.Serialize(new SerializingContainer(stream, package));
        Assert.AreEqual(30L, stream.Length); stream.Position = 12;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        foreach (string name in order)
        {
            var field = fields.SingleOrDefault(f => f.Name == name);
            Assert.AreEqual(field?.BaseIndex ?? 0, reader.ReadUInt16()); Assert.AreEqual(field?.Size ?? 0, reader.ReadUInt16());
            Assert.AreEqual(field?.BufferIndex ?? 0, reader.ReadUInt16());
        }
        stream.Position = 0; var copy = new FVertexFactoryParameterRef(); copy.Serialize(new SerializingContainer(stream, package, true));
        Assert.AreEqual(stream.Length, stream.Position); Assert.IsInstanceOfType<FLensFlareVertexFactoryShaderParameters>(copy.Parameters);
        Assert.AreEqual(Factory, copy.VertexFactoryType.Name);
    }

    [TestMethod]
    [DataRow("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", false)]
    [DataRow("TDepthOnlyVertexShader<0>", true)]
    [DataRow("TAOMeshVertexShader<0>", false)]
    public void CompiledTypedFactoryConstructionPreservesArchiveAndInputProvenance(string type, bool cameras)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3LensTyped.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        TryGetKnownPermutationEnvironment(type, Factory, out var environment);
        string body = "float4x4 LocalToWorld; " + (cameras ? "float4 CameraRight, CameraUp; " : "") +
            $"float4 {environment.Target.EntryPoint}(float4 p:POSITION):SV_Position {{ return mul(LocalToWorld,p)" + (cameras ? " + CameraRight + CameraUp" : "") + "; }";
        var snapshot = new LE3ShaderSourceSnapshot(new Dictionary<string, string> { ["MaterialTemplate.usf"] = Template(),
            [environment.Target.SourceFileName + ".usf"] = body, ["LensFlareVertexFactory.usf"] = "// fixture" });
        var translation = compiler.TranslateMaterialHlsl(snapshot);
        var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
        var compiled = compiler.CompileShader(job, snapshot); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
        var result = compiler.ConstructKnownShader(translation, compiled); Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(0, result.UnboundParameters.Count); Assert.AreSame(compiler.CompilationInput, compiled.CompilationInput);
        Shader shader = result.Shader;
        using var stream = new MemoryStream(); new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0;
        Shader copy = null; new SerializingContainer(stream, package, true).Serialize(ref copy); Assert.AreEqual(stream.Length, stream.Position);
        var reference = (FVertexFactoryParameterRef)copy.GetType().GetField("VertexFactoryParameters").GetValue(copy);
        Assert.IsInstanceOfType<FLensFlareVertexFactoryShaderParameters>(reference.Parameters);
        Assert.AreEqual(cameras ? (ushort)16 : (ushort)0, ((FLensFlareVertexFactoryShaderParameters)reference.Parameters).CameraRight.NumBytes);
        CollectionAssert.AreEqual(shader.ShaderByteCode, copy.ShaderByteCode);
    }

    [TestMethod]
    public void ObservedBioSpuRegistrationIsAnAlwaysFalseGateNotACompilableFactory()
    {
        const string bio = "FBioSPUSkinVertexFactory";
        var factory = GetObservedVertexFactoryRegistrations().Single(v => v.Name == bio);
        Assert.IsTrue(factory.UsedWithMaterials); Assert.IsFalse(factory.SupportsStaticLighting);
        Assert.AreEqual(true, factory.SupportsDynamicLighting);
        Assert.IsFalse(TryGetKnownVertexFactoryRegistration(bio, out _));
        Assert.IsFalse(TryGetKnownPermutationEnvironment(VertexType, bio, out _));
        Assert.ThrowsExactly<InvalidOperationException>(() => new LE3ShaderParameterBinder([]).BindVertexFactoryParameters(bio));
        for (int bits = 0; bits < 32; bits++)
        {
            bool Bit(int n) => (bits & (1 << n)) != 0;
            var context = new ShouldCacheMaterialContext(Bit(0), false, Bit(1), false, false, false, false, false)
            { IsTerrainMaterial = Bit(2), UsedWithSkeletalMesh = Bit(3), UsedWithDecals = Bit(4) };
            Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(bio, context, factory.CreateCacheContext(), out bool actual));
            Assert.IsFalse(actual);
        }
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3BioSpuExclusion.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        TryGetKnownPermutationEnvironment(VertexType, "FLocalVertexFactory", out var local);
        var candidate = local.CreateCandidate() with { Target = local.Target with { VertexFactoryTypeName = bio,
            VertexFactoryShaderFileName = factory.SourceFileName }, VertexFactoryContext = factory.CreateCacheContext() };
        var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(Template()), MaterialResource.Create(), [candidate]);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(0, plan.Jobs.Count);
        Assert.AreEqual(ShaderCompileGate.VertexFactoryShouldCache, plan.Decisions.Single().Gate);
        Assert.IsFalse(ComparePlannedPermutations(plan, Array.Empty<ShaderPermutationIdentity>()).IsCompleteShaderSet);
    }

    private static ShaderParameterBinding Constant(string name, ushort index) => new(name, ShaderParameterBindingKind.Constant, 0, index, 16, null);
    private static string Template() => "/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n*/";
}
