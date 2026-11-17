using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3BioWoundTests
{
    [TestMethod]
    [DataRow(false, 0)] [DataRow(false, 1)] [DataRow(false, -1)] [DataRow(false, 42)]
    [DataRow(true, 0)] [DataRow(true, 1)] [DataRow(true, -1)] [DataRow(true, 42)]
    public void WoundEmitsExactNumberAsPixelLocal(bool half, int woundNumber)
    {
        var (package, material, wound) = Fixture();
        using (package)
        {
            wound.WriteProperty(new IntProperty(woundNumber, "m_nWoundNumber"));
            material.WriteProperty(Input("EmissiveColor", wound));
            var result = Translate(material, half);
            Success(result);
            string type = half ? "half4" : "float4";
            Assert.AreEqual($"\t{type} Local0 = GetWoundParameters{woundNumber}(Parameters);\r\n\treturn Local0.rgb", result.PropertyCode["EmissiveColor"]);
            AssertPixelOnlyMetadata(result);
        }
    }

    [TestMethod]
    public void MissingWoundNumberUsesDefaultZero()
    {
        var (package, material, wound) = Fixture();
        using (package)
        {
            material.WriteProperty(Input("Opacity", wound));
            var result = Translate(material);
            Success(result);
            StringAssert.Contains(result.PropertyCode["Opacity"], "float4 Local0 = GetWoundParameters0(Parameters);");
            StringAssert.EndsWith(result.PropertyCode["Opacity"], "return Local0.r");
        }
    }

    [TestMethod]
    [DataRow(1, "r")]
    [DataRow(2, "rg")]
    [DataRow(3, "rgb")]
    [DataRow(4, "rgba")]
    public void ExplicitMaskSelectsRequestedWoundComponents(int width, string channels)
    {
        var (package, material, wound) = Fixture();
        using (package)
        {
            material.WriteProperty(MaskedInput("EmissiveColor", wound, width));
            var result = Translate(material);
            Success(result);
            StringAssert.Contains(result.PropertyCode["EmissiveColor"], "Local0." + channels);
            if (width == 2) StringAssert.EndsWith(result.PropertyCode["EmissiveColor"], "return float3(Local0.rg,0)");
        }
    }

    [TestMethod]
    public void WoundCanFeedOpacityRedChannel()
    {
        var (package, material, wound) = Fixture();
        using (package)
        {
            material.WriteProperty(Input("Opacity", wound));
            var result = Translate(material);
            Success(result);
            StringAssert.Contains(result.PropertyCode["Opacity"], "return Local0.r");
        }
    }

    [TestMethod]
    public void WoundFailsClosedInVertexBeforeVisitingAuthoredInput()
    {
        var (package, material, wound) = Fixture();
        using (package)
        {
            wound.WriteProperty(Input("Input", package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false)));
            material.WriteProperty(Input("WorldPositionOffset", wound));
            var result = Translate(material);
            Failure(result);
            CollectionAssert.AreEqual(new[] { "Invalid node used in vertex shader input!" }, result.Errors.ToArray());
            Assert.IsFalse(result.UsesTime);
            Assert.AreEqual(0u, result.UsingTransforms);
            Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformVertexScalarExpressions.Count +
                result.UniformPixelVectorExpressions.Count + result.UniformVertexVectorExpressions.Count);
        }
    }

    [TestMethod]
    public void DistinctWoundNodesUseDistinctLocalsAndRepeatedNodeUsesCache()
    {
        var (package, material, _) = Fixture();
        using (package)
        {
            var first = Wound(package, material, "First", 1);
            var second = Wound(package, material, "Second", 42);
            var inner = package.CreateExport("InnerAdd", "MaterialExpressionAdd", material, indexed: false);
            inner.WriteProperty(Input("A", first));
            inner.WriteProperty(Input("B", second));
            var outer = package.CreateExport("OuterAdd", "MaterialExpressionAdd", material, indexed: false);
            outer.WriteProperty(Input("A", inner));
            outer.WriteProperty(Input("B", inner));
            material.WriteProperty(Input("EmissiveColor", outer));
            var result = Translate(material);
            Success(result);
            string code = result.PropertyCode["EmissiveColor"];
            StringAssert.Contains(code, "float4 Local0 = GetWoundParameters1(Parameters);");
            StringAssert.Contains(code, "float4 Local1 = GetWoundParameters42(Parameters);");
            Assert.AreEqual(1, Count(code, "GetWoundParameters1(Parameters)"));
            Assert.AreEqual(1, Count(code, "GetWoundParameters42(Parameters)"));
        }
    }

    [TestMethod]
    public void NonzeroOutputIndexIsRejectedAsUnsupported()
    {
        var (package, material, wound) = Fixture();
        using (package)
        {
            material.WriteProperty(Input("EmissiveColor", wound, outputIndex: 1));
            var result = Translate(material);
            Failure(result);
            Assert.IsTrue(result.Errors.Any(error => error.Contains("selects output 1", StringComparison.Ordinal) &&
                error.Contains("multiple outputs are not supported yet", StringComparison.Ordinal)));
        }
    }

    private static (IMEPackage Package, ExportEntry Material, ExportEntry Wound) Fixture()
    {
        GlobalTest.Init();
        var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3BioWound.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        return (package, material, Wound(package, material, "Wound", null));
    }

    private static ExportEntry Wound(IMEPackage package, ExportEntry material, string name, int? woundNumber)
    {
        var wound = package.CreateExport(name, "BioMaterialExpressionWound", material, indexed: false);
        if (woundNumber.HasValue) wound.WriteProperty(new IntProperty(woundNumber.Value, "m_nWoundNumber"));
        return wound;
    }

    private static StructProperty Input(string name, ExportEntry expression, int outputIndex = 0)
    {
        var properties = new PropertyCollection { new ObjectProperty(expression.UIndex, "Expression") };
        if (outputIndex != 0) properties.Add(new IntProperty(outputIndex, "OutputIndex"));
        return new StructProperty("ExpressionInput", properties, name);
    }

    private static StructProperty MaskedInput(string name, ExportEntry expression, int width) =>
        new("ExpressionInput", new PropertyCollection
        {
            new ObjectProperty(expression.UIndex, "Expression"), new BoolProperty(true, "Mask"),
            new BoolProperty(width >= 1, "MaskR"), new BoolProperty(width >= 2, "MaskG"),
            new BoolProperty(width >= 3, "MaskB"), new BoolProperty(width >= 4, "MaskA")
        }, name);

    private static LE3MaterialShaderCompiler.TranslationResult Translate(ExportEntry material, bool half = false) =>
        new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl(Template(), half);

    private static void Success(LE3MaterialShaderCompiler.TranslationResult result) =>
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));

    private static void Failure(LE3MaterialShaderCompiler.TranslationResult result)
    {
        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
    }

    private static void AssertPixelOnlyMetadata(LE3MaterialShaderCompiler.TranslationResult result)
    {
        Assert.AreEqual(0u, result.UsingTransforms);
        Assert.IsFalse(result.UsesVertexColor);
        Assert.IsFalse(result.UsesLightmapUVs);
        Assert.IsFalse(result.UsesDynamicParameter);
        Assert.IsFalse(result.UsesTime);
        Assert.IsFalse(result.UsesSceneDepth);
        Assert.IsFalse(result.UsesSceneColor);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count +
            result.UniformVertexScalarExpressions.Count + result.UniformVertexVectorExpressions.Count);
    }

    private static int Count(string value, string fragment) =>
        (value.Length - value.Replace(fragment, string.Empty, StringComparison.Ordinal).Length) / fragment.Length;

    private static string Template() => "/*\n%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n*/";
}
