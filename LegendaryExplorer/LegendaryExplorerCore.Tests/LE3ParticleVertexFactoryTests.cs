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
public class LE3ParticleVertexFactoryTests
{
    [TestMethod]
    public void ObservedRegistryDoesNotMakeUnimplementedMaterialFactoriesCompilable()
    {
        var observed = GetObservedVertexFactoryRegistrations();
        Assert.AreEqual(29, observed.Count); Assert.AreEqual(29, observed.Select(v => v.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual(28, observed.Count(v => v.UsedWithMaterials));
        foreach (var known in GetKnownVertexFactoryRegistrations()) Assert.AreEqual(known, observed.Single(v => v.Name == known.Name));
        var missing = observed.Where(v => !GetKnownVertexFactoryRegistrations().Any(k => k.Name == v.Name)).ToArray();
        Assert.AreEqual(29 - GetKnownVertexFactoryRegistrations().Count, missing.Length);
        foreach (var factory in missing)
        {
            Assert.IsFalse(TryGetKnownVertexFactoryRegistration(factory.Name, out _));
            Assert.IsFalse(TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", factory.Name, out _));
            Assert.ThrowsExactly<InvalidOperationException>(() => new LE3ShaderParameterBinder([]).BindVertexFactoryParameters(factory.Name));
        }
    }

    [TestMethod]
    public void ObservedLocalShadowIsFilteredBeforeUnresolvedMaterialOrFactoryCallbacks()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ObservedShadowFilter.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var shadow = GetObservedVertexFactoryRegistrations().Single(v => v.Name == "FLocalShadowVertexFactory");
        Assert.IsFalse(shadow.UsedWithMaterials); Assert.IsFalse(shadow.SupportsStaticLighting);
        Assert.AreEqual(true, shadow.SupportsDynamicLighting); Assert.AreEqual(false, shadow.SupportsPrecisePreviousPosition);
        TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", "FLocalVertexFactory", out var local);
        var candidate = local.CreateCandidate() with { Target = local.Target with { VertexFactoryTypeName = shadow.Name },
            VertexFactoryContext = shadow.CreateCacheContext() };
        var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(Template()), MaterialResource.Create(), [candidate],
            materialResourceShouldCache: (_, _) => throw new InvalidOperationException("Must not evaluate an excluded factory"));
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(0, plan.Jobs.Count);
        Assert.AreEqual(ShaderCompileGate.VertexFactoryUsage, plan.Decisions.Single().Gate);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DynamicParameterProducesOneLocalAndNoUniformOrTextureData(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DynamicProducer.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("Dynamic", "MaterialExpressionDynamicParameter", material, indexed: false);
        node.WriteProperty(new NameProperty("NotAUniform", "ParameterName"));
        material.WriteProperty(Input("EmissiveColor", node.UIndex));
        var translation = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
        Assert.IsTrue(translation.UsesDynamicParameter); Assert.IsFalse(translation.UsesVertexColor);
        Assert.AreEqual($"\t{(half ? "half" : "float")}4 Local0 = GetDynamicParameter(Parameters);\r\n\treturn Local0.rgb",
            translation.PropertyCode["EmissiveColor"]);
        Assert.AreEqual(0, translation.UniformPixelVectorExpressions.Count + translation.UniformPixelScalarExpressions.Count +
            translation.UniformExpressionTextures.Count);
    }

    [TestMethod]
    public void DynamicParameterRejectsVertexFrequencyBeforeSettingUsage()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DynamicVertexError.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("Dynamic", "MaterialExpressionDynamicParameter", material, indexed: false);
        material.WriteProperty(Input("WorldPositionOffset", node.UIndex));
        var translation = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(translation.Success); Assert.IsFalse(translation.UsesDynamicParameter);
        Assert.IsNull(translation.MaterialHlsl);
        CollectionAssert.Contains(translation.Errors.ToArray(), "Invalid node used in vertex shader input!");
    }

    [TestMethod]
    [DataRow(0, "r")]
    [DataRow(1, "g")]
    [DataRow(2, "b")]
    [DataRow(3, "a")]
    public void DynamicParameterRootMasksSelectEachRuntimeChannelWithoutUniformPacking(int channel, string suffix)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DynamicMasks.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("Dynamic", "MaterialExpressionDynamicParameter", material, indexed: false);
        var input = Input("Opacity", node.UIndex);
        input.Properties.Add(new IntProperty(1, "Mask"));
        foreach (var (name, index) in new[] { ("MaskR", 0), ("MaskG", 1), ("MaskB", 2), ("MaskA", 3) })
            input.Properties.Add(new IntProperty(channel == index ? 1 : 0, name));
        material.WriteProperty(input);
        var translation = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
        Assert.IsTrue(translation.UsesDynamicParameter);
        Assert.AreEqual("\tfloat4 Local0 = GetDynamicParameter(Parameters);\r\n\treturn Local0." + suffix, translation.PropertyCode["Opacity"]);
        Assert.AreEqual(0, translation.UniformPixelScalarExpressions.Count + translation.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void ResolvedInstanceStaticValuesDriveParticleUsageAndPreserveRequestedInstance(bool selected, bool overrideFlag)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DynamicInstanceSeam.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var instance = package.CreateExport("Instance", "MaterialInstanceConstant", indexed: false);
        var dynamic = package.CreateExport("Dynamic", "MaterialExpressionDynamicParameter", material, indexed: false);
        var constant = package.CreateExport("Constant", "MaterialExpressionConstant", material, indexed: false);
        var branch = package.CreateExport("Switch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        branch.WriteProperty(new NameProperty("ParticleMode", "ParameterName"));
        branch.WriteProperty(new BoolProperty(!selected, "DefaultValue"));
        branch.WriteProperty(Input("A", dynamic.UIndex)); branch.WriteProperty(Input("B", constant.UIndex));
        material.WriteProperty(Input("EmissiveColor", branch.UIndex));
        var parameters = (StaticParameterSet)Guid.NewGuid();
        parameters.StaticSwitchParameters = [new() { ParameterName = "ParticleMode", Value = selected, bOverride = overrideFlag }];
        var input = new ResolvedMaterialCompilationInput(material, parameters, instance);
        var translation = new LE3MaterialShaderCompiler(input).TranslateMaterialHlsl(Template());
        Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
        Assert.AreEqual(selected, translation.UsesDynamicParameter);
        Assert.AreSame(input, translation.CompilationInput); Assert.AreSame(instance, translation.CompilationInput.RequestedMaterial);
        Assert.AreEqual(overrideFlag, input.GetStaticParametersSnapshot().StaticSwitchParameters.Single().bOverride);
    }

    [TestMethod]
    public void SelectedStaticBranchAndRetranslationRecomputeDynamicUsageRatherThanInheritCookedResource()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DynamicStaticSelection.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(true, "bUsedWithParticleSprites"));
        material.WriteProperty(new BoolProperty(true, "bUsedWithParticleSubUV"));
        material.WriteProperty(new BoolProperty(true, "bUsedWithBeamTrails"));
        var dynamic = package.CreateExport("Dynamic", "MaterialExpressionDynamicParameter", material, indexed: false);
        var constant = package.CreateExport("Constant", "MaterialExpressionConstant", material, indexed: false);
        var branch = package.CreateExport("Branch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        branch.WriteProperty(new NameProperty("ParticleMode", "ParameterName"));
        branch.WriteProperty(Input("A", dynamic.UIndex)); branch.WriteProperty(Input("B", constant.UIndex));
        material.WriteProperty(Input("EmissiveColor", branch.UIndex));
        var compiler = new LE3MaterialShaderCompiler(material);
        var resource = MaterialResource.Create();
        foreach (bool selected in new[] { false, true, false })
        {
            branch.WriteProperty(new BoolProperty(selected, "DefaultValue"));
            resource.bUsesDynamicParameter = !selected;
            var translation = compiler.TranslateMaterialHlsl(Template());
            Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
            Assert.AreEqual(selected, translation.UsesDynamicParameter);
            var candidates = new[] { "FParticleVertexFactory", "FParticleDynamicParameterVertexFactory", "FParticleSubUVVertexFactory",
                "FParticleSubUVDynamicParameterVertexFactory", "FParticleBeamTrailVertexFactory", "FParticleBeamTrailDynamicParameterVertexFactory" }
                .Select(name => { TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", name, out var env); return env.CreateCandidate(); }).ToArray();
            var plan = compiler.BuildProvisionalCompilePlan(translation, resource, candidates);
            Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(selected ? 4 : 3, plan.Jobs.Count);
            foreach (var decision in plan.Decisions)
            {
                string name = decision.Target.VertexFactoryTypeName;
                bool expected = name == "FParticleBeamTrailVertexFactory" || name.Contains("DynamicParameter", StringComparison.Ordinal) == selected;
                Assert.AreEqual(expected ? ShaderCompileDisposition.Eligible : ShaderCompileDisposition.Excluded, decision.Disposition, name);
            }
            Assert.AreEqual(!selected, resource.bUsesDynamicParameter, "Planning must not mutate the caller's resource.");
        }
    }

    [TestMethod]
    [DataRow("FParticleBeamTrailVertexFactory", false)]
    [DataRow("FParticleBeamTrailDynamicParameterVertexFactory", true)]
    public void BeamStandardIgnoresDynamicBitWhileDynamicVariantRequiresIt(string name, bool dynamic)
    {
        TryGetKnownVertexFactoryRegistration(name, out var factory);
        Assert.AreEqual("ParticleBeamTrailVertexFactory", factory.SourceFileName);
        Assert.IsFalse(factory.SupportsStaticLighting); Assert.AreEqual(true, factory.SupportsDynamicLighting);
        Assert.AreEqual(false, factory.SupportsPrecisePreviousPosition);
        for (int bits = 0; bits < 128; bits++)
        {
            bool Bit(int n) => (bits & (1 << n)) != 0;
            var context = new ShouldCacheMaterialContext(false, false, Bit(2), false, false, false, false, false)
            { UsedWithBeamTrails = Bit(0), UsesDynamicParameter = Bit(1), UsedWithDecals = Bit(3), IsTerrainMaterial = Bit(4),
                UsedWithParticleSprites = Bit(5), UsedWithParticleSubUV = Bit(6) };
            Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(name, context, factory.CreateCacheContext(), out bool actual));
            Assert.AreEqual(((Bit(0) && (!dynamic || Bit(1))) || Bit(2)) && !Bit(3), actual, name + "/" + bits);
        }
        TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", name, out var environment);
        Assert.AreEqual("2", environment.CreateCandidate().EnvironmentMacros["NUM_AXIS_ROTATION_VECTORS"]);
        Assert.AreEqual(dynamic, environment.CreateCandidate().EnvironmentMacros.ContainsKey("USE_DYNAMIC_PARAMETERS"));
        Assert.AreEqual(dynamic, environment.CreateCandidate().EnvironmentMacros.ContainsKey("SIMPLE_VERTEX_LIGHTMAP"));
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => new LE3ShaderParameterBinder([]).BindVertexFactoryParameters(name));
        StringAssert.Contains(error.Message, "LocalToWorld");
    }

    [TestMethod]
    [DataRow("FParticleBeamTrailVertexFactory", false)]
    [DataRow("FParticleBeamTrailVertexFactory", true)]
    [DataRow("FParticleBeamTrailDynamicParameterVertexFactory", false)]
    [DataRow("FParticleBeamTrailDynamicParameterVertexFactory", true)]
    public void BeamArchiveUsesOptionalCamerasAndRequiredTransformInNativeOrder(string name, bool optional)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3BeamArchive.pcc", MEGame.LE3);
        string[] fields = ["CameraWorldPosition", "CameraRight", "CameraUp", "ScreenAlignment", "LocalToWorld"];
        var allocations = fields.Where(f => optional || f == "LocalToWorld").Select((f, i) =>
            new ShaderParameterBinding(f, ShaderParameterBindingKind.Constant, (ushort)(i + 1), (ushort)(i * 16 + 3), (ushort)(16 + i), null)).ToArray();
        var binder = new LE3ShaderParameterBinder(allocations);
        var reference = new FVertexFactoryParameterRef { VertexFactoryType = name, Parameters = binder.BindVertexFactoryParameters(name) };
        Assert.AreEqual(0, binder.UnboundParameters.Count);
        using var stream = new MemoryStream(); reference.Serialize(new SerializingContainer(stream, package));
        Assert.AreEqual(42L, stream.Length); stream.Position = 12;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        foreach (string field in fields)
        {
            var allocation = allocations.SingleOrDefault(a => a.Name == field);
            Assert.AreEqual(allocation?.BaseIndex ?? 0, reader.ReadUInt16());
            Assert.AreEqual(allocation?.Size ?? 0, reader.ReadUInt16());
            Assert.AreEqual(allocation?.BufferIndex ?? 0, reader.ReadUInt16());
        }
        stream.Position = 0; var copy = new FVertexFactoryParameterRef();
        copy.Serialize(new SerializingContainer(stream, package, isLoading: true)); Assert.AreEqual(stream.Length, stream.Position);
        Assert.IsInstanceOfType<FParticleBeamTrailVertexFactoryShaderParameters>(copy.Parameters);
        Assert.AreEqual(name, copy.VertexFactoryType.Name);
    }
    [TestMethod]
    [DataRow("FParticleVertexFactory", false, false)]
    [DataRow("FParticleDynamicParameterVertexFactory", false, true)]
    [DataRow("FParticleSubUVVertexFactory", true, false)]
    [DataRow("FParticleSubUVDynamicParameterVertexFactory", true, true)]
    public void SpriteCapabilitiesAndNativeEnvironmentIncludeTheAxisArrayLength(string name, bool subUV, bool dynamic)
    {
        Assert.IsTrue(TryGetKnownVertexFactoryRegistration(name, out var factory));
        Assert.AreEqual("ParticleSpriteVertexFactory", factory.SourceFileName);
        Assert.IsTrue(factory.UsedWithMaterials); Assert.IsFalse(factory.SupportsStaticLighting);
        Assert.AreEqual(true, factory.SupportsDynamicLighting); Assert.AreEqual(false, factory.SupportsPrecisePreviousPosition);
        foreach (var shader in GetKnownShaderTypeRegistrations().Where(s => s.IsMeshMaterial))
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(shader.Name, name, out var environment));
            var macros = environment.CreateCandidate().EnvironmentMacros;
            Assert.AreEqual("1", macros["PARTICLES_ALLOW_AXIS_ROTATION"]);
            Assert.AreEqual("2", macros["NUM_AXIS_ROTATION_VECTORS"]);
            Assert.AreEqual("1", macros["USE_OCCLUSION_PERCENTAGE"]);
            Assert.AreEqual(subUV, macros.ContainsKey("SUBUV_PARTICLES"));
            Assert.AreEqual(dynamic, macros.ContainsKey("USE_DYNAMIC_PARAMETERS"));
            foreach (var macro in environment.MacroDefinitions.Where(m => m.Name is "PARTICLES_ALLOW_AXIS_ROTATION" or
                "NUM_AXIS_ROTATION_VECTORS" or "USE_OCCLUSION_PERCENTAGE" or "SUBUV_PARTICLES" or "USE_DYNAMIC_PARAMETERS"))
                Assert.AreEqual(ShaderEnvironmentEvidence.LE3RegistrationCallback, macro.Evidence);
            Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.CompileFlagsEvidence);
            Assert.IsFalse(macros.ContainsKey("QUAT_SKINNING")); Assert.IsFalse(macros.ContainsKey("DECAL_FACTORY"));
        }
        TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", name, out var noLight);
        Assert.AreEqual(dynamic, noLight.CreateCandidate().EnvironmentMacros.ContainsKey("SIMPLE_VERTEX_LIGHTMAP"));
    }

    [TestMethod]
    [DataRow("FParticleVertexFactory", false, false)]
    [DataRow("FParticleDynamicParameterVertexFactory", false, true)]
    [DataRow("FParticleSubUVVertexFactory", true, false)]
    [DataRow("FParticleSubUVDynamicParameterVertexFactory", true, true)]
    public void CacheSelectsDynamicUsageWithoutInventingTerrainLightingOrSiblingUsageGates(string name, bool subUV, bool dynamic)
    {
        TryGetKnownVertexFactoryRegistration(name, out var factory);
        for (int bits = 0; bits < 256; bits++)
        {
            bool Bit(int n) => (bits & (1 << n)) != 0;
            var context = new ShouldCacheMaterialContext(Bit(6), false, Bit(3), false, false, false, false, Bit(7))
            {
                UsedWithParticleSprites = Bit(0), UsedWithParticleSubUV = Bit(1), UsesDynamicParameter = Bit(2),
                UsedWithDecals = Bit(4), IsTerrainMaterial = Bit(5)
            };
            Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(name, context, factory.CreateCacheContext(), out bool actual));
            Assert.AreEqual((((subUV ? Bit(1) : Bit(0)) && Bit(2) == dynamic) || Bit(3)) && !Bit(4), actual,
                name + "/" + bits);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MetadataReadsOnlyAuthoredUsageNotAnInventedDynamicProperty(bool usage)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ParticleMetadata.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var compiler = new LE3MaterialShaderCompiler(material);
        var properties = new PropertyCollection { new BoolProperty(usage, "bUsedWithParticleSprites"),
            new BoolProperty(usage, "bUsedWithParticleSubUV"), new BoolProperty(true, "bUsesDynamicParameter") };
        var context = compiler.BuildShouldCacheMaterialContext(properties);
        Assert.AreEqual(usage, context.UsedWithParticleSprites); Assert.AreEqual(usage, context.UsedWithParticleSubUV);
        Assert.IsFalse(context.UsesDynamicParameter);
        material.WriteProperties(properties);
        Assert.AreEqual(context, compiler.BuildShouldCacheMaterialContext());
    }

    [TestMethod]
    [DataRow("FParticleVertexFactory", "LocalToWorld")]
    [DataRow("FParticleVertexFactory", "AxisRotationVectorSourceIndex")]
    [DataRow("FParticleVertexFactory", "AxisRotationVectors")]
    [DataRow("FParticleVertexFactory", "ParticleUpRightResultScalars")]
    [DataRow("FParticleDynamicParameterVertexFactory", "LocalToWorld")]
    [DataRow("FParticleDynamicParameterVertexFactory", "AxisRotationVectorSourceIndex")]
    [DataRow("FParticleDynamicParameterVertexFactory", "AxisRotationVectors")]
    [DataRow("FParticleDynamicParameterVertexFactory", "ParticleUpRightResultScalars")]
    [DataRow("FParticleSubUVVertexFactory", "LocalToWorld")]
    [DataRow("FParticleSubUVVertexFactory", "AxisRotationVectorSourceIndex")]
    [DataRow("FParticleSubUVVertexFactory", "AxisRotationVectors")]
    [DataRow("FParticleSubUVVertexFactory", "ParticleUpRightResultScalars")]
    [DataRow("FParticleSubUVDynamicParameterVertexFactory", "LocalToWorld")]
    [DataRow("FParticleSubUVDynamicParameterVertexFactory", "AxisRotationVectorSourceIndex")]
    [DataRow("FParticleSubUVDynamicParameterVertexFactory", "AxisRotationVectors")]
    [DataRow("FParticleSubUVDynamicParameterVertexFactory", "ParticleUpRightResultScalars")]
    public void SpriteRequiredBindingsCannotBeTreatedAsOptimizerElimination(string name, string missing)
    {
        var binder = new LE3ShaderParameterBinder(Fields(false).Where(f => f != missing)
            .Select((f, i) => new ShaderParameterBinding(f, ShaderParameterBindingKind.Constant, 0, (ushort)(i * 16), 16, null)).ToArray());
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => binder.BindVertexFactoryParameters(name));
        StringAssert.Contains(error.Message, missing);
    }

    [TestMethod]
    [DataRow("FParticleVertexFactory", false)]
    [DataRow("FParticleVertexFactory", true)]
    [DataRow("FParticleDynamicParameterVertexFactory", false)]
    [DataRow("FParticleDynamicParameterVertexFactory", true)]
    [DataRow("FParticleSubUVVertexFactory", false)]
    [DataRow("FParticleSubUVVertexFactory", true)]
    [DataRow("FParticleSubUVDynamicParameterVertexFactory", false)]
    [DataRow("FParticleSubUVDynamicParameterVertexFactory", true)]
    public void SpriteArchivesKeepNativeElevenFieldOrderAndOptionalZeros(string name, bool optional)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ParticleArchive.pcc", MEGame.LE3);
        var allocations = Fields(optional).Select((f, i) => new ShaderParameterBinding(f, ShaderParameterBindingKind.Constant,
            (ushort)(i + 1), (ushort)(i * 16 + 3), (ushort)(16 + i), null)).ToArray();
        var binder = new LE3ShaderParameterBinder(allocations);
        var reference = new FVertexFactoryParameterRef { VertexFactoryType = name, Parameters = binder.BindVertexFactoryParameters(name) };
        Assert.IsInstanceOfType<FParticleVertexFactoryShaderParameters>(reference.Parameters);
        Assert.AreEqual(0, binder.UnboundParameters.Count);
        using var stream = new MemoryStream();
        reference.Serialize(new SerializingContainer(stream, package)); stream.Position = 0;
        var copy = new FVertexFactoryParameterRef(); copy.Serialize(new SerializingContainer(stream, package, isLoading: true));
        Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(name, copy.VertexFactoryType.Name);
        foreach (var field in Fields(true))
        {
            var parameter = (FShaderParameter)copy.Parameters.GetType().GetField(field).GetValue(copy.Parameters);
            var allocation = allocations.SingleOrDefault(a => a.Name == field);
            Assert.AreEqual(allocation?.BufferIndex ?? 0, parameter.BufferIndex, field);
            Assert.AreEqual(allocation?.BaseIndex ?? 0, parameter.BaseIndex, field);
            Assert.AreEqual(allocation?.Size ?? 0, parameter.NumBytes, field);
        }
        Assert.AreEqual(66L, stream.Length - 12, "Factory archive header is name plus end offset; eleven six-byte records follow.");
        stream.Position = 12;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        foreach (string field in Fields(true))
        {
            var allocation = allocations.SingleOrDefault(a => a.Name == field);
            Assert.AreEqual(allocation?.BaseIndex ?? 0, reader.ReadUInt16(), field);
            Assert.AreEqual(allocation?.Size ?? 0, reader.ReadUInt16(), field);
            Assert.AreEqual(allocation?.BufferIndex ?? 0, reader.ReadUInt16(), field);
        }
    }

    [TestMethod]
    [DataRow("FParticleVertexFactory")]
    [DataRow("FParticleDynamicParameterVertexFactory")]
    [DataRow("FParticleSubUVVertexFactory")]
    [DataRow("FParticleSubUVDynamicParameterVertexFactory")]
    public void TypedConstructionUsesSpriteLayoutNotLocalAndRetainsUniformAndProvenance(string name)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ParticleTyped.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", name, out var environment);
        var fields = Fields(false);
        var source = string.Concat(fields.Select(f => "float4 " + f + ";\n")) +
            "float4 Main(float4 p:POSITION):SV_Position { return p" + string.Concat(fields.Select(f => " + " + f)) + "; }";
        var snapshot = new LE3ShaderSourceSnapshot(new Dictionary<string, string> { ["MaterialTemplate.usf"] = Template(),
            [environment.Target.SourceFileName + ".usf"] = source, [environment.Target.VertexFactoryShaderFileName + ".usf"] = "// fixture" });
        var translation = compiler.TranslateMaterialHlsl(snapshot);
        var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
        var compiled = compiler.CompileShader(job, snapshot); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
        var result = compiler.ConstructKnownShader(translation, compiled); Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreSame(compiler.CompilationInput, compiled.CompilationInput);
        var shader = result.Shader;
        using var stream = new MemoryStream(); new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0;
        Shader copy = null; new SerializingContainer(stream, package, isLoading: true).Serialize(ref copy);
        Assert.AreEqual(stream.Length, stream.Position);
        var factoryRef = (FVertexFactoryParameterRef)copy.GetType().GetField("VertexFactoryParameters").GetValue(copy);
        Assert.IsInstanceOfType<FParticleVertexFactoryShaderParameters>(factoryRef.Parameters);
    }

    private static string[] Fields(bool optional) => optional
        ? ["CameraWorldPosition", "CameraRight", "CameraUp", "ScreenAlignment", "LocalToWorld", "AxisRotationVectorSourceIndex",
           "AxisRotationVectors", "ParticleUpRightResultScalars", "NormalsType", "NormalsSphereCenter", "NormalsCylinderUnitDirection"]
        : ["LocalToWorld", "AxisRotationVectorSourceIndex", "AxisRotationVectors", "ParticleUpRightResultScalars"];
    private static string Template() => "/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n*/";
    private static StructProperty Input(string name, int expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression, "Expression") }, name);
}
