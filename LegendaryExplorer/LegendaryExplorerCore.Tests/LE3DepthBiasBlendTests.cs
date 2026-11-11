using System;
using System.IO;
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
public class LE3DepthBiasBlendTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void DefaultGraphPreservesOrderedCallbackComposition(bool half, bool normalize)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        var texture = package.CreateExport("Texture", "Texture2D", indexed: false); node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        node.WriteProperty(new BoolProperty(normalize, "bNormalize")); node.WriteProperty(new BoolProperty(false, "bDecompressNormalMap"));
        material.WriteProperty(Input("EmissiveColor", node)); int reads = 0;
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: reference => { Assert.AreSame(texture, reference); reads++; return new(TextureDimension.Texture2D); }).TranslateMaterialHlsl(Template(), half);
        Success(result); Assert.AreEqual(1, reads); Assert.IsTrue(result.UsesSceneColor); Assert.IsTrue(result.UsesSceneDepth); Assert.AreEqual(1u, result.NumUserTexCoords);
        Assert.IsFalse(result.ForcesFloatTexCoords); Assert.IsFalse(result.UsesTime); Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
        Assert.AreEqual(typeof(MaterialUniformExpressionTexture), result.Uniform2DTextureExpressions.Single().GetType());
        CollectionAssert.AreEqual(new[] { texture.UIndex }, result.UniformExpressionTextures.ToArray());
        string t = half ? "half" : "float"; string sample = half ? "h4tex2D" : "tex2D";
        string expected = $"\t{t}2 Local0 = Parameters.TexCoords[0].xy;\r\n" +
            $"\t{t}4 Local1 = {sample}(Texture2D_0,Local0);\r\n" +
            $"\t{t}4 Local2 = (Local1 * {t}4(1.00000000,1.00000000,1.00000000,1.00000000));\r\n" +
            $"\t{t}4 Local3 = (Local2 + {t}4(0.00000000,0.00000000,0.00000000,0.00000000));\r\n" +
            $"\t{t}4 Local4 = PreviousLighting(Parameters.ScreenPosition);\r\n" +
            $"\t{t} Local5 = PreviousDepth(Parameters.ScreenPosition);\r\n" +
            $"\t{t} Local6 = (Local5 - ({t}(1.00000000) * {t}(1.00000000)));\r\n" +
            $"\t{t} Local7 = (Local5 - Parameters.ScreenPosition.w);\r\n" +
            $"\t{t} Local8 = (Local7 / ({t}(1.00000000) * {t}(1.00000000)));\r\n" +
            $"\t{t} Local9 = min(max(Local8,{t}({t}(0.00000000))),{t}({t}(1.00000000)));\r\n" +
            $"\t{t}4 Local10 = lerp(Local4,Local3,Local9);\r\n" +
            $"\t{t}4 Local11 = ((Local5 >= Parameters.ScreenPosition.w) ? (Local5 > Parameters.ScreenPosition.w ? Local10 : Local10) : Local4);\r\n" +
            $"\t{t}4 Local12 = ((Parameters.ScreenPosition.w >= Local6) ? (Parameters.ScreenPosition.w > Local6 ? Local11 : Local11) : Local3);\r\n\treturn Local12.rgb";
        Assert.AreEqual(expected, result.PropertyCode["EmissiveColor"]); Assert.IsFalse(result.MaterialHlsl.Contains("DepthBiasedBlend("));
    }

    [TestMethod]
    [DataRow("TC_Normalmap", "BiasNormalizeNormalMap_DXT1")]
    [DataRow("TC_NormalmapAlpha", "BiasNormalizeNormalMap_DXT5")]
    [DataRow("TC_NormalmapUncompressed", "BiasNormalizeNormalMap_V8U8")]
    [DataRow("TC_NormalmapBC5", "BiasNormalizeNormalMap_BC5")]
    [DataRow("TC_NormalmapBC7", "BiasNormalizeNormalMap_BC7")]
    [DataRow("TC_NormalmapHQ", "")]
    public void TextureUnpackIsUnconditionalAndIgnoresParameterNormalOverrides(string compression, string helper)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        var texture = package.CreateExport("Texture", "Texture2D", indexed: false); node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        node.WriteProperty(new NameProperty("NotAParameter", "ParameterName")); node.WriteProperty(new BoolProperty(false, "bDecompressNormalMap"));
        material.WriteProperty(Input("EmissiveColor", node)); var parameters = (StaticParameterSet)Guid.Empty;
        parameters.NormalParameters = [new() { ParameterName = "NotAParameter", CompressionSettings = 255, bOverride = true }];
        var metadata = new TextureCompilationMetadata(TextureDimension.Texture2D, compression, new(-1, -1, -1, 0), Vector4.One);
        var compiler = new LE3MaterialShaderCompiler(material, parameters, getTextureMetadata: _ => metadata);
        var result = compiler.TranslateMaterialHlsl(Template()); Success(result); string code = result.PropertyCode["EmissiveColor"];
        if (helper.Length != 0) StringAssert.Contains(code, helper + "(Local1)");
        else { Assert.IsFalse(code.Contains("BiasNormalizeNormalMap")); StringAssert.Contains(code, "float4(2.00000000,2.00000000,2.00000000,1.00000000)"); }
        Assert.AreEqual(typeof(MaterialUniformExpressionTexture), result.Uniform2DTextureExpressions.Single().GetType());
        node.WriteProperty(new BoolProperty(true, "bDecompressNormalMap")); var alternate = compiler.TranslateMaterialHlsl(Template()); Success(alternate);
        Assert.AreEqual(result.MaterialHlsl, alternate.MaterialHlsl);
    }

    [TestMethod]
    [DataRow(false, 0f)]
    [DataRow(false, -2f)]
    [DataRow(false, 3f)]
    [DataRow(true, 0f)]
    [DataRow(true, -2f)]
    [DataRow(true, 3f)]
    public void BiasAndScaleKeepNativeOrderedUniformTreeAndZeroDivisionHlsl(bool half, float scale)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        var texture = package.CreateExport("Texture", "Texture2D", indexed: false); node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        var bias = package.CreateExport("Bias", "MaterialExpressionScalarParameter", material, indexed: false); bias.WriteProperty(new NameProperty("Bias", "ParameterName"));
        node.WriteProperty(Input("Bias", bias)); node.WriteProperty(new FloatProperty(scale, "BiasScale")); material.WriteProperty(Input("EmissiveColor", node));
        var result = Compiler(material).TranslateMaterialHlsl(Template(), half); Success(result); string t = half ? "half" : "float";
        Assert.AreEqual(3, result.UniformPixelScalarExpressions.Count);
        var tree = (MaterialUniformExpressionFoldedMath)result.UniformPixelScalarExpressions[2]; Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Mul, tree.Op);
        var difference = (MaterialUniformExpressionFoldedMath)tree.A; Assert.AreEqual(MaterialUniformExpressionFoldedMath.EFoldedMathOperation.Sub, difference.Op);
        Assert.AreEqual(1f, ((MaterialUniformExpressionConstant)difference.A).Value.R); Assert.AreEqual("Bias", ((MaterialUniformExpressionScalarParameter)difference.B).ParameterName.Name);
        Assert.AreSame(result.UniformPixelScalarExpressions[0], difference.B); Assert.AreSame(result.UniformPixelScalarExpressions[1], difference);
        Assert.AreEqual(scale, ((MaterialUniformExpressionConstant)tree.B).Value.R); StringAssert.Contains(result.PropertyCode["EmissiveColor"], "Local7 / UniformPixelScalars_0.z");
        Assert.IsFalse(result.PropertyCode["EmissiveColor"].Contains("max(UniformPixelScalars_0.z"));
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], $"{t} Local6 = (Local5 - UniformPixelScalars_0.z)");
    }

    [TestMethod]
    [DataRow(1, false)]
    [DataRow(1, true)]
    [DataRow(2, false)]
    [DataRow(2, true)]
    [DataRow(3, false)]
    [DataRow(3, true)]
    [DataRow(4, false)]
    [DataRow(4, true)]
    public void VectorBiasCannotBroadcastAgainstExplicitDepthWithoutInputMask(int width, bool mask)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        node.WriteProperty(new ObjectProperty(package.CreateExport("Texture", "Texture2D", indexed: false).UIndex, "Texture"));
        var bias = package.CreateExport("Bias", width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector", material, indexed: false);
        var input = Input("Bias", bias); if (mask) { input.Properties.Add(new IntProperty(1, "Mask")); input.Properties.Add(new IntProperty(1, "MaskR")); }
        node.WriteProperty(input); material.WriteProperty(Input("EmissiveColor", node)); var result = Compiler(material).TranslateMaterialHlsl(Template());
        Assert.AreEqual(width == 1 || mask, result.Success, string.Join("; ", result.Errors)); Assert.IsTrue(result.UsesSceneColor); Assert.IsTrue(result.UsesSceneDepth);
        if (!result.Success) { Assert.IsNull(result.MaterialHlsl); StringAssert.Contains(string.Join("; ", result.Errors), "arithmetic between native numeric types"); }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void MetadataOnlyImportsAndPartialTextureExportsStayUnloaded(bool compressed, bool imported)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material); IEntry texture;
        if (imported) { var import = new ImportEntry(package, 0, "ExternalTexture") { ClassName = "Texture2D", PackageFile = "UnopenedDonor" }; package.AddImport(import); texture = import; }
        else texture = package.CreateExport("Texture", "Texture2D", indexed: false);
        node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture")); material.WriteProperty(Input("EmissiveColor", node));
        using var stream = package.SaveToStream(compressed); stream.Position = 0;
        using var partial = MEPackageHandler.UnsafePartialLoadFromStream(stream, "LE3DepthBiasPartial.pcc", e => e.ClassName is not ("Texture2D" or "TextureCube"));
        int reads = 0, donors = 0;
        var compiler = new LE3MaterialShaderCompiler(partial.GetUExport(material.UIndex), resolveTextureImport: _ => { donors++; throw new InvalidOperationException("No donor resolution expected."); },
            getTextureMetadata: entry => { Assert.AreEqual(texture.UIndex, entry.UIndex); reads++; return new(TextureDimension.Texture2D); });
        var result = compiler.TranslateMaterialHlsl(Template()); Success(result); Assert.AreEqual(1, reads); Assert.AreEqual(0, donors);
        CollectionAssert.AreEqual(new[] { texture.UIndex }, result.UniformExpressionTextures.ToArray());
        if (!imported) Assert.IsFalse(partial.GetUExport(texture.UIndex).IsDataLoaded());
    }

    [TestMethod]
    [DataRow("BLEND_Opaque", false, false, false)]
    [DataRow("BLEND_Masked", false, false, false)]
    [DataRow("BLEND_SoftMasked", false, false, false)]
    [DataRow("BLEND_Opaque", true, false, true)]
    [DataRow("BLEND_Translucent", false, false, true)]
    [DataRow("BLEND_Additive", false, false, true)]
    [DataRow("BLEND_Translucent", false, true, false)]
    [DataRow("BLEND_Opaque", true, true, false)]
    public void ResourceDepthAndSceneColorGatesBothApply(string blend, bool effect, bool decal, bool success)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        node.WriteProperty(new ObjectProperty(package.CreateExport("Texture", "Texture2D", indexed: false).UIndex, "Texture")); material.WriteProperty(Input("EmissiveColor", node));
        material.WriteProperty(new EnumProperty(blend, "EBlendMode", MEGame.LE3, "BlendMode")); material.WriteProperty(new BoolProperty(effect, "bUsedWithMaterialEffect")); material.WriteProperty(new BoolProperty(decal, "bUsedWithDecals"));
        var result = Compiler(material).TranslateMaterialResourceHlsl(Template()); Assert.AreEqual(success, result.Success, string.Join("; ", result.Errors));
        Assert.IsTrue(result.UsesSceneDepth); Assert.IsTrue(result.UsesSceneColor);
        if (!success) StringAssert.Contains(string.Join("; ", result.Errors), decal ? "Materials used with decals must not read from scene color." : "Only transparent materials can read from scene depth.");
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void VertexGatePreservesMissingTextureAndExplicitCoordinateOrder(bool texturePresent, bool coordinatesPresent)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        if (texturePresent) node.WriteProperty(new ObjectProperty(package.CreateExport("Texture", "Texture2D", indexed: false).UIndex, "Texture"));
        if (coordinatesPresent) node.WriteProperty(Input("Coordinates", package.CreateExport("Clock", "MaterialExpressionTime", material, indexed: false)));
        material.WriteProperty(Input("WorldPositionOffset", node)); int reads = 0;
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => { reads++; return new(TextureDimension.Texture2D); }).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsFalse(result.UsesSceneDepth); Assert.IsFalse(result.UsesSceneColor); Assert.AreEqual(texturePresent ? 1 : 0, reads);
        Assert.AreEqual(texturePresent && coordinatesPresent, result.UsesTime); Assert.AreEqual(texturePresent && coordinatesPresent, result.ForcesFloatTexCoords);
        StringAssert.Contains(string.Join("; ", result.Errors), texturePresent ? "Invalid node used in vertex shader input!" : "TextureSample> Missing input texture");
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CubeSamplingRequiresExplicitFloat3Coordinates(bool connected)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        node.WriteProperty(new ObjectProperty(package.CreateExport("Cube", "TextureCube", indexed: false).UIndex, "Texture"));
        if (connected) node.WriteProperty(Input("Coordinates", package.CreateExport("Coordinates", "MaterialExpressionWorldPosition", material, indexed: false)));
        material.WriteProperty(Input("EmissiveColor", node)); var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => new(TextureDimension.TextureCube)).TranslateMaterialHlsl(Template());
        Assert.AreEqual(connected, result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(connected, result.UsesSceneDepth); Assert.AreEqual(connected, result.UsesSceneColor);
        if (connected) { StringAssert.Contains(result.PropertyCode["EmissiveColor"], "texCUBE(TextureCube_0,Parameters.WorldPosition)"); Assert.AreEqual(1, result.UniformCubeTextureExpressions.Count); }
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("dimension")]
    [DataRow("compression")]
    [DataRow("coordinates")]
    [DataRow("cycle")]
    public void InvalidInputsWithholdHlslAndDoNotInventSceneFlags(string failure)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        node.WriteProperty(new ObjectProperty(package.CreateExport("Texture", "Texture2D", indexed: false).UIndex, "Texture"));
        if (failure == "coordinates") node.WriteProperty(Input("Coordinates", package.CreateExport("Coords", "MaterialExpressionConstant4Vector", material, indexed: false)));
        if (failure == "cycle") node.WriteProperty(Input("Bias", node)); material.WriteProperty(Input("EmissiveColor", node)); int reads = 0;
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => { reads++; return failure switch { "missing" => null, "dimension" => new(TextureDimension.TextureCube), "compression" => new(TextureDimension.Texture2D, "TC_#255"), _ => new(TextureDimension.Texture2D) }; }).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.IsFalse(result.UsesSceneDepth); Assert.IsFalse(result.UsesSceneColor); Assert.AreEqual(failure == "cycle" ? 0 : 1, reads);
    }

    [TestMethod]
    [DataRow(1, false)]
    [DataRow(1, true)]
    [DataRow(2, false)]
    [DataRow(2, true)]
    [DataRow(3, false)]
    [DataRow(3, true)]
    [DataRow(4, false)]
    [DataRow(4, true)]
    public void TwoDimensionalCoordinatesUseNativeScalarReplicationAndWidthCoercion(int width, bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        node.WriteProperty(new ObjectProperty(package.CreateExport("Texture", "Texture2D", indexed: false).UIndex, "Texture"));
        var uv = package.CreateExport("UV", width == 1 ? "MaterialExpressionConstant" : $"MaterialExpressionConstant{width}Vector", material, indexed: false);
        node.WriteProperty(Input("Coordinates", uv)); material.WriteProperty(Input("EmissiveColor", node)); var result = Compiler(material).TranslateMaterialHlsl(Template(), half);
        bool valid = width <= 2;
        Assert.AreEqual(valid, result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(valid, result.UsesSceneDepth); Assert.AreEqual(valid, result.UsesSceneColor);
        Assert.AreEqual(0u, result.NumUserTexCoords); Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        string coordinates = width == 1 ? "float2(float(0.00000000),float(0.00000000))" : "float2(0.00000000,0.00000000)";
        if (valid) StringAssert.Contains(result.PropertyCode["EmissiveColor"], $"{(half ? "h4tex2D" : "tex2D")}(Texture2D_0,{coordinates})");
        else Assert.IsNull(result.MaterialHlsl);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NestedCoordinateSampleGetsEarlierSamplerAndEarlierGlobalTextureIndexInAccessOrder(bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        var outer = package.CreateExport("OuterTexture", "Texture2D", indexed: false); var inner = package.CreateExport("InnerTexture", "Texture2D", indexed: false);
        node.WriteProperty(new ObjectProperty(outer.UIndex, "Texture")); var child = package.CreateExport("Coordinates", "MaterialExpressionTextureSample", material, indexed: false); child.WriteProperty(new ObjectProperty(inner.UIndex, "Texture"));
        var uv = Input("Coordinates", child); uv.Properties.Add(new IntProperty(1, "Mask")); uv.Properties.Add(new IntProperty(1, "MaskR")); uv.Properties.Add(new IntProperty(1, "MaskG")); node.WriteProperty(uv); material.WriteProperty(Input("EmissiveColor", node));
        var reads = new System.Collections.Generic.List<int>(); var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: entry => { reads.Add(entry.UIndex); return new(TextureDimension.Texture2D); }).TranslateMaterialHlsl(Template(), half); Success(result);
        CollectionAssert.AreEqual(new[] { outer.UIndex, inner.UIndex }, reads); CollectionAssert.AreEqual(new[] { inner.UIndex, outer.UIndex }, result.UniformExpressionTextures.ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, result.Uniform2DTextureExpressions.Select(x => x.TextureIndex).ToArray());
        string function = half ? "h4tex2D" : "tex2D"; StringAssert.Contains(result.PropertyCode["EmissiveColor"], function + "(Texture2D_1,Local3.rg)");
        Assert.AreEqual(1u, result.NumUserTexCoords);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MissingPixelTextureIsReportedBeforeMetadataOrComposition(bool invalidReference)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var node = Node(package, material);
        if (invalidReference) node.WriteProperty(new ObjectProperty(99999, "Texture")); material.WriteProperty(Input("EmissiveColor", node));
        var result = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => throw new InvalidOperationException("No metadata expected.")).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl); Assert.IsFalse(result.UsesSceneDepth); Assert.IsFalse(result.UsesSceneColor);
        CollectionAssert.AreEqual(new[] { "TextureSample> Missing input texture" }, result.Errors.ToArray());
    }

    [TestMethod]
    public void StaticSelectionAndSharedSamplerIdentityResetBetweenTranslations()
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package); var texture = package.CreateExport("Texture", "Texture2D", indexed: false);
        var node = Node(package, material); node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        var ordinary = package.CreateExport("Ordinary", "MaterialExpressionTextureSample", material, indexed: false); ordinary.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        var branch = package.CreateExport("Switch", "MaterialExpressionStaticSwitchParameter", material, indexed: false);
        branch.WriteProperty(Input("A", node)); branch.WriteProperty(Input("B", ordinary)); branch.WriteProperty(new BoolProperty(true, "DefaultValue")); material.WriteProperty(Input("EmissiveColor", branch));
        material.WriteProperty(Input("DiffuseColor", ordinary)); int reads = 0; var compiler = new LE3MaterialShaderCompiler(material, getTextureMetadata: _ => { reads++; return new(TextureDimension.Texture2D); });
        var selected = compiler.TranslateMaterialHlsl(Template()); Success(selected); Assert.IsTrue(selected.UsesSceneDepth); Assert.AreEqual(1, reads); Assert.AreEqual(1, selected.Uniform2DTextureExpressions.Count);
        branch.WriteProperty(new BoolProperty(false, "DefaultValue")); var inactive = compiler.TranslateMaterialHlsl(Template()); Success(inactive); Assert.IsFalse(inactive.UsesSceneDepth); Assert.IsFalse(inactive.UsesSceneColor); Assert.AreEqual(2, reads);
        Assert.AreEqual(1, inactive.Uniform2DTextureExpressions.Count); Assert.IsFalse(inactive.MaterialHlsl.Contains("PreviousDepth"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExplicitFloat1ClampAndLerpKeepNativeType(bool half)
    {
        GlobalTest.Init(); using var package = Package(); var material = Material(package);
        var pixel = package.CreateExport("Pixel", "MaterialExpressionPixelDepth", material, indexed: false); var clamp = package.CreateExport("Clamp", "MaterialExpressionClamp", material, indexed: false);
        clamp.WriteProperty(Input("Input", pixel));
        var min = package.CreateExport("Min", "MaterialExpressionConstant", material, indexed: false);
        var max = package.CreateExport("Max", "MaterialExpressionConstant", material, indexed: false); max.WriteProperty(new FloatProperty(1f, "R"));
        clamp.WriteProperty(Input("Min", min)); clamp.WriteProperty(Input("Max", max));
        var lerp = package.CreateExport("Lerp", "MaterialExpressionLinearInterpolate", material, indexed: false);
        var color = package.CreateExport("Color", "MaterialExpressionConstant3Vector", material, indexed: false); lerp.WriteProperty(Input("A", color)); lerp.WriteProperty(Input("B", color)); lerp.WriteProperty(Input("Alpha", clamp)); material.WriteProperty(Input("EmissiveColor", lerp));
        var result = Compiler(material).TranslateMaterialHlsl(Template(), half); Success(result); string t = half ? "half" : "float";
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], $"min(max(Parameters.ScreenPosition.w,{t}({t}(0.00000000))),{t}({t}(1.00000000)))");
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], ",Local0)"); Assert.IsFalse(result.PropertyCode["EmissiveColor"].Contains($"{t}(Local0)"));
        var multiply = package.CreateExport("Multiply", "MaterialExpressionMultiply", material, indexed: false); multiply.WriteProperty(Input("A", clamp)); multiply.WriteProperty(Input("B", color)); material.WriteProperty(Input("EmissiveColor", multiply));
        result = Compiler(material).TranslateMaterialHlsl(Template(), half); Assert.IsFalse(result.Success); StringAssert.Contains(string.Join("; ", result.Errors), "native numeric types 0x1 and 0x4");
    }

    private static IMEPackage Package() => MEPackageHandler.CreateMemoryEmptyPackage("LE3DepthBiasBlend.pcc", MEGame.LE3);
    private static ExportEntry Material(IMEPackage package) => package.CreateExport("Material", "Material", indexed: false);
    private static ExportEntry Node(IMEPackage package, ExportEntry material) => package.CreateExport("DepthBias", "MaterialExpressionDepthBiasBlend", material, indexed: false);
    private static StructProperty Input(string name, ExportEntry node) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(node.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static LE3MaterialShaderCompiler Compiler(ExportEntry material) => new(material, getTextureMetadata: _ => new(TextureDimension.Texture2D));
    private static void Success(TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
}
