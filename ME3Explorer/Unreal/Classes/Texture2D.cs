using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using static ME3Explorer.EmbeddedTextureViewer;

namespace ME3Explorer.Unreal.Classes
{
    public class Texture2D
    {
        public List<Texture2DMipInfo> Mips { get; }
        public readonly bool NeverStream;
        public readonly ExportEntry Export;
        private readonly string TextureFormat;

        public Texture2D(ExportEntry export)
        {
            Export = export;
            PropertyCollection properties = export.GetProperties();
            TextureFormat = properties.GetProp<EnumProperty>("Format").Value.Name;
            var cache = properties.GetProp<NameProperty>("TextureFileCacheName");

            NeverStream = properties.GetProp<BoolProperty>("NeverStream") ?? false;
            Mips = GetTexture2DMipInfos(export, cache?.Value);

            /*
            //pccRef = pccObj;
            //// check if texIdx is an Export index and a Texture2D class
            //if (pccObj.isUExport(texIdx + 1) && pccObj.getExport(texIdx).ClassName == className)
            //{
            //    textureExport = pccObj.getExport(texIdx);
            //    pccOffset = (uint)textureExport.DataOffset;
            //    texName = textureExport.ObjectName;

            //    texFormat = textureExport.GetProperty<EnumProperty>("Format")?.Value.Name.Substring(3) ?? "";
            //    arcName = textureExport.GetProperty<NameProperty>("TextureFileCacheName")?.Value.Name ?? "";
            //    int dataOffset = textureExport.propsEnd();
            //    // if "None" property isn't found throws an exception
            //    if (dataOffset == 0)
            //        throw new Exception("\"None\" property not found");
            //    imageData = textureExport.Data;
            //}
            //else
            //    throw new Exception($"Texture2D {texIdx} not found");

            //MemoryStream dataStream = new MemoryStream(imageData);
            //dataStream.Position = textureExport.propsEnd(); //scroll to binary
            //if (pccObj.Game != MEGame.ME3)
            //{
            //    dataStream.Position += 16; //12 zeros, file offset
            //}
            //uint numMipMaps = dataStream.ReadValueU32();
            //uint count = numMipMaps;

            //imgList = new List<Texture2DMipInfo>();
            //while (dataStream.Position < dataStream.Length && count > 0)
            //{
            //    var imgInfo = new Texture2DMipInfo
            //    {
            //        storageType = (StorageTypes)dataStream.ReadValueS32(),
            //        uncompressedSize = dataStream.ReadValueS32(),
            //        compressedSize = dataStream.ReadValueS32(),
            //        externalOffset = dataStream.ReadValueS32(),
            //        localExportOffset = (int)dataStream.Position
            //    };

            //    //if locally stored, skip to next mip info
            //    if (imgInfo.storageType == StorageTypes.pccUnc)
            //    {
            //        dataStream.Seek(imgInfo.uncompressedSize, SeekOrigin.Current);
            //    }
            //    else if (imgInfo.storageType == StorageTypes.pccLZO || imgInfo.storageType == StorageTypes.pccZlib)
            //    {
            //        dataStream.Seek(imgInfo.compressedSize, SeekOrigin.Current);
            //    }
            //    imgInfo.imgSize = new ImageSize(dataStream.ReadValueU32(), dataStream.ReadValueU32());

            //    /* We might want to implement this. this is from mem code
            //    if (mip.width == 4 && mips.Exists(m => m.width == mip.width))
            //        mip.width = mips.Last().width / 2;
            //    if (mip.height == 4 && mips.Exists(m => m.height == mip.height))
            //        mip.height = mips.Last().height / 2;
            //    if (mip.width == 0)
            //        mip.width = 1;
            //    if (mip.height == 0)
            //        mip.height = 1;
            //     */

            //    imgList.Add(imgInfo);
            //    count--;
            //}

            //// save what remains
            ///int remainingBytes = (int)(dataStream.Length - dataStream.Position);
            //footerData = new byte[remainingBytes];
            //dataStream.Read(footerData, 0, footerData.Length);*/
        }

        //public static string GetTFC(string arcname, MEGame game)
        //{
        //    if (!arcname.EndsWith(".tfc"))
        //        arcname += ".tfc";

        //    foreach (string s in MELoadedFiles.GetEnabledDLC(game).OrderBy(dir => MELoadedFiles.GetMountPriority(dir, game)).Append(MEDirectories.BioGamePath(game)))
        //    {
        //        foreach (string file in Directory.EnumerateFiles(Path.Combine(s, game == MEGame.ME2 ? "CookedPC" : "CookedPCConsole")))
        //        {
        //            if (Path.GetFileName(file) == arcname)
        //            {
        //                return file;
        //            }
        //        }
        //    }
        //    return "";
        //}

        //public byte[] extractRawData(Texture2DMipInfo imgInfo, IMEPackage package = null)
        //{
        //    byte[] imgBuffer;
        //    string archiveDir = null;
        //    if (package != null) archiveDir = Path.GetDirectoryName(package.FilePath);
        //    switch (imgInfo.storageType)
        //    {
        //        case StorageTypes.pccUnc:
        //            imgBuffer = new byte[imgInfo.uncSize];
        //            System.Buffer.BlockCopy(imageData, imgInfo.offset, imgBuffer, 0, imgInfo.uncSize);
        //            break;
        //        case StorageTypes.pccLZO:
        //        case StorageTypes.pccZlib:
        //            imgBuffer = new byte[imgInfo.uncSize];
        //            using (MemoryStream tmpStream = new MemoryStream(textureExport.Data, (int)imgInfo.inExportDataOffset, imgInfo.cprSize)) //pcc stored don't use the direct offsets
        //            {
        //                try
        //                {
        //                    TextureCompression.DecompressTexture(imgBuffer, tmpStream, imgInfo.storageType, imgInfo.uncSize, imgInfo.cprSize);
        //                }
        //                catch (Exception e)
        //                {
        //                    throw new Exception(e.Message + "\nError decompressing texture.");
        //                }
        //            }


        //            break;
        //        case StorageTypes.extUnc:
        //        case StorageTypes.extZlib:
        //        case StorageTypes.extLZO:
        //            if (pccRef.Game == MEGame.ME1)
        //            {
        //                //UPK Lookup
        //                IEntry parent = matImport.Parent;
        //                while (parent.HasParent)
        //                {
        //                    parent = parent.Parent;
        //                }
        //                var loadedPackages = MELoadedFiles.GetFilesLoadedInGame(MEGame.ME1);
        //                if (loadedPackages.TryGetValue(parent.ObjectName, out string packagePath))
        //                {
        //                    packageFilename = packagePath;
        //                }
        //                else
        //                {
        //                    throw new Exception("Cannot find referenced package file: " + parent.ObjectName);
        //                }
        //            }
        //            else
        //            {
        //                //TFC Lookup
        //                string archivePath;
        //                imgBuffer = new byte[imgInfo.uncSize];
        //                if (archiveDir != null && File.Exists(Path.Combine(archiveDir, arcName)))
        //                {
        //                    archivePath = Path.Combine(archiveDir, arcName);
        //                }
        //                else
        //                {
        //                    archivePath = GetTFC(arcName, package.Game);
        //                }

        //                if (archivePath != null && File.Exists(archivePath))
        //                {
        //                    Debug.WriteLine($"Loading texture from tfc '{archivePath}'.");
        //                    try
        //                    {
        //                        using (FileStream archiveStream = File.OpenRead(archivePath))
        //                        {
        //                            archiveStream.Seek(imgInfo.offset, SeekOrigin.Begin);
        //                            if (imgInfo.storageType == StorageTypes.extZlib || imgInfo.storageType == StorageTypes.extLZO)
        //                            {

        //                                using (MemoryStream tmpStream = new MemoryStream(archiveStream.ReadBytes(imgInfo.cprSize)))
        //                                {
        //                                    try
        //                                    {
        //                                        TextureCompression.DecompressTexture(imgBuffer, tmpStream, imgInfo.storageType, imgInfo.uncSize, imgInfo.cprSize);
        //                                    }
        //                                    catch (Exception e)
        //                                    {
        //                                        throw new Exception(e.Message + "\n" + "File: " + archivePath + "\n" +
        //                                                            "StorageType: " + imgInfo.storageType + "\n" +
        //                                                            "External file offset: " + imgInfo.offset);
        //                                    }
        //                                }
        //                            }
        //                            else
        //                            {
        //                                archiveStream.Read(imgBuffer, 0, imgBuffer.Length);
        //                            }
        //                        }
        //                    }
        //                    catch (Exception e)
        //                    {
        //                        //how do i put default unreal texture
        //                        imgBuffer = null; //this will cause exception that will bubble up.
        //                        throw new Exception(e.Message + "\n" + "File: " + archivePath + "\n" +
        //                                            "StorageType: " + imgInfo.storageType + "\n" +
        //                                            "External file offset: " + imgInfo.offset);
        //                    }
        //                }
        //            }
        //            break;
        //        default:
        //            throw new FormatException("Unsupported texture storage type: " + imgInfo.storageType);
        //    }
        //    return imgBuffer; //cannot be uninitialized.
        //}

        /// <summary>
        /// Creates a Direct 3D 11 textured based off the top mip of this Texture2D export
        /// </summary>
        /// <param name="device">Device to render texture from/to ?</param>
        /// <param name="description">Direct3D description of the texture</param>
        /// <returns></returns>
        public SharpDX.Direct3D11.Texture2D generatePreviewTexture(Device device, out Texture2DDescription description)
        {
            Texture2DMipInfo info = new Texture2DMipInfo();
            info = Mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);
            if (info == null)
            {
                description = new Texture2DDescription();
                return null;
            }

            
            Debug.WriteLine($"Generating preview texture for Texture2D {info.Export.GetFullPath} of format {TextureFormat}");

            byte[] imageBytes = null;
            try
            {
                imageBytes = GetTextureData(info);
            }
            catch (FileNotFoundException e)
            {
                //External archive not found - using built in mips (will be hideous, but better than nothing)
                info = Mips.FirstOrDefault(x => x.storageType == StorageTypes.pccUnc);
                if (info != null)
                {
                    imageBytes = GetTextureData(info);
                }
            }
            if (imageBytes == null)
            {
                throw new Exception("Could not fetch texture for 3D preview");
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
    }
}
