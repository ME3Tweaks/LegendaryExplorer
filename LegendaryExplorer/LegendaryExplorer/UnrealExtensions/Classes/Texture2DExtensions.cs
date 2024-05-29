using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Textures;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using Image = LegendaryExplorerCore.Textures.Image;

namespace LegendaryExplorer.UnrealExtensions.Classes
{
    public static class Texture2DExtensions
    {

        /*
        /// <summary>
        /// Creates a Direct 3D 11 textured based off the top mip of this Texture2D export
        /// </summary>
        /// <param name="device">Device to render texture from/to ?</param>
        /// <param name="description">Direct3D description of the texture</param>
        /// <returns></returns>
        public static SharpDX.Direct3D11.Texture2D generatePreviewTexture(this Texture2D t2d, SharpDX.Direct3D11.Device device, out SharpDX.Direct3D11.Texture2DDescription description, Texture2DMipInfo info = null, byte[] imageBytes = null)
        {
            if (info == null)
            {
                info = new Texture2DMipInfo();
                info = t2d.Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
            }
            if (info == null)
            {
                description = new SharpDX.Direct3D11.Texture2DDescription();
                return null;
            }


            Debug.WriteLine($"Generating preview texture for Texture2D {info.Export.FullPath} of format {t2d.TextureFormat}");
            imageBytes ??= t2d.GetImageBytesForMip(info, t2d.Export.Game, true, usedMip: out info);
            int width = (int)info.width;
            int height = (int)info.height;
            var bmp = Image.convertRawToBitmapARGB(imageBytes, info.width, info.height, Image.getPixelFormatType(t2d.TextureFormat));

            // Convert compressed image data to an A8R8G8B8 System.Drawing.Bitmap
            /* DDSFormat format;
            const Format dxformat = Format.B8G8R8A8_UNorm;
            switch (texFormat)
            {
                case "DXT1":
                    format = DDSFormat.DXT1;
                    break;
                case "DXT5":
                    format = DDSFormat.DXT5;
                    break;
                case "V8U8":
                    format = DDSFormat.V8U8;
                    break;
                case "G8":
                    format = DDSFormat.G8;
                    break;
                case "A8R8G8B8":
                    format = DDSFormat.ARGB;
                    break;
                case "NormalMap_HQ":
                    format = DDSFormat.ATI2;
                    break;
                default:
                    throw new FormatException("Unknown texture format: " + texFormat);
            }

            byte[] compressedData = extractRawData(info, pccRef);
            Bitmap bmp = DDSImage.ToBitmap(compressedData, format, width, height); */
        /*
        // Load the decompressed data into an array
        System.Drawing.Imaging.BitmapData data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var pixels = new byte[data.Stride * data.Height];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            bmp.UnlockBits(data);

            // Create description of texture
            description.Width = width;
            description.Height = height;
            description.MipLevels = 1;
            description.ArraySize = 1;
            description.Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm;
            description.SampleDescription.Count = 1;
            description.SampleDescription.Quality = 0;
            description.Usage = SharpDX.Direct3D11.ResourceUsage.Default;
            description.BindFlags = SharpDX.Direct3D11.BindFlags.ShaderResource | SharpDX.Direct3D11.BindFlags.RenderTarget;
            description.CpuAccessFlags = 0;
            description.OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.GenerateMipMaps;

            // Set up the texture data
            int stride = width * 4;
        var ds = new SharpDX.DataStream(height * stride, true, true);
        ds.Write(pixels, 0, height* stride);
        ds.Position = 0;
            // Create texture
            var tex = new SharpDX.Direct3D11.Texture2D(device, description, new SharpDX.DataRectangle(ds.DataPointer, stride));
        ds.Dispose();

            return tex;
        }
    */
    }
}