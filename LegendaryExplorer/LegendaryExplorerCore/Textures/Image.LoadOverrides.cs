using System.Collections.Generic;

namespace LegendaryExplorerCore.Textures
{
    public partial class Image
    {
        /// <summary>Opt-in loading for packed texture data; the original LoadFromFile path is unchanged.</summary>
        public static Image LoadFromFileWithOverrides(string filename, PixelFormat targetFormat, TextureLoadOverrides overrides)
        {
            var pixels = TexConverter.LoadTextureWithOverrides(filename, out var width, out var height, ref targetFormat, overrides);
            return new Image(new List<MipMap> { new MipMap(pixels, (int)width, (int)height, targetFormat) }, targetFormat);
        }

        /// <summary>Opt-in memory loading. Image types: 1 DDS, 2 WIC, 3 TGA.</summary>
        public static Image LoadFromFileMemoryWithOverrides(byte[] buffer, int imageType, PixelFormat targetFormat, TextureLoadOverrides overrides)
        {
            var pixels = TexConverter.LoadTextureFromMemoryWithOverrides(buffer, imageType, out var width, out var height, ref targetFormat, overrides);
            return new Image(new List<MipMap> { new MipMap(pixels, (int)width, (int)height, targetFormat) }, targetFormat);
        }
    }
}
