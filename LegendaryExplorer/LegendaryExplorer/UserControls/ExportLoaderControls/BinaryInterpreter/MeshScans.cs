using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal.Classes;
using static LegendaryExplorer.UserControls.ExportLoaderControls.BinaryNodeFactory;


namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public partial class BinaryInterpreterWPF
{
    private IEnumerable<ITreeItem> StartStaticMeshScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var matOffsets = new List<long>();
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            subnodes.Add(MakeBoxSphereBoundsNode(bin, "Bounds"));
            subnodes.Add(MakeEntryNode(bin, "BodySetup", Pcc));
            subnodes.Add(MakekDOPTreeNode(bin));

            subnodes.Add(MakeInt32Node(bin, "InternalVersion"));
            int count;
            bool bUseFullPrecisionUVs;
            int numTexCoords;
            int numVertices;
            if (Pcc.Game == MEGame.UDK)
            {
                // Unsure how accurate these are.
                subnodes.Add(MakeBoolIntNode(bin, "bHaveSourceData", out bool hasSourceData));
                if (hasSourceData)
                {
                    // Not sure how to serialize this
                }


                subnodes.Add(MakeInt32Node(bin, "OptimizationSettings"));
                subnodes.Add(MakeBoolIntNode(bin, "bHasBeenSimplified"));
                subnodes.Add(MakeBoolIntNode(bin, "bIsMeshProxy"));
            }

            subnodes.Add(MakeArrayNode(bin, "LODModels", i =>
            {
                BinInterpNode lodNode = new BinInterpNode(bin.Position, $"{i}")
                {
                    IsExpanded = true
                };
                try
                {
                    lodNode.Items.Add(new BinInterpNode(bin.Position, $"BulkDataFlags: {(EBulkDataFlags)bin.ReadUInt32()}"));
                    lodNode.Items.Add(new BinInterpNode(bin.Position, $"Element Count: {count = bin.ReadInt32()}"));
                    lodNode.Items.Add(MakeInt32Node(bin, "BulkDataSizeOnDisk"));
                    lodNode.Items.Add(MakeInt32Node(bin, "BulkDataOffsetInFile"));
                    lodNode.Items.Add(new BinInterpNode(bin.Position, $"RawTriangles ({count})")
                    {
                        Items = ReadList(count, j =>
                        {
                            return new BinInterpNode(bin.Position, $"{j}")
                            {
                                Items =
                                    {
                                        new BinInterpNode(bin.Position, "Vertices")
                                        {
                                            Items = ReadList(3, k => MakeVectorNode(bin, $"{k}"))
                                        },
                                        new BinInterpNode(bin.Position, "UVs")
                                        {
                                            Items = ReadList(3, k => new BinInterpNode(bin.Position, $"UV[{k}]")
                                            {
                                                Items = ReadList(8, l => MakeVector2DNode(bin, $"{l}"))
                                            })
                                        },
                                        new BinInterpNode(bin.Position, "Colors")
                                        {
                                            Items = ReadList(3, k => MakeColorNode(bin, $"{k}"))
                                        },
                                        MakeInt32Node(bin, "MaterialIndex"),
                                        ListInitHelper.ConditionalAdd(Pcc.Game >= MEGame.ME3, () => new ITreeItem[] {MakeInt32Node(bin, "FragmentIndex")}),
                                        MakeUInt32Node(bin, "SmoothingMask"),
                                        MakeInt32Node(bin, "NumUVs"),
                                        ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game == MEGame.UDK, () => MakeBoolIntNode(bin, "bExplicitNormals")),
                                        ListInitHelper.ConditionalAdd(Pcc.Game >= MEGame.ME3, () => new ITreeItem[]
                                        {
                                            new BinInterpNode(bin.Position, "TangentX")
                                            {
                                                Items = ReadList(3, k => MakeVectorNode(bin, $"{k}"))
                                            },
                                            new BinInterpNode(bin.Position, "TangentY")
                                            {
                                                Items = ReadList(3, k => MakeVectorNode(bin, $"{k}"))
                                            },
                                            new BinInterpNode(bin.Position, "TangentZ")
                                            {
                                                Items = ReadList(3, k => MakeVectorNode(bin, $"{k}"))
                                            },
                                            MakeBoolIntNode(bin, "bOverrideTangentBasis")
                                        })
                                    }
                            };
                        })
                    });
                    lodNode.Items.Add(MakeArrayNode(bin, "Elements", j =>
                    {
                        matOffsets.Add(bin.Position);
                        BinInterpNode node = new BinInterpNode(bin.Position, $"{j}");
                        node.Items.Add(MakeEntryNode(bin, "Material", Pcc));
                        node.Items.Add(MakeBoolIntNode(bin, "EnableCollision"));
                        node.Items.Add(MakeBoolIntNode(bin, "OldEnableCollision"));
                        node.Items.Add(MakeBoolIntNode(bin, "bEnableShadowCasting"));
                        node.Items.Add(MakeUInt32Node(bin, "FirstIndex"));
                        node.Items.Add(MakeUInt32Node(bin, "NumTriangles"));
                        node.Items.Add(MakeUInt32Node(bin, "MinVertexIndex"));
                        node.Items.Add(MakeUInt32Node(bin, "MaxVertexIndex"));
                        node.Items.Add(MakeInt32Node(bin, "MaterialIndex"));
                        node.Items.Add(ListInitHelper.ConditionalAdd(Pcc.Game >= MEGame.ME3, () => new ITreeItem[]
                        {
                                MakeArrayNode(bin, "Fragments", k => new BinInterpNode(bin.Position, $"{k}")
                                {
                                    Items =
                                    {
                                        MakeInt32Node(bin, "BaseIndex"),
                                        MakeInt32Node(bin, "NumPrimitives")
                                    }
                                }),
                                MakeBoolByteNode(bin, "LoadPlatformData")
                            //More stuff here if LoadPlatformData is true, but I don't think it ever is in ME3
                        }));
                        return node;
                    }));
                    lodNode.Items.Add(new BinInterpNode(bin.Position, "PositionVertexBuffer")
                    {
                        Items =
                            {
                                MakeUInt32Node(bin, "Stride"),
                                MakeUInt32Node(bin, "NumVertices"),
                                ListInitHelper.ConditionalAdd(Pcc.Game == MEGame.ME3 || Pcc.Game.IsLEGame(), () => new ITreeItem[]{ MakeUInt32Node(bin, "unk") }),
                                MakeInt32Node(bin, "FVector size"),
                                MakeArrayNode(bin, "VertexData", k => MakeVectorNode(bin, $"{k}"))
                            }
                    });
                    lodNode.Items.Add(new BinInterpNode(bin.Position, "VertexBuffer")
                    {
                        Items =
                            {
                                new BinInterpNode(bin.Position, $"NumTexCoords: {numTexCoords = bin.ReadInt32()}"),
                                MakeUInt32Node(bin, "Stride"),
                                MakeUInt32Node(bin, "NumVertices"),
                                new BinInterpNode(bin.Position, $"bUseFullPrecisionUVs: {bUseFullPrecisionUVs = bin.ReadBoolInt()}"),
                                ListInitHelper.ConditionalAdd(Pcc.Game == MEGame.ME3 || Pcc.Game.IsLEGame(), () => new ITreeItem[]
                                {
                                    MakeUInt32Node(bin, "unk")
                                }),
                                MakeInt32Node(bin, "element size"),
                                MakeArrayNode(bin, "VertexData", k => new BinInterpNode(bin.Position, $"{k}")
                                {
                                    Items =
                                    {
                                        MakePackedNormalNode(bin, "TangentX"),
                                        MakePackedNormalNode(bin, "TangentZ"),
                                        ListInitHelper.ConditionalAdd(Pcc.Game < MEGame.ME3, () => new ITreeItem[]
                                        {
                                            MakeColorNode(bin, "Color"),
                                        }),
                                        ListInitHelper.ConditionalAdd(bUseFullPrecisionUVs,
                                                                      () => ReadList(numTexCoords, l => MakeVector2DNode(bin, $"UV[{l}]")),
                                                                      () => ReadList(numTexCoords, l => MakeVector2DHalfNode(bin, $"UV[{l}]")))
                                    }
                                })
                            }
                    });
                    lodNode.Items.Add(ListInitHelper.ConditionalAdd(Pcc.Game >= MEGame.ME3, () => new List<ITreeItem>
                        {
                            new BinInterpNode(bin.Position, "ColorVertexBuffer")
                            {
                                Items =
                                {
                                    MakeUInt32Node(bin, "Stride"),
                                    new BinInterpNode(bin.Position, $"NumVertices: {numVertices = bin.ReadInt32()}"),
                                    ListInitHelper.ConditionalAdd(numVertices > 0, () => new ITreeItem[]
                                    {
                                        MakeInt32Node(bin, "FColor size"),
                                        MakeArrayNode(bin, "VertexData", k => MakeColorNode(bin, $"{k}"))
                                    }),
                                }
                            }
                        }));
                    if (Pcc.Game < MEGame.UDK)
                    {
                        lodNode.Items.Add(new BinInterpNode(bin.Position, "ShadowExtrusionVertexBuffer")
                        {
                            Items =
                                {
                                    MakeUInt32Node(bin, "Stride"),
                                    MakeUInt32Node(bin, "NumVertices"),
                                    MakeInt32Node(bin, "float size"),
                                    MakeArrayNode(bin, "VertexData", k => MakeFloatNode(bin, $"ShadowExtrusionPredicate {k}"))
                                }
                        });
                    }
                    lodNode.Items.Add(MakeUInt32Node(bin, "NumVertices"));
                    lodNode.Items.Add(MakeInt32Node(bin, "ushort size"));
                    lodNode.Items.Add(MakeArrayNode(bin, "IndexBuffer", j => MakeUInt16Node(bin, $"{j}")));
                    lodNode.Items.Add(MakeInt32Node(bin, "ushort size"));
                    lodNode.Items.Add(MakeArrayNode(bin, "WireframeIndexBuffer", j => MakeUInt16Node(bin, $"{j}")));
                    if (Pcc.Game < MEGame.UDK)
                    {
                        lodNode.Items.Add(MakeInt32Node(bin, "FMeshEdge size"));
                        lodNode.Items.Add(MakeArrayNode(bin, "Edges", j => new BinInterpNode(bin.Position, $"{j}")
                        {
                            Items =
                                {
                                    MakeInt32Node(bin, "Vertices[0]"),
                                    MakeInt32Node(bin, "Vertices[1]"),
                                    MakeInt32Node(bin, "Faces[0]"),
                                    MakeInt32Node(bin, "Faces[1]"),
                                }
                        }));
                        lodNode.Items.Add(MakeArrayNode(bin, "ShadowTriangleDoubleSided", j => MakeByteNode(bin, $"{j}")));
                    }
                    else
                    {
                        lodNode.Items.Add(MakeInt32Node(bin, "ushort size"));
                        lodNode.Items.Add(MakeArrayNode(bin, "AdjacencyIndexBuffer", j => MakeUInt16Node(bin, $"{j}"))); // Version 841+
                    }
                    lodNode.Items.Add(ListInitHelper.ConditionalAdd(Pcc.Game == MEGame.ME1, () => new List<ITreeItem>
                        {
                            MakeUInt32Node(bin, "unk"),
                            MakeUInt32Node(bin, "BulkDataFlags"),
                            MakeInt32Node(bin, "ElementCount"),
                            new BinInterpNode(bin.Position, $"BulkDataSizeOnDisk: {count = bin.ReadInt32()}"),
                            MakeInt32Node(bin, "BulkDataOffsetInFile"),
                            ListInitHelper.ConditionalAdd(count > 0, () =>
                            {
                                ITreeItem node = new BinInterpNode(bin.Position, $"XML file ({count} bytes)") {Length = count};
                                bin.Skip(count);
                                return new []{node};
                            })
                        }));
                }
                catch (Exception ex)
                {
                    lodNode.Items.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
                }
                return lodNode;
            }));

            if (Pcc.Game == MEGame.ME1)
            {
                subnodes.Add(MakeUInt32Node(bin, "unk"));
                subnodes.Add(MakeUInt32Node(bin, "unk"));
                subnodes.Add(MakeUInt32Node(bin, "unk"));
                subnodes.Add(MakeUInt32Node(bin, "unk"));
                subnodes.Add(MakeUInt32Node(bin, "unk"));
            }
            else
            {
                subnodes.Add(MakeArrayNode(bin, "LODInfo (Editor only)", x=> null)); // This appears to be TArray; however cannot find any examples in game files for verification. It seems like it doesn't actually serialize, maybe it's editor only transient?
                subnodes.Add(new BinInterpNode(bin.Position, $"ThumbnailAngle: (Pitch: {bin.ReadInt32()}, Yaw: {bin.ReadInt32()}, Roll: {bin.ReadInt32()})"));
                subnodes.Add(MakeFloatNode(bin, "ThumbnailDistance"));
                if (Pcc.Game < MEGame.UDK)
                {
                    subnodes.Add(MakeUInt32Node(bin, "unk"));
                }
                if (Pcc.Game >= MEGame.ME3)
                {
                    subnodes.Add(MakeStringNode(bin, "HighResSourceMeshName", Pcc.Game));
                    subnodes.Add(MakeUInt32Node(bin, "HighResSourceMeshCRC"));
                    subnodes.Add(MakeGuidNode(bin, "LightingGuid"));
                }
                if (Pcc.Game == MEGame.UDK)
                {
                    subnodes.Add(MakeUInt32Node(bin, "VertexPositionVersionNumber"));
                    subnodes.Add(MakeArrayNode(bin, "CachedStreamingTextureFactors", j => MakeFloatNode(bin, $"{j}")));
                    subnodes.Add(MakeBoolIntNode(bin, "bRemoveDegenerates"));
                    subnodes.Add(MakeBoolIntNode(bin, "bPerLODStaticLightingForInstancing"));
                    subnodes.Add(MakeBoolIntNode(bin, "ConsolePreallocateInstanceCount"));
                }
            }

            binarystart = (int)bin.Position;
            return subnodes.Prepend(new BinInterpNode("Materials")
            {
                IsExpanded = true,
                Items = ReadList(matOffsets.Count, i =>
                {
                    bin.JumpTo(matOffsets[i]);
                    var matNode = MakeEntryNode(bin, $"Material[{i}]", Pcc);
                    try
                    {
                        if (Pcc.GetEntry(bin.Skip(-4).ReadInt32()) is ExportEntry matExport)
                        {
                            foreach (IEntry texture in MaterialInstanceConstant.GetTextures(matExport, resolveImports: false))
                            {
                                matNode.Items.Add(new BinInterpNode($"#{texture.UIndex} {matExport.FileRef.GetEntryString(texture.UIndex)}"));
                            }
                        }
                    }
                    catch
                    {
                        matNode.Items.Add(new BinInterpNode("Error reading Material!"));
                    }

                    return matNode;
                })
            });
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }
    private IEnumerable<ITreeItem> StartFracturedStaticMeshScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            subnodes.AddRange(StartStaticMeshScan(data, ref binarystart));
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            subnodes.Add(MakeEntryNode(bin, "SourceStaticMesh", Pcc));
            subnodes.Add(MakeArrayNode(bin, "Fragments", i => new BinInterpNode(bin.Position, $"{i}")
            {
                Items =
                    {
                        MakeVectorNode(bin, "Center"),
                        new BinInterpNode(bin.Position, "ConvexElem")
                        {
                            Items =
                            {
                                MakeArrayNode(bin, "VertexData", j => MakeVectorNode(bin, $"{j}")),
                                MakeArrayNode(bin, "PermutedVertexData", j => MakeQuatNode(bin, $"{j}")), //actually planes, not quats
                                MakeArrayNode(bin, "FaceTriData", j => MakeInt32Node(bin, $"{j}")),
                                MakeArrayNode(bin, "EdgeDirections", j => MakeVectorNode(bin, $"{j}")),
                                MakeArrayNode(bin, "FaceNormalDirections", j => MakeVectorNode(bin, $"{j}")),
                                MakeArrayNode(bin, "FacePlaneData", j => MakeQuatNode(bin, $"{j}")), //actually planes, not quats
                                MakeBoxNode(bin, "ElemBox")
                            }
                        },
                        MakeBoxSphereBoundsNode(bin, "Bounds"),
                        ListInitHelper.ConditionalAdd(Pcc.Game >= MEGame.ME3, () => new ITreeItem[]
                        {
                            MakeArrayNode(bin, "Neighbours", j => MakeByteNode(bin, $"{j}")),
                            MakeBoolIntNode(bin, "bCanBeDestroyed"),
                            MakeBoolIntNode(bin, "bRootFragment"),
                            MakeBoolIntNode(bin, "bNeverSpawnPhysicsChunk"),
                            MakeVectorNode(bin, "AverageExteriorNormal"),
                            MakeArrayNode(bin, "NeighbourDims", j => MakeFloatNode(bin, $"{j}")),
                        })
                    }
            }));
            subnodes.Add(MakeInt32Node(bin, "CoreFragmentIndex"));
            if (Pcc.Game >= MEGame.ME3)
            {
                subnodes.Add(MakeInt32Node(bin, "InteriorElementIndex"));
                subnodes.Add(MakeVectorNode(bin, "CoreMeshScale3D"));
                subnodes.Add(MakeVectorNode(bin, "CoreMeshOffset"));
                subnodes.Add(MakeRotatorNode(bin, "CoreMeshRotation"));
                subnodes.Add(MakeVectorNode(bin, "PlaneBias"));
                subnodes.Add(MakeUInt16Node(bin, "NonCriticalBuildVersion"));
                subnodes.Add(MakeUInt16Node(bin, "LicenseeNonCriticalBuildVersion"));
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private BinInterpNode MakekDOPTreeNode(EndianReader bin)
    {
        bool bIsLeaf;
        return new BinInterpNode(bin.Position, "kDOPTree")
        {
            IsExpanded = true,
            Items =
                {
                    ListInitHelper.ConditionalAdd(Pcc.Game >= MEGame.ME3, () => new ITreeItem[]
                    {
                        new BinInterpNode(bin.Position, "RootBound")
                        {
                            Items =
                            {
                                MakeFloatNode(bin, "Min[0]"),
                                MakeFloatNode(bin, "Min[1]"),
                                MakeFloatNode(bin, "Min[2]"),
                                MakeFloatNode(bin, "Max[0]"),
                                MakeFloatNode(bin, "Max[1]"),
                                MakeFloatNode(bin, "Max[2]")
                            }
                        }
                    }),
                    MakeInt32Node(bin, "kDOPNodeSize"),
                    MakeArrayNode(bin, "Nodes", i => new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items =
                        {
                            ListInitHelper.ConditionalAdd(Pcc.Game >= MEGame.ME3, () => new ITreeItem[]
                            {
                                new BinInterpNode(bin.Position, "BoundingVolume")
                                {
                                    IsExpanded = true,
                                    Items =
                                    {
                                        MakeByteNode(bin, "Min[0]"),
                                        MakeByteNode(bin, "Min[1]"),
                                        MakeByteNode(bin, "Min[2]"),
                                        MakeByteNode(bin, "Max[0]"),
                                        MakeByteNode(bin, "Max[1]"),
                                        MakeByteNode(bin, "Max[2]")
                                    }
                                }
                            },() => new List<ITreeItem>
                            {
                                new BinInterpNode(bin.Position, "BoundingVolume")
                                {
                                    Items =
                                    {
                                        MakeFloatNode(bin, "Min[0]"),
                                        MakeFloatNode(bin, "Min[1]"),
                                        MakeFloatNode(bin, "Min[2]"),
                                        MakeFloatNode(bin, "Max[0]"),
                                        MakeFloatNode(bin, "Max[1]"),
                                        MakeFloatNode(bin, "Max[2]")
                                    }
                                },
                                new BinInterpNode(bin.Position, $"bIsLeaf: {bIsLeaf = bin.ReadBoolInt()}"),
                                ListInitHelper.ConditionalAdd(bIsLeaf, () => new ITreeItem[]
                                {
                                    MakeUInt16Node(bin, "NumTriangles"),
                                    MakeUInt16Node(bin, "StartIndex")
                                }, () => new ITreeItem[]
                                {
                                    MakeUInt16Node(bin, "LeftNode"),
                                    MakeUInt16Node(bin, "RightNode")
                                })
                            })
                        }
                    }),
                    MakeInt32Node(bin, "FkDOPCollisionTriangleSize"),
                    MakeArrayNode(bin, "Triangles", i => new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items =
                        {
                            MakeUInt16Node(bin, "Vertex1"),
                            MakeUInt16Node(bin, "Vertex2"),
                            MakeUInt16Node(bin, "Vertex3"),
                            MakeUInt16Node(bin, "MaterialIndex"),
                        }
                    })
                }
        };
    }

    private List<ITreeItem> StartSkeletalMeshScan(byte[] data, ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        var game = CurrentLoadedExport.FileRef.Game;
        try
        {
            PackageCache cache = new PackageCache();
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            subnodes.Add(MakeBoxSphereBoundsNode(bin, "Bounds"));
            subnodes.Add(MakeArrayNode(bin, "Materials", i =>
            {
                var matNode = MakeEntryNode(bin, $"{i}", Pcc);
                try
                {
                    var value = bin.Skip(-4).ReadInt32();
                    if (value != 0 && Pcc.GetEntry(value) is ExportEntry matExport)
                    {
                        foreach (IEntry texture in MaterialInstanceConstant.GetTextures(matExport, cache))
                        {
                            matNode.Items.Add(new BinInterpNode(-1, $"#{texture.UIndex} {texture.FileRef.GetEntryString(texture.UIndex)}", NodeType.StructLeafObject) { UIndexValue = texture.UIndex });
                        }
                    }
                }
                catch
                {
                    matNode.Items.Add(new BinInterpNode("Error reading Material!"));
                }

                return matNode;
            }, true, BinInterpNode.ArrayPropertyChildAddAlgorithm.FourBytes));
            subnodes.Add(MakeVectorNode(bin, "Origin"));
            subnodes.Add(MakeRotatorNode(bin, "Rotation Origin"));
            subnodes.Add(MakeArrayNode(bin, "RefSkeleton", i => new BinInterpNode(bin.Position, $"{i}: {bin.ReadNameReference(Pcc).Instanced}")
            {
                Items =
                {
                    MakeUInt32Node(bin, "Flags"),
                    MakeQuatNode(bin, "Bone Orientation (quaternion)"),
                    MakeVectorNode(bin, "Bone Position"),
                    MakeInt32Node(bin, "NumChildren"),
                    MakeInt32Node(bin, "ParentIndex"),
                    ListInitHelper.ConditionalAddOne<ITreeItem>( Pcc.Game >= MEGame.ME3, () => MakeColorNode(bin, "BoneColor")),
                }
            }));
            subnodes.Add(MakeInt32Node(bin, "SkeletalDepth"));
            int rawPointIndicesCount;
            bool useFullPrecisionUVs = true;
            int numTexCoords = 1;
            subnodes.Add(MakeArrayNode(bin, "LODModels", i =>
            {
                BinInterpNode node = new BinInterpNode(bin.Position, $"{i}");
                try
                {
                    node.Items.Add(MakeArrayNode(bin, "Sections", j => new BinInterpNode(bin.Position, $"{j}")
                    {
                        Items =
                        {
                            MakeUInt16Node(bin, "MaterialIndex"),
                            MakeUInt16Node(bin, "ChunkIndex"),
                            MakeUInt32Node(bin, "BaseIndex"),
                            ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game >= MEGame.ME3,
                                () => MakeUInt32Node(bin, "NumTriangles"),
                                () => MakeUInt16Node(bin, "NumTriangles")),
                            ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game == MEGame.UDK, () => MakeByteNode(bin, "TriangleSorting"))
                        }
                    }));
                    node.Items.Add(ListInitHelper.ConditionalAdd(Pcc.Game == MEGame.UDK, () => new ITreeItem[]
                    {
                        MakeBoolIntNode(bin, "NeedsCPUAccess"),
                        MakeByteNode(bin, "Datatype size"),
                    }));
                    node.Items.Add(MakeInt32Node(bin, "Index size?"));
                    if (Pcc.Game == MEGame.UDK && bin.Skip(-4).ReadInt32() == 4)
                    {
                        node.Items.Add(MakeArrayNode(bin, "IndexBuffer", j => MakeUInt32Node(bin, $"{j}")));
                    }
                    else
                    {
                        node.Items.Add(MakeArrayNode(bin, "IndexBuffer", j => MakeUInt16Node(bin, $"{j}")));
                    }
                    node.Items.Add(ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game != MEGame.UDK, () => MakeArrayNode(bin, "ShadowIndices", j => MakeUInt16Node(bin, $"{j}"))));
                    node.Items.Add(MakeArrayNode(bin, "ActiveBoneIndices", j => MakeUInt16Node(bin, $"{j}")));
                    node.Items.Add(ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game != MEGame.UDK, () => MakeArrayNode(bin, "ShadowTriangleDoubleSided", j => MakeByteNode(bin, $"{j}"))));
                    node.Items.Add(MakeArrayNode(bin, "Chunks", j => new BinInterpNode(bin.Position, $"{j}")
                    {
                        Items =
                        {
                            MakeUInt32Node(bin, "BaseVertexIndex"),
                            MakeArrayNode(bin, "RigidVertices", k => new BinInterpNode(bin.Position, $"{k}")
                            {
                                Items =
                                {
                                    MakeVectorNode(bin, "Position"),
                                    MakePackedNormalNode(bin, "TangentX"),
                                    MakePackedNormalNode(bin, "TangentY"),
                                    MakePackedNormalNode(bin, "TangentZ"),
                                    MakeVector2DNode(bin, "UV"),
                                    ListInitHelper.ConditionalAdd(Pcc.Game == MEGame.UDK, () => new ITreeItem[]
                                    {
                                        MakeVector2DNode(bin, "UV2"),
                                        MakeVector2DNode(bin, "UV3"),
                                        MakeVector2DNode(bin, "UV4"),
                                        MakeColorNode(bin, "BoneColor"),
                                    }),
                                    MakeByteNode(bin, "Bone")
                                }
                            }),
                            MakeArrayNode(bin, "SoftVertices", k => new BinInterpNode(bin.Position, $"{k}")
                            {
                                Items =
                                {
                                    MakeVectorNode(bin, "Position"),
                                    MakePackedNormalNode(bin, "TangentX"),
                                    MakePackedNormalNode(bin, "TangentY"),
                                    MakePackedNormalNode(bin, "TangentZ"),
                                    MakeVector2DNode(bin, "UV"),
                                    ListInitHelper.ConditionalAdd(Pcc.Game == MEGame.UDK, () => new ITreeItem[]
                                    {
                                        MakeVector2DNode(bin, "UV2"),
                                        MakeVector2DNode(bin, "UV3"),
                                        MakeVector2DNode(bin, "UV4"),
                                        MakeColorNode(bin, "BoneColor"),
                                    }),
                                    new ListInitHelper.InitCollection<ITreeItem>(ReadList(4, l => MakeByteNode(bin, $"InfluenceBones[{l}]"))),
                                    new ListInitHelper.InitCollection<ITreeItem>(ReadList(4, l => MakeByteNode(bin, $"InfluenceWeights[{l}]")))
                                }
                            }),
                            MakeArrayNode(bin, "BoneMap", k => MakeUInt16Node(bin, $"{k}")),
                            MakeInt32Node(bin, "NumRigidVertices"),
                            MakeInt32Node(bin, "NumSoftVertices"),
                            MakeInt32Node(bin, "MaxBoneInfluences"),
                        }
                    }));
                    node.Items.Add(MakeUInt32Node(bin, "Size"));
                    node.Items.Add(MakeUInt32Node(bin, "NumVertices"));
                    node.Items.Add(ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game != MEGame.UDK, () => MakeArrayNode(bin, "Edges", j => new BinInterpNode(bin.Position, $"{j}")
                    {
                        Items =
                        {
                            MakeInt32Node(bin, "Vertices[0]"),
                            MakeInt32Node(bin, "Vertices[1]"),
                            MakeInt32Node(bin, "Faces[0]"),
                            MakeInt32Node(bin, "Faces[1]"),
                        }
                    })));
                    node.Items.Add(MakeArrayNode(bin, "RequiredBones", j => MakeByteNode(bin, $"{j}")));
                    node.Items.Add(MakeUInt32Node(bin, "RawPointIndices BulkDataFlags"));
                    node.Items.Add(new BinInterpNode(bin.Position, $"RawPointIndices Count: {rawPointIndicesCount = bin.ReadInt32()}"));
                    node.Items.Add(MakeUInt32Node(bin, "RawPointIndices size"));
                    node.Items.Add(MakeUInt32Node(bin, "RawPointIndices file offset"));
                    node.Items.Add(ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game == MEGame.UDK,
                        () => MakeArrayNode(rawPointIndicesCount, bin, "RawPointIndices", k => MakeInt32Node(bin, $"{k}")),
                        () => MakeArrayNode(rawPointIndicesCount, bin, "RawPointIndices", k => MakeUInt16Node(bin, $"{k}"))));
                    node.Items.Add(ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game == MEGame.UDK, () => MakeInt32Node(bin, "NumTexCoords")));
                    BinInterpNode item = new BinInterpNode(bin.Position, "VertexBufferGPUSkin")
                    {
                        IsExpanded = true
                    };
                    node.Items.Add(item);
                    item.Items.Add(ListInitHelper.ConditionalAdd(Pcc.Game != MEGame.ME1, () => new List<ITreeItem>
                    {
                        ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game == MEGame.UDK, () => MakeInt32Node(bin, "NumTexCoords", out numTexCoords)),
                        MakeBoolIntNode(bin, "bUseFullPrecisionUVs", out useFullPrecisionUVs),
                        ListInitHelper.ConditionalAdd(Pcc.Game >= MEGame.ME3, () => new ITreeItem[]
                        {
                            MakeBoolIntNode(bin, "bUsePackedPosition"),
                            MakeVectorNode(bin, "MeshExtension"),
                            MakeVectorNode(bin, "MeshOrigin"),
                        }),
                    }));
                    item.Items.Add(MakeInt32Node(bin, "vertex size"));
                    item.Items.Add(MakeArrayNode(bin, "VertexData", k => new BinInterpNode(bin.Position, $"{k}")
                    {
                        Items =
                        {
                            ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game <= MEGame.ME2, () => MakeVectorNode(bin, "Position")),
                            MakePackedNormalNode(bin, "TangentX"),
                            ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game == MEGame.ME1, () =>  MakePackedNormalNode(bin, "TangentY")),
                            MakePackedNormalNode(bin, "TangentZ"),
                            ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game == MEGame.ME1, () =>  MakeVector2DNode(bin, "UV")),
                            new ListInitHelper.InitCollection<ITreeItem>(ReadList(4, l => MakeByteNode(bin, $"InfluenceBones[{l}]"))),
                            new ListInitHelper.InitCollection<ITreeItem>(ReadList(4, l => MakeByteNode(bin, $"InfluenceWeights[{l}]"))),
                            ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game >= MEGame.ME3, () => MakeVectorNode(bin, "Position")),
                            ListInitHelper.ConditionalAdd(Pcc.Game != MEGame.ME1,
                                () => ListInitHelper.ConditionalAddOne<ITreeItem>(useFullPrecisionUVs,
                                    () => MakeVector2DNode(bin, "UV"),
                                    () => MakeVector2DHalfNode(bin, "UV"))),
                            ListInitHelper.ConditionalAddOne<ITreeItem>(numTexCoords > 1, () => MakeArrayNode(numTexCoords - 1, bin, "Additional UVs",
                                i => useFullPrecisionUVs ? MakeVector2DNode(bin, "UV") : MakeVector2DHalfNode(bin, "UV")))
                        }
                    }));
                    int vertexInfluenceSize;
                    node.Items.Add(ListInitHelper.ConditionalAdd(Pcc.Game >= MEGame.ME3, () => new List<ITreeItem>
                    {
                        new BinInterpNode(bin.Position, $"VertexInfluence size: {vertexInfluenceSize = bin.ReadInt32()}", NodeType.StructLeafInt) { Length = 4 },
                        ListInitHelper.ConditionalAdd<ITreeItem>(vertexInfluenceSize > 0, () => new ITreeItem[]
                        {
                            MakeArrayNode(bin, "VertexInfluences", i => MakeInt32Node(bin, $"{i}")),
                            MakeInt32Node(bin, "Unknown")
                        })
                    }));
                    if (Pcc.Game is MEGame.UDK)
                    {
                        node.Items.Add(MakeBoolIntNode(bin, "NeedsCPUAccess"));
                        node.Items.Add(MakeByteNode(bin, "Datatype size"));
                        node.Items.Add(MakeInt32Node(bin, "index size", out int indexSize));
                        if (indexSize == 4)
                        {
                            node.Items.Add(MakeArrayNode(bin, "Second IndexBuffer?", j => MakeUInt32Node(bin, $"{j}")));
                        }
                        else
                        {
                            node.Items.Add(MakeArrayNode(bin, "Second IndexBuffer?", j => MakeUInt16Node(bin, $"{j}")));
                        }
                    }
                }
                catch (Exception e)
                {
                    node.Items.Add(new BinInterpNode { Header = $"Error reading binary data: {e}" });
                }
                return node;
            }, true));
            subnodes.Add(MakeArrayNode(bin, "NameIndexMap", i => new BinInterpNode(bin.Position, $"{bin.ReadNameReference(Pcc).Instanced} => {bin.ReadInt32()}")));
            subnodes.Add(MakeArrayNode(bin, "PerPolyBoneKDOPs", i => new BinInterpNode(bin.Position, $"{i}")
            {
                Items =
                {
                    MakekDOPTreeNode(bin),
                    MakeArrayNode(bin, "CollisionVerts", j => MakeVectorNode(bin, $"{j}"))
                }
            }));
            if (Pcc.Game >= MEGame.ME3)
            {
                subnodes.Add(MakeArrayNode(bin, "BoneBreakNames", i => new BinInterpNode(bin.Position, $"{i}: {bin.ReadUnrealString()}")));
                subnodes.Add(MakeArrayNode(bin, "ClothingAssets", i => MakeEntryNode(bin, $"{i}", Pcc)));
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }
}
