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
    [DataRow("FLocalVertexFactoryApex", false)]
    [DataRow("FGPUSkinVertexFactoryApex", true)]
    public void ApexCapabilitiesAndCacheGateDoNotInheritOrdinaryLocal(string factoryName, bool dynamicLighting)
    {
        Assert.IsTrue(TryGetKnownVertexFactoryRegistration(factoryName, out var factory));
        Assert.AreEqual(dynamicLighting ? "ApexGpuSkinVertexFactory_1Bone" : "LocalVertexFactory", factory.SourceFileName);
        Assert.IsTrue(factory.UsedWithMaterials);
        Assert.IsFalse(factory.SupportsStaticLighting);
        Assert.AreEqual(dynamicLighting, factory.SupportsDynamicLighting);
        Assert.AreEqual(false, factory.SupportsPrecisePreviousPosition);
        for (int bits = 0; bits < 64; bits++)
        {
            bool Bit(int n) => (bits & (1 << n)) != 0;
            var material = Context() with { UsedWithAPEXMeshes = Bit(0), IsSpecialEngineMaterial = Bit(1),
                IsTerrainMaterial = Bit(2), UsedWithDecals = Bit(3), UsedWithSkeletalMesh = Bit(4), UsedWithInstancedMeshes = Bit(5) };
            Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(factory.Name, material, factory.CreateCacheContext(), out bool actual));
            Assert.AreEqual(Bit(0) || Bit(1), actual, "bits=" + bits);
        }
        foreach (var shader in GetKnownShaderTypeRegistrations().Where(s => s.IsMeshMaterial))
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(shader.Name, factory.Name, out var apex));
            Assert.IsTrue(TryGetKnownPermutationEnvironment(shader.Name, "FLocalVertexFactory", out var local));
            CollectionAssert.AreEquivalent(local.MacroDefinitions.ToArray(), apex.MacroDefinitions.ToArray());
            Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, apex.CompileFlagsEvidence);
            Assert.AreEqual(factory.CreateCacheContext(), apex.CreateCandidate().VertexFactoryContext);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LocalApexMetadataCanBeSuppliedWithoutAssetData(bool usage)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ApexMetadata.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var compiler = new LE3MaterialShaderCompiler(material);
        Assert.IsFalse(compiler.BuildShouldCacheMaterialContext().UsedWithAPEXMeshes);
        var properties = new PropertyCollection { new BoolProperty(usage, "bUsedWithAPEXMeshes") };
        Assert.AreEqual(usage, compiler.BuildShouldCacheMaterialContext(properties).UsedWithAPEXMeshes);
        material.WriteProperties(properties);
        Assert.AreEqual(usage, compiler.BuildShouldCacheMaterialContext().UsedWithAPEXMeshes);
    }

    [TestMethod]
    [DataRow("FGPUSkinMorphVertexFactory")]
    [DataRow("FInstancedStaticMeshVertexFactory")]
    [DataRow("FSplineMeshVertexFactory")]
    [DataRow("FParticleInstancedMeshVertexFactory")]
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
            material = material with { UsedWithSplineMeshes = Bit(4), UsedWithInstancedMeshParticles = Bit(3) };
            bool expected = factoryName == "FGPUSkinMorphVertexFactory"
                ? (Bit(3) || Bit(0)) && (Bit(5) || Bit(6) || Bit(0)) && !Bit(1)
                : factoryName == "FParticleInstancedMeshVertexFactory" ? (Bit(3) || Bit(0)) && !Bit(1)
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
        Assert.IsFalse(defaults.UsedWithSplineMeshes);
        Assert.IsFalse(defaults.UsedWithInstancedMeshParticles);
        var properties = new PropertyCollection
        {
            new BoolProperty(usage, "bUsedWithMorphTargets"), new BoolProperty(usage, "bUsedWithInstancedMeshes"),
            new BoolProperty(usage, "bUsedWithSplineMeshes"), new BoolProperty(usage, "bUsedWithInstancedMeshParticles")
        };
        var supplied = compiler.BuildShouldCacheMaterialContext(properties);
        Assert.AreEqual(usage, supplied.UsedWithMorphTargets); Assert.AreEqual(usage, supplied.UsedWithInstancedMeshes);
        Assert.AreEqual(usage, supplied.UsedWithSplineMeshes);
        Assert.AreEqual(usage, supplied.UsedWithInstancedMeshParticles);
        material.WriteProperties(properties);
        var stored = compiler.BuildShouldCacheMaterialContext();
        Assert.AreEqual(usage, stored.UsedWithMorphTargets); Assert.AreEqual(usage, stored.UsedWithInstancedMeshes);
        Assert.AreEqual(usage, stored.UsedWithSplineMeshes);
        Assert.AreEqual(usage, stored.UsedWithInstancedMeshParticles);
    }

    [TestMethod]
    [DataRow("FGPUSkinMorphVertexFactory", false, false, "GPUSKIN_MORPH_BLEND")]
    [DataRow("FInstancedStaticMeshVertexFactory", true, true, "USE_INSTANCING")]
    [DataRow("FSplineMeshVertexFactory", true, true, "USE_SPLINEDEFORM")]
    [DataRow("FParticleInstancedMeshVertexFactory", false, false, "COLOR_OVER_LIFE")]
    public void RegistrationsAndEnvironmentsRetainNativeCapabilitiesAndMacroEvidence(string factoryName,
        bool staticLighting, bool precisePrevious, string macro)
    {
        Assert.IsTrue(TryGetKnownVertexFactoryRegistration(factoryName, out var factory));
        Assert.IsTrue(factory.UsedWithMaterials); Assert.IsTrue(factory.SupportsDynamicLighting.Value);
        Assert.AreEqual(staticLighting, factory.SupportsStaticLighting);
        Assert.AreEqual(precisePrevious, factory.SupportsPrecisePreviousPosition.Value);
        foreach (var shader in GetKnownShaderTypeRegistrations().Where(s => s.IsMeshMaterial))
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(shader.Name, factoryName, out var environment));
            var candidate = environment.CreateCandidate();
            Assert.AreEqual("1", candidate.EnvironmentMacros[macro]);
            Assert.AreEqual(ShaderEnvironmentEvidence.LE3RegistrationCallback,
                environment.MacroDefinitions.Single(m => m.Name == macro).Evidence);
            Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.CompileFlagsEvidence,
                "Existing shader-type calibration does not calibrate a new factory permutation.");
            Assert.AreEqual(factory.CreateCacheContext(), candidate.VertexFactoryContext);
            if (factoryName == "FParticleInstancedMeshVertexFactory")
            {
                Assert.AreEqual("1", candidate.EnvironmentMacros["SIMPLE_VERTEX_LIGHTMAP"]);
                Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction,
                    environment.MacroDefinitions.Single(m => m.Name == "SIMPLE_VERTEX_LIGHTMAP").Evidence);
                Assert.AreEqual(candidate.EnvironmentMacros.Count, environment.MacroDefinitions.Count);
            }
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
        foreach (string name in new[] { "Unknown", "", "flocalvertexfactory", "FLocalDecalVertexFactoryApex" })
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
    [DataRow("InvNumVerticesPerInstance")]
    [DataRow("NumVerticesPerInstance")]
    [DataRow("InstancedPreViewTranslation")]
    public void ParticleBinderKeepsOptionalFieldsIndependentAndDoesNotInventALocalPrefix(string present)
    {
        var binder = new LE3ShaderParameterBinder([Constant(present, 48)]);
        var parameters = binder.BindVertexFactoryParameters("FParticleInstancedMeshVertexFactory");
        Assert.AreEqual(typeof(FParticleInstancedMeshVertexFactoryShaderParameters), parameters.GetType());
        foreach (string name in FactoryFields("FParticleInstancedMeshVertexFactory", true))
            Assert.AreEqual(name == present ? (ushort)16 : (ushort)0,
                ((FShaderParameter)parameters.GetType().GetField(name).GetValue(parameters)).NumBytes);
        Assert.AreEqual(0, binder.UnboundParameters.Count);
        var foreign = new LE3ShaderParameterBinder([Constant("LocalToWorld", 64)]);
        foreign.BindVertexFactoryParameters("FParticleInstancedMeshVertexFactory");
        Assert.AreEqual("LocalToWorld", foreign.UnboundParameters.Single().Name);
    }

    [TestMethod]
    [DataRow("FSplineMeshVertexFactory")]
    [DataRow("FParticleInstancedMeshVertexFactory")]
    public void NativeFactoryArchiveOrderUsesDistinctAllocationRecords(string factoryName)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FactoryRecordOrder.pcc", MEGame.LE3);
        string[] names = FactoryFields(factoryName, true);
        var allocations = names.Select((name, index) => new ShaderParameterBinding(name,
            ShaderParameterBindingKind.Constant, (ushort)(index + 1), (ushort)(index * 32), (ushort)(index + 4), null)).ToArray();
        var binder = new LE3ShaderParameterBinder(allocations);
        var reference = new FVertexFactoryParameterRef
        {
            VertexFactoryType = factoryName, Parameters = binder.BindVertexFactoryParameters(factoryName)
        };
        using var stream = new MemoryStream();
        reference.Serialize(new SerializingContainer(stream, package));
        Assert.AreEqual(12 + names.Length * 6, stream.Length);
        stream.Position = 12;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        foreach (var allocation in allocations)
        {
            Assert.AreEqual(allocation.BaseIndex, reader.ReadUInt16(), allocation.Name);
            Assert.AreEqual(allocation.Size, reader.ReadUInt16(), allocation.Name);
            Assert.AreEqual(allocation.BufferIndex, reader.ReadUInt16(), allocation.Name);
        }
        Assert.AreEqual(stream.Length, stream.Position);
        stream.Position = 0;
        var copy = new FVertexFactoryParameterRef();
        copy.Serialize(new SerializingContainer(stream, package, isLoading: true));
        Assert.AreEqual(stream.Length, stream.Position);
        foreach (var allocation in allocations)
        {
            var parameter = (FShaderParameter)copy.Parameters.GetType().GetField(allocation.Name).GetValue(copy.Parameters);
            Assert.AreEqual(allocation.BufferIndex, parameter.BufferIndex);
            Assert.AreEqual(allocation.BaseIndex, parameter.BaseIndex);
            Assert.AreEqual(allocation.Size, parameter.NumBytes);
        }
        Assert.AreEqual(0, binder.UnboundParameters.Count);
    }

    [TestMethod]
    [DataRow("FGPUSkinMorphVertexFactory", "LocalToWorld")]
    [DataRow("FGPUSkinMorphVertexFactory", "BoneMatrices")]
    [DataRow("FInstancedStaticMeshVertexFactory", "InstancedViewTranslation")]
    [DataRow("FGPUSkinDecalVertexFactory", "LocalToWorld")]
    [DataRow("FGPUSkinDecalVertexFactory", "BoneMatrices")]
    [DataRow("FGPUSkinMorphDecalVertexFactory", "LocalToWorld")]
    [DataRow("FGPUSkinMorphDecalVertexFactory", "BoneMatrices")]
    [DataRow("FSplineMeshVertexFactory", "SplineStartPos")]
    [DataRow("FSplineMeshVertexFactory", "SplineStartTangent")]
    [DataRow("FSplineMeshVertexFactory", "SplineStartRoll")]
    [DataRow("FSplineMeshVertexFactory", "SplineStartScale")]
    [DataRow("FSplineMeshVertexFactory", "SplineStartOffset")]
    [DataRow("FSplineMeshVertexFactory", "SplineEndPos")]
    [DataRow("FSplineMeshVertexFactory", "SplineEndTangent")]
    [DataRow("FSplineMeshVertexFactory", "SplineEndRoll")]
    [DataRow("FSplineMeshVertexFactory", "SplineEndScale")]
    [DataRow("FSplineMeshVertexFactory", "SplineEndOffset")]
    [DataRow("FSplineMeshVertexFactory", "SplineXDir")]
    [DataRow("FSplineMeshVertexFactory", "SmoothInterpRollScale")]
    [DataRow("FSplineMeshVertexFactory", "MeshMinZ")]
    [DataRow("FSplineMeshVertexFactory", "MeshRangeZ")]
    [DataRow("FLocalVertexFactoryApex", "LocalToWorld")]
    [DataRow("FGPUSkinVertexFactoryApex", "LocalToWorld")]
    [DataRow("FGPUSkinVertexFactoryApex", "BoneMatrices")]
    [DataRow("FGPUSkinVertexFactoryApex", "ApexDummy")]
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
    [DataRow("FGPUSkinDecalVertexFactory", "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy")]
    [DataRow("FLocalDecalVertexFactory", "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy")]
    [DataRow("FGPUSkinDecalVertexFactory", "TDepthOnlyVertexShader<0>")]
    [DataRow("FLocalDecalVertexFactory", "TDepthOnlyVertexShader<0>")]
    [DataRow("FGPUSkinDecalVertexFactory", "TAOMeshVertexShader<0>")]
    [DataRow("FLocalDecalVertexFactory", "TAOMeshVertexShader<0>")]
    [DataRow("FGPUSkinDecalVertexFactory", "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight")]
    [DataRow("FLocalDecalVertexFactory", "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight")]
    [DataRow("FGPUSkinMorphDecalVertexFactory", "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy")]
    [DataRow("FGPUSkinMorphDecalVertexFactory", "TDepthOnlyVertexShader<0>")]
    [DataRow("FGPUSkinMorphDecalVertexFactory", "TAOMeshVertexShader<0>")]
    [DataRow("FGPUSkinMorphDecalVertexFactory", "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight")]
    [DataRow("FSplineMeshVertexFactory", "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy")]
    [DataRow("FSplineMeshVertexFactory", "TDepthOnlyVertexShader<0>")]
    [DataRow("FSplineMeshVertexFactory", "TAOMeshVertexShader<0>")]
    [DataRow("FSplineMeshVertexFactory", "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight")]
    [DataRow("FParticleInstancedMeshVertexFactory", "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy")]
    [DataRow("FParticleInstancedMeshVertexFactory", "TDepthOnlyVertexShader<0>")]
    [DataRow("FParticleInstancedMeshVertexFactory", "TAOMeshVertexShader<0>")]
    [DataRow("FParticleInstancedMeshVertexFactory", "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight")]
    [DataRow("FLocalVertexFactoryApex", "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy")]
    [DataRow("FLocalVertexFactoryApex", "TDepthOnlyVertexShader<0>")]
    [DataRow("FLocalVertexFactoryApex", "TAOMeshVertexShader<0>")]
    [DataRow("FLocalVertexFactoryApex", "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight")]
    [DataRow("FGPUSkinVertexFactoryApex", "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy")]
    [DataRow("FGPUSkinVertexFactoryApex", "TDepthOnlyVertexShader<0>")]
    [DataRow("FGPUSkinVertexFactoryApex", "TAOMeshVertexShader<0>")]
    [DataRow("FGPUSkinVertexFactoryApex", "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight")]
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
                Type expectedType = factoryName switch
                {
                    "FGPUSkinMorphVertexFactory" => typeof(FGPUSkinVertexFactoryShaderParameters),
                    "FInstancedStaticMeshVertexFactory" => typeof(FInstancedStaticMeshVertexFactoryShaderParameters),
                    "FGPUSkinDecalVertexFactory" or "FGPUSkinMorphDecalVertexFactory" => typeof(FGPUSkinDecalVertexFactoryShaderParameters),
                    "FSplineMeshVertexFactory" => typeof(FSplineMeshVertexFactoryShaderParameters),
                    "FParticleInstancedMeshVertexFactory" => typeof(FParticleInstancedMeshVertexFactoryShaderParameters),
                    "FLocalVertexFactoryApex" => typeof(FLocalVertexFactoryShaderParameters),
                    "FGPUSkinVertexFactoryApex" => typeof(FGPUSkinVertexFactoryApexShaderParameters),
                    _ => typeof(FLocalDecalVertexFactoryShaderParameters)
                };
                Assert.AreEqual(expectedType, reference.Parameters.GetType());
                foreach (string name in fields)
                    Assert.AreEqual((ushort)16, ((FShaderParameter)reference.Parameters.GetType().GetField(name).GetValue(reference.Parameters)).NumBytes);
                string optional = factoryName switch
                {
                    "FGPUSkinMorphVertexFactory" => "WoundEllipse1", "FInstancedStaticMeshVertexFactory" => "InstancingParameters",
                    "FGPUSkinDecalVertexFactory" or "FGPUSkinMorphDecalVertexFactory" => "BoneToDecalRow1",
                    "FSplineMeshVertexFactory" => "LocalToWorld", "FParticleInstancedMeshVertexFactory" => "InstancedPreViewTranslation",
                    "FLocalVertexFactoryApex" or "FGPUSkinVertexFactoryApex" => "WorldToLocal",
                    _ => "DecalBlendInterval"
                };
                Assert.AreEqual(optionalPresent ? (ushort)16 : (ushort)0,
                    ((FShaderParameter)reference.Parameters.GetType().GetField(optional).GetValue(reference.Parameters)).NumBytes);
            }
        }
    }

    [TestMethod]
    [DataRow("FGPUSkinMorphVertexFactory")]
    [DataRow("FInstancedStaticMeshVertexFactory")]
    [DataRow("FSplineMeshVertexFactory")]
    [DataRow("FParticleInstancedMeshVertexFactory")]
    [DataRow("FLocalVertexFactoryApex")]
    [DataRow("FGPUSkinVertexFactoryApex")]
    public void PlannerUsesNewUsageMetadataWithoutChangingRequestedInputProvenance(string factoryName)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FactoryPlanning.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(true, "bUsedWithSkeletalMesh"));
        Assert.IsTrue(TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", factoryName, out var environment));
        foreach (bool usage in new[] { false, true })
        {
            material.WriteProperty(new BoolProperty(usage, factoryName switch
            {
                "FGPUSkinMorphVertexFactory" => "bUsedWithMorphTargets",
                "FSplineMeshVertexFactory" => "bUsedWithSplineMeshes", "FParticleInstancedMeshVertexFactory" => "bUsedWithInstancedMeshParticles",
                "FLocalVertexFactoryApex" or "FGPUSkinVertexFactoryApex" => "bUsedWithAPEXMeshes",
                _ => "bUsedWithInstancedMeshes"
            }));
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
    private static string[] FactoryFields(string factoryName, bool optional) => factoryName switch
    {
        "FGPUSkinMorphVertexFactory" => optional
            ? ["LocalToWorld", "WorldToLocal", "BoneMatrices", "MaxBoneInfluences", "MeshOrigin", "MeshExtension", "WoundEllipse0", "WoundEllipse1"]
            : ["LocalToWorld", "BoneMatrices"],
        "FInstancedStaticMeshVertexFactory" => optional
            ? ["LocalToWorld", "LocalToWorldRotDeterminantFlip", "WorldToLocal", "InstancedViewTranslation", "InstancingParameters"]
            : ["InstancedViewTranslation"],
        "FGPUSkinDecalVertexFactory" or "FGPUSkinMorphDecalVertexFactory" => optional
            ? ["LocalToWorld", "WorldToLocal", "BoneMatrices", "MaxBoneInfluences", "MeshOrigin", "MeshExtension", "WoundEllipse0", "WoundEllipse1", "BoneToDecalRow0", "BoneToDecalRow1", "DecalLocation"]
            : ["LocalToWorld", "BoneMatrices"],
        "FLocalDecalVertexFactory" => optional
            ? ["LocalToWorld", "LocalToWorldRotDeterminantFlip", "WorldToLocal", "DecalMatrix", "DecalLocation", "DecalOffset", "DecalLocalBinormal", "DecalLocalTangent", "DecalLocalNormal", "DecalBlendInterval"]
            : [],
        "FSplineMeshVertexFactory" => optional
            ? ["LocalToWorld", "LocalToWorldRotDeterminantFlip", "WorldToLocal", "SplineStartPos", "SplineStartTangent",
               "SplineStartRoll", "SplineStartScale", "SplineStartOffset", "SplineEndPos", "SplineEndTangent", "SplineEndRoll",
               "SplineEndScale", "SplineEndOffset", "SplineXDir", "SmoothInterpRollScale", "MeshMinZ", "MeshRangeZ"]
            : ["SplineStartPos", "SplineStartTangent", "SplineStartRoll", "SplineStartScale", "SplineStartOffset",
               "SplineEndPos", "SplineEndTangent", "SplineEndRoll", "SplineEndScale", "SplineEndOffset",
               "SplineXDir", "SmoothInterpRollScale", "MeshMinZ", "MeshRangeZ"],
        "FParticleInstancedMeshVertexFactory" => optional
            ? ["InvNumVerticesPerInstance", "NumVerticesPerInstance", "InstancedPreViewTranslation"] : [],
        "FLocalVertexFactoryApex" => optional
            ? ["LocalToWorld", "LocalToWorldRotDeterminantFlip", "WorldToLocal"] : ["LocalToWorld"],
        "FGPUSkinVertexFactoryApex" => optional
            ? ["LocalToWorld", "LocalToWorldRotDeterminantFlip", "WorldToLocal", "BoneMatrices", "ApexDummy"]
            : ["LocalToWorld", "BoneMatrices", "ApexDummy"],
        _ => throw new ArgumentException(factoryName)
    };
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
