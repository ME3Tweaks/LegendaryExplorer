using System;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3BioKeyedFlipbookTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DefaultFlipbookUsesShippingGridAndNativeExpressionOrder(bool half)
    {
        using var package = Package(); var material = Material(package); var flipbook = Flipbook(package, material, Time(package, material));
        material.WriteProperty(Input("EmissiveColor", flipbook));
        var result = Compile(material, _ => new(TextureDimension.Texture2D), half); Success(result);
        string code = result.PropertyCode["EmissiveColor"], type = half ? "half" : "float";
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        Assert.IsInstanceOfType<MaterialUniformExpressionTexture>(result.Uniform2DTextureExpressions.Single());
        StringAssert.Contains(code, type + "2 Local");
        StringAssert.Contains(code, "Parameters.TexCoords[0].xy");
        StringAssert.Contains(code, "fmod(");
        StringAssert.Contains(code, half ? "h4tex2D(Texture2D_0," : "tex2D(Texture2D_0,");
        Assert.IsFalse(code.Contains("Unpack", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("BiasNormalizeNormalMap", StringComparison.Ordinal));
        Assert.AreEqual(1u, result.NumUserTexCoords);
        Assert.AreEqual(1, result.UniformPixelScalarExpressions.Count);
    }

    [TestMethod]
    [DataRow(2)] [DataRow(3)] [DataRow(4)]
    public void AuthoredCoordinateForceCastsEveryVectorWidthToFloat2(int width)
    {
        using var package = Package(); var material = Material(package); var coord = Vector(package, material, width);
        var flipbook = Flipbook(package, material, Time(package, material), coord); material.WriteProperty(Input("Opacity", flipbook));
        var result = Compile(material, _ => new(TextureDimension.Texture2D)); Success(result);
        StringAssert.Contains(result.PropertyCode["Opacity"], "float2(");
    }

    [TestMethod]
    [DataRow("MaterialExpressionConstant", false)]
    [DataRow("MaterialExpressionScalarParameter", true)]
    [DataRow("MaterialExpressionTime", true)]
    public void GenericScalarTimePreservesOriginalUniformAccess(string kind, bool packed)
    {
        using var package = Package(); var material = Material(package);
        var time = package.CreateExport("ScalarTime", kind, material, indexed: false);
        if (kind == "MaterialExpressionConstant") time.WriteProperty(new FloatProperty(.5f, "R"));
        if (kind == "MaterialExpressionScalarParameter")
        {
            time.WriteProperty(new NameProperty("TimeValue", "ParameterName"));
            time.WriteProperty(new FloatProperty(.5f, "DefaultValue"));
        }
        var flipbook = Flipbook(package, material, time); material.WriteProperty(Input("Opacity", flipbook));
        var result = Compile(material, _ => new(TextureDimension.Texture2D)); Success(result);
        StringAssert.Contains(result.PropertyCode["Opacity"], "fmod(");
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(packed ? 1 : 0, result.UniformPixelScalarExpressions.Count);
        Assert.IsFalse(result.UniformPixelScalarExpressions.Any(p => p is MaterialUniformExpressionBIOMod or MaterialUniformExpressionFoldedMath or MaterialUniformExpressionFloor));
    }

    [TestMethod]
    [DataRow(2)] [DataRow(3)] [DataRow(4)]
    public void ExplicitVectorTimeForceCastTruncatesToFloat1AndSurvivesIndexArithmetic(int width)
    {
        using var package = Package(); var material = Material(package); var time = Vector(package, material, width);
        var flipbook = Flipbook(package, material, time);
        material.WriteProperty(Input("Opacity", flipbook));
        var result = Compile(material, _ => new(TextureDimension.Texture2D)); Success(result);
        string code = result.PropertyCode["Opacity"];
        StringAssert.Contains(code, "floor("); StringAssert.Contains(code, "fmod(");
        StringAssert.Contains(code, "float(float(1.00000000))");
    }

    [TestMethod]
    public void MissingTimePrecheckPrecedesMissingTextureAndDoesNotVisitOtherInput()
    {
        using var package = Package(); var material = Material(package); var flipbook = Flipbook(package, material, null, includeTexture: false);
        flipbook.WriteProperty(Input("Coord", package.CreateExport("Unsupported", "MaterialExpressionComment", material, indexed: false)));
        material.WriteProperty(Input("Opacity", flipbook));
        var result = Compile(material, _ => throw new AssertFailedException("Metadata must not be requested."));
        FailedWith(result, "Missing KeyedFlipbook input Time"); Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    public void MissingTexturePrecheckPrecedesCoordinateAndTimeVisits()
    {
        using var package = Package(); var material = Material(package); var flipbook = Flipbook(package, material,
            package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false), includeTexture: false);
        flipbook.WriteProperty(Input("Coord", package.CreateExport("Unsupported", "MaterialExpressionComment", material, indexed: false)));
        material.WriteProperty(Input("Opacity", flipbook));
        var result = Compile(material, _ => throw new AssertFailedException("Metadata must not be requested."));
        FailedWith(result, "Missing KeyedFlipbook Texture"); Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    public void UnsampledTextureGetsNoGlobalIndexWhenAuthoredCoordinateFails()
    {
        using var package = Package(); var material = Material(package); var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        var coord = package.CreateExport("UnsupportedCoord", "MaterialExpressionComment", material, indexed: false);
        var flipbook = Flipbook(package, material, time, coord); material.WriteProperty(Input("Opacity", flipbook));
        var result = Compile(material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(result.Success); Assert.IsTrue(result.UsesTime);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count);
    }

    [TestMethod]
    public void DefaultCoordinateVertexGuardPrecedesTimeVisitButTimeRootIsStillVisited()
    {
        using var package = Package(); var material = Material(package); var time = package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
        var flipbook = Flipbook(package, material, time); material.WriteProperty(Input("WorldPositionOffset", flipbook));
        var result = Compile(material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(result.Success); Assert.IsTrue(result.UsesTime);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        StringAssert.Contains(string.Join("; ", result.Errors), "Invalid node used in vertex shader input!");
    }

    [TestMethod]
    public void StageTextureReferenceRejectsVertexPropertyWithoutRegisteringTexture()
    {
        using var package = Package(); var material = Material(package); var flipbook = Flipbook(package, material, Time(package, material));
        material.WriteProperty(Input("WorldPositionOffset", flipbook));
        var result = Compile(material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(result.Success); Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        StringAssert.Contains(string.Join("; ", result.Errors), "Invalid node used in vertex shader input!");
    }

    [TestMethod]
    [DataRow(-3, 2)] [DataRow(3, -2)] [DataRow(-3, -2)]
    [DataRow(46341, 46341)] [DataRow(int.MinValue, 1)] [DataRow(int.MaxValue, -1)]
    public void GridDimensionsArePassedThroughWithoutValidation(int columns, int rows)
    {
        using var package = Package(); var material = Material(package); var flipbook = Flipbook(package, material, Time(package, material));
        flipbook.WriteProperty(new IntProperty(columns, "FlipTexCols")); flipbook.WriteProperty(new IntProperty(rows, "FlipTexRows"));
        material.WriteProperty(Input("Opacity", flipbook));
        var result = Compile(material, _ => new(TextureDimension.Texture2D)); Success(result);
        StringAssert.Contains(result.PropertyCode["Opacity"], "fmod(");
    }

    [TestMethod]
    public void ProviderMetadataForImportedTextureBypassesDonorResolution()
    {
        using var package = Package(); var material = Material(package); var flipbook = package.CreateExport("Flip", "BioMaterialExpressionKeyedFlipbook", material, indexed: false);
        flipbook.WriteProperty(Input("Time", Time(package, material)));
        var import = new ImportEntry(package, 0, "DonorFlip") { ClassName = "Texture2D", PackageFile = "DonorPackage" }; package.AddImport(import);
        flipbook.WriteProperty(new ObjectProperty(import.UIndex, "FlipTex")); material.WriteProperty(Input("Opacity", flipbook)); int reads = 0;
        var result = new LE3MaterialShaderCompiler(material,
            resolveTextureImport: _ => throw new AssertFailedException("Provider metadata must bypass donor resolution."),
            getTextureMetadata: entry => { reads++; Assert.AreSame(import, entry); return new(TextureDimension.Texture2D); }).TranslateMaterialHlsl(Template());
        Success(result); Assert.AreEqual(1, reads); CollectionAssert.Contains(result.UniformExpressionTextures.ToArray(), import.UIndex);
    }

    [TestMethod]
    [DataRow("TextureCube")] [DataRow("TextureRenderTarget2D")] [DataRow("Texture")]
    public void NonTexture2DFlipTexReferenceFailsBeforeMetadata(string textureClass)
    {
        using var package = Package(); var material = Material(package); var flipbook = Flipbook(package, material, Time(package, material));
        flipbook.WriteProperty(new ObjectProperty(package.CreateExport("WrongTexture", textureClass, indexed: false).UIndex, "FlipTex"));
        material.WriteProperty(Input("Opacity", flipbook));
        var result = Compile(material, _ => throw new AssertFailedException("Wrong-class texture must fail before metadata."));
        FailedWith(result, ".FlipTex requires a Texture2D reference.");
    }

    [TestMethod]
    public void BIOModUsesSignedCpuRemainderAndEvaluatesAThenB()
    {
        var calls = new System.Collections.Generic.List<string>();
        var mod = new MaterialUniformExpressionBIOMod
        {
            ExpressionType = "FMaterialUniformExpressionBIOMod",
            A = new ProbeUniform("A", new(-7.5f, float.NaN, 5f, 8f), calls),
            B = new ProbeUniform("B", new(3f, 2f, 0f, 3f), calls)
        };
        var output = LinearColor.Black; mod.GetNumberValue(Context(), ref output);
        CollectionAssert.AreEqual(new[] { "A", "B" }, calls);
        Assert.AreEqual(-1.5f, output.R); Assert.IsTrue(float.IsNaN(output.G));
        Assert.IsTrue(float.IsNaN(output.B)); Assert.AreEqual(2f, output.A);
    }

    [TestMethod]
    public void OrdinaryFmodAndBIOModHaveDistinctTypesAndExpressionNames()
    {
        Assert.AreNotEqual(typeof(MaterialUniformExpressionFmod), typeof(MaterialUniformExpressionBIOMod));
        Assert.AreNotEqual("FMaterialUniformExpressionFmod", new MaterialUniformExpressionBIOMod { ExpressionType = "FMaterialUniformExpressionBIOMod" }.ExpressionType.Name);
    }

    [TestMethod]
    public void BIOModRoundTripRetainsDistinctTypeAndOrderedChildren()
    {
        using var package = Package();
        var expression = new MaterialUniformExpressionBIOMod
        {
            ExpressionType = "FMaterialUniformExpressionBIOMod",
            A = new MaterialUniformExpressionConstant { ExpressionType = "FMaterialUniformExpressionConstant", ValueType = 8, Value = new(-7.5f, 5f, 11f, 8f) },
            B = new MaterialUniformExpressionConstant { ExpressionType = "FMaterialUniformExpressionConstant", ValueType = 8, Value = new(3f, 2f, 4f, 3f) }
        };
        using var stream = new MemoryStream();
        expression.Serialize(new SerializingContainer(stream, package));
        stream.Position = 0;
        var reader = new SerializingContainer(stream, package, isLoading: true);
        var restored = MaterialUniformExpression.Create(reader);
        restored.Serialize(reader);
        Assert.AreEqual(stream.Length, stream.Position);
        Assert.IsInstanceOfType<MaterialUniformExpressionBIOMod>(restored);
        var binary = (MaterialUniformExpressionBIOMod)restored;
        Assert.AreEqual(((MaterialUniformExpressionConstant)expression.A).Value, ((MaterialUniformExpressionConstant)binary.A).Value);
        Assert.AreEqual(((MaterialUniformExpressionConstant)expression.B).Value, ((MaterialUniformExpressionConstant)binary.B).Value);
        var expected = LinearColor.Black; expression.GetNumberValue(Context(), ref expected);
        var actual = LinearColor.Black; restored.GetNumberValue(Context(), ref actual);
        Assert.AreEqual(expected, actual);
    }

    private static IMEPackage Package()
    {
        GlobalTest.Init();
        return MEPackageHandler.CreateMemoryEmptyPackage("LE3BioKeyedFlipbook.pcc", MEGame.LE3);
    }

    private static ExportEntry Material(IMEPackage package) => package.CreateExport("Material", "Material", indexed: false);
    private static ExportEntry Flipbook(IMEPackage package, ExportEntry material, ExportEntry time, ExportEntry coord = null, bool includeTexture = true)
    {
        var node = package.CreateExport("KeyedFlipbook", "BioMaterialExpressionKeyedFlipbook", material, indexed: false);
        if (time != null) node.WriteProperty(Input("Time", time));
        if (coord != null) node.WriteProperty(Input("Coord", coord));
        if (includeTexture) node.WriteProperty(new ObjectProperty(package.CreateExport("FlipTex", "Texture2D", indexed: false).UIndex, "FlipTex"));
        return node;
    }

    private static ExportEntry Time(IMEPackage package, ExportEntry material) => package.CreateExport("Time", "MaterialExpressionTime", material, indexed: false);
    private static ExportEntry Vector(IMEPackage package, ExportEntry material, int width)
    {
        var node = package.CreateExport("Vector" + width, $"MaterialExpressionConstant{width}Vector", material, indexed: false);
        node.WriteProperty(new FloatProperty(1f, "R")); node.WriteProperty(new FloatProperty(2f, "G"));
        if (width > 2) node.WriteProperty(new FloatProperty(3f, "B"));
        if (width > 3) node.WriteProperty(new FloatProperty(4f, "A"));
        return node;
    }

    private static StructProperty Input(string name, ExportEntry expression) =>
        new("ExpressionInput", new PropertyCollection { new ObjectProperty(expression?.UIndex ?? 0, "Expression") }, name);

    private static TranslationResult Compile(ExportEntry material, Func<IEntry, TextureCompilationMetadata> metadata, bool half = false) =>
        new LE3MaterialShaderCompiler(material, getTextureMetadata: metadata).TranslateMaterialHlsl(Template(), half);

    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static UniformExpressionRenderContext Context() => new(new(), new(), 0f, 0f, (_, _) => LinearColor.Black);
    private static void Success(TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
    private static void FailedWith(TranslationResult result, string message)
    {
        Assert.IsFalse(result.Success); Assert.IsNull(result.MaterialHlsl);
        StringAssert.Contains(string.Join("; ", result.Errors), message);
    }

    private sealed class ProbeUniform(string name, LinearColor value, System.Collections.Generic.List<string> calls) : MaterialUniformExpression
    {
        public override bool IsNotFrameDependent => true;
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            calls.Add(name); outVal = value;
        }
    }
}
