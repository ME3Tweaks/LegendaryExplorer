using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public partial class BinaryInterpreterWPF
{
    /// <summary>
    /// Reads the common header for FaceFX archives.
    /// </summary>
    private (int version, MEGame game) ReadFaceFXHeader(EndianReader bin, List<ITreeItem> subnodes)
    {
        var archiveSize = bin.ReadInt32();
        subnodes.Add(new BinInterpNode(bin.Position - 4, $"Archive size: {archiveSize} ({FileSize.FormatSize(archiveSize)})"));
        subnodes.Add(new BinInterpNode(bin.Position, $"Magic: {bin.ReadInt32():X8}") { Length = 4 });
        int versionID = bin.ReadInt32(); //1710 = ME1, 1610 = ME2, 1731 = ME3.
        var game = versionID == 1710 ? MEGame.ME1 :
            versionID == 1610 ? MEGame.ME2 :
            versionID == 1731 ? MEGame.ME3 :
            MEGame.Unknown;
        var vIdStr = versionID.ToString();
        var vers = new Version(vIdStr[0] - '0', vIdStr[1] - '0', vIdStr[2] - '0', vIdStr[3] - '0'); //Mega hack
        subnodes.Add(new BinInterpNode(bin.Position - 4, $"SDK Version: {versionID} ({vers})") { Length = 4 });
        if (versionID == 1731)
        {
            subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
        }

        subnodes.Add(new BinInterpNode(bin.Position, $"Licensee: {bin.ReadFaceFXString(game)}"));
        subnodes.Add(new BinInterpNode(bin.Position, $"Project: {bin.ReadFaceFXString(game)}"));

        var licenseeVersion = bin.ReadInt32();
        vIdStr = licenseeVersion.ToString();
        vers = new Version(vIdStr[0] - '0', vIdStr[1] - '0', vIdStr[2] - '0', vIdStr[3] - '0'); //Mega hack
        subnodes.Add(new BinInterpNode(bin.Position - 4, $"Licensee version: {vIdStr} ({vers})") { Length = 4 });

        return (versionID, game);
    }

    private List<ITreeItem> StartFaceFXAnimSetScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);
            ReadFaceFXHeader(bin, subnodes);

            if (Pcc.Game == MEGame.ME2)
            {
                subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
            }
            else
            {
                subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
            }

            if (Pcc.Game != MEGame.ME2)
            {
                int hNodeCount = bin.ReadInt32();
                var hNodes = new List<ITreeItem>();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Nodes: {hNodeCount} items")
                {
                    Items = hNodes
                });
                for (int i = 0; i < hNodeCount; i++)
                {
                    var hNodeNodes = new List<ITreeItem>();
                    hNodes.Add(new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items = hNodeNodes
                    });
                    hNodeNodes.Add(MakeInt32Node(bin, "Unknown"));
                    var nNameCount = bin.ReadInt32();
                    hNodeNodes.Add(new BinInterpNode(bin.Position, $"Name Count: {nNameCount}") { Length = 4 });
                    for (int n = 0; n < nNameCount; n++)
                    {
                        hNodeNodes.Add(new BinInterpNode(bin.Position, $"Name: {bin.BaseStream.ReadStringLatin1(bin.ReadInt32())}"));
                        hNodeNodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                }
            }

            int nameCount = bin.ReadInt32();
            subnodes.Add(new BinInterpNode(bin.Position - 4, $"Names: {nameCount} items")
            {
                //ME2 different to ME3/1
                Items = ReadList(nameCount, i => new BinInterpNode(bin.Skip(Pcc.Game != MEGame.ME2 ? 0 : 4).Position, $"{i}: {bin.BaseStream.ReadStringLatin1(bin.ReadInt32())}"))
            });

            subnodes.Add(MakeInt32Node(bin, "Unknown"));
            subnodes.Add(MakeInt32Node(bin, "Unknown"));
            subnodes.Add(MakeInt32Node(bin, "Unknown"));
            subnodes.Add(MakeInt32Node(bin, "Unknown"));
            if (Pcc.Game == MEGame.ME2)
            {
                subnodes.Add(MakeInt32Node(bin, "Unknown"));
                subnodes.Add(MakeInt32Node(bin, "Unknown"));
                subnodes.Add(MakeInt32Node(bin, "Unknown"));
                subnodes.Add(MakeInt32Node(bin, "Unknown"));
                subnodes.Add(MakeInt32Node(bin, "Unknown"));
            }

            int lineCount = bin.ReadInt32();
            var lines = new List<ITreeItem>();

            subnodes.Add(new BinInterpNode(bin.Position - 4, $"FaceFXLines: {lineCount} items")
            {
                Items = lines
            });
            for (int i = 0; i < lineCount; i++)
            {
                var nodes = new List<ITreeItem>();
                lines.Add(new BinInterpNode(bin.Position, $"{i}")
                {
                    Items = nodes
                });
                if (Pcc.Game == MEGame.ME2)
                {
                    nodes.Add(MakeInt32Node(bin, "Unknown"));
                    nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }
                nodes.Add(MakeInt32Node(bin, "Name"));
                if (Pcc.Game == MEGame.ME2)
                {
                    nodes.Add(MakeInt32Node(bin, "Unknown"));
                }
                int animationCount = bin.ReadInt32();
                var anims = new List<ITreeItem>();
                nodes.Add(new BinInterpNode(bin.Position - 4, $"Animations: {animationCount} items")
                {
                    Items = anims
                });
                for (int j = 0; j < animationCount; j++)
                {
                    var animNodes = new List<ITreeItem>();
                    anims.Add(new BinInterpNode(bin.Position, $"{j}")
                    {
                        Items = animNodes
                    });
                    if (Pcc.Game == MEGame.ME2)
                    {
                        animNodes.Add(MakeInt32Node(bin, "Unknown"));
                        animNodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                    animNodes.Add(MakeInt32Node(bin, "Name"));
                    animNodes.Add(MakeInt32Node(bin, "Unknown"));
                    if (Pcc.Game == MEGame.ME2)
                    {
                        animNodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                }

                int pointsCount = bin.ReadInt32();
                nodes.Add(new BinInterpNode(bin.Position - 4, $"Points: {pointsCount} items")
                {
                    Items = ReadList(pointsCount, j => new BinInterpNode(bin.Position, $"{j}")
                    {
                        Items = new List<ITreeItem>
                        {
                            new BinInterpNode(bin.Position, $"Time: {bin.ReadFloat()}") {Length = 4},
                            new BinInterpNode(bin.Position, $"Weight: {bin.ReadFloat()}") {Length = 4},
                            new BinInterpNode(bin.Position, $"InTangent: {bin.ReadFloat()}") {Length = 4},
                            new BinInterpNode(bin.Position, $"LeaveTangent: {bin.ReadFloat()}") {Length = 4}
                        }
                    })
                });

                if (pointsCount > 0)
                {
                    if (Pcc.Game == MEGame.ME2)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                    nodes.Add(new BinInterpNode(bin.Position, $"NumKeys: {bin.ReadInt32()} items")
                    {
                        Items = ReadList(bin.Skip(-4).ReadInt32(), j => new BinInterpNode(bin.Position, $"{bin.ReadInt32()} keys"))
                    });
                }
                nodes.Add(new BinInterpNode(bin.Position, $"Fade In Time: {bin.ReadFloat()}") { Length = 4 });
                nodes.Add(new BinInterpNode(bin.Position, $"Fade Out Time: {bin.ReadFloat()}") { Length = 4 });
                nodes.Add(MakeInt32Node(bin, "Unknown"));
                if (Pcc.Game == MEGame.ME2)
                {
                    nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }
                nodes.Add(new BinInterpNode(bin.Position, $"Path: {bin.BaseStream.ReadStringLatin1(bin.ReadInt32())}"));
                if (Pcc.Game == MEGame.ME2)
                {
                    nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }
                nodes.Add(new BinInterpNode(bin.Position, $"ID: {bin.BaseStream.ReadStringLatin1(bin.ReadInt32())}"));
                nodes.Add(MakeInt32Node(bin, "index"));
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }

    private List<ITreeItem> StartFaceFXAssetScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);
            var (version, game) = ReadFaceFXHeader(bin, subnodes);

            if (game == MEGame.ME2)
            {
                subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
            }
            else
            {
                subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
            }
            //Node Table
            if (game != MEGame.ME2)
            {
                int hNodeCount = bin.ReadInt32();
                var hNodes = new List<ITreeItem>();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Nodes: {hNodeCount} items")
                {
                    Items = hNodes
                });
                for (int i = 0; i < hNodeCount; i++)
                {
                    var hNodeNodes = new List<ITreeItem>();
                    hNodes.Add(new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items = hNodeNodes
                    });
                    hNodeNodes.Add(MakeInt32Node(bin, "Unknown"));
                    var nNameCount = bin.ReadInt32();
                    hNodeNodes.Add(new BinInterpNode(bin.Position, $"Name Count: {nNameCount}") { Length = 4 });
                    for (int n = 0; n < nNameCount; n++)
                    {
                        hNodeNodes.Add(new BinInterpNode(bin.Position, $"Name: {bin.BaseStream.ReadStringLatin1(bin.ReadInt32())}"));
                        hNodeNodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                }
            }

            //Name Table

            var nameTable = new List<string>();
            int nameCount = bin.ReadInt32();
            var nametablePos = bin.Position - 4;
            var nametabObj = new List<ITreeItem>();

            // Does this need byte aligned? ME2 seems like it does...
            //if (game == MEGame.ME2)
            //{
            //    bin.ReadByte(); // Align to bytes?
            //}

            for (int m = 0; m < nameCount; m++)
            {
                var pos = bin.Position;
                var mName = bin.ReadFaceFXString(game, true);
                nameTable.Add(mName);
                nametabObj.Add(new BinInterpNode(pos, $"{m}: {mName}"));
                //if (game != MEGame.ME2)
                //{
                //    bin.Skip(4);
                //}
            }

            subnodes.Add(new BinInterpNode(nametablePos, $"Names: {nameCount} items")
            {
                //ME1 and ME3 same, ME2 different
                Items = nametabObj
            });

            subnodes.Add(MakeInt32Node(bin, "Unknown"));
            subnodes.Add(MakeInt32Node(bin, "Unknown"));

            if (game == MEGame.ME2)
            {
                subnodes.Add(MakeInt32Node(bin, "Unknown"));
                subnodes.Add(MakeInt16Node(bin, "Unknown"));
                subnodes.Add(MakeInt16Node(bin, "Unknown"));
                subnodes.Add(MakeInt16Node(bin, "Unknown"));
            }

            //LIST A - BONES
            var bonesList = new List<ITreeItem>();
            var bonesCount = bin.ReadInt32();
            if (game == MEGame.ME2)
            {
                subnodes.Add(MakeInt32Node(bin, "Unknown"));
                subnodes.Add(MakeInt16Node(bin, "Unknown"));
            }
            subnodes.Add(new BinInterpNode(((game == MEGame.ME2) ? (bin.Position - 10) : (bin.Position - 4)), $"Bone Nodes: {bonesCount} items")
            {
                Items = bonesList
            });

            for (int a = 0; a < bonesCount; a++) //NOT EXACT??
            {
                var boneNode = new List<ITreeItem>();
                bonesList.Add(new BinInterpNode(bin.Position, $"{nameTable[bin.ReadInt32()]}")
                {
                    Items = boneNode
                });
                boneNode.Add(MakeFloatNode(bin, "X"));
                boneNode.Add(MakeFloatNode(bin, "Y"));
                boneNode.Add(MakeFloatNode(bin, "Z"));

                boneNode.Add(MakeFloatNode(bin, "Unknown float"));
                boneNode.Add(MakeFloatNode(bin, "Unknown float"));
                boneNode.Add(MakeFloatNode(bin, "Unknown float"));

                boneNode.Add(MakeFloatNode(bin, "Unknown float"));
                boneNode.Add(MakeFloatNode(bin, "Unknown float"));
                boneNode.Add(MakeFloatNode(bin, "Unknown float"));

                boneNode.Add(MakeFloatNode(bin, "Unknown float"));
                boneNode.Add(MakeFloatNode(bin, "Unknown float"));
                boneNode.Add(MakeFloatNode(bin, "Unknown float"));

                boneNode.Add(MakeFloatNode(bin, "Unknown float"));
                boneNode.Add(MakeFloatNode(bin, "Unknown float"));
                if (game != MEGame.ME2)
                {
                    boneNode.Add(MakeFloatNode(bin, "Unknown float"));
                    boneNode.Add(MakeFloatNode(bin, "Bone weight?"));
                    //while (true)
                    //{
                    //    var item = bin.ReadInt32();
                    //    if (item == 2147483647)
                    //    {
                    //        tableItems.Add(new BinInterpNode(bin.Position - 4, $"End Marker: FF FF FF 7F") { Length = 4 });
                    //        tableItems.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                    //        break;
                    //    }

                    //    bin.Skip(-4);
                    //    tableItems.Add(MakeFloatNode(bin, "Unknown float"));

                    //}
                    //Name list to Bones and other facefx?
                    var unkNameList1 = new List<ITreeItem>();
                    var countUk1 = bin.ReadInt32();
                    boneNode.Add(new BinInterpNode(bin.Position - 4, $"Functions?: {countUk1} items")
                    {
                        Items = unkNameList1
                    });
                    for (int b = 0; b < countUk1; b++)
                    {
                        var unameVal = bin.ReadInt32();
                        var unkNameList1items = new List<ITreeItem>();
                        unkNameList1.Add(new BinInterpNode(bin.Position - 4, $"{b},{unameVal}")
                        {
                            Items = unkNameList1items
                        });
                        unkNameList1items.Add(new BinInterpNode(bin.Position, $"{nameTable[bin.ReadInt32()]}"));
                        unkNameList1items.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        unkNameList1items.Add(MakeInt32Node(bin, "Unknown"));
                        unkNameList1items.Add(MakeInt32Node(bin, "Unknown"));
                        unkNameList1items.Add(MakeInt32Node(bin, "Unknown"));
                    }
                }
            }

            //LIST B - COMBINER NODES
            //FROM HERE ME3 ONLY WIP

            //I have literally no idea how this works in ME2

            var combinerList = new List<ITreeItem>();
            var combinerListNames = new List<string>();
            var combinerCount = bin.ReadInt32();

            if (game == MEGame.ME2)
            {
                subnodes.Add(MakeInt32Node(bin, "Unknown"));
            }

            subnodes.Add(new BinInterpNode(bin.Position - (game == MEGame.ME2 ? 8 : 4), $"Combiner nodes: {combinerCount} items")
            {
                Items = combinerList
            });

            for (int b = 0, i = 0; i < combinerCount; b++, i++)
            {
                var bLocation = bin.Position;
                // There seem to be several types, known types are 0, 4, 6, 8. 
                int formatType;
                if (game == MEGame.ME2)
                {
                    formatType = bin.ReadInt16();
                }
                else formatType = bin.ReadInt32();

                var nameIdx = bin.ReadInt32();

                var combinerNode = new List<ITreeItem>();
                combinerList.Add(new BinInterpNode(bin.Position - 4, $"{b}: {nameTable[nameIdx]} - {(FxNodeType)formatType}")
                {
                    Items = combinerNode
                });
                combinerListNames.Add(nameTable[nameIdx]);

                combinerNode.Add(new BinInterpNode(bin.Position - 8, $"Format: {formatType} - {(FxNodeType)formatType}"));
                combinerNode.Add(new BinInterpNode(bin.Position - 4, $"Table index: {nameIdx}"));
                combinerNode.Add(new BinInterpNode(bin.Position, $"Minimum Value: {bin.ReadSingle()}") { Length = 4 });
                combinerNode.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                combinerNode.Add(new BinInterpNode(bin.Position, $"Maximum Value: {bin.ReadSingle()}") { Length = 4 });
                combinerNode.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                var inputOp = bin.ReadInt32();
                combinerNode.Add(new BinInterpNode(bin.Position - 4, $"Input Operation: {inputOp} - {(FxInputOperation)inputOp}"));

                // Parent links section
                var parentLinks = new List<ITreeItem>(); //Name list to Bones and other facefx phenomes?
                var parentLinksCount = bin.ReadInt32();
                combinerNode.Add(new BinInterpNode(bin.Position - 4, $"Parent Links: {parentLinksCount} items")
                {
                    Items = parentLinks
                });
                for (int n2 = 0; n2 < parentLinksCount; n2++)
                {
                    var combinerIdx = bin.ReadInt32();
                    var linkedNode = combinerIdx < b ? combinerListNames[combinerIdx] : "";
                    var parentLinkItems = new List<ITreeItem>();
                    parentLinks.Add(new BinInterpNode(bin.Position - 4, $"Combiner Idx: {combinerIdx} {linkedNode}")
                    {
                        Items = parentLinkItems
                    });
                    var linkFunction = bin.ReadInt32();
                    parentLinkItems.Add(new BinInterpNode(bin.Position - 4, $"Link Function: {(FxLinkFunction)linkFunction}"));
                    var n3count = bin.ReadInt32();
                    parentLinkItems.Add(new BinInterpNode(bin.Position - 4, $"Parameter Count: {n3count}"));
                    for (int n3 = 0; n3 < n3count; n3++)
                    {
                        parentLinkItems.Add(new BinInterpNode(bin.Position, $"Function Parameter {n3}: {bin.ReadSingle()}") { Length = 4 });
                    }
                }

                // Parameters section
                int parameterCount = bin.ReadInt32();
                var fxaParameter = new List<ITreeItem>(parameterCount);
                combinerNode.Add(new BinInterpNode(bin.Position - 4, $"Parameters: {parameterCount} items")
                {
                    Items = fxaParameter
                });
                for (int fxaIndex = 0; fxaIndex < parameterCount; fxaIndex++)
                {
                    int fxaIdxVal = bin.ReadInt32();
                    var fxaInfoItem = new List<ITreeItem>();
                    fxaParameter.Add(new BinInterpNode(bin.Position - 4, $"{nameTable[fxaIdxVal]} - {fxaIdxVal}")
                    {
                        Items = fxaInfoItem
                    });
                    int parameterFmt = bin.ReadInt32();
                    fxaInfoItem.Add(new BinInterpNode(bin.Position - 4, $"Parameter Name: {nameTable[fxaIdxVal]} ({fxaIdxVal})"));
                    fxaInfoItem.Add(new BinInterpNode(bin.Position - 4, $"Parameter Format: {(FxNodeParamFormat)parameterFmt} ({parameterFmt})") { Length = 4 });
                    // Parameter format - 0 means first int is the param value, 3 means there is a string on the end that is the param value

                    var firstUnkIntName = parameterFmt == 0 ? "Int Value" : "Unknown int";
                    fxaInfoItem.Add(new BinInterpNode(bin.Position, $"{firstUnkIntName}: {bin.ReadInt32()}") { Length = 4 });
                    fxaInfoItem.Add(new BinInterpNode(bin.Position, $"Float value?: {bin.ReadSingle()}") { Length = 4 });
                    fxaInfoItem.Add(new BinInterpNode(bin.Position, $"Unknown int: {bin.ReadInt32()}") { Length = 4 });

                    if (parameterFmt == 3)
                    {
                        var unkStringLength = bin.ReadInt32();
                        fxaInfoItem.Add(new BinInterpNode(bin.Position - 4, $"Parameter Value: {bin.BaseStream.ReadStringLatin1(unkStringLength)}"));
                    }
                }
            }

            // Fix names for bone node functions now that we've parsed combiner table - this is terrible code
            foreach (var bone in bonesList)
            {
                var functions = (bone as BinInterpNode).Items[^1];
                if (functions is BinInterpNode functionNode && functionNode.Header.Contains("Function"))
                {
                    foreach (var funcItem in functionNode.Items)
                    {
                        if (funcItem is BinInterpNode func)
                        {
                            var ints = func.Header.Split(',', ' ').Where(str => Int32.TryParse(str, out _)).Select(str => Convert.ToInt32(str)).ToArray();
                            if (ints.Length != 2) break;
                            func.Header = $"{ints[0]}: Combiner Node {ints[1]} ({combinerListNames[ints[1]]})";
                        }
                    }
                }
            }

            // Unknown Table C First 4 bytes
            // Theory 1: This could refer to a number of "unique" entries, it seems to be a number of entries in the table that have only 1 string reference to the combiner.
            //           Subtracting: (entries in this table that have 2 or more strings) - (total amount of combiner entires) = seems to result in same number that exists in these first 4 bytes.
            byte[] unkHeaderC = bin.ReadBytes(4);
            var unkListC = new List<ITreeItem>();
            subnodes.Add(new BinInterpNode(bin.Position - 4, $"Unknown Table C - Combiner Mapping?")
            {
                Items = unkListC
            });

            for (int c = 0; c < combinerCount; c++)
            {
                // Table begins with an unknown ID - this ID seems to be from some kind of global table as same entries with same names use same IDs across different FaceFX files
                //                                   (not 100% sure as only smallish sample of about 20 files was checked)
                int entryID = bin.ReadInt32();
                // Add tree item with entry ID as an idicator
                var unkListCitems = new List<ITreeItem>();
                unkListC.Add(new BinInterpNode(bin.Position - 4, $"{c}: {entryID}")
                {
                    Items = unkListCitems
                });
                unkListCitems.Add(new BinInterpNode(bin.Position - 4, $"Unknown int: {entryID}") { Length = 4 });
                // String count:
                int stringCount = bin.ReadInt32();
                unkListCitems.Add(new BinInterpNode(bin.Position - 4, $"String count: {stringCount}") { Length = 4 });
                // Combiner entry name:
                int stringLength = bin.ReadInt32();
                string stringText = bin.ReadEndianASCIIString(stringLength);
                unkListCitems.Add(new BinInterpNode(bin.Position - stringLength - 4, $"Combiner String: {stringText}") { Length = stringLength + 4 });
                // Combiner entry ID:
                int name = bin.ReadInt32();
                unkListCitems.Add(new BinInterpNode(bin.Position - 4, $"Combiner ID: {name} {combinerListNames[name]}") { Length = 4 });
                // Do the same for next strings if theres more than one.
                for (int i = 1; i < stringCount; i++)
                {
                    c++;
                    stringLength = bin.ReadInt32();
                    stringText = bin.ReadEndianASCIIString(stringLength);
                    unkListCitems.Add(new BinInterpNode(bin.Position - stringLength - 4, $"Combiner String: {stringText}") { Length = stringLength + 4 });
                    name = bin.ReadInt32();
                    unkListCitems.Add(new BinInterpNode(bin.Position - 4, $"Combiner ID: {name} {combinerListNames[name]}") { Length = 4 });
                }
            }

            if (game == MEGame.ME2)
            {
                subnodes.Add(MakeInt32Node(bin, "Unknown"));
                subnodes.Add(MakeInt32Node(bin, "Unknown"));
                subnodes.Add(MakeInt32Node(bin, "Unknown"));
                subnodes.Add(MakeInt32Node(bin, "Unknown"));
                subnodes.Add(MakeInt32Node(bin, "Unknown"));
            }

            subnodes.Add(new BinInterpNode(bin.Position, $"Name: {nameTable[bin.ReadInt32()]}") { Length = 4 });

            int lineCount;
            var lines = new List<ITreeItem>();
            subnodes.Add(new BinInterpNode(bin.Position, $"FaceFXLines: {lineCount = bin.ReadInt32()}")
            {
                Items = lines
            });
            for (int i = 0; i < lineCount; i++)
            {
                var nodes = new List<ITreeItem>();
                lines.Add(new BinInterpNode(bin.Position, $"{i}")
                {
                    Items = nodes
                });
                nodes.Add(new BinInterpNode(bin.Position, $"Name: {nameTable[bin.ReadInt32()]}") { Length = 4 });
                int animationCount = bin.ReadInt32();
                var anims = new List<ITreeItem>();
                nodes.Add(new BinInterpNode(bin.Position - 4, $"Animations: {animationCount} items")
                {
                    Items = anims
                });
                for (int j = 0; j < animationCount; j++)
                {
                    var animNodes = new List<ITreeItem>();
                    anims.Add(new BinInterpNode(bin.Position, $"{j}")
                    {
                        Items = animNodes
                    });
                    animNodes.Add(new BinInterpNode(bin.Position, $"Name: {nameTable[bin.ReadInt32()]}") { Length = 4 });
                    animNodes.Add(MakeInt32Node(bin, "Unknown"));
                    if (game == MEGame.ME2)
                    {
                        animNodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                }
                int pointsCount = bin.ReadInt32();
                nodes.Add(new BinInterpNode(bin.Position - 4, $"Points: {pointsCount} items")
                {
                    Items = ReadList(pointsCount, j => new BinInterpNode(bin.Position, $"{j}")
                    {
                        Items = new List<ITreeItem>
                        {
                            new BinInterpNode(bin.Position, $"Time: {bin.ReadFloat()}") {Length = 4},
                            new BinInterpNode(bin.Position, $"Weight: {bin.ReadFloat()}") {Length = 4},
                            new BinInterpNode(bin.Position, $"InTangent: {bin.ReadFloat()}") {Length = 4},
                            new BinInterpNode(bin.Position, $"LeaveTangent: {bin.ReadFloat()}") {Length = 4}
                        }
                    })
                });
                if (pointsCount > 0)
                {
                    if (game == MEGame.ME2)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }

                    nodes.Add(new BinInterpNode(bin.Position, $"NumKeys: {bin.ReadInt32()} items")
                    {
                        Items = ReadList(bin.Skip(-4).ReadInt32(), j => new BinInterpNode(bin.Position, $"{bin.ReadInt32()} keys"))
                    });
                }

                nodes.Add(new BinInterpNode(bin.Position, $"Fade In Time: {bin.ReadFloat()}") { Length = 4 });
                nodes.Add(new BinInterpNode(bin.Position, $"Fade Out Time: {bin.ReadFloat()}") { Length = 4 });
                nodes.Add(MakeInt32Node(bin, "Unknown"));
                if (game == MEGame.ME2)
                {
                    nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }

                nodes.Add(new BinInterpNode(bin.Position, $"Path: {bin.BaseStream.ReadStringLatin1(bin.ReadInt32())}"));
                if (game == MEGame.ME2)
                {
                    nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }

                nodes.Add(new BinInterpNode(bin.Position, $"ID: {bin.BaseStream.ReadStringLatin1(bin.ReadInt32())}"));
                nodes.Add(MakeInt32Node(bin, "index"));
            }

            subnodes.Add(MakeInt32Node(bin, "unknown"));

            subnodes.Add(MakeArrayNode(bin, "Unknown Table D - Mapping?", i => new BinInterpNode(bin.Position, $"{i}")
            {
                IsExpanded = true,
                Items =
                {
                    new BinInterpNode(bin.Position, $"Column Id?: {bin.ReadInt32()}") {Length = 4},
                    new BinInterpNode(bin.Position, $"Name: {nameTable[bin.ReadInt32()]}") {Length = 4},
                    MakeFloatNode(bin, "Unk Float")
                }
            }));
            subnodes.Add(MakeArrayNode(bin, "Lip sync phoneme list:", i => new BinInterpNode(bin.Position, $"Name: {nameTable[bin.ReadInt32()]}") { Length = 4 }));
            subnodes.Add(MakeInt32Node(bin, "Unknown"));
            if (game is MEGame.LE1 or MEGame.LE2)
            {
                subnodes.Add(MakeArrayNode(bin, "Unknown Ints", i => new BinInterpNode(bin.Position, $"Unknown: {nameTable[bin.ReadInt32()]}") { Length = 4 }));
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }
        return subnodes;
    }
}