using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3LightmapPolicyTests
{
    [TestMethod]
    [DataRow("FCustomSimpleLightMapTexturePolicy", "Cone", false)]
    [DataRow("FCustomSimpleLightMapTexturePolicy", "Cone", true)]
    [DataRow("FCustomSimpleLightMapTexturePolicy", "Constant", false)]
    [DataRow("FCustomSimpleLightMapTexturePolicy", "Constant", true)]
    [DataRow("FCustomVectorVertexLightMapPolicy", "Cone", false)]
    [DataRow("FCustomVectorVertexLightMapPolicy", "Cone", true)]
    [DataRow("FCustomVectorVertexLightMapPolicy", "Constant", false)]
    [DataRow("FCustomVectorVertexLightMapPolicy", "Constant", true)]
    [DataRow("FCustomVectorVertexLightMapPolicy", "LinearHalfspace", false)]
    [DataRow("FCustomVectorVertexLightMapPolicy", "LinearHalfspace", true)]
    [DataRow("FCustomSimpleLightMapTexturePolicy", "LinearHalfspace", false)]
    [DataRow("FCustomSimpleLightMapTexturePolicy", "LinearHalfspace", true)]
    [DataRow("FCustomSimpleLightMapTexturePolicy", "Sphere", false)]
    [DataRow("FCustomSimpleLightMapTexturePolicy", "Sphere", true)]
    [DataRow("FCustomVectorVertexLightMapPolicy", "Sphere", false)]
    [DataRow("FCustomVectorVertexLightMapPolicy", "Sphere", true)]
    public void FoggedCustomPolicyMaterialOverridesDoNotMutateTypeEnvironment(string policy, string density, bool usage)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FoggedMaterialMacros.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        bool simple = policy == "FCustomSimpleLightMapTexturePolicy";
        material.WriteProperty(new BoolProperty(usage, simple ? "bUsedWithSimpleLightMaps" : "bUsedWithVectorLightMaps"));
        var compiler = new LE3MaterialShaderCompiler(material);
        string template = "/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n*/";
        Assert.IsTrue(TryGetKnownPermutationEnvironment("TBasePassVertexShader" + policy + "F" + density + "DensityPolicy",
            "FLocalVertexFactory", out var environment));
        var candidate = environment.CreateCandidate();
        var job = compiler.BuildProvisionalCompileJob(compiler.TranslateMaterialHlsl(template),
            LegendaryExplorerCore.Unreal.BinaryConverters.MaterialResource.Create(), environment.Target, candidate.EnvironmentMacros);
        Assert.AreEqual(simple && usage, job.Macros.ContainsKey("SIMPLE_TEXTURE_LIGHTMAP"));
        Assert.AreEqual(!simple && usage, job.Macros.ContainsKey("USING_VECTORLIGHTMAPS"));
        Assert.AreEqual(!simple && usage, job.Macros.ContainsKey("VERTEX_LIGHTMAP"));
        Assert.AreEqual(simple && usage ? "1" : "2", job.Macros["NUM_LIGHTMAP_COEFFICIENTS"]);
        Assert.AreEqual("2", candidate.EnvironmentMacros["NUM_LIGHTMAP_COEFFICIENTS"]);
        Assert.IsFalse(candidate.EnvironmentMacros.ContainsKey("SIMPLE_TEXTURE_LIGHTMAP"));
        Assert.IsFalse(candidate.EnvironmentMacros.ContainsKey("USING_VECTORLIGHTMAPS"));
    }


    [TestMethod]
    [DataRow("FCustomSimpleLightMapTexturePolicy", "Cone", "", 2)]
    [DataRow("FCustomSimpleLightMapTexturePolicy", "Constant", "", 2)]
    [DataRow("FCustomSimpleVertexLightMapPolicy", "Cone", "", 2)]
    [DataRow("FCustomSimpleVertexLightMapPolicy", "Constant", "SIMPLE_VERTEX_LIGHTMAP", 1)]
    [DataRow("FCustomVectorLightMapTexturePolicy", "Cone", "", 2)]
    [DataRow("FCustomVectorLightMapTexturePolicy", "Constant", "TEXTURE_LIGHTMAP", 2)]
    [DataRow("FCustomVectorVertexLightMapPolicy", "Cone", "", 2)]
    [DataRow("FCustomVectorVertexLightMapPolicy", "Constant", "", 2)]
    [DataRow("FCustomVectorVertexLightMapPolicy", "LinearHalfspace", "", 2)]
    [DataRow("FDirectionalLightLightMapPolicy", "Cone", "ENABLE_DIRECTIONAL_LIGHT", 2)]
    [DataRow("FDirectionalLightLightMapPolicy", "Constant", "ENABLE_DIRECTIONAL_LIGHT", 2)]
    [DataRow("FDirectionalLightMapTexturePolicy", "Cone", "", 2)]
    [DataRow("FDirectionalLightMapTexturePolicy", "Constant", "TEXTURE_LIGHTMAP", 2)]
    [DataRow("FDirectionalVertexLightMapPolicy", "Cone", "VERTEX_LIGHTMAP", 2)]
    [DataRow("FDirectionalVertexLightMapPolicy", "Constant", "VERTEX_LIGHTMAP", 2)]
    [DataRow("FNoLightMapPolicy", "Cone", "", 2)]
    [DataRow("FSHLightLightMapPolicy", "Cone", "ENABLE_DIRECTIONAL_LIGHT", 2)]
    [DataRow("FSHLightLightMapPolicy", "Constant", "ENABLE_DIRECTIONAL_LIGHT", 2)]
    [DataRow("FSimpleLightMapTexturePolicy", "Cone", "TEXTURE_LIGHTMAP", 1)]
    [DataRow("FSimpleVertexLightMapPolicy", "Cone", "SIMPLE_VERTEX_LIGHTMAP", 1)]
    [DataRow("FCustomSimpleLightMapTexturePolicy", "LinearHalfspace", "", 2)]
    [DataRow("FCustomSimpleLightMapTexturePolicy", "Sphere", "", 2)]
    [DataRow("FCustomSimpleVertexLightMapPolicy", "LinearHalfspace", "SIMPLE_VERTEX_LIGHTMAP", 1)]
    [DataRow("FCustomSimpleVertexLightMapPolicy", "Sphere", "SIMPLE_VERTEX_LIGHTMAP", 1)]
    [DataRow("FCustomVectorLightMapTexturePolicy", "LinearHalfspace", "TEXTURE_LIGHTMAP", 2)]
    [DataRow("FCustomVectorLightMapTexturePolicy", "Sphere", "TEXTURE_LIGHTMAP", 2)]
    [DataRow("FCustomVectorVertexLightMapPolicy", "Sphere", "", 2)]
    [DataRow("FDirectionalLightLightMapPolicy", "LinearHalfspace", "ENABLE_DIRECTIONAL_LIGHT", 2)]
    [DataRow("FDirectionalLightLightMapPolicy", "Sphere", "ENABLE_DIRECTIONAL_LIGHT", 2)]
    [DataRow("FDirectionalLightMapTexturePolicy", "LinearHalfspace", "TEXTURE_LIGHTMAP", 2)]
    [DataRow("FDirectionalLightMapTexturePolicy", "Sphere", "TEXTURE_LIGHTMAP", 2)]
    [DataRow("FDirectionalVertexLightMapPolicy", "LinearHalfspace", "VERTEX_LIGHTMAP", 2)]
    [DataRow("FDirectionalVertexLightMapPolicy", "Sphere", "VERTEX_LIGHTMAP", 2)]
    [DataRow("FSHLightLightMapPolicy", "LinearHalfspace", "ENABLE_DIRECTIONAL_LIGHT", 2)]
    [DataRow("FSHLightLightMapPolicy", "Sphere", "ENABLE_DIRECTIONAL_LIGHT", 2)]
    [DataRow("FSimpleLightMapTexturePolicy", "LinearHalfspace", "TEXTURE_LIGHTMAP", 1)]
    [DataRow("FSimpleLightMapTexturePolicy", "Sphere", "TEXTURE_LIGHTMAP", 1)]
    [DataRow("FSimpleLightMapTexturePolicy", "Constant", "TEXTURE_LIGHTMAP", 1)]
    [DataRow("FSimpleVertexLightMapPolicy", "LinearHalfspace", "SIMPLE_VERTEX_LIGHTMAP", 1)]
    [DataRow("FSimpleVertexLightMapPolicy", "Sphere", "SIMPLE_VERTEX_LIGHTMAP", 1)]
    [DataRow("FSimpleVertexLightMapPolicy", "Constant", "SIMPLE_VERTEX_LIGHTMAP", 1)]
    public void FoggedBasePassGatesComposeNativeFogAndLightmapPredicates(string policy, string density, string extra, int coefficients)
    {
        string name = "TBasePassVertexShader" + policy + "F" + density + "DensityPolicy";
        foreach (var factory in GetKnownVertexFactoryRegistrations())
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, factory.Name, out var environment));
            Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.CompileFlagsEvidence);
            Assert.AreEqual(0x9000u, environment.Target.CompileFlags);
            var macros = environment.CreateCandidate().EnvironmentMacros;
            string densityMacro = "FOGVOLUMEDENSITY_" + (density == "Cone" ? "CONEDENSITY" : density == "Constant" ? "CONSTANT" : density == "Sphere" ? "SPHEREDENSITY" : "LINEARHALFSPACE");
            Assert.AreEqual("1", macros[densityMacro]);
            Assert.AreEqual(ShaderEnvironmentEvidence.LE3RegistrationCallback, environment.MacroDefinitions.Single(m => m.Name == densityMacro).Evidence);
            Assert.AreEqual(coefficients.ToString(), macros["NUM_LIGHTMAP_COEFFICIENTS"]);
            int factoryMacros = factory.Name switch
            {
                "FGPUSkinVertexFactory" => 1, "FGPUSkinMorphVertexFactory" => 2,
                "FInstancedStaticMeshVertexFactory" => 1, "FGPUSkinDecalVertexFactory" => 2,
                "FLocalDecalVertexFactory" => 2, "FGPUSkinMorphDecalVertexFactory" => 3, "FSplineMeshVertexFactory" => 1,
                "FParticleInstancedMeshVertexFactory" => extra == "SIMPLE_VERTEX_LIGHTMAP" ? 1 : 2, _ => 0
            };
            Assert.AreEqual(6 + factoryMacros + (extra.Length == 0 ? 0 : 1), macros.Count);
            if (extra.Length != 0) Assert.AreEqual("1", macros[extra]);
            Assert.IsFalse(macros.ContainsKey("FOGVOLUMEDENSITY_NONE"));
            Assert.IsFalse(macros.ContainsKey("ENABLE_SH_LIGHT"));
            Assert.IsFalse(macros.ContainsKey("TRANSLUCENCY_ONEPASS_DYNAMICALLY_SHADOWED"));
        }
        for (int flags = 0; flags < 1024; flags++)
        foreach (EBlendMode blend in Enum.GetValues<EBlendMode>())
        foreach (string vfName in new[] { "FLocalVertexFactory", "FGPUSkinVertexFactory" })
        foreach (bool fog in new[] { false, true })
        {
            bool Bit(int n) => (flags & (1 << n)) != 0;
            var context = BaseContext() with { IsUnlit = Bit(0), IsSpecialEngineMaterial = Bit(1), UsedWithStaticLighting = Bit(2),
                RequiresSimpleStaticLighting = Bit(3), IsTerrainMaterial = Bit(4), UsedWithDecals = Bit(5),
                UsedWithVectorLightMaps = Bit(6), UsedWithSimpleLightMaps = Bit(7), BlendMode = blend, UsedWithFogVolumes = fog };
            bool expected = density != "Cone" && blend is EBlendMode.BLEND_Translucent or EBlendMode.BLEND_Additive or EBlendMode.BLEND_AlphaComposite &&
                (fog || vfName == "FLocalVertexFactory");
            if (policy is "FDirectionalLightLightMapPolicy" or "FSHLightLightMapPolicy")
                expected &= Bit(9) && !Bit(0) && !Bit(5);
            else if (policy.StartsWith("FCustom", StringComparison.Ordinal))
            {
                bool texture = policy.EndsWith("TexturePolicy", StringComparison.Ordinal);
                bool usage = policy.StartsWith("FCustomVector", StringComparison.Ordinal) ? Bit(6) : Bit(7);
                expected &= !Bit(3) && vfName == "FLocalVertexFactory" && !Bit(0) && (texture || !Bit(4)) && Bit(8) &&
                    (Bit(1) || Bit(2) && (Bit(5) || usage));
            }
            else expected &= (policy.StartsWith("FSimple", StringComparison.Ordinal) || !Bit(3)) &&
                !Bit(0) && Bit(8) && (Bit(2) || Bit(1)) &&
                (!policy.Contains("Vertex", StringComparison.Ordinal) || !Bit(4));
            Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(name, context, vfName, new(Bit(8), SupportsDynamicLighting: Bit(9)), out bool actual));
            Assert.AreEqual(expected, actual, name + "/" + flags + "/" + blend + "/" + vfName + "/" + fog);
        }
        var opaque = BaseContext();
        Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(name, opaque, null, null, out bool rejected));
        Assert.IsFalse(rejected);
        var enabled = opaque with { BlendMode = EBlendMode.BLEND_Translucent };
        Assert.AreEqual(density == "Cone", TryEvaluateKnownShaderTypeShouldCache(name, enabled, null, null, out _));
        if (density != "Cone")
            Assert.IsFalse(TryEvaluateKnownShaderTypeShouldCache(name, enabled with { UsedWithFogVolumes = true },
                "FLocalVertexFactory", null, out _));
    }

    [TestMethod]
    public void FoggedBasePassInventoryDoesNotInventSymmetricRegistrations()
    {
        Assert.IsTrue(TryGetKnownShaderTypeRegistration("TBasePassVertexShaderFSimpleLightMapTexturePolicyFConstantDensityPolicy", out _));
        Assert.IsTrue(TryGetKnownShaderTypeRegistration("TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy", out _));
        Assert.IsTrue(TryGetKnownShaderTypeRegistration("TBasePassVertexShaderFSHLightLightMapPolicyFSphereDensityPolicy", out _));
        Assert.IsFalse(TryGetKnownShaderTypeRegistration("TBasePassVertexShaderFUnknownLightMapPolicyFSphereDensityPolicy", out _));
        Assert.IsFalse(TryGetKnownShaderTypeRegistration("TBasePassPixelShaderFSimpleLightMapTexturePolicyFSphereDensityPolicy", out _));
    }


    [TestMethod]
    public void DynamicLightingCapabilityUnknownWithholdsPlanInsteadOfSilentlyExcludingTarget()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3DynamicCapability.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var compiler = new LE3MaterialShaderCompiler(material);
        var translation = compiler.TranslateMaterialHlsl("/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n*/");
        Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
        Assert.IsTrue(TryGetKnownPermutationEnvironment("TBasePassVertexShaderFDirectionalLightLightMapPolicyFNoDensityPolicy", "FLocalVertexFactory", out var environment));
        var candidate = environment.CreateCandidate() with { VertexFactoryContext = new(true) };
        var unresolved = compiler.BuildProvisionalCompilePlan(translation, LegendaryExplorerCore.Unreal.BinaryConverters.MaterialResource.Create(), [candidate], BaseContext());
        Assert.IsFalse(unresolved.Success); Assert.AreEqual(0, unresolved.Jobs.Count);
        Assert.AreEqual(0, unresolved.ExcludedTargets.Count);
        Assert.AreEqual(ShaderCompileDisposition.Unresolved, unresolved.Decisions.Single().Disposition);
        Assert.AreEqual(ShaderCompileGate.ShaderTypeShouldCache, unresolved.Decisions.Single().Gate);
        var excluded = compiler.BuildProvisionalCompilePlan(translation, LegendaryExplorerCore.Unreal.BinaryConverters.MaterialResource.Create(),
            [candidate with { VertexFactoryContext = new(true, SupportsDynamicLighting: false) }], BaseContext());
        Assert.IsTrue(excluded.Success, string.Join("; ", excluded.Errors));
        Assert.AreEqual(1, excluded.ExcludedTargets.Count);
        Assert.AreEqual(ShaderCompileDisposition.Excluded, excluded.Decisions.Single().Disposition);
    }

    [TestMethod]
    [DataRow("FNoLightMapPolicy")]
    [DataRow("FDirectionalLightMapTexturePolicy")]
    [DataRow("FSimpleLightMapTexturePolicy")]
    [DataRow("FDummyLightMapTexturePolicy")]
    public void LightmapDensityPolicyGateDoesNotConflateTextureDensityDebugOrCoordinateRequirements(string policy)
    {
        foreach (string stage in new[] { "Vertex", "Pixel" })
        {
            string name = $"TLightMapDensity{stage}Shader<{policy}>";
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
            var macros = environment.CreateCandidate().EnvironmentMacros;
            bool texture = policy != "FNoLightMapPolicy";
            Assert.AreEqual(texture, macros.ContainsKey("SIMPLE_VERTEX_LIGHTMAP"));
            Assert.AreEqual(texture, macros.ContainsKey("TEXTURE_LIGHTMAP"));
            Assert.AreEqual(texture, macros.ContainsKey("SIMPLE_TEXTURE_LIGHTMAP"));
            Assert.AreEqual(policy == "FSimpleLightMapTexturePolicy" ? "1" : "2", macros["NUM_LIGHTMAP_COEFFICIENTS"]);
            for (int flags = 0; flags < 256; flags++)
            {
                bool Bit(int index) => (flags & (1 << index)) != 0;
                var context = BaseContext() with { IsSpecialEngineMaterial = Bit(0), IsMasked = Bit(1), IsUnlit = Bit(2),
                    UsedWithStaticLighting = Bit(3), RequiresSimpleStaticLighting = Bit(5), IsTerrainMaterial = Bit(6),
                    UsedWithDecals = Bit(7), NumUserTexCoords = 0, BlendMode = EBlendMode.BLEND_Translucent };
                bool expected = Bit(0) || Bit(1);
                if (texture) expected &= !Bit(2) && Bit(4) && (Bit(3) || Bit(0)) &&
                    (policy != "FDirectionalLightMapTexturePolicy" || !Bit(5));
                Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(name, context, "FLocalVertexFactory", new(Bit(4)),
                    out bool actual, new(false)));
                Assert.AreEqual(expected, actual, name + "/" + flags);
            }
            var enabled = BaseContext() with { IsMasked = true };
            Assert.AreEqual(!texture, TryEvaluateKnownShaderTypeShouldCache(name, enabled, "FLocalVertexFactory", null, out _));
            Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(name, BaseContext(), null, null, out bool excluded));
            Assert.IsFalse(excluded, "The native common gate rejects before querying a factory capability.");
        }
    }

    [TestMethod]
    [DataRow("DirectionalLightLightMap", "Vertex", false)]
    [DataRow("DirectionalLightLightMap", "Pixel", false)]
    [DataRow("DirectionalLightLightMap", "Pixel", true)]
    [DataRow("SHLightLightMap", "Vertex", false)]
    [DataRow("SHLightLightMap", "Pixel", false)]
    [DataRow("SHLightLightMap", "Pixel", true)]
    public void DynamicLightmapGateRequiresKnownDynamicCapabilityLitNonDecalButNotOpaqueBlend(string policy, string stage, bool sky)
    {
        string name = $"TBasePass{stage}ShaderF{policy}Policy" + (stage == "Vertex" ? "FNoDensityPolicy" : sky ? "SkyLight" : "NoSkyLight");
        for (int flags = 0; flags < 64; flags++)
        foreach (EBlendMode blend in Enum.GetValues<EBlendMode>())
        {
            bool Bit(int index) => (flags & (1 << index)) != 0;
            var context = BaseContext() with { IsUnlit = Bit(0), UsedWithDecals = Bit(1), IsSpecialEngineMaterial = Bit(2),
                UsedWithStaticLighting = Bit(3), IsMasked = Bit(4), BlendMode = blend };
            foreach (bool staticLighting in new[] { false, true })
            {
                var vf = new ShouldCacheVertexFactoryContext(staticLighting, SupportsDynamicLighting: Bit(5));
                Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(name, context, "FLocalVertexFactory", vf, out bool actual));
                Assert.AreEqual(!Bit(0) && !Bit(1) && Bit(5), actual, name + "/" + flags + "/" + blend);
            }
        }
        Assert.IsFalse(TryEvaluateKnownShaderTypeShouldCache(name, BaseContext(), "FLocalVertexFactory", null, out _));
        Assert.IsFalse(TryEvaluateKnownShaderTypeShouldCache(name, BaseContext(), "FLocalVertexFactory", new(true), out _));
        Assert.IsFalse(new VertexFactoryRegistration("Unresolved", "Unresolved", true, true).CreateCacheContext().SupportsDynamicLighting.HasValue);
        foreach (var factory in GetKnownVertexFactoryRegistrations())
        {
            bool expectedDynamic = factory.Name != "FLocalVertexFactoryApex";
            Assert.AreEqual(expectedDynamic, factory.SupportsDynamicLighting);
            Assert.AreEqual(expectedDynamic, factory.CreateCacheContext().SupportsDynamicLighting);
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, factory.Name, out var environment));
            var macros = environment.CreateCandidate().EnvironmentMacros;
            Assert.AreEqual("1", macros["ENABLE_DIRECTIONAL_LIGHT"]);
            Assert.AreEqual("1", macros["TRANSLUCENCY_ONEPASS_DYNAMICALLY_SHADOWED"]);
            Assert.AreEqual("2", macros["NUM_LIGHTMAP_COEFFICIENTS"]);
            Assert.AreEqual(policy == "SHLightLightMap", macros.ContainsKey("ENABLE_SH_LIGHT"));
            Assert.AreEqual(sky, macros.ContainsKey("ENABLE_SKY_LIGHT"));
            Assert.AreEqual(stage == "Vertex", macros.ContainsKey("FOGVOLUMEDENSITY_NONE"));
            foreach (var macro in environment.MacroDefinitions)
                if (macro.Name is "ENABLE_DIRECTIONAL_LIGHT" or "ENABLE_SH_LIGHT" or "TRANSLUCENCY_ONEPASS_DYNAMICALLY_SHADOWED")
                    Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, macro.Evidence);
        }
    }

    [TestMethod]
    [DataRow("DirectionalVertexLightMap", "Vertex", false)]
    [DataRow("DirectionalVertexLightMap", "Pixel", false)]
    [DataRow("DirectionalVertexLightMap", "Pixel", true)]
    [DataRow("CustomVectorVertexLightMap", "Vertex", false)]
    [DataRow("CustomVectorVertexLightMap", "Pixel", false)]
    [DataRow("CustomVectorVertexLightMap", "Pixel", true)]
    [DataRow("CustomSimpleVertexLightMap", "Vertex", false)]
    [DataRow("CustomSimpleVertexLightMap", "Pixel", false)]
    [DataRow("CustomSimpleVertexLightMap", "Pixel", true)]
    [DataRow("CustomVectorLightMapTexture", "Vertex", false)]
    [DataRow("CustomVectorLightMapTexture", "Pixel", false)]
    [DataRow("CustomVectorLightMapTexture", "Pixel", true)]
    [DataRow("CustomSimpleLightMapTexture", "Vertex", false)]
    [DataRow("CustomSimpleLightMapTexture", "Pixel", false)]
    [DataRow("CustomSimpleLightMapTexture", "Pixel", true)]
    public void VertexLightmapNativeGateMatrixAndExactFactoryIdentity(string policy, string stage, bool sky)
    {
        string name = $"TBasePass{stage}ShaderF{policy}Policy" + (stage == "Vertex" ? "FNoDensityPolicy" : sky ? "SkyLight" : "NoSkyLight");
        Assert.IsTrue(TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
        Assert.AreEqual(ShaderEnvironmentEvidence.ShippingBytecode, environment.CompileFlagsEvidence);
        Assert.AreEqual(0x9000u, environment.Target.CompileFlags);
        bool custom = policy.StartsWith("Custom", StringComparison.Ordinal), vector = policy.Contains("Vector", StringComparison.Ordinal);
        bool texture = policy.Contains("Texture", StringComparison.Ordinal);
        for (int flags = 0; flags < 512; flags++)
        {
            bool Bit(int index) => (flags & (1 << index)) != 0;
            var context = BaseContext() with
            {
                IsUnlit = Bit(0), IsTerrainMaterial = Bit(1), UsedWithStaticLighting = Bit(2),
                IsSpecialEngineMaterial = Bit(3), RequiresSimpleStaticLighting = Bit(4), UsedWithDecals = Bit(6),
                UsedWithVectorLightMaps = Bit(7), UsedWithSimpleLightMaps = Bit(8)
            };
            var vf = new ShouldCacheVertexFactoryContext(Bit(5));
            bool expected = !Bit(0) && (texture || !Bit(1)) && !Bit(4) && Bit(5) && (Bit(2) || Bit(3));
            if (custom) expected &= Bit(3) || Bit(2) && (Bit(6) || (vector ? Bit(7) : Bit(8)));
            foreach (string factory in new[] { "FLocalVertexFactory", "FLocalDecalVertexFactory" })
            {
                Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(name, context, factory, vf, out bool actual));
                Assert.AreEqual(expected, actual, name + "/" + factory + "/" + flags);
            }
        }
        var enabled = BaseContext() with { UsedWithStaticLighting = true, UsedWithVectorLightMaps = true, UsedWithSimpleLightMaps = true };
        Assert.IsFalse(TryEvaluateKnownShaderTypeShouldCache(name, enabled, null, new(true), out _));
        Assert.IsFalse(TryEvaluateKnownShaderTypeShouldCache(name, enabled, "FLocalVertexFactory", null, out _));
        foreach (string factory in new[] { "FInstancedStaticMeshVertexFactory", "FLocalVertexFactoryAlias" })
        {
            Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(name, enabled, factory, new(true), out bool actual));
            Assert.AreEqual(!custom && !factory.Contains("FInstancedStaticMeshVertex", StringComparison.Ordinal), actual);
        }
        var macros = environment.MacroDefinitions.ToDictionary(m => m.Name, m => m.Definition);
        string coefficients = policy == "CustomSimpleLightMapTexture" || policy == "CustomSimpleVertexLightMap" && (stage == "Vertex" || sky) ? "1" : "2";
        Assert.AreEqual(coefficients, macros["NUM_LIGHTMAP_COEFFICIENTS"], "Keep captured CustomSimple NoSky2 / Sky1 asymmetry explicit.");
        Assert.AreEqual(vector, macros.ContainsKey("USING_VECTORLIGHTMAPS"));
        Assert.AreEqual(sky, macros.ContainsKey("ENABLE_SKY_LIGHT"));
        Assert.AreEqual(stage == "Vertex", macros.ContainsKey("FOGVOLUMEDENSITY_NONE"));
    }

    [TestMethod]
    public void OrdinaryContextReadsBothNativeMaterialLightmapUsageGetters()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3LightmapContext.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(true, "bUsedWithVectorLightMaps"));
        material.WriteProperty(new BoolProperty(false, "bUsedWithSimpleLightMaps"));
        var compiler = new LE3MaterialShaderCompiler(material);
        var actual = compiler.BuildShouldCacheMaterialContext();
        Assert.IsTrue(actual.UsedWithVectorLightMaps);
        Assert.IsFalse(actual.UsedWithSimpleLightMaps);
        var resolved = new PropertyCollection { new BoolProperty(false, "bUsedWithVectorLightMaps"), new BoolProperty(true, "bUsedWithSimpleLightMaps") };
        actual = compiler.BuildShouldCacheMaterialContext(resolved);
        Assert.IsFalse(actual.UsedWithVectorLightMaps);
        Assert.IsTrue(actual.UsedWithSimpleLightMaps);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CustomSimpleTextureMaterialMacroIsConditionedAndNeverAppliedToPixelStage(bool usage)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3CustomSimpleTextureMacro.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(usage, "bUsedWithSimpleLightMaps"));
        var compiler = new LE3MaterialShaderCompiler(material);
        string template = "/*\n%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n*/";
        var translation = compiler.TranslateMaterialHlsl(template);
        Assert.IsTrue(translation.Success, string.Join("; ", translation.Errors));
        foreach (string name in new[] { "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy",
            "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicyNoSkyLight", "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicySkyLight" })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
            var job = compiler.BuildProvisionalCompileJob(translation, LegendaryExplorerCore.Unreal.BinaryConverters.MaterialResource.Create(),
                environment.Target, environment.CreateCandidate().EnvironmentMacros);
            Assert.AreEqual(usage && environment.Target.Stage == ShaderStage.Vertex, job.Macros.ContainsKey("SIMPLE_TEXTURE_LIGHTMAP"));
            Assert.AreEqual("1", job.Macros["TEXTURE_LIGHTMAP"]);
            Assert.AreEqual("1", job.Macros["NUM_LIGHTMAP_COEFFICIENTS"]);
        }
    }

    [TestMethod]
    [DataRow("TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy", true)]
    [DataRow("TBasePassPixelShaderFNoLightMapPolicySkyLight", true)]
    [DataRow("TBasePassVertexShaderFDirectionalLightLightMapPolicyFNoDensityPolicy", true)]
    [DataRow("TBasePassPixelShaderFDirectionalLightLightMapPolicyNoSkyLight", true)]
    [DataRow("TBasePassVertexShaderFSHLightLightMapPolicyFNoDensityPolicy", true)]
    [DataRow("TBasePassPixelShaderFSHLightLightMapPolicySkyLight", true)]
    [DataRow("TBasePassVertexShaderFSimpleVertexLightMapPolicyFNoDensityPolicy", false)]
    [DataRow("TBasePassPixelShaderFDirectionalLightMapTexturePolicySkyLight", false)]
    [DataRow("TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy", false)]
    public void FinalPolicyCleanupRemovesOnlyDynamicAndNoLightMapFallbacks(string shaderName, bool removes)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FinalMacroCleanup.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(true, "bUsedWithSimpleLightMaps"));
        var compiler = new LE3MaterialShaderCompiler(material);
        var translation = compiler.TranslateMaterialHlsl("/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n*/");
        foreach (var factory in GetKnownVertexFactoryRegistrations())
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(shaderName, factory.Name, out var environment));
            var macros = environment.CreateCandidate().EnvironmentMacros.ToDictionary(pair => pair.Key, pair => pair.Value);
            macros["SIMPLE_VERTEX_LIGHTMAP"] = "1";
            macros["TEXTURE_LIGHTMAP"] = "1";
            var job = compiler.BuildProvisionalCompileJob(translation, LegendaryExplorerCore.Unreal.BinaryConverters.MaterialResource.Create(),
                environment.Target, macros);
            Assert.AreEqual(!removes, job.Macros.ContainsKey("SIMPLE_VERTEX_LIGHTMAP"));
            Assert.AreEqual(!removes, job.Macros.ContainsKey("TEXTURE_LIGHTMAP"));
            Assert.AreEqual("1", macros["SIMPLE_VERTEX_LIGHTMAP"], "Caller macro dictionary must remain unchanged.");
            foreach (var pair in macros.Where(pair => pair.Key is not ("SIMPLE_VERTEX_LIGHTMAP" or "TEXTURE_LIGHTMAP")))
                Assert.AreEqual(pair.Value, job.Macros[pair.Key], pair.Key);
        }
    }

    private static ShouldCacheMaterialContext BaseContext() => new(false, false, false, false, false, false, false, false);
}
