using System;
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
public class LE3DecalVertexFactoryTests
{
    [TestMethod]
    [DataRow("FGPUSkinDecalVertexFactory")]
    [DataRow("FLocalDecalVertexFactory")]
    [DataRow("FGPUSkinMorphDecalVertexFactory")]
    public void CacheTruthTableSeparatesDebugFallbackAndFactorySpecificVetoes(string factoryName)
    {
        Assert.IsTrue(TryGetKnownVertexFactoryRegistration(factoryName, out var factory));
        for (int bits = 0; bits < 256; bits++)
        {
            bool Bit(int n) => (bits & (1 << n)) != 0;
            var context = Context() with { UsedWithDecals = Bit(0), IsSpecialEngineMaterial = Bit(1),
                UsedWithSkeletalMesh = Bit(2), UsedWithFracturedMeshes = Bit(3), IsTerrainMaterial = Bit(4),
                DecalFactoryFallback = Bit(5), UsedWithMorphTargets = Bit(7) };
            bool expected = (Bit(0) || Bit(5) || Bit(1) && Bit(6)) &&
                (factoryName != "FLocalDecalVertexFactory" ? Bit(2) || Bit(3) || Bit(1) : !Bit(4)) &&
                (factoryName != "FGPUSkinMorphDecalVertexFactory" || Bit(7) || Bit(1));
            Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(factoryName, context, factory.CreateCacheContext(),
                out bool actual, new ShouldCacheEnvironmentContext(Bit(6))));
            Assert.AreEqual(expected, actual, factoryName + ": bits=" + bits);
        }
    }

    [TestMethod]
    [DataRow("FGPUSkinDecalVertexFactory")]
    [DataRow("FLocalDecalVertexFactory")]
    [DataRow("FGPUSkinMorphDecalVertexFactory")]
    public void UnknownDerivedFallbackIsUnresolvedOnlyWhenItCanAffectEligibility(string factoryName)
    {
        Assert.IsTrue(TryGetKnownVertexFactoryRegistration(factoryName, out var factory));
        var context = Context() with { UsedWithSkeletalMesh = true, UsedWithMorphTargets = true, DecalFactoryFallback = null };
        Assert.IsFalse(TryEvaluateKnownVertexFactoryShouldCache(factoryName, context, factory.CreateCacheContext(), out bool actual));
        Assert.IsFalse(actual);
        Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(factoryName, context with { UsedWithDecals = true },
            factory.CreateCacheContext(), out actual, new(false))); Assert.IsTrue(actual);
        Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(factoryName, context with { IsSpecialEngineMaterial = true },
            factory.CreateCacheContext(), out actual, new(true))); Assert.IsTrue(actual);
        Assert.IsFalse(TryEvaluateKnownVertexFactoryShouldCache(factoryName, context with { IsSpecialEngineMaterial = true },
            factory.CreateCacheContext(), out actual, new(false))); Assert.IsFalse(actual);
        var veto = factoryName != "FLocalDecalVertexFactory" ? context with { UsedWithSkeletalMesh = false } : context with { IsTerrainMaterial = true };
        Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache(factoryName, veto, factory.CreateCacheContext(), out actual)); Assert.IsFalse(actual);
    }

    [TestMethod]
    [DataRow("FGPUSkinDecalVertexFactory")]
    [DataRow("FLocalDecalVertexFactory")]
    [DataRow("FGPUSkinMorphDecalVertexFactory")]
    public void UnknownFallbackWithholdsPartialPlanAndExplicitFalseIsResolvedExclusion(string factoryName)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DecalFallbackPlan.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var translation = compiler.TranslateMaterialHlsl(Template());
        Assert.IsTrue(TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", factoryName, out var decal));
        Assert.IsTrue(TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", "FLocalVertexFactory", out var local));
        foreach (bool? fallback in new bool?[] { null, false, true })
        {
            var context = Context() with { UsedWithSkeletalMesh = true, UsedWithMorphTargets = true, DecalFactoryFallback = fallback };
            var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(),
                [local.CreateCandidate(), decal.CreateCandidate()], context);
            var decision = plan.Decisions.Single(d => d.Target.VertexFactoryTypeName == factoryName);
            if (fallback == null)
            {
                Assert.IsFalse(plan.Success); Assert.AreEqual(0, plan.Jobs.Count);
                Assert.AreEqual(ShaderCompileDisposition.Unresolved, decision.Disposition);
                Assert.AreEqual(ShaderCompileGate.VertexFactoryShouldCache, decision.Gate);
            }
            else
            {
                Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(fallback.Value ? 2 : 1, plan.Jobs.Count);
                Assert.AreEqual(fallback.Value ? ShaderCompileDisposition.Eligible : ShaderCompileDisposition.Excluded, decision.Disposition);
            }
        }
    }

    [TestMethod]
    [DataRow("FGPUSkinDecalVertexFactory")]
    [DataRow("FLocalDecalVertexFactory")]
    [DataRow("FGPUSkinMorphDecalVertexFactory")]
    public void PlannerForwardsEngineDebugGateForSpecialNonDecalMaterials(string factoryName)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DecalDebugPlan.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(true, "bUsedAsSpecialEngineMaterial"));
        var compiler = new LE3MaterialShaderCompiler(material);
        Assert.IsFalse(compiler.BuildShouldCacheMaterialContext().DecalFactoryFallback.Value);
        Assert.IsTrue(TryGetKnownPermutationEnvironment("TDepthOnlyVertexShader<0>", factoryName, out var environment));
        var translation = compiler.TranslateMaterialHlsl(Template());
        foreach (bool debug in new[] { false, true })
        {
            var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), [environment.CreateCandidate()],
                environmentContext: new(debug));
            Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(debug ? 1 : 0, plan.Jobs.Count);
        }
    }

    [TestMethod]
    [DataRow("FGPUSkinDecalVertexFactory", false, "GPUSKIN_DECAL")]
    [DataRow("FLocalDecalVertexFactory", true, "DECAL_FACTORY")]
    [DataRow("FGPUSkinMorphDecalVertexFactory", false, "GPUSKIN_DECAL")]
    public void EnvironmentsKeepNativeMacrosAndShaderClientWorldCoordsEvidenceSeparate(string factoryName, bool local, string macro)
    {
        Assert.IsTrue(TryGetKnownVertexFactoryRegistration(factoryName, out var factory));
        Assert.AreEqual(local, factory.SupportsStaticLighting); Assert.IsTrue(factory.SupportsDynamicLighting.Value);
        Assert.IsFalse(factory.SupportsPrecisePreviousPosition.Value); Assert.IsTrue(factory.UsedWithMaterials);
        foreach (var shader in GetKnownShaderTypeRegistrations().Where(s => s.IsMeshMaterial))
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(shader.Name, factoryName, out var environment));
            Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.CompileFlagsEvidence);
            Assert.AreEqual("1", environment.CreateCandidate().EnvironmentMacros[macro]);
            Assert.AreEqual(ShaderEnvironmentEvidence.LE3RegistrationCallback, environment.MacroDefinitions.Single(m => m.Name == macro).Evidence);
            Assert.AreEqual(!local, environment.CreateCandidate().EnvironmentMacros.ContainsKey("QUAT_SKINNING"));
            if (local)
            {
                var world = environment.MacroDefinitions.Single(m => m.Name == "WORLD_COORDS");
                Assert.AreEqual("1", world.Definition); Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, world.Evidence);
            }
            else Assert.AreEqual("0", environment.CreateCandidate().EnvironmentMacros["QUAT_SKINNING"]);
            Assert.AreEqual(factoryName == "FGPUSkinMorphDecalVertexFactory",
                environment.CreateCandidate().EnvironmentMacros.ContainsKey("GPUSKIN_MORPH_BLEND"));
        }
    }

    [TestMethod]
    public void LocalDecalBinderAllowsAllFieldsToBeOptimizedOut()
    {
        var binder = new LE3ShaderParameterBinder([]);
        var parameters = (FLocalDecalVertexFactoryShaderParameters)binder.BindVertexFactoryParameters("FLocalDecalVertexFactory");
        Assert.AreEqual(default(FShaderParameter), parameters.LocalToWorld);
        Assert.AreEqual(default(FShaderParameter), parameters.DecalMatrix);
        Assert.AreEqual(default(FShaderParameter), parameters.DecalBlendInterval);
        Assert.AreEqual(0, binder.UnboundParameters.Count);
    }

    private static ShouldCacheMaterialContext Context() => new(false, false, false, false, false, false, false, false);
    private static string Template() => "/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n*/";
}
