using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using DocumentFormat.OpenXml.Drawing;
using Gammtek.Conduit.Extensions.IO;
using Gibbed.IO;
using ME3Explorer.Packages;
using ME3Explorer.Soundplorer;
using ME3Explorer.Unreal;
using ME3Explorer.Scene3D;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Explorer.Unreal.ME3Enums;
using StreamHelpers;
using static ME3Explorer.TlkManagerNS.TLKManagerWPF;

namespace ME3Explorer
{
    public partial class BinaryInterpreterWPF
    {
        private List<ITreeItem> StartShaderCacheScanStream(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                int dataOffset = CurrentLoadedExport.DataOffset;
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);

                subnodes.Add(new BinInterpNode(bin.Position, $"Platform: {(EShaderPlatform)bin.ReadByte()}") { Length = 1 });

                int mapCount = Pcc.Game == MEGame.ME3 ? 2 : 1;
                for (; mapCount > 0; mapCount--)
                {
                    int vertexMapCount = bin.ReadInt32();
                    var mappingNode = new BinInterpNode(bin.Position - 4, $"Name Mapping {mapCount}, {vertexMapCount} items");
                    subnodes.Add(mappingNode);

                    for (int i = 0; i < vertexMapCount; i++)
                    {
                        NameReference shaderName = bin.ReadNameReference(Pcc);
                        int shaderCRC = bin.ReadInt32();
                        mappingNode.Items.Add(new BinInterpNode(bin.Position - 12, $"CRC:{shaderCRC:X8} {shaderName.Instanced}") { Length = 12 });
                    }
                }

                int embeddedShaderFileCount = bin.ReadInt32();
                var embeddedShaderCount = new BinInterpNode(bin.Position - 4, $"Embedded Shader File Count: {embeddedShaderFileCount}");
                subnodes.Add(embeddedShaderCount);
                for (int i = 0; i < embeddedShaderFileCount; i++)
                {
                    NameReference shaderName = bin.ReadNameReference(Pcc);
                    var shaderNode = new BinInterpNode(bin.Position - 8, $"Shader {i} {shaderName.Instanced}");

                    shaderNode.Items.Add(new BinInterpNode(bin.Position - 8, $"Shader Type: {shaderName.Instanced}") { Length = 8 });
                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Shader GUID {bin.ReadValueGuid()}") { Length = 16 });

                    int shaderEndOffset = bin.ReadInt32();
                    shaderNode.Items.Add(new BinInterpNode(bin.Position - 4, $"Shader End Offset: {shaderEndOffset}") { Length = 4 });


                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Platform: {(EShaderPlatform)bin.ReadByte()}") { Length = 1 });
                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Frequency: {(EShaderFrequency)bin.ReadByte()}") { Length = 1 });

                    int shaderSize = bin.ReadInt32();
                    shaderNode.Items.Add(new BinInterpNode(bin.Position - 4, $"Shader File Size: {shaderSize}") { Length = 4 });

                    shaderNode.Items.Add(new BinInterpNode(bin.Position, "Shader File") { Length = shaderSize });
                    bin.Skip(shaderSize);

                    shaderNode.Items.Add(MakeInt32Node(bin, "ParameterMap CRC"));

                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Shader End GUID: {bin.ReadValueGuid()}") { Length = 16 });

                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });

                    shaderNode.Items.Add(MakeInt32Node(bin, "Number of Instructions"));

                    embeddedShaderCount.Items.Add(shaderNode);

                    bin.JumpTo(shaderEndOffset - dataOffset);
                }

                int vertexFactoryMapCount = bin.ReadInt32();
                var factoryMapNode = new BinInterpNode(bin.Position - 4, $"Vertex Factory Name Mapping, {vertexFactoryMapCount} items");
                subnodes.Add(factoryMapNode);

                for (int i = 0; i < vertexFactoryMapCount; i++)
                {
                    NameReference shaderName = bin.ReadNameReference(Pcc);
                    int shaderCRC = bin.ReadInt32();
                    factoryMapNode.Items.Add(new BinInterpNode(bin.Position - 12, $"{shaderCRC:X8} {shaderName.Instanced}") { Length = 12 });
                }

                int materialShaderMapcount = bin.ReadInt32();
                var materialShaderMaps = new BinInterpNode(bin.Position - 4, $"Material Shader Maps, {materialShaderMapcount} items");
                subnodes.Add(materialShaderMaps);
                for (int i = 0; i < materialShaderMapcount; i++)
                {
                    var nodes = new List<ITreeItem>();
                    materialShaderMaps.Items.Add(new BinInterpNode(bin.Position, $"Material Shader Map {i}") { Items = nodes });
                    nodes.AddRange(ReadFStaticParameterSet(bin));

                    if (Pcc.Game == MEGame.ME3)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"Unreal Version {bin.ReadInt32()}") { Length = 4 });
                        nodes.Add(new BinInterpNode(bin.Position, $"Licensee Version {bin.ReadInt32()}") { Length = 4 });
                    }

                    int shaderMapEndOffset = bin.ReadInt32();
                    nodes.Add(new BinInterpNode(bin.Position - 4, $"Material Shader Map end offset {shaderMapEndOffset}") { Length = 4 });

                    int unkCount = bin.ReadInt32();
                    var unkNodes = new List<ITreeItem>();
                    nodes.Add(new BinInterpNode(bin.Position - 4, $"Shaders {unkCount}") { Length = 4, Items = unkNodes });
                    for (int j = 0; j < unkCount; j++)
                    {
                        unkNodes.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc).Instanced}") { Length = 8 });
                        unkNodes.Add(new BinInterpNode(bin.Position, $"GUID: {bin.ReadValueGuid()}") { Length = 16 });
                        unkNodes.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc).Instanced}") { Length = 8 });
                    }

                    int meshShaderMapsCount = bin.ReadInt32();
                    var meshShaderMaps = new BinInterpNode(bin.Position - 4, $"Mesh Shader Maps, {meshShaderMapsCount} items") { Length = 4 };
                    nodes.Add(meshShaderMaps);
                    for (int j = 0; j < meshShaderMapsCount; j++)
                    {
                        var nodes2 = new List<ITreeItem>();
                        meshShaderMaps.Items.Add(new BinInterpNode(bin.Position, $"Mesh Shader Map {j}") { Items = nodes2 });

                        int shaderCount = bin.ReadInt32();
                        var shaders = new BinInterpNode(bin.Position - 4, $"Shaders, {shaderCount} items") { Length = 4 };
                        nodes2.Add(shaders);
                        for (int k = 0; k < shaderCount; k++)
                        {
                            var nodes3 = new List<ITreeItem>();
                            shaders.Items.Add(new BinInterpNode(bin.Position, $"Shader {k}") { Items = nodes3 });

                            nodes3.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });
                            nodes3.Add(new BinInterpNode(bin.Position, $"GUID: {bin.ReadValueGuid()}") { Length = 16 });
                            nodes3.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });
                        }
                        nodes2.Add(new BinInterpNode(bin.Position, $"Vertex Factory Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });
                    }

                    nodes.Add(new BinInterpNode(bin.Position, $"MaterialId: {bin.ReadValueGuid()}") { Length = 16 });

                    nodes.Add(MakeStringNode(bin, "Friendly Name"));

                    nodes.AddRange(ReadFStaticParameterSet(bin));

                    if (Pcc.Game == MEGame.ME3)
                    {
                        string[] uniformExpressionArrays =
                        {
                            "UniformPixelVectorExpressions",
                            "UniformPixelScalarExpressions",
                            "Uniform2DTextureExpressions",
                            "UniformCubeTextureExpressions",
                            "UniformVertexVectorExpressions",
                            "UniformVertexScalarExpressions"
                        };

                        foreach (string uniformExpressionArrayName in uniformExpressionArrays)
                        {
                            int expressionCount = bin.ReadInt32();
                            nodes.Add(new BinInterpNode(bin.Position - 4, $"{uniformExpressionArrayName}, {expressionCount} expressions")
                            {
                                Items = ReadList(expressionCount, x => ReadMaterialUniformExpression(bin))
                            });
                        }
                        nodes.Add(new BinInterpNode(bin.Position, $"Platform: {(EShaderPlatform)bin.ReadInt32()}") { Length = 4 });
                    }

                    bin.JumpTo(shaderMapEndOffset - dataOffset);
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private BinInterpNode MakeStringNode(MemoryStream bin, string nodeName)
        {
            long pos = bin.Position;
            int strLen = bin.ReadInt32();
            string str;
            if (Pcc.Game == MEGame.ME3)
            {
                strLen *= -2;
                str = bin.ReadStringUnicodeNull(strLen);
            }
            else
            {
                str = bin.ReadStringASCIINull(strLen);
            }
            return new BinInterpNode(pos, $"{nodeName}: {str}") { Length = strLen + 4 };
        }

        enum EShaderFrequency : byte
        {
            Vertex = 0,
            Pixel = 1,
        }
        enum EShaderPlatform : byte
        {
            PCDirect3D_ShaderModel3 = 0,
            PS3 = 1,
            XBOXDirect3D = 2,
            PCDirect3D_ShaderModel4 = 3
        }

        private BinInterpNode ReadMaterialUniformExpression(MemoryStream bin, string prefix = "")
        {
            NameReference expressionType = bin.ReadNameReference(Pcc);
            var node = new BinInterpNode(bin.Position - 8, $"{prefix}{(string.IsNullOrEmpty(prefix) ? "" : ": ")}{expressionType.Instanced}");

            switch (expressionType.Name)
            {
                case "FMaterialUniformExpressionAbs":
                case "FMaterialUniformExpressionCeil":
                case "FMaterialUniformExpressionFloor":
                case "FMaterialUniformExpressionFrac":
                case "FMaterialUniformExpressionPeriodic":
                case "FMaterialUniformExpressionSquareRoot":
                    node.Items.Add(ReadMaterialUniformExpression(bin, "X"));
                    break;
                case "FMaterialUniformExpressionAppendVector":
                    node.Items.Add(ReadMaterialUniformExpression(bin, "A"));
                    node.Items.Add(ReadMaterialUniformExpression(bin, "B"));
                    node.Items.Add(MakeUInt32Node(bin, "NumComponentsA:"));
                    break;
                case "FMaterialUniformExpressionClamp":
                    node.Items.Add(ReadMaterialUniformExpression(bin, "Input"));
                    node.Items.Add(ReadMaterialUniformExpression(bin, "Min"));
                    node.Items.Add(ReadMaterialUniformExpression(bin, "Max"));
                    break;
                case "FMaterialUniformExpressionConstant":
                    node.Items.Add(MakeFloatNode(bin, "R"));
                    node.Items.Add(MakeFloatNode(bin, "G"));
                    node.Items.Add(MakeFloatNode(bin, "B"));
                    node.Items.Add(MakeFloatNode(bin, "A"));
                    node.Items.Add(MakeByteNode(bin, "ValueType"));
                    break;
                case "FMaterialUniformExpressionFmod":
                case "FMaterialUniformExpressionMax":
                case "FMaterialUniformExpressionMin":
                    node.Items.Add(ReadMaterialUniformExpression(bin, "A"));
                    node.Items.Add(ReadMaterialUniformExpression(bin, "B"));
                    break;
                case "FMaterialUniformExpressionFoldedMath":
                    node.Items.Add(ReadMaterialUniformExpression(bin, "A"));
                    node.Items.Add(ReadMaterialUniformExpression(bin, "B"));
                    node.Items.Add(new BinInterpNode(bin.Position, $"Op: {(EFoldedMathOperation)bin.ReadByte()}"));
                    break;
                case "FMaterialUniformExpressionRealTime":
                    //intentionally left blank. outputs current real-time, has no parameters
                    break;
                case "FMaterialUniformExpressionScalarParameter":
                    node.Items.Add(new BinInterpNode(bin.Position, $"ParameterName: {bin.ReadNameReference(Pcc).Instanced}"));
                    node.Items.Add(MakeFloatNode(bin, "DefaultValue"));
                    break;
                case "FMaterialUniformExpressionSine":
                    node.Items.Add(ReadMaterialUniformExpression(bin, "X"));
                    node.Items.Add(MakeBoolIntNode(bin, "bIsCosine"));
                    break;
                case "FMaterialUniformExpressionTexture":
                case "FMaterialUniformExpressionFlipBookTextureParameter":
                    if (Pcc.Game == MEGame.ME3)
                    {
                        node.Items.Add(MakeInt32Node(bin, "TextureIndex:"));
                    }
                    else
                    {
                        node.Items.Add(MakeEntryNode(bin, "TextureIndex:"));
                    }
                    break;
                case "FMaterialUniformExpressionFlipbookParameter":
                    node.Items.Add(MakeInt32Node(bin, "Index:"));
                    node.Items.Add(MakeEntryNode(bin, "TextureIndex:"));
                    break;
                case "FMaterialUniformExpressionTextureParameter":
                    node.Items.Add(new BinInterpNode(bin.Position, $"ParameterName: {bin.ReadNameReference(Pcc).Instanced}"));
                    node.Items.Add(MakeInt32Node(bin, "TextureIndex:"));
                    break;
                case "FMaterialUniformExpressionTime":
                    //intentionally left blank. outputs current scene time, has no parameters
                    break;
                case "FMaterialUniformExpressionVectorParameter":
                    node.Items.Add(new BinInterpNode(bin.Position, $"ParameterName: {bin.ReadNameReference(Pcc).Instanced}"));
                    node.Items.Add(MakeFloatNode(bin, "Default R"));
                    node.Items.Add(MakeFloatNode(bin, "Default G"));
                    node.Items.Add(MakeFloatNode(bin, "Default B"));
                    node.Items.Add(MakeFloatNode(bin, "Default A"));
                    break;
                case "FMaterialUniformExpressionFractionOfEffectEnabled":
                    //Not sure what it does, but it doesn't seem to have any parameters
                    break;
                default:
                    throw new ArgumentException(expressionType.Instanced);
            }

            return node;
        }

        enum EFoldedMathOperation : byte
        {
            Add,
            Sub,
            Mul,
            Div,
            Dot
        }

        private List<ITreeItem> StartStaticMeshComponentScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);
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
                        Items = ReadList(bin.Skip(-4).ReadInt32(), j => MakeEntryNode(bin, $"{j}"))
                    });
                    node.Items.Add(new BinInterpNode(bin.Position, $"ShadowVertexBuffers ({bin.ReadInt32()})")
                    {
                        Items = ReadList(bin.Skip(-4).ReadInt32(), j => MakeEntryNode(bin, $"{j}"))
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
                var bin = new MemoryStream(data);
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
                var bin = new MemoryStream(data);
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
                                MakeInt32Node(bin, "Unknown"),
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

        private List<ITreeItem> StartTerrainScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);

                subnodes.Add(MakeArrayNode(bin, "Heights", i => MakeUInt16Node(bin, $"{i}")));
                subnodes.Add(MakeArrayNode(bin, "InfoData", i => new BinInterpNode(bin.Position, $"{i}: {(EInfoFlags)bin.ReadByte()}")));
                subnodes.Add(MakeArrayNode(bin, "AlphaMaps", i => MakeArrayNode(bin, $"{i}: Data", j => new BinInterpNode(bin.Position, $"{j}: {bin.ReadByte()}"))));
                subnodes.Add(MakeArrayNode(bin, "WeightedTextureMaps", i => MakeEntryNode(bin, $"{i}")));
                for (int k = Pcc.Game == MEGame.ME1 ? 1 : 2; k > 0; k--)
                {
                    subnodes.Add(MakeArrayNode(bin, "CachedTerrainMaterials", i =>
                    {
                        var node = MakeMaterialResourceNode(bin, $"{i}");

                        node.Items.Add(MakeEntryNode(bin, "Terrain"));
                        node.Items.Add(new BinInterpNode(bin.Position, "Mask")
                        {
                            IsExpanded = true,
                            Items =
                            {
                                MakeInt32Node(bin, "NumBits"),
                                new BinInterpNode(bin.Position, $"BitMask: {Convert.ToString(bin.ReadInt64(), 2).PadLeft(64, '0')}")
                            }
                        });
                        node.Items.Add(MakeArrayNode(bin, "MaterialIds", j => MakeGuidNode(bin, $"{j}")));
                        if (Pcc.Game == MEGame.ME3)
                        {
                            node.Items.Add(MakeGuidNode(bin, "LightingGuid"));
                        }

                        return node;
                    }));
                }
                if (Pcc.Game != MEGame.ME1)
                {
                    subnodes.Add(MakeArrayNode(bin, "CachedDisplacements", i => new BinInterpNode(bin.Position, $"{i}: {bin.ReadByte()}")));
                    subnodes.Add(MakeFloatNode(bin, "MaxCollisionDisplacement"));
                }

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        [Flags]
        enum EInfoFlags : byte
        {
            TID_Visibility_Off = 1,
            TID_OrientationFlip = 2,
            TID_Unreachable = 4,
            TID_Locked = 8,
        }

        private BinInterpNode MakeLightMapNode(MemoryStream bin)
        {
            ELightMapType lightMapType;
            int bulkSerializeElementCount;
            int bulkSerializeDataSize;
            return new BinInterpNode(bin.Position, "LightMap ")
            {
                IsExpanded = true,
                Items =
                {
                    new BinInterpNode(bin.Position, $"LightMapType: {lightMapType = (ELightMapType)bin.ReadInt32()}"),
                    ListInitHelper.ConditionalAdd(lightMapType != ELightMapType.LMT_None, () => new List<ITreeItem>
                    {
                        new BinInterpNode(bin.Position, $"LightGuids ({bin.ReadInt32()})")
                        {
                            Items = ReadList(bin.Skip(-4).ReadInt32(), j => new BinInterpNode(bin.Position, $"{j}: {bin.ReadGuid()}"))
                        },
                        ListInitHelper.ConditionalAdd(lightMapType == ELightMapType.LMT_1D, () => new ITreeItem[]
                        {
                            MakeEntryNode(bin, "Owner"),
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
                            MakeEntryNode(bin, "Texture 1"),
                            MakeVectorNode(bin, "ScaleVector 1"),
                            MakeEntryNode(bin, "Texture 2"),
                            MakeVectorNode(bin, "ScaleVector 2"),
                            MakeEntryNode(bin, "Texture 3"),
                            MakeVectorNode(bin, "ScaleVector 3"),
                            ListInitHelper.ConditionalAdd(Pcc.Game < MEGame.ME3, () => new ITreeItem[]
                            {
                                MakeEntryNode(bin, "Texture 4"),
                                MakeVectorNode(bin, "ScaleVector 4"),
                            }),
                            MakeVector2DNode(bin, "CoordinateScale"),
                            MakeVector2DNode(bin, "CoordinateBias")
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
                            MakeEntryNode(bin, "Texture 1"),
                            new ListInitHelper.InitCollection<ITreeItem>(ReadList(8, j => MakeFloatNode(bin, "Unknown float"))),
                            MakeEntryNode(bin, "Texture 2"),
                            new ListInitHelper.InitCollection<ITreeItem>(ReadList(8, j => MakeFloatNode(bin, "Unknown float"))),
                            MakeEntryNode(bin, "Texture 3"),
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
                    })
                }
            };
        }

        private static List<ITreeItem> StartBrushComponentScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);
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

        private static List<ITreeItem> StartRB_BodySetupScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);
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

        private List<ITreeItem> StartModelComponentScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                int count;
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);

                subnodes.Add(MakeEntryNode(bin, "Model"));
                subnodes.Add(MakeInt32Node(bin, "ZoneIndex"));
                subnodes.Add(new BinInterpNode(bin.Position, $"Elements ({count = bin.ReadInt32()})")
                {
                    Items = ReadList(count, i => new BinInterpNode(bin.Position, $"{i}: FModelElement")
                    {
                        Items =
                        {
                            MakeLightMapNode(bin),
                            MakeEntryNode(bin, "Component"),
                            MakeEntryNode(bin, "Material"),
                            new BinInterpNode(bin.Position, $"Nodes ({count = bin.ReadInt32()})")
                            {
                                Items = ReadList(count, j => new BinInterpNode(bin.Position, $"{j}: {bin.ReadUInt16()}"))
                            },
                            new BinInterpNode(bin.Position, $"ShadowMaps ({count = bin.ReadInt32()})")
                            {
                                Items = ReadList(count, j => MakeEntryNode(bin, $"{j}"))
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
                var bin = new MemoryStream(data);
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

        private List<ITreeItem> StartDominantLightScan(byte[] data)
        {

            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);


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

        private List<ITreeItem> StartBioPawnScan(byte[] data, ref int binarystart)
        {

            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);


                int count;
                subnodes.Add(new BinInterpNode(bin.Position, $"AnimationMap? ({count = bin.ReadInt32()})")
                {
                    Items = ReadList(count, i => new BinInterpNode(bin.Position, $"{bin.ReadNameReference(Pcc)}: {entryRefString(bin)}", NodeType.StructLeafObject) { Length = 4 })
                });

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private static List<ITreeItem> StartPhysicsAssetInstanceScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);
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
                            new BinInterpNode(bin.Position, $"false: {bin.ReadBoolInt()}")
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

        private static List<ITreeItem> StartCookedBulkDataInfoContainerScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);

                int count;
                subnodes.Add(new BinInterpNode(bin.Position, $"UnknownMap ({count = bin.ReadInt32()})")
                {
                    IsExpanded = true,
                    Items = ReadList(count, i => new BinInterpNode(bin.Position, $"{bin.ReadStringASCIINull(bin.ReadInt32())}")
                    {
                        Items =
                        {
                            MakeInt32Node(bin, "Unknown 1"),
                            MakeInt32Node(bin, "Unknown 2"),
                            MakeInt32Node(bin, "Unknown 3"),
                            MakeInt32Node(bin, "Unknown 4"),
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

        private static List<ITreeItem> StartMorphTargetScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);
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
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);
                if (Pcc.Game == MEGame.ME3)
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
                var bin = new MemoryStream(data);
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

        private string entryRefString(MemoryStream bin) { int n = bin.ReadInt32(); return $"#{n} {CurrentLoadedExport.FileRef.GetEntryString(n)}"; }

        private List<ITreeItem> StartModelScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);
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
                    subnodes.Add(new BinInterpNode(bin.Position, $"LightingGuid: {bin.ReadValueGuid()}") { Length = 16 });

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

        private List<ITreeItem> StartDecalComponentScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);

                int numStaticRecievers;
                int count;
                int fDecalVertexSize;
                subnodes.Add(new BinInterpNode(bin.Position, $"StaticReceivers: {numStaticRecievers = bin.ReadInt32()}")
                {
                    IsExpanded = true,
                    Items = ReadList(numStaticRecievers, i => new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items =
                        {
                            MakeEntryNode(bin, "Component"),
                            new BinInterpNode(bin.Position, $"FDecalVertex Size: {fDecalVertexSize = bin.ReadInt32()}"),
                            new BinInterpNode(bin.Position, $"Vertices ({count = bin.ReadInt32()})")
                            {
                                Length = 4 + fDecalVertexSize * count,
                                Items = ReadList(count, j => new BinInterpNode(bin.Position, $"{j}")
                                {
                                    Length = fDecalVertexSize,
                                    Items =
                                    {
                                        MakeVectorNode(bin, "Position"),
                                        MakePackedNormalNode(bin, "TangentX"),
                                        MakePackedNormalNode(bin, "TangentZ"),
                                        ListInitHelper.ConditionalAdd(Pcc.Game != MEGame.ME3, () => new ITreeItem[]
                                        {
                                            MakeVector2DNode(bin, "LegacyProjectedUVs")
                                        }),
                                        MakeVector2DNode(bin, "LightMapCoordinate"),
                                        ListInitHelper.ConditionalAdd(Pcc.Game != MEGame.ME3, () => new ITreeItem[]
                                        {
                                            MakeVector2DNode(bin, "LegacyNormalTransform[0]"),
                                            MakeVector2DNode(bin, "LegacyNormalTransform[1]")
                                        }),
                                    }
                                })
                            },
                            MakeInt32Node(bin, "unsigned short size"),
                            new BinInterpNode(bin.Position, $"Indices ({count = bin.ReadInt32()})")
                            {
                                Length = 4 + count * 2,
                                Items = ReadList(count, j => new BinInterpNode(bin.Position, $"{j}: {bin.ReadUInt16()}") { Length = 2 })
                            },
                            MakeUInt32Node(bin, "NumTriangles"),
                            MakeLightMapNode(bin),
                            ListInitHelper.ConditionalAdd(Pcc.Game == MEGame.ME3, () => new ITreeItem[]
                            {
                                new BinInterpNode(bin.Position, $"ShadowMap1D ({count = bin.ReadInt32()})")
                                {
                                    Length = 4 + count * 4,
                                    Items = ReadList(count, j => new BinInterpNode(bin.Position, $"{j}: {entryRefString(bin)}") { Length = 4 })
                                },
                                MakeInt32Node(bin, "Data"),
                                MakeInt32Node(bin, "InstanceIndex"),
                            }),
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

        private static List<ITreeItem> ReadList(int count, Func<int, ITreeItem> selector)
        {
            return Enumerable.Range(0, count).Select(selector).ToList();
        }

        private List<ITreeItem> StartWorldScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);

                subnodes.Add(MakeEntryNode(bin, "PersistentLevel"));
                if (Pcc.Game == MEGame.ME3)
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
                if (Pcc.Game == MEGame.ME1)
                {
                    subnodes.Add(MakeEntryNode(bin, "DecalManager"));
                }

                int extraObjsCount;
                subnodes.Add(new BinInterpNode(bin.Position, $"ExtraReferencedObjects: {extraObjsCount = bin.ReadInt32()}")
                {
                    Items = ReadList(extraObjsCount, i => new BinInterpNode(bin.Position, $"{entryRefString(bin)}"))
                });

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private List<ITreeItem> StartStackScan(byte[] data)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);

                string nodeString;
                subnodes.Add(new BinInterpNode(bin.Position, "Stack")
                {
                    IsExpanded = true,
                    Items =
                    {
                        new BinInterpNode(bin.Position, $"Node: {nodeString = entryRefString(bin)}", NodeType.StructLeafObject) {Length = 4},
                        ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game != MEGame.UDK, () => MakeEntryNode(bin, "StateNode")),
                        new BinInterpNode(bin.Position, $"ProbeMask: {bin.ReadUInt64():X16}"),
                        ListInitHelper.ConditionalAdd(Pcc.Game >= MEGame.ME3, () => new ITreeItem[]
                        {
                            MakeUInt16Node(bin, "LatentAction")
                        }, () => new ITreeItem[]
                        {
                            MakeUInt32Node(bin, "LatentAction")
                        }),
                        MakeArrayNode(bin, "StateStack", i => new BinInterpNode(bin.Position, $"{i}")
                        {
                            Items =
                            {
                                MakeEntryNode(bin, "State"),
                                MakeEntryNode(bin, "Node"),
                                MakeInt32Node(bin, "Offset")
                            }
                        }),
                        ListInitHelper.ConditionalAdd(nodeString != "Null", () => new ITreeItem[]
                        {
                            MakeInt32Node(bin, "Offset")
                        })
                    }
                });
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
                int offset = binarystart;

                int count = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Unknown int (not count): {count}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                MemoryStream ms = new MemoryStream(data);
                ms.Position = offset;
                int i = 0;
                while (ms.Position + 1 < ms.Length)
                {
                    offset = (int)ms.Position;

                    string label = null;
                    if (i % 2 == 1)
                    {
                        var postint = ms.ReadInt32();
                        var nameIdx = ms.ReadInt32();
                        label = CurrentLoadedExport.FileRef.GetNameEntry(nameIdx);
                        ms.ReadInt32();
                    }

                    var strLen = ms.ReadUInt32();
                    var line = Gibbed.IO.StreamHelpers.ReadString(ms, strLen, true, Encoding.ASCII);
                    if (label != null)
                    {
                        subnodes.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X6}    {label}:\n{line}\n",
                            Name = "_" + offset,
                            Tag = NodeType.None
                        });
                    }
                    else
                    {
                        subnodes.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X6} {line}",
                            Name = "_" + offset,
                            Tag = NodeType.None
                        });
                    }
                    Debug.WriteLine("Read string " + i + ", end at 0x" + offset.ToString("X6"));
                    i++;
                }
                /*
                offset = binarystart + 0x18;

                MemoryStream ms = new MemoryStream(data);
                ms.Position = offset;
                var scriptStructProperties = PropertyCollection.ReadProps(CurrentLoadedExport.FileRef, ms, "ScriptStruct", includeNoneProperty: true);

                UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                foreach (UProperty prop in scriptStructProperties)
                {
                    InterpreterWPF.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
                }*/
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
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
                        Name = "_" + offset,
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
                            Name = "_" + offset,
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
                                Name = "_" + offset,
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

        private List<ITreeItem> StartSoundNodeWaveScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                int offset = binarystart;


                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X8} Item1: {classObjTree} (0x{classObjTree:X8})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X8} Data length: {classObjTree} (0x{classObjTree:X8})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X8} Data length: {classObjTree} (0x{classObjTree:X8})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X8} Item4: {classObjTree} (0x{classObjTree:X8})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;
                /*
                offset = binarystart + 0x18;

                MemoryStream ms = new MemoryStream(data);
                ms.Position = offset;
                var scriptStructProperties = PropertyCollection.ReadProps(CurrentLoadedExport.FileRef, ms, "ScriptStruct", includeNoneProperty: true);

                UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                foreach (UProperty prop in scriptStructProperties)
                {
                    InterpreterWPF.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
                }*/
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> StartBioSoundNodeWaveStreamingDataScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                int offset = binarystart;


                int numBytesOfStreamingData = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Streaming Data Size: {numBytesOfStreamingData}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                var nextFileOffset = BitConverter.ToInt32(data, offset);
                var node = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Next file offset: {nextFileOffset}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                };

                var clickToGotoOffset = new BinInterpNode
                {
                    Header = $"0x{offset:X5} Click to go to referenced offset 0x{nextFileOffset:X5}",
                    Name = "_" + nextFileOffset
                };
                node.Items.Add(clickToGotoOffset);

                subnodes.Add(node);
                offset += 4;

                MemoryStream asStream = new MemoryStream(data);
                asStream.Position = offset;

                while (asStream.Position < asStream.Length)
                {
                    Debug.WriteLine("Reading at " + asStream.Position);
                    ISACT_Parser.ReadStream(asStream);
                }
                /*
                offset = binarystart + 0x18;

                MemoryStream ms = new MemoryStream(data);
                ms.Position = offset;
                var scriptStructProperties = PropertyCollection.ReadProps(CurrentLoadedExport.FileRef, ms, "ScriptStruct", includeNoneProperty: true);

                UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                foreach (UProperty prop in scriptStructProperties)
                {
                    InterpreterWPF.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
                }*/
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
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                subnodes.Add(EventCountNode);

                for (int e = 0; e < eCount; e++) //EVENTS
                {
                    int iEventID = BitConverter.ToInt32(data, offset);  //EVENT ID
                    var EventIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} State Transition ID: {iEventID} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    EventCountNode.Items.Add(EventIDs);

                    int EventMapInstVer = BitConverter.ToInt32(data, offset); //Event Instance Version
                    EventIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Instance Version: {EventMapInstVer} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int nTransitions = BitConverter.ToInt32(data, offset); //Count of State Events
                    var TransitionsIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Transitions: {nTransitions} ",
                        Name = "_" + offset,
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
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            tPlotID = BitConverter.ToInt32(data, offset);  //Plot
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Plot ID: {tPlotID} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tNewValue = BitConverter.ToInt32(data, offset);  //NewValue
                            bool bNewValue = false;
                            if (tNewValue == 1) { bNewValue = true; }
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} New Value: {tNewValue}  {bNewValue} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                            bool bUseParam = false;
                            if (tUseParam == 1) { bUseParam = true; }
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;
                        }
                        else if (transTYPE == 1) //TYPE 1 = CONSEQUENCE
                        {
                            var nTransition = new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Type: {transTYPE} Consequence",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tConsequenceParam = BitConverter.ToInt32(data, offset);  //Consequence parameter
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Consequence Parameter: {tConsequenceParam} ",
                                Name = "_" + offset,
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
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            tPlotID = BitConverter.ToInt32(data, offset);  //Plot
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Plot ID: {tPlotID} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            float tNewValue = BitConverter.ToInt32(data, offset);  //NewValue
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} New Value: {tNewValue} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafFloat
                            });
                            offset += 4;

                            int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                            bool bUseParam = false;
                            if (tUseParam == 1) { bUseParam = true; }
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tIncrement = BitConverter.ToInt32(data, offset);  //Increment bool
                            bool bIncrement = false;
                            if (tIncrement == 1) { bIncrement = true; }
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Increment value: {tIncrement}  {bIncrement} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;
                        }
                        else if (transTYPE == 3)  // TYPE 3 = FUNCTION
                        {
                            var nTransition = new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Type: {transTYPE} Function",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int PackageName = BitConverter.ToInt32(data, offset);  //Package name
                            offset += 4;
                            int PackageIdx = BitConverter.ToInt32(data, offset);  //Package name idx
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Package Name: {CurrentLoadedExport.FileRef.GetNameEntry(PackageName)}_{PackageIdx}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafName
                            });
                            offset += 4;

                            int ClassName = BitConverter.ToInt32(data, offset);  //Class name
                            offset += 4;
                            int ClassIdx = BitConverter.ToInt32(data, offset);  //Class name idx
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Class Name: {CurrentLoadedExport.FileRef.GetNameEntry(ClassName)}_{ClassIdx}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafName
                            });
                            offset += 4;

                            int FunctionName = BitConverter.ToInt32(data, offset);  //Function name
                            offset += 4;
                            int FunctionIdx = BitConverter.ToInt32(data, offset);  //Function name idx
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Function Name: {CurrentLoadedExport.FileRef.GetNameEntry(FunctionName)}_{FunctionIdx}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafName
                            });
                            offset += 4;


                            int Parameter = BitConverter.ToInt32(data, offset);  //Parameter
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Parameter: {Parameter} ",
                                Name = "_" + offset,
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
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            tPlotID = BitConverter.ToInt32(data, offset);  //Plot
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Plot ID: {tPlotID} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tNewValue = BitConverter.ToInt32(data, offset);  //NewValue
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} New Value: {tNewValue} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafFloat
                            });
                            offset += 4;

                            int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                            bool bUseParam = false;
                            if (tUseParam == 1) { bUseParam = true; }
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tIncrement = BitConverter.ToInt32(data, offset);  //Increment bool
                            bool bIncrement = false;
                            if (tIncrement == 1) { bIncrement = true; }
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Increment value: {tIncrement}  {bIncrement} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;
                        }
                        else if (transTYPE == 5)  // TYPE 5 = LOCAL BOOL
                        {
                            var nTransition = new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Type: {transTYPE} Local Bool",
                                Name = "_" + offset,
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
                                Name = "_" + offset,
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
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tObjtag = BitConverter.ToInt32(data, offset);  //Use Object tag??
                            bool bObjtag = false;
                            if (tObjtag == 1) { bObjtag = true; }
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Object Tag: {tObjtag}  {bObjtag} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int FunctionName = BitConverter.ToInt32(data, offset);  //Function name
                            offset += 4;
                            int FunctionIdx = BitConverter.ToInt32(data, offset);  //Function name
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Function Name: {CurrentLoadedExport.FileRef.GetNameEntry(FunctionName)}_{FunctionIdx}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafName
                            });
                            offset += 4;

                            int TagName = BitConverter.ToInt32(data, offset);  //Object name
                            offset += 4;
                            int TagIdx = BitConverter.ToInt32(data, offset);  //Object idx
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Object Name: {CurrentLoadedExport.FileRef.GetNameEntry(TagName)}_{TagIdx}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafName
                            });
                            offset += 4;

                            int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                            bool bUseParam = false;
                            if (tUseParam == 1) { bUseParam = true; }
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tNewValue = BitConverter.ToInt32(data, offset);  //NewValue
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} New Value: {tNewValue} ",
                                Name = "_" + offset,
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
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            TransitionsIDs.Items.Add(nTransition);

                            int TransInstVersion = BitConverter.ToInt32(data, offset);  //Instance Version
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Instance Version: {TransInstVersion} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            tPlotID = BitConverter.ToInt32(data, offset);  //Plot
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Plot ID: {tPlotID} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tNewValue = BitConverter.ToInt32(data, offset);  //NewState Bool
                            bool bNewValue = false;
                            if (tNewValue == 1) { bNewValue = true; }
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} New State: {tNewValue}  {bNewValue}",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int tUseParam = BitConverter.ToInt32(data, offset);  //Use Parameter bool
                            bool bUseParam = false;
                            if (tUseParam == 1) { bUseParam = true; }
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Use parameter: {tUseParam}  {bUseParam} ",
                                Name = "_" + offset,
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
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int ParentIdx = BitConverter.ToInt32(data, offset);  //Parent Bool
                            nTransition.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Parent Bool: {ParentIdx} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            });
                            offset += 4;

                            int sibCount = BitConverter.ToInt32(data, offset); //Sibling Substates
                            var SiblingIDs = new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Sibling Substates Count: {sibCount} ",
                                Name = "_" + offset,
                                Tag = NodeType.StructLeafInt
                            };
                            offset += 4;
                            nTransition.Items.Add(SiblingIDs);

                            for (int s = 0; s < sibCount; s++)  //SIBLING SUBSTATE BOOLS
                            {
                                int nSibling = BitConverter.ToInt32(data, offset);
                                var nSiblings = new BinInterpNode
                                {
                                    Header = $"0x{offset:X5} Sibling: {s}  Bool: { nSibling }",
                                    Name = "_" + offset,
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
                    Name = "_" + offset,
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
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    QuestNode.Items.Add(QuestIDs);

                    int Unknown1 = BitConverter.ToInt32(data, offset); //Unknown1
                    QuestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Unknown: {Unknown1} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int Unknown2 = BitConverter.ToInt32(data, offset); //Unknown2
                    QuestIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Unknown: {Unknown2} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int gCount = BitConverter.ToInt32(data, offset); //Goal Count
                    var GoalsIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Goals: {gCount} ",
                        Name = "_" + offset,
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
                            Header = $"0x{offset:X5} Goal start plot/cnd: {goalStart} { startType }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        };
                        GoalsIDs.Items.Add(nGoalIDs);

                        int iGoalInstVersion = BitConverter.ToInt32(data, offset);  //Goal Instance Version
                        nGoalIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Goal Instance Version: {iGoalInstVersion} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int gTitle = BitConverter.ToInt32(data, offset); //Goal Name
                        string gttlkLookup = GlobalFindStrRefbyID(gTitle, game, CurrentLoadedExport.FileRef);
                        nGoalIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Goal Name StrRef: {gTitle} { gttlkLookup }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        int gDescription = BitConverter.ToInt32(data, offset); //Goal Description
                        string gdtlkLookup = GlobalFindStrRefbyID(gDescription, game, CurrentLoadedExport.FileRef);
                        nGoalIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Goal Description StrRef: {gDescription} { gdtlkLookup }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        gConditional = BitConverter.ToInt32(data, offset); //Conditional
                        nGoalIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Conditional: {gConditional} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        gState = BitConverter.ToInt32(data, offset); //State
                        nGoalIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Bool State: {gState} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }

                    int tCount = BitConverter.ToInt32(data, offset); //Task Count
                    var TaskIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Tasks Count: {tCount} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    QuestIDs.Items.Add(TaskIDs);

                    for (int t = 0; t < tCount; t++)  //TASKS
                    {

                        var nTaskIDs = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Task: {t}",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        };
                        TaskIDs.Items.Add(nTaskIDs);

                        int iTaskInstVersion = BitConverter.ToInt32(data, offset);  //Task Instance Version
                        nTaskIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Task Instance Version: {iTaskInstVersion} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int tFinish = BitConverter.ToInt32(data, offset); //Primary Codex
                        bool bFinish = false;
                        if (tFinish == 1) { bFinish = true; }
                        nTaskIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Task Finishes Quest: {tFinish}  { bFinish }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        int tTitle = BitConverter.ToInt32(data, offset); //Task Name
                        string tttlkLookup = GlobalFindStrRefbyID(tTitle, game, CurrentLoadedExport.FileRef);
                        nTaskIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Task Name StrRef: {tTitle} { tttlkLookup }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        int tDescription = BitConverter.ToInt32(data, offset); //Task Description
                        string tdtlkLookup = GlobalFindStrRefbyID(tDescription, game, CurrentLoadedExport.FileRef);
                        nTaskIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Task Description StrRef: {tDescription} { tdtlkLookup }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        int piCount = BitConverter.ToInt32(data, offset); //Plot item Count
                        var PlotIDs = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Plot Item Count: {piCount} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset += 4;
                        nTaskIDs.Items.Add(PlotIDs);

                        for (int pi = 0; pi < piCount; pi++)  //TASK PLOT ITEMS
                        {
                            int iPlotItem = BitConverter.ToInt32(data, offset);  //Plot item index
                            var nPlotItems = new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Plot items: {pi}  Index: { iPlotItem }",
                                Name = "_" + offset,
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
                            Header = $"0x{offset:X5} Planet Name: {CurrentLoadedExport.FileRef.GetNameEntry(planetName)}_{planetIdx} ",
                            Name = "_" + offset,
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
                            wpRef = Gibbed.IO.StreamHelpers.ReadString(ms, wpStrLgth, true, Encoding.ASCII);
                        }
                        nTaskIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Waypoint ref: {wpRef} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafStr
                        });
                        offset += wpStrLgth;
                    }

                    int pCount = BitConverter.ToInt32(data, offset); //Plot Item Count
                    var PlotItemIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Plot Items: {pCount} ",
                        Name = "_" + offset,
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
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        };
                        PlotItemIDs.Items.Add(nPlotItemIDs);

                        int iPlotInstVersion = BitConverter.ToInt32(data, offset);  //Plot Item Instance Version
                        nPlotItemIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Plot item Instance Version: {iPlotInstVersion} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int pTitle = BitConverter.ToInt32(data, offset); //Plot item Name
                        string pitlkLookup = GlobalFindStrRefbyID(pTitle, game, CurrentLoadedExport.FileRef);
                        nPlotItemIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Goal Name StrRef: {pTitle} { pitlkLookup }",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        int pIcon = BitConverter.ToInt32(data, offset); //Icon Index
                        nPlotItemIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Icon Index: {pIcon} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int pConditional = BitConverter.ToInt32(data, offset); //Conditional
                        nPlotItemIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Conditional: {pConditional} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int pState = BitConverter.ToInt32(data, offset); //Int
                        nPlotItemIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Integer State: {pState} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int pTarget = BitConverter.ToInt32(data, offset); //Target Index
                        nPlotItemIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Item Count Target: {pTarget} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }

                }

                int bsCount = BitConverter.ToInt32(data, offset);
                var bsNode = new BinInterpNode
                {
                    Header = $"0x{offset:X4} Bool Journal Events: {bsCount}",
                    Name = "_" + offset,
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
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    bsNode.Items.Add(BoolEvtIDs);

                    int bsInstVersion = BitConverter.ToInt32(data, offset); //Instance Version
                    var BoolQuestIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Instance Version: {bsInstVersion} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    BoolEvtIDs.Items.Add(BoolQuestIDs);

                    int bqstCount = BitConverter.ToInt32(data, offset); //Related Quests Count
                    var bqstNode = new BinInterpNode
                    {
                        Header = $"0x{offset:X4} Related Quests: {bqstCount}",
                        Name = "_" + offset,
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
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset -= 16;
                        bqstNode.Items.Add(bquestIDs);

                        int bqInstVersion = BitConverter.ToInt32(data, offset);  //Bool quest Instance Version
                        bquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Instance Version: {bqInstVersion} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int bqTask = BitConverter.ToInt32(data, offset);  //Bool quest Instance Version
                        bquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Related Task Link: {bqTask} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int bqState = BitConverter.ToInt32(data, offset);  //Bool quest State
                        bquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Bool State: {bqState} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int bqConditional = BitConverter.ToInt32(data, offset);  //Bool quest Conditional
                        bquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Conditional: {bqConditional} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;


                        bqQuest = BitConverter.ToInt32(data, offset);  //Bool quest ID
                        bquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Quest Link: {bqQuest} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }

                }

                int isCount = BitConverter.ToInt32(data, offset);
                var isNode = new BinInterpNode
                {
                    Header = $"0x{offset:X4} Int Journal Events: {isCount}",
                    Name = "_" + offset,
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
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    isNode.Items.Add(IntEvtIDs);

                    int isInstVersion = BitConverter.ToInt32(data, offset); //Instance Version
                    var IntQuestIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Instance Version: {isInstVersion} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    IntEvtIDs.Items.Add(IntQuestIDs);

                    int iqstCount = BitConverter.ToInt32(data, offset); //Related Quests Count
                    var iqstNode = new BinInterpNode
                    {
                        Header = $"0x{offset:X4} Related Quests: {iqstCount}",
                        Name = "_" + offset,
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
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset -= 16;
                        iqstNode.Items.Add(iquestIDs);

                        int iqInstVersion = BitConverter.ToInt32(data, offset);  //Int quest Instance Version
                        iquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Instance Version: {iqInstVersion} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int iqTask = BitConverter.ToInt32(data, offset);  //Int quest Instance Version
                        iquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Related Task Link: {iqTask} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int iqState = BitConverter.ToInt32(data, offset);  //Int quest State
                        iquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Bool State: {iqState} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int iqConditional = BitConverter.ToInt32(data, offset);  //Int quest Conditional
                        iquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Conditional: {iqConditional} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        iqQuest = BitConverter.ToInt32(data, offset);  //Int quest ID
                        iquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Quest Link: {iqQuest} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }

                }

                int fsCount = BitConverter.ToInt32(data, offset);
                var fsNode = new BinInterpNode
                {
                    Header = $"0x{offset:X4} Float Journal Events: {fsCount}",
                    Name = "_" + offset,
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
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    fsNode.Items.Add(FloatEvtIDs);

                    int fsInstVersion = BitConverter.ToInt32(data, offset); //Instance Version
                    var FloatQuestIDs = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Instance Version: {fsInstVersion} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    FloatEvtIDs.Items.Add(FloatQuestIDs);

                    int fqstCount = BitConverter.ToInt32(data, offset); //Related Quests Count
                    var fqstNode = new BinInterpNode
                    {
                        Header = $"0x{offset:X4} Related Quests: {fqstCount}",
                        Name = "_" + offset,
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
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        };
                        offset -= 16;
                        fqstNode.Items.Add(fquestIDs);

                        int fqInstVersion = BitConverter.ToInt32(data, offset);  //float quest Instance Version
                        fquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Instance Version: {fqInstVersion} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int fqTask = BitConverter.ToInt32(data, offset);  //Float quest Instance Version
                        fquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Related Task Link: {fqTask} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int fqState = BitConverter.ToInt32(data, offset);  //Float quest State
                        fquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Bool State: {fqState} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        int fqConditional = BitConverter.ToInt32(data, offset);  //Float quest Conditional
                        fquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Conditional: {fqConditional} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;

                        fqQuest = BitConverter.ToInt32(data, offset);  //Float quest ID
                        fquestIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Quest Link: {fqQuest} ",
                            Name = "_" + offset,
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
                    Name = "_" + offset,
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
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    SectionsNode.Items.Add(SectionIDs);

                    int instVersion = BitConverter.ToInt32(data, offset); //Instance Version
                    SectionIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Instance Version: {instVersion} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int sTitle = BitConverter.ToInt32(data, offset); //Codex Title
                    string ttlkLookup = GlobalFindStrRefbyID(sTitle, game, CurrentLoadedExport.FileRef);
                    SectionIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Section Title StrRef: {sTitle} { ttlkLookup }",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    int sDescription = BitConverter.ToInt32(data, offset); //Codex Description
                    string dtlkLookup = GlobalFindStrRefbyID(sDescription, game, CurrentLoadedExport.FileRef);
                    SectionIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Section Description StrRef: {sDescription} { dtlkLookup }",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    int sTexture = BitConverter.ToInt32(data, offset); //Texture ID
                    SectionIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Section Texture ID: {sTexture} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    int sPriority = BitConverter.ToInt32(data, offset); //Priority
                    SectionIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Section Priority: {sPriority}  (5 is low, 1 is high)",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                    if (instVersion >= 3)
                    {
                        int sndExport = BitConverter.ToInt32(data, offset);
                        SectionIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X8} Codex Sound: {sndExport} {CurrentLoadedExport.FileRef.GetEntryString(sndExport)}",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;
                    }

                    int sPrimary = BitConverter.ToInt32(data, offset); //Primary Codex
                    bool bPrimary = false;
                    if (sPrimary == 1) { bPrimary = true; }
                    SectionIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Is Primary Codex: {sPrimary}  { bPrimary }",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafObject
                    });
                    offset += 4;

                }
                //START OF CODEX PAGES SECTION
                int pCount = BitConverter.ToInt32(data, offset);
                var PagesNode = new BinInterpNode
                {
                    Header = $"0x{offset:X4} Codex Page Count: {pCount}",
                    Name = "_" + offset,
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
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    };
                    offset += 4;
                    PagesNode.Items.Add(PageIDs);

                    int instVersion = BitConverter.ToInt32(data, offset); //Instance Version
                    PageIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Instance Version: {instVersion} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int pTitle = BitConverter.ToInt32(data, offset); //Codex Title
                    string ttlkLookup = GlobalFindStrRefbyID(pTitle, game, CurrentLoadedExport.FileRef);
                    PageIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Page Title StrRef: {pTitle} { ttlkLookup }",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int pDescription = BitConverter.ToInt32(data, offset); //Codex Description
                    string dtlkLookup = GlobalFindStrRefbyID(pDescription, game, CurrentLoadedExport.FileRef);
                    PageIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Page Description StrRef: {pDescription} { dtlkLookup }",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int pTexture = BitConverter.ToInt32(data, offset); //Texture ID
                    PageIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Section Texture ID: {pTexture} ",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    int pPriority = BitConverter.ToInt32(data, offset); //Priority
                    PageIDs.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Section Priority: {pPriority}  (5 is low, 1 is high)",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    if (instVersion == 4) //ME3 use object reference found sound then section
                    {
                        int sndExport = BitConverter.ToInt32(data, offset);
                        PageIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X8} Codex Sound: {sndExport} {CurrentLoadedExport.FileRef.GetEntryString(sndExport)}",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafObject
                        });
                        offset += 4;

                        int pSection = BitConverter.ToInt32(data, offset); //Section ID
                        PageIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Section Reference: {pSection} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }
                    else if (instVersion == 3) //ME2 use Section then no sound reference 
                    {
                        int pSection = BitConverter.ToInt32(data, offset); //Section ID
                        PageIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Section Reference: {pSection} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafInt
                        });
                        offset += 4;
                    }
                    else  //ME1 has different order (section ID then codex sound) and uses a string reference.
                    {
                        int pSection = BitConverter.ToInt32(data, offset); //Section ID
                        PageIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Section Reference: {pSection} ",
                            Name = "_" + offset,
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
                            sndRef = Gibbed.IO.StreamHelpers.ReadString(ms, sndStrLgth, true, Encoding.ASCII);
                        }
                        PageIDs.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} SoundRef String: {sndRef} ",
                            Name = "_" + offset,
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

        private List<ITreeItem> StartAnimSequenceScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            var game = CurrentLoadedExport.FileRef.Game;

            try
            {
                var TrackOffsets = CurrentLoadedExport.GetProperty<ArrayProperty<IntProperty>>("CompressedTrackOffsets");
                var animsetData = CurrentLoadedExport.GetProperty<ObjectProperty>("m_pBioAnimSetData");
                var boneList = Pcc.GetUExport(animsetData.Value).GetProperty<ArrayProperty<NameProperty>>("TrackBoneNames");
                Enum.TryParse(CurrentLoadedExport.GetProperty<EnumProperty>("RotationCompressionFormat").Value.Name, out AnimationCompressionFormat rotCompression);
                int offset = binarystart;

                int binLength = BitConverter.ToInt32(data, offset);
                var LengthNode = new BinInterpNode
                {
                    Header = $"0x{offset:X4} AnimBinary length: {binLength}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                };
                offset += 4;
                subnodes.Add(LengthNode);
                var animBinStart = offset;

                int bone = 0;

                for (int i = 0; i < TrackOffsets.Count; i++)
                {
                    var bonePosOffset = TrackOffsets[i].Value;
                    i++;
                    var bonePosCount = TrackOffsets[i].Value;
                    var BoneID = new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Bone: {bone} {boneList[bone].Value}",
                        Name = "_" + offset,
                        Tag = NodeType.Unknown
                    };
                    subnodes.Add(BoneID);

                    for (int j = 0; j < bonePosCount; j++)
                    {
                        offset = animBinStart + bonePosOffset + j * 12;
                        var PosKeys = new BinInterpNode
                        {
                            Header = $"0x{offset:X5} PosKey {j}",
                            Name = "_" + offset,
                            Tag = NodeType.Unknown
                        };
                        BoneID.Items.Add(PosKeys);


                        var posX = BitConverter.ToSingle(data, offset);
                        PosKeys.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} X: {posX} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafFloat
                        });
                        offset += 4;
                        var posY = BitConverter.ToSingle(data, offset);
                        PosKeys.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Y: {posY} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafFloat
                        });
                        offset += 4;
                        var posZ = BitConverter.ToSingle(data, offset);
                        PosKeys.Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Z: {posZ} ",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafFloat
                        });
                        offset += 4;

                    }
                    i++;
                    var boneRotOffset = TrackOffsets[i].Value;
                    i++;
                    var boneRotCount = TrackOffsets[i].Value;
                    int l = 12; // 12 length of rotation by default
                    var offsetRotX = boneRotOffset;
                    var offsetRotY = boneRotOffset;
                    var offsetRotZ = boneRotOffset;
                    var offsetRotW = boneRotOffset;
                    for (int j = 0; j < boneRotCount; j++)
                    {
                        float rotX = 0;
                        float rotY = 0;
                        float rotZ = 0;
                        float rotW = 0;

                        switch (rotCompression)
                        {
                            case AnimationCompressionFormat.ACF_None:
                                l = 16;
                                offset = animBinStart + boneRotOffset + j * l;
                                offsetRotX = offset;
                                rotX = BitConverter.ToSingle(data, offset);
                                offset += 4;
                                offsetRotY = offset;
                                rotY = BitConverter.ToSingle(data, offset);
                                offset += 4;
                                offsetRotZ = offset;
                                rotZ = BitConverter.ToSingle(data, offset);
                                offset += 4;
                                offsetRotW = offset;
                                rotW = BitConverter.ToSingle(data, offset);
                                offset += 4;
                                break;
                            case AnimationCompressionFormat.ACF_Float96NoW:
                                offset = animBinStart + boneRotOffset + j * l;
                                offsetRotX = offset;
                                rotX = BitConverter.ToSingle(data, offset);
                                offset += 4;
                                offsetRotY = offset;
                                rotY = BitConverter.ToSingle(data, offset);
                                offset += 4;
                                offsetRotZ = offset;
                                rotZ = BitConverter.ToSingle(data, offset);
                                offset += 4;
                                break;
                            case AnimationCompressionFormat.ACF_Fixed48NoW: // normalized quaternion with 3 16-bit fixed point fields
                                                                            //FQuat r;
                                                                            //r.X = (X - 32767) / 32767.0f;
                                                                            //r.Y = (Y - 32767) / 32767.0f;
                                                                            //r.Z = (Z - 32767) / 32767.0f;
                                                                            //RESTORE_QUAT_W(r);
                                                                            //break;
                            case AnimationCompressionFormat.ACF_Fixed32NoW:// normalized quaternion with 11/11/10-bit fixed point fields
                                                                           //FQuat r;
                                                                           //r.X = X / 1023.0f - 1.0f;
                                                                           //r.Y = Y / 1023.0f - 1.0f;
                                                                           //r.Z = Z / 511.0f - 1.0f;
                                                                           //RESTORE_QUAT_W(r);
                                                                           //break;
                            case AnimationCompressionFormat.ACF_IntervalFixed32NoW:
                            //FQuat r;
                            //r.X = (X / 1023.0f - 1.0f) * Ranges.X + Mins.X;
                            //r.Y = (Y / 1023.0f - 1.0f) * Ranges.Y + Mins.Y;
                            //r.Z = (Z / 511.0f - 1.0f) * Ranges.Z + Mins.Z;
                            //RESTORE_QUAT_W(r);
                            //break;
                            case AnimationCompressionFormat.ACF_Float32NoW:
                                //FQuat r;

                                //int _X = data >> 21;            // 11 bits
                                //int _Y = (data >> 10) & 0x7FF;  // 11 bits
                                //int _Z = data & 0x3FF;          // 10 bits

                                //*(unsigned*)&r.X = ((((_X >> 7) & 7) + 123) << 23) | ((_X & 0x7F | 32 * (_X & 0xFFFFFC00)) << 16);
                                //*(unsigned*)&r.Y = ((((_Y >> 7) & 7) + 123) << 23) | ((_Y & 0x7F | 32 * (_Y & 0xFFFFFC00)) << 16);
                                //*(unsigned*)&r.Z = ((((_Z >> 6) & 7) + 123) << 23) | ((_Z & 0x3F | 32 * (_Z & 0xFFFFFE00)) << 17);

                                //RESTORE_QUAT_W(r);


                                break;
                            case AnimationCompressionFormat.ACF_BioFixed48:
                                offset = animBinStart + boneRotOffset + j * l;
                                const float shift = 0.70710678118f;
                                const float scale = 1.41421356237f;
                                offsetRotX = offset;
                                rotX = (data[0] & 0x7FFF) / 32767.0f * scale - shift;
                                offset += 4;
                                offsetRotY = offset;
                                rotY = (data[1] & 0x7FFF) / 32767.0f * scale - shift;
                                offset += 4;
                                offsetRotZ = offset;
                                rotZ = (data[2] & 0x7FFF) / 32767.0f * scale - shift;
                                //float w = 1.0f - (rotX * rotX + rotY * rotY + rotZ * rotZ);
                                //w = w >= 0.0f ? (float)Math.Sqrt(w) : 0.0f;
                                //int s = ((data[0] >> 14) & 2) | ((data[1] >> 15) & 1);
                                break;
                        }

                        if (rotCompression == AnimationCompressionFormat.ACF_BioFixed48 || rotCompression == AnimationCompressionFormat.ACF_Float96NoW || rotCompression == AnimationCompressionFormat.ACF_None)
                        {
                            var RotKeys = new BinInterpNode
                            {
                                Header = $"0x{offsetRotX:X5} RotKey {j}",
                                Name = "_" + offsetRotX,
                                Tag = NodeType.Unknown
                            };
                            BoneID.Items.Add(RotKeys);
                            RotKeys.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offsetRotX:X5} RotX: {rotX} ",
                                Name = "_" + offsetRotX,
                                Tag = NodeType.StructLeafFloat
                            });
                            RotKeys.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offsetRotY:X5} RotY: {rotY} ",
                                Name = "_" + offsetRotY,
                                Tag = NodeType.StructLeafFloat
                            });
                            RotKeys.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offsetRotZ:X5} RotZ: {rotZ} ",
                                Name = "_" + offsetRotZ,
                                Tag = NodeType.StructLeafFloat
                            });
                            if (rotCompression == AnimationCompressionFormat.ACF_None)
                            {
                                RotKeys.Items.Add(new BinInterpNode
                                {
                                    Header = $"0x{offsetRotW:X5} RotW: {rotW} ",
                                    Name = "_" + offsetRotW,
                                    Tag = NodeType.StructLeafFloat
                                });
                            }
                        }
                        else
                        {

                            BoneID.Items.Add(new BinInterpNode
                            {
                                Header = $"0x{offset:X5} Rotationformat {rotCompression} cannot be parsed at this time.",
                                Name = "_" + offset,
                                Tag = NodeType.Unknown
                            });
                        }

                    }
                    bone++;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> StartFaceFXAnimSetScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(CurrentLoadedExport.Data);
                bin.JumpTo(binarystart);
                bin.Skip(4);
                subnodes.Add(new BinInterpNode(bin.Position, $"Magic: {bin.ReadInt32():X8}") { Length = 4 });
                subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
                if (Pcc.Game == MEGame.ME3)
                {
                    subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
                }

                if (Pcc.Game == MEGame.ME2)
                {
                    subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }
                subnodes.Add(new BinInterpNode(bin.Position, $"Licensee: {bin.ReadStringASCII(bin.ReadInt32())}"));
                if (Pcc.Game == MEGame.ME2)
                {
                    subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }
                subnodes.Add(new BinInterpNode(bin.Position, $"Project: {bin.ReadStringASCII(bin.ReadInt32())}"));
                subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
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
                            hNodeNodes.Add(new BinInterpNode(bin.Position, $"Name: {bin.ReadStringASCII(bin.ReadInt32())}"));
                            hNodeNodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                        }
                    }
                }

                int nameCount = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Names: {nameCount} items")
                {
                    //ME2 different to ME3/1
                    Items = ReadList(nameCount, i => new BinInterpNode(bin.Skip(Pcc.Game != MEGame.ME2 ? 0 : 4).Position, $"{bin.ReadStringASCII(bin.ReadInt32())}"))
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
                        animNodes.Add(MakeInt32Node(bin, "Index"));
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
                    nodes.Add(new BinInterpNode(bin.Position, $"Path: {bin.ReadStringASCII(bin.ReadInt32())}"));
                    if (Pcc.Game == MEGame.ME2)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                    nodes.Add(new BinInterpNode(bin.Position, $"ID: {bin.ReadStringASCII(bin.ReadInt32())}"));
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
                var bin = new MemoryStream(CurrentLoadedExport.Data);
                bin.JumpTo(binarystart);
                bin.Skip(4);
                subnodes.Add(new BinInterpNode(bin.Position, $"Magic: {bin.ReadInt32():X8}") { Length = 4 });
                int versionID = bin.ReadInt32(); //1710 = ME1, 1610 = ME2, 1731 = ME3.
                subnodes.Add(new BinInterpNode(bin.Position, $"Version: {versionID} {versionID:X8}") { Length = 4 });
                if (versionID == 1731)
                {
                    subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
                }

                if (versionID == 1610)
                {
                    subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }
                subnodes.Add(new BinInterpNode(bin.Position, $"Licensee: {bin.ReadStringASCII(bin.ReadInt32())}"));
                if (versionID == 1610)
                {
                    subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }
                subnodes.Add(new BinInterpNode(bin.Position, $"Project: {bin.ReadStringASCII(bin.ReadInt32())}"));
                subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
                if (versionID == 1610)
                {
                    subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt32():X8}") { Length = 4 });
                }
                else
                {
                    subnodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                }
                //Node Table
                if (versionID != 1610)
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
                            hNodeNodes.Add(new BinInterpNode(bin.Position, $"Name: {bin.ReadStringASCII(bin.ReadInt32())}"));
                            hNodeNodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                        }
                    }
                }

                //Name Table
                var nameTable = new List<string>();
                int nameCount = bin.ReadInt32();
                var nametablePos = bin.Position - 4;
                var nametabObj = new List<ITreeItem>();
                for (int m = 0; m < nameCount; m++)
                {
                    var pos = bin.Position;
                    var mName = bin.ReadStringASCII(bin.ReadInt32());
                    nameTable.Add(mName);
                    nametabObj.Add(new BinInterpNode(bin.Skip(versionID != 1610 ? 0 : 4).Position, $"{m}: {mName}"));
                }

                subnodes.Add(new BinInterpNode(nametablePos, $"Names: {nameCount} items")
                {
                    //ME1 and ME3 same, ME2 different
                    Items = nametabObj
                });

                subnodes.Add(MakeInt32Node(bin, "Unknown"));
                subnodes.Add(MakeInt32Node(bin, "Unknown"));


                //FROM HERE ME3 ONLY WIP
                //LIST A
                var unkListA = new List<ITreeItem>();
                var countA = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Unknown Table A: {countA} items")
                {
                    Items = unkListA
                });

                for (int a = 0; a < countA; a++) //NOT EXACT??
                {
                    var tableItems = new List<ITreeItem>();
                    unkListA.Add(new BinInterpNode(bin.Position, $"Table Index: {bin.ReadInt32()}")
                    {
                        Items = tableItems
                    });
                    bool iscontinuing = true;
                    while (iscontinuing)
                    {
                        var loc = bin.Position;
                        var item = bin.ReadInt32();
                        if (item == 2147483647)
                        {
                            tableItems.Add(new BinInterpNode(bin.Position - 4, $"End Marker: FF FF FF 7F") { Length = 4 });
                            tableItems.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                            iscontinuing = false;
                            break;
                        }
                        else
                        {
                            bin.Position = loc;
                            tableItems.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                        }

                    }
                    //Name list to Bones and other facefx?
                    var unkNameList1 = new List<ITreeItem>();
                    var countUk1 = bin.ReadInt32();
                    tableItems.Add(new BinInterpNode(bin.Position - 4, $"Unknown Name List: {countUk1} items")
                    {
                        Items = unkNameList1
                    });
                    for (int b = 0; b < countUk1; b++)
                    {
                        var unameVal = bin.ReadInt32();
                        var unkNameList1items = new List<ITreeItem>();
                        unkNameList1.Add(new BinInterpNode(bin.Position - 4, $"Name {b}: {unameVal} {nameTable[unameVal]}")
                        {
                            Items = unkNameList1items
                        });
                        unkNameList1items.Add(MakeInt32Node(bin, "Table index"));
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

                //LIST B
                var unkListB = new List<ITreeItem>();
                var countB = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Unknown Table B: {countB} items")
                {
                    Items = unkListB
                });

                for (int b = 0, i = 0; i < countB; b++, i++)
                {
                    var bLocation = bin.Position;
                    var firstval = bin.ReadInt32();  //maybe version id?
                    var bIdxVal = bin.ReadInt32();
                    var unkListBitems = new List<ITreeItem>();
                    unkListB.Add(new BinInterpNode(bin.Position - 4, $"{b}: Table Index: {bIdxVal} : {nameTable[bIdxVal]}")
                    {
                        Items = unkListBitems
                    });
                    switch (firstval)
                    {
                        case 2:
                            unkListBitems.Add(new BinInterpNode(bin.Position - 8, $"Version??: {firstval}"));
                            unkListBitems.Add(new BinInterpNode(bin.Position - 4, $"Table index: {bIdxVal}"));
                            unkListBitems.Add(MakeInt32Node(bin, "Unknown int"));
                            unkListBitems.Add(MakeInt32Node(bin, "Unknown int"));
                            unkListBitems.Add(MakeInt32Node(bin, "Unknown int"));
                            break;
                        default:
                            unkListBitems.Add(new BinInterpNode(bin.Position - 8, $"Version??: {firstval}"));
                            unkListBitems.Add(new BinInterpNode(bin.Position - 4, $"Table index: {bIdxVal}"));
                            int flagMaybe = bin.ReadInt32();
                            unkListBitems.Add(new BinInterpNode(bin.Position - 4, $"another version?: {flagMaybe}") { Length = 4 });
                            unkListBitems.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                            unkListBitems.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                            unkListBitems.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                            bool hasNameList;
                            if (flagMaybe == 0)
                            {
                                unkListBitems.Add(MakeInt32Node(bin, "Unknown int"));
                                hasNameList = true;
                            }
                            else
                            {
                                var unkStringLength = bin.ReadInt32();
                                unkListBitems.Add(new BinInterpNode(bin.Position - 4, $"Unknown String: {bin.ReadStringASCII(unkStringLength)}"));
                                hasNameList = unkStringLength == 0;
                            }
                            if (hasNameList)
                            {
                                var unkNameList2 = new List<ITreeItem>(); //Name list to Bones and other facefx phenomes?
                                var countUk2 = bin.ReadInt32();
                                unkListBitems.Add(new BinInterpNode(bin.Position - 4, $"Unknown Name List: {countUk2} items")
                                {
                                    Items = unkNameList2
                                });
                                for (int n2 = 0; n2 < countUk2; n2++)
                                {
                                    var unameVal = bin.ReadInt32();
                                    var unkNameList2items = new List<ITreeItem>();
                                    unkNameList2.Add(new BinInterpNode(bin.Position - 4, $"Name: {unameVal} {nameTable[unameVal]}")
                                    {
                                        Items = unkNameList2items
                                    });
                                    unkNameList2items.Add(MakeInt32Node(bin, "Unknown int"));
                                    var n3count = bin.ReadInt32();
                                    unkNameList2items.Add(new BinInterpNode(bin.Position - 4, $"Unknown count: {n3count}"));
                                    for (int n3 = 0; n3 < n3count; n3++)
                                    {
                                        unkNameList2items.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                                    }
                                }
                                if (firstval != 6)
                                {
                                    unkListBitems.Add(new BinInterpNode(bin.Position, $"Unknown float: {bin.ReadSingle()}") { Length = 4 });
                                }
                            }
                            break;
                    }

                    if (firstval == 6)
                    {
                        i -= 2;
                    }
                }

                var unkListC = new List<ITreeItem>();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Unknown Table C")
                {
                    Items = unkListC
                });

                for (int c = 0; c < countB; c++)
                {
                    var unkListCitems = new List<ITreeItem>();
                    unkListC.Add(new BinInterpNode(bin.Position, $"{c}")
                    {
                        Items = unkListCitems
                    });
                    int name = bin.ReadInt32();
                    unkListCitems.Add(new BinInterpNode(bin.Position, $"Name?: {name} {nameTable[name]}") { Length = 4 });
                    unkListCitems.Add(MakeInt32Node(bin, "Unknown int"));
                    int stringCount = bin.ReadInt32();
                    unkListCitems.Add(new BinInterpNode(bin.Position - 4, $"Unknown int: {stringCount}") { Length = 4 });
                    unkListCitems.Add(new BinInterpNode(bin.Position, $"Unknown String: {bin.ReadStringASCII(bin.ReadInt32())}"));
                    for (int i = 1; i < stringCount; i++)
                    {
                        c++;
                        unkListCitems.Add(MakeInt32Node(bin, "Unknown int"));
                        unkListCitems.Add(new BinInterpNode(bin.Position, $"Unknown String: {bin.ReadStringASCII(bin.ReadInt32())}"));
                    }

                }



                if (versionID == 1610)
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
                    nodes.Add(new BinInterpNode(bin.Position, $"Name: {nameTable[bin.ReadInt32()]}") { Length = 4 });
                    nodes.Add(MakeInt32Node(bin, "Unknown"));
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
                        if (versionID == 1610)
                        {
                            animNodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                        }
                    }

                    if (animationCount > 0)
                    {
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
                            if (versionID == 1610)
                            {
                                nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                            }
                            nodes.Add(new BinInterpNode(bin.Position, $"NumKeys: {bin.ReadInt32()} items")
                            {
                                Items = ReadList(bin.Skip(-4).ReadInt32(), j => new BinInterpNode(bin.Position, $"{bin.ReadInt32()} keys"))
                            });
                        }
                    }
                    nodes.Add(new BinInterpNode(bin.Position, $"Fade In Time: {bin.ReadFloat()}") { Length = 4 });
                    nodes.Add(new BinInterpNode(bin.Position, $"Fade Out Time: {bin.ReadFloat()}") { Length = 4 });
                    nodes.Add(MakeInt32Node(bin, "Unknown"));
                    if (versionID == 1610)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                        nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                    nodes.Add(new BinInterpNode(bin.Position, $"Path: {bin.ReadStringASCII(bin.ReadInt32())}"));
                    if (versionID == 1610)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"Unknown: {bin.ReadInt16()}") { Length = 2 });
                    }
                    nodes.Add(new BinInterpNode(bin.Position, $"ID: {bin.ReadStringASCII(bin.ReadInt32())}"));
                    nodes.Add(MakeInt32Node(bin, "index"));
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }
        private List<ITreeItem> StartSoundCueScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                int offset = binarystart;


                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} NextItemCompilingChain: {classObjTree} {CurrentLoadedExport.FileRef.GetEntryString(classObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;
                /*
                offset = binarystart + 0x18;

                MemoryStream ms = new MemoryStream(data);
                ms.Position = offset;
                var scriptStructProperties = PropertyCollection.ReadProps(CurrentLoadedExport.FileRef, ms, "ScriptStruct", includeNoneProperty: true);

                UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                foreach (UProperty prop in scriptStructProperties)
                {
                    InterpreterWPF.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
                }*/
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> StartScriptStructScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                int offset = binarystart + 0x4;

                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} NextItemCompilingChain: {classObjTree} {CurrentLoadedExport.FileRef.GetEntryString(classObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int childObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} ChildCompilingChain: {childObjTree} {CurrentLoadedExport.FileRef.GetEntryString(childObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                offset = binarystart + (CurrentLoadedExport.FileRef.Game == MEGame.ME3 ? 0x18 : 0x24);

                MemoryStream ms = new MemoryStream(data);
                ms.Position = offset;
                var scriptStructProperties = PropertyCollection.ReadProps(CurrentLoadedExport, ms, "ScriptStruct", includeNoneProperty: true, entry: CurrentLoadedExport);

                UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                foreach (UProperty prop in scriptStructProperties)
                {
                    InterpreterWPF.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
                }
                subnodes.AddRange(topLevelTree.ChildrenProperties);

                //subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                //{
                //    Header = $"{offset:X4} Name: {CurrentLoadedExport.FileRef.getNameEntry(entryUIndex)}",
                //    Name = "_" + offset.ToString()
                //});
                //offset += 12;


                /*
                for (int i = 0; i < count; i++)
                {
                    int name1 = BitConverter.ToInt32(data, offset);
                    int name2 = BitConverter.ToInt32(data, offset + 8);
                    string text = $"{offset:X4} Item {i}: {CurrentLoadedExport.FileRef.getNameEntry(name1)} => {CurrentLoadedExport.FileRef.getNameEntry(name2)}";
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = text,
                        Name = "_" + offset.ToString()
                    });
                    offset += 16;
                }*/
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> StartBioGestureRuntimeDataScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);
                subnodes.Add(MakeArrayNode(bin, "AnimToPackageMap?", i => new BinInterpNode(bin.Position, $"{bin.ReadNameReference(Pcc)} => {bin.ReadNameReference(Pcc)}")));

                int count;
                var propDataNode = new BinInterpNode(bin.Position, $"PropData? ({count = bin.ReadInt32()} items)");
                subnodes.Add(propDataNode);
                for (int i = 0; i < count; i++)
                {
                    BinInterpNode node = new BinInterpNode(bin.Position, $"{i}")
                    {
                        IsExpanded = true
                    };
                    propDataNode.Items.Add(node);
                    node.Items.Add(MakeNameNode(bin, "Name1"));
                    node.Items.Add(MakeNameNode(bin, "Name2"));
                    node.Items.Add(MakeStringNode(bin, "Model Path?"));
                    node.Items.Add(MakeNameNode(bin, "Name3"));
                    node.Items.Add(MakeVectorNode(bin, "Position?"));
                    node.Items.Add(MakeRotatorNode(bin, "Rotation?"));
                    node.Items.Add(MakeVectorNode(bin, "Scale3D?"));
                    int count2;
                    var propActionsNode = new BinInterpNode(bin.Position, $"prop actions? ({count2 = bin.ReadInt32()} items)");
                    node.Items.Add(propActionsNode);
                    for (int j = 0; j < count2; j++)
                    {
                        BinInterpNode node2 = new BinInterpNode(bin.Position, $"{j}")
                        {
                            IsExpanded = true
                        };
                        propActionsNode.Items.Add(node2);
                        node2.Items.Add(MakeNameNode(bin, "Name1"));
                        node2.Items.Add(MakeNameNode(bin, "Name2"));
                        node2.Items.Add(MakeInt32Node(bin, "unk"));
                        node2.Items.Add(MakeNameNode(bin, "Name3"));
                        node2.Items.Add(MakeVectorNode(bin, "Position?"));
                        node2.Items.Add(MakeRotatorNode(bin, "Rotation?"));
                        node2.Items.Add(MakeVectorNode(bin, "Scale3D?"));
                        node2.Items.Add(MakeStringNode(bin, "Model Path?"));
                        node2.Items.Add(MakeInt32Node(bin, "unk"));
                        node2.Items.Add(MakeInt32Node(bin, "unk"));
                        node2.Items.Add(MakeVectorNode(bin, "unk?"));
                        node2.Items.Add(MakeVectorNode(bin, "unk?"));
                        node2.Items.Add(MakeNameNode(bin, "Name?"));
                        node2.Items.Add(MakeVectorNode(bin, "unk?"));
                        node2.Items.Add(MakeVectorNode(bin, "unk?"));
                    }
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }
        private List<ITreeItem> StartObjectRedirectorScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();

            int redirnum = BitConverter.ToInt32(data, binarystart);
            subnodes.Add(new BinInterpNode
            {
                Header = $"{binarystart:X4} Redirect references to this export to: {redirnum} {CurrentLoadedExport.FileRef.GetEntry(redirnum).InstancedFullPath}",
                Name = "_" + binarystart.ToString()
            });
            return subnodes;
        }

        private List<ITreeItem> StartObjectScan(byte[] data)
        {
            var subnodes = new List<ITreeItem>();
            try
            {

                var bin = new MemoryStream(data);
                int offset = 0; //this property starts at 0 for parsing

                subnodes.Add(MakeInt32Node(bin, "Unreal Unique Index"));
                subnodes.Add(MakeNameNode(bin, "Unreal None property"));
                subnodes.Add(MakeEntryNode(bin, "Superclass"));
                subnodes.Add(MakeEntryNode(bin, "Next item in compiling chain"));
                subnodes.Add(MakeInt32Node(bin, "Unknown1"));

                UnrealFlags.EPropertyFlags ObjectFlagsMask = (UnrealFlags.EPropertyFlags)bin.ReadUInt64();
                BinInterpNode objectFlagsNode;
                subnodes.Add(objectFlagsNode = new BinInterpNode(bin.Position - 8, $"ObjectFlags: 0x{(ulong)ObjectFlagsMask:X16}")
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
                            Name = "_" + bin.Position
                        });
                    }
                }

                subnodes.Add(MakeNameNode(bin, "Unreal None property"));
                subnodes.Add(MakeInt32Node(bin, "Unknown1"));

                switch (CurrentLoadedExport.ClassName)
                {
                    case "ByteProperty":
                    case "StructProperty":
                    case "ObjectProperty":
                    case "ComponentProperty":
                        {
                            if ((ObjectFlagsMask & UnrealFlags.EPropertyFlags.RepRetry) != 0)
                            {
                                bin.Skip(2);
                            }
                            subnodes.Add(MakeEntryNode(bin, "Holds objects of type"));
                        }
                        break;
                    case "DelegateProperty":
                        subnodes.Add(MakeEntryNode(bin, "Holds objects of type"));
                        subnodes.Add(MakeEntryNode(bin, "Same as above but only if this in a function"));
                        break;
                    case "ArrayProperty":
                        {
                            subnodes.Add(MakeEntryNode(bin, "Holds objects of type"));
                        }
                        break;
                    case "ClassProperty":
                        {

                            subnodes.Add(MakeEntryNode(bin, "Outer class"));
                            subnodes.Add(MakeEntryNode(bin, "Class type"));
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> Scan_WwiseStreamBank(byte[] data)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                int pos = CurrentLoadedExport.propsEnd();
                if (Pcc.Game == MEGame.ME2)
                {
                    pos += CurrentLoadedExport.ClassName == "WwiseBank" ? 8 : 32;
                }

                int unk1 = BitConverter.ToInt32(data, pos);
                int DataSize = BitConverter.ToInt32(data, pos + 4);
                int DataSize2 = BitConverter.ToInt32(data, pos + 8);
                int DataOffset = BitConverter.ToInt32(data, pos + 0xC);

                subnodes.Add(new BinInterpNode
                {
                    Header = $"{pos:X4} Unknown: {unk1}",
                    Name = "_" + pos,
                });
                pos += 4;
                string dataset1type = CurrentLoadedExport.ClassName == "WwiseStream" ? "Stream length" : "Bank size";
                subnodes.Add(new BinInterpNode
                {
                    Header = $"{pos:X4} : {dataset1type} {DataSize} (0x{DataSize:X})",
                    Name = "_" + pos,
                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                subnodes.Add(new BinInterpNode
                {
                    Header = $"{ pos:X4} {dataset1type}: {DataSize2} (0x{ DataSize2:X})",
                    Name = "_" + pos,
                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                string dataset2type = CurrentLoadedExport.ClassName == "WwiseStream" ? "Stream offset" : "Bank offset";
                subnodes.Add(new BinInterpNode
                {
                    Header = $"{pos:X4} {dataset2type} in file: {DataOffset} (0x{DataOffset:X})",
                    Name = "_" + pos,
                    Tag = NodeType.StructLeafInt
                });
                pos += 4;

                if (CurrentLoadedExport.ClassName == "WwiseBank")
                {
                    //if (CurrentLoadedExport.DataOffset < DataOffset && (CurrentLoadedExport.DataOffset + CurrentLoadedExport.DataSize) < DataOffset)
                    //{
                    subnodes.Add(new BinInterpNode
                    {
                        Header = "Click here to jump to the calculated end offset of wwisebank in this export",
                        Name = "_" + (DataSize2 + pos),
                        Tag = NodeType.Unknown
                    });
                    //}
                }

                switch (CurrentLoadedExport.ClassName)
                {
                    case "WwiseStream" when pos < data.Length && CurrentLoadedExport.GetProperty<NameProperty>("Filename") == null:
                        {
                            subnodes.Add(new BinInterpNode
                            {
                                Header = $"{pos:X4} Embedded sound data. Use Soundplorer to modify this data.",
                                Name = "_" + pos,
                                Tag = NodeType.Unknown
                            });
                            subnodes.Add(new BinInterpNode
                            {
                                Header = "The stream offset to this data will be automatically updated when this file is saved.",
                                Tag = NodeType.Unknown
                            });
                            break;
                        }
                    case "WwiseBank":
                        subnodes.Add(new BinInterpNode
                        {
                            Header = $"{pos:X4} Embedded soundbank. Use Soundplorer WPF to view data.",
                            Name = "_" + pos,
                            Tag = NodeType.Unknown
                        });
                        subnodes.Add(new BinInterpNode
                        {
                            Header = "The bank offset to this data will be automatically updated when this file is saved.",
                            Tag = NodeType.Unknown
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> Scan_WwiseEvent(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
                {
                    int count = BitConverter.ToInt32(data, binarystart);
                    subnodes.Add(new BinInterpNode { Header = $"0x{binarystart:X4} Count: {count.ToString()}", Name = "_" + binarystart });
                    binarystart += 4; //+ int
                    for (int i = 0; i < count; i++)
                    {
                        string nodeText = $"0x{binarystart:X4} ";
                        int val = BitConverter.ToInt32(data, binarystart);
                        string name = val.ToString();
                        if (Pcc.GetEntry(val) is ExportEntry exp)
                        {
                            nodeText += $"{i}: {name} {exp.InstancedFullPath} ({exp.ClassName})";
                        }
                        else if (Pcc.GetEntry(val) is ImportEntry imp)
                        {
                            nodeText += $"{i}: {name} {imp.InstancedFullPath} ({imp.ClassName})";
                        }

                        subnodes.Add(new BinInterpNode
                        {
                            Header = nodeText,
                            Tag = NodeType.StructLeafObject,
                            Name = "_" + binarystart
                        });
                        binarystart += 4;
                        /*
                        int objectindex = BitConverter.ToInt32(data, binarypos);
                        IEntry obj = pcc.getEntry(objectindex);
                        string nodeValue = obj.GetFullPath;
                        node.Tag = nodeType.StructLeafObject;
                        */
                    }
                }
                else if (CurrentLoadedExport.FileRef.Game == MEGame.ME2)
                {
                    var wwiseID = data.Skip(binarystart).Take(4).ToArray();
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"0x{binarystart:X4} WwiseEventID: {wwiseID[0]:X2}{wwiseID[1]:X2}{wwiseID[2]:X2}{wwiseID[3]:X2}",
                        Tag = NodeType.Unknown,
                        Name = "_" + binarystart
                    });
                    binarystart += 4;

                    int count = BitConverter.ToInt32(data, binarystart);
                    var Streams = new BinInterpNode
                    {
                        Header = $"0x{binarystart:X4} Link Count: {count}",
                        Name = "_" + binarystart,
                        Tag = NodeType.StructLeafInt
                    };
                    binarystart += 4;
                    subnodes.Add(Streams); //Are these variables properly named?

                    for (int s = 0; s < count; s++)
                    {
                        int bankcount = BitConverter.ToInt32(data, binarystart);
                        subnodes.Add(new BinInterpNode
                        {
                            Header = $"0x{binarystart:X4} BankCount: {bankcount}",
                            Tag = NodeType.StructLeafInt,
                            Name = "_" + binarystart
                        });
                        binarystart += 4;
                        for (int b = 0; b < bankcount; b++)
                        {
                            int bank = BitConverter.ToInt32(data, binarystart);
                            subnodes.Add(new BinInterpNode
                            {
                                Header = $"0x{binarystart:X4} WwiseBank: {bank} {CurrentLoadedExport.FileRef.GetEntryString(bank)}",
                                Tag = NodeType.StructLeafObject,
                                Name = "_" + binarystart
                            });
                            binarystart += 4;
                        }

                        int streamcount = BitConverter.ToInt32(data, binarystart);
                        subnodes.Add(new BinInterpNode
                        {
                            Header = $"0x{binarystart:X4} StreamCount: {streamcount}",
                            Tag = NodeType.StructLeafInt,
                            Name = "_" + binarystart
                        });
                        binarystart += 4;
                        for (int w = 0; w < streamcount; w++)
                        {
                            int wwstream = BitConverter.ToInt32(data, binarystart);
                            subnodes.Add(new BinInterpNode
                            {
                                Header = $"0x{binarystart:X4} WwiseStream: {wwstream} {CurrentLoadedExport.FileRef.GetEntryString(wwstream)}",
                                Tag = NodeType.StructLeafObject,
                                Name = "_" + binarystart
                            });
                            binarystart += 4;
                        }
                    }
                }
                else
                {
                    subnodes.Add(new BinInterpNode("Only ME3 and ME2 are supported for this scan."));
                    return subnodes;
                }

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
                int count = BitConverter.ToInt32(data, binarypos);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{binarypos:X4} Count: {count.ToString()}"
                });
                binarypos += 4; //+ int
                for (int i = 0; i < count; i++)
                {
                    int nameIndex = BitConverter.ToInt32(data, binarypos);
                    int nameIndexNum = BitConverter.ToInt32(data, binarypos + 4);
                    int shouldBe1 = BitConverter.ToInt32(data, binarypos + 8);

                    var name = CurrentLoadedExport.FileRef.GetNameEntry(nameIndex);
                    string nodeValue = $"{(name == "INVALID NAME VALUE " + nameIndex ? "" : name)}_{nameIndexNum}";
                    if (shouldBe1 != 1)
                    {
                        //ERROR
                        nodeValue += " - Not followed by 1 (integer)!";
                    }

                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"0x{binarypos:X4} Name: {nodeValue}",
                        Tag = NodeType.StructLeafName,
                        Name = $"_{binarypos.ToString()}",
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

        //TODO: unfinished. currently does not display the properties for the list of BioStageCamera objects at the end
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
                        Name = $"_{pos.ToString()}"
                    });
                    pos += 4;
                    if (length != 0)
                    {
                        int nameindex = BitConverter.ToInt32(data, pos);
                        int num = BitConverter.ToInt32(data, pos + 4);

                        var name = new NameReference(CurrentLoadedExport.FileRef.GetNameEntry(nameindex), num);
                        subnodes.Add(new BinInterpNode
                        {
                            Header = $"{(pos - binarystart):X4} Camera: {name.Instanced}",
                            Name = $"_{pos.ToString()}",
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
                            Name = $"_{pos.ToString()}"
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
                                    Header = $"{(pos - binarystart):X4} Camera {i + 1}: {CurrentLoadedExport.FileRef.GetNameEntry(nameindex)}_{num}",
                                    Tag = NodeType.StructLeafName,
                                    Name = $"_{pos.ToString()}"
                                };
                                subnodes.Add(parentnode);
                                pos += 8;
                                stream.Seek(pos, SeekOrigin.Begin);
                                var props = PropertyCollection.ReadProps(CurrentLoadedExport, stream, "BioStageCamera", includeNoneProperty: true);

                                UPropertyTreeViewEntry topLevelTree = new UPropertyTreeViewEntry(); //not used, just for holding and building data.
                                foreach (UProperty prop in props)
                                {
                                    InterpreterWPF.GenerateUPropertyTreeForProperty(prop, topLevelTree, CurrentLoadedExport);
                                }
                                subnodes.AddRange(topLevelTree.ChildrenProperties);

                                //finish writing function here
                                pos = props.endOffset;

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

        private List<ITreeItem> StartClassScan(byte[] data)
        {
            //const int nonTableEntryCount = 2; //how many items we parse that are not part of the functions table. e.g. the count, the defaults pointer
            var subnodes = new List<ITreeItem>();
            try
            {
                int offset = 4;

                int superclassIndex = BitConverter.ToInt32(data, offset);
                string superclassStr = CurrentLoadedExport.FileRef.GetEntryString(superclassIndex);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Superclass Index: {superclassIndex}({superclassStr})",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int unknown1 = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Unknown 1: {unknown1}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int childProbeUIndex = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} ChildListStart: {childProbeUIndex} ({CurrentLoadedExport.FileRef.GetEntryString(childProbeUIndex)}))",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;


                //I am not sure what these mean. However if Pt1&2 are 33/25, the following bytes that follow are extended.
                //int headerUnknown1 = BitConverter.ToInt32(data, offset);
                long ignoreMask = BitConverter.ToInt64(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} IgnoreMask: 0x{ignoreMask:X16}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 8;

                //Int16 labelOffset = BitConverter.ToInt16(data, offset);
                //subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                //{
                //    Header = $"0x{offset:X5} LabelOffset: 0x{labelOffset:X4}",
                //    Name = "_" + offset

                //});
                //offset += 2;

                int skipAmount = 0x6;
                //Find end of script block. Seems to be 10 FF's.
                while (offset + skipAmount + 10 < data.Length)
                {
                    //Debug.WriteLine($"Checking at 0x{offset + skipAmount + 10:X4}");
                    bool isEnd = true;
                    for (int i = 0; i < 10; i++)
                    {
                        byte b = data[offset + skipAmount + i];
                        if (b != 0xFF)
                        {
                            isEnd = false;
                            break;
                        }
                    }
                    if (isEnd)
                    {
                        break;
                    }
                    skipAmount++;
                }
                //if (headerUnknown1 == 33 && headerUnknown2 == 25)
                //{
                //    skipAmount = 0x2F;
                //}
                //else if (headerUnknown1 == 34 && headerUnknown2 == 26)
                //{
                //    skipAmount = 0x30;
                //}
                //else if (headerUnknown1 == 728 && headerUnknown2 == 532)
                //{
                //    skipAmount = 0x22A;
                //}
                int offsetEnd = offset + skipAmount + 10;
                var scriptBlock = new BinInterpNode
                {
                    Header = $"0x{offset:X5} State/Script Block: 0x{offset:X4} - 0x{offsetEnd:X4}",
                    Name = "_" + offset,
                    IsExpanded = true
                };
                subnodes.Add(scriptBlock);

                if (CurrentLoadedExport.FileRef.Game == MEGame.ME3 && skipAmount > 6 && ignoreMask != 0)
                {
                    byte[] scriptmemory = data.Skip(offset).Take(skipAmount).ToArray();
                    try
                    {
                        var tokens = Bytecode.ParseBytecode(scriptmemory, CurrentLoadedExport, offset);
                        string scriptText = "";
                        foreach (Token t in tokens.Item1)
                        {
                            scriptText += $"0x{t.pos:X4} {t.text}\n";
                        }

                        scriptBlock.Items.Add(new BinInterpNode
                        {
                            Header = scriptText,
                            Name = "_" + offset
                        });
                    }
                    catch (Exception) { }

                }


                offset += skipAmount + 10;

                uint stateMask = BitConverter.ToUInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} StateFlags: {stateMask} [{getStateFlagsStr(stateMask)}]",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;
                //}
                //offset += 2; //oher unknown
                int localFunctionsTableCount = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Local Functions Count: {localFunctionsTableCount}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;
                for (int i = 0; i < localFunctionsTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    //int nameIndex = BitConverter.ToInt32(data, offset + 4);
                    offset += 8;
                    int functionObjectIndex = BitConverter.ToInt32(data, offset);
                    offset += 4;
                    (subnodes.Last() as BinInterpNode).Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset - 12:X5}  {CurrentLoadedExport.FileRef.GetNameEntry(nameTableIndex)}() = {functionObjectIndex} ({CurrentLoadedExport.FileRef.GetEntryString(functionObjectIndex)})",
                        Name = "_" + (offset - 12),
                        Tag = NodeType.StructLeafName //might need to add a subnode for the 3rd int
                    });
                }

                UnrealFlags.EClassFlags ClassFlags = (UnrealFlags.EClassFlags)BitConverter.ToUInt32(data, offset);

                var classFlagsNode = new BinInterpNode()
                {
                    Header = $"0x{offset:X5} ClassFlags: 0x{((int)ClassFlags):X8}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                };
                subnodes.Add(classFlagsNode);

                //Create claskmask tree
                foreach (UnrealFlags.EClassFlags flag in Enums.GetValues<UnrealFlags.EClassFlags>())
                {
                    if ((ClassFlags & flag) != UnrealFlags.EClassFlags.None)
                    {
                        string reason = UnrealFlags.classflagdesc[flag];
                        classFlagsNode.Items.Add(new BinInterpNode
                        {
                            Header = $"{(ulong)flag:X16} {flag} {reason}",
                            Name = "_" + offset
                        });
                    }
                }
                offset += 4;

                if (CurrentLoadedExport.FileRef.Game != MEGame.ME3)
                {
                    offset += 1; //seems to be a blank byte here
                }

                int coreReference = BitConverter.ToInt32(data, offset);
                string coreRefFullPath = CurrentLoadedExport.FileRef.GetEntryString(coreReference);

                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Outer Class: {coreReference} ({coreRefFullPath})",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafObject
                });
                offset += 4;


                if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
                {
                    offset = ClassParser_ReadComponentsTable(subnodes, data, offset);
                    offset = ClassParser_ReadImplementsTable(subnodes, data, offset);
                    int postComponentsNoneNameIndex = BitConverter.ToInt32(data, offset);
                    //int postComponentNoneIndex = BitConverter.ToInt32(data, offset + 4);
                    string postCompName = CurrentLoadedExport.FileRef.GetNameEntry(postComponentsNoneNameIndex); //This appears to be unused in ME#, it is always None it seems.
                                                                                                                 /*if (postCompName != "None")
                                                                                                                 {
                                                                                                                     Debugger.Break();
                                                                                                                 }*/
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Post-Components Blank ({postCompName})",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafName
                    });
                    offset += 8;

                    int unknown4 = BitConverter.ToInt32(data, offset);
                    /*if (unknown4 != 0)
                    {
                        Debug.WriteLine("Unknown 4 is not 0: {unknown4);
                       // Debugger.Break();
                    }*/
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Unknown 4: {unknown4}",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;
                }
                else
                {
                    offset = ClassParser_ReadImplementsTable(subnodes, data, offset);
                    offset = ClassParser_ReadComponentsTable(subnodes, data, offset);

                    /*int unknown4 = BitConverter.ToInt32(data, offset);
                    node = new BinaryInterpreterWPFTreeViewItem($"0x{offset:X5} Unknown 4: {unknown4);
                    node.Name = offset.ToString();
                    node.Tag = nodeType.StructLeafInt;
                    subnodes.Add(node);
                    offset += 4;*/

                    int me12unknownend1 = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} ME1/ME2 Unknown1: {me12unknownend1}",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafName
                    });
                    offset += 4;

                    int me12unknownend2 = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} ME1/ME2 Unknown2: {me12unknownend2}",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafName
                    });
                    offset += 4;
                }

                int defaultsClassLink = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Class Defaults: {defaultsClassLink} ({CurrentLoadedExport.FileRef.GetEntryString(defaultsClassLink)}))",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
                {
                    int functionsTableCount = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Full Functions Table Count: {functionsTableCount}",
                        Name = "_" + offset,

                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    for (int i = 0; i < functionsTableCount; i++)
                    {
                        int functionsTableIndex = BitConverter.ToInt32(data, offset);
                        string impexpName = CurrentLoadedExport.FileRef.GetEntryString(functionsTableIndex);
                        (subnodes.Last() as BinInterpNode).Items.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} {impexpName}",
                            Tag = NodeType.StructLeafObject,
                            Name = "_" + offset

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

        public enum StateFlags : uint
        {
            None = 0,
            Editable = 0x00000001U,
            Auto = 0x00000002U,
            Simulated = 0x00000004U,
        }

        public static string getStateFlagsStr(uint stateFlags)
        {
            if (stateFlags == 0)
            {
                return "None";
            }
            if ((stateFlags & (uint)StateFlags.Editable) != 0)
            {
                return "Editable ";
            }
            if ((stateFlags & (uint)StateFlags.Auto) != 0)
            {
                return "Auto";
            }
            if ((stateFlags & (uint)StateFlags.Editable) != 0)
            {
                return "Simulated";
            }
            return "";
        }

        private int ClassParser_ReadComponentsTable(List<ITreeItem> subnodes, byte[] data, int offset)
        {
            if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
            {
                int componentTableNameIndex = BitConverter.ToInt32(data, offset);
                //int componentTableIndex = BitConverter.ToInt32(data, offset + 4);
                offset += 8;

                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset - 8:X5} Components Table ({CurrentLoadedExport.FileRef.GetNameEntry(componentTableNameIndex)})",
                    Name = "_" + (offset - 8),

                    Tag = NodeType.StructLeafName
                });
                int componentTableCount = BitConverter.ToInt32(data, offset);
                offset += 4;

                for (int i = 0; i < componentTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    //int nameIndex = BitConverter.ToInt32(data, offset + 4);
                    offset += 8;
                    int componentObjectIndex = BitConverter.ToInt32(data, offset);
                    offset += 4;
                    string objectName = CurrentLoadedExport.FileRef.GetEntryString(componentObjectIndex);
                    (subnodes.Last() as BinInterpNode).Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset - 12:X5}  {CurrentLoadedExport.FileRef.GetNameEntry(nameTableIndex)}({objectName})",
                        Name = "_" + (offset - 12),

                        Tag = NodeType.StructLeafName
                    });
                }
            }
            else
            {
                int componentTableCount = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Components Table Count: {componentTableCount}",
                    Name = "_" + offset,

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                for (int i = 0; i < componentTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    //int nameIndex = BitConverter.ToInt32(data, offset + 4);
                    offset += 8;
                    int componentObjectIndex = BitConverter.ToInt32(data, offset);

                    string objName = "Null";
                    if (componentObjectIndex != 0)
                    {
                        objName = CurrentLoadedExport.FileRef.GetEntryString(componentObjectIndex);
                    }
                    (subnodes.Last() as BinInterpNode).Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset - 8:X5}  {CurrentLoadedExport.FileRef.GetNameEntry(nameTableIndex)}({objName})",
                        Name = "_" + (offset - 8),

                        Tag = NodeType.StructLeafName
                    });
                    offset += 4;

                }
            }
            return offset;
        }

        private int ClassParser_ReadImplementsTable(List<ITreeItem> subnodes, byte[] data, int offset)
        {
            if (CurrentLoadedExport.FileRef.Game == MEGame.ME3)
            {
                int interfaceCount = BitConverter.ToInt32(data, offset);

                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Implemented Interfaces Table Count: {interfaceCount}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;
                for (int i = 0; i < interfaceCount; i++)
                {
                    int interfaceIndex = BitConverter.ToInt32(data, offset);
                    offset += 4;

                    string objectName = CurrentLoadedExport.FileRef.GetEntryString(interfaceIndex);
                    BinInterpNode subnode = new BinInterpNode
                    {
                        Header = $"0x{offset - 12:X5}  {interfaceIndex} {objectName}",
                        Name = "_" + (offset - 4),
                        Tag = NodeType.StructLeafName
                    };
                    ((BinInterpNode)subnodes.Last()).Items.Add(subnode);

                    //propertypointer
                    interfaceIndex = BitConverter.ToInt32(data, offset);
                    offset += 4;

                    objectName = CurrentLoadedExport.FileRef.GetEntryString(interfaceIndex);
                    subnode.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{offset - 12:X5}  Interface Property Link: {interfaceIndex} {objectName}",
                        Name = "_" + (offset - 4),

                        Tag = NodeType.StructLeafObject
                    });
                }
            }
            else
            {
                int interfaceTableName = BitConverter.ToInt32(data, offset); //????
                offset += 8;

                int interfaceCount = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset - 8:X5} Implemented Interfaces Table Count: {interfaceCount} ({CurrentLoadedExport.FileRef.GetNameEntry(interfaceTableName)})",
                    Name = "_" + (offset - 8),

                    Tag = NodeType.StructLeafInt
                });
                offset += 4;
                for (int i = 0; i < interfaceCount; i++)
                {
                    int interfaceNameIndex = BitConverter.ToInt32(data, offset);
                    offset += 8;

                    BinInterpNode subnode = new BinInterpNode
                    {
                        Header = $"0x{offset - 8:X5}  {CurrentLoadedExport.FileRef.GetNameEntry(interfaceNameIndex)}",
                        Name = "_" + (offset - 8),

                        Tag = NodeType.StructLeafName
                    };
                    ((BinInterpNode)subnodes.Last()).Items.Add(subnode);

                    //propertypointer
                    /* interfaceIndex = BitConverter.ToInt32(data, offset);
                     offset += 4;

                     objectName = CurrentLoadedExport.FileRef.GetEntryString(interfaceIndex);
                     TreeNode subsubnode = new TreeNode($"0x{offset - 12:X5}  Interface Property Link: {interfaceIndex} {objectName}");
                     subsubnode.Name = (offset - 4).ToString();
                     subsubnode.Tag = nodeType.StructLeafObject;
                     subnode.Nodes.Add(subsubnode);
                     */
                }
            }
            return offset;
        }

        private List<ITreeItem> StartEnumScan(byte[] data)
        {
            var subnodes = new List<ITreeItem>();

            try
            {
                int offset = 0;
                int unrealExportIndex = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Unreal Unique Index: {unrealExportIndex}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafInt
                });
                offset += 4;

                int noneUnrealProperty = BitConverter.ToInt32(data, offset);
                //int noneUnrealPropertyIndex = BitConverter.ToInt32(data, offset + 4);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Unreal property None Name: {CurrentLoadedExport.FileRef.GetNameEntry(noneUnrealProperty)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafName
                });
                offset += 8;

                int superclassIndex = BitConverter.ToInt32(data, offset);
                string superclassStr = CurrentLoadedExport.FileRef.GetEntryString(superclassIndex);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} Superclass: {superclassIndex}({superclassStr})",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                int classObjTree = BitConverter.ToInt32(data, offset);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"0x{offset:X5} NextItemCompilingChain: {classObjTree} {CurrentLoadedExport.FileRef.GetEntryString(classObjTree)}",
                    Name = "_" + offset,
                    Tag = NodeType.StructLeafObject
                });
                offset += 4;

                if (CurrentLoadedExport.ClassName == "Enum")
                {

                    int enumSize = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Enum Size: {enumSize}",
                        Name = "_" + offset,
                        Tag = NodeType.StructLeafInt
                    });
                    offset += 4;

                    for (int i = 0; i < enumSize; i++)
                    {
                        int enumName = BitConverter.ToInt32(data, offset);
                        //int enumNameIndex = BitConverter.ToInt32(data, offset + 4);
                        subnodes.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} EnumName[{i}]: {CurrentLoadedExport.FileRef.GetNameEntry(enumName)}",
                            Name = "_" + offset,
                            Tag = NodeType.StructLeafName
                        });
                        offset += 8;
                    }
                }

                if (CurrentLoadedExport.ClassName == "Const")
                {
                    int literalStringLength = BitConverter.ToInt32(data, offset);
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"0x{offset:X5} Const Literal Length: {literalStringLength}",
                        Name = "_" + offset,
                        Tag = NodeType.IntProperty
                    });
                    offset += 4;

                    //value is stored as a literal string in binary.
                    MemoryStream stream = new MemoryStream(data) { Position = offset };
                    if (literalStringLength < 0)
                    {
                        string str = Gibbed.IO.StreamHelpers.ReadString(stream, (literalStringLength * -2), true, Encoding.Unicode);
                        subnodes.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Const Literal Value: {str}",
                            Name = "_" + offset,
                            Tag = NodeType.StrProperty
                        });
                    }
                    else
                    {
                        string str = Gibbed.IO.StreamHelpers.ReadString(stream, (literalStringLength), false, Encoding.ASCII);
                        subnodes.Add(new BinInterpNode
                        {
                            Header = $"0x{offset:X5} Const Literal Value: {str}",
                            Name = "_" + offset,
                            Tag = NodeType.StrProperty
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
            }
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
                    Name = "_" + pos,

                });
                pos += 4;
                for (int i = 0; i < count && pos < data.Length; i++)
                {
                    int nameRef = BitConverter.ToInt32(data, pos);
                    int nameIdx = BitConverter.ToInt32(data, pos + 4);
                    Guid guid = new Guid(data.Skip(pos + 8).Take(16).ToArray());
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"{(pos - binarystart):X4} {CurrentLoadedExport.FileRef.GetNameEntry(nameRef)}_{nameIdx}: {{{guid}}}",
                        Name = "_" + pos,

                        Tag = NodeType.StructLeafName
                    });
                    //Debug.WriteLine($"{pos:X4} {CurrentLoadedExport.FileRef.getNameEntry(nameRef)}_{nameIdx}: {{{guid}}}");
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
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);

                subnodes.Add(MakeEntryNode(bin, "Self"));
                int actorsCount;
                BinInterpNode levelActorsNode;
                subnodes.Add(levelActorsNode = new BinInterpNode(bin.Position, $"Level Actors: ({actorsCount = bin.ReadInt32()})", NodeType.StructLeafInt)
                {
                    ArrayAddAlgoritm = BinInterpNode.ArrayPropertyChildAddAlgorithm.LevelItem,
                    IsExpanded = true
                });
                levelActorsNode.Items = ReadList(actorsCount, i => new BinInterpNode(bin.Position, $"{i}: {entryRefString(bin)}", NodeType.ArrayLeafObject)
                {
                    ArrayAddAlgoritm = BinInterpNode.ArrayPropertyChildAddAlgorithm.LevelItem,
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
                    Items = ReadList(forceStreamTexturesCount, i => MakeBoolIntNode(bin, "Texture: {entryRefString(bin)} | ForceStream"))
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
                if (Pcc.Game == MEGame.ME3)
                {
                    int guidToIntMapCount;
                    subnodes.Add(new BinInterpNode(bin.Position, $"guidToIntMap?: ({guidToIntMapCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(guidToIntMapCount, i => MakeInt32Node(bin, $"{bin.ReadValueGuid()}"))
                    });

                    int coverListCount;
                    subnodes.Add(new BinInterpNode(bin.Position, $"Coverlinks: ({coverListCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(coverListCount, i => MakeEntryNode(bin, $"{i}"))
                    });

                    int intToByteMapCount;
                    subnodes.Add(new BinInterpNode(bin.Position, $"IntToByteMap?: ({intToByteMapCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(intToByteMapCount, i => new BinInterpNode(bin.Position, $"{bin.ReadInt32()}: {bin.ReadByte()}"))
                    });

                    int guidToIntMap2Count;
                    subnodes.Add(new BinInterpNode(bin.Position, $"2nd guidToIntMap?: ({guidToIntMap2Count = bin.ReadInt32()})")
                    {
                        Items = ReadList(guidToIntMap2Count, i => MakeInt32Node(bin, $"{bin.ReadValueGuid()}"))
                    });

                    int navListCount;
                    subnodes.Add(new BinInterpNode(bin.Position, $"NavPoints?: ({navListCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(navListCount, i => MakeEntryNode(bin, $"{i}"))
                    });

                    int numbersCount;
                    subnodes.Add(new BinInterpNode(bin.Position, $"Ints?: ({numbersCount = bin.ReadInt32()})")
                    {
                        Items = ReadList(numbersCount, i => MakeInt32Node(bin, $"{i}"))
                    });
                }

                int crossLevelActorsCount;
                subnodes.Add(new BinInterpNode(bin.Position, $"CrossLevelActors?: ({crossLevelActorsCount = bin.ReadInt32()})")
                {
                    Items = ReadList(crossLevelActorsCount, i => MakeEntryNode(bin, $"{i}"))
                });

                if (Pcc.Game == MEGame.ME1)
                {
                    subnodes.Add(MakeEntryNode(bin, "BioArtPlaceable 1?"));
                    subnodes.Add(MakeEntryNode(bin, "BioArtPlaceable 2?"));
                }

                if (Pcc.Game == MEGame.UDK)
                {
                    subnodes.Add(MakeUInt32Node(bin, "unk"));
                    subnodes.Add(MakeUInt32Node(bin, "unk"));
                    subnodes.Add(MakeUInt32Node(bin, "unk"));
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
                    item.Items.Add(MakeVector2DNode(bin, "PrecomputedVisibilityCellSizeXY"));
                    item.Items.Add(MakeFloatNode(bin, "PrecomputedVisibilityCellSizeZ"));
                    item.Items.Add(MakeInt32Node(bin, "PrecomputedVisibilityCellBucketSizeXY"));
                    //item.Items.Add(MakeInt32Node(bin, "PrecomputedVisibilityNumCellBuckets"));
                    item.Items.Add(MakeArrayNode(bin, "PrecomputedVisibilityCellBuckets", i => new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items =
                        {
                            MakeInt32Node(bin, "CellDataSize"),
                            MakeArrayNode(bin, "Cells", j => new BinInterpNode(bin.Position, $"{j}")
                            {
                                Items =
                                {
                                    MakeVectorNode(bin, "Min"),
                                    MakeUInt16Node(bin, "ChunkIndex"),
                                    MakeUInt16Node(bin, "DataOffset")
                                }
                            }),
                            MakeArrayNode(bin, "CellDataChunks", j => new BinInterpNode(bin.Position, $"{j}")
                            {
                                Items =
                                {
                                    MakeBoolIntNode(bin, "bCompressed"),
                                    MakeInt32Node(bin, "UncompressedSize"),
                                    MakeByteArrayNode(bin, "Data")
                                }
                            })
                        }
                    }));
                }

                binarystart = (int)bin.Position;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;
        }

        private static BinInterpNode MakeBoolIntNode(MemoryStream bin, string name) => new BinInterpNode(bin.Position, $"{name}: {bin.ReadBoolInt()}", NodeType.StructLeafBool) { Length = 4 };

        private static BinInterpNode MakeBoolIntNode(MemoryStream bin, string name, out bool boolVal)
        {
            return new BinInterpNode(bin.Position, $"{name}: {boolVal = bin.ReadBoolInt()}", NodeType.StructLeafBool) {Length = 4};
        }

        private static BinInterpNode MakeBoolByteNode(MemoryStream bin, string name) => new BinInterpNode(bin.Position, $"{name}: {bin.ReadBoolByte()}") { Length = 1 };

        private static BinInterpNode MakeFloatNode(MemoryStream bin, string name) => new BinInterpNode(bin.Position, $"{name}: {bin.ReadFloat()}", NodeType.StructLeafFloat) { Length = 4 };

        private static BinInterpNode MakeUInt32Node(MemoryStream bin, string name) => new BinInterpNode(bin.Position, $"{name}: {bin.ReadUInt32()}") { Length = 4 };

        private static BinInterpNode MakeInt32Node(MemoryStream bin, string name) => new BinInterpNode(bin.Position, $"{name}: {bin.ReadInt32()}", NodeType.StructLeafInt) { Length = 4 };

        private static BinInterpNode MakeUInt16Node(MemoryStream bin, string name) => new BinInterpNode(bin.Position, $"{name}: {bin.ReadUInt16()}") { Length = 2 };

        private static BinInterpNode MakeByteNode(MemoryStream bin, string name) => new BinInterpNode(bin.Position, $"{name}: {bin.ReadByte()}") { Length = 1 };

        private BinInterpNode MakeNameNode(MemoryStream bin, string name) => new BinInterpNode(bin.Position, $"{name}: {bin.ReadNameReference(Pcc)}", NodeType.StructLeafName) { Length = 8 };

        private BinInterpNode MakeEntryNode(MemoryStream bin, string name) => new BinInterpNode(bin.Position, $"{name}: {entryRefString(bin)}", NodeType.StructLeafObject) { Length = 4 };

        private static BinInterpNode MakePackedNormalNode(MemoryStream bin, string name) =>
            new BinInterpNode(bin.Position, $"{name}: (X: {bin.ReadByte() / 127.5f - 1}, Y: {bin.ReadByte() / 127.5f - 1}, Z: {bin.ReadByte() / 127.5f - 1}, W: {bin.ReadByte() / 127.5f - 1})")
            {
                Length = 4
            };

        private static BinInterpNode MakeVectorNode(MemoryStream bin, string name) =>
            new BinInterpNode(bin.Position, $"{name}: (X: {bin.ReadFloat()}, Y: {bin.ReadFloat()}, Z: {bin.ReadFloat()})") { Length = 12 };

        private static BinInterpNode MakeQuatNode(MemoryStream bin, string name) =>
            new BinInterpNode(bin.Position, $"{name}: (X: {bin.ReadFloat()}, Y: {bin.ReadFloat()}, Z: {bin.ReadFloat()}, W: {bin.ReadFloat()})") { Length = 16 };

        private static BinInterpNode MakeRotatorNode(MemoryStream bin, string name) =>
            new BinInterpNode(bin.Position, $"{name}: (Pitch: {bin.ReadInt32()}, Yaw: {bin.ReadInt32()}, Roll: {bin.ReadInt32()})") { Length = 12 };

        private static BinInterpNode MakeBoxNode(MemoryStream bin, string name) =>
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

        private static BinInterpNode MakeVector2DNode(MemoryStream bin, string name) =>
            new BinInterpNode(bin.Position, $"{name}: (X: {bin.ReadFloat()}, Y: {bin.ReadFloat()})") { Length = 8 };

        private static BinInterpNode MakeVector2DHalfNode(MemoryStream bin, string name) =>
            new BinInterpNode(bin.Position, $"{name}: (X: {bin.ReadFloat16()}, Y: {bin.ReadFloat16()})") { Length = 4 };

        private static BinInterpNode MakeColorNode(MemoryStream bin, string name)
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

        private static BinInterpNode MakeBoxSphereBoundsNode(MemoryStream bin, string name)
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

        private static BinInterpNode MakeGuidNode(MemoryStream bin, string name) => new BinInterpNode(bin.Position, $"{name}: {bin.ReadGuid()}") { Length = 16 };

        private static BinInterpNode MakeArrayNode(MemoryStream bin, string name, Func<int, BinInterpNode> selector, bool IsExpanded = false)
        {
            int count;
            return new BinInterpNode(bin.Position, $"{name} ({count = bin.ReadInt32()})")
            {
                IsExpanded = IsExpanded,
                Items = ReadList(count, selector)
            };
        }

        private static BinInterpNode MakeByteArrayNode(MemoryStream bin, string name)
        {
            long pos = bin.Position;
            int count = bin.ReadInt32();
            bin.Skip(count);
            return new BinInterpNode(pos, $"{name} ({count} bytes)");
        }

        private static BinInterpNode MakeArrayNode(int count, MemoryStream bin, string name, Func<int, BinInterpNode> selector, bool IsExpanded = false)
        {
            return new BinInterpNode(bin.Position, $"{name} ({count})")
            {
                IsExpanded = IsExpanded,
                Items = ReadList(count, selector)
            };
        }

        private List<ITreeItem> StartMaterialScan(byte[] data, ref int binarystart)
        {
            var nodes = new List<ITreeItem>();

            if (binarystart >= data.Length)
            {
                return new List<ITreeItem> { new BinInterpNode { Header = "No Binary Data" } };
            }
            try
            {
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);

                nodes.Add(MakeMaterialResourceNode(bin, "ShaderMap 3 Material Resource"));
                if (Pcc.Game != MEGame.UDK)
                {
                    nodes.Add(MakeMaterialResourceNode(bin, "ShaderMap 2 Material Resource"));
                }

            }
            catch (Exception ex)
            {
                nodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return nodes;
        }

        private List<ITreeItem> StartMaterialInstanceScan(byte[] data, ref int binarystart)
        {
            var nodes = new List<ITreeItem>();

            if (binarystart >= data.Length)
            {
                return new List<ITreeItem> { new BinInterpNode { Header = "No Binary Data" } };
            }
            try
            {
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);

                nodes.Add(MakeMaterialResourceNode(bin, "Material Resource"));
                nodes.AddRange(ReadFStaticParameterSet(bin));
                nodes.Add(MakeMaterialResourceNode(bin, "2nd Material Resource"));
                nodes.AddRange(ReadFStaticParameterSet(bin));
            }
            catch (Exception ex)
            {
                nodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return nodes;
        }
        private List<ITreeItem> ReadFStaticParameterSet(MemoryStream bin)
        {
            var nodes = new List<ITreeItem>();

            nodes.Add(new BinInterpNode(bin.Position, $"Base Material GUID {bin.ReadValueGuid()}") { Length = 16 });
            int staticSwitchParameterCount = bin.ReadInt32();
            var staticSwitchParamsNode = new BinInterpNode(bin.Position - 4, $"Static Switch Parameters, {staticSwitchParameterCount} items") { Length = 4 };

            nodes.Add(staticSwitchParamsNode);
            for (int j = 0; j < staticSwitchParameterCount; j++)
            {
                var paramName = bin.ReadNameReference(Pcc);
                var paramVal = bin.ReadBooleanInt();
                var paramOverride = bin.ReadBooleanInt();
                Guid g = bin.ReadValueGuid();
                staticSwitchParamsNode.Items.Add(new BinInterpNode(bin.Position - 32, $"{j}: Name: {paramName.Instanced}, Value: {paramVal}, Override: {paramOverride}\nGUID:{g}")
                {
                    Length = 32
                });
            }

            int staticComponentMaskParameterCount = bin.ReadInt32();
            var staticComponentMaskParametersNode = new BinInterpNode(bin.Position - 4, $"Static Component Mask Parameters, {staticComponentMaskParameterCount} items")
            {
                Length = 4
            };
            nodes.Add(staticComponentMaskParametersNode);
            for (int i = 0; i < staticComponentMaskParameterCount; i++)
            {
                var subnodes = new List<ITreeItem>();
                staticComponentMaskParametersNode.Items.Add(new BinInterpNode(bin.Position, $"Parameter {i}")
                {
                    Length = 44,
                    Items = subnodes
                });
                subnodes.Add(new BinInterpNode(bin.Position, $"ParameterName: {bin.ReadNameReference(Pcc).Instanced}") { Length = 8 });
                subnodes.Add(new BinInterpNode(bin.Position, $"R: {bin.ReadBooleanInt()}") { Length = 4 });
                subnodes.Add(new BinInterpNode(bin.Position, $"G: {bin.ReadBooleanInt()}") { Length = 4 });
                subnodes.Add(new BinInterpNode(bin.Position, $"B: {bin.ReadBooleanInt()}") { Length = 4 });
                subnodes.Add(new BinInterpNode(bin.Position, $"A: {bin.ReadBooleanInt()}") { Length = 4 });
                subnodes.Add(new BinInterpNode(bin.Position, $"bOverride: {bin.ReadBooleanInt()}") { Length = 4 });
                subnodes.Add(new BinInterpNode(bin.Position, $"ExpressionGUID: {bin.ReadValueGuid()}") { Length = 16 });
            }

            if (Pcc.Game == MEGame.ME3)
            {
                int NormalParameterCount = bin.ReadInt32();
                var NormalParametersNode = new BinInterpNode(bin.Position - 4, $"Normal Parameters, {NormalParameterCount} items")
                {
                    Length = 4
                };
                nodes.Add(NormalParametersNode);
                for (int i = 0; i < NormalParameterCount; i++)
                {
                    var subnodes = new List<ITreeItem>();
                    NormalParametersNode.Items.Add(new BinInterpNode(bin.Position, $"Parameter {i}")
                    {
                        Length = 29,
                        Items = subnodes
                    });
                    subnodes.Add(new BinInterpNode(bin.Position, $"ParameterName: {bin.ReadNameReference(Pcc).Instanced}") { Length = 8 });
                    subnodes.Add(new BinInterpNode(bin.Position, $"CompressionSettings: {(TextureCompressionSettings)bin.ReadByte()}") { Length = 1 });
                    subnodes.Add(new BinInterpNode(bin.Position, $"bOverride: {bin.ReadBooleanInt()}") { Length = 4 });
                    subnodes.Add(new BinInterpNode(bin.Position, $"ExpressionGUID: {bin.ReadValueGuid()}") { Length = 16 });
                }
            }

            return nodes;
        }

        private BinInterpNode MakeMaterialResourceNode(MemoryStream bin, string name)
        {
            BinInterpNode node = new BinInterpNode(bin.Position, name)
            {
                IsExpanded = true
            };
            List<ITreeItem> nodes = node.Items;
            try
            {
                nodes.Add(MakeArrayNode(bin, "Compile Errors", i => MakeStringNode(bin, $"{i}")));
                nodes.Add(MakeArrayNode(bin, "TextureDependencyLengthMap", i => new BinInterpNode(bin.Position, $"{entryRefString(bin)}: {bin.ReadInt32()}")));
                nodes.Add(MakeInt32Node(bin, "MaxTextureDependencyLength"));
                nodes.Add(MakeGuidNode(bin, "ID"));
                nodes.Add(MakeUInt32Node(bin, "NumUserTexCoords"));
                if (Pcc.Game >= MEGame.ME3)
                {
                    nodes.Add(MakeArrayNode(bin, "UniformExpressionTextures", i => MakeEntryNode(bin, $"{i}")));
                }
                else
                {
                    nodes.Add(MakeArrayNode(bin, "UniformPixelVectorExpressions", i => ReadMaterialUniformExpression(bin, $"{i}")));
                    nodes.Add(MakeArrayNode(bin, "UniformPixelScalarExpressions", i => ReadMaterialUniformExpression(bin, $"{i}")));
                    nodes.Add(MakeArrayNode(bin, "Uniform2DTextureExpressions", i => ReadMaterialUniformExpression(bin, $"{i}")));
                    nodes.Add(MakeArrayNode(bin, "UniformCubeTextureExpressions", i => ReadMaterialUniformExpression(bin, $"{i}")));
                }
                nodes.Add(MakeBoolIntNode(bin, "bUsesSceneColor"));
                nodes.Add(MakeBoolIntNode(bin, "bUsesSceneDepth"));
                nodes.Add(ListInitHelper.ConditionalAdd(Pcc.Game >= MEGame.ME3, () => new List<ITreeItem>
                {
                    MakeBoolIntNode(bin, "bUsesDynamicParameter"),
                    MakeBoolIntNode(bin, "bUsesLightmapUVs"),
                    MakeBoolIntNode(bin, "bUsesMaterialVertexPositionOffset"),
                    ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game == MEGame.ME3, () => MakeBoolIntNode(bin, "unknown bool?"))
                }));
                nodes.Add(new BinInterpNode(bin.Position, $"UsingTransforms: {(ECoordTransformUsage)bin.ReadUInt32()}"));
                if (Pcc.Game == MEGame.ME1)
                {
                    int count = bin.ReadInt32();
                    var unkListNode = new BinInterpNode(bin.Position - 4, $"MaterialExpressions list? ({count} items)");
                    nodes.Add(unkListNode);
                    for (int i = 0; i < count; i++)
                    {
                        var iNode = new BinInterpNode(bin.Position, $"{i}");
                        unkListNode.Items.Add(iNode);
                        iNode.Items.Add(MakeArrayNode(bin, "VectorExpressions", j => ReadMaterialUniformExpression(bin, $"{j}")));
                        iNode.Items.Add(MakeArrayNode(bin, "ScalarExpressions", j => ReadMaterialUniformExpression(bin, $"{j}")));
                        iNode.Items.Add(MakeArrayNode(bin, "TextureExpressions", j => ReadMaterialUniformExpression(bin, $"{j}")));
                        iNode.Items.Add(MakeArrayNode(bin, "UniformCubeTextureExpressions", j => ReadMaterialUniformExpression(bin, $"{j}")));
                        iNode.Items.Add(MakeUInt32Node(bin, "unk?"));
                        iNode.Items.Add(MakeUInt32Node(bin, "unk?"));
                        iNode.Items.Add(MakeUInt32Node(bin, "unk?"));
                        iNode.Items.Add(MakeUInt32Node(bin, "unk?"));
                    }
                }
                else
                {
                    nodes.Add(MakeArrayNode(bin, "TextureLookups", i => new BinInterpNode(bin.Position, $"{i}")
                    {
                        Items =
                        {
                            MakeInt32Node(bin, "TexCoordIndex"),
                            MakeInt32Node(bin, "TextureIndex"),
                            ListInitHelper.ConditionalAdd(Pcc.Game == MEGame.ME1, ifTrue: () => new ITreeItem[]
                            {
                                MakeFloatNode(bin, "TilingScale")
                            }, ifFalse: () => new ITreeItem[]
                            {
                                MakeFloatNode(bin, "TilingUScale"),
                                MakeFloatNode(bin, "TilingVScale")
                            }),
                        }
                    }));
                }
                nodes.Add(MakeInt32Node(bin, "unk"));
                if (Pcc.Game == MEGame.ME1)
                {
                    int unkCount;
                    nodes.Add(new BinInterpNode(bin.Position, $"Unknown count: {unkCount = bin.ReadInt32()}"));
                    nodes.Add(MakeInt32Node(bin, "Unknown?"));
                    if (unkCount > 0)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"unknown array ({unkCount} items)")
                        {
                            Items = ReadList(unkCount, i => new BinInterpNode(bin.Position, $"{i}")
                            {
                                Items =
                                {
                                    MakeInt32Node(bin, "unk int"),
                                    MakeFloatNode(bin, "unk float"),
                                    MakeInt32Node(bin, "unk int")
                                }
                            })
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                nodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return node;
        }

        [Flags]
        public enum ECoordTransformUsage : uint
        {
            // no transforms used
            UsedCoord_None = 0,
            // local to world used
            UsedCoord_World = 1 << 0,
            // local to view used
            UsedCoord_View = 1 << 1,
            // local to local used
            UsedCoord_Local = 1 << 2,
            // World Position used
            UsedCoord_WorldPos = 1 << 3
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
                var bin = new MemoryStream(data);
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

        private List<ITreeItem> StartSkeletalMeshScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            var game = CurrentLoadedExport.FileRef.Game;
            try
            {

                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);

                subnodes.Add(MakeBoxSphereBoundsNode(bin, "Bounds"));
                subnodes.Add(MakeArrayNode(bin, "Materials", i => MakeEntryNode(bin, $"{i}"), true));
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
                        node.Items.Add(MakeInt32Node(bin, "ushort size"));
                        node.Items.Add(MakeArrayNode(bin, "IndexBuffer", j => MakeUInt16Node(bin, $"{j}")));
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
                            ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game == MEGame.UDK, () => MakeInt32Node(bin, "NumTexCoords")),
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
                                                                                                                () => MakeVector2DHalfNode(bin, "UV")))
                            }
                        }));
                        node.Items.Add(ListInitHelper.ConditionalAddOne<ITreeItem>(Pcc.Game >= MEGame.ME3, () => MakeInt32Node(bin, "VertexInfluences count")));
                        node.Items.Add(ListInitHelper.ConditionalAdd(Pcc.Game == MEGame.UDK, () => new ITreeItem[]
                        {
                            MakeBoolIntNode(bin, "NeedsCPUAccess"),
                            MakeByteNode(bin, "Datatype size"),
                            MakeInt32Node(bin, "ushort size"),
                            MakeArrayNode(bin, "Second IndexBuffer?", j => MakeUInt16Node(bin, $"{j}")),
                        }));
                    }
                    catch (Exception e)
                    {
                        node.Items.Add(new BinInterpNode { Header = $"Error reading binary data: {e}" });
                    }
                    return node;
                }, true));
                subnodes.Add(MakeArrayNode(bin, "NameIndexMap", i => new BinInterpNode(bin.Position, $"{bin.ReadNameReference(Pcc).Instanced}: {bin.ReadInt32()}")));
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
                    subnodes.Add(MakeArrayNode(bin, "ClothingAssets", i => MakeEntryNode(bin, $"{i}")));
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }

            return subnodes;

        }

        private List<ITreeItem> StartStaticMeshCollectionActorScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                //get a list of staticmesh stuff from the props.
                var smacitems = new List<ExportEntry>();
                var props = CurrentLoadedExport.GetProperty<ArrayProperty<ObjectProperty>>("StaticMeshComponents");

                foreach (var prop in props)
                {
                    if (prop.Value > 0)
                    {
                        smacitems.Add(Pcc.GetUExport(prop.Value));
                    }
                    else
                    {
                        smacitems.Add(null);
                    }
                }

                //find start of class binary (end of props)
                int start = binarystart;

                //Lets make sure this binary is divisible by 64.
                if ((data.Length - start) % 64 != 0)
                {
                    subnodes.Add(new BinInterpNode
                    {
                        Tag = NodeType.Unknown,
                        Header = $"{start:X4} Binary data is not divisible by 64 ({data.Length - start})! SMCA binary data should be a length divisible by 64.",
                        Name = "_" + start

                    });
                    return subnodes;
                }

                int smcaindex = 0;
                while (start < data.Length && smcaindex < smacitems.Count)
                {
                    BinInterpNode smcanode = new BinInterpNode
                    {
                        Tag = NodeType.Unknown
                    };
                    ExportEntry associatedData = smacitems[smcaindex];
                    string staticmesh = "";
                    string objtext = "Null - unused data";
                    if (associatedData != null)
                    {
                        objtext = $"[Export {associatedData.UIndex}] {associatedData.ObjectName.Instanced}";

                        //find associated static mesh value for display.
                        byte[] smc_data = associatedData.Data;
                        int staticmeshstart = 0x4;
                        bool found = false;
                        while (staticmeshstart < smc_data.Length && smc_data.Length - 8 >= staticmeshstart)
                        {
                            ulong nameindex = BitConverter.ToUInt64(smc_data, staticmeshstart);
                            if (nameindex < (ulong)CurrentLoadedExport.FileRef.Names.Count && CurrentLoadedExport.FileRef.Names[(int)nameindex] == "StaticMesh")
                            {
                                //found it
                                found = true;
                                break;
                            }
                            else
                            {
                                staticmeshstart += 1;
                            }
                        }

                        if (found)
                        {
                            int staticmeshexp = BitConverter.ToInt32(smc_data, staticmeshstart + 0x18);
                            if (staticmeshexp > 0 && staticmeshexp < CurrentLoadedExport.FileRef.ExportCount)
                            {
                                staticmesh = Pcc.GetEntry(staticmeshexp).ObjectName.Instanced;
                            }
                        }
                    }

                    smcanode.Header = $"{start:X4} [{smcaindex}] {objtext} {staticmesh}";
                    smcanode.Name = "_" + start;
                    subnodes.Add(smcanode);

                    //Read nodes
                    for (int i = 0; i < 16; i++)
                    {
                        float smcadata = BitConverter.ToSingle(data, start);
                        BinInterpNode node = new BinInterpNode
                        {
                            Tag = NodeType.StructLeafFloat,
                            Header = start.ToString("X4")
                        };

                        //TODO: Figure out what the rest of these mean
                        string label = i.ToString();
                        switch (i)
                        {
                            case 1:
                                label = "ScalingXorY1:";
                                break;
                            case 12:
                                label = "LocX:";
                                break;
                            case 13:
                                label = "LocY:";
                                break;
                            case 14:
                                label = "LocZ:";
                                break;
                            case 15:
                                label = "CameraLayerDistance?:";
                                break;
                        }

                        node.Header += $" {label} {smcadata}";

                        //TODO: Lookup staticmeshcomponent so we can see what this actually is without changing to the export

                        node.Name = "_" + start;
                        smcanode.Items.Add(node);
                        start += 4;
                    }

                    smcaindex++;
                }
                //topLevelTree.ItemsSource = subnodes;
                binarystart = start;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;

        }

        private List<ITreeItem> StartStaticLightCollectionActorScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                //get a list of lightcomponents from the props.
                var slcaitems = new List<ExportEntry>();
                var props = CurrentLoadedExport.GetProperty<ArrayProperty<ObjectProperty>>("LightComponents");

                foreach (var prop in props)
                {
                    if (prop.Value > 0)
                    {
                        slcaitems.Add(CurrentLoadedExport.FileRef.GetEntry(prop.Value) as ExportEntry);
                    }
                    else
                    {
                        slcaitems.Add(null);
                    }
                }

                //find start of class binary (end of props)
                int start = binarystart;

                //Lets make sure this binary is divisible by 64.
                if ((data.Length - start) % 64 != 0)
                {
                    subnodes.Add(new BinInterpNode
                    {
                        Tag = NodeType.Unknown,
                        Header = $"{start:X4} Binary data is not divisible by 64 ({data.Length - start})! SLCA binary data should be a length divisible by 64.",
                        Name = "_" + start

                    });
                    return subnodes;
                }

                int slcaindex = 0;
                while (start < data.Length && slcaindex < slcaitems.Count)
                {
                    BinInterpNode slcanode = new BinInterpNode
                    {
                        Tag = NodeType.Unknown
                    };
                    ExportEntry assossiateddata = slcaitems[slcaindex];
                    string objtext = "Null - unused data";
                    if (assossiateddata != null)
                    {
                        objtext = $"[Export {assossiateddata.UIndex}] {assossiateddata.ObjectName.Instanced}";
                    }

                    slcanode.Header = $"{start:X4} [{slcaindex}] {objtext}";
                    slcanode.Name = "_" + start;
                    subnodes.Add(slcanode);

                    //Read nodes
                    for (int i = 0; i < 16; i++)
                    {
                        float slcadata = BitConverter.ToSingle(data, start);
                        BinInterpNode node = new BinInterpNode
                        {
                            Tag = NodeType.StructLeafFloat,
                            Header = start.ToString("X4")
                        };

                        //TODO: Figure out what the rest of these mean
                        string label = i.ToString();
                        switch (i)
                        {
                            case 1:
                                label = "ScalingXorY1:";
                                break;
                            case 12:
                                label = "LocX:";
                                break;
                            case 13:
                                label = "LocY:";
                                break;
                            case 14:
                                label = "LocZ:";
                                break;
                            case 15:
                                label = "CameraLayerDistance?:";
                                break;
                        }

                        node.Header += $" {label} {slcadata}";

                        node.Name = "_" + start;
                        slcanode.Items.Add(node);
                        start += 4;
                    }

                    slcaindex++;
                }

                binarystart = start;
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
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

        private IEnumerable<ITreeItem> StartStaticMeshScan(byte[] data, ref int binarystart)
        {
            var subnodes = new List<ITreeItem>();
            try
            {
                var matOffsets = new List<long>();
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);

                subnodes.Add(MakeBoxSphereBoundsNode(bin, "Bounds"));
                subnodes.Add(MakeEntryNode(bin, "BodySetup"));
                subnodes.Add(MakekDOPTreeNode(bin));

                subnodes.Add(MakeInt32Node(bin, "InternalVersion"));
                int count;
                bool bUseFullPrecisionUVs;
                int numTexCoords;
                int numVertices;
                if (Pcc.Game == MEGame.UDK)
                {
                    subnodes.Add(MakeInt32Node(bin, "unk"));
                    subnodes.Add(MakeInt32Node(bin, "unk"));
                    subnodes.Add(MakeInt32Node(bin, "unk"));
                    subnodes.Add(MakeInt32Node(bin, "unk"));
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
                            node.Items.Add(MakeEntryNode(bin, "Material"));
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
                                ListInitHelper.ConditionalAdd(Pcc.Game == MEGame.ME3, () => new ITreeItem[]{ MakeUInt32Node(bin, "unk") }),
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
                                ListInitHelper.ConditionalAdd(Pcc.Game == MEGame.ME3, () => new ITreeItem[]
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
                            lodNode.Items.Add(MakeArrayNode(bin, "unknown buffer", j => MakeUInt16Node(bin, $"{j}")));
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
                    subnodes.Add(MakeUInt32Node(bin, "unk"));
                    subnodes.Add(new BinInterpNode(bin.Position, $"ThumbnailAngle: (Pitch: {bin.ReadInt32()}, Yaw: {bin.ReadInt32()}, Roll: {bin.ReadInt32()})"));
                    subnodes.Add(MakeFloatNode(bin, "ThumbnailDistance"));
                    if (Pcc.Game < MEGame.UDK)
                    {
                        subnodes.Add(MakeUInt32Node(bin, "unk"));
                    }
                    if (Pcc.Game >= MEGame.ME3)
                    {
                        subnodes.Add(MakeStringNode(bin, "HighResSourceMeshName"));
                        subnodes.Add(MakeUInt32Node(bin, "HighResSourceMeshCRC"));
                        subnodes.Add(MakeGuidNode(bin, "LightingGuid"));
                    }
                    if (Pcc.Game == MEGame.UDK)
                    {
                        subnodes.Add(MakeUInt32Node(bin, "unk"));
                        subnodes.Add(MakeArrayNode(bin, "unk float list", j => MakeFloatNode(bin, $"{j}")));
                        subnodes.Add(MakeUInt32Node(bin, "unk"));
                        subnodes.Add(MakeUInt32Node(bin, "unk"));
                        subnodes.Add(MakeUInt32Node(bin, "unk"));
                    }
                }

                binarystart = (int)bin.Position;
                return subnodes.Prepend(new BinInterpNode("Materials")
                {
                    IsExpanded = true,
                    Items = ReadList(matOffsets.Count, i =>
                    {
                        bin.JumpTo(matOffsets[i]);
                        return MakeEntryNode(bin, $"Material[{i}]");
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
                var bin = new MemoryStream(data);
                bin.JumpTo(binarystart);

                subnodes.Add(MakeEntryNode(bin, "SourceStaticMesh"));
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

        private BinInterpNode MakekDOPTreeNode(MemoryStream bin)
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

        private List<ITreeItem> StartTextureBinaryScan(byte[] data, int binarystart)
        {
            var subnodes = new List<ITreeItem>();

            try
            {
                var bin = new MemoryStream(data);

                subnodes.Add(MakeInt32Node(bin, "Unreal Unique Index"));

                if (bin.Length == binarystart)
                    return subnodes; // no binary data


                bin.JumpTo(binarystart);
                if (Pcc.Game != MEGame.ME3)
                {
                    bin.Skip(8); // 12 zeros
                    int thumbnailSize = bin.ReadInt32();
                    subnodes.Add(MakeInt32Node(bin, "File Offset"));
                    bin.Skip(thumbnailSize);
                }

                int numMipMaps = bin.ReadInt32();
                subnodes.Add(new BinInterpNode(bin.Position - 4, $"Num MipMaps: {numMipMaps}"));
                for (int l = 0; l < numMipMaps; l++)
                {
                    var mipMapNode = new BinInterpNode
                    {
                        Header = $"0x{bin.Position:X4} MipMap #{l}",
                        Name = "_" + (bin.Position)

                    };
                    subnodes.Add(mipMapNode);

                    StorageTypes storageType = (StorageTypes)bin.ReadInt32();
                    mipMapNode.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{bin.Position - 4:X4} Storage Type: {storageType}",
                        Name = "_" + (bin.Position - 4)

                    });

                    var uncompressedSize = bin.ReadInt32();
                    mipMapNode.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{bin.Position - 4:X4} Uncompressed Size: {uncompressedSize}",
                        Name = "_" + (bin.Position - 4)

                    });

                    var compressedSize = bin.ReadInt32();
                    mipMapNode.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{bin.Position - 4:X4} Compressed Size: {compressedSize}",
                        Name = "_" + (bin.Position - 4)

                    });

                    var dataOffset = bin.ReadInt32();
                    mipMapNode.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{bin.Position - 4:X4} Data Offset: 0x{dataOffset:X8}",
                        Name = "_" + (bin.Position - 4)

                    });

                    switch (storageType)
                    {
                        case StorageTypes.pccUnc:
                            bin.Skip(uncompressedSize);
                            break;
                        case StorageTypes.pccLZO:
                        case StorageTypes.pccZlib:
                            bin.Skip(compressedSize);
                            break;
                    }

                    var mipWidth = bin.ReadInt32();
                    mipMapNode.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{bin.Position - 4:X4} Mip Width: {mipWidth}",
                        Name = "_" + (bin.Position - 4),
                        Tag = NodeType.StructLeafInt
                    });

                    var mipHeight = bin.ReadInt32();
                    mipMapNode.Items.Add(new BinInterpNode
                    {
                        Header = $"0x{bin.Position - 4:X4} Mip Height: {mipHeight}",
                        Name = "_" + (bin.Position - 4),
                        Tag = NodeType.StructLeafInt
                    });
                }

                if (Pcc.Game != MEGame.UDK)
                {
                    bin.Skip(4);
                }
                if (CurrentLoadedExport.FileRef.Game != MEGame.ME1)
                {
                    subnodes.Add(MakeGuidNode(bin, "Texture GUID"));
                }

                if (Pcc.Game == MEGame.UDK)
                {
                    bin.Skip(8 * 4);
                }

                if (Pcc.Game >= MEGame.ME3 && CurrentLoadedExport.ClassName == "LightMapTexture2D")
                {
                    if (Pcc.Game == MEGame.ME3)
                    {
                        bin.Skip(4);
                    }
                    subnodes.Add(new BinInterpNode(bin.Position, $"LightMapFlags: {(ELightMapFlags)bin.ReadInt32()}"));
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        enum ELightMapFlags
        {
            LMF_None,
            LMF_Streamed,
            LMF_SimpleLightmap
        }

        private List<ITreeItem> StartTextureMovieScan(byte[] data, ref int binarystart)
        {
            /*
             *  
             *  count +4
             *      stream length in TFC +4
             *      stream length in TFC +4 (repeat)
             *      stream offset in TFC +4
             *  
             */
            var subnodes = new List<ITreeItem>();
            try
            {
                int pos = binarystart;
                int unk1 = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"{(pos - binarystart):X4} Unknown: {unk1}",
                    Name = "_" + pos,

                });
                pos += 4;
                int length = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"{(pos - binarystart):X4} bik length: {length} (0x{length:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                length = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"{(pos - binarystart):X4} bik length: {length} (0x{length:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                int offset = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinInterpNode
                {
                    Header = $"{(pos - binarystart):X4} bik offset in file: {offset} (0x{offset:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                if (pos < data.Length && CurrentLoadedExport.GetProperty<NameProperty>("Filename") == null)
                {
                    subnodes.Add(new BinInterpNode
                    {
                        Header = $"{(pos - binarystart):X4} The rest of the binary is the bik.",
                        Name = "_" + pos,

                        Tag = NodeType.Unknown
                    });
                    subnodes.Add(new BinInterpNode
                    {
                        Header = "The stream offset to this data will be automatically updated when this file is saved.",
                        Tag = NodeType.Unknown
                    });
                }
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
        }

        private List<ITreeItem> StartStateScan(byte[] data, ref int binarystart)
        {
            /*
             * Has UnrealScript Functions contained within, however 
             * the exact format of the data has yet to be determined.
             * Probably better in Script Editor
             */
            var subnodes = new List<ITreeItem>();

            try
            {


                /*int length = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{(pos - binarystart):X4} bik length: {length} (0x{length:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                length = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{(pos - binarystart):X4} bik length: {length} (0x{length:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                int offset = BitConverter.ToInt32(data, pos);
                subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                {
                    Header = $"{(pos - binarystart):X4} bik offset in file: {offset} (0x{offset:X})",
                    Name = "_" + pos,

                    Tag = NodeType.StructLeafInt
                });
                pos += 4;
                if (pos < data.Length && CurrentLoadedExport.GetProperty<NameProperty>("Filename") == null)
                {
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = $"{(pos - binarystart):X4} The rest of the binary is the bik.",
                        Name = "_" + pos,

                        Tag = NodeType.Unknown
                    });
                    subnodes.Add(new BinaryInterpreterWPFTreeViewItem
                    {
                        Header = "The stream offset to this data will be automatically updated when this file is saved.",
                        Tag = NodeType.Unknown
                    });
                }
                */
            }
            catch (Exception ex)
            {
                subnodes.Add(new BinInterpNode() { Header = $"Error reading binary data: {ex}" });
            }
            return subnodes;
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
                                    nodeText += $"{val.ToString().PadRight(14, ' ')}{CurrentLoadedExport.FileRef.GetNameEntry(val)}";
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
                    node.Name = "_" + binarypos;
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