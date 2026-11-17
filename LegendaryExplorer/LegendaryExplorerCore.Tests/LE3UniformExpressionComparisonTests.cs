using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3UniformExpressionComparisonTests
{
    [TestMethod]
    [DataRow("Constant")]
    [DataRow("ScalarParameter")]
    [DataRow("VectorParameter")]
    [DataRow("Texture")]
    [DataRow("TextureParameter")]
    [DataRow("Time")]
    [DataRow("RealTime")]
    [DataRow("Abs")]
    [DataRow("Ceil")]
    [DataRow("Floor")]
    [DataRow("Frac")]
    [DataRow("Periodic")]
    [DataRow("SquareRoot")]
    [DataRow("Sine")]
    [DataRow("Fmod")]
    [DataRow("Max")]
    [DataRow("Min")]
    [DataRow("AppendVector")]
    [DataRow("FoldedMath")]
    [DataRow("Clamp")]
    public void IndependentTreesMatchForEachSupportedUniformClass(string kind)
    {
        var actual = Create(kind); var expected = Clone(actual);
        Assert.AreNotSame(actual, expected);
        string array = actual is MaterialUniformExpressionTexture ? "Uniform2DTextureExpressions" : "UniformPixelVectorExpressions";
        var comparison = CompareUniformExpressionsWithShaderMap(Translation(array, actual), Map(array, expected));
        Assert.IsTrue(comparison.MatchesExpectedUniformExpressions, string.Join("; ", comparison.Errors.Concat(comparison.Differences)));
        Assert.AreEqual(1, comparison.ExpectedExpressionCount); Assert.AreEqual(1, comparison.TranslatedExpressionCount);
        Assert.IsFalse(comparison.IsCompleteShaderSet);
    }

    [TestMethod]
    [DataRow("UniformPixelVectorExpressions")]
    [DataRow("UniformPixelScalarExpressions")]
    [DataRow("Uniform2DTextureExpressions")]
    [DataRow("UniformCubeTextureExpressions")]
    [DataRow("UniformVertexVectorExpressions")]
    [DataRow("UniformVertexScalarExpressions")]
    public void EachArrayRetainsOrdinalOrderAndCounts(string array)
    {
        var first = Create(array.Contains("Texture") ? "Texture" : "ScalarParameter");
        var second = Clone(first);
        if (second is MaterialUniformExpressionTexture t) t.TextureIndex++;
        else ((MaterialUniformExpressionScalarParameter)second).DefaultValue++;
        var translation = Translation(array, first, second);
        var reordered = CompareUniformExpressionsWithShaderMap(translation, Map(array, Clone(second), Clone(first)));
        Assert.IsFalse(reordered.MatchesExpectedUniformExpressions); Assert.AreEqual(2, reordered.Differences.Count);
        StringAssert.Contains(reordered.Differences[0], array + "[0]");
        var missing = CompareUniformExpressionsWithShaderMap(translation, Map(array, Clone(first)));
        Assert.AreEqual(1, missing.Differences.Count); StringAssert.Contains(missing.Differences[0], array + ".Count");
        Assert.AreEqual(0, missing.Errors.Count);
    }

    [TestMethod]
    [DataRow("UniformPixelVectorExpressions")]
    [DataRow("UniformPixelScalarExpressions")]
    [DataRow("Uniform2DTextureExpressions")]
    [DataRow("UniformCubeTextureExpressions")]
    [DataRow("UniformVertexVectorExpressions")]
    [DataRow("UniformVertexScalarExpressions")]
    public void MissingAndNullExpectedArraysFailClosed(string array)
    {
        var map = Map(); typeof(MaterialShaderMap).GetField(array)!.SetValue(map, null);
        var comparison = CompareUniformExpressionsWithShaderMap(Translation(), map);
        Assert.IsFalse(comparison.MatchesExpectedUniformExpressions); Assert.AreEqual(1, comparison.Errors.Count);
        StringAssert.Contains(comparison.Errors[0], array);
    }

    [TestMethod]
    [DataRow("UniformPixelVectorExpressions")]
    [DataRow("UniformPixelScalarExpressions")]
    [DataRow("Uniform2DTextureExpressions")]
    [DataRow("UniformCubeTextureExpressions")]
    [DataRow("UniformVertexVectorExpressions")]
    [DataRow("UniformVertexScalarExpressions")]
    public void MissingTranslatedArraysFailClosed(string array)
    {
        var translation = Translation(); typeof(TranslationResult).GetProperty(array)!.SetValue(translation, null);
        var comparison = CompareUniformExpressionsWithShaderMap(translation, Map());
        Assert.IsFalse(comparison.MatchesExpectedUniformExpressions); Assert.AreEqual(1, comparison.Errors.Count);
        StringAssert.Contains(comparison.Errors[0], "Translated." + array);
    }

    [TestMethod]
    public void TextureInNumericArrayCannotMatchItselfAsValidInventory()
    {
        var texture = Create("Texture");
        var comparison = CompareUniformExpressionsWithShaderMap(Translation("UniformPixelVectorExpressions", texture), Map("UniformPixelVectorExpressions", texture));
        Assert.IsFalse(comparison.MatchesExpectedUniformExpressions); Assert.AreEqual(2, comparison.Errors.Count);
        Assert.IsTrue(comparison.Errors.All(e => e.Contains("numeric/texture root category", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("Constant", "ValueType")]
    [DataRow("Constant", "Value")]
    [DataRow("ScalarParameter", "ParameterName")]
    [DataRow("ScalarParameter", "DefaultValue")]
    [DataRow("VectorParameter", "ParameterName")]
    [DataRow("VectorParameter", "DefaultValue")]
    [DataRow("Texture", "TextureIndex")]
    [DataRow("TextureParameter", "ParameterName")]
    [DataRow("TextureParameter", "TextureIndex")]
    [DataRow("Sine", "bIsCosine")]
    [DataRow("AppendVector", "NumComponentsA")]
    [DataRow("FoldedMath", "Op")]
    public void SerializedIdentityFieldsHaveIndependentMismatchDiagnostics(string kind, string field)
    {
        var actual = Create(kind); var expected = Clone(actual);
        switch (expected)
        {
            case MaterialUniformExpressionConstant c: if (field == "ValueType") c.ValueType = 4; else c.Value.A++; break;
            case MaterialUniformExpressionScalarParameter s: if (field == "ParameterName") s.ParameterName = new("Parameter", 1); else s.DefaultValue++; break;
            case MaterialUniformExpressionVectorParameter v: if (field == "ParameterName") v.ParameterName = "Different"; else v.DefaultValue.A++; break;
            case MaterialUniformExpressionTextureParameter t: if (field == "ParameterName") t.ParameterName = "Different"; else t.TextureIndex++; break;
            case MaterialUniformExpressionTexture t: t.TextureIndex++; break;
            case MaterialUniformExpressionSine sine: sine.bIsCosine = true; break;
            case MaterialUniformExpressionAppendVector append: append.NumComponentsA = 2; break;
            case MaterialUniformExpressionFoldedMath math: math.Op = MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Div; break;
        }
        string array = actual is MaterialUniformExpressionTexture ? "UniformCubeTextureExpressions" : "UniformPixelVectorExpressions";
        var comparison = CompareUniformExpressionsWithShaderMap(Translation(array, actual), Map(array, expected));
        Assert.IsFalse(comparison.MatchesExpectedUniformExpressions); Assert.AreEqual(0, comparison.Errors.Count);
        Assert.AreEqual(1, comparison.Differences.Count); StringAssert.Contains(comparison.Differences[0], field);
    }

    [TestMethod]
    [DataRow("Abs", "X")]
    [DataRow("Fmod", "A")]
    [DataRow("Fmod", "B")]
    [DataRow("Clamp", "Input")]
    [DataRow("Clamp", "Min")]
    [DataRow("Clamp", "Max")]
    public void RecursiveDifferencesReportTheOrderedChildPath(string kind, string child)
    {
        var actual = Create(kind); var expected = Clone(actual);
        expected.GetType().GetField(child)!.SetValue(expected, Create("Time"));
        var comparison = CompareUniformExpressionsWithShaderMap(Translation("UniformPixelVectorExpressions", actual), Map("UniformPixelVectorExpressions", expected));
        Assert.IsFalse(comparison.MatchesExpectedUniformExpressions); Assert.AreEqual(0, comparison.Errors.Count);
        StringAssert.Contains(comparison.Differences.Single(), "." + child + ".Class differs");
    }

    [TestMethod]
    [DataRow("Constant")]
    [DataRow("ScalarParameter")]
    [DataRow("VectorParameter")]
    public void NativeLeafIdentityAcceptsSignedZeroAndUnorderedNaNs(string kind)
    {
        var actual = Create(kind); var expected = Clone(actual);
        SetValue(actual, -0f); SetValue(expected, 0f);
        var match = CompareUniformExpressionsWithShaderMap(Translation("UniformPixelScalarExpressions", actual), Map("UniformPixelScalarExpressions", expected));
        Assert.IsTrue(match.MatchesExpectedUniformExpressions);
        SetValue(actual, float.NaN);
        var sameNan = CompareUniformExpressionsWithShaderMap(Translation("UniformPixelScalarExpressions", actual), Map("UniformPixelScalarExpressions", actual));
        Assert.IsTrue(sameNan.MatchesExpectedUniformExpressions);
        var unordered = CompareUniformExpressionsWithShaderMap(Translation("UniformPixelScalarExpressions", actual), Map("UniformPixelScalarExpressions", expected));
        Assert.IsTrue(unordered.MatchesExpectedUniformExpressions, "UCOMISS/JNZ has no parity branch: an unordered lane passes.");
        Assert.IsTrue(CompareUniformExpressionsWithShaderMap(Translation("UniformPixelScalarExpressions", expected), Map("UniformPixelScalarExpressions", actual)).MatchesExpectedUniformExpressions);

        static void SetValue(MaterialUniformExpression node, float value)
        {
            if (node is MaterialUniformExpressionConstant c) c.Value.R = value;
            if (node is MaterialUniformExpressionScalarParameter s) s.DefaultValue = value;
            if (node is MaterialUniformExpressionVectorParameter v) v.DefaultValue.R = value;
        }
    }

    [TestMethod]
    [DataRow("ScalarParameter")]
    [DataRow("VectorParameter")]
    [DataRow("TextureParameter")]
    public void ParameterNamesUseFNameCaseAndNumberSemantics(string kind)
    {
        var actual = Create(kind); var expected = Clone(actual);
        expected.GetType().GetField("ParameterName")!.SetValue(expected, new NameReference("pArAmEtEr"));
        string array = actual is MaterialUniformExpressionTexture ? "Uniform2DTextureExpressions" : "UniformPixelVectorExpressions";
        Assert.IsTrue(CompareUniformExpressionsWithShaderMap(Translation(array, actual), Map(array, expected)).MatchesExpectedUniformExpressions);
        expected.GetType().GetField("ParameterName")!.SetValue(expected, new NameReference("Parameter", 2));
        Assert.IsFalse(CompareUniformExpressionsWithShaderMap(Translation(array, actual), Map(array, expected)).MatchesExpectedUniformExpressions);
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("unknown")]
    [DataRow("tag")]
    [DataRow("tagNumber")]
    [DataRow("cycle")]
    [DataRow("depth")]
    [DataRow("name")]
    [DataRow("nameNumber")]
    [DataRow("textureIndex")]
    [DataRow("valueType")]
    [DataRow("appendSplit")]
    [DataRow("opcode")]
    public void MalformedAndUnsupportedTreesFailClosedIncludingUnmatchedTrailingEntries(string shape)
    {
        MaterialUniformExpression node = Create("ScalarParameter");
        switch (shape)
        {
            case "null": node = null; break;
            case "unknown": node = new MaterialUniformExpressionFractionOfEffectEnabled { ExpressionType = "FMaterialUniformExpressionFractionOfEffectEnabled" }; break;
            case "tag": node.ExpressionType = "FMaterialUniformExpressionTime"; break;
            case "tagNumber": node.ExpressionType = new(node.ExpressionType.Name, 1); break;
            case "cycle": var cycle = (MaterialUniformExpressionAbs)Create("Abs"); cycle.X = cycle; node = cycle; break;
            case "depth": for (int i = 0; i < 130; i++) node = new MaterialUniformExpressionAbs { ExpressionType = "FMaterialUniformExpressionAbs", X = node }; break;
            case "name": ((MaterialUniformExpressionScalarParameter)node).ParameterName = default; break;
            case "nameNumber": ((MaterialUniformExpressionScalarParameter)node).ParameterName = new("Parameter", -1); break;
            case "textureIndex": node = Create("Texture"); ((MaterialUniformExpressionTexture)node).TextureIndex = -1; break;
            case "valueType": node = Create("Constant"); ((MaterialUniformExpressionConstant)node).ValueType = 0; break;
            case "appendSplit": node = Create("AppendVector"); ((MaterialUniformExpressionAppendVector)node).NumComponentsA = 5; break;
            case "opcode": node = Create("FoldedMath"); ((MaterialUniformExpressionFoldedMath)node).Op = (MaterialUniformExpressionFoldedMath.EFoldedMathOperation)255; break;
        }
        var comparison = CompareUniformExpressionsWithShaderMap(Translation(), Map("UniformPixelVectorExpressions", node));
        Assert.IsFalse(comparison.MatchesExpectedUniformExpressions); Assert.IsTrue(comparison.Errors.Count > 0);
        Assert.AreEqual(1, comparison.Differences.Count, "Count mismatch remains distinct from malformed inventory diagnostics.");
    }

    [TestMethod]
    public void SharedDagIsValidButValidationBudgetIsBounded()
    {
        var leaf = Create("ScalarParameter");
        var dag = (MaterialUniformExpressionFoldedMath)Create("FoldedMath"); dag.A = dag.B = leaf;
        var comparison = CompareUniformExpressionsWithShaderMap(Translation("UniformPixelVectorExpressions", dag), Map("UniformPixelVectorExpressions", Clone(dag)));
        Assert.IsTrue(comparison.MatchesExpectedUniformExpressions);
        var many = Enumerable.Repeat(leaf, 8193).ToArray();
        var tooLarge = CompareUniformExpressionsWithShaderMap(Translation("UniformPixelVectorExpressions", many), Map());
        Assert.IsFalse(tooLarge.MatchesExpectedUniformExpressions);
        Assert.AreEqual(1, tooLarge.Errors.Count); StringAssert.Contains(tooLarge.Errors[0], "visit validation limit");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void TranslatorParameterPoolUsesNativeFirstMatchForUnorderedDefaults(bool vector, bool nanFirst)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3UnorderedUniformPool.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var first = package.CreateExport("First", vector ? "MaterialExpressionVectorParameter" : "MaterialExpressionScalarParameter", material, indexed: false);
        var second = package.CreateExport("Second", first.ClassName, material, indexed: false);
        first.WriteProperty(new NameProperty("SameParameter", "ParameterName")); second.WriteProperty(new NameProperty("SameParameter", "ParameterName"));
        WriteDefault(first, nanFirst ? float.NaN : .25f); WriteDefault(second, nanFirst ? .25f : float.NaN);
        material.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(first.UIndex, "Expression") }, vector ? "EmissiveColor" : "DiffusePower"));
        material.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(second.UIndex, "Expression") }, vector ? "DiffuseColor" : "SpecularPower"));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl("%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n");
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        var uniforms = vector ? result.UniformPixelVectorExpressions : result.UniformPixelScalarExpressions;
        Assert.AreEqual(1, uniforms.Count, "A NaN default does not create a second native-identical parameter slot.");
        float stored = vector ? ((MaterialUniformExpressionVectorParameter)uniforms[0]).DefaultValue.R : ((MaterialUniformExpressionScalarParameter)uniforms[0]).DefaultValue;
        if (nanFirst) Assert.IsTrue(float.IsNaN(stored)); else Assert.AreEqual(.25f, stored);

        void WriteDefault(ExportEntry node, float red)
        {
            if (!vector) node.WriteProperty(new FloatProperty(red, "DefaultValue"));
            else node.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
            { new FloatProperty(red, "R"), new FloatProperty(.5f, "G"), new FloatProperty(.75f, "B"), new FloatProperty(1f, "A") }, "DefaultValue", isImmutable: true));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConstantIdentityReusesFirstPropertyCodeChunkButNotAnotherPropertysLiteral(bool nanFirst)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3UnorderedConstantChunks.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var first = package.CreateExport("First", "MaterialExpressionConstant", material, indexed: false);
        var second = package.CreateExport("Second", "MaterialExpressionConstant", material, indexed: false);
        first.WriteProperty(new FloatProperty(nanFirst ? float.NaN : 1f, "R"));
        second.WriteProperty(new FloatProperty(nanFirst ? 1f : float.NaN, "R"));
        var add = package.CreateExport("Sum", "MaterialExpressionAdd", material, indexed: false);
        add.WriteProperty(Input("A", first)); add.WriteProperty(Input("B", second));
        material.WriteProperty(Input("EmissiveColor", add)); material.WriteProperty(Input("DiffuseColor", second));
        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl("%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n");
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        if (!nanFirst)
        {
            StringAssert.Contains(result.PropertyCode["EmissiveColor"], "(float(1.00000000) + float(1.00000000))");
            StringAssert.Contains(result.PropertyCode["DiffuseColor"], "NaN");
        }
        else
        {
            Assert.IsFalse(result.PropertyCode["EmissiveColor"].Contains("1.00000000", StringComparison.Ordinal));
            StringAssert.Contains(result.PropertyCode["DiffuseColor"], "float(1.00000000)");
        }

        static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") }, name);
    }

    [TestMethod]
    public void StagesClassesAndSuccessfulEmptyTranslationsRemainDistinct()
    {
        var empty = CompareUniformExpressionsWithShaderMap(Translation(), Map());
        Assert.IsTrue(empty.MatchesExpectedUniformExpressions); Assert.IsFalse(empty.IsCompleteShaderSet);
        var staged = CompareUniformExpressionsWithShaderMap(Translation("UniformVertexScalarExpressions", Create("Time")), Map("UniformPixelScalarExpressions", Create("Time")));
        Assert.IsFalse(staged.MatchesExpectedUniformExpressions); Assert.AreEqual(2, staged.Differences.Count);
        var clock = CompareUniformExpressionsWithShaderMap(Translation("UniformPixelScalarExpressions", Create("Time")), Map("UniformPixelScalarExpressions", Create("RealTime")));
        StringAssert.Contains(clock.Differences.Single(), "Class differs");
        Assert.ThrowsExactly<ArgumentNullException>(() => CompareUniformExpressionsWithShaderMap(null, Map()));
        Assert.ThrowsExactly<ArgumentNullException>(() => CompareUniformExpressionsWithShaderMap(Translation(), null));
        Assert.IsFalse(CompareUniformExpressionsWithShaderMap(new TranslationResult(), Map()).MatchesExpectedUniformExpressions);
    }

    private static TranslationResult Translation(string array = null, params MaterialUniformExpression[] expressions)
    {
        var translation = new TranslationResult();
        typeof(TranslationResult).GetProperty(nameof(TranslationResult.Errors))!.SetValue(translation, Array.Empty<string>());
        typeof(TranslationResult).GetProperty(nameof(TranslationResult.MaterialHlsl))!.SetValue(translation, "independent uniform comparison fixture");
        if (array != null) typeof(TranslationResult).GetProperty(array)!.SetValue(translation,
            array.Contains("Texture") ? expressions.Cast<MaterialUniformExpressionTexture>().ToArray() : expressions);
        return translation;
    }

    private static MaterialShaderMap Map(string array = null, params MaterialUniformExpression[] expressions)
    {
        var map = new MaterialShaderMap
        {
            UniformPixelVectorExpressions = [], UniformPixelScalarExpressions = [], Uniform2DTextureExpressions = [],
            UniformCubeTextureExpressions = [], UniformVertexVectorExpressions = [], UniformVertexScalarExpressions = []
        };
        if (array != null) typeof(MaterialShaderMap).GetField(array)!.SetValue(map,
            array.Contains("Texture") ? expressions.Cast<MaterialUniformExpressionTexture>().ToArray() : expressions);
        return map;
    }

    private static MaterialUniformExpression Create(string kind)
    {
        string name = "MaterialUniformExpression" + kind;
        var type = typeof(MaterialUniformExpression).Assembly.GetType("LegendaryExplorerCore.Unreal.BinaryConverters." + name)!;
        var node = (MaterialUniformExpression)Activator.CreateInstance(type)!; node.ExpressionType = "F" + name;
        if (node is MaterialUniformExpressionConstant c) { c.ValueType = 15; c.Value = new(.25f, .5f, .75f, 1f); }
        if (node is MaterialUniformExpressionScalarParameter s) { s.ParameterName = "Parameter"; s.DefaultValue = .25f; }
        if (node is MaterialUniformExpressionVectorParameter v) { v.ParameterName = "Parameter"; v.DefaultValue = new(.25f, .5f, .75f, 1f); }
        if (node is MaterialUniformExpressionTexture t) t.TextureIndex = 2;
        if (node is MaterialUniformExpressionTextureParameter p) p.ParameterName = "Parameter";
        if (node is MaterialUniformExpressionUnaryOp unary) unary.X = Create("ScalarParameter");
        if (node is MaterialUniformExpressionBinaryOp binary) { binary.A = Create("ScalarParameter"); binary.B = Create("Constant"); }
        if (node is MaterialUniformExpressionAppendVector append) append.NumComponentsA = 1;
        if (node is MaterialUniformExpressionClamp clamp) { clamp.Input = Create("ScalarParameter"); clamp.Min = Create("Constant"); clamp.Max = Create("Constant"); }
        return node;
    }

    private static MaterialUniformExpression Clone(MaterialUniformExpression source)
    {
        var clone = (MaterialUniformExpression)Activator.CreateInstance(source.GetType())!;
        foreach (var field in source.GetType().GetFields())
        {
            var value = field.GetValue(source);
            field.SetValue(clone, value is MaterialUniformExpression child ? Clone(child) : value);
        }
        return clone;
    }
}
