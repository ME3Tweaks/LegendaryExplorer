using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ME3Explorer.Unreal;
using ME3Explorer.Packages;
using Gibbed.IO;
using AmaroK86.ImageFormat;
using MassEffectModder;
using AmaroK86.MassEffect3.ZlibBlock;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using static ME3Explorer.EmbeddedTextureViewer;
using static MassEffectModder.Images.Image;
using System.Windows.Media.Imaging;
using MassEffectModder.Images;
using Buffer = System.Buffer;

namespace ME3Explorer.Unreal.Classes
{
    public class Texture2D
    {
        public List<Texture2DMipInfo> Mips { get; }
        public readonly bool NeverStream;
        public readonly ExportEntry Export;
        public readonly string TextureFormat;
        public Guid TextureGuid;

        public Texture2D(ExportEntry export)
        {
            Export = export;
            PropertyCollection properties = export.GetProperties();
            TextureFormat = properties.GetProp<EnumProperty>("Format").Value.Name;
            var cache = properties.GetProp<NameProperty>("TextureFileCacheName");

            NeverStream = properties.GetProp<BoolProperty>("NeverStream") ?? false;
            Mips = GetTexture2DMipInfos(export, cache?.Value);
            if (Export.Game != MEGame.ME1)
            {
                TextureGuid = new Guid(Export.Data.Skip(Export.Data.Length - 16).Take(16).ToArray());
            }
        }

        public void RemoveEmptyMipsFromMipList()
        {
            Mips.RemoveAll(x => x.storageType == StorageTypes.empty);
        }

        public bool ExportToPNG(string outputPath)
        {
            Texture2DMipInfo info = new Texture2DMipInfo();
            info = Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
            if (info != null)
            {
                byte[] imageBytes = null;
                try
                {
                    imageBytes = GetTextureData(info);
                }
                catch (FileNotFoundException e)
                {
                    Debug.WriteLine("External cache not found. Defaulting to internal mips.");
                    //External archive not found - using built in mips (will be hideous, but better than nothing)
                    info = Mips.FirstOrDefault(x => x.storageType == StorageTypes.pccUnc);
                    if (info != null)
                    {
                        imageBytes = GetTextureData(info);
                    }
                }

                if (imageBytes != null)
                {
                    PixelFormat format = Image.getPixelFormatType(TextureFormat);

                    PngBitmapEncoder image = Image.convertToPng(imageBytes, info.width, info.height, format);
                    using (FileStream fs = new FileStream(outputPath, FileMode.Create))
                    {
                        image.Save(fs);
                    }
                }
            }

            return true;
        }


        /// <summary>
        /// Creates a Direct 3D 11 textured based off the top mip of this Texture2D export
        /// </summary>
        /// <param name="device">Device to render texture from/to ?</param>
        /// <param name="description">Direct3D description of the texture</param>
        /// <returns></returns>
        public SharpDX.Direct3D11.Texture2D generatePreviewTexture(Device device, out Texture2DDescription description, Texture2DMipInfo info = null, byte[] imageBytes = null)
        {
            if (info == null)
            {
                info = new Texture2DMipInfo();
                info = Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
            }
            if (info == null)
            {
                description = new Texture2DDescription();
                return null;
            }


            Debug.WriteLine($"Generating preview texture for Texture2D {info.Export.FullPath} of format {TextureFormat}");
            if (imageBytes == null)
            {
                imageBytes = GetImageBytesForMip(info);
            }
            int width = (int)info.width;
            int height = (int)info.height;
            var fmt = AmaroK86.ImageFormat.DDSImage.convertFormat(TextureFormat);
            var bmp = AmaroK86.ImageFormat.DDSImage.ToBitmap(imageBytes, fmt, info.width, info.height);
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
            description.Format = Format.B8G8R8A8_UNorm;
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

        internal Texture2DMipInfo GetTopMip()
        {
            return Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
        }

        public static List<Texture2DMipInfo> GetTexture2DMipInfos(ExportEntry exportEntry, string cacheName)
        {
            MemoryStream ms = new MemoryStream(exportEntry.Data);
            ms.Seek(exportEntry.propsEnd(), SeekOrigin.Begin);
            if (exportEntry.FileRef.Game != MEGame.ME3)
            {
                ms.Seek(12, SeekOrigin.Current); // 12 zeros
                ms.Seek(4, SeekOrigin.Current); // position in the package
            }

            var mips = new List<Texture2DMipInfo>();
            int numMipMaps = ms.ReadValueS32();
            for (int l = 0; l < numMipMaps; l++)
            {
                Texture2DMipInfo mip = new Texture2DMipInfo
                {
                    Export = exportEntry,
                    index = l,
                    storageType = (StorageTypes)ms.ReadValueS32(),
                    uncompressedSize = ms.ReadValueS32(),
                    compressedSize = ms.ReadValueS32(),
                    externalOffset = ms.ReadValueS32(),
                    localExportOffset = (int)ms.Position,
                    TextureCacheName = cacheName //If this is ME1, this will simply be ignored in the setter
                };
                switch (mip.storageType)
                {
                    case StorageTypes.pccUnc:
                        ms.Seek(mip.uncompressedSize, SeekOrigin.Current);
                        break;
                    case StorageTypes.pccLZO:
                    case StorageTypes.pccZlib:
                        ms.Seek(mip.compressedSize, SeekOrigin.Current);
                        break;
                }

                mip.width = ms.ReadValueS32();
                mip.height = ms.ReadValueS32();
                if (mip.width == 4 && mips.Exists(m => m.width == mip.width))
                    mip.width = mips.Last().width / 2;
                if (mip.height == 4 && mips.Exists(m => m.height == mip.height))
                    mip.height = mips.Last().height / 2;
                if (mip.width == 0)
                    mip.width = 1;
                if (mip.height == 0)
                    mip.height = 1;
                mips.Add(mip);
            }

            return mips;
        }

        public byte[] GetImageBytesForMip(Texture2DMipInfo info)
        {
            byte[] imageBytes = null;
            try
            {
                imageBytes = GetTextureData(info);
            }
            catch (FileNotFoundException e)
            {
                Debug.WriteLine("External cache not found. Defaulting to internal mips.");
                //External archive not found - using built in mips (will be hideous, but better than nothing)
                info = Mips.FirstOrDefault(x => x.storageType == StorageTypes.pccUnc);
                if (info != null)
                {
                    imageBytes = GetTextureData(info);
                }
            }
            if (imageBytes == null)
            {
                throw new Exception("Could not fetch texture data for texture " + info?.Export.ObjectName.Instanced);
            }
            return imageBytes;
        }

        private byte[] GetImageBytes()
        {
            throw new NotImplementedException();
        }

        internal byte[] SerializeNewData()
        {
            MemoryStream ms = new MemoryStream();
            if (Export.FileRef.Game != MEGame.ME3)
            {
                for (int i = 0; i < 12; i++)
                    ms.WriteByte(0); //12 0s
                for (int i = 0; i < 4; i++)
                    ms.WriteByte(0); //position in the package. will be updated later
            }

            ms.WriteValueS32(Mips.Count);
            foreach (var mip in Mips)
            {
                ms.WriteValueU32((uint)mip.storageType);
                ms.WriteValueS32(mip.uncompressedSize);
                ms.WriteValueS32(mip.compressedSize);
                ms.WriteValueS32(mip.externalOffset);
                if (mip.storageType == StorageTypes.pccUnc ||
                    mip.storageType == StorageTypes.pccLZO ||
                    mip.storageType == StorageTypes.pccZlib)
                {
                    ms.Write(mip.newDataForSerializing, 0, mip.newDataForSerializing.Length);
                }
                ms.WriteValueS32(mip.width);
                ms.WriteValueS32(mip.height);
            }
            ms.WriteValueS32(0);
            if (Export.Game != MEGame.ME1)
            {
                ms.WriteValueGuid(TextureGuid);
            }
            return ms.ToArray();
        }

        internal void ReplaceMips(List<Texture2DMipInfo> mipmaps)
        {
            Mips.Clear();
            Mips.AddRange(mipmaps);
        }


        public static byte[] GetTextureData(Texture2DMipInfo mipToLoad, bool decompress = true)
        {
            var imagebytes = new byte[decompress ? mipToLoad.uncompressedSize : mipToLoad.compressedSize];
            Debug.WriteLine("getting texture data for " + mipToLoad.Export.FullPath);
            if (mipToLoad.storageType == StorageTypes.pccUnc)
            {
                Buffer.BlockCopy(mipToLoad.Export.Data, mipToLoad.localExportOffset, imagebytes, 0, mipToLoad.uncompressedSize);
            }
            else if (mipToLoad.storageType == StorageTypes.pccLZO || mipToLoad.storageType == StorageTypes.pccZlib)
            {
                if (decompress)
                {
                    try
                    {
                        TextureCompression.DecompressTexture(imagebytes,
                                                             new MemoryStream(mipToLoad.Export.Data, mipToLoad.localExportOffset, mipToLoad.compressedSize),
                                                             mipToLoad.storageType, mipToLoad.uncompressedSize, mipToLoad.compressedSize);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"{e.Message}\nStorageType: {mipToLoad.storageType}\n");
                    }
                }
                else
                {
                    Buffer.BlockCopy(mipToLoad.Export.Data, mipToLoad.localExportOffset, imagebytes, 0, mipToLoad.compressedSize);
                }
            }
            else if (mipToLoad.storageType == StorageTypes.extUnc || mipToLoad.storageType == StorageTypes.extLZO || mipToLoad.storageType == StorageTypes.extZlib)
            {
                string filename = null;
                List<string> loadedFiles = MEDirectories.EnumerateGameFiles(mipToLoad.Export.Game, MEDirectories.GamePath(mipToLoad.Export.Game));
                if (mipToLoad.Export.Game == MEGame.ME1)
                {
                    var fullPath = loadedFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(mipToLoad.TextureCacheName, StringComparison.InvariantCultureIgnoreCase));
                    if (fullPath != null)
                    {
                        filename = fullPath;
                    }
                    else
                    {
                        throw new FileNotFoundException($"Externally referenced texture file not found in game: {mipToLoad.TextureCacheName}.");
                    }
                }
                else
                {
                    string archive = mipToLoad.TextureCacheName + ".tfc";
                    var localDirectoryTFCPath = Path.Combine(Path.GetDirectoryName(mipToLoad.Export.FileRef.FilePath), archive);
                    if (File.Exists(localDirectoryTFCPath))
                    {
                        filename = localDirectoryTFCPath;
                    }
                    else
                    {
                        var tfcs = loadedFiles.Where(x => x.EndsWith(".tfc")).ToList();

                        var fullPath = loadedFiles.FirstOrDefault(x => Path.GetFileName(x).Equals(archive, StringComparison.InvariantCultureIgnoreCase));
                        if (fullPath != null)
                        {
                            filename = fullPath;
                        }
                        else
                        {
                            throw new FileNotFoundException($"Externally referenced texture cache not found: {archive}.");
                        }
                    }
                }

                //exceptions above will prevent filename from being null here

                try
                {
                    using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                    {
                        try
                        {
                            fs.Seek(mipToLoad.externalOffset, SeekOrigin.Begin);
                            if (mipToLoad.storageType == StorageTypes.extLZO || mipToLoad.storageType == StorageTypes.extZlib)
                            {
                                if (decompress)
                                {
                                    using (MemoryStream tmpStream = new MemoryStream(fs.ReadBytes(mipToLoad.compressedSize)))
                                    {
                                        try
                                        {
                                            TextureCompression.DecompressTexture(imagebytes, tmpStream, mipToLoad.storageType, mipToLoad.uncompressedSize, mipToLoad.compressedSize);
                                        }
                                        catch (Exception e)
                                        {
                                            throw new Exception(e.Message + "\n" + "File: " + filename + "\n" +
                                                                "StorageType: " + mipToLoad.storageType + "\n" +
                                                                "External file offset: " + mipToLoad.externalOffset);
                                        }
                                    }
                                }
                                else
                                {
                                    fs.Read(imagebytes, 0, mipToLoad.compressedSize);
                                }
                            }
                            else
                            {
                                fs.Read(imagebytes, 0, mipToLoad.uncompressedSize);
                            }
                        }
                        catch (Exception e)
                        {
                            throw new Exception(e.Message + "\n" + "File: " + filename + "\n" +
                                "StorageType: " + mipToLoad.storageType + "\n" +
                                "External file offset: " + mipToLoad.externalOffset);
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + "\n" + "File: " + filename + "\n" +
                        "StorageType: " + mipToLoad.storageType + "\n" +
                        "External file offset: " + mipToLoad.externalOffset);
                }
            }
            return imagebytes;
        }

    }

    [DebuggerDisplay("Texture2DMipInfo for {Export.ObjectName.Instanced} | {width}x{height} | {storageType}")]
    public class Texture2DMipInfo
    {
        public ExportEntry Export;
        public bool NeverStream; //copied from parent
        public int index;
        public int uncompressedSize;
        public int compressedSize;
        public int width;
        public int height;
        public int externalOffset;
        public int localExportOffset;
        public StorageTypes storageType;
        private string _textureCacheName;
        public byte[] newDataForSerializing;

        public string TextureCacheName
        {
            get
            {
                if (Export.Game != MEGame.ME1) return _textureCacheName; //ME2/ME3 have property specifying the name. ME1 uses package lookup

                //ME1 externally references the UPKs. I think. It doesn't load external textures from SFMs
                string baseName = Export.FileRef.FollowLink(Export.idxLink).Split('.')[0].ToUpper() + ".upk"; //get top package name

                if (storageType == StorageTypes.extLZO || storageType == StorageTypes.extZlib || storageType == StorageTypes.extUnc)
                {
                    return baseName;
                }

                //NeverStream is set if there are more than 6 mips. Some sort of design implementation of ME1 texture streaming
                if (baseName != "" && !NeverStream)
                {
                    var gameFiles = MELoadedFiles.GetFilesLoadedInGame(MEGame.ME1);
                    if (gameFiles.ContainsKey(baseName)) //I am pretty sure these will only ever resolve to UPKs...
                    {
                        return baseName;
                    }
                }

                return null;
            }
            set => _textureCacheName = value; //This isn't INotifyProperty enabled so we don't need to SetProperty this
        }

        public string MipDisplayString
        {
            get
            {
                string mipinfostring = "Mip " + index;
                mipinfostring += "\nStorage Type: ";
                mipinfostring += storageType;
                if (storageType == StorageTypes.extLZO || storageType == StorageTypes.extZlib || storageType == StorageTypes.extUnc)
                {
                    mipinfostring += "\nLocated in: ";
                    mipinfostring += TextureCacheName ?? "(NULL!)";
                }

                mipinfostring += "\nUncompressed size: ";
                mipinfostring += uncompressedSize;
                mipinfostring += "\nCompressed size: ";
                mipinfostring += compressedSize;
                mipinfostring += "\nOffset: ";
                mipinfostring += externalOffset;
                mipinfostring += "\nWidth: ";
                mipinfostring += width;
                mipinfostring += "\nHeight: ";
                mipinfostring += height;
                return mipinfostring;
            }
        }
    }
}
