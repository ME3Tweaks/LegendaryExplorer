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

        /// <summary>
        /// Each mip is the rounded average of the 2x2 block above it. Truncating instead biases every
        /// level down and, since each mip is built from the previous one, the loss compounds - which
        /// shows in dark, smooth data such as lightmaps.
        /// </summary>
        [TestMethod]
        public void TestMipDownscaleRounds()
        {
            GlobalTest.Init();

            // Per 2x2 block: B has two 1s (average 0.5, rounds up), G has one 1 (0.25, rounds down),
            // R has three 1s (0.75, rounds up), A is constant.
            const int size = 4;
            byte[] top = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = (y * size + x) * 4;
                    top[i + 0] = (byte)((x + y) % 2 == 0 ? 1 : 0);
                    top[i + 1] = (byte)(x % 2 == 0 && y % 2 == 0 ? 1 : 0);
                    top[i + 2] = (byte)(x % 2 == 0 && y % 2 == 0 ? 0 : 1);
                    top[i + 3] = 255;
                }
            }

            var image = new Image(new List<MipMap> { new MipMap(top, size, size, PixelFormat.ARGB) }, PixelFormat.ARGB);
            image.correctMips(PixelFormat.ARGB);

            Assert.AreEqual(3, image.mipMaps.Count, "expected mip chain 4/2/1");
            foreach (var mip in image.mipMaps.Skip(1))
            {
                for (int i = 0; i < mip.data.Length; i += 4)
                {
                    Assert.AreEqual(1, mip.data[i + 0], $"{mip.origWidth}x{mip.origHeight}: B 0.5 should round up");
                    Assert.AreEqual(0, mip.data[i + 1], $"{mip.origWidth}x{mip.origHeight}: G 0.25 should round down");
                    Assert.AreEqual(1, mip.data[i + 2], $"{mip.origWidth}x{mip.origHeight}: R 0.75 should round up");
                    Assert.AreEqual(255, mip.data[i + 3], $"{mip.origWidth}x{mip.origHeight}: constant A must stay constant");
                }
            }
        }

        /// <summary>The 1-pixel-wide/tall path (used once a mip chain runs out of one dimension) rounds too.</summary>
        [TestMethod]
        public void TestMipDownscaleRoundsNarrowMips()
        {
            GlobalTest.Init();

            // 4x2 -> 2x1 -> 1x1. The left 2x2 block is all 1s in B and the right one all 0s, so the 2x1 mip
            // is exact, and its two pixels then average to 0.5.
            byte[] top = new byte[4 * 2 * 4];
            int[] blue = { 1, 1, 0, 0, 1, 1, 0, 0 };
            for (int i = 0; i < 8; i++)
            {
                top[i * 4 + 0] = (byte)blue[i];
                top[i * 4 + 3] = 255;
            }

            var image = new Image(new List<MipMap> { new MipMap(top, 4, 2, PixelFormat.ARGB) }, PixelFormat.ARGB);
            image.correctMips(PixelFormat.ARGB);

            Assert.AreEqual(3, image.mipMaps.Count, "expected mip chain 4x2/2x1/1x1");
            Assert.AreEqual(1, image.mipMaps[1].data[0], "2x1 left pixel");
            Assert.AreEqual(0, image.mipMaps[1].data[4], "2x1 right pixel");
            Assert.AreEqual(1, image.mipMaps[2].data[0], "1x1: (1 + 0) / 2 = 0.5 should round up");
        }

        /// <summary>The smallest mip's brightness stays within one count of the top mip's true average.</summary>
        [TestMethod]
        public void TestMipChainKeepsAverageBrightness()
        {
            GlobalTest.Init();

            const int size = 64;
            byte[] top = new byte[size * size * 4];
            double sum = 0;
            var random = new System.Random(1234);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Dark, noisy values, like an indirect-light lightmap: where truncation hurts most.
                    byte value = (byte)random.Next(0, 8);
                    int i = (y * size + x) * 4;
                    top[i + 0] = value;
                    top[i + 1] = value;
                    top[i + 2] = value;
                    top[i + 3] = 255;
                    sum += value;
                }
            }
            double mean = sum / (size * size);

            var image = new Image(new List<MipMap> { new MipMap(top, size, size, PixelFormat.ARGB) }, PixelFormat.ARGB);
            image.correctMips(PixelFormat.ARGB);

            var smallest = image.mipMaps[image.mipMaps.Count - 1];
            Assert.AreEqual(1, smallest.origWidth);
            Assert.AreEqual(mean, smallest.data[0], 1.0, "1x1 mip drifted from the true average of the top mip");
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
