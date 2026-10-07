using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3SceneTextureTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void DefaultUvIsAlreadyScreenAlignedRegardlessOfNodeOption(bool half, bool align)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var scene = Scene(package, material, align); material.WriteProperty(Input("EmissiveColor", scene));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.IsTrue(result.UsesSceneColor); Assert.IsFalse(result.UsesSceneDepth);
        Assert.IsFalse(result.ForcesFloatTexCoords); Assert.IsFalse(result.UsesTime); Assert.AreEqual(0u, result.NumUserTexCoords);
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], $"{(half ? "half4" : "float4")} Local0 = tex2D(SceneColorTexture,float2(ScreenAlignedPosition(Parameters.ScreenPosition).xy));");
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count + result.UniformCubeTextureExpressions.Count + result.UniformExpressionTextures.Count);
    }

    [TestMethod]
    [DataRow(1, false)]
    [DataRow(1, true)]
    [DataRow(2, false)]
    [DataRow(2, true)]
    [DataRow(3, false)]
    [DataRow(3, true)]
    [DataRow(4, false)]
    [DataRow(4, true)]
    public void ExplicitUvUsesFullPrecisionScalarReplicationOrFloat2AndRejectsOtherWidths(int width, bool align)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var uv = package.CreateExport("Uv", width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector", material, indexed: false);
        uv.WriteProperty(new FloatProperty(.25f, "R")); if (width > 1) uv.WriteProperty(new FloatProperty(.75f, "G"));
        var scene = Scene(package, material, align); scene.WriteProperty(Input("Coordinates", uv)); material.WriteProperty(Input("EmissiveColor", scene));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), useHalfTypes: true);
        Assert.AreEqual(width <= 2, result.Success, string.Join("; ", result.Errors)); Assert.IsTrue(result.UsesSceneColor);
        if (width > 2) { Assert.IsNull(result.MaterialHlsl); StringAssert.Contains(string.Join("; ", result.Errors), "Coercion failed:"); return; }
        string code = width == 1 ? "float2(float(0.25000000),float(0.25000000))" : "float2(0.25000000,0.75000000)";
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], $"tex2D(SceneColorTexture,{(align ? $"ScreenAlignedUV({code})" : code + " * DynamicScale.xy")})");
    }

    [TestMethod]
    [DataRow("enum")]
    [DataRow("byte")]
    [DataRow("numbered")]
    [DataRow("wrongType")]
    public void UnsupportedSceneTextureTypesFailBeforeUsageIsSet(string shape)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var scene = Scene(package, material, false);
        switch (shape)
        {
            case "enum": scene.WriteProperty(new EnumProperty("SceneTex_MAX", "ESceneTextureType", MEGame.LE3, "SceneTextureType")); break;
            case "byte": scene.WriteProperty(new ByteProperty(1, "SceneTextureType")); break;
            case "numbered": scene.WriteProperty(new EnumProperty(new NameReference("SceneTex_Lighting", 1), "ESceneTextureType", MEGame.LE3, "SceneTextureType")); break;
            default: scene.WriteProperty(new IntProperty(0, "SceneTextureType")); break;
        }
        material.WriteProperty(Input("EmissiveColor", scene)); var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsFalse(result.UsesSceneColor); Assert.IsNull(result.MaterialHlsl);
        CollectionAssert.AreEqual(new[] { "Scene texture type not supported." }, result.Errors.ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void VertexGuardPrecedesTypeValidationAndDoesNotSetUsage(bool unsupportedType)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var scene = Scene(package, material, false);
        if (unsupportedType) scene.WriteProperty(new ByteProperty(1, "SceneTextureType"));
        material.WriteProperty(Input("WorldPositionOffset", scene)); var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsFalse(result.UsesSceneColor);
        CollectionAssert.AreEqual(new[] { "Invalid node used in vertex shader input!" }, result.Errors.ToArray());
    }

    [TestMethod]
    [DataRow("BLEND_Opaque", false, false)]
    [DataRow("BLEND_Masked", false, false)]
    [DataRow("BLEND_SoftMasked", false, false)]
    [DataRow("BLEND_Opaque", true, true)]
    [DataRow("BLEND_Masked", true, true)]
    [DataRow("BLEND_SoftMasked", true, true)]
    [DataRow("BLEND_Translucent", false, true)]
    [DataRow("BLEND_Additive", false, true)]
    [DataRow("BLEND_Modulate", false, true)]
    [DataRow("BLEND_AlphaComposite", false, true)]
    public void ResourceGateRequiresTransparentBlendOrMaterialEffect(string blend, bool effect, bool success)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        material.WriteProperty(new EnumProperty(blend, "EBlendMode", MEGame.LE3, "BlendMode")); material.WriteProperty(new BoolProperty(effect, "bUsedWithMaterialEffect"));
        var scene = Scene(package, material, false); material.WriteProperty(Input("EmissiveColor", scene));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.AreEqual(success, result.Success, string.Join("; ", result.Errors)); Assert.IsTrue(result.UsesSceneColor);
        if (!success) CollectionAssert.AreEqual(new[] { "Only transparent materials can read from scene color." }, result.Errors.ToArray());
    }

    [TestMethod]
    public void OpaqueDepthColorAndDecalErrorsRetainNativeOrderAndIndependentPredicates()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); material.WriteProperty(new BoolProperty(true, "bUsedWithDecals"));
        var scene = Scene(package, material, false); var depth = package.CreateExport("Depth", "MaterialExpressionSceneDepth", material, indexed: false);
        material.WriteProperty(Input("EmissiveColor", scene)); material.WriteProperty(Input("Opacity", depth)); var compiler = new LE3MaterialShaderCompiler(material);
        var result = compiler.TranslateMaterialResourceHlsl(Template()); Assert.IsTrue(result.UsesSceneDepth); Assert.IsTrue(result.UsesSceneColor);
        CollectionAssert.AreEqual(new[] { "Only transparent materials can read from scene depth.", "Only transparent materials can read from scene color.", "Materials used with decals must not read from scene color." }, result.Errors.ToArray());
        material.WriteProperty(new BoolProperty(true, "bUsedWithMaterialEffect")); result = compiler.TranslateMaterialResourceHlsl(Template());
        CollectionAssert.AreEqual(new[] { "Materials used with decals must not read from scene color." }, result.Errors.ToArray());
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void AlignedScreenPositionElsewhereInPropertySuppressesScaleOnlyWhenAlreadyCompiled(bool sceneFirst, bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var screen = package.CreateExport("Screen", "MaterialExpressionScreenPosition", material, indexed: false); screen.WriteProperty(new BoolProperty(true, "ScreenAlign"));
        var uv = package.CreateExport("Uv", "MaterialExpressionConstant2Vector", material, indexed: false);
        var scene = Scene(package, material, false); scene.WriteProperty(Input("Coordinates", uv));
        var add = package.CreateExport("Sum", "MaterialExpressionAdd", material, indexed: false); add.WriteProperty(Input("A", sceneFirst ? scene : screen)); add.WriteProperty(Input("B", sceneFirst ? screen : scene));
        material.WriteProperty(Input("EmissiveColor", add)); var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.IsTrue(result.UsesSceneColor);
        Assert.AreEqual(sceneFirst, result.PropertyCode["EmissiveColor"].Contains(" * DynamicScale.xy", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AlignedScreenPositionInEarlierPropertyDoesNotSuppressNextPropertyScale(bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var screen = package.CreateExport("Screen", "MaterialExpressionScreenPosition", material, indexed: false); screen.WriteProperty(new BoolProperty(true, "ScreenAlign")); material.WriteProperty(Input("Normal", screen));
        var uv = package.CreateExport("Uv", "MaterialExpressionConstant2Vector", material, indexed: false); var scene = Scene(package, material, false); scene.WriteProperty(Input("Coordinates", uv)); material.WriteProperty(Input("EmissiveColor", scene));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); StringAssert.Contains(result.PropertyCode["EmissiveColor"], " * DynamicScale.xy");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InactiveSceneBranchDoesNotLeakUsageAcrossTranslations(bool active)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var scene = Scene(package, material, false);
        var zero = package.CreateExport("Zero", "MaterialExpressionConstant", material, indexed: false); var branch = package.CreateExport("Branch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        branch.WriteProperty(new NameProperty("ReadScene", "ParameterName")); branch.WriteProperty(new BoolProperty(active, "DefaultValue")); branch.WriteProperty(Input("A", scene)); branch.WriteProperty(Input("B", zero)); material.WriteProperty(Input("EmissiveColor", branch));
        var compiler = new LE3MaterialShaderCompiler(material); var result = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.AreEqual(!active, result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(active, result.UsesSceneColor);
        material.WriteProperty(Input("EmissiveColor", zero)); result = compiler.TranslateMaterialResourceHlsl(Template()); Assert.IsTrue(result.Success); Assert.IsFalse(result.UsesSceneColor);
    }
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CachedSampleIsNotRecompiledAfterLaterScreenPositionChangesPropertyState(bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var uv = package.CreateExport("Uv", "MaterialExpressionConstant2Vector", material, indexed: false); var scene = Scene(package, material, false); scene.WriteProperty(Input("Coordinates", uv));
        var screen = package.CreateExport("Screen", "MaterialExpressionScreenPosition", material, indexed: false); screen.WriteProperty(new BoolProperty(true, "ScreenAlign"));
        var first = package.CreateExport("First", "MaterialExpressionAdd", material, indexed: false); first.WriteProperty(Input("A", scene)); first.WriteProperty(Input("B", screen));
        var repeated = package.CreateExport("Repeated", "MaterialExpressionAdd", material, indexed: false); repeated.WriteProperty(Input("A", first)); repeated.WriteProperty(Input("B", scene)); material.WriteProperty(Input("EmissiveColor", repeated));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half); Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(1, result.PropertyCode["EmissiveColor"].Split("tex2D(SceneColorTexture,").Length - 1);
        Assert.AreEqual(1, result.PropertyCode["EmissiveColor"].Split(" * DynamicScale.xy").Length - 1);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitLightingEnumAndByteZeroUseTheSupportedSceneType(bool enumType)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var scene = Scene(package, material, false);
        if (enumType) scene.WriteProperty(new EnumProperty("SceneTex_Lighting", "ESceneTextureType", MEGame.LE3, "SceneTextureType")); else scene.WriteProperty(new ByteProperty(0, "SceneTextureType"));
        material.WriteProperty(Input("EmissiveColor", scene)); var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.IsTrue(result.UsesSceneColor);
    }

    [TestMethod]
    public void UnknownSceneColorBlendReturnsDiagnosticInsteadOfThrowing()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var scene = Scene(package, material, false);
        material.WriteProperty(new EnumProperty("BLEND_Unknown", "EBlendMode", MEGame.LE3, "BlendMode")); material.WriteProperty(Input("EmissiveColor", scene));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template()); Assert.IsFalse(result.Success); Assert.IsTrue(result.UsesSceneColor);
        StringAssert.Contains(string.Join("; ", result.Errors), "Unknown LE3 blend mode BLEND_Unknown.");
    }
    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("LE3SceneTexture.pcc", MEGame.LE3);
    private static ExportEntry Material(IMEPackage package)
    {
        var material = package.CreateExport("Material", "Material", indexed: false); material.WriteProperty(new EnumProperty("MLM_Unlit", "EMaterialLightingModel", MEGame.LE3, "LightingModel")); return material;
    }
    private static ExportEntry Scene(IMEPackage package, ExportEntry material, bool align)
    {
        var scene = package.CreateExport("Scene", "MaterialExpressionSceneTexture", material, indexed: false); scene.WriteProperty(new BoolProperty(align, "ScreenAlign")); return scene;
    }
    private static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
