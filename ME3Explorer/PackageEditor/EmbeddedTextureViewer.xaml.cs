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
using Gammtek.Conduit.Extensions.IO;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;

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
            InitializeComponent();
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
                        throw new Exception($"Externally referenced texture file not found in game: {mipToLoad.TextureCacheName}.");
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

                    if (filename != null)
                    {
                        if (loadedFiles.TryGetValue(archive, out string fullPath))
                        {
                            filename = fullPath;
                        }
                        else
                        {
                            throw new Exception($"Externally referenced texture cache not found: {mipToLoad.TextureCacheName}.tfc.");
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
                    string baseName = Export.FileRef.FollowLink(Export.idxLink).Split('.')[0].ToUpper()+".upk"; //get top package name

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
