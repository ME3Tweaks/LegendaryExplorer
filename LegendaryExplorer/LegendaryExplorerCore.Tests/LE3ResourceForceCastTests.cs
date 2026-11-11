using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3ResourceForceCastTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EmissiveAccessesInputUniformBeforeAddingSelectionColor(bool resourceFloat)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ForceCastEmissive.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var emissive = VectorParameter(package, material, "Emissive", "Emissive");
        material.WriteProperty(Input("EmissiveColor", emissive.UIndex));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template(), resourceFloat);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        CollectionAssert.AreEqual(new[] { "SelectionColor", "Emissive" },
            result.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionVectorParameter>().Select(p => p.ParameterName.Name).ToArray());
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "UniformPixelVector_0.rgb");
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "UniformPixelVector_1.rgb");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DiffuseCreatesSelectionFactorBeforeAccessingInputUniform(bool resourceFloat)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ForceCastDiffuse.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var diffuse = VectorParameter(package, material, "Diffuse", "Diffuse");
        material.WriteProperty(Input("DiffuseColor", diffuse.UIndex));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template(), resourceFloat);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        CollectionAssert.AreEqual(new[] { "SelectionColor", "Diffuse" },
            result.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionVectorParameter>().Select(p => p.ParameterName.Name).ToArray());
        StringAssert.Contains(result.PropertyCode["DiffuseColor"], "(float(1.00000000) - UniformPixelVector_0.rgb)");
        string diffuseCode = result.PropertyCode["DiffuseColor"];
        Assert.IsTrue(diffuseCode.IndexOf("UniformPixelVector_0", StringComparison.Ordinal) <
            diffuseCode.IndexOf("UniformPixelVector_1", StringComparison.Ordinal), diffuseCode);
        StringAssert.Contains(diffuseCode, "UniformPixelVector_1.rgb");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TwoSidedMaskAccessesColorBeforeDirectScalarParameterAndKeepsScalarUniform(bool resourceFloat)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ForceCastMaskScalar.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var color = VectorParameter(package, material, "TwoSidedColor", "Color");
        var mask = ScalarParameter(package, material, "TwoSidedMask", "Mask");
        material.WriteProperty(Input("TwoSidedLightingColor", color.UIndex));
        material.WriteProperty(Input("TwoSidedLightingMask", mask.UIndex));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template(), resourceFloat);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("Color", ((MaterialUniformExpressionVectorParameter)result.UniformPixelVectorExpressions[1]).ParameterName.Name);
        Assert.AreEqual("Mask", ((MaterialUniformExpressionScalarParameter)result.UniformPixelScalarExpressions[0]).ParameterName.Name);
        StringAssert.Contains(result.PropertyCode["TwoSidedLightingMask"], "(UniformPixelScalars_0.x * UniformPixelVector_1)");
        Assert.IsTrue(result.PropertyCode["TwoSidedLightingMask"].EndsWith(".rgb", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TwoSidedVectorMaskRetainsVectorLanesUntilAfterMultiply(bool resourceFloat)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ForceCastMaskVector.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var color = VectorParameter(package, material, "TwoSidedColor", "Color");
        var mask = VectorParameter(package, material, "TwoSidedMask", "Mask");
        material.WriteProperty(Input("TwoSidedLightingColor", color.UIndex));
        material.WriteProperty(Input("TwoSidedLightingMask", mask.UIndex));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template(), resourceFloat);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        // Color is compiled first, but remains lazy until Mul. ForceCast packs Mask first.
        CollectionAssert.AreEqual(new[] { "SelectionColor", "Mask", "Color" }, result.UniformPixelVectorExpressions
            .OfType<MaterialUniformExpressionVectorParameter>().Select(p => p.ParameterName.Name).ToArray());
        Assert.AreEqual(3, result.UniformPixelVectorExpressions.Count);
        StringAssert.Contains(result.PropertyCode["TwoSidedLightingMask"], "(UniformPixelVector_1 * UniformPixelVector_2)");
        Assert.IsFalse(result.PropertyCode["TwoSidedLightingMask"].Contains("UniformPixelVector_2.r *", StringComparison.Ordinal));
        Assert.IsTrue(result.PropertyCode["TwoSidedLightingMask"].EndsWith(".rgb", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TwoSidedFoldedScalarMaskAccessesColorFirstAndKeepsFoldedUniform(bool resourceFloat)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ForceCastMaskFolded.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var color = VectorParameter(package, material, "TwoSidedColor", "Color");
        var mask = ScalarParameter(package, material, "TwoSidedMask", "Mask");
        var scale = package.CreateExport("Scale", "MaterialExpressionConstant", material, indexed: false);
        scale.WriteProperty(new FloatProperty(2, "R"));
        var multiply = package.CreateExport("Multiply", "MaterialExpressionMultiply", material, indexed: false);
        multiply.WriteProperty(Input("A", mask.UIndex)); multiply.WriteProperty(Input("B", scale.UIndex));
        material.WriteProperty(Input("TwoSidedLightingColor", color.UIndex));
        material.WriteProperty(Input("TwoSidedLightingMask", multiply.UIndex));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template(), resourceFloat);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual("Color", ((MaterialUniformExpressionVectorParameter)result.UniformPixelVectorExpressions[1]).ParameterName.Name);
        Assert.IsTrue(result.UniformPixelScalarExpressions.Any(e => e is MaterialUniformExpressionFoldedMath));
        StringAssert.Contains(result.PropertyCode["TwoSidedLightingMask"], "UniformPixelScalars_0.y");
        StringAssert.Contains(result.PropertyCode["TwoSidedLightingMask"], "* UniformPixelVector_1");
        Assert.IsTrue(result.PropertyCode["TwoSidedLightingMask"].EndsWith(".rgb", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConstantTwoSidedMaskCanFoldWithDynamicColor(bool resourceFloat)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ForceCastMaskConstant.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var color = VectorParameter(package, material, "TwoSidedColor", "Color");
        var mask = package.CreateExport("Mask", "MaterialExpressionConstant", material, indexed: false);
        mask.WriteProperty(new FloatProperty(.5f, "R"));
        material.WriteProperty(Input("TwoSidedLightingColor", color.UIndex));
        material.WriteProperty(Input("TwoSidedLightingMask", mask.UIndex));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template(), resourceFloat);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        Assert.IsTrue(result.UniformPixelVectorExpressions.Any(e => e is MaterialUniformExpressionFoldedMath));
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
        Assert.AreEqual(3, result.UniformPixelVectorExpressions.Count);
        Assert.IsTrue(result.PropertyCode["TwoSidedLightingMask"].EndsWith("UniformPixelVector_2.rgb", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ResourceRootForceCastTruncatesVectorFourAndPadsVectorTwo(bool resourceFloat)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ForceCastRootWidths.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var vector4 = VectorParameter(package, material, "Four", "Four");
        material.WriteProperty(Input("EmissiveColor", vector4.UIndex));
        var vector2 = package.CreateExport("Vector2", "MaterialExpressionConstant2Vector", material, indexed: false);
        vector2.WriteProperty(new FloatProperty(3, "R")); vector2.WriteProperty(new FloatProperty(4, "G"));
        material.WriteProperty(Input("AnisotropicDirection", vector2.UIndex));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template(), resourceFloat);

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "UniformPixelVector_1.rgb");
        Assert.AreEqual("\treturn float3(float2(3.00000000,4.00000000),0)", result.PropertyCode["AnisotropicDirection"]);
        Assert.AreEqual("Four", ((MaterialUniformExpressionVectorParameter)result.UniformPixelVectorExpressions[1]).ParameterName.Name);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ResourceForceCastDoesNotChangeAuthoredHalfPrecisionPath(bool resourceFloat)
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ForceCastAuthoredPrecision.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var emissive = VectorParameter(package, material, "Emissive", "Emissive");
        material.WriteProperty(Input("EmissiveColor", emissive.UIndex));
        string template = "half2 TexCoords[NUM_MATERIAL_TEXCOORDS];\n" + Template();
        var compiler = new LE3MaterialShaderCompiler(material);

        var authored = compiler.TranslateMaterialHlsl(template);
        var resource = compiler.TranslateMaterialResourceHlsl(template, resourceFloat);

        Assert.IsTrue(authored.Success, string.Join("; ", authored.Errors));
        Assert.IsTrue(resource.Success, string.Join("; ", resource.Errors));
        Assert.IsFalse(authored.UsesHalfTypes);
        Assert.IsFalse(resource.UsesHalfTypes);
        Assert.AreEqual("\treturn UniformPixelVector_0.rgb", authored.PropertyCode["EmissiveColor"]);
        StringAssert.StartsWith(resource.MaterialHlsl,
            resourceFloat ? "float2 TexCoords[NUM_MATERIAL_TEXCOORDS];" : "half2 TexCoords[NUM_MATERIAL_TEXCOORDS];");
        Assert.AreEqual(authored.ForcesFloatTexCoords, compiler.TranslateMaterialHlsl(template).ForcesFloatTexCoords);
    }

    [TestMethod]
    public void ResourceAppendBeyondFourComponentsReturnsDiagnostic()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ForceCastWidthDiagnostic.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var vector3 = package.CreateExport("Vector3", "MaterialExpressionConstant3Vector", material, indexed: false);
        var vector2 = package.CreateExport("Vector2", "MaterialExpressionConstant2Vector", material, indexed: false);
        var append = package.CreateExport("Append", "MaterialExpressionAppendVector", material, indexed: false);
        append.WriteProperty(Input("A", vector3.UIndex)); append.WriteProperty(Input("B", vector2.UIndex));
        material.WriteProperty(Input("Normal", append.UIndex));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        CollectionAssert.Contains(result.Errors.ToArray(), "Material.Append AppendVector produces 5 components; only 2-4 are supported.");
    }

    [TestMethod]
    public void TwoSidedVectorFourMaskWithVectorThreeColorFailsClosed()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ForceCastMaskIncompatibleWidth.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var color = package.CreateExport("Color", "MaterialExpressionConstant3Vector", material, indexed: false);
        color.WriteProperty(new FloatProperty(1, "R")); color.WriteProperty(new FloatProperty(1, "G")); color.WriteProperty(new FloatProperty(1, "B"));
        var mask = VectorParameter(package, material, "Mask", "Mask");
        material.WriteProperty(Input("TwoSidedLightingColor", color.UIndex));
        material.WriteProperty(Input("TwoSidedLightingMask", mask.UIndex));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("Arithmetic between Vector4 and Vector3 is undefined at TwoSidedLightingMask", StringComparison.Ordinal)),
            string.Join("; ", result.Errors));
    }

    [TestMethod]
    public void TwoSidedVectorFourMaskAndColorPreserveFullWidthUntilRootReturn()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ForceCastMaskCompatibleWidth.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var color = VectorParameter(package, material, "Color", "Color");
        var mask = VectorParameter(package, material, "Mask", "Mask");
        material.WriteProperty(Input("TwoSidedLightingColor", color.UIndex));
        material.WriteProperty(Input("TwoSidedLightingMask", mask.UIndex));

        var result = new LE3MaterialShaderCompiler(material).TranslateMaterialResourceHlsl(Template());

        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        StringAssert.Contains(result.PropertyCode["TwoSidedLightingMask"], "(UniformPixelVector_1 * UniformPixelVector_2)");
        Assert.IsFalse(result.PropertyCode["TwoSidedLightingMask"].Contains("UniformPixelVector_2.r *", StringComparison.Ordinal));
        Assert.IsTrue(result.PropertyCode["TwoSidedLightingMask"].EndsWith(".rgb", StringComparison.Ordinal));
    }

    private static ExportEntry VectorParameter(IMEPackage package, ExportEntry material, string name, string parameterName)
    {
        var parameter = package.CreateExport(name, "MaterialExpressionVectorParameter", material, indexed: false);
        parameter.WriteProperty(new NameProperty(parameterName, "ParameterName"));
        return parameter;
    }

    private static ExportEntry ScalarParameter(IMEPackage package, ExportEntry material, string name, string parameterName)
    {
        var parameter = package.CreateExport(name, "MaterialExpressionScalarParameter", material, indexed: false);
        parameter.WriteProperty(new NameProperty(parameterName, "ParameterName"));
        return parameter;
    }

    private static StructProperty Input(string name, int expression) => new("ExpressionInput",
        new PropertyCollection { new ObjectProperty(expression, "Expression") }, name);

    private static string Template() => "/*\n%u\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n%s\n#line %u\n*/";
}
