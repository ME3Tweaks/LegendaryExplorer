using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UnrealExtensions;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Sound.ISACT;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

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

    private List<ITreeItem> Scan_WwiseBank(byte[] data)
    {
        var subnodes = new List<ITreeItem>();

        var bin = new EndianReader(new MemoryStream(data)) { Endian = Pcc.Endian };
        bin.JumpTo(CurrentLoadedExport.propsEnd());

        if (Pcc.Game is MEGame.ME2 or MEGame.LE2)
        {
            subnodes.Add(MakeUInt32Node(bin, "Unk1"));
            subnodes.Add(MakeUInt32Node(bin, "Unk2"));
            if (bin.Skip(-8).ReadInt64() == 0)
            {
                return subnodes;
            }
        }
        subnodes.Add(MakeUInt32Node(bin, "BulkDataFlags"));
        subnodes.Add(MakeInt32Node(bin, "DataSize1", out var datasize));
        int dataSize = bin.Skip(-4).ReadInt32();
        subnodes.Add(MakeInt32Node(bin, "DataSize2"));
        subnodes.Add(MakeInt32Node(bin, "DataOffset"));

        //var sb = new InMemorySoundBank(data.Skip((int)bin.Position).ToArray());

        //var bkhd = sb.GetChunk(SoundBankChunkType.BKHD);
        //var datab = sb.GetChunk(SoundBankChunkType.DATA);
        //var didx = sb.GetChunk(SoundBankChunkType.DIDX);
        //var envs = sb.GetChunk(SoundBankChunkType.ENVS);
        //var stid = sb.GetChunk(SoundBankChunkType.STID);
        //var stmg = sb.GetChunk(SoundBankChunkType.STMG);
        //var hirc = sb.GetChunk(SoundBankChunkType.HIRC);

        return Scan_WwiseBankOld(data);
    }

    private List<ITreeItem> Scan_WwiseBankOld(byte[] data)
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
                if (bin.Skip(-8).ReadInt64() == 0)
                {
                    return subnodes;
                }
            }
            subnodes.Add(new BinInterpNode(bin.Position, $"BulkDataFlags: {(EBulkDataFlags)bin.ReadUInt32()}"));
            subnodes.Add(MakeInt32Node(bin, "Element Count", out int dataSize));
            subnodes.Add(MakeInt32Node(bin, "BulkDataSizeOnDisk"));
            subnodes.Add(MakeUInt32HexNode(bin, "BulkDataOffsetInFile"));

            if (dataSize == 0)
            {
                // Nothing more
                return subnodes;
            }

            var chunksNode = new BinInterpNode(bin.Position, "Chunks")
            {
                IsExpanded = true,
            };
            subnodes.Add(chunksNode);
            while (bin.Position < bin.Length)
            {
                var start = bin.Position;
                string chunkID = bin.BaseStream.ReadStringLatin1(4); //This is not endian swapped!
                int size = bin.ReadInt32();
                var chunk = new BinInterpNode(start, $"{chunkID}: {size} bytes")
                {
                    Length = size + 8
                };
                chunksNode.Items.Add(chunk);

                switch (chunkID)
                {
                    case "BKHD":
                        chunk.Items.Add(MakeUInt32Node(bin, "Version"));
                        chunk.Items.Add(new BinInterpNode(bin.Position, $"ID: {bin.ReadUInt32():X}") { Length = 4 });
                        chunk.Items.Add(MakeUInt32Node(bin, "Unk Zero"));
                        chunk.Items.Add(MakeUInt32Node(bin, "Unk Zero"));
                        break;
                    case "STMG":
                        if (Pcc.Game.IsGame2())
                        {
                            chunk.Items.Add(new BinInterpNode(bin.Position, "STMG node not parsed for ME2"));
                            break;
                        }
                        chunk.Items.Add(MakeFloatNode(bin, "Volume Threshold"));
                        if (Pcc.Game == MEGame.ME3)
                        {
                            chunk.Items.Add(MakeUInt16Node(bin, "Max Voice Instances"));
                        }
                        int numStateGroups;
                        var stateGroupsNode = new BinInterpNode(bin.Position, $"State Groups ({numStateGroups = bin.ReadInt32()})");
                        chunk.Items.Add(stateGroupsNode);
                        for (int i = 0; i < numStateGroups; i++)
                        {
                            stateGroupsNode.Items.Add(MakeUInt32HexNode(bin, "ID"));
                            stateGroupsNode.Items.Add(MakeUInt32Node(bin, "Default Transition Time (ms)"));
                            int numTransitionTimes;
                            var transitionTimesNode = new BinInterpNode(bin.Position, $"Custom Transition Times ({numTransitionTimes = bin.ReadInt32()})");
                            stateGroupsNode.Items.Add(transitionTimesNode);
                            for (int j = 0; j < numTransitionTimes; j++)
                            {
                                transitionTimesNode.Items.Add(MakeUInt32HexNode(bin, "'From' state ID"));
                                transitionTimesNode.Items.Add(MakeUInt32HexNode(bin, "'To' state ID"));
                                transitionTimesNode.Items.Add(MakeUInt32Node(bin, "Transition Time (ms)"));
                            }
                        }
                        int numSwitchGroups;
                        var switchGroupsNode = new BinInterpNode(bin.Position, $"Switch Groups ({numSwitchGroups = bin.ReadInt32()})");
                        chunk.Items.Add(switchGroupsNode);
                        for (int i = 0; i < numSwitchGroups; i++)
                        {
                            switchGroupsNode.Items.Add(MakeUInt32HexNode(bin, "ID"));
                            switchGroupsNode.Items.Add(MakeUInt32HexNode(bin, "Game Parameter ID"));
                            int numPoints;
                            var pointNode = new BinInterpNode(bin.Position, $"Points ({numPoints = bin.ReadInt32()})");
                            switchGroupsNode.Items.Add(pointNode);
                            for (int j = 0; j < numPoints; j++)
                            {
                                pointNode.Items.Add(MakeFloatNode(bin, "Game Parameter value"));
                                pointNode.Items.Add(MakeUInt32HexNode(bin, "ID of Switch set when Game Parameter >= given value"));
                                pointNode.Items.Add(MakeUInt32Node(bin, "Curve shape. (9 = constant)"));
                            }
                        }
                        int numGameParams;
                        var gameParamNode = new BinInterpNode(bin.Position, $"Game Parameters ({numGameParams = bin.ReadInt32()})");
                        chunk.Items.Add(gameParamNode);
                        for (int i = 0; i < numGameParams; i++)
                        {
                            gameParamNode.Items.Add(MakeUInt32HexNode(bin, "ID"));
                            gameParamNode.Items.Add(MakeFloatNode(bin, "default value"));
                        }
                        break;
                    case "DIDX":
                        for (int i = 0; i < size / 12; i++)
                        {
                            BinInterpNode item = new BinInterpNode(bin.Position, $"{i}: Embedded File Info")
                            {
                                Length = 12,
                                IsExpanded = true
                            };
                            chunk.Items.Add(item);
                            item.Items.Add(new BinInterpNode(bin.Position, $"ID: {bin.ReadUInt32():X}") { Length = 4 });
                            item.Items.Add(MakeUInt32Node(bin, "Offset"));
                            item.Items.Add(MakeUInt32Node(bin, "Length"));

                            // //todo: remove testing code
                            // bin.Skip(-8);
                            // int off = bin.ReadInt32();
                            // int len = bin.ReadInt32();
                            // item.Items.Add(new BinInterpNode(0xf4 + off, "file start"));
                            // item.Items.Add(new BinInterpNode(0xf4 + off + len, "file end"));
                        }
                        break;
                    case "DATA":
                        chunk.Items.Add(new BinInterpNode(bin.Position, "Start of DATA section. Embedded file offsets are relative to here"));
                        break;
                    case "HIRC":
                        chunk.Items.Add(MakeUInt32Node(bin, "HIRC object count"));
                        uint hircCount = bin.Skip(-4).ReadUInt32();
                        for (int i = 0; i < hircCount; i++)
                        {
                            chunk.Items.Add(ScanHircObject(bin, i));
                        }
                        break;
                    case "STID":
                        chunk.Items.Add(MakeUInt32Node(bin, "Unk One"));
                        chunk.Items.Add(MakeUInt32Node(bin, "WwiseBanks referenced in this bank"));
                        uint soundBankCount = bin.Skip(-4).ReadUInt32();
                        for (int i = 0; i < soundBankCount; i++)
                        {
                            BinInterpNode item = new BinInterpNode(bin.Position, $"{i}: Referenced WwiseBank")
                            {
                                Length = 12,
                                IsExpanded = true
                            };
                            item.Items.Add(new BinInterpNode(bin.Position, $"ID: {bin.ReadUInt32():X}") { Length = 4 });
                            int strLen = bin.ReadByte();
                            item.Items.Add(new BinInterpNode(bin.Position, $"Name: {bin.ReadStringASCII(strLen)}") { Length = strLen });
                            chunk.Items.Add(item);
                        }
                        break;
                    default:
                        chunk.Items.Add(new BinInterpNode(bin.Position, "UNPARSED CHUNK!"));
                        break;
                }

                bin.JumpTo(start + size + 8);
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;

        BinInterpNode ScanHircObject(EndianReader bin, int idx)
        {
            var startPos = bin.Position;
            HIRCType hircType = (HIRCType)(Pcc.Game == MEGame.ME2 ? bin.ReadUInt32() : bin.ReadByte());
            int len = bin.ReadInt32();
            uint id = bin.ReadUInt32();
            var node = new BinInterpNode(startPos, $"{idx}: Type: {AudioStreamHelper.GetHircObjTypeString(hircType)} | Length:{len} | ID:{id:X8}")
            {
                Length = len + 4 + (Pcc.Game == MEGame.ME2 ? 4 : 1)
            };
            bin.JumpTo(startPos);
            node.Items.Add(Pcc.Game == MEGame.ME2 ? MakeUInt32Node(bin, "Type") : MakeByteNode(bin, "Type"));
            node.Items.Add(MakeInt32Node(bin, "Length"));
            node.Items.Add(MakeUInt32HexNode(bin, "ID"));
            var endPos = startPos + node.Length;
            switch (hircType)
            {
                case HIRCType.Event:
                    if (Pcc.Game.IsLEGame())
                    {
                        MakeArrayNodeByteCount(bin, "Event Actions", i => MakeUInt32HexNode(bin, $"{i}"));
                    }
                    else
                    {
                        MakeArrayNode(bin, "Event Actions", i => MakeUInt32HexNode(bin, $"{i}"));
                    }
                    break;
                case HIRCType.EventAction:
                {
                    node.Items.Add(new BinInterpNode(bin.Position, $"Scope: {(WwiseBankParsed.EventActionScope)bin.ReadByte()}", NodeType.StructLeafByte) { Length = 1 });
                    WwiseBankParsed.EventActionType actType;
                    node.Items.Add(new BinInterpNode(bin.Position, $"Action Type: {actType = (WwiseBankParsed.EventActionType)bin.ReadByte()}", NodeType.StructLeafByte) { Length = 1 });
                    if (Pcc.Game.IsOTGame())
                        node.Items.Add(MakeUInt16Node(bin, "Unknown1"));
                    node.Items.Add(MakeUInt32HexNode(bin, "Referenced Object ID"));
                    switch (actType)
                    {
                        case WwiseBankParsed.EventActionType.Play:
                            node.Items.Add(MakeUInt32Node(bin, "Delay (ms)"));
                            node.Items.Add(MakeInt32Node(bin, "Delay Randomization Range lower bound (ms)"));
                            node.Items.Add(MakeInt32Node(bin, "Delay Randomization Range upper bound (ms)"));
                            node.Items.Add(MakeUInt32Node(bin, "Unknown2"));
                            node.Items.Add(MakeUInt32Node(bin, "Fade-in (ms)"));
                            node.Items.Add(MakeInt32Node(bin, "Fade-in Randomization Range lower bound (ms)"));
                            node.Items.Add(MakeInt32Node(bin, "Fade-in Randomization Range upper bound (ms)"));
                            node.Items.Add(MakeByteNode(bin, "Fade-in curve Shape"));
                            break;
                        case WwiseBankParsed.EventActionType.Stop:
                            node.Items.Add(MakeUInt32Node(bin, "Delay (ms)"));
                            node.Items.Add(MakeInt32Node(bin, "Delay Randomization Range lower bound (ms)"));
                            node.Items.Add(MakeInt32Node(bin, "Delay Randomization Range upper bound (ms)"));
                            node.Items.Add(MakeUInt32Node(bin, "Unknown2"));
                            node.Items.Add(MakeUInt32Node(bin, "Fade-out (ms)"));
                            node.Items.Add(MakeInt32Node(bin, "Fade-out Randomization Range lower bound (ms)"));
                            node.Items.Add(MakeInt32Node(bin, "Fade-out Randomization Range upper bound (ms)"));
                            node.Items.Add(MakeByteNode(bin, "Fade-out curve Shape"));
                            break;
                        case WwiseBankParsed.EventActionType.Play_LE:
                            node.Items.Add(MakeByteNode(bin, "Unknown1"));
                            bool playHasFadeIn;
                            node.Items.Add(new BinInterpNode(bin.Position, $"Has Fade In: {playHasFadeIn = (bool)bin.ReadBoolByte()}", NodeType.StructLeafByte) { Length = 1 });
                            if (playHasFadeIn)
                            {
                                node.Items.Add(MakeByteNode(bin, "Unknown byte"));
                                node.Items.Add(MakeUInt32Node(bin, "Fade-in (ms)"));
                            }
                            bool RandomFade;
                            node.Items.Add(new BinInterpNode(bin.Position, $"Unknown 2: {RandomFade = (bool)bin.ReadBoolByte()}", NodeType.StructLeafByte) { Length = 1 });
                            if (RandomFade)
                            {
                                node.Items.Add(MakeByteNode(bin, "Enabled?"));
                                node.Items.Add(MakeUInt32Node(bin, "MinOffset"));
                                node.Items.Add(MakeUInt32Node(bin, "MaxOffset"));
                            }
                            node.Items.Add(new BinInterpNode(bin.Position, $"Fade-out curve Shape: {(WwiseBankParsed.EventActionFadeCurve)bin.ReadByte()}", NodeType.StructLeafByte) { Length = 1 });
                            node.Items.Add(MakeUInt32HexNode(bin, "Bank ID"));
                            break;
                        case WwiseBankParsed.EventActionType.Stop_LE:
                            node.Items.Add(MakeByteNode(bin, "Unknown1"));
                            bool stopHasFadeOut;
                            node.Items.Add(new BinInterpNode(bin.Position, $"Has Fade In: {stopHasFadeOut = (bool)bin.ReadBoolByte()}", NodeType.StructLeafByte) { Length = 1 });
                            if (stopHasFadeOut)
                            {
                                node.Items.Add(MakeByteNode(bin, "Unknown byte"));
                                node.Items.Add(MakeUInt32Node(bin, "Fade-in (ms)"));
                            }
                            node.Items.Add(MakeByteNode(bin, "Unknown2"));
                            node.Items.Add(new BinInterpNode(bin.Position, $"Fade-out curve Shape: {(WwiseBankParsed.EventActionFadeCurve)bin.ReadByte()}", NodeType.StructLeafByte) { Length = 1 });
                            node.Items.Add(MakeByteNode(bin, "Unknown3"));
                            node.Items.Add(MakeByteNode(bin, "Unknown4"));
                            break;
                        case WwiseBankParsed.EventActionType.SetLPF_LE:
                        case WwiseBankParsed.EventActionType.SetVolume_LE:
                        case WwiseBankParsed.EventActionType.ResetLPF_LE:
                        case WwiseBankParsed.EventActionType.ResetVolume_LE:
                            node.Items.Add(MakeByteNode(bin, "Unknown1"));
                            bool HasFade;
                            node.Items.Add(new BinInterpNode(bin.Position, $"Has Fade In: {HasFade = (bool)bin.ReadBoolByte()}", NodeType.StructLeafByte) { Length = 1 });
                            if (HasFade)
                            {
                                node.Items.Add(MakeByteNode(bin, "Unknown byte"));
                                node.Items.Add(MakeUInt32Node(bin, "Fade-in (ms)"));
                            }
                            node.Items.Add(MakeByteNode(bin, "Unknown2"));
                            node.Items.Add(new BinInterpNode(bin.Position, $"Fade-out curve Shape: {(WwiseBankParsed.EventActionFadeCurve)bin.ReadByte()}", NodeType.StructLeafByte) { Length = 1 });
                            node.Items.Add(MakeByteNode(bin, "Unknown3"));
                            node.Items.Add(MakeFloatNode(bin, "Unknown float A"));
                            node.Items.Add(MakeInt32Node(bin, "Unknown int/float B"));
                            node.Items.Add(MakeInt32Node(bin, "Unknown int/float C"));
                            node.Items.Add(MakeByteNode(bin, "Unknown4"));
                            break;
                    }
                    goto default;
                }
                case HIRCType.SoundSXFSoundVoice:
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown1"));
                    //WwiseBank.SoundState soundState;
                    //node.Items.Add(new BinInterpNode(bin.Position, $"State: {soundState = (WwiseBank.SoundState)bin.ReadUInt32()}", NodeType.StructLeafInt) { Length = 4 });
                    //switch (soundState)
                    //{
                    //    case WwiseBank.SoundState.Embed:
                    //        node.Items.Add(MakeUInt32HexNode(bin, "Audio ID"));
                    //        node.Items.Add(MakeUInt32HexNode(bin, "Source ID"));
                    //        node.Items.Add(MakeInt32Node(bin, "Type?"));
                    //        node.Items.Add(MakeInt32Node(bin, "Prefetch length?"));
                    //        break;
                    //    case WwiseBank.SoundState.Streamed:
                    //        node.Items.Add(MakeUInt32HexNode(bin, "Audio ID"));
                    //        node.Items.Add(MakeUInt32HexNode(bin, "Source ID"));
                    //        break;
                    //    case WwiseBank.SoundState.StreamPrefetched:
                    //        node.Items.Add(MakeUInt32HexNode(bin, "Audio ID"));
                    //        node.Items.Add(MakeUInt32HexNode(bin, "Source ID"));
                    //        node.Items.Add(MakeInt32Node(bin, "Type?"));
                    //        node.Items.Add(MakeInt32Node(bin, "Prefetch length?"));
                    //        break;

                    //}

                    //WwiseBank.SoundType soundType;
                    //node.Items.Add(new BinInterpNode(bin.Position, $"SoundType: {soundType = (WwiseBank.SoundType)bin.ReadUInt32()}", NodeType.StructLeafInt) { Length = 4 });
                    //switch (soundType)
                    //{
                    //    case WwiseBank.SoundType.SFX:
                    //        node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //        node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //        node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //        node.Items.Add(MakeUInt32HexNode(bin, "Mixer Out Reference ID"));
                    //        break;
                    //    case WwiseBank.SoundType.Voice:
                    //        node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //        node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //        node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //        node.Items.Add(MakeUInt32HexNode(bin, "Mixer Out Reference ID"));
                    //        break;
                    //    default:
                    //        node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //        node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //        node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //        node.Items.Add(MakeUInt32HexNode(bin, "Mixer Out Reference ID"));
                    //        break;
                    //}
                    //node.Items.Add(MakeByteNode(bin, "Unknown_hex32"));  //Maybe standard mixer package (shared with actor/mixer)
                    //node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //node.Items.Add(MakeByteNode(bin, "PreVolume-UnknownF6"));
                    //node.Items.Add(MakeFloatNode(bin, "Volume (-db)"));
                    //node.Items.Add(MakeInt32Node(bin, "Unknown_A_4bytes"));  //These maybe link or RTPC or randomizer?
                    //node.Items.Add(MakeInt32Node(bin, "Unknown_B_4bytes"));
                    //node.Items.Add(MakeFloatNode(bin, "Low Frequency Effect (LFE)"));
                    //node.Items.Add(MakeInt32Node(bin, "Unknown_C_4bytes"));
                    //node.Items.Add(MakeInt32Node(bin, "Unknown_D_4bytes"));
                    //node.Items.Add(MakeFloatNode(bin, "Pitch"));
                    //node.Items.Add(MakeFloatNode(bin, "Unknown_E_float"));
                    //node.Items.Add(MakeFloatNode(bin, "Unknown_F_float"));
                    //node.Items.Add(MakeFloatNode(bin, "Low Pass Filter"));
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_G_4bytes"));
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_H_4bytes"));
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_I_4bytes"));
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_J_4bytes"));
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_K_4bytes"));
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_L_4bytes")); //In v56 banks?
                    //                                                         //node.Items.Add(MakeByteNode(bin, "Unknown"));  In imported v53 banks?
                    //                                                         //node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //node.Items.Add(MakeInt32Node(bin, "Loop Count (0=infinite)"));
                    //node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //node.Items.Add(MakeByteNode(bin, "Unknown"));

                    goto default;
                case HIRCType.RandomOrSequenceContainer:
                case HIRCType.SwitchContainer:
                case HIRCType.ActorMixer:
                    //node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //int nEffects = 0;
                    //node.Items.Add(new BinInterpNode(bin.Position, $"Count of Effects (?Aux Bus?): {nEffects = bin.ReadByte()}") { Length = 1 });
                    //for (int b = 0; b < nEffects; b++)
                    //{
                    //    node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //    node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //    node.Items.Add(MakeUInt32HexNode(bin, "Effect Reference ID"));
                    //    node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //}
                    //if (nEffects > 0)
                    //{
                    //    node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //}
                    //node.Items.Add(MakeUInt32HexNode(bin, "Master Audio Bus Reference ID"));
                    //node.Items.Add(MakeUInt32HexNode(bin, "Audio Out link"));
                    //node.Items.Add(MakeByteNode(bin, "Unknown_hex32"));  //Is here on a standard mixer? Appears in SoundSFX/voice
                    //node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //node.Items.Add(MakeByteNode(bin, "Unknown"));
                    //node.Items.Add(MakeByteNode(bin, "PreVolume-Unknown_hexF6"));
                    //node.Items.Add(MakeFloatNode(bin, "Volume (-db)"));
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_A_4bytes"));  //These maybe link or RTPC or randomizer?
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_B_4bytes"));
                    //node.Items.Add(MakeFloatNode(bin, "Low Frequency Effect (LFE)"));
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_C_4bytes"));
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_D_4bytes"));
                    //node.Items.Add(MakeFloatNode(bin, "Pitch"));
                    //node.Items.Add(MakeFloatNode(bin, "Unknown_E_float"));
                    //node.Items.Add(MakeFloatNode(bin, "Unknown_F_float"));
                    //node.Items.Add(MakeFloatNode(bin, "Low Pass Filter"));
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_G_4bytes"));
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_H_4bytes")); //Mixer end

                    ////Minimum is 4 x 4bytes but can expanded
                    //bool hasEffects = false; //Maybe something else
                    //node.Items.Add(new BinInterpNode(bin.Position, $"Unknown_Byte->Int + Unk4 + Float: {hasEffects = bin.ReadBoolByte()}") { Length = 1 }); //Count of something? effects?
                    //if (hasEffects)
                    //{
                    //    node.Items.Add(MakeInt32Node(bin, "Unknown_Int"));
                    //    node.Items.Add(MakeUInt32Node(bin, "Unknown_I_4bytes"));
                    //    node.Items.Add(MakeFloatNode(bin, "Unknown Float"));
                    //}
                    //bool hasAttenuation = false;
                    //node.Items.Add(new BinInterpNode(bin.Position, $"Attenuations?: {hasAttenuation = bin.ReadBoolByte()}") { Length = 1 }); //RPTCs? Count? Attenuations?
                    //if (hasAttenuation)
                    //{
                    //    node.Items.Add(MakeInt32Node(bin, "Unknown_J_Int")); //<= extra if byte?
                    //    node.Items.Add(MakeUInt32HexNode(bin, "Attenuation? Reference")); //<= extra if byte?
                    //    node.Items.Add(MakeUInt32Node(bin, "Unknown_L_4bytes"));
                    //    node.Items.Add(MakeUInt32Node(bin, "Unknown_M_4bytes"));
                    //    node.Items.Add(MakeByteNode(bin, "UnknownByte"));
                    //}
                    //else
                    //{
                    //    node.Items.Add(MakeInt32Node(bin, "Unknown_J_Int"));
                    //    node.Items.Add(MakeUInt32Node(bin, "Unknown_L_4bytes"));
                    //}

                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_N_4bytes"));
                    //node.Items.Add(MakeUInt32Node(bin, "Unknown_O_4bytes"));
                    //goto default;
                    //node.Items.Add(MakeArrayNode(bin, "Input References", i => MakeUInt32HexNode(bin, $"{i}")));
                    goto default;
                case HIRCType.AudioBus:
                case HIRCType.BlendContainer:
                case HIRCType.MusicSegment:
                case HIRCType.MusicTrack:
                case HIRCType.MusicSwitchContainer:
                case HIRCType.MusicPlaylistContainer:
                case HIRCType.Attenuation:
                case HIRCType.DialogueEvent:
                case HIRCType.MotionBus:
                case HIRCType.MotionFX:
                case HIRCType.Effect:
                case HIRCType.AuxiliaryBus:
                //node.Items.Add(MakeByteNode(bin, "Count of something?"));
                //node.Items.Add(MakeByteNode(bin, "Unknown"));
                //node.Items.Add(MakeByteNode(bin, "Unknown"));
                //node.Items.Add(MakeByteNode(bin, "Unknown"));
                //node.Items.Add(MakeUInt32Node(bin, "Unknown_Int"));
                //node.Items.Add(MakeFloatNode(bin, "Unknown Float"));
                //break;
                case HIRCType.Settings:
                default:
                    if (bin.Position < endPos)
                    {
                        node.Items.Add(new BinInterpNode(bin.Position, "Unknown bytes")
                        {
                            Length = (int)(endPos - bin.Position)
                        });
                    }
                    break;
            }

            bin.JumpTo(endPos);
            return node;
        }
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