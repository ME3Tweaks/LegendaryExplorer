using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3TextureCoordinateCoercionTests
{
    private static readonly string Template = "%u\n" + string.Concat(Enumerable.Repeat("%s\n", 18)) + "#line %u\n";

    [TestMethod]
    public void TwoDimensionalSamplesCoerceGenericScalarsAndShareSamplerIdentity()
    {
        var cases = new[]
        {
            (Source: "Constant", Half: false, SampleClass: "MaterialExpressionTextureSample"),
            (Source: "ScalarParameter", Half: true, SampleClass: "MaterialExpressionTextureSampleParameter2D"),
            (Source: "MaskedScalar", Half: false, SampleClass: "MaterialExpressionTextureSample"),
            (Source: "Time", Half: true, SampleClass: "MaterialExpressionTextureSampleParameter2D")
        };
        foreach (var testCase in cases)
        {
            using var f = new Fixture("Scalar2D_" + testCase.Source);
            var coordinates = ScalarCoordinates(f, testCase.Source);
            var sample = f.Sample(testCase.SampleClass, cube: false, parameterized: testCase.SampleClass != "MaterialExpressionTextureSample");
            sample.WriteProperty(Input("Coordinates", coordinates.Export, coordinates.Masked));
            f.Material.WriteProperty(Input("EmissiveColor", sample));

            var result = f.Translate(testCase.Half);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
            Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
            Assert.AreEqual(0, result.UniformCubeTextureExpressions.Count);
            Assert.AreEqual(1, result.UniformExpressionTextures.Count);
            Assert.AreEqual(f.Texture.UIndex, result.UniformExpressionTextures.Single());
            string code = result.PropertyCode["EmissiveColor"];
            StringAssert.Contains(code, "tex2D(Texture2D_0,");
            int numericAccess = code.IndexOf("UniformPixelScalars_", StringComparison.Ordinal);
            if (testCase.Source is "ScalarParameter" or "Time")
            {
                Assert.IsTrue(numericAccess >= 0, code);
                Assert.AreEqual(1, result.UniformPixelScalarExpressions.Count);
                StringAssert.Contains(SampleCoordinates(code), "float2(UniformPixelScalars_0.x,UniformPixelScalars_0.x)");
            }
            string coordinate = SampleCoordinates(code);
            Assert.IsTrue(coordinate.StartsWith("float2(", StringComparison.Ordinal), code);
            Assert.IsFalse(coordinate.Contains("half", StringComparison.Ordinal), code);
        }
    }

    [TestMethod]
    [DataRow(false, "MaterialExpressionTextureSample")]
    [DataRow(true, "MaterialExpressionTextureSampleParameter2D")]
    [DataRow(true, "MaterialExpressionTextureSampleParameterNormal")]
    public void TwoDimensionalExplicitVectorAndInlineHalfCoordinatesUseFullFloatUv(bool half, string sampleClass)
    {
        using var f = new Fixture("Vector2D_" + half + sampleClass);
        var coordinates = f.Expression("Coordinates", "MaterialExpressionConstant2Vector");
        coordinates.WriteProperty(new FloatProperty(.25f, "R"));
        coordinates.WriteProperty(new FloatProperty(.75f, "G"));
        var sample = f.Sample(sampleClass, cube: false, parameterized: sampleClass != "MaterialExpressionTextureSample");
        sample.WriteProperty(Input("Coordinates", coordinates));
        f.Material.WriteProperty(Input("EmissiveColor", sample));

        var result = f.Translate(half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string coordinateCode = "float2(0.25000000,0.75000000)";
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], $"tex2D(Texture2D_0,{coordinateCode})");
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
    }

    [TestMethod]
    [DataRow("CMOT_Float1", "enum", "MaterialExpressionTextureSample")]
    [DataRow("0", "byte", "MaterialExpressionTextureSampleParameter2D")]
    public void ExplicitFloat1CustomCoordinatesFailBeforeNumericPackingButRegisterTextureSampler(string outputType, string shape, string sampleClass)
    {
        using var f = new Fixture("ExplicitFloat1_" + shape + sampleClass);
        var custom = f.Expression("Explicit", "MaterialExpressionCustom");
        custom.WriteProperty(new StrProperty("1", "Code"));
        custom.WriteProperty(shape == "enum"
            ? new EnumProperty(outputType, "ECustomMaterialOutputType", MEGame.LE3, "OutputType")
            : new ByteProperty(byte.Parse(outputType), "OutputType"));
        var sample = f.Sample(sampleClass, cube: false, parameterized: sampleClass != "MaterialExpressionTextureSample");
        sample.WriteProperty(Input("Coordinates", custom));
        f.Material.WriteProperty(Input("EmissiveColor", sample));

        var result = f.Translate();
        AssertCoercionFailure(result, "float -> float2");
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(1, result.UniformExpressionTextures.Count);
        Assert.AreEqual(f.Texture.UIndex, result.UniformExpressionTextures.Single());
    }

    [TestMethod]
    public void ExplicitWrongWidthTwoDimensionalCoordinatesFailWithoutPackedUniforms()
    {
        foreach (var (coordinateClass, width) in new[]
        {
            ("MaterialExpressionConstant3Vector", 3),
            ("MaterialExpressionConstant4Vector", 4),
            ("MaterialExpressionVectorParameter", 4)
        })
        {
            using var f = new Fixture("WrongWidth2D_" + coordinateClass);
            var coordinates = f.Expression("Coordinates", coordinateClass);
            if (coordinateClass == "MaterialExpressionVectorParameter")
                coordinates.WriteProperty(new NameProperty("WrongWidth", "ParameterName"));
            else
                WriteConstantVector(coordinates, width);
            var sample = f.Sample("MaterialExpressionTextureSample", cube: false, parameterized: false);
            sample.WriteProperty(Input("Coordinates", coordinates));
            f.Material.WriteProperty(Input("EmissiveColor", sample));

            var result = f.Translate();
            AssertCoercionFailure(result, $"float{width} -> float2");
            Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
            Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
            Assert.AreEqual(1, result.UniformExpressionTextures.Count);
            Assert.AreEqual(f.Texture.UIndex, result.UniformExpressionTextures.Single());
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CubeSamplesUseNumericCoercionAndRetainHalfCoordinates(bool half)
    {
        const string sampleClass = "MaterialExpressionTextureSampleParameterCube";
        using var f = new Fixture("CubeScalar_" + half + sampleClass, cube: true);
        var coordinates = f.Expression("Coordinates", "MaterialExpressionConstant");
        coordinates.WriteProperty(new FloatProperty(.5f, "R"));
        var sample = f.Sample(sampleClass, cube: true, parameterized: sampleClass != "MaterialExpressionTextureSample");
        sample.WriteProperty(Input("Coordinates", coordinates));
        f.Material.WriteProperty(Input("EmissiveColor", sample));

        var result = f.Translate(half);
        Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
        string type = half ? "half" : "float";
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], $"{type}3({type}(0.50000000),{type}(0.50000000),{type}(0.50000000))");
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "texCUBE(TextureCube_0,");
        Assert.AreEqual(1, result.UniformCubeTextureExpressions.Count);
        Assert.AreEqual(1, result.UniformExpressionTextures.Count);
    }

    [TestMethod]
    public void ExplicitFloat1CubeCoordinatesFailBeforeNumericPackingButRegisterTextureSampler()
    {
        const string outputType = "CMOT_Float1";
        using var f = new Fixture("CubeExplicitFloat1", cube: true);
        var custom = f.Expression("Explicit", "MaterialExpressionCustom");
        custom.WriteProperty(new StrProperty("1", "Code"));
        custom.WriteProperty(new EnumProperty(outputType, "ECustomMaterialOutputType", MEGame.LE3, "OutputType"));
        var sample = f.Sample("MaterialExpressionTextureSample", cube: true, parameterized: false);
        sample.WriteProperty(Input("Coordinates", custom));
        f.Material.WriteProperty(Input("EmissiveColor", sample));

        var result = f.Translate();
        AssertCoercionFailure(result, "float -> float3");
        Assert.AreEqual(1, result.UniformCubeTextureExpressions.Count);
        Assert.AreEqual(1, result.UniformExpressionTextures.Count);
        Assert.AreEqual(f.Texture.UIndex, result.UniformExpressionTextures.Single());
    }

    [TestMethod]
    public void CubeAcceptsOnlyExplicitFloat3Coordinates()
    {
        foreach (var (coordinateClass, width, accepted) in new[]
        {
            ("MaterialExpressionConstant2Vector", 2, false),
            ("MaterialExpressionConstant3Vector", 3, true),
            ("MaterialExpressionConstant4Vector", 4, false),
            ("MaterialExpressionVectorParameter", 4, false)
        })
        {
            using var f = new Fixture("CubeVector_" + coordinateClass, cube: true);
            var coordinates = f.Expression("Coordinates", coordinateClass);
            if (coordinateClass == "MaterialExpressionVectorParameter")
                coordinates.WriteProperty(new NameProperty("WrongWidth", "ParameterName"));
            else
                WriteConstantVector(coordinates, width);
            var sample = f.Sample("MaterialExpressionTextureSample", cube: true, parameterized: false);
            sample.WriteProperty(Input("Coordinates", coordinates));
            f.Material.WriteProperty(Input("EmissiveColor", sample));

            var result = f.Translate();
            Assert.AreEqual(accepted, result.Success, string.Join("; ", result.Errors));
            if (accepted)
            {
                StringAssert.Contains(result.PropertyCode["EmissiveColor"], "texCUBE(TextureCube_0,float3(");
                Assert.AreEqual(1, result.UniformCubeTextureExpressions.Count);
            }
            else
            {
                AssertCoercionFailure(result, $"float{width} -> float3");
                Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
                Assert.AreEqual(1, result.UniformCubeTextureExpressions.Count);
                Assert.AreEqual(1, result.UniformExpressionTextures.Count);
            }
        }
    }

    [TestMethod]
    public void VertexFrequencyGuardPrecedesTextureAndCoordinateProcessing()
    {
        using var f = new Fixture("VertexGuard");
        var custom = f.Expression("Explicit", "MaterialExpressionCustom");
        custom.WriteProperty(new StrProperty("1", "Code"));
        custom.WriteProperty(new EnumProperty("CMOT_Float1", "ECustomMaterialOutputType", MEGame.LE3, "OutputType"));
        var sample = f.Sample("MaterialExpressionTextureSample", cube: false, parameterized: false);
        sample.WriteProperty(Input("Coordinates", custom));
        // Authored WorldPositionOffset is dispatched as a vertex property.
        f.Material.WriteProperty(Input("WorldPositionOffset", sample));

        var result = f.Translate();
        Assert.IsFalse(result.Success);
        CollectionAssert.AreEqual(new[] { "Invalid node used in vertex shader input!" }, result.Errors.ToArray());
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count + result.UniformExpressionTextures.Count);
    }

    [TestMethod]
    public void TextureUniformIdentityIsSharedWithinPropertyAndTranslationStateIsFresh()
    {
        foreach (bool half in new[] { false, true })
        {
            using var f = new Fixture("TextureCache_" + half);
            var coordinates = f.Expression("Coordinates", "MaterialExpressionConstant2Vector");
            coordinates.WriteProperty(new FloatProperty(.25f, "R"));
            coordinates.WriteProperty(new FloatProperty(.75f, "G"));
            var sample = f.Sample("MaterialExpressionTextureSample", cube: false, parameterized: false);
            sample.WriteProperty(Input("Coordinates", coordinates));
            var add = f.Expression("RepeatedSample", "MaterialExpressionAdd");
            add.WriteProperty(Input("A", sample));
            add.WriteProperty(Input("B", sample));
            f.Material.WriteProperty(Input("EmissiveColor", add));
            f.Material.WriteProperty(Input("DiffuseColor", sample));

            var first = f.Translate(half);
            Assert.IsTrue(first.Success, string.Join("; ", first.Errors));
            Assert.AreEqual(1, first.Uniform2DTextureExpressions.Count);
            Assert.AreEqual(1, first.UniformExpressionTextures.Count);
            Assert.AreEqual(1, first.PropertyCode["EmissiveColor"].Split("tex2D(Texture2D_0,", StringSplitOptions.None).Length - 1);
            Assert.AreEqual(1, first.PropertyCode["DiffuseColor"].Split("tex2D(Texture2D_0,", StringSplitOptions.None).Length - 1);
            var second = f.Translate(half);
            Assert.IsTrue(second.Success, string.Join("; ", second.Errors));
            Assert.AreEqual(first.MaterialHlsl, second.MaterialHlsl);
            Assert.AreEqual(first.UniformExpressionTextures.Single(), second.UniformExpressionTextures.Single());
            Assert.AreEqual(first.PropertyCode["EmissiveColor"], second.PropertyCode["EmissiveColor"]);
        }
    }

    private static void AssertCoercionFailure(LE3MaterialShaderCompiler.TranslationResult result, string expectedWidth)
    {
        Assert.IsFalse(result.Success);
        StringAssert.Contains(string.Join("; ", result.Errors), "Coercion failed:");
        StringAssert.Contains(string.Join("; ", result.Errors), expectedWidth);
    }

    private static string SampleCoordinates(string code)
    {
        int start = code.IndexOf("(Texture2D_0,", StringComparison.Ordinal);
        if (start < 0) start = code.IndexOf("(TextureCube_0,", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, code);
        start += code.IndexOf(',', start) - start + 1;
        int end = code.IndexOf(");", start, StringComparison.Ordinal);
        Assert.IsTrue(end >= 0, code);
        return code[start..end];
    }

    private static (ExportEntry Export, bool Masked) ScalarCoordinates(Fixture f, string source)
    {
        if (source == "MaskedScalar")
        {
            var vector = f.Expression("MaskedCoordinates", "MaterialExpressionConstant2Vector");
            vector.WriteProperty(new FloatProperty(.25f, "R"));
            vector.WriteProperty(new FloatProperty(.75f, "G"));
            return (vector, true);
        }
        string className = source switch
        {
            "Constant" => "MaterialExpressionConstant",
            "ScalarParameter" => "MaterialExpressionScalarParameter",
            "Time" => "MaterialExpressionTime",
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        var scalar = f.Expression("ScalarCoordinates", className);
        if (source == "Constant") scalar.WriteProperty(new FloatProperty(.25f, "R"));
        if (source == "ScalarParameter")
        {
            scalar.WriteProperty(new NameProperty("UvScale", "ParameterName"));
            scalar.WriteProperty(new FloatProperty(.25f, "DefaultValue"));
        }
        return (scalar, false);
    }

    private static void WriteConstantVector(ExportEntry export, int width)
    {
        string[] names = ["R", "G", "B", "A"];
        for (int i = 0; i < width; i++) export.WriteProperty(new FloatProperty(i + .25f, names[i]));
    }

    private static StructProperty Input(string name, ExportEntry expression, bool masked = false) =>
        masked ? new StructProperty("ExpressionInput", new PropertyCollection
        {
            new ObjectProperty(expression.UIndex, "Expression"), new BoolProperty(true, "Mask"),
            new BoolProperty(true, "MaskR"), new BoolProperty(false, "MaskG"),
            new BoolProperty(false, "MaskB"), new BoolProperty(false, "MaskA")
        }, name) : new StructProperty("ExpressionInput", new PropertyCollection
        {
            new ObjectProperty(expression.UIndex, "Expression")
        }, name);

    private sealed class Fixture : IDisposable
    {
        private readonly bool cube;
        private readonly TextureCompilationMetadata metadata;
        public IMEPackage Package { get; }
        public ExportEntry Material { get; }
        public ExportEntry Texture { get; }

        public Fixture(string name, bool cube = false)
        {
            GlobalTest.Init();
            this.cube = cube;
            Package = MEPackageHandler.CreateMemoryEmptyPackage(name + ".pcc", MEGame.LE3);
            Material = Package.CreateExport("Material", "Material", indexed: false);
            Texture = Package.CreateExport(cube ? "TextureCube" : "Texture2D", cube ? "TextureCube" : "Texture2D", indexed: false);
            metadata = new TextureCompilationMetadata(cube ? TextureDimension.TextureCube : TextureDimension.Texture2D);
        }

        public ExportEntry Expression(string name, string className) => Package.CreateExport(name, className, Material, indexed: false);

        public ExportEntry Sample(string className, bool cube, bool parameterized)
        {
            Assert.AreEqual(this.cube, cube);
            var sample = Expression("Sample", className);
            sample.WriteProperty(new ObjectProperty(Texture.UIndex, "Texture"));
            if (parameterized) sample.WriteProperty(new NameProperty("TextureParameter", "ParameterName"));
            return sample;
        }

        public LE3MaterialShaderCompiler.TranslationResult Translate(bool half = false) =>
            new LE3MaterialShaderCompiler(Material, getTextureMetadata: export =>
            {
                Assert.AreSame(Texture, export);
                Assert.AreEqual(cube ? TextureDimension.TextureCube : TextureDimension.Texture2D, metadata.Dimension);
                return metadata;
            }).TranslateMaterialHlsl(Template, useHalfTypes: half);

        public void Dispose() => Package.Dispose();
    }
}
