using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using AmaroK86.ImageFormat;
using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for EmbeddedTextureViewer.xaml
    /// </summary>
    public partial class EmbeddedTextureViewer : ExportLoaderControl
    {
        public ObservableCollectionExtended<Texture2DMipInfo> MipList { get; } = new ObservableCollectionExtended<Texture2DMipInfo>();
        private string CurrentLoadedFormat;
        private string CurrentLoadedCacheName;
        private string CurrentLoadedBasePackageName;

        private string _cannotShowTextureText;
        public string CannotShowTextureText
        {
            get => _cannotShowTextureText;
            set => SetProperty(ref _cannotShowTextureText, value);
        }

        private Visibility _cannotShowTextureTextVisibility;
        public Visibility CannotShowTextureTextVisibility
        {
            get => _cannotShowTextureTextVisibility;
            set => SetProperty(ref _cannotShowTextureTextVisibility, value);
        }

        public EmbeddedTextureViewer()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Embedded Texture Viewer Export Loader", new WeakReference(this));

            DataContext = this;
            CannotShowTextureText = "Select a mip to view";
            CannotShowTextureTextVisibility = Visibility.Visible;
            LoadCommands();
            InitializeComponent();
        }

        public ICommand ExportToPNGCommand { get; set; }
        private void LoadCommands()
        {
            ExportToPNGCommand = new GenericCommand(ExportToPNG, NonEmptyMipSelected);
        }

        private void ExportToPNG()
        {
            SaveFileDialog d = new SaveFileDialog
            {
                Filter = "PNG files|*.png",
                FileName = CurrentLoadedExport.ObjectName + ".png"
            };
            if (d.ShowDialog() == true)
            {
                Texture2D t2d = new Texture2D(CurrentLoadedExport);
                t2d.ExportToPNG(d.FileName);
            }

        }

        private bool NonEmptyMipSelected()
        {
            if (MipList.Count > 0 && Mips_ListBox.SelectedIndex >= 0)
            {
                return MipList[Mips_ListBox.SelectedIndex].storageType != StorageTypes.empty;
            }
            return false;
        }

        public override bool CanParse(ExportEntry exportEntry) => exportEntry.IsTexture();

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new EmbeddedTextureViewer(), CurrentLoadedExport)
                {
                    Title = $"Texture Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.GetFullPath}_{CurrentLoadedExport.indexValue} - {Pcc.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            TextureImage.Source = null;
            try
            {
                PropertyCollection properties = exportEntry.GetProperties();
                var format = properties.GetProp<EnumProperty>("Format");
                var cache = properties.GetProp<NameProperty>("TextureFileCacheName");
                if (cache != null)
                {
                    CurrentLoadedCacheName = cache.Value.Name;
                }
                var neverStream = properties.GetProp<BoolProperty>("NeverStream") ?? false;

                List<Texture2DMipInfo> mips = GetTexture2DMipInfos(exportEntry, CurrentLoadedCacheName);

                var topmip = mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);

                if (exportEntry.FileRef.Game == MEGame.ME1)
                {
                    string baseName = exportEntry.FileRef.FollowLink(exportEntry.idxLink).Split('.')[0].ToUpper();
                    if (mips.Exists(s => s.storageType == StorageTypes.extLZO) ||
                        mips.Exists(s => s.storageType == StorageTypes.extZlib) ||
                        mips.Exists(s => s.storageType == StorageTypes.extUnc))
                    {
                        CurrentLoadedBasePackageName = baseName;
                    }
                    else
                    {
                        if (baseName != "" && !neverStream)
                        {
                            List<string> gameFiles = MEDirectories.EnumerateGameFiles(MEGame.ME1, ME1Directory.gamePath);
                            if (gameFiles.Exists(s => System.IO.Path.GetFileNameWithoutExtension(s).ToUpperInvariant() == baseName))
                            {
                                CurrentLoadedBasePackageName = baseName;
                            }
                        }
                    }
                }

                CurrentLoadedExport = exportEntry;
                CurrentLoadedFormat = format.Value.Name;
                MipList.ReplaceAll(mips);
                if (Properties.Settings.Default.EmbeddedTextureViewer_AutoLoad)
                {
                    Mips_ListBox.SelectedIndex = MipList.IndexOf(topmip);
                }
                //
                //LoadMip(topmip);
            }
            catch (Exception)
            {
                //Error loading texture
            }
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
            int numMipMaps = ms.ReadInt32();
            for (int l = 0; l < numMipMaps; l++)
            {
                Texture2DMipInfo mip = new Texture2DMipInfo
                {
                    Export = exportEntry,
                    index = l,
                    storageType = (StorageTypes)ms.ReadInt32(),
                    uncompressedSize = ms.ReadInt32(),
                    compressedSize = ms.ReadInt32(),
                    externalOffset = ms.ReadInt32(),
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

                mip.width = ms.ReadInt32();
                mip.height = ms.ReadInt32();
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

        private void LoadMip(Texture2DMipInfo mipToLoad)
        {
            if (mipToLoad == null)
            {
                TextureImage.Source = null;
                CannotShowTextureText = "Select a mip to view";
                CannotShowTextureTextVisibility = Visibility.Visible;
                return;
            }

            if (mipToLoad.storageType == StorageTypes.empty)
            {
                TextureImage.Source = null;
                CannotShowTextureText = "Selected mip is null/empty";
                CannotShowTextureTextVisibility = Visibility.Visible;
                return;
            }
            TextureImage.Source = null;
            var imagebytes = GetTextureData(mipToLoad);

            CannotShowTextureTextVisibility = Visibility.Collapsed;
            var fmt = AmaroK86.ImageFormat.DDSImage.convertFormat(CurrentLoadedFormat);
            var bitmap = AmaroK86.ImageFormat.DDSImage.ToBitmap(imagebytes, fmt, mipToLoad.width, mipToLoad.height);
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                TextureImage.Source = bitmapImage; //image1 is your control            }
            }
        }

        public static byte[] GetTextureData(Texture2DMipInfo mipToLoad, bool decompress = true)
        {
            var imagebytes = new byte[decompress ? mipToLoad.uncompressedSize : mipToLoad.compressedSize];

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
                var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(mipToLoad.Export.Game);
                if (mipToLoad.Export.Game == MEGame.ME1)
                {
                    if (loadedFiles.TryGetValue(mipToLoad.TextureCacheName, out string filepath))
                    {
                        filename = filepath;
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
                        if (loadedFiles.TryGetValue(archive, out string fullPath))
                        {
                            filename = fullPath;
                        }
                        else
                        {
                            throw new FileNotFoundException($"Externally referenced texture cache not found: {mipToLoad.TextureCacheName}.tfc.");
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

        public override void UnloadExport()
        {
            TextureImage.Source = null;
            CurrentLoadedFormat = null;
            MipList.ClearEx();
        }

        private void MipList_SelectedItemChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MipList.Count > 0 && Mips_ListBox.SelectedIndex >= 0)
            {
                Debug.WriteLine($"Loading mip: {Mips_ListBox.SelectedIndex}");
                LoadMip(MipList[Mips_ListBox.SelectedIndex]);
            }
        }
        /*
        public string replaceTextures()
        {
            string errors = "";


            Texture texture = new Texture(package, matched.exportID, package.getExportData(matched.exportID));
            string fmt = texture.properties.getProperty("Format").valueName;
            PixelFormat pixelFormat = Image.getPixelFormatType(fmt);

            while (texture.mipMapsList.Exists(s => s.storageType == Texture.StorageTypes.empty))
            {
                texture.mipMapsList.Remove(texture.mipMapsList.First(s => s.storageType == Texture.StorageTypes.empty));
            }

            ImageFile image = new ImageFile();
            if (image == null)
            {
                using (FileStream fs = new FileStream(mod.memPath, FileMode.Open, FileAccess.Read))
                {
                    fs.JumpTo(mod.memEntryOffset);
                    byte[] data = decompressData(fs, mod.memEntrySize);
                    image = new ImageFile(data, ImageFormat.DDS);
                    if (memorySize > 8 || modsToReplace.Count == 1)
                        mod.cacheImage = image;
                }
            }

            if (image.mipMaps[0].origWidth / image.mipMaps[0].origHeight !=
                texture.mipMapsList[0].width / texture.mipMapsList[0].height)
            {
                errors += "Error in texture: " + mod.textureName + " This texture has wrong aspect ratio, skipping texture..." + Environment.NewLine;
                continue;
            }

            if (CurrentLoadedExport.Game == MEGame.ME1 && texture.mipMapsList.Count < 6)
            {
                for (int i = texture.mipMapsList.Count - 1; i != 0; i--)
                    texture.mipMapsList.RemoveAt(i);
            }

            PixelFormat newPixelFormat = pixelFormat;

            //Changing Texture Type. Not sure what this is, exactly.
            if (mod.markConvert)
                newPixelFormat = changeTextureType(pixelFormat, image.pixelFormat, ref package, ref texture);

            if (!image.checkDDSHaveAllMipmaps() ||
                (texture.mipMapsList.Count > 1 && image.mipMaps.Count() <= 1) ||
                (mod.markConvert && image.pixelFormat != newPixelFormat) ||
                (!mod.markConvert && image.pixelFormat != pixelFormat))
            {
                bool dxt1HasAlpha = false;
                byte dxt1Threshold = 128;
                if (pixelFormat == PixelFormat.DXT1 && texture.properties.exists("CompressionSettings") &&
                    texture.properties.getProperty("CompressionSettings").valueName == "TC_OneBitAlpha")
                {
                    dxt1HasAlpha = true;
                    if (image.pixelFormat == PixelFormat.ARGB ||
                        image.pixelFormat == PixelFormat.DXT3 ||
                        image.pixelFormat == PixelFormat.DXT5)
                    {
                        errors += "Warning for texture: " + mod.textureName + ". This texture converted from full alpha to binary alpha." + Environment.NewLine;
                    }
                }
                image.correctMips(newPixelFormat, dxt1HasAlpha, dxt1Threshold);
                if (memorySize > 8 || modsToReplace.Count == 1)
                    mod.cacheImage = image;
            }

            // remove lower mipmaps from source image which not exist in game data
            for (int t = 0; t < image.mipMaps.Count(); t++)
            {
                if (image.mipMaps[t].origWidth <= texture.mipMapsList[0].width &&
                    image.mipMaps[t].origHeight <= texture.mipMapsList[0].height &&
                    texture.mipMapsList.Count > 1)
                {
                    if (!texture.mipMapsList.Exists(m => m.width == image.mipMaps[t].origWidth && m.height == image.mipMaps[t].origHeight))
                    {
                        image.mipMaps.RemoveAt(t--);
                    }
                }
            }

            // put empty mips if missing
            for (int t = 0; t < texture.mipMapsList.Count; t++)
            {
                if (texture.mipMapsList[t].width <= image.mipMaps[0].origWidth &&
                    texture.mipMapsList[t].height <= image.mipMaps[0].origHeight)
                {
                    if (!image.mipMaps.Exists(m => m.origWidth == texture.mipMapsList[t].width && m.origHeight == texture.mipMapsList[t].height))
                    {
                        MipMap mipmap = new MipMap(texture.mipMapsList[t].width, texture.mipMapsList[t].height, pixelFormat);
                        image.mipMaps.Add(mipmap);
                    }
                }
            }

            if (!texture.properties.exists("LODGroup"))
                texture.properties.setByteValue("LODGroup", "TEXTUREGROUP_Character", "TextureGroup", 1025);

            if (mod.cacheCprMipmaps == null)
            {
                mod.cacheCprMipmaps = new List<byte[]>();
                for (int m = 0; m < image.mipMaps.Count(); m++)
                {
                    if (CurrentLoadedExport.Game == MEGame.ME1)
                        mod.cacheCprMipmaps.Add(texture.compressTexture(image.mipMaps[m].data, Texture.StorageTypes.extLZO));
                    else
                        mod.cacheCprMipmaps.Add(texture.compressTexture(image.mipMaps[m].data, Texture.StorageTypes.extZlib));
                }
            }

            if (verify)
                matched.crcs = new List<uint>();
            List<Texture.MipMap> mipmaps = new List<Texture.MipMap>();
            for (int m = 0; m < image.mipMaps.Count(); m++)
            {
                if (verify)
                    matched.crcs.Add(texture.getCrcData(image.mipMaps[m].data));
                Texture.MipMap mipmap = new Texture.MipMap();
                mipmap.width = image.mipMaps[m].origWidth;
                mipmap.height = image.mipMaps[m].origHeight;
                if (texture.existMipmap(mipmap.width, mipmap.height))
                    mipmap.storageType = texture.getMipmap(mipmap.width, mipmap.height).storageType;
                else
                {
                    mipmap.storageType = texture.getTopMipmap().storageType;
                    if (texture.mipMapsList.Count() > 1)
                    {
                        if (CurrentLoadedExport.Game == MEGame.ME1 && matched.linkToMaster == -1)
                        {
                            if (mipmap.storageType == Texture.StorageTypes.pccUnc)
                            {
                                mipmap.storageType = Texture.StorageTypes.pccLZO;
                            }
                        }
                        else if (CurrentLoadedExport.Game == MEGame.ME1 && matched.linkToMaster != -1)
                        {
                            if (mipmap.storageType == Texture.StorageTypes.pccUnc ||
                                mipmap.storageType == Texture.StorageTypes.pccLZO ||
                                mipmap.storageType == Texture.StorageTypes.pccZlib)
                            {
                                mipmap.storageType = Texture.StorageTypes.extLZO;
                            }
                        }
                        else if (CurrentLoadedExport.Game == MEGame.ME2 || CurrentLoadedExport.Game == MEGame.ME3)
                        {
                            if (texture.properties.exists("TextureFileCacheName"))
                            {
                                if (texture.mipMapsList.Count < 6)
                                {
                                    mipmap.storageType = Texture.StorageTypes.pccUnc;
                                    texture.properties.setBoolValue("NeverStream", true);
                                }
                                else
                                {
                                    if (CurrentLoadedExport.Game == MEGame.ME2)
                                        mipmap.storageType = Texture.StorageTypes.extLZO;
                                    else
                                        mipmap.storageType = Texture.StorageTypes.extZlib;
                                }
                            }
                        }
                    }
                }

                if (GameData.gameType != MeType.ME1_TYPE)
                {
                    if (mipmap.storageType == Texture.StorageTypes.extLZO)
                        mipmap.storageType = Texture.StorageTypes.extZlib;
                    if (mipmap.storageType == Texture.StorageTypes.pccLZO)
                        mipmap.storageType = Texture.StorageTypes.pccZlib;
                }

                if (mod.arcTexture != null)
                {
                    if (mod.arcTexture[m].storageType != mipmap.storageType)
                    {
                        mod.arcTexture = null;
                    }
                }

                mipmap.width = image.mipMaps[m].width;
                mipmap.height = image.mipMaps[m].height;
                mipmaps.Add(mipmap);
                if (texture.mipMapsList.Count() == 1)
                    break;
            }

            bool triggerCacheArc = false;
            bool newTfcFile = false;
            bool oldSpace = true;
            string archiveFile = "";
            if (texture.properties.exists("TextureFileCacheName"))
            {
                string archive = texture.properties.getProperty("TextureFileCacheName").valueName;
                if (mod.arcTfcDLC && mod.arcTfcName != archive)
                    mod.arcTexture = null;

                if (mod.arcTexture == null)
                {
                    archiveFile = Path.Combine(GameData.MainData, archive + ".tfc");
                    if (matched.path.ToLowerInvariant().Contains("\\dlc"))
                    {
                        mod.arcTfcDLC = true;
                        string DLCArchiveFile = Path.Combine(Path.GetDirectoryName(GameData.GamePath + matched.path), archive + ".tfc");
                        if (File.Exists(DLCArchiveFile))
                            archiveFile = DLCArchiveFile;
                        else if (!File.Exists(archiveFile))
                        {
                            List<string> files = Directory.GetFiles(GameData.bioGamePath, archive + ".tfc",
                                SearchOption.AllDirectories).Where(item => item.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase)).ToList();
                            if (files.Count == 1)
                                archiveFile = files[0];
                            else if (files.Count == 0)
                            {
                                using (FileStream fs = new FileStream(DLCArchiveFile, FileMode.CreateNew, FileAccess.Write))
                                {
                                    fs.WriteFromBuffer(texture.properties.getProperty("TFCFileGuid").valueStruct);
                                }
                                archiveFile = DLCArchiveFile;
                                newTfcFile = true;
                            }
                            else
                                throw new Exception("More instnces of TFC file: " + archive + ".tfc");
                        }
                    }
                    else
                    {
                        mod.arcTfcDLC = false;
                    }

                    // check if texture fit in old space
                    for (int mip = 0; mip < image.mipMaps.Count(); mip++)
                    {
                        Texture.MipMap testMipmap = new Texture.MipMap();
                        testMipmap.width = image.mipMaps[mip].origWidth;
                        testMipmap.height = image.mipMaps[mip].origHeight;
                        if (texture.existMipmap(testMipmap.width, testMipmap.height))
                            testMipmap.storageType = texture.getMipmap(testMipmap.width, testMipmap.height).storageType;
                        else
                        {
                            oldSpace = false;
                            break;
                        }

                        if (testMipmap.storageType == Texture.StorageTypes.extZlib ||
                            testMipmap.storageType == Texture.StorageTypes.extLZO)
                        {
                            Texture.MipMap oldTestMipmap = texture.getMipmap(testMipmap.width, testMipmap.height);
                            if (mod.cacheCprMipmaps[mip].Length > oldTestMipmap.compressedSize)
                            {
                                oldSpace = false;
                                break;
                            }
                        }
                        if (texture.mipMapsList.Count() == 1)
                            break;
                    }

                    long fileLength = new FileInfo(archiveFile).Length;
                    if (!oldSpace && fileLength + 0x5000000 > 0x80000000)
                    {
                        archiveFile = "";
                        foreach (TFCTexture newGuid in guids)
                        {
                            archiveFile = Path.Combine(GameData.MainData, newGuid.name + ".tfc");
                            if (!File.Exists(archiveFile))
                            {
                                texture.properties.setNameValue("TextureFileCacheName", newGuid.name);
                                texture.properties.setStructValue("TFCFileGuid", "Guid", newGuid.guid);
                                using (FileStream fs = new FileStream(archiveFile, FileMode.CreateNew, FileAccess.Write))
                                {
                                    fs.WriteFromBuffer(newGuid.guid);
                                }
                                newTfcFile = true;
                                break;
                            }
                            else
                            {
                                fileLength = new FileInfo(archiveFile).Length;
                                if (fileLength + 0x5000000 < 0x80000000)
                                {
                                    texture.properties.setNameValue("TextureFileCacheName", newGuid.name);
                                    texture.properties.setStructValue("TFCFileGuid", "Guid", newGuid.guid);
                                    break;
                                }
                            }
                            archiveFile = "";
                        }
                        if (archiveFile == "")
                            throw new Exception("No free TFC texture file!");
                    }
                }
                else
                {
                    texture.properties.setNameValue("TextureFileCacheName", mod.arcTfcName);
                    texture.properties.setStructValue("TFCFileGuid", "Guid", mod.arcTfcGuid);
                }
            }

            for (int m = 0; m < image.mipMaps.Count(); m++)
            {
                Texture.MipMap mipmap = mipmaps[m];
                mipmap.uncompressedSize = image.mipMaps[m].data.Length;
                if (CurrentLoadedExport.Game == MEGame.ME1)
                {
                    if (mipmap.storageType == Texture.StorageTypes.pccLZO ||
                        mipmap.storageType == Texture.StorageTypes.pccZlib)
                    {
                        if (matched.linkToMaster == -1)
                            mipmap.newData = mod.cacheCprMipmaps[m];
                        else
                            mipmap.newData = mod.masterTextures.First(s => s.Value == matched.linkToMaster).Key[m].newData;
                        mipmap.compressedSize = mipmap.newData.Length;
                    }
                    if (mipmap.storageType == Texture.StorageTypes.pccUnc)
                    {
                        mipmap.compressedSize = mipmap.uncompressedSize;
                        mipmap.newData = image.mipMaps[m].data;
                    }
                    if ((mipmap.storageType == Texture.StorageTypes.extLZO ||
                        mipmap.storageType == Texture.StorageTypes.extZlib) && matched.linkToMaster != -1)
                    {
                        mipmap.compressedSize = mod.masterTextures.First(s => s.Value == matched.linkToMaster).Key[m].compressedSize;
                        mipmap.dataOffset = mod.masterTextures.First(s => s.Value == matched.linkToMaster).Key[m].dataOffset;
                    }
                }
                else
                {
                    if (mipmap.storageType == Texture.StorageTypes.extZlib ||
                        mipmap.storageType == Texture.StorageTypes.extLZO)
                    {
                        if (mod.cacheCprMipmaps.Count != image.mipMaps.Count())
                            throw new Exception();
                        mipmap.newData = mod.cacheCprMipmaps[m];
                        mipmap.compressedSize = mipmap.newData.Length;
                    }

                    if (mipmap.storageType == Texture.StorageTypes.pccUnc ||
                        mipmap.storageType == Texture.StorageTypes.extUnc)
                    {
                        mipmap.compressedSize = mipmap.uncompressedSize;
                        mipmap.newData = image.mipMaps[m].data;
                    }
                    if (mipmap.storageType == Texture.StorageTypes.extZlib ||
                        mipmap.storageType == Texture.StorageTypes.extLZO ||
                        mipmap.storageType == Texture.StorageTypes.extUnc)
                    {
                        if (mod.arcTexture == null)
                        {
                            triggerCacheArc = true;

                            if (!newTfcFile && oldSpace)
                            {
                                try
                                {
                                    using (FileStream fs = new FileStream(archiveFile, FileMode.Open, FileAccess.Write))
                                    {
                                        Texture.MipMap oldMipmap = texture.getMipmap(mipmap.width, mipmap.height);
                                        fs.JumpTo(oldMipmap.dataOffset);
                                        mipmap.dataOffset = oldMipmap.dataOffset;
                                        fs.WriteFromBuffer(mipmap.newData);
                                    }
                                }
                                catch
                                {
                                    throw new Exception("Problem with access to TFC file: " + archiveFile);
                                }
                            }
                            else
                            {
                                try
                                {
                                    using (FileStream fs = new FileStream(archiveFile, FileMode.Open, FileAccess.Write))
                                    {
                                        fs.SeekEnd();
                                        mipmap.dataOffset = (uint)fs.Position;
                                        fs.WriteFromBuffer(mipmap.newData);
                                    }
                                }
                                catch
                                {
                                    throw new Exception("Problem with access to TFC file: " + archiveFile);
                                }
                            }
                        }
                        else
                        {
                            if ((mipmap.width >= 4 && mod.arcTexture[m].width != mipmap.width) ||
                                (mipmap.height >= 4 && mod.arcTexture[m].height != mipmap.height))
                            {
                                throw new Exception("Dimensions mismatch!");
                            }
                            mipmap.dataOffset = mod.arcTexture[m].dataOffset;
                        }
                    }
                }
                mipmaps[m] = mipmap;
                if (texture.mipMapsList.Count() == 1)
                    break;
            }

            texture.replaceMipMaps(mipmaps);

            //Set properties
            var props = CurrentLoadedExport.GetProperties();
            props.AddOrReplaceProp(new IntProperty(texture.mipMapsList.First().width, "SizeX"));
            props.AddOrReplaceProp(new IntProperty(texture.mipMapsList.First().height, "SizeY"));
            var mipTailIdx = props.GetProp<IntProperty>("MipTailBaseIdx");
            if (mipTailIdx != null)
            {
                mipTailIdx.Value = texture.mipMapsList.Count() - 1;
            }
            CurrentLoadedExport.WriteProperties(props); //Write updated props back


            using (MemoryStream newData = new MemoryStream())
            {
                newData.WriteFromBuffer(texture.properties.toArray());
                newData.WriteFromBuffer(texture.toArray(0, false)); // filled later
                package.setExportData(matched.exportID, newData.ToArray());
            }

            using (MemoryStream newData = new MemoryStream())
            {
                newData.WriteFromBuffer(texture.properties.toArray());
                newData.WriteFromBuffer(texture.toArray(package.exportsTable[matched.exportID].dataOffset + (uint)newData.Position));
                package.setExportData(matched.exportID, newData.ToArray());
            }

            //Since this is single replacement, we don't want to relink to master
            //We want to ensure names are different though, will have to implement into UI
            //if (CurrentLoadedExport.Game == MEGame.ME1)
            //{
            //    if (matched.linkToMaster == -1)
            //        mod.masterTextures.Add(texture.mipMapsList, entryMap.listIndex);
            //}
            //else
            //{
            //    if (triggerCacheArc)
            //    {
            //        mod.arcTexture = texture.mipMapsList;
            //        mod.arcTfcGuid = texture.properties.getProperty("TFCFileGuid").valueStruct;
            //        mod.arcTfcName = texture.properties.getProperty("TextureFileCacheName").valueName;
            //    }
            //}

            matched.removeEmptyMips = false;
            if (!map[e].slave)
            {
                for (int r = 0; r < map[e].removeMips.exportIDs.Count; r++)
                {
                    if (map[e].removeMips.exportIDs[r] == matched.exportID)
                    {
                        map[e].removeMips.exportIDs.RemoveAt(r);
                        break;
                    }
                }
            }

            mod.instance--;
            if (mod.instance < 0)
                throw new Exception();
            if (mod.instance == 0)
            {
                if (mod.arcTexture != null)
                {
                    mod.arcTexture.Clear();
                    mod.arcTexture = null;
                }
                if (mod.cacheCprMipmaps != null)
                {
                    mod.cacheCprMipmaps.Clear();
                    mod.cacheCprMipmaps = null;
                }
                mod.cacheImage = null;
                mod.arcTfcGuid = null;
                if (mod.masterTextures != null)
                {
                    mod.masterTextures.Clear();
                    mod.masterTextures = null;
                }
            }

            if (memorySize <= 6 && mod.cacheCprMipmaps != null && modsToReplace.Count != 1)
            {
                mod.cacheCprMipmaps.Clear();
                mod.cacheCprMipmaps = null;
            }

            modsToReplace[entryMap.modIndex] = mod;
            textures[entryMap.texturesIndex].list[entryMap.listIndex] = matched;

            return errors;
        }*/

        public override void Dispose()
        {
            //Nothing to dispose
        }

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
                    else
                    {
                        //NeverStream is set if there are more than 6 mips. Some sort of design implementation of ME1 texture streaming
                        if (baseName != "" && !NeverStream)
                        {
                            var gameFiles = MELoadedFiles.GetFilesLoadedInGame(MEGame.ME1);
                            if (gameFiles.ContainsKey(baseName)) //I am pretty sure these will only ever resolve to UPKs...
                            {
                                return baseName;
                            }
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
}
