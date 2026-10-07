using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3DepthBiasedTests
{
    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public void DefaultsPreserveNativeLocalCastsAndAppend(bool blend, bool half, bool normalize)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var node = Node(package, material, blend); node.WriteProperty(new BoolProperty(normalize, "bNormalize"));
        if (blend) node.WriteProperty(Input("Bias", Constant(package, material, 1, .5f)));
        string root = blend ? "EmissiveColor" : "Opacity"; material.WriteProperty(Input(root, node));
        var result = Translate(material, half); AssertSuccess(result);
        string t = half ? "half" : "float";
        string call = blend
            ? $"\t{t}3 Local0 = DepthBiasedBlend(Parameters,{t}3(0.00000000,0.00000000,0.00000000),{t}({t}(0.50000000)),{t}({t}(1.00000000)));\r\n\treturn {t}4(Local0,{t}(1.00000000)).rgb"
            : $"\t{t} Local0 = DepthBiasedAlpha(Parameters,{t}({t}(1.00000000)),{t}({t}(0.50000000)),{t}({t}(1.00000000)));\r\n\treturn Local0";
        Assert.AreEqual(call, result.PropertyCode[root]); Assert.IsTrue(result.UsesSceneDepth); Assert.AreEqual(blend, result.UsesSceneColor);
        Assert.AreEqual(0u, result.NumUserTexCoords); Assert.IsFalse(result.ForcesFloatTexCoords); Assert.IsFalse(result.UsesTime);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count + result.UniformExpressionTextures.Count);
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
    public void AlphaMasksVectorInputsToRInsteadOfRejecting(int width, bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, false);
        node.WriteProperty(Input("Alpha", Constant(package, material, width, .25f))); material.WriteProperty(Input("Opacity", node));
        var result = Translate(material, half); AssertSuccess(result); string t = half ? "half" : "float";
        string source = width == 1 ? $"{t}({t}(0.25000000))" : $"{t}({t}{width}(" + string.Join(",", Enumerable.Repeat("0.25000000", width)) + ").r)";
        StringAssert.Contains(result.PropertyCode["Opacity"], $"DepthBiasedAlpha(Parameters,{source},"); Assert.IsTrue(result.UsesSceneDepth);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
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
    public void BlendRejectsVectorAlphaBeforeSceneReads(int width, bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, true);
        node.WriteProperty(Input("Alpha", Constant(package, material, width))); node.WriteProperty(Input("Bias", Constant(package, material, 1)));
        material.WriteProperty(Input("EmissiveColor", node)); var result = Translate(material, half);
        Assert.AreEqual(width == 1, result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(width == 1, result.UsesSceneDepth); Assert.AreEqual(width == 1, result.UsesSceneColor);
        if (width != 1) { CollectionAssert.AreEqual(new[] { "Alpha input must be float1" }, result.Errors.ToArray()); Assert.IsNull(result.MaterialHlsl); }
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
    public void BlendBroadcastsGenericScalarMasksFloat4AndRejectsFloat2(int width, bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, true);
        node.WriteProperty(Input("RGB", Constant(package, material, width, .25f))); node.WriteProperty(Input("Bias", Constant(package, material, 1)));
        material.WriteProperty(Input("EmissiveColor", node)); var result = Translate(material, half);
        Assert.AreEqual(width != 2, result.Success, string.Join("; ", result.Errors)); Assert.IsTrue(result.UsesSceneColor); Assert.IsTrue(result.UsesSceneDepth);
        if (width == 2) { StringAssert.Contains(string.Join("; ", result.Errors), "Coercion failed:"); Assert.IsNull(result.MaterialHlsl); return; }
        string t = half ? "half" : "float";
        string source = width == 1 ? $"{t}3({t}(0.25000000),{t}(0.25000000),{t}(0.25000000))"
            : $"{t}{width}(" + string.Join(",", Enumerable.Repeat("0.25000000", width)) + ")" + (width == 4 ? ".rgb" : "");
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], $"DepthBiasedBlend(Parameters,{source},");
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], $"return {t}4(Local0,{t}(1.00000000)).rgb");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void DynamicUniformAccessOrderIsBiasThenSourceThenAppendedAlpha(bool blend, bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, blend);
        var alpha = Parameter(package, material, "Alpha", false); var bias = Parameter(package, material, "Bias", false);
        node.WriteProperty(Input("Alpha", alpha)); node.WriteProperty(Input("Bias", bias));
        if (blend) node.WriteProperty(Input("RGB", Parameter(package, material, "Color", true)));
        material.WriteProperty(Input(blend ? "EmissiveColor" : "Opacity", node)); var result = Translate(material, half); AssertSuccess(result);
        CollectionAssert.AreEqual(new[] { "Bias", "Alpha" }, result.UniformPixelScalarExpressions.Cast<MaterialUniformExpressionScalarParameter>().Select(x => x.ParameterName.Name).ToArray());
        string code = result.PropertyCode[blend ? "EmissiveColor" : "Opacity"]; string t = half ? "half" : "float";
        StringAssert.Contains(code, $"{t}(UniformPixelScalars_0.x)");
        if (blend)
        {
            Assert.AreEqual("Color", ((MaterialUniformExpressionVectorParameter)result.UniformPixelVectorExpressions.Single()).ParameterName.Name);
            StringAssert.Contains(code, "UniformPixelVector_0.rgb"); StringAssert.Contains(code, $"{t}4(Local0,UniformPixelScalars_0.y)");
        }
        else StringAssert.Contains(code, $"DepthBiasedAlpha(Parameters,{t}(UniformPixelScalars_0.y),{t}(UniformPixelScalars_0.x),");
        Assert.AreEqual(0, result.UniformVertexScalarExpressions.Count + result.UniformVertexVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void VertexStageRejectsBeforeSceneUsageButAfterExpressionInputAccess(bool blend, bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, blend);
        node.WriteProperty(Input("Alpha", Parameter(package, material, "Alpha", true)));
        if (blend) node.WriteProperty(Input("Bias", Constant(package, material, 1)));
        material.WriteProperty(Input("WorldPositionOffset", node)); var result = Translate(material, half);
        Assert.IsFalse(result.Success); Assert.IsFalse(result.UsesSceneDepth); Assert.IsFalse(result.UsesSceneColor);
        CollectionAssert.AreEqual(new[] { blend ? "Alpha input must be float1" : "Invalid node used in vertex shader input!" }, result.Errors.ToArray());
        Assert.AreEqual(blend ? 0 : 1, result.UniformVertexVectorExpressions.Count, "Alpha's R mask accesses a vector uniform before the translator stage gate.");
    }

    [TestMethod]
    [DataRow(false, 2)]
    [DataRow(false, 3)]
    [DataRow(false, 4)]
    [DataRow(true, 2)]
    [DataRow(true, 3)]
    [DataRow(true, 4)]
    public void InvalidBiasCoercionRetainsCallbackUsageButWithholdsHlsl(bool blend, int width)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, blend);
        node.WriteProperty(Input("Bias", Constant(package, material, width))); node.WriteProperty(Input("Alpha", Parameter(package, material, "Alpha", false)));
        material.WriteProperty(Input(blend ? "EmissiveColor" : "Opacity", node)); var result = Translate(material);
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.IsTrue(result.UsesSceneDepth); Assert.AreEqual(blend, result.UsesSceneColor);
        StringAssert.Contains(string.Join("; ", result.Errors), "Coercion failed:");
        Assert.AreEqual(blend ? 0 : 1, result.UniformPixelScalarExpressions.Count, "Alpha is accessed by its callback, while Blend's separate appended Alpha is withheld.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MissingBlendBiasFailsWithoutInventingAlphaBiasDefault(bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, true);
        material.WriteProperty(Input("EmissiveColor", node)); var result = Translate(material, half);
        Assert.IsFalse(result.Success); Assert.IsFalse(result.UsesSceneDepth); Assert.IsFalse(result.UsesSceneColor); Assert.IsNull(result.MaterialHlsl);
        StringAssert.Contains(string.Join("; ", result.Errors), ".Bias is not connected.");
        node.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(0, "Expression") }, "Bias"));
        result = Translate(material, half); Assert.IsFalse(result.Success); Assert.IsFalse(result.UsesSceneDepth);
    }

    [TestMethod]
    [DataRow(false, "BLEND_Opaque", false, false, false)]
    [DataRow(false, "BLEND_Masked", false, false, false)]
    [DataRow(false, "BLEND_SoftMasked", false, false, false)]
    [DataRow(false, "BLEND_Opaque", true, true, true)]
    [DataRow(false, "BLEND_Translucent", false, true, true)]
    [DataRow(true, "BLEND_Opaque", false, false, false)]
    [DataRow(true, "BLEND_Masked", true, false, true)]
    [DataRow(true, "BLEND_Translucent", false, false, true)]
    [DataRow(true, "BLEND_Translucent", false, true, false)]
    [DataRow(true, "BLEND_Opaque", true, true, false)]
    public void OrdinaryResourceEnforcesDepthAndColorRules(bool blend, string mode, bool effect, bool decal, bool success)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, blend);
        material.WriteProperty(new EnumProperty(mode, "EBlendMode", MEGame.LE3, "BlendMode"));
        material.WriteProperty(new EnumProperty("MLM_Unlit", "EMaterialLightingModel", MEGame.LE3, "LightingModel"));
        material.WriteProperty(new BoolProperty(effect, "bUsedWithMaterialEffect")); material.WriteProperty(new BoolProperty(decal, "bUsedWithDecals"));
        if (blend) node.WriteProperty(Input("Bias", Constant(package, material, 1)));
        material.WriteProperty(Input("EmissiveColor", node)); var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.AreEqual(success, result.Success, string.Join("; ", result.Errors)); Assert.IsTrue(result.UsesSceneDepth); Assert.AreEqual(blend, result.UsesSceneColor);
        if (!success) StringAssert.Contains(string.Join("; ", result.Errors), blend && decal ? "Materials used with decals must not read from scene color." : "Only transparent materials can read from scene depth.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SelectedBranchAndFreshTranslationsResetUsageAndCachedEmission(bool blend)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, blend);
        if (blend) node.WriteProperty(Input("Bias", Constant(package, material, 1)));
        var branch = package.CreateExport("Switch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        branch.WriteProperty(Input("A", node)); branch.WriteProperty(Input("B", Constant(package, material, 1)));
        branch.WriteProperty(new BoolProperty(true, "DefaultValue")); material.WriteProperty(Input("EmissiveColor", branch));
        var compiler = new LE3MaterialShaderCompiler(material); var selected = compiler.TranslateMaterialHlsl(Template()); AssertSuccess(selected);
        Assert.IsTrue(selected.UsesSceneDepth); Assert.AreEqual(blend, selected.UsesSceneColor);
        branch.WriteProperty(new BoolProperty(false, "DefaultValue")); var inactive = compiler.TranslateMaterialHlsl(Template()); AssertSuccess(inactive);
        Assert.IsFalse(inactive.UsesSceneDepth); Assert.IsFalse(inactive.UsesSceneColor); Assert.IsFalse(inactive.MaterialHlsl.Contains("DepthBiased"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitFloat1AlphaAndBiasDoNotGainGenericScalarCasts(bool blend)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, blend);
        var pixel = package.CreateExport("Pixel", "MaterialExpressionPixelDepth", material, indexed: false);
        node.WriteProperty(Input("Alpha", pixel)); node.WriteProperty(Input("Bias", pixel));
        material.WriteProperty(Input(blend ? "EmissiveColor" : "Opacity", node)); var result = Translate(material); AssertSuccess(result);
        string code = result.PropertyCode[blend ? "EmissiveColor" : "Opacity"];
        StringAssert.Contains(code, blend ? ",Parameters.ScreenPosition.w,float(float(1.00000000)))" : "DepthBiasedAlpha(Parameters,Parameters.ScreenPosition.w,Parameters.ScreenPosition.w,");
        Assert.IsFalse(code.Contains("float(Parameters.ScreenPosition.w)"));
    }

    [TestMethod]
    public void ExplicitFloat1RgbIsNotBroadcastAndInputMasksRemainSupported()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, true);
        var pixel = package.CreateExport("Pixel", "MaterialExpressionPixelDepth", material, indexed: false);
        node.WriteProperty(Input("RGB", pixel)); node.WriteProperty(Input("Bias", Constant(package, material, 1))); material.WriteProperty(Input("EmissiveColor", node));
        var result = Translate(material); Assert.IsFalse(result.Success); Assert.IsTrue(result.UsesSceneColor); Assert.IsTrue(result.UsesSceneDepth);
        node.WriteProperty(Input("RGB", Constant(package, material, 4)));
        var alpha = Input("Alpha", Constant(package, material, 4)); alpha.Properties.Add(new IntProperty(-1, "Mask")); alpha.Properties.Add(new IntProperty(-1, "MaskA")); node.WriteProperty(alpha);
        result = Translate(material); AssertSuccess(result); StringAssert.Contains(result.PropertyCode["EmissiveColor"], ").a).rgb");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CyclesAndNonlocalReferencesFailBeforeCallbackFlags(bool blend)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, blend);
        node.WriteProperty(Input("Bias", node)); material.WriteProperty(Input("Opacity", node));
        var result = Translate(material); Assert.IsFalse(result.Success); Assert.IsFalse(result.UsesSceneDepth); StringAssert.Contains(string.Join("; ", result.Errors), "Expression cycle");
        node.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(-999, "Expression") }, "Bias"));
        result = Translate(material); Assert.IsFalse(result.Success); Assert.IsFalse(result.UsesSceneDepth); Assert.IsFalse(result.UsesSceneColor);
        StringAssert.Contains(string.Join("; ", result.Errors), "not a local MaterialExpression export");
    }

    [TestMethod]
    [DataRow(false, 0f)]
    [DataRow(false, -2f)]
    [DataRow(false, 4f)]
    [DataRow(true, 0f)]
    [DataRow(true, -2f)]
    [DataRow(true, 4f)]
    public void AuthoredBiasScaleIsPassedVerbatimWithoutCompilerEpsilonClamp(bool blend, float scale)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, blend);
        node.WriteProperty(new FloatProperty(scale, "BiasScale")); if (blend) node.WriteProperty(Input("Bias", Constant(package, material, 1)));
        material.WriteProperty(Input(blend ? "EmissiveColor" : "Opacity", node)); var result = Translate(material); AssertSuccess(result);
        string literal = scale.ToString("F8", System.Globalization.CultureInfo.InvariantCulture);
        StringAssert.Contains(result.PropertyCode[blend ? "EmissiveColor" : "Opacity"], $",float(float({literal})))");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReusedNodeIsEmittedOncePerPropertyWhileUniformsRemainShared(bool blend)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, blend);
        node.WriteProperty(Input("Bias", Parameter(package, material, "Bias", false)));
        var add = package.CreateExport("Add", "MaterialExpressionAdd", material, indexed: false); add.WriteProperty(Input("A", node)); add.WriteProperty(Input("B", node));
        material.WriteProperty(Input("EmissiveColor", add)); material.WriteProperty(Input("DiffuseColor", add)); var result = Translate(material); AssertSuccess(result);
        foreach (string root in new[] { "EmissiveColor", "DiffuseColor" })
        {
            Assert.AreEqual(1, result.PropertyCode[root].Split("DepthBiased", StringSplitOptions.None).Length - 1);
            Assert.AreEqual(1, result.PropertyCode[root].Split(" = DepthBiased", StringSplitOptions.None).Length - 1);
        }
        Assert.AreNotEqual(result.PropertyCode["EmissiveColor"].Split(" = DepthBiased")[0], result.PropertyCode["DiffuseColor"].Split(" = DepthBiased")[0], "Locals have translation-wide ordinals, while expression caches reset per property.");
        Assert.AreEqual(1, result.UniformPixelScalarExpressions.Count); Assert.IsTrue(result.UsesSceneDepth); Assert.AreEqual(blend, result.UsesSceneColor);
    }

    [TestMethod]
    public void FailedVectorBiasDoesNotPackItButAlphaCallbackStillAccessesItsSource()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material, false);
        node.WriteProperty(Input("Bias", Parameter(package, material, "Bias", true))); node.WriteProperty(Input("Alpha", Parameter(package, material, "Alpha", false)));
        material.WriteProperty(Input("Opacity", node)); var result = Translate(material); Assert.IsFalse(result.Success); Assert.IsTrue(result.UsesSceneDepth);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count); Assert.AreEqual("Alpha", ((MaterialUniformExpressionScalarParameter)result.UniformPixelScalarExpressions.Single()).ParameterName.Name);
    }

    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("LE3DepthBiased.pcc", MEGame.LE3);
    private static ExportEntry Material(IMEPackage package) => package.CreateExport("Material", "Material", indexed: false);
    private static ExportEntry Node(IMEPackage package, ExportEntry material, bool blend) => package.CreateExport("Biased", "MaterialExpressionDepthBiased" + (blend ? "Blend" : "Alpha"), material, indexed: false);
    private static ExportEntry Constant(IMEPackage package, ExportEntry material, int width, float value = .25f)
    {
        var node = package.CreateExport("Value", width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector", material);
        foreach (string lane in new[] { "R", "G", "B", "A" }.Take(width)) node.WriteProperty(new FloatProperty(value, lane)); return node;
    }
    private static ExportEntry Parameter(IMEPackage package, ExportEntry material, string name, bool vector)
    {
        var node = package.CreateExport(name, "MaterialExpression" + (vector ? "VectorParameter" : "ScalarParameter"), material, indexed: false);
        node.WriteProperty(new NameProperty(name, "ParameterName")); return node;
    }
    private static StructProperty Input(string name, ExportEntry node) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(node.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static LE3MaterialShaderCompiler.TranslationResult Translate(ExportEntry material, bool half = false) => new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
    private static void AssertSuccess(LE3MaterialShaderCompiler.TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
}
