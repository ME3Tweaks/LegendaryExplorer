using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LegendaryExplorerCore.Textures
{
    /// <summary>
    /// Builds uncompressed mip chains for <see cref="Image.correctMips"/> when <see cref="MipGenerationOptions"/> ask for
    /// something other than the historical box filter. Everything here works on ARGB byte arrays in this codebase's
    /// layout (B, G, R, A in memory) and returns new arrays; the input is never modified.
    /// </summary>
    internal static class MipGenerator
    {
        /// <summary>Texels with alpha at or below this are "transparent" for <see cref="MipGenerationOptions.PadTransparentColor"/>.</summary>
        private const byte TransparentAlphaThreshold = 2;

        /// <summary>How many texels deep transparent colour is padded.</summary>
        private const int PadPasses = 4;

        /// <summary>
        /// Builds every level of the chain, largest first, down to 1x1. Level 0 is the (possibly padded) top mip.
        /// </summary>
        public static List<byte[]> BuildChain(byte[] topArgb, int width, int height, MipGenerationOptions options)
        {
            var levels = new List<byte[]>();

            // The source every level is built from. Padding it matters for the box filter (which averages straight
            // colour); the Lanczos path weights colour by alpha, so it ignores whatever is under alpha 0.
            byte[] source = options.PadTransparentColor ? PadTransparentColor(topArgb, width, height) : topArgb;
            levels.Add(source);

            int w = width;
            int h = height;
            byte[] previousUnprocessed = source;
            int pw = w;
            int ph = h;
            while (w > 1 || h > 1)
            {
                w = Math.Max(w >> 1, 1);
                h = Math.Max(h >> 1, 1);

                byte[] level;
                if (options.Filter == MipFilter.Lanczos3)
                {
                    level = ResizeLanczos3(source, width, height, w, h);
                }
                else
                {
                    // Box: each level is the average of the unprocessed level above, as it has always been, so that
                    // sharpening one level is not baked into the ones below it.
                    level = Image.downscaleARGB(previousUnprocessed, pw, ph);
                    previousUnprocessed = level;
                    pw = w;
                    ph = h;
                }

                byte[] processed = level;
                if (options.SharpenAmount > 0f && Math.Min(w, h) >= 4)
                {
                    processed = UnsharpMaskPremultiplied(level, w, h, options.SharpenRadius, options.SharpenAmount);
                }

                if (options.PadTransparentColor)
                {
                    processed = PadTransparentColor(processed, w, h);
                }

                levels.Add(processed);
            }

            return levels;
        }

        /// <summary>
        /// Lanczos-3 resize in premultiplied alpha (ImageSharp premultiplies for the resample and un-premultiplies the
        /// result), straight from the full-size source rather than from the previous level.
        /// </summary>
        private static byte[] ResizeLanczos3(byte[] sourceArgb, int sourceWidth, int sourceHeight, int width, int height)
        {
            using var image = SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(sourceArgb, sourceWidth, sourceHeight);
            image.Mutate(c => c.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Lanczos3,
                PremultiplyAlpha = true,
                Compand = false
            }));

            byte[] result = new byte[width * height * 4];
            image.CopyPixelDataTo(result);
            return result;
        }

        /// <summary>
        /// Unsharp mask in premultiplied colour: out = c + amount * (c - blur(c)), with each colour clamped to [0, alpha]
        /// so a sharpened edge can neither go negative nor brighter than its own opacity allows.
        /// </summary>
        internal static byte[] UnsharpMaskPremultiplied(byte[] argb, int w, int h, float radius, float amount)
        {
            int count = w * h;
            var alpha = new float[count];
            var channels = new float[3][];
            for (int c = 0; c < 3; c++)
            {
                channels[c] = new float[count];
            }

            for (int i = 0; i < count; i++)
            {
                float a = argb[i * 4 + 3];
                alpha[i] = a;
                float scale = a / 255f;
                channels[0][i] = argb[i * 4 + 0] * scale;
                channels[1][i] = argb[i * 4 + 1] * scale;
                channels[2][i] = argb[i * 4 + 2] * scale;
            }

            float[] kernel = GaussianKernel(radius);
            var sharpened = new float[3][];
            for (int c = 0; c < 3; c++)
            {
                float[] blurred = Blur(channels[c], w, h, kernel);
                sharpened[c] = new float[count];
                for (int i = 0; i < count; i++)
                {
                    float v = channels[c][i] + amount * (channels[c][i] - blurred[i]);
                    sharpened[c][i] = Math.Clamp(v, 0f, alpha[i]);
                }
            }

            byte[] result = new byte[argb.Length];
            for (int i = 0; i < count; i++)
            {
                float a = alpha[i];
                if (a > 0f)
                {
                    float inv = 255f / a;
                    result[i * 4 + 0] = ToByte(sharpened[0][i] * inv);
                    result[i * 4 + 1] = ToByte(sharpened[1][i] * inv);
                    result[i * 4 + 2] = ToByte(sharpened[2][i] * inv);
                }
                result[i * 4 + 3] = argb[i * 4 + 3];
            }

            return result;
        }

        private static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v), 0, 255);

        private static float[] GaussianKernel(float sigma)
        {
            int radius = Math.Max(1, (int)MathF.Ceiling(sigma * 3f));
            var kernel = new float[radius * 2 + 1];
            float sum = 0f;
            for (int i = -radius; i <= radius; i++)
            {
                float v = MathF.Exp(-(i * i) / (2f * sigma * sigma));
                kernel[i + radius] = v;
                sum += v;
            }

            for (int i = 0; i < kernel.Length; i++)
            {
                kernel[i] /= sum;
            }

            return kernel;
        }

        /// <summary>Separable blur with clamped (edge-replicating) borders.</summary>
        private static float[] Blur(float[] src, int w, int h, float[] kernel)
        {
            int radius = kernel.Length / 2;
            var tmp = new float[src.Length];
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    float acc = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sx = Math.Clamp(x + k, 0, w - 1);
                        acc += src[row + sx] * kernel[k + radius];
                    }

                    tmp[row + x] = acc;
                }
            }

            var dst = new float[src.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float acc = 0f;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sy = Math.Clamp(y + k, 0, h - 1);
                        acc += tmp[sy * w + x] * kernel[k + radius];
                    }

                    dst[y * w + x] = acc;
                }
            }

            return dst;
        }

        /// <summary>
        /// Gives fully transparent texels (alpha &lt;= <see cref="TransparentAlphaThreshold"/>) the average colour of their
        /// visible 8-neighbours, repeated <see cref="PadPasses"/> times so the colour reaches a few texels out.
        /// Alpha is never changed. Returns a new array.
        /// </summary>
        internal static byte[] PadTransparentColor(byte[] argb, int w, int h)
        {
            byte[] result = (byte[])argb.Clone();
            var known = new bool[w * h];
            for (int i = 0; i < known.Length; i++)
            {
                known[i] = argb[i * 4 + 3] > TransparentAlphaThreshold;
            }

            for (int pass = 0; pass < PadPasses; pass++)
            {
                var newlyKnown = new List<int>();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int index = y * w + x;
                        if (known[index])
                        {
                            continue;
                        }

                        int b = 0, g = 0, r = 0, n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int ny = y + dy;
                            if (ny < 0 || ny >= h)
                            {
                                continue;
                            }

                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                if ((dx == 0 && dy == 0) || nx < 0 || nx >= w)
                                {
                                    continue;
                                }

                                int neighbour = ny * w + nx;
                                if (!known[neighbour])
                                {
                                    continue;
                                }

                                b += result[neighbour * 4 + 0];
                                g += result[neighbour * 4 + 1];
                                r += result[neighbour * 4 + 2];
                                n++;
                            }
                        }

                        if (n > 0)
                        {
                            result[index * 4 + 0] = (byte)((b + n / 2) / n);
                            result[index * 4 + 1] = (byte)((g + n / 2) / n);
                            result[index * 4 + 2] = (byte)((r + n / 2) / n);
                            newlyKnown.Add(index);
                        }
                    }
                }

                if (newlyKnown.Count == 0)
                {
                    break;
                }

                // Colour spreads one texel per pass: a texel filled this pass must not feed another texel in the same pass.
                foreach (int index in newlyKnown)
                {
                    known[index] = true;
                }
            }

            return result;
        }
    }
}
