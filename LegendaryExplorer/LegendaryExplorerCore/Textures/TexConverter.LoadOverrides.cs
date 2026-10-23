using System;
using System.Runtime.InteropServices;

namespace LegendaryExplorerCore.Textures
{
    public static partial class TexConverter
    {
        [DllImport(TEXCONVERTER_DLL_FILENAME, EntryPoint = "LoadTextureWithOverrides", CharSet = CharSet.Ansi)]
        private static extern unsafe int TCLoadTextureWithOverrides(string filename, TextureBuffer* output, TextureLoadOverrides overrides);

        [DllImport(TEXCONVERTER_DLL_FILENAME, EntryPoint = "LoadTextureFromMemoryWithOverrides")]
        private static extern unsafe int TCLoadTextureFromMemoryWithOverrides(byte* input, nuint length, int imageType, TextureBuffer* output, TextureLoadOverrides overrides);

        /// <summary>Loads with explicit overrides. None uses the original API, including with older native DLLs.</summary>
        public static unsafe byte[] LoadTextureWithOverrides(string filename, out uint width, out uint height, ref PixelFormat pixelFormat, TextureLoadOverrides overrides)
        {
            ValidateLoadOverrides(overrides);
            if (overrides == TextureLoadOverrides.None)
            {
                return LoadTexture(filename, out width, out height, ref pixelFormat);
            }
            EnsureInitialized();
            var output = new TextureBuffer { Format = GetDXGIFormatForPixelFormat(pixelFormat) };
            try
            {
                Marshal.ThrowExceptionForHR(TCLoadTextureWithOverrides(filename, &output, overrides));
            }
            catch (EntryPointNotFoundException ex)
            {
                throw new InvalidOperationException("Texture load overrides require the rebuilt TexConverter.dll with override exports.", ex);
            }
            return CopyOverrideTexture(ref output, out width, out height, ref pixelFormat);
        }

        /// <summary>Memory equivalent of LoadTextureWithOverrides. Image types: 1 DDS, 2 WIC, 3 TGA.</summary>
        public static unsafe byte[] LoadTextureFromMemoryWithOverrides(ReadOnlySpan<byte> buffer, int imageType, out uint width, out uint height, ref PixelFormat pixelFormat, TextureLoadOverrides overrides)
        {
            ValidateLoadOverrides(overrides);
            if (overrides == TextureLoadOverrides.None)
            {
                return LoadTextureFromMemory(buffer, imageType, out width, out height, ref pixelFormat);
            }
            EnsureInitialized();
            var output = new TextureBuffer { Format = GetDXGIFormatForPixelFormat(pixelFormat) };
            try
            {
                fixed (byte* input = buffer)
                {
                    Marshal.ThrowExceptionForHR(TCLoadTextureFromMemoryWithOverrides(input, (nuint)buffer.Length, imageType, &output, overrides));
                }
            }
            catch (EntryPointNotFoundException ex)
            {
                throw new InvalidOperationException("Texture load overrides require the rebuilt TexConverter.dll with override exports.", ex);
            }
            return CopyOverrideTexture(ref output, out width, out height, ref pixelFormat);
        }

        private static void ValidateLoadOverrides(TextureLoadOverrides overrides)
        {
            if ((overrides & ~TextureLoadOverrides.PreserveAllZeroTgaAlpha) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(overrides), overrides, "Unknown texture load override.");
            }
        }

        private static unsafe byte[] CopyOverrideTexture(ref TextureBuffer output, out uint width, out uint height, ref PixelFormat pixelFormat)
        {
            try
            {
                var result = new byte[checked((int)output.PixelDataLength)];
                Marshal.Copy((IntPtr)output.PixelData, result, 0, result.Length);
                width = output.Width;
                height = output.Height;
                pixelFormat = GetPixelFormatForDXGIFormat(output.Format);
                return result;
            }
            finally
            {
                fixed (TextureBuffer* buffer = &output)
                {
                    Marshal.ThrowExceptionForHR(TCFreePixelData(buffer));
                }
            }
        }
    }
}
