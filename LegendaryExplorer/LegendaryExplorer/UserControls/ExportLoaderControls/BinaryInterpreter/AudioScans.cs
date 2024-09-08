using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Sound.ISACT;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public partial class BinaryInterpreterWPF
{
    private List<ITreeItem> ReadISACTPair(byte[] data, ref int binarystart, int pairStart)
    {
        int offset = pairStart;

        var subnodes = new List<ITreeItem>();
        // ISB Offset
        var isbOffset = BitConverter.ToInt32(data, offset);
        var node = new BinInterpNode
        {
            Header = $"0x{offset:X5} ISB file offset: 0x{isbOffset:X8} (0x{(isbOffset + pairStart):X8} in binary)",
            Offset = offset,
            Tag = NodeType.StructLeafInt
        };
        subnodes.Add(node);

        // Offset is not incremented here as this method reads paired data which includes the offset
        var isactBankPair = ISACTHelper.GetPairedBanks(data[offset..]);
        subnodes.Add(MakeISACTBankNode(isactBankPair.ICBBank, offset));
        subnodes.Add(MakeISACTBankNode(isactBankPair.ISBBank, offset));

        return subnodes;
    }

    private ITreeItem MakeISACTBankNode(ISACTBank iSBBank, int binOffset)
    {
        BinInterpNode bin = new BinInterpNode(iSBBank.BankRIFFPosition + binOffset, $"{iSBBank.BankType} Bank") { IsExpanded = true };
        foreach (var bc in iSBBank.BankChunks)
        {
            MakeISACTBankChunkNode(bin, bc, binOffset);
        }

        return bin;
    }

    private void MakeISACTBankChunkNode(BinInterpNode parent, BankChunk bc, int binOffset)
    {
        if (bc is NameOnlyBankChunk)
        {
            parent.Items.Add(new BinInterpNode(bc.ChunkDataStartOffset - 4 + binOffset, bc.ToChunkDisplay()));
        }
        else if (bc is ISACTListBankChunk lbc)
        {
            var lParent = new BinInterpNode(bc.ChunkDataStartOffset - 8 + binOffset, bc.ToChunkDisplay());
            parent.Items.Add(lParent);
            foreach (var sc in lbc.SubChunks)
            {
                MakeISACTBankChunkNode(lParent, sc, binOffset);
            }
        }
        else
        {
            parent.Items.Add(new BinInterpNode(bc.ChunkDataStartOffset - 8 + binOffset, bc.ToChunkDisplay()));
        }
    }

    private ITreeItem ReadISACTNode(Stream inStream, string title, int size)
    {
        var pos = inStream.Position - 8;
        switch (title)
        {
            case "snde":
                // not a section?
                inStream.Position -= 4;
                return null;
            case "qcnt":
            {
                // reads number of integers based on 2 * size / 8
                // Don't really care about this so just gonna skip 8
                inStream.Position += 8;
                // This is not actually a length... I guess?
                return new BinInterpNode(pos, $"{title} (Sound Queue... something): {size}");
            }
            case "titl":
            {
                return new BinInterpNode(pos, $"{title} (Title): {inStream.ReadStringUnicodeNull(size)}");
            }
            case "indx":
            {
                return new BinInterpNode(pos, $"{title} (Index): {inStream.ReadInt32()}");
            }
            case "geix":
            {
                return new BinInterpNode(pos, $"{title} (???): {inStream.ReadInt32()}");
            }
            case "trks":
            {
                return new BinInterpNode(pos, $"{title} (Track count?): {inStream.ReadInt32()}");
            }
            case "gbst":
                return new BinInterpNode(pos, $"{title} (???): {inStream.ReadFloat()}");
            case "tmcd":
            case "dtmp":
            case "dtsg":
                return new BinInterpNode(pos, $"{title} (???): {inStream.ReadInt32()}");
            default:
            {
                var n = new BinInterpNode(pos, $"{title}, length {size}");
                inStream.Skip(size);
                return n;
            }
        }
    }

    private List<ITreeItem> StartSoundCueScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(data) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.Skip(binarystart);

            subnodes.Add(MakeArrayNode(bin, "EditorData", i => new BinInterpNode(bin.Position, $"{i}")
            {
                IsExpanded = true,
                Items =
                {
                    MakeEntryNode(bin, "SoundNode"),
                    MakeInt32Node(bin, "NodePosX"),
                    MakeInt32Node(bin, "NodePosY")
                }
            }, true));
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> Scan_WwiseStream(byte[] data)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = Pcc.Endian };
            bin.JumpTo(CurrentLoadedExport.propsEnd());

            if (Pcc.Game is MEGame.ME2 or MEGame.LE2)
            {
                subnodes.Add(MakeUInt32Node(bin, "Unk1"));
                subnodes.Add(MakeUInt32Node(bin, "Unk2"));
                if (Pcc.Game == MEGame.ME2 && Pcc.Platform != MEPackage.GamePlatform.PS3)
                {
                    if (bin.Skip(-8).ReadInt64() == 0)
                    {
                        return subnodes;
                    }
                    subnodes.Add(MakeGuidNode(bin, "UnkGuid"));
                    subnodes.Add(MakeUInt32Node(bin, "Unk3"));
                    subnodes.Add(MakeUInt32Node(bin, "Unk4"));
                }
            }
            subnodes.Add(new BinInterpNode(bin.Position, $"BulkDataFlags: {(EBulkDataFlags)bin.ReadUInt32()}"));
            subnodes.Add(MakeInt32Node(bin, "Element Count", out int dataSize));
            subnodes.Add(MakeInt32Node(bin, "BulkDataSizeOnDisk"));
            subnodes.Add(MakeUInt32HexNode(bin, "BulkDataOffsetInFile"));
            if (CurrentLoadedExport.GetProperty<NameProperty>("Filename") is null)
            {
                subnodes.Add(new BinInterpNode(bin.Position, "Embedded sound data. Use Soundplorer to modify this data.")
                {
                    Length = dataSize
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
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> Scan_WwiseEvent(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            // Is this right for LE3?
            if (CurrentLoadedExport.FileRef.Game is MEGame.ME3 or MEGame.LE3 || CurrentLoadedExport.FileRef.Platform == MEPackage.GamePlatform.PS3)
            {
                int count = EndianReader.ToInt32(data, binarystart, CurrentLoadedExport.FileRef.Endian);
                subnodes.Add(new BinInterpNode { Header = $"0x{binarystart:X4} Count: {count.ToString()}", Offset = binarystart });
                binarystart += 4; //+ int
                for (int i = 0; i < count; i++)
                {
                    string nodeText = $"0x{binarystart:X4} ";
                    int val = EndianReader.ToInt32(data, binarystart, CurrentLoadedExport.FileRef.Endian);
                    string name = val.ToString();
                    if (Pcc.GetEntry(val) is ExportEntry exp)
                    {
                        nodeText += $"{i}: {name} {exp.InstancedFullPath} ({exp.ClassName})";
                    }
                    else if (Pcc.GetEntry(val) is ImportEntry imp)
                    {
                        nodeText += $"{i}: {name} {imp.InstancedFullPath} ({imp.ClassName})";
                    }

                    subnodes.Add(new BinInterpNode
                    {
                        Header = nodeText,
                        Tag = NodeType.StructLeafObject,
                        Offset = binarystart
                    });
                    binarystart += 4;
                    /*
                        int objectindex = BitConverter.ToInt32(data, binarypos);
                        IEntry obj = pcc.getEntry(objectindex);
                        string nodeValue = obj.GetFullPath;
                        node.Tag = nodeType.StructLeafObject;
                        */
                }
            }
            else if (CurrentLoadedExport.FileRef.Game == MEGame.ME2 && CurrentLoadedExport.FileRef.Platform != MEPackage.GamePlatform.PS3)
            {
                var wwiseID = data.Skip(binarystart).Take(4).ToArray();
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{binarystart:X4} WwiseEventID: {wwiseID[0]:X2}{wwiseID[1]:X2}{wwiseID[2]:X2}{wwiseID[3]:X2}",
                    Tag = NodeType.Unknown,
                    Offset = binarystart
                });
                binarystart += 4;

                int count = EndianReader.ToInt32(data, binarystart, CurrentLoadedExport.FileRef.Endian);
                var Streams = new BinInterpNode
                {
                    Header = $"0x{binarystart:X4} Link Count: {count}",
                    Offset = binarystart,
                    Tag = NodeType.StructLeafInt
                };
                binarystart += 4;
                subnodes.Add(Streams); //Are these variables properly named?

                for (int s = 0; s < count; s++)
                {
                    int bankcount = EndianReader.ToInt32(data, binarystart, CurrentLoadedExport.FileRef.Endian);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"0x{binarystart:X4} BankCount: {bankcount}",
                        Tag = NodeType.StructLeafInt,
                        Offset = binarystart
                    });
                    binarystart += 4;
                    for (int b = 0; b < bankcount; b++)
                    {
                        int bank = EndianReader.ToInt32(data, binarystart, CurrentLoadedExport.FileRef.Endian);
                        subnodes.Add(new BinInterpNode
                        {
                            Header = $"0x{binarystart:X4} WwiseBank: {bank} {CurrentLoadedExport.FileRef.GetEntryString(bank)}",
                            Tag = NodeType.StructLeafObject,
                            Offset = binarystart
                        });
                        binarystart += 4;
                    }

                    int streamcount = EndianReader.ToInt32(data, binarystart, CurrentLoadedExport.FileRef.Endian);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"0x{binarystart:X4} StreamCount: {streamcount}",
                        Tag = NodeType.StructLeafInt,
                        Offset = binarystart
                    });
                    binarystart += 4;
                    for (int w = 0; w < streamcount; w++)
                    {
                        int wwstream = EndianReader.ToInt32(data, binarystart, CurrentLoadedExport.FileRef.Endian);
                        subnodes.Add(new BinInterpNode
                        {
                            Header = $"0x{binarystart:X4} WwiseStream: {wwstream} {CurrentLoadedExport.FileRef.GetEntryString(wwstream)}",
                            Tag = NodeType.StructLeafObject,
                            Offset = binarystart
                        });
                        binarystart += 4;
                    }
                }
            }
            else if (CurrentLoadedExport.Game.IsLEGame())
            {
                int count = EndianReader.ToInt32(data, binarystart, CurrentLoadedExport.FileRef.Endian);
                subnodes.Add(new BinInterpNode { Header = $"0x{binarystart:X4} EventID: {count} (0x{count:X8})", Offset = binarystart });
            }
            else
            {
                subnodes.Add(new BinInterpNode($"{CurrentLoadedExport.Game} is not supported for this scan."));
                return subnodes;
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartSoundNodeWaveScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(data) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            int count;
            if (Pcc.Game.IsLEGame() || CurrentLoadedExport.FileRef.Platform == MEPackage.GamePlatform.PS3)
            {
                subnodes.Add(new BinInterpNode(bin.Position, $"Unknown int 1: {bin.ReadInt32()}"));
                subnodes.Add(new BinInterpNode(bin.Position, $"Unknown int 2: {bin.ReadInt32()}"));
            }

            subnodes.Add(new BinInterpNode(bin.Position, $"BulkDataFlags: {(EBulkDataFlags)bin.ReadUInt32()}"));
            subnodes.Add(new BinInterpNode(bin.Position, $"Element Count: {count = bin.ReadInt32()}"));

            subnodes.Add(MakeInt32Node(bin, "BulkDataSizeOnDisk"));
            subnodes.Add(MakeUInt32HexNode(bin, "BulkDataOffsetInFile"));
            var rawDataNode = new BinInterpNode(bin.Position, count > 0 ? "RawData (Embedded Non-Streaming Audio)" : "RawData (None, Streaming Audio)") { Length = count, IsExpanded = true };
            subnodes.Add(rawDataNode);

            if (count > 0)
            {
                rawDataNode.Items.AddRange(ReadISACTPair(data, ref binarystart, (int)bin.Position));
            }
            bin.Skip(count);

            if (Pcc.Game.IsLEGame())
            {
                subnodes.Add(new BinInterpNode(bin.Position, $"Unknown int 3: {bin.ReadInt32()}"));
            }
            subnodes.Add(new BinInterpNode(bin.Position, $"BulkDataFlags: {(EBulkDataFlags)bin.ReadUInt32()}"));
            subnodes.Add(new BinInterpNode(bin.Position, $"Element Count: {count = bin.ReadInt32()}"));
            subnodes.Add(MakeInt32Node(bin, "BulkDataSizeOnDisk"));
            subnodes.Add(MakeUInt32HexNode(bin, "BulkDataOffsetInFile"));
            subnodes.Add(new BinInterpNode(bin.Position, "CompressedPCData") { Length = count });
            bin.Skip(count);

            subnodes.Add(new BinInterpNode(bin.Position, $"BulkDataFlags: {(EBulkDataFlags)bin.ReadUInt32()}"));
            subnodes.Add(new BinInterpNode(bin.Position, $"Element Count: {count = bin.ReadInt32()}"));
            subnodes.Add(MakeInt32Node(bin, "BulkDataSizeOnDisk"));
            subnodes.Add(MakeUInt32HexNode(bin, "BulkDataOffsetInFile"));
            subnodes.Add(new BinInterpNode(bin.Position, "CompressedXbox360Data") { Length = count });
            bin.Skip(count);

            subnodes.Add(new BinInterpNode(bin.Position, $"BulkDataFlags: {(EBulkDataFlags)bin.ReadUInt32()}"));
            subnodes.Add(new BinInterpNode(bin.Position, $"Element Count: {count = bin.ReadInt32()}"));
            subnodes.Add(MakeInt32Node(bin, "BulkDataSizeOnDisk"));
            subnodes.Add(MakeUInt32HexNode(bin, "BulkDataOffsetInFile"));
            subnodes.Add(new BinInterpNode(bin.Position, "CompressedPS3Data") { Length = count });
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartBioSoundNodeWaveStreamingDataScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            int offset = binarystart;

            // Size of the entire data to follow
            int numBytesOfStreamingData = BitConverter.ToInt32(data, offset);
            subnodes.Add(new BinInterpNode
            {
                Header = $"0x{offset:X5} Streaming Data Size: {numBytesOfStreamingData}",
                Offset = offset,
                Tag = NodeType.StructLeafInt
            });
            offset += 4;

            subnodes.AddRange(ReadISACTPair(data, ref binarystart, offset));
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }
}