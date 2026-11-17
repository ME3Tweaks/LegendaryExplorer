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
public class LE3AntialiasedTextureMaskTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DefaultCoordinatesAndCallbackUseFloatOrHalfScalar(bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        var texture = Texture(package, node); material.WriteProperty(Input("Opacity", node)); int reads = 0;
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: entry => { Assert.AreSame(texture, entry); reads++; return new(TextureDimension.Texture2D); }).TranslateMaterialHlsl(Template(), half);
        Success(result); string t = half ? "half" : "float";
        Assert.AreEqual($"\t{t}2 Local0 = Parameters.TexCoords[0].xy;\r\n\t{t} Local1 = AntialiasedTextureMask(Texture2D_0,Texture2D_0Sampler,Local0,{t}(0.50000000),{t}(3.00000000));\r\n\treturn Local1", result.PropertyCode["Opacity"]);
        Assert.AreEqual(1, reads); Assert.AreEqual(1u, result.NumUserTexCoords); Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(typeof(MaterialUniformExpressionTexture), result.Uniform2DTextureExpressions.Single().GetType());
        CollectionAssert.AreEqual(new[] { texture.UIndex }, result.UniformExpressionTextures.ToArray());
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
        Assert.IsFalse(result.UsesTime); Assert.IsFalse(result.ForcesFloatTexCoords);
        Assert.IsFalse(result.UsesSceneColor); Assert.IsFalse(result.UsesSceneDepth);
    }

    [TestMethod]
    [DataRow("TCC_Red", 0)]
    [DataRow("TCC_Green", 1)]
    [DataRow("TCC_Blue", 2)]
    [DataRow("TCC_Alpha", 3)]
    public void NamedChannelEnumsCompileToTheirNumericChannel(string channel, int value)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); Texture(package, node);
        node.WriteProperty(new EnumProperty(channel, "ETextureColorChannel", MEGame.LE3, "Channel")); material.WriteProperty(Input("Opacity", node));
        var result = Compiler(material).TranslateMaterialHlsl(Template()); Success(result);
        StringAssert.Contains(result.PropertyCode["Opacity"], $"AntialiasedTextureMask(Texture2D_0,Texture2D_0Sampler,Local0,float(0.50000000),float({value}.00000000))");
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(255)]
    public void RawByteChannelValuesArePassedThrough(int channel)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); Texture(package, node);
        node.WriteProperty(new ByteProperty((byte)channel, "Channel")); material.WriteProperty(Input("Opacity", node));
        var result = Compiler(material).TranslateMaterialHlsl(Template()); Success(result);
        StringAssert.Contains(result.PropertyCode["Opacity"], $",float({channel}.00000000))");
    }

    [TestMethod]
    [DataRow(false, "", false)]
    [DataRow(true, "None", false)]
    [DataRow(true, "MaskParam", true)]
    public void NamedParameterIdentityAndNoneFallback(bool hasParameter, string parameter, bool expectedParameter)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); Texture(package, node);
        if (hasParameter) node.WriteProperty(new NameProperty(parameter, "ParameterName")); material.WriteProperty(Input("Opacity", node));
        var result = Compiler(material).TranslateMaterialHlsl(Template()); Success(result);
        Assert.AreEqual(expectedParameter, result.Uniform2DTextureExpressions.Single() is MaterialUniformExpressionTextureParameter);
        if (expectedParameter) Assert.AreEqual("MaskParam", ((MaterialUniformExpressionTextureParameter)result.Uniform2DTextureExpressions.Single()).ParameterName.Name);
    }

    [TestMethod]
    public void NoneParameterNumberStillMeansOrdinaryTexture()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); Texture(package, node);
        node.WriteProperty(new NameProperty(new NameReference("None", 4), "ParameterName")); material.WriteProperty(Input("Opacity", node));
        var result = Compiler(material).TranslateMaterialHlsl(Template()); Success(result);
        Assert.AreEqual(typeof(MaterialUniformExpressionTexture), result.Uniform2DTextureExpressions.Single().GetType());
    }

    [TestMethod]
    [DataRow(0f)]
    [DataRow(-1.25f)]
    [DataRow(1f)]
    public void ThresholdIsPassedWithoutClamping(float threshold)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); Texture(package, node);
        node.WriteProperty(new FloatProperty(threshold, "Threshold")); material.WriteProperty(Input("Opacity", node));
        var result = Compiler(material).TranslateMaterialHlsl(Template()); Success(result);
        StringAssert.Contains(result.PropertyCode["Opacity"], $"float({threshold.ToString("0.00000000", System.Globalization.CultureInfo.InvariantCulture)})");
    }

    [TestMethod]
    [DataRow("TC_Default")]
    [DataRow("TC_Normalmap")]
    [DataRow("TC_NormalmapAlpha")]
    [DataRow("TC_NormalmapUncompressed")]
    [DataRow("TC_NormalmapBC5")]
    [DataRow("TC_NormalmapBC7")]
    public void CompressionUnpackAndNormalDecompressionMetadataDoNotChangeCallback(string compression)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); var texture = Texture(package, node);
        node.WriteProperty(new BoolProperty(true, "bDecompressNormalMap")); material.WriteProperty(Input("Opacity", node)); int reads = 0;
        var metadata = new TextureCompilationMetadata(TextureDimension.Texture2D, compression, new(-2, -3, -4, -5), new(7, 8, 9, 10));
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: entry => { Assert.AreSame(texture, entry); reads++; return metadata; }).TranslateMaterialHlsl(Template()); Success(result);
        Assert.AreEqual(1, reads); Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
        StringAssert.Contains(result.PropertyCode["Opacity"], "AntialiasedTextureMask(Texture2D_0,Texture2D_0Sampler,Local0,float(0.50000000),float(3.00000000))");
        Assert.IsFalse(result.PropertyCode["Opacity"].Contains("tex2D(")); Assert.IsFalse(result.PropertyCode["Opacity"].Contains("BiasNormalizeNormalMap"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MetadataOnlyProviderIsCachedAcrossRepeatedNodeVisits(bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var texture = package.CreateExport("Texture", "Texture2D", indexed: false);
        var first = Node(package, material, "First"); var second = Node(package, material, "Second");
        first.WriteProperty(new ObjectProperty(texture.UIndex, "Texture")); second.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        material.WriteProperty(Input("EmissiveColor", first)); material.WriteProperty(Input("Opacity", second)); int reads = 0;
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => { reads++; return new(TextureDimension.Texture2D); }).TranslateMaterialHlsl(Template(), half); Success(result);
        Assert.AreEqual(1, reads); Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        CollectionAssert.AreEqual(new[] { texture.UIndex }, result.UniformExpressionTextures.ToArray());
        Assert.AreEqual(1u, result.NumUserTexCoords);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MetadataProviderCompilesUnloadedTextureWithoutMipReads(bool compressed)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); var texture = Texture(package, node);
        material.WriteProperty(Input("Opacity", node)); using var stream = package.SaveToStream(compressed); stream.Position = 0;
        using var partial = MEPackageHandler.UnsafePartialLoadFromStream(stream, "LE3AntialiasedTextureMaskPartial.pcc", entry => entry.ClassName != "Texture2D");
        int reads = 0; var compiler = new LE3MaterialShaderCompiler(partial.GetUExport(material.UIndex),
            getTextureMetadata: entry => { Assert.AreEqual(texture.UIndex, entry.UIndex); reads++; return new(TextureDimension.Texture2D); });
        var result = compiler.TranslateMaterialHlsl(Template()); Success(result);
        Assert.AreEqual(1, reads); Assert.IsFalse(partial.GetUExport(texture.UIndex).IsDataLoaded());
        CollectionAssert.AreEqual(new[] { texture.UIndex }, result.UniformExpressionTextures.ToArray());
    }

    [TestMethod]
    public void MissingTextureFailsBeforeCoordinatesAndMetadata()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        node.WriteProperty(Input("Coordinates", package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false))); material.WriteProperty(Input("EmissiveColor", node));
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => throw new InvalidOperationException("No metadata expected.")).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.IsFalse(result.UsesTime); Assert.AreEqual(0u, result.NumUserTexCoords);
        CollectionAssert.AreEqual(new[] { "UMaterialExpressionAntialiasedTextureMask> Missing input texture" }, result.Errors.ToArray());
    }

    [TestMethod]
    [DataRow("TextureCube")]
    [DataRow("TextureRenderTarget2D")]
    [DataRow("Texture")]
    public void NonExactTexture2DClassFailsAfterExplicitCoordinates(string textureClass)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        var texture = package.CreateExport("Texture", textureClass, indexed: false); node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        var uv = package.CreateExport("UV", "MaterialExpressionTime", material, indexed: false); node.WriteProperty(Input("Coordinates", uv)); material.WriteProperty(Input("EmissiveColor", node)); int reads = 0;
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => { reads++; return new(TextureDimension.Texture2D); }).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.AreEqual(0, reads); Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count); Assert.IsTrue(result.UsesTime);
        StringAssert.Contains(string.Join("; ", result.Errors), "AAMasked Param2D 'None'> Requires Texture2D");
        Assert.IsTrue(result.UsesTime);
    }

    [TestMethod]
    public void MissingMetadataFailsWithoutRegisteringTexture()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); Texture(package, node); material.WriteProperty(Input("Opacity", node));
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => null).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count); Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        StringAssert.Contains(string.Join("; ", result.Errors), "has no compilation metadata for texture");
    }

    [TestMethod]
    [DataRow("unknown", false)]
    [DataRow("numbered", false)]
    [DataRow("int", false)]
    [DataRow("negative-byte", false)]
    [DataRow("large-int", false)]
    public void MalformedChannelsFailClosed(string kind, bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); Texture(package, node);
        switch (kind)
        {
            case "unknown": node.WriteProperty(new EnumProperty("TCC_MAX", "ETextureColorChannel", MEGame.LE3, "Channel")); break;
            case "numbered": node.WriteProperty(new EnumProperty(new NameReference("TCC_Red", 1), "ETextureColorChannel", MEGame.LE3, "Channel")); break;
            case "int": node.WriteProperty(new IntProperty(3, "Channel")); break;
            case "negative-byte": node.WriteProperty(new IntProperty(-1, "Channel")); break;
            case "large-int": node.WriteProperty(new IntProperty(256, "Channel")); break;
        }
        material.WriteProperty(Input("Opacity", node)); var result = Compiler(material).TranslateMaterialHlsl(Template(), half);
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count);
        StringAssert.Contains(string.Join("; ", result.Errors), ".Channel must be a texture color-channel enum or byte.");
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void ExplicitCoordinateWidthsHaveNoExpressionWidthGate(int width)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); Texture(package, node);
        var uv = Constant(package, material, "UV", width); node.WriteProperty(Input("Coordinates", uv)); material.WriteProperty(Input("Opacity", node));
        var result = Compiler(material).TranslateMaterialHlsl(Template()); Success(result);
        Assert.AreEqual(0u, result.NumUserTexCoords); Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        StringAssert.Contains(result.PropertyCode["Opacity"], "AntialiasedTextureMask(Texture2D_0,Texture2D_0Sampler,");
    }

    [TestMethod]
    public void ExplicitConstantCoordinatesDoNotConsumeDefaultTexCoord()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); Texture(package, node);
        node.WriteProperty(Input("Coordinates", Constant(package, material, "UV", 2))); material.WriteProperty(Input("Opacity", node));
        var result = Compiler(material).TranslateMaterialHlsl(Template()); Success(result); Assert.AreEqual(0u, result.NumUserTexCoords);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DefaultCoordinatesArePixelOnlyWhileExplicitWorldPositionCanTranslateInVertex(bool explicitCoordinates)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); Texture(package, node);
        if (explicitCoordinates) node.WriteProperty(Input("Coordinates", package.CreateExport("WorldPosition", "MaterialExpressionWorldPosition", material, indexed: false)));
        material.WriteProperty(Input("WorldPositionOffset", node)); var result = Compiler(material).TranslateMaterialHlsl(Template());
        Assert.AreEqual(explicitCoordinates, result.Success, string.Join("; ", result.Errors));
        if (explicitCoordinates) StringAssert.Contains(result.PropertyCode["WorldPositionOffset"], "AntialiasedTextureMask(Texture2D_0,Texture2D_0Sampler,Parameters.WorldPosition,");
        else StringAssert.Contains(string.Join("; ", result.Errors), "Invalid node used in vertex shader input!");
    }

    [TestMethod]
    public void StaticBranchOnlyRegistersVisitedTextureNode()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var selected = Node(package, material, "Selected"); var inactive = Node(package, material, "Inactive");
        var selectedTexture = Texture(package, selected, "SelectedTexture"); var inactiveTexture = Texture(package, inactive, "InactiveTexture");
        var branch = package.CreateExport("Switch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        branch.WriteProperty(Input("A", selected)); branch.WriteProperty(Input("B", inactive)); branch.WriteProperty(new BoolProperty(true, "DefaultValue")); material.WriteProperty(Input("Opacity", branch)); int reads = 0;
        var compiler = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => { reads++; return new(TextureDimension.Texture2D); });
        var first = compiler.TranslateMaterialHlsl(Template()); Success(first); Assert.AreEqual(1, reads); Assert.IsTrue(first.UniformExpressionTextures.Contains(selectedTexture.UIndex)); Assert.IsFalse(first.UniformExpressionTextures.Contains(inactiveTexture.UIndex));
        branch.WriteProperty(new BoolProperty(false, "DefaultValue")); var second = compiler.TranslateMaterialHlsl(Template()); Success(second); Assert.AreEqual(2, reads);
        Assert.IsFalse(second.UniformExpressionTextures.Contains(selectedTexture.UIndex)); Assert.IsTrue(second.UniformExpressionTextures.Contains(inactiveTexture.UIndex));
    }

    [TestMethod]
    public void CoordinateTextureSampleRegistersBeforeMaskTexture()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        var outer = Texture(package, node, "Outer"); var inner = package.CreateExport("Inner", "Texture2D", indexed: false);
        var sample = package.CreateExport("CoordinateSample", "MaterialExpressionTextureSample", material, indexed: false); sample.WriteProperty(new ObjectProperty(inner.UIndex, "Texture"));
        var coordinates = Input("Coordinates", sample); coordinates.Properties.Add(new IntProperty(1, "Mask")); coordinates.Properties.Add(new IntProperty(1, "MaskR")); coordinates.Properties.Add(new IntProperty(1, "MaskG")); node.WriteProperty(coordinates); material.WriteProperty(Input("Opacity", node));
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => new(TextureDimension.Texture2D)).TranslateMaterialHlsl(Template()); Success(result);
        CollectionAssert.AreEqual(new[] { inner.UIndex, outer.UIndex }, result.UniformExpressionTextures.ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, result.Uniform2DTextureExpressions.Select(x => x.TextureIndex).ToArray());
        StringAssert.Contains(result.PropertyCode["Opacity"], "AntialiasedTextureMask(Texture2D_1,Texture2D_1Sampler,");
    }

    [TestMethod]
    public void CoordinateCycleFailsBeforeTextureMetadata()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); Texture(package, node);
        node.WriteProperty(Input("Coordinates", node)); material.WriteProperty(Input("Opacity", node)); int reads = 0;
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => { reads++; return new(TextureDimension.Texture2D); }).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.AreEqual(0, reads); Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count);
        StringAssert.Contains(string.Join("; ", result.Errors), "cycle");
    }

    [TestMethod]
    public void DefaultThresholdAndChannelMatchCookedDefaults()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); Texture(package, node); material.WriteProperty(Input("Opacity", node));
        var result = Compiler(material).TranslateMaterialHlsl(Template()); Success(result);
        StringAssert.Contains(result.PropertyCode["Opacity"], "float(0.50000000),float(3.00000000)");
    }

    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("LE3AntialiasedTextureMask.pcc", MEGame.LE3);
    private static ExportEntry Material(IMEPackage package) => package.CreateExport("Material", "Material", indexed: false);
    private static ExportEntry Node(IMEPackage package, ExportEntry material, string name = "Mask") => package.CreateExport(name, "MaterialExpressionAntialiasedTextureMask", material, indexed: false);
    private static ExportEntry Texture(IMEPackage package, ExportEntry node, string name = "Texture")
    {
        var texture = package.CreateExport(name, "Texture2D", indexed: false); node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture")); return texture;
    }
    private static ExportEntry Constant(IMEPackage package, ExportEntry material, string name, int width)
    {
        var constant = package.CreateExport(name, width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector", material, indexed: false);
        constant.WriteProperty(new FloatProperty(.125f, "R")); constant.WriteProperty(new FloatProperty(.25f, "G"));
        constant.WriteProperty(new FloatProperty(.5f, "B")); constant.WriteProperty(new FloatProperty(.75f, "A")); return constant;
    }
    private static StructProperty Input(string name, ExportEntry node) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(node.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static LE3MaterialShaderCompiler Compiler(ExportEntry material) => new(material, getTextureMetadata: _ => new(TextureDimension.Texture2D));
    private static void Success(TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
}
