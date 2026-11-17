using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3ObservedShaderPlanningTests
{
    [TestMethod]
    [DataRow("ordinary")]
    [DataRow("special")]
    [DataRow("decal")]
    [DataRow("dynamicParticles")]
    [DataRow("nonmesh")]
    public void ObservedPlanAccountsForBothNonCompilableRegistrationsWithoutChangingSelectedJobs(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ObservedPlanning.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", shape == "decal" ? "DecalMaterial" : "Material", indexed: false);
        material.WriteProperty(new BoolProperty(shape == "special", "bUsedAsSpecialEngineMaterial"));
        if (shape == "dynamicParticles")
        {
            var node = package.CreateExport("Dynamic", "MaterialExpressionDynamicParameter", material, indexed: false);
            material.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(node.UIndex, "Expression") }, "EmissiveColor"));
            material.WriteProperty(new BoolProperty(true, "bUsedWithParticleSprites"));
            material.WriteProperty(new BoolProperty(true, "bUsedWithParticleSubUV"));
            material.WriteProperty(new BoolProperty(true, "bUsedWithBeamTrails"));
        }
        if (shape == "nonmesh")
        {
            material.WriteProperty(new BoolProperty(true, "bUsedAsLightFunction"));
            material.WriteProperty(new BoolProperty(true, "bUsedWithRadialBlur"));
        }
        var compiler = new LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler(material);
        var translation = compiler.TranslateMaterialHlsl(Template());
        var known = compiler.BuildKnownSubsetCompilePlan(translation, MaterialResource.Create());
        var observed = compiler.BuildObservedSubsetCompilePlan(translation, MaterialResource.Create());
        Assert.IsTrue(known.Success, string.Join("; ", known.Errors)); Assert.IsTrue(observed.Success, string.Join("; ", observed.Errors));
        Assert.AreEqual(4244, known.Decisions.Count); Assert.AreEqual(4558, observed.Decisions.Count);
        CollectionAssert.AreEqual(known.Jobs.Select(j => j.Target).ToArray(), observed.Jobs.Select(j => j.Target).ToArray());
        Assert.AreEqual(known.ExcludedTargets.Count + 314, observed.ExcludedTargets.Count);
        Assert.AreSame(compiler.CompilationInput, observed.CompilationInput);
        Assert.IsTrue(observed.Jobs.All(j => ReferenceEquals(compiler.CompilationInput, j.CompilationInput)));
        var shadow = observed.Decisions.Where(d => d.Target.VertexFactoryTypeName == "FLocalShadowVertexFactory").ToArray();
        Assert.AreEqual(157, shadow.Length);
        Assert.IsTrue(shadow.All(d => d.Disposition == ShaderCompileDisposition.Excluded && d.Gate == ShaderCompileGate.VertexFactoryUsage));
        var bio = observed.Decisions.Where(d => d.Target.VertexFactoryTypeName == "FBioSPUSkinVertexFactory").ToArray();
        Assert.AreEqual(157, bio.Length); Assert.IsTrue(bio.All(d => d.Disposition == ShaderCompileDisposition.Excluded));
        Assert.IsTrue(bio.Any(d => d.Gate == ShaderCompileGate.VertexFactoryShouldCache));
        Assert.IsFalse(observed.Jobs.Any(j => j.Target.VertexFactoryTypeName is "FBioSPUSkinVertexFactory" or "FLocalShadowVertexFactory"));
        var comparison = ComparePlannedPermutations(observed, known.Jobs.Select(j => new ShaderPermutationIdentity(j.Target.ShaderTypeName, j.Target.VertexFactoryTypeName)).ToArray());
        Assert.IsTrue(comparison.MatchesExpectedPermutations); Assert.IsFalse(comparison.IsCompleteShaderSet);
    }

    [TestMethod]
    public void ObservedResourceGateIsNotSkippedForAnAlwaysFalseFactoryAndFailureWithholdsPartialJobs()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ObservedResourceOrder.pcc", MEGame.LE3);
        var compiler = new LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var translation = compiler.TranslateMaterialHlsl(Template());
        int bioCalls = 0;
        var plan = compiler.BuildObservedSubsetCompilePlan(translation, MaterialResource.Create(), materialResourceShouldCache: (input, target) =>
        {
            Assert.AreSame(compiler.CompilationInput, input);
            if (target.VertexFactoryTypeName == "FLocalShadowVertexFactory") Assert.Fail("Non-material factory must be filtered before resource gate.");
            if (target.VertexFactoryTypeName == "FBioSPUSkinVertexFactory") { bioCalls++; return null; }
            return true;
        });
        Assert.IsTrue(bioCalls > 0); Assert.IsFalse(plan.Success); Assert.AreEqual(0, plan.Jobs.Count);
        Assert.IsTrue(plan.Decisions.Any(d => d.Disposition == ShaderCompileDisposition.Eligible));
        Assert.IsTrue(plan.Decisions.Where(d => d.Disposition == ShaderCompileDisposition.Unresolved)
            .All(d => d.Gate == ShaderCompileGate.MaterialResourceShouldCache && d.Target.VertexFactoryTypeName == "FBioSPUSkinVertexFactory"));
        Assert.IsFalse(ComparePlannedPermutations(plan, []).MatchesExpectedPermutations);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitUnresolvedEnvironmentCanOnlyProduceAnExclusionNeverAJob(bool otherwiseEligible)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3UnknownEnvironment.pcc", MEGame.LE3);
        var compiler = new LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", "FLocalVertexFactory", out var local);
        TryGetKnownPermutationEnvironment("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", "FFoliageVertexFactory", out var foliage);
        var missing = foliage.CreateCandidate() with { IsCompilationEnvironmentResolved = false };
        var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(Template()), MaterialResource.Create(),
            [local.CreateCandidate(), missing], new ShouldCacheMaterialContext(false, false, false, false, false, false, false, false)
                { UsedWithFoliage = otherwiseEligible });
        Assert.AreEqual(!otherwiseEligible, plan.Success); Assert.AreEqual(otherwiseEligible ? 0 : 1, plan.Jobs.Count);
        var decision = plan.Decisions.Single(d => d.Target.VertexFactoryTypeName == "FFoliageVertexFactory");
        Assert.AreEqual(otherwiseEligible ? ShaderCompileDisposition.Unresolved : ShaderCompileDisposition.Excluded, decision.Disposition);
        Assert.AreEqual(otherwiseEligible ? ShaderCompileGate.CompilationEnvironment : ShaderCompileGate.VertexFactoryShouldCache, decision.Gate);
        Assert.IsTrue(local.CreateCandidate().IsCompilationEnvironmentResolved, "Existing callers keep their explicit environment contract.");
    }

    [TestMethod]
    public void ObservedPlannerDoesNotAcceptHalfSm5OrMutateCookedResourceDynamicUsage()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ObservedHalf.pcc", MEGame.LE3);
        var compiler = new LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var resource = MaterialResource.Create(); resource.bUsesDynamicParameter = true;
        var plan = compiler.BuildObservedSubsetCompilePlan(compiler.TranslateMaterialHlsl(Template(), true), resource);
        Assert.IsFalse(plan.Success); Assert.AreEqual(0, plan.Jobs.Count); Assert.AreEqual(0, plan.Decisions.Count);
        Assert.IsTrue(resource.bUsesDynamicParameter);
    }

    private static string Template() => "/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n*/";
}
