using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using static LegendaryExplorer.UserControls.ExportLoaderControls.BinaryNodeFactory;

namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public partial class BinaryInterpreterWPF
{
    private List<ITreeItem> StartBioPawnScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            int count;
            subnodes.Add(new BinInterpNode(bin.Position, $"AnimationMap? ({count = bin.ReadInt32()})")
            {
                Items = ReadList(count, i => new BinInterpNode(bin.Position, $"{bin.ReadNameReference(Pcc)}: {MakeEntryNodeString(bin, Pcc)}", NodeType.StructLeafObject) { Length = 4 })
            });

            binarystart = (int)bin.Position;
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private List<ITreeItem> StartSFXMorphFaceFrontEndDataSourceScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            subnodes.Add(MakeArrayNode(bin, "DefaultSettingsNames", i => new BinInterpNode(bin.Position, $"{i}")
            {
                IsExpanded = true,
                Items =
                {
                    MakeStringNode(bin, "Name", Pcc.Game),
                    MakeInt32Node(bin, "Index")
                }
            }, true));

            binarystart = (int)bin.Position;
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private List<ITreeItem> StartBioCreatureSoundSetScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            subnodes.Add(MakeArrayNode(bin, "UnkToCueMap?", i => new BinInterpNode(bin.Position, $"{i}")
            {
                IsExpanded = true,
                Items =
                {
                    MakeByteNode(bin, "Unknown byte"),
                    MakeInt32Node(bin, "index into m_aAllCues?")
                }
            }, true));

            binarystart = (int)bin.Position;
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private List<ITreeItem> StartBioTlkFileSetScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            int offset = binarystart;
            if (data.Length > binarystart)
            {
                int count = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X4} Count: {count}",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                //offset += 4;
                //offset += 8; //skip 8

                for (int i = 0; i < count; i++)
                {
                    int langRef = BitConverter.ToInt32(data, offset);
                    int langTlkCount = BitConverter.ToInt32(data, offset + 8);
                    var languageNode = new BinInterpNode
                    {
                        Header = $"0x{offset:X4} {CurrentLoadedExport.FileRef.GetNameEntry(langRef)} - {langTlkCount} entries",
                        Offset = offset,
                        Tag = NodeType.StructLeafName,
                        IsExpanded = true
                    };
                    subnodes.Add(languageNode);
                    offset += 12;

                    for (int k = 0; k < langTlkCount; k++)
                    {
                        int tlkIndex = BitConverter.ToInt32(data, offset); //-1 in reader
                        languageNode.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X4} TLK #{k} export: {tlkIndex} {CurrentLoadedExport.FileRef.GetEntryString(tlkIndex)}",
                            Offset = offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartBioStateEventMapScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            int offset = binarystart;

            int eCount = BitConverter.ToInt32(data, offset);
            var EventCountNode = new BinInterpNode
            {
                Header = $"0x{offset:X4} State Event Count: {eCount}",
                Offset = offset,
                Tag = NodeType.StructLeafInt
            };
            offset += 4;
            subnodes.Add(EventCountNode);

            for (int e = 0; e < eCount; e++) //EVENTS
            {
                int iEventID = BitConverter.ToInt32(data, offset);  //EVENT ID
                var EventIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} [{e}] State Transition ID: {iEventID} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                EventCountNode.Items.Add(EventIDs);

                int EventMapInstVer = BitConverter.ToInt32(data, offset); //Event Instance Version
                EventIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Instance Version: {EventMapInstVer} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int nTransitions = BitConverter.ToInt32(data, offset); //Count of State Events
                var TransitionsIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Transitions: {nTransitions} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                EventIDs.Items.Add(TransitionsIDs);

                for (int t = 0; t < nTransitions; t++) //TRANSITIONS
                {
                    int transTYPE = BitConverter.ToInt32(data, offset); //Get TYPE
                    if (transTYPE == 0)  // TYPE 0 = BOOL STATE EVENT
                    {
                        offset += 8;
                        int tPlotID = BitConverter.ToInt32(data, offset);  //Get Plot
                        offset -= 8;
                        var nTransition = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Type: {transTYPE} Transition on Bool {tPlotID}",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset += 4;
                        TransitionsIDs.Items.Add(nTransition);

                        int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        tPlotID = BitConverter.ToInt32(data, offset);  //Plot
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Plot ID: {tPlotID} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int tNewValue = BitConverter.ToInt32(data, offset);  //NewValue
                        bool bNewValue = false;
                        if (tNewValue == 1) { bNewValue = true; }
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} New Value: {tNewValue}  {bNewValue} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                        bool bUseParam = false;
                        if (tUseParam == 1) { bUseParam = true; }
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }
                    else if (transTYPE == 1) //TYPE 1 = CONSEQUENCE
                    {
                        var nTransition = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Type: {transTYPE} Consequence",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset += 4;
                        TransitionsIDs.Items.Add(nTransition);

                        int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int tConsequenceParam = BitConverter.ToInt32(data, offset);  //Consequence parameter
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Consequence Parameter: {tConsequenceParam} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }
                    else if (transTYPE == 2)  // TYPE 2 = FLOAT TRANSITION
                    {
                        offset += 8;
                        int tPlotID = BitConverter.ToInt32(data, offset);  //Get Plot
                        offset -= 8;
                        var nTransition = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Type: {transTYPE} transition on Float {tPlotID}",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset += 4;
                        TransitionsIDs.Items.Add(nTransition);

                        int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        if (Pcc.Game.IsGame2())
                        {
                            int tIncrement = BitConverter.ToInt32(data, offset);  //Increment bool
                            bool bIncrement = false;
                            if (tIncrement == 1) { bIncrement = true; }
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Increment value: {tIncrement}  {bIncrement} ",
                                Offset = offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;
                        }

                        tPlotID = BitConverter.ToInt32(data, offset);  //Plot
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Plot ID: {tPlotID} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        float tNewValue = BitConverter.ToInt32(data, offset);  //NewValue
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} New Value: {tNewValue} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafFloat
                        });
                        offset += 4;

                        int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                        bool bUseParam = false;
                        if (tUseParam == 1) { bUseParam = true; }
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        if (!Pcc.Game.IsGame2())
                        {
                            int tIncrement = BitConverter.ToInt32(data, offset);  //Increment bool
                            bool bIncrement = false;
                            if (tIncrement == 1) { bIncrement = true; }
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Increment value: {tIncrement}  {bIncrement} ",
                                Offset = offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;
                        }
                    }
                    else if (transTYPE == 3)  // TYPE 3 = FUNCTION
                    {
                        var nTransition = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Type: {transTYPE} Function",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset += 4;
                        TransitionsIDs.Items.Add(nTransition);

                        int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int PackageName = BitConverter.ToInt32(data, offset);  //Package name
                        offset += 4;
                        int PackageIdx = BitConverter.ToInt32(data, offset);  //Package name idx
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Package Name: {new NameReference(CurrentLoadedExport.FileRef.GetNameEntry(PackageName), PackageIdx).Instanced}",
                            Offset = offset,
                            Tag = NodeType.StructLeafName
                        });
                        offset += 4;

                        int ClassName = BitConverter.ToInt32(data, offset);  //Class name
                        offset += 4;
                        int ClassIdx = BitConverter.ToInt32(data, offset);  //Class name idx
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Class Name: {new NameReference(CurrentLoadedExport.FileRef.GetNameEntry(ClassName), ClassIdx).Instanced}",
                            Offset = offset,
                            Tag = NodeType.StructLeafName
                        });
                        offset += 4;

                        int FunctionName = BitConverter.ToInt32(data, offset);  //Function name
                        offset += 4;
                        int FunctionIdx = BitConverter.ToInt32(data, offset);  //Function name idx
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Function Name: {new NameReference(CurrentLoadedExport.FileRef.GetNameEntry(FunctionName), FunctionIdx).Instanced}",
                            Offset = offset,
                            Tag = NodeType.StructLeafName
                        });
                        offset += 4;

                        int Parameter = BitConverter.ToInt32(data, offset);  //Parameter
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Parameter: {Parameter} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }
                    else if (transTYPE == 4)  // TYPE 4 = INT TRANSITION
                    {
                        offset += 8;
                        int tPlotID = BitConverter.ToInt32(data, offset);  //Get Plot
                        offset -= 8;
                        var nTransition = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Type: {transTYPE} transition on INT {tPlotID}",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset += 4;
                        TransitionsIDs.Items.Add(nTransition);

                        int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        tPlotID = BitConverter.ToInt32(data, offset);  //Plot
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Plot ID: {tPlotID} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int tNewValue = BitConverter.ToInt32(data, offset);  //NewValue
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} New Value: {tNewValue} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafFloat
                        });
                        offset += 4;

                        int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                        bool bUseParam = false;
                        if (tUseParam == 1) { bUseParam = true; }
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int tIncrement = BitConverter.ToInt32(data, offset);  //Increment bool
                        bool bIncrement = false;
                        if (tIncrement == 1) { bIncrement = true; }
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Increment value: {tIncrement}  {bIncrement} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }
                    else if (transTYPE == 5)  // TYPE 5 = LOCAL BOOL
                    {
                        var nTransition = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Type: {transTYPE} Local Bool",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset += 36;
                        TransitionsIDs.Items.Add(nTransition);
                    }
                    else if (transTYPE == 6)  // TYPE 6 = LOCAL FLOAT
                    {
                        var nTransition = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Type: {transTYPE} Local Float",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset += 36;
                        TransitionsIDs.Items.Add(nTransition);
                    }
                    else if (transTYPE == 7)  // TYPE 7 = LOCAL INT
                    {
                        var nTransition = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Type: {transTYPE} Local Int",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset += 4;
                        TransitionsIDs.Items.Add(nTransition);

                        int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int tObjtag = BitConverter.ToInt32(data, offset);  //Use Object tag??
                        bool bObjtag = false;
                        if (tObjtag == 1) { bObjtag = true; }
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Object Tag: {tObjtag}  {bObjtag} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int FunctionName = BitConverter.ToInt32(data, offset);  //Function name
                        offset += 4;
                        int FunctionIdx = BitConverter.ToInt32(data, offset);  //Function name
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Function Name: {new NameReference(CurrentLoadedExport.FileRef.GetNameEntry(FunctionName), FunctionIdx).Instanced}",
                            Offset = offset,
                            Tag = NodeType.StructLeafName
                        });
                        offset += 4;

                        int TagName = BitConverter.ToInt32(data, offset);  //Object name
                        offset += 4;
                        int TagIdx = BitConverter.ToInt32(data, offset);  //Object idx
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Object Name: {new NameReference(CurrentLoadedExport.FileRef.GetNameEntry(TagName), TagIdx).Instanced}",
                            Offset = offset,
                            Tag = NodeType.StructLeafName
                        });
                        offset += 4;

                        int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                        bool bUseParam = false;
                        if (tUseParam == 1) { bUseParam = true; }
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int tNewValue = BitConverter.ToInt32(data, offset);  //NewValue
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} New Value: {tNewValue} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafFloat
                        });
                        offset += 4;
                    }
                    else if (transTYPE == 8)  // TYPE 8 = SUBSTATE
                    {
                        offset += 8;
                        int tPlotID = BitConverter.ToInt32(data, offset);  //Get Plot
                        offset -= 8;
                        var nTransition = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Type: {transTYPE} Substate Transition on Bool {tPlotID}",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset += 4;
                        TransitionsIDs.Items.Add(nTransition);

                        int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        tPlotID = BitConverter.ToInt32(data, offset);  //Plot
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Plot ID: {tPlotID} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int tNewValue = BitConverter.ToInt32(data, offset);  //NewState Bool
                        bool bNewValue = false;
                        if (tNewValue == 1) { bNewValue = true; }
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} New State: {tNewValue}  {bNewValue}",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                        bool bUseParam = false;
                        if (tUseParam == 1) { bUseParam = true; }
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int tParentType = BitConverter.ToInt32(data, offset);  //Parent OR type flag
                        bool bParentType = false;
                        string sParentType = "ALL of siblings TRUE => Parent TRUE";
                        if (tParentType == 1)
                        {
                            bParentType = true;
                            sParentType = "ANY of siblings TRUE => Parent TRUE";
                        }
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Parent OR type: {tParentType}  {bParentType} {sParentType}",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int ParentIdx = BitConverter.ToInt32(data, offset);  //Parent Bool
                        nTransition.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Parent Bool: {ParentIdx} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int sibCount = BitConverter.ToInt32(data, offset); //Sibling Substates
                        var SiblingIDs = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Sibling Substates Count: {sibCount} ",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset += 4;
                        nTransition.Items.Add(SiblingIDs);

                        for (int s = 0; s < sibCount; s++)  //SIBLING SUBSTATE BOOLS
                        {
                            int nSibling = BitConverter.ToInt32(data, offset);
                            var nSiblings = new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Sibling: {s}  Bool: {nSibling}",
                                Offset = offset,
                                Tag = NodeType.StructLeafInt
                            };
                            SiblingIDs.Items.Add(nSiblings);
                            offset += 4;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartBioQuestMapScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        var game = CurrentLoadedExport.FileRef.Game;
        try
        {
            int offset = binarystart;

            int qCount = BitConverter.ToInt32(data, offset);
            var QuestNode = new BinInterpNode
            {
                Header = $"0x{offset:X4} Quest Count: {qCount}",
                Offset = offset,
                Tag = NodeType.StructLeafInt
            };
            offset += 4;
            subnodes.Add(QuestNode);

            for (int i = 0; i < qCount; i++) //QUESTS
            {
                int iQuestID = BitConverter.ToInt32(data, offset);  //QUEST ID
                var QuestIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Quest ID: {iQuestID} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                QuestNode.Items.Add(QuestIDs);

                int instanceVersion = BitConverter.ToInt32(data, offset); //Unknown1
                QuestIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} InstanceVersion: {instanceVersion} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int isMission = BitConverter.ToInt32(data, offset); //Unknown2
                bool isMissionB = isMission == 1;
                QuestIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} IsMission: {isMission} {isMissionB} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int gCount = BitConverter.ToInt32(data, offset); //Goal Count
                var GoalsIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Goals: {gCount} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                QuestIDs.Items.Add(GoalsIDs);

                for (int g = 0; g < gCount; g++) //GOALS
                {
                    //Add either state or Conditional as starting node
                    offset += 12;
                    int gConditional = BitConverter.ToInt32(data, offset); //Conditional
                    offset += 4;
                    int gState = BitConverter.ToInt32(data, offset); //State
                    offset -= 16;
                    int goalStart = gState;
                    string startType = "Bool";
                    if (gState == -1)
                    {
                        goalStart = gConditional;
                        startType = "Conditional";
                    }
                    var nGoalIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Goal start plot/cnd: {goalStart} {startType}",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    };
                    GoalsIDs.Items.Add(nGoalIDs);

                    int iGoalInstVersion = BitConverter.ToInt32(data, offset);  //Goal Instance Version
                    nGoalIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Goal Instance Version: {iGoalInstVersion} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int gTitle = BitConverter.ToInt32(data, offset); //Goal Name
                    string gttlkLookup = TLKManagerWPF.GlobalFindStrRefbyID(gTitle, game, CurrentLoadedExport.FileRef);
                    nGoalIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Goal Name StrRef: {gTitle} {gttlkLookup}",
                        Offset = offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    int gDescription = BitConverter.ToInt32(data, offset); //Goal Description
                    string gdtlkLookup = TLKManagerWPF.GlobalFindStrRefbyID(gDescription, game, CurrentLoadedExport.FileRef);
                    nGoalIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Goal Description StrRef: {gDescription} {gdtlkLookup}",
                        Offset = offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    gConditional = BitConverter.ToInt32(data, offset); //Conditional
                    nGoalIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Conditional: {gConditional} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    gState = BitConverter.ToInt32(data, offset); //State
                    nGoalIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Bool State: {gState} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;
                }

                int tCount = BitConverter.ToInt32(data, offset); //Task Count
                var TaskIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Tasks Count: {tCount} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                QuestIDs.Items.Add(TaskIDs);

                for (int t = 0; t < tCount; t++)  //TASKS
                {
                    var nTaskIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Task: {t}",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    };
                    TaskIDs.Items.Add(nTaskIDs);

                    int iTaskInstVersion = BitConverter.ToInt32(data, offset);  //Task Instance Version
                    nTaskIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Task Instance Version: {iTaskInstVersion} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int tFinish = BitConverter.ToInt32(data, offset); //Primary Codex
                    bool bFinish = tFinish == 1;
                    nTaskIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Task Finishes Quest: {tFinish}  {bFinish}",
                        Offset = offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    int tTitle = BitConverter.ToInt32(data, offset); //Task Name
                    string tttlkLookup = TLKManagerWPF.GlobalFindStrRefbyID(tTitle, game, CurrentLoadedExport.FileRef);
                    nTaskIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Task Name StrRef: {tTitle} {tttlkLookup}",
                        Offset = offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    int tDescription = BitConverter.ToInt32(data, offset); //Task Description
                    string tdtlkLookup = TLKManagerWPF.GlobalFindStrRefbyID(tDescription, game, CurrentLoadedExport.FileRef);
                    nTaskIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Task Description StrRef: {tDescription} {tdtlkLookup}",
                        Offset = offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    int piCount = BitConverter.ToInt32(data, offset); //Plot item Count
                    var PlotIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Plot Item Count: {piCount} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    nTaskIDs.Items.Add(PlotIDs);

                    for (int pi = 0; pi < piCount; pi++)  //TASK PLOT ITEMS
                    {
                        int iPlotItem = BitConverter.ToInt32(data, offset);  //Plot item index
                        var nPlotItems = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Plot items: {pi}  Index: {iPlotItem}",
                            Offset = offset,
                            Tag = NodeType.StructLeafInt
                        };
                        PlotIDs.Items.Add(nPlotItems);
                        offset += 4;
                    }

                    int planetName = BitConverter.ToInt32(data, offset); //Planet name
                    offset += 4;
                    int planetIdx = BitConverter.ToInt32(data, offset); //Name index
                    offset -= 4;
                    nTaskIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Planet Name: {new NameReference(CurrentLoadedExport.FileRef.GetNameEntry(planetName), planetIdx).Instanced} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafName
                    });
                    offset += 8;

                    int wpStrLgth = BitConverter.ToInt32(data, offset); //String length for waypoint
                    offset += 4;
                    string wpRef = "No Waypoint data";
                    if (wpStrLgth > 0)
                    {
                        //offset += 1;
                        MemoryStream ms = new MemoryStream(data);
                        ms.Position = offset;
                        wpRef = ms.ReadStringLatin1Null(wpStrLgth);
                    }
                    nTaskIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Waypoint ref: {wpRef} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafStr
                    });
                    offset += wpStrLgth;
                }

                int pCount = BitConverter.ToInt32(data, offset); //Plot Item Count
                var PlotItemIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Plot Items: {pCount} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                QuestIDs.Items.Add(PlotItemIDs);

                for (int p = 0; p < pCount; p++) //PLOT ITEM
                {
                    //Add count starting node
                    var nPlotItemIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Plot Item: {p} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    };
                    PlotItemIDs.Items.Add(nPlotItemIDs);

                    int iPlotInstVersion = BitConverter.ToInt32(data, offset);  //Plot Item Instance Version
                    nPlotItemIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Plot item Instance Version: {iPlotInstVersion} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int pTitle = BitConverter.ToInt32(data, offset); //Plot item Name
                    string pitlkLookup = TLKManagerWPF.GlobalFindStrRefbyID(pTitle, game, CurrentLoadedExport.FileRef);
                    nPlotItemIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Goal Name StrRef: {pTitle} {pitlkLookup}",
                        Offset = offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    int pIcon = BitConverter.ToInt32(data, offset); //Icon Index
                    nPlotItemIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Icon Index: {pIcon} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int pConditional = BitConverter.ToInt32(data, offset); //Conditional
                    nPlotItemIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Conditional: {pConditional} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int pState = BitConverter.ToInt32(data, offset); //Int
                    nPlotItemIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Integer State: {pState} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int pTarget = BitConverter.ToInt32(data, offset); //Target Index
                    nPlotItemIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Item Count Target: {pTarget} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;
                }
            }

            int bsCount = BitConverter.ToInt32(data, offset);
            var bsNode = new BinInterpNode
            {
                Header = $"0x{offset:X4} Bool Journal Events: {bsCount}",
                Offset = offset,
                Tag = NodeType.StructLeafInt
            };
            offset += 4;
            subnodes.Add(bsNode);

            for (int b = 0; b < bsCount; b++)
            {
                int iBoolEvtID = BitConverter.ToInt32(data, offset);  //BOOL STATE ID
                var BoolEvtIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Bool Journal Event: {iBoolEvtID} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                bsNode.Items.Add(BoolEvtIDs);

                int bsInstVersion = BitConverter.ToInt32(data, offset); //Instance Version
                var BoolQuestIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Instance Version: {bsInstVersion} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                BoolEvtIDs.Items.Add(BoolQuestIDs);

                int bqstCount = BitConverter.ToInt32(data, offset); //Related Quests Count
                var bqstNode = new BinInterpNode
                {
                    Header = $"0x{offset:X4} Related Quests: {bqstCount}",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                BoolQuestIDs.Items.Add(bqstNode);

                for (int bq = 0; bq < bqstCount; bq++) //Related Quests
                {
                    offset += 16;
                    int bqQuest = BitConverter.ToInt32(data, offset);  //Bool quest ID
                    var bquestIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Related Quest: {bqQuest} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset -= 16;
                    bqstNode.Items.Add(bquestIDs);

                    int bqInstVersion = BitConverter.ToInt32(data, offset);  //Bool quest Instance Version
                    bquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Instance Version: {bqInstVersion} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int bqTask = BitConverter.ToInt32(data, offset);  //Bool quest Instance Version
                    bquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Related Task Link: {bqTask} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int bqConditional = BitConverter.ToInt32(data, offset);  //Bool quest Conditional
                    bquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Conditional: {bqConditional} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int bqState = BitConverter.ToInt32(data, offset);  //Bool quest State
                    bquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Bool State: {bqState} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    bqQuest = BitConverter.ToInt32(data, offset);  //Bool quest ID
                    bquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Quest Link: {bqQuest} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;
                }
            }

            int isCount = BitConverter.ToInt32(data, offset);
            var isNode = new BinInterpNode
            {
                Header = $"0x{offset:X4} Int Journal Events: {isCount}",
                Offset = offset,
                Tag = NodeType.StructLeafInt
            };
            offset += 4;
            subnodes.Add(isNode);

            for (int iEvt = 0; iEvt < isCount; iEvt++)  //INTEGER STATE EVENTS
            {
                int iInttEvtID = BitConverter.ToInt32(data, offset);
                var IntEvtIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Int Journal Event: {iInttEvtID} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                isNode.Items.Add(IntEvtIDs);

                int isInstVersion = BitConverter.ToInt32(data, offset); //Instance Version
                var IntQuestIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Instance Version: {isInstVersion} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                IntEvtIDs.Items.Add(IntQuestIDs);

                int iqstCount = BitConverter.ToInt32(data, offset); //Related Quests Count
                var iqstNode = new BinInterpNode
                {
                    Header = $"0x{offset:X4} Related Quests: {iqstCount}",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                IntQuestIDs.Items.Add(iqstNode);

                for (int iq = 0; iq < iqstCount; iq++) //Related Quests
                {
                    offset += 16;
                    int iqQuest = BitConverter.ToInt32(data, offset);  //int quest ID
                    var iquestIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Related Quest: {iqQuest} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset -= 16;
                    iqstNode.Items.Add(iquestIDs);

                    int iqInstVersion = BitConverter.ToInt32(data, offset);  //Int quest Instance Version
                    iquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Instance Version: {iqInstVersion} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int iqTask = BitConverter.ToInt32(data, offset);  //Int quest Instance Version
                    iquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Related Task Link: {iqTask} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int iqConditional = BitConverter.ToInt32(data, offset);  //Int quest Conditional
                    iquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Conditional: {iqConditional} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int iqState = BitConverter.ToInt32(data, offset);  //Int quest State
                    iquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Bool State: {iqState} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    iqQuest = BitConverter.ToInt32(data, offset);  //Int quest ID
                    iquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Quest Link: {iqQuest} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;
                }
            }

            int fsCount = BitConverter.ToInt32(data, offset);
            var fsNode = new BinInterpNode
            {
                Header = $"0x{offset:X4} Float Journal Events: {fsCount}",
                Offset = offset,
                Tag = NodeType.StructLeafInt
            };
            offset += 4;
            subnodes.Add(fsNode);

            for (int f = 0; f < fsCount; f++)  //FLOAT STATE EVENTS
            {
                int iFloatEvtID = BitConverter.ToInt32(data, offset);  //FLOAT STATE ID
                var FloatEvtIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Float Journal Event: {iFloatEvtID} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                fsNode.Items.Add(FloatEvtIDs);

                int fsInstVersion = BitConverter.ToInt32(data, offset); //Instance Version
                var FloatQuestIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Instance Version: {fsInstVersion} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                FloatEvtIDs.Items.Add(FloatQuestIDs);

                int fqstCount = BitConverter.ToInt32(data, offset); //Related Quests Count
                var fqstNode = new BinInterpNode
                {
                    Header = $"0x{offset:X4} Related Quests: {fqstCount}",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                FloatQuestIDs.Items.Add(fqstNode);

                for (int fq = 0; fq < fqstCount; fq++) //Related Quests
                {
                    offset += 16;
                    int fqQuest = BitConverter.ToInt32(data, offset);  //float quest ID
                    var fquestIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Related Quest: {fqQuest} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset -= 16;
                    fqstNode.Items.Add(fquestIDs);

                    int fqInstVersion = BitConverter.ToInt32(data, offset);  //float quest Instance Version
                    fquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Instance Version: {fqInstVersion} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int fqTask = BitConverter.ToInt32(data, offset);  //Float quest Instance Version
                    fquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Related Task Link: {fqTask} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int fqConditional = BitConverter.ToInt32(data, offset);  //Float quest Conditional
                    fquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Conditional: {fqConditional} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int fqState = BitConverter.ToInt32(data, offset);  //Float quest State
                    fquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Bool State: {fqState} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    fqQuest = BitConverter.ToInt32(data, offset);  //Float quest ID
                    fquestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Quest Link: {fqQuest} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;
                }
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartBioCodexMapScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        var game = CurrentLoadedExport.FileRef.Game;
        try
        {
            int offset = binarystart;

            int sCount = BitConverter.ToInt32(data, offset);
            var SectionsNode = new BinInterpNode
            {
                Header = $"0x{offset:X4} Codex Section Count: {sCount}",
                Offset = offset,
                Tag = NodeType.StructLeafInt
            };
            offset += 4;
            subnodes.Add(SectionsNode);

            for (int i = 0; i < sCount; i++)
            {
                int iSectionID = BitConverter.ToInt32(data, offset);  //Section ID
                var SectionIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Section ID: {iSectionID} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                SectionsNode.Items.Add(SectionIDs);

                int instVersion = BitConverter.ToInt32(data, offset); //Instance Version
                SectionIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Instance Version: {instVersion} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int sTitle = BitConverter.ToInt32(data, offset); //Codex Title
                string ttlkLookup = TLKManagerWPF.GlobalFindStrRefbyID(sTitle, game, CurrentLoadedExport.FileRef);
                SectionIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Section Title StrRef: {sTitle} {ttlkLookup}",
                    Offset = offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int sDescription = BitConverter.ToInt32(data, offset); //Codex Description
                string dtlkLookup = TLKManagerWPF.GlobalFindStrRefbyID(sDescription, game, CurrentLoadedExport.FileRef);
                SectionIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Section Description StrRef: {sDescription} {dtlkLookup}",
                    Offset = offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int sTexture = BitConverter.ToInt32(data, offset); //Texture ID
                SectionIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Section Texture ID: {sTexture} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int sPriority = BitConverter.ToInt32(data, offset); //Priority
                SectionIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Section Priority: {sPriority}  (5 is low, 1 is high)",
                    Offset = offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                if (instVersion >= 3)
                {
                    int sndExport = BitConverter.ToInt32(data, offset);
                    SectionIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X8} Codex Sound: {sndExport} {CurrentLoadedExport.FileRef.GetEntryString(sndExport)}",
                        Offset = offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;
                }

                int sPrimary = BitConverter.ToInt32(data, offset); //Primary Codex
                bool bPrimary = false;
                if (sPrimary == 1) { bPrimary = true; }
                SectionIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Is Primary Codex: {sPrimary}  {bPrimary}",
                    Offset = offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;
            }
            //START OF CODEX PAGES SECTION
            int pCount = BitConverter.ToInt32(data, offset);
            var PagesNode = new BinInterpNode
            {
                Header = $"0x{offset:X4} Codex Page Count: {pCount}",
                Offset = offset,
                Tag = NodeType.StructLeafInt
            };
            offset += 4;
            subnodes.Add(PagesNode);

            for (int i = 0; i < pCount; i++)
            {
                int iPageID = BitConverter.ToInt32(data, offset);  //Page ID
                var PageIDs = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Page Bool: {iPageID} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                PagesNode.Items.Add(PageIDs);

                int instVersion = BitConverter.ToInt32(data, offset); //Instance Version
                PageIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Instance Version: {instVersion} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int pTitle = BitConverter.ToInt32(data, offset); //Codex Title
                string ttlkLookup = TLKManagerWPF.GlobalFindStrRefbyID(pTitle, game, CurrentLoadedExport.FileRef);
                PageIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Page Title StrRef: {pTitle} {ttlkLookup}",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int pDescription = BitConverter.ToInt32(data, offset); //Codex Description
                string dtlkLookup = TLKManagerWPF.GlobalFindStrRefbyID(pDescription, game, CurrentLoadedExport.FileRef);
                PageIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Page Description StrRef: {pDescription} {dtlkLookup}",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int pTexture = BitConverter.ToInt32(data, offset); //Texture ID
                PageIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Section Texture ID: {pTexture} ",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int pPriority = BitConverter.ToInt32(data, offset); //Priority
                PageIDs.Items.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Section Priority: {pPriority}  (5 is low, 1 is high)",
                    Offset = offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                if (instVersion == 4) //ME3 use object reference found sound then section
                {
                    int sndExport = BitConverter.ToInt32(data, offset);
                    PageIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X8} Codex Sound: {sndExport} {CurrentLoadedExport.FileRef.GetEntryString(sndExport)}",
                        Offset = offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    int pSection = BitConverter.ToInt32(data, offset); //Section ID
                    PageIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Section Reference: {pSection} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;
                }
                else if (instVersion == 3 && Pcc.Game != MEGame.LE1) //ME2 use Section then no sound reference //LE1 uses something else...
                {
                    int pSection = BitConverter.ToInt32(data, offset); //Section ID
                    PageIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Section Reference: {pSection} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;
                }
                else if (instVersion == 3 && Pcc.Game == MEGame.LE1)
                {
                    int unkSection = BitConverter.ToInt32(data, offset); //Unknown ID
                    PageIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Unknown Int: {unkSection} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int pSection = BitConverter.ToInt32(data, offset); //Section ID
                    PageIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Section Reference: {pSection} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int sndStrLgth = BitConverter.ToInt32(data, offset); //String length for sound
                    string sndRef = "No sound data";
                    if (sndStrLgth > 0)
                    {
                        MemoryStream ms = new MemoryStream(data);
                        ms.Position = offset + 4;
                        sndRef = ms.ReadStringLatin1Null(sndStrLgth);
                    }
                    PageIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} SoundRef String: {sndRef} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;
                    offset += sndStrLgth;
                }
                else  //ME1 has different order (section ID then codex sound) and uses a string reference.
                {
                    int pSection = BitConverter.ToInt32(data, offset); //Section ID
                    PageIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Section Reference: {pSection} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int sndStrLgth = BitConverter.ToInt32(data, offset); //String length for sound
                    offset += 4;
                    string sndRef = "No sound data";
                    if (sndStrLgth > 0)
                    {
                        MemoryStream ms = new MemoryStream(data);
                        ms.Position = offset;
                        sndRef = ms.ReadStringLatin1Null(sndStrLgth);
                    }
                    PageIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} SoundRef String: {sndRef} ",
                        Offset = offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += sndStrLgth;
                }
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartBioGestureRuntimeDataScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);
            subnodes.Add(MakeArrayNode(bin, "m_mapAnimSetOwners", i => new BinInterpNode(bin.Position, $"{bin.ReadNameReference(Pcc)} => {bin.ReadNameReference(Pcc)}")
            {
                Length = 16
            }));

            int count;
            if (CurrentLoadedExport.Game.IsGame1())
            {
                var propDataNode = new BinInterpNode(bin.Position, $"m_mapCharTypeOverrides ({count = bin.ReadInt32()} items)");
                subnodes.Add(propDataNode);
                for (int i = 0; i < count; i++)
                {
                    propDataNode.Items.Add(new BinInterpNode(bin.Position, $"{i}: {bin.ReadNameReference(Pcc)}", NodeType.StructLeafName)
                    {
                        Length = 8,
                        IsExpanded = true,
                        Items =
                        {
                            MakeNameNode(bin, "nm_Female", Pcc),
                            MakeNameNode(bin, "nm_Asari", Pcc),
                            MakeNameNode(bin, "nm_Turian", Pcc),
                            MakeNameNode(bin, "nm_Salarian", Pcc),
                            MakeNameNode(bin, "nm_Quarian", Pcc),
                            MakeNameNode(bin, "nm_Other", Pcc),
                            MakeNameNode(bin, "nm_Krogan", Pcc),
                            MakeNameNode(bin, "nm_Geth", Pcc),
                            MakeNameNode(bin, "nm_Other_Artificial", Pcc)
                        }
                    });
                }
            }
            else
            {
                var propDataNode = new BinInterpNode(bin.Position, $"m_mapMeshProps ({count = bin.ReadInt32()} items)");
                subnodes.Add(propDataNode);
                for (int i = 0; i < count; i++)
                {
                    BinInterpNode node = new BinInterpNode(bin.Position, $"{i}: {bin.ReadNameReference(Pcc)}", NodeType.StructLeafName)
                    {
                        Length = 8
                    };
                    propDataNode.Items.Add(node);
                    node.Items.Add(MakeNameNode(bin, "nmPropName", Pcc));
                    node.Items.Add(MakeStringNode(bin, "sMesh", Pcc.Game));
                    node.Items.Add(MakeNameNode(bin, "nmAttachTo", Pcc));
                    node.Items.Add(MakeVectorNode(bin, "vOffsetLocation"));
                    node.Items.Add(MakeRotatorNode(bin, "rOffsetRotation"));
                    node.Items.Add(MakeVectorNode(bin, "vOffsetScale"));
                    int count2;
                    var propActionsNode = new BinInterpNode(bin.Position, $"mapActions ({count2 = bin.ReadInt32()} items)")
                    {
                        IsExpanded = true
                    };
                    node.Items.Add(propActionsNode);
                    for (int j = 0; j < count2; j++)
                    {
                        BinInterpNode node2 = new BinInterpNode(bin.Position, $"{j}: {bin.ReadNameReference(Pcc)}", NodeType.StructLeafName)
                        {
                            Length = 8
                        };
                        propActionsNode.Items.Add(node2);
                        node2.Items.Add(MakeNameNode(bin, "nmActionName", Pcc));
                        if (CurrentLoadedExport.Game.IsGame2())
                        {
                            node2.Items.Add(MakeStringNode(bin, "sEffect", Pcc.Game));
                        }

                        node2.Items.Add(MakeBoolIntNode(bin, "bActivate"));
                        node2.Items.Add(MakeNameNode(bin, "nmAttachTo", Pcc));
                        node2.Items.Add(MakeVectorNode(bin, "vOffsetLocation"));
                        node2.Items.Add(MakeRotatorNode(bin, "rOffsetRotation"));
                        node2.Items.Add(MakeVectorNode(bin, "vOffsetScale"));
                        if (CurrentLoadedExport.Game.IsGame3())
                        {
                            node2.Items.Add(MakeStringNode(bin, "sParticleSys", Pcc.Game));
                            node2.Items.Add(MakeStringNode(bin, "sClientEffect", Pcc.Game));
                            node2.Items.Add(MakeBoolIntNode(bin, "bCooldown"));
                            node2.Items.Add(new BinInterpNode(bin.Position, "tSpawnParams")
                            {
                                Length = 0x38,
                                IsExpanded = true,
                                Items =
                                {
                                    MakeVectorNode(bin, "vHitLocation"),
                                    MakeVectorNode(bin, "vHitNormal"),
                                    MakeNameNode(bin, "nmHitBone", Pcc),
                                    MakeVectorNode(bin, "vRayDir"),
                                    MakeVectorNode(bin, "vSpawnValue")
                                }
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> Scan_Bio2DA(byte[] data)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(data) { Endian = Pcc.Endian };
            bin.JumpTo(CurrentLoadedExport.propsEnd());

            bool isIndexed = !bin.ReadBoolInt();
            bin.Skip(-4);
            if (isIndexed)
            {
                subnodes.Add(MakeUInt32Node(bin, "Zero"));
            }

            int cellCount;
            subnodes.Add(new BinInterpNode(bin.Position, $"Populated Cell Count: {cellCount = bin.ReadInt32()}", NodeType.StructLeafInt) { Length = 4 });

            for (int i = 0; i < cellCount; i++)
            {
                Bio2DACell.Bio2DADataType type;
                subnodes.Add(new BinInterpNode(bin.Position, $"Cell {(isIndexed ? bin.ReadInt32() : i)}", NodeType.StructLeafInt)
                {
                    Items =
                    {
                        new BinInterpNode(bin.Position, $"Type: {type = (Bio2DACell.Bio2DADataType)bin.ReadByte()}") { Length = 1 },
                        type switch
                        {
                            Bio2DACell.Bio2DADataType.TYPE_INT => MakeInt32Node(bin, "Value"),
                            Bio2DACell.Bio2DADataType.TYPE_NAME => MakeNameNode(bin, "Value", Pcc),
                            Bio2DACell.Bio2DADataType.TYPE_FLOAT => MakeFloatNode(bin, "Value"),
                            Bio2DACell.Bio2DADataType.TYPE_NULL => new BinInterpNode("Value: NULL"),
                            _ => throw new ArgumentOutOfRangeException()
                        }
                    }
                });
            }

            if (!isIndexed)
            {
                subnodes.Add(MakeUInt32Node(bin, "Zero"));
            }

            int columnCount;
            subnodes.Add(new BinInterpNode(bin.Position, $"Column Count: {columnCount = bin.ReadInt32()}", NodeType.StructLeafInt) { Length = 4 });

            for (int i = 0; i < columnCount; i++)
            {
                subnodes.Add(new BinInterpNode(bin.Position, $"Name: {bin.ReadNameReference(Pcc)}, Index: {bin.ReadInt32()}", NodeType.StructLeafInt) { Length = 12 });
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartBioSquadCombatScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();

        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);
            subnodes.Add(MakeArrayNode(bin, "Count", i =>
            {
                string entry = null;
                if (Pcc.Game.IsLEGame())
                {
                    entry = Pcc.GetEntryString(bin.ReadInt32());
                }

                var guid = bin.ReadGuid();
                int num = bin.ReadInt32();

                return new BinInterpNode(bin.Position, $"{guid}: {num} {entry}");
            }));
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartBioDynamicAnimSetScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();

        try
        {
            int binarypos = binarystart;
            int count = EndianReader.ToInt32(data, binarypos, CurrentLoadedExport.FileRef.Endian);
            subnodes.Add(new BinInterpNode
            {
                Header = $"0x{binarypos:X4} Count: {count}"
            });
            binarypos += 4; //+ int
            for (int i = 0; i < count; i++)
            {
                int nameIndex = EndianReader.ToInt32(data, binarypos, CurrentLoadedExport.FileRef.Endian);
                int nameIndexNum = EndianReader.ToInt32(data, binarypos + 4, CurrentLoadedExport.FileRef.Endian);
                int shouldBe1 = EndianReader.ToInt32(data, binarypos + 8, CurrentLoadedExport.FileRef.Endian);

                var name = CurrentLoadedExport.FileRef.GetNameEntry(nameIndex);
                string nodeValue = $"{new NameReference(name, nameIndexNum).Instanced}";
                if (shouldBe1 != 1)
                {
                    //ERROR
                    nodeValue += " - Not followed by 1 (integer)!";
                }

                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{binarypos:X4} Name: {nodeValue}",
                    Tag = NodeType.StructLeafName,
                    Offset = binarypos,
                });
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{(binarypos + 8):X4} Unknown 1: {shouldBe1}",
                    Tag = NodeType.StructLeafInt,
                    Offset = (binarypos + 8),
                });
                binarypos += 12;
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartBioStageScan(byte[] data, ref int binarystart)
    {
        /*
             * Length (int)
                Name: m_aCameraList
                int unknown 0
                Count + int unknown
                [Camera name
                    unreal property data]*/
        var subnodes = new List<ITreeItem>();
        //if ((CurrentLoadedExport.Header[0x1f] & 0x2) != 0)
        {
            int pos = binarystart;
            if (data.Length > binarystart)
            {
                int length = BitConverter.ToInt32(data, binarystart);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"{binarystart:X4} Length: {length}",
                    Offset = pos
                });
                pos += 4;
                if (length != 0)
                {
                    int nameindex = BitConverter.ToInt32(data, pos);
                    int num = BitConverter.ToInt32(data, pos + 4);

                    var name = new NameReference(CurrentLoadedExport.FileRef.GetNameEntry(nameindex), num);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"{(pos - binarystart):X4} Array Name: {name.Instanced}",
                        Offset = pos,
                        Tag = NodeType.StructLeafName
                    });

                    pos += 8;
                    int shouldbezero = BitConverter.ToInt32(data, pos);
                    if (shouldbezero != 0)
                    {
                        Debug.WriteLine($"NOT ZERO FOUND: {pos}");
                    }
                    pos += 4;

                    int count = BitConverter.ToInt32(data, pos);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"{(pos - binarystart):X4} Count: {count}",
                        Offset = pos
                    });
                    pos += 4;

                    shouldbezero = BitConverter.ToInt32(data, pos);
                    if (shouldbezero != 0)
                    {
                        Debug.WriteLine($"NOT ZERO FOUND: {pos}");
                    }
                    pos += 4;
                    try
                    {
                        var stream = new MemoryStream(data);
                        for (int i = 0; i < count; i++)
                        {
                            nameindex = BitConverter.ToInt32(data, pos);
                            num = BitConverter.ToInt32(data, pos + 4);
                            BinInterpNode parentnode = new BinInterpNode
                            {
                                Header = $"{(pos - binarystart):X4} Camera {i + 1}: {new NameReference(CurrentLoadedExport.FileRef.GetNameEntry(nameindex), num).Instanced}",
                                Tag = NodeType.StructLeafName,
                                Offset = pos
                            };
                            subnodes.Add(parentnode);
                            pos += 8;
                            stream.Seek(pos, SeekOrigin.Begin);
                            var props = PropertyCollection.ReadProps(CurrentLoadedExport, stream, "BioStageCamera", includeNoneProperty: true);

                            UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                            foreach (Property prop in props)
                            {
                                InterpreterExportLoader.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
                            }
                            subnodes.AddRange(topLevelTree.ChildrenProperties);

                            //finish writing function here
                            pos = (int)stream.Position;
                        }
                    }
                    catch (Exception ex)
                    {
                        subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
                    }
                }
            }
        }
        return subnodes;
    }

    private List<ITreeItem> StartBioGestureRulesDataScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();

        if (binarystart >= data.Length)
        {
            return subnodes;
        }

        int pos = binarystart;
        var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
        bin.JumpTo(binarystart);
        try
        {
            var count = bin.ReadInt32();
            bin.Position -= 4; // Set back so the node can be made
            subnodes.Add(MakeInt32Node(bin, "Count"));

            for (int i = 0; i < count; i++)
            {
                var node = new BinInterpNode(bin.Position, $"Rule {i}");
                subnodes.Add(node);

                node.Items.Add(MakeNameNode(bin, "Name", Pcc));

                var subcount = bin.ReadInt32();
                var subnode = new BinInterpNode(bin.Position - 4, $"Num somethings: {subcount}");
                node.Items.Add(subnode);

                for (int j = 0; j < subcount; j++)
                {
                    // Read name, some integer
                    subnode.Items.Add(MakeNameNode(bin, "SomeName", Pcc));
                    subnode.Items.Add(MakeInt32Node(bin, "SomeNum"));
                }
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private List<ITreeItem> StartBioMorphFaceScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            subnodes.Add(MakeArrayNode(bin, "LOD Count:", k => new BinInterpNode(bin.Position, $"{k}")
            {
                Items =
                {
                    MakeInt32Node(bin, "Size of Vector"),
                    MakeArrayNode(bin, $"LOD {k} Vertex Positional Data", n => new BinInterpNode(bin.Position, $"{n}")
                    {
                        Items =
                        {
                            MakeVectorNode(bin, "Position"),
                        }
                    }),
                }
            }, true));
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }
}