using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using ME3Tweaks.Wwiser.Formats;
using ME3Tweaks.Wwiser.Model;
using ME3Tweaks.Wwiser.Model.Action;
using ME3Tweaks.Wwiser.Model.Action.Specific;
using ME3Tweaks.Wwiser.Model.GlobalSettings;
using ME3Tweaks.Wwiser.Model.Hierarchy;
using ME3Tweaks.Wwiser.Model.Hierarchy.Enums;
using ME3Tweaks.Wwiser.Model.ParameterNode;
using ME3Tweaks.Wwiser.Model.ParameterNode.Positioning;
using ME3Tweaks.Wwiser.Model.RTPC;
using static ME3Tweaks.Wwiser.Model.Hierarchy.Enums.AccumType;
using static ME3Tweaks.Wwiser.Model.Hierarchy.Enums.CurveScaling;
using static ME3Tweaks.Wwiser.Model.Hierarchy.Enums.GroupType;
using static ME3Tweaks.Wwiser.Model.Hierarchy.Enums.PriorityOverrideFlags;
using static ME3Tweaks.Wwiser.Model.Hierarchy.MediaInformation;
using static ME3Tweaks.Wwiser.Model.Hierarchy.RanSeqFlags;
using static ME3Tweaks.Wwiser.Model.ParameterNode.AdvSettingsParams;
using static ME3Tweaks.Wwiser.Model.ParameterNode.AuxParams;
using static ME3Tweaks.Wwiser.Model.ParameterNode.Positioning.PathMode;
using static ME3Tweaks.Wwiser.Model.ParameterNode.Positioning.PositioningChunk;
using static ME3Tweaks.Wwiser.Model.RTPC.RtpcType;
using static ME3Tweaks.Wwiser.Model.State.SyncType;
using LanguageId = ME3Tweaks.Wwiser.Model.LanguageId;
using static LegendaryExplorer.UserControls.ExportLoaderControls.BinaryNodeFactory;

namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public class WwiseBankScans
{
    private record WwiseItem(uint Id, string Name, long Position);

    private Dictionary<uint, WwiseItem> WwiseIdMap = new();

    private List<(BinInterpNodeOffsetReference, uint)> WwiseRefs = new();

    private BinInterpNode MakeWwiseIdNode(EndianReader bin, string refName, string nodeName = "ID")
    {
        var node = MakeUInt32Node(bin, nodeName, out var id);
        var item = new WwiseItem(id, refName, bin.Position - 4);
        WwiseIdMap[id] = item;
        return node;
    }

    private BinInterpNode MakeWwiseIdRefNode(EndianReader bin, string name)
    {
        var pos = bin.Position;
        var id = bin.ReadUInt32();
        var node = new BinInterpNodeOffsetReference(pos, $"{name}: {id}") { Length = 4 };
        WwiseRefs.Add((node, id));
        return node;
    }

    private BinInterpNode MakeWwiseUniNode(EndianReader bin, string name)
    {
        var node = new BinInterpNode(bin.Position, $"{name}: ") { Length = 4 };
        Span<byte> span = stackalloc byte[4];
        var read = bin.BaseStream.Read(span);
        if (read != 4)
        {
            node.Header += "Error reading data. Expected 4 bytes.";
            return node;
        }
        uint value = BitConverter.ToUInt32(span);
        if (value > 0x10000000)
        {
            // float
            var f = BitConverter.ToSingle(span);
            node.Header += f.ToString(CultureInfo.InvariantCulture);
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

    private static BinInterpNode MakeArrayNodeWwiseVarCount(EndianReader bin, string name, Func<int, BinInterpNode> selector, bool IsExpanded = false,
        BinInterpNode.ArrayPropertyChildAddAlgorithm arrayAddAlgo = BinInterpNode.ArrayPropertyChildAddAlgorithm.None)
    {
        var pos = bin.Position;
        uint count;
        return new BinInterpNode(bin.Position, $"{name} ({count = VarCount.ReadResizingUint(bin.BaseStream)})")
        {
            IsExpanded = IsExpanded,
            Items = ReadList((int)count, selector),
            ArrayAddAlgorithm = arrayAddAlgo,
            Length = (int)(bin.Position - pos)
        };
    }

    public List<ITreeItem> Scan_WwiseBank(byte[] data, ExportEntry export)
    {
        var subnodes = new List<ITreeItem>();
        WwiseIdMap.Clear();
        var bin = new EndianReader(new MemoryStream(data)) { Endian = export.FileRef.Endian };
        bin.JumpTo(export.propsEnd());

        if (export.Game is MEGame.ME2 or MEGame.LE2)
        {
            subnodes.Add(MakeUInt32Node(bin, "Unk1"));
            subnodes.Add(MakeUInt32Node(bin, "Unk2"));
            if (bin.Skip(-8).ReadInt64() == 0)
            {
                return subnodes;
            }
        }
        subnodes.Add(new BinInterpNode(bin.Position, $"BulkDataFlags: {(BinaryInterpreterWPF.EBulkDataFlags)bin.ReadUInt32()}"));
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
                case "STMG":
                    Scan_WwiseBank_STMG(chunkNode, bin, version);
                    break;
            }

            // Just in case we don't parse chunk in full - jump to next chunk
            bin.JumpTo(start + size + 8);
        }
        
        // At the end of the file, fill in all the reference details
        AddNodeReferences();
        return subnodes;
    }

    public void AddNodeReferences()
    {
        foreach(var (refNode, refId) in WwiseRefs)
        {
            if (WwiseIdMap.TryGetValue(refId, out var item))
            {
                refNode.Header += $" (Ref to {item.Name})";
                refNode.OffsetTarget = (int)item.Position;
            }
            else
            {
                refNode.Header += $" (Ref to unknown)";
            }
        }
        WwiseRefs = new List<(BinInterpNodeOffsetReference, uint)>();
    }

    private (uint, bool) Scan_WwiseBank_BKHD(BinInterpNode root, EndianReader bin, int size)
    {
        bool useFeedback = false;
        uint version;
        
        root.Items.Add(MakeUInt32Node(bin, "WwiseVersion", out version));
        root.Items.Add(MakeWwiseIdNode(bin, "SoundBank", "SoundBankId"));

        if(version <= 122)
        {
            root.Items.Add(MakeUInt32EnumNode<LanguageId>(bin, "LanguageID"));
        }
        else
        {
            root.Items.Add(MakeUInt32Node(bin, "LanguageIDStringHash"));
        }

        if(version is > 27 and < 126)
        {
            root.Items.Add(MakeBoolByteNode(bin, "UseFeedback", out useFeedback));
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
        root.Items.Add(MakeArrayNode(bin, "Items", i => MakeHIRCNode(i, bin, version, useFeedback), isExpanded: true));
    }

    public BinInterpNode MakeHIRCNode(int index, EndianReader bin, uint version, bool useFeedback)
    {
        var start = bin.Position;
        var root = new BinInterpNode(bin.Position, $"{index}: ");

        var type = HircSmartType.DeserializeStatic(bin.BaseStream, version);
        var typeLen = (version <= 48) ? 4 : 1;
        root.Items.Add(new BinInterpNode(bin.Position - typeLen, $"Type: {type}") { Length = typeLen });
        
        
        root.Items.Add(MakeUInt32Node(bin, "Size", out var fullSize));
        fullSize += (uint)(version <= 48 ? 8 : 5);

        root.Items.Add(MakeWwiseIdNode(bin, type.ToString()));

        root.Header += $"{type}";
        root.Length = (int)fullSize;

        switch(type)
        {
            case HircType.State:
                if (version <= 56)
                {
                    root.Items.Add(MakeFloatNode(bin, "Volume"));
                    root.Items.Add(MakeFloatNode(bin, "LFEVolume"));
                    root.Items.Add(MakeFloatNode(bin, "Pitch"));
                    root.Items.Add(MakeFloatNode(bin, "LPF"));
                    if(version <= 52)
                    {
                        root.Items.Add(MakeByteEnumNode<VolumeMeaning>(bin, "VolumeValueMeaning"));
                        root.Items.Add(MakeByteEnumNode<VolumeMeaning>(bin, "LFEValueMeaning"));
                        root.Items.Add(MakeByteEnumNode<VolumeMeaning>(bin, "PitchValueMeaning"));
                        root.Items.Add(MakeByteEnumNode<VolumeMeaning>(bin, "LPFValueMeaning"));
                    }
                }
                else
                {
                    var propBPos = bin.Position;
                    var propCount = (version <= 126) ? bin.ReadByte() : (byte)bin.ReadUInt16();
                    root.Items.Add(new BinInterpNode(propBPos, $"Prop Count: {propCount}") { Length = (int)(bin.Position - propBPos) });
                    root.Items.Add(MakeArrayNode(propCount, bin, "ParameterIds", i =>
                    {
                        var pidPos = bin.Position;
                        // TODO: Does this ever use modulator? prolly not
                        var (paramId, modParamId) = ParameterId.DeserializeStatic(bin.BaseStream, version, false, isState: true);
                        return paramId.HasValue
                            ? new BinInterpNode(pidPos, $"ParameterId {i}: {Enum.GetName(paramId.Value)}")
                                { Length = (int)(bin.Position - pidPos) }
                            : new BinInterpNode(pidPos, $"ModulatorParameterId {i}: {Enum.GetName(modParamId.Value)}")
                                { Length = (int)(bin.Position - pidPos) };

                    }, true));
                    root.Items.Add(MakeArrayNode(propCount, bin, "Values", i => MakeFloatNode(bin, $"{i}"), true));
                }

                break;
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
            case HircType.Action:
                var (actionType, actionFlags) = ActionType.DeserializeStatic(bin.BaseStream, version);
                root.Header += $" ({actionType.ToString()})";
                var typeLength = version <= 56 ? 4 : 2;
                root.Items.Add(new BinInterpNode(bin.Position - typeLength,
                    $"ActionType: {actionType.ToString()}, Flags: {actionFlags.ToString()}") { Length = typeLength });
                root.Items.Add(MakeWwiseIdRefNode(bin, "Target ID"));
                if (version <= 56)
                {
                    root.Items.Add(MakeInt32Node(bin, "Delay"));
                    root.Items.Add(MakeInt32Node(bin, "DelayModMin"));
                    root.Items.Add(MakeInt32Node(bin, "DelayModMax"));
                }

                if (version > 65) root.Items.Add(MakeBoolByteNode(bin, "IsBus"));
                if (version > 56) Scan_HIRC_InitialParams(root, bin, version);
                switch (actionType)
                {
                    case ActionTypeValue.Play:
                    case ActionTypeValue.PlayAndContinue:
                    case ActionTypeValue.PlayEventUnknown:
                        if (version <= 56)
                        {
                            root.Items.Add(MakeUInt32Node(bin, "SubsectionSize"));
                            root.Items.Add(MakeInt32Node(bin, "TransitionTime"));
                            root.Items.Add(MakeInt32Node(bin, "TransitionTimeModMin"));
                            root.Items.Add(MakeInt32Node(bin, "TransitionTimeModMax"));
                        }
                        root.Items.Add(MakeByteEnumNode<CurveInterpolation>(bin, "CurveInterpolation"));

                        if (version <= 56)
                        {
                            Scan_HIRC_ActionSpecificParams(root, bin, version, actionType);
                            Scan_HIRC_ActionExceptParams(root, bin, version);
                        }
                        root.Items.Add(MakeWwiseIdRefNode(bin, "BankId"));
                        break;
                    case ActionTypeValue.SetState:
                    case ActionTypeValue.SetSwitch:
                        if (version <= 56) root.Items.Add(MakeUInt32Node(bin, "SubsectionSize"));
                        root.Items.Add(MakeWwiseIdRefNode(bin, "GroupId"));
                        root.Items.Add(MakeWwiseIdRefNode(bin, "TargetStateId"));
                        break;
                    case ActionTypeValue.SetRTPC:
                        if (version <= 56) root.Items.Add(MakeUInt32Node(bin, "SubsectionSize"));
                        root.Items.Add(MakeWwiseIdRefNode(bin, "RTPCId"));
                        root.Items.Add(MakeFloatNode(bin, "RTPCValue"));
                        break;
                    case ActionTypeValue.SetFX1:
                    case ActionTypeValue.SetFX2:
                        if (version <= 56) root.Items.Add(MakeUInt32Node(bin, "SubsectionSize"));
                        root.Items.Add(MakeBoolByteNode(bin, "IsAudioDeviceElement"));
                        root.Items.Add(MakeByteNode(bin, "SlotIndex"));
                        root.Items.Add(MakeWwiseIdRefNode(bin, "FXId"));
                        root.Items.Add(MakeBoolByteNode(bin, "IsShared"));
                        Scan_HIRC_ActionExceptParams(root, bin, version);
                        break;
                    case ActionTypeValue.BypassFX1:
                    case ActionTypeValue.BypassFX2:
                    case ActionTypeValue.BypassFX3:
                    case ActionTypeValue.BypassFX4:
                    case ActionTypeValue.BypassFX5:
                    case ActionTypeValue.BypassFX6:
                    case ActionTypeValue.BypassFX7:
                        if (version <= 56) root.Items.Add(MakeUInt32Node(bin, "SubsectionSize"));
                        root.Items.Add(MakeBoolByteNode(bin, "IsBypass"));
                        root.Items.Add(MakeByteNode(bin, "TargetMask"));
                        Scan_HIRC_ActionExceptParams(root, bin, version);
                        break;
                    case ActionTypeValue.Seek:
                        if (version <= 56) root.Items.Add(MakeUInt32Node(bin, "SubsectionSize"));
                        root.Items.Add(MakeBoolByteNode(bin, "IsSeekRelativeToDuration"));
                        root.Items.Add(MakeFloatNode(bin, "SeekValue"));
                        root.Items.Add(MakeFloatNode(bin, "SeekValueModMin"));
                        root.Items.Add(MakeFloatNode(bin, "SeekValueModMax"));
                        root.Items.Add(MakeBoolByteNode(bin, "SnapToNearestMarker"));
                        Scan_HIRC_ActionExceptParams(root, bin, version);
                        break;
                    case ActionTypeValue.UseState1:
                    case ActionTypeValue.UseState2:
                        if (version == 56)
                        {
                            root.Items.Add(MakeInt32Node(bin, "Time"));
                            root.Items.Add(MakeInt32Node(bin, "TimeModMin"));
                            root.Items.Add(MakeInt32Node(bin, "TimeModMax"));
                            root.Items.Add(MakeByteEnumNode<CurveInterpolation>(bin, "CurveInterpolation"));
                            Scan_HIRC_ActionSpecificParams(root, bin, version, actionType);
                            Scan_HIRC_ActionExceptParams(root, bin, version);
                        }
                        break;
                    case ActionTypeValue.Release:
                    case ActionTypeValue.PlayEvent:
                    case ActionTypeValue.Event1:
                    case ActionTypeValue.Event2:
                    case ActionTypeValue.Event3:
                    case ActionTypeValue.Duck:
                    case ActionTypeValue.Break:
                    case ActionTypeValue.Trigger:
                        break;
                    default:
                        if (version <= 56)
                        {
                            root.Items.Add(MakeUInt32Node(bin, "SubsectionSize"));
                            root.Items.Add(MakeInt32Node(bin, "TransitionTime"));
                            root.Items.Add(MakeInt32Node(bin, "TransitionTimeModMin"));
                            root.Items.Add(MakeInt32Node(bin, "TransitionTimeModMax"));
                        }
                        root.Items.Add(MakeByteEnumNode<CurveInterpolation>(bin, "CurveInterpolation"));
                        Scan_HIRC_ActionSpecificParams(root, bin, version, actionType);
                        Scan_HIRC_ActionExceptParams(root, bin, version);
                        break;
                }
                break;
            case HircType.Event:
                root.Items.Add(MakeArrayNodeWwiseVarCount(bin, "Actions", i => MakeWwiseIdRefNode(bin, i.ToString())));
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
                    n.Items.Add(MakeWwiseIdRefNode(bin, "PlaylistItemId"));
                    n.Items.Add(version <= 56 ? MakeByteNode(bin, "Weight") : MakeInt32Node(bin, "Weight"));
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
                    g.Items.Add(MakeArrayNode(bin, "ItemIDs", j => MakeWwiseIdRefNode(bin, $"Item {j}")));
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
                        var gItem = new BinInterpNode(bin.Position, "");
                        gItem.Items.Add(MakeFloatNode(bin, "From", out float from));
                        gItem.Items.Add(MakeFloatNode(bin, "To", out float to));
                        gItem.Items.Add(MakeUInt32EnumNode<CurveInterpolation>(bin, "CurveInterpolation"));
                        gItem.Header += $"{k}: {from} to {to}";
                        return gItem;
                    })));
                    return l;
                }));
                break;
            case HircType.Attenuation:
                if (version > 136) root.Items.Add(MakeBoolByteNode(bin, "IsHeightSpreadEnabled"));
                root.Items.Add(MakeBoolByteNode(bin, "IsConeEnabled", out var isConeEnabled));
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
                    c.Items.Add(MakeArrayNodeInt16Count(bin, $"Graph", k => MakeWwiseGraphItem(bin, k), true));
                    return c;
                }, true));
                Scan_HIRC_RTPCParameterNodeBase(root, bin, version);
                break;
            case HircType.FxShareSet:
            case HircType.FxCustom:
                root.Items.Add(MakeUInt32Node(bin, "PluginID"));
                root.Items.Add(MakeUInt32Node(bin, "PluginParametersSize", out var paramLength));
                root.Items.Add(new BinInterpNode(bin.Position, "PluginParameters") { Length = (int)paramLength });
                bin.Skip(paramLength);
                
                root.Items.Add(MakeArrayNodeByteCount(bin, "Media", i =>
                {
                    var item = new BinInterpNode(bin.Position, i.ToString());
                    item.Items.Add(MakeByteNode(bin, "Index"));
                    item.Items.Add(MakeWwiseIdRefNode(bin, "SourceID"));
                    return item;
                }));
                
                Scan_HIRC_RTPCParameterNodeBase(root, bin, version);
                
                if(version is > 123 and < 126) root.Items.Add(MakeUInt16Node(bin, "Unk1"));
                
                if(version > 126) Scan_HIRC_State(root, bin, version);
                
                if(version > 90) root.Items.Add(MakeArrayNodeInt16Count(bin, "RTPCInitValues", i =>
                {
                    var item = new BinInterpNode(bin.Position, i.ToString());
                    item.Items.Add(MakeWwiseIdRefNode(bin, "ParameterID"));
                    if(version > 126) item.Items.Add(MakeByteNode(bin, "RTPCAccum"));
                    item.Items.Add(MakeFloatNode(bin, "InitValue"));
                    return item;
                }));
                break;
        }

        // Just in case we don't parse item in full - jump to next item
        bin.JumpTo(start + fullSize);
        return root;
    }

    private BinInterpNode MakeWwiseGraphItem(EndianReader bin, int k)
    {
        var gItem = new BinInterpNode(bin.Position, $"Graph Item {k}");
        gItem.Items.Add(MakeFloatNode(bin, "From"));
        gItem.Items.Add(MakeFloatNode(bin, "To"));
        gItem.Items.Add(MakeUInt32EnumNode<CurveInterpolation>(bin, "CurveInterpolation"));
        return gItem;
    }

    private void Scan_HIRC_ActionExceptParams(BinInterpNode root, EndianReader bin, uint version)
    {
        root.Items.Add(MakeArrayNodeWwiseVarCount(bin, "Exceptions", i =>
        {
            var item = MakeWwiseIdNode(bin, $"Exception {i}", $"{i}: ID");
            if (version > 65)
            {
                item.Items.Add(MakeBoolByteNode(bin, "IsBus"));
            }

            return item;
        }));
    }

    private void Scan_HIRC_ActionSpecificParams(BinInterpNode root, EndianReader bin, uint version, ActionTypeValue actionType)
    {
        switch (actionType)
        {
            case ActionTypeValue.Stop:
            case ActionTypeValue.Pause:
            case ActionTypeValue.Resume:
                if (version <= 56)
                {
                    root.Items.Add(MakeBoolIntNode(bin, "IsMaster"));
                    root.Items.Add(MakeReverseBoolIntNode(bin, "IncludePendingResume"));
                    root.Items.Add(MakeReverseBoolIntNode(bin, "ApplyToStateTransitions"));
                    root.Items.Add(MakeReverseBoolIntNode(bin, "ApplyToDynamicSequence"));
                }
                else
                {
                    root.Items.Add(MakeByteEnumNode<ActiveFlags.ActiveFlagsInner>(bin, "ActiveFlags"));
                }

                break;
                
            case ActionTypeValue.SetHPF1:
            case ActionTypeValue.SetHPF2:
                if(version <= 56) root.Items.Add(MakeUInt32EnumNode<SmartValueMeaning.ValueMeaning>(bin, "ValueMeaning"));
                else root.Items.Add(MakeByteEnumNode<SmartValueMeaning.ValueMeaning>(bin, "ValueMeaning"));
                root.Items.Add(MakeFloatNode(bin, "RandomizerModifier"));
                root.Items.Add(MakeFloatNode(bin, "RandomizerModifierMin"));
                root.Items.Add(MakeFloatNode(bin, "RandomizerModifierMax"));
                break;
            case ActionTypeValue.SetGameParameter1:
            case ActionTypeValue.SetGameParameter2:
                if(version > 89) root.Items.Add(MakeBoolByteNode(bin, "BypassTransition"));
                if(version <= 56) root.Items.Add(MakeUInt32EnumNode<SmartValueMeaning.ValueMeaning>(bin, "ValueMeaning"));
                else root.Items.Add(MakeByteEnumNode<SmartValueMeaning.ValueMeaning>(bin, "ValueMeaning"));
                root.Items.Add(MakeFloatNode(bin, "RandomizerModifier"));
                root.Items.Add(MakeFloatNode(bin, "RandomizerModifierMin"));
                root.Items.Add(MakeFloatNode(bin, "RandomizerModifierMax"));
                break;
            case ActionTypeValue.ResetPlaylist:
                break;
            default:
                if (actionType is >= ActionTypeValue.SetPitch1 and <= ActionTypeValue.SetLPF2)
                {
                    // same as SetHPF - I just don't want to write out all the cases.
                    if(version > 89) root.Items.Add(MakeBoolByteNode(bin, "BypassTransition"));
                    if(version <= 56) root.Items.Add(MakeUInt32EnumNode<SmartValueMeaning.ValueMeaning>(bin, "ValueMeaning"));
                    else root.Items.Add(MakeByteEnumNode<SmartValueMeaning.ValueMeaning>(bin, "ValueMeaning"));
                    root.Items.Add(MakeFloatNode(bin, "RandomizerModifier"));
                    root.Items.Add(MakeFloatNode(bin, "RandomizerModifierMin"));
                    root.Items.Add(MakeFloatNode(bin, "RandomizerModifierMax"));
                }
                else if(version <= 56)
                {
                    root.Items.Add(new BinInterpNode(bin.Position, $"Unk") { Length = 10 });
                    bin.Skip(10);
                }

                break;
        }
    }

    private void Scan_HIRC_BankSourceData(BinInterpNode root, EndianReader bin, uint version)
    {
        //var pluginExists = bin.ReadUInt32();
        //bin.Skip(-4);
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
        root.Items.Add(MakeByteNode(bin, "FXCount", out var fxCount));
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
        if(version < 126 && useFeedback) Scan_HIRC_FeedbackInfo(root, bin);
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
            ipNode.Items.Add(MakeByteNode(bin, "ParamsLength", out var paramLength));

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
            
            ipNode.Items.Add(MakeByteNode(bin, "RangesLength", out var rangeLength));

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
                    pNode.Items.Add(MakeBoolByteNode(bin, "Has2DPositioning", out has2dPositioning));
                }

                pNode.Items.Add(MakeBoolByteNode(bin, "Has3DPositioning", out has3dPositioning));
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

        bool hasAux;

        if(version <= 89)
        {
            aNode.Items.Add(MakeBoolByteNode(bin, "OverrideGameAuxSends"));
            aNode.Items.Add(MakeBoolByteNode(bin, "UseGameAuxSends"));
            aNode.Items.Add(MakeBoolByteNode(bin, "OverrideUserAuxSends"));
            aNode.Items.Add(MakeBoolByteNode(bin, "HasAux", out hasAux));
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
            ReadStateGroup(sNode);

        }
        else
        {
            sNode.Items.Add(MakeArrayNodeWwiseVarCount(bin, "StateProperties", i =>
            {
                var item = new BinInterpNode(bin.Position, i.ToString());

                item.Items.Add(MakeWwiseVarCountNode(bin, "PropertyId"));
                var accumType = (AccumTypeInner)bin.ReadByte();
                if (version <= 125) accumType += 1;
                item.Items.Add(new BinInterpNode(bin.Position - 1, $"AccumType: {Enum.GetName(accumType)}"));
                if (version > 126) item.Items.Add(MakeBoolByteNode(bin, "InDb"));
                
                return item;
            }));
            
            sNode.Items.Add(MakeArrayNodeWwiseVarCount(bin, "StateGroups", i =>
            {
                var item = new BinInterpNode(bin.Position, i.ToString());

                item.Items.Add(MakeWwiseIdNode(bin, "StateGroup"));
                ReadStateGroup(item);
                
                return item;
            }));
        }

        root.Items.Add(sNode);
        return;

        void ReadStateGroup(BinInterpNode grp)
        {
            grp.Items.Add(MakeByteEnumNode<SyncTypeInner>(bin, "SyncType"));
            var countPos = bin.Position;
            var stateCount = ReadStateCount();
            var length = (int)(bin.Position - countPos);
            bin.JumpTo(countPos);
            grp.Items.Add(new BinInterpNode(bin.Position, $"StateCount: {ReadStateCount()}") { Length = length });
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
            grp.Items.Add(states);
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

            if (version <= 48)
            {
                rtpc.Items.Add(MakeWwiseIdRefNode(bin, "PluginId"));
                rtpc.Items.Add(MakeBoolByteNode(bin, "IsRendered"));
            }
            rtpc.Items.Add(MakeWwiseIdRefNode(bin, "RTPCId"));
            var rtpcType = bin.ReadByte();
            if (version <= 140 && rtpcType == 0x02) rtpcType = 0x04;
            rtpc.Items.Add(new BinInterpNode(bin.Position - 1, $"RTPCType: {Enum.GetName((RtpcTypeInner)rtpcType)}") { Length = 1 });
            var accumType = (AccumTypeInner)bin.ReadByte();
            if (version <= 125) accumType += 1;
            rtpc.Items.Add(new BinInterpNode(bin.Position - 1, $"AccumType: {Enum.GetName(accumType)}"));
            
            var pidPos = bin.Position;
            var (paramId, modParamId) = ParameterId.DeserializeStatic(bin.BaseStream, version, false); // TODO: use modulator not handles on high versions!
            rtpc.Items.Add(paramId.HasValue
                ? new BinInterpNode(pidPos, $"ParameterId: {Enum.GetName(paramId.Value)}")
                    { Length = (int)(bin.Position - pidPos) }
                : new BinInterpNode(pidPos, $"ModulatorParameterId: {Enum.GetName(modParamId.Value)}")
                    { Length = (int)(bin.Position - pidPos) });
            
            rtpc.Items.Add(MakeWwiseIdRefNode(bin, "RtpcCurveId"));
            rtpc.Items.Add(MakeByteEnumNode<CurveScalingInner>(bin, "CurveScaling"));
            rtpc.Items.Add(MakeArrayNodeInt16Count(bin, "Graph", j =>
            {
                var gItem = new BinInterpNode(bin.Position, $"Graph Item {j}");
                gItem.Items.Add(MakeFloatNode(bin, "From"));
                gItem.Items.Add(MakeFloatNode(bin, "To"));
                gItem.Items.Add(MakeUInt32EnumNode<CurveInterpolation>(bin, "CurveInterpolation"));
                return gItem;
            }));

            return rtpc;
        }));
    }

    private void Scan_HIRC_FeedbackInfo(BinInterpNode root, EndianReader bin)
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

    private void Scan_WwiseBank_STMG(BinInterpNode root, EndianReader bin, uint version)
    {
        root.Items.Add(MakeFloatNode(bin, "VolumeThreshold"));
        if (version > 53) root.Items.Add(MakeUInt16Node(bin, "MaxNumVoicesLimitInternal"));
        if (version > 126) root.Items.Add(MakeUInt16Node(bin, "MaxNumDangerousVirtVoicesLimitInternal"));
        
        root.Items.Add(MakeArrayNode(bin, "StateGroups", i =>
        {
            var sg = new BinInterpNode(bin.Position, $"Group {i}");
            sg.Items.Add(MakeWwiseIdNode(bin, "StateId"));
            sg.Items.Add(MakeUInt32Node(bin, "DefaultTransitionTime"));
            if(version <= 52) sg.Items.Add(MakeArrayNode(bin, "CustomStates", j =>  MakeHIRCNode(j, bin, version, false)));
            sg.Items.Add(MakeArrayNode(bin, "StateTransitions", j =>
            {
                var st = new BinInterpNode(bin.Position, $"{j}");
                st.Items.Add(MakeWwiseIdRefNode(bin, "FromStateId"));
                st.Items.Add(MakeWwiseIdRefNode(bin, "ToStateId"));
                st.Items.Add(MakeUInt32Node(bin, "TransitionTime"));
                return st;
            }));
                
            return sg;
        }));
        
        root.Items.Add(MakeArrayNode(bin, "SwitchGroups", i =>
        {
            var sg = new BinInterpNode(bin.Position, $"Group {i}");
            sg.Items.Add(MakeWwiseIdNode(bin, "GroupId"));
            sg.Items.Add(MakeWwiseIdRefNode(bin, "RtpcId?"));
            if (version > 89)
            {
                var rtpcType = bin.ReadByte();
                if (version <= 140 && rtpcType == 0x02)
                {
                    rtpcType = 0x04;
                }
                sg.Items.Add(new BinInterpNode(bin.Position - 1, $"RtpcType: {Enum.GetName((RtpcTypeInner)rtpcType)}") { Length = 1 });
            }
            sg.Items.Add(MakeArrayNode(bin, "Graph", j => MakeWwiseGraphItem(bin, j)));
            return sg;
        }));
        
        root.Items.Add(MakeArrayNode(bin, "Params", i =>
        {
            var p = new BinInterpNode(bin.Position, $"Param {i}");
            p.Items.Add(MakeWwiseIdRefNode(bin, "RtpcParamId"));
            p.Items.Add(MakeFloatNode(bin, "Value"));
            if (version > 89)
            {
                p.Items.Add(MakeUInt32EnumNode<TransitionRampingType>(bin, "RampingType"));
                p.Items.Add(MakeFloatNode(bin, "RampUp"));
                p.Items.Add(MakeFloatNode(bin, "RampDown"));
                p.Items.Add(MakeByteEnumNode<BuiltInParam>(bin, "BuiltInParam"));
            }
            return p;
        }));

        if (version > 118) root.Items.Add(MakeArrayNode(bin, "AcousticTextures", i =>
        {
            var at = new BinInterpNode(bin.Position, $"Acoustic Texture {i}");
            at.Items.Add(MakeWwiseIdNode(bin, "AcousticTexture"));
            foreach(var p in (string[])["AbsorptionOffset", "AbsorptionLow", "AbsorptionMidLow", "AbsorptionMidHigh", "AbsorptionHigh", "Scattering"])
            {
                at.Items.Add(MakeFloatNode(bin, p));
            }
            return at;
        }));
    }
}
