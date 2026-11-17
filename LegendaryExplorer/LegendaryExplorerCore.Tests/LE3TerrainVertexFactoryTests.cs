using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3TerrainVertexFactoryTests
{
    private const string VertexType = "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy";
    private static readonly string[] BaseOrder = ["LocalToWorld", "WorldToLocal", "LocalToView", "TerrainLightmapCoordinateScaleBias",
        "TessellationInterpolation", "InvMaxTesselationLevel_ZScale", "InvTerrainSize_SectionBase", "Unused",
        "TessellationDistanceScale", "TessInterpDistanceValues"];
    private static readonly string[] DecalOrder = ["DecalMatrix", "DecalLocation", "DecalOffset", "DecalLocalBinormal", "DecalLocalTangent"];

    [TestMethod]
    [DataRow("FTerrainVertexFactory", false, false, false)]
    [DataRow("FTerrainMorphVertexFactory", true, false, false)]
    [DataRow("FTerrainFullMorphVertexFactory", true, true, false)]
    [DataRow("FTerrainDecalVertexFactory", false, false, true)]
    [DataRow("FTerrainMorphDecalVertexFactory", true, false, true)]
    [DataRow("FTerrainFullMorphDecalVertexFactory", true, true, true)]
    public void NativeVariantMacrosAndCapabilitiesKeepWorldCoordsAsReconstruction(string name, bool morph, bool gradients, bool decal)
    {
        TryGetKnownVertexFactoryRegistration(name, out var factory);
        Assert.AreEqual("TerrainVertexFactory", factory.SourceFileName);
        Assert.IsTrue(factory.UsedWithMaterials); Assert.IsTrue(factory.SupportsStaticLighting);
        Assert.AreEqual(true, factory.SupportsDynamicLighting); Assert.AreEqual(false, factory.SupportsPrecisePreviousPosition);
        Assert.AreEqual(factory, GetObservedVertexFactoryRegistrations().Single(v => v.Name == name));
        foreach (var shader in GetKnownShaderTypeRegistrations().Where(s => s.IsMeshMaterial))
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(shader.Name, name, out var environment));
            var macros = environment.CreateCandidate().EnvironmentMacros;
            Assert.AreEqual(morph, macros.ContainsKey("TERRAIN_MORPHING_ENABLED"));
            Assert.AreEqual(gradients, macros.ContainsKey("TERRAIN_MORPHING_GRADIENTS")); Assert.AreEqual(decal, macros.ContainsKey("DECAL_FACTORY"));
            Assert.AreEqual("1", macros["WORLD_COORDS"]);
            Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.MacroDefinitions.Single(m => m.Name == "WORLD_COORDS").Evidence);
            foreach (var macro in environment.MacroDefinitions.Where(m => m.Name is "TERRAIN_MORPHING_ENABLED" or "TERRAIN_MORPHING_GRADIENTS" or "DECAL_FACTORY"))
            { Assert.AreEqual("1", macro.Definition); Assert.AreEqual(ShaderEnvironmentEvidence.LE3RegistrationCallback, macro.Evidence); }
            Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.CompileFlagsEvidence);
            Assert.IsFalse(macros.ContainsKey("XBOXTESSELLATION")); Assert.IsFalse(macros.ContainsKey("QUAT_SKINNING"));
        }
    }

    [TestMethod]
    [DataRow("FTerrainVertexFactory")]
    [DataRow("FTerrainMorphVertexFactory")]
    [DataRow("FTerrainFullMorphVertexFactory")]
    public void NonDecalTerrainCachesTerrainOrSpecialNotStaticLightingUsage(string name)
    {
        TryGetKnownVertexFactoryRegistration(name, out var factory);
        for (int bits = 0; bits < 64; bits++)
        {
            bool Bit(int n) => (bits & (1 << n)) != 0;
            var context = Context() with { IsTerrainMaterial = Bit(0), IsSpecialEngineMaterial = Bit(1), UsedWithDecals = Bit(2),
                UsedWithStaticLighting = Bit(3), UsedWithFoliage = Bit(4), IsUnlit = Bit(5) };
            Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(name, context, factory.CreateCacheContext(), out bool actual));
            Assert.AreEqual((Bit(0) || Bit(1)) && !Bit(2), actual, "bits=" + bits);
        }
    }

    [TestMethod]
    [DataRow("FTerrainDecalVertexFactory")]
    [DataRow("FTerrainMorphDecalVertexFactory")]
    [DataRow("FTerrainFullMorphDecalVertexFactory")]
    public void DecalTerrainUsesTypeNameVetoAndConditionalFallbackResolution(string name)
    {
        TryGetKnownVertexFactoryRegistration(name, out var factory);
        foreach (string type in new[] { VertexType, "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFNoDensityPolicy", "lowercasevertexlightmappolicy" })
        for (int bits = 0; bits < 128; bits++)
        {
            bool Bit(int n) => (bits & (1 << n)) != 0;
            var context = Context() with { UsedWithDecals = Bit(0), IsSpecialEngineMaterial = Bit(1), DecalFactoryFallback = Bit(2),
                IsTerrainMaterial = Bit(4), IsUnlit = Bit(5), UsedWithStaticLighting = Bit(6) };
            Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(name, context, factory.CreateCacheContext(), out bool actual,
                new(Bit(3)), shaderTypeName: type, shaderSourceFileName: "VertexLightMapPolicy"));
            Assert.AreEqual((Bit(0) || Bit(1) && Bit(3) || Bit(2)) && !type.Contains("VertexLightMapPolicy", StringComparison.Ordinal), actual);
        }
        var unknown = Context() with { DecalFactoryFallback = null };
        Assert.IsFalse(TryEvaluateKnownVertexFactoryShouldCache(name, unknown, factory.CreateCacheContext(), out _, shaderTypeName: VertexType));
        Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(name, unknown, factory.CreateCacheContext(), out bool excluded,
            shaderTypeName: "AnyVertexLightMapPolicy")); Assert.IsFalse(excluded);
        Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(name, unknown with { UsedWithDecals = true }, factory.CreateCacheContext(),
            out bool allowed, shaderTypeName: VertexType)); Assert.IsTrue(allowed);
        Assert.IsFalse(TryEvaluateKnownVertexFactoryShouldCache(name, unknown with { UsedWithDecals = true }, factory.CreateCacheContext(), out _));
        Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(name, Context(), factory.CreateCacheContext(), out excluded)); Assert.IsFalse(excluded);
    }

    [TestMethod]
    [DataRow("FTerrainVertexFactory")]
    [DataRow("FTerrainMorphVertexFactory")]
    [DataRow("FTerrainFullMorphVertexFactory")]
    [DataRow("FTerrainDecalVertexFactory")]
    [DataRow("FTerrainMorphDecalVertexFactory")]
    [DataRow("FTerrainFullMorphDecalVertexFactory")]
    public void RequiredTransformCannotBeReplacedByOptionalFields(string name)
    {
        var fields = Fields(name).Where(f => f != "LocalToWorld" && f != "Unused").Select((f, i) => Constant(f, (ushort)(i * 16))).ToArray();
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => new LE3ShaderParameterBinder(fields).BindVertexFactoryParameters(name));
        StringAssert.Contains(error.Message, "LocalToWorld");
    }

    [TestMethod]
    [DataRow("FTerrainVertexFactory", false)]
    [DataRow("FTerrainVertexFactory", true)]
    [DataRow("FTerrainMorphVertexFactory", false)]
    [DataRow("FTerrainMorphVertexFactory", true)]
    [DataRow("FTerrainFullMorphVertexFactory", false)]
    [DataRow("FTerrainFullMorphVertexFactory", true)]
    [DataRow("FTerrainDecalVertexFactory", false)]
    [DataRow("FTerrainDecalVertexFactory", true)]
    [DataRow("FTerrainMorphDecalVertexFactory", false)]
    [DataRow("FTerrainMorphDecalVertexFactory", true)]
    [DataRow("FTerrainFullMorphDecalVertexFactory", false)]
    [DataRow("FTerrainFullMorphDecalVertexFactory", true)]
    public void OrderedArchiveInjectsCanonicalUnboundPlaceholderNotAnAllocation(string name, bool optional)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TerrainArchive.pcc", MEGame.LE3);
        var fields = Fields(name).Where(f => f != "Unused" && (optional || f == "LocalToWorld")).Reverse().Select((f, i) =>
            new ShaderParameterBinding(f, ShaderParameterBindingKind.Constant, (ushort)(i + 1), (ushort)(i * 16 + 3), (ushort)(16 + i), null)).ToArray();
        var binder = new LE3ShaderParameterBinder(fields);
        var reference = new FVertexFactoryParameterRef { VertexFactoryType = name, Parameters = binder.BindVertexFactoryParameters(name) };
        Assert.AreEqual(0, binder.UnboundParameters.Count);
        using var stream = new MemoryStream(); reference.Serialize(new SerializingContainer(stream, package));
        Assert.AreEqual(12L + Fields(name).Length * 6, stream.Length); stream.Position = 12;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        foreach (string fieldName in Fields(name))
        {
            var field = fields.SingleOrDefault(f => f.Name == fieldName);
            Assert.AreEqual(field?.BaseIndex ?? 0, reader.ReadUInt16(), fieldName);
            Assert.AreEqual(field?.Size ?? 0, reader.ReadUInt16(), fieldName); Assert.AreEqual(field?.BufferIndex ?? 0, reader.ReadUInt16(), fieldName);
        }
        stream.Position = 0; var copy = new FVertexFactoryParameterRef(); copy.Serialize(new SerializingContainer(stream, package, true));
        Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(name, copy.VertexFactoryType.Name);
        Assert.IsInstanceOfType<FTerrainVertexFactoryShaderParameters>(copy.Parameters);
        Assert.AreEqual(name.Contains("Decal", StringComparison.Ordinal), copy.Parameters is FTerrainDecalVertexFactoryShaderParameters);
        Assert.AreEqual(default(FShaderParameter), ((FTerrainVertexFactoryShaderParameters)copy.Parameters).Unused);
    }

    [TestMethod]
    [DataRow("FTerrainVertexFactory")]
    [DataRow("FTerrainMorphVertexFactory")]
    [DataRow("FTerrainFullMorphVertexFactory")]
    [DataRow("FTerrainDecalVertexFactory")]
    [DataRow("FTerrainMorphDecalVertexFactory")]
    [DataRow("FTerrainFullMorphDecalVertexFactory")]
    public void UnusedReflectionAllocationIsNotConsumedAsARealNativeParameter(string name)
    {
        var binder = new LE3ShaderParameterBinder([Constant("LocalToWorld", 0), Constant("Unused", 64)]);
        var result = (FTerrainVertexFactoryShaderParameters)binder.BindVertexFactoryParameters(name);
        Assert.AreEqual(default(FShaderParameter), result.Unused);
        Assert.AreEqual("Unused", binder.UnboundParameters.Single().Name);
    }

    [TestMethod]
    [DataRow("FTerrainVertexFactory")]
    [DataRow("FTerrainMorphVertexFactory")]
    [DataRow("FTerrainFullMorphVertexFactory")]
    [DataRow("FTerrainDecalVertexFactory")]
    [DataRow("FTerrainMorphDecalVertexFactory")]
    [DataRow("FTerrainFullMorphDecalVertexFactory")]
    public void TypedFactoryPreservesAllOptionalFieldsAndPlannerRequiresExplicitTerrainSemantics(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3TerrainTyped.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        TryGetKnownPermutationEnvironment(VertexType, name, out var environment);
        string[] optional = Fields(name).Where(f => f != "LocalToWorld" && f != "Unused").ToArray();
        string body = "float4x4 LocalToWorld; " + string.Concat(optional.Select(f => "float4 " + f + "; ")) +
            "float4 Main(float4 p:POSITION):SV_Position { return mul(LocalToWorld,p)" + string.Concat(optional.Select(f => " + " + f)) + "; }";
        var snapshot = new LE3ShaderSourceSnapshot(new Dictionary<string, string> { ["MaterialTemplate.usf"] = Template(),
            [environment.Target.SourceFileName + ".usf"] = body, ["TerrainVertexFactory.usf"] = "// fixture" });
        var translation = compiler.TranslateMaterialHlsl(snapshot);
        var ordinary = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), [environment.CreateCandidate()]);
        Assert.IsTrue(ordinary.Success, string.Join("; ", ordinary.Errors)); Assert.AreEqual(0, ordinary.Jobs.Count);
        var context = compiler.BuildShouldCacheMaterialContext() with { IsTerrainMaterial = true, UsedWithDecals = name.Contains("Decal", StringComparison.Ordinal) };
        var explicitPlan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), [environment.CreateCandidate()], context);
        Assert.IsTrue(explicitPlan.Success, string.Join("; ", explicitPlan.Errors)); Assert.AreEqual(1, explicitPlan.Jobs.Count);
        Assert.AreSame(compiler.CompilationInput, explicitPlan.Jobs.Single().CompilationInput);
        var compiled = compiler.CompileShader(explicitPlan.Jobs.Single(), snapshot); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
        var result = compiler.ConstructKnownShader(translation, compiled); Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(0, result.UnboundParameters.Count);
        Shader shader = result.Shader;
        using var stream = new MemoryStream(); new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0;
        Shader copy = null; new SerializingContainer(stream, package, true).Serialize(ref copy); Assert.AreEqual(stream.Length, stream.Position);
        var reference = (FVertexFactoryParameterRef)copy.GetType().GetField("VertexFactoryParameters").GetValue(copy);
        Assert.IsInstanceOfType<FTerrainVertexFactoryShaderParameters>(reference.Parameters);
        Assert.AreEqual(name.Contains("Decal", StringComparison.Ordinal), reference.Parameters is FTerrainDecalVertexFactoryShaderParameters);
        Assert.AreEqual(default(FShaderParameter), ((FTerrainVertexFactoryShaderParameters)reference.Parameters).Unused);
        CollectionAssert.AreEqual(shader.ShaderByteCode, copy.ShaderByteCode);
    }

    private static string[] Fields(string name) => name.Contains("Decal", StringComparison.Ordinal) ? BaseOrder.Concat(DecalOrder).ToArray() : BaseOrder;
    private static ShaderParameterBinding Constant(string name, ushort index) => new(name, ShaderParameterBindingKind.Constant, 0, index, 16, null);
    private static ShouldCacheMaterialContext Context() => new(false, false, false, false, false, false, false, false);
    private static string Template() => "/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n*/";
}
