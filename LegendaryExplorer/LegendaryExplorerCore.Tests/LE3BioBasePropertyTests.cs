using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3BioBasePropertyTests
{
    private const string BasePropertyClass = "BioMaterialExpressionEffectMatBaseProperty";

    [TestMethod]
    [DataRow(0, "0.00000000, 0.00000000, 0.00000000")]
    [DataRow(1, "1.00000000")]
    [DataRow(2, "1.00000000")]
    [DataRow(3, "0.00000000, 0.00000000")]
    [DataRow(4, "0.00000000, 0.00000000, 0.00000000")]
    [DataRow(5, "0.00000000, 0.00000000, 0.00000000")]
    [DataRow(6, "0.00000000")]
    [DataRow(7, "0.00000000, 0.00000000, 0.00000000")]
    [DataRow(8, "15.00000000")]
    [DataRow(9, "0.00000000, 0.00000000, 1.00000000")]
    [DataRow(10, "0.00000000, 0.00000000, 0.00000000")]
    [DataRow(11, "0.00000000")]
    [DataRow(12, "0.00000000")]
    [DataRow(13, "0.00000000")]
    [DataRow(14, "0.00000000")]
    [DataRow(15, "1.00000000, 1.00000000, 1.00000000")]
    [DataRow(16, "0.00000000")]
    [DataRow(255, "0.00000000")]
    public void DisconnectedInputUsesByteDefaults(int propertyId, string expected)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        material.WriteProperty(Connected("EmissiveColor", BaseProperty(package, material, propertyId)));

        var result = Translate(material);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], expected.Replace(" ", ""));
    }

    [TestMethod]
    [DataRow(0)] [DataRow(4)] [DataRow(5)] [DataRow(7)] [DataRow(9)] [DataRow(10)] [DataRow(15)]
    public void ConnectedVectorPropertyCastsFloat3(int propertyId)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var input = Constant(package, material, 3, 2f);
        material.WriteProperty(Connected("EmissiveColor", BaseProperty(package, material, propertyId, input)));

        var result = Translate(material);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "2.00000000,2.00000000,2.00000000");
    }

    [TestMethod]
    [DataRow(1)] [DataRow(2)] [DataRow(8)] [DataRow(14)]
    public void ConnectedScalarPropertyCastsFloat1(int propertyId)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var input = Constant(package, material, 1, 2f);
        material.WriteProperty(Connected("Opacity", BaseProperty(package, material, propertyId, input)));

        var result = Translate(material);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["Opacity"], "2.00000000");
    }

    [TestMethod]
    public void ConnectedVector2PropertyCastsFloat2()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var input = Constant(package, material, 2, 2f);
        material.WriteProperty(Connected("EmissiveColor", BaseProperty(package, material, 3, input)));

        var result = Translate(material);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "2.00000000,2.00000000");
    }

    [TestMethod]
    [DataRow(6)] [DataRow(11)] [DataRow(12)] [DataRow(13)] [DataRow(16)] [DataRow(255)]
    public void UnsupportedConnectedPropertyFailsAfterVisitingChild(int propertyId)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        material.WriteProperty(Connected("Opacity", BaseProperty(package, material, propertyId, time)));

        var result = Translate(material);

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.UsesTime);
        Assert.IsTrue(result.UniformPixelScalarExpressions.Any(expression => expression is MaterialUniformExpressionTime));
        Assert.IsTrue(result.Errors.Count > 0);
    }

    [TestMethod]
    public void DynamicUniformGenericCastKeepsScalarTypeAndValue()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var parameter = ScalarParameter(package, material);
        material.WriteProperty(Connected("EmissiveColor", BaseProperty(package, material, 0, parameter)));

        var result = Translate(material);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(1, result.UniformPixelScalarExpressions.Count);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
        Assert.IsInstanceOfType<MaterialUniformExpressionScalarParameter>(result.UniformPixelScalarExpressions[0]);
    }

    [TestMethod]
    public void VectorParameterPacksFourComponentsBeforeFloat3Cast()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var parameter = VectorParameter(package, material);
        material.WriteProperty(Connected("EmissiveColor", BaseProperty(package, material, 0, parameter)));

        var result = Translate(material);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(1, result.UniformPixelVectorExpressions.Count);
        Assert.IsInstanceOfType<MaterialUniformExpressionVectorParameter>(result.UniformPixelVectorExpressions[0]);
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], ".rgb");
    }

    [TestMethod]
    public void VectorParameterFloat2CastSelectsRg()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var parameter = VectorParameter(package, material);
        material.WriteProperty(Connected("EmissiveColor", BaseProperty(package, material, 3, parameter)));

        var result = Translate(material);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], ".rg");
    }

    [TestMethod]
    public void ExplicitVector2ToVector3PadsZero()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var input = Constant(package, material, 2, 2f);
        material.WriteProperty(Connected("EmissiveColor", BaseProperty(package, material, 0, input)));

        var result = Translate(material);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "float3(float2(2.00000000,2.00000000),0)");
    }

    [TestMethod]
    public void ExplicitVector3ToScalarTruncatesToRed()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var input = Constant(package, material, 3, 2f);
        material.WriteProperty(Connected("Opacity", BaseProperty(package, material, 1, input)));

        var result = Translate(material);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.EndsWith(result.PropertyCode["Opacity"], ").r");
    }

    [TestMethod]
    [DataRow("MP_EmissiveColor", 0)] [DataRow("MP_Opacity", 1)] [DataRow("MP_OpacityMask", 2)]
    [DataRow("MP_Distortion", 3)] [DataRow("MP_TwoSidedLightingMask", 4)] [DataRow("MP_DiffuseColor", 5)]
    [DataRow("MP_DiffusePower", 6)] [DataRow("MP_SpecularColor", 7)] [DataRow("MP_SpecularPower", 8)]
    [DataRow("MP_Normal", 9)] [DataRow("MP_CustomLighting", 10)] [DataRow("MP_CustomLightingDiffuse", 11)]
    [DataRow("MP_AnisotropicDirection", 12)] [DataRow("MP_WorldPositionOffset", 13)] [DataRow("MP_TMissionMask", 14)]
    [DataRow("MP_TMissionColor", 15)] [DataRow("MP_CustomSkylightDiffuse", 16)]
    [DataRow("MP_WrapLightingCoefficients", 17)] [DataRow("MP_MAX", 18)]
    public void EnumBasePropertyNamesUseNativeIndices(string propertyName, int expectedId)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        material.WriteProperty(Connected("EmissiveColor", BaseProperty(package, material, propertyName)));

        var result = Translate(material);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], DefaultFor(expectedId).Replace(" ", ""));
    }

    [TestMethod]
    public void DirectLinkedOutputZeroHasNoAuthorableMaskOrIndex()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var direct = package.CreateExport("Direct", "MaterialExpressionWorldPosition", material, indexed: false);
        var property = BaseProperty(package, material, 0, direct);
        material.WriteProperty(Connected("EmissiveColor", property));

        var compiler = new LE3MaterialShaderCompiler(material);
        var graph = compiler.AnalyzeGraph();
        var result = compiler.TranslateMaterialHlsl("/*\n%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n*/");

        Assert.IsTrue(graph.IsValid, string.Join("; ", graph.Errors));
        Assert.IsTrue(graph.ReachableExpressions.Contains(direct));
        Assert.IsFalse(graph.ConnectedInputs.ContainsKey("Direct"));
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsFalse(result.PropertyCode["EmissiveColor"].Contains(".rgba", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DirectLinkCyclesAreDetectedByBothGraphEntryPoints()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var first = BaseProperty(package, material, 0);
        var second = BaseProperty(package, material, 0, first);
        first.WriteProperty(new ObjectProperty(second.UIndex, "Input"));
        material.WriteProperty(Connected("EmissiveColor", first));

        var compiler = new LE3MaterialShaderCompiler(material);

        AssertCycle(compiler.AnalyzeGraph());
        AssertCycle(compiler.AnalyzeMaterialResourceGraph());
    }

    [TestMethod]
    public void StaticInactiveBranchIsNotVisited()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var inactive = package.CreateExport("Inactive", "MaterialExpressionTextureCoordinate", material, indexed: false);
        var active = Constant(package, material, 3, 1f);
        var selector = package.CreateExport("Selector", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        selector.WriteProperty(new NameProperty("InactiveFeature", "ParameterName"));
        selector.WriteProperty(new BoolProperty(false, "DefaultValue"));
        selector.WriteProperty(Connected("A", inactive));
        selector.WriteProperty(Connected("B", active));
        var node = BaseProperty(package, material, 0, selector);
        material.WriteProperty(Connected("EmissiveColor", node));

        var graph = new LE3MaterialShaderCompiler(material).AnalyzeGraph();

        Assert.IsTrue(graph.IsValid, string.Join("; ", graph.Errors));
        Assert.IsFalse(graph.ReachableExpressions.Contains(inactive));
    }

    [TestMethod]
    public void InvalidLinkAndWrongInputPropertyTypeFailClosed()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var missing = BaseProperty(package, material, 0);
        missing.WriteProperty(new ObjectProperty(99999, "Input"));
        material.WriteProperty(Connected("EmissiveColor", missing));

        var graph = new LE3MaterialShaderCompiler(material).AnalyzeGraph();
        Assert.IsFalse(graph.IsValid);

        missing.WriteProperty(new IntProperty(3, "Input"));
        var result = Translate(material);
        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("Input", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    [DataRow("numbered")]
    [DataRow("unknown")]
    [DataRow("wrong-type")]
    public void InvalidBasePropertyValueFailsClosed(string kind)
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var node = package.CreateExport("BaseProperty", BasePropertyClass, material, indexed: false);
        switch (kind)
        {
            case "numbered": node.WriteProperty(new EnumProperty(new NameReference("MP_Opacity", 1), "EMaterialProperty", MEGame.LE3, "BaseProperty")); break;
            case "unknown": node.WriteProperty(new EnumProperty("MP_NotAProperty", "EMaterialProperty", MEGame.LE3, "BaseProperty")); break;
            default: node.WriteProperty(new IntProperty(1, "BaseProperty")); break;
        }
        material.WriteProperty(Connected("EmissiveColor", node));

        var result = Translate(material);

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("BaseProperty", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void WorldPositionTransformPropagatesThroughDirectLinkAndNotWpoOnlyUsage()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var worldPosition = package.CreateExport("WorldPosition", "MaterialExpressionWorldPosition", material, indexed: false);
        var direct = BaseProperty(package, material, 0, worldPosition);
        material.WriteProperty(Connected("EmissiveColor", direct));
        var pixel = Translate(material);
        Assert.IsTrue(pixel.Success, string.Join("; ", pixel.Errors));
        Assert.AreNotEqual(0u, pixel.UsingTransforms);

        var wpoMaterial = Material(package);
        var wpoWorldPosition = package.CreateExport("WpoWorldPosition", "MaterialExpressionWorldPosition", wpoMaterial, indexed: false);
        wpoMaterial.WriteProperty(Connected("WorldPositionOffset", BaseProperty(package, wpoMaterial, 0, wpoWorldPosition)));
        var wpo = Translate(wpoMaterial);
        Assert.IsTrue(wpo.Success, string.Join("; ", wpo.Errors));
        Assert.AreEqual(0u, wpo.UsingTransforms);
    }

    [TestMethod]
    public void VertexUsageRetainsChildStageAndTimeDiagnostics()
    {
        GlobalTest.Init();
        using var package = Package();
        var material = Material(package);
        var texCoord = package.CreateExport("TexCoord", "MaterialExpressionTextureCoordinate", material, indexed: false);
        material.WriteProperty(Connected("WorldPositionOffset", BaseProperty(package, material, 0, texCoord)));

        var result = Translate(material);

        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("vertex shader", StringComparison.OrdinalIgnoreCase)));
    }

    private static void AssertCycle(LE3MaterialShaderCompiler.GraphAnalysis graph)
    {
        Assert.IsFalse(graph.IsValid);
        Assert.IsTrue(graph.Errors.Any(error => error.Contains("cycle", StringComparison.OrdinalIgnoreCase)));
    }

    private static string DefaultFor(int id) => id switch
    {
        0 or 4 or 5 or 7 or 10 => "0.00000000, 0.00000000, 0.00000000",
        1 or 2 => "1.00000000",
        3 => "0.00000000, 0.00000000",
        8 => "15.00000000",
        9 => "0.00000000, 0.00000000, 1.00000000",
        15 => "1.00000000, 1.00000000, 1.00000000",
        _ => "0.00000000"
    };

    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("LE3BioBaseProperty.pcc", MEGame.LE3);
    private static ExportEntry Material(IMEPackage package) => package.CreateExport("Material", "Material", indexed: false);

    private static ExportEntry BaseProperty(IMEPackage package, ExportEntry material, int id, ExportEntry input = null)
    {
        var node = package.CreateExport("BaseProperty", BasePropertyClass, material, indexed: false);
        node.WriteProperty(new ByteProperty(checked((byte)id), "BaseProperty"));
        if (input != null) node.WriteProperty(new ObjectProperty(input.UIndex, "Input"));
        return node;
    }

    private static ExportEntry BaseProperty(IMEPackage package, ExportEntry material, string enumName)
    {
        var node = package.CreateExport("BaseProperty", BasePropertyClass, material, indexed: false);
        node.WriteProperty(new EnumProperty(enumName, "EMaterialProperty", MEGame.LE3, "BaseProperty"));
        return node;
    }

    private static StructProperty Connected(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection
    {
        new ObjectProperty(expression.UIndex, "Expression")
    }, name);

    private static ExportEntry Constant(IMEPackage package, ExportEntry material, int width, float value)
    {
        string className = width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector";
        var node = package.CreateExport("Constant", className, material, indexed: false);
        node.WriteProperty(new FloatProperty(value, "R"));
        if (width > 1) node.WriteProperty(new FloatProperty(value, "G"));
        if (width > 2) node.WriteProperty(new FloatProperty(value, "B"));
        if (width > 3) node.WriteProperty(new FloatProperty(value, "A"));
        return node;
    }

    private static ExportEntry ScalarParameter(IMEPackage package, ExportEntry material)
    {
        var node = package.CreateExport("Scalar", "MaterialExpressionScalarParameter", material, indexed: false);
        node.WriteProperty(new NameProperty("Scalar", "ParameterName"));
        node.WriteProperty(new FloatProperty(4f, "DefaultValue"));
        return node;
    }

    private static ExportEntry VectorParameter(IMEPackage package, ExportEntry material)
    {
        var node = package.CreateExport("Vector", "MaterialExpressionVectorParameter", material, indexed: false);
        node.WriteProperty(new NameProperty("Vector", "ParameterName"));
        node.WriteProperty(new StructProperty("LinearColor", new PropertyCollection
        {
            new FloatProperty(1f, "R"), new FloatProperty(2f, "G"), new FloatProperty(3f, "B"), new FloatProperty(4f, "A")
        }, "DefaultValue", isImmutable: true));
        return node;
    }

    private static LE3MaterialShaderCompiler.TranslationResult Translate(ExportEntry material) => new LE3MaterialShaderCompiler(material).TranslateMaterialHlsl("/*\n%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n*/");
}
