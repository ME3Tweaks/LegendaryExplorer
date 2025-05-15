using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    public partial class BinaryInterpreterWPF
    {
        private List<ITreeItem> StartForceFeedbackWaveformScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            if (CurrentLoadedExport.Game < MEGame.ME3)
            {
                try
                {
                    var bin = new EndianReader(data) { Endian = CurrentLoadedExport.FileRef.Endian };
                    bin.JumpTo(binarystart);

                    subnodes.Add(MakeBoolIntNode(bin, "bIsLooping"));
                    subnodes.Add(MakeArrayNode(bin, "Samples", i => new BinInterpNode(bin.Position, $"Sample #{i}")
                    {
                        IsExpanded = true,
                        Items =
                        {
                            MakeByteNode(bin, "Left amplitude"),
                            MakeByteNode(bin, "Right amplitude"),
                            MakeByteNode(bin, "Left function"),
                            MakeByteNode(bin, "Right function"),
                            MakeFloatNode(bin, "Duration")
                        }
                    }, true));

                    binarystart = (int)bin.Position;
                }
                catch (Exception ex)
                {
                    subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
                }
            }

            return subnodes;
        }

        private List<ITreeItem> StartRB_BodySetupScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);

                int preCachedPhysDataCount;
                int cachedConvexElementsCount;
                subnodes.Add(new BinInterpNode(bin.Position, $"PreCachedPhysData ({preCachedPhysDataCount = bin.ReadInt32()})")
                {
                    Items = ReadList(preCachedPhysDataCount, i => new BinInterpNode(bin.Position, $"{i} CachedConvexElements ({cachedConvexElementsCount = bin.ReadInt32()})")
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
                    })
                });

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private List<ITreeItem> StartDominantLightScan()
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new EndianReader(CurrentLoadedExport.GetReadOnlyDataStream()) { Endian = CurrentLoadedExport.FileRef.Endian };

                if (Pcc.Game >= MEGame.ME3)
                {
                    int count;
                    subnodes.Add(new BinInterpNode(bin.Position, $"DominantLightShadowMap ({count = bin.ReadInt32()})")
                    {
                        Items = ReadList(count, i => new BinInterpNode(bin.Position, $"{i}: {bin.ReadUInt16()}"))
                    });
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private List<ITreeItem> StartPhysicsAssetInstanceScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);

                int count;
                subnodes.Add(new BinInterpNode(bin.Position, $"CollisionDisableTable ({count = bin.ReadInt32()})")
                {
                    IsExpanded = true,
                    Items = ReadList(count, i => new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items =
                        {
                            MakeInt32Node(bin, "BodyAIndex"),
                            MakeInt32Node(bin, "BodyBIndex"),
                            MakeBoolIntNode(bin, "False")
                        }
                    })
                });

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private List<ITreeItem> StartCookedBulkDataInfoContainerScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);

                int count;
                subnodes.Add(new BinInterpNode(bin.Position, $"Global Mip Data? ({count = bin.ReadInt32()})")
                {
                    IsExpanded = true,
                    Items = ReadList(count, i => new BinInterpNode(bin.Position, $"{bin.BaseStream.ReadStringLatin1Null(bin.ReadInt32())}")
                    {
                        Items =
                        {
                            new BinInterpNode(bin.Position, $"Storage Type: {(StorageTypes)bin.ReadInt32()}", NodeType.StructLeafInt) { Length = 4 },
                            MakeInt32Node(bin, "Uncompressed Size"),
                            MakeInt32Node(bin, "Offset"),
                            MakeInt32Node(bin, "Compressed Size"),
                        }
                    })
                });

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private List<ITreeItem> StartMorphTargetScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);

                subnodes.Add(MakeArrayNode(bin, "MorphLODModels", i => new BinInterpNode(bin.Position, $"{i}")
                {
                    Items =
                    {
                        MakeArrayNode(bin, "Vertices", j => new BinInterpNode(bin.Position, $"{j}")
                        {
                            Items =
                            {
                                MakeVectorNode(bin, "PositionDelta"),
                                MakePackedNormalNode(bin, "TangentZDelta"),
                                MakeUInt16Node(bin, "SourceIdx")
                            }
                        }),
                        MakeInt32Node(bin, "NumBaseMeshVerts")
                    }
                }));
                subnodes.Add(MakeArrayNode(bin, "BoneOffsets", i => new BinInterpNode(bin.Position, $"{i}")
                {
                    Items =
                    {
                        MakeVectorNode(bin, "Offset"),
                        MakeNameNode(bin, "Bone")
                    }
                }));

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private List<ITreeItem> StartShadowMap1DScan(byte[] data, int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);
                if (Pcc.Game.IsGame3())
                {
                    subnodes.Add(new BinInterpNode(bin.Position, $"float size ({bin.ReadInt32()})"));
                }

                int sampleCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Samples ({sampleCount})")
                {
                    Items = ReadList(sampleCount, i => MakeFloatNode(bin, $"{i}"))
                });
                subnodes.Add(new BinInterpNode(bin.Position, $"LightGuid ({bin.ReadGuid()})"));
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private List<ITreeItem> StartPolysScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);

                int polysCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Count: {polysCount}"));
                subnodes.Add(MakeInt32Node(bin, "Max"));
                subnodes.Add(MakeEntryNode(bin, "Owner (self)"));
                if (polysCount > 0)
                {
                    subnodes.Add(new BinInterpNode(bin.Position, $"Elements ({polysCount})")
                    {
                        Items = ReadList(polysCount, i => new BinInterpNode(bin.Position, $"{i}")
                        {
                            Items = new List<ITreeItem>
                            {
                                MakeVectorNode(bin, "Base"),
                                MakeVectorNode(bin, "Normal"),
                                MakeVectorNode(bin, "TextureU"),
                                MakeVectorNode(bin, "TextureV"),
                                new BinInterpNode(bin.Position, $"Vertices ({bin.ReadInt32()})")
                                {
                                    Items = ReadList(bin.Skip(-4).ReadInt32(), j =>
                                                         new BinInterpNode(bin.Position, $"{j}: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()})"))
                                },
                                MakeInt32Node(bin, "PolyFlags"),
                                MakeEntryNode(bin, "Actor"),
                                new BinInterpNode(bin.Position, $"ItemName: {bin.ReadNameReference(Pcc)}"),
                                MakeEntryNode(bin, "Material"),
                                MakeInt32Node(bin, "iLink"),
                                MakeInt32Node(bin, "iBrushPoly"),
                                MakeFloatNode(bin, "ShadowMapScale"),
                                MakeInt32Node(bin, "LightingChannels"),
                                ListInitHelper.ConditionalAdd(Pcc.Game >= MEGame.ME3, () => new ITreeItem[]
                                {
                                    MakeBoolIntNode(bin, "bUseTwoSidedLighting"),
                                    MakeBoolIntNode(bin, "bShadowIndirectOnly"),
                                    MakeFloatNode(bin, "FullyOccludedSamplesFraction"),
                                    MakeBoolIntNode(bin, "bUseEmissiveForStaticLighting"),
                                    MakeFloatNode(bin, "EmissiveLightFalloffExponent"),
                                    MakeFloatNode(bin, "EmissiveLightExplicitInfluenceRadius"),
                                    MakeFloatNode(bin, "EmissiveBoost"),
                                    MakeFloatNode(bin, "DiffuseBoost"),
                                    MakeFloatNode(bin, "SpecularBoost"),
                                    MakeNameNode(bin, "RulesetVariation")
                                }),
                            }
                        })
                    });
                }

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private List<ITreeItem> StartModelScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            //data = data.Slice(binarystart, data.Length - binarystart);
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            try
            {
                //uncomment when removing slice
                bin.JumpTo(binarystart);
                subnodes.Add(MakeBoxSphereBoundsNode(bin, "Bounds"));

                subnodes.Add(MakeInt32Node(bin, "FVector Size"));
                int vectorsCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Vectors ({vectorsCount})")
                {
                    Items = ReadList(vectorsCount, i => new BinInterpNode(bin.Position, $"{i}: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()})"))
                });

                subnodes.Add(MakeInt32Node(bin, "FVector Size"));
                int pointsCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Points ({pointsCount})")
                {
                    Items = ReadList(pointsCount, i => new BinInterpNode(bin.Position, $"{i}: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()})"))
                });

                subnodes.Add(MakeInt32Node(bin, "FBspNode Size"));
                int nodesCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Nodes ({nodesCount})")
                {
                    Items = ReadList(nodesCount, i => new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items = new List<ITreeItem>
                        {
                            new BinInterpNode(bin.Position, $"Plane: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()}, W: {bin.ReadSingle()})"),
                            MakeInt32Node(bin, "iVertPool"),
                            MakeInt32Node(bin, "iSurf"),
                            MakeInt32Node(bin, "iVertexIndex"),
                            new BinInterpNode(bin.Position, $"ComponentIndex: {bin.ReadUInt16()}"),
                            new BinInterpNode(bin.Position, $"ComponentNodeIndex: {bin.ReadUInt16()}"),
                            MakeInt32Node(bin, "ComponentElementIndex"),
                            MakeInt32Node(bin, "iBack"),
                            MakeInt32Node(bin, "iFront"),
                            MakeInt32Node(bin, "iPlane"),
                            MakeInt32Node(bin, "iCollisionBound"),
                            new BinInterpNode(bin.Position, $"iZone[0]: {bin.ReadByte()}"),
                            new BinInterpNode(bin.Position, $"iZone[1]: {bin.ReadByte()}"),
                            new BinInterpNode(bin.Position, $"NumVertices: {bin.ReadByte()}"),
                            new BinInterpNode(bin.Position, $"NodeFlags: {bin.ReadByte()}"),
                            MakeInt32Node(bin, "iLeaf[0]"),
                            new BinInterpNode(bin.Position, $"iLeaf[1]: {bin.ReadInt32()}")
                        }
                    })
                });

                subnodes.Add(MakeEntryNode(bin, "Owner (self)"));
                int surfsCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Surfaces ({surfsCount})")
                {
                    Items = ReadList(surfsCount, i => new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items = new List<ITreeItem>
                        {
                            MakeEntryNode(bin, "Material"),
                            MakeInt32Node(bin, "PolyFlags"),
                            MakeInt32Node(bin, "pBase"),
                            MakeInt32Node(bin, "vNormal"),
                            MakeInt32Node(bin, "vTextureU"),
                            MakeInt32Node(bin, "vTextureV"),
                            MakeInt32Node(bin, "iBrushPoly"),
                            MakeEntryNode(bin, "Actor"),
                            new BinInterpNode(bin.Position, $"Plane: (X: {bin.ReadSingle()}, Y: {bin.ReadSingle()}, Z: {bin.ReadSingle()}, W: {bin.ReadSingle()})"),
                            MakeFloatNode(bin, "ShadowMapScale"),
                            MakeInt32Node(bin, "LightingChannels(Bitfield)"),
                            Pcc.Game >= MEGame.ME3 ? new BinInterpNode(bin.Position, $"iLightmassIndex: {bin.ReadInt32()}") : null,
                        }.NonNull().ToList()
                    })
                });

                int fVertSize = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"FVert Size: {fVertSize}"));
                int vertsCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Verts ({vertsCount})")
                {
                    Items = ReadList(vertsCount, i => new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items = new List<ITreeItem>
                        {
                            MakeInt32Node(bin, "pVertex"),
                            MakeInt32Node(bin, "iSide"),
                            MakeVector2DNode(bin, "ShadowTexCoord"),
                            fVertSize == 24 ? MakeVector2DNode(bin, "BackfaceShadowTexCoord") : null
                        }.NonNull().ToList()
                    })
                });

                subnodes.Add(MakeInt32Node(bin, "NumSharedSides"));
                int numZones = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"NumZones: {numZones}")
                {
                    Items = ReadList(numZones, i => new BinInterpNode(bin.Position, $"Zone {i}")
                    {
                        Items = new List<ITreeItem>
                        {
                            MakeEntryNode(bin, "ZoneActor"),
                            MakeFloatNode(bin, "LastRenderTime"),
                            new BinInterpNode(bin.Position, $"Connectivity: {Convert.ToString(bin.ReadInt64(), 2).PadLeft(64, '0')}"),
                            new BinInterpNode(bin.Position, $"Visibility: {Convert.ToString(bin.ReadInt64(), 2).PadLeft(64, '0')}"),
                        }
                    })
                });

                subnodes.Add(MakeEntryNode(bin, "Polys"));
                subnodes.Add(MakeInt32Node(bin, "integer Size"));
                int leafHullsCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"LeafHulls ({leafHullsCount})")
                {
                    Items = ReadList(leafHullsCount, i => MakeInt32Node(bin, $"{i}"))
                });

                subnodes.Add(MakeInt32Node(bin, "FLeaf Size"));
                int leavesCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Leaves ({leavesCount})")
                {
                    Items = ReadList(leavesCount, i => MakeInt32Node(bin, $"{i}: iZone"))
                });

                subnodes.Add(MakeBoolIntNode(bin, "RootOutside"));
                subnodes.Add(MakeBoolIntNode(bin, "Linked"));

                subnodes.Add(MakeInt32Node(bin, "integer Size"));
                int portalNodesCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"PortalNodes ({portalNodesCount})")
                {
                    Items = ReadList(portalNodesCount, i => MakeInt32Node(bin, $"{i}"))
                });

                if (Pcc.Game < MEGame.UDK)
                {
                    subnodes.Add(MakeInt32Node(bin, "FMeshEdge Size"));
                    int legacyedgesCount = bin.ReadInt32();
                    subnodes.Add(new BinInterpNode(bin.Position - 4, $"ShadowVolume? ({legacyedgesCount})")
                    {
                        Items = ReadList(legacyedgesCount, i => new BinInterpNode(bin.Position, $"MeshEdge {i}")
                        {
                            Items = new List<ITreeItem>
                            {
                                MakeInt32Node(bin, "Vertices[0]"),
                                MakeInt32Node(bin, "Vertices[1]"),
                                MakeInt32Node(bin, "Faces[0]"),
                                new BinInterpNode(bin.Position, $"Faces[1]: {bin.ReadInt32()}")
                            }
                        })
                    });
                }

                subnodes.Add(MakeUInt32Node(bin, "NumVertices:"));

                subnodes.Add(MakeInt32Node(bin, "FModelVertex Size"));
                int verticesCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"VertexBuffer Vertices({verticesCount})")
                {
                    Items = ReadList(verticesCount, i => new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items = new List<ITreeItem>
                        {
                            MakeVectorNode(bin, "Position"),
                            MakePackedNormalNode(bin, "TangentX"),
                            MakePackedNormalNode(bin, "TangentZ"),
                            MakeVector2DNode(bin, "TexCoord"),
                            MakeVector2DNode(bin, "ShadowTexCoord")
                        }
                    })
                });

                if (Pcc.Game >= MEGame.ME3)
                {
                    subnodes.Add(new BinInterpNode(bin.Position, $"LightingGuid: {bin.ReadGuid()}") { Length = 16 });

                    int lightmassSettingsCount = bin.ReadInt32();
                    subnodes.Add(new BinInterpNode(bin.Position - 4, $"LightmassSettings ({lightmassSettingsCount})")
                    {
                        Items = ReadList(lightmassSettingsCount, i => new BinInterpNode(bin.Position, $"{i}")
                        {
                            Items = new List<ITreeItem>
                            {
                                MakeBoolIntNode(bin, "bUseTwoSidedLighting"),
                                MakeBoolIntNode(bin, "bShadowIndirectOnly"),
                                MakeFloatNode(bin, "FullyOccludedSamplesFraction"),
                                MakeBoolIntNode(bin, "bUseEmissiveForStaticLighting"),
                                MakeFloatNode(bin, "EmissiveLightFalloffExponent"),
                                MakeFloatNode(bin, "EmissiveLightExplicitInfluenceRadius"),
                                MakeFloatNode(bin, "EmissiveBoost"),
                                MakeFloatNode(bin, "DiffuseBoost"),
                                new BinInterpNode(bin.Position, $"SpecularBoost: {bin.ReadSingle()}")
                            }
                        })
                    });
                }

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> StartWorldScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);

                subnodes.Add(MakeEntryNode(bin, "PersistentLevel"));
                if (Pcc.Game == MEGame.ME3 || Pcc.Game.IsLEGame())
                {
                    subnodes.Add(MakeEntryNode(bin, "PersistentFaceFXAnimSet"));
                }
                subnodes.AddRange(ReadList(4, i => new BinInterpNode(bin.Position, $"EditorView {i}")
                {
                    Items =
                    {
                        MakeVectorNode(bin, "CamPosition"),
                        new BinInterpNode(bin.Position, $"CamRotation: (Pitch: {bin.ReadInt32()}, Yaw: {bin.ReadInt32()}, Roll: {bin.ReadInt32()})"),
                        new BinInterpNode(bin.Position, $"CamOrthoZoom: {bin.ReadSingle()}")
                    }
                }));
                if (Pcc.Game == MEGame.UDK)
                {
                    subnodes.Add(MakeFloatNode(bin, "unkFloat"));
                }
                subnodes.Add(MakeEntryNode(bin, "Null"));
                if (Pcc.Game is MEGame.ME1 or MEGame.LE1)
                {
                    subnodes.Add(MakeEntryNode(bin, "DecalManager"));
                }

                int extraObjsCount;
                subnodes.Add(new BinInterpNode(bin.Position, $"ExtraReferencedObjects: {extraObjsCount = bin.ReadInt32()}")
                {
                    ArrayAddAlgorithm = BinInterpNode.ArrayPropertyChildAddAlgorithm.FourBytes,
                    Items = ReadList(extraObjsCount, i => new BinInterpNode(bin.Position, $"{entryRefString(bin)}", NodeType.ArrayLeafObject))
                });

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private List<ITreeItem> StartStackScan(out int endPos)
        {
            endPos = 0;
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new EndianReader(CurrentLoadedExport.GetReadOnlyDataStream()) { Endian = CurrentLoadedExport.FileRef.Endian };
                
                var item = new BinInterpNode(bin.Position, "Stack")
                {
                    IsExpanded = true
                };
                List<ITreeItem> items = item.Items;
                items.Add(MakeEntryNode(bin, "Node", out int uIndex));
                if (Pcc.Game is not MEGame.UDK)
                {
                    items.Add(MakeEntryNode(bin, "StateNode"));
                }
                items.Add(new BinInterpNode(bin.Position, $"ProbeMask: {bin.ReadUInt64():X16}"));
                if (Pcc.Game >= MEGame.ME3 || Pcc.Platform is MEPackage.GamePlatform.PS3)
                {
                    items.Add(MakeUInt16Node(bin, "LatentAction"));
                }
                else
                {
                    items.Add(MakeUInt32Node(bin, "LatentAction"));
                }
                items.Add(MakeArrayNode(bin, "StateStack", i => new BinInterpNode(bin.Position, $"{i}")
                {
                    Items =
                    {
                        MakeEntryNode(bin, "State"),
                        MakeEntryNode(bin, "Node"),
                        MakeInt32Node(bin, "Offset")
                    }
                }));
                if (uIndex is not 0)
                {
                    items.Add(MakeInt32Node(bin, "Offset"));
                }
                subnodes.Add(item);
                endPos = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private List<ITreeItem> StartMetaDataScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new EndianReader(data) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);
                subnodes.Add(MakeArrayNode(bin, "Object to Metadata Map", i =>
                {
                    var node = Pcc.Game is MEGame.UDK ? MakeEntryNode(bin, "Object") : MakeStringNode(bin, "Object");
                    node.IsExpanded = true;
                    int count = bin.ReadInt32();
                    while (count-- > 0)
                    {
                        var metadataType = bin.ReadNameReference(Pcc);
                        node.Items.Add(MakeStringNode(bin, metadataType.Instanced));
                    }
                    return node;
                }, true));
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> StartTextBufferScan(byte[] data, int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new EndianReader(data) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);
                subnodes.Add(MakeInt32Node(bin, "Position"));
                subnodes.Add(MakeInt32Node(bin, "Top"));
                subnodes.Add(MakeStringNode(bin, "Text"));
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> StartAnimSequenceScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();

            #region UDK AKF_PerTrackCompression

            if (Pcc.Game == MEGame.UDK && CurrentLoadedExport.GetProperty<EnumProperty>("KeyEncodingFormat")?.Value.Name == "AKF_PerTrackCompression")
            {
                try
                {
                    var TrackOffsets = CurrentLoadedExport.GetProperty<ArrayProperty<IntProperty>>("CompressedTrackOffsets");
                    var numFrames = CurrentLoadedExport.GetProperty<IntProperty>("NumFrames")?.Value ?? 0;

                    List<string> boneList = ((ExportEntry)CurrentLoadedExport.Parent).GetProperty<ArrayProperty<NameProperty>>("TrackBoneNames").Select(np => $"{np}").ToList();

                    var bin = new EndianReader(new MemoryStream(data)) { Endian = Pcc.Endian };
                    bin.JumpTo(binarystart);

                    int numTracks = bin.ReadInt32() * 2;
                    bin.Skip(-4);

                    BinInterpNode rawAnimDataNode = MakeInt32Node(bin, "RawAnimationData: NumTracks");
                    subnodes.Add(rawAnimDataNode);
                    for (int i = 0; i < numTracks; i++)
                    {
                        int keySize = bin.ReadInt32();
                        int numKeys = bin.ReadInt32();
                        for (int j = 0; j < numKeys; j++)
                        {
                            if (keySize == 12)
                            {
                                rawAnimDataNode.Items.Add(MakeVectorNode(bin, $"{boneList[i / 2]}, PosKey {j}"));
                            }
                            else if (keySize == 16)
                            {
                                rawAnimDataNode.Items.Add(MakeQuatNode(bin, $"{boneList[i / 2]}, RotKey {j}"));
                            }
                            else
                            {
                                throw new NotImplementedException($"Unexpected key size: {keySize}");
                            }
                        }
                    }

                    subnodes.Add(MakeInt32Node(bin, "AnimBinary length"));
                    var startOff = bin.Position;
                    for (int i = 0; i < boneList.Count; i++)
                    {
                        var boneNode = new BinInterpNode(bin.Position, boneList[i]);
                        subnodes.Add(boneNode);

                        int posOff = TrackOffsets[i * 2];

                        if (posOff >= 0)
                        {
                            bin.JumpTo(startOff + posOff);
                            int header = bin.ReadInt32();
                            int numKeys = header & 0x00FFFFFF;
                            int formatFlags = (header >> 24) & 0x0F;
                            AnimationCompressionFormat keyFormat = (AnimationCompressionFormat)((header >> 28) & 0x0F);

                            boneNode.Items.Add(new BinInterpNode(bin.Position - 4, $"PosKey Header: {numKeys} keys, Compression: {keyFormat}, FormatFlags:{formatFlags:X}") { Length = 4 });

                            for (int j = 0; j < numKeys; j++)
                            {
                                switch (keyFormat)
                                {
                                    case AnimationCompressionFormat.ACF_None:
                                    case AnimationCompressionFormat.ACF_Float96NoW:
                                        if ((formatFlags & 7) == 0)
                                        {
                                            boneNode.Items.Add(MakeVectorNode(bin, $"PosKey {j}"));
                                        }
                                        else
                                        {
                                            int binPosition = (int)bin.Position;
                                            int keyLength = 4 * ((formatFlags & 1) + ((formatFlags >> 1) & 1) + ((formatFlags >> 2) & 1));
                                            float x = (formatFlags & 1) != 0 ? bin.ReadFloat() : 0,
                                                  y = (formatFlags & 2) != 0 ? bin.ReadFloat() : 0,
                                                  z = (formatFlags & 4) != 0 ? bin.ReadFloat() : 0;
                                            boneNode.Items.Add(new BinInterpNode(binPosition, $"PosKey {j}: (X: {x}, Y: {y}, Z: {z})")
                                            {
                                                Length = keyLength
                                            });
                                        }
                                        break;
                                    case AnimationCompressionFormat.ACF_Fixed48NoW:
                                    case AnimationCompressionFormat.ACF_IntervalFixed32NoW:
                                    case AnimationCompressionFormat.ACF_Fixed32NoW:
                                    case AnimationCompressionFormat.ACF_Float32NoW:
                                    case AnimationCompressionFormat.ACF_BioFixed48:
                                    default:
                                        throw new NotImplementedException($"{keyFormat} is not supported yet!");
                                }
                            }
                        }

                        int rotOff = TrackOffsets[i * 2 + 1];

                        if (rotOff >= 0)
                        {
                            bin.JumpTo(startOff + rotOff);
                            int header = bin.ReadInt32();
                            int numKeys = header & 0x00FFFFFF;
                            int formatFlags = (header >> 24) & 0x0F;
                            AnimationCompressionFormat keyFormat = (AnimationCompressionFormat)((header >> 28) & 0x0F);

                            boneNode.Items.Add(new BinInterpNode(bin.Position - 4, $"RotKey Header: {numKeys} keys, Compression: {keyFormat}, FormatFlags:{formatFlags:X}") { Length = 4 });
                            switch (keyFormat)
                            {
                                case AnimationCompressionFormat.ACF_None:
                                    {
                                        for (int j = 0; j < numKeys; j++)
                                        {
                                            boneNode.Items.Add(MakeQuatNode(bin, $"RotKey {j}"));
                                        }
                                        break;
                                    }
                                case AnimationCompressionFormat.ACF_Fixed48NoW:
                                    {
                                        const float scale = 32767.0f;
                                        const ushort unkConst = 32767;
                                        int keyLength = 2 * ((formatFlags & 1) + ((formatFlags >> 1) & 1) + ((formatFlags >> 2) & 1));
                                        for (int j = 0; j < numKeys; j++)
                                        {
                                            int binPosition = (int)bin.Position;
                                            float x = (formatFlags & 1) != 0 ? (bin.ReadUInt16() - unkConst) / scale : 0,
                                                  y = (formatFlags & 2) != 0 ? (bin.ReadUInt16() - unkConst) / scale : 0,
                                                  z = (formatFlags & 4) != 0 ? (bin.ReadUInt16() - unkConst) / scale : 0;
                                            boneNode.Items.Add(new BinInterpNode(binPosition, $"RotKey {j}: (X: {x}, Y: {y}, Z: {z}, W: {getW(x, y, z)})")
                                            {
                                                Length = keyLength
                                            });
                                        }
                                        break;
                                    }
                                case AnimationCompressionFormat.ACF_Float96NoW:
                                    {
                                        float x, y, z;
                                        for (int j = 0; j < numKeys; j++)
                                        {
                                            boneNode.Items.Add(new BinInterpNode(bin.Position, $"RotKey {j}: (X: {x = bin.ReadFloat()}, Y: {y = bin.ReadFloat()}, Z: {z = bin.ReadFloat()}, W: {getW(x, y, z)})")
                                            {
                                                Length = 12
                                            });
                                        }
                                        break;
                                    }
                                case AnimationCompressionFormat.ACF_IntervalFixed32NoW:
                                case AnimationCompressionFormat.ACF_Fixed32NoW:
                                case AnimationCompressionFormat.ACF_Float32NoW:
                                case AnimationCompressionFormat.ACF_BioFixed48:
                                default:
                                    throw new NotImplementedException($"{keyFormat} is not supported yet!");
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

            #endregion

            try
            {
                var TrackOffsets = CurrentLoadedExport.GetProperty<ArrayProperty<IntProperty>>("CompressedTrackOffsets");
                var numFrames = CurrentLoadedExport.GetProperty<IntProperty>("NumFrames")?.Value ?? 0;

                List<string> boneList;
                if (Pcc.Game == MEGame.UDK)
                {
                    boneList = ((ExportEntry)CurrentLoadedExport.Parent)?.GetProperty<ArrayProperty<NameProperty>>("TrackBoneNames")?.Select(np => $"{np}").ToList();
                }
                else
                {
                    var animsetData = CurrentLoadedExport.GetProperty<ObjectProperty>("m_pBioAnimSetData");
                    //In ME2, BioAnimSetData can sometimes be in a different package. 
                    boneList = animsetData != null && Pcc.IsUExport(animsetData.Value)
                        ? Pcc.GetUExport(animsetData.Value).GetProperty<ArrayProperty<NameProperty>>("TrackBoneNames")?.Select(np => $"{np}").ToList()
                        : null;
                }

                boneList ??= Enumerable.Repeat("???", TrackOffsets.Count / 4).ToList();
                Enum.TryParse(CurrentLoadedExport.GetProperty<EnumProperty>("KeyEncodingFormat")?.Value.Name, out AnimationKeyFormat keyEncoding);
                Enum.TryParse(CurrentLoadedExport.GetProperty<EnumProperty>("RotationCompressionFormat")?.Value.Name, out AnimationCompressionFormat rotCompression);
                Enum.TryParse(CurrentLoadedExport.GetProperty<EnumProperty>("TranslationCompressionFormat")?.Value.Name, out AnimationCompressionFormat posCompression);

                var bin = new EndianReader(new MemoryStream(data)) { Endian = Pcc.Endian };
                bin.JumpTo(binarystart);
                if (Pcc.Game is MEGame.ME2 or MEGame.LE2 && Pcc.Platform != MEPackage.GamePlatform.PS3)
                {
                    bin.Skip(12);
                    subnodes.Add(MakeInt32Node(bin, "AnimBinary Offset"));
                }
                else if (Pcc.Game == MEGame.UDK)
                {
                    int numTracks = bin.ReadInt32() * 2;
                    bin.Skip(-4);

                    BinInterpNode rawAnimDataNode = MakeInt32Node(bin, "RawAnimationData: NumTracks");
                    subnodes.Add(rawAnimDataNode);
                    for (int i = 0; i < numTracks; i++)
                    {
                        int keySize = bin.ReadInt32();
                        int numKeys = bin.ReadInt32();
                        for (int j = 0; j < numKeys; j++)
                        {
                            if (keySize == 12)
                            {
                                rawAnimDataNode.Items.Add(MakeVectorNode(bin, $"{boneList[i / 2]}, PosKey {j}"));
                            }
                            else if (keySize == 16)
                            {
                                rawAnimDataNode.Items.Add(MakeQuatNode(bin, $"{boneList[i / 2]}, RotKey {j}"));
                            }
                            else
                            {
                                throw new NotImplementedException($"Unexpected key size: {keySize}");
                            }
                        }
                    }
                }

                subnodes.Add(MakeInt32Node(bin, "AnimBinary length"));
                var startOffset = bin.Position;
                for (int i = 0; i < boneList.Count; i++)
                {
                    var boneNode = new BinInterpNode(bin.Position, boneList[i]);
                    subnodes.Add(boneNode);

                    int posOff = TrackOffsets[i * 4];
                    int posKeys = TrackOffsets[i * 4 + 1];
                    int rotOff = TrackOffsets[i * 4 + 2];
                    int rotKeys = TrackOffsets[i * 4 + 3];

                    if (posKeys > 0)
                    {
                        bin.JumpTo(startOffset + posOff);

                        AnimationCompressionFormat compressionFormat = posCompression;

                        if (posKeys == 1)
                        {
                            compressionFormat = AnimationCompressionFormat.ACF_None;
                        }
                        for (int j = 0; j < posKeys; j++)
                        {
                            BinInterpNode posKeyNode;
                            switch (compressionFormat)
                            {
                                case AnimationCompressionFormat.ACF_None:
                                case AnimationCompressionFormat.ACF_Float96NoW:
                                    posKeyNode = MakeVectorNode(bin, $"PosKey {j}");
                                    break;
                                case AnimationCompressionFormat.ACF_IntervalFixed32NoW:
                                case AnimationCompressionFormat.ACF_Fixed48NoW:
                                case AnimationCompressionFormat.ACF_Fixed32NoW:
                                case AnimationCompressionFormat.ACF_Float32NoW:
                                case AnimationCompressionFormat.ACF_BioFixed48:
                                default:
                                    throw new NotImplementedException($"Translation keys in format {compressionFormat} cannot be read!");
                            }
                            boneNode.Items.Add(posKeyNode);
                        }

                        readTrackTable(posKeys);
                    }

                    if (rotKeys > 0)
                    {
                        bin.JumpTo(startOffset + rotOff);

                        AnimationCompressionFormat compressionFormat = rotCompression;

                        if (rotKeys == 1)
                        {
                            compressionFormat = AnimationCompressionFormat.ACF_Float96NoW;
                        }
                        else if (Pcc.Game != MEGame.UDK)
                        {
                            boneNode.Items.Add(MakeVectorNode(bin, "Mins"));
                            boneNode.Items.Add(MakeVectorNode(bin, "Ranges"));
                        }

                        for (int j = 0; j < rotKeys; j++)
                        {
                            BinInterpNode rotKeyNode;
                            switch (compressionFormat)
                            {
                                case AnimationCompressionFormat.ACF_None:
                                    rotKeyNode = MakeQuatNode(bin, $"RotKey {j}");
                                    break;
                                case AnimationCompressionFormat.ACF_Float96NoW:
                                    {
                                        float x, y, z;
                                        rotKeyNode = new BinInterpNode(bin.Position, $"RotKey {j}: (X: {x = bin.ReadFloat()}, Y: {y = bin.ReadFloat()}, Z: {z = bin.ReadFloat()}, W: {getW(x, y, z)})")
                                        {
                                            Length = 12
                                        };
                                        break;
                                    }
                                case AnimationCompressionFormat.ACF_BioFixed48:
                                    {
                                        const float shift = 0.70710678118f;
                                        const float scale = 1.41421356237f;
                                        const float precisionMult = 32767.0f;
                                        var pos = bin.Position;
                                        ushort a = bin.ReadUInt16();
                                        ushort b = bin.ReadUInt16();
                                        ushort c = bin.ReadUInt16();
                                        float x = (a & 0x7FFF) / precisionMult * scale - shift;
                                        float y = (b & 0x7FFF) / precisionMult * scale - shift;
                                        float z = (c & 0x7FFF) / precisionMult * scale - shift;
                                        float w = getW(x, y, z);
                                        int wPos = ((a >> 14) & 2) | ((b >> 15) & 1);
                                        var rot = wPos switch
                                        {
                                            0 => new Quaternion(w, x, y, z),
                                            1 => new Quaternion(x, w, y, z),
                                            2 => new Quaternion(x, y, w, z),
                                            _ => new Quaternion(x, y, z, w)
                                        };
                                        rotKeyNode = new BinInterpNode(pos, $"RotKey {j}: (X: {rot.X}, Y: {rot.Y}, Z: {rot.Z}, W: {rot.W})")
                                        {
                                            Length = 6
                                        };
                                        break;
                                    }
                                case AnimationCompressionFormat.ACF_Fixed48NoW:
                                case AnimationCompressionFormat.ACF_IntervalFixed32NoW:
                                case AnimationCompressionFormat.ACF_Fixed32NoW:
                                case AnimationCompressionFormat.ACF_Float32NoW:
                                default:
                                    throw new NotImplementedException($"Rotation keys in format {compressionFormat} cannot be read!");
                            }
                            boneNode.Items.Add(rotKeyNode);
                        }

                        readTrackTable(rotKeys);
                    }

                    void readTrackTable(int numKeys)
                    {
                        if (keyEncoding == AnimationKeyFormat.AKF_VariableKeyLerp && numKeys > 1)
                        {
                            bin.JumpTo((bin.Position - startOffset).Align(4) + startOffset);
                            var trackTable = new BinInterpNode(bin.Position, "TrackTable");
                            boneNode.Items.Add(trackTable);

                            for (int j = 0; j < numKeys; j++)
                            {
                                trackTable.Items.Add(numFrames > 0xFF ? MakeUInt16Node(bin, $"{j}") : MakeByteNode(bin, $"{j}"));
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

            static float getW(float x, float y, float z)
            {
                float wSquared = 1.0f - (x * x + y * y + z * z);
                return (float)(wSquared > 0 ? Math.Sqrt(wSquared) : 0);
            }
        }

        private List<ITreeItem> StartObjectRedirectorScan(byte[] data, ref int binaryStart)
        {
            var subnodes = new List<ITreeItem>();
            var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.Skip(binaryStart);
            subnodes.Add(MakeEntryNode(bin, "Redirect references to this export to"));
            return subnodes;
        }

        private List<ITreeItem> StartGuidCacheScan(byte[] data, ref int binarystart)
        {
            /*
             *  
             *  count +4
             *      nameentry +8
             *      guid +16
             *      
             */
            var subnodes = new List<ITreeItem>();

            try
            {
                int pos = binarystart;
                int count = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"{(pos - binarystart):X4} count: {count}",
                    Offset = pos,
                });
                pos += 4;
                for (int i = 0; i < count && pos < data.Length; i++)
                {
                    int nameRef = BitConverter.ToInt32(data, pos);
                    int nameIdx = BitConverter.ToInt32(data, pos + 4);
                    Guid guid = new Guid(data.Skip(pos + 8).Take(16).ToArray());
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"{(pos - binarystart):X4} {new NameReference(CurrentLoadedExport.FileRef.GetNameEntry(nameRef), nameIdx).Instanced}: {{{guid}}}",
                        Offset = pos,

                        Tag = NodeType.StructLeafName
                    });
                    //Debug.WriteLine($"{pos:X4} {new NameReference(CurrentLoadedExport.FileRef.getNameEntry(nameRef), nameIdx).Instanced}: {{{guid}}}");
                    pos += 24;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> StartLevelScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);

                subnodes.Add(MakeEntryNode(bin, "Self"));
                int actorsCount;
                BinInterpNode levelActorsNode;
                subnodes.Add(levelActorsNode = new BinInterpNode(bin.Position, $"Level Actors: ({actorsCount = bin.ReadInt32()})", NodeType.StructLeafInt)
                {
                    ArrayAddAlgorithm = BinInterpNode.ArrayPropertyChildAddAlgorithm.FourBytes,
                    IsExpanded = true
                });
                levelActorsNode.Items = ReadList(actorsCount, i => new BinInterpNode(bin.Position, $"{i}: {entryRefString(bin)}", NodeType.ArrayLeafObject)
                {
                    ArrayAddAlgorithm = BinInterpNode.ArrayPropertyChildAddAlgorithm.FourBytes,
                    Parent = levelActorsNode,
                });

                subnodes.Add(new BinInterpNode(bin.Position, "URL")
                {
                    Items =
                    {
                        MakeStringNode(bin, "Protocol"),
                        MakeStringNode(bin, "Host"),
                        MakeStringNode(bin, "Map"),
                        MakeStringNode(bin, "Portal"),
                        new BinInterpNode(bin.Position, $"Op: ({bin.ReadInt32()} items)")
                        {
                            Items = ReadList(bin.Skip(-4).ReadInt32(), i => MakeStringNode(bin, $"{i}"))
                        },
                        MakeInt32Node(bin, "Port"),
                        new BinInterpNode(bin.Position, $"Valid: {bin.ReadInt32()}")
                    }
                });
                subnodes.Add(MakeEntryNode(bin, "Model"));
                int modelcomponentsCount;
                subnodes.Add(new BinInterpNode(bin.Position, $"ModelComponents: ({modelcomponentsCount = bin.ReadInt32()})")
                {
                    Items = ReadList(modelcomponentsCount, i => MakeEntryNode(bin, $"{i}"))
                });
                int sequencesCount;
                subnodes.Add(new BinInterpNode(bin.Position, $"GameSequences: ({sequencesCount = bin.ReadInt32()})")
                {
                    Items = ReadList(sequencesCount, i => MakeEntryNode(bin, $"{i}"))
                });
                int texToInstCount;
                int streamableTexInstCount;
                subnodes.Add(new BinInterpNode(bin.Position, $"TextureToInstancesMap: ({texToInstCount = bin.ReadInt32()})")
                {
                    Items = ReadList(texToInstCount, i =>
                                         new BinInterpNode(bin.Position, $"{entryRefString(bin)}: ({streamableTexInstCount = bin.ReadInt32()} StreamableTextureInstances)")
                                         {
                                             Items = ReadList(streamableTexInstCount, j => new BinInterpNode(bin.Position, $"{j}")
                                             {
                                                 IsExpanded = true,
                                                 Items =
                            {
                                new BinInterpNode(bin.Position, "BoundingSphere")
                                {
                                    IsExpanded = true,
                                    Items =
                                    {
                                        MakeVectorNode(bin, "Center"),
                                        new BinInterpNode(bin.Position, $"Radius: {bin.ReadSingle()}")
                                    }
                                },
                                new BinInterpNode(bin.Position, $"TexelFactor: {bin.ReadSingle()}")
                            }
                                             })
                                         })
                });
                if (Pcc.Game == MEGame.UDK)
                {
                    subnodes.Add(MakeArrayNode(bin, "MeshesComponentsWithDynamicLighting?",
                                               i => new BinInterpNode(bin.Position, $"{i}: {entryRefString(bin)}, {bin.ReadInt32()}")));
                }

                if (Pcc.Game >= MEGame.ME3)
                {
                    int apexSize;
                    subnodes.Add(new BinInterpNode(bin.Position, $"APEX Size: {apexSize = bin.ReadInt32()}"));
                    //should always be zero, but just in case...
                    if (apexSize > 0)
                    {
                        subnodes.Add(new BinInterpNode(bin.Position, $"APEX mesh?: {apexSize} bytes") { Length = apexSize });
                        bin.Skip(apexSize);
                    }
                }

                int cachedPhysBSPDataSize;
                subnodes.Add(MakeInt32Node(bin, "size of byte"));
                subnodes.Add(new BinInterpNode(bin.Position, $"CachedPhysBSPData Size: {cachedPhysBSPDataSize = bin.ReadInt32()}"));
                if (cachedPhysBSPDataSize > 0)
                {
                    subnodes.Add(new BinInterpNode(bin.Position, $"CachedPhysBSPData: {cachedPhysBSPDataSize} bytes") { Length = cachedPhysBSPDataSize });
                    bin.Skip(cachedPhysBSPDataSize);
                }

                int cachedPhysSMDataMapCount;
                subnodes.Add(new BinInterpNode(bin.Position, $"CachedPhysSMDataMap: ({cachedPhysSMDataMapCount = bin.ReadInt32()})")
                {
                    Items = ReadList(cachedPhysSMDataMapCount, i => new BinInterpNode(bin.Position, $"{entryRefString(bin)}")
                    {
                        Items =
                        {
                            MakeVectorNode(bin, "Scale3D"),
                            new BinInterpNode(bin.Position, $"CachedDataIndex: {bin.ReadInt32()}")
                        }
                    })
                });

                int cachedPhysSMDataStoreCount;
                int cachedConvexElementsCount;
                subnodes.Add(new BinInterpNode(bin.Position, $"CachedPhysSMDataStore: ({cachedPhysSMDataStoreCount = bin.ReadInt32()})")
                {
                    Items = ReadList(cachedPhysSMDataStoreCount, i => new BinInterpNode(bin.Position, $"{i}: CachedConvexElements ({cachedConvexElementsCount = bin.ReadInt32()})")
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
                    })
                });

                int cachedPhysPerTriSMDataMapCount;
                subnodes.Add(new BinInterpNode(bin.Position, $"CachedPhysPerTriSMDataMap: ({cachedPhysPerTriSMDataMapCount = bin.ReadInt32()})")
                {
                    Items = ReadList(cachedPhysPerTriSMDataMapCount, i => new BinInterpNode(bin.Position, $"{entryRefString(bin)}")
                    {
                        Items =
                        {
                            MakeVectorNode(bin, "Scale3D"),
                            new BinInterpNode(bin.Position, $"CachedDataIndex: {bin.ReadInt32()}")
                        }
                    })
                });

                int cachedPhysPerTriSMDataStoreCount;
                subnodes.Add(new BinInterpNode(bin.Position, $"CachedPhysPerTriSMDataStore: ({cachedPhysPerTriSMDataStoreCount = bin.ReadInt32()})")
                {
                    Items = ReadList(cachedPhysPerTriSMDataStoreCount, j =>
                    {
                        int size;
                        var item = new BinInterpNode(bin.Position, $"{j}: CachedPerTriData (size of byte: {bin.ReadInt32()}) (number of bytes: {size = bin.ReadInt32()})")
                        {
                            Length = size + 8
                        };
                        bin.Skip(size);
                        return item;
                    })
                });

                subnodes.Add(MakeInt32Node(bin, "CachedPhysBSPDataVersion"));
                subnodes.Add(MakeInt32Node(bin, "CachedPhysSMDataVersion"));

                int forceStreamTexturesCount;
                subnodes.Add(new BinInterpNode(bin.Position, $"ForceStreamTextures: ({forceStreamTexturesCount = bin.ReadInt32()})")
                {
                    Items = ReadList(forceStreamTexturesCount, i => MakeBoolIntNode(bin, $"Texture: {entryRefString(bin)} | ForceStream"))
                });

                if (Pcc.Game == MEGame.UDK)
                {
                    subnodes.Add(new BinInterpNode(bin.Position, "CachedPhysConvexBSPData")
                    {
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
                    subnodes.Add(MakeInt32Node(bin, "CachedPhysConvexBSPVersion"));
                }

                subnodes.Add(MakeEntryNode(bin, "NavListStart"));
                subnodes.Add(MakeEntryNode(bin, "NavListEnd"));
                subnodes.Add(MakeEntryNode(bin, "CoverListStart"));
                subnodes.Add(MakeEntryNode(bin, "CoverListEnd"));
                if (Pcc.Game >= MEGame.ME3)
                {
                    subnodes.Add(MakeEntryNode(bin, "PylonListStart"));
                    subnodes.Add(MakeEntryNode(bin, "PylonListEnd"));
                }
                if (Pcc.Game is MEGame.ME3 or MEGame.LE3 or MEGame.UDK)
                {
                    int guidToIntMapCount;
                    subnodes.Add(new BinInterpNode(bin.Position, $"CrossLevelCoverGuidRefs: ({guidToIntMapCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(guidToIntMapCount, i => MakeInt32Node(bin, $"{bin.ReadGuid()}"))
                    });

                    int coverListCount;
                    subnodes.Add(new BinInterpNode(bin.Position, $"CoverLinkRefs: ({coverListCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(coverListCount, i => MakeEntryNode(bin, $"{i}"))
                    });

                    int intToByteMapCount;
                    subnodes.Add(new BinInterpNode(bin.Position, $"CoverIndexPairs: ({intToByteMapCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(intToByteMapCount, i => new BinInterpNode(bin.Position, $"[{i}] {bin.ReadInt32()}: {bin.ReadByte()}"))
                    });

                    if (Pcc.Game != MEGame.UDK)
                    {
                        // BioWare only

                        int guidToIntMap2Count;
                        subnodes.Add(new BinInterpNode(bin.Position, $"CrossLevelNavGuidRefs: ({guidToIntMap2Count = bin.ReadInt32()})")
                        {
                            Items = ReadList(guidToIntMap2Count, i => MakeInt32Node(bin, $"{bin.ReadGuid()}"))
                        });

                        int navListCount;
                        subnodes.Add(new BinInterpNode(bin.Position, $"NavRefs: ({navListCount = bin.ReadInt32()})")
                        {
                            Items = ReadList(navListCount, i => MakeEntryNode(bin, $"{i}"))
                        });

                        int numbersCount;
                        subnodes.Add(new BinInterpNode(bin.Position,
                            $"NavRefIndices: ({numbersCount = bin.ReadInt32()})")
                        {
                            Items = ReadList(numbersCount, i => MakeInt32Node(bin, $"{i}"))
                        });
                    }
                }

                int crossLevelActorsCount;
                subnodes.Add(new BinInterpNode(bin.Position, $"CrossLevelActors?: ({crossLevelActorsCount = bin.ReadInt32()})")
                {
                    Items = ReadList(crossLevelActorsCount, i => MakeEntryNode(bin, $"{i}"))
                });

                if (Pcc.Game is MEGame.ME1 or MEGame.LE1)
                {
                    subnodes.Add(MakeEntryNode(bin, "BioArtPlaceable 1?"));
                    subnodes.Add(MakeEntryNode(bin, "BioArtPlaceable 2?"));
                }

                if (Pcc.Game >= MEGame.ME3)
                {
                    bool bInitialized;
                    int samplesCount;
                    subnodes.Add(new BinInterpNode(bin.Position, "PrecomputedLightVolume")
                    {
                        Items =
                        {
                            new BinInterpNode(bin.Position, $"bInitialized: ({bInitialized = bin.ReadBoolInt()})"),
                            ListInitHelper.ConditionalAdd(bInitialized, () => new ITreeItem[]
                            {
                                MakeBoxNode(bin, "Bounds"),
                                MakeFloatNode(bin, "SampleSpacing"),
                                new BinInterpNode(bin.Position, $"Samples ({samplesCount = bin.ReadInt32()})")
                                {
                                    Items = ReadList(samplesCount, i => new BinInterpNode(bin.Position, $"{i}")
                                    {
                                        Items =
                                        {
                                            MakeVectorNode(bin, "Position"),
                                            MakeFloatNode(bin, "Radius"),
                                            ListInitHelper.ConditionalAdd(Pcc.Game == MEGame.UDK, () => new ITreeItem[]
                                            {
                                                MakeByteNode(bin, "IndirectDirectionTheta"),
                                                MakeByteNode(bin, "IndirectDirectionPhi"),
                                                MakeByteNode(bin, "EnvironmentDirectionTheta"),
                                                MakeByteNode(bin, "EnvironmentDirectionPhi"),
                                                MakeColorNode(bin, "IndirectRadiance"),
                                                MakeColorNode(bin, "EnvironmentRadiance"),
                                                MakeColorNode(bin, "AmbientRadiance"),
                                                MakeByteNode(bin, "bShadowedFromDominantLights"),
                                            }, () => new []
                                            {
                                                //SirCxyrtyx: This is a color, but is serialized as an FQuantizedSHVectorRGB, a vector of colored, quantized spherical harmonic coefficients.
                                                //Conversion to ARGB is possible, but devilishly tricky. Let me know if this is something that's actually needed
                                                new BinInterpNode(bin.Position, $"Ambient Radiance? : {bin.ReadToBuffer(39)}"){ Length = 39}
                                            })
                                        }
                                    })
                                }
                            })
                        }
                    });
                }
                if (Pcc.Game == MEGame.UDK)
                {
                    BinInterpNode item = new BinInterpNode(bin.Position, "PrecomputedVisibilityHandler")
                    {
                        IsExpanded = true
                    };
                    subnodes.Add(item);
                    item.Items.Add(MakeVector2DNode(bin, "PrecomputedVisibilityCellBucketOriginXY"));
                    item.Items.Add(MakeFloatNode(bin, "PrecomputedVisibilityCellSizeXY"));
                    item.Items.Add(MakeFloatNode(bin, "PrecomputedVisibilityCellSizeZ"));
                    item.Items.Add(MakeInt32Node(bin, "PrecomputedVisibilityCellBucketSizeXY"));
                    item.Items.Add(MakeInt32Node(bin, "PrecomputedVisibilityNumCellBuckets"));

                    item = new BinInterpNode(bin.Position, "PrecomputedVolumeDistanceField")
                    {
                        IsExpanded = true
                    };

                    subnodes.Add(item);
                    item.Items.Add(MakeFloatNode(bin, "VolumeMaxDistance"));
                    item.Items.Add(MakeBoxNode(bin, "VolumeBox"));
                    item.Items.Add(MakeInt32Node(bin, "VolumeSizeX"));
                    item.Items.Add(MakeInt32Node(bin, "VolumeSizeY"));
                    item.Items.Add(MakeInt32Node(bin, "VolumeSizeZ"));
                    item.Items.Add(MakeArrayNode(bin, "Data", x=> MakeColorNode(bin, $"Color[{x}]")));
                    item.Items.Add(MakeInt32Node(bin, "UDKUnknown"));

                }

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }


        private List<ITreeItem> StartPrefabInstanceScan(byte[] data, ref int binarystart)
        {
            /*
             *  count: 4 bytes 
             *      Prefab ref : 4 bytes
             *      Level Object : 4 bytes
             *  0: 4 bytes
             *  
             */
            var subnodes = new List<ITreeItem>();
            if (!CurrentLoadedExport.HasStack)
            {
                return subnodes;
            }

            try
            {
                var bin = new EndianReader(new MemoryStream(data)) { Endian = CurrentLoadedExport.FileRef.Endian };
                bin.JumpTo(binarystart);
                subnodes.Add(MakeArrayNode(bin, "ArchetypeToInstanceMap", i => new BinInterpNode(bin.Position, $"{i}")
                {
                    IsExpanded = true,
                    Items =
                    {
                        MakeEntryNode(bin, "Archetype"),
                        MakeEntryNode(bin, "Instance")
                    }
                }, true));
                subnodes.Add(MakeArrayNode(bin, "PrefabInstance_ObjectMap", i => new BinInterpNode(bin.Position, $"{i}")
                {
                    IsExpanded = true,
                    Items =
                    {
                        MakeEntryNode(bin, "Object:"),
                        MakeInt32Node(bin, "int")
                    }
                }, true));
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        [Flags]
        enum EBulkDataFlags
        {
            BULKDATA_None = 0,
            BULKDATA_StoreInSeparateFile = 1 << 0,
            BULKDATA_SerializeCompressedZLIB = 1 << 1,
            BULKDATA_ForceSingleElementSerialization = 1 << 2,
            BULKDATA_SingleUse = 1 << 3,
            BULKDATA_SerializeCompressedLZO = 1 << 4,
            BULKDATA_Unused = 1 << 5,
            BULKDATA_StoreOnlyPayload = 1 << 6,
            BULKDATA_SerializeCompressedLZX = 1 << 7,
            BULKDATA_SerializeCompressed = (BULKDATA_SerializeCompressedZLIB | BULKDATA_SerializeCompressedLZO | BULKDATA_SerializeCompressedLZX),
        }


        private List<ITreeItem> StartGenericScan(byte[] data, ref int binarystart)
        {
            binarystart = ByteShiftUpDownValue.Value + binarystart;
            var subnodes = new List<ITreeItem>();

            if (binarystart >= data.Length)
            {
                return subnodes;
            }
            try
            {
                int binarypos = binarystart;

                //binarypos += 0x1C; //Skip ??? and GUID
                //int guid = BitConverter.ToInt32(data, binarypos);
                /*int num1 = BitConverter.ToInt32(data, binarypos);
                TreeNode node = new TreeNode($"0x{binarypos:X4} ???: {num1.ToString());
                subnodes.Add(node);
                binarypos += 4;
                int num2 = BitConverter.ToInt32(data, binarypos);
                node = new TreeNode($"0x{binarypos:X4} Count: {num2.ToString());
                subnodes.Add(node);
                binarypos += 4;
                */
                int datasize = 4;
                if (interpreterMode == InterpreterMode.Names)
                {
                    datasize = 8;
                }

                while (binarypos <= data.Length - datasize)
                {
                    string nodeText = $"0x{binarypos:X4} : ";
                    var node = new BinInterpNode();

                    switch (interpreterMode)
                    {
                        case InterpreterMode.Objects:
                            {
                                int val = BitConverter.ToInt32(data, binarypos);
                                string name = $"0x{binarypos:X6}: {val}";
                                if (CurrentLoadedExport.FileRef.IsEntry(val) && CurrentLoadedExport.FileRef.GetEntry(val) is IEntry ent)
                                {
                                    name += " " + CurrentLoadedExport.FileRef.GetEntryString(val);
                                }

                                nodeText = name;
                                node.Tag = NodeType.StructLeafObject;
                                break;
                            }
                        case InterpreterMode.Names:
                            {
                                int val = BitConverter.ToInt32(data, binarypos);
                                if (val > 0 && val <= CurrentLoadedExport.FileRef.NameCount)
                                {
                                    nodeText += $"{val,-14}{CurrentLoadedExport.FileRef.GetNameEntry(val)}";
                                }
                                else
                                {
                                    nodeText += $"              {val}"; //14 spaces
                                }
                                node.Tag = NodeType.StructLeafName;
                                break;
                            }
                        case InterpreterMode.Floats:
                            {
                                float val = BitConverter.ToSingle(data, binarypos);
                                nodeText += val.ToString();
                                node.Tag = NodeType.StructLeafFloat;
                                break;
                            }
                        case InterpreterMode.Integers:
                            {
                                int val = BitConverter.ToInt32(data, binarypos);
                                nodeText += val.ToString();
                                node.Tag = NodeType.StructLeafInt;
                                break;
                            }
                    }
                    node.Header = nodeText;
                    node.Offset = binarypos;
                    subnodes.Add(node);
                    binarypos += 4;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }
    }
}
