using System;
using System.Globalization;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3NumericCompositionTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BiasScaleDefaultsComposeBiasFirstAndScaleSecond(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("BiasScale.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var x = Constant(package, material, "X", 3);
        var node = Node(package, material, "ConstantBiasScale", x);
        material.WriteProperty(Input("Opacity", node)); material.WriteProperty(Input("WorldPositionOffset", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string t = half ? "half" : "float";
        Assert.AreEqual($"\treturn (({t}(1.00000000) + {t}(3.00000000)) * {t}(0.50000000))", result.PropertyCode["Opacity"]);
        Assert.AreEqual(result.PropertyCode["Opacity"], result.PropertyCode["WorldPositionOffset"]);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformVertexScalarExpressions.Count);
    }

    [TestMethod]
    [DataRow(3f, 1f, .5f, 2f)]
    [DataRow(-3f, -1f, -2f, 8f)]
    [DataRow(7f, 0f, 0f, 0f)]
    [DataRow(.25f, .125f, 2f, .75f)]
    public void BiasScaleDynamicTreeAndCpuValuesPreserveOriginalChildren(float input, float bias, float scale, float expected)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("BiasScaleUniform.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var x = ScalarParameter(package, material, "X", input);
        var node = Node(package, material, "ConstantBiasScale", x);
        node.WriteProperty(new FloatProperty(bias, "Bias")); node.WriteProperty(new FloatProperty(scale, "Scale"));
        material.WriteProperty(Input("Opacity", node)); material.WriteProperty(Input("WorldPositionOffset", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(3, result.UniformPixelScalarExpressions.Count);
        var mul = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions.Last();
        Assert.AreSame(mul, result.UniformVertexScalarExpressions.Last());
        Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Mul, mul.Op);
        Assert.AreEqual(scale, ((MaterialUniformExpressionConstant)mul.B).Value.R);
        var add = (MaterialUniformExpressionFoldedMath)mul.A;
        Assert.AreSame(add, result.UniformPixelScalarExpressions[1]);
        Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Add, add.Op);
        Assert.AreEqual(bias, ((MaterialUniformExpressionConstant)add.A).Value.R);
        Assert.AreSame(result.UniformPixelScalarExpressions[0], add.B);
        var value = LinearColor.Black; mul.GetNumberValue(Context(), ref value); Assert.AreEqual(expected, value.R);
        StringAssert.Contains(result.PropertyCode["Opacity"], "UniformPixelScalars_0.z");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BiasScaleNonuniformVectorUsesScalarFirstMultiply(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("BiasScaleWorld.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var x = package.CreateExport("X", "MaterialExpressionWorldPosition", material, indexed: false);
        var node = Node(package, material, "ConstantBiasScale", x); material.WriteProperty(Input("EmissiveColor", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string t = half ? "half" : "float";
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], $"{t}3 Local0 = ({t}(1.00000000) + Parameters.WorldPosition);");
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], $"{t}3 Local1 = ({t}(0.50000000) * Local0);");
        Assert.AreEqual(8u, result.UsingTransforms);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void DistanceConstantWidthsUseSubtractDotSqrtWithoutIntrinsic(int width)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("Distance.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var a = Vector(package, material, "A", width, 3, 4, 0, 0);
        var b = Vector(package, material, "B", width, 0, 0, 0, 0);
        var node = Node(package, material, "Distance", a, b); material.WriteProperty(Input("Opacity", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["Opacity"], "sqrt(dot(");
        Assert.IsFalse(result.PropertyCode["Opacity"].Contains("distance(", StringComparison.Ordinal));
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
    }

    [TestMethod]
    [DataRow(3f, -1f, 4f)]
    [DataRow(-1f, 3f, 4f)]
    [DataRow(2f, 2f, 0f)]
    public void DistanceDynamicTreeKeepsOrderedSubtractAndRepeatedDot(float aValue, float bValue, float expected)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("DistanceUniform.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var a = ScalarParameter(package, material, "A", aValue); var b = ScalarParameter(package, material, "B", bValue);
        var node = Node(package, material, "Distance", a, b);
        material.WriteProperty(Input("Opacity", node)); material.WriteProperty(Input("WorldPositionOffset", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var sqrt = (MaterialUniformExpressionSquareRoot)result.UniformPixelScalarExpressions.Last();
        Assert.AreSame(sqrt, result.UniformVertexScalarExpressions.Last());
        var dot = (MaterialUniformExpressionFoldedMath)sqrt.X;
        Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Dot, dot.Op); Assert.AreSame(dot.A, dot.B);
        var sub = (MaterialUniformExpressionFoldedMath)dot.A;
        Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Sub, sub.Op);
        Assert.AreEqual("A", ((MaterialUniformExpressionScalarParameter)sub.A).ParameterName.Name);
        Assert.AreEqual("B", ((MaterialUniformExpressionScalarParameter)sub.B).ParameterName.Name);
        var value = LinearColor.Black; sqrt.GetNumberValue(Context(), ref value); Assert.AreEqual(expected, value.R);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DistanceNonuniformUsesThreeLocalsAndBothStages(bool half)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("DistanceWorld.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var a = package.CreateExport("A", "MaterialExpressionWorldPosition", material, indexed: false);
        var b = package.CreateExport("B", "MaterialExpressionObjectWorldPosition", material, indexed: false);
        var node = Node(package, material, "Distance", a, b); material.WriteProperty(Input("Opacity", node)); material.WriteProperty(Input("WorldPositionOffset", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string t = half ? "half" : "float";
        StringAssert.Contains(result.PropertyCode["Opacity"], $"{t}3 Local0 = (Parameters.WorldPosition - ObjectWorldPositionAndRadius.xyz);");
        StringAssert.Contains(result.PropertyCode["Opacity"], $"{t} Local1 = dot(Local0,Local0);");
        StringAssert.Contains(result.PropertyCode["Opacity"], $"{t} Local2 = sqrt(Local1);");
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void DeriveNormalConstantCastWidthsRetainNativeBitIntersection(int width)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("DeriveNormal.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var xy = Vector(package, material, "XY", width, .3f, .4f, .9f, .8f);
        var node = Node(package, material, "DeriveNormalZ", xy); material.WriteProperty(Input("Normal", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string code = result.PropertyCode["Normal"];
        StringAssert.Contains(code, "sqrt("); StringAssert.Contains(code, "dot(");
        Assert.IsFalse(code.Contains("saturate", StringComparison.Ordinal));
        if (width == 1) { StringAssert.Contains(code, "float3(float2("); StringAssert.Contains(code, ",0)"); }
        if (width > 2) StringAssert.Contains(code, ".rg");
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DeriveNormalDynamicInputIsAccessedBeforeCompatibleForceCast(bool scalar)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("DeriveUniform.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var xy = scalar ? ScalarParameter(package, material, "XY", .3f)
            : package.CreateExport("XY", "MaterialExpressionVectorParameter", material, indexed: false);
        if (!scalar) { xy.WriteProperty(new NameProperty("XY", "ParameterName")); xy.WriteProperty(new StructProperty("LinearColor", new PropertyCollection { new FloatProperty(.3f, "R"), new FloatProperty(.4f, "G"), new FloatProperty(0f, "B"), new FloatProperty(1f, "A") }, "DefaultValue")); }
        var node = Node(package, material, "DeriveNormalZ", xy);
        material.WriteProperty(Input("Normal", node)); material.WriteProperty(Input("WorldPositionOffset", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(scalar ? 1 : 0, result.UniformPixelScalarExpressions.Count);
        Assert.AreEqual(scalar ? 0 : 1, result.UniformPixelVectorExpressions.Count);
        StringAssert.Contains(result.PropertyCode["Normal"], "Local0 = dot(UniformPixel");
        StringAssert.Contains(result.PropertyCode["Normal"], "Local2 = sqrt(Local1);");
        StringAssert.Contains(result.PropertyCode["WorldPositionOffset"], "UniformVertex");
        Assert.IsFalse(result.UniformPixelScalarExpressions.Any(e => e is MaterialUniformExpressionSquareRoot));
    }

    [TestMethod]
    [DataRow(0f, 0f)]
    [DataRow(.3f, .4f)]
    [DataRow(1f, 0f)]
    [DataRow(2f, 2f)]
    public void DeriveNormalDoesNotGuardSquareRootOutsideUnitCircle(float x, float y)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("DeriveDomain.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = Node(package, material, "DeriveNormalZ", Vector(package, material, "XY", 2, x, y)); material.WriteProperty(Input("Normal", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["Normal"], "sqrt((float(1.00000000) - dot(");
        Assert.IsFalse(result.PropertyCode["Normal"].Contains("max(", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("ConstantBiasScale", "Input", "Missing ConstantBiasScale input")]
    [DataRow("Distance", "A", "Missing input A")]
    [DataRow("Distance", "B", "Missing input B")]
    [DataRow("DeriveNormalZ", "InXY", "Missing input normal xy vector whose z should be derived.")]
    public void RequiredInputsFailBeforeVisitingOtherInputs(string kind, string missing, string error)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("NumericMissing.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        var node = Node(package, material, kind, time, time);
        node.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(0, "Expression") }, missing));
        material.WriteProperty(Input("Opacity", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        CollectionAssert.AreEqual(new[] { error }, result.Errors.ToArray()); Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    [DataRow("ConstantBiasScale")]
    [DataRow("Distance")]
    [DataRow("DeriveNormalZ")]
    public void CyclesFailClosedAndFreshTranslationDoesNotRetainState(string kind)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("NumericCycle.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var x = Constant(package, material, "X", .5f); var node = Node(package, material, kind, x, x);
        string input = kind == "Distance" ? "A" : kind == "DeriveNormalZ" ? "InXY" : "Input";
        node.WriteProperty(Input(input, node)); material.WriteProperty(Input("Opacity", node));
        var compiler = new LE3MaterialShaderCompiler(material);
        var failed = compiler.TranslateMaterialHlsl(Template()); Assert.IsFalse(failed.Success); Assert.IsNull(failed.MaterialHlsl);
        node.WriteProperty(Input(input, x));
        var valid = compiler.TranslateMaterialHlsl(Template()); Assert.IsTrue(valid.Success, string.Join("; ", valid.Errors));
    }

    [TestMethod]
    [DataRow("ConstantBiasScale")]
    [DataRow("Distance")]
    [DataRow("DeriveNormalZ")]
    public void VertexInputRestrictionsBelongToVisitedInputsNotComposition(string kind)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("NumericVertex.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var uv = package.CreateExport("UV", "MaterialExpressionTextureCoordinate", material, indexed: false);
        var node = Node(package, material, kind, uv, uv); material.WriteProperty(Input("WorldPositionOffset", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Invalid node used in vertex shader input!", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void DistanceIncompatibleWidthsFailClosed()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("DistanceWidths.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = Node(package, material, "Distance", Vector(package, material, "A", 2, 1, 2, 0, 0), Vector(package, material, "B", 3, 1, 2, 3, 0));
        material.WriteProperty(Input("Opacity", node));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template()); Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
    }

    [TestMethod]
    public void BiasScaleFormattingRemainsInvariant()
    {
        var old = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); BiasScaleDefaultsComposeBiasFirstAndScaleSecond(false); }
        finally { CultureInfo.CurrentCulture = old; }
    }

    private static ExportEntry Node(IMEPackage package, ExportEntry material, string kind, ExportEntry a, ExportEntry b = null)
    {
        var node = package.CreateExport("Node", "MaterialExpression" + kind, material, indexed: false);
        node.WriteProperty(Input(kind == "Distance" ? "A" : kind == "DeriveNormalZ" ? "InXY" : "Input", a));
        if (kind == "Distance") node.WriteProperty(Input("B", b)); return node;
    }
    private static ExportEntry Constant(IMEPackage package, ExportEntry material, string name, float value)
    { var node = package.CreateExport(name, "MaterialExpressionConstant", material, indexed: false); node.WriteProperty(new FloatProperty(value, "R")); return node; }
    private static ExportEntry ScalarParameter(IMEPackage package, ExportEntry material, string name, float value)
    { var node = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false); node.WriteProperty(new NameProperty(name, "ParameterName")); node.WriteProperty(new FloatProperty(value, "DefaultValue")); return node; }
    private static ExportEntry Vector(IMEPackage package, ExportEntry material, string name, int width, float r, float g, float b = 0, float a = 0)
    {
        if (width == 1) return Constant(package, material, name, r);
        var node = package.CreateExport(name, $"MaterialExpressionConstant{width}Vector", material, indexed: false);
        if (width == 2) { node.WriteProperty(new FloatProperty(r, "R")); node.WriteProperty(new FloatProperty(g, "G")); }
        else
        {
            node.WriteProperty(new FloatProperty(r, "R")); node.WriteProperty(new FloatProperty(g, "G"));
            node.WriteProperty(new FloatProperty(b, "B")); if (width == 4) node.WriteProperty(new FloatProperty(a, "A"));
        }
        return node;
    }
    private static UniformExpressionRenderContext Context() => new(new(), new(), 0, 0, null);
    private static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
}
