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
public class LE3FoliageVertexFactoryTests
{
    private const string Factory = "FFoliageVertexFactory";
    private const string VertexType = "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy";

    [TestMethod]
    public void NativeFoliageGateHasNoTerrainOrInstancedMeshUsageSubstitution()
    {
        Assert.IsTrue(TryGetKnownVertexFactoryRegistration(Factory, out var factory));
        Assert.IsTrue(factory.UsedWithMaterials); Assert.IsTrue(factory.SupportsStaticLighting);
        Assert.AreEqual(true, factory.SupportsDynamicLighting); Assert.AreEqual(false, factory.SupportsPrecisePreviousPosition);
        Assert.AreEqual("FoliageVertexFactory", factory.SourceFileName);
        for (int bits = 0; bits < 128; bits++)
        {
            bool Bit(int n) => (bits & (1 << n)) != 0;
            var context = new ShouldCacheMaterialContext(Bit(4), false, Bit(1), false, false, false, false, false)
            { UsedWithFoliage = Bit(0), UsedWithDecals = Bit(2), IsTerrainMaterial = Bit(3),
                UsedWithInstancedMeshes = Bit(5), UsedWithInstancedMeshParticles = Bit(6) };
            Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(Factory, context, factory.CreateCacheContext(), out bool actual));
            Assert.AreEqual((Bit(0) || Bit(1)) && !Bit(2), actual, "bits=" + bits);
        }
        foreach (var type in GetKnownShaderTypeRegistrations().Where(t => t.IsMeshMaterial))
        {
            TryGetKnownPermutationEnvironment(type.Name, Factory, out var foliage);
            TryGetKnownPermutationEnvironment(type.Name, "FLocalVertexFactory", out var local);
            CollectionAssert.AreEquivalent(local.MacroDefinitions.ToArray(), foliage.MacroDefinitions.ToArray());
            Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, foliage.CompileFlagsEvidence);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FoliageUsageIsStoredOrEffectiveMetadataAndPlannerRetainsInput(bool usage)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FoliageMetadata.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var compiler = new LE3MaterialShaderCompiler(material);
        Assert.IsFalse(compiler.BuildShouldCacheMaterialContext().UsedWithFoliage);
        var properties = new PropertyCollection { new BoolProperty(usage, "bUsedWithFoliage") };
        Assert.AreEqual(usage, compiler.BuildShouldCacheMaterialContext(properties).UsedWithFoliage);
        material.WriteProperties(properties);
        Assert.AreEqual(usage, compiler.BuildShouldCacheMaterialContext().UsedWithFoliage);
        TryGetKnownPermutationEnvironment(VertexType, Factory, out var environment);
        var translation = compiler.TranslateMaterialHlsl(Template());
        var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), [environment.CreateCandidate()]);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(usage ? 1 : 0, plan.Jobs.Count);
        if (usage) Assert.AreSame(compiler.CompilationInput, plan.Jobs.Single().CompilationInput);
        else Assert.AreEqual(ShaderCompileGate.VertexFactoryShouldCache, plan.Decisions.Single().Gate);
        Assert.AreEqual(0, translation.UniformExpressionTextures.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TwoOptionalRecordsSerializeInNativeOrderWithoutParticleThirdField(bool present)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FoliageArchive.pcc", MEGame.LE3);
        string[] order = ["InvNumVerticesPerInstance", "NumVerticesPerInstance"];
        var fields = present ? order.Reverse().Select((name, i) =>
            new ShaderParameterBinding(name, ShaderParameterBindingKind.Constant, (ushort)(i + 1), (ushort)(i * 16 + 3), (ushort)(4 + i), null)).ToArray() : [];
        var binder = new LE3ShaderParameterBinder(fields);
        var reference = new FVertexFactoryParameterRef { VertexFactoryType = Factory, Parameters = binder.BindVertexFactoryParameters(Factory) };
        Assert.AreEqual(0, binder.UnboundParameters.Count);
        using var stream = new MemoryStream(); reference.Serialize(new SerializingContainer(stream, package));
        Assert.AreEqual(24L, stream.Length); stream.Position = 12;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        foreach (string name in order)
        {
            var field = fields.SingleOrDefault(f => f.Name == name);
            Assert.AreEqual(field?.BaseIndex ?? 0, reader.ReadUInt16()); Assert.AreEqual(field?.Size ?? 0, reader.ReadUInt16());
            Assert.AreEqual(field?.BufferIndex ?? 0, reader.ReadUInt16());
        }
        stream.Position = 0; var copy = new FVertexFactoryParameterRef(); copy.Serialize(new SerializingContainer(stream, package, true));
        Assert.AreEqual(stream.Length, stream.Position); Assert.IsInstanceOfType<FFoliageVertexFactoryShaderParameters>(copy.Parameters);
        Assert.AreEqual(Factory, copy.VertexFactoryType.Name);
    }

    [TestMethod]
    [DataRow("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", false)]
    [DataRow("TBasePassVertexShaderFDirectionalVertexLightMapPolicyFNoDensityPolicy", true)]
    [DataRow("TDepthOnlyVertexShader<0>", true)]
    [DataRow("TAOMeshVertexShader<0>", false)]
    public void TypedFoliageObjectsDoNotRequireLocalTransformsAndRoundTrip(string type, bool present)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FoliageTyped.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        TryGetKnownPermutationEnvironment(type, Factory, out var environment);
        bool lightmap = type.Contains("FDirectionalVertexLightMapPolicy", StringComparison.Ordinal);
        string body = (present ? "float InvNumVerticesPerInstance, NumVerticesPerInstance; " : "") +
            (lightmap ? "float4 LightMapScale; " : "") +
            $"float4 {environment.Target.EntryPoint}(float4 p:POSITION):SV_Position {{ return p" +
            (present ? " + InvNumVerticesPerInstance + NumVerticesPerInstance" : "") + (lightmap ? " + LightMapScale" : "") + "; }";
        var snapshot = new LE3ShaderSourceSnapshot(new Dictionary<string, string> { ["MaterialTemplate.usf"] = Template(),
            [environment.Target.SourceFileName + ".usf"] = body, ["FoliageVertexFactory.usf"] = "// fixture" });
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
        Assert.IsInstanceOfType<FFoliageVertexFactoryShaderParameters>(reference.Parameters);
        Assert.AreEqual(present ? (ushort)4 : (ushort)0, ((FFoliageVertexFactoryShaderParameters)reference.Parameters).NumVerticesPerInstance.NumBytes);
        CollectionAssert.AreEqual(shader.ShaderByteCode, copy.ShaderByteCode);
    }

    private static string Template() => "/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n*/";
}
