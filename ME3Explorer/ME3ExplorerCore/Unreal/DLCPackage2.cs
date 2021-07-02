//using ME3ExplorerCore.Helpers;
//using System;
//using System.IO;

//namespace ME3ExplorerCore.Unreal
//{
//    class DLCPackage2
//    {
//        public string FileName;

//        public struct HeaderStruct
//        {
//            public uint Magic;
//            public uint Version;
//            public uint DataOffset;
//            public uint EntryOffset;
//            public uint FileCount;
//            public uint BlockTableOffset;
//            public uint MaxBlockSize;
//            public char[] CompressionScheme;

//            public void Read(Stream stream)
//            {
//                Magic = stream.ReadUInt32();
//                Version = stream.ReadUInt32();
//                DataOffset = stream.ReadUInt32();
//                EntryOffset = stream.ReadUInt32();
//                FileCount = stream.ReadUInt32();
//                BlockTableOffset = stream.ReadUInt32();
//                MaxBlockSize = stream.ReadUInt32();

//                CompressionScheme = stream.ReadStringASCII(4).ToCharArray();
//                if (Magic != 0x53464152 ||
//                    Version != 0x00010000 ||
//                    MaxBlockSize != 0x00010000)
//                    throw new Exception("Not supported DLC file!");
//            }

//            //public TreeNode ToTree()
//            //{
//            //    TreeNode result = new TreeNode("Header");
//            //    result.Nodes.Add("Magic : " + Magic.ToString("X8"));
//            //    result.Nodes.Add("Version : " + Version.ToString("X8"));
//            //    result.Nodes.Add("DataOffset : " + DataOffset.ToString("X8"));
//            //    result.Nodes.Add("EntryOffset : " + EntryOffset.ToString("X8"));
//            //    result.Nodes.Add("FileCount : " + FileCount.ToString("X8"));
//            //    result.Nodes.Add("BlockTableOffset : " + BlockTableOffset.ToString("X8"));
//            //    result.Nodes.Add("MaxBlockSize : " + MaxBlockSize.ToString("X8"));
//            //    string Scheme = "";
//            //    for (int i = 3; i >= 0; i--)
//            //        Scheme += CompressionScheme[i];
//            //    result.Nodes.Add("Scheme : " + Scheme);
//            //    return result;
//            //}
//        }

//        public struct FileEntryStruct
//        {
//            public HeaderStruct Header;
//            public uint MyOffset;
//            public byte[] Hash;
//            public uint BlockSizeIndex;
//            public uint UncompressedSize;
//            public byte UncompressedSizeAdder;
//            public long RealUncompressedSize;
//            public uint DataOffset;
//            public byte DataOffsetAdder;
//            public long RealDataOffset;
//            public long BlockTableOffset;
//            public long[] BlockOffsets;
//            public ushort[] BlockSizes;
//            public string FileName;

//            public void Read(Stream s, HeaderStruct header)
//            {
//                Header = header;
//                MyOffset = (uint) s.Position;
//                Hash = s.ReadToBuffer(16);
//                BlockSizeIndex = s.ReadUInt32();
//                UncompressedSize = s.ReadUInt32();
//                UncompressedSizeAdder = (byte) s.ReadByte();
//                RealUncompressedSize = UncompressedSize + UncompressedSizeAdder << 32; //... This does nothing?
//                DataOffset = s.ReadUInt32();
//                DataOffsetAdder = (byte) s.ReadByte();
//                RealDataOffset = DataOffset + DataOffsetAdder << 32; //... This does nothing?
//                if ((int) BlockSizeIndex == -1)
//                {
//                    BlockOffsets = new long[1];
//                    BlockOffsets[0] = RealDataOffset;
//                    BlockSizes = new ushort[1];
//                    BlockSizes[0] = (ushort) UncompressedSize;
//                    BlockTableOffset = 0;
//                }
//                else
//                {
//                    //Todo: Finish this someday to make DLCPackage work with Testpatch added files
//                    //int numBlocks = (int)Math.Ceiling(UncompressedSize / (double)header.MaxBlockSize);
//                    ////if (con.isLoading)
//                    ////{
//                    //BlockOffsets = new long[numBlocks];
//                    //BlockSizes = new ushort[numBlocks];
//                    ////}
//                    //BlockOffsets[0] = RealDataOffset;
//                    //long pos = con.Memory.Position;
//                    //con.Seek((int)getBlockOffset((int)BlockSizeIndex, header.EntryOffset, header.FileCount), SeekOrigin.Begin);
//                    //BlockTableOffset = con.Memory.Position;
//                    //BlockSizes[0] = con + BlockSizes[0];
//                    //for (int i = 1; i < numBlocks; i++)
//                    //{
//                    //    BlockSizes[i] = con + BlockSizes[i];
//                    //    BlockOffsets[i] = BlockOffsets[i - 1] + BlockSizes[i];
//                    //}
//                    //con.Seek((int)pos, SeekOrigin.Begin);
//                }
//            }
//        }
//    }
//}
