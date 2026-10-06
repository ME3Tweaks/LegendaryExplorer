using System;
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
public class LE3LightShaderObjectTests
{
    [TestMethod]
    [DataRow("ShadowTexture", true)]
    [DataRow("ShadowTexture", false)]
    [DataRow("ShadowVertexBuffer", true)]
    [DataRow("ShadowVertexBuffer", false)]
    [DataRow("SignedDistanceFieldShadowTexture", false)]
    [DataRow("SignedDistanceFieldShadowTexture", true)]
    public void ShShadowPoliciesRemainExcludedWithNativeStageSpecificMacros(string shadow, bool vertex)
    {
        string name = $"TLight{(vertex ? "Vertex" : "Pixel")}ShaderFSphericalHarmonicLightPolicyF{shadow}Policy";
        for (int flags = 0; flags < 32; flags++)
        foreach (EBlendMode blend in Enum.GetValues<EBlendMode>())
        foreach (ShouldCacheVertexFactoryContext vf in new ShouldCacheVertexFactoryContext[] { null, new(false), new(true) })
        {
            bool Bit(int index) => (flags & (1 << index)) != 0;
            var material = new ShouldCacheMaterialContext(Bit(0), false, Bit(1), false, false, false, false, Bit(2),
                BlendMode: blend, IsTerrainMaterial: Bit(3), UsedWithDecals: Bit(4));
            Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(name, material, "FLocalVertexFactory", vf, out bool actual));
            Assert.IsFalse(actual, name + "/" + flags + "/" + blend);
        }
        Assert.IsTrue(TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
        Assert.AreEqual(ShaderEnvironmentEvidence.ShaderClientReconstruction, environment.CompileFlagsEvidence);
        Assert.AreEqual(vertex ? 0x9000u : 0x9400u, environment.Target.CompileFlags);
        bool sdfVertex = vertex && shadow == "SignedDistanceFieldShadowTexture";
        Assert.AreEqual(vertex ? sdfVertex ? 7 : 6 : 5, environment.MacroDefinitions.Count);
        Assert.IsFalse(environment.MacroDefinitions.Any(m => m.Name == "SIMPLE_VERTEX_LIGHTMAP"));
        var staticLighting = environment.MacroDefinitions.Where(m => m.Name.StartsWith("STATICLIGHTING_", StringComparison.Ordinal)).ToArray();
        Assert.AreEqual(vertex ? sdfVertex ? 2 : 1 : 0, staticLighting.Length);
        if (vertex)
        {
            Assert.AreEqual(shadow.EndsWith("Texture", StringComparison.Ordinal) ? "STATICLIGHTING_TEXTUREMASK" : "STATICLIGHTING_VERTEXMASK", staticLighting[0].Name);
            Assert.AreEqual("1", staticLighting[0].Definition);
            Assert.AreEqual(ShaderEnvironmentEvidence.LE3RegistrationCallback, staticLighting[0].Evidence);
            if (sdfVertex)
            {
                Assert.AreEqual("STATICLIGHTING_SIGNEDDISTANCEFIELD", staticLighting[1].Name);
                Assert.AreEqual("1", staticLighting[1].Definition);
                Assert.AreEqual(ShaderEnvironmentEvidence.LE3RegistrationCallback, staticLighting[1].Evidence);
            }
        }
    }

    [TestMethod]
    public void ShShadowInventoryIsExcludedAsAKnownSubsetNotACompleteSet()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShShadowExclusions.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new BoolProperty(true, "bUsedAsSpecialEngineMaterial"));
        material.WriteProperty(new BoolProperty(true, "bUsedWithStaticLighting"));
        var compiler = new LE3MaterialShaderCompiler(material);
        var candidates = GetKnownShaderTypeRegistrations().Where(t => t.Name.Contains("FSphericalHarmonicLightPolicy", StringComparison.Ordinal) &&
            !t.Name.EndsWith("FNoStaticShadowingPolicy", StringComparison.Ordinal)).SelectMany(t => GetKnownVertexFactoryRegistrations().Select(v =>
            { Assert.IsTrue(TryGetKnownPermutationEnvironment(t.Name, v.Name, out var environment)); return environment.CreateCandidate(); })).ToArray();
        Assert.AreEqual(66, candidates.Length);
        var plan = compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(CommentTemplate()), MaterialResource.Create(), candidates);
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(0, plan.Jobs.Count);
        Assert.AreEqual(66, plan.ExcludedTargets.Count);
        Assert.IsFalse(ComparePlannedPermutations(plan, Array.Empty<ShaderPermutationIdentity>()).IsCompleteShaderSet);
        Assert.IsTrue(plan.Decisions.All(d => d.Disposition == ShaderCompileDisposition.Excluded && d.Gate == ShaderCompileGate.ShaderTypeShouldCache));
        Assert.IsTrue(TryGetKnownShaderTypeRegistration("TLightVertexShaderFSphericalHarmonicLightPolicyFSignedDistanceFieldShadowTexturePolicy", out _));
    }

    [TestMethod]
    [DataRow("ShadowTexture", true)]
    [DataRow("ShadowTexture", false)]
    [DataRow("ShadowVertexBuffer", true)]
    [DataRow("ShadowVertexBuffer", false)]
    [DataRow("SignedDistanceFieldShadowTexture", false)]
    [DataRow("SignedDistanceFieldShadowTexture", true)]
    public void ShShadowTypedArchivesPreserveOptionalPolicyAndLightingBindings(string shadow, bool vertex)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ShShadowObjects.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        string name = $"TLight{(vertex ? "Vertex" : "Pixel")}ShaderFSphericalHarmonicLightPolicyF{shadow}Policy";
        bool texture = shadow.EndsWith("Texture", StringComparison.Ordinal), sdf = shadow.StartsWith("SignedDistance", StringComparison.Ordinal);
        foreach (bool gpu in new[] { false, true })
        foreach (bool present in new[] { false, true })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", out var environment));
            string declarations = vertex ? "float4x4 LocalToWorld; " + (gpu ? "float4 BoneMatrices[3]; " : "") : "";
            string expression = vertex ? "mul(LocalToWorld,p)" + (gpu ? " + BoneMatrices[0]" : "") : "p";
            if (vertex && texture && present) { declarations += "float4 LightmapCoordinateScaleBias; "; expression += " + LightmapCoordinateScaleBias"; }
            if (!vertex && present)
            {
                declarations += "float4 WorldIncidentLighting[7]; bool bReceiveDynamicShadows; Texture2D LightAttenuationTexture; SamplerState LightAttenuationTextureSampler; ";
                expression += " + WorldIncidentLighting[0] + WorldIncidentLighting[6] + (bReceiveDynamicShadows ? 1 : 0) + LightAttenuationTexture.Sample(LightAttenuationTextureSampler,p.xy)";
                if (texture) { declarations += "Texture2D ShadowTexture; SamplerState ShadowTextureSampler; "; expression += " + ShadowTexture.Sample(ShadowTextureSampler,p.xy)"; }
                if (sdf) { declarations += "float4 DistanceFieldParameters; "; expression += " + DistanceFieldParameters"; }
            }
            var sources = new LE3ShaderSourceSnapshot(new System.Collections.Generic.Dictionary<string, string>
            {
                ["MaterialTemplate.usf"] = CommentTemplate(),
                [environment.Target.SourceFileName + ".usf"] = declarations + $"float4 Main(float4 p:{(vertex ? "POSITION" : "SV_Position")}):{(vertex ? "SV_Position" : "SV_Target")} {{ return {expression}; }}",
                [environment.Target.VertexFactoryShaderFileName + ".usf"] = "// retained CRC input"
            });
            translation = compiler.TranslateMaterialHlsl(sources);
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
            var compiled = compiler.CompileShader(job, sources); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
            var construction = compiler.ConstructKnownShader(translation, compiled);
            Assert.IsTrue(construction.Success, string.Join("; ", construction.Errors)); Assert.AreEqual(0, construction.UnboundParameters.Count);
            Shader shader = construction.Shader; using var stream = new MemoryStream();
            new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0; Shader copy = null;
            new SerializingContainer(stream, package, true).Serialize(ref copy);
            Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(shader.GetType(), copy.GetType());
            Assert.AreEqual(shader.ParameterMapCRC, copy.ParameterMapCRC); CollectionAssert.AreEqual(shader.ShaderByteCode, copy.ShaderByteCode);
            object policy = copy.GetType().GetField(vertex ? "ShadowingVertexParams" : "ShadowingPixelParams").GetValue(copy);
            if (vertex && texture) Assert.AreEqual(present ? (ushort)16 : (ushort)0, ((FShadowTexturePolicy.VertexParametersType)policy).LightmapCoordinateScaleBias.NumBytes);
            if (!vertex)
            {
                object light = copy.GetType().GetField("LightTypePixelParams").GetValue(copy);
                Assert.AreEqual(present ? (ushort)112 : (ushort)0, ((FSphericalHarmonicLightPolicy.PixelParametersType)light).WorldIncidentLighting.NumBytes);
                if (sdf) Assert.AreEqual(present ? (ushort)16 : (ushort)0, ((FSignedDistanceFieldShadowTexturePolicy.PixelParametersType)policy).DistanceFieldParameters.NumBytes);
                if (texture) Assert.AreEqual(present, ((FShaderResourceParameter)policy.GetType().GetField("ShadowTexture").GetValue(policy)).IsBound());
            }
            // Explicit construction of a disabled type tests layout, not native cache eligibility.
        }
    }

    [TestMethod]
    [DataRow("SFXPoint", true)]
    [DataRow("SFXPoint", false)]
    [DataRow("SphericalHarmonic", true)]
    [DataRow("SphericalHarmonic", false)]
    public void FinalLightPoliciesCacheLitMaterialsAndOnlySHVetoesTerrain(string light, bool vertex)
    {
        string name = $"TLight{(vertex ? "Vertex" : "Pixel")}ShaderF{light}LightPolicyFNoStaticShadowingPolicy";
        for (int flags = 0; flags < 64; flags++)
        foreach (EBlendMode blend in Enum.GetValues<EBlendMode>())
        {
            bool Bit(int index) => (flags & (1 << index)) != 0;
            var context = new ShouldCacheMaterialContext(Bit(0), Bit(2), Bit(3), false, false, false, false, Bit(4),
                IsTerrainMaterial: Bit(1), UsedWithDecals: Bit(5), BlendMode: blend);
            Assert.IsTrue(TryEvaluateKnownShaderTypeShouldCache(name, context, null, null, out bool actual, new(false)));
            Assert.AreEqual(!Bit(0) && (light != "SphericalHarmonic" || !Bit(1)), actual, name + "/" + flags + "/" + blend);
        }
    }

    [TestMethod]
    [DataRow("SFXPoint", true)]
    [DataRow("SFXPoint", false)]
    [DataRow("SphericalHarmonic", true)]
    [DataRow("SphericalHarmonic", false)]
    public void FinalLightPolicyFieldsRoundTripWithOptionalAllocationsPresentOrAbsent(string light, bool vertex)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FinalLights.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        string name = $"TLight{(vertex ? "Vertex" : "Pixel")}ShaderF{light}LightPolicyFNoStaticShadowingPolicy";
        foreach (bool gpu in new[] { false, true })
        foreach (bool optionalPresent in new[] { false, true })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", out var environment));
            string declarations = vertex ? "float4x4 LocalToWorld; " + (gpu ? "float4 BoneMatrices[3]; " : "") : "";
            string expression = vertex ? "mul(LocalToWorld,p)" + (gpu ? " + BoneMatrices[0]" : "") : "p";
            if (vertex && light == "SFXPoint")
            {
                declarations += "float4 LightPositionAndInvRadius; "; expression += " + LightPositionAndInvRadius";
                if (optionalPresent) { declarations += "float4x4 ShadowViewProjection; "; expression += " + mul(ShadowViewProjection,p)"; }
            }
            if (!vertex && optionalPresent)
            {
                declarations += "bool bReceiveDynamicShadows; Texture2D LightAttenuationTexture; SamplerState LightAttenuationTextureSampler; ";
                expression += " + (bReceiveDynamicShadows ? 1 : 0) + LightAttenuationTexture.Sample(LightAttenuationTextureSampler,p.xy)";
                if (light == "SphericalHarmonic")
                {
                    declarations += "float4 WorldIncidentLighting[7]; "; expression += " + WorldIncidentLighting[0] + WorldIncidentLighting[6]";
                }
                else
                {
                    declarations += "Texture2D LightSpaceShadowMap; SamplerState LightSpaceShadowMapSampler; float4 LightColorAndFalloffExponent, ShadowFilter, ShadowTextureRegion; float MaxVarianceShadowAttenuation; ";
                    expression += " + LightSpaceShadowMap.Sample(LightSpaceShadowMapSampler,p.xy) + LightColorAndFalloffExponent + ShadowFilter + ShadowTextureRegion + MaxVarianceShadowAttenuation";
                }
            }
            string source = declarations + $"float4 Main(float4 p:{(vertex ? "POSITION" : "SV_Position")}):{(vertex ? "SV_Position" : "SV_Target")} {{ return {expression}; }}";
            string directory = Path.Combine(Path.GetTempPath(), "LE3FinalLightTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, environment.Target.SourceFileName + ".usf"), source);
            try
            {
                var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target, environment.CreateCandidate().EnvironmentMacros);
                var compiled = compiler.CompileShader(job, directory); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
                var construction = compiler.ConstructKnownShader(translation, compiled); Assert.IsTrue(construction.Success, string.Join("; ", construction.Errors));
                Assert.AreEqual(0, construction.UnboundParameters.Count);
                Shader shader = construction.Shader; using var stream = new MemoryStream();
                new SerializingContainer(stream, package).Serialize(ref shader); stream.Position = 0; Shader copy = null;
                new SerializingContainer(stream, package, true).Serialize(ref copy);
                Assert.AreEqual(stream.Length, stream.Position); Assert.AreEqual(shader.GetType(), copy.GetType());
                Assert.AreEqual(shader.ParameterMapCRC, copy.ParameterMapCRC); CollectionAssert.AreEqual(shader.ShaderByteCode, copy.ShaderByteCode);
                string policyField = vertex ? "LightTypeVertexParams" : "LightTypePixelParams";
                object original = shader.GetType().GetField(policyField).GetValue(shader), loaded = copy.GetType().GetField(policyField).GetValue(copy);
                Assert.AreEqual(original, loaded);
                if (vertex && light == "SFXPoint")
                {
                    var parameters = (FSFXPointLightPolicy.VertexParametersType)loaded;
                    Assert.AreEqual((ushort)16, parameters.LightPositionAndInvRadius.NumBytes);
                    Assert.AreEqual(optionalPresent ? (ushort)64 : (ushort)0, parameters.ShadowViewProjection.NumBytes);
                }
                if (!vertex)
                {
                    Assert.AreEqual(optionalPresent, ((FShaderParameter)copy.GetType().GetField("bReceiveDynamicShadows").GetValue(copy)).IsBound());
                    Assert.AreEqual(optionalPresent, ((FShaderResourceParameter)copy.GetType().GetField("LightAttenuationTexture").GetValue(copy)).IsBound());
                    if (light == "SphericalHarmonic") Assert.AreEqual(optionalPresent ? (ushort)112 : (ushort)0,
                        ((FSphericalHarmonicLightPolicy.PixelParametersType)loaded).WorldIncidentLighting.NumBytes);
                    else Assert.AreEqual(optionalPresent, ((FSFXPointLightPolicy.PixelParametersType)loaded).LightSpaceShadowMap.IsBound());
                }
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    [TestMethod]
    [DataRow("FLocalVertexFactory")]
    [DataRow("FGPUSkinVertexFactory")]
    public void SFXPointVertexRequiresLightPositionAfterValidFactoryBindings(string factory)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SFXPointRequired.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        Assert.IsTrue(TryGetKnownPermutationEnvironment("TLightVertexShaderFSFXPointLightPolicyFNoStaticShadowingPolicy", factory, out var environment));
        string source = "float4x4 LocalToWorld; " + (factory == "FGPUSkinVertexFactory" ? "float4 BoneMatrices[3]; " : "") +
            "float4 Main(float4 p:POSITION):SV_Position { return mul(LocalToWorld,p)" + (factory == "FGPUSkinVertexFactory" ? " + BoneMatrices[0]" : "") + "; }";
        string directory = Path.Combine(Path.GetTempPath(), "LE3SFXRequired", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, environment.Target.SourceFileName + ".usf"), source);
        try
        {
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), environment.Target);
            var compiled = compiler.CompileShader(job, directory); Assert.IsTrue(compiled.Success, compiled.Diagnostics);
            var result = compiler.ConstructKnownShader(translation, compiled);
            Assert.IsFalse(result.Success); Assert.IsNull(result.Shader);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("LightPositionAndInvRadius", StringComparison.Ordinal)));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    [DataRow("Directional", "NoStaticShadowing", true)]
    [DataRow("Directional", "NoStaticShadowing", false)]
    [DataRow("Point", "NoStaticShadowing", true)]
    [DataRow("Point", "NoStaticShadowing", false)]
    [DataRow("Spot", "NoStaticShadowing", true)]
    [DataRow("Spot", "NoStaticShadowing", false)]
    [DataRow("Directional", "ShadowVertexBuffer", true)]
    [DataRow("Directional", "ShadowVertexBuffer", false)]
    [DataRow("Point", "ShadowVertexBuffer", true)]
    [DataRow("Point", "ShadowVertexBuffer", false)]
    [DataRow("Spot", "ShadowVertexBuffer", true)]
    [DataRow("Spot", "ShadowVertexBuffer", false)]
    [DataRow("Directional", "ShadowTexture", true)]
    [DataRow("Directional", "ShadowTexture", false)]
    [DataRow("Point", "ShadowTexture", true)]
    [DataRow("Point", "ShadowTexture", false)]
    [DataRow("Spot", "ShadowTexture", true)]
    [DataRow("Spot", "ShadowTexture", false)]
    [DataRow("Directional", "SignedDistanceFieldShadowTexture", true)]
    [DataRow("Directional", "SignedDistanceFieldShadowTexture", false)]
    [DataRow("Point", "SignedDistanceFieldShadowTexture", true)]
    [DataRow("Point", "SignedDistanceFieldShadowTexture", false)]
    [DataRow("Spot", "SignedDistanceFieldShadowTexture", true)]
    [DataRow("Spot", "SignedDistanceFieldShadowTexture", false)]
    public void LightPolicyFieldsBindAndRoundTripForEachRegisteredIdentity(string light, string policy, bool vertex)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3LightObjects.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        string name = $"TLight{(vertex ? "Vertex" : "Pixel")}ShaderF{light}LightPolicyF{policy}Policy";
        bool texture = policy.EndsWith("Texture", StringComparison.Ordinal), sdf = policy.StartsWith("SignedDistance", StringComparison.Ordinal);
        foreach (bool gpu in new[] { false, true })
        {
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, gpu ? "FGPUSkinVertexFactory" : "FLocalVertexFactory", out var environment));
            var candidate = environment.CreateCandidate();
            string[] lightFields = vertex ? [light == "Directional" ? "LightDirection" : "LightPositionAndInvRadius"] : light switch
            {
                "Directional" => ["LightColor", "bReceiveDynamicShadows", "bEnableDistanceShadowFading", "DistanceFadeParameters"],
                "Spot" => ["SpotAngles", "SpotDirection", "LightColorAndFalloffExponent"], _ => ["LightColorAndFalloffExponent"]
            };
            string[] constants = lightFields.Concat(vertex ? texture ? ["LightmapCoordinateScaleBias"] : Array.Empty<string>() :
                (sdf ? new[] { "DistanceFieldParameters", "bReceiveDynamicShadows" } : new[] { "bReceiveDynamicShadows" })).Distinct().ToArray();
            string source = string.Join(" ", constants.Select(f => "float4 " + f + ";")) +
                (vertex ? " float4x4 LocalToWorld; " + (gpu ? "float4 BoneMatrices[3]; " : "") :
                    " Texture2D LightAttenuationTexture; SamplerState LightAttenuationTextureSampler; " +
                    (texture ? "Texture2D ShadowTexture; SamplerState ShadowTextureSampler; " : "")) +
                (vertex ? "float4 Main(float4 p:POSITION):SV_Position { return mul(LocalToWorld,p)" : "float4 Main(float4 p:SV_Position):SV_Target { return p") +
                "+" + string.Join("+", constants) + (vertex ? gpu ? "+BoneMatrices[0]" : "" :
                    "+LightAttenuationTexture.Sample(LightAttenuationTextureSampler,p.xy)" + (texture ? "+ShadowTexture.Sample(ShadowTextureSampler,p.xy)" : "")) + "; }";
            string directory = Path.Combine(Path.GetTempPath(), "LE3LightShaderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, candidate.Target.SourceFileName + ".usf"), source);
            try
            {
                var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), candidate.Target, candidate.EnvironmentMacros);
                var compiled = compiler.CompileShader(job, directory);
                Assert.IsTrue(compiled.Success, compiled.Diagnostics);
                var result = compiler.ConstructKnownShader(translation, compiled);
                Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
                Shader shader = result.Shader;
                using var stream = new MemoryStream();
                new SerializingContainer(stream, package).Serialize(ref shader);
                stream.Position = 0;
                Shader copy = null;
                new SerializingContainer(stream, package, isLoading: true).Serialize(ref copy);
                Assert.AreEqual(stream.Length, stream.Position);
                Assert.AreEqual(shader.GetType(), copy.GetType());
                Assert.AreEqual(shader.ParameterMapCRC, copy.ParameterMapCRC);
                Assert.AreEqual(vertex ? (byte)0 : (byte)3, (byte)copy.Frequency);
                object originalPolicy = shader.GetType().GetField(vertex ? "LightTypeVertexParams" : "LightTypePixelParams").GetValue(shader);
                object copiedPolicy = copy.GetType().GetField(vertex ? "LightTypeVertexParams" : "LightTypePixelParams").GetValue(copy);
                Assert.AreEqual(originalPolicy, copiedPolicy);
                Assert.AreEqual(shader.GetType().GetField(vertex ? "ShadowingVertexParams" : "ShadowingPixelParams").GetValue(shader),
                    copy.GetType().GetField(vertex ? "ShadowingVertexParams" : "ShadowingPixelParams").GetValue(copy));
                foreach (var field in copiedPolicy.GetType().GetFields())
                    Assert.AreEqual((ushort)16, ((FShaderParameter)field.GetValue(copiedPolicy)).NumBytes);
                if (vertex)
                {
                    Assert.AreEqual(candidate.Target.VertexFactoryTypeName, copy.VertexFactoryType.Value.Name);
                    var vf = (FVertexFactoryParameterRef)copy.GetType().GetField("VertexFactoryParameters").GetValue(copy);
                    Assert.AreEqual(candidate.Target.VertexFactoryTypeName, vf.VertexFactoryType.Name);
                }
                else
                {
                    Assert.IsNull(copy.VertexFactoryType);
                    Assert.AreEqual((ushort)1, ((FShaderResourceParameter)copy.GetType().GetField("LightAttenuationTexture").GetValue(copy)).NumResources);
                    var receive = (FShaderParameter)copy.GetType().GetField("bReceiveDynamicShadows").GetValue(copy);
                    Assert.AreEqual((ushort)16, receive.NumBytes);
                    if (light == "Directional") Assert.AreEqual(receive, ((FDirectionalLightPolicy.PixelParametersType)copiedPolicy).bReceiveDynamicShadows);
                    if (light == "Point") Assert.AreEqual(((FPointLightPolicy.PixelParametersType)copiedPolicy).WorldIncidentLighting,
                        ((FPointLightPolicy.PixelParametersType)copiedPolicy).LightColorAndFalloffExponent);
                }
                // Constructor validity is independent of ShouldCache eligibility. Synthetic GPU
                // static-shadow objects above test layout, not permission to select those jobs.
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    [TestMethod]
    [DataRow("Directional")]
    [DataRow("Point")]
    [DataRow("Spot")]
    public void MissingLightVertexPolicyParameterWithholdsEveryShadowVariant(string light)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3RequiredLight.pcc", MEGame.LE3);
        var compiler = new LE3MaterialShaderCompiler(package.CreateExport("Material", "Material", indexed: false));
        var translation = compiler.TranslateMaterialHlsl(CommentTemplate());
        foreach (string policy in new[] { "NoStaticShadowing", "ShadowVertexBuffer", "ShadowTexture", "SignedDistanceFieldShadowTexture" })
        {
            string name = $"TLightVertexShaderF{light}LightPolicyF{policy}Policy";
            Assert.IsTrue(TryGetKnownPermutationEnvironment(name, "FLocalVertexFactory", out var environment));
            var candidate = environment.CreateCandidate();
            var job = compiler.BuildProvisionalCompileJob(translation, MaterialResource.Create(), candidate.Target, candidate.EnvironmentMacros);
            string directory = Path.Combine(Path.GetTempPath(), "LE3RequiredLightTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, candidate.Target.SourceFileName + ".usf"), "float4 Main(float4 p:POSITION):SV_Position { return p; }");
            try
            {
                var result = compiler.ConstructKnownShader(translation, compiler.CompileShader(job, directory));
                Assert.IsFalse(result.Success);
                Assert.IsNull(result.Shader);
                StringAssert.Contains(result.Errors.Single(), light == "Directional" ? "LightDirection" : "LightPositionAndInvRadius");
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
    }

    private static string CommentTemplate() => "/*\n%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n*/";
}
