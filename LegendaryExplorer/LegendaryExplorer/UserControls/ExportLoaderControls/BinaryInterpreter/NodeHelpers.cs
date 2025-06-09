using System;
using System.Collections.Generic;
using System.Text;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public partial class BinaryInterpreterWPF
{
    private static BinInterpNode MakeBoolIntNode(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadBoolInt()}", NodeType.StructLeafBool) { Length = 4 };

    private static BinInterpNode MakeReverseBoolIntNode(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadUInt32() != 1}", NodeType.StructLeafBool) { Length = 4 };

    private static BinInterpNode MakeBoolIntNode(EndianReader bin, string name, out bool boolVal)
    {
        return new BinInterpNode(bin.Position, $"{name}: {boolVal = bin.ReadBoolInt()}", NodeType.StructLeafBool)
            { Length = 4 };
    }

    private static BinInterpNode MakeBoolByteNode(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadBoolByte()}") { Length = 1 };

    private static BinInterpNode MakeBoolByteNode(EndianReader bin, string name, out bool value) =>
        new BinInterpNode(bin.Position, $"{name}: {value = bin.ReadBoolByte()}") { Length = 1 };
    
    private static BinInterpNode MakeByteNode(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadByte()}") { Length = 1 };

    private static BinInterpNode MakeByteNode(EndianReader bin, string name, out byte value) =>
        new BinInterpNode(bin.Position, $"{name}: {value = bin.ReadByte()}") { Length = 1 };

    private static BinInterpNode MakeSByteNode(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadSByte()}") { Length = 1 };

    private static BinInterpNode MakeSByteNode(EndianReader bin, string name, out sbyte value) =>
        new BinInterpNode(bin.Position, $"{name}: {value = bin.ReadSByte()}") { Length = 1 };

    private static BinInterpNode MakeInt16Node(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadInt16()}") { Length = 2 };

    private static BinInterpNode MakeInt16Node(EndianReader bin, string name, out short value) =>
        new BinInterpNode(bin.Position, $"{name}: {value = bin.ReadInt16()}") { Length = 2 };

    private static BinInterpNode MakeUInt16Node(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadUInt16()}") { Length = 2 };

    private static BinInterpNode MakeUInt16Node(EndianReader bin, string name, out ushort value) =>
        new BinInterpNode(bin.Position, $"{name}: {value = bin.ReadUInt16()}") { Length = 2 };

    private static BinInterpNode MakeInt32Node(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadInt32()}", NodeType.StructLeafInt) { Length = 4 };

    private static BinInterpNode MakeInt32Node(EndianReader bin, string name, out int value) =>
        new BinInterpNode(bin.Position, $"{name}: {value = bin.ReadInt32()}", NodeType.StructLeafInt) { Length = 4 };

    private static BinInterpNode MakeUInt32Node(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadUInt32()}") { Length = 4 };

    private static BinInterpNode MakeUInt32Node(EndianReader bin, string name, out uint value) =>
        new BinInterpNode(bin.Position, $"{name}: {value = bin.ReadUInt32()}") { Length = 4 };

    private static BinInterpNode MakeUInt32HexNode(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadUInt32():X8}") { Length = 4 };

    private static BinInterpNode MakeInt64Node(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadInt64()}") { Length = 8 };

    private static BinInterpNode MakeInt64Node(EndianReader bin, string name, out long value) =>
        new BinInterpNode(bin.Position, $"{name}: {value = bin.ReadInt64()}") { Length = 8 };

    private static BinInterpNode MakeUInt64Node(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadUInt64()}") { Length = 8 };

    private static BinInterpNode MakeUInt64Node(EndianReader bin, string name, out ulong value) =>
        new BinInterpNode(bin.Position, $"{name}: {value = bin.ReadUInt64()}") { Length = 8 };
    
    private static BinInterpNode MakeFloatNode(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadFloat()}", NodeType.StructLeafFloat) { Length = 4 };

    private static BinInterpNode MakeFloatNode(EndianReader bin, string name, out float value)
    {
        return new BinInterpNode(bin.Position, $"{name}: {value = bin.ReadFloat()}", NodeType.StructLeafFloat)
            { Length = 4 };
    }

    private static BinInterpNode MakeFloatNodeConditional(EndianReader bin, string name, bool create)
    {
        if (create)
        {
            return new BinInterpNode(bin.Position, $"{name}: {bin.ReadFloat()}", NodeType.StructLeafFloat)
                { Length = 4 };
        }

        return null;
    }

    private BinInterpNode MakeNameNode(EndianReader bin, string name) => new BinInterpNode(bin.Position,
        $"{name}: {bin.ReadNameReference(Pcc).Instanced}", NodeType.StructLeafName) { Length = 8 };

    private BinInterpNode MakeNameNode(EndianReader bin, string name, out NameReference nameRef) =>
        new BinInterpNode(bin.Position, $"{name}: {nameRef = bin.ReadNameReference(Pcc).Instanced}",
            NodeType.StructLeafName) { Length = 8 };

    private BinInterpNode MakeEntryNode(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {entryRefString(bin)}", NodeType.StructLeafObject) { Length = 4 };

    private BinInterpNode MakeEntryNode(EndianReader bin, string name, out int uIndex)
    {
        long binPosition = bin.Position;
        uIndex = bin.ReadInt32();
        string refString = $"#{uIndex} {CurrentLoadedExport.FileRef.GetEntryString(uIndex)}";
        return new BinInterpNode(binPosition, $"{name}: {refString}", NodeType.StructLeafObject) { Length = 4 };
    }
    
        private static BinInterpNode MakeArrayNode(int count, EndianReader bin, string name, Func<int, BinInterpNode> selector, bool IsExpanded = false)
    {
        return new BinInterpNode(bin.Position, $"{name} ({count})")
        {
            IsExpanded = IsExpanded,
            Items = ReadList(count, selector)
        };
    }
    
    private static BinInterpNode MakeArrayNode(EndianReader bin, string name, Func<int, BinInterpNode> selector, bool IsExpanded = false,
        BinInterpNode.ArrayPropertyChildAddAlgorithm arrayAddAlgo = BinInterpNode.ArrayPropertyChildAddAlgorithm.None)
    {
        int count;
        return new BinInterpNode(bin.Position, $"{name} ({count = bin.ReadInt32()})")
        {
            IsExpanded = IsExpanded,
            Items = ReadList(count, selector),
            ArrayAddAlgorithm = arrayAddAlgo,
            Length = 4
        };
    }

    private static BinInterpNode MakeArrayNodeByteCount(EndianReader bin, string name, Func<int, BinInterpNode> selector, bool IsExpanded = false,
        BinInterpNode.ArrayPropertyChildAddAlgorithm arrayAddAlgo = BinInterpNode.ArrayPropertyChildAddAlgorithm.None)
    {
        int count;
        return new BinInterpNode(bin.Position, $"{name} ({count = bin.ReadByte()})")
        {
            IsExpanded = IsExpanded,
            Items = ReadList(count, selector),
            ArrayAddAlgorithm = arrayAddAlgo,
            Length = 1
        };
    }

    private static BinInterpNode MakeArrayNodeInt16Count(EndianReader bin, string name, Func<int, BinInterpNode> selector, bool IsExpanded = false,
        BinInterpNode.ArrayPropertyChildAddAlgorithm arrayAddAlgo = BinInterpNode.ArrayPropertyChildAddAlgorithm.None)
    {
        int count;
        return new BinInterpNode(bin.Position, $"{name} ({count = bin.ReadInt16()})")
        {
            IsExpanded = IsExpanded,
            Items = ReadList(count, selector),
            ArrayAddAlgorithm = arrayAddAlgo,
            Length = 2
        };
    }
    
    private static BinInterpNode MakeByteArrayNode(EndianReader bin, string name)
    {
        int pos = (int)bin.Position;
        int count = bin.ReadInt32();
        bin.Skip(count);
        return new BinInterpNode(pos, $"{name} ({count} bytes)");
    }

    private static BinInterpNode MakePackedNormalNode(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position,
            $"{name}: (X: {bin.ReadByte() / 127.5f - 1}, Y: {bin.ReadByte() / 127.5f - 1}, Z: {bin.ReadByte() / 127.5f - 1}, W: {bin.ReadByte() / 127.5f - 1})")
        {
            Length = 4
        };

    private static BinInterpNode MakeVectorNodeEditable(EndianReader bin, string name, bool expanded = false)
    {
        var node = new BinInterpNode(bin.Position,
            $"{name}: (X: {bin.ReadFloat()}, Y: {bin.ReadFloat()}, Z: {bin.ReadFloat()})") { Length = 12 };
        bin.Position -= 12;
        node.Items.Add(MakeFloatNode(bin, "X"));
        node.Items.Add(MakeFloatNode(bin, "Y"));
        node.Items.Add(MakeFloatNode(bin, "Z"));
        node.IsExpanded = expanded;
        return node;
    }

    private static BinInterpNode MakeVector2DNodeEditable(EndianReader bin, string name, bool expanded = false)
    {
        var node = new BinInterpNode(bin.Position, $"{name}: (X: {bin.ReadFloat()}, Y: {bin.ReadFloat()})")
            { Length = 8 };
        bin.Position -= 8;
        node.Items.Add(MakeFloatNode(bin, "X"));
        node.Items.Add(MakeFloatNode(bin, "Y"));
        node.IsExpanded = expanded;
        return node;
    }

    private static BinInterpNode MakeVectorNode(EndianReader bin, string name)
    {
        var node = new BinInterpNode(bin.Position,
            $"{name}: (X: {bin.ReadFloat()}, Y: {bin.ReadFloat()}, Z: {bin.ReadFloat()})") { Length = 12 };
        bin.Position -= 12;
        node.Items.Add(MakeFloatNode(bin, "X"));
        node.Items.Add(MakeFloatNode(bin, "Y"));
        node.Items.Add(MakeFloatNode(bin, "Z"));
        return node;
    }

    private static BinInterpNode MakeQuatNode(EndianReader bin, string name)
    {
        var node = new BinInterpNode(bin.Position,
                $"{name}: (X: {bin.ReadFloat()}, Y: {bin.ReadFloat()}, Z: {bin.ReadFloat()}, W: {bin.ReadFloat()})")
            { Length = 16 };
        bin.Position -= 16;
        node.Items.Add(MakeFloatNode(bin, "X"));
        node.Items.Add(MakeFloatNode(bin, "Y"));
        node.Items.Add(MakeFloatNode(bin, "Z"));
        node.Items.Add(MakeFloatNode(bin, "W"));
        return node;
    }

    private static BinInterpNode MakeRotatorNode(EndianReader bin, string name)
    {
        var node = new BinInterpNode(bin.Position,
            $"{name}: (Pitch: {bin.ReadInt32()}, Yaw: {bin.ReadInt32()}, Roll: {bin.ReadInt32()})") { Length = 12 };
        bin.Position -= 12;
        node.Items.Add(MakeInt32Node(bin, "Pitch"));
        node.Items.Add(MakeInt32Node(bin, "Yaw"));
        node.Items.Add(MakeInt32Node(bin, "Roll"));
        return node;
    }

    private static BinInterpNode MakeBoxNode(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, name)
        {
            IsExpanded = true,
            Items =
            {
                MakeVectorNode(bin, "Min"),
                MakeVectorNode(bin, "Max"),
                new BinInterpNode(bin.Position, $"IsValid: {bin.ReadBoolByte()}")
            },
            Length = 25
        };

    private static BinInterpNode MakeVector2DNode(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: (X: {bin.ReadFloat()}, Y: {bin.ReadFloat()})") { Length = 8 };

    private static BinInterpNode MakeVector2DHalfNode(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: (X: {bin.ReadFloat16()}, Y: {bin.ReadFloat16()})") { Length = 4 };

    private static BinInterpNode MakeColorNode(EndianReader bin, string name)
    {
        return new BinInterpNode(bin.Position, $"{name}")
        {
            Length = 4,
            Items =
            {
                new BinInterpNode(bin.Position, $"B: {bin.ReadByte()}"),
                new BinInterpNode(bin.Position, $"G: {bin.ReadByte()}"),
                new BinInterpNode(bin.Position, $"R: {bin.ReadByte()}"),
                new BinInterpNode(bin.Position, $"A: {bin.ReadByte()}"),
            }
        };
    }

    private static BinInterpNode MakeBoxSphereBoundsNode(EndianReader bin, string name)
    {
        return new BinInterpNode(bin.Position, $"{name}")
        {
            Items =
            {
                MakeVectorNode(bin, "Origin"),
                MakeVectorNode(bin, "BoxExtent"),
                MakeFloatNode(bin, "SphereRadius")
            }
        };
    }

    private static BinInterpNode MakeGuidNode(EndianReader bin, string name) =>
        new BinInterpNode(bin.Position, $"{name}: {bin.ReadGuid()}", NodeType.Guid) { Length = 16 };

    private static BinInterpNode MakeMaterialGuidNode(EndianReader bin, string name,
        Dictionary<Guid, string> materialGuidMap = null)
    {
        var guid = bin.ReadGuid();
        var node = new BinInterpNode(bin.Position - 16, $"{name}: {guid}") { Length = 16 };

#if DEBUG
        if (materialGuidMap != null && materialGuidMap.TryGetValue(guid, out var matName))
        {
            node.Header += " " + matName;
        }
#endif

        node.Tag = NodeType.Guid;
        return node;
    }

    private BinInterpNode MakeStringNode(EndianReader bin, string nodeName)
    {
        int pos = (int)bin.Position;
        int strLen = bin.ReadInt32();
        string str;
        if (Pcc.Game is MEGame.ME3 or MEGame.LE3)
        {
            strLen *= -2;
            str = bin.BaseStream.ReadStringUnicodeNull(strLen);
        }
        else
        {
            str = bin.BaseStream.ReadStringLatin1Null(strLen);
        }

        return new BinInterpNode(pos, $"{nodeName}: {str}", NodeType.StructLeafStr) { Length = strLen + 4 };
    }

    private static BinInterpNode MakeStringUTF8Node(EndianReader bin, string nodeName)
    {
        int pos = (int)bin.Position;
        int strLen = bin.ReadInt32();
        string str = bin.BaseStream.ReadStringUtf8(strLen);
        return new BinInterpNode(pos, $"{nodeName}: {str}", NodeType.StructLeafStr) { Length = strLen + 4 };
    }

    private static BinInterpNode MakeSHANode(EndianReader bin, string name, out string sha)
    {
        var shaBytes = bin.ReadBytes(20);
        StringBuilder sb = new StringBuilder();
        foreach (var b in shaBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        sha = sb.ToString();
        return new BinInterpNode(bin.Position, $"{name}: {sha}") { Length = 20 };
    }

    private static List<ITreeItem> ReadList(int count, Func<int, ITreeItem> selector)
    {
        //sanity check. if this number is too small, feel free to increase
        if (count > 5097152)
        {
            throw new Exception($"Is this actually a list? {count} seems like an incorrect count");
        }
        var list = new List<ITreeItem>();
        try
        {
            for (int i = 0; i < count; i++)
            {
                list.Add(selector(i));
            }
        }
        catch (Exception ex)
        {
            list.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return list;
    }

    private string entryRefString(EndianReader bin)
    {
        int n = bin.ReadInt32();
        return $"#{n} {CurrentLoadedExport.FileRef.GetEntryString(n)}";
    }
    
    private BinInterpNode MakeByteEnumNode<T>(EndianReader bin, string name) where T : Enum
    {
        var value = bin.ReadByte();
        var parsedValue = Enum.GetName(typeof(T), value);
        if (string.IsNullOrEmpty(parsedValue)) parsedValue = "None";
        return new BinInterpNode(bin.Position - 1, $"{name}: {parsedValue}") { Length = 1 };
    }

    private BinInterpNode MakeUInt32EnumNode<T>(EndianReader bin, string name) where T : Enum
    {
        var value = bin.ReadUInt32();
        var parsedValue = Enum.GetName(typeof(T), value);
        if (string.IsNullOrEmpty(parsedValue)) parsedValue = "None";
        return new BinInterpNode(bin.Position - 4, $"{name}: {parsedValue}") { Length = 4 };
    }
}