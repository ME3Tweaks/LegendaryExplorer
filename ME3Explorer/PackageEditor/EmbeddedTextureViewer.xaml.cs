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
using ME3Explorer.Unreal;
using static ME3Explorer.BinaryInterpreter;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for EmbeddedTextureViewer.xaml
    /// </summary>
    public partial class EmbeddedTextureViewer : ExportLoaderControl
    {
        public EmbeddedTextureViewer()
        {
            InitializeComponent();
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return exportEntry.FileRef.Game == MEGame.ME3 && exportEntry.ClassName == "Texture2D";
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            PropertyCollection properties = exportEntry.GetProperties();
            var format = properties.GetProp<EnumProperty>("Format");
            TextPrev.Content = format.Value;

            MemoryStream ms = new MemoryStream(exportEntry.Data);
            ms.Seek(properties.endOffset, SeekOrigin.Begin);
            List<Texture2DMipInfo> mips = new List<Texture2DMipInfo>();
            int numMipMaps = ms.ReadInt32();
            for (int l = 0; l < numMipMaps; l++)
            {
                Texture2DMipInfo mip = new Texture2DMipInfo();
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

            string content = "";
            foreach (Texture2DMipInfo mip in mips)
            {
                content += "----\n";
                content += mip.storageType;
                content += "\n";
                content += mip.uncompressedSize;
                content += "\n";
                content += mip.compressedSize;
                content += "\n";
                content += mip.offset;
                content += "\n";
                content += mip.width;
                content += "\n";
                content += mip.height;
                content += "\n";

            }
            TextPrev.Content = content;
        }

        public override void UnloadExport()
        {
            //throw new NotImplementedException();
        }

        class Texture2DMipInfo
        {
            public int uncompressedSize;
            public int compressedSize;
            public int width;
            public int height;
            public int offset;
            public StorageTypes storageType;
        }
    }
}
