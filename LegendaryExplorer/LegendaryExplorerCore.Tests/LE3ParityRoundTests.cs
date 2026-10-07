using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

/// <summary>
/// Shipping-parity behaviour changes: the effects-owner (URvrEffectsMaterialUser slot 127) resource ShouldCache gate,
/// connected WorldPositionOffset in the resource path, and the Grayscale/OneBitAlpha/BC7 affine unpack compressions.
/// </summary>
[TestClass]
public class LE3ParityRoundTests
{
    private const string MeshShader = "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight";

    // ---- Effects-owner resource gate -------------------------------------------------------------------------

    [TestMethod]
    [DataRow("FLocalVertexFactory", true, true)]
    [DataRow("FLocalVertexFactory", false, false)]
    [DataRow("FLocalVertexFactory", null, false)]
    [DataRow("FLocalDecalVertexFactory", true, true)]
    [DataRow("FLocalDecalVertexFactory", false, true)]
    [DataRow("FLocalDecalVertexFactory", null, true)]
    [DataRow("FGPUSkinVertexFactory", true, true)]
    [DataRow("FGPUSkinVertexFactory", false, true)]
    [DataRow("FGPUSkinVertexFactory", null, true)]
    [DataRow("FParticleVertexFactory", true, true)]
    [DataRow("FParticleVertexFactory", false, true)]
    [DataRow("FParticleVertexFactory", null, true)]
    public void EffectsOwnerGateRejectsOnlyTheExactLocalFactoryWithoutStaticMeshSupport(string factory, bool? supportsStaticMeshes, bool expectedCaches)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3EffectsOwnerTruth.pcc", MEGame.LE3);
        var owner = package.CreateExport("Owner", "RvrEffectsMaterialUser", indexed: false);
        if (supportsStaticMeshes != null) owner.WriteProperty(new BoolProperty(supportsStaticMeshes.Value, "m_bSupportsStaticMeshes"));
        var plan = PlanSingle(package, owner, MeshShader, factory);
        var decision = plan.Decisions.Single();
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors));
        Assert.AreNotEqual(ShaderCompileDisposition.Unresolved, decision.Disposition);
        if (expectedCaches)
        {
            Assert.AreEqual(1, plan.Jobs.Count, decision.Reason);
            Assert.AreEqual(0, plan.ExcludedTargets.Count);
        }
        else
        {
            Assert.AreEqual(0, plan.Jobs.Count);
            Assert.AreEqual(1, plan.ExcludedTargets.Count);
            Assert.AreEqual(ShaderCompileGate.MaterialResourceShouldCache, decision.Gate);
            Assert.AreEqual(ShaderCompileDisposition.Excluded, decision.Disposition);
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [DataRow(null)]
    public void EffectsOwnerGatePassesEveryNonMeshShaderRegardlessOfStaticMeshSupport(bool? supportsStaticMeshes)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3EffectsOwnerNonMesh.pcc", MEGame.LE3);
        var owner = package.CreateExport("Owner", "RvrEffectsMaterialUser", indexed: false);
        if (supportsStaticMeshes != null) owner.WriteProperty(new BoolProperty(supportsStaticMeshes.Value, "m_bSupportsStaticMeshes"));
        var nonMesh = GetKnownShaderTypeRegistrations().Where(s => !s.IsMeshMaterial).ToArray();
        Assert.IsTrue(nonMesh.Length > 0);
        int jobs = 0, index = 0;
        foreach (var shader in nonMesh)
        {
            // Each plan needs its own material export; the owner is shared.
            var plan = PlanSingle(package, owner, shader.Name, null, "Material" + index++);
            Assert.IsTrue(plan.Success, shader.Name + ": " + string.Join("; ", plan.Errors));
            Assert.IsFalse(plan.Decisions.Any(d => d.Gate == ShaderCompileGate.MaterialResourceShouldCache && d.Disposition != ShaderCompileDisposition.Eligible),
                shader.Name + " must never be excluded or left unresolved by the effects-owner gate.");
            jobs += plan.Jobs.Count;
        }
        Assert.IsTrue(jobs > 0, "At least one non-mesh shader must reach the resource gate and compile for the default material.");
    }

    [TestMethod]
    [DataRow("FLocalVertexFactory")]
    [DataRow("FGPUSkinVertexFactory")]
    public void ImportEffectsOwnerIsUnresolvedBecauseItsPropertiesCannotBeRead(string factory)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3EffectsOwnerImport.pcc", MEGame.LE3);
        var owner = new ImportEntry(package, 0, "ImportedOwner") { ClassName = "RvrEffectsMaterialUser", PackageFile = "Donor" };
        package.AddImport(owner);
        var plan = PlanSingle(package, owner, MeshShader, factory);
        Assert.IsFalse(plan.Success); Assert.AreEqual(0, plan.Jobs.Count); Assert.AreEqual(0, plan.ExcludedTargets.Count);
        var decision = plan.Decisions.Single();
        Assert.AreEqual(ShaderCompileDisposition.Unresolved, decision.Disposition);
        Assert.AreEqual(ShaderCompileGate.MaterialResourceShouldCache, decision.Gate);
    }

    [TestMethod]
    public void OrdinaryNonEffectsParentKeepsTheUnconditionalTrueGate()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3OrdinaryParent.pcc", MEGame.LE3);
        var parent = package.CreateExport("Parent", "Package", indexed: false);
        var plan = PlanSingle(package, parent, MeshShader, "FLocalVertexFactory");
        Assert.IsTrue(plan.Success, string.Join("; ", plan.Errors)); Assert.AreEqual(1, plan.Jobs.Count);
    }

    private static ShaderCompilePlan PlanSingle(IMEPackage package, IEntry owner, string shaderType, string vertexFactory, string materialName = "Material")
    {
        var material = package.CreateExport(materialName, "Material", owner, indexed: false);
        // Special engine materials pass every vertex-factory usage gate, so the owner gate is the only variable.
        material.WriteProperty(new BoolProperty(true, "bUsedAsSpecialEngineMaterial"));
        var compiler = new LE3MaterialShaderCompiler(material);
        Assert.IsTrue(TryGetKnownPermutationEnvironment(shaderType, vertexFactory, out var environment), shaderType + " / " + vertexFactory);
        return compiler.BuildProvisionalCompilePlan(compiler.TranslateMaterialHlsl(Template()), MaterialResource.Create(), [environment.CreateCandidate()]);
    }

    // ---- Connected WorldPositionOffset in the resource path ---------------------------------------------------

    [TestMethod]
    public void ConnectedResourceWorldPositionOffsetEmitsVertexCodeAndVertexUniformsWithoutSelectionColor()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ParityWpoVertex.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var offset = package.CreateExport("Offset", "MaterialExpressionVectorParameter", material, indexed: false);
        offset.WriteProperty(new NameProperty("Offset", "ParameterName"));
        material.WriteProperty(Input("WorldPositionOffset", offset.UIndex));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsTrue(result.PropertyCode.ContainsKey("WorldPositionOffset"));
        StringAssert.Contains(result.PropertyCode["WorldPositionOffset"], "UniformVertexVector_0");
        // Exactly the authored Offset parameter; the pixel-root SelectionColor parameter is not created for a vertex property.
        Assert.AreEqual(1, result.UniformVertexVectorExpressions.Count);
        var parameter = (MaterialUniformExpressionVectorParameter)result.UniformVertexVectorExpressions.Single();
        Assert.AreEqual("Offset", parameter.ParameterName.Name);
        Assert.IsFalse(result.UniformVertexVectorExpressions.OfType<MaterialUniformExpressionVectorParameter>().Any(p => p.ParameterName.Name == "SelectionColor"));
        Assert.IsFalse(result.PropertyCode["WorldPositionOffset"].Contains("SelectionColor"));
        Assert.AreEqual(0, result.UniformVertexScalarExpressions.Count);
        // SelectionColor belongs to the pixel roots only.
        Assert.AreEqual("SelectionColor", ((MaterialUniformExpressionVectorParameter)result.UniformPixelVectorExpressions.Single()).ParameterName.Name);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
        StringAssert.Contains(result.MaterialHlsl, "UniformVertexVector_0");
    }

    [TestMethod]
    public void UnconnectedResourceWorldPositionOffsetStillEmitsNothing()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ParityWpoNone.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsFalse(result.PropertyCode.ContainsKey("WorldPositionOffset"));
        Assert.AreEqual(0, result.UniformVertexVectorExpressions.Count);
    }

    [TestMethod]
    public void ConnectedResourceWorldPositionOffsetCompilesInMaterialInputOrderBetweenAnisotropicAndWrapLighting()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ParityWpoOrder.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(ConstantVectorInput("AnisotropicDirection", 11, 12, 13));
        material.WriteProperty(ConstantVectorInput("WorldPositionOffset", 21, 22, 23));
        material.WriteProperty(ConstantVectorInput("WrapLightingCoefficients", 31, 32, 33));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var keys = result.PropertyCode.Keys.ToList();
        Assert.IsTrue(keys.IndexOf("AnisotropicDirection") >= 0 && keys.IndexOf("AnisotropicDirection") < keys.IndexOf("WorldPositionOffset"));
        Assert.IsTrue(keys.IndexOf("WorldPositionOffset") < keys.IndexOf("WrapLightingCoefficients"));
        int aniso = result.MaterialHlsl.IndexOf("float3(11.00000000,12.00000000,13.00000000)", StringComparison.Ordinal);
        int wpo = result.MaterialHlsl.IndexOf("float3(21.00000000,22.00000000,23.00000000)", StringComparison.Ordinal);
        int wrap = result.MaterialHlsl.IndexOf("float3(31.00000000,32.00000000,33.00000000)", StringComparison.Ordinal);
        Assert.IsTrue(aniso >= 0 && aniso < wpo && wpo < wrap, $"{aniso} < {wpo} < {wrap}");
    }

    [TestMethod]
    public void ResourceWorldPositionOffsetUseConstantIsAVectorLikeNormal()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ParityWpoConstant.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(ConstantVectorInput("WorldPositionOffset", 1.5f, -2, 3));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("\treturn float3(1.50000000,-2.00000000,3.00000000)", result.PropertyCode["WorldPositionOffset"]);
        Assert.AreEqual(0, result.UniformVertexVectorExpressions.Count + result.UniformVertexScalarExpressions.Count);
        // Only the pixel roots' SelectionColor parameter exists; the constant adds no uniform of its own.
        Assert.AreEqual("SelectionColor", ((MaterialUniformExpressionVectorParameter)result.UniformPixelVectorExpressions.Single()).ParameterName.Name);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
        // Same Vector constant layout as Normal.
        material.WriteProperty(ConstantVectorInput("Normal", 1.5f, -2, 3));
        var normal = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.AreEqual(result.PropertyCode["WorldPositionOffset"], normal.PropertyCode["Normal"]);
        // A UseConstant input with no authored Constant defaults to zero.
        material.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new BoolProperty(true, "UseConstant") }, "WorldPositionOffset"));
        var zero = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(zero.Success, string.Join("; ", zero.Errors));
        Assert.AreEqual("\treturn float3(0.00000000,0.00000000,0.00000000)", zero.PropertyCode["WorldPositionOffset"]);
    }

    [TestMethod]
    public void InheritedResourceWorldPositionOffsetExpressionStillFailsButInheritedConstantUsesTheConstant()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ParityWpoInherited.pcc", MEGame.LE3);
        var archetype = package.CreateExport("Archetype", "Material", indexed: false);
        var node = package.CreateExport("Offset", "MaterialExpressionConstant3Vector", archetype, indexed: false);
        archetype.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(node.UIndex, "Expression") }, "WorldPositionOffset"));
        var material = package.CreateExport("Material", "Material", indexed: false); material.Archetype = archetype;
        var failed = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsFalse(failed.Success); Assert.IsNull(failed.MaterialHlsl);
        StringAssert.Contains(string.Join("; ", failed.Errors), "inherited expression reference requiring graph/package resolution");
        archetype.WriteProperty(ConstantVectorInput("WorldPositionOffset", 4, 5, 6));
        var constant = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsTrue(constant.Success, string.Join("; ", constant.Errors));
        Assert.AreEqual("\treturn float3(4.00000000,5.00000000,6.00000000)", constant.PropertyCode["WorldPositionOffset"]);
    }

    // ---- Texture compressions ---------------------------------------------------------------------------------

    [TestMethod]
    [DataRow("TC_Grayscale")]
    [DataRow("TC_OneBitAlpha")]
    [DataRow("TC_BC7")]
    public void GrayscaleOneBitAlphaAndBc7TexturesUseTheOrdinaryAffineUnpack(string compression)
    {
        string baseline = Translate("TC_Default", false).PropertyCode["EmissiveColor"];
        StringAssert.Contains(baseline, "9.00000000,11.00000000,13.00000000,15.00000000");
        foreach (bool half in new[] { false, true })
        {
            var result = Translate(compression, half);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            string code = result.PropertyCode["EmissiveColor"];
            Assert.AreEqual(Translate("TC_Default", half).PropertyCode["EmissiveColor"], code);
            Assert.IsFalse(code.Contains("BiasNormalizeNormalMap"));
            Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        }
        // Normalmap-style handling is unaffected.
        StringAssert.Contains(Translate("TC_Normalmap", false).PropertyCode["EmissiveColor"], "BiasNormalizeNormalMap");

        TranslationResult Translate(string settings, bool half)
        {
            GlobalTest.Init();
            var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ParityCompression.pcc", MEGame.LE3);
            var material = TextureMaterial(package);
            var metadata = new TextureCompilationMetadata(TextureDimension.Texture2D, settings, new Vector4(-2, -3, -4, -5), new Vector4(7, 8, 9, 10));
            return new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => metadata).TranslateMaterialHlsl(Template(), half);
        }
    }

    [TestMethod]
    [DataRow("TC_Displacementmap")]
    [DataRow("TC_VectorDisplacementmap")]
    [DataRow("TC_HDR")]
    public void OtherSpecialCompressionsAreStillRejected(string compression)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ParityCompressionRejected.pcc", MEGame.LE3);
        var material = TextureMaterial(package);
        var metadata = new TextureCompilationMetadata(TextureDimension.Texture2D, compression);
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => metadata).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        StringAssert.Contains(string.Join("; ", result.Errors), "CompressionSettings=" + compression);
    }

    // ---- Helpers ----------------------------------------------------------------------------------------------

    private static ExportEntry TextureMaterial(IMEPackage package)
    {
        var material = package.CreateExport("Material", "Material", indexed: false);
        var texture = package.CreateExport("Texture", "Texture2D", indexed: false);
        var sample = package.CreateExport("Sample", "MaterialExpressionTextureSample", material, indexed: false);
        sample.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        material.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection
        {
            new ObjectProperty(sample.UIndex, "Expression"), new BoolProperty(true, "Mask"), new BoolProperty(true, "MaskR"),
            new BoolProperty(true, "MaskG"), new BoolProperty(true, "MaskB"), new BoolProperty(false, "MaskA")
        }, "EmissiveColor"));
        return material;
    }

    private static StructProperty Input(string name, int expression) =>
        new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression, "Expression") }, name);

    private static StructProperty ConstantVectorInput(string name, float x, float y, float z) => new("ExpressionInput", new PropertyCollection
    {
        new ObjectProperty(0, "Expression"), new BoolProperty(true, "UseConstant"),
        new StructProperty("Vector", new PropertyCollection { new FloatProperty(x, "X"), new FloatProperty(y, "Y"), new FloatProperty(z, "Z") }, "Constant", isImmutable: true)
    }, name);

    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";

    // ---- Blend-mode environment ------------------------------------------------------------------------------

    [TestMethod]
    [DataRow("BLEND_Opaque", "MATERIALBLENDING_SOLID")]
    [DataRow("BLEND_Masked", "MATERIALBLENDING_MASKED")]
    [DataRow("BLEND_SoftMasked", "MATERIALBLENDING_SOFTMASKED")]
    [DataRow("BLEND_Translucent", "MATERIALBLENDING_TRANSLUCENT")]
    [DataRow("BLEND_Additive", "MATERIALBLENDING_ADDITIVE")]
    [DataRow("BLEND_AlphaComposite", "MATERIALBLENDING_ADDITIVE")]
    [DataRow("BLEND_Modulate", "MATERIALBLENDING_MODULATE")]
    public void BlendModeSelectsExactlyOneShippingBlendingMacro(string blendMode, string expectedMacro)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3BlendMacroTest.pcc", MEGame.LE3);
        var material = package.CreateExport("BlendMaterial", "Material", indexed: false);
        material.WriteProperty(new EnumProperty(blendMode, "EBlendMode", MEGame.LE3, "BlendMode"));
        var compiler = new LE3MaterialShaderCompiler(material);
        var macros = compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), compiler.AnalyzeGraph());
        var blending = macros.Keys.Where(k => k.StartsWith("MATERIALBLENDING_", StringComparison.Ordinal)).ToArray();
        CollectionAssert.AreEqual(new[] { expectedMacro }, blending, blendMode);
        Assert.AreEqual("1", macros[expectedMacro]);
        Assert.IsFalse(macros.ContainsKey("MATERIAL_ONELAYERDISTORTION"), blendMode);
    }

    [TestMethod]
    [DataRow("BLEND_Opaque", false, false, false)]
    [DataRow("BLEND_Masked", true, false, false)]
    [DataRow("BLEND_SoftMasked", true, false, false)]
    [DataRow("BLEND_Translucent", true, false, true)]
    [DataRow("BLEND_Translucent", false, true, false)]
    [DataRow("BLEND_Additive", true, false, true)]
    [DataRow("BLEND_AlphaComposite", true, false, true)]
    [DataRow("BLEND_Modulate", true, false, true)]
    [DataRow("BLEND_Modulate", true, true, true)]
    [DataRow("BLEND_Opaque", true, true, false)]
    public void OneLayerDistortionFollowsTheStoredFlagForBlendedOutput(string blendMode, bool oneLayer, bool usesDistortion, bool expected)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3OneLayerTest.pcc", MEGame.LE3);
        var material = package.CreateExport("OneLayerMaterial", "Material", indexed: false);
        material.WriteProperty(new EnumProperty(blendMode, "EBlendMode", MEGame.LE3, "BlendMode"));
        material.WriteProperty(new BoolProperty(oneLayer, "bUseOneLayerDistortion"));
        material.WriteProperty(new BoolProperty(usesDistortion, "bUsesDistortion"));
        var compiler = new LE3MaterialShaderCompiler(material);
        var macros = compiler.BuildProvisionalMaterialMacros(MaterialResource.Create(), compiler.AnalyzeGraph());
        Assert.AreEqual(expected, macros.ContainsKey("MATERIAL_ONELAYERDISTORTION"));
        if (expected) Assert.AreEqual("1", macros["MATERIAL_ONELAYERDISTORTION"]);
        // Native IsDistorted (ab51b30): bUsesDistortion && !bUseOneLayerDistortion.
        Assert.AreEqual(usesDistortion && !oneLayer, compiler.BuildShouldCacheMaterialContext().IsDistorted);
    }
}
