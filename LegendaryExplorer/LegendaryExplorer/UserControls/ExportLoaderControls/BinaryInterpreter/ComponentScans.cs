using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using static LegendaryExplorer.UserControls.ExportLoaderControls.BinaryNodeFactory;

namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public partial class BinaryInterpreterWPF
{
    private List<ITreeItem> StartStaticMeshComponentScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            bool bLoadVertexColorData;
            uint numVertices;

            int lodDataCount = bin.ReadInt32();
            subnodes.Add(new BinInterpNode(bin.Position - 4, $"LODData count: {lodDataCount}"));
            subnodes.AddRange(ReadList(lodDataCount, i =>
            {
                BinInterpNode node = new BinInterpNode(bin.Position, $"LODData {i}")
                {
                    IsExpanded = true
                };
                node.Items.Add(new BinInterpNode(bin.Position, $"ShadowMaps ({bin.ReadInt32()})")
                {
                    Items = ReadList(bin.Skip(-4).ReadInt32(), j => MakeEntryNode(bin, $"{j}", Pcc))
                });
                node.Items.Add(new BinInterpNode(bin.Position, $"ShadowVertexBuffers ({bin.ReadInt32()})")
                {
                    Items = ReadList(bin.Skip(-4).ReadInt32(), j => MakeEntryNode(bin, $"{j}", Pcc))
                });
                node.Items.Add(MakeLightMapNode(bin));
                node.Items.Add(ListInitHelper.ConditionalAdd(Pcc.Game >= MEGame.ME3, () => new List<ITreeItem>
                {
                    new BinInterpNode(bin.Position, $"bLoadVertexColorData ({bLoadVertexColorData = bin.ReadBoolByte()})"),
                    ListInitHelper.ConditionalAdd(bLoadVertexColorData, () => new ITreeItem[]
                    {
                        new BinInterpNode(bin.Position, "OverrideVertexColors ")
                        {
                            Items =
                            {
                                MakeUInt32Node(bin, "Stride:"),
                                new BinInterpNode(bin.Position, $"NumVertices: {numVertices = bin.ReadUInt32()}"),
                                ListInitHelper.ConditionalAdd(numVertices > 0, () => new ITreeItem[]
                                {
                                    MakeInt32Node(bin, "FColor size"),
                                    new BinInterpNode(bin.Position, $"VertexData ({bin.ReadInt32()})")
                                    {
                                        Items = ReadList(bin.Skip(-4).ReadInt32(), j => MakeColorNode(bin, $"{j}"))
                                    },
                                }),
                            }
                        }
                    })
                }));
                return node;
            }));

            binarystart = (int)bin.Position;
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private List<ITreeItem> StartFluidSurfaceComponentScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            if (Pcc.Game == MEGame.ME3)
            {
                subnodes.Add(MakeLightMapNode(bin));
            }

            binarystart = (int)bin.Position;
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private List<ITreeItem> StartTerrainComponentScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);
            bool bIsLeaf;
            subnodes.Add(MakeArrayNode(bin, "CollisionVertices", i => MakeVectorNode(bin, $"{i}")));
            subnodes.Add(new BinInterpNode(bin.Position, "BVTree")
            {
                IsExpanded = true,
                Items =
                {
                    MakeArrayNode(bin, "Nodes", i => new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items =
                        {
                            MakeBoxNode(bin, "BoundingVolume"),
                            new BinInterpNode(bin.Position, $"bIsLeaf: {bIsLeaf = bin.ReadBoolInt()}"),
                            ListInitHelper.ConditionalAdd(bIsLeaf, () => new ITreeItem[]
                            {
                                MakeUInt16Node(bin, "XPos"),
                                MakeUInt16Node(bin, "YPos"),
                                MakeUInt16Node(bin, "XSize"),
                                MakeUInt16Node(bin, "YSize"),
                            }, () => new ITreeItem[]
                            {
                                MakeUInt16Node(bin, "NodeIndex[0]"),
                                MakeUInt16Node(bin, "NodeIndex[1]"),
                                MakeUInt16Node(bin, "NodeIndex[2]"),
                                MakeUInt16Node(bin, "NodeIndex[3]"),
                            }),
                            MakeFloatNodeConditional(bin, "Unknown float", CurrentLoadedExport.Game != MEGame.UDK),
                        }
                    })
                }
            });
            subnodes.Add(MakeArrayNode(bin, "PatchBounds", i => new BinInterpNode(bin.Position, $"{i}")
            {
                Items =
                {
                    MakeFloatNode(bin, "MinHeight"),
                    MakeFloatNode(bin, "MaxHeight"),
                    MakeFloatNode(bin, "MaxDisplacement"),
                }
            }));
            subnodes.Add(MakeLightMapNode(bin));

            binarystart = (int)bin.Position;
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private List<ITreeItem> StartBrushComponentScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            int cachedConvexElementsCount;
            subnodes.Add(new BinInterpNode(bin.Position, "CachedPhysBrushData")
            {
                IsExpanded = true,
                Items =
                {
                    new BinInterpNode(bin.Position, $"CachedConvexElements ({cachedConvexElementsCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(cachedConvexElementsCount, j =>
                        {
                            int size;
                            var item = new BinInterpNode(bin.Position, $"{j}: ConvexElementData (size of byte: {bin.ReadInt32()}) (number of bytes: {size = bin.ReadInt32()})")
                            {
                                Length = size + 8
                            };
                            bin.Skip(size);
                            return item;
                        })
                    }
                }
            });

            binarystart = (int)bin.Position;
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private List<ITreeItem> StartModelComponentScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
        try
        {
            int count;
            bin.JumpTo(binarystart);
            subnodes.Add(MakeEntryNode(bin, "Model", Pcc));
            subnodes.Add(MakeInt32Node(bin, "ZoneIndex"));
            subnodes.Add(new BinInterpNode(bin.Position, $"Elements ({count = bin.ReadInt32()})")
            {
                Items = ReadList(count, i => new BinInterpNode(bin.Position, $"{i}: FModelElement")
                {
                    Items =
                    {
                        MakeLightMapNode(bin),
                        MakeEntryNode(bin, "Component", Pcc),
                        MakeEntryNode(bin, "Material", Pcc),
                        new BinInterpNode(bin.Position, $"Nodes ({count = bin.ReadInt32()})")
                        {
                            Items = ReadList(count, j => new BinInterpNode(bin.Position, $"{j}: {bin.ReadUInt16()}"))
                        },
                        new BinInterpNode(bin.Position, $"ShadowMaps ({count = bin.ReadInt32()})")
                        {
                            Items = ReadList(count, j => MakeEntryNode(bin, $"{j}", Pcc))
                        },
                        new BinInterpNode(bin.Position, $"IrrelevantLights ({count = bin.ReadInt32()})")
                        {
                            Items = ReadList(count, j => new BinInterpNode(bin.Position, $"{j}: {bin.ReadGuid()}"))
                        }
                    }
                })
            });
            subnodes.Add(new BinInterpNode(bin.Position, $"ComponentIndex: {bin.ReadUInt16()}"));
            subnodes.Add(new BinInterpNode(bin.Position, $"Nodes ({count = bin.ReadInt32()})")
            {
                Items = ReadList(count, i => new BinInterpNode(bin.Position, $"{i}: {bin.ReadUInt16()}"))
            });

            binarystart = (int)bin.Position;
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private List<ITreeItem> StartLightComponentScan(byte[] data, int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        if (Pcc.Game == MEGame.UDK)
        {
            return subnodes;
        }
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            int count;
            foreach (string propName in new[] { "InclusionConvexVolumes", "ExclusionConvexVolumes" })
            {
                subnodes.Add(new BinInterpNode(bin.Position, $"{propName} ({count = bin.ReadInt32()})")
                {
                    Items = ReadList(count, i => new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items =
                        {
                            new BinInterpNode(bin.Position, $"Planes ({count = bin.ReadInt32()})")
                            {
                                Items = ReadList(count, j =>
                                    new BinInterpNode(bin.Position, $"{j}: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()}, W: {bin.ReadSingle()})"))
                            },
                            new BinInterpNode(bin.Position, $"PermutedPlanes ({count = bin.ReadInt32()})")
                            {
                                Items = ReadList(count, j =>
                                    new BinInterpNode(bin.Position, $"{j}: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()}, W: {bin.ReadSingle()})"))
                            }
                        }
                    })
                });
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private List<ITreeItem> StartDecalComponentScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            int numStaticRecievers;
            int count;
            int fDecalVertexSize;
            var item = new BinInterpNode(bin.Position, $"StaticReceivers: {numStaticRecievers = bin.ReadInt32()}")
            {
                IsExpanded = true
            };
            item.Items = ReadList(numStaticRecievers, i =>
            {
                var node = new BinInterpNode(bin.Position, $"{i}");
                try
                {
                    node.Items.Add(MakeEntryNode(bin, "Component", Pcc));
                    node.Items.Add(new BinInterpNode(bin.Position, $"FDecalVertex Size: {fDecalVertexSize = bin.ReadInt32()}"));
                    BinInterpNode interpNode = new BinInterpNode(bin.Position, $"Vertices ({count = bin.ReadInt32()})")
                    {
                        Length = 4 + fDecalVertexSize * count
                    };
                    interpNode.Items = ReadList(count, j => new BinInterpNode(bin.Position, $"{j}")
                    {
                        Length = fDecalVertexSize,
                        Items =
                        {
                            MakeVectorNode(bin, "Position"),
                            MakePackedNormalNode(bin, "TangentX"),
                            MakePackedNormalNode(bin, "TangentZ"),
                            ListInitHelper.ConditionalAdd(Pcc.Game < MEGame.ME3, () => new ITreeItem[]
                            {
                                MakeVector2DNode(bin, "LegacyProjectedUVs")
                            }),
                            MakeVector2DNode(bin, "LightMapCoordinate"),
                            ListInitHelper.ConditionalAdd(Pcc.Game < MEGame.ME3, () => new ITreeItem[]
                            {
                                MakeVector2DNode(bin, "LegacyNormalTransform[0]"),
                                MakeVector2DNode(bin, "LegacyNormalTransform[1]")
                            }),
                        }
                    });
                    node.Items.Add(interpNode);
                    node.Items.Add(MakeInt32Node(bin, "unsigned short size"));
                    node.Items.Add(new BinInterpNode(bin.Position, $"Indices ({count = bin.ReadInt32()})")
                    {
                        Length = 4 + count * 2,
                        Items = ReadList(count, j => new BinInterpNode(bin.Position, $"{j}: {bin.ReadUInt16()}") { Length = 2 })
                    });
                    node.Items.Add(MakeUInt32Node(bin, "NumTriangles"));
                    node.Items.Add(MakeLightMapNode(bin));
                    if (Pcc.Game >= MEGame.ME3)
                    {
                        node.Items.Add(new BinInterpNode(bin.Position, $"ShadowMap1D ({count = bin.ReadInt32()})")
                        {
                            Length = 4 + count * 4,
                            Items = ReadList(count, j => new BinInterpNode(bin.Position, $"{j}: {MakeEntryNodeString(bin, Pcc)}") { Length = 4 })
                        });
                        node.Items.Add(MakeInt32Node(bin, "Data"));
                        node.Items.Add(MakeInt32Node(bin, "InstanceIndex"));
                    }
                }
                catch (Exception e)
                {
                    node.Items.Add(new BinInterpNode { Header = $"Error reading binary data: {e}" });
                }
                return node;
            });
            subnodes.Add(item);

            binarystart = (int)bin.Position;
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private BinInterpNode MakeLightMapNode(EndianReader bin, List<(int, int)> lightmapChunksToRemove = null)
    {
        ELightMapType lightMapType;
        int bulkSerializeElementCount;
        int bulkSerializeDataSize;

        var retvalue = new BinInterpNode(bin.Position, "LightMap ")
        {
            IsExpanded = true,
            Items =
            {
                new BinInterpNode(bin.Position, $"LightMapType: {lightMapType = (ELightMapType) bin.ReadInt32()}"),
                ListInitHelper.ConditionalAdd(lightMapType != ELightMapType.LMT_None, () =>
                {
                    //chunk starts at 0 - postion of LM type
                    var chunk = ((int)bin.Position - 4,0);
                    var tree = new List<ITreeItem>
                    {
                        new BinInterpNode(bin.Position, $"LightGuids ({bin.ReadInt32()})")
                        {
                            Items = ReadList(bin.Skip(-4).ReadInt32(), j => new BinInterpNode(bin.Position, $"{j}: {bin.ReadGuid()}"))
                        },
                        ListInitHelper.ConditionalAdd(lightMapType == ELightMapType.LMT_1D, () => new ITreeItem[]
                        {
                            MakeEntryNode(bin, "Owner", Pcc),
                            MakeUInt32Node(bin, "BulkDataFlags:"),
                            new BinInterpNode(bin.Position, $"ElementCount: {bulkSerializeElementCount = bin.ReadInt32()}"),
                            new BinInterpNode(bin.Position, $"BulkDataSizeOnDisk: {bulkSerializeDataSize = bin.ReadInt32()}"),
                            MakeInt32Node(bin, "BulkDataOffsetInFile"),
                            new BinInterpNode(bin.Position, $"DirectionalSamples: ({bulkSerializeElementCount})")
                            {
                                Items = ReadList(bulkSerializeElementCount, j => new BinInterpNode(bin.Position, $"{j}")
                                {
                                    Items = ReadList(bulkSerializeDataSize / bulkSerializeElementCount / 4, k => new BinInterpNode(bin.Position,
                                        $"(B: {bin.ReadByte()}, G: {bin.ReadByte()}, R: {bin.ReadByte()}, A: {bin.ReadByte()})"))
                                })
                            },
                            MakeVectorNode(bin, "ScaleVector 1"),
                            MakeVectorNode(bin, "ScaleVector 2"),
                            MakeVectorNode(bin, "ScaleVector 3"),
                            Pcc.Game < MEGame.ME3 ? MakeVectorNode(bin, "ScaleVector 4") : null,
                            MakeUInt32Node(bin, "BulkDataFlags:"),
                            new BinInterpNode(bin.Position, $"ElementCount: {bulkSerializeElementCount = bin.ReadInt32()}"),
                            new BinInterpNode(bin.Position, $"BulkDataSizeOnDisk: {bulkSerializeDataSize = bin.ReadInt32()}"),
                            MakeInt32Node(bin, "BulkDataOffsetInFile"),
                            new BinInterpNode(bin.Position, $"SimpleSamples: ({bulkSerializeElementCount})")
                            {
                                Items = ReadList(bulkSerializeElementCount, j => new BinInterpNode(bin.Position, $"{j}")
                                {
                                    Items = ReadList(bulkSerializeDataSize / bulkSerializeElementCount / 4, k => new BinInterpNode(bin.Position,
                                        $"(B: {bin.ReadByte()}, G: {bin.ReadByte()}, R: {bin.ReadByte()}, A: {bin.ReadByte()})"))
                                })
                            },
                        }.NonNull()),
                        ListInitHelper.ConditionalAdd(lightMapType == ELightMapType.LMT_2D, () => new List<ITreeItem>
                        {
                            MakeEntryNode(bin, "Texture 1", Pcc),
                            MakeVectorNodeEditable(bin, "ScaleVector 1", true),
                            MakeEntryNode(bin, "Texture 2", Pcc),
                            MakeVectorNodeEditable(bin, "ScaleVector 2", true),
                            MakeEntryNode(bin, "Texture 3", Pcc),
                            MakeVectorNodeEditable(bin, "ScaleVector 3", true),
                            ListInitHelper.ConditionalAdd(Pcc.Game < MEGame.ME3, () => new ITreeItem[]
                            {
                                MakeEntryNode(bin, "Texture 4", Pcc),
                                MakeVectorNodeEditable(bin, "ScaleVector 4", true),
                            }),
                            MakeVector2DNodeEditable(bin, "CoordinateScale", true),
                            MakeVector2DNodeEditable(bin, "CoordinateBias", true)
                        }),
                        ListInitHelper.ConditionalAdd(lightMapType == ELightMapType.LMT_3, () => new ITreeItem[]
                        {
                            MakeInt32Node(bin, "Unknown"),
                            MakeUInt32Node(bin, "BulkDataFlags:"),
                            new BinInterpNode(bin.Position, $"ElementCount: {bulkSerializeElementCount = bin.ReadInt32()}"),
                            new BinInterpNode(bin.Position, $"BulkDataSizeOnDisk: {bulkSerializeDataSize = bin.ReadInt32()}"),
                            MakeInt32Node(bin, "BulkDataOffsetInFile"),
                            new BinInterpNode(bin.Position, $"DirectionalSamples?: ({bulkSerializeElementCount})")
                            {
                                Items = ReadList(bulkSerializeElementCount, j => new BinInterpNode(bin.Position, $"{j}")
                                {
                                    Items = ReadList(bulkSerializeDataSize / bulkSerializeElementCount / 4, k => new BinInterpNode(bin.Position,
                                        $"(B: {bin.ReadByte()}, G: {bin.ReadByte()}, R: {bin.ReadByte()}, A: {bin.ReadByte()})"))
                                })
                            },
                            MakeVectorNode(bin, "ScaleVector?"),
                            MakeVectorNode(bin, "ScaleVector?")
                        }),
                        ListInitHelper.ConditionalAdd(lightMapType == ELightMapType.LMT_4 || lightMapType == ELightMapType.LMT_6, () => new List<ITreeItem>
                        {
                            MakeEntryNode(bin, "Texture 1", Pcc),
                            new ListInitHelper.InitCollection<ITreeItem>(ReadList(8, j => MakeFloatNode(bin, "Unknown float"))),
                            MakeEntryNode(bin, "Texture 2", Pcc),
                            new ListInitHelper.InitCollection<ITreeItem>(ReadList(8, j => MakeFloatNode(bin, "Unknown float"))),
                            MakeEntryNode(bin, "Texture 3", Pcc),
                            new ListInitHelper.InitCollection<ITreeItem>(ReadList(8, j => MakeFloatNode(bin, "Unknown float"))),
                            new ListInitHelper.InitCollection<ITreeItem>(ReadList(4, j => MakeFloatNode(bin, "Unknown float"))),
                        }),
                        ListInitHelper.ConditionalAdd(lightMapType == ELightMapType.LMT_5, () => new ITreeItem[]
                        {
                            MakeInt32Node(bin, "Unknown"),
                            MakeUInt32Node(bin, "BulkDataFlags:"),
                            new BinInterpNode(bin.Position, $"ElementCount: {bulkSerializeElementCount = bin.ReadInt32()}"),
                            new BinInterpNode(bin.Position, $"BulkDataSizeOnDisk: {bulkSerializeDataSize = bin.ReadInt32()}"),
                            MakeInt32Node(bin, "BulkDataOffsetInFile"),
                            new BinInterpNode(bin.Position, $"SimpleSamples?: ({bulkSerializeElementCount})")
                            {
                                Items = ReadList(bulkSerializeElementCount, j => new BinInterpNode(bin.Position, $"{j}")
                                {
                                    Items = ReadList(bulkSerializeDataSize / bulkSerializeElementCount / 4, k => new BinInterpNode(bin.Position,
                                        $"(B: {bin.ReadByte()}, G: {bin.ReadByte()}, R: {bin.ReadByte()}, A: {bin.ReadByte()})"))
                                })
                            },
                            MakeVectorNode(bin, "ScaleVector?")
                        }),
                    };
                    chunk.Item2 = (int)bin.Position;
                    lightmapChunksToRemove?.Add(chunk);
                    return tree;
                })
            }
        };

        return retvalue;
    }
}