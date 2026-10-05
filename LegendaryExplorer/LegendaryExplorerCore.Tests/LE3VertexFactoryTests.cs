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
public class LE3VertexFactoryTests
{
    [TestMethod]
    [DataRow("FGPUSkinMorphVertexFactory")]
    [DataRow("FInstancedStaticMeshVertexFactory")]
    public void CacheTruthTablesKeepUsageTerrainDecalAndSpecialGatesSeparate(string factoryName)
    {
        Assert.IsTrue(TryGetKnownVertexFactoryRegistration(factoryName, out var factory));
        for (int bits = 0; bits < 256; bits++)
        {
            bool Bit(int n) => (bits & (1 << n)) != 0;
            var material = Context() with
            {
                IsSpecialEngineMaterial = Bit(0), UsedWithDecals = Bit(1), IsTerrainMaterial = Bit(2),
                UsedWithMorphTargets = Bit(3), UsedWithInstancedMeshes = Bit(4),
                UsedWithSkeletalMesh = Bit(5), UsedWithFracturedMeshes = Bit(6), IsUnlit = Bit(7)
            };
            bool expected = factoryName == "FGPUSkinMorphVertexFactory"
                ? (Bit(3) || Bit(0)) && (Bit(5) || Bit(6) || Bit(0)) && !Bit(1)
                : (Bit(4) || Bit(0)) && !Bit(2) && !Bit(1);
            foreach (bool staticLighting in new[] { false, true })
            {
                Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(factoryName, material,
                    factory.CreateCacheContext() with { SupportsStaticLighting = staticLighting }, out bool actual));
                Assert.AreEqual(expected, actual, $"{factoryName}: bits={bits}, static={staticLighting}");
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NewUsageContextReadsStoredMetadataAndHonorsEffectiveProperties(bool usage)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FactoryMetadata.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var compiler = new LE3MaterialShaderCompiler(material);
        var defaults = compiler.BuildShouldCacheMaterialContext();
        Assert.IsFalse(defaults.UsedWithMorphTargets); Assert.IsFalse(defaults.UsedWithInstancedMeshes);
        var properties = new PropertyCollection
        {
            new BoolProperty(usage, "bUsedWithMorphTargets"), new BoolProperty(usage, "bUsedWithInstancedMeshes")
        };
        var supplied = compiler.BuildShouldCacheMaterialContext(properties);
        Assert.AreEqual(usage, supplied.UsedWithMorphTargets); Assert.AreEqual(usage, supplied.UsedWithInstancedMeshes);
        material.WriteProperties(properties);
        var stored = compiler.BuildShouldCacheMaterialContext();
        Assert.AreEqual(usage, stored.UsedWithMorphTargets); Assert.AreEqual(usage, stored.UsedWithInstancedMeshes);
    }

    [TestMethod]
    [DataRow("FGPUSkinMorphVertexFactory", false, false, "GPUSKIN_MORPH_BLEND")]
    [DataRow("FInstancedStaticMeshVertexFactory", true, true, "USE_INSTANCING")]
    public void RegistrationsAndEnvironmentsRetainNativeCapabilitiesAndMacroEvidence(string factoryName,
        bool staticLighting, bool precisePrevious, string macro)
    {
        Assert.IsTrue(TryGetKnownVertexFactoryRegistration(factoryName, out var factory));
        Assert.IsTrue(factory.UsedWithMaterials); Assert.IsTrue(factory.SupportsDynamicLighting.Value);
        Assert.AreEqual(staticLighting, factory.SupportsStaticLighting);
        Assert.AreEqual(precisePrevious, factory.SupportsPrecisePreviousPosition.Value);
        foreach (var shader in GetKnownShaderTypeRegistrations())
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(shader.Name, factoryName, out var environment));
            var candidate = environment.CreateCandidate();
            Assert.AreEqual("1", candidate.EnvironmentMacros[macro]);
            Assert.AreEqual(ShaderEnvironmentEvidence.LE3RegistrationCallback,
                environment.MacroDefinitions.Single(m => m.Name == macro).Evidence);
            Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.CompileFlagsEvidence,
                "Existing shader-type calibration does not calibrate a new factory permutation.");
            Assert.AreEqual(factory.CreateCacheContext(), candidate.VertexFactoryContext);
            Assert.AreEqual(factoryName == "FGPUSkinMorphVertexFactory", candidate.EnvironmentMacros.ContainsKey("QUAT_SKINNING"));
            if (factoryName == "FGPUSkinMorphVertexFactory") Assert.AreEqual("0", candidate.EnvironmentMacros["QUAT_SKINNING"]);
        }
    }

    [TestMethod]
    public void ExactBinderDispatchCannotSilentlyUseLocalForUnknownFactories()
    {
        var binder = new LE3ShaderParameterBinder([]);
        Assert.IsInstanceOfType<FLocalVertexFactoryShaderParameters>(binder.BindVertexFactoryParameters("FLocalVertexFactory"));
        Assert.ThrowsExactly<ArgumentNullException>(() => binder.BindVertexFactoryParameters(null));
        foreach (string name in new[] { "Unknown", "", "flocalvertexfactory", "FLocalDecalVertexFactory" })
            Assert.ThrowsExactly<InvalidOperationException>(() => binder.BindVertexFactoryParameters(name));
        Assert.AreEqual(0, binder.UnboundParameters.Count);
    }

    [TestMethod]
    public void InstancedBinderRequiresTranslationButAllowsAllOtherFieldsToBeOptimizedOut()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => new LE3ShaderParameterBinder([])
            .BindVertexFactoryParameters("FInstancedStaticMeshVertexFactory"));
        var binder = new LE3ShaderParameterBinder([Constant("InstancedViewTranslation", 64)]);
        var parameters = (FInstancedStaticMeshVertexFactoryShaderParameters)binder.BindVertexFactoryParameters("FInstancedStaticMeshVertexFactory");
        Assert.AreEqual((ushort)64, parameters.InstancedViewTranslation.BaseIndex);
        Assert.AreEqual(default(FShaderParameter), parameters.LocalToWorld);
        Assert.AreEqual(default(FShaderParameter), parameters.InstancingParameters);
        Assert.AreEqual(0, binder.UnboundParameters.Count);
    }

    [TestMethod]
    [DataRow("FGPUSkinMorphVertexFactory", "LocalToWorld")]
    [DataRow("FGPUSkinMorphVertexFactory", "BoneMatrices")]
    [DataRow("FInstancedStaticMeshVertexFactory", "InstancedViewTranslation")]
    public void RequiredFactoryBindingsFailTypedConstructionWhenAbsent(string factoryName, string missing)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FactoryMissingBinding.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        Assert.IsTrue(TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", factoryName, out var environment));
        var names = FactoryFields(factoryName, false).Where(name => name != missing).ToArray();
        var sources = Sources(environment.Target, names);
        var translation = compiler.TranslateMaterialHlsl(sources);
        var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
        var compiled = compiler.CompileShader(job, sources); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
        var result = compiler.ConstructKnownShader(translation, compiled);
        Assert.IsFalse(result.Success); Assert.IsNull(result.Shader);
        StringAssert.Contains(string.Join("; ", result.Errors), missing);
    }

    [TestMethod]
    [DataRow("FGPUSkinMorphVertexFactory", "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy")]
    [DataRow("FInstancedStaticMeshVertexFactory", "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy")]
    [DataRow("FGPUSkinMorphVertexFactory", "TDepthOnlyVertexShader<0>")]
    [DataRow("FInstancedStaticMeshVertexFactory", "TDepthOnlyVertexShader<0>")]
    [DataRow("FGPUSkinMorphVertexFactory", "TAOMeshVertexShader<0>")]
    [DataRow("FInstancedStaticMeshVertexFactory", "TAOMeshVertexShader<1>")]
    [DataRow("FGPUSkinMorphVertexFactory", "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight")]
    [DataRow("FInstancedStaticMeshVertexFactory", "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight")]
    public void FactoryLayoutsConstructAndRoundTripAcrossShaderConstructorFamilies(string factoryName, string shaderName)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FactoryTypedArchives.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        Assert.IsTrue(TryGetKnownPermutationEnvironment(shaderName, factoryName, out var environment));
        foreach (bool optionalPresent in new[] { false, true })
        {
            var fields = environment.Target.Stage == ShaderStage.Vertex ? FactoryFields(factoryName, optionalPresent) : [];
            var sources = Sources(environment.Target, fields);
            var translation = compiler.TranslateMaterialHlsl(sources); Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
            var compiled = compiler.CompileShader(job, sources); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
            var constructed = compiler.ConstructKnownShader(translation, compiled);
            Assert.IsTrue(constructed.Success, string.Join("; ", constructed.Errors)); Assert.AreEqual(0, constructed.UnboundParameters.Count);
            Shader shader = constructed.Shader;
            using var stream = new MemoryStream();
            new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0; Shader copy = null;
            new SerializingContainer(stream, package, true).Serialize(ref copy);
            Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(shader.GetType(), copy.GetType());
            Assert.AreEqual(shader.ParameterMapCRC, copy.ParameterMapCRC); CollectionAssert.AreEqual(shader.ShaderByteCode, copy.ShaderByteCode);
            if (environment.Target.Stage == ShaderStage.Vertex)
            {
                var reference = (FVertexFactoryParameterRef)copy.GetType().GetField("VertexFactoryParameters").GetValue(copy);
                Assert.AreEqual(factoryName, reference.VertexFactoryType.Name);
                Assert.AreEqual(factoryName == "FGPUSkinMorphVertexFactory" ? typeof(FGPUSkinVertexFactoryShaderParameters) :
                    typeof(FInstancedStaticMeshVertexFactoryShaderParameters), reference.Parameters.GetType());
                foreach (string name in fields)
                    Assert.AreEqual((ushort)16, ((FShaderParameter)reference.Parameters.GetType().GetField(name).GetValue(reference.Parameters)).NumBytes);
                string optional = factoryName == "FGPUSkinMorphVertexFactory" ? "WoundEllipse1" : "InstancingParameters";
                Assert.AreEqual(optionalPresent ? (ushort)16 : (ushort)0,
                    ((FShaderParameter)reference.Parameters.GetType().GetField(optional).GetValue(reference.Parameters)).NumBytes);
            }
        }
    }

    [TestMethod]
    [DataRow("FGPUSkinMorphVertexFactory")]
    [DataRow("FInstancedStaticMeshVertexFactory")]
    public void PlannerUsesNewUsageMetadataWithoutChangingRequestedInputProvenance(string factoryName)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FactoryPlanning.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(true, "bUsedWithSkeletalMesh"));
        Assert.IsTrue(TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", factoryName, out var environment));
        foreach (bool usage in new[] { false, true })
        {
            material.WriteProperty(new BoolProperty(usage, factoryName == "FGPUSkinMorphVertexFactory" ? "bUsedWithMorphTargets" : "bUsedWithInstancedMeshes"));
            var compiler = new LE3MaterialShaderCompiler(material);
            var translation = compiler.TranslateMaterialHlsl(Template());
            var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), [environment.CreateCandidate()]);
            Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(usage ? 1 : 0, plan.Jobs.Count);
            Assert.AreEqual(usage ? ShaderCompileDisposition.Eligible : ShaderCompileDisposition.Excluded, plan.Decisions.Single().Disposition);
            if (usage) Assert.AreSame(compiler.CompilationInput, plan.Jobs.Single().CompilationInput);
            else Assert.AreEqual(ShaderCompileGate.VertexFactoryShouldCache, plan.Decisions.Single().Gate);
        }
    }

    private static ShouldCacheMaterialContext Context() => new(false, false, false, false, false, false, false, false);
    private static ShaderParameterBinding Constant(string name, ushort offset) => new(name, ShaderParameterBindingKind.Constant, 0, offset, 16, null);
    private static string Template() => "/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n*/";
    private static string[] FactoryFields(string factoryName, bool optional) => factoryName == "FGPUSkinMorphVertexFactory"
        ? optional ? ["LocalToWorld", "WorldToLocal", "BoneMatrices", "MaxBoneInfluences", "MeshOrigin", "MeshExtension", "WoundEllipse0", "WoundEllipse1"]
            : ["LocalToWorld", "BoneMatrices"]
        : optional ? ["LocalToWorld", "LocalToWorldRotDeterminantFlip", "WorldToLocal", "InstancedViewTranslation", "InstancingParameters"]
            : ["InstancedViewTranslation"];
    private static LE3ShaderSourceSnapshot Sources(ShaderCompileTarget target, string[] fields)
    {
        string body = string.Concat(fields.Select(name => "float4 " + name + ";\n")) +
            $"float4 {target.EntryPoint}(float4 p:{(target.Stage == ShaderStage.Vertex ? "POSITION" : "SV_Position")}):" +
            (target.Stage == ShaderStage.Vertex ? "SV_Position" : "SV_Target") +
            " { return p" + string.Concat(fields.Select(name => " + " + name)) + "; }";
        return new LE3ShaderSourceSnapshot(new Dictionary<string, string>
        {
            ["MaterialTemplate.usf"] = Template(), [target.SourceFileName + ".usf"] = body,
            [target.VertexFactoryShaderFileName + ".usf"] = "// retained factory source-CRC input"
        });
    }
}
