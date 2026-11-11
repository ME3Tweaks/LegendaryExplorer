using System;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3BioSineSubtendTests
{
    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(1, 2)]
    [DataRow(1, 3)]
    [DataRow(1, 4)]
    [DataRow(2, 2)]
    [DataRow(3, 3)]
    [DataRow(4, 4)]
    public void NativeNumericScalarAndMatchingVectorInputsTranslate(int widthA, int widthB)
    {
        var result = TranslateConstants(widthA, widthB);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        AssertSineSubtendCode(result.PropertyCode["Opacity"]);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void GenericScalarBReplicatesToExplicitVectorA(int vectorWidth)
    {
        var result = TranslateConstants(vectorWidth, 1);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        AssertSineSubtendCode(result.PropertyCode["Opacity"]);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public void DifferentExplicitVectorWidthsFailAfterVisitingB(int widthA)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var a = Constant(package, material, widthA, 1f);
        var b = VectorParameter(package, material, "VisitedB");
        var operation = SineSubtend(package, material, a, b);
        material.WriteProperty(Input("Opacity", operation));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("coerc", StringComparison.OrdinalIgnoreCase) ||
            e.Contains("numeric", StringComparison.OrdinalIgnoreCase)), string.Join("; ", result.Errors));
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count,
            "Visiting B creates its expression, but a failed coercion must not pack it.");
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(false, false, true)]
    [DataRow(true, true, true)]
    public void EmitsScalarClampedSubtendFormulaAtRequestedPrecision(bool half, bool useLocal, bool vertex)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        ExportEntry a = useLocal ? package.CreateExport("HalfAngle", "BioMaterialExpressionHalfAngle", material, indexed: false)
            : Constant(package, material, 3, 0.25f);
        var b = Constant(package, material, 3, 0.5f);
        var operation = SineSubtend(package, material, a, b);
        string property = vertex ? "WorldPositionOffset" : "Opacity";
        material.WriteProperty(Input(property, operation));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string code = result.PropertyCode[property];
        AssertSineSubtendCode(code);
        StringAssert.Contains(code, half ? "half" : "float");
        Assert.IsFalse(result.UsesTime);
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.IsFalse(result.UsesVertexColor);
        Assert.IsFalse(result.UsesLightmapUVs);
        Assert.IsFalse(result.UsesDynamicParameter);
        Assert.IsFalse(result.UsesSceneDepth);
        Assert.IsFalse(result.UsesSceneColor);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TimeBIsAccessedButRetainsTheOriginalUniformTree(bool vertex)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var a = Constant(package, material, 3, 0.25f);
        var b = package.CreateExport("TimeB", "MaterialExpressionTime", material, indexed: false);
        var operation = SineSubtend(package, material, a, b);
        string property = vertex ? "WorldPositionOffset" : "Opacity";
        material.WriteProperty(Input(property, operation));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsTrue(result.UsesTime);
        var scalars = vertex ? result.UniformVertexScalarExpressions : result.UniformPixelScalarExpressions;
        Assert.AreEqual(2, scalars.Count);
        Assert.IsInstanceOfType<MaterialUniformExpressionTime>(scalars[0]);
        var tree = (MaterialUniformExpressionBIOSineSubtend)scalars[1];
        Assert.AreSame(scalars[0], tree.B);
        Assert.IsInstanceOfType<MaterialUniformExpressionConstant>(tree.A);
        StringAssert.Contains(result.PropertyCode[property], vertex ? "UniformVertexScalars_0.y" : "UniformPixelScalars_0.y");
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void MissingEitherInputIsReportedBeforeVisitingTheOther(bool missingA, bool missingB)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var timeA = package.CreateExport("TimeA", "MaterialExpressionTime", material, indexed: false);
        var timeB = package.CreateExport("TimeB", "MaterialExpressionTime", material, indexed: false);
        var operation = package.CreateExport("SineSubtend", "BioMaterialExpressionSineSubtend", material, indexed: false);
        if (!missingA) operation.WriteProperty(Input("A", timeA));
        if (!missingB) operation.WriteProperty(Input("B", timeB));
        material.WriteProperty(Input("Opacity", operation));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());

        Assert.IsFalse(result.Success);
        CollectionAssert.AreEqual(new[] { missingA ? "Missing SineSubtend input A" : "Missing SineSubtend input B" }, result.Errors.ToArray());
        Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    public void UniformResultRetainsOriginalOrderedChildrenAndDistinctType()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var a = VectorParameter(package, material, "A");
        var b = VectorParameter(package, material, "B");
        Assert.AreEqual("A", a.GetProperty<NameProperty>("ParameterName")?.Value.Name, "Fixture A parameter");
        Assert.AreEqual("B", b.GetProperty<NameProperty>("ParameterName")?.Value.Name, "Fixture B parameter");
        var operation = SineSubtend(package, material, a, b);
        material.WriteProperty(Input("Opacity", operation));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var tree = result.UniformPixelScalarExpressions.OfType<MaterialUniformExpressionBIOSineSubtend>().Single();
        var vectorParameters = result.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionVectorParameter>().ToArray();
        Assert.AreEqual(2, vectorParameters.Length,
            $"Packed: {string.Join(',', vectorParameters.Select(p => p.ParameterName.Instanced))}; " +
            $"children: {((MaterialUniformExpressionVectorParameter)tree.A).ParameterName.Instanced}," +
            $"{((MaterialUniformExpressionVectorParameter)tree.B).ParameterName.Instanced}; code: {result.PropertyCode["Opacity"]}");
        Assert.AreSame(vectorParameters.Single(p => p.ParameterName.Name == "A"), tree.A);
        Assert.AreSame(vectorParameters.Single(p => p.ParameterName.Name == "B"), tree.B);
        Assert.AreEqual("FMaterialUniformExpressionBIOSineSubtend", tree.ExpressionType.Name);
        Assert.IsFalse(result.UniformPixelScalarExpressions.Any(e => e is MaterialUniformExpressionFoldedMath or MaterialUniformExpressionSquareRoot));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UniformResultInternsAcrossPropertiesAndStages(bool includeVertex)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var a = ScalarParameter(package, material, "A", 0.25f);
        var b = ScalarParameter(package, material, "B", 0.75f);
        var operation = SineSubtend(package, material, a, b);
        material.WriteProperty(Input("Opacity", operation));
        if (includeVertex) material.WriteProperty(Input("WorldPositionOffset", operation));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var pixel = result.UniformPixelScalarExpressions.OfType<MaterialUniformExpressionBIOSineSubtend>().Single();
        if (includeVertex)
            Assert.AreSame(pixel, result.UniformVertexScalarExpressions.OfType<MaterialUniformExpressionBIOSineSubtend>().Single());
        Assert.AreSame(pixel, result.UniformPixelScalarExpressions.OfType<MaterialUniformExpressionBIOSineSubtend>().Single());
    }

    [TestMethod]
    public void ReversedUniformChildrenProduceSeparateInternedTrees()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var a = ScalarParameter(package, material, "A", 0.25f);
        var b = ScalarParameter(package, material, "B", 0.75f);
        var ab = SineSubtend(package, material, a, b, "AB");
        var ba = SineSubtend(package, material, b, a, "BA");
        material.WriteProperty(Input("Opacity", ab));
        material.WriteProperty(Input("DiffusePower", ba));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var trees = result.UniformPixelScalarExpressions.OfType<MaterialUniformExpressionBIOSineSubtend>().ToArray();
        Assert.AreEqual(2, trees.Length);
        Assert.AreNotSame(trees[0].A, trees[1].A);
        Assert.AreSame(trees[0].A, trees[1].B);
        Assert.AreSame(trees[0].B, trees[1].A);
    }

    [TestMethod]
    [DataRow(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f)]
    [DataRow(0f, 0f, 0f, 1f, 0f, 0f, 0f, 0.5f, 0.8660254f)]
    [DataRow(1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f)]
    [DataRow(0f, 0f, 0f, 2f, 0f, 0f, 0f, 2f, 0f)]
    [DataRow(0f, 0f, 0f, 0f, 0f, 0f, 0f, -2f, 1f)]
    public void CpuEvaluationUsesAllFourChannelsAndClamps(float ar, float ag, float ab, float aa,
        float br, float bg, float bb, float ba, float expected)
    {
        var expression = Uniform(ConstantValue(ar, ag, ab, aa), ConstantValue(br, bg, bb, ba));

        var value = Evaluate(expression);

        Assert.AreEqual(expected, value.R, 0.00001f);
        Assert.AreEqual(value.R, value.G);
        Assert.AreEqual(value.R, value.B);
        Assert.AreEqual(value.R, value.A);
    }

    [TestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.MaxValue)]
    public void CpuEvaluationMapsNanAndOverflowToZero(float component)
    {
        var expression = Uniform(ConstantValue(component, 0, 0, 0), ConstantValue(2, 0, 0, 0));

        var value = Evaluate(expression);

        Assert.AreEqual(Vector4.Zero, (Vector4)value);
    }

    [TestMethod]
    public void CpuEvaluationReadsAThenBAndIsFrameIndependentOnlyWhenBothChildrenAre()
    {
        var order = new System.Collections.Generic.List<string>();
        var expression = Uniform(new RecordingExpression("A", order), new RecordingExpression("B", order));
        Assert.IsTrue(expression.IsNotFrameDependent);
        Evaluate(expression);
        CollectionAssert.AreEqual(new[] { "A", "B" }, order.ToArray());
        expression.B = new MaterialUniformExpressionTime { ExpressionType = "FMaterialUniformExpressionTime" };
        Assert.IsFalse(expression.IsNotFrameDependent);
        Assert.AreEqual(1f, Evaluate(expression, time: 1f).R);
    }

    [TestMethod]
    public void UniformSerializationRestoresDistinctBinaryTypeAndOriginalChildren()
    {
        GlobalTest.Init();
        using var package = Package();
        var expression = Uniform(ConstantValue(0.25f, 0.5f, 0.75f, 1f), ConstantValue(1f, 0.75f, 0.5f, 0.25f));
        using var stream = new MemoryStream();
        expression.Serialize(new SerializingContainer(stream, package));
        stream.Position = 0;
        var reader = new SerializingContainer(stream, package, isLoading: true);
        var restored = (MaterialUniformExpressionBIOSineSubtend)MaterialUniformExpression.Create(reader);
        restored.Serialize(reader);

        Assert.AreEqual(stream.Length, stream.Position);
        Assert.AreEqual("FMaterialUniformExpressionBIOSineSubtend", restored.ExpressionType.Name);
        Assert.AreEqual(Evaluate(expression), Evaluate(restored));
        Assert.IsInstanceOfType<MaterialUniformExpressionConstant>(restored.A);
        Assert.IsInstanceOfType<MaterialUniformExpressionConstant>(restored.B);
    }

    [TestMethod]
    public void CyclesFailClosed()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var constant = Constant(package, material, 1, 0.5f);
        var operation = package.CreateExport("SineSubtend", "BioMaterialExpressionSineSubtend", material, indexed: false);
        operation.WriteProperty(Input("A", operation));
        operation.WriteProperty(Input("B", constant));
        material.WriteProperty(Input("Opacity", operation));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("cycle", StringComparison.OrdinalIgnoreCase)));
    }

    private static LE3MaterialShaderCompiler.TranslationResult TranslateConstants(int widthA, int widthB)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var a = Constant(package, material, widthA, 0.25f);
        var b = Constant(package, material, widthB, 0.5f);
        var operation = SineSubtend(package, material, a, b);
        material.WriteProperty(Input("Opacity", operation));
        return new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template());
    }

    private static void AssertSineSubtendCode(string code)
    {
        StringAssert.Contains(code, "sqrt(");
        StringAssert.Contains(code, "max(");
        StringAssert.Contains(code, "min(");
        Assert.IsTrue(code.Split("dot(", StringSplitOptions.None).Length - 1 >= 2, code);
    }

    private static MaterialUniformExpressionBIOSineSubtend Uniform(MaterialUniformExpression a, MaterialUniformExpression b) => new()
    {
        ExpressionType = "FMaterialUniformExpressionBIOSineSubtend", A = a, B = b
    };

    private static MaterialUniformExpressionConstant ConstantValue(float r, float g, float b, float a) => new()
    {
        ExpressionType = "FMaterialUniformExpressionConstant", Value = new LinearColor(r, g, b, a)
    };

    private static LinearColor Evaluate(MaterialUniformExpression expression, float time = 0f)
    {
        LinearColor value = default;
        expression.GetNumberValue(new UniformExpressionRenderContext(new(), new(), time, time, null), ref value);
        return value;
    }

    private sealed class RecordingExpression(string name, System.Collections.Generic.List<string> order) : MaterialUniformExpression
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            order.Add(name);
            outVal = new LinearColor(0f, 0f, 0f, 0f);
        }

        public override bool IsNotFrameDependent => true;
    }

    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("LE3BioSineSubtend.pcc", MEGame.LE3);
    private static ExportEntry Material(IMEPackage package) => package.CreateExport("Material", "Material", indexed: false);

    private static ExportEntry Constant(IMEPackage package, ExportEntry material, int width, float value)
    {
        string className = width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector";
        var node = package.CreateExport($"Constant{width}_{package.Exports.Count}", className, material, indexed: false);
        node.WriteProperty(new FloatProperty(value, "R"));
        if (width > 1) node.WriteProperty(new FloatProperty(value, "G"));
        if (width > 2) node.WriteProperty(new FloatProperty(value, "B"));
        if (width > 3) node.WriteProperty(new FloatProperty(value, "A"));
        return node;
    }

    private static ExportEntry ScalarParameter(IMEPackage package, ExportEntry material, string name, float value)
    {
        var node = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
        node.WriteProperty(new NameProperty(name, "ParameterName"));
        node.WriteProperty(new FloatProperty(value, "DefaultValue"));
        return node;
    }

    private static ExportEntry VectorParameter(IMEPackage package, ExportEntry material, string name)
    {
        var node = package.CreateExport(name, "MaterialExpressionVectorParameter", material, indexed: false);
        node.WriteProperty(new NameProperty(name, "ParameterName"));
        node.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
        {
            new FloatProperty(1f, "R"), new FloatProperty(2f, "G"), new FloatProperty(3f, "B"), new FloatProperty(4f, "A")
        }, "DefaultValue", isImmutable: true));
        return node;
    }

    private static ExportEntry SineSubtend(IMEPackage package, ExportEntry material, ExportEntry a, ExportEntry b, string name = "SineSubtend")
    {
        var node = package.CreateExport(name, "BioMaterialExpressionSineSubtend", material, indexed: false);
        node.WriteProperty(Input("A", a));
        node.WriteProperty(Input("B", b));
        return node;
    }

    private static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput",
        new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") }, name);

    private static string Template() => "/*\n%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n";
}
