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
public class LE3FontSampleTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FontSampleUsesRawPageSampleAndAffineRange(bool half)
    {
        using var f = Fixture();
        f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        int reads = 0;
        var result = Compiler(f, getTextureMetadata: texture =>
        {
            Assert.AreSame(f.Page0, texture); reads++;
            return Metadata("TC_Normalmap", new(-2, -3, -4, -5), new(7, 8, 9, 10));
        }).TranslateMaterialHlsl(Template(), half);

        Success(result); Assert.AreEqual(1, reads);
        Assert.AreEqual(1u, result.NumUserTexCoords);
        Assert.AreEqual(1, result.UniformExpressionTextures.Count);
        Assert.AreEqual(f.Page0.UIndex, result.UniformExpressionTextures.Single());
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        AssertOrdered(result.PropertyCode["EmissiveColor"], "Parameters.TexCoords[0]", half ? "h4tex2D(" : "tex2D(",
            "Texture2D_0", "9.00000000,11.00000000,13.00000000,15.00000000", "-2.00000000");
        Assert.IsFalse(result.PropertyCode["EmissiveColor"].Contains("BiasNormalizeNormalMap", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FontSampleUsesPageZeroByDefaultAndSelectsPageOne()
    {
        using var f = Fixture(pageCount: 2);
        f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var page0 = Compiler(f, getTextureMetadata: _ => new(TextureDimension.Texture2D)).TranslateMaterialHlsl(Template());
        Success(page0); Assert.AreEqual(f.Page0.UIndex, page0.UniformExpressionTextures.Single());

        f.Node.WriteProperty(new IntProperty(1, "FontTexturePage"));
        var page1 = Compiler(f, getTextureMetadata: _ => new(TextureDimension.Texture2D)).TranslateMaterialHlsl(Template());
        Success(page1); Assert.AreEqual(f.Page1.UIndex, page1.UniformExpressionTextures.Single());
    }

    [TestMethod]
    public void FontSampleParameterPreservesFullNamedFNameAndNoneNumberFourIsOrdinary()
    {
        using var f = Fixture(className: "MaterialExpressionFontSampleParameter");
        f.Node.WriteProperty(new NameProperty(new NameReference("GlyphAtlas", 6), "ParameterName"));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var named = Compiler(f, getTextureMetadata: _ => new(TextureDimension.Texture2D)).TranslateMaterialHlsl(Template());
        Success(named);
        var parameter = named.Uniform2DTextureExpressions.Single() as MaterialUniformExpressionTextureParameter;
        Assert.IsNotNull(parameter); Assert.AreEqual(new NameReference("GlyphAtlas", 6), parameter.ParameterName);

        using var ordinary = Fixture(className: "MaterialExpressionFontSampleParameter");
        ordinary.Node.WriteProperty(new NameProperty(new NameReference("None", 4), "ParameterName"));
        ordinary.Material.WriteProperty(Input("EmissiveColor", ordinary.Node));
        var result = Compiler(ordinary, getTextureMetadata: _ => new(TextureDimension.Texture2D)).TranslateMaterialHlsl(Template());
        Success(result); Assert.IsFalse(result.Uniform2DTextureExpressions.Single() is MaterialUniformExpressionTextureParameter);
    }

    [TestMethod]
    public void AuthoredCoordinatesAndTimeAreIgnored()
    {
        using var f = Fixture();
        var time = f.Package.CreateExport("Time", "MaterialExpressionTime", f.Material, indexed: false);
        f.Node.WriteProperty(Input("Coordinates", time)); f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var result = Compiler(f, getTextureMetadata: _ => new(TextureDimension.Texture2D)).TranslateMaterialHlsl(Template());
        Success(result); Assert.IsFalse(result.UsesTime); Assert.AreEqual(1u, result.NumUserTexCoords);
        StringAssert.Contains(result.PropertyCode["EmissiveColor"], "Parameters.TexCoords[0]");
        Assert.IsFalse(result.PropertyCode["EmissiveColor"].Contains("Time", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SharedPagesAreMetadataDeduplicatedAcrossRootsAndRefreshedPerTranslation()
    {
        using var f = Fixture();
        var second = FontNode(f, "Second", f.Node.ClassName);
        f.Material.WriteProperty(new ArrayProperty<ObjectProperty>([new(f.Node.UIndex), new(second.UIndex)], "Expressions"));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node)); f.Material.WriteProperty(Input("Opacity", second));
        int pageReads = 0, metadataReads = 0;
        var compiler = new LE3MaterialShaderCompiler(f.Material,
            getTextureMetadata: texture => { Assert.AreSame(f.Page0, texture); metadataReads++; return new(TextureDimension.Texture2D); },
            getFontTexturePages: font => { Assert.AreSame(f.Font, font); pageReads++; return [f.Page0]; });
        var first = compiler.TranslateMaterialHlsl(Template()); Success(first);
        Assert.AreEqual(1, pageReads); Assert.AreEqual(1, metadataReads);
        Assert.AreEqual(1, first.UniformExpressionTextures.Count); Assert.AreEqual(1, first.Uniform2DTextureExpressions.Count);
        var secondTranslation = compiler.TranslateMaterialHlsl(Template()); Success(secondTranslation);
        Assert.AreEqual(2, pageReads); Assert.AreEqual(2, metadataReads);
    }

    [TestMethod]
    public void ImportedFontUsesProviderWithoutChangingOriginalReferenceIdentity()
    {
        using var f = Fixture(includeFont: false);
        var fontImport = new ImportEntry(f.Package, 0, "ImportedFont") { ClassName = "Font", PackageFile = "Donor" };
        f.Package.AddImport(fontImport); f.Node.WriteProperty(new ObjectProperty(fontImport.UIndex, "Font"));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node)); int providerReads = 0;
        var compiler = new LE3MaterialShaderCompiler(f.Material,
            resolveTextureImport: _ => throw new AssertFailedException("Font pages are provided directly."),
            getTextureMetadata: texture => { Assert.AreSame(f.Page0, texture); return new(TextureDimension.Texture2D); },
            getFontTexturePages: font => { providerReads++; Assert.AreSame(fontImport, font); return [f.Page0]; });
        var result = compiler.TranslateMaterialHlsl(Template());
        Success(result); Assert.AreEqual(1, providerReads); Assert.AreEqual(f.Page0.UIndex, result.UniformExpressionTextures.Single());
    }

    [TestMethod]
    public void NullFontPageProviderResultFailsWithoutLocalFallback()
    {
        using var f = Fixture(); f.Material.WriteProperty(Input("EmissiveColor", f.Node)); int metadataReads = 0;
        var result = Compiler(f, getTextureMetadata: _ => { metadataReads++; return new(TextureDimension.Texture2D); },
            getFontTexturePages: _ => null).TranslateMaterialHlsl(Template());
        Failed(result, "font-page metadata"); Assert.AreEqual(0, metadataReads);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count + result.Uniform2DTextureExpressions.Count);
    }

    [TestMethod]
    public void ImportedFontWithoutPageProviderFailsActionably()
    {
        using var f = Fixture(includeFont: false);
        var fontImport = new ImportEntry(f.Package, 0, "ImportedFont") { ClassName = "Font", PackageFile = "Donor" };
        f.Package.AddImport(fontImport); f.Node.WriteProperty(new ObjectProperty(fontImport.UIndex, "Font"));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var result = Compiler(f, getTextureMetadata: _ => new(TextureDimension.Texture2D)).TranslateMaterialHlsl(Template());
        Failed(result, "supply font-page metadata");
    }

    [TestMethod]
    public void NullSelectedPageRequiresCallerWorldDefaultTexture()
    {
        using var f = Fixture(); SetPages(f, (IEntry)null); f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        int metadataReads = 0;
        var missing = Compiler(f, getTextureMetadata: _ => { metadataReads++; return new(TextureDimension.Texture2D); })
            .TranslateMaterialHlsl(Template());
        Failed(missing, "WorldDefaultTexture"); Assert.AreEqual(0, metadataReads);

        var world = Texture(f.Package, "WorldDefault", "Texture2D");
        var withWorld = Compiler(f, worldDefault: world,
            getTextureMetadata: texture => { Assert.AreSame(world, texture); return new(TextureDimension.Texture2D); })
            .TranslateMaterialHlsl(Template());
        Success(withWorld); Assert.AreEqual(world.UIndex, withWorld.UniformExpressionTextures.Single());
    }

    [TestMethod]
    public void WorldDefaultTextureIsDistinctFromEngineDefaultTexture()
    {
        using var f = Fixture(); SetPages(f, (IEntry)null);
        var engine = Texture(f.Package, "EngineDefault", "Texture2D");
        f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var result = Compiler(f, worldDefault: engine, engineDefault: f.Page0,
            getTextureMetadata: texture => { Assert.AreSame(engine, texture); return new(TextureDimension.Texture2D); })
            .TranslateMaterialHlsl(Template());
        Success(result); Assert.AreEqual(engine.UIndex, result.UniformExpressionTextures.Single());
        Assert.AreNotEqual(f.Page0.UIndex, result.UniformExpressionTextures.Single());
    }

    [TestMethod]
    public void SelectedFontPageMustBeRegisteredTextureInBasePackage()
    {
        using var f = Fixture();
        using var foreignPackage = MEPackageHandler.CreateMemoryEmptyPackage("LE3FontForeign.pcc", MEGame.LE3);
        var foreign = Texture(foreignPackage, "Foreign", "Texture2D");
        f.Material.WriteProperty(Input("EmissiveColor", f.Node)); int metadataReads = 0;
        var result = Compiler(f, getTextureMetadata: _ => { metadataReads++; return new(TextureDimension.Texture2D); },
            getFontTexturePages: _ => [foreign]).TranslateMaterialHlsl(Template());
        Failed(result, "registered 2D font-page texture"); Assert.AreEqual(0, metadataReads);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(1)]
    public void InvalidFontPageReportsPageAndCount(int page)
    {
        using var f = Fixture(); f.Node.WriteProperty(new IntProperty(page, "FontTexturePage"));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var result = Compiler(f, getTextureMetadata: _ => new(TextureDimension.Texture2D)).TranslateMaterialHlsl(Template());
        Failed(result, $"Invalid font page {page}. Max allowed is 1");
    }

    [TestMethod]
    public void MissingFontFailsBeforePageOrTextureMetadataLookup()
    {
        using var f = Fixture(); f.Node.WriteProperty(new ObjectProperty(0, "Font"));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node)); int pageReads = 0, textureReads = 0;
        var result = Compiler(f, getTextureMetadata: _ => { textureReads++; return new(TextureDimension.Texture2D); },
            getFontTexturePages: _ => { pageReads++; return [f.Page0]; }).TranslateMaterialHlsl(Template());
        Failed(result, "Missing input Font"); Assert.AreEqual(0, pageReads); Assert.AreEqual(0, textureReads);
    }

    [TestMethod]
    public void FontMetadataProviderIsAuthoritativeAndCachedOncePerTexturePerTranslation()
    {
        using var f = Fixture();
        var second = FontNode(f, "Second", f.Node.ClassName);
        f.Material.WriteProperty(new ArrayProperty<ObjectProperty>([new(f.Node.UIndex), new(second.UIndex)], "Expressions"));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node)); f.Material.WriteProperty(Input("Opacity", second));
        int metadataReads = 0, resolverReads = 0;
        var compiler = new LE3MaterialShaderCompiler(f.Material, resolveTextureImport: _ => { resolverReads++; return null; },
            getTextureMetadata: texture => { Assert.AreSame(f.Page0, texture); metadataReads++; return Metadata("TC_Default", Vector4.Zero, Vector4.One); });
        Success(compiler.TranslateMaterialHlsl(Template()));
        Assert.AreEqual(1, metadataReads); Assert.AreEqual(0, resolverReads);
    }

    [TestMethod]
    public void MissingTextureMetadataFailsWithoutRegistrationOrResolverFallback()
    {
        using var f = Fixture(); f.Material.WriteProperty(Input("EmissiveColor", f.Node)); int resolverReads = 0;
        var result = new LE3MaterialShaderCompiler(f.Material,
            resolveTextureImport: _ => { resolverReads++; return null; }, getTextureMetadata: _ => null)
            .TranslateMaterialHlsl(Template());
        Failed(result, "no compilation metadata"); Assert.AreEqual(0, resolverReads);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count + result.Uniform2DTextureExpressions.Count);
    }

    [TestMethod]
    public void VertexFontSampleSelectsPageButDoesNotReadTextureMetadata()
    {
        using var f = Fixture();
        f.Material.WriteProperty(Input("WorldPositionOffset", f.Node)); int pageReads = 0, textureReads = 0;
        var result = Compiler(f, getTextureMetadata: _ => { textureReads++; return new(TextureDimension.Texture2D); },
            getFontTexturePages: font => { pageReads++; Assert.AreSame(f.Font, font); return [f.Page0]; }).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.AreEqual(1, pageReads); Assert.AreEqual(0, textureReads);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count + result.Uniform2DTextureExpressions.Count);
        Assert.AreEqual(0, result.UniformCubeTextureExpressions.Count);
        Assert.AreEqual(0u, result.NumUserTexCoords);
        Assert.IsTrue(result.Errors.Count(error => error == "Invalid node used in vertex shader input!") >= 3);
    }

    [TestMethod]
    public void InvalidUnselectedPageIsNotReadAndSelectedInvalidReferenceIsNotWorldFallback()
    {
        using var f = Fixture();
        f.Font.WriteProperty(new ArrayProperty<ObjectProperty>([new(f.Page0.UIndex), new(987654)], "Textures"));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var compiler = Compiler(f, worldDefault: f.Page0, getTextureMetadata: _ => new(TextureDimension.Texture2D));
        Success(compiler.TranslateMaterialHlsl(Template()));
        f.Node.WriteProperty(new IntProperty(1, "FontTexturePage"));
        var failed = compiler.TranslateMaterialHlsl(Template());
        Failed(failed, "Textures[1] has an invalid reference");
        Assert.AreEqual(0, failed.UniformExpressionTextures.Count);
    }

    [TestMethod]
    public void EngineDefaultAloneCannotSupplyNullFontPage()
    {
        using var f = Fixture(); SetPages(f, (IEntry)null);
        f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        Failed(Compiler(f, engineDefault: f.Page0, getTextureMetadata: _ => throw new AssertFailedException())
            .TranslateMaterialHlsl(Template()), "WorldDefaultTexture");
    }

    [TestMethod]
    public void WorldDefaultRejectsForeignCubeOrUnregisteredReferences()
    {
        using var f = Fixture();
        using var foreign = MEPackageHandler.CreateMemoryEmptyPackage("LE3FontWorldForeign.pcc", MEGame.LE3);
        var other = Texture(foreign, "Other", "Texture2D");
        var cube = Texture(f.Package, "Cube", "TextureCube");
        var unregistered = new ImportEntry(f.Package, 0, "Unregistered") { ClassName = "Texture2D", PackageFile = "Engine" };
        foreach (IEntry invalid in new IEntry[] { other, cube, unregistered })
            Assert.ThrowsExactly<ArgumentException>(() => new ResolvedMaterialCompilationInput(f.Material, worldDefaultTexture: invalid));
        var valid = new ResolvedMaterialCompilationInput(f.Material, worldDefaultTexture: f.Page0);
        Assert.AreSame(f.Page0, valid.WorldDefaultTexture);
    }

    [TestMethod]
    public void FontPageProviderIsSnapshottedAndInactiveFontDoesNotRequestMetadata()
    {
        using var f = Fixture();
        var pages = new IEntry[] { f.Page0 };
        f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        f.Material.WriteProperty(Input("Opacity", f.Node));
        var compiler = Compiler(f, getFontTexturePages: _ => pages,
            getTextureMetadata: _ => { pages[0] = null; return new(TextureDimension.Texture2D); });
        Success(compiler.TranslateMaterialHlsl(Template()));
        var failed = compiler.TranslateMaterialHlsl(Template());
        Failed(failed, "WorldDefaultTexture");
        f.Material.WriteProperty(Input("EmissiveColor", f.Package.CreateExport("Constant", "MaterialExpressionConstant", f.Material, indexed: false)));
        f.Material.WriteProperty(new StructProperty("ExpressionInput", new PropertyCollection { new ObjectProperty(0, "Expression") }, "Opacity"));
        Success(Compiler(f, getFontTexturePages: _ => throw new AssertFailedException(), getTextureMetadata: _ => throw new AssertFailedException())
            .TranslateMaterialHlsl(Template()));
    }

    private sealed record TestFixture(IMEPackage Package, ExportEntry Material, ExportEntry Node, ExportEntry Font,
        ExportEntry Page0, ExportEntry Page1 = null) : IDisposable
    {
        public void Dispose() => Package.Dispose();
    }

    private static TestFixture Fixture(string className = "MaterialExpressionFontSample", int pageCount = 1, bool includeFont = true)
    {
        GlobalTest.Init();
        var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FontSample.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("FontSample", className, material, indexed: false);
        var font = includeFont ? package.CreateExport("Font", "Font", indexed: false) : null;
        var page0 = Texture(package, "Page0", "Texture2D");
        var page1 = pageCount > 1 ? Texture(package, "Page1", "TextureRenderTarget2D") : null;
        if (font != null)
        {
            if (page1 == null) SetPages(package, font, page0);
            else SetPages(package, font, page0, page1);
            node.WriteProperty(new ObjectProperty(font.UIndex, "Font"));
        }
        return new(package, material, node, font, page0, page1);
    }

    private static ExportEntry FontNode(TestFixture f, string name, string className)
    {
        var node = f.Package.CreateExport(name, className, f.Material, indexed: false);
        node.WriteProperty(new ObjectProperty(f.Font.UIndex, "Font"));
        return node;
    }

    private static void SetPages(TestFixture f, params IEntry[] pages) => SetPages(f.Package, f.Font, pages);

    private static void SetPages(IMEPackage package, ExportEntry font, params IEntry[] pages) =>
        font.WriteProperty(new ArrayProperty<ObjectProperty>(pages.Select(page => new ObjectProperty(page?.UIndex ?? 0)).ToArray(), "Textures"));

    private static ExportEntry Texture(IMEPackage package, string name, string className) => package.CreateExport(name, className, indexed: false);
    private static StructProperty Input(string name, ExportEntry node) => new("ExpressionInput", new PropertyCollection
    { new ObjectProperty(node.UIndex, "Expression") }, name);
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static TextureCompilationMetadata Metadata(string compression, Vector4 min, Vector4 max) =>
        new(TextureDimension.Texture2D, compression, min, max);
    private static LE3MaterialShaderCompiler Compiler(TestFixture f, IEntry worldDefault = null, IEntry engineDefault = null,
        Func<IEntry, TextureCompilationMetadata> getTextureMetadata = null,
        Func<IEntry, System.Collections.Generic.IReadOnlyList<IEntry>> getFontTexturePages = null) =>
        new(f.Material, getTextureMetadata: getTextureMetadata, engineDefaultTexture: engineDefault,
            worldDefaultTexture: worldDefault, getFontTexturePages: getFontTexturePages);
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
