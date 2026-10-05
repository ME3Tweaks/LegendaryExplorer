using System;
using System.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class TextureEncodingPolicyTests
{
    [TestMethod]
    public void SignedNormalPlansBC5ButDataRetainsItsSemantics()
    {
        var normal=TextureEncodingPolicy.Plan(MEGame.LE3,"PF_V8U8","TC_NormalmapUncompressed",true);
        Assert.AreEqual("PF_BC5",normal.TargetFormat);Assert.AreEqual("TC_NormalmapBC5",normal.TargetCompression);Assert.IsFalse(normal.ReconstructZ);
        foreach(var setting in new[]{"TC_Default","TC_Grayscale","TC_OneBitAlpha"})
        {
            var data=TextureEncodingPolicy.Plan(MEGame.LE3,"PF_V8U8",setting,true);
            Assert.AreEqual("PF_BC7",data.TargetFormat);Assert.AreEqual(setting,data.TargetCompression);
            Assert.IsFalse(data.IsNormal);Assert.IsFalse(data.ReconstructZ);
        }
    }

    [TestMethod]
    public void BC7PreferenceExcludesExternalDataAndUDKButPreservesColorAlpha()
    {
        foreach(var format in new[]{"PF_DXT1","PF_DXT3","PF_DXT5"})
        {
            var color=TextureEncodingPolicy.Plan(MEGame.LMP,format,null,true,preferBC7:true,hasReplacementPixels:true);
            Assert.AreEqual("PF_BC7",color.TargetFormat);Assert.IsNull(color.TargetCompression);
            Assert.IsFalse(TextureEncodingPolicy.Plan(MEGame.LE3,format,"TC_Default",false,preferBC7:true,hasReplacementPixels:true).ChangesEncoding);
            Assert.IsFalse(TextureEncodingPolicy.Plan(MEGame.UDK,format,"TC_Default",true,preferBC7:true,hasReplacementPixels:true).ChangesEncoding);
            Assert.IsFalse(TextureEncodingPolicy.Plan(MEGame.LE3,format,"TC_Default",true,preferBC7:true,srgb:false,hasReplacementPixels:true).ChangesEncoding);
        }
        Assert.AreEqual("TC_OneBitAlpha",TextureEncodingPolicy.Plan(MEGame.LE3,"PF_DXT1","TC_OneBitAlpha",true,preferBC7:true,hasReplacementPixels:true).TargetCompression);
        Assert.IsFalse(TextureEncodingPolicy.Plan(MEGame.LE3,"PF_DXT1","TC_NormalmapHQ",true,preferBC7:false).ChangesEncoding);
        Assert.IsFalse(TextureEncodingPolicy.Plan(MEGame.LE3,"PF_BC7","TC_NormalmapHQ",true,preferBC7:false).ChangesEncoding);
    }

    [TestMethod]
    public void BC5ConversionIsAnExplicitSamplerDecisionAndBC7IsIdempotent()
    {
        Assert.IsFalse(TextureEncodingPolicy.Plan(MEGame.LE3,"PF_BC5","TC_NormalmapBC5",true).ChangesEncoding);
        var plan=TextureEncodingPolicy.Plan(MEGame.LE3,"PF_BC5","TC_NormalmapBC5",true,rebuildBC5:true);
        Assert.IsTrue(plan.ReconstructZ);Assert.AreEqual("PF_BC7",plan.TargetFormat);
        Assert.IsFalse(TextureEncodingPolicy.Plan(MEGame.LE3,plan.TargetFormat,plan.TargetCompression,true).ChangesEncoding);
    }

    [TestMethod]
    public void ReconstructsOnlyBlueAndKeepsXYAndAlpha()
    {
        byte[] pixel={0,128,204,71};
        var image=Image.LoadFromRaw(pixel,PixelFormat.ARGB,1,1);
        TextureEncodingPolicy.ReconstructNormalZ(image);
        Assert.AreEqual((byte)229,image.mipMaps[0].data[0]);
        Assert.AreEqual((byte)128,image.mipMaps[0].data[1]);Assert.AreEqual((byte)204,image.mipMaps[0].data[2]);Assert.AreEqual((byte)71,image.mipMaps[0].data[3]);
    }

    [TestMethod]
    public void DetectsDDSChannelsBeforeARGBConversionAndRejectsTruncatedDX10()
    {
        string file=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".dds");
        try
        {
            var bytes=new byte[148];BitConverter.GetBytes(0x20534444u).CopyTo(bytes,0);BitConverter.GetBytes(0x30315844u).CopyTo(bytes,84);
            foreach(uint format in new uint[]{83,84,51})
            {BitConverter.GetBytes(format).CopyTo(bytes,128);File.WriteAllBytes(file,bytes);Assert.IsTrue(TextureEncodingPolicy.HasTwoChannelSource(file));}
            BitConverter.GetBytes(98u).CopyTo(bytes,128);File.WriteAllBytes(file,bytes);Assert.IsFalse(TextureEncodingPolicy.HasTwoChannelSource(file));
            File.WriteAllBytes(file,bytes[..128]);Assert.ThrowsExactly<InvalidDataException>(()=>TextureEncodingPolicy.HasTwoChannelSource(file));
        }
        finally{File.Delete(file);}
    }
}
