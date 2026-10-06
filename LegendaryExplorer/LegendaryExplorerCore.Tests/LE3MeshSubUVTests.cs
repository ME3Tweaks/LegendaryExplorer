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
public class LE3MeshSubUVTests
{
    [TestMethod]
    [DataRow("MaterialExpressionMeshSubUV")]
    [DataRow("MaterialExpressionTextureSampleParameterMeshSubUV")]
    public void SingleMeshSubUVPacksFixedParametersAndUsesAuthoredCoordinates(string className)
    {
        using var f = Fixture(className);
        f.Node.WriteProperty(new NameProperty(new NameReference("None", 3), "ParameterName"));
        f.Node.WriteProperty(Input("Coordinates", Constant(f.Package, f.Material, "MaterialExpressionConstant2Vector", "Constant", new Vector2(.25f, .75f))));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(result);
        string code = result.PropertyCode["EmissiveColor"];
        AssertOrdered(code, " * ", " + ", "tex2D(");
        Assert.AreEqual(0u, result.NumUserTexCoords);
        Assert.AreEqual(2, result.UniformPixelVectorExpressions.Count);
        var vectorParameters = VectorParameters(result);
        Assert.AreEqual(new NameReference("TextureOffsetParameter"), vectorParameters[0].ParameterName);
        Assert.AreEqual(new NameReference("TextureScaleParameter"), vectorParameters[1].ParameterName);
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(className.Contains("Parameter", StringComparison.Ordinal), result.Uniform2DTextureExpressions.Single() is MaterialUniformExpressionTextureParameter);
    }

    [TestMethod]
    [DataRow("MaterialExpressionMeshSubUV")]
    [DataRow("MaterialExpressionTextureSampleParameterMeshSubUV")]
    public void SingleMeshSubUVDefaultsToPixelCoordinateZeroAndUsesRawTcWithoutAnExpression(string className)
    {
        using var f = Fixture(className);
        f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(result);
        string code = result.PropertyCode["Opacity"];
        StringAssert.Contains(code, "Parameters.TexCoords[0]");
        Assert.IsFalse(code.Contains("TextureCoordinate", StringComparison.Ordinal));
        Assert.AreEqual(1, Count(code, "Parameters.TexCoords[0]"));
        Assert.AreEqual(1u, result.NumUserTexCoords);
    }

    [TestMethod]
    public void SingleMeshSubUVRejectsVector4AuthoredCoordinatesWithoutTruncation()
    {
        using var f = Fixture("MaterialExpressionMeshSubUV");
        f.Node.WriteProperty(Input("Coordinates", Constant(f.Package, f.Material, "MaterialExpressionConstant4Vector", "Constant", Vector4.One)));
        f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.Errors.Count > 0);
    }

    [TestMethod]
    public void MeshSubUVBlendPacksScaleOffsetThenOffsetOneAndAlphaAfterBothFrames()
    {
        using var f = Fixture("MaterialExpressionMeshSubUVBlend");
        f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(result);
        string code = result.PropertyCode["Opacity"];
        AssertOrdered(code, "Parameters.TexCoords[0]", "tex2D(", "Parameters.TexCoords[0]", "tex2D(", "lerp(");
        Assert.AreEqual(2, Count(code, "tex2D("));
        var parameters = VectorParameters(result);
        Assert.AreEqual(new NameReference("TextureScaleParameter"), parameters[0].ParameterName);
        Assert.AreEqual(new NameReference("TextureOffsetParameter"), parameters[1].ParameterName);
        Assert.AreEqual(new NameReference("TextureOffset1Parameter"), parameters[2].ParameterName);
        Assert.AreEqual(1, parameters.Count(p => p.ParameterName == new NameReference("TextureOffsetParameter")));
        AssertOrdered(code, "tex2D(", "tex2D(", " * float4", " + float4", " * float4", " + float4", "lerp(");
    }

    [TestMethod]
    public void MeshSubUVBlendCompressionNormalsBypassAffineRecoveryEvenForNoncanonicalRanges()
    {
        foreach (var compression in new[] { "TC_Normalmap", "TC_NormalmapAlpha", "TC_NormalmapUncompressed", "TC_NormalmapBC5", "TC_NormalmapBC7" })
        {
            using var f = Fixture("MaterialExpressionMeshSubUVBlend"); f.Material.WriteProperty(Input("Opacity", f.Node));
            var metadata = new TextureCompilationMetadata(TextureDimension.Texture2D, compression, new Vector4(-2, -3, -4, -5), new Vector4(7, 8, 9, 10));
            var result = Compile(f.Material, _ => metadata); Success(result);
            StringAssert.Contains(result.PropertyCode["Opacity"], "BiasNormalizeNormalMap");
            Assert.IsFalse(result.PropertyCode["Opacity"].Contains("9.00000000,11.00000000,13.00000000,15.00000000", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    [DataRow("TC_Default")]
    [DataRow("TC_NormalmapHQ")]
    public void MeshSubUVBlendDefaultAndHighQualityNormalUseAffineUnpack(string compression)
    {
        using var f = Fixture("MaterialExpressionMeshSubUVBlend"); f.Material.WriteProperty(Input("Opacity", f.Node));
        var metadata = new TextureCompilationMetadata(TextureDimension.Texture2D, compression, new Vector4(-2, -3, -4, -5), new Vector4(7, 8, 9, 10));
        var result = Compile(f.Material, _ => metadata); Success(result);
        Assert.AreEqual(2, Count(result.PropertyCode["Opacity"], "9.00000000,11.00000000,13.00000000,15.00000000"));
    }

    [TestMethod]
    [DataRow("MaterialExpressionMeshSubUV", false)]
    [DataRow("MaterialExpressionTextureSampleParameterMeshSubUV", true)]
    [DataRow("MaterialExpressionMeshSubUVBlend", false)]
    [DataRow("MaterialExpressionTextureSampleParameterMeshSubUVBlend", true)]
    public void MeshSubUVMissingTextureFailsBeforeCoordinatesOrMetadata(string className, bool parameterized)
    {
        using var f = Fixture(className, includeTexture: false);
        f.Node.WriteProperty(Input("Coordinates", Junk(f.Package, f.Material, "MaterialExpressionTime")));
        f.Material.WriteProperty(Input("Opacity", f.Node)); int reads = 0;
        var result = Compile(f.Material, _ => { reads++; throw new AssertFailedException("Texture metadata must not be queried."); });
        Failed(result, parameterized ? "Requires a valid Texture2D" : "missing texture");
        Assert.AreEqual(0, reads); Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    [DataRow("MaterialExpressionTextureSampleParameterMeshSubUV", "Texture2D", true)]
    [DataRow("MaterialExpressionTextureSampleParameterMeshSubUV", "TextureRenderTarget2D", true)]
    [DataRow("MaterialExpressionTextureSampleParameterMeshSubUV", "TextureFlipBook", false)]
    [DataRow("MaterialExpressionTextureSampleParameterMeshSubUVBlend", "TextureCube", false)]
    [DataRow("MaterialExpressionMeshSubUV", "Texture2D", true)]
    [DataRow("MaterialExpressionMeshSubUV", "TextureRenderTarget2D", true)]
    [DataRow("MaterialExpressionMeshSubUVBlend", "TextureFlipBook", true)]
    public void MeshSubUVValidatesRequiredTextureClasses(string className, string textureClass, bool valid)
    {
        using var f = Fixture(className, textureClass); f.Material.WriteProperty(Input("Opacity", f.Node)); int reads = 0;
        var result = Compile(f.Material, _ => { reads++; return new(TextureDimension.Texture2D); });
        Assert.AreEqual(valid, result.Success, string.Join("; ", result.Errors)); Assert.AreEqual(valid ? 1 : 0, reads);
    }

    [TestMethod]
    public void RenderTargetWithoutProviderFailsClosedInsteadOfReadingItsExport()
    {
        using var f = Fixture("MaterialExpressionMeshSubUV", "TextureRenderTarget2D");
        f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = new LE3MaterialShaderCompiler(f.Material).TranslateMaterialHlsl(Template());
        Failed(result, "supply texture compilation metadata");
        Assert.IsNull(result.MaterialHlsl);
    }

    [TestMethod]
    public void MeshSubUVImportedTextureUsesOwnMetadataWithoutResolvingDonor()
    {
        using var f = Fixture("MaterialExpressionTextureSampleParameterMeshSubUV", includeTexture: false);
        var import = new ImportEntry(f.Package, 0, "Donor") { ClassName = "Texture2D", PackageFile = "DonorPackage" }; f.Package.AddImport(import);
        f.Node.WriteProperty(new ObjectProperty(import.UIndex, "Texture")); f.Material.WriteProperty(Input("Opacity", f.Node)); int reads = 0;
        var compiler = new LE3MaterialShaderCompiler(f.Material,
            resolveTextureImport: _ => throw new AssertFailedException("Donor resolution is out of scope."),
            getTextureMetadata: entry => { reads++; Assert.AreSame(import, entry); return new(TextureDimension.Texture2D); });
        var result = compiler.TranslateMaterialHlsl(Template()); Success(result); Assert.AreEqual(1, reads);
        Assert.AreEqual(import.UIndex, result.UniformExpressionTextures.Single());
    }

    [TestMethod]
    public void MeshSubUVAlwaysUsesTextureParameterAndPreservesNoneNameNumbers()
    {
        using var f = Fixture("MaterialExpressionTextureSampleParameterMeshSubUV", "Texture2D");
        f.Node.WriteProperty(new NameProperty(new NameReference("None", 4), "ParameterName")); f.Material.WriteProperty(Input("Opacity", f.Node));
        var numbered = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(numbered);
        var parameter = numbered.Uniform2DTextureExpressions.Single() as MaterialUniformExpressionTextureParameter;
        Assert.IsNotNull(parameter); Assert.AreEqual(new NameReference("None", 4), parameter.ParameterName);
        f.Node.WriteProperty(new NameProperty(new NameReference("None", 0), "ParameterName"));
        var zeroNone = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(zeroNone);
        var zeroParameter = zeroNone.Uniform2DTextureExpressions.Single() as MaterialUniformExpressionTextureParameter;
        Assert.IsNotNull(zeroParameter); Assert.AreEqual(new NameReference("None", 0), zeroParameter.ParameterName);
    }

    [TestMethod]
    public void MeshSubUVBlendUsesTwoSamplesAndPreservesSecondFrameStagesOnCoordinateFailure()
    {
        using var f = Fixture("MaterialExpressionTextureSampleParameterMeshSubUVBlend", "Texture2D");
        f.Material.WriteProperty(Input("WorldPositionOffset", f.Node));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(result.Success);
        string errors = string.Join("; ", result.Errors);
        Assert.IsTrue(errors.Contains("Invalid node used in vertex shader input!", StringComparison.Ordinal));
        Assert.IsTrue(VectorParameters(result).Any(p => p.ParameterName == new NameReference("TextureOffset1Parameter")));
        Assert.IsTrue(errors.Contains("TextureOffsetParameter", StringComparison.Ordinal) || VectorParameters(result).Any(p => p.ParameterName == new NameReference("TextureOffsetParameter")));
    }

    [TestMethod]
    public void MeshSubUVSelectsCoordinatesOnlyAndRejectsConnectedCycles()
    {
        using var f = Fixture("MaterialExpressionMeshSubUV");
        f.Node.WriteProperty(new StructProperty("Junk", new PropertyCollection { Input("Expression", Junk(f.Package, f.Material, "MaterialExpressionTime")) }, "Unrelated"));
        f.Node.WriteProperty(Input("Coordinates", f.Node)); f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(result.Success);
        Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    public void AuthoredMeshSubUVScaleAndOffsetDoNotReplaceFixedParameters()
    {
        using var f = Fixture("MaterialExpressionMeshSubUV"); f.Material.WriteProperty(Input("Opacity", f.Node));
        f.Node.WriteProperty(new StructProperty("LinearColor", new PropertyCollection { new FloatProperty(8, "R"), new FloatProperty(9, "G"), new FloatProperty(0, "B"), new FloatProperty(1, "A") }, "TextureScale", isImmutable: true));
        f.Node.WriteProperty(new StructProperty("LinearColor", new PropertyCollection { new FloatProperty(3, "R"), new FloatProperty(4, "G"), new FloatProperty(0, "B"), new FloatProperty(1, "A") }, "TextureOffset", isImmutable: true));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(result);
        var parameters = VectorParameters(result);
        Assert.AreEqual(2, parameters.Count);
        Assert.AreEqual(new NameReference("TextureOffsetParameter"), parameters[0].ParameterName);
        Assert.AreEqual(new NameReference("TextureScaleParameter"), parameters[1].ParameterName);
    }

    private sealed class TestFixture : IDisposable
    {
        public IMEPackage Package { get; }
        public ExportEntry Material { get; }
        public ExportEntry Node { get; }
        public TestFixture(IMEPackage package, ExportEntry material, ExportEntry node) => (Package, Material, Node) = (package, material, node);
        public void Dispose() => (Package as IDisposable)?.Dispose();
    }

    private static TestFixture Fixture(string className, string textureClass = null, bool includeTexture = true)
    {
        GlobalTest.Init(); var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3MeshSubUV.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("MeshSubUV", className, material, indexed: false);
        if (includeTexture)
        {
            var texture = package.CreateExport("Texture", textureClass ?? "Texture2D", indexed: false);
            node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        }
        return new(package, material, node);
    }

    private static ExportEntry Junk(IMEPackage package, ExportEntry material, string className) => package.CreateExport("Ignored", className, material, indexed: false);
    private static ExportEntry Constant(IMEPackage package, ExportEntry material, string className, string property, object value)
    {
        var entry = package.CreateExport("Coordinate", className, material, indexed: false);
        if (value is Vector2 v2)
        {
            entry.WriteProperty(new FloatProperty(v2.X, "R"));
            entry.WriteProperty(new FloatProperty(v2.Y, "G"));
        }
        else if (value is Vector4 v4)
        {
            entry.WriteProperty(new FloatProperty(v4.X, "R")); entry.WriteProperty(new FloatProperty(v4.Y, "G"));
            entry.WriteProperty(new FloatProperty(v4.Z, "B")); entry.WriteProperty(new FloatProperty(v4.W, "A"));
        }
        return entry;
    }
    private static StructProperty Input(string name, ExportEntry node) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(node.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static TranslationResult Compile(ExportEntry material, Func<IEntry, TextureCompilationMetadata> metadata) =>
        new LE3MaterialShaderCompiler(material, getTextureMetadata: metadata).TranslateMaterialHlsl(Template());
    private static int Count(string text, string value) => (text.Length - text.Replace(value, "", StringComparison.Ordinal).Length) / value.Length;
    private static System.Collections.Generic.List<MaterialUniformExpressionVectorParameter> VectorParameters(TranslationResult result) =>
        result.UniformPixelVectorExpressions.Concat(result.UniformVertexVectorExpressions).OfType<MaterialUniformExpressionVectorParameter>().ToList();
    private static void AssertOrdered(string text, params string[] tokens)
    {
        int position = -1;
        foreach (var token in tokens) { int next = text.IndexOf(token, position + 1, StringComparison.Ordinal); Assert.IsTrue(next > position, $"Expected '{token}' after offset {position} in: {text}"); position = next; }
    }
    private static void Success(TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
    private static void Failed(TranslationResult result, string message) { Assert.IsFalse(result.Success); StringAssert.Contains(string.Join("; ", result.Errors), message); }
}
