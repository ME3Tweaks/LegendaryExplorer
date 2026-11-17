using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

/// <summary>
/// How <see cref="Texture2D.Replace"/> treats mips: generated from the top mip (optionally with
/// <see cref="MipGenerationOptions"/>) or taken as supplied.
/// </summary>
[TestClass]
public class Texture2DMipReplaceTests
{
    private const int Size = 16;
    private static readonly byte[] LevelColours = { 10, 60, 110, 160, 210 }; // 16, 8, 4, 2, 1

    /// <summary>A complete chain where every level is a different flat grey, so a regenerated chain is easy to tell apart.</summary>
    private static Image FlatChain(int levelCount = 5)
    {
        var levels = new List<MipMap>();
        int size = Size;
        for (int i = 0; i < levelCount; i++, size /= 2)
        {
            byte[] data = new byte[size * size * 4];
            for (int p = 0; p < size * size; p++)
            {
                data[p * 4 + 0] = LevelColours[i];
                data[p * 4 + 1] = LevelColours[i];
                data[p * 4 + 2] = LevelColours[i];
                data[p * 4 + 3] = 255;
            }
            levels.Add(new MipMap(data, size, size, PixelFormat.ARGB));
        }
        return new Image(levels, PixelFormat.ARGB);
    }

    private static Image NoisyTop()
    {
        byte[] data = new byte[Size * Size * 4];
        var random = new Random(5);
        random.NextBytes(data);
        for (int i = 3; i < data.Length; i += 4) data[i] = 255;
        return new Image(new List<MipMap> { new MipMap(data, Size, Size, PixelFormat.ARGB) }, PixelFormat.ARGB);
    }

    /// <summary>A single-mip BC7 texture, like a cooked UI texture.</summary>
    private static ExportEntry CreateSingleMipTexture(IMEPackage package) =>
        Texture2D.CreateTexture(package, "MipTest", Size, Size, PixelFormat.BC7, false);

    private static IMEPackage CreatePackage() =>
        MEPackageHandler.CreateAndOpenPackage(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pcc"), MEGame.LE3);

    private static byte[] DecodeMip(Texture2DMipInfo mip)
    {
        byte[] data = Texture2D.GetTextureData(mip, MEGame.LE3);
        int w = Math.Max(mip.width, 4), h = Math.Max(mip.height, 4);
        return Image.convertRawToARGB(data, ref w, ref h, PixelFormat.BC7);
    }

    [TestMethod]
    public void SuppliedChainIsStoredLevelByLevel()
    {
        GlobalTest.Init();
        using var package = CreatePackage();
        var export = CreateSingleMipTexture(package);

        var messages = new Texture2D(export).Replace(FlatChain(), export.GetProperties(), isPackageStored: true, forceMipping: true, useSuppliedMips: true);

        Assert.IsEmpty(messages, string.Join("; ", messages));
        var mips = new Texture2D(export).Mips;
        Assert.HasCount(LevelColours.Length, mips);
        for (int i = 0; i < mips.Count; i++)
        {
            Assert.AreEqual(Size >> i, mips[i].width);
            Assert.AreEqual(LevelColours[i], DecodeMip(mips[i])[0], 4, $"mip {i} was not stored as supplied");
        }
    }

    [TestMethod]
    public void WithoutUseSuppliedMipsTheChainIsRegeneratedFromTheTopMip()
    {
        GlobalTest.Init();
        using var package = CreatePackage();
        var export = CreateSingleMipTexture(package);

        new Texture2D(export).Replace(FlatChain(), export.GetProperties(), isPackageStored: true, forceMipping: true);

        var mips = new Texture2D(export).Mips;
        Assert.HasCount(LevelColours.Length, mips);
        // A box chain of a flat top mip is flat: every level has the top level's colour, not the supplied ones
        Assert.AreEqual(LevelColours[0], DecodeMip(mips[3])[0], 4);
    }

    [TestMethod]
    public void IncompleteSuppliedChainFallsBackToGeneratedMipsWithAMessage()
    {
        GlobalTest.Init();
        using var package = CreatePackage();
        var export = CreateSingleMipTexture(package);

        var messages = new Texture2D(export).Replace(FlatChain(2), export.GetProperties(), isPackageStored: true, forceMipping: true, useSuppliedMips: true);

        Assert.IsTrue(messages.Any(m => m.Contains("complete")), string.Join("; ", messages));
        Assert.HasCount(LevelColours.Length, new Texture2D(export).Mips, "mips should have been generated down to 1x1");
    }

    [TestMethod]
    public void SuppliedChainOnSingleMipTextureNeedsForceMipping()
    {
        GlobalTest.Init();
        using var package = CreatePackage();
        var export = CreateSingleMipTexture(package);

        var messages = new Texture2D(export).Replace(FlatChain(), export.GetProperties(), isPackageStored: true, useSuppliedMips: true);

        Assert.IsTrue(messages.Any(m => m.Contains("single mip")), string.Join("; ", messages));
        Assert.HasCount(1, new Texture2D(export).Mips);
    }

    [TestMethod]
    public void MipOptionsChangeTheGeneratedChain()
    {
        GlobalTest.Init();

        byte[] Mip1(MipGenerationOptions options)
        {
            using var package = CreatePackage();
            var export = CreateSingleMipTexture(package);
            new Texture2D(export).Replace(NoisyTop(), export.GetProperties(), isPackageStored: true, forceMipping: true, mipOptions: options);
            var mips = new Texture2D(export).Mips;
            Assert.HasCount(LevelColours.Length, mips);
            return DecodeMip(mips[1]);
        }

        byte[] box = Mip1(null);
        byte[] lanczos = Mip1(MipGenerationOptions.HighQuality);
        Assert.IsFalse(box.SequenceEqual(lanczos), "Lanczos + sharpen should not give the box filtered chain");
    }

    [TestMethod]
    public void GenerateMipsAcceptsOptionsAndKeepsTopMipUntouched()
    {
        GlobalTest.Init();
        using var package = CreatePackage();
        var export = CreateSingleMipTexture(package);
        new Texture2D(export).Replace(NoisyTop(), export.GetProperties(), isPackageStored: true);
        byte[] topBefore = Texture2D.GetTextureData(new Texture2D(export).GetTopMip(), MEGame.LE3);

        new Texture2D(export).GenerateMips(options: MipGenerationOptions.HighQuality);

        var after = new Texture2D(export);
        Assert.HasCount(LevelColours.Length, after.Mips);
        // Same format, so the (already compressed) top mip must not be recompressed
        CollectionAssert.AreEqual(topBefore, Texture2D.GetTextureData(after.GetTopMip(), MEGame.LE3));
    }
}
