using System;
using System.Collections.Generic;
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
            DataContext = this;
            CannotShowTextureText = "Select a mip to view";
            CannotShowTextureTextVisibility = Visibility.Visible;
            InitializeComponent();
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return exportEntry.FileRef.Game == MEGame.ME3 && exportEntry.ClassName == "Texture2D";
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
                MemoryStream ms = new MemoryStream(exportEntry.Data);
                ms.Seek(properties.endOffset, SeekOrigin.Begin);
                List<Texture2DMipInfo> mips = new List<Texture2DMipInfo>();
                int numMipMaps = ms.ReadInt32();
                for (int l = 0; l < numMipMaps; l++)
                {
                    Texture2DMipInfo mip = new Texture2DMipInfo();
                    mip.index = l;
                    mip.storageType = (StorageTypes)ms.ReadInt32();
                    mip.uncompressedSize = ms.ReadInt32();
                    mip.compressedSize = ms.ReadInt32();
                    mip.offset = ms.ReadInt32();
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
                    mips.Add(mip);
                }


                var topmip = mips.FirstOrDefault(x => x.storageType != StorageTypes.empty);

                CurrentLoadedExport = exportEntry;
                CurrentLoadedFormat = format.Value.Name;
                MipList.ReplaceAll(mips);
                LoadMip(topmip);
            }
            catch (Exception e)
            {

            }
        }

        private void LoadMip(Texture2DMipInfo mipToLoad)
        {
            if (mipToLoad == null || mipToLoad.storageType == StorageTypes.extUnc || mipToLoad.storageType == StorageTypes.empty) { return; }
            byte[] imagebytes = new byte[mipToLoad.uncompressedSize];

            if (mipToLoad.storageType == StorageTypes.pccUnc)
            {
                Buffer.BlockCopy(CurrentLoadedExport.Data, mipToLoad.offset - CurrentLoadedExport.DataOffset, imagebytes, 0, mipToLoad.uncompressedSize);
            }
            if ((mipToLoad.storageType == StorageTypes.extLZO || mipToLoad.storageType == StorageTypes.extZlib) && CurrentLoadedCacheName != null)
            {

                TextureImage.Source = null;
                CannotShowTextureText = "Cannot display TFC stored mips\nSelect a lower mip to display\n\nComing in a future release";
                CannotShowTextureTextVisibility = Visibility.Visible;

                return;
                //Aquadran: This does not work for me. I cannot display TFC stored mips.
                //Uncomment above lines to activate this
                string filename = CurrentLoadedCacheName + ".tfc";
                string localPathText = System.IO.Path.Combine(Directory.GetParent(CurrentLoadedExport.FileRef.FileName).FullName, filename);
                if (File.Exists(localPathText))
                {
                    using (var file = File.OpenRead(localPathText))
                    {
                        int bytesRead;
                        var buffer = new byte[mipToLoad.compressedSize];
                        file.Seek(mipToLoad.offset, SeekOrigin.Begin);
                        bytesRead = file.Read(buffer, 0, buffer.Length);

                        if (mipToLoad.storageType == StorageTypes.extLZO)
                        {
                            LZO2Helper.LZO2.Decompress(buffer, (uint)buffer.Length, imagebytes);
                        }
                        if (mipToLoad.storageType == StorageTypes.extZlib)
                        {
                            imagebytes = AmaroK86.MassEffect3.ZlibBlock.ZBlock.Decompress(file, mipToLoad.compressedSize);
                            //new ZlibHelper.Zlib().Decompress(buffer, (uint)buffer.Length, imagebytes);
                        }
                    }
                }
            }

            //CannotShowTextureText = "Cannot display TFC stored mips\nComing in a future release";
            CannotShowTextureTextVisibility = Visibility.Collapsed;
            AmaroK86.ImageFormat.DDS dds = new AmaroK86.ImageFormat.DDS(null, new AmaroK86.ImageFormat.ImageSize((uint)mipToLoad.width, (uint)mipToLoad.height), CurrentLoadedFormat.Substring(3), imagebytes);
            AmaroK86.ImageFormat.DDSImage ddsimage = new AmaroK86.ImageFormat.DDSImage(dds.ToArray());
            var bitmap = ddsimage.mipMaps[0].bitmap;
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
            if (MipList.Count > 0)
            {
                LoadMip(MipList[MipListView.SelectedIndex]);
            }
        }

        public class Texture2DMipInfo
        {
            public int index;
            public int uncompressedSize;
            public int compressedSize;
            public int width;
            public int height;
            public int offset;
            public StorageTypes storageType;

            public string MipDisplayString
            {
                get
                {
                    string mipinfostring = "Mip " + index;
                    mipinfostring += "\nStorage Type: ";
                    mipinfostring += storageType;
                    mipinfostring += "\nUncompressed size: ";
                    mipinfostring += uncompressedSize;
                    mipinfostring += "\nCompressed size: ";
                    mipinfostring += compressedSize;
                    mipinfostring += "\nOffset: ";
                    mipinfostring += offset;
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
