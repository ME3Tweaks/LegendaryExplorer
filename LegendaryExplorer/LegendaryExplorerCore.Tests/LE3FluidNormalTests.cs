using System;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.BinaryConverters.Shaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Shaders.LE3MaterialShaderCompiler;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3FluidNormalTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FluidNormalUsesNativeDetailCompositionAndTextureIdentity(bool half)
    {
        using var f = Fixture();
        var defaultTexture = Texture(f.Package, "Texture2D");
        f.Node.WriteProperty(Input("Coordinates", Junk(f.Package, f.Material, "MaterialExpressionTime")));
        f.Node.WriteProperty(new NameProperty("IgnoredAuthoredName", "ParameterName"));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        f.Material.WriteProperty(Input("Opacity", f.Node));
        int reads = 0;
        var compiler = Compiler(f.Material, defaultTexture, entry =>
        {
            Assert.AreSame(defaultTexture, entry); reads++;
            return new(TextureDimension.Texture2D, "TC_Normalmap", new(-1), new(0));
        });

        var result = compiler.TranslateMaterialHlsl(Template(), half);
        Success(result);
        Assert.AreEqual(1, reads, "The selected node's texture metadata is read once per translation.");
        Assert.AreSame(compiler.CompilationInput, result.CompilationInput);
        Assert.AreSame(defaultTexture, result.CompilationInput.EngineDefaultTexture);
        Assert.AreEqual(1u, result.NumUserTexCoords);
        Assert.IsFalse(result.UsesTime);
        Assert.AreEqual(2, result.UniformPixelVectorExpressions.Count);
        Assert.AreEqual(0, result.UniformPixelScalarExpressions.Count + result.UniformVertexVectorExpressions.Count + result.UniformVertexScalarExpressions.Count);
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(typeof(MaterialUniformExpressionTextureParameter), result.Uniform2DTextureExpressions.Single().GetType());
        Assert.AreEqual(new NameReference("FluidDetailNormal"), ((MaterialUniformExpressionTextureParameter)result.Uniform2DTextureExpressions.Single()).ParameterName);
        var offset = (MaterialUniformExpressionVectorParameter)result.UniformPixelVectorExpressions[0];
        var scale = (MaterialUniformExpressionVectorParameter)result.UniformPixelVectorExpressions[1];
        Assert.AreEqual(new NameReference("DetailCoordOffset"), offset.ParameterName);
        Assert.AreEqual(new NameReference("DetailCoordScale"), scale.ParameterName);
        Assert.AreEqual(LinearColor.Black, offset.DefaultValue);
        Assert.AreEqual(LinearColor.Black, scale.DefaultValue);
        CollectionAssert.AreEqual(new[] { defaultTexture.UIndex }, result.UniformExpressionTextures.ToArray());

        string t = half ? "half" : "float";
        string sample = half ? "h4tex2D(" : "tex2D(";
        string code = result.PropertyCode["EmissiveColor"];
        AssertOrdered(code, "Parameters.TexCoords[0].xy", " - UniformPixelVector_0.rg", " * UniformPixelVector_1.rg",
            sample, "Texture2D_0", "dot(", " - Local", "sqrt(", t + "3(");
        StringAssert.Contains(code, $"{t}2 Local");
        StringAssert.Contains(code, $"{t}4 Local");
        Assert.IsFalse(code.Contains("BiasNormalizeNormalMap", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("Coordinates", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("saturate(", StringComparison.Ordinal));
        Assert.AreEqual(0, result.UniformCubeTextureExpressions.Count);
    }

    [TestMethod]
    public void MissingEngineDefaultTextureFailsAfterTC0AndVectorPacking()
    {
        using var f = Fixture(); f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        int reads = 0;
        var result = Compiler(f.Material, null, _ => { reads++; return new(TextureDimension.Texture2D); }).TranslateMaterialHlsl(Template());
        Failed(result, "EngineDefaultTexture");
        Assert.AreEqual(0, reads);
        Assert.AreEqual(1u, result.NumUserTexCoords);
        Assert.AreEqual(2, result.UniformPixelVectorExpressions.Count);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count + result.Uniform2DTextureExpressions.Count);
        AssertOrdered(result.Errors.Single(), "FluidNormal", "EngineDefaultTexture");
    }

    [TestMethod]
    public void MissingMetadataFailsWithoutDonorResolutionOrTextureRegistration()
    {
        using var f = Fixture(); var texture = Texture(f.Package, "Texture2D"); f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        int reads = 0, donorReads = 0;
        var compiler = new LE3MaterialShaderCompiler(f.Material, resolveTextureImport: _ => { donorReads++; throw new AssertFailedException("No donor read expected."); },
            getTextureMetadata: entry => { Assert.AreSame(texture, entry); reads++; return null; }, engineDefaultTexture: texture);
        var result = compiler.TranslateMaterialHlsl(Template());
        Failed(result, "metadata");
        Assert.AreEqual(1, reads); Assert.AreEqual(0, donorReads);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count + result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(1u, result.NumUserTexCoords);
    }

    [TestMethod]
    [DataRow("Texture2D")]
    [DataRow("TextureRenderTarget2D")]
    public void DefaultTextureAcceptsOnlyRegisteredBasePackageTextureReferences(string className)
    {
        using var f = Fixture(); var texture = Texture(f.Package, className);
        var input = new ResolvedMaterialCompilationInput(f.Material, engineDefaultTexture: texture);
        Assert.AreSame(texture, input.EngineDefaultTexture);
        var compiler = new LE3MaterialShaderCompiler(input);
        Assert.AreSame(texture, compiler.CompilationInput.EngineDefaultTexture);

        using var foreignPackage = MEPackageHandler.CreateMemoryEmptyPackage("LE3FluidForeign.pcc", MEGame.LE3);
        var foreignTexture = Texture(foreignPackage, className);
        Assert.ThrowsExactly<ArgumentException>(() => new ResolvedMaterialCompilationInput(f.Material, engineDefaultTexture: foreignTexture));
        Assert.ThrowsExactly<ArgumentException>(() => new LE3MaterialShaderCompiler(f.Material, engineDefaultTexture: foreignTexture));
    }

    [TestMethod]
    public void DefaultTextureAcceptsRegisteredImportAndRejectsInvalidClassOrReference()
    {
        using var f = Fixture();
        var textureImport = new ImportEntry(f.Package, 0, "DefaultTexture") { ClassName = "Texture2D", PackageFile = "Engine" };
        f.Package.AddImport(textureImport);
        Assert.AreSame(textureImport, new ResolvedMaterialCompilationInput(f.Material, engineDefaultTexture: textureImport).EngineDefaultTexture);

        var invalidClass = Texture(f.Package, "TextureCube");
        var unregistered = new ImportEntry(f.Package, -987654, "NotRegistered") { ClassName = "Texture2D", PackageFile = "Engine" };
        int providerCalls = 0;
        Assert.ThrowsExactly<ArgumentException>(() => Compiler(f.Material, invalidClass, _ => { providerCalls++; return new(TextureDimension.Texture2D); }));
        Assert.ThrowsExactly<ArgumentException>(() => Compiler(f.Material, unregistered, _ => { providerCalls++; return new(TextureDimension.Texture2D); }));
        Assert.AreEqual(0, providerCalls);
    }

    [TestMethod]
    public void InactiveFluidNormalDoesNotRequireDefaultTextureOrReadMetadata()
    {
        using var f = Fixture();
        f.Node.WriteProperty(Input("Coordinates", f.Node));
        var unrelated = Junk(f.Package, f.Material, "MaterialExpressionConstant3Vector");
        unrelated.WriteProperty(new FloatProperty(1f, "R"));
        f.Material.WriteProperty(Input("EmissiveColor", unrelated));
        int reads = 0;
        var compiler = new LE3MaterialShaderCompiler(f.Material, getTextureMetadata: _ => { reads++; throw new AssertFailedException(); });
        var result = compiler.TranslateMaterialHlsl(Template());
        Success(result); Assert.AreEqual(0, reads); Assert.AreEqual(0u, result.NumUserTexCoords);
        Assert.IsFalse(result.UsesTime);
        Assert.AreSame(compiler.CompilationInput, result.CompilationInput);
    }

    [TestMethod]
    public void SharedFluidNormalNodeAndIgnoredCoordinateCycleAreAnalyzedOnce()
    {
        using var f = Fixture(); var texture = Texture(f.Package, "TextureRenderTarget2D");
        f.Node.WriteProperty(Input("Coordinates", f.Node));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node)); f.Material.WriteProperty(Input("Opacity", f.Node));
        int reads = 0;
        var compiler = Compiler(f.Material, texture, entry => { Assert.AreSame(texture, entry); reads++; return new(TextureDimension.Texture2D); });
        var result = compiler.TranslateMaterialHlsl(Template());
        Success(result); Assert.AreEqual(1, reads);
        Assert.AreEqual(1, result.UniformExpressionTextures.Count); Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(1u, result.NumUserTexCoords); Assert.IsFalse(result.UsesTime);
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "tex2D(Texture2D_0,");
        StringAssert.Contains(result.PropertyCode["Opacity"], "tex2D(Texture2D_0,");
        StringAssert.EndsWith(result.PropertyCode["Opacity"], ").r");
    }

    [TestMethod]
    public void VertexFluidNormalKeepsVectorPackingButRejectsCoordinateAndTextureAccess()
    {
        using var f = Fixture(); var texture = Texture(f.Package, "Texture2D");
        f.Material.WriteProperty(Input("WorldPositionOffset", f.Node));
        int reads = 0;
        var result = Compiler(f.Material, texture, _ => { reads++; return new(TextureDimension.Texture2D); }).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success);
        Assert.IsNull(result.MaterialHlsl);
        Assert.AreEqual(0, reads);
        Assert.AreEqual(0u, result.NumUserTexCoords);
        Assert.AreEqual(2, result.UniformVertexVectorExpressions.Count);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count + result.Uniform2DTextureExpressions.Count);
        Assert.IsTrue(result.Errors.Count(error => error.Contains("Invalid node used in vertex shader input!", StringComparison.Ordinal)) >= 3);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TranslationStateIsFreshAndRetainsRequestedInstanceIdentity(bool half)
    {
        using var f = Fixture(); var texture = Texture(f.Package, "Texture2D");
        using var instancePackage = MEPackageHandler.CreateMemoryEmptyPackage("LE3FluidInstance.pcc", MEGame.LE3);
        var instance = instancePackage.CreateExport("Instance", "MaterialInstanceConstant", indexed: false);
        var input = new ResolvedMaterialCompilationInput(f.Material, requestedMaterialInstance: instance, engineDefaultTexture: texture);
        int reads = 0;
        var compiler = new LE3MaterialShaderCompiler(input, getTextureMetadata: entry => { Assert.AreSame(texture, entry); reads++; return new(TextureDimension.Texture2D); });
        f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var first = compiler.TranslateMaterialHlsl(Template(), half);
        var second = compiler.TranslateMaterialHlsl(Template(), half);
        Success(first); Success(second); Assert.AreEqual(2, reads);
        Assert.AreSame(input, first.CompilationInput); Assert.AreSame(input, second.CompilationInput);
        Assert.AreSame(instance, first.CompilationInput.RequestedMaterial);
        Assert.AreEqual(first.PropertyCode["EmissiveColor"], second.PropertyCode["EmissiveColor"]);
        Assert.AreNotSame(first.UniformPixelVectorExpressions[0], second.UniformPixelVectorExpressions[0]);
        Assert.AreEqual(1u, first.NumUserTexCoords); Assert.AreEqual(1u, second.NumUserTexCoords);
        Assert.AreEqual(1, first.UniformExpressionTextures.Count); Assert.AreEqual(1, second.UniformExpressionTextures.Count);
    }

    private sealed record TestFixture(IMEPackage Package, ExportEntry Material, ExportEntry Node) : IDisposable
    {
        public void Dispose() => Package.Dispose();
    }

    private static TestFixture Fixture()
    {
        GlobalTest.Init();
        var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FluidNormal.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("FluidNormal", "MaterialExpressionFluidNormal", material, indexed: false);
        return new(package, material, node);
    }

    private static ExportEntry Texture(IMEPackage package, string className) => package.CreateExport("EngineDefault", className, indexed: false);
    private static ExportEntry Junk(IMEPackage package, ExportEntry material, string className) => package.CreateExport("Ignored", className, material, indexed: false);
    private static StructProperty Input(string name, ExportEntry node) => new("ExpressionInput", new PropertyCollection { new ObjectProperty(node.UIndex, "Expression") }, name);
    private static LE3MaterialShaderCompiler Compiler(ExportEntry material, IEntry texture, Func<IEntry, TextureCompilationMetadata> metadata) =>
        new(material, getTextureMetadata: metadata, engineDefaultTexture: texture);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static void Success(TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
    private static void Failed(TranslationResult result, string message) { Assert.IsFalse(result.Success); StringAssert.Contains(string.Join("; ", result.Errors), message); }
    private static void AssertOrdered(string text, params string[] tokens)
    {
        int position = -1;
        foreach (string token in tokens)
        {
            int next = text.IndexOf(token, position + 1, StringComparison.Ordinal);
            Assert.IsTrue(next > position, $"Expected '{token}' after offset {position} in: {text}");
            position = next;
        }
    }
}
