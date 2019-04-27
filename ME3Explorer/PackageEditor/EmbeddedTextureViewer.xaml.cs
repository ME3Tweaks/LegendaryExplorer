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
using System.Windows.Shapes;
using Gammtek.Conduit.Extensions.IO;
using KFreonLib.MEDirectories;
using KFreonLib.Textures;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using static ME3Explorer.BinaryInterpreter;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for EmbeddedTextureViewer.xaml
    /// </summary>
    public partial class EmbeddedTextureViewer : ExportLoaderControl
    {
        public ObservableCollectionExtended<Texture2DMipInfo> MipList { get; private set; } = new ObservableCollectionExtended<Texture2DMipInfo>();
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
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("[PackEdWPF] Texture ExportLoader", new WeakReference(this));

            DataContext = this;
            CannotShowTextureText = "Select a mip to view";
            CannotShowTextureTextVisibility = Visibility.Visible;
            InitializeComponent();
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return exportEntry.ClassName == "Texture2D" ||
                   exportEntry.ClassName == "LightMapTexture2D" ||
                   exportEntry.ClassName == "ShadowMapTexture2D" ||
                   exportEntry.ClassName == "TextureFlipBook";
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new EmbeddedTextureViewer(), CurrentLoadedExport);
                elhw.Title = $"Texture Viewer - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.GetFullPath}_{CurrentLoadedExport.indexValue} - {CurrentLoadedExport.FileRef.FileName}";
                elhw.Show();
            }
        }

        public override void LoadExport(IExportEntry exportEntry)
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

                MemoryStream ms = new MemoryStream(exportEntry.Data);
                ms.Seek(properties.endOffset, SeekOrigin.Begin);
                if (exportEntry.FileRef.Game != MEGame.ME3)
                {
                    ms.Seek(12, SeekOrigin.Current); // 12 zeros
                    ms.Seek(4, SeekOrigin.Current); // position in the package
                }
                List<Texture2DMipInfo> mips = new List<Texture2DMipInfo>();
                int numMipMaps = ms.ReadInt32();
                for (int l = 0; l < numMipMaps; l++)
                {
                    Texture2DMipInfo mip = new Texture2DMipInfo
                    {
                        index = l,
                        storageType = (StorageTypes)ms.ReadInt32(),
                        uncompressedSize = ms.ReadInt32(),
                        compressedSize = ms.ReadInt32(),
                        externalOffset = ms.ReadInt32(),
                        packageOffset = (int)ms.Position,
                        cacheFile = CurrentLoadedCacheName
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
                            MEDirectories MEExDirecs = new MEDirectories
                            {
                                WhichGame = (int)exportEntry.FileRef.Game
                            };
                            MEExDirecs.SetupPaths((int)exportEntry.FileRef.Game);
                            List<string> gameFiles =
                                MEDirectories.EnumerateGameFiles(MEExDirecs.WhichGame, System.IO.Path.GetDirectoryName(MEExDirecs.PathBIOGame));
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
            catch (Exception e)
            {
                //Error loading texture
            }
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

            byte[] imagebytes = new byte[mipToLoad.uncompressedSize];

            if (mipToLoad.storageType == StorageTypes.pccUnc)
            {
                Buffer.BlockCopy(CurrentLoadedExport.Data, mipToLoad.packageOffset, imagebytes, 0, mipToLoad.uncompressedSize);
            }
            else if (mipToLoad.storageType == StorageTypes.pccLZO ||
                     mipToLoad.storageType == StorageTypes.pccZlib)
            {
                try
                {
                    TextureCompression.DecompressTexture(imagebytes,
                                         new MemoryStream(CurrentLoadedExport.Data, mipToLoad.packageOffset, mipToLoad.compressedSize),
                                         mipToLoad.storageType, mipToLoad.uncompressedSize, mipToLoad.compressedSize);
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + "\n" + "StorageType: " + mipToLoad.storageType + "\n");
                }
            }
            if (mipToLoad.storageType == StorageTypes.extUnc ||
                mipToLoad.storageType == StorageTypes.extLZO ||
                mipToLoad.storageType == StorageTypes.extZlib)
            {
                TextureImage.Source = null;
                string filename;
                MEDirectories MEExDirecs = new MEDirectories
                {
                    WhichGame = (int)CurrentLoadedExport.FileRef.Game
                };
                MEExDirecs.SetupPaths((int)CurrentLoadedExport.FileRef.Game);
                if (CurrentLoadedExport.FileRef.Game == MEGame.ME1)
                {
                    List<string> gameFiles =
                        MEDirectories.EnumerateGameFiles(MEExDirecs.WhichGame, System.IO.Path.GetDirectoryName(MEExDirecs.PathBIOGame));
                    filename = gameFiles.Find(s => System.IO.Path.GetFileNameWithoutExtension(s).Equals(CurrentLoadedBasePackageName, StringComparison.OrdinalIgnoreCase));
                    if (filename == null || filename == "")
                        throw new Exception("File not found in game: " + CurrentLoadedBasePackageName + ".*");
                }
                else
                {
                    string archive = CurrentLoadedCacheName;
                    filename = System.IO.Path.Combine(MEExDirecs.pathCooked, archive + ".tfc");
                    string packagePath = CurrentLoadedExport.FileRef.FileName;
                    string currentPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(packagePath), archive + ".tfc");
                    if (File.Exists(currentPath))
                        filename = currentPath;
                    else if (packagePath.ToLowerInvariant().Contains("\\dlc"))
                    {
                        string DLCArchiveFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(packagePath), archive + ".tfc");
                        if (File.Exists(DLCArchiveFile))
                            filename = DLCArchiveFile;
                        else if (!File.Exists(filename))
                        {
                            List<string> files = Directory.GetFiles(MEExDirecs.PathBIOGame, archive + ".tfc",
                                SearchOption.AllDirectories).Where(item => item.EndsWith(".tfc", StringComparison.OrdinalIgnoreCase)).ToList();
                            if (files.Count == 1)
                                filename = files[0];
                            else if (files.Count == 0)
                                throw new Exception("TFC File Not Found: " + archive + ".tfc");
                            else
                                throw new Exception("More instances of TFC file: " + archive + ".tfc");
                        }
                    }
                }

                try
                {
                    using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                    {
                        try
                        {
                            fs.Seek(mipToLoad.externalOffset, SeekOrigin.Begin);
                            if (mipToLoad.storageType == StorageTypes.extLZO || mipToLoad.storageType == StorageTypes.extZlib)
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
                Debug.WriteLine("Loading mip: " + Mips_ListBox.SelectedIndex);
                LoadMip(MipList[Mips_ListBox.SelectedIndex]);
            }
        }

        public override void Dispose()
        {
            //Nothing to dispose
        }

        public class Texture2DMipInfo
        {
            public int index;
            public int uncompressedSize;
            public int compressedSize;
            public int width;
            public int height;
            public int externalOffset;
            public int packageOffset;
            public string cacheFile;
            public StorageTypes storageType;

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
                        mipinfostring += cacheFile;
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
