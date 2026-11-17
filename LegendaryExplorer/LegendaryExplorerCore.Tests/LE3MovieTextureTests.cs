using System;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3MovieTextureTests
{
    [TestMethod]
    public void MovieParameterRequiresAnExactTextureMovieAndTexture2DMetadata()
    {
        foreach (string textureClass in new[] { "Texture2D", "TextureMovieChild", "TextureCube", "Texture" })
        {
            var f = Fixture(textureClass); int reads = 0;
            f.Material.WriteProperty(Input("EmissiveColor", f.Node));
            var failed = Compile(f.Material, _ => { reads++; return new(TextureDimension.Texture2D); });
            Failed(failed, "Requires TextureMovie"); Assert.AreEqual(0, reads, textureClass);
        }

        var valid = Fixture("TextureMovie"); int metadataReads = 0;
        valid.Material.WriteProperty(Input("EmissiveColor", valid.Node));
        var result = Compile(valid.Material, entry =>
        {
            metadataReads++; Assert.AreSame(valid.Texture, entry);
            return new(TextureDimension.Texture2D, "TC_Default");
        });
        Success(result); Assert.AreEqual(1, metadataReads);
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(1, result.UniformExpressionTextures.Count);
    }

    [TestMethod]
    public void MovieReferenceNeedsMetadataAndMetadataMustBeTexture2D()
    {
        var f = Fixture("TextureMovie"); f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var withoutProvider = new LE3MaterialShaderCompiler(f.Material).TranslateMaterialHlsl(Template());
        Failed(withoutProvider, "supply texture compilation metadata");

        var wrongDimension = Compile(f.Material, _ => new(TextureDimension.TextureCube));
        Failed(wrongDimension, "requires Texture2D metadata");
        Assert.AreEqual(0, wrongDimension.Uniform2DTextureExpressions.Count);
    }

    [TestMethod]
    public void MovieParameterNameNoneUsesOrdinaryTextureRegardlessOfNumber()
    {
        foreach (int number in new[] { 0, 4 })
        {
            var f = Fixture("TextureMovie");
            f.Node.WriteProperty(new NameProperty(new NameReference("None", number), "ParameterName"));
            f.Material.WriteProperty(Input("EmissiveColor", f.Node));
            var result = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(result);
            Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
            Assert.IsFalse(result.Uniform2DTextureExpressions.Single() is MaterialUniformExpressionTextureParameter);
        }

        var named = Fixture("TextureMovie");
        named.Node.WriteProperty(new NameProperty(new NameReference("Foo", 6), "ParameterName"));
        named.Material.WriteProperty(Input("EmissiveColor", named.Node));
        var namedResult = Compile(named.Material, _ => new(TextureDimension.Texture2D)); Success(namedResult);
        var namedParameter = namedResult.Uniform2DTextureExpressions.Single() as MaterialUniformExpressionTextureParameter;
        Assert.IsNotNull(namedParameter); Assert.AreEqual(new NameReference("Foo", 6), namedParameter.ParameterName);
    }

    [TestMethod]
    public void MovieCompilationReadsImportedTextureMetadataDirectly()
    {
        var f = Fixture(includeTexture: false);
        var textureImport = new ImportEntry(f.Package, 0, "Movie") { ClassName = "TextureMovie", PackageFile = "Donor" };
        f.Package.AddImport(textureImport); f.Node.WriteProperty(new ObjectProperty(textureImport.UIndex, "Texture"));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node)); int reads = 0;
        var compiler = new LE3MaterialShaderCompiler(f.Material,
            resolveTextureImport: _ => throw new AssertFailedException("The metadata provider owns imported texture lookup."),
            getTextureMetadata: entry => { reads++; Assert.AreSame(textureImport, entry); return new(TextureDimension.Texture2D); });
        var result = compiler.TranslateMaterialHlsl(Template()); Success(result);
        Assert.AreEqual(1, reads); Assert.AreEqual(textureImport.UIndex, result.UniformExpressionTextures.Single());
    }

    [TestMethod]
    public void MovieUnpackUsesMetadataAffineRangesAndNormalCompressionRules()
    {
        var f = Fixture(); f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var ranged = new TextureCompilationMetadata(TextureDimension.Texture2D, "TC_Default",
            new Vector4(-2, -3, -4, -5), new Vector4(7, 8, 9, 10));
        var affine = Compile(f.Material, _ => ranged); Success(affine);
        StringAssert.Contains(affine.PropertyCode["EmissiveColor"], "-2.00000000");
        StringAssert.Contains(affine.PropertyCode["EmissiveColor"], "9.00000000,11.00000000,13.00000000,15.00000000");

        foreach (string compression in new[] { "TC_Normalmap", "TC_NormalmapHQ" })
        {
            var normal = Compile(f.Material, _ => new(TextureDimension.Texture2D, compression,
                new Vector4(-1, -1, -1, 0), new Vector4(1, 1, 1, 1)));
            Success(normal);
            if (compression == "TC_Normalmap") StringAssert.Contains(normal.PropertyCode["EmissiveColor"], "BiasNormalizeNormalMap_DXT1");
            else StringAssert.Contains(normal.PropertyCode["EmissiveColor"], "2.00000000,2.00000000,2.00000000,1.00000000");
        }
    }

    [TestMethod]
    public void MovieUsesAuthoredOrDefaultCoordinatesAndNative2DCoercion()
    {
        var f = Fixture(); f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var defaultUV = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(defaultUV);
        StringAssert.Contains(defaultUV.PropertyCode["EmissiveColor"], "Parameters.TexCoords[0]");
        Assert.AreEqual(1u, defaultUV.NumUserTexCoords);

        var scalar = f.Package.CreateExport("Scalar", "MaterialExpressionTime", f.Material, indexed: false);
        f.Node.WriteProperty(Input("Coordinates", scalar));
        var authored = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(authored);
        Assert.IsTrue(authored.UsesTime);
        StringAssert.Contains(authored.PropertyCode["EmissiveColor"], "tex2D(");

        var vector4 = Fixture(); var four = vector4.Package.CreateExport("Four", "MaterialExpressionConstant4Vector", vector4.Material, indexed: false);
        four.WriteProperty(new FloatProperty(1f, "A"));
        vector4.Node.WriteProperty(Input("Coordinates", four)); vector4.Material.WriteProperty(Input("EmissiveColor", vector4.Node));
        var rejected = Compile(vector4.Material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(rejected.Success);
        Assert.IsNull(rejected.MaterialHlsl);
    }

    [TestMethod]
    public void MovieVertexGuardPrecedesCoordinatesButContinuesTextureAndSampleGuards()
    {
        var f = Fixture(); var time = f.Package.CreateExport("Time", "MaterialExpressionTime", f.Material, indexed: false);
        f.Node.WriteProperty(Input("Coordinates", time)); f.Material.WriteProperty(Input("WorldPositionOffset", f.Node));
        int reads = 0; var result = Compile(f.Material, _ => { reads++; return new(TextureDimension.Texture2D); });
        Assert.IsFalse(result.Success); Assert.AreEqual(0, reads);
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count); Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        Assert.AreEqual(0u, result.NumUserTexCoords); Assert.IsTrue(result.UsesTime);
        Assert.AreEqual(2, result.Errors.Count(e => e == "Invalid node used in vertex shader input!"));
    }

    [TestMethod]
    public void MovieSamplesAndMetadataAreInternedAcrossRootsAndRefreshAcrossTranslations()
    {
        var f = Fixture(); var second = f.Package.CreateExport("Second", "MaterialExpressionTextureSampleParameterMovie", f.Material, indexed: false);
        second.WriteProperty(new ObjectProperty(f.Texture.UIndex, "Texture"));
        f.Material.WriteProperty(new ArrayProperty<ObjectProperty>([new(f.Node.UIndex), new(second.UIndex)], "Expressions"));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node)); f.Material.WriteProperty(Input("Opacity", second)); int reads = 0;
        var compiler = new LE3MaterialShaderCompiler(f.Material, getTextureMetadata: _ => { reads++; return new(TextureDimension.Texture2D); });
        var first = compiler.TranslateMaterialHlsl(Template()); Success(first);
        Assert.AreEqual(1, reads); Assert.AreEqual(1, first.Uniform2DTextureExpressions.Count); Assert.AreEqual(1, first.UniformExpressionTextures.Count);
        Success(compiler.TranslateMaterialHlsl(Template())); Assert.AreEqual(2, reads);
    }

    [TestMethod]
    public void MovieVertexDefaultCoordinatesReportZeroTexCoordsAndHalfUsesHalfSampler()
    {
        var vertex = Fixture(); vertex.Material.WriteProperty(Input("WorldPositionOffset", vertex.Node));
        var vertexResult = Compile(vertex.Material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(vertexResult.Success); Assert.AreEqual(0u, vertexResult.NumUserTexCoords);
        Assert.AreEqual(3, vertexResult.Errors.Count(e => e == "Invalid node used in vertex shader input!"));

        var pixel = Fixture(); pixel.Material.WriteProperty(Input("EmissiveColor", pixel.Node));
        var half = Compile(pixel.Material, _ => new(TextureDimension.Texture2D), half: true); Success(half);
        StringAssert.Contains(half.PropertyCode["EmissiveColor"], "h4tex2D(");
    }

    [TestMethod]
    public void GenericAndNormalTextureParametersWithNumberedNoneFallBackToOrdinaryTexture()
    {
        foreach (string className in new[] { "MaterialExpressionTextureSampleParameter2D", "MaterialExpressionTextureSampleParameterNormal" })
        {
            var f = Fixture(className: className, textureClass: "Texture2D");
            f.Node.WriteProperty(new NameProperty(new NameReference("None", 4), "ParameterName"));
            f.Material.WriteProperty(Input("EmissiveColor", f.Node));
            var result = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(result);
            Assert.IsFalse(result.Uniform2DTextureExpressions.Single() is MaterialUniformExpressionTextureParameter, className);
        }
    }

    private sealed record TestFixture(IMEPackage Package, ExportEntry Material, ExportEntry Node, ExportEntry Texture);

    private static TestFixture Fixture(string textureClass = "TextureMovie", string className = "MaterialExpressionTextureSampleParameterMovie", bool includeTexture = true)
    {
        GlobalTest.Init(); var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MovieTexture.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("MovieSample", className, material, indexed: false);
        var texture = includeTexture ? package.CreateExport("Movie", textureClass, indexed: false) : null;
        if (texture != null) node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        return new(package, material, node, texture);
    }

    private static StructProperty Input(string name, ExportEntry expression) => new("ExpressionInput", new PropertyCollection
    { new ObjectProperty(expression.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static TranslationResult Compile(ExportEntry material, Func<IEntry, TextureCompilationMetadata> metadata, bool half = false) =>
        new LE3MaterialShaderCompiler(material, getTextureMetadata: metadata).TranslateMaterialHlsl(Template(), half);
    private static void Success(TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
    private static void Failed(TranslationResult result, string text) { Assert.IsFalse(result.Success); StringAssert.Contains(string.Join("; ", result.Errors), text); }
}
