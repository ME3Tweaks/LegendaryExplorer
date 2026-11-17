using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3LightmassReplaceTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BothStagesReturnRealtimeWithoutWidthPromotion(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LightmassReplace.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var realtime = Constant(package, material, "Realtime", .25f);
        var bake = package.CreateExport("Bake", "MaterialExpressionConstant4Vector", material, indexed: false);
        var node = Replace(package, material, realtime, bake);
        material.WriteProperty(Input("Opacity", node)); material.WriteProperty(Input("WorldPositionOffset", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual($"\treturn {(half ? "half" : "float")}(0.25000000)", result.PropertyCode["Opacity"]);
        Assert.AreEqual(result.PropertyCode["Opacity"], result.PropertyCode["WorldPositionOffset"]);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow("Realtime")]
    [DataRow("Lightmass")]
    public void RequiredInputsCheckBeforeEitherBranchVisit(string missing)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LightmassMissing.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        var node = Replace(package, material, time, time);
        node.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(0, "Expression") }, missing));
        material.WriteProperty(Input("Opacity", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.IsFalse(result.UsesTime);
        CollectionAssert.AreEqual(new[] { "Missing LightmassReplace input " + missing }, result.Errors.ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DiscardedTimeBranchStillSetsFlagsButDoesNotForcePacking(bool vertex)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LightmassTime.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var realtime = Constant(package, material, "Realtime", .25f);
        var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        var node = Replace(package, material, realtime, time); material.WriteProperty(Input(vertex ? "WorldPositionOffset" : "Opacity", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.IsTrue(result.UsesTime);
        Assert.IsTrue(result.ForcesFloatTexCoords);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformVertexScalarExpressions.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DiscardedPixelDestinationRetainsUsageAndEmittedLocal(bool color)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LightmassScene.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        material.WriteProperty(new EnumProperty("BLEND_Translucent", "EBlendMode", MEGame.LE3, "BlendMode"));
        var realtime = Constant(package, material, "Realtime", .25f);
        var destination = package.CreateExport("Bake", "MaterialExpression" + (color ? "DestColor" : "DestDepth"), material, indexed: false);
        var node = Replace(package, material, realtime, destination); material.WriteProperty(Input("Opacity", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(color, result.UsesSceneColor); Assert.AreEqual(!color, result.UsesSceneDepth);
        StringAssert.Contains(result.PropertyCode["Opacity"], "Local0 = Previous" + (color ? "Lighting" : "Depth") + "(Parameters.ScreenPosition);");
        StringAssert.Contains(result.PropertyCode["Opacity"], "return float(0.25000000)");
    }

    [TestMethod]
    public void DiscardedDynamicArithmeticPreservesNativeLazyAccessOrder()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LightmassUniform.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var realtime = Parameter(package, material, "Realtime", .25f);
        var a = Parameter(package, material, "A", 1); var b = Parameter(package, material, "B", 2);
        var add = package.CreateExport("Bake", "MaterialExpressionAdd", material, indexed: false); add.WriteProperty(Input("A", a)); add.WriteProperty(Input("B", b));
        var node = Replace(package, material, realtime, add); material.WriteProperty(Input("Opacity", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        // Add accesses B then A, but discarded final sum and input visitation alone do not pack.
        CollectionAssert.AreEqual(new[] { "B", "A", "Realtime" }, result.UniformPixelScalarExpressions.Cast<MaterialUniformExpressionScalarParameter>().Select(p => p.ParameterName.Name).ToArray());
        Assert.AreEqual("\treturn UniformPixelScalars_0.z", result.PropertyCode["Opacity"]);
    }

    [TestMethod]
    [DataRow("Realtime")]
    [DataRow("Lightmass")]
    public void CyclicBranchesFailEvenWhenLightmassResultIsDiscarded(string branch)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LightmassCycle.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var value = Constant(package, material, "Value", 1); var node = Replace(package, material, value, value);
        node.WriteProperty(Input(branch, node)); material.WriteProperty(Input("Opacity", node));
        var compiler = new LE3MaterialShaderCompiler(material); var failed = compiler.TranslateMaterialHlsl(Template());
        Assert.IsFalse(failed.Success); Assert.IsNull(failed.MaterialHlsl);
        node.WriteProperty(Input(branch, value)); var valid = compiler.TranslateMaterialHlsl(Template()); Assert.IsTrue(valid.Success, string.Join("; ", valid.Errors));
    }

    [TestMethod]
    [DataRow("Realtime")]
    [DataRow("Lightmass")]
    public void EitherBranchCanRejectVertexCompilation(string branch)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LightmassVertex.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var value = Constant(package, material, "Value", 1); var node = Replace(package, material, value, value);
        var sign = package.CreateExport("Sign", "MaterialExpressionTwoSidedSign", material, indexed: false); node.WriteProperty(Input(branch, sign));
        material.WriteProperty(Input("WorldPositionOffset", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Invalid node used in vertex shader input!", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void FailedRealtimeStillVisitsLightmassForDiagnosticState()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LightmassFailureOrder.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var invalid = package.CreateExport("Invalid", "MaterialExpressionComment", material, indexed: false);
        var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        var node = Replace(package, material, invalid, time); material.WriteProperty(Input("Opacity", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.UsesTime);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void StaticSwitchOutsideReplaceControlsBothBranchVisits(bool selected)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LightmassStatic.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var value = Constant(package, material, "Value", 1); var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        var node = Replace(package, material, value, time);
        var branch = package.CreateExport("Branch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        branch.WriteProperty(new NameProperty("UseReplace", "ParameterName")); branch.WriteProperty(new BoolProperty(selected, "DefaultValue"));
        branch.WriteProperty(Input("A", node)); branch.WriteProperty(Input("B", value)); material.WriteProperty(Input("Opacity", branch));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.IsTrue(result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(selected, result.UsesTime);
    }

    private static ExportEntry Replace(IMEPackage package, ExportEntry material, ExportEntry realtime, ExportEntry lightmass)
    { var node = package.CreateExport("Replace", "MaterialExpressionLightmassReplace", material, indexed: false); node.WriteProperty(Input("Realtime", realtime)); node.WriteProperty(Input("Lightmass", lightmass)); return node; }
    private static ExportEntry Constant(IMEPackage package, ExportEntry material, string name, float value)
    { var node = package.CreateExport(name, "MaterialExpressionConstant", material, indexed: false); node.WriteProperty(new FloatProperty(value, "R")); return node; }
    private static ExportEntry Parameter(IMEPackage package, ExportEntry material, string name, float value)
    { var node = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false); node.WriteProperty(new FloatProperty(value, "DefaultValue")); node.WriteProperty(new NameProperty(name, "ParameterName")); return node; }
    private static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
