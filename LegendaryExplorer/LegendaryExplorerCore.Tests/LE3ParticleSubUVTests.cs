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
public class LE3ParticleSubUVTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ParticleSubUVUsesNativeSamplesUnpacksAndLerpOrder(bool half)
    {
        var f = Fixture("MaterialExpressionParticleSubUV"); f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D), half); Success(result);
        var code = result.PropertyCode["Opacity"];
        string vectorType = half ? "half4" : "float4";
        AssertOrdered(code, "Parameters.TexCoords[0]", "tex2D(", "Parameters.TexCoords[1]", "tex2D(",
            " * " + vectorType, " + " + vectorType, " * " + vectorType, " + " + vectorType, "Parameters.TexCoords[2]", "lerp(");
        StringAssert.Contains(code, half ? "h4tex2D(" : "tex2D(");
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count); Assert.AreEqual(1, result.UniformExpressionTextures.Count);
        Assert.AreEqual(3u, result.NumUserTexCoords); Assert.IsFalse(result.UsesTime);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformVertexScalarExpressions.Count + result.UniformPixelVectorExpressions.Count);
    }

    [TestMethod]
    public void ParticleSubUVMissingTextureFailsBeforeGraphTraversal()
    {
        var f = Fixture("MaterialExpressionParticleSubUV", includeTexture: false);
        f.Node.WriteProperty(Input("Coordinates", Junk(f.Package, f.Material, "MaterialExpressionTime")));
        f.Material.WriteProperty(Input("Opacity", f.Node)); int reads = 0;
        var result = Compile(f.Material, _ => { reads++; return new(TextureDimension.Texture2D); });
        Failed(result, "Missing ParticleSubUV input texture"); Assert.AreEqual(0, reads); Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    public void ParticleSubUVIgnoresCoordinatesAndInternsOrdinaryTextureOnce()
    {
        var f = Fixture("MaterialExpressionParticleSubUV");
        f.Node.WriteProperty(Input("Coordinates", Junk(f.Package, f.Material, "MaterialExpressionTime")));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node)); f.Material.WriteProperty(Input("Opacity", f.Node)); int reads = 0;
        var result = Compile(f.Material, _ => { reads++; return new(TextureDimension.Texture2D); }); Success(result);
        Assert.AreEqual(1, reads); Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count); Assert.AreEqual(1, result.UniformExpressionTextures.Count);
        Assert.IsFalse(result.UsesTime); Assert.AreEqual(3u, result.NumUserTexCoords);
    }

    [TestMethod]
    public void ParticleSubUVAlwaysEmitsIdentityUnpackForEachFrame()
    {
        var f = Fixture("MaterialExpressionParticleSubUV"); f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(result);
        var code = result.PropertyCode["Opacity"];
        Assert.AreEqual(2, Count(code, " * float4(1.00000000,1.00000000,1.00000000,1.00000000)"));
        Assert.AreEqual(2, Count(code, " + float4(0.00000000,0.00000000,0.00000000,0.00000000)"));
        AssertOrdered(code, " * float4", " + float4", " * float4", " + float4", "Parameters.TexCoords[2]", "lerp(");
    }

    [TestMethod]
    public void ParticleSubUVCoordinatesDoNotCauseCycleOrCoordinateUsage()
    {
        var f = Fixture("MaterialExpressionParticleSubUV"); f.Node.WriteProperty(Input("Coordinates", f.Node));
        f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(result);
        Assert.AreEqual(3u, result.NumUserTexCoords); Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    public void ParameterSubUVUsesTwoIndependentSamplesAndPreservesNumberedNone()
    {
        var f = Fixture("MaterialExpressionTextureSampleParameterSubUV", "Texture2D");
        f.Node.WriteProperty(new NameProperty(new NameReference("None", 3), "ParameterName"));
        f.Material.WriteProperty(Input("Opacity", f.Node)); int reads = 0;
        var result = Compile(f.Material, _ => { reads++; return new(TextureDimension.Texture2D); }); Success(result);
        var code = result.PropertyCode["Opacity"];
        AssertOrdered(code, "Parameters.TexCoords[0]", "tex2D(", "Parameters.TexCoords[0]", "tex2D(",
            " * float4", " + float4", " * float4", " + float4", "Parameters.TexCoords[2]", "lerp(");
        Assert.AreEqual(2, Count(code, "tex2D(")); Assert.AreEqual(1, reads);
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        var parameter = result.Uniform2DTextureExpressions.Single() as MaterialUniformExpressionTextureParameter;
        Assert.IsNotNull(parameter); Assert.AreEqual(new NameReference("None", 3), parameter.ParameterName);
        Assert.AreEqual(3u, result.NumUserTexCoords);
    }

    [TestMethod]
    public void ParameterSubUVNoneZeroRemainsNamedTextureUniform()
    {
        var f = Fixture("MaterialExpressionTextureSampleParameterSubUV", "Texture2D");
        f.Node.WriteProperty(new NameProperty(new NameReference("None", 0), "ParameterName")); f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(result);
        var parameter = (MaterialUniformExpressionTextureParameter)result.Uniform2DTextureExpressions.Single();
        Assert.AreEqual(NameReference.None, parameter.ParameterName);
    }

    [TestMethod]
    [DataRow("Texture2D", true)]
    [DataRow("TextureRenderTarget2D", true)]
    [DataRow("TextureFlipBook", false)]
    [DataRow("TextureCube", false)]
    [DataRow("Texture", false)]
    public void ParameterSubUVValidatesNativeTextureClasses(string textureClass, bool valid)
    {
        var f = Fixture("MaterialExpressionTextureSampleParameterSubUV", textureClass); f.Material.WriteProperty(Input("Opacity", f.Node)); int reads = 0;
        var result = Compile(f.Material, _ => { reads++; return new(TextureDimension.Texture2D); });
        Assert.AreEqual(valid, result.Success, string.Join("; ", result.Errors));
        Assert.AreEqual(valid ? 1 : 0, reads);
        if (!valid) Failed(result, "ParameterSubUV> Requires a valid Texture2D");
    }

    [TestMethod]
    public void ParameterSubUVMissingTextureUsesNativeError()
    {
        var f = Fixture("MaterialExpressionTextureSampleParameterSubUV", includeTexture: false); f.Material.WriteProperty(Input("Opacity", f.Node));
        Failed(Compile(f.Material, _ => throw new AssertFailedException("No metadata expected.")), "ParameterSubUV> Requires a valid Texture2D");
    }

    [TestMethod]
    public void ImportedSubUVTextureMetadataDoesNotResolveDonor()
    {
        var f = Fixture("MaterialExpressionTextureSampleParameterSubUV", includeTexture: false);
        var import = new ImportEntry(f.Package, 0, "Donor") { ClassName = "Texture2D", PackageFile = "DonorPackage" }; f.Package.AddImport(import);
        f.Node.WriteProperty(new ObjectProperty(import.UIndex, "Texture")); f.Material.WriteProperty(Input("Opacity", f.Node)); int reads = 0;
        var compiler = new LE3MaterialShaderCompiler(f.Material,
            resolveTextureImport: _ => throw new AssertFailedException("Provider metadata must bypass donor resolution."),
            getTextureMetadata: entry => { reads++; Assert.AreSame(import, entry); return new(TextureDimension.Texture2D); });
        var result = compiler.TranslateMaterialHlsl(Template()); Success(result); Assert.AreEqual(1, reads);
        Assert.AreEqual(import.UIndex, result.UniformExpressionTextures.Single());
    }

    [TestMethod]
    public void ParameterSubUVNormalMetadataAppliesCanonicalBiasAndScale()
    {
        var f = Fixture("MaterialExpressionTextureSampleParameterSubUV", "Texture2D"); f.Material.WriteProperty(Input("Opacity", f.Node));
        var metadata = new TextureCompilationMetadata(TextureDimension.Texture2D, "TC_Normalmap", new Vector4(-1, -1, -1, 0), new Vector4(1, 1, 1, 1));
        var result = Compile(f.Material, _ => metadata); Success(result);
        StringAssert.Contains(result.PropertyCode["Opacity"], "BiasNormalizeNormalMap");
    }

    [TestMethod]
    public void SubUVNoncanonicalUnpackMetadataUsesAffineRangesAndUnknownCompressionFailsClosed()
    {
        var f = Fixture("MaterialExpressionParticleSubUV"); f.Material.WriteProperty(Input("Opacity", f.Node));
        var ranges = new TextureCompilationMetadata(TextureDimension.Texture2D, "TC_Default", new Vector4(-2, -3, -4, -5), new Vector4(7, 8, 9, 10));
        var ranged = Compile(f.Material, _ => ranges); Success(ranged);
        StringAssert.Contains(ranged.PropertyCode["Opacity"], "-2.00000000");
        StringAssert.Contains(ranged.PropertyCode["Opacity"], "9.00000000,11.00000000,13.00000000,15.00000000");

        var unsupported = new TextureCompilationMetadata(TextureDimension.Texture2D, "TC_NotASupportedCompression", new Vector4(-1), Vector4.One);
        var rejected = Compile(f.Material, _ => unsupported);
        Assert.IsFalse(rejected.Success); Assert.IsNull(rejected.MaterialHlsl);
    }

    [TestMethod]
    public void AuthoredCoordinatesDoNotTraverseUnknownGraphNodes()
    {
        var f = Fixture("MaterialExpressionParticleSubUV");
        f.Node.WriteProperty(Input("Coordinates", Junk(f.Package, f.Material, "MaterialExpressionComment")));
        f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(result);
        Assert.AreEqual(3u, result.NumUserTexCoords); Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SubUVTextureUnpackSupportsIdentityAndHalfPrecision(bool half)
    {
        var f = Fixture("MaterialExpressionTextureSampleParameterSubUV", "Texture2D"); f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D), half); Success(result);
        StringAssert.Contains(result.PropertyCode["Opacity"], half ? "h4tex2D(" : "tex2D(");
        string vectorType = half ? "half4" : "float4";
        AssertOrdered(result.PropertyCode["Opacity"], " * " + vectorType, " + " + vectorType,
            " * " + vectorType, " + " + vectorType, "Parameters.TexCoords[2]");
    }

    [TestMethod]
    public void SubUVDefaultCoordinatesArePixelOnlyAndDecalCoordinatesRemapBeforeNativeGuard()
    {
        var vertex = Fixture("MaterialExpressionParticleSubUV"); vertex.Material.WriteProperty(Input("WorldPositionOffset", vertex.Node));
        var vertexResult = Compile(vertex.Material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(vertexResult.Success); StringAssert.Contains(string.Join("; ", vertexResult.Errors), "Invalid node used in vertex shader input!");
        Assert.AreEqual(0, vertexResult.Uniform2DTextureExpressions.Count);

        var decal = Fixture("MaterialExpressionParticleSubUV"); decal.Material.WriteProperty(new BoolProperty(true, "bUsedWithDecals"));
        decal.Material.WriteProperty(Input("Opacity", decal.Node));
        var decalResult = Compile(decal.Material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(decalResult.Success); Assert.IsTrue(decalResult.Errors.Count > 0);
        Assert.AreEqual(3u, decalResult.NumUserTexCoords);
    }

    [TestMethod]
    public void ParameterSubUVVertexGuardsContinueThroughTextureCoordinatesAndBothSamples()
    {
        var f = Fixture("MaterialExpressionTextureSampleParameterSubUV", "Texture2D");
        f.Material.WriteProperty(Input("WorldPositionOffset", f.Node));
        var result = Compile(f.Material, _ => new(TextureDimension.Texture2D));
        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        Assert.AreEqual(0u, result.NumUserTexCoords);
        var guard = "Invalid node used in vertex shader input!";
        Assert.IsTrue(result.Errors.Count(error => error.Contains(guard, StringComparison.Ordinal)) >= 6,
            "The texture guard, three coordinate guards, and both independent sample guards must all be reported.");
    }

    [TestMethod]
    public void AuthoredUVAndBlendPropertiesDoNotChangeNativeSubUVCode()
    {
        var f = Fixture("MaterialExpressionParticleSubUV"); f.Material.WriteProperty(Input("Opacity", f.Node));
        var baseline = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(baseline);
        f.Node.WriteProperty(new BoolProperty(true, "bBlend")); f.Node.WriteProperty(new IntProperty(2, "Coordinates"));
        f.Node.WriteProperty(new IntProperty(7, "TextureCoordinateIndex"));
        var authored = Compile(f.Material, _ => new(TextureDimension.Texture2D)); Success(authored);
        Assert.AreEqual(baseline.PropertyCode["Opacity"], authored.PropertyCode["Opacity"]);
    }

    private sealed record TestFixture(IMEPackage Package, ExportEntry Material, ExportEntry Node);

    private static TestFixture Fixture(string className, string textureClass = null, bool includeTexture = true)
    {
        GlobalTest.Init(); var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3ParticleSubUV.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("SubUV", className, material, indexed: false);
        if (includeTexture)
        {
            var texture = package.CreateExport("Texture", textureClass ?? "Texture2D", indexed: false);
            node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        }
        return new(package, material, node);
    }

    private static ExportEntry Junk(IMEPackage package, ExportEntry material, string className) => package.CreateExport("Ignored", className, material, indexed: false);
    private static StructProperty Input(string name, ExportEntry node) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(node.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static TranslationResult Compile(ExportEntry material, Func<IEntry, TextureCompilationMetadata> metadata, bool half = false) =>
        new LE3MaterialShaderCompiler(material, getTextureMetadata: metadata).TranslateMaterialHlsl(Template(), half);
    private static int Count(string text, string value) => (text.Length - text.Replace(value, "", StringComparison.Ordinal).Length) / value.Length;
    private static void AssertOrdered(string text, params string[] tokens)
    {
        int position = -1;
        foreach (var token in tokens) { int next = text.IndexOf(token, position + 1, StringComparison.Ordinal); Assert.IsTrue(next > position, $"Expected '{token}' after offset {position} in: {text}"); position = next; }
    }
    private static void Success(TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
    private static void Failed(TranslationResult result, string message) { Assert.IsFalse(result.Success); StringAssert.Contains(string.Join("; ", result.Errors), message); }
}
