using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Textures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class ImageMipTests
    {
        /// <summary>
        /// Mips smaller than a 4x4 block must be generated as a whole padded block containing
        /// actual compressed pixel data, not zero-filled or undersized buffers.
        /// </summary>
        [TestMethod]
        public void TestSmallMipGeneration()
        {
            GlobalTest.Init();

            foreach (var format in new[] { PixelFormat.DXT1, PixelFormat.DXT3, PixelFormat.DXT5, PixelFormat.ATI2, PixelFormat.BC5, PixelFormat.BC7 })
            {
                const int topSize = 16;
                var image = new Image(new List<MipMap> { new MipMap(CreateGradientARGB(topSize, topSize), topSize, topSize, PixelFormat.ARGB) }, PixelFormat.ARGB);
                image.correctMips(format);

                Assert.AreEqual(5, image.mipMaps.Count, $"{format}: expected mip chain 16/8/4/2/1");

                int expectedSize = topSize;
                foreach (var mip in image.mipMaps)
                {
                    Assert.AreEqual(expectedSize, mip.origWidth, $"{format}: unexpected mip width");
                    Assert.AreEqual(expectedSize, mip.origHeight, $"{format}: unexpected mip height");

                    // Block-compressed data is always whole 4x4 blocks, even for 2x2 and 1x1 mips
                    int blockAligned = expectedSize < 4 ? 4 : expectedSize;
                    int expectedDataSize = format == PixelFormat.DXT1
                        ? blockAligned * blockAligned / 2
                        : blockAligned * blockAligned;
                    Assert.AreEqual(expectedDataSize, mip.data.Length, $"{format}: wrong data size for {expectedSize}x{expectedSize} mip");
                    Assert.IsTrue(mip.data.Any(b => b != 0), $"{format}: {expectedSize}x{expectedSize} mip is all zeros - pixel data was not compressed");

                    // Data must decompress without throwing and contain non-black pixels
                    int w = mip.width;
                    int h = mip.height;
                    byte[] argb = Image.convertRawToARGB(mip.data, ref w, ref h, format);
                    Assert.AreEqual(w * h * 4, argb.Length, $"{format}: decompressed {expectedSize}x{expectedSize} mip has wrong size");
                    Assert.IsTrue(Enumerable.Range(0, argb.Length).Any(i => i % 4 != 3 && argb[i] != 0),
                        $"{format}: decompressed {expectedSize}x{expectedSize} mip is all black");

                    expectedSize /= 2;
                }
            }
        }

        private static byte[] CreateGradientARGB(int w, int h)
        {
            byte[] data = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    data[i + 0] = (byte)(64 + (x + y) * 4); // B
                    data[i + 1] = (byte)(255 - x * 8);      // G
                    data[i + 2] = (byte)(64 + y * 8);       // R
                    data[i + 3] = 255;                      // A
                }
            }
            return data;
        }
    }
}
