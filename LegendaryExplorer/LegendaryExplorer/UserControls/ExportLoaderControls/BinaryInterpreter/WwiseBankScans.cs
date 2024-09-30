using DocumentFormat.OpenXml.Math;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UnrealExtensions;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using ME3Tweaks.Wwiser.Formats;
using ME3Tweaks.Wwiser.Model;
using ME3Tweaks.Wwiser.Model.Hierarchy;
using ME3Tweaks.Wwiser.Model.Hierarchy.Enums;
using ME3Tweaks.Wwiser.Model.ParameterNode;
using ME3Tweaks.Wwiser.Model.ParameterNode.Positioning;
using ME3Tweaks.Wwiser.Model.RTPC;
using System;
using System.Collections.Generic;
using System.IO;
using static ME3Tweaks.Wwiser.Model.Hierarchy.Enums.AccumType;
using static ME3Tweaks.Wwiser.Model.Hierarchy.Enums.CurveScaling;
using static ME3Tweaks.Wwiser.Model.Hierarchy.Enums.GroupType;
using static ME3Tweaks.Wwiser.Model.Hierarchy.Enums.ParameterId;
using static ME3Tweaks.Wwiser.Model.Hierarchy.Enums.PriorityOverrideFlags;
using static ME3Tweaks.Wwiser.Model.Hierarchy.MediaInformation;
using static ME3Tweaks.Wwiser.Model.Hierarchy.RanSeqFlags;
using static ME3Tweaks.Wwiser.Model.ParameterNode.AdvSettingsParams;
using static ME3Tweaks.Wwiser.Model.ParameterNode.AuxParams;
using static ME3Tweaks.Wwiser.Model.ParameterNode.Positioning.PathMode;
using static ME3Tweaks.Wwiser.Model.ParameterNode.Positioning.PositioningChunk;
using static ME3Tweaks.Wwiser.Model.RTPC.RtpcType;
using static ME3Tweaks.Wwiser.Model.State.SyncType;

namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public partial class BinaryInterpreterWPF
{
    private record WwiseItem(uint Id, string Name, long Position);

    private Dictionary<uint, WwiseItem> WwiseIdMap = new();

    private BinInterpNode MakeWwiseIdNode(EndianReader bin, string refName, string nodeName = "ID")
    {
        var id = bin.ReadUInt32();
        bin.Skip(-4);
        var item = new WwiseItem(id, refName, bin.Position);
        WwiseIdMap.Add(id, item);
        var node = MakeUInt32Node(bin, nodeName);
        return node;
    }

    private BinInterpNode MakeWwiseIdRefNode(EndianReader bin, string name)
    {
        var pos = bin.Position;
        var id = bin.ReadUInt32();
        var node = new BinInterpNode(pos, $"{name}: {id}") { Length = 4 };
        if (WwiseIdMap.TryGetValue(id, out var item))
        {
            node.Header += $" (Ref to {item.Name})";
        }
        else
        {
            node.Header += $" (Ref)";
        }
        return node;
    }

    private BinInterpNode MakeWwiseUniNode(EndianReader bin, string name)
    {
        var node = new BinInterpNode(bin.Position, $"{name}: ") { Length = 4 };
        Span<byte> span = stackalloc byte[4];
        var read = bin.BaseStream.Read(span);
        uint value = BitConverter.ToUInt32(span);
        if (value > 0x10000000)
        {
            // float
            var f = BitConverter.ToSingle(span);
            node.Header += f.ToString();
        }
        else
        {
            // int
            node.Header += value.ToString();
        }
        return node;
    }

    private BinInterpNode MakeWwiseVarCountNode(EndianReader bin, string name)
    {
        var node = new BinInterpNode(bin.Position, $"{name}: ");
        var pos = bin.Position;

        var value = VarCount.ReadResizingUint(bin.BaseStream);
        node.Length = (int)(bin.Position - pos);
        node.Header += value.ToString();

        return node;
    }

    private BinInterpNode MakeUInt32EnumNode<T>(EndianReader bin, string name) where T : Enum
    {
        var value = bin.ReadUInt32();
        var parsedValue = Enum.GetName(typeof(T), value);
        if (string.IsNullOrEmpty(parsedValue)) parsedValue = "None";
        return new BinInterpNode(bin.Position - 4, $"{name}: {parsedValue}") { Length = 4 };
    }

    private BinInterpNode MakeByteEnumNode<T>(EndianReader bin, string name) where T : Enum
    {
        var value = bin.ReadByte();
        var parsedValue = Enum.GetName(typeof(T), value);
        if (string.IsNullOrEmpty(parsedValue)) parsedValue = "None";
        return new BinInterpNode(bin.Position - 1, $"{name}: {parsedValue}") { Length = 1 };
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
                    Scan_WwiseBank_HIRC(chunkNode, bin, version, useFeedback);
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
        root.Items.Add(MakeWwiseIdNode(bin, "SoundBank", "SoundBankId"));

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

            var itemNode = MakeWwiseIdNode(bin, $"Media Idx {i}", i.ToString());
            itemNode.Length = 12;
            bin.Skip(-4);
            itemNode.Items.Add(MakeUInt32Node(bin, "Id"));
            itemNode.Items.Add(MakeUInt32Node(bin, "Offset"));
            itemNode.Items.Add(MakeUInt32Node(bin, "Size"));
            root.Items.Add(itemNode);
        }
    }

    private void Scan_WwiseBank_HIRC(BinInterpNode root, EndianReader bin, uint version, bool useFeedback)
    {
        root.Items.Add(MakeArrayNode(bin, "Items", i => MakeHIRCNode(i, bin, version, useFeedback), IsExpanded: true));
    }

    private BinInterpNode MakeHIRCNode(int index, EndianReader bin, uint version, bool useFeedback)
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

        root.Items.Add(MakeWwiseIdNode(bin, type.ToString()));

        root.Header += $"{type}";
        root.Length = fullSize;

        switch(type)
        {
            case HircType.Sound:
                Scan_HIRC_BankSourceData(root, bin, version);
                Scan_HIRC_NodeBaseParams(root, bin, version, useFeedback);
                if(version <= 56)
                {
                    root.Items.Add(MakeInt16Node(bin, "Loop"));
                    root.Items.Add(MakeInt16Node(bin, "LoopModMin"));
                    root.Items.Add(MakeInt16Node(bin, "LoopModMax"));
                }
                break;
            case HircType.Event:
                root.Items.Add(MakeArrayNode(bin, "Actions", i => MakeWwiseIdRefNode(bin, i.ToString()), IsExpanded:true));
                break;
            case HircType.RandomSequenceContainer:
                Scan_HIRC_NodeBaseParams(root, bin, version, useFeedback);
                root.Items.Add(MakeUInt16Node(bin, "LoopCount"));
                if(version > 72)
                {
                    root.Items.Add(MakeUInt16Node(bin, "LoopModMin"));
                    root.Items.Add(MakeUInt16Node(bin, "LoopModMax"));
                }
                root.Items.Add(MakeFloatNode(bin, "TransitionTime"));
                root.Items.Add(MakeFloatNode(bin, "TransitionTimeModMin"));
                root.Items.Add(MakeFloatNode(bin, "TransitionTimeModMax"));
                root.Items.Add(MakeUInt16Node(bin, "AvoidRepeatCount"));
                root.Items.Add(MakeByteEnumNode<TransitionMode>(bin, "TransitionMode"));
                root.Items.Add(MakeByteEnumNode<RandomMode>(bin, "RandomMode"));
                root.Items.Add(MakeByteEnumNode<ContainerMode>(bin, "Mode"));
                if(version <= 89)
                {
                    root.Items.Add(MakeBoolByteNode(bin, "IsUsingWeight"));
                    root.Items.Add(MakeBoolByteNode(bin, "ResetPlaylistAtEachPlay"));
                    root.Items.Add(MakeBoolByteNode(bin, "IsRestartBackwards"));
                    root.Items.Add(MakeBoolByteNode(bin, "IsContinuous"));
                    root.Items.Add(MakeBoolByteNode(bin, "IsGlobal"));
                }
                else root.Items.Add(MakeByteEnumNode<RanSeqInner>(bin, "RandSeqFlags"));
                root.Items.Add(MakeArrayNode(bin, "Children", i => MakeWwiseIdRefNode(bin, $"Child {i}")));
                root.Items.Add(MakeArrayNodeInt16Count(bin, "Playlist", i =>
                {
                    var n = new BinInterpNode(bin.Position, $"Item {i}");
                    n.Items.Add(MakeWwiseIdNode(bin, "PlaylistItemId"));
                    if (version <= 56) n.Items.Add(MakeByteNode(bin, "Weight"));
                    else n.Items.Add(MakeInt32Node(bin, "Weight"));
                    return n;
                }));
                break;
            case HircType.SwitchContainer:
                Scan_HIRC_NodeBaseParams(root, bin, version, useFeedback);
                if (version <= 89) root.Items.Add(MakeUInt32EnumNode<GroupTypeInner>(bin, "GroupType"));
                else root.Items.Add(MakeByteEnumNode<GroupTypeInner>(bin, "GroupType"));
                root.Items.Add(MakeWwiseIdRefNode(bin, "GroupId"));
                root.Items.Add(MakeWwiseIdRefNode(bin, "DefaultSwitchId"));
                root.Items.Add(MakeBoolByteNode(bin, "IsContinuousValidation"));
                root.Items.Add(MakeArrayNode(bin, "Children", i => MakeWwiseIdRefNode(bin, $"Child {i}")));
                root.Items.Add(MakeArrayNode(bin, "SwitchGroups", i =>
                {
                    var g = new BinInterpNode(bin.Position, $"Group {i}");
                    g.Items.Add(MakeWwiseIdNode(bin, "GroupId"));
                    g.Items.Add(MakeArrayNode(bin, "ItemIDs", i => MakeWwiseIdRefNode(bin, $"Item {i}")));
                    return g;
                }));
                root.Items.Add(MakeArrayNode(bin, "SwitchParams", i =>
                {
                    var p = new BinInterpNode(bin.Position, $"Params {i}");
                    p.Items.Add(MakeWwiseIdNode(bin, "ParamId"));
                    if(version <= 89)
                    {
                        p.Items.Add(MakeBoolByteNode(bin, "IsFirstOnly"));
                        p.Items.Add(MakeBoolByteNode(bin, "ContinuePlayback"));
                        p.Items.Add(MakeUInt32EnumNode<OnSwitchMode>(bin, "OnSwitchMode"));
                    }
                    else
                    {
                        var bitVector = bin.ReadByte();
                        var bvValue = ((bitVector & (1 << 0)) == 1 << 0) ? "IsFirstOnly" : "";
                        if (!string.IsNullOrEmpty(bvValue)) bvValue += " ";
                        if ((bitVector & (1 << 1)) == 1 << 1) bvValue += "ContinuePlayback";
                        if (string.IsNullOrEmpty(bvValue)) bvValue = "None";
                        p.Items.Add(new BinInterpNode(bin.Position - 1, $"Flags (BitVector): {bvValue}"));

                        p.Items.Add(MakeByteEnumNode<OnSwitchMode>(bin, "OnSwitchMode"));
                    }

                    p.Items.Add(MakeFloatNode(bin, "FadeInTime"));
                    p.Items.Add(MakeFloatNode(bin, "FadeOutTime"));
                    return p;
                }));
                break;
            case HircType.ActorMixer:
                Scan_HIRC_NodeBaseParams(root, bin, version, useFeedback);
                root.Items.Add(MakeArrayNode(bin, "Children", i => MakeWwiseIdRefNode(bin, $"Child {i}"), true));
                break;
            case HircType.LayerContainer:
                Scan_HIRC_NodeBaseParams(root, bin, version, useFeedback);
                root.Items.Add(MakeArrayNode(bin, "Children", i => MakeWwiseIdRefNode(bin, $"Child {i}")));
                root.Items.Add(MakeArrayNode(bin, "Layers", i =>
                {
                    var l = new BinInterpNode(bin.Position, $"Layer {i}");
                    l.Items.Add(MakeWwiseIdNode(bin, "LayerID"));
                    Scan_HIRC_RTPCParameterNodeBase(l, bin, version);
                    l.Items.Add(MakeWwiseIdRefNode(bin, "RtpcID"));
                    if (version > 89)
                    {
                        var rtpcType = bin.ReadByte();
                        if (version <= 140 && rtpcType == 0x02)
                        {
                            rtpcType = 0x04;
                        }
                        l.Items.Add(new BinInterpNode(bin.Position - 1, $"RtpcType: {Enum.GetName((RtpcTypeInner)rtpcType)}") { Length = 1 });
                    }
                    if (version <= 56) l.Items.Add(MakeFloatNode(bin, "CrossfadingRtpcDefaultValue"));
                    root.Items.Add(MakeArrayNode(bin, "AssociatedChildren", j => MakeArrayNode(bin, $"Child {j} Curves", k =>
                    {
                        var gItem = new BinInterpNode(bin.Position, $"Graph Item {k}");
                        gItem.Items.Add(MakeFloatNode(bin, "From"));
                        gItem.Items.Add(MakeFloatNode(bin, "To"));
                        gItem.Items.Add(MakeUInt32EnumNode<CurveInterpolation>(bin, "CurveInterpolation"));
                        return gItem;
                    })));
                    return l;
                }));
                break;
            case HircType.Attenuation:
                if (version > 136) root.Items.Add(MakeBoolByteNode(bin, "IsHeightSpreadEnabled"));
                var isConeEnabled = bin.ReadBoolByte();
                bin.Skip(-1);
                root.Items.Add(MakeBoolByteNode(bin, "IsConeEnabled"));
                if (isConeEnabled)
                {
                    root.Items.Add(MakeFloatNode(bin, "InsideDegrees"));
                    root.Items.Add(MakeFloatNode(bin, "OutsideDegrees"));
                    root.Items.Add(MakeFloatNode(bin, "OutsideVolume"));
                    root.Items.Add(MakeFloatNode(bin, "LowPass"));
                    if(version > 89) root.Items.Add(MakeFloatNode(bin, "HighPass"));
                }
                var count = CurveToUse.GetCurveCount(version);
                var curvesToUse = new BinInterpNode(bin.Position, "CurvesToUse") { Length = count };
                for(var i = 0; i < count; i++)
                {
                    curvesToUse.Items.Add(MakeSByteNode(bin, $"{i}"));
                }
                root.Items.Add(curvesToUse);
                root.Items.Add(MakeArrayNodeByteCount(bin, "ConversionTable", i =>
                {
                    var c = new BinInterpNode(bin.Position, $"Item {i}");
                    c.Items.Add(MakeByteEnumNode<CurveScalingInner>(bin, "CurveScaling"));
                    c.Items.Add(MakeArrayNodeInt16Count(bin, $"Graph", k =>
                    {
                        var gItem = new BinInterpNode(bin.Position, $"Graph Item {k}");
                        gItem.Items.Add(MakeFloatNode(bin, "From"));
                        gItem.Items.Add(MakeFloatNode(bin, "To"));
                        gItem.Items.Add(MakeUInt32EnumNode<CurveInterpolation>(bin, "CurveInterpolation"));
                        return gItem;
                    }, true));
                    return c;
                }, true));
                Scan_HIRC_RTPCParameterNodeBase(root, bin, version);
                break;
        }

        // Just in case we don't parse item in full - jump to next item
        bin.JumpTo(start + fullSize);
        return root;
    }

    private void Scan_HIRC_BankSourceData(BinInterpNode root, EndianReader bin, uint version)
    {
        var pluginExists = bin.ReadUInt32();
        bin.Skip(-4);
        root.Items.Add(MakeUInt32Node(bin, "PluginID"));

        var streamTypeNode = new BinInterpNode(bin.Position, "");
        var streamType = StreamType.DeserializeStatic(bin.BaseStream, version);
        streamTypeNode.Header += $"StreamType: {streamType}";
        streamTypeNode.Length = version <= 89 ? 4 : 1;
        root.Items.Add(streamTypeNode);

        if(version <= 46)
        {
            root.Items.Add(MakeUInt32Node(bin, "SampleRate"));
            root.Items.Add(MakeUInt32Node(bin, "FormatBits"));
        }

        root.Items.Add(MakeUInt32Node(bin, "SourceID"));
        if(version <= 86)
        {
            root.Items.Add(MakeUInt32Node(bin, "FileID"));
            if(streamType != StreamType.StreamTypeInner.Streaming)
            {
                root.Items.Add(MakeUInt32Node(bin, "FileOffset"));
                root.Items.Add(MakeUInt32Node(bin, "InMemoryMediaSize"));
            }
        }
        else
        {
            root.Items.Add(MakeUInt32Node(bin, "InMemoryMediaSize"));
        }

        var flags = (MediaInformationFlags)bin.ReadByte();
        if (version <= 112 && flags.HasFlag(MediaInformationFlags.Prefetch))
        {
            // On <= 122, HasSource is bit 1. To deserialize, replace prefetch with HasSource
            flags &= MediaInformationFlags.HasSource;
            flags &= ~MediaInformationFlags.Prefetch;
        }
        root.Items.Add(new BinInterpNode(bin.Position - 1, $"MediaInformationFlags: {flags}") { Length = 1 });
        
    }

    private void Scan_HIRC_NodeBaseParams(BinInterpNode root, EndianReader bin, uint version, bool useFeedback)
    {
        root.Items.Add(MakeBoolByteNode(bin, "IsOverrideParentFX"));
        var fxCount = bin.ReadByte();
        bin.Skip(-1);
        root.Items.Add(MakeByteNode(bin, "FXCount"));
        if (fxCount > 0)
        {
            root.Items.Add(MakeByteNode(bin, "BitsFXBypass"));
            var fxList = new BinInterpNode(bin.Position, "Effects");
            root.Items.Add(fxList);
            for (var i = 0; i < fxCount; i++)
            {
                var fx = MakeByteNode(bin, $"({i})");
                fxList.Items.Add(fx);
                fx.Items.Add(MakeWwiseIdNode(bin, "FX"));
                if (version is > 49 and < 145)
                {
                    fx.Items.Add(MakeBoolByteNode(bin, "IsShareSet"));
                }
                fx.Items.Add(MakeBoolByteNode(bin, "IsRendered"));

                if (version <= 48)
                {
                    var pLength = bin.ReadUInt32();
                    bin.Skip(-4);
                    fx.Items.Add(new BinInterpNode(bin.Position - 4, "FXParameters") { Length = (int)(pLength + 4) });
                    bin.Skip(pLength);
                }
            }
        }


        if (version is > 90 and < 145 )
        {
            root.Items.Add(MakeBoolByteNode(bin, "OverrideAttachmentParams"));
        }
        root.Items.Add(MakeWwiseIdRefNode(bin, "OverrideBusId"));
        root.Items.Add(MakeWwiseIdRefNode(bin, "DirectParentId"));

        if(version <= 56)
        {
            root.Items.Add(MakeByteNode(bin, "Priority"));
        }

        var pfPos = bin.Position;
        PriorityFlagsInner priorityFlags = 0;
        if (version <= 89)
        {
            var overrideParent = bin.ReadByte();
            if (overrideParent is 1) priorityFlags |= PriorityFlagsInner.PriorityOverrideParent;

            var applyDistFactor = bin.ReadByte();
            if (applyDistFactor is 1) priorityFlags |= PriorityFlagsInner.PriorityApplyDistFactor;
        }
        else
        {
            priorityFlags = (PriorityFlagsInner)bin.ReadByte();
        }
        root.Items.Add(new BinInterpNode(pfPos, $"PriorityFlags: {priorityFlags}") { Length = (version <= 89 ? 2 : 1) });

        if (version <= 56) root.Items.Add(MakeSByteNode(bin, "DistOffset"));

        Scan_HIRC_InitialParams(root, bin, version);
        if(version <= 52) root.Items.Add(MakeWwiseIdRefNode(bin, "StateGroupId"));
        Scan_HIRC_Positioning(root, bin, version);
        if (version > 65) Scan_HIRC_AuxParams(root, bin, version);
        Scan_HIRC_AdvSettingsParams(root, bin, version);
        Scan_HIRC_State(root, bin, version);
        Scan_HIRC_RTPCParameterNodeBase(root, bin, version);
        if(version < 126 && useFeedback) Scan_HIRC_FeedbackInfo(root, bin, version);
    }

    private void Scan_HIRC_InitialParams(BinInterpNode root, EndianReader bin, uint version)
    {
        var ipNode = new BinInterpNode(bin.Position, "InitialParameters");
        root.Items.Add(ipNode);
        if(version <= 56)
        {
            ipNode.Items.Add(MakeFloatNode(bin, "Volume"));
            ipNode.Items.Add(MakeFloatNode(bin, "VolumeMin"));
            ipNode.Items.Add(MakeFloatNode(bin, "VolumeMax"));
            ipNode.Items.Add(MakeFloatNode(bin, "LFE"));
            ipNode.Items.Add(MakeFloatNode(bin, "LFEMin"));
            ipNode.Items.Add(MakeFloatNode(bin, "LFEMax"));
            ipNode.Items.Add(MakeFloatNode(bin, "Pitch"));
            ipNode.Items.Add(MakeFloatNode(bin, "PitchMin"));
            ipNode.Items.Add(MakeFloatNode(bin, "PitchMax"));
            ipNode.Items.Add(MakeFloatNode(bin, "LPF"));
            ipNode.Items.Add(MakeFloatNode(bin, "LPFMin"));
            ipNode.Items.Add(MakeFloatNode(bin, "LPFMax"));
        }
        else
        {
            ipNode.IsExpanded = true;
            var paramLength = bin.ReadByte();
            bin.Skip(-1);
            ipNode.Items.Add(MakeByteNode(bin, "ParamsLength"));

            if(paramLength > 0)
            {
                var parameters = new BinInterpNode(bin.Position, "ParameterIds");
                ipNode.Items.Add(parameters);
                var paramIds = new List<PropId>();
                for (int i = 0; i < paramLength; i++)
                {
                    var (propId, _) = SmartPropId.DeserializeStatic(bin.BaseStream, false, version);
                    paramIds.Add(propId);
                    parameters.Items.Add(new BinInterpNode(bin.Position - 1, $"({i}) {propId}") { Length = 1 });
                }
                var paramVals = new BinInterpNode(bin.Position, "ParameterValues");
                ipNode.Items.Add(paramVals);
                for (int i = 0; i < paramLength; i++)
                {
                    if (paramIds[i] is PropId.AttachedPluginFXID or PropId.AttenuationID)
                    {
                        paramVals.Items.Add(MakeWwiseIdRefNode(bin, $"({i})"));
                    }
                    else
                    {
                        paramVals.Items.Add(MakeWwiseUniNode(bin, $"({i})"));
                    }
                }
            }

            var rangeLength = bin.ReadByte();
            bin.Skip(-1);
            ipNode.Items.Add(MakeByteNode(bin, "RangesLength"));

            if(rangeLength > 0)
            {
                var ranges = new BinInterpNode(bin.Position, "RangeIds");
                ipNode.Items.Add(ranges);
                for (int i = 0; i < rangeLength; i++)
                {
                    var (propId, _) = SmartPropId.DeserializeStatic(bin.BaseStream, false, version);
                    ranges.Items.Add(new BinInterpNode(bin.Position - 1, $"({i}) {propId}") { Length = 1 });
                }

                var rangeVals = new BinInterpNode(bin.Position, "RangeValues");
                ipNode.Items.Add(rangeVals);

                for (int i = 0; i < rangeLength; i++)
                {
                    rangeVals.Items.Add(MakeWwiseUniNode(bin, $"{i} Low"));
                    rangeVals.Items.Add(MakeWwiseUniNode(bin, $"{i} High"));
                }
            }
        }
    }

    private void Scan_HIRC_Positioning(BinInterpNode root, EndianReader bin, uint version)
    {
        var pNode = new BinInterpNode(bin.Position, "Positioning");

        var initial = bin.ReadByte();
        bin.Skip(-1);

        var bits = (PositioningFlags)initial;
        if (version is > 112 and <= 122 && bits.HasFlag(PositioningFlags.Unknown2D2))
        {
            bits |= PositioningFlags.Is3DPositioningAvailable;
            bits &= ~PositioningFlags.Unknown2D2;
        }
        var panningType = (SpeakerPanningType)(initial >> 2);
        var positionType = (PositionType3D)(initial >> 5);


        var initialNode = MakeByteEnumNode<PositioningFlags>(bin, "PositioningFlags");
        initialNode.Items.Add(new BinInterpNode(bin.Position - 1, $"SpeakerPanningType (Bit field): {panningType}"));
        initialNode.Items.Add(new BinInterpNode(bin.Position - 1, $"PositionType3D (Bit field): {panningType}"));
        pNode.Items.Add(initialNode);

        // booleans derived from a bunch of this chunk. determines it's own serialization based on these bool values!
        var hasPositioning = bits.HasFlag(PositioningFlags.PositioningInfoOverrideParent);
        // Type == 1 OR Type == 2 and Type != 1
        var hasAutomation = version > 129 && 
                                (positionType.HasFlag(PositionType3D.EmitterWithAutomation) ||
                                (positionType.HasFlag(PositionType3D.ListenerWithAutomation) && !positionType.HasFlag(PositionType3D.EmitterWithAutomation)));
        
        var has3dPositioning = version > 129 
                                ? bits.HasFlag(PositioningFlags.HasListenerRelativeRouting) 
                                : bits.HasFlag(PositioningFlags.Is3DPositioningAvailable);

        bool has2dPositioning = false;
        bool hasDynamic = false;

        if (hasPositioning)
        {
            if(version <= 56)
            {
                pNode.Items.Add(MakeInt32Node(bin, "CenterPct"));
                pNode.Items.Add(MakeFloatNode(bin, "PanRL"));
                pNode.Items.Add(MakeFloatNode(bin, "PanFR"));
            }

            if(version <= 89)
            {
                if(version < 72)
                {
                    pNode.Items.Add(MakeBoolByteNode(bin, "Has2DPositioning"));
                    bin.Skip(-1);
                    has2dPositioning = bin.ReadBoolByte();
                }

                pNode.Items.Add(MakeBoolByteNode(bin, "Has3DPositioning"));
                bin.Skip(-1);
                has3dPositioning = bin.ReadBoolByte();
                if ((!has3dPositioning && version <= 72) || has2dPositioning)
                {
                    pNode.Items.Add(MakeBoolByteNode(bin, "HasPanner"));
                }
            }
        }

        if (has3dPositioning)
        {
            if(version <= 89)
            {
                pNode.Items.Add(MakeUInt32EnumNode<PositioningType>(bin, "PositioningType"));
                bin.Skip(-4);
                var type = (PositioningType)bin.ReadUInt32();

                (hasAutomation, hasDynamic) = SpatializationHelpers.GetBoolFlagsFromType(type, hasAutomation, version);
            }
            else
            {
                var mode = SpatializationHelpers.GetModeFromByte(bin.ReadByte(), version);
                pNode.Items.Add(new BinInterpNode(bin.Position - 1, $"SpatializationMode: {mode}") { Length = 1});
                hasAutomation = SpatializationHelpers.GetHasAutomationFromMode(mode, hasAutomation, version);
            }

            if (version <= 129) pNode.Items.Add(MakeWwiseIdRefNode(bin, "AttenuationId"));
            if (version <= 89) pNode.Items.Add(MakeBoolByteNode(bin, "IsSpatialized"));
            if(hasDynamic) pNode.Items.Add(MakeBoolByteNode(bin, "UnkBool"));
            
            if(hasAutomation)
            {
                Scan_HIRC_Automation(pNode, bin, version);
            }
        }

        root.Items.Add(pNode);
    }

    private void Scan_HIRC_Automation(BinInterpNode root, EndianReader bin, uint version)
    {
        var aNode = new BinInterpNode(bin.Position, "Automation");

        if (version <= 89)
        {
            aNode.Items.Add(MakeUInt32EnumNode<PathModeInner>(bin, "PathModeInner"));
            aNode.Items.Add(MakeBoolByteNode(bin, "IsLooping"));
        }
        else aNode.Items.Add(MakeByteEnumNode<PathModeInner>(bin, "PathModeInner"));

        aNode.Items.Add(MakeInt32Node(bin, "TransitionTime"));
        if (version is > 37 and < 89) aNode.Items.Add(MakeBoolByteNode(bin, "FollowOrientation"));

        aNode.Items.Add(MakeArrayNode(bin, "Vertices", (i) =>
        {
            var vert = new BinInterpNode(bin.Position, $"Vertex {i}");
            vert.Items.Add(MakeFloatNode(bin, "X"));
            vert.Items.Add(MakeFloatNode(bin, "Y"));
            vert.Items.Add(MakeFloatNode(bin, "Z"));
            vert.Items.Add(MakeFloatNode(bin, "Duration"));
            return vert;
        }));

        var pathListCount = bin.ReadUInt32();
        bin.Skip(-4);
        aNode.Items.Add(MakeArrayNode(bin, "PathList", (i) => {
            var path = new BinInterpNode(bin.Position, $"Path Item {i}");
            path.Items.Add(MakeUInt32Node(bin, "VerticesOffset"));
            path.Items.Add(MakeUInt32Node(bin, "VerticesCount"));
            return path;
        }));

        var autoParamsList = new BinInterpNode(bin.Position, "AutomationParams");
        for (var i = 0; i < pathListCount; i++)
        {
            var param = new BinInterpNode(bin.Position, $"Path Item {i}");
            param.Items.Add(MakeFloatNode(bin, "XRange"));
            param.Items.Add(MakeFloatNode(bin, "YRange"));
            if(version > 89) param.Items.Add(MakeFloatNode(bin, "ZRange"));
            autoParamsList.Items.Add(param);
        }
        aNode.Items.Add(autoParamsList);

        root.Items.Add(aNode);
    }

    private void Scan_HIRC_AuxParams(BinInterpNode root, EndianReader bin, uint version)
    {
        var aNode = new BinInterpNode(bin.Position, "AuxParams");

        bool hasAux = false;

        if(version <= 89)
        {
            aNode.Items.Add(MakeBoolByteNode(bin, "OverrideGameAuxSends"));
            aNode.Items.Add(MakeBoolByteNode(bin, "UseGameAuxSends"));
            aNode.Items.Add(MakeBoolByteNode(bin, "OverrideUserAuxSends"));
            hasAux = bin.ReadBoolByte();
            bin.Skip(-1);
            aNode.Items.Add(MakeBoolByteNode(bin, "HasAux"));
        }
        else
        {
            var flags = (AuxFlags)bin.ReadByte();
            bin.Skip(-1);
            aNode.Items.Add(MakeByteEnumNode<AuxFlags>(bin, "AuxFlags"));
            if (version is 122 or > 135 && flags.HasFlag(AuxFlags.OverrideReflections)) // not sure how relevant this is - copied from Wwiser.NET
            {
                flags |= AuxFlags.HasAux;
            }
            hasAux = flags.HasFlag(AuxFlags.HasAux);
        }

        if(hasAux)
        {
            for (var i = 0; i < 4; i++)
            {
                aNode.Items.Add(MakeWwiseIdRefNode(bin, $"AuxFlags[{i}]"));
            }
        }

        if(version > 134)
        {
            aNode.Items.Add(MakeWwiseIdRefNode(bin, "ReflectionsAuxBus"));
        }

        root.Items.Add(aNode);
    }

    private void Scan_HIRC_AdvSettingsParams(BinInterpNode root, EndianReader bin, uint version)
    {
        var aNode = new BinInterpNode(bin.Position, "AdvancedSettingsParams");

        if (version > 89)
        {
            aNode.Items.Add(MakeByteEnumNode<AdvFlags>(bin, "AdvFlags"));
        }

        aNode.Items.Add(MakeByteEnumNode<VirtualQueueBehavior>(bin, "VirtualQueueBehavior"));

        if (version <= 89)
        {
            aNode.Items.Add(MakeBoolByteNode(bin, "KillNewest"));
            if (version > 53) aNode.Items.Add(MakeBoolByteNode(bin, "UseVirtualBehavior"));
        }

        aNode.Items.Add(MakeUInt16Node(bin, "MaxNumInstance"));

        if (version is <= 89 and > 53) aNode.Items.Add(MakeBoolByteNode(bin, "IsGlobalLimit"));
        aNode.Items.Add(MakeByteEnumNode<BelowThresholdBehavior>(bin, "BelowThresholdBehavior"));

        if (version <= 89)
        {
            aNode.Items.Add(MakeBoolByteNode(bin, "IsMaxNumInstOverrideParent"));
            aNode.Items.Add(MakeBoolByteNode(bin, "IsVVoicesOptOverrideParent"));
            if (version > 72)
            {
                aNode.Items.Add(MakeBoolByteNode(bin, "OverrideHdrEnvelope"));
                aNode.Items.Add(MakeBoolByteNode(bin, "OverrideAnalysis"));
                aNode.Items.Add(MakeBoolByteNode(bin, "NormalizeLoudness"));
                aNode.Items.Add(MakeBoolByteNode(bin, "EnableEnvelope"));
            }
        }
        else
        {
            aNode.Items.Add(MakeByteEnumNode<AdvOverrides>(bin, "AdvOverrides"));
        }
        root.Items.Add(aNode);
    }

    private void Scan_HIRC_State(BinInterpNode root, EndianReader bin, uint version)
    {
        var sNode = new BinInterpNode(bin.Position, "State");

        if(version <= 52)
        {
            ReadStateGroup(sNode, bin, version);

        }
        else
        {
            var countPos = bin.Position;
            var propsCount = VarCount.ReadResizingUint(bin.BaseStream);
            bin.JumpTo(countPos);
            sNode.Items.Add(MakeWwiseVarCountNode(bin, "StatePropsCount"));

            if(propsCount > 0)
            {
                var props = new BinInterpNode(bin.Position, "PropertyInfo") { IsExpanded = true };
                for (var i = 0;i < propsCount; i++)
                {
                    var item = new BinInterpNode(bin.Position, i.ToString());

                    item.Items.Add(MakeWwiseVarCountNode(bin, "PropertyId"));
                    var accumType = (AccumTypeInner)bin.ReadByte();
                    if (version <= 125) accumType += 1;
                    item.Items.Add(new BinInterpNode(bin.Position - 1, $"AccumType: {Enum.GetName(accumType)}"));
                    if (version > 126) item.Items.Add(MakeBoolByteNode(bin, "InDb"));

                    props.Items.Add(item);
                }

                sNode.Items.Add(props);
            }

            countPos = bin.Position;
            var groupsCount = VarCount.ReadResizingUint(bin.BaseStream);
            bin.JumpTo(countPos);
            sNode.Items.Add(MakeWwiseVarCountNode(bin, "StateGroupsCount"));

            if(groupsCount > 0)
            {
                var groups = new BinInterpNode(bin.Position, "GroupChunks") { IsExpanded = true };
                for (var i = 0; i < propsCount; i++)
                {
                    var item = new BinInterpNode(bin.Position, i.ToString());

                    item.Items.Add(MakeWwiseIdNode(bin, "StateGroup"));
                    ReadStateGroup(item, bin, version);
                    groups.Items.Add(item);
                }

                sNode.Items.Add(groups);
            }
        }

        root.Items.Add(sNode);

        void ReadStateGroup(BinInterpNode root, EndianReader bin, uint version)
        {
            root.Items.Add(MakeByteEnumNode<SyncTypeInner>(bin, "SyncType"));
            var countPos = bin.Position;
            var stateCount = ReadStateCount();
            var length = (int)(bin.Position - countPos);
            bin.JumpTo(countPos);
            root.Items.Add(new BinInterpNode(bin.Position, $"StateCount: {ReadStateCount()}") { Length = length });
            var states = new BinInterpNode(bin.Position, "States") { IsExpanded = true };
            for (var i = 0; i < stateCount; i++)
            {
                var state = new BinInterpNode(bin.Position, $"{i}");
                state.Items.Add(MakeWwiseIdNode(bin, "State"));
                if (version <= 120) state.Items.Add(MakeWwiseIdRefNode(bin, "StateId"));
                if (version <= 52) state.Items.Add(MakeBoolByteNode(bin, "IsCustom"));
                if (version <= 145) state.Items.Add(MakeWwiseIdRefNode(bin, "StateInstanceId"));

                // bunch of stuff goes right here except its only higher wwise versions! score!
                states.Items.Add(state);
            }
            root.Items.Add(states);
        }

        uint ReadStateCount()
        {
            if (version > 122)
            {
                return VarCount.ReadResizingUint(bin.BaseStream);
            }
            else if (version is > 36 and <= 52)
            {
                return bin.ReadUInt16();
            }
            else
            {
                return bin.ReadUInt32();
            }
        }
    }

    private void Scan_HIRC_RTPCParameterNodeBase(BinInterpNode root, EndianReader bin, uint version)
    {
        root.Items.Add(MakeArrayNodeInt16Count(bin, "RTPCs", i =>
        {
            var rtpc = new BinInterpNode(bin.Position, $"RTPC {i}");

            rtpc.Items.Add(MakeWwiseIdRefNode(bin, "PluginId"));
            rtpc.Items.Add(MakeBoolByteNode(bin, "IsRendered"));
            rtpc.Items.Add(MakeWwiseIdRefNode(bin, "RTPCId"));
            var rtpcType = bin.ReadByte();
            if (version <= 140 && rtpcType == 0x02) rtpcType = 0x04;
            rtpc.Items.Add(new BinInterpNode(bin.Position - 1, $"RTPCType: {Enum.GetName((RtpcTypeInner)rtpcType)}") { Length = 1 });
            var accumType = (AccumTypeInner)bin.ReadByte();
            if (version <= 125) accumType += 1;
            rtpc.Items.Add(new BinInterpNode(bin.Position - 1, $"AccumType: {Enum.GetName(accumType)}"));
            if(version <= 89) rtpc.Items.Add(MakeUInt32EnumNode<RtpcParameterId>(bin, "ParameterId"));
            else if(version <= 113) rtpc.Items.Add(MakeByteEnumNode<RtpcParameterId>(bin, "ParameterId"));
            else
            {
                var pos = bin.Position;
                var parameterId = (RtpcParameterId)VarCount.ReadResizingUint(bin.BaseStream);
                bin.JumpTo(pos);
                var node = MakeWwiseVarCountNode(bin, $"ParameterId");
                node.Header += $" ({Enum.GetName(parameterId)})";
                rtpc.Items.Add(node);
            }
            rtpc.Items.Add(MakeWwiseIdRefNode(bin, "RtpcCurveId"));
            rtpc.Items.Add(MakeByteEnumNode<CurveScalingInner>(bin, "CurveScaling"));
            rtpc.Items.Add(MakeArrayNodeInt16Count(bin, "Graph", i =>
            {
                var gItem = new BinInterpNode(bin.Position, $"Graph Item {i}");
                gItem.Items.Add(MakeFloatNode(bin, "From"));
                gItem.Items.Add(MakeFloatNode(bin, "To"));
                gItem.Items.Add(MakeUInt32EnumNode<CurveInterpolation>(bin, "CurveInterpolation"));
                return gItem;
            }));

            return rtpc;
        }));
    }

    private void Scan_HIRC_FeedbackInfo(BinInterpNode root, EndianReader bin, uint version)
    {
        root.Items.Add(MakeWwiseIdRefNode(bin, "BusId"));
        bin.Skip(-4);
        if(bin.ReadUInt32() != 0)
        {
            root.Items.Add(MakeFloatNode(bin, "FeedbackVolume"));
            root.Items.Add(MakeFloatNode(bin, "FeedbackModifierMin"));
            root.Items.Add(MakeFloatNode(bin, "FeedbackModifierMax"));
            root.Items.Add(MakeFloatNode(bin, "FeedbackLPF"));
            root.Items.Add(MakeFloatNode(bin, "FeedbackLPFModifierMin"));
            root.Items.Add(MakeFloatNode(bin, "FeedbackLPFModifierMax"));
        }
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
