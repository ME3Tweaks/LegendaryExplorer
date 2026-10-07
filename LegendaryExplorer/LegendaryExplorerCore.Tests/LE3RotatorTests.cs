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
public class LE3RotatorTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DefaultRotatorKeepsClockRowsExtraUvWorkAndRejectsImplicitVertexUv(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3RotatorDefaults.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var rotator = package.CreateExport("Rotator", "MaterialExpressionRotator", material, indexed: false);
        material.WriteProperty(Input("Distortion", rotator));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsTrue(result.UsesTime); Assert.IsTrue(result.ForcesFloatTexCoords); Assert.AreEqual(1u, result.NumUserTexCoords);
        string type = half ? "half" : "float";
        StringAssert.Contains(result.PropertyCode["Distortion"], $"{type}2 Local0 = Parameters.TexCoords[0].xy;");
        Assert.AreEqual(4, result.PropertyCode["Distortion"].Split(" = dot(").Length - 1, "Native emits two masked and two full-coordinate projections.");
        StringAssert.Contains(result.PropertyCode["Distortion"], type + "2(0.50000000,0.50000000)");
        Assert.AreEqual(5, result.UniformPixelScalarExpressions.Count);
        var cos = result.UniformPixelScalarExpressions.OfType<MaterialUniformExpressionSine>().Single(e => e.bIsCosine);
        var sin = result.UniformPixelScalarExpressions.OfType<MaterialUniformExpressionSine>().Single(e => !e.bIsCosine);
        Assert.AreSame(cos.X, sin.X); var angle = (MaterialUniformExpressionFoldedMath)cos.X;
        Assert.AreEqual(.25f, ((MaterialUniformExpressionConstant)angle.B).Value.R);
        var negated = result.UniformPixelScalarExpressions.OfType<MaterialUniformExpressionFoldedMath>()
            .Single(e => e.A is MaterialUniformExpressionConstant c && c.Value.R == -1f);
        Assert.AreEqual(-1f, ((MaterialUniformExpressionConstant)negated.A).Value.R); Assert.AreSame(sin, negated.B);
        material.WriteProperty(Input("WorldPositionOffset", rotator));
        var rejected = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsFalse(rejected.Success); StringAssert.Contains(string.Join("; ", rejected.Errors), "Invalid node used in vertex shader input!");
    }

    [TestMethod]
    [DataRow(1, false)]
    [DataRow(1, true)]
    [DataRow(2, false)]
    [DataRow(2, true)]
    [DataRow(3, false)]
    [DataRow(3, true)]
    public void ExplicitConstantCoordinatesRetainTheNativeWidthBranchAndUnscaledAngle(int width, bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3RotatorWidths.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var coordinate = package.CreateExport("Coordinate", width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector", material, indexed: false);
        coordinate.WriteProperty(new FloatProperty(.25f, "R")); if (width > 1) coordinate.WriteProperty(new FloatProperty(.75f, "G")); if (width > 2) coordinate.WriteProperty(new FloatProperty(2f, "B"));
        var time = package.CreateExport("TimeValue", "MaterialExpressionConstant", material, indexed: false); time.WriteProperty(new FloatProperty(2f, "R"));
        var rotator = Rotator(package, material, coordinate, time); rotator.WriteProperty(new FloatProperty(1f, "Speed"));
        material.WriteProperty(Input(width == 3 ? "DiffuseColor" : "Distortion", rotator));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsFalse(result.UsesTime); Assert.IsFalse(result.ForcesFloatTexCoords); Assert.AreEqual(0u, result.NumUserTexCoords);
        string code = result.PropertyCode[width == 3 ? "DiffuseColor" : "Distortion"];
        StringAssert.Contains(code, "cos(("); StringAssert.Contains(code, "sin((");
        Assert.IsFalse(code.Contains("6.283185", StringComparison.Ordinal), "Rotator calls the trig primitive directly, without expression Period scaling.");
        if (width == 3) { StringAssert.Contains(code, ".b)"); StringAssert.Contains(code, "return " + (half ? "half3" : "float3") + "(Local4,"); }
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void UniformClockCompositionSharesOriginalTreesAndEvaluatesCoordinatePair(bool realTime, bool vertex)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3RotatorUniform.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var coordinate = package.CreateExport("Coordinate", "MaterialExpressionConstant2Vector", material, indexed: false);
        coordinate.WriteProperty(new FloatProperty(.25f, "R")); coordinate.WriteProperty(new FloatProperty(.75f, "G"));
        var time = package.CreateExport("Clock", "MaterialExpressionTime", material, indexed: false); time.WriteProperty(new BoolProperty(realTime, "bIgnorePause"));
        var rotator = Rotator(package, material, coordinate, time); rotator.WriteProperty(new FloatProperty(1f, "Speed"));
        material.WriteProperty(Input(vertex ? "WorldPositionOffset" : "Distortion", rotator));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsTrue(result.UsesTime); Assert.IsTrue(result.ForcesFloatTexCoords); Assert.AreEqual(0u, result.NumUserTexCoords);
        var vectors = vertex ? result.UniformVertexVectorExpressions : result.UniformPixelVectorExpressions;
        var final = (MaterialUniformExpressionFoldedMath)vectors.Last();
        var initial = LinearColor.Black; final.GetNumberValue(Context(0, 0), ref initial);
        Assert.AreEqual(.25f, initial.R, .000001f); Assert.AreEqual(.75f, initial.G, .000001f);
        var rotated = LinearColor.Black; final.GetNumberValue(Context(realTime ? 0 : 1.57079637f, realTime ? 1.57079637f : 0), ref rotated);
        Assert.AreEqual(.25f, rotated.R, .000001f); Assert.AreEqual(.25f, rotated.G, .000001f);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitNonClockTimeDoesNotAcquireClockFlagsAndCoordinateIsVisitedOnce(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3RotatorTimeInput.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var coordinate = package.CreateExport("Coordinate", "MaterialExpressionTextureCoordinate", material, indexed: false);
        var time = package.CreateExport("TimeValue", "MaterialExpressionScalarParameter", material, indexed: false); time.WriteProperty(new NameProperty("Angle", "ParameterName"));
        var rotator = Rotator(package, material, coordinate, time); material.WriteProperty(Input("Distortion", rotator));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsFalse(result.UsesTime); Assert.IsFalse(result.ForcesFloatTexCoords);
        Assert.AreEqual(1, result.PropertyCode["Distortion"].Split("Parameters.TexCoords[0].xy").Length - 1);
        Assert.AreEqual(1u, result.NumUserTexCoords);
        Assert.AreEqual(1, result.UniformPixelScalarExpressions.OfType<MaterialUniformExpressionScalarParameter>().Count());
    }

    [TestMethod]
    [DataRow("width4")]
    [DataRow("coordinateCycle")]
    [DataRow("timeCycle")]
    public void InvalidWidthAndCyclicInputsFailClosed(string shape)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3RotatorInvalid.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var coordinate = package.CreateExport("Coordinate", "MaterialExpressionConstant4Vector", material, indexed: false);
        var rotator = Rotator(package, material, coordinate, null);
        if (shape == "coordinateCycle") rotator.WriteProperty(Input("Coordinate", rotator));
        if (shape == "timeCycle") rotator.WriteProperty(Input("Time", rotator));
        material.WriteProperty(Input("Distortion", rotator));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
    }

    private static ExportEntry Rotator(IMEPackage package, ExportEntry material, ExportEntry coordinate, ExportEntry time)
    {
        var rotator = package.CreateExport("Rotator", "MaterialExpressionRotator", material, indexed: false);
        rotator.WriteProperty(Input("Coordinate", coordinate)); if (time != null) rotator.WriteProperty(Input("Time", time)); return rotator;
    }
    private static UniformExpressionRenderContext Context(float time, float realTime) => new(new Dictionary<string, float>(), new Dictionary<string, LinearColor>(), time, realTime, null);
    private static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
