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

        /// <summary>Null, and options that ask for nothing, must give exactly the historical chain.</summary>
        [TestMethod]
        public void TestDefaultOptionsMatchHistoricalChain()
        {
            GlobalTest.Init();

            Image Build(MipGenerationOptions options)
            {
                var image = new Image(new List<MipMap> { new MipMap(CreateGradientARGB(32, 32), 32, 32, PixelFormat.ARGB) }, PixelFormat.ARGB);
                image.correctMips(PixelFormat.ARGB, options: options);
                return image;
            }

            var expected = Build(null);
            foreach (var options in new[] { MipGenerationOptions.Default, new MipGenerationOptions { SharpenRadius = 3f } })
            {
                var actual = Build(options);
                Assert.AreEqual(expected.mipMaps.Count, actual.mipMaps.Count);
                for (int i = 0; i < expected.mipMaps.Count; i++)
                {
                    CollectionAssert.AreEqual(expected.mipMaps[i].data, actual.mipMaps[i].data, $"mip {i} differs");
                }
            }
        }

        [TestMethod]
        public void TestLanczosChainHasEveryLevel()
        {
            GlobalTest.Init();

            var image = new Image(new List<MipMap> { new MipMap(CreateGradientARGB(64, 32), 64, 32, PixelFormat.ARGB) }, PixelFormat.ARGB);
            image.correctMips(PixelFormat.ARGB, options: MipGenerationOptions.HighQuality);

            // 64x32, 32x16, 16x8, 8x4, 4x2, 2x1, 1x1
            Assert.AreEqual(7, image.mipMaps.Count);
            int w = 64, h = 32;
            foreach (var mip in image.mipMaps)
            {
                Assert.AreEqual(w, mip.origWidth);
                Assert.AreEqual(h, mip.origHeight);
                Assert.AreEqual(w * h * 4, mip.data.Length);
                w = System.Math.Max(w / 2, 1);
                h = System.Math.Max(h / 2, 1);
            }
            Assert.IsTrue(image.HasCompleteMipChain());
        }

        /// <summary>
        /// A white cut-out on transparent black: Lanczos works in premultiplied alpha, so the half-covered edge
        /// texels stay white (just less opaque). The box filter averages straight colour and turns them grey.
        /// </summary>
        [TestMethod]
        public void TestLanczosKeepsEdgeColourOfCutouts()
        {
            GlobalTest.Init();

            const int size = 16;
            const int whiteColumns = 11; // odd, so the boundary falls inside a 2x2 block of the next level
            byte[] top = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < whiteColumns; x++)
                {
                    int i = (y * size + x) * 4;
                    top[i + 0] = top[i + 1] = top[i + 2] = top[i + 3] = 255;
                }
            }

            Image Build(MipGenerationOptions options)
            {
                var image = new Image(new List<MipMap> { new MipMap((byte[])top.Clone(), size, size, PixelFormat.ARGB) }, PixelFormat.ARGB);
                image.correctMips(PixelFormat.ARGB, options: options);
                return image;
            }

            int boundaryX = whiteColumns / 2; // column of the 8x8 mip that straddles white and transparent
            var lanczos = Build(new MipGenerationOptions { Filter = MipFilter.Lanczos3 }).mipMaps[1];
            var box = Build(null).mipMaps[1];

            int row = 4;
            int li = (row * 8 + boundaryX) * 4;
            Assert.IsTrue(lanczos.data[li + 3] > 16 && lanczos.data[li + 3] < 240, $"expected a half transparent edge texel, alpha was {lanczos.data[li + 3]}");
            Assert.IsGreaterThanOrEqualTo((byte)250, lanczos.data[li + 0], "Lanczos edge colour should stay white");
            Assert.IsGreaterThanOrEqualTo((byte)250, lanczos.data[li + 1]);
            Assert.IsGreaterThanOrEqualTo((byte)250, lanczos.data[li + 2]);

            Assert.IsLessThan((byte)200, box.data[li + 0], "the straight-colour box filter is expected to darken the edge");
        }

        [TestMethod]
        public void TestSharpenAddsDetailAndLeavesAlphaAlone()
        {
            GlobalTest.Init();

            const int size = 32;
            byte[] top = new byte[size * size * 4];
            var random = new System.Random(77);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = (y * size + x) * 4;
                    top[i + 0] = (byte)random.Next(40, 200);
                    top[i + 1] = (byte)random.Next(40, 200);
                    top[i + 2] = (byte)random.Next(40, 200);
                    top[i + 3] = (byte)(100 + (x * 4)); // 100..224, varies so alpha handling is exercised
                }
            }

            Image Build(float sharpen)
            {
                var image = new Image(new List<MipMap> { new MipMap((byte[])top.Clone(), size, size, PixelFormat.ARGB) }, PixelFormat.ARGB);
                image.correctMips(PixelFormat.ARGB, options: new MipGenerationOptions { Filter = MipFilter.Lanczos3, SharpenAmount = sharpen });
                return image;
            }

            double Energy(MipMap mip)
            {
                double sum = 0;
                int w = mip.origWidth;
                for (int y = 0; y < mip.origHeight; y++)
                {
                    for (int x = 1; x < w; x++)
                    {
                        int a = (y * w + x) * 4;
                        int b = (y * w + x - 1) * 4;
                        sum += System.Math.Abs(mip.data[a] - mip.data[b]) + System.Math.Abs(mip.data[a + 1] - mip.data[b + 1]) + System.Math.Abs(mip.data[a + 2] - mip.data[b + 2]);
                    }
                }
                return sum;
            }

            var plain = Build(0f);
            var sharp = Build(0.8f);
            Assert.IsGreaterThan(Energy(plain.mipMaps[1]), Energy(sharp.mipMaps[1]), "sharpening should increase high frequency detail");

            for (int level = 1; level < plain.mipMaps.Count; level++)
            {
                for (int i = 3; i < plain.mipMaps[level].data.Length; i += 4)
                {
                    Assert.AreEqual(plain.mipMaps[level].data[i], sharp.mipMaps[level].data[i], $"mip {level}: sharpening must not touch alpha");
                }
            }
        }

        /// <summary>
        /// Without padding, the black under transparent texels is averaged into the colour of the edge; with it, the
        /// edge keeps the colour of the art next to it.
        /// </summary>
        [TestMethod]
        public void TestPadTransparentColorKeepsEdgeColourInBoxChain()
        {
            GlobalTest.Init();

            const int size = 16;
            const int redColumns = 7;
            byte[] top = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < redColumns; x++)
                {
                    int i = (y * size + x) * 4;
                    top[i + 2] = 220; // R
                    top[i + 3] = 255;
                }
            }

            int edge = (4 * 8 + redColumns / 2) * 4; // 8x8 mip, row 4: column 3 covers one red and one transparent source column

            Image Build(bool pad)
            {
                var image = new Image(new List<MipMap> { new MipMap((byte[])top.Clone(), size, size, PixelFormat.ARGB) }, PixelFormat.ARGB);
                image.correctMips(PixelFormat.ARGB, options: new MipGenerationOptions { PadTransparentColor = pad });
                return image;
            }

            var unpadded = Build(false).mipMaps[1];
            var padded = Build(true).mipMaps[1];
            Assert.IsLessThan((byte)150, unpadded.data[edge + 2], "unpadded edge should be darkened by the transparent black");
            Assert.IsGreaterThanOrEqualTo((byte)200, padded.data[edge + 2], "padded edge should keep the red of the art");
            Assert.AreEqual(unpadded.data[edge + 3], padded.data[edge + 3], 2, "alpha must not change");
        }

        [TestMethod]
        public void TestHasCompleteMipChain()
        {
            GlobalTest.Init();

            var image = new Image(new List<MipMap> { new MipMap(CreateGradientARGB(16, 16), 16, 16, PixelFormat.ARGB) }, PixelFormat.ARGB);
            Assert.IsFalse(image.HasCompleteMipChain(), "a single level is not a chain");

            image.correctMips(PixelFormat.ARGB);
            Assert.IsTrue(image.HasCompleteMipChain());

            var missingLast = new Image(new List<MipMap>(image.mipMaps.Take(image.mipMaps.Count - 1)), PixelFormat.ARGB);
            Assert.IsFalse(missingLast.HasCompleteMipChain(), "a chain that stops before 1x1 is incomplete");

            var wrongSizes = new Image(new List<MipMap>
            {
                new MipMap(CreateGradientARGB(16, 16), 16, 16, PixelFormat.ARGB),
                new MipMap(CreateGradientARGB(4, 4), 4, 4, PixelFormat.ARGB),
            }, PixelFormat.ARGB);
            Assert.IsFalse(wrongSizes.HasCompleteMipChain());
        }

        /// <summary>
        /// ConvertMipsTo must carry each level's own pixels across, not rebuild the chain from the top level:
        /// every level here is a different flat colour, so a regenerated chain would make them all the same.
        /// </summary>
        [TestMethod]
        public void TestConvertMipsToKeepsEveryLevel()
        {
            GlobalTest.Init();

            foreach (var format in new[] { PixelFormat.DXT5, PixelFormat.BC7 })
            {
                var levels = new List<MipMap>();
                byte[] colours = { 10, 60, 110, 160, 210 };
                int size = 16;
                for (int i = 0; i < colours.Length; i++, size /= 2)
                {
                    byte[] data = new byte[size * size * 4];
                    for (int p = 0; p < size * size; p++)
                    {
                        data[p * 4 + 0] = colours[i];
                        data[p * 4 + 1] = colours[i];
                        data[p * 4 + 2] = colours[i];
                        data[p * 4 + 3] = 255;
                    }
                    levels.Add(new MipMap(data, size, size, PixelFormat.ARGB));
                }

                var image = new Image(levels, PixelFormat.ARGB);
                Assert.IsTrue(image.HasCompleteMipChain());
                image.ConvertMipsTo(format);

                Assert.AreEqual(format, image.pixelFormat);
                Assert.AreEqual(colours.Length, image.mipMaps.Count);
                for (int i = 0; i < colours.Length; i++)
                {
                    var mip = image.mipMaps[i];
                    Assert.AreEqual(format, image.pixelFormat);
                    int w = mip.width, h = mip.height;
                    byte[] argb = Image.convertRawToARGB(mip.data, ref w, ref h, format);
                    Assert.AreEqual(colours[i], argb[0], 4, $"{format}: level {i} lost its own colour");
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
