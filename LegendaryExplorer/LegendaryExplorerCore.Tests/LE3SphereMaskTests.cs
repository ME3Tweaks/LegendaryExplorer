using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3SphereMaskTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConstantCompositionUsesNativeDefaultsAndNoUniformSlots(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SphereConstants.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var a = package.CreateExport("A", "MaterialExpressionConstant", material, indexed: false); a.WriteProperty(new FloatProperty(4f, "R"));
        var b = package.CreateExport("B", "MaterialExpressionConstant", material, indexed: false); b.WriteProperty(new FloatProperty(1f, "R"));
        var mask = Sphere(package, material, a, b);
        material.WriteProperty(Input("Opacity", mask)); material.WriteProperty(Input("WorldPositionOffset", mask));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(result.PropertyCode["Opacity"], result.PropertyCode["WorldPositionOffset"]);
        string type = half ? "half" : "float";
        StringAssert.Contains(result.PropertyCode["Opacity"], "sqrt(dot(");
        StringAssert.Contains(result.PropertyCode["Opacity"], type + "(0.00390625)");
        StringAssert.Contains(result.PropertyCode["Opacity"], "min(max(");
        Assert.IsFalse(result.PropertyCode["Opacity"].Contains("Local", StringComparison.Ordinal));
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformVertexScalarExpressions.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UniformCompositionRetainsOrderedOriginalChildrenAndSharesStages(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SphereUniform.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var a = Parameter(package, material, "A", 4f); var b = Parameter(package, material, "B", 1f);
        var sphere = Sphere(package, material, a, b); sphere.WriteProperty(new FloatProperty(4f, "AttenuationRadius")); sphere.WriteProperty(new FloatProperty(50f, "HardnessPercent"));
        material.WriteProperty(Input("Opacity", sphere)); material.WriteProperty(Input("WorldPositionOffset", sphere));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var final = (MaterialUniformExpressionClamp)result.UniformPixelScalarExpressions.Last();
        Assert.AreSame(final, result.UniformVertexScalarExpressions.Last());
        var shape = (MaterialUniformExpressionFoldedMath)final.Input;
        Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Mul, shape.Op);
        Assert.AreEqual(2f, ((MaterialUniformExpressionConstant)shape.B).Value.R);
        var inverted = (MaterialUniformExpressionFoldedMath)shape.A;
        Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Sub, inverted.Op);
        var normalized = (MaterialUniformExpressionFoldedMath)inverted.B;
        Assert.AreEqual(.25f, ((MaterialUniformExpressionConstant)normalized.B).Value.R);
        var sqrt = (MaterialUniformExpressionSquareRoot)normalized.A;
        var dot = (MaterialUniformExpressionFoldedMath)sqrt.X;
        Assert.AreSame(dot.A, dot.B); Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Dot, dot.Op);
        var delta = (MaterialUniformExpressionFoldedMath)dot.A;
        Assert.AreEqual("A", ((MaterialUniformExpressionScalarParameter)delta.A).ParameterName.Name);
        Assert.AreEqual("B", ((MaterialUniformExpressionScalarParameter)delta.B).ParameterName.Name);
        var value = LinearColor.Black; final.GetNumberValue(Context(), ref value); Assert.AreEqual(.5f, value.R);
        var overrides = Context(new() { ["A"] = 5f }); final.GetNumberValue(overrides, ref value); Assert.AreEqual(0f, value.R);
        final.GetNumberValue(Context(new() { ["A"] = 1f }), ref value); Assert.AreEqual(1f, value.R);
    }

    [TestMethod]
    [DataRow(4f, 50f, .5f)]
    [DataRow(4f, 100f, .25f)]
    [DataRow(4f, 0f, 1f)]
    [DataRow(4f, -50f, 1f)]
    [DataRow(0f, 100f, 0f)]
    [DataRow(-4f, 100f, 0f)]
    [DataRow(float.NaN, 100f, 0f)]
    [DataRow(4f, float.NaN, 1f)]
    [DataRow(float.PositiveInfinity, 100f, 1f)]
    [DataRow(4f, float.PositiveInfinity, 0f)]
    public void RadiusAndHardnessUseFloat32MaxSecondOperandSemantics(float radius, float hardness, float expected)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SphereBounds.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var a = Parameter(package, material, "A", 4f); var b = Parameter(package, material, "B", 1f);
        var sphere = Sphere(package, material, a, b); sphere.WriteProperty(new FloatProperty(radius, "AttenuationRadius")); sphere.WriteProperty(new FloatProperty(hardness, "HardnessPercent"));
        material.WriteProperty(Input("Opacity", sphere));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var output = LinearColor.Black; result.UniformPixelScalarExpressions.Last().GetNumberValue(Context(), ref output);
        Assert.AreEqual(expected, output.R);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NonuniformInputsEmitSevenNativeComposedLocals(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SpherePixel.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var a = package.CreateExport("A", "MaterialExpressionWorldPosition", material, indexed: false);
        var b = package.CreateExport("B", "MaterialExpressionObjectWorldPosition", material, indexed: false);
        var sphere = Sphere(package, material, a, b); material.WriteProperty(Input("Opacity", sphere));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string type = half ? "half" : "float";
        StringAssert.Contains(result.PropertyCode["Opacity"], $"{type}3 Local0 = (Parameters.WorldPosition - ObjectWorldPositionAndRadius.xyz);");
        StringAssert.Contains(result.PropertyCode["Opacity"], $"{type} Local1 = dot(Local0,Local0);");
        StringAssert.Contains(result.PropertyCode["Opacity"], $"{type} Local2 = sqrt(Local1);");
        StringAssert.Contains(result.PropertyCode["Opacity"], "Local6 = min(max(Local5,");
        Assert.AreEqual(8u, result.UsingTransforms); Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
    }

    [TestMethod]
    [DataRow("A")]
    [DataRow("B")]
    [DataRow("cycle")]
    [DataRow("width")]
    public void MissingCyclicAndIncompatibleInputsFailClosed(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3SphereInvalid.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var a = package.CreateExport("A", shape == "width" ? "MaterialExpressionConstant2Vector" : "MaterialExpressionConstant", material, indexed: false);
        var b = package.CreateExport("B", shape == "width" ? "MaterialExpressionConstant3Vector" : "MaterialExpressionConstant", material, indexed: false);
        var sphere = Sphere(package, material, a, b);
        if (shape is "A" or "B") sphere.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(0, "Expression") }, shape));
        if (shape == "cycle") sphere.WriteProperty(Input("A", sphere));
        material.WriteProperty(Input("Opacity", sphere));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        if (shape is "A" or "B") CollectionAssert.AreEqual(new[] { "Missing input " + shape }, result.Errors.ToArray());
    }

    private static ExportEntry Sphere(IMEPackage package, ExportEntry material, ExportEntry a, ExportEntry b)
    {
        var sphere = package.CreateExport("Sphere", "MaterialExpressionSphereMask", material, indexed: false);
        sphere.WriteProperty(Input("A", a)); sphere.WriteProperty(Input("B", b)); return sphere;
    }
    private static ExportEntry Parameter(IMEPackage package, ExportEntry material, string name, float value)
    {
        var parameter = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
        parameter.WriteProperty(new NameProperty(name, "ParameterName")); parameter.WriteProperty(new FloatProperty(value, "DefaultValue")); return parameter;
    }
    private static UniformExpressionRenderContext Context(Dictionary<string, float> values = null) => new(values ?? new(), new(), 0, 0, null);
    private static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
