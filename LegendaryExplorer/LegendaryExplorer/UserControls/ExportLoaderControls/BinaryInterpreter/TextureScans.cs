using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    public partial class BinaryInterpreterWPF
    {
        private BinInterpNode MakeTextureMipNode(EndianReader bin, int mipNumber)
        {
            var mipMapNode = new BinInterpNode
            {
                Header = $"0x{bin.Position:X4} MipMap #{mipNumber}",
                Offset = (int)(bin.Position)
            };

            StorageTypes storageType = (StorageTypes)bin.ReadInt32();
            mipMapNode.Items.Add(new BinInterpNode
            {
                Header = $"0x{bin.Position - 4:X4} Storage Type: {storageType}",
                Offset = (int)(bin.Position - 4)
            });

            var uncompressedSize = bin.ReadInt32();
            mipMapNode.Items.Add(new BinInterpNode
            {
                Header = $"0x{bin.Position - 4:X4} Uncompressed Size: {uncompressedSize}",
                Offset = (int)(bin.Position - 4)
            });

            var compressedSize = bin.ReadInt32();
            mipMapNode.Items.Add(new BinInterpNode
            {
                Header = $"0x{bin.Position - 4:X4} Compressed Size: {compressedSize}",
                Offset = (int)(bin.Position - 4)
            });

            var dataOffset = bin.ReadInt32();
            mipMapNode.Items.Add(new BinInterpNode
            {
                Header = $"0x{bin.Position - 4:X4} Data Offset: 0x{dataOffset:X8}",
                Offset = (int)(bin.Position - 4)
            });

            switch (storageType)
            {
                case StorageTypes.pccUnc:
                    bin.Skip(uncompressedSize);
                    break;
                case StorageTypes.pccLZO:
                case StorageTypes.pccZlib:
                case StorageTypes.pccOodle:
                    bin.Skip(compressedSize);
                    break;
            }

            var mipWidth = bin.ReadInt32();
            mipMapNode.Items.Add(new BinInterpNode
            {
                Header = $"0x{bin.Position - 4:X4} Mip Width: {mipWidth}",
                Offset = (int)(bin.Position - 4),
                Tag = NodeType.StructLeafInt
            });

            var mipHeight = bin.ReadInt32();
            mipMapNode.Items.Add(new BinInterpNode
            {
                Header = $"0x{bin.Position - 4:X4} Mip Height: {mipHeight}",
                Offset = (int)(bin.Position - 4),
                Tag = NodeType.StructLeafInt
            });

            return mipMapNode;
        }

        private void ReadBulkData(EndianReader bin, List<ITreeItem> subnodes, string bulkDataName = null)
        {
            subnodes.Add(new BinInterpNode(bin.Position, $"BulkDataFlags: {(EBulkDataFlags)bin.ReadUInt32()}"));
            subnodes.Add(new BinInterpNode(bin.Position, $"Element Count: {bin.ReadInt32()}"));
            int bulkSize = 0;
            if (bulkDataName != null)
            {
                subnodes.Add(MakeInt32Node(bin, $"BulkDataSizeOnDisk ({bulkDataName})", out bulkSize));
            }
            else
            {
                subnodes.Add(MakeInt32Node(bin, "BulkDataSizeOnDisk", out bulkSize));
            }
            subnodes.Add(MakeUInt32HexNode(bin, "BulkDataOffsetInFile"));
            bin.Skip(bulkSize);
        }

        private List<ITreeItem> StartTextureBinaryScan(byte[] data, int binarystart)
        {
            var subnodes = new List<ITreeItem>();

            try
            {
                var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
                if (bin.Length == binarystart)
                    return subnodes; // no binary data

                bin.JumpTo(binarystart);
                if (Pcc.Game is not (MEGame.ME3 or MEGame.LE3) || (Pcc.FilePath != null && Pcc.FilePath.EndsWith(".upk")))
                {
                    ReadBulkData(bin, subnodes, "SourceArt");
                }

                if (CurrentLoadedExport != null &&
                    CurrentLoadedExport.ClassName.ToLower() is "texturecube" or "texturerendertarget2d")
                {
                    return subnodes; // No more nodes to parse
                }

                subnodes.Add(MakeInt32Node(bin, "NumMipMaps", out var numMipMaps));
                for (int l = 0; l < numMipMaps; l++)
                {
                    subnodes.Add(MakeTextureMipNode(bin, l));
                }

                if (Pcc.Game != MEGame.UDK)
                {
                    subnodes.Add(MakeInt32Node(bin, "Unknown Int"));
                }
                if (CurrentLoadedExport.FileRef.Game != MEGame.ME1)
                {
                    subnodes.Add(MakeGuidNode(bin, "Texture GUID"));
                }

                if (Pcc.Game == MEGame.UDK)
                {
                    // Extra mips?
                    subnodes.Add(MakeInt32Node(bin, "CachedPVRTCMips", out var extraMips1));
                    for (int l = 0; l < extraMips1; l++)
                    {
                        subnodes.Add(MakeTextureMipNode(bin, l));
                    }
                    subnodes.Add(MakeInt32Node(bin, "CachedFlashMipsMaxResolution"));

                    subnodes.Add(MakeInt32Node(bin, "CachedATITCMips", out var extraMips2));
                    for (int l = 0; l < extraMips2; l++)
                    {
                        subnodes.Add(MakeTextureMipNode(bin, l));
                    }

                    
                    ReadBulkData(bin, subnodes, "CachedFlashMips");

                    subnodes.Add(MakeInt32Node(bin, "CachedETCMips", out var extraMips4));
                    for (int l = 0; l < extraMips4; l++)
                    {
                        subnodes.Add(MakeTextureMipNode(bin, l));
                    }
                }

                if (Pcc.Game == MEGame.ME3 || Pcc.Game.IsLEGame())
                {
                    subnodes.Add(MakeInt32Node(bin, "Unknown Int ME3/LE"));
                }

                if (Pcc.Game >= MEGame.ME3 && CurrentLoadedExport.ClassName == "LightMapTexture2D")
                {
                    subnodes.Add(new BinInterpNode(bin.Position, $"LightMapFlags: {(ELightMapFlags)bin.ReadInt32()}"));
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> StartTextureMovieScan(byte[] data, ref int binarystart)
        {
            /*
             *  
             *      flag 1 = external cache / 0 = local storage +4   //ME3
             *      stream length in local/TFC +4
             *      stream length in local/TFC +4 (repeat)
             *      stream offset in local/TFC +4
             *      
             *      //ME1/2 THIS IS REPEATED
             *      unknown 0  +4
             *      unknown 0  +4
             *      unknown 0  +4
             *      offset (1st)
             *      count +4  (0) 
             *      stream length in local +4
             *      stream length in local +4 (repeat)
             *      stream 2nd offset in local +4
             *  
             */
            var subnodes = new List<ITreeItem>();
            try
            {
                int pos = binarystart;
                int unk1 = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"{(pos - binarystart):X4} Flag (0 = local, 1 = external): {unk1}",
                    Offset = pos,
                });
                pos += 4;
                if (Pcc.Game is MEGame.ME3 or MEGame.LE3)
                {
                    int length = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"{(pos - binarystart):X4} Uncompressed size: {length} (0x{length:X})",
                        Offset = pos,

                        Tag = NodeType.StructLeafInt
                    });
                    pos += 4;
                    length = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinInterpNode
                    {
                        // Note: Bik's can't be compressed.
                        Header = $"{(pos - binarystart):X4} Compressed size: {length} (0x{length:X})",
                        Offset = pos,

                        Tag = NodeType.StructLeafInt
                    });
                    pos += 4;
                }
                else
                {
                    int unkME2 = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"{(pos - binarystart):X4} Unknown: {unkME2} ",
                        Offset = pos,

                        Tag = NodeType.StructLeafInt
                    });
                    pos += 4;
                    unkME2 = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"{(pos - binarystart):X4} Unknown: {unkME2} ",
                        Offset = pos,

                        Tag = NodeType.StructLeafInt
                    });
                    pos += 4;
                }

                int offset = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"{(pos - binarystart):X4} bik offset in file: {offset} (0x{offset:X})",
                    Offset = pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;

                if (Pcc.Game is not (MEGame.ME3 or MEGame.LE3))
                {
                    int unkT = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"{(pos - binarystart):X4} Flag (0 = local, 1 = external): {unkT} ",
                        Offset = pos,

                        Tag = NodeType.StructLeafInt
                    });
                    pos += 4;
                    int length = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"{(pos - binarystart):X4} bik length: {length} (0x{length:X})",
                        Offset = pos,

                        Tag = NodeType.StructLeafInt
                    });
                    pos += 4;
                    length = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"{(pos - binarystart):X4} bik length: {length} (0x{length:X})",
                        Offset = pos,

                        Tag = NodeType.StructLeafInt
                    });
                    pos += 4;
                    offset = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"{(pos - binarystart):X4} bik 2nd offset in file: {offset} (0x{offset:X})",
                        Offset = pos,

                        Tag = NodeType.StructLeafInt
                    });
                    pos += 4;
                }

                if (pos < data.Length && CurrentLoadedExport.GetProperty<NameProperty>("Filename") == null)
                {
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"{(pos - binarystart):X4} The rest of the binary is the bik.",
                        Offset = pos,

                        Tag = NodeType.Unknown
                    });
                    subnodes.Add(new BinInterpNode
                    {
                        Header = "The stream offset to this data will be automatically updated when this file is saved.",
                        Tag = NodeType.Unknown
                    });
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }
    }
}
