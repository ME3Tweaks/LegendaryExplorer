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
public class LE3BioDirtTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DirtComposesRawGrimeSampleAtRequestedPrecision(bool half)
    {
        using var package = Package(); var material = Material(package); var input = Constant(package, material, 3);
        var dirt = Dirt(package, material, input); material.WriteProperty(Input("EmissiveColor", dirt));
        var result = Compile(material, _ => new(TextureDimension.Texture2D), half); Success(result);
        string code = result.PropertyCode["EmissiveColor"], type = half ? "half" : "float";
        Assert.AreEqual(4, Count(code, " = "));
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        Assert.IsInstanceOfType<MaterialUniformExpressionTextureParameter>(result.Uniform2DTextureExpressions.Single());
        Assert.AreEqual(1, result.UniformPixelScalarExpressions.Count);
        Assert.IsInstanceOfType<MaterialUniformExpressionScalarParameter>(result.UniformPixelScalarExpressions.Single());
        StringAssert.Contains(code, type + "4 Local");
        StringAssert.Contains(code, half ? "h4tex2D(Texture2D_0,Local0)" : "tex2D(Texture2D_0,Local0)");
        StringAssert.Contains(code, "Parameters.TexCoords[0].xy");
        Assert.IsFalse(code.Contains("BiasNormalizeNormalMap", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("Unpack", StringComparison.Ordinal));
        Assert.AreEqual(1u, result.NumUserTexCoords);
    }

    [TestMethod]
    public void ShippingDefaultsAndAuthoredNamesPreserveNameNumbers()
    {
        using var package = Package(); var material = Material(package); var input = Constant(package, material, 3); var dirt = Dirt(package, material, input);
        material.WriteProperty(Input("Opacity", dirt));
        var defaults = Compile(material, _ => new(TextureDimension.Texture2D)); Success(defaults);
        var defaultTexture = (MaterialUniformExpressionTextureParameter)defaults.Uniform2DTextureExpressions.Single();
        var defaultScale = (MaterialUniformExpressionScalarParameter)defaults.UniformPixelScalarExpressions.Single();
        Assert.AreEqual("Tex__Grime", defaultTexture.ParameterName.Name); Assert.AreEqual(0, defaultTexture.ParameterName.Number);
        Assert.AreEqual("Scalar__Grime", defaultScale.ParameterName.Name); Assert.AreEqual(0f, defaultScale.DefaultValue);

        dirt.WriteProperty(new NameProperty(new NameReference("GrimeTexture", 7), "GrimeTexParamName"));
        dirt.WriteProperty(new NameProperty(new NameReference("GrimeAmount", 3), "GrimeScaleParamName"));
        var named = Compile(material, _ => new(TextureDimension.Texture2D)); Success(named);
        Assert.AreEqual(new NameReference("GrimeTexture", 7), ((MaterialUniformExpressionTextureParameter)named.Uniform2DTextureExpressions.Single()).ParameterName);
        Assert.AreEqual(new NameReference("GrimeAmount", 3), ((MaterialUniformExpressionScalarParameter)named.UniformPixelScalarExpressions.Single()).ParameterName);
    }

    [TestMethod]
    [DataRow(0, true)]
    [DataRow(2, true)]
    public void NoneTextureParameterRemainsNamedWithOriginalNumber(int number, bool parameter)
    {
        using var package = Package(); var material = Material(package); var dirt = Dirt(package, material, Constant(package, material, 3));
        dirt.WriteProperty(new NameProperty(new NameReference("None", number), "GrimeTexParamName")); material.WriteProperty(Input("Opacity", dirt));
        var result = Compile(material, _ => new(TextureDimension.Texture2D)); Success(result);
        Assert.AreEqual(parameter ? typeof(MaterialUniformExpressionTextureParameter) : typeof(MaterialUniformExpressionTexture), result.Uniform2DTextureExpressions.Single().GetType());
        Assert.AreEqual(new NameReference("None", number), ((MaterialUniformExpressionTextureParameter)result.Uniform2DTextureExpressions.Single()).ParameterName);
    }

    [TestMethod]
    public void MissingInputPrecheckWinsOverMissingTextureAndDoesNotVisitInput()
    {
        using var package = Package(); var material = Material(package); var dirt = Dirt(package, material, null, includeTexture: false);
        dirt.WriteProperty(Input("OtherInput", package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false)));
        material.WriteProperty(Input("Opacity", dirt));
        var result = Compile(material, _ => throw new AssertFailedException("Texture metadata must not be requested."));
        FailedWith(result, "Missing dirt input"); Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    public void MissingTexturePrecheckWinsOverInputVisit()
    {
        using var package = Package(); var material = Material(package); var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        var dirt = Dirt(package, material, time, includeTexture: false); material.WriteProperty(Input("Opacity", dirt));
        var result = Compile(material, _ => throw new AssertFailedException("Texture metadata must not be requested."));
        FailedWith(result, "Missing default textures"); Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    public void ExistingTextureRegistersBeforeTimeInputAndCoordinateGuard()
    {
        using var package = Package(); var material = Material(package); var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        var dirt = Dirt(package, material, time); material.WriteProperty(Input("WorldPositionOffset", dirt));
        var result = Compile(material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.IsTrue(result.UsesTime);
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformVertexScalarExpressions.Count);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        Assert.AreEqual(3, result.Errors.Count(error => error == "Invalid node used in vertex shader input!"));
    }

    [TestMethod]
    [DataRow("TextureCube")]
    [DataRow("TextureRenderTarget2D")]
    [DataRow("Texture")]
    public void NonTexture2DGrimeReferenceFails(string textureClass)
    {
        using var package = Package(); var material = Material(package); var dirt = package.CreateExport("Dirt", "BioMaterialExpressionDirt", material, indexed: false);
        dirt.WriteProperty(Input("Input", Constant(package, material, 3)));
        var texture = package.CreateExport("Grime", textureClass, indexed: false); dirt.WriteProperty(new ObjectProperty(texture.UIndex, "GrimeTex"));
        material.WriteProperty(Input("Opacity", dirt));
        var result = Compile(material, _ => throw new AssertFailedException("Wrong-class reference must fail before metadata."));
        FailedWith(result, ".GrimeTex requires a Texture2D reference.");
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count);
    }

    [TestMethod]
    public void NullMetadataFailsBeforeUniformRegistrationOrInputVisit()
    {
        using var package = Package(); var material = Material(package); var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        var dirt = Dirt(package, material, time); material.WriteProperty(Input("Opacity", dirt)); int calls = 0;
        var result = Compile(material, _ => { calls++; return null; });
        Assert.IsFalse(result.Success); Assert.AreEqual(1, calls); Assert.IsFalse(result.UsesTime);
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count);
        StringAssert.Contains(string.Join("; ", result.Errors), "has no compilation metadata for texture");
    }

    [TestMethod]
    public void TextureCubeMetadataIsRejectedBeforeInputVisit()
    {
        using var package = Package(); var material = Material(package); var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        var dirt = Dirt(package, material, time); material.WriteProperty(Input("Opacity", dirt));
        var result = Compile(material, _ => new(TextureDimension.TextureCube));
        Assert.IsFalse(result.Success); Assert.IsFalse(result.UsesTime); Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void ConstantInputsThreeAndFourExposeNativeRgbAndLerpWidthRules(int width)
    {
        using var package = Package(); var material = Material(package); var dirt = Dirt(package, material, Constant(package, material, width)); material.WriteProperty(Input("EmissiveColor", dirt));
        var result = Compile(material, _ => new(TextureDimension.Texture2D));
        Assert.AreEqual(width == 3, result.Success, string.Join("; ", result.Errors));
        if (width == 3) StringAssert.Contains(result.PropertyCode["EmissiveColor"], "rgb");
        else Assert.IsTrue(result.Errors.Any(error => error.Contains(width == 2 ? "component mask 1110" : "Vector3", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TwoComponentInputFailsRgbMask()
    {
        using var package = Package(); var material = Material(package); var dirt = Dirt(package, material, Constant(package, material, 2)); material.WriteProperty(Input("EmissiveColor", dirt));
        var result = Compile(material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(result.Success); StringAssert.Contains(string.Join("; ", result.Errors), "component mask 1110");
    }

    [TestMethod]
    public void GenericScalarInputDoesNotAssumeLerpBroadcast()
    {
        using var package = Package(); var material = Material(package); var scalar = package.CreateExport("Scalar", "MaterialExpressionScalarParameter", material, indexed: false);
        scalar.WriteProperty(new NameProperty("Scalar", "ParameterName")); scalar.WriteProperty(new FloatProperty(2f, "DefaultValue"));
        var dirt = Dirt(package, material, scalar); material.WriteProperty(Input("EmissiveColor", dirt));
        var result = Compile(material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(result.Success); Assert.IsTrue(result.Errors.Any(error => error.Contains("3", StringComparison.Ordinal) && error.Contains("4", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CompressionAndUnpackMetadataDoNotAddUnpackOrAffineTail(bool half)
    {
        using var package = Package(); var material = Material(package); var dirt = Dirt(package, material, Constant(package, material, 3)); material.WriteProperty(Input("EmissiveColor", dirt));
        var metadata = new TextureCompilationMetadata(TextureDimension.Texture2D, "TC_Normalmap", new Vector4(-2, -3, -4, -5), new Vector4(7, 8, 9, 10));
        var result = Compile(material, _ => metadata, half); Success(result); var code = result.PropertyCode["EmissiveColor"];
        Assert.IsFalse(code.Contains("BiasNormalizeNormalMap", StringComparison.Ordinal)); Assert.IsFalse(code.Contains("Unpack", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("-2.00000000", StringComparison.Ordinal)); Assert.IsFalse(code.Contains("7.00000000", StringComparison.Ordinal));
        StringAssert.Contains(code, half ? "half4 Local" : "float4 Local");
    }

    [TestMethod]
    public void NestedInputTextureAccessPrecedesGrimeTextureAndSamplerPackingIsLazy()
    {
        using var package = Package(); var material = Material(package); var inputTexture = package.CreateExport("InputTexture", "Texture2D", indexed: false);
        var sample = package.CreateExport("InputSample", "MaterialExpressionTextureSample", material, indexed: false); sample.WriteProperty(new ObjectProperty(inputTexture.UIndex, "Texture"));
        var dirt = Dirt(package, material, sample); material.WriteProperty(Input("Opacity", dirt));
        var grimeTexture = dirt.GetProperties().GetProp<ObjectProperty>("GrimeTex").Value;
        var result = Compile(material, _ => new(TextureDimension.Texture2D));
        CollectionAssert.AreEqual(new[] { inputTexture.UIndex, grimeTexture }, result.UniformExpressionTextures.ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, result.Uniform2DTextureExpressions.Select(x => x.TextureIndex).ToArray());
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
        StringAssert.Contains(string.Join("; ", result.Errors), "Vector3");
    }

    [TestMethod]
    public void MetadataProviderIsReadOnceAcrossRootsForSharedGrimeTexture()
    {
        using var package = Package(); var material = Material(package); var texture = package.CreateExport("SharedGrime", "Texture2D", indexed: false);
        var first = Dirt(package, material, Constant(package, material, 3), texture); var second = Dirt(package, material, Constant(package, material, 3), texture);
        material.WriteProperty(Input("EmissiveColor", first)); material.WriteProperty(Input("Opacity", second)); int reads = 0;
        var result = Compile(material, entry => { Assert.AreSame(texture, entry); reads++; return new(TextureDimension.Texture2D); }); Success(result);
        Assert.AreEqual(1, reads); Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(1, result.UniformPixelScalarExpressions.Count);
        CollectionAssert.AreEqual(new[] { texture.UIndex }, result.UniformExpressionTextures.ToArray());
    }

    [TestMethod]
    public void ImportedGrimeUsesProviderWithoutResolvingDonorWhenMetadataExists()
    {
        using var package = Package(); var material = Material(package); var dirt = package.CreateExport("Dirt", "BioMaterialExpressionDirt", material, indexed: false);
        dirt.WriteProperty(Input("Input", Constant(package, material, 3)));
        var import = new ImportEntry(package, 0, "DonorGrime") { ClassName = "Texture2D", PackageFile = "DonorPackage" }; package.AddImport(import);
        dirt.WriteProperty(new ObjectProperty(import.UIndex, "GrimeTex")); material.WriteProperty(Input("Opacity", dirt)); int reads = 0;
        var result = new LE3MaterialShaderCompiler(material, resolveTextureImport: _ => throw new AssertFailedException("Provider metadata must bypass donor resolution."),
            getTextureMetadata: entry => { reads++; Assert.AreSame(import, entry); return new(TextureDimension.Texture2D); }).TranslateMaterialHlsl(Template());
        Success(result); Assert.AreEqual(1, reads);
        Assert.AreEqual(import.UIndex, result.UniformExpressionTextures.Single());
    }

    [TestMethod]
    public void VertexTextureGuardPrecedesInputAndDefaultCoordinateGuard()
    {
        using var package = Package(); var material = Material(package); var input = Constant(package, material, 3); var dirt = Dirt(package, material, input);
        material.WriteProperty(Input("WorldPositionOffset", dirt));
        var result = Compile(material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(result.Success); Assert.IsFalse(result.UsesTime);
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count); Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        Assert.AreEqual(3, result.Errors.Count(error => error == "Invalid node used in vertex shader input!"));
    }

    [TestMethod]
    public void UnsupportedInputStillRegistersTextureWithoutPackingTheScale()
    {
        using var package = Package(); var material = Material(package); var missing = package.CreateExport("Missing", "MaterialExpressionComment", material, indexed: false);
        var dirt = Dirt(package, material, missing); material.WriteProperty(Input("Opacity", dirt));
        var result = Compile(material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(result.Success); Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count); Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count);
    }

    private static IMEPackage Package()
    {
        GlobalTest.Init();
        return MEPackageHandler.CreateMemoryEmptyPackage("LE3BioDirt.pcc", MEGame.LE3);
    }
    private static ExportEntry Material(IMEPackage package) => package.CreateExport("Material", "Material", indexed: false);
    private static ExportEntry Dirt(IMEPackage package, ExportEntry material, ExportEntry input, ExportEntry texture = null, bool includeTexture = true)
    {
        var dirt = package.CreateExport("Dirt", "BioMaterialExpressionDirt", material, indexed: false);
        if (input != null) dirt.WriteProperty(Input("Input", input));
        if (includeTexture) dirt.WriteProperty(new ObjectProperty(texture?.UIndex ?? package.CreateExport("Grime", "Texture2D", indexed: false).UIndex, "GrimeTex"));
        return dirt;
    }
    private static ExportEntry Constant(IMEPackage package, ExportEntry material, int width)
    {
        var node = package.CreateExport("Constant" + width, width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector", material, indexed: false);
        node.WriteProperty(new FloatProperty(1f, "R")); node.WriteProperty(new FloatProperty(2f, "G"));
        node.WriteProperty(new FloatProperty(3f, "B")); node.WriteProperty(new FloatProperty(4f, "A")); return node;
    }
    private static StructProperty Input(string name, ExportEntry node) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(node?.UIndex ?? 0, "Expression") }, name);
    private static TranslationResult Compile(ExportEntry material, Func<IEntry, TextureCompilationMetadata> metadata, bool half = false) =>
        new LE3MaterialShaderCompiler(material, getTextureMetadata: metadata).TranslateMaterialHlsl(Template(), half);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static int Count(string text, string value) => (text.Length - text.Replace(value, "", StringComparison.Ordinal).Length) / value.Length;
    private static void Success(TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
    private static void FailedWith(TranslationResult result, string message)
    {
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); StringAssert.Contains(string.Join("; ", result.Errors), message);
    }
}
