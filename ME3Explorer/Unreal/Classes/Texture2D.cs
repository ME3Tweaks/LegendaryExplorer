using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Drawing;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;
using Gibbed.IO;
using AmaroK86.ImageFormat;
using AmaroK86.MassEffect3.ZlibBlock;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace ME3Explorer.Unreal.Classes
{
    public class Texture2D
    {
        public enum storage
        {
            arcCpr = 0x3, // archive compressed
            arcUnc = 0x1, // archive uncompressed (DLC)
            pccSto = 0x0, // pcc local storage
            empty = 0x21  // unused image (void pointer sorta)
        }

        public struct ImageInfo
        {
            public storage storageType;
            public int uncSize;
            public int cprSize;
            public int offset;
            public ImageSize imgSize;
        }

        readonly ME3Package pccRef;
        public const string className = "Texture2D";
        public string texName { get; }
        public string arcName { get; }
        private readonly string texFormat;
        private readonly byte[] imageData;
        public uint pccOffset;
        public List<ImageInfo> imgList { get; } // showable image list

        public Texture2D(ME3Package pccObj, int texIdx)
        {
            pccRef = pccObj;
            // check if texIdx is an Export index and a Texture2D class
            if (pccObj.isExport(texIdx) && pccObj.getExport(texIdx).ClassName == className)
            {
                IExportEntry expEntry = pccObj.getExport(texIdx);
                pccOffset = (uint)expEntry.DataOffset;
                texName = expEntry.ObjectName;

                texFormat = expEntry.GetProperty<EnumProperty>("Format")?.Value.Name.Substring(3) ?? "";
                arcName = expEntry.GetProperty<NameProperty>("TextureFileCacheName")?.Value.Name ?? "";
                int dataOffset = expEntry.propsEnd();
                // if "None" property isn't found throws an exception
                if (dataOffset == 0)
                    throw new Exception("\"None\" property not found");
                byte[] rawData = expEntry.Data;
                imageData = new byte[rawData.Length - dataOffset];
                System.Buffer.BlockCopy(rawData, dataOffset, imageData, 0, imageData.Length);
            }
            else
                throw new Exception($"Texture2D {texIdx} not found");

            MemoryStream dataStream = new MemoryStream(imageData);
            uint numMipMaps = dataStream.ReadValueU32();
            uint count = numMipMaps;

            imgList = new List<ImageInfo>();
            while (dataStream.Position < dataStream.Length && count > 0)
            {
                ImageInfo imgInfo = new ImageInfo
                {
                    storageType = (storage)dataStream.ReadValueS32(),
                    uncSize = dataStream.ReadValueS32(),
                    cprSize = dataStream.ReadValueS32(),
                    offset = dataStream.ReadValueS32()
                };
                if (imgInfo.storageType == storage.pccSto)
                {
                    //imgInfo.offset = (int)(pccOffset + dataOffset); // saving pcc offset as relative to exportdata offset, not absolute
                    imgInfo.offset = (int)dataStream.Position; // saving pcc offset as relative to exportdata offset, not absolute
                    //MessageBox.Show("Pcc class offset: " + pccOffset + "\nimages data offset: " + imgInfo.offset.ToString());
                    dataStream.Seek(imgInfo.uncSize, SeekOrigin.Current);
                }
                imgInfo.imgSize = new ImageSize(dataStream.ReadValueU32(), dataStream.ReadValueU32());
                imgList.Add(imgInfo);
                count--;
            }

            // save what remains
            /*int remainingBytes = (int)(dataStream.Length - dataStream.Position);
            footerData = new byte[remainingBytes];
            dataStream.Read(footerData, 0, footerData.Length);*/
        }

        public static string GetTFC(string arcname)
        {
            if (!arcname.EndsWith(".tfc"))
                arcname += ".tfc";

            foreach (string s in ME3LoadedFiles.GetEnabledDLC().OrderBy(ME3LoadedFiles.GetMountPriority).Append(ME3Directory.BIOGamePath))
            {
                foreach (string file in Directory.EnumerateFiles(Path.Combine(s, "CookedPCConsole")))
                {
                    if (Path.GetFileName(file) == arcname)
                    {
                        return file;
                    }
                }
            }
            return "";
        }

        public byte[] extractRawData(ImageInfo imgInfo, string archiveDir = null)
        {
            byte[] imgBuffer;

            switch (imgInfo.storageType)
            {
                case storage.pccSto:
                    imgBuffer = new byte[imgInfo.uncSize];
                    System.Buffer.BlockCopy(imageData, imgInfo.offset, imgBuffer, 0, imgInfo.uncSize);
                    break;
                case storage.arcCpr:
                case storage.arcUnc:
                    string archivePath;
                    if (archiveDir != null && File.Exists(Path.Combine(archiveDir, arcName)))
                    {
                        archivePath = Path.Combine(archiveDir, arcName);
                    }
                    else
                    {
                        archivePath = GetTFC(arcName);
                    }
                    if (archivePath != null && File.Exists(archivePath))
                    {
                        Console.WriteLine($"Loaded texture from tfc '{archivePath}'.");

                        using (FileStream archiveStream = File.OpenRead(archivePath))
                        {
                            archiveStream.Seek(imgInfo.offset, SeekOrigin.Begin);
                            if (imgInfo.storageType == storage.arcCpr)
                            {
                                imgBuffer = ZBlock.Decompress(archiveStream, imgInfo.cprSize);
                            }
                            else
                            {
                                imgBuffer = new byte[imgInfo.uncSize];
                                archiveStream.Read(imgBuffer, 0, imgBuffer.Length);
                            }
                        }
                    } else
                    {
                        //how do i put default unreal texture
                        imgBuffer = null; //this will cause exception that will bubble up.
                    }
                    
                    break;
                default:
                    throw new FormatException("Unsupported texture storage type");
            }
            return imgBuffer; //cannot be uninitialized.
        }

        // Creates a Direct3D texture that looks like this one.
        public SharpDX.Direct3D11.Texture2D generatePreviewTexture(Device device, out Texture2DDescription description)
        {
            ImageInfo info = new ImageInfo();
            foreach (ImageInfo i in imgList)
            {
                if (i.storageType != storage.empty)
                {
                    info = i;
                    break;
                }
            }

            int width = (int)info.imgSize.width;
            int height = (int)info.imgSize.height;
            Console.WriteLine($"Generating preview texture for Texture2D of format {texFormat}");

            // Convert compressed image data to an A8R8G8B8 System.Drawing.Bitmap
            DDSFormat format;
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
                default:
                    throw new FormatException("Unknown ME3 texture format");
            }

            byte[] compressedData = extractRawData(info, Path.GetDirectoryName(pccRef.FilePath));
            Bitmap bmp = DDSImage.ToBitmap(compressedData, format, width, height);

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
            description.Format = dxformat;
            description.SampleDescription.Count = 1;
            description.SampleDescription.Quality = 0;
            description.Usage = ResourceUsage.Default;
            description.BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget;
            description.CpuAccessFlags = 0;
            description.OptionFlags = ResourceOptionFlags.GenerateMipMaps;

            // Set up the texture data
            int stride = width * 4; 
            DataStream ds = new DataStream(height * stride, true, true);
            ds.Write(pixels, 0, height * stride);
            ds.Position = 0;
            // Create texture
            SharpDX.Direct3D11.Texture2D tex = new SharpDX.Direct3D11.Texture2D(device, description, new DataRectangle(ds.DataPointer, stride));
            ds.Dispose();

            return tex;
        }
    }
}
