using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using AmaroK86.MassEffect3.ZlibBlock;

namespace ME3Explorer.UnrealHelper
{
    public class UTexture2D
    {
#region Declarations
        public byte[] memory;
        public int memsize;
        public string[] names;
        public int currpos;
        public uint index;
        public List<Property> OProps;
        public byte[] ImageRaw;
        public List<byte[]> AltImageRaw;
        public ImageFile ImageF;
        public struct Property
        {
            public string name;
            public string value;
            public byte[] raw;
        }
        public struct Texture2DHeader
        {
            public string PixelFormat;
            public string Tsource;
            public int TCompressionSet;
            public int Format;
            public int LODGroup;
            public int LODBias;
            public int CompSetting;
            public int SizeX;
            public int SizeY;
            public int OSizeX;
            public int OSizeY;
            public int MipTail;
            public int InternalBias;
            public uint offset;
            public uint offSizeX;
            public uint offSizeY;
            public uint offOSizeX;
            public uint offOSizeY;
            public byte CompNoMip;
            public byte CompNoAlpha;
            public byte NeverStream;
            public byte SRGB;
            public byte CompNone;
            public byte IsSourceArtUnComp;
            public uint memsize;
            public uint headerend;
        }
        public struct TextureRef
        {
            public uint size;
            public uint offset;
            public int HeaderOffset;
            public int sizeX;
            public int sizeY;
            public bool InCache;
        }       
        public Texture2DHeader T2D = new Texture2DHeader();
        public List<TextureRef> TextureTFCs = new List<TextureRef>();

        public string[] Props = new string[]
        {"TFCFileGuid\0"                  //0
         ,"Textures\0"
         ,"CharTextures\0"
         ,"StructProperty\0"
         ,"EPixelFormat\0"
         ,"TextureCompressionSettings\0"//5
         ,"Guid\0"
         ,"TextureFileCacheName\0"
         ,"Format\0"
         ,"LODGroup\0"
         ,"CompressionSettings\0"       //10
         ,"SizeX\0"
         ,"SizeY\0"
         ,"OriginalSizeX\0"
         ,"OriginalSizeY\0"
         ,"MipTailBaseIdx\0"            //15
         ,"LODBias\0"
         ,"InternalFormatLODBias\0"
         ,"CompressionNoMipmaps\0"
         ,"NeverStream\0"
         ,"SRGB\0"                      //20
         ,"CompressionNone\0"
         ,"UnpackMin\0"
         ,"TextureGroup\0"
         ,"None\0"
         ,"CompressionNoAlpha\0"        //25
         ,"TextureMipGenSettings\0"
         ,"bIsSourceArtUncompressed\0"
         ,"AddressX\0"
         ,"AddressY\0"
         ,"TextureAddress\0"            //30
         ,"Filter\0"
         ,"TextureFilter\0"
         ,"AdjustBrightnessCurve\0"
        };  

#endregion

        public UTexture2D(byte[] mem, string[] Names)
        {
            memory = mem;
            memsize = mem.Length;
            names = Names;
            Deserialize();
        }

#region Deserialize
        public void Deserialize()
        {
            currpos = 0;

            ReadIndex(currpos);
            ReadProperties(currpos);
            ReadImageRaw(currpos);
            ReadAltImageRaw(currpos);
        }

        private void ReadIndex(int off)
        {
            index = BitConverter.ToUInt32(memory, off);
            currpos += 4;
        }

        private void ReadProperties(int off)
        {
            TextureTFCs = new List<TextureRef>();
            TextureRef TRef = new TextureRef();
            OProps = new List<Property>();
            int pos = off;
            int MAXName = names.Length;
            string s = "",v;
            int tmp = 0;
            int n = getInt(pos);
            if(n<MAXName) s=names[n];
            int t = getName(s);
            while (t != -1 && T2D.memsize == 0)
            {
                v = "";
                switch (t)
                {
                    case 0:
                        pos += 8;
                        break;
                    case 1:
                        T2D.Tsource = "Textures";
                        pos += 8; 
                        break;
                    case 2:
                        T2D.Tsource = "CharTextures";
                        pos += 8;
                        break;
                    case 3:
                        pos += 16;
                        v = "0x" + BitConverter.ToUInt32(memory, pos - 8).ToString("X");
                        break;
                    case 4:
                        pos += 8;
                        n = getInt(pos);
                        if (n < MAXName) T2D.PixelFormat = names[n];
                        v = T2D.PixelFormat;
                        pos += 8;
                        break;
                    case 5:
                        pos += 8;
                        T2D.TCompressionSet = getInt(pos);
                        pos += 8;
                        break;
                    case 6:
                        pos += 24;
                        v = "0x" + BitConverter.ToUInt32(memory, pos - 16).ToString("X") + " 0x" + 
                                   BitConverter.ToUInt32(memory, pos - 12).ToString("X") + " 0x" + 
                                   BitConverter.ToUInt32(memory, pos - 8).ToString("X") + " 0x" + 
                                   BitConverter.ToUInt32(memory, pos - 4).ToString("X");
                        break;
                    case 7:
                        pos += 24;
                        int src = getInt(pos);
                        T2D.Tsource = names[src].Substring(0,Math.Max(0,names[src].Length-1));
                        pos += 8;
                        v = T2D.Tsource;
                        break;
                    case 8:
                        pos += 16;
                        T2D.Format = getInt(pos);
                        v = "0x" + BitConverter.ToUInt32(memory, pos).ToString("X");
                        pos += 8;
                        break;
                    case 9:
                        pos += 16;
                        T2D.LODGroup = getInt(pos);
                        v = "0x" + BitConverter.ToUInt32(memory, pos).ToString("X");
                        pos += 8;
                        break;
                    case 10:
                        pos += 16;
                        T2D.CompSetting = getInt(pos);
                        v = "0x" + BitConverter.ToUInt32(memory, pos).ToString("X");
                        pos += 8;
                        break;
                    case 11:
                        pos += 24;
                        T2D.SizeX = getInt(pos);
                        T2D.offSizeX =(uint)pos;
                        v = BitConverter.ToUInt32(memory, pos) + " pixels";
                        pos += 4;
                        break;
                    case 12:
                        pos += 24;
                        T2D.SizeY = getInt(pos);
                        T2D.offSizeY = (uint)pos;
                        v = BitConverter.ToUInt32(memory, pos) + " pixels";
                        pos += 4;
                        break;
                    case 13:
                        pos += 24;
                        T2D.OSizeX = getInt(pos);
                        T2D.offOSizeX = (uint)pos;
                        v = BitConverter.ToUInt32(memory, pos) + " pixels";
                        pos += 4;
                        break;
                    case 14:
                        pos += 24;
                        T2D.OSizeY = getInt(pos);
                        T2D.offOSizeY = (uint)pos;
                        v = BitConverter.ToUInt32(memory, pos) + " pixels";
                        pos += 4;
                        break;
                    case 15:
                        pos += 24;
                        T2D.MipTail = getInt(pos);
                        v = "0x" + BitConverter.ToUInt32(memory, pos).ToString("X");
                        pos += 4;
                        break;
                    case 16:
                        pos += 24;
                        T2D.LODBias = getInt(pos);
                        v = "0x" + BitConverter.ToUInt32(memory, pos).ToString("X");
                        pos += 4;
                        break;
                    case 17:
                        pos += 24;
                        T2D.InternalBias = getInt(pos);
                        v = "0x" + BitConverter.ToUInt32(memory, pos).ToString("X");
                        pos += 4;
                        break;
                    case 18:
                        pos += 12;
                        T2D.CompNoMip = memory[pos];
                        v = "0x" + BitConverter.ToUInt32(memory, pos).ToString("X");
                        pos += 13;
                        break;
                    case 19:
                        pos += 12;
                        T2D.NeverStream = memory[pos];
                        v = "0x" + BitConverter.ToUInt32(memory, pos).ToString("X");
                        pos += 13;
                        break;
                    case 20:
                        pos += 12;
                        T2D.SRGB = memory[pos];
                        v = "0x" + BitConverter.ToUInt32(memory, pos).ToString("X");
                        pos += 13;
                        break; 
                    case 21:
                        pos += 12;
                        T2D.CompNone = memory[pos];
                        v = "0x" + BitConverter.ToUInt32(memory, pos).ToString("X");
                        pos += 13;
                        break;
                    case 22:
                        pos += 28;
                        v = "0x" + BitConverter.ToUInt32(memory, pos - 4).ToString("X");
                        break;
                    case 23:
                        pos += 24;
                        tmp = getInt(pos);
                        pos += 8;
                        if (tmp<names.Length && names[tmp] == "ByteProperty\0")
                            pos += 8;
                        else
                        {
                            T2D.memsize = (uint)getInt(pos);
                            TRef.sizeX = T2D.SizeX;
                            TRef.sizeY = T2D.SizeY;
                            TRef.size = getUInt(pos + 4);
                            TRef.offset = getUInt(pos + 8); ;
                            TRef.HeaderOffset = pos - 12;
                            TRef.InCache = true;
                            TextureTFCs.Add(TRef);
                            pos += 12;
                                while (isSquare2(getInt(pos)) && isSquare2(getInt(pos + 4)))
                                {
                                    T2D.SizeX = getInt(pos) / 2;
                                    T2D.SizeY = getInt(pos + 4) / 2;
                                    if (T2D.SizeX == 0)
                                        T2D.SizeX = 1;
                                    if (T2D.SizeY == 0)
                                        T2D.SizeY = 1;
                                    TRef.sizeX = T2D.SizeX;
                                    TRef.sizeY = T2D.SizeY;
                                    TRef.size = getUInt(pos + 16);
                                    TRef.offset = getUInt(pos + 20); ;
                                    TRef.HeaderOffset = pos;
                                    TextureTFCs.Add(TRef);
                                    T2D.memsize = (uint)getInt(pos + 12);
                                    pos += 24;
                                }
                            T2D.headerend = (uint)pos;
                        }
                        break;
                    case 24:
                        pos += 16;
                        T2D.memsize = (uint)getInt(pos);
                        TRef.sizeX = T2D.SizeX;
                        TRef.sizeY = T2D.SizeY;
                        TRef.size = getUInt(pos + 4);
                        TRef.offset = getUInt(pos + 8);
                        TRef.HeaderOffset = pos - 12;
                        TRef.InCache = true;
                        TextureTFCs.Add(TRef);
                        pos += 12;
                                while (isSquare2(getInt(pos)) && isSquare2(getInt(pos + 4)))
                                {
                                    T2D.SizeX = getInt(pos) / 2;
                                    T2D.SizeY = getInt(pos + 4) / 2;
                                    if (T2D.SizeX == 0)
                                        T2D.SizeX = 1;
                                    if (T2D.SizeY == 0)
                                        T2D.SizeY = 1;
                                    TRef.sizeX = T2D.SizeX;
                                    TRef.sizeY = T2D.SizeY;
                                    TRef.size = getUInt(pos + 16);
                                    TRef.offset = getUInt(pos + 20); ;
                                    TRef.HeaderOffset = pos;
                                    TRef.InCache = true;
                                    TextureTFCs.Add(TRef);
                                    T2D.memsize = (uint)getInt(pos + 12);
                                    pos += 24;
                                }
                        T2D.headerend = (uint)pos;
                        break;
                    case 25:
                        pos += 12;
                        T2D.CompNoAlpha = memory[pos];
                        v = "0x" + BitConverter.ToUInt32(memory, pos).ToString("X");
                        pos += 13;
                        break; 
                    case 26:
                        pos += 16;
                        v = "0x" + BitConverter.ToUInt32(memory, pos - 4).ToString("X");
                        break;
                    case 27:
                        pos += 12;
                        T2D.IsSourceArtUnComp = memory[pos];
                        v = "0x" + BitConverter.ToUInt32(memory, pos).ToString("X");
                        pos += 13;
                        break;
                    case 28:
                        pos += 24;
                        v = "0x" + BitConverter.ToUInt32(memory, pos - 4).ToString("X");
                        break;
                    case 29:
                        pos += 24;
                        v = "0x" + BitConverter.ToUInt32(memory, pos - 4).ToString("X");
                        break;
                    case 30:
                        pos += 16;
                        v = "0x" + BitConverter.ToUInt32(memory, pos - 4).ToString("X");
                        break;
                    case 31:
                        pos += 24;
                        v = "0x" + BitConverter.ToUInt32(memory, pos - 8).ToString("X");
                        break;
                    case 32:
                        pos += 16;
                        v = "";
                        break;
                    case 33:
                        pos += 28;
                        v = "0x" + BitConverter.ToSingle(memory, pos - 4).ToString();
                        break;

                }
                OProps.Add(makeProp(s,v,ReadRaw(currpos,pos-currpos)));
                s = "";
                n = getInt(pos);
                if (n>= 0 && n < MAXName) s = names[n];
                t = getName(s);
            }
            currpos = pos;
        }

        private void ReadImageRaw(int off)
        {
            if (TextureTFCs == null || TextureTFCs.Count < 1)
                return;
            int size = (int)TextureTFCs[TextureTFCs.Count - 1].size;
            TextureRef t = TextureTFCs[TextureTFCs.Count - 1];
            t.offset = (uint)off;
            t.InCache = false;
            TextureTFCs[TextureTFCs.Count - 1] = t;
            ImageRaw = new byte[size];
            if (off + size >= memory.Length)
                return;
            for (int i = 0; i < size; i++)
                ImageRaw[i] = memory[off + i];
            ImageF = new ImageFile(ImageRaw, T2D.PixelFormat, T2D.SizeX, T2D.SizeY);
            currpos = off + size;
        }

        private void ReadAltImageRaw(int off)
        {
            AltImageRaw = new List<byte[]>();
            int size = getInt(off);
            byte[] buff;
            TextureRef t;
            int pos = off;
            while (isSquare2(size))
            {
                int sizeX = size;
                int sizeY = getInt(pos + 4);
                int msize = getInt(pos + 16);
                if (pos + msize > memsize)
                {
                    pos += 24;
                    break;
                }
                int offset = pos + 24;
                t = new TextureRef();
                t.sizeX = sizeX / 2;                
                t.sizeY = sizeY / 2;
                if (t.sizeX == 0)
                    t.sizeX = 1;
                if (t.sizeY == 0)
                    t.sizeY = 1;
                t.size = (uint)msize;
                t.offset = (uint)offset;
                t.InCache = false;
                buff = new byte[msize + 24];
                for (int i = 0; i < msize + 24; i++)
                    buff[i] = memory[pos + i];
                pos += msize + 24;
                TextureTFCs.Add(t);
                AltImageRaw.Add(buff);
                size = getInt(pos);
            }
            currpos = pos;
        }

#endregion

#region Serialize
        public byte[] Serialize()
        {
            return memory;
        }
#endregion

#region Export

        public Texture getDirectXTexture(Device device)
        {
            MemoryStream m = new MemoryStream();
            m.Write(ImageRaw, 0, ImageRaw.Length);
            Texture t = TextureLoader.FromStream(device,m);
            
            return t;
        }

        public void MakePreview()
        {
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (T2D.PixelFormat == "PF_DXT1\0" || T2D.PixelFormat == "PF_DXT5\0")
            {
                if (File.Exists(loc + "\\exec\\preview00.tga"))
                    File.Delete(loc + "\\exec\\preview00.tga");
                if (File.Exists(loc + "\\exec\\preview.tga"))
                    File.Delete(loc + "\\exec\\preview.tga");
                ExportToFile(loc + "\\exec\\preview.dds");
                RunShell(loc + "\\exec\\readdxt.exe", "preview.dds");
                File.Copy(loc + "\\exec\\preview00.tga", loc + "\\exec\\preview.tga",true);
            }
            if (T2D.PixelFormat == "PF_A8R8G8B8\0" || T2D.PixelFormat == "PF_G8\0" || T2D.PixelFormat == "PF_V8U8\0")
                if (ImageF != null)
                    ImageF.ExportToFile(loc + "\\exec\\preview.tga");
        }

        public void MakePreview(int idx)
        {
            if (idx < 0 || idx >= TextureTFCs.Count)
                return;
            string loc = Path.GetDirectoryName(Application.ExecutablePath);
            if (T2D.PixelFormat == "PF_DXT1\0" || T2D.PixelFormat == "PF_DXT5\0")
            {
                if (File.Exists(loc + "\\exec\\preview00.tga"))
                    File.Delete(loc + "\\exec\\preview00.tga");
                if (File.Exists(loc + "\\exec\\preview.tga"))
                    File.Delete(loc + "\\exec\\preview.tga");
                ExportToFile(loc + "\\exec\\preview.dds");
                RunShell(loc + "\\exec\\readdxt.exe", "preview.dds");
                File.Copy(loc + "\\exec\\preview00.tga", loc + "\\exec\\preview.tga", true);
            }
            if (T2D.PixelFormat == "PF_A8R8G8B8\0" || T2D.PixelFormat == "PF_G8\0" || T2D.PixelFormat == "PF_V8U8\0")
                if (ImageF != null)
                    ImageF.ExportToFile(loc + "\\exec\\preview.tga");
        }

        public void ExportToFile(string path = "")
        {
            if (ImageF != null)
                ImageF.ExportToFile(path);
        }
        public Texture ExportToTexture(Device device)
        {
            if (ImageF != null)
            {
                Texture t = null;
                
                MemoryStream m = ImageF.ExportToStream();
                m.Seek(0, SeekOrigin.Begin);
                if(m!=null)
                    t = TextureLoader.FromStream(device, m);
                return t;
            }
            else
                return null;
        }

        public Texture ExportToTexture(Device device,int tfc,string pathTex,string pathCTex)
        {
            if (tfc < 0 || tfc >= TextureTFCs.Count)
                return null;
            if (ImageF != null)
            {
                Texture t = null;
                MemoryStream m = new MemoryStream();
                if (T2D.Tsource == "Textures" &&
                   TextureTFCs[tfc].offset > 0 &&
                   TextureTFCs[tfc].size > 0 &&
                   TextureTFCs[tfc].size != TextureTFCs[tfc].offset)
                    m = ExportToStream(tfc, pathTex);
                if (T2D.Tsource == "CharTextures" &&
                   TextureTFCs[tfc].offset > 0 &&
                   TextureTFCs[tfc].size > 0 &&
                    TextureTFCs[tfc].size != TextureTFCs[tfc].offset)
                    m = ExportToStream(tfc, pathCTex);
                if (m != null && m.Length > 0)
                {
                    m.Seek(0, SeekOrigin.Begin);
                    t = TextureLoader.FromStream(device, m);
                }
                return t;
            }
            else
                return null;
        }

        public void ExportToFile(int tfc)
        {
            if (tfc < 0 || tfc >= TextureTFCs.Count)
                return;
            if (TextureTFCs[tfc].InCache == true)
            {
                TFCFile TFCf = new TFCFile(T2D.Tsource);
                if (TFCf.CheckTFC(TextureTFCs[tfc].offset))
                {
                    ImageF = new ImageFile(TFCf.getRawTFCComp(TextureTFCs[tfc].offset), T2D.PixelFormat, TextureTFCs[tfc].sizeX, TextureTFCs[tfc].sizeY);
                }
                else
                {
                    ImageF = new ImageFile(TFCf.getRawTFC(TextureTFCs[tfc].offset, (int)TextureTFCs[tfc].size), T2D.PixelFormat, TextureTFCs[tfc].sizeX, TextureTFCs[tfc].sizeY);
                }
            }
            else
            {
                int SizeX = TextureTFCs[tfc].sizeX;
                int SizeY = TextureTFCs[tfc].sizeY;
                int Size = (int)TextureTFCs[tfc].size;
                int Offset = (int)TextureTFCs[tfc].offset;
                byte[] buff = new byte[Size];
                for (int i = 0; i < Size; i++)
                    buff[i] = memory[Offset + i];
                ImageF = new ImageFile(buff, T2D.PixelFormat, SizeX, SizeY);
            }
            if (T2D.PixelFormat == "PF_DXT1\0" || 
                T2D.PixelFormat == "PF_DXT5\0" || 
                T2D.PixelFormat == "PF_A8R8G8B8\0" || 
                T2D.PixelFormat == "PF_G8\0" ||
                T2D.PixelFormat == "PF_V8U8\0")
            {
                ImageF.ExportToFile("");
            }
        }

        public void ExportToFile(int tfc, string path = "")
        {
            if (tfc < 0 || tfc >= TextureTFCs.Count)
                return;
            if (TextureTFCs[tfc].InCache == true)
            {
                TFCFile TFCf = new TFCFile(T2D.Tsource);
                if (TFCf.CheckTFC(TextureTFCs[tfc].offset))
                {
                    ImageF = new ImageFile(TFCf.getRawTFCComp(TextureTFCs[tfc].offset), T2D.PixelFormat, TextureTFCs[tfc].sizeX, TextureTFCs[tfc].sizeY);
                }
                else
                {
                    ImageF = new ImageFile(TFCf.getRawTFC(TextureTFCs[tfc].offset, (int)TextureTFCs[tfc].size), T2D.PixelFormat, TextureTFCs[tfc].sizeX, TextureTFCs[tfc].sizeY);
                }
            }
            else
            {
                int SizeX = TextureTFCs[tfc].sizeX;
                int SizeY = TextureTFCs[tfc].sizeY;
                int Size = (int)TextureTFCs[tfc].size;
                int Offset = (int)TextureTFCs[tfc].offset;
                byte[] buff = new byte[Size];
                for (int i = 0; i < Size; i++)
                    buff[i] = memory[Offset + i];
                ImageF = new ImageFile(buff, T2D.PixelFormat, SizeX, SizeY);
            }
            if (T2D.PixelFormat == "PF_DXT1\0" ||
                T2D.PixelFormat == "PF_DXT5\0" ||
                T2D.PixelFormat == "PF_A8R8G8B8\0" ||
                T2D.PixelFormat == "PF_G8\0" ||
                T2D.PixelFormat == "PF_V8U8\0")
                ImageF.ExportToFile(path);
        }

        public void ExportToFile(int tfc, string path = "", string cachepath = "")
        {
            if (tfc < 0 || tfc >= TextureTFCs.Count)
                return;
            if (TextureTFCs[tfc].InCache == true)
            {
                TFCFile TFCf = new TFCFile(cachepath);
                if (TFCf.CheckTFC(TextureTFCs[tfc].offset))
                {
                    ImageF = new ImageFile(TFCf.getRawTFCComp(TextureTFCs[tfc].offset), T2D.PixelFormat, TextureTFCs[tfc].sizeX, TextureTFCs[tfc].sizeY);
                }
                else
                    return;
            }
            else
            {
                int SizeX = TextureTFCs[tfc].sizeX;
                int SizeY = TextureTFCs[tfc].sizeY;
                int Size = (int)TextureTFCs[tfc].size;
                int Offset = (int)TextureTFCs[tfc].offset;
                byte[] buff = new byte[Size];
                for (int i = 0; i < Size; i++)
                    buff[i] = memory[Offset + i];
                ImageF = new ImageFile(buff, T2D.PixelFormat, SizeX, SizeY);
            }
            if (T2D.PixelFormat == "PF_DXT1\0" ||
                T2D.PixelFormat == "PF_DXT5\0" ||
                T2D.PixelFormat == "PF_A8R8G8B8\0" ||
                T2D.PixelFormat == "PF_G8\0" ||
                T2D.PixelFormat == "PF_V8U8\0")
                ImageF.ExportToFile(path);
        }

        public MemoryStream ExportToStream(int tfc, string cachepath)
        {
            MemoryStream m = new MemoryStream();
            if (tfc < 0 || tfc >= TextureTFCs.Count)
                return m;
            if (TextureTFCs[tfc].InCache && cachepath != "")
            {
                TFCFile TFCf = new TFCFile(cachepath);
                if (TFCf.CheckTFC(TextureTFCs[tfc].offset))
                    ImageF = new ImageFile(TFCf.getRawTFCComp(TextureTFCs[tfc].offset), T2D.PixelFormat, TextureTFCs[tfc].sizeX, TextureTFCs[tfc].sizeY);
                else
                    return m;
            }
            else
            {
                if (ImageF != null)
                    m = ImageF.ExportToStream();
                return m;
            }
            if (!TextureTFCs[tfc].InCache)
            {
                int SizeX = TextureTFCs[tfc].sizeX;
                int SizeY = TextureTFCs[tfc].sizeY;
                int Size = (int)TextureTFCs[tfc].size;
                int Offset = (int)TextureTFCs[tfc].offset;
                byte[] buff = new byte[Size];
                for (int i = 0; i < Size; i++)
                    buff[i] = memory[Offset + i];
                ImageF = new ImageFile(buff, T2D.PixelFormat, SizeX, SizeY);
            }
            if (T2D.PixelFormat == "PF_DXT1\0" ||
                T2D.PixelFormat == "PF_DXT5\0" ||
                T2D.PixelFormat == "PF_A8R8G8B8\0" ||
                T2D.PixelFormat == "PF_G8\0" ||
                T2D.PixelFormat == "PF_V8U8\0")
                m = ImageF.ExportToStream();
            return m;
        }


        public TreeNode ExportToTree()
        {
            TreeNode ret = new TreeNode("Texture2D");
            ret.Nodes.Add(PropsToTree());
            ret.Nodes.Add(TFCToTree());
            return ret;
        }

        public TreeNode PropsToTree()
        {
            TreeNode ret = new TreeNode("Properties");
            if (OProps == null)
                return ret;
            for (int i = 0; i < OProps.Count; i++)
            {
                TreeNode t = new TreeNode(OProps[i].name);
                TreeNode t2 = new TreeNode(OProps[i].value);
                t.Nodes.Add(t2);
                ret.Nodes.Add(t);
            }
            return ret;
        }

        public TreeNode TFCToTree()
        {
            TreeNode ret = new TreeNode("TFC References");
            if (TextureTFCs == null)
                return ret;
            for (int i = 0; i < TextureTFCs.Count; i++)
            {
                string s = "Ref. " + i.ToString();
                s += " Size: " + TextureTFCs[i].sizeX + "x" + TextureTFCs[i].sizeY;
                s += " Memsize: " + (int)TextureTFCs[i].size + " Offset:" + (int)TextureTFCs[i].offset;
                s += " Cached: " + TextureTFCs[i].InCache;
                TreeNode t = new TreeNode(s);
                ret.Nodes.Add(t);
            }
            return ret;
        }

#endregion

#region Import

        public void ImportFromFile(int tfc)
        {
            OpenFileDialog Dialog = new OpenFileDialog();
            if (T2D.PixelFormat == "PF_DXT1\0" ||
                T2D.PixelFormat == "PF_DXT5\0")
                Dialog.Filter = "DDS Files (*.dds)|*.dds";
            if (T2D.PixelFormat == "PF_G8\0" ||
                T2D.PixelFormat == "PF_V8U8\0" ||
                T2D.PixelFormat == "PF_A8R8G8B8\0")
                Dialog.Filter = "TGA Files (*.tga)|*.tga";
            int format = -1;
            if (T2D.PixelFormat == "PF_DXT1\0")
                format = 0;
            if (T2D.PixelFormat == "PF_DXT5\0")
                format = 1;
            if (T2D.PixelFormat == "PF_V8U8\0")
                format = 2;
            if (T2D.PixelFormat == "PF_A8R8G8B8\0")
                format = 3;
            if (T2D.PixelFormat == "PF_G8\0")
                format = 4;
            if (!TextureTFCs[tfc].InCache)
            {
                if (Dialog.ShowDialog() == DialogResult.OK)
                {
                    if (format == 0 || format == 1)
                    {
                        ImageFile t = new ImageFile();
                        t.ImportFromFile(Dialog.FileName);
                        if (t.ImageSizeX == TextureTFCs[tfc].sizeX &&
                            t.ImageSizeY == TextureTFCs[tfc].sizeY &&
                            t.ImageFormat == format &&
                            t.memsize == TextureTFCs[tfc].size)
                        {
                            for (int i = 0; i < t.memsize; i++)
                                memory[TextureTFCs[tfc].offset + i] = t.memory[i];
                            MessageBox.Show("Done.");
                        }
                    }
                    if (format > 1 && format < 5)
                    {
                        ImageFile t = new ImageFile();
                        t.ImportFromFile(Dialog.FileName);
                        if (t.ImageSizeX == TextureTFCs[tfc].sizeX &&
                            t.ImageSizeY == TextureTFCs[tfc].sizeY)
                            if (t.ImageBits != 32)
                            {
                                MessageBox.Show("Please use 32 bit Targa image!");
                            }
                            else
                            {
                                switch (format)
                                {
                                    case 2:
                                        for (int i = 0; i < t.memsize / 4; i++)
                                        {
                                            memory[TextureTFCs[tfc].offset + i * 2] = t.memory[i * 4];
                                            memory[TextureTFCs[tfc].offset + i * 2 + 1] = t.memory[i * 4 + 2];
                                        }
                                        break;
                                    case 3:
                                        for (int i = 0; i < t.memsize; i++)
                                            memory[TextureTFCs[tfc].offset + i] = t.memory[i];
                                        break;
                                    case 4:
                                        for (int i = 0; i < t.memsize / 4; i++)
                                            memory[TextureTFCs[tfc].offset + i] = t.memory[i * 4];
                                        break;
                                }
                                MessageBox.Show("Done.");
                            }
                    }
                }
            }
            else
                if (Dialog.ShowDialog() == DialogResult.OK)
                {
                    ImageFile t = new ImageFile();
                    t.ImportFromFile(Dialog.FileName);
                    if ((format == 0 || format == 1) && t.ImageFormat == format)
                        if (t.ImageSizeX == TextureTFCs[tfc].sizeX &&
                            t.ImageSizeY == TextureTFCs[tfc].sizeY)
                        {
                            TFCFile TFCf = new TFCFile(T2D.Tsource);
                            byte[] buff;
                            if (TFCf.isTFCCompressed())
                                buff = ZBlock.Compress(t.memory);
                            else
                                buff = t.memory;
                            byte[] buff2 = BitConverter.GetBytes(TFCf.getFileSize());
                            for (int i = 0; i < 4; i++)
                                memory[TextureTFCs[tfc].HeaderOffset + 20 + i] = buff2[i];
                            buff2 = BitConverter.GetBytes(buff.Length);
                            for (int i = 0; i < 4; i++)
                                memory[TextureTFCs[tfc].HeaderOffset + 16 + i] = buff2[i];
                            TFCf.AppendToTFC(buff);
                            int size = TFCf.getFileSize();
                            if (size != -1)
                            {
                                TOCeditor tc = new TOCeditor();
                                if (!tc.UpdateFile(T2D.Tsource + ".tfc", (uint)size))
                                    MessageBox.Show("Didn't found Entry");
                                tc.Close();
                            }
                        }
                        else
                        {
                            System.Windows.Forms.DialogResult m = MessageBox.Show("The size doesn't match, import anyway?", "ME3 Explorer", MessageBoxButtons.YesNo);
                            if (m == DialogResult.Yes)
                            {
                                TFCFile TFCf = new TFCFile(T2D.Tsource);
                                byte[] buff;
                                if (TFCf.isTFCCompressed())
                                    buff = ZBlock.Compress(t.memory);
                                else
                                    buff = t.memory;
                                byte[] buff2 = BitConverter.GetBytes(TFCf.getFileSize());
                                for (int i = 0; i < 4; i++)
                                    memory[TextureTFCs[tfc].HeaderOffset + 20 + i] = buff2[i];
                                buff2 = BitConverter.GetBytes(buff.Length);
                                for (int i = 0; i < 4; i++)
                                    memory[TextureTFCs[tfc].HeaderOffset + 16 + i] = buff2[i];
                                if (tfc == 0)
                                {
                                    buff2 = BitConverter.GetBytes(t.ImageSizeX);
                                    for (int i = 0; i < 4; i++)
                                        memory[T2D.offSizeX + i] = buff2[i];
                                    buff2 = BitConverter.GetBytes(t.ImageSizeY);
                                    for (int i = 0; i < 4; i++)
                                        memory[T2D.offSizeY + i] = buff2[i];
                                }
                                else
                                {
                                    buff2 = BitConverter.GetBytes(t.ImageSizeX * 2);
                                    for (int i = 0; i < 4; i++)
                                        memory[TextureTFCs[tfc].HeaderOffset + i] = buff2[i];
                                    buff2 = BitConverter.GetBytes(t.ImageSizeY * 2);
                                    for (int i = 0; i < 4; i++)
                                        memory[TextureTFCs[tfc].HeaderOffset + 4 + i] = buff2[i];
                                    TFCf.AppendToTFC(buff);
                                }
                                TFCf.AppendToTFC(buff);
                                int size = TFCf.getFileSize();
                                if (size != -1)
                                {
                                    TOCeditor tc = new TOCeditor();
                                    if (!tc.UpdateFile(T2D.Tsource + ".tfc", (uint)size))
                                        MessageBox.Show("Didn't found Entry");
                                    tc.Close();
                                }
                            }
                        }
                    if (format > 1 && format < 5)
                    {
                        if (t.ImageSizeX == TextureTFCs[tfc].sizeX &&
                            t.ImageSizeY == TextureTFCs[tfc].sizeY)
                            if (t.ImageBits != 32)
                            {
                                MessageBox.Show("Please use 32 bit Targa image!");
                            }
                            else
                            {
                                byte[] buf = new byte[0];
                                switch (format)
                                {
                                    case 2:
                                        buf = new byte[t.memsize / 2];
                                        for (int i = 0; i < t.memsize / 4; i++)
                                        {
                                            buf[i * 2] = t.memory[i * 4];
                                            buf[i * 2 + 1] = t.memory[i * 4 + 2];
                                        }
                                        break;
                                    case 3:
                                        buf = t.memory;
                                        break;
                                    case 4:
                                        buf = new byte[t.memsize / 4];
                                        for (int i = 0; i < t.memsize / 4; i++)
                                            buf[i] = t.memory[i * 4];
                                        break;
                                }
                                TFCFile TFCf = new TFCFile(T2D.Tsource);
                                byte[] buff;
                                if (TFCf.isTFCCompressed())
                                    buff = ZBlock.Compress(t.memory);
                                else
                                    buff = t.memory;
                                byte[] buff2 = BitConverter.GetBytes(TFCf.getFileSize());
                                for (int i = 0; i < 4; i++)
                                    memory[TextureTFCs[tfc].HeaderOffset + 20 + i] = buff2[i];
                                buff2 = BitConverter.GetBytes(buff.Length);
                                for (int i = 0; i < 4; i++)
                                    memory[TextureTFCs[tfc].HeaderOffset + 12 + i] = buff2[i];
                                TFCf.AppendToTFC(buff);
                                int size = TFCf.getFileSize();
                                if (size != -1)
                                {
                                    TOCeditor tc = new TOCeditor();
                                    if (!tc.UpdateFile(T2D.Tsource + ".tfc", (uint)size))
                                        MessageBox.Show("Didn't found Entry");
                                    tc.Close();
                                }
                                MessageBox.Show("Done.");
                            }
                    }
                }
        }

#endregion

#region Helpers



        private Boolean isSquare2(int v)
        {
            int[] numbers = {0x1, 0x2, 0x4, 0x8, 0x10, 0x20, 0x40, 0x80, 0x100, 0x200, 0x400, 0x800, 0x1000, 0x2000 };
            Boolean ret = false;
            for (int i = 0; i < numbers.Length; i++)
                if (numbers[i] == v)
                {
                    ret = true;
                    break;
                }
            return ret;
        }

        private byte[] ReadRaw(int off, int len)
        {
            byte[] buff = new byte[len];
            for (int i = 0; i < len; i++)
                buff[i] = memory[off + i];
            return buff;
        }

        private Property makeProp(string n, string v, byte[] raw)
        {
            Property p = new Property();
            p.name = n;
            p.value = v;
            p.raw = raw;
            return p;
        }

        private int getName(string s)
        {
            int r = -1;
            for (int i = 0; i < Props.Length; i++)
                if (Props[i] == s)
                {
                    r = i;
                    break;
                }
            return r;
        }

        private uint getUInt(int index)
        {
            return BitConverter.ToUInt32(memory,index);
        }

        private int getInt(int index)
        {
            return BitConverter.ToInt32(memory, index);
        }

        private void RunShell(string cmd, string args)
        {
            System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(cmd, args);
            procStartInfo.WorkingDirectory = Path.GetDirectoryName(cmd);
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.Start();
            proc.WaitForExit();
        }
        
#endregion
    }
}
