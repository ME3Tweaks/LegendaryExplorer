Exit code: 0
Wall time: 0.2 seconds
Output:
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using static LegendaryExplorer.UserControls.ExportLoaderControls.BinaryNodeFactory;

namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public partial class BinaryInterpreterWPF
{
Exit code: 0
Wall time: 0.4 seconds
Output:
/// <summary>
        /// Reads the common header for FaceFX archives.
        /// </summary>
        private (int sdkVersion, List<string> names) ReadFaceFXHeader(EndianReader bin, List<ITreeItem> subnodes)
        {
            var archiveSize = bin.ReadInt32();
            subnodes.Add(new BinInterpNode(bin.Position - 4, $"Archive size: {archiveSize} ({FileSize.FormatSize(archiveSize)})"));
            subnodes.Add(new BinInterpNode(bin.Position, $"Magic: {bin.ReadInt32():X8}") { Length = 4 });
            int sdkVersion = bin.ReadInt32(); //1710 = ME1, 1610 = ME2, 1731 = ME3/LE1/LE2/LE3.

            var vIdStr = sdkVersion.ToString();
            var vers = new Version(vIdStr[0] - '0', vIdStr[1] - '0', vIdStr[2] - '0', vIdStr[3] - '0'); //Mega hack
            subnodes.Add(new BinInterpNode(bin.Position - 4, $"SDK Version: {sdkVersion} ({vers})") { Length = 4 });
            if (sdkVersion == 1731)
            {
                subnodes.Add(new BinInterpNode(bin.Position, $"File Format Version: {bin.ReadInt32():X8}") { Length = 4 });
            }

            subnodes.Add(new BinInterpNode(bin.Position, $"Licensee: {bin.ReadFaceFXString(sdkVersion)}"));
            subnodes.Add(new BinInterpNode(bin.Position, $"Project: {bin.ReadFaceFXString(sdkVersion)}"));

            var licenseeVersion = bin.ReadInt32();
            vIdStr = licenseeVersion.ToString();
            vers = new Version(vIdStr[0] - '0', vIdStr[1] - '0', vIdStr[2] - '0', vIdStr[3] - '0'); //Mega hack
            subnodes.Add(new BinInterpNode(bin.Position - 4, $"Licensee version: {vIdStr} ({vers})") { Length = 4 });

            ushort dirVersion = 0;
            subnodes.Add(new BinInterpNode(bin.Position, $"Directory Version: {dirVersion = bin.ReadUInt16()}") { Length = 2 });
            if (dirVersion >= 1)
            {
                int binCount = bin.ReadInt32();
                var clsVersionsDictBins = new List<ITreeItem>();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Class Versions Dict")
                {
                    Items = clsVersionsDictBins
                });
                for (int i = 0; i < binCount; i++)
                {
                    var kvps = new List<ITreeItem>();
                    clsVersionsDictBins.Add(new BinInterpNode(bin.Position, $"Bin {bin.ReadInt32()}")
                    {
                        Items = kvps
                    });
                    var nNameCount = bin.ReadInt32();
                    kvps.Add(new BinInterpNode(bin.Position, $"Num KeyValuePairs in bin: {nNameCount}") { Length = 4 });
                    for (int n = 0; n < nNameCount; n++)
                    {
                        kvps.Add(new BinInterpNode(bin.Position, $"Class: {bin.BaseStream.ReadStringLatin1(bin.ReadInt32())}"));
                        kvps.Add(new BinInterpNode(bin.Position, $"Version: {bin.ReadInt16()}") { Length = 2 });
                    }
                }
            }

            if (sdkVersion < 1700)
            {
                subnodes.Add(MakeUInt16Node(bin, "FxArray version"));
            }

            //Name Table

            var nameTable = new List<string>();
            int nameCount = bin.ReadInt32();
            var nametablePos = bin.Position - 4;
            var nametabObj = new List<ITreeItem>();

            for (int m = 0; m < nameCount; m++)
            {
                var pos = bin.Position;
                if (sdkVersion < 1700)
                {
                    //FxArchiveNameEntry version, always 0
                    bin.ReadUInt16();
                }
                var mName = bin.ReadFaceFXString(sdkVersion);
                nameTable.Add(mName);
                nametabObj.Add(new BinInterpNode(pos, $"{m}: {mName}"));
            }

            subnodes.Add(new BinInterpNode(nametablePos, $"Names: {nameCount} items")
            {
                Items = nametabObj
            });

            subnodes.Add(MakeUInt32Node(bin, "NumObjectPointers"));

            return (sdkVersion, nameTable);
        }

        private List<ITreeItem> StartFaceFXAnimSetScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);
                (int sdkVersion, List<string> names) = ReadFaceFXHeader(bin, subnodes);

                if (sdkVersion < 1700)
                {
                    subnodes.Add(MakeUInt16Node(bin, "FxObject version"));
                    subnodes.Add(MakeUInt16Node(bin, "FxNamedObject version"));
                    subnodes.Add(MakeUInt16Node(bin, "FxName version"));
                }
                subnodes.Add(new BinInterpNode(bin.Position, $"FxAnimSet Name: {names[bin.ReadInt32()]}"));
                if (sdkVersion < 1700)
                {
                    subnodes.Add(MakeUInt16Node(bin, "FxAnimSet version"));
                    subnodes.Add(MakeUInt16Node(bin, "FxName version"));
                }
                subnodes.Add(new BinInterpNode(bin.Position, $"FxAsset Name: {names[bin.ReadInt32()]}"));
                if (sdkVersion < 1700)
                {
                    subnodes.Add(MakeUInt16Node(bin, "FxObject version"));
                    subnodes.Add(MakeUInt16Node(bin, "FxNamedObject version"));
                    subnodes.Add(MakeUInt16Node(bin, "FxName version"));
                }
                subnodes.Add(new BinInterpNode(bin.Position, $"FxAnimGroup Name: {names[bin.ReadInt32()]}"));
                if (sdkVersion < 1700)
                {
                    subnodes.Add(MakeUInt16Node(bin, "FxAnimGroup version"));
                }
                if (sdkVersion < 1700)
                {
                    subnodes.Add(MakeUInt16Node(bin, "FxArray version"));
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
                    if (sdkVersion < 1700)
                    {
                        nodes.Add(MakeUInt16Node(bin, "FxObject version"));
                        nodes.Add(MakeUInt16Node(bin, "FxNamedObject version"));
                        nodes.Add(MakeUInt16Node(bin, "FxName version"));
                    }
                    nodes.Add(new BinInterpNode(bin.Position, $"Name: {names[bin.ReadInt32()]}"));
                    if (sdkVersion < 1700)
                    {
                        nodes.Add(MakeUInt16Node(bin, "FxAnim version"));
                        nodes.Add(MakeUInt16Node(bin, "FxArray version"));
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
                        if (sdkVersion < 1700)
                        {
                            animNodes.Add(MakeUInt16Node(bin, "FxObject version"));
                            animNodes.Add(MakeUInt16Node(bin, "FxNamedObject version"));
                            animNodes.Add(MakeUInt16Node(bin, "FxName version"));
                        }
                        animNodes.Add(new BinInterpNode(bin.Position, $"Name: {names[bin.ReadInt32()]}"));
                        if (sdkVersion < 1700)
                        {
                            animNodes.Add(MakeUInt16Node(bin, "FxAnimCurve version"));
                        }
                        animNodes.Add(new BinInterpNode(bin.Position, $"Interpolation type: {(FxInterpolationType)bin.ReadInt32()}"));
                    }

                    int pointsCount = bin.ReadInt32();
                    nodes.Add(new BinInterpNode(bin.Position - 4, $"Points: {pointsCount} items")
                    {
                        Items = ReadList(pointsCount, j => new BinInterpNode(bin.Position, $"{j}")
                        {
                            Items =
                            [
                                new BinInterpNode(bin.Position, $"Time: {bin.ReadFloat()}") {Length = 4},
                                new BinInterpNode(bin.Position, $"Weight: {bin.ReadFloat()}") {Length = 4},
                                new BinInterpNode(bin.Position, $"InTangent: {bin.ReadFloat()}") {Length = 4},
                                new BinInterpNode(bin.Position, $"LeaveTangent: {bin.ReadFloat()}") {Length = 4}
                            ]
                        })
                    });

                    if (pointsCount > 0)
                    {
                        if (sdkVersion < 1700)
                        {
                            nodes.Add(new BinInterpNode(bin.Position, $"FxArray version: {bin.ReadInt16()}") { Length = 2 });
                        }
                        nodes.Add(new BinInterpNode(bin.Position, $"NumKeys: {bin.ReadInt32()} items")
                        {
                            Items = ReadList(bin.Skip(-4).ReadInt32(), j => new BinInterpNode(bin.Position, $"{bin.ReadInt32()} keys"))
                        });
                    }
                    nodes.Add(new BinInterpNode(bin.Position, $"Fade In Time: {bin.ReadFloat()}") { Length = 4 });
                    nodes.Add(new BinInterpNode(bin.Position, $"Fade Out Time: {bin.ReadFloat()}") { Length = 4 });
                    if (sdkVersion < 1700)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"FxArray version: {bin.ReadInt16()}") { Length = 2 });
                    }
                    nodes.Add(MakeInt32Node(bin, "BoneWeights Array (Always 0 items)"));
                    if (sdkVersion < 1700)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"FxString version: {bin.ReadInt16()}") { Length = 2 });
                    }
                    nodes.Add(new BinInterpNode(bin.Position, $"Path: {bin.BaseStream.ReadStringLatin1(bin.ReadInt32())}"));
                    if (sdkVersion < 1700)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"FxString version: {bin.ReadInt16()}") { Length = 2 });
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
                (int sdkVersion, var nameTable) = ReadFaceFXHeader(bin, subnodes);

                if (sdkVersion < 1700)
                {
                    subnodes.Add(MakeUInt16Node(bin, "FxObject Version"));
                    subnodes.Add(MakeUInt16Node(bin, "FxNamedObject Version"));
                    subnodes.Add(MakeUInt16Node(bin, "FxName Version"));
                }

                subnodes.Add(new BinInterpNode(bin.Position, $"FxActorName {nameTable[bin.ReadInt32()]}"));

                if (sdkVersion < 1700)
                {
                    subnodes.Add(MakeUInt16Node(bin, "FxActor Version"));
                    subnodes.Add(MakeUInt16Node(bin, "FxMasterBoneList Version"));
                }


                //LIST A - BONES
                var bonesList = new List<ITreeItem>();
                var bonesCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode((bin.Position - 4), $"MasterBoneList: {bonesCount} items")
                {
                    Items = bonesList
                });
                List<(int, int, BinInterpNode)> nodesToRename = [];
                for (int a = 0; a < bonesCount; a++)
                {
                    var boneNodeItems = new List<ITreeItem>();
                    if (sdkVersion < 1700)
                    {
                        boneNodeItems.Add(MakeUInt16Node(bin, "FxObject Version"));
                        boneNodeItems.Add(MakeUInt16Node(bin, "FxNamedObject Version"));
                        boneNodeItems.Add(MakeUInt16Node(bin, "FxName Version"));
                    }
                    BinInterpNode boneNode = new(bin.Position, $"{nameTable[bin.ReadInt32()]}")
                    {
                        Items = boneNodeItems
                    };
                    bonesList.Add(boneNode);
                    if (sdkVersion < 1700)
                    {
                        boneNodeItems.Add(MakeUInt16Node(bin, "FxBone Version"));
                    }

                    boneNodeItems.Add(MakeVectorNode(bin, "Position"));
                    boneNodeItems.Add(MakeFxQuatNode(bin, "Rotation"));
                    boneNodeItems.Add(MakeVectorNode(bin, "Scale"));
                    if (sdkVersion >= 1700)
                    {
                        boneNodeItems.Add(MakeFxQuatNode(bin, "Inverse Rotation"));
                    }
                    boneNodeItems.Add(MakeInt32Node(bin, "Index"));
                    boneNodeItems.Add(MakeFloatNode(bin, "Weight"));
                    if (sdkVersion >= 1700)
                    {
                        var links = new List<ITreeItem>();
                        var linksCount = bin.ReadInt32();
                        boneNodeItems.Add(new BinInterpNode(bin.Position - 4, $"Links: {linksCount} items")
                        {
                            Items = links
                        });
                        for (int b = 0; b < linksCount; b++)
                        {
                            var linkItems = new List<ITreeItem>();
                            //formatted like this so it can be parsed by nodesToRename loop below
                            BinInterpNode linkNode = new(bin.Position, "")
                            {
                                Items = linkItems
                            };
                            nodesToRename.Add(b, bin.ReadInt32(), linkNode);
                            links.Add(linkNode);

                            linkItems.Add(new BinInterpNode(bin.Position, $"Bone Name: {nameTable[bin.ReadInt32()]}"));
                            linkItems.Add(MakeVectorNode(bin, "Position"));
                            linkItems.Add(MakeFxQuatNode(bin, "Rotation"));
                            linkItems.Add(MakeVectorNode(bin, "Scale"));
                        }
                    }
                }

                if (sdkVersion < 1700)
                {
                    subnodes.Add(new BinInterpNode(bin.Position, "Remainder unparsed"));
                    return subnodes;
                }

                var combinerList = new List<ITreeItem>();
                var combinerListNames = new List<string>();
                var combinerCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"CompiledFaceGraph Nodes: {combinerCount} items")
                {
                    Items = combinerList
                });

                for (int b = 0, i = 0; i < combinerCount; b++, i++)
                {
                    var bLocation = bin.Position;
                    // There seem to be several types, known types are 0, 4, 6, 8.
                    var formatType = (FxNodeType)bin.ReadInt32();

                    var nodeName = nameTable[bin.ReadInt32()];

                    var combinerNode = new List<ITreeItem>();
                    combinerList.Add(new BinInterpNode(bLocation, $"{b}: {nodeName} - {formatType}")
                    {
                        Items = combinerNode
                    });
                    combinerListNames.Add(nodeName);

                    combinerNode.Add(new BinInterpNode(bin.Position - 8, $"Format: {formatType}"));
                    combinerNode.Add(new BinInterpNode(bin.Position - 4, $"Node: {nodeName}"));
                    combinerNode.Add(new BinInterpNode(bin.Position, $"Min Value: {bin.ReadSingle()}") { Length = 4 });
                    combinerNode.Add(new BinInterpNode(bin.Position, $"Reciprocal of Min: {bin.ReadSingle()}") { Length = 4 });
                    combinerNode.Add(new BinInterpNode(bin.Position, $"Maximum Value: {bin.ReadSingle()}") { Length = 4 });
                    combinerNode.Add(new BinInterpNode(bin.Position, $"Reciprocal of Max: {bin.ReadSingle()}") { Length = 4 });
                    var inputOp = (FxInputOperation)bin.ReadInt32();
                    combinerNode.Add(new BinInterpNode(bin.Position - 4, $"Input Operation: {inputOp}"));

                    var inputLinks = new List<ITreeItem>();
                    var inputLinksCount = bin.ReadInt32();
                    combinerNode.Add(new BinInterpNode(bin.Position - 4, $"Input Links: {inputLinksCount} items")
                    {
                        Items = inputLinks
                    });
                    for (int n2 = 0; n2 < inputLinksCount; n2++)
                    {
                        var combinerIdx = bin.ReadInt32();
                        var linkedNode = combinerIdx < b ? combinerListNames[combinerIdx] : "";
                        var parentLinkItems = new List<ITreeItem>();
                        inputLinks.Add(new BinInterpNode(bin.Position - 4, $"NodeIndex: {combinerIdx} {linkedNode}")
                        {
                            Items = parentLinkItems
                        });
                        var linkFunction = (FxLinkFunction)bin.ReadInt32();
                        parentLinkItems.Add(new BinInterpNode(bin.Position - 4, $"Link Function type: {linkFunction}"));
                        var n3count = bin.ReadInt32();
                        parentLinkItems.Add(new BinInterpNode(bin.Position - 4, $"Parameter Count: {n3count}"));
                        for (int n3 = 0; n3 < n3count; n3++)
                        {
                            parentLinkItems.Add(new BinInterpNode(bin.Position, $"Function Parameter {n3}: {bin.ReadSingle()}") { Length = 4 });
                        }
                    }

                    // Parameters section
                    int parameterCount = bin.ReadInt32();
                    var userPropNode = new List<ITreeItem>(parameterCount);
                    combinerNode.Add(new BinInterpNode(bin.Position - 4, $"UserProperties: {parameterCount} items")
                    {
                        Items = userPropNode
                    });
                    for (int userPropIndex = 0; userPropIndex < parameterCount; userPropIndex++)
                    {
                        string userPropName = nameTable[bin.ReadInt32()];
                        var userPropNodeItems = new List<ITreeItem>();
                        userPropNode.Add(new BinInterpNode(bin.Position - 4, $"{userPropName}")
                        {
                            Items = userPropNodeItems
                        });
                        var parameterFmt = (FxGraphNodeUserPropertyType)bin.ReadInt32();
                        userPropNodeItems.Add(new BinInterpNode(bin.Position - 8, $"Name: {userPropName}"));
                        userPropNodeItems.Add(new BinInterpNode(bin.Position - 4, $"PropertyType: {parameterFmt}") { Length = 4 });

                        userPropNodeItems.Add(new BinInterpNode(bin.Position, $"Int Value: {bin.ReadInt32()}") { Length = 4 });
                        userPropNodeItems.Add(new BinInterpNode(bin.Position, $"Float value: {bin.ReadSingle()}") { Length = 4 });
                        int strValueCount;
                        userPropNodeItems.Add(new BinInterpNode(bin.Position, $"String Value count: {strValueCount = bin.ReadInt32()}") { Length = 4 });
                        for (int strValueIdx = 0; strValueIdx < strValueCount; strValueIdx++)
                        {
                            userPropNodeItems.Add(new BinInterpNode(bin.Position, $"[{strValueIdx}]: {bin.ReadFaceFXString(sdkVersion)}"));
                        }
                    }
                }

                // Fix names for bone node links now that we've parsed combiner table

                foreach ((int linkIdx, int nodeIndex, BinInterpNode nodeToRename) in nodesToRename)
                {
                    nodeToRename.Header = $"{linkIdx}: FaceGraph Node {nodeIndex} ({combinerListNames[nodeIndex]})";
                }

                int nodeDictBinCount = bin.ReadInt32();
                var nodeDictNodes = new List<ITreeItem>();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $" FaceGraph Node lookup dict - {nodeDictBinCount} bins")
                {
                    Items = nodeDictNodes
                });

                for (int c = 0; c < nodeDictBinCount; c++)
                {
                    var unkListCitems = new List<ITreeItem>();
                    int binIndex;
                    nodeDictNodes.Add(new BinInterpNode(bin.Position, $"{c}: {binIndex = bin.ReadInt32()}")
                    {
                        Items = unkListCitems
                    });
                    unkListCitems.Add(new BinInterpNode(bin.Position - 4, $"Bin Index: {binIndex}") { Length = 4 });

                    int kvpCount = bin.ReadInt32();
                    unkListCitems.Add(new BinInterpNode(bin.Position - 4, $"Num KeyValuePairs: {kvpCount}") { Length = 4 });
                    for (int i = 0; i < kvpCount; i++)
                    {
                        var stringLength = bin.ReadInt32();
                        var stringText = bin.ReadEndianASCIIString(stringLength);
                        unkListCitems.Add(new BinInterpNode(bin.Position - stringLength - 4, $"Key (FaceGraph node name): {stringText}") { Length = stringLength + 4 });
                        var name = bin.ReadInt32();
                        unkListCitems.Add(new BinInterpNode(bin.Position - 4, $"Value (FaceGraph node index): {name} {combinerListNames[name]}") { Length = 4 });
                    }
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
                        if (sdkVersion < 1700)
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
                        if (sdkVersion < 1700)
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
                    if (sdkVersion < 1700)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                        nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }

                    nodes.Add(new BinInterpNode(bin.Position, $"Path: {bin.BaseStream.ReadStringLatin1(bin.ReadInt32())}"));
                    if (sdkVersion < 1700)
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
                if (CurrentLoadedExport.Game is MEGame.LE1 or MEGame.LE2)
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
