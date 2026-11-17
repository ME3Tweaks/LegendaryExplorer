using System;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class NormalCompressionTests
{
    private static (IMEPackage Package, ExportEntry Instance, ExportEntry Parent, ExportEntry Expression, ExportEntry Texture) Fixture(
        MEGame game = MEGame.LE3, string format = "PF_BC5", string compression = "TC_NormalmapBC5", byte stored = 1)
    {
        GlobalTest.Init();
        var package = MEPackageHandler.CreateAndOpenPackage(Path.Combine(Path.GetTempPath(), "NormalCompressionTest.pcc"), game);
        var parent = package.CreateExport("Parent", "DecalMaterial");
        parent.WriteBinary(Material.Create());
        var texture = package.CreateExport("NormalTexture", "Texture2D");
        texture.WriteProperty(new EnumProperty(format, "EPixelFormat", game, "Format"));
        texture.WriteProperty(new EnumProperty(compression, "TextureCompressionSettings", game, "CompressionSettings"));
        var expression = package.CreateExport("NormalExpression", "MaterialExpressionTextureSampleParameterNormal", parent);
        var name = new NameReference("NORM", 2);
        var guid = Guid.NewGuid();
        expression.WriteProperty(new NameProperty(name, "ParameterName"));
        expression.WriteProperty(StructProperty.FromGuid(guid, "ExpressionGUID"));
        expression.WriteProperty(new ObjectProperty(texture, "Texture"));
        parent.WriteProperty(new ArrayProperty<ObjectProperty>(new[] { new ObjectProperty(expression) }, "Expressions"));
        var instance = package.CreateExport("Instance", "MaterialInstanceConstant");
        instance.WriteProperty(new ObjectProperty(parent, "Parent"));
        instance.WriteProperty(new BoolProperty(true, "bHasStaticPermutationResource"));
        instance.WriteProperty(new FloatProperty(0.37f, "TestAuthoredValue"));
        var binary = MaterialInstance.Create();
        binary.SM3StaticParameterSet.NormalParameters = new[] { new StaticParameterSet.NormalParameter {
            ParameterName = name, ExpressionGUID = guid, CompressionSettings = stored, bOverride = true } };
        binary.SM3StaticParameterSet.StaticSwitchParameters = new[] { new StaticParameterSet.StaticSwitchParameter {
            ParameterName = "BumpOffset", ExpressionGUID = Guid.NewGuid(), Value = true, bOverride = true } };
        binary.SM3StaticParameterSet.BaseMaterialId = Guid.NewGuid();
        binary.SM3StaticPermutationResource.ID = Guid.NewGuid();
        instance.WriteBinary(binary);
        return (package, instance, parent, expression, texture);
    }

    [TestMethod]
    public void DefaultTextureRepairPreservesAuthoredFieldsAndIsIdempotent()
    {
        var f = Fixture();
        using var package = f.Package;
        var before = ObjectBinary.From<MaterialInstance>(f.Instance);
        var originalNormal = before.SM3StaticParameterSet.NormalParameters[0];
        var analysis = NormalCompressionHelper.AnalyzeNormalCompressionOverrides(package).Single();
        Assert.AreEqual(NormalCompressionStatus.Mismatch, analysis.Status);
        Assert.AreEqual((byte)1, ObjectBinary.From<MaterialInstance>(f.Instance).SM3StaticParameterSet.NormalParameters[0].CompressionSettings);
        Assert.IsTrue(NormalCompressionHelper.FixNormalCompressionOverrides(package).Single().Applied);
        var after = ObjectBinary.From<MaterialInstance>(f.Instance);
        var normal = after.SM3StaticParameterSet.NormalParameters[0];
        Assert.AreEqual((byte)8, normal.CompressionSettings);
        Assert.AreEqual(originalNormal.ParameterName, normal.ParameterName);
        Assert.AreEqual(originalNormal.ExpressionGUID, normal.ExpressionGUID);
        Assert.AreEqual(originalNormal.bOverride, normal.bOverride);
        Assert.AreEqual(before.SM3StaticParameterSet.BaseMaterialId, after.SM3StaticParameterSet.BaseMaterialId);
        Assert.IsTrue(before.SM3StaticParameterSet.StaticSwitchParameters.SequenceEqual(after.SM3StaticParameterSet.StaticSwitchParameters));
        Assert.AreEqual(0.37f, f.Instance.GetProperty<FloatProperty>("TestAuthoredValue").Value);
        Assert.AreEqual(Guid.Empty, after.SM3StaticPermutationResource.ID);
        Assert.IsFalse(NormalCompressionHelper.FixNormalCompressionOverrides(package).Any(x => x.Applied));
    }

    [TestMethod]
    public void NearestInheritedTextureWinsAndNullDoesNotFallBack()
    {
        var f = Fixture();
        using var package = f.Package;
        var ancestor = package.CreateExport("Ancestor", "MaterialInstanceConstant");
        ancestor.WriteProperty(new ObjectProperty(f.Parent, "Parent"));
        f.Instance.WriteProperty(new ObjectProperty(ancestor, "Parent"));
        var alternate = package.CreateExport("Alternate", "Texture2D");
        alternate.WriteProperty(new EnumProperty("PF_BC7", "EPixelFormat", MEGame.LE3, "Format"));
        alternate.WriteProperty(new EnumProperty("TC_NormalmapBC7", "TextureCompressionSettings", MEGame.LE3, "CompressionSettings"));
        void Override(ExportEntry owner, ExportEntry texture) => owner.WriteProperty(new ArrayProperty<StructProperty>(new[] {
            new StructProperty("TextureParameterValue", new PropertyCollection {
                new NameProperty(new NameReference("NORM", 2), "ParameterName"), new ObjectProperty(texture?.UIndex ?? 0, "ParameterValue") }, "", false) }, "TextureParameterValues"));
        Override(ancestor, alternate);
        Assert.AreEqual((byte)12, NormalCompressionHelper.AnalyzeNormalCompressionOverrides(package).Single().ExpectedCompression);
        Override(f.Instance, f.Texture);
        Assert.AreEqual((byte)8, NormalCompressionHelper.AnalyzeNormalCompressionOverrides(package).Single().ExpectedCompression);
        f.Instance.WriteProperty(new ObjectProperty(0, "Parent"));
        Assert.AreEqual((byte)8, NormalCompressionHelper.AnalyzeNormalCompressionOverrides(package).Single().ExpectedCompression);
        Override(f.Instance, null);
        Assert.AreEqual(NormalCompressionStatus.Unresolved, NormalCompressionHelper.FixNormalCompressionOverrides(package).Single().Status);
        Assert.AreEqual((byte)1, ObjectBinary.From<MaterialInstance>(f.Instance).SM3StaticParameterSet.NormalParameters[0].CompressionSettings);
    }

    [TestMethod]
    public void MissingTextureMetadataProducesAnExplicitDiagnostic()
    {
        var f = Fixture();
        using var package = f.Package;
        f.Texture.RemoveProperty("CompressionSettings");
        var finding = NormalCompressionHelper.FixNormalCompressionOverrides(package).Single();
        Assert.AreEqual(NormalCompressionStatus.Unresolved, finding.Status);
        Assert.AreEqual("Explicit texture compression/format metadata is absent.", finding.Reason);
        Assert.IsFalse(finding.Applied);
    }

    [TestMethod]
    public void CyclesAndAmbiguousDefaultsAreNotRepaired()
    {
        var f = Fixture();
        using var package = f.Package;
        f.Instance.WriteProperty(new ObjectProperty(f.Instance, "Parent"));
        Assert.AreEqual(NormalCompressionStatus.Unresolved, NormalCompressionHelper.FixNormalCompressionOverrides(package).Single().Status);
        f.Instance.WriteProperty(new ObjectProperty(f.Parent, "Parent"));
        f.Parent.WriteProperty(new ArrayProperty<ObjectProperty>(new[] { new ObjectProperty(f.Expression), new ObjectProperty(f.Expression) }, "Expressions"));
        Assert.AreEqual(NormalCompressionStatus.Unresolved, NormalCompressionHelper.FixNormalCompressionOverrides(package).Single().Status);
    }

    [TestMethod]
    public void InactiveAndUnsupportedNormalEncodingsRemainUntouched()
    {
        var f = Fixture(MEGame.UDK, "PF_DXT5", "TC_Default", 10);
        using var package = f.Package;
        Assert.AreEqual(NormalCompressionStatus.Unsupported, NormalCompressionHelper.FixNormalCompressionOverrides(package).Single().Status);
        ObjectBinary.EditBinary<MaterialInstance>(f.Instance, b => b.SM3StaticParameterSet.NormalParameters[0].bOverride = false);
        Assert.AreEqual(NormalCompressionStatus.Inactive, NormalCompressionHelper.FixNormalCompressionOverrides(package).Single().Status);
        Assert.AreEqual((byte)10, ObjectBinary.From<MaterialInstance>(f.Instance).SM3StaticParameterSet.NormalParameters[0].CompressionSettings);
    }

    [TestMethod]
    [DataRow("PF_DXT1")]
    [DataRow("PF_DXT5")]
    public void UdkAffineNormalsNormalizeCompressionWithoutChangingTexture(string format)
    {
        var f = Fixture(MEGame.UDK, format, "TC_Default", 10);
        using var package = f.Package;
        f.Texture.WriteProperty(new BoolProperty(false, "SRGB"));
        var properties = f.Texture.GetProperties();
        for (int i = 0; i < 3; i++) properties.Add(new FloatProperty(-1f, "UnpackMin") { StaticArrayIndex = i });
        f.Texture.WriteProperties(properties);
        var textureData = f.Texture.Data.ToArray();
        Assert.AreEqual((byte)0, NormalCompressionHelper.AnalyzeNormalCompressionOverrides(package).Single().ExpectedCompression);
        Assert.IsTrue(NormalCompressionHelper.FixNormalCompressionOverrides(package).Single().Applied);
        Assert.IsTrue(textureData.SequenceEqual(f.Texture.Data));
        Assert.AreEqual(NormalCompressionStatus.Match, NormalCompressionHelper.AnalyzeNormalCompressionOverrides(package).Single().Status);
        Assert.IsFalse(NormalCompressionHelper.FixNormalCompressionOverrides(package).Single().Applied);
        f.Texture.RemoveProperty("CompressionSettings"); // Native UDK resaves omit this zero-valued default.
        var nativeDefault = NormalCompressionHelper.AnalyzeNormalCompressionOverrides(package).Single();
        Assert.AreEqual(NormalCompressionStatus.Match, nativeDefault.Status);
        Assert.IsTrue(nativeDefault.TextureCompressionIsNativeDefault);
        f.Texture.WriteProperty(new EnumProperty("TC_Default", "TextureCompressionSettings", MEGame.UDK, "CompressionSettings"));
        f.Texture.WriteProperty(new BoolProperty(true, "SRGB"));
        Assert.AreEqual(NormalCompressionStatus.Unsupported, NormalCompressionHelper.AnalyzeNormalCompressionOverrides(package).Single().Status);
        f.Texture.WriteProperty(new BoolProperty(false, "SRGB"));
        f.Texture.WriteProperty(new FloatProperty(0f, "UnpackMin") { StaticArrayIndex = 1 });
        Assert.AreEqual(NormalCompressionStatus.Unsupported, NormalCompressionHelper.AnalyzeNormalCompressionOverrides(package).Single().Status);
    }

    [TestMethod]
    public void UdkUncompressedRgbNormalAlreadyUsesTheCorrectDecoder()
    {
        var f = Fixture(MEGame.UDK, "PF_A8R8G8B8", "TC_Normalmap", 1);
        using var package = f.Package;
        f.Texture.WriteProperty(new BoolProperty(false, "SRGB"));
        var properties = f.Texture.GetProperties();
        for (int i = 0; i < 3; i++) properties.Add(new FloatProperty(-1f, "UnpackMin") { StaticArrayIndex = i });
        f.Texture.WriteProperties(properties);
        Assert.AreEqual(NormalCompressionStatus.Match, NormalCompressionHelper.AnalyzeNormalCompressionOverrides(package).Single().Status);
        Assert.IsFalse(NormalCompressionHelper.FixNormalCompressionOverrides(package).Single().Applied);
    }

    [TestMethod]
    public void UdkConversionUsesDestinationEncodingAndBothPresentQualityBlocks()
    {
        var f = Fixture(MEGame.UDK, "PF_DXT1", "TC_Normalmap", 12);
        using var package = f.Package;
        ObjectBinary.EditBinary<MaterialInstance>(f.Instance, b => {
            b.UDKHasLowQualityPermutation = true;
            b.SM2StaticParameterSet = (StaticParameterSet)Guid.Empty;
            b.SM2StaticPermutationResource = MaterialResource.Create();
            b.SM2StaticParameterSet.NormalParameters = new[] { new StaticParameterSet.NormalParameter {
                ParameterName = new NameReference("NORM", 2), ExpressionGUID = b.SM3StaticParameterSet.NormalParameters[0].ExpressionGUID,
                CompressionSettings = 12, bOverride = true } };
        });
        Assert.AreEqual(2, NormalCompressionHelper.FixNormalCompressionOverrides(package).Count(finding => finding.Applied));
        var after = ObjectBinary.From<MaterialInstance>(f.Instance);
        Assert.AreEqual((byte)1, after.SM3StaticParameterSet.NormalParameters[0].CompressionSettings);
        Assert.AreEqual((byte)1, after.SM2StaticParameterSet.NormalParameters[0].CompressionSettings);
    }
}
