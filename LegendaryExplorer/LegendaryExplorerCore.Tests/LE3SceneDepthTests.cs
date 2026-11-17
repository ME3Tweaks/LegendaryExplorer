using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3SceneDepthTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void DefaultCoordinatesAreScreenAlignedAndNormalizeDoesNotChangeCode(bool half, bool normalize)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SceneDepthDefault.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var depth = package.CreateExport("Depth", "MaterialExpressionSceneDepth", material, indexed: false);
        depth.WriteProperty(new BoolProperty(normalize, "bNormalize")); material.WriteProperty(Input("Opacity", depth));
        var compiler = new LE3MaterialShaderCompiler(material);
        var result = compiler.TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.IsTrue(result.UsesSceneDepth);
        Assert.IsFalse(result.UsesTime); Assert.IsFalse(result.ForcesFloatTexCoords); Assert.AreEqual(0u, result.NumUserTexCoords);
        Assert.AreEqual($"\t{(half ? "half" : "float")} Local0 = CalcSceneDepth(float2(ScreenAlignedPosition(Parameters.ScreenPosition).xy));\r\n\treturn Local0", result.PropertyCode["Opacity"]);
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count + result.UniformCubeTextureExpressions.Count);
        material.WriteProperty(Input("WorldPositionOffset", depth));
        var vertex = compiler.TranslateMaterialHlsl(Template(), half); Assert.IsFalse(vertex.Success);
        Assert.IsTrue(vertex.UsesSceneDepth, "The successful earlier pixel root remains recorded when a later vertex root fails.");
        StringAssert.Contains(string.Join("; ", vertex.Errors), "Invalid node used in vertex shader input!");
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
    public void ExplicitCoordinatesUseFullPrecisionCoercionWithoutTruncatingWidths(int width, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SceneDepthWidths.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var coordinates = package.CreateExport("Uv", width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector", material, indexed: false);
        coordinates.WriteProperty(new FloatProperty(.25f, "R")); if (width > 1) coordinates.WriteProperty(new FloatProperty(.75f, "G"));
        var depth = package.CreateExport("Depth", "MaterialExpressionSceneDepth", material, indexed: false);
        depth.WriteProperty(Input("Coordinates", coordinates)); material.WriteProperty(Input("Opacity", depth));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.AreEqual(width <= 2, result.Success, string.Join("; ", result.Errors)); Assert.IsTrue(result.UsesSceneDepth);
        if (width > 2) { Assert.IsNull(result.MaterialHlsl); StringAssert.Contains(string.Join("; ", result.Errors), "Coercion failed:"); return; }
        string uv = width == 1 ? "float2(float(0.25000000),float(0.25000000))" : "float2(0.25000000,0.75000000)";
        StringAssert.Contains(result.PropertyCode["Opacity"], $"CalcSceneDepth({uv})");
        Assert.AreEqual(0u, result.NumUserTexCoords); Assert.IsFalse(result.ForcesFloatTexCoords);
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
    public void OrdinaryResourceAppliesNativeTransparentOrMaterialEffectGate(string blend, bool effect, bool success)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SceneDepthGate.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new EnumProperty(blend, "EBlendMode", MEGame.LE3, "BlendMode"));
        material.WriteProperty(new EnumProperty("MLM_Unlit", "EMaterialLightingModel", MEGame.LE3, "LightingModel"));
        material.WriteProperty(new BoolProperty(effect, "bUsedWithMaterialEffect")); material.WriteProperty(new BoolProperty(true, "bUsedWithDecals"));
        var depth = package.CreateExport("Depth", "MaterialExpressionSceneDepth", material, indexed: false); material.WriteProperty(Input("EmissiveColor", depth));
        var compiler = new LE3MaterialShaderCompiler(material); var result = compiler.TranslateMaterialResourceHlsl(Template());
        Assert.AreEqual(success, result.Success, string.Join("; ", result.Errors)); Assert.IsTrue(result.UsesSceneDepth);
        if (!success) CollectionAssert.AreEqual(new[] { "Only transparent materials can read from scene depth." }, result.Errors.ToArray());
        material.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(0, "Expression") }, "EmissiveColor"));
        var fresh = compiler.TranslateMaterialResourceHlsl(Template()); Assert.IsTrue(fresh.Success); Assert.IsFalse(fresh.UsesSceneDepth);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UniformUvPackingAndVertexOnlyFailureDoNotInventTextures(bool vertex)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SceneDepthUniform.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var parameter = package.CreateExport("Uv", "MaterialExpressionScalarParameter", material, indexed: false);
        parameter.WriteProperty(new NameProperty("Uv", "ParameterName")); parameter.WriteProperty(new FloatProperty(.5f, "DefaultValue"));
        var depth = package.CreateExport("Depth", "MaterialExpressionSceneDepth", material, indexed: false); depth.WriteProperty(Input("Coordinates", parameter));
        material.WriteProperty(Input(vertex ? "WorldPositionOffset" : "Opacity", depth));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.AreEqual(!vertex, result.Success); Assert.AreEqual(!vertex, result.UsesSceneDepth);
        Assert.AreEqual(vertex ? 0 : 1, result.UniformPixelScalarExpressions.Count);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        if (!vertex) StringAssert.Contains(result.PropertyCode["Opacity"], "CalcSceneDepth(float2(UniformPixelScalars_0.x,UniformPixelScalars_0.x))");
    }
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitRgInputMaskAllowsWideCoordinatesWithoutImplicitTruncation(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SceneDepthMask.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var coordinates = package.CreateExport("Uv", "MaterialExpressionWorldPosition", material, indexed: false);
        var depth = package.CreateExport("Depth", "MaterialExpressionSceneDepth", material, indexed: false);
        depth.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(coordinates.UIndex, "Expression"), new IntProperty(1, "Mask"), new IntProperty(1, "MaskR"), new IntProperty(1, "MaskG"), new IntProperty(0, "MaskB"), new IntProperty(0, "MaskA") }, "Coordinates"));
        material.WriteProperty(Input("Opacity", depth));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.IsTrue(result.UsesSceneDepth);
        StringAssert.Contains(result.PropertyCode["Opacity"], "CalcSceneDepth(Parameters.WorldPosition.rg)");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InactiveStaticBranchDoesNotSetSceneDepthUsageOrRejectOpaqueResource(bool active)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SceneDepthInactive.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var depth = package.CreateExport("Depth", "MaterialExpressionSceneDepth", material, indexed: false);
        var zero = package.CreateExport("Zero", "MaterialExpressionConstant", material, indexed: false);
        var branch = package.CreateExport("Branch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        branch.WriteProperty(new NameProperty("ReadDepth", "ParameterName")); branch.WriteProperty(new BoolProperty(active, "DefaultValue"));
        branch.WriteProperty(Input("A", depth)); branch.WriteProperty(Input("B", zero)); material.WriteProperty(Input("Opacity", branch));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.AreEqual(!active, result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(active, result.UsesSceneDepth);
    }
    [TestMethod]
    public void UnknownResourceBlendReturnsDiagnosticInsteadOfThrowing()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SceneDepthUnknownBlend.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new EnumProperty("BLEND_Unknown", "EBlendMode", MEGame.LE3, "BlendMode"));
        var depth = package.CreateExport("Depth", "MaterialExpressionSceneDepth", material, indexed: false); material.WriteProperty(Input("Opacity", depth));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.IsTrue(result.UsesSceneDepth);
        StringAssert.Contains(string.Join("; ", result.Errors), "Unknown LE3 blend mode BLEND_Unknown.");
    }
    private static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
