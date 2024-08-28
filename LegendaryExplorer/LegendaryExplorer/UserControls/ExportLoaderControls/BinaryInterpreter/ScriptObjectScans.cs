using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public partial class BinaryInterpreterWPF
{
    private List<ITreeItem> StartScriptStructScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(data) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.Skip(binarystart);
            subnodes.AddRange(MakeUStructNodes(bin));

            UnrealFlags.ScriptStructFlags flags = (UnrealFlags.ScriptStructFlags)bin.ReadUInt32();
            BinInterpNode objectFlagsNode;
            subnodes.Add(objectFlagsNode = new BinInterpNode(bin.Position - 4, $"Struct Flags: 0x{(uint)flags:X8}")
            {
                IsExpanded = true
            });

            foreach (var flag in Enums.GetValues<UnrealFlags.ScriptStructFlags>())
            {
                if (flags.HasFlag(flag))
                {
                    objectFlagsNode.Items.Add(new BinInterpNode
                    {
                        Header = $"{(ulong)flag:X16} {flag}",
                        Offset = (int)bin.Position
                    });
                }
            }

            MemoryStream ms = new MemoryStream(data) { Position = bin.Position };
            var containingClass = CurrentLoadedExport.FileRef.GetUExport(CurrentLoadedExport.idxLink);
            var scriptStructProperties = PropertyCollection.ReadProps(CurrentLoadedExport, ms, "ScriptStruct", includeNoneProperty: true, entry: containingClass);

            UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
            foreach (Property prop in scriptStructProperties)
            {
                InterpreterExportLoader.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
            }
            subnodes.AddRange(topLevelTree.ChildrenProperties);
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartPropertyScan(byte[] data, ref int binaryStart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.Skip(binaryStart);

            subnodes.AddRange(MakeUFieldNodes(bin));
            subnodes.Add(MakeInt32Node(bin, "ArraySize"));

            UnrealFlags.EPropertyFlags ObjectFlagsMask = (UnrealFlags.EPropertyFlags)bin.ReadUInt64();
            BinInterpNode objectFlagsNode;
            subnodes.Add(objectFlagsNode = new BinInterpNode(bin.Position - 8, $"PropertyFlags: 0x{(ulong)ObjectFlagsMask:X16}")
            {
                IsExpanded = true
            });

            //Create objectflags tree
            foreach (UnrealFlags.EPropertyFlags flag in Enums.GetValues<UnrealFlags.EPropertyFlags>())
            {
                if ((ObjectFlagsMask & flag) != UnrealFlags.EPropertyFlags.None)
                {
                    string reason = UnrealFlags.propertyflagsdesc[flag];
                    objectFlagsNode.Items.Add(new BinInterpNode
                    {
                        Header = $"{(ulong)flag:X16} {flag} {reason}",
                        Offset = (int)(bin.Position - 8)
                    });
                }
            }

            if (Pcc.Platform is MEPackage.GamePlatform.PC || (Pcc.Game is not MEGame.ME3 && Pcc.Platform is MEPackage.GamePlatform.Xenon))
            {
                // This seems missing on Xenon 2011. Not sure about others
                subnodes.Add(MakeNameNode(bin, "Category"));
                subnodes.Add(MakeEntryNode(bin, "ArraySizeEnum"));
            }

            if (ObjectFlagsMask.HasFlag(UnrealFlags.EPropertyFlags.Net))
            {
                subnodes.Add(MakeUInt16Node(bin, "ReplicationOffset"));
            }

            switch (CurrentLoadedExport.ClassName)
            {
                case "ByteProperty":
                case "BioMask4Property":
                case "StructProperty":
                case "ObjectProperty":
                case "ComponentProperty":
                case "ArrayProperty":
                case "InterfaceProperty":
                    subnodes.Add(MakeEntryNode(bin, "Holds objects of type"));
                    break;
                case "DelegateProperty":
                    subnodes.Add(MakeEntryNode(bin, "Holds objects of type"));
                    subnodes.Add(MakeEntryNode(bin, "Same as above but only if this is in a function or struct"));
                    break;
                case "ClassProperty":
                    subnodes.Add(MakeEntryNode(bin, "Outer class"));
                    subnodes.Add(MakeEntryNode(bin, "Class type"));
                    break;
                case "MapProperty":
                    subnodes.Add(MakeEntryNode(bin, "Key Type"));
                    subnodes.Add(MakeEntryNode(bin, "Value Type"));
                    break;
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private IEnumerable<ITreeItem> MakeUFieldNodes(EndianReader bin)
    {
        if (Pcc.Game is not MEGame.UDK)
        {
            yield return MakeEntryNode(bin, "SuperClass");
        }
        yield return MakeEntryNode(bin, "Next item in compiling chain");
    }

    private IEnumerable<ITreeItem> MakeUStructNodes(EndianReader bin)
    {
        foreach (ITreeItem node in MakeUFieldNodes(bin))
        {
            yield return node;
        }
        if (Pcc.Game is MEGame.UDK)
        {
            yield return MakeEntryNode(bin, "SuperClass");
        }
        if (Pcc.Game is MEGame.ME1 or MEGame.ME2 or MEGame.UDK && Pcc.Platform != MEPackage.GamePlatform.PS3)
        {
            yield return MakeEntryNode(bin, "ScriptText");
        }
        yield return MakeEntryNode(bin, "ChildListStart");
        if (Pcc.Game is MEGame.ME1 or MEGame.ME2 or MEGame.UDK && Pcc.Platform != MEPackage.GamePlatform.PS3)
        {
            yield return MakeEntryNode(bin, "C++ Text");
            yield return MakeInt32Node(bin, "Source file line number");
            yield return MakeInt32Node(bin, "Source file text position");
        }
        if (Pcc.Game >= MEGame.ME3 || Pcc.Platform == MEPackage.GamePlatform.PS3)
        {
            yield return MakeInt32Node(bin, "ScriptByteCodeSize");
        }

        int pos = (int)bin.Position;
        int count = bin.ReadInt32();
        var scriptNode = new BinInterpNode(pos, $"Script ({count} bytes)") { Length = 4 + count };

        byte[] scriptBytes = bin.ReadBytes(count);

        if (Pcc.Game == MEGame.ME3 && count > 0)
        {
            try
            {
                (List<Token> tokens, _) = Bytecode.ParseBytecode(scriptBytes, CurrentLoadedExport);
                string scriptText = "";
                foreach (Token t in tokens)
                {
                    scriptText += $"0x{t.pos:X4} {t.text}\n";
                }

                scriptNode.Items.Add(new BinInterpNode
                {
                    Header = scriptText,
                    Offset = pos + 4,
                    Length = count
                });
            }
            catch (Exception)
            {
                scriptNode.Items.Add(new BinInterpNode
                {
                    Header = "Error reading script",
                    Offset = pos + 4
                });
            }
        }

        yield return scriptNode;
    }

    private IEnumerable<ITreeItem> MakeUStateNodes(EndianReader bin)
    {
        foreach (ITreeItem node in MakeUStructNodes(bin))
        {
            yield return node;
        }

        int probeMaskPos = (int)bin.Position;
        var probeFuncs = (UnrealFlags.EProbeFunctions)(Pcc.Game is MEGame.UDK ? bin.ReadUInt32() : bin.ReadUInt64());
        var probeMaskNode = new BinInterpNode(probeMaskPos, $"ProbeMask: {(ulong)probeFuncs:X16}")
        {
            Length = 8,
            IsExpanded = true
        };
        foreach (UnrealFlags.EProbeFunctions flag in probeFuncs.MaskToList())
        {
            probeMaskNode.Items.Add(new BinInterpNode
            {
                Header = $"{(ulong)flag:X16} {flag}",
                Offset = probeMaskPos,
                Length = Pcc.Game is MEGame.UDK ? 4 : 8
            });
        }
        yield return probeMaskNode;

        if (Pcc.Game is not MEGame.UDK)
        {
            int ignoreMaskPos = (int)bin.Position;
            var ignoredFuncs = (UnrealFlags.EProbeFunctions)bin.ReadUInt64();
            var ignoreMaskNode = new BinInterpNode(ignoreMaskPos, $"IgnoreMask: {(ulong)ignoredFuncs:X16}")
            {
                Length = 8,
                IsExpanded = true
            };
            foreach (UnrealFlags.EProbeFunctions flag in (~ignoredFuncs).MaskToList())
            {
                ignoreMaskNode.Items.Add(new BinInterpNode
                {
                    Header = $"{(ulong)flag:X16} {flag}",
                    Offset = ignoreMaskPos,
                    Length = 8
                });
            }
            yield return ignoreMaskNode;
        }
        yield return MakeInt16Node(bin, "Label Table Offset");
        yield return new BinInterpNode(bin.Position, $"StateFlags: {getStateFlagsStr((UnrealFlags.EStateFlags)bin.ReadUInt32())}") { Length = 4 };
        yield return MakeArrayNode(bin, "Local Functions", i =>
            new BinInterpNode(bin.Position, $"{bin.ReadNameReference(Pcc)}() = {Pcc.GetEntryString(bin.ReadInt32())}"));
    }

    private List<ITreeItem> StartClassScan(byte[] data)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.SkipInt32(); // This is already done by normal scan
            subnodes.AddRange(MakeUStateNodes(bin));

            int classFlagsPos = (int)bin.Position;
            var ClassFlags = (UnrealFlags.EClassFlags)bin.ReadUInt32();

            var classFlagsNode = new BinInterpNode(classFlagsPos, $"ClassFlags: {(int)ClassFlags:X8}", NodeType.StructLeafInt) { IsExpanded = true };
            subnodes.Add(classFlagsNode);

            foreach (UnrealFlags.EClassFlags flag in ClassFlags.MaskToList())
            {
                if (flag != UnrealFlags.EClassFlags.Inherit)
                {
                    classFlagsNode.Items.Add(new BinInterpNode
                    {
                        Header = $"{(ulong)flag:X16} {flag}",
                        Offset = classFlagsPos
                    });
                }
            }

            if (Pcc.Game <= MEGame.ME2 && Pcc.Platform != MEPackage.GamePlatform.PS3)
            {
                subnodes.Add(MakeByteNode(bin, "Unknown byte"));
            }
            subnodes.Add(MakeEntryNode(bin, "Outer Class"));
            subnodes.Add(MakeNameNode(bin, "Class Config Name"));

            if (Pcc.Game <= MEGame.ME2 && Pcc.Platform != MEPackage.GamePlatform.PS3)
            {
                subnodes.Add(MakeArrayNode(bin, "Unknown name list 1", i => MakeNameNode(bin, $"{i}")));
            }

            subnodes.Add(MakeArrayNode(bin, "Component Table", i =>
                new BinInterpNode(bin.Position, $"{bin.ReadNameReference(Pcc)} ({Pcc.GetEntryString(bin.ReadInt32())})")));
            subnodes.Add(MakeArrayNode(bin, "Interface Table", i =>
                new BinInterpNode(bin.Position, $"{Pcc.GetEntryString(bin.ReadInt32())} => {Pcc.GetEntryString(bin.ReadInt32())}")));

            if (Pcc.Game is MEGame.UDK)
            {
                subnodes.Add(MakeArrayNode(bin, "DontSortCategories", i => MakeNameNode(bin, $"{i}")));
                subnodes.Add(MakeArrayNode(bin, "HideCategories", i => MakeNameNode(bin, $"{i}")));
                subnodes.Add(MakeArrayNode(bin, "AutoExpandCategories", i => MakeNameNode(bin, $"{i}")));
                subnodes.Add(MakeArrayNode(bin, "AutoCollapseCategories", i => MakeNameNode(bin, $"{i}")));
                subnodes.Add(MakeBoolIntNode(bin, "bForceScriptOrder"));
                subnodes.Add(MakeArrayNode(bin, "Unknown name list", i => MakeNameNode(bin, $"{i}")));
                subnodes.Add(MakeStringNode(bin, "Class Name?"));
            }

            if (Pcc.Game >= MEGame.ME3 || Pcc.Platform == MEPackage.GamePlatform.PS3)
            {
                subnodes.Add(MakeNameNode(bin, "DLL Bind Name"));
                if (Pcc.Game is not MEGame.UDK)
                {
                    subnodes.Add(MakeUInt32Node(bin, "Unknown"));
                }
            }
            else
            {
                subnodes.Add(MakeArrayNode(bin, "Unknown name list 2", i => MakeNameNode(bin, $"{i}")));
            }

            if (Pcc.Game is MEGame.LE2 || Pcc.Platform == MEPackage.GamePlatform.PS3 && Pcc.Game == MEGame.ME2)
            {
                subnodes.Add(MakeUInt32Node(bin, "LE2 & PS3 ME2 Unknown"));
            }
            subnodes.Add(MakeEntryNode(bin, "Defaults"));
            if (Pcc.Game.IsGame3())
            {
                subnodes.Add(MakeArrayNode(bin, "Virtual Function Table", i => MakeEntryNode(bin, $"{i}: ")));
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    public static string getStateFlagsStr(UnrealFlags.EStateFlags stateFlags)
    {
        string str = null;
        if (stateFlags.Has(UnrealFlags.EStateFlags.Editable))
        {
            str += "Editable ";
        }
        if (stateFlags.Has(UnrealFlags.EStateFlags.Auto))
        {
            str += "Auto ";
        }
        if (stateFlags.Has(UnrealFlags.EStateFlags.Simulated))
        {
            str += "Simulated ";
        }
        return str ?? "None";
    }

    private List<ITreeItem> StartEnumScan(byte[] data, ref int binaryStart)
    {
        var subnodes = new List<ITreeItem>();

        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.Skip(binaryStart);
            subnodes.AddRange(MakeUFieldNodes(bin));

            subnodes.Add(MakeInt32Node(bin, "Enum size"));
            int enumCount = bin.Skip(-4).ReadInt32();
            NameReference enumName = CurrentLoadedExport.ObjectName;
            for (int i = 0; i < enumCount; i++)
            {
                subnodes.Add(MakeNameNode(bin, $"{enumName}[{i}]"));
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartConstScan(byte[] data, ref int binaryStart)
    {
        var subnodes = new List<ITreeItem>();

        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.Skip(binaryStart);
            subnodes.AddRange(MakeUFieldNodes(bin));

            subnodes.Add(MakeStringNode(bin, "Literal Value"));
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartStateScan(byte[] data, ref int binaryStart)
    {
        var subnodes = new List<ITreeItem>();

        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.Skip(binaryStart);
            subnodes.AddRange(MakeUStateNodes(bin));
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartFunctionScan(byte[] data, ref int binaryStart)
    {
        var subnodes = new List<ITreeItem>();

        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.Skip(binaryStart);
            subnodes.AddRange(MakeUStructNodes(bin));
            subnodes.Add(MakeUInt16Node(bin, "NativeIndex"));
            if (Pcc.Game.IsGame1() || Pcc.Game.IsGame2() || Pcc.Game is MEGame.UDK)
            {
                subnodes.Add(MakeByteNode(bin, "OperatorPrecedence"));
            }

            int funcFlagsPos = (int)bin.Position;
            var funcFlags = (UnrealFlags.EFunctionFlags)bin.ReadUInt32();
            var probeMaskNode = new BinInterpNode(funcFlagsPos, $"Function Flags: {(ulong)funcFlags:X8}")
            {
                Length = 4,
                IsExpanded = true
            };
            foreach (UnrealFlags.EFunctionFlags flag in funcFlags.MaskToList())
            {
                probeMaskNode.Items.Add(new BinInterpNode
                {
                    Header = $"{(ulong)flag:X8} {flag}",
                    Offset = funcFlagsPos,
                    Length = 4
                });
            }
            subnodes.Add(probeMaskNode);

            if (Pcc.Game is MEGame.ME1 or MEGame.ME2 or MEGame.UDK && Pcc.Platform != MEPackage.GamePlatform.PS3 && funcFlags.Has(UnrealFlags.EFunctionFlags.Net))
            {
                subnodes.Add(MakeUInt16Node(bin, "ReplicationOffset"));
            }
            if ((Pcc.Game.IsGame1() || Pcc.Game.IsGame2() || Pcc.Game is MEGame.UDK) && Pcc.Platform != MEPackage.GamePlatform.PS3)
            {
                subnodes.Add(MakeNameNode(bin, "FriendlyName"));
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }
}