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
public class LE3FlipBookSampleTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FlipBookUsesMetadataScaleCoordinatesOffsetRawSampleAndUnpackInOrder(bool half)
    {
        using var f = Fixture();
        f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var result = Compiler(f).TranslateMaterialHlsl(Template(), half);
        Success(result);
        string code = result.PropertyCode["EmissiveColor"];
        AssertOrdered(code, "Parameters.TexCoords[0]", half ? "half2(0.25000000,0.50000000)" : "float2(0.25000000,0.50000000)",
            " * ", " + ", half ? "h4tex2D(" : "tex2D(", "Texture2D_0", half ? " * half4" : " * float4");
        Assert.AreEqual(1u, result.NumUserTexCoords);
        Assert.AreEqual(1, result.UniformExpressionTextures.Count);
        Assert.AreEqual(f.Texture.UIndex, result.UniformExpressionTextures.Single());
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
    }

    [TestMethod]
    public void FlipBookOffsetUsesOriginalFloat4NumericUniformAndMasksRedGreen()
    {
        using var f = Fixture(); SetTextureOffset(f.Texture, 3, 4, 5, 6);
        f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compiler(f).TranslateMaterialHlsl(Template()); Success(result);
        var offset = result.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionFlipBookTextureParameter>().Single();
        Assert.AreEqual("FMaterialUniformExpressionFlipBookTextureParameter", offset.ExpressionType.Name);
        Assert.AreEqual(0, offset.TextureIndex);
        Assert.AreEqual(f.Texture.UIndex, result.UniformExpressionTextures[offset.TextureIndex]);
        Assert.IsFalse(result.Uniform2DTextureExpressions.Contains(offset));
        Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
        AssertOrdered(result.PropertyCode["Opacity"], "Parameters.TexCoords[0]", " * ", " + ", "UniformPixelVector_", ".rg", "tex2D(");
        StringAssert.Contains(result.PropertyCode["Opacity"], "UniformPixelVector_0.rg");
    }

    [TestMethod]
    public void AuthoredParameterNameDoesNotChangeFlipBookOffsetLeaf()
    {
        using var f = Fixture();
        f.Node.WriteProperty(new NameProperty(new NameReference("AuthoredOffset", 7), "ParameterName"));
        f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compiler(f).TranslateMaterialHlsl(Template()); Success(result);
        var offset = result.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionFlipBookTextureParameter>().Single();
        Assert.AreEqual("FMaterialUniformExpressionFlipBookTextureParameter", offset.ExpressionType.Name);
        Assert.AreEqual(f.Texture.UIndex, result.UniformExpressionTextures[offset.TextureIndex]);
    }

    [TestMethod]
    [DataRow("TC_Default", false, false)]
    [DataRow("TC_NormalmapHQ", false, false)]
    [DataRow("TC_Normalmap", true, false)]
    [DataRow("TC_Normalmap", true, true)]
    public void FlipBookUsesCompressionSpecificUnpackPath(string compression, bool normalHelper, bool canonicalNormalRange)
    {
        Vector4 min = canonicalNormalRange ? new Vector4(-1, -1, -1, 0) : new Vector4(-2, -3, -4, -5);
        Vector4 max = canonicalNormalRange ? Vector4.One : new Vector4(7, 8, 9, 10);
        using var f = Fixture(compression: compression, unpackMin: min, unpackMax: max);
        f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compiler(f).TranslateMaterialHlsl(Template()); Success(result);
        string code = result.PropertyCode["Opacity"];
        if (normalHelper)
        {
            StringAssert.Contains(code, "BiasNormalizeNormalMap");
            if (canonicalNormalRange)
                Assert.IsFalse(code.Contains("9.00000000,11.00000000,13.00000000,15.00000000", StringComparison.Ordinal));
            else
                AssertOrdered(code, "BiasNormalizeNormalMap", "+ float4(1.00000000,1.00000000,1.00000000,0.00000000)",
                    "* float4(0.50000000,0.50000000,0.50000000,1.00000000)", "* float4(9.00000000,11.00000000,13.00000000,15.00000000)",
                    "+ float4(-2.00000000,-3.00000000,-4.00000000,-5.00000000)");
        }
        else StringAssert.Contains(code, "9.00000000,11.00000000,13.00000000,15.00000000");
    }

    [TestMethod]
    public void ImportedFlipBookUsesAuthoritativeMetadataWithoutResolvingDonor()
    {
        using var f = Fixture(includeTexture: false);
        var imported = new ImportEntry(f.Package, 0, "ImportedFlipBook") { ClassName = "TextureFlipBook", PackageFile = "Donor" };
        f.Package.AddImport(imported); f.Node.WriteProperty(new ObjectProperty(imported.UIndex, "Texture"));
        f.Material.WriteProperty(Input("Opacity", f.Node)); int reads = 0;
        var compiler = new LE3MaterialShaderCompiler(f.Material,
            resolveTextureImport: _ => throw new AssertFailedException("FlipBook metadata comes from the provider."),
            getTextureMetadata: texture => { reads++; Assert.AreSame(imported, texture); return Metadata(flipBookScale: new Vector2(.25f, .5f)); });
        var result = compiler.TranslateMaterialHlsl(Template());
        Success(result); Assert.AreEqual(1, reads); Assert.AreEqual(imported.UIndex, result.UniformExpressionTextures.Single());
    }

    [TestMethod]
    public void MetadataIsAuthoritativeCachedPerTextureAndRefreshedPerTranslation()
    {
        using var f = Fixture(); var second = NewNode(f, "Second");
        f.Material.WriteProperty(new ArrayProperty<ObjectProperty>([new(f.Node.UIndex), new(second.UIndex)], "Expressions"));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node)); f.Material.WriteProperty(Input("Opacity", second));
        int reads = 0;
        var compiler = new LE3MaterialShaderCompiler(f.Material, getTextureMetadata: texture =>
        { Assert.AreSame(f.Texture, texture); reads++; return Metadata(flipBookScale: new Vector2(.125f, .75f)); });
        var first = compiler.TranslateMaterialHlsl(Template()); Success(first); Assert.AreEqual(1, reads);
        var secondTranslation = compiler.TranslateMaterialHlsl(Template()); Success(secondTranslation); Assert.AreEqual(2, reads);
        var firstOffset = first.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionFlipBookTextureParameter>().Single();
        var secondOffset = secondTranslation.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionFlipBookTextureParameter>().Single();
        Assert.AreNotSame(firstOffset, secondOffset);
    }

    [TestMethod]
    public void ExplicitFromPropertiesScaleIsUsedWithoutReadingTexturePayload()
    {
        using var f = Fixture(); f.Material.WriteProperty(Input("Opacity", f.Node));
        var metadata = TextureCompilationMetadata.FromProperties(TextureDimension.Texture2D, new PropertyCollection(), new Vector2(.125f, .375f));
        var result = Compiler(f, _ => metadata).TranslateMaterialHlsl(Template()); Success(result);
        StringAssert.Contains(result.PropertyCode["Opacity"], "float2(0.12500000,0.37500000)");
    }

    [TestMethod]
    public void SharedOriginalTextureInternsOffsetAcrossNodesButDistinctTexturesRemainDistinct()
    {
        using var f = Fixture(); var shared = NewNode(f, "Shared");
        var otherTexture = Texture(f.Package, "OtherTexture", "TextureFlipBook");
        var distinct = NewNode(f, "Distinct"); distinct.WriteProperty(new ObjectProperty(otherTexture.UIndex, "Texture"));
        f.Material.WriteProperty(new ArrayProperty<ObjectProperty>([new(f.Node.UIndex), new(shared.UIndex), new(distinct.UIndex)], "Expressions"));
        f.Material.WriteProperty(Input("EmissiveColor", f.Node)); f.Material.WriteProperty(Input("Opacity", shared));
        f.Material.WriteProperty(Input("DiffuseColor", distinct));
        var result = Compiler(f, _ => Metadata(flipBookScale: new Vector2(.25f, .5f))).TranslateMaterialHlsl(Template()); Success(result);
        var offsets = result.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionFlipBookTextureParameter>().ToArray();
        Assert.AreEqual(2, offsets.Length);
        Assert.AreEqual(2, offsets.Select(offset => offset.TextureIndex).Distinct().Count());
        CollectionAssert.AreEqual(new[] { f.Texture.UIndex, otherTexture.UIndex }, result.UniformExpressionTextures.ToArray());
    }

    [TestMethod]
    public void ScalarCoordinatesBroadcastWhileExplicitThreeAndFourVectorInputsFail()
    {
        using (var f = Fixture())
        {
            f.Node.WriteProperty(Input("Coordinates", Constant(f.Package, f.Material, "MaterialExpressionConstant", "R", .75f)));
            f.Material.WriteProperty(Input("Opacity", f.Node));
            var result = Compiler(f).TranslateMaterialHlsl(Template()); Success(result);
            Assert.AreEqual(typeof(MaterialUniformExpressionFlipBookTextureParameter), result.UniformPixelVectorExpressions.Single().GetType());
            StringAssert.Contains(result.PropertyCode["Opacity"], "0.75000000");
            Assert.AreEqual(0u, result.NumUserTexCoords);
        }
        foreach (var value in new object[] { new Vector3(.1f, .2f, .3f), new Vector4(.1f, .2f, .3f, .4f) })
        {
            using var f = Fixture();
            string className = value is Vector3 ? "MaterialExpressionConstant3Vector" : "MaterialExpressionConstant4Vector";
            f.Node.WriteProperty(Input("Coordinates", Constant(f.Package, f.Material, className, "Constant", value)));
            f.Material.WriteProperty(Input("Opacity", f.Node));
            var result = Compiler(f).TranslateMaterialHlsl(Template());
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Errors.Count > 0);
            Assert.AreEqual(1, result.UniformExpressionTextures.Count);
            Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count);
        }
    }

    [TestMethod]
    public void VertexFlipBookKeepsTextureCoordinateOffsetAndSampleGuards()
    {
        using var f = Fixture(); f.Material.WriteProperty(Input("WorldPositionOffset", f.Node));
        var result = Compiler(f).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success);
        Assert.AreEqual(4, result.Errors.Count(error => error == "Invalid node used in vertex shader input!"));
        Assert.AreEqual(0u, result.NumUserTexCoords);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        Assert.AreEqual(0, result.Uniform2DTextureExpressions.Count + result.UniformCubeTextureExpressions.Count);
        Assert.AreEqual(0, result.UniformVertexVectorExpressions.Count + result.UniformVertexScalarExpressions.Count);
        Assert.AreEqual(0, result.UniformPixelVectorExpressions.Count + result.UniformPixelScalarExpressions.Count);
    }

    [TestMethod]
    public void AuthoredTimeIsVisitedButNeverUsedAsFlipBookFrameInput()
    {
        using var f = Fixture();
        var time = f.Package.CreateExport("Time", "MaterialExpressionTime", f.Material, indexed: false);
        f.Node.WriteProperty(Input("Coordinates", time)); f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compiler(f).TranslateMaterialHlsl(Template());
        Success(result); Assert.IsTrue(result.UsesTime);
        Assert.AreEqual(1, result.UniformPixelScalarExpressions.OfType<MaterialUniformExpressionTime>().Count());
        Assert.IsFalse(result.PropertyCode["Opacity"].Contains("Time", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MissingTextureAndWrongTextureClassFailBeforeMetadataOrCoordinates()
    {
        using (var f = Fixture(includeTexture: false))
        {
            f.Node.WriteProperty(Input("Coordinates", Junk(f.Package, f.Material, "MaterialExpressionTime")));
            f.Material.WriteProperty(Input("Opacity", f.Node)); int reads = 0;
            var result = Compiler(f, _ => { reads++; throw new AssertFailedException(); }).TranslateMaterialHlsl(Template());
            Failed(result, "Missing input texture"); Assert.AreEqual(0, reads); Assert.IsFalse(result.UsesTime);
        }
        using (var f = Fixture(textureClass: "Texture2D"))
        {
            f.Node.WriteProperty(Input("Coordinates", Junk(f.Package, f.Material, "MaterialExpressionTime")));
            f.Material.WriteProperty(Input("Opacity", f.Node)); int reads = 0;
            var result = Compiler(f, _ => { reads++; throw new AssertFailedException(); }).TranslateMaterialHlsl(Template());
            Failed(result, "Texture is not a FlipBook"); Assert.AreEqual(0, reads); Assert.IsFalse(result.UsesTime);
        }
    }

    [TestMethod]
    public void MissingOrNonfiniteFlipBookScaleFailsClosed()
    {
        foreach (var metadata in new TextureCompilationMetadata[]
        {
            new(TextureDimension.Texture2D),
            Metadata(flipBookScale: new Vector2(float.NaN, .5f)),
            Metadata(flipBookScale: new Vector2(.25f, float.PositiveInfinity))
        })
        {
            using var f = Fixture(); f.Material.WriteProperty(Input("Opacity", f.Node));
            var result = Compiler(f, _ => metadata).TranslateMaterialHlsl(Template());
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Errors.Count > 0);
            Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        }
        using (var f = Fixture())
        {
            f.Material.WriteProperty(Input("Opacity", f.Node));
            var result = Compiler(f, _ => null).TranslateMaterialHlsl(Template());
            Failed(result, "no compilation metadata"); Assert.AreEqual(0, result.UniformExpressionTextures.Count);
        }
    }

    [TestMethod]
    public void OrdinaryTextureIgnoresFlipBookScaleMetadata()
    {
        using var f = Fixture(textureClass: "Texture2D");
        var sample = f.Package.CreateExport("OrdinarySample", "MaterialExpressionTextureSample", f.Material, indexed: false);
        sample.WriteProperty(new ObjectProperty(f.Texture.UIndex, "Texture"));
        f.Material.WriteProperty(Input("Opacity", sample));
        var result = new LE3MaterialShaderCompiler(f.Material, getTextureMetadata: _ => Metadata(flipBookScale: new(float.NaN, float.PositiveInfinity)))
            .TranslateMaterialHlsl(Template());
        Success(result); Assert.AreEqual(1, result.Uniform2DTextureExpressions.Count);
    }

    [TestMethod]
    public void LoadedFlipBookAdapterDoesNotInventTransientScaleOrPostLoadTexture()
    {
        using var f = Fixture();
        f.Texture.WriteProperty(new IntProperty(4, "HorizontalImages"));
        f.Texture.WriteProperty(new IntProperty(2, "VerticalImages"));
        f.Material.WriteProperty(Input("Opacity", f.Node));
        int resolverCalls = 0;
        var result = new LE3MaterialShaderCompiler(f.Material, resolveTextureImport: _ => { resolverCalls++; throw new AssertFailedException(); })
            .TranslateMaterialHlsl(Template());
        Failed(result, "requires effective FlipBookScale");
        Assert.AreEqual(0, resolverCalls);
        Assert.AreEqual(0, result.UniformExpressionTextures.Count + result.Uniform2DTextureExpressions.Count);
        Assert.IsNull(f.Texture.GetProperties().GetProp<FloatProperty>("HorizontalScale"));
        Assert.IsNull(f.Texture.GetProperties().GetProp<FloatProperty>("VerticalScale"));
    }

    [TestMethod]
    public void CoordinateCycleFailsWithoutReadingInactiveInputs()
    {
        using var f = Fixture();
        f.Node.WriteProperty(Input("Coordinates", f.Node));
        f.Node.WriteProperty(Input("Unrelated", Junk(f.Package, f.Material, "MaterialExpressionTime")));
        f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compiler(f).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsFalse(result.UsesTime);
    }

    [TestMethod]
    public void FlipBookOffsetUniformEvaluatesThroughRuntimeDelegateAndIsFrameDependent()
    {
        using var f = Fixture(); f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compiler(f).TranslateMaterialHlsl(Template()); Success(result);
        var offset = result.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionFlipBookTextureParameter>().Single();
        Assert.IsFalse(offset.IsNotFrameDependent);
        var expected = new LinearColor(11, 12, 0, 0); int calls = 0;
        var context = new UniformExpressionRenderContext(null, null, 2, 3, (_, textureIndex) =>
        { calls++; Assert.AreEqual(0, textureIndex); return expected; });
        var actual = new LinearColor(0, 0, 0, 0); offset.GetNumberValue(context, ref actual);
        Assert.AreEqual(1, calls); Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void FlipBookOffsetNumericExpressionSerializesAndReadsBackAsNumericLeaf()
    {
        using var f = Fixture(); f.Material.WriteProperty(Input("Opacity", f.Node));
        var result = Compiler(f).TranslateMaterialHlsl(Template()); Success(result);
        var original = result.UniformPixelVectorExpressions.OfType<MaterialUniformExpressionFlipBookTextureParameter>().Single();
        using var stream = new MemoryStream(); original.Serialize(new SerializingContainer(stream, f.Package));
        stream.Position = 0;
        var reader = new SerializingContainer(stream, f.Package, isLoading: true);
        var copy = MaterialUniformExpression.Create(reader);
        copy.Serialize(reader);
        Assert.AreEqual(stream.Length, stream.Position);
        Assert.IsInstanceOfType<MaterialUniformExpressionFlipBookTextureParameter>(copy);
        var loaded = (MaterialUniformExpressionFlipBookTextureParameter)copy;
        Assert.AreEqual("FMaterialUniformExpressionFlipBookTextureParameter", loaded.ExpressionType.Name);
        Assert.AreEqual(original.TextureIndex, loaded.TextureIndex);
        Assert.AreEqual(f.Texture.UIndex, result.UniformExpressionTextures[loaded.TextureIndex]);
        Assert.IsFalse(result.Uniform2DTextureExpressions.Contains(loaded));
        var expected = new MaterialShaderMap
        {
            UniformPixelVectorExpressions = [loaded], UniformPixelScalarExpressions = [], Uniform2DTextureExpressions = result.Uniform2DTextureExpressions.ToArray(),
            UniformCubeTextureExpressions = [], UniformVertexVectorExpressions = [], UniformVertexScalarExpressions = []
        };
        var comparison = LE3MaterialShaderCompiler.CompareUniformExpressionsWithShaderMap(result, expected);
        Assert.IsTrue(comparison.MatchesExpectedUniformExpressions, string.Join("; ", comparison.Errors.Concat(comparison.Differences)));
    }

    [TestMethod]
    public void NestedCoordinateSampleGetsGlobalTextureIdentityInAccessOrder()
    {
        using var f = Fixture();
        var innerTexture = Texture(f.Package, "InnerTexture", "Texture2D");
        var inner = f.Package.CreateExport("InnerSample", "MaterialExpressionTextureSample", f.Material, indexed: false);
        inner.WriteProperty(new ObjectProperty(innerTexture.UIndex, "Texture"));
        var mask = f.Package.CreateExport("InnerMask", "MaterialExpressionComponentMask", f.Material, indexed: false);
        mask.WriteProperty(Input("Input", inner)); mask.WriteProperty(new BoolProperty(true, "R")); mask.WriteProperty(new BoolProperty(true, "G"));
        f.Node.WriteProperty(Input("Coordinates", mask)); f.Material.WriteProperty(Input("EmissiveColor", f.Node));
        var result = Compiler(f, texture => ReferenceEquals(texture, f.Texture) ? f.CompilationMetadata : new(TextureDimension.Texture2D))
            .TranslateMaterialHlsl(Template());
        Success(result);
        CollectionAssert.AreEqual(new[] { innerTexture.UIndex, f.Texture.UIndex }, result.UniformExpressionTextures.ToArray());
        CollectionAssert.AreEqual(new[] { 0, 1 }, result.Uniform2DTextureExpressions.Select(texture => texture.TextureIndex).ToArray());
        Assert.AreEqual(1, ((MaterialUniformExpressionFlipBookTextureParameter)result.UniformPixelVectorExpressions.Single()).TextureIndex);
        AssertOrdered(result.PropertyCode["EmissiveColor"], "tex2D(Texture2D_0,", "UniformPixelVector_0.rg", "tex2D(Texture2D_1,");
    }

    [TestMethod]
    public void VertexAuthoredClockIsVisitedWithoutRegisteringFlipBookOffset()
    {
        using var f = Fixture();
        var time = f.Package.CreateExport("Time", "MaterialExpressionTime", f.Material, indexed: false);
        f.Node.WriteProperty(Input("Coordinates", time)); f.Material.WriteProperty(Input("WorldPositionOffset", f.Node));
        var result = Compiler(f).TranslateMaterialHlsl(Template());
        Assert.IsFalse(result.Success); Assert.IsTrue(result.UsesTime);
        Assert.AreEqual(3, result.Errors.Count(error => error == "Invalid node used in vertex shader input!"));
        Assert.AreEqual(typeof(MaterialUniformExpressionTime), result.UniformVertexScalarExpressions.Single().GetType());
        Assert.AreEqual(0, result.UniformExpressionTextures.Count + result.Uniform2DTextureExpressions.Count + result.UniformVertexVectorExpressions.Count);
    }

    [TestMethod]
    public void PropertyMetadataAdapterRetainsExplicitScaleWithoutInterpretingAnimationProperties()
    {
        var properties = new PropertyCollection { new IntProperty(99, "HorizontalImages"), new IntProperty(77, "VerticalImages") };
        Assert.IsNull(TextureCompilationMetadata.FromProperties(TextureDimension.Texture2D, properties).FlipBookScale);
        var supplied = new Vector2(.25f, .5f);
        var metadata = TextureCompilationMetadata.FromProperties(TextureDimension.Texture2D, properties, supplied);
        Assert.AreEqual(supplied, metadata.FlipBookScale.Value);
        Assert.AreEqual(Vector4.Zero, metadata.UnpackMin); Assert.AreEqual(Vector4.One, metadata.UnpackMax);
    }

    [TestMethod]
    public void NumericOffsetComparisonRejectsSamplerArrayAndNegativeResourceIndex()
    {
        using var f = Fixture(); f.Material.WriteProperty(Input("Opacity", f.Node));
        var translation = Compiler(f).TranslateMaterialHlsl(Template()); Success(translation);
        var offset = (MaterialUniformExpressionFlipBookTextureParameter)translation.UniformPixelVectorExpressions.Single();
        var expected = new MaterialShaderMap
        {
            UniformPixelVectorExpressions = [], UniformPixelScalarExpressions = [], Uniform2DTextureExpressions = [offset],
            UniformCubeTextureExpressions = [], UniformVertexVectorExpressions = [], UniformVertexScalarExpressions = []
        };
        var wrongCategory = CompareUniformExpressionsWithShaderMap(translation, expected);
        StringAssert.Contains(string.Join("; ", wrongCategory.Errors), "wrong numeric/texture root category");
        expected.Uniform2DTextureExpressions = translation.Uniform2DTextureExpressions.ToArray();
        expected.UniformPixelVectorExpressions = [new MaterialUniformExpressionFlipBookTextureParameter
            { ExpressionType = "FMaterialUniformExpressionFlipBookTextureParameter", TextureIndex = -1 }];
        StringAssert.Contains(string.Join("; ", CompareUniformExpressionsWithShaderMap(translation, expected).Errors), "nonnegative resource slot");
    }

    private sealed record TestFixture(IMEPackage Package, ExportEntry Material, ExportEntry Node, ExportEntry Texture,
        TextureCompilationMetadata CompilationMetadata) : IDisposable
    {
        public void Dispose() => Package.Dispose();
    }

    private static TestFixture Fixture(string textureClass = "TextureFlipBook", bool includeTexture = true,
        string compression = "TC_Default", Vector4? unpackMin = null, Vector4? unpackMax = null)
    {
        GlobalTest.Init();
        var package = MEPackageHandler.CreateMemoryEmptyPackage("LE3FlipBookSample.pcc", MEGame.LE3);
        var material = package.CreateExport("Material", "Material", indexed: false);
        var node = package.CreateExport("FlipBook", "MaterialExpressionFlipBookSample", material, indexed: false);
        var texture = includeTexture ? Texture(package, "FlipBookTexture", textureClass) : null;
        if (texture != null) node.WriteProperty(new ObjectProperty(texture.UIndex, "Texture"));
        return new(package, material, node, texture, Metadata(compression, unpackMin, unpackMax, new Vector2(.25f, .5f)));
    }

    private static ExportEntry NewNode(TestFixture f, string name)
    {
        var node = f.Package.CreateExport(name, "MaterialExpressionFlipBookSample", f.Material, indexed: false);
        node.WriteProperty(new ObjectProperty(f.Texture.UIndex, "Texture"));
        return node;
    }

    private static ExportEntry Texture(IMEPackage package, string name, string className) => package.CreateExport(name, className, indexed: false);
    private static void SetTextureOffset(ExportEntry texture, float r, float g, float b, float a) => texture.WriteProperty(
        new StructProperty("LinearColor", new PropertyCollection
        { new FloatProperty(r, "R"), new FloatProperty(g, "G"), new FloatProperty(b, "B"), new FloatProperty(a, "A") }, "TextureOffset", isImmutable: true));
    private static StructProperty Input(string name, ExportEntry node) => new("ExpressionInput", new PropertyCollection
    { new ObjectProperty(node.UIndex, "Expression") }, name);
    private static ExportEntry Junk(IMEPackage package, ExportEntry material, string className) => package.CreateExport("Ignored", className, material, indexed: false);
    private static ExportEntry Constant(IMEPackage package, ExportEntry material, string className, string property, object value)
    {
        var expression = package.CreateExport("Coordinate", className, material, indexed: false);
        switch (value)
        {
            case float scalar: expression.WriteProperty(new FloatProperty(scalar, property)); break;
            case Vector3 vector3:
                expression.WriteProperty(new FloatProperty(vector3.X, "R")); expression.WriteProperty(new FloatProperty(vector3.Y, "G"));
                expression.WriteProperty(new FloatProperty(vector3.Z, "B")); break;
            case Vector4 vector4:
                expression.WriteProperty(new FloatProperty(vector4.X, "R")); expression.WriteProperty(new FloatProperty(vector4.Y, "G"));
                expression.WriteProperty(new FloatProperty(vector4.Z, "B")); expression.WriteProperty(new FloatProperty(vector4.W, "A")); break;
            default: throw new ArgumentOutOfRangeException(nameof(value));
        }
        return expression;
    }

    private static TextureCompilationMetadata Metadata(string compression = "TC_Default", Vector4? unpackMin = null,
        Vector4? unpackMax = null, Vector2? flipBookScale = null) =>
        new(TextureDimension.Texture2D, compression, unpackMin, unpackMax, flipBookScale ?? new Vector2(.25f, .5f));

    private static LE3MaterialShaderCompiler Compiler(TestFixture f, Func<IEntry, TextureCompilationMetadata> getTextureMetadata = null) =>
        new(f.Material, getTextureMetadata: getTextureMetadata ?? (_ => f.CompilationMetadata));
    private static string Template() => "%u\n" + string.Join("\n", Enumerable.Repeat("%s", 18)) + "\n#line %u\n";
    private static void Success(TranslationResult result) => Assert.IsTrue(result.Success, string.Join("; ", result.Errors));
    private static void Failed(TranslationResult result, string message)
    { Assert.IsFalse(result.Success); StringAssert.Contains(string.Join("; ", result.Errors), message); }
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
