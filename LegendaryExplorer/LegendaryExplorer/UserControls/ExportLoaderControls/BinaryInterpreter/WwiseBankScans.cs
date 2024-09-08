using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UnrealExtensions;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using ME3Tweaks.Wwiser.Model;
using ME3Tweaks.Wwiser.Model.Hierarchy.Enums;
using System;
using System.Collections.Generic;
using System.IO;

namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public partial class BinaryInterpreterWPF
{
    private record WwiseItem(uint Id, string Name, long Position);

    private Dictionary<uint, WwiseItem> WwiseIdMap = new();

    private BinInterpNode MakeWwiseIdNode(EndianReader bin, string name)
    {
        var id = bin.ReadUInt32();
        bin.Skip(-4);
        var item = new WwiseItem(id, name, bin.Position);
        WwiseIdMap.Add(id, item);
        var node = MakeUInt32Node(bin, name);
        return node;
    }

    private BinInterpNode MakeWwiseIdRefNode(EndianReader bin, string name)
    {
        var pos = bin.Position;
        var id = bin.ReadUInt32();
        var node = new BinInterpNode(pos, $"{name}: {id}") { Length = 4 };
        if(WwiseIdMap.TryGetValue(id, out var item))
        {
            node.Header += $" (Ref to {item.Name})";
        }
        return node;
    }

    private BinInterpNode MakeUInt32EnumNode<T>(EndianReader bin, string name) where T : Enum
    {
        var value = bin.ReadUInt32();
        var parsedValue = Enum.GetName(typeof(T), value);
        if (string.IsNullOrEmpty(parsedValue)) parsedValue = "None";
        return new BinInterpNode(bin.Position - 4, $"{name}: {parsedValue}") { Length = 4 };
    }

    private List<ITreeItem> Scan_WwiseBank(byte[] data)
    {
        var subnodes = new List<ITreeItem>();
        WwiseIdMap.Clear();
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

        uint version = 0;
        bool useFeedback = false;

        while (bin.Position < bin.Length)
        {
            var start = bin.Position;
            string chunkID = bin.BaseStream.ReadStringLatin1(4); //This is not endian swapped!
            int size = bin.ReadInt32();
            var chunkNode = new BinInterpNode(start, $"{chunkID}: {size} bytes")
            {
                Length = size + 8
            };
            subnodes.Add(chunkNode);
            switch(chunkID)
            {
                case "BKHD":
                    (version, useFeedback) = Scan_WwiseBank_BKHD(chunkNode, bin, size);
                    break;
                case "DIDX":
                    Scan_WwiseBank_DIDX(chunkNode, bin, size);
                    break;
                case "HIRC":
                    Scan_WwiseBank_HIRC(chunkNode, bin, version);
                    break;
                case "STID":
                    Scan_WwiseBank_STID(chunkNode, bin);
                    break;
                case "PLAT":
                    Scan_WwiseBank_PLAT(chunkNode, bin);
                    break;
                case "INIT":
                    Scan_WwiseBank_INIT(chunkNode, bin);
                    break;
            }

            // Just in case we don't parse chunk in full - jump to next chunk
            bin.JumpTo(start + size + 8);
        }
        return subnodes;
    }

    private (uint, bool) Scan_WwiseBank_BKHD(BinInterpNode root, EndianReader bin, int size)
    {
        var version = bin.ReadUInt32();
        bool useFeedback = false;
        bin.Skip(-4);

        root.Items.Add(MakeUInt32Node(bin, "WwiseVersion"));
        root.Items.Add(MakeWwiseIdNode(bin, "SoundBankId"));

        if(version <= 122)
        {
            root.Items.Add(MakeUInt32EnumNode<LanguageId>(bin, "LanguageID"));
        }
        else
        {
            root.Items.Add(MakeUInt32Node(bin, "LanguageIDStringHash"));
        }

        if(version > 27 && version < 126)
        {
            useFeedback = bin.ReadBoolByte();
            bin.Skip(-1);
            root.Items.Add(MakeBoolByteNode(bin, "UseFeedback"));
        }

        if(version > 126)
        {
            root.Items.Add(MakeUInt32EnumNode<AltValues>(bin, "AltValues"));
        }

        if(version > 76)
        {
            root.Items.Add(MakeUInt32Node(bin, "ProjectID"));
        }

        if (version > 141)
        {
            root.Items.Add(MakeUInt32Node(bin, "SoundBankType"));
            root.Items.Add(new BinInterpNode(bin.Position, "BankHash"){ Length = 16 });
            bin.Skip(16);
        }

        var paddingSize = BankHeaderPadding.GetPaddingSize(version, (uint)size);
        if(paddingSize > 0)
        {
            root.Items.Add(new BinInterpNode(bin.Position, "Padding") { Length = (int)paddingSize });
        }
        return (version, useFeedback);
    }

    private void Scan_WwiseBank_DIDX(BinInterpNode root, EndianReader bin, int size)
    {
        var count = size / 12;
        root.Header += $", {count} items";
        for (int i = 0; i < count; i++)
        {

            var itemNode = MakeWwiseIdNode(bin, $"{i}");
            itemNode.Length = 12;
            bin.Skip(-4);
            itemNode.Items.Add(MakeUInt32Node(bin, "Id"));
            itemNode.Items.Add(MakeUInt32Node(bin, "Offset"));
            itemNode.Items.Add(MakeUInt32Node(bin, "Size"));
            root.Items.Add(itemNode);
        }
    }

    private void Scan_WwiseBank_HIRC(BinInterpNode root, EndianReader bin, uint version)
    {
        root.Items.Add(MakeArrayNode(bin, "Items", i => MakeHIRCNode(i, bin, version), IsExpanded: true));
    }

    private BinInterpNode MakeHIRCNode(int index, EndianReader bin, uint version)
    {
        var start = bin.Position;
        var root = new BinInterpNode(bin.Position, $"{index}: ");

        var type = HircSmartType.DeserializeStatic(bin.BaseStream, version);

        if(version <= 48)
        {
            root.Items.Add(new BinInterpNode(bin.Position - 4, $"Type: {type}") { Length = 4 });
        }
        else
        {
            root.Items.Add(new BinInterpNode(bin.Position - 1, $"Type: {type}") { Length = 1 });
        }

        var fullSize = bin.ReadInt32() + (version <= 48 ? 8 : 5);

        bin.Skip(-4);
        root.Items.Add(MakeUInt32Node(bin, "Size"));

        root.Items.Add(MakeWwiseIdNode(bin, "ID"));

        root.Header += $"{type}";
        root.Length = fullSize;

        // Just in case we don't parse item in full - jump to next item
        bin.JumpTo(start + fullSize);
        return root;
    }

    private void Scan_WwiseBank_STID(BinInterpNode root, EndianReader bin)
    {
        root.Items.Add(MakeUInt32EnumNode<AKBKStringType>(bin, "StringType"));
        root.Items.Add(MakeArrayNode(bin, "BankHashHeaders", i =>
        {
            var bhhRoot = new BinInterpNode(bin.Position, $"{i}");
            bhhRoot.Items.Add(MakeWwiseIdRefNode(bin, "Id"));
            var stringPos = bin.Position;
            var stringLength = bin.ReadByte();
            var stringVal = bin.ReadStringASCII(stringLength);
            var fileName = new BinInterpNode(stringPos, $"FileName: {stringVal}") { Length = stringLength + 1 };
            bhhRoot.Header += $": {stringVal}";
            bhhRoot.Items.Add(fileName);
            bhhRoot.Length = 4 + 1 + stringLength;
            return bhhRoot;
        }));
    }

    private void Scan_WwiseBank_PLAT(BinInterpNode root, EndianReader bin)
    {
        root.Items.Add(MakeStringUTF8Node(bin, "CustomPlatformName"));
    }

    private void Scan_WwiseBank_INIT(BinInterpNode root, EndianReader bin)
    {
        root.Items.Add(MakeArrayNode(bin, "Plugins", i =>
        {
            var plugin = new BinInterpNode(bin.Position, $"{i}");
            // TODO: This is a pretty complex enum. 2 enums && together
            plugin.Items.Add(MakeUInt32Node(bin, "PluginID"));
            plugin.Items.Add(MakeStringUTF8Node(bin, "DLLName"));
            return plugin;
        }));
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

}
