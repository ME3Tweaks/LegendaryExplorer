using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

/// <summary>
/// ShaderCompilePlan.CoversNativeRequiredSet and the IsCompleteShaderSet flags of the map/cache
/// fragments. A plan covers the native required set only for the known/observed registry planners
/// with a successful plan, a debug-viewmode-allowing (or default) environment, no caller resource
/// predicate and a resource translation. Zero-job native plans are unreachable for ordinary
/// materials (the smallest real set is two jobs), so complete empty fragments are exercised by
/// setting the internal flag on an explicit empty plan through reflection; that isolates fragment
/// propagation from planner behaviour.
/// </summary>
[TestClass]
public class LE3ShaderSetCompletenessTests
{
    [TestMethod]
    [DataRow("known")]
    [DataRow("observed")]
    public void OrdinaryResourceTranslationOnTheDefaultContextCoversTheNativeSet(string planner)
    {
        var (compiler, translation) = Ordinary("OrdinaryNative");
        var plan = Plan(planner, compiler, translation);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors));
        Assert.IsTrue(plan.Jobs.Count > 0);
        Assert.IsTrue(plan.CoversNativeRequiredSet);
    }

    [TestMethod]
    [DataRow("known")]
    [DataRow("observed")]
    public void ExplicitDefaultEnvironmentContextStillCoversTheNativeSet(string planner)
    {
        var (compiler, translation) = Ordinary("ExplicitEnvironment");
        var implicitPlan = Plan(planner, compiler, translation);
        var explicitPlan = Plan(planner, compiler, translation, environment: new ShouldCacheEnvironmentContext());
        var explicitTrue = Plan(planner, compiler, translation, environment: new ShouldCacheEnvironmentContext(true));
        Assert.IsTrue(explicitPlan.CoversNativeRequiredSet); Assert.IsTrue(explicitTrue.CoversNativeRequiredSet);
        CollectionAssert.AreEqual(implicitPlan.Jobs.Select(j => j.Target).ToArray(), explicitPlan.Jobs.Select(j => j.Target).ToArray());
    }

    [TestMethod]
    [DataRow("known")]
    [DataRow("observed")]
    public void DisallowedDebugViewModesAreNotTheShippingContextAndNeverCover(string planner)
    {
        var (compiler, translation) = Ordinary("NoDebugViewModes");
        var shipping = Plan(planner, compiler, translation);
        var noDebug = Plan(planner, compiler, translation, environment: new ShouldCacheEnvironmentContext(false));
        Assert.IsTrue(noDebug.Success, string.Join("; ", noDebug.Errors));
        Assert.IsTrue(noDebug.Jobs.Count < shipping.Jobs.Count, "The debug viewmode shaders must drop out of the selected set.");
        Assert.IsFalse(noDebug.CoversNativeRequiredSet);
        Assert.IsTrue(shipping.CoversNativeRequiredSet);
    }

    [TestMethod]
    [DataRow("known", "allTrue")]
    [DataRow("known", "allFalse")]
    [DataRow("known", "unresolved")]
    [DataRow("observed", "allTrue")]
    [DataRow("observed", "allFalse")]
    [DataRow("observed", "unresolved")]
    public void AnyCallerResourcePredicateWithholdsNativeCoverage(string planner, string callbackShape)
    {
        var (compiler, translation) = Ordinary("ResourceCallback");
        Func<ResolvedMaterialCompilationInput, ShaderCompileTarget, bool?> callback = callbackShape switch
        {
            "allTrue" => (_, _) => true,
            "allFalse" => (_, _) => false,
            _ => (_, _) => null
        };
        int calls = 0;
        var plan = Plan(planner, compiler, translation, callback: (input, target) => { calls++; return callback(input, target); });
        Assert.IsTrue(calls > 0);
        Assert.IsFalse(plan.CoversNativeRequiredSet);
        Assert.AreEqual(callbackShape != "unresolved", plan.Success, string.Join("; ", plan.Errors));
        if (callbackShape == "allTrue")
        {
            // Same selection as the uncalled plan: only the certification is withheld, not the planning.
            var native = Plan(planner, compiler, translation);
            CollectionAssert.AreEqual(native.Jobs.Select(j => j.Target).ToArray(), plan.Jobs.Select(j => j.Target).ToArray());
        }
        if (callbackShape == "allFalse") Assert.AreEqual(0, plan.Jobs.Count);
    }

    [TestMethod]
    [DataRow("known")]
    [DataRow("observed")]
    public void LegacyAuthoredRootTranslationNeverCoversTheNativeSet(string planner)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CompletenessLegacy.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var legacy = compiler.TranslateMaterialHlsl(Template());
        Assert.IsFalse(legacy.IncludesMaterialResourceInputs);
        var plan = Plan(planner, compiler, legacy);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.IsTrue(plan.Jobs.Count > 0);
        Assert.IsFalse(plan.CoversNativeRequiredSet);
        // The same material through the resource translation is certified, so the translation kind is the only difference.
        var resource = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(resource.IncludesMaterialResourceInputs);
        var resourcePlan = Plan(planner, compiler, resource);
        Assert.IsTrue(resourcePlan.CoversNativeRequiredSet);
        CollectionAssert.AreEqual(plan.Jobs.Select(j => j.Target).ToArray(), resourcePlan.Jobs.Select(j => j.Target).ToArray());
    }

    [TestMethod]
    [DataRow("known")]
    [DataRow("observed")]
    public void HalfTypeEarlyReturnPlanIsFailedAndNotCovering(string planner)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CompletenessHalf.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        // Resource translations are always float; only the legacy path can request half types.
        var half = compiler.TranslateMaterialHlsl(Template(), useHalfTypes: true);
        Assert.IsTrue(half.UsesHalfTypes);
        var plan = Plan(planner, compiler, half);
        Assert.IsFalse(plan.Success); Assert.AreEqual(0, plan.Jobs.Count); Assert.AreEqual(0, plan.Decisions.Count);
        Assert.IsFalse(plan.CoversNativeRequiredSet);
        Assert.IsFalse(compiler.TranslateMaterialResourceHlsl(Template()).UsesHalfTypes);
    }

    [TestMethod]
    [DataRow("known")]
    [DataRow("observed")]
    public void ExplicitCandidatePlansNeverCoverEvenForAnExhaustiveCandidateList(string planner)
    {
        var (compiler, translation) = Ordinary("ProvisionalNever");
        var native = Plan(planner, compiler, translation);
        // Same registry inventory the native planner walks, but supplied as explicit candidates.
        var explicitCandidates = GetKnownShaderTypeRegistrations().SelectMany(shader => shader.IsMeshMaterial
            ? GetKnownVertexFactoryRegistrations().Select(factory => TryGetKnownPermutationEnvironment(shader.Name, factory.Name, out var e) ? e.CreateCandidate() : null)
            : [TryGetKnownPermutationEnvironment(shader.Name, null, out var n) ? n.CreateCandidate() : null]).Where(c => c != null).ToArray();
        Assert.IsTrue(explicitCandidates.Length > 0);
        var provisional = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), explicitCandidates);
        Assert.IsTrue(provisional.Success, string.Join("; ", provisional.Errors));
        Assert.IsFalse(provisional.CoversNativeRequiredSet);
        // Identical job selection to the known planner proves coverage is a provenance claim, not a job-set property.
        if (planner == "known")
            CollectionAssert.AreEquivalent(native.Jobs.Select(j => j.Target).ToArray(), provisional.Jobs.Select(j => j.Target).ToArray());
        Assert.IsTrue(native.CoversNativeRequiredSet);
        Assert.IsFalse(compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), []).CoversNativeRequiredSet);
        Assert.IsFalse(compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), explicitCandidates,
            environmentContext: new ShouldCacheEnvironmentContext(true)).CoversNativeRequiredSet);
    }

    [TestMethod]
    [DataRow("known")]
    [DataRow("observed")]
    public void EffectsOwnerPlanResolvesNativelyAndCoversTheNativeSet(string planner)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CompletenessEffectsOwner.pcc", MEGame.LE3);
        var owner = package.CreateExport("Owner", "RvrEffectsMaterialUser", indexed: false);
        var effectsMaterial = package.CreateExport("Material", "Material", owner, indexed: false);
        effectsMaterial.WriteProperty(new BoolProperty(true, "bUsedWithSkeletalMesh"));
        var compiler = new LE3MaterialShaderCompiler(effectsMaterial);
        var translation = compiler.TranslateMaterialResourceHlsl(Template());
        var plan = Plan(planner, compiler, translation);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.IsTrue(plan.Jobs.Count > 0);
        Assert.IsFalse(plan.Decisions.Any(d => d.Disposition == ShaderCompileDisposition.Unresolved));
        Assert.IsTrue(plan.CoversNativeRequiredSet);
        // m_bSupportsStaticMeshes absent/false drops exactly the FLocalVertexFactory permutations; true keeps them.
        Assert.IsFalse(plan.Jobs.Any(j => j.Target.VertexFactoryTypeName == "FLocalVertexFactory"));
        Assert.IsTrue(plan.Decisions.Any(d => d.Gate == ShaderCompileGate.MaterialResourceShouldCache && d.Disposition == ShaderCompileDisposition.Excluded));
        owner.WriteProperty(new BoolProperty(true, "m_bSupportsStaticMeshes"));
        var supported = Plan(planner, compiler, translation);
        Assert.IsTrue(supported.Success, string.Join("; ", supported.Errors)); Assert.IsTrue(supported.CoversNativeRequiredSet);
        Assert.IsTrue(supported.Jobs.Any(j => j.Target.VertexFactoryTypeName == "FLocalVertexFactory"));
        Assert.IsTrue(supported.Jobs.Count > plan.Jobs.Count);
        // A caller-supplied predicate is still a caller claim, never native coverage.
        var resolved = Plan(planner, compiler, translation, callback: (_, _) => true);
        Assert.IsTrue(resolved.Success, string.Join("; ", resolved.Errors)); Assert.IsTrue(resolved.Jobs.Count > 0);
        Assert.IsFalse(resolved.CoversNativeRequiredSet);
    }

    [TestMethod]
    [DataRow("known")]
    [DataRow("observed")]
    public void FailedTranslationCannotBePlannedAtAll(string planner)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CompletenessFailedTranslation.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var failed = compiler.TranslateMaterialResourceHlsl("not a material template");
        Assert.IsFalse(failed.Success);
        Assert.ThrowsExactly<InvalidOperationException>(() => Plan(planner, compiler, failed));
    }

    [TestMethod]
    [DataRow("plain")]
    [DataRow("unlitModulate")]
    [DataRow("lightFunction")]
    [DataRow("decal")]
    [DataRow("translucentDistorted")]
    public void KnownAndObservedPlannersAgreeOnCompleteness(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CompletenessAgreement.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", shape == "decal" ? "DecalMaterial" : "Material", indexed: false);
        if (shape == "unlitModulate")
        {
            material.WriteProperty(new EnumProperty("MLM_Unlit", "EMaterialLightingModel", MEGame.LE3, "LightingModel"));
            material.WriteProperty(new EnumProperty("BLEND_Modulate", "EBlendMode", MEGame.LE3, "BlendMode"));
        }
        if (shape == "lightFunction") material.WriteProperty(new BoolProperty(true, "bUsedAsLightFunction"));
        if (shape == "translucentDistorted")
        {
            material.WriteProperty(new EnumProperty("BLEND_Translucent", "EBlendMode", MEGame.LE3, "BlendMode"));
            material.WriteProperty(new BoolProperty(true, "bUsesDistortion"));
        }
        var compiler = new LE3MaterialShaderCompiler(material);
        var translation = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
        var known = Plan("known", compiler, translation); var observed = Plan("observed", compiler, translation);
        Assert.IsTrue(known.Success, string.Join("; ", known.Errors)); Assert.IsTrue(observed.Success, string.Join("; ", observed.Errors));
        Assert.AreEqual(known.CoversNativeRequiredSet, observed.CoversNativeRequiredSet);
        Assert.IsTrue(known.CoversNativeRequiredSet);
        CollectionAssert.AreEqual(known.Jobs.Select(j => j.Target).ToArray(), observed.Jobs.Select(j => j.Target).ToArray());
        // The observed inventory only adds exclusions, so a callback degrades both identically.
        Assert.IsFalse(Plan("known", compiler, translation, callback: (_, _) => true).CoversNativeRequiredSet);
        Assert.IsFalse(Plan("observed", compiler, translation, callback: (_, _) => true).CoversNativeRequiredSet);
    }

    [TestMethod]
    [DataRow("known")]
    [DataRow("observed")]
    public void CapturedGlassMembershipIsReproducedByTheNativePlannerAndCovers(string planner)
    {
        // Same property snapshot and expected 25-permutation membership as
        // KnownPlannerInventoryReproducesCapturedGlassCandidateSubset, but through the registry planner
        // and the resource translation, with no resource callback.
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CompletenessGlass.pcc", MEGame.LE3);
        var material = package.CreateExport("GlassState", "Material", indexed: false);
        material.WriteProperty(new EnumProperty("MLM_Unlit", "EMaterialLightingModel", MEGame.LE3, "LightingModel"));
        material.WriteProperty(new EnumProperty("BLEND_Translucent", "EBlendMode", MEGame.LE3, "BlendMode"));
        foreach (string flag in new[] { "bUsesDistortion", "bUsedWithSkeletalMesh", "bUsedWithStaticLighting",
            "bUseLitTranslucencyPostRenderDepthPass", "bCastLitTranslucencyShadowAsMasked" })
            material.WriteProperty(new BoolProperty(true, flag));
        var compiler = new LE3MaterialShaderCompiler(material);
        var translation = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
        var plan = Plan(planner, compiler, translation);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors));
        Assert.AreEqual(25, plan.Jobs.Count);
        Assert.IsTrue(plan.CoversNativeRequiredSet);
        string[] common =
        [
            "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight", "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy",
            "TDepthOnlyVertexShader<0>", "FTranslucencyPostRenderDepthPixelShader",
            "TDistortionMeshVertexShader<FDistortMeshAccumulatePolicy>", "TDistortionMeshPixelShader<FDistortMeshAccumulatePolicy>",
            "TShadowDepthVertexShader<ShadowDepth_OutputDepth>", "TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>",
            "TShadowDepthVertexShader<ShadowDepth_OutputDepthToColor>", "TShadowDepthPixelShaderTRUEFALSE", "TShadowDepthPixelShaderFALSEFALSE"
        ];
        string[] localOnly =
        [
            "TBasePassVertexShaderFNoLightMapPolicyFConstantDensityPolicy",
            "TBasePassVertexShaderFNoLightMapPolicyFLinearHalfspaceDensityPolicy",
            "TBasePassVertexShaderFNoLightMapPolicyFSphereDensityPolicy"
        ];
        var expected = common.Concat(localOnly).Select(n => new ShaderPermutationIdentity(n, "FLocalVertexFactory"))
            .Concat(common.Select(n => new ShaderPermutationIdentity(n, "FGPUSkinVertexFactory"))).ToArray();
        var comparison = ComparePlannedPermutations(plan, expected);
        Assert.IsTrue(comparison.MatchesExpectedPermutations);
        Assert.IsFalse(comparison.IsCompleteShaderSet, "A membership comparison never certifies completeness itself.");
        // With a resource predicate the membership is unchanged but coverage is withheld.
        var called = Plan(planner, compiler, translation, callback: (_, _) => true);
        Assert.IsTrue(ComparePlannedPermutations(called, expected).MatchesExpectedPermutations);
        Assert.IsFalse(called.CoversNativeRequiredSet);
    }

    [TestMethod]
    [DataRow("known")]
    [DataRow("observed")]
    public void ZeroJobCallbackPlanProducesAnIncompleteFragment(string planner)
    {
        var (compiler, translation) = Ordinary("ExcludedByCallback");
        var plan = Plan(planner, compiler, translation, callback: (_, _) => false);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(0, plan.Jobs.Count);
        Assert.IsTrue(plan.Decisions.Count > 0); Assert.IsFalse(plan.CoversNativeRequiredSet);
        AssertFragments(compiler, translation, plan, success: true, complete: false);
    }

    [TestMethod]
    [DataRow("known")]
    [DataRow("observed")]
    public void DefaultContextPlanWithoutAllJobsCompiledIsNotAFragmentAndNotComplete(string planner)
    {
        var (compiler, translation) = Ordinary("MissingJobs");
        var plan = Plan(planner, compiler, translation);
        Assert.IsTrue(plan.CoversNativeRequiredSet); Assert.IsTrue(plan.Jobs.Count > 0);
        // A covering plan only yields a complete fragment when every job is compiled exactly once.
        var map = compiler.BuildShaderMapFragment(translation, plan, [], Guid.NewGuid(), "Missing");
        Assert.IsFalse(map.Success); Assert.IsNull(map.Map); Assert.IsFalse(map.IsCompleteShaderSet);
        StringAssert.Contains(string.Join("; ", map.Errors), "Missing compiled shader job");
        var cache = compiler.BuildShaderCacheFragment(translation, plan, [], Guid.NewGuid(), "Missing", translation.SourceSnapshot ?? new LE3ShaderSourceSnapshot(
            new Dictionary<string, string> { ["MaterialTemplate.usf"] = Template() }));
        Assert.IsFalse(cache.Success); Assert.IsFalse(cache.IsCompleteShaderSet);
    }

    [TestMethod]
    public void CompleteZeroJobFragmentsReportCompletenessAndCarryItThroughTheCache()
    {
        var (compiler, translation, sources) = OrdinarySnapshot("CompleteEmpty");
        var plan = Covering(compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), []));
        Assert.IsTrue(plan.Success); Assert.AreEqual(0, plan.Jobs.Count); Assert.IsTrue(plan.CoversNativeRequiredSet);
        var map = compiler.BuildShaderMapFragment(translation, plan, [], Guid.NewGuid(), "CompleteEmpty");
        Assert.IsTrue(map.Success, string.Join("; ", map.Errors)); Assert.IsTrue(map.IsCompleteShaderSet);
        Assert.AreEqual(0, map.Shaders.Count);
        var cache = compiler.BuildShaderCacheFragment(translation, plan, [], Guid.NewGuid(), "CompleteEmpty", sources);
        Assert.IsTrue(cache.Success, string.Join("; ", cache.Errors)); Assert.IsTrue(cache.IsCompleteShaderSet);
        Assert.AreEqual(0, cache.Cache.Shaders.Count);
        // The checked-snapshot overload and the CRC-session overload agree.
        var viaSession = compiler.BuildShaderCacheFragment(translation, plan, [], Guid.NewGuid(), "CompleteEmpty", sources.CreateCrcSession());
        Assert.IsTrue(viaSession.Success); Assert.IsTrue(viaSession.IsCompleteShaderSet);
        // The same empty plan without the flag is a plain explicit subset.
        var plain = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), []);
        Assert.IsFalse(compiler.BuildShaderMapFragment(translation, plain, [], Guid.NewGuid(), "Explicit").IsCompleteShaderSet);
    }

    [TestMethod]
    [DataRow("map")]
    [DataRow("cache")]
    public void CoveringFlagDoesNotSurviveFragmentFailures(string kind)
    {
        var (compiler, translation, sources) = OrdinarySnapshot("CompleteFailure");
        var good = Covering(compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), []));
        // A failed plan cannot reach the fragment even if the flag were forced on.
        var failedPlan = Covering(compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(),
            [new ShaderCompileCandidate(null)]));
        Assert.IsFalse(failedPlan.Success); Assert.IsTrue(failedPlan.CoversNativeRequiredSet);
        AssertFragments(compiler, translation, failedPlan, success: false, complete: false, sources);
        // An unexpected compiled entry fails assembly of an otherwise-complete empty plan.
        foreach (var extra in new ShaderCompilationResult[] { null, new() })
        {
            if (kind == "map")
            {
                var map = compiler.BuildShaderMapFragment(translation, good, [extra], Guid.NewGuid(), "Unexpected");
                Assert.IsFalse(map.Success); Assert.IsNull(map.Map); Assert.IsFalse(map.IsCompleteShaderSet);
            }
            else
            {
                var cache = compiler.BuildShaderCacheFragment(translation, good, [extra], Guid.NewGuid(), "Unexpected",
                    new LE3ShaderSourceCrc(_ => throw new InvalidOperationException("Reject results before reading sources.")));
                Assert.IsFalse(cache.Success); Assert.IsNull(cache.Cache); Assert.IsFalse(cache.IsCompleteShaderSet);
            }
        }
        // A foreign snapshot fails the cache after the map would have succeeded.
        if (kind == "cache")
        {
            var wrong = compiler.BuildShaderCacheFragment(translation, good, [], Guid.NewGuid(), "WrongSnapshot", MinimalSources());
            Assert.IsFalse(wrong.Success); Assert.IsFalse(wrong.IsCompleteShaderSet);
            // A CRC read failure only matters when a job exists, so the empty complete plan stays complete.
            var noReads = compiler.BuildShaderCacheFragment(translation, good, [], Guid.NewGuid(), "NoReads",
                new LE3ShaderSourceCrc(_ => throw new InvalidOperationException("An empty plan reads no sources.")));
            Assert.IsTrue(noReads.Success); Assert.IsTrue(noReads.IsCompleteShaderSet);
        }
    }

    [TestMethod]
    public void ComparisonResultsNeverClaimCompletenessEvenForACoveringPlan()
    {
        var (compiler, translation, _) = OrdinarySnapshot("ComparisonsStayFalse");
        var native = Plan("known", compiler, translation);
        Assert.IsTrue(native.CoversNativeRequiredSet);
        var exact = ComparePlannedPermutations(native, native.Jobs.Select(j => new ShaderPermutationIdentity(
            j.Target.ShaderTypeName, j.Target.VertexFactoryTypeName)).ToArray());
        Assert.IsTrue(exact.MatchesExpectedPermutations); Assert.IsFalse(exact.IsCompleteShaderSet);
        Assert.IsFalse(ComparePlannedPermutations(native, []).IsCompleteShaderSet);
        // Uniform comparison against the map of a complete empty fragment.
        var empty = Covering(compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), []));
        var map = compiler.BuildShaderMapFragment(translation, empty, [], Guid.NewGuid(), "Uniforms");
        Assert.IsTrue(map.IsCompleteShaderSet);
        var uniforms = CompareUniformExpressionsWithShaderMap(translation, map.Map);
        Assert.IsTrue(uniforms.MatchesExpectedUniformExpressions, string.Join("; ", uniforms.Errors.Concat(uniforms.Differences)));
        Assert.IsFalse(uniforms.IsCompleteShaderSet);
    }

    private static void AssertFragments(LE3MaterialShaderCompiler compiler, TranslationResult translation, ShaderCompilePlan plan,
        bool success, bool complete, LE3ShaderSourceSnapshot sources = null)
    {
        sources ??= translation.SourceSnapshot;
        var map = compiler.BuildShaderMapFragment(translation, plan, [], Guid.NewGuid(), "Fragment");
        Assert.AreEqual(success, map.Success, string.Join("; ", map.Errors)); Assert.AreEqual(complete, map.IsCompleteShaderSet);
        Assert.AreEqual(success, map.Map != null);
        var cache = compiler.BuildShaderCacheFragment(translation, plan, [], Guid.NewGuid(), "Fragment", sources);
        Assert.AreEqual(success, cache.Success, string.Join("; ", cache.Errors)); Assert.AreEqual(complete, cache.IsCompleteShaderSet);
        Assert.AreEqual(success, cache.Cache != null);
    }

    private static ShaderCompilePlan Covering(ShaderCompilePlan plan)
    {
        typeof(ShaderCompilePlan).GetProperty(nameof(ShaderCompilePlan.CoversNativeRequiredSet))!.SetValue(plan, true);
        return plan;
    }

    private static ShaderCompilePlan Plan(string planner, LE3MaterialShaderCompiler compiler, TranslationResult translation,
        ShouldCacheEnvironmentContext environment = null, Func<ResolvedMaterialCompilationInput, ShaderCompileTarget, bool?> callback = null)
        => planner == "known"
            ? compiler.BuildKnownSubsetCompilePlan(translation, MaterialResource.Create(), environment, callback)
            : compiler.BuildObservedSubsetCompilePlan(translation, MaterialResource.Create(), environment, callback);

    private static (LE3MaterialShaderCompiler, TranslationResult) Ordinary(string name)
    {
        var (compiler, translation, _) = OrdinarySnapshot(name);
        return (compiler, translation);
    }

    private static (LE3MaterialShaderCompiler, TranslationResult, LE3ShaderSourceSnapshot) OrdinarySnapshot(string name)
    {
        GlobalTest.Init();
        // The package must outlive the translation; the memory package is intentionally not disposed per test.
        var package = MEPackageHandler.CreateMemoryEmptyPackage($"LE3Completeness{name}.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var color = package.CreateExport("Tint", "MaterialExpressionVectorParameter", material, indexed: false);
        color.WriteProperty(new NameProperty("Tint", "ParameterName"));
        material.WriteProperty(new StructProperty("ExpressionInput",
            new PropertyCollection { new ObjectProperty(color.UIndex, "Expression") }, "DiffuseColor"));
        var compiler = new LE3MaterialShaderCompiler(material);
        var sources = MinimalSources();
        var translation = compiler.TranslateMaterialResourceHlsl(sources);
        Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
        Assert.IsTrue(translation.IncludesMaterialResourceInputs);
        return (compiler, translation, sources);
    }

    private static LE3ShaderSourceSnapshot MinimalSources() => new(new Dictionary<string, string> { ["MaterialTemplate.usf"] = Template() });
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
