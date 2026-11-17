using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3VelocityResourceGateTests
{
    private static readonly string[] VelocityStages = ["FVelocityVertexShader", "FVelocityPixelShader"];
    private static readonly EBlendMode[] BlendModes =
    [
        EBlendMode.BLEND_Opaque, EBlendMode.BLEND_Masked, EBlendMode.BLEND_Translucent,
        EBlendMode.BLEND_Additive, EBlendMode.BLEND_Modulate, EBlendMode.BLEND_SoftMasked,
        EBlendMode.BLEND_AlphaComposite
    ];

    [TestMethod]
    public void BothVelocityStagesComposeEligibilityWithNullableDecalResourceGate()
    {
        foreach (string stage in VelocityStages)
        for (int bits = 0; bits < 8; bits++)
        foreach (EBlendMode blend in BlendModes)
        {
            bool special = (bits & 1) != 0;
            bool masked = (bits & 2) != 0;
            bool twoSided = (bits & 4) != 0;
            bool eligible = special || masked || (twoSided && blend is
                EBlendMode.BLEND_Opaque or EBlendMode.BLEND_Masked or EBlendMode.BLEND_SoftMasked);
            var context = Context() with
            {
                IsSpecialEngineMaterial = special,
                IsMasked = masked,
                IsTwoSided = twoSided,
                BlendMode = blend
            };

            foreach (bool? fallback in new bool?[] { false, true, null })
            {
                bool resolved = TryEvaluateKnownShaderTypeShouldCache(stage,
                    context with { DecalFactoryFallback = fallback }, out bool actual);
                string caseName = $"stage={stage}, bits={bits}, blend={blend}, fallback={fallback?.ToString() ?? "null"}";
                if (!eligible)
                {
                    Assert.IsTrue(resolved, caseName);
                    Assert.IsFalse(actual, caseName);
                }
                else if (fallback is null)
                {
                    Assert.IsFalse(resolved, caseName);
                    Assert.IsFalse(actual, caseName);
                }
                else
                {
                    Assert.IsTrue(resolved, caseName);
                    Assert.AreEqual(!fallback.Value, actual, caseName);
                }
            }
        }
    }

    [TestMethod]
    public void ContextUsesDecalExportClassForResourceFallbackIndependentOfUsageAndMaskFlags()
    {
        GlobalTest.Init();
        using var package = Package("LE3VelocityContext.pcc");
        foreach (string className in new[] { "Material", "DecalMaterial" })
        foreach (bool decals in new[] { false, true })
        foreach (bool masked in new[] { false, true })
        foreach (bool explicitUsage in new[] { false, true })
        {
            var material = package.CreateExport($"{className}_{decals}_{masked}_{explicitUsage}", className, indexed: false);
            if (explicitUsage) material.WriteProperty(new BoolProperty(decals, "bUsedWithDecals"));
            material.WriteProperty(new BoolProperty(masked, "bIsMasked"));

            var context = new LE3MaterialShaderCompiler(material).BuildShouldCacheMaterialContext();
            Assert.AreEqual(className == "DecalMaterial", context.DecalFactoryFallback, className);
            Assert.AreEqual(explicitUsage ? decals : className == "DecalMaterial", context.UsedWithDecals,
                $"{className}, explicit={explicitUsage}, usage={decals}");
            Assert.AreEqual(masked, context.IsMasked, $"{className}, masked={masked}");
        }
    }

    [TestMethod]
    public void ExplicitLocalVelocityCandidatesRejectDecalResourcesAtShaderTypeGate()
    {
        GlobalTest.Init();
        using var package = Package("LE3VelocityPlanner.pcc");
        foreach (string className in new[] { "Material", "DecalMaterial" })
        {
            var material = Material(package, className, usedWithDecals: true, masked: true);
            var compiler = new LE3MaterialShaderCompiler(material);
            var translation = compiler.TranslateMaterialResourceHlsl(Template());
            Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));

            var candidates = VelocityStages.Select(stage =>
            {
                Assert.IsTrue(TryGetKnownPermutationEnvironment(stage, "FLocalVertexFactory", out var environment), stage);
                return environment.CreateCandidate();
            }).ToArray();
            var plan = compiler.BuildProvisionalCompilePlan(translation, MaterialResource.Create(), candidates,
                materialContext: compiler.BuildShouldCacheMaterialContext());

            Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors));
            foreach (string stage in VelocityStages)
            {
                var decision = plan.Decisions.Single(d => d.Target.ShaderTypeName == stage);
                if (className == "DecalMaterial")
                {
                    Assert.AreEqual(ShaderCompileGate.ShaderTypeShouldCache, decision.Gate, stage);
                    Assert.AreEqual(ShaderCompileDisposition.Excluded, decision.Disposition, stage);
                    Assert.IsFalse(plan.Jobs.Any(j => j.Target.ShaderTypeName == stage), stage);
                }
                else
                {
                    Assert.AreEqual(ShaderCompileGate.CompilationEnvironment, decision.Gate, stage);
                    Assert.AreEqual(ShaderCompileDisposition.Eligible, decision.Disposition, stage);
                    Assert.IsTrue(plan.Jobs.Any(j => j.Target.ShaderTypeName == stage), stage);
                }
            }
        }
    }

    [TestMethod]
    public void KnownAndObservedSubsetPlannersApplyTheSameVelocityResourceGate()
    {
        GlobalTest.Init();
        using var package = Package("LE3VelocitySubsetPlanner.pcc");
        foreach (string className in new[] { "Material", "DecalMaterial" })
        {
            var material = Material(package, className, usedWithDecals: true, masked: true);
            var compiler = new LE3MaterialShaderCompiler(material);
            var translation = compiler.TranslateMaterialResourceHlsl(Template());
            Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
            foreach (var plan in new[]
            {
                compiler.BuildKnownSubsetCompilePlan(translation, MaterialResource.Create()),
                compiler.BuildObservedSubsetCompilePlan(translation, MaterialResource.Create())
            })
            {
                Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors));
                if (className == "DecalMaterial")
                    Assert.IsFalse(plan.Jobs.Any(j => VelocityStages.Contains(j.Target.ShaderTypeName)),
                        "A decal resource must not select either velocity stage for any factory.");
                foreach (string stage in VelocityStages)
                {
                    var decision = plan.Decisions.Single(d => d.Target.ShaderTypeName == stage &&
                        d.Target.VertexFactoryTypeName == "FLocalVertexFactory");
                    Assert.AreEqual(className == "DecalMaterial" ? ShaderCompileDisposition.Excluded :
                        ShaderCompileDisposition.Eligible, decision.Disposition, stage);
                    Assert.AreEqual(className == "DecalMaterial" ? ShaderCompileGate.ShaderTypeShouldCache :
                        ShaderCompileGate.CompilationEnvironment, decision.Gate, stage);
                    Assert.AreEqual(className != "DecalMaterial", plan.Jobs.Any(j =>
                        j.Target.ShaderTypeName == stage && j.Target.VertexFactoryTypeName == "FLocalVertexFactory"), stage);
                }
            }
        }
    }

    [TestMethod]
    [DataRow("Material")]
    [DataRow("DecalMaterial")]
    public void RequestedInstanceDoesNotReplaceBaseMaterialResourceClass(string className)
    {
        GlobalTest.Init();
        using var package = Package("LE3VelocityInstance.pcc");
        var material = Material(package, className, usedWithDecals: false, masked: true);
        var instance = package.CreateExport("RequestedInstance", "MaterialInstanceConstant", indexed: false);
        instance.WriteProperty(new ObjectProperty(material.UIndex, "Parent"));
        var input = new ResolvedMaterialCompilationInput(material, requestedMaterialInstance: instance);
        var context = new LE3MaterialShaderCompiler(input).BuildShouldCacheMaterialContext();

        Assert.AreSame(instance, input.RequestedMaterial);
        Assert.AreSame(material, input.BaseMaterial);
        Assert.AreEqual(className == "DecalMaterial", context.DecalFactoryFallback.Value);
        foreach (string stage in VelocityStages)
        {
            Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(stage, context, out bool shouldCache), stage);
            Assert.AreEqual(className != "DecalMaterial", shouldCache, stage);
        }
    }

    [TestMethod]
    public void DecalFactoryFallbackCanAdmitExistingDecalVertexFactoryGateWithoutDecalUsageFlag()
    {
        var context = Context() with
        {
            UsedWithDecals = false,
            UsedWithSkeletalMesh = true,
            DecalFactoryFallback = true
        };
        var factory = new ShouldCacheVertexFactoryContext(SupportsStaticLighting: true);

        Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache("FGPUSkinDecalVertexFactory", context,
            factory, out bool shouldCache));
        Assert.IsTrue(shouldCache);
        Assert.IsTrue(TryEvaluateKnownVertexFactoryShouldCache("FGPUSkinDecalVertexFactory",
            context with { DecalFactoryFallback = false }, factory, out shouldCache));
        Assert.IsFalse(shouldCache);
    }

    private static IMEPackage Package(string name) => MEPackageHandler.CreateMemoryEmptyPackage(name, MEGame.LE3);

    private static ExportEntry Material(IMEPackage package, string className, bool usedWithDecals, bool masked)
    {
        var material = package.CreateExport(className, className, indexed: false);
        material.WriteProperty(new BoolProperty(usedWithDecals, "bUsedWithDecals"));
        material.WriteProperty(new BoolProperty(masked, "bIsMasked"));
        var color = package.CreateExport($"{className}Tint", "MaterialExpressionVectorParameter", material, indexed: false);
        color.WriteProperty(new NameProperty("Tint", "ParameterName"));
        material.WriteProperty(new StructProperty("ExpressionInput",
            new PropertyCollection { new ObjectProperty(color.UIndex, "Expression") }, "DiffuseColor"));
        return material;
    }

    private static ShouldCacheMaterialContext Context() => new(false, false, false, false, false, false, false, false);

    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
