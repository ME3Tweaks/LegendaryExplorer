using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.UnrealHelper
{
    public class ImageFile
    {

#region Declarations
        public DDSHeader DDS;
        public TGAHeader TGA;
        public byte[] memory;
        public int memsize;
        public int ImageSizeX;
        public int ImageSizeY;
        public int ImageBits;
        public int ImageFormat;
        //0=DXT1
        //1=DXT5
        //2=V8U8
        //3=A8R8G8B8
        //4=G8
        [Serializable()]
        public struct DDS_PIXELFORMAT
        {
            public int dwSize;
            public int dwFlags;
            public int dwFourCC;
            public int dwRGBBitCount;
            public int dwRBitMask;
            public int dwGBitMask;
            public int dwBBitMask;
            public int dwABitMask;
        }
        [Serializable()]
        public struct DDSHeader
        {
            public int magic;
            public int dwSize;
            public int dwFlags;
            public int dwHeight;
            public int dwWidth;
            public int dwPitchOrLinearSize;
            public int dwDepth;
            public int dwMipMapCount;
            unsafe public fixed int dwReserved1[11];
            public DDS_PIXELFORMAT ddspf;
            public int dwCaps;
            public int dwCaps2;
            public int dwCaps3;
            public int dwCaps4;
            public int dwReserved2;
        }
        [Serializable()]
        public struct TGAHeader
        {
            public byte identsize;
            public byte colourmaptype;
            public byte imagetype;
            public Int16 colourmapstart;
            public Int16 colourmaplength;
            public byte colourmapbits;
            public Int16 ystart;
            public Int16 width;
            public Int16 height;
            public byte bits;
            public byte descriptor;
        }
#endregion

        public ImageFile()
        {
        }

        public ImageFile(byte[]Raw,string format,int SizeX, int SizeY)
        {
            memory = Raw;
            memsize = Raw.Length;
            switch (format)
            {
                case "PF_DXT1\0":
                    ImageFormat = 0;
                    break;
                case "PF_DXT5\0":
                    ImageFormat = 1;
                    break;
                case "PF_V8U8\0":
                    ImageFormat = 2;
                    break;
                case "PF_A8R8G8B8\0":
                    ImageFormat = 3;
                    break;
                case "PF_G8\0":
                    ImageFormat = 4;
                    break;
                default:
                    ImageFormat = -1;
                    break;
            }
            ImageSizeX = SizeX;
            ImageSizeY = SizeY;
        }

#region Export
        public void ExportToFile(string path = "")
        {
            ClearFiles();
            switch (ImageFormat)
            {
                case 0:
                    ExportDXT1(path);
                    break;
                case 1:
                    ExportDXT5(path);
                    break;
                case 2:
                    ExportUVTGA(path);
                    break;
                case 3:
                    ExportTGA(path);
                    break;
                case 4:
                    ExportGreyTGA(path);
                    break;
            }
        }

        public MemoryStream ExportToStream()
        {
            MemoryStream m = new MemoryStream();
            switch (ImageFormat)
            {
                case 0:
                    return ExportDXT1();
                case 1:
                    return ExportDXT5();
                case 2:
                    return ExportUVTGA();
                case 3:
                    return ExportTGA();
                case 4:
                    return ExportGreyTGA();
                default:
                    return m;
            }
        }

        private void ExportDXT1(string path="")
        {
            bool ok = !(path == "");
            SaveFileDialog FileDialog1 = new SaveFileDialog();
            FileDialog1.Filter = "DDS Files(*.dds)|*.dds";
            if (ok || FileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (!ok)
                    path = FileDialog1.FileName;
                FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
                DDS = CreateHeaderDDS();
                DDS.dwWidth = ImageSizeX;
                DDS.dwHeight = ImageSizeY;
                byte[] buff = StructureToByteArray(DDS);
                fs.Write(buff, 0, buff.Length);
                fs.Write(memory, 0, memsize);
                fs.Close();
                if(!ok)
                    MessageBox.Show("Done");

            }
        }
        private MemoryStream ExportDXT1()
        {
            MemoryStream m = new MemoryStream();
            DDS = CreateHeaderDDS();
            DDS.dwWidth = ImageSizeX;
            DDS.dwHeight = ImageSizeY;
            byte[] buff = StructureToByteArray(DDS);
            m.Write(buff, 0, buff.Length);
            m.Write(memory, 0, memsize);
            return m;
        }
        private void ExportDXT5(string path = "")
        {
            bool ok = !(path == "");
            SaveFileDialog FileDialog1 = new SaveFileDialog();
            FileDialog1.Filter = "DDS Files(*.dds)|*.dds";
            if (ok || FileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (!ok)
                    path = FileDialog1.FileName;
                FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
                DDS = CreateHeaderDDS();
                DDS.dwWidth = ImageSizeX;
                DDS.dwHeight = ImageSizeY;
                DDS.ddspf.dwFourCC = 0x35545844;
                byte[] buff = StructureToByteArray(DDS);
                fs.Write(buff, 0, buff.Length);
                fs.Write(memory, 0, memsize);
                fs.Close();
                if (!ok)
                    MessageBox.Show("Done");

            }
        }
        private MemoryStream ExportDXT5()
        {
            MemoryStream m = new MemoryStream();
            DDS = CreateHeaderDDS();
            DDS.dwWidth = ImageSizeX;
            DDS.dwHeight = ImageSizeY;
            DDS.ddspf.dwFourCC = 0x35545844;
            byte[] buff = StructureToByteArray(DDS);
            m.Write(buff, 0, buff.Length);
            m.Write(memory, 0, memsize);
            return m;
        }
        private void ExportTGA(string path = "")
        {
            bool ok = !(path == "");
            SaveFileDialog FileDialog1 = new SaveFileDialog();
            FileDialog1.Filter = "TGA Files(*.tga)|*.tga";
            if (ok || FileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (!ok)
                    path = FileDialog1.FileName;
                FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
                TGA = CreateHeaderTGA();
                TGA.width = (short)ImageSizeX;
                TGA.height =  (short)ImageSizeY;
                TGA.bits = 32;
                TGA.descriptor = 32;
                byte[] buff = StructureToByteArray(TGA);
                fs.Write(buff, 0, buff.Length);
                fs.Write(memory, 0, memsize);
                fs.Close();
                if (!ok)
                    MessageBox.Show("Done");

            }
        }
        private MemoryStream ExportTGA()
        {
            MemoryStream m = new MemoryStream();
            TGA = CreateHeaderTGA();
            TGA.width = (short)ImageSizeX;
            TGA.height = (short)ImageSizeY;
            TGA.bits = 32;
            TGA.descriptor = 32;
            byte[] buff = StructureToByteArray(TGA);
            m.Write(buff, 0, buff.Length);
            m.Write(memory, 0, memsize);
            return m;
        }
        private void ExportGreyTGA(string path = "")
        {
            bool ok = !(path == "");
            SaveFileDialog FileDialog1 = new SaveFileDialog();
            FileDialog1.Filter = "TGA Files(*.tga)|*.tga";
            if (ok || FileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (!ok)
                    path = FileDialog1.FileName;
                FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
                TGA = CreateHeaderTGA();
                TGA.width = (short)ImageSizeX;
                TGA.height = (short)ImageSizeY;
                TGA.bits = 32;
                TGA.descriptor = 32;
                byte[] buff = StructureToByteArray(TGA);
                fs.Write(buff, 0, buff.Length);
                for (int i = 0; i < memsize; i++)
                {
                    fs.WriteByte(memory[i]);
                    fs.WriteByte(memory[i]);
                    fs.WriteByte(memory[i]);
                    fs.WriteByte(255);
                }
                fs.Close();
                if (!ok)
                    MessageBox.Show("Done");

            }
        }
        private MemoryStream ExportGreyTGA()
        {
            MemoryStream m = new MemoryStream();
            TGA = CreateHeaderTGA();
            TGA.width = (short)ImageSizeX;
            TGA.height = (short)ImageSizeY;
            TGA.bits = 32;
            TGA.descriptor = 32;
            byte[] buff = StructureToByteArray(TGA);
            m.Write(buff, 0, buff.Length);
            for (int i = 0; i < memsize; i++)
            {
                m.WriteByte(memory[i]);
                m.WriteByte(memory[i]);
                m.WriteByte(memory[i]);
                m.WriteByte(255);
            }
            return m;
        }
        private void ExportUVTGA(string path = "")
        {
            bool ok = !(path == "");
            SaveFileDialog FileDialog1 = new SaveFileDialog();
            FileDialog1.Filter = "TGA Files(*.tga)|*.tga";
            if (ok || FileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (!ok)
                    path = FileDialog1.FileName;
                FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write);
                TGA = CreateHeaderTGA();
                TGA.width = (short)ImageSizeX;
                TGA.height = (short)ImageSizeY;
                TGA.bits = 32;
                TGA.descriptor = 32;
                byte[] buff = StructureToByteArray(TGA);
                fs.Write(buff, 0, buff.Length);
                for (int i = 0; i < memsize/2; i++)
                {
                    
                    fs.WriteByte(255);
                    fs.WriteByte(memory[i * 2]);
                    fs.WriteByte(memory[i * 2 + 1]);
                    fs.WriteByte(255);
                }
                fs.Close();
                if (!ok)
                    MessageBox.Show("Done");

            }
        }
        private MemoryStream ExportUVTGA()
        {
            MemoryStream m = new MemoryStream();
            TGA = CreateHeaderTGA();
            TGA.width = (short)ImageSizeX;
            TGA.height = (short)ImageSizeY;
            TGA.bits = 32;
            TGA.descriptor = 32;
            byte[] buff = StructureToByteArray(TGA);
            m.Write(buff, 0, buff.Length);
            for (int i = 0; i < memsize / 2; i++)
            {

                m.WriteByte(255);
                m.WriteByte(memory[i * 2]);
                m.WriteByte(memory[i * 2 + 1]);
                m.WriteByte(255);
            }
            return m;
        }
#endregion

#region Import

        public void ImportFromFile(string path)
        {
            string s = Path.GetExtension(path).ToLower();
            if (s == ".dds")
            {
                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                byte[] buff = StructureToByteArray(DDS);
                fs.Read(buff, 0, buff.Length);
                uint version = BitConverter.ToUInt32(buff, 84);
                if (version == 0x31545844)
                    ImageFormat = 0;
                if (version == 0x35545844)
                    ImageFormat = 1;
                ImageSizeY = BitConverter.ToInt32(buff, 12);
                ImageSizeX = BitConverter.ToInt32(buff, 16);
                memsize =(int)(fs.Length - buff.Length);
                memory = new byte[memsize];
                for (int i = 0; i < memsize; i++)
                    memory[i] = (byte)fs.ReadByte();
                fs.Close();
            }
            if (s == ".tga")
            {
                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                byte[] buff = StructureToByteArray(TGA);
                fs.Read(buff, 0, buff.Length);
                ImageSizeX = BitConverter.ToUInt16(buff, 12);
                ImageSizeY = BitConverter.ToUInt16(buff, 14);
                ImageBits = buff[16];
                memsize = (int)(fs.Length - buff.Length);
                memory = new byte[memsize];
                for (int i = 0; i < memsize; i++)
                    memory[i] = (byte)fs.ReadByte();
                fs.Close();
            }
        }

#endregion

#region Helper
        public DDSHeader CreateHeaderDDS()
        {
            DDSHeader temp = new DDSHeader();
            temp.magic = 0x20534444;
            temp.dwSize = 0x7C;
            temp.dwFlags = 0x1007;
            temp.dwPitchOrLinearSize = 0;
            unsafe { temp.dwReserved1[9] = 0x5454564E; }
            unsafe { temp.dwReserved1[10] = 0x20006; }
            temp.ddspf.dwSize = 0x20;
            temp.ddspf.dwFlags = 0x4;
            temp.ddspf.dwFourCC = 0x31545844;
            temp.dwCaps = 0x1000;
            return temp;

        }

        public TGAHeader CreateHeaderTGA()
        {
            TGAHeader temp = new TGAHeader();
            temp.imagetype = 0x2;
            temp.bits = 0x18;
            return temp;

        }

        static byte[] StructureToByteArray(object obj)
        {

            int len = Marshal.SizeOf(obj);
            byte[] arr = new byte[len];
            IntPtr ptr = Marshal.AllocHGlobal(len);
            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, arr, 0, len);
            Marshal.FreeHGlobal(ptr);
            return arr;

        }

        private void ClearFiles()
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (System.IO.File.Exists(loc + "\\exec\\temp.dat"))
                System.IO.File.Delete(loc + "\\exec\\temp.dat");
            if (System.IO.File.Exists(loc + "\\exec\\out.dat"))
                System.IO.File.Delete(loc + "\\exec\\out.dat");
        }

#endregion
    }
}
