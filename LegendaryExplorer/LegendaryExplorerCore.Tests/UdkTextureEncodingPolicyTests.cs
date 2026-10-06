using System;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class UdkTextureEncodingPolicyTests
{
    [TestMethod]
    public void ColorAndNormalIntermediatesPreserveDifferentSemantics()
    {
        foreach (string compression in new[] { null, "TC_Default", "TC_BC7", "TC_OneBitAlpha" })
        {
            var color = UdkTextureEncodingPolicy.Plan("PF_BC7", compression);
            Assert.AreEqual("PF_A8R8G8B8", color.TargetFormat);
            Assert.IsFalse(color.IsNormal); Assert.IsFalse(color.ReconstructZ);
            Assert.AreEqual(compression == "TC_OneBitAlpha" ? compression : "TC_Default", color.TargetCompression);
        }
        var normal = UdkTextureEncodingPolicy.Plan("PF_BC7", "TC_NormalmapBC7");
        Assert.IsTrue(normal.IsNormal); Assert.IsFalse(normal.ReconstructZ);
        Assert.AreEqual("TC_Normalmap", normal.TargetCompression);
        Assert.IsFalse(UdkTextureEncodingPolicy.Plan("PF_BC5", "TC_NormalmapBC5").ChangesPixels);
        Assert.ThrowsExactly<InvalidOperationException>(() => UdkTextureEncodingPolicy.Plan("PF_BC5", "TC_Default", true));
        Assert.IsTrue(UdkTextureEncodingPolicy.Plan("PF_BC5", "TC_NormalmapBC5", true).ReconstructZ);
    }

    [TestMethod]
    public void ImportPreservesDecodedRgbaAtEveryMipAndDoesNotMutateSource()
    {
        GlobalTest.Init();
        using var source = MEPackageHandler.CreateAndOpenPackage(Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".pcc"), MEGame.LE3);
        using var target = MEPackageHandler.CreateAndOpenPackage(Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".upk"), MEGame.UDK);
        var texture = Texture2D.CreateTexture(source, "Color", 8, 8, PixelFormat.BC7, true);
        var props = texture.GetProperties(); props.AddOrReplaceProp(new BoolProperty(true,"SRGB"));
        byte[] pixels = Enumerable.Range(0, 8*8*4).Select(i => (byte)(i*13+17)).ToArray();
        var image = Image.LoadFromRaw(pixels, PixelFormat.ARGB, 8, 8);
        new Texture2D(texture).Replace(image, props, isPackageStored:true);
        byte[] unchanged = texture.Data.ToArray();
        var expected = UdkTextureEncodingPolicy.CreateImage(texture, UdkTextureEncodingPolicy.Plan(texture));
        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, texture, target,
            null, true, new RelinkerOptionsPackage(), out var imported);
        var converted = (ExportEntry)imported;
        Assert.AreEqual("PF_A8R8G8B8", converted.GetProperty<EnumProperty>("Format").Value.Name);
        Assert.AreEqual("TC_Default", converted.GetProperty<EnumProperty>("CompressionSettings").Value.Name);
        Assert.IsTrue(converted.GetProperty<BoolProperty>("SRGB").Value);
        Assert.IsTrue(converted.GetProperty<BoolProperty>("CompressionNone").Value);
        var actual = new Texture2D(converted);
        Assert.AreEqual(expected.mipMaps.Count, actual.Mips.Count);
        for (int i=0;i<actual.Mips.Count;i++)
        {
            Assert.AreEqual(expected.mipMaps[i].origWidth, actual.Mips[i].width);
            Assert.AreEqual(expected.mipMaps[i].origHeight, actual.Mips[i].height);
            CollectionAssert.AreEqual(expected.mipMaps[i].data, Texture2D.GetTextureData(actual.Mips[i], MEGame.UDK));
        }
        CollectionAssert.AreEqual(unchanged, texture.Data);
        Assert.IsTrue(expected.mipMaps[0].data.Where((_,i)=>(i&3)==3).Distinct().Count()>2);
    }

    [TestMethod]
    public void NormalIntermediateRebuildsZAndSetsExplicitLinearSignedRange()
    {
        GlobalTest.Init();
        using var package = MEPackageHandler.CreateAndOpenPackage(Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".upk"), MEGame.UDK);
        var texture = Texture2D.CreateTexture(package, "Normal", 4, 4, PixelFormat.BC5, false);
        texture.WriteProperty(new EnumProperty("TC_NormalmapBC5", "TextureCompressionSettings", MEGame.UDK,"CompressionSettings"));
        byte[] pixel = {0,128,204,255};
        var source = Image.LoadFromRaw(Enumerable.Range(0,16).SelectMany(_=>pixel).ToArray(),PixelFormat.ARGB,4,4);
        new Texture2D(texture).Replace(source,texture.GetProperties(),isPackageStored:true);
        var original = new Texture2D(texture).ToImage(PixelFormat.ARGB);
        UdkTextureEncodingPolicy.ReconstructNormalZ(original);
        var decision = UdkTextureEncodingPolicy.Plan(texture,true);
        var converted = UdkTextureEncodingPolicy.CreateImage(texture,decision);
        CollectionAssert.AreEqual(original.mipMaps[0].data,converted.mipMaps[0].data);
        var props = texture.GetProperties(); UdkTextureEncodingPolicy.PrepareProperties(props,decision);
        Assert.AreEqual("TC_Normalmap",props.GetProp<EnumProperty>("CompressionSettings").Value.Name);
        Assert.IsFalse(props.GetProp<BoolProperty>("SRGB").Value);
        for(int lane=0;lane<3;lane++) Assert.AreEqual(-1f,props.GetProp<FloatProperty>("UnpackMin",lane).Value);
    }
}
